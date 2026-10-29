// Runs the held-out suite against one built binary and scores the result. The suite is decrypted only into a fresh 0700 directory under
// the operator's home (never the temp directory: the agent sandbox hides the home behind a tmpfs but promises nothing about /tmp), and
// removed afterwards even when the runner throws. The runner itself is trusted code on the HOST; it reaches the cell's binary only
// through the scoring box it is handed. Nothing here reads the environment: the key is a file, and the runner gets a fixed one.
import { randomBytes } from 'node:crypto';
import { chmodSync, existsSync, lstatSync, mkdirSync, readdirSync, realpathSync, rmSync, writeFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { removeUnder } from '../../policy-ab-quick/lib/safe-rm.mjs';
import { refuseInsideWorktree } from './git-guard.mjs';
import { unpackTo } from './heldout-archive.mjs';
import { defaultKeyPath, readKey, unseal } from './heldout-crypto.mjs';
import { readFileOrNull } from './inputs.mjs';
import { DEFAULT_LIMITS } from './limits.mjs';
import { fail, ok } from './result.mjs';
import { runBounded } from './run.mjs';

export const PYTHON = '/usr/bin/python3';
export const SUITE_ENC = fileURLToPath(new URL('../heldout/suite.enc', import.meta.url));
export const defaultWorkRoot = (home = os.homedir()) => path.join(home, 'rsd', 'minigit');
/** Where the clear held-out sources live while they are authored (outside every repository). */
export const heldoutSrcDir = (home = os.homedir()) => path.join(defaultWorkRoot(home), 'heldout-src');
const RUNNER_ENV = Object.freeze({ PATH: '/usr/bin:/bin', LANG: 'C.UTF-8' });
const DAY_MS = 24 * 3600 * 1000;

const inside = (child, root) => {
  const rel = path.relative(root, child);
  return rel !== '' && !rel.startsWith('..') && !path.isAbsolute(rel);
};
const sameOrInside = (child, root) => child === root || inside(child, root);

/** realpath of the longest existing prefix, then the rest appended: a link anywhere on the way is resolved before the check. */
export function realpathDeep(p) {
  const rest = [];
  let cur = path.resolve(p);
  while (!existsSync(cur) && path.dirname(cur) !== cur) {
    rest.unshift(path.basename(cur));
    cur = path.dirname(cur);
  }
  return path.join(realpathSync(cur), ...rest);
}

/** ok(resolved root) when it is strictly under the home and not under the temp directory; otherwise a fail saying which. */
export function assertWorkRoot(workRoot) {
  if (typeof workRoot !== 'string' || !path.isAbsolute(workRoot)) return fail(`work root ${JSON.stringify(workRoot)} must be an absolute path`);
  const real = realpathDeep(workRoot);
  const home = realpathDeep(os.homedir());
  const tmp = realpathDeep(os.tmpdir());
  if (sameOrInside(real, tmp) || sameOrInside(path.resolve(workRoot), path.resolve(os.tmpdir()))) {
    return fail(`work root ${workRoot} is under the temp directory ${os.tmpdir()}: the clear suite is never decrypted there`);
  }
  if (!inside(real, home)) return fail(`work root ${workRoot} must be a folder under the home ${os.homedir()}, not the home itself nor outside it`);
  const clean = refuseInsideWorktree(path.resolve(workRoot), 'work root');
  return clean.ok ? ok(path.resolve(workRoot)) : clean;
}

const scoreTmpOf = root => path.join(root, 'score-tmp');

/**
 * Decrypt `suiteEnc` into `<workRoot>/score-tmp/<rid>-<rand>/` (0700), run `fn(dir)`, remove the directory in `finally`.
 * Resolves ok(fn's value), or a fail for a refused root, a missing/bad key, a missing suite or a failed authentication.
 */
export async function withClearSuite({ suiteEnc = SUITE_ENC, keyFile = defaultKeyPath(), workRoot = defaultWorkRoot(), rid = 'run' }, fn) {
  const root = assertWorkRoot(workRoot);
  if (!root.ok) return root;
  const base = scoreTmpOf(root.value);
  mkdirSync(base, { recursive: true, mode: 0o700 });
  const key = readKey(keyFile);
  if (!key.ok) return key;
  const sealed = readFileOrNull(suiteEnc);
  if (sealed === null) return fail(`the sealed suite ${suiteEnc} does not exist`);
  const plain = unseal(sealed, key.value);
  if (!plain.ok) return plain;
  const dir = path.join(base, `${rid}-${randomBytes(4).toString('hex')}`);
  try {
    const unpacked = unpackTo(plain.value, dir);
    if (!unpacked.ok) return unpacked;
    return ok(await fn(dir));
  } finally {
    removeUnder(dir, base);
  }
}

/** Remove leftover clear directories (a crash skipped the `finally`) older than `olderThanMs`. A failure to remove one is reported, not thrown. */
export function sweepLeftovers({ workRoot = defaultWorkRoot(), olderThanMs = DAY_MS, nowMs = Date.now() } = {}) {
  const out = { removed: [], kept: [], failed: [] };
  // two places hold leftovers: score-tmp/<anything> (a crash skipped a `finally`) and testsToDelete-<anything> straight under the root
  const places = [[scoreTmpOf(workRoot), () => true], [path.resolve(workRoot), n => /^testsToDelete-/i.test(n)]];
  for (const [base, mine] of places) {
    if (!existsSync(base)) continue;
    for (const name of readdirSync(base).filter(mine).sort()) {
      const target = path.join(base, name);
      if (nowMs - lstatSync(target).mtimeMs <= olderThanMs) { out.kept.push(name); continue; }
      try {
        removeUnder(target, base);
        out.removed.push(name);
      } catch (e) {
        out.failed.push({ name: name, reason: e.message });
      }
    }
  }
  return out;
}

/** Run the clear suite's runner for one phase. ok(parsed result.json) | fail(reason). */
export async function runSuite({ suiteDir, binary, phase, box, workDir, outPath, timeoutS = DEFAULT_LIMITS.perInvocationS, wallS = 1800 }) {
  mkdirSync(workDir, { recursive: true });
  const args = ['-I', path.join(suiteDir, 'runner.py'), '--phase', phase, '--binary', binary, '--work', workDir,
    '--box', JSON.stringify(box), '--timeout', String(timeoutS), '--out', outPath];
  const r = await runBounded(PYTHON, args, { cwd: workDir, env: RUNNER_ENV, timeoutMs: wallS * 1000 });
  if (!r.ok) return r;
  // what the runner printed can quote cell bytes or case source: it goes to a private file, never into a reason, a trace or a score
  const stderrFile = `${outPath}.stderr`;
  writeFileSync(stderrFile, r.value.stderr, { mode: 0o600 });
  chmodSync(stderrFile, 0o600);
  if (r.value.timedOut) return fail(`the runner did not finish within ${wallS} s (its stderr is kept in ${stderrFile})`);
  if (r.value.code !== 0) return fail(`the runner exited ${r.value.code} (its stderr is kept in ${stderrFile})`);
  const text = readFileOrNull(outPath);
  if (text === null) return fail(`the runner wrote no result at ${outPath}`);
  chmodSync(outPath, 0o600);
  return parseResult(text.toString('utf8'));
}

/** ok(result) for a readable `result.json` of format 1 with a cases array; otherwise a fail saying what is wrong with it. */
export function parseResult(text) {
  let doc;
  try {
    doc = JSON.parse(text);
  } catch (e) {
    if (e instanceof SyntaxError) return fail('the result is not valid JSON');
    throw e;
  }
  return doc?.format === 1 && Array.isArray(doc.cases) ? ok(doc) : fail('the result is not of format 1 with a cases array');
}

const STATUSES = new Set(['pass', 'fail', 'blocked']);
const validCase = c => c !== null && typeof c === 'object' && typeof c.id === 'string' && STATUSES.has(c.status) && typeof c.group === 'string';
const tally = list => {
  const passed = list.filter(c => c.status === 'pass').length;
  return {
    total: list.length,
    passed,
    failed: list.filter(c => c.status === 'fail').length,
    blocked: list.filter(c => c.status === 'blocked').length,
    percent: Math.round((passed / list.length) * 1000) / 10, // passed / N: a failed AND a blocked case both count as not passed
  };
};

/** Distinct failed clauses (the SPEC lines a failed case asserts): a blocked case is never a second failure, two failures of one clause are one defect. */
const defectsOf = scored => {
  const clauses = {};
  for (const c of scored.filter(x => x.status === 'fail')) clauses[c.spec ?? c.id] = [...(clauses[c.spec ?? c.id] ?? []), c.id];
  return { count: Object.keys(clauses).length, clauses };
};
const slim = c => ({ id: c.id, group: c.group, status: c.status, ...(typeof c.spec === 'string' ? { spec: c.spec } : {}), ...(c.reason ? { reason: c.reason } : {}), ...(c.blockedBy ? { blockedBy: c.blockedBy } : {}) });

/**
 * Pure. Per group { total, passed, failed, blocked } over the SCORED cases; primary100 = nothing failed and nothing blocked (a blocked
 * case denies acceptance without being counted as a second failure). Secondary cases and latency sit beside it, never in the percentage.
 * A result that cannot be read, or holds no scored case, is { known: false, why } -- never a zero.
 */
export function heldoutScore(result) {
  if (result === null || typeof result !== 'object' || result.format !== 1 || !Array.isArray(result.cases)) return { known: false, why: 'the held-out result is unreadable' };
  if (!result.cases.every(validCase)) return { known: false, why: 'the held-out result has a malformed case' };
  const scored = result.cases.filter(c => c.scored === true);
  if (scored.length === 0) return { known: false, why: 'the held-out result holds no scored case' };
  const groups = Object.fromEntries([...new Set(scored.map(c => c.group))].sort().map(g => [g, tally(scored.filter(c => c.group === g))]));
  const totals = tally(scored);
  return {
    known: true, phase: result.phase, primary100: totals.failed === 0 && totals.blocked === 0, groups, totals,
    defects: defectsOf(scored), cases: scored.map(slim), secondary: result.cases.filter(c => c.scored !== true).map(slim), latency: result.latency ?? { known: false, why: 'the runner reported no latency' },
  };
}

/**
 * The case repositories hold case text (contents, messages): they are removed once the result is written. rmSync does not follow a link,
 * so a link the cell planted is unlinked, not followed. A directory that cannot be removed is reported, not hidden.
 */
export function clearCases(casesDir) {
  const out = { removed: 0, failed: [] };
  if (!existsSync(casesDir)) return out;
  for (const name of readdirSync(casesDir)) {
    try {
      rmSync(path.join(casesDir, name), { recursive: true, force: true });
      out.removed += 1;
    } catch (e) {
      out.failed.push({ name, reason: e.code ?? e.message });
    }
  }
  return out;
}

/**
 * The scoring pipeline's `deps.heldout`: decrypt, run, score, clean up. Any trouble before a result exists is `{ known: false, why }`
 * (the verdict then reads unknown, never a pass and never a silent false).
 */
export function makeHeldoutDep({ suiteEnc = SUITE_ENC, keyFile = defaultKeyPath(), workRoot = defaultWorkRoot(), timeoutS = DEFAULT_LIMITS.perInvocationS, wallS = 1800, clearDir } = {}) {
  return async function heldout({ binary, phase, box, casesDir, scoreDir, timeoutS: planS = timeoutS }) {
    const outPath = path.join(scoreDir, `heldout-${phase}.json`);
    mkdirSync(scoreDir, { recursive: true });
    const run = suiteDir => runSuite({ suiteDir, binary, phase, box, workDir: casesDir, outPath, timeoutS: planS, wallS });
    const finish = score => ({ ...score, casesCleanup: clearCases(casesDir) });
    // `clearDir` is the authoring mode (calibrate --src): the clear sources are used where they are, nothing is decrypted
    const ran = clearDir ? ok(await run(clearDir)) : await withClearSuite({ suiteEnc, keyFile, workRoot, rid: phase }, run);
    if (!ran.ok) return finish({ known: false, why: ran.reason });
    if (!ran.value.ok) return finish({ known: false, why: ran.value.reason });
    return finish({ ...heldoutScore(ran.value.value), resultPath: outPath });
  };
}
