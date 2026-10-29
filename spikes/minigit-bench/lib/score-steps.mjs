// The setup and build steps of the scoring pipeline, and the cleanup. Each step takes the shared context
// { plan, deps, runId, startedMs } and returns its record; expected failures are values.
import path from 'node:path';
import { scoreBoxArgv } from './bwrap.mjs';
import { verifyFiles } from './inputs.mjs';
import { fail, ok } from './result.mjs';
import { excludedDirs } from './score-deps.mjs';

export const MARKER_SUFFIX = '.bench-score-owned';
const FIXED_NAMES = ['copy', 'home', 'tmp', 'cases', 'cache-nuget', 'cache-npm'];
const isOwnedName = n => FIXED_NAMES.includes(n) || n.startsWith('run-');

/** A box that could not start, or a spawn that failed: the host cannot score, which is not the same as the session scoring badly. */
export class ScorerEnvironmentError extends Error {}

/** Ownership markers live in `<score>/.owned/`, a directory the box never binds, so no tree can forge, edit or delete one. */
const markerPath = (plan, name) => path.join(plan.scoreDir, '.owned', `${name}${MARKER_SUFFIX}`);

/**
 * The scoring box over `dir` (bound read-write), with or without a working directory. The case repos are bound only for the held-out
 * step (`cases`), and `roFiles` are laid read-only over the copy (the harness script the tree must not rewrite).
 */
export function boxFor(ctx, dir, { cwd = true, cases = false, roFiles = [] } = {}) {
  const { plan, deps } = ctx;
  return scoreBoxArgv({ copyDir: dir, casesDir: cases ? plan.casesDir : undefined, home: plan.homeDir, tmp: plan.tmpDir, ...deps.box,
    nugetCache: plan.nugetCopy, npmCache: plan.npmCopy, roFiles, cwd: cwd ? dir : undefined });
}

/** Run argv inside the box over `dir`; the process tree is killed at `timeoutS`. A process that cannot be started at all throws. */
export async function inBox(ctx, dir, argv, timeoutS, boxOptions = {}) {
  const [exe, ...prefix] = boxFor(ctx, dir, boxOptions);
  const r = await ctx.deps.run(exe, [...prefix, ...argv], { timeoutMs: Math.max(1, Math.round(timeoutS * 1000)) });
  if (!r.ok) throw new ScorerEnvironmentError(r.reason);
  return r.value;
}

/** Marks `name` as created by this run, BEFORE the directory exists (a crash in between leaves a marker, never an unmarked directory). */
export function markOwned(ctx, name) {
  const { plan, deps } = ctx;
  deps.fs.mkdir(path.join(plan.scoreDir, '.owned'));
  deps.fs.writeFile(markerPath(plan, name), JSON.stringify({ sessionId: plan.sessionId, runId: ctx.runId, startedMs: ctx.startedMs }));
  deps.fs.mkdir(path.join(plan.scoreDir, name));
}

function readMarker(fs, plan, name) {
  const bytes = fs.readFile(markerPath(plan, name));
  if (bytes === null) return null;
  try {
    return JSON.parse(bytes.toString('utf8'));
  } catch (e) {
    if (e instanceof SyntaxError) return null; // a marker nobody can read is no marker: its directory stays untouched
    throw e;
  }
}

/** Step 0. Removes THIS session's marked leftovers older than the wall limit; a directory without a marker is only reported. */
export function stepSweep(ctx) {
  const { plan, deps } = ctx;
  const dirs = deps.fs.listDir(plan.scoreDir).filter(isOwnedName);
  const marked = deps.fs.listDir(path.join(plan.scoreDir, '.owned')).filter(n => n.endsWith(MARKER_SUFFIX)).map(n => n.slice(0, -MARKER_SUFFIX.length));
  const removed = [];
  const unmarked = [];
  const young = [];
  for (const name of [...new Set([...dirs, ...marked])].sort()) {
    const marker = readMarker(deps.fs, plan, name);
    if (marker === null) { if (dirs.includes(name)) unmarked.push(name); continue; }
    if (ctx.startedMs - marker.startedMs <= plan.limits.wallS * 1000) { young.push(name); continue; }
    if (dirs.includes(name)) deps.fs.removeTree(path.join(plan.scoreDir, name), plan.scoreDir);
    deps.fs.removeFile(markerPath(plan, name));
    removed.push(name);
  }
  return { removed, unmarked, young };
}

const refusal = (reason, kind) => fail(reason, { kind });

/** Step 1. `sourceDir` -> `<score>/copy`, the seeded caches -> per-scoring copies, and the scratch HOME, TMPDIR and cases directories. */
export function stepCopy(ctx, sweep) {
  const { plan, deps } = ctx;
  if (deps.fs.stat(plan.sourceDir)?.isDir !== true) return refusal(`source directory ${plan.sourceDir} does not exist`, 'configuration');
  if (deps.fs.stat(plan.copyDir) !== null) {
    const why = sweep.young.includes('copy') ? 'another scoring of this session is in progress (a younger marked copy)' : 'it carries no scorer marker, so it is not ours to remove';
    return refusal(`${plan.copyDir} already exists: ${why}`, 'environment');
  }
  markOwned(ctx, 'copy');
  deps.fs.copyTree(plan.sourceDir, plan.copyDir, excludedDirs(plan.stack));
  for (const [name, source] of [['cache-nuget', plan.nugetCache], ['cache-npm', plan.npmCache]]) {
    if (!source) continue;
    markOwned(ctx, name);
    deps.fs.copyTree(source, path.join(plan.scoreDir, name), []);
  }
  for (const [name, dir] of [['home', plan.homeDir], ['tmp', plan.tmpDir], ['cases', plan.casesDir]]) if (deps.fs.stat(dir) === null) markOwned(ctx, name);
  return ok({ copied: plan.copyDir });
}

/** Step 1b. Can the box start at all, with this toolchain and these binds? If not, the host cannot score: an environment failure, never a score. */
export async function stepPreflight(ctx) {
  const r = await inBox(ctx, ctx.plan.copyDir, ['true'], 60);
  if (r.timedOut || r.code !== 0) return refusal(`the scoring box cannot start (exit ${r.code}${r.timedOut ? ', timed out' : ''}): ${tail(r.stderr || r.stdout, 600).trim()}`, 'environment');
  return ok({ started: true });
}

/** The copy's own SPEC and test files and the hashes.txt entry each must equal (the root SPECs are byte copies of upstream/'s). */
const PINNED_AS = Object.freeze({
  'SPEC-v1.txt': 'upstream/SPEC-v1.txt', 'SPEC-v2.txt': 'upstream/SPEC-v2.txt', 'test-v1.sh': 'test-v1.sh', 'test-v2.sh': 'test-v2.sh' });

/**
 * Step 2. Which of the input files the copy carries differ from the pinned ones, and which it does not carry. Only a regular file of
 * a sane size is hashed: a link, device, pipe, directory or huge file counts as tampered and is never opened.
 */
export function stepInputs(ctx) {
  const { plan, deps } = ctx;
  const byPath = new Map(deps.inputs.entries.map(e => [e.path, e.sha256]));
  const entries = Object.entries(PINNED_AS).map(([copyRel, pinned]) => ({ path: copyRel, sha256: byPath.get(pinned) }));
  const rows = verifyFiles(plan.copyDir, entries, deps.fs.readInput);
  return {
    inputsTampered: rows.filter(r => !r.ok && r.actual !== null).map(r => r.path),
    inputsAbsent: rows.filter(r => r.actual === null).map(r => r.path),
  };
}

const tail = (text, n = 2000) => (text.length > n ? text.slice(-n) : text);
const isExecutable = st => st !== null && st.isFile && (st.mode & 0o111) !== 0;

/** Runs the build commands one after another inside the box, each within what is left of the build limit. */
async function runBuildCommands(ctx, commands, started) {
  const { plan, deps } = ctx;
  const log = [];
  for (const argv of commands) {
    const leftS = plan.limits.buildS - (deps.now() - started) / 1000;
    if (leftS <= 0) return { exitCode: 124, timedOut: true, log: [...log, `${argv[0]}: the build limit was reached before it started`] };
    const r = await inBox(ctx, plan.copyDir, argv, leftS);
    log.push(`$ ${argv.join(' ')}\n${r.stdout}${r.stderr}`);
    if (r.timedOut) return { exitCode: 124, timedOut: true, log };
    // a failed chmod is not a build failure: a missing or non-executable minigit is judged below (the box itself was proven by the preflight)
    if (r.code !== 0 && argv[0] !== 'chmod') return { exitCode: r.code ?? 1, timedOut: false, log };
  }
  return { exitCode: 0, timedOut: false, log };
}

/** Step 3. `make -s` if a Makefile, then `bash build.sh` if present, then `chmod +x minigit`, all inside the box, within the build limit. */
export async function stepBuild(ctx) {
  const { plan, deps } = ctx;
  const has = name => deps.fs.stat(path.join(plan.copyDir, name)) !== null;
  const commands = [
    ...(has('Makefile') || has('makefile') ? [['make', '-s']] : []),
    ...(has('build.sh') ? [['bash', 'build.sh']] : []),
    ['chmod', '+x', 'minigit'],
  ];
  const started = deps.now();
  const { exitCode, timedOut, log } = await runBuildCommands(ctx, commands, started);
  const executable = isExecutable(deps.fs.stat(path.join(plan.copyDir, 'minigit')));
  return { ok: exitCode === 0 && !timedOut && executable, exitCode, timedOut, executable, ms: deps.now() - started, logTail: tail(log.join('\n')) };
}

/** Last step, always. Removes the copies and scratch directories THIS run created (matched by its runId); reports what it could not. */
export function stepCleanup(ctx) {
  const { plan, deps } = ctx;
  const removed = [];
  const failed = [];
  for (const name of deps.fs.listDir(plan.scoreDir).filter(n => isOwnedName(n) && n !== 'cases').sort()) {
    if (readMarker(deps.fs, plan, name)?.runId !== ctx.runId) continue;
    try {
      deps.fs.removeTree(path.join(plan.scoreDir, name), plan.scoreDir);
      deps.fs.removeFile(markerPath(plan, name));
      removed.push(name);
    } catch (e) {
      failed.push({ name, reason: e.message });
    }
  }
  return { removed, failed };
}
