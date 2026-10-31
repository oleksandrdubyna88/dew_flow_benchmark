// The Node side of the judge (plan section 4): builds the scoring-box argvs (v1's `scoreBoxArgv` with seam W3's locale), hands them and the cases to `runner/run_samples.py --heldout`, and maps its JSON to v1's
// score shape (`bare-record.mjs` `qualityPart`) plus the pilot's `explore` section. The judge runs on the host; the cases are piped from there and never enter the box.
// Failures are values. The one thing this module does not do is run anything inside a session: it scores a finished tree.
import { cpSync, existsSync, mkdirSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { makeWritable } from '../../minigit-bench/lib/cache-snapshot.mjs';
import { scoreBoxArgv, scoreEnv } from '../../minigit-bench/lib/bwrap.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { runBounded } from '../../minigit-bench/lib/run.mjs';
import { excludedDirs, nodeFs } from '../../minigit-bench/lib/score-deps.mjs';
import { frozenChanges } from './workspace.mjs';
import { SPIKE } from './paths.mjs';

export const JUDGE = path.join(SPIKE, 'runner', 'run_samples.py');
/** The names the judge replaces, per case, in the box argvs (exact element match). */
export const PLACE = Object.freeze({ tree: '@@TREE@@', home: '@@HOME@@', tmp: '@@TMP@@' });
const DEFAULT_SLACK_MS = 60_000;

/**
 * `argv` (a scoring box argv) with `--setenv` entries replaced or added: an existing key keeps its place, a new one goes after the last. Pure.
 */
export function withEnv(argv, overrides) {
  const last = argv.lastIndexOf('--setenv');
  if (last < 0) throw new Error('withEnv: the argv has no --setenv entry (not a scoring box argv)');
  const at = argv.map((a, i) => isValueOf(argv, overrides, i));
  const replaced = new Set(argv.filter((a, i) => at[i + 1]));
  const out = argv.map((a, i) => (at[i] ? overrides[argv[i - 1]] : a));
  out.splice(last + 3, 0, ...Object.entries(overrides).filter(([k]) => !replaced.has(k)).flatMap(([k, v]) => ['--setenv', k, v]));
  return Object.freeze(out);
}

/** Is `argv[i]` the VALUE of a `--setenv <key> <value>` triple whose key is one the overrides name? */
function isValueOf(argv, overrides, i) {
  return i > 1 && argv[i - 2] === '--setenv' && Object.hasOwn(overrides, argv[i - 1]);
}

/**
 * The two scoring box argvs the judge fills in per case: `buildBox` (the tree, a home and a tmp, and the cache copies, read-write) and `runBox` (the tree, a home and a tmp, no cache). Both carry
 * the locale (W3), the pilot's extra environment keys and `tsc` on PATH; neither binds the cases.
 */
export function boxArgvs({ toolchainDirs, tsDir, nugetCache, npmCache, locale, envExtra, operatorHome, exists }) {
  const common = { copyDir: PLACE.tree, home: PLACE.home, tmp: PLACE.tmp, toolchainDirs, cwd: PLACE.tree, locale, ...(operatorHome ? { operatorHome } : {}), ...(exists ? { exists } : {}) };
  const env = { ...envExtra, PATH: `${scoreEnv({ ...common, toolchainDirs }).PATH}:${tsDir}/node_modules/.bin` };
  return {
    buildBox: withEnv(scoreBoxArgv({ ...common, nugetCache, npmCache }), env),
    runBox: withEnv(scoreBoxArgv(common), env),
  };
}

/** The pristine copy of a session's workspace: the agent's tree without the build output of the stack, with every frozen file put back as it was (the changes are reported, not silently repaired). */
export function pristineCopy({ source, dest, stack, files }) {
  nodeFs.copyTree(source, dest, excludedDirs(stack));
  const changed = frozenChanges({ files, work: dest });
  for (const rel of changed.map(c => c.path)) {
    mkdirSync(path.dirname(path.join(dest, rel)), { recursive: true });
    writeFileSync(path.join(dest, rel), files[rel].bytes, { mode: files[rel].mode });
  }
  return changed;
}

/** The sample cases of the frozen workspace as a case directory (`<id>.in` / `<id>.out`). */
export function writeSamples({ files, dir }) {
  mkdirSync(dir, { recursive: true });
  for (const [rel, f] of Object.entries(files)) if (/^samples\/s\d\d\.(in|out)$/.test(rel)) writeFileSync(path.join(dir, path.basename(rel)), f.bytes);
}

/** The judge's whole-run deadline: the build, every case at its limit, and a margin. */
export const judgeDeadlineMs = ({ limits, cases }) => limits.buildTimeoutMs + cases * (limits.timeMs + 20_000) + DEFAULT_SLACK_MS;

/** How many cases the sets hold (their `.in` files). */
export const caseCount = sets => sets.reduce((n, s) => n + readdirSync(s.dir).filter(f => f.endsWith('.in')).length, 0);

/** Run the Python judge in held-out mode over a config. -> `ok(result)` | `fail`. `kind: 'harness'` when the judge reported a harness failure (a process left in a scope: the pilot stops). */
export async function runJudge({ python = '/usr/bin/python3', judge = JUDGE, config, configFile, timeoutMs, cpus = '0-13' }) {
  writeFileSync(configFile, `${JSON.stringify(config, null, 2)}\n`);
  const r = await runBounded('taskset', ['-c', cpus, 'nice', '-n', '19', python, '-I', judge, '--heldout', configFile], { timeoutMs });
  return r.ok ? judgeResult({ r: r.value, out: config.out, timeoutMs }) : r;
}

/** What a finished judge process means: its JSON, or the sentence saying why there is none. */
function judgeResult({ r, out, timeoutMs }) {
  if (r.timedOut) return fail(`the judge did not finish within ${Math.round(timeoutMs / 1000)} s and its process group was killed`, { kind: 'scorer-timeout' });
  if (!existsSync(out)) return fail(`the judge exited ${r.code} and wrote no result: ${r.stderr.slice(-300)}`, { kind: 'failed' });
  const result = JSON.parse(readFileSync(out, 'utf8'));
  return result.harnessFailure ? fail(`harness failure: ${result.harnessFailure}`, { kind: 'harness', result }) : ok(result);
}

const passed = cases => cases.filter(c => c.verdict === 'pass').length;
const bySet = (cases, name) => cases.filter(c => c.set === name);
const percent = (n, total) => (total === 0 ? 0 : Math.round((n / total) * 1000) / 10);

/** Verdict counts by class of a list of cases. */
export const verdictCounts = cases => Object.fromEntries(Object.entries(Object.groupBy(cases, c => c.verdict)).map(([k, v]) => [k, v.length]));

const visibleStep = (result, cases) => ({
  script: 'visible samples', passed: passed(cases), failed: cases.length - passed(cases), expectedTotal: cases.length, summaryCaptured: true, buildFailed: !result.build.ok, exitCode: 0, timedOut: false, signal: null,
});

function heldoutStep(result, cases, traps) {
  if (!result.build.ok) return { known: true, primary100: false, reason: 'the build failed: no case ran' };
  const failed = cases.length - passed(cases);
  return {
    known: true, primary100: failed === 0 && cases.length > 0, totals: { total: cases.length, passed: passed(cases), failed, blocked: 0, percent: percent(passed(cases), cases.length) },
    defects: { count: failed, clauses: [] }, latency: { known: false, why: 'the pilot records the wall of every case in the explore section; v1 has no latency runs here' },
    trapHits: cases.filter(c => traps.includes(c.id) && c.verdict !== 'pass').length,
  };
}

/** The judge's JSON -> v1's score shape (`build`, `visible`, `heldout`, `upstream`) plus `explore`. `traps` are the held-out ids that catch a stack's overflow trap (counted, never named in a record's public rendering). */
export function scoreShape({ result, traps = [], frozen = [] }) {
  const visible = bySet(result.cases, 'visible');
  const held = bySet(result.cases, 'heldout');
  return {
    build: { ok: result.build.ok }, visible: [visibleStep(result, visible)], heldout: heldoutStep(result, held, traps), upstream: [],
    explore: {
      limits: result.limits, build: result.build, frozenChanges: frozen, cases: result.cases, verdicts: { visible: verdictCounts(visible), heldout: verdictCounts(held) },
      passRate: { visible: percent(passed(visible), visible.length), heldout: percent(passed(held), held.length) },
      trapHits: held.filter(c => traps.includes(c.id) && c.verdict !== 'pass').length, trapCases: traps.length,
    },
  };
}

/** The accepted rule of CPC: a clean build, every visible and held-out case passed, the memory claim certified (`peakBytes` known on every case). */
const allPassed = v => v.failed === 0 && v.passed === v.expectedTotal;
const certified = score => score.explore.cases.every(c => typeof c.peakBytes === 'number');
export const acceptedOf = score => score.build.ok && score.visible.every(allPassed) && score.heldout.primary100 === true && certified(score);

/**
 * Score a tree end to end. `files` is the frozen workspace (`workspaceFiles`), `sets` the case directories besides the visible samples (`[{ name: 'heldout', dir }]`), `caches` the cache directories to copy for the
 * build. The work directory is created under `work` and removed after. -> `ok(score)` | `fail`.
 */
export async function scoreTree({ explore, stack, source, files, sets, limits, toolchains, caches, work, traps = [], python, judge, deadlineMs, operatorHome }) {
  const area = path.join(work, 'judge');
  removeArea(area);
  mkdirSync(area, { recursive: true });
  try {
    const frozen = pristineCopy({ source, dest: path.join(area, 'tree'), stack, files });
    const config = judgeConfig({ explore, files, sets, limits, toolchains, caches, area, operatorHome });
    const judged = await runJudge({ python, judge, config, configFile: path.join(area, 'config.json'), timeoutMs: deadlineMs ?? judgeDeadlineMs({ limits, cases: caseCount(config.sets) }) });
    return judged.ok ? ok(scoreShape({ result: judged.value, traps, frozen })) : judged;
  } finally {
    removeArea(area);
  }
}

/** Remove a scoring area. A seeded cache copy keeps its snapshot's read-only modes, so the tree is made writable first. */
export function removeArea(dir) {
  makeWritable(dir);
  rmSync(dir, { recursive: true, force: true });
}

/** Copy a cache directory (when there is one) into the scoring area, writable; an absent source is an empty cache. */
function copyCache(from, to) {
  mkdirSync(to, { recursive: true });
  if (from && existsSync(from)) cpSync(from, to, { recursive: true, verbatimSymlinks: true });
  makeWritable(to);
  return to;
}

/** The judge's config for a scoring area: the tree is in `area/tree`; the visible samples, the cache copies and the boxes are made here. */
export function judgeConfig({ explore, files, sets, limits, toolchains, caches, area, operatorHome }) {
  const nugetCache = copyCache(caches.nuget, path.join(area, 'cache-nuget'));
  const npmCache = copyCache(caches.npm, path.join(area, 'cache-npm'));
  const visibleDir = path.join(area, 'visible');
  writeSamples({ files, dir: visibleDir });
  const boxes = boxArgvs({ toolchainDirs: toolchains.dirs, tsDir: toolchains.tsDir, nugetCache, npmCache, locale: explore.locale, envExtra: explore.envExtra, operatorHome });
  return { work: path.join(area, 'run'), tree: path.join(area, 'tree'), out: path.join(area, 'result.json'), limits, placeholders: PLACE, buildBox: boxes.buildBox, runBox: boxes.runBox, sets: [{ name: 'visible', dir: visibleDir }, ...sets] };
}

