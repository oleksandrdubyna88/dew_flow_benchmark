// The setup and build steps of the scoring pipeline, and the cleanup. Each step takes the shared context
// { plan, deps, runId, startedMs } and returns its record; expected failures are values.
import path from 'node:path';
import { verifyFiles } from './inputs.mjs';
import { scoreBoxArgv } from './bwrap.mjs';
import { fail, ok } from './result.mjs';

export const MARKER = '.bench-score-owned';
const isOwnedName = n => ['copy', 'home', 'tmp', 'cases'].includes(n) || n.startsWith('run-');

/** The scoring box over `dir` (bound read-write), with or without a working directory. */
export function boxFor(ctx, dir, { cwd = true } = {}) {
  const { plan, deps } = ctx;
  return scoreBoxArgv({ copyDir: dir, casesDir: plan.casesDir, home: plan.homeDir, tmp: plan.tmpDir, ...deps.box, cwd: cwd ? dir : undefined });
}

/** Run argv inside the box over `dir`; the process tree is killed at `timeoutS`. */
export function inBox(ctx, dir, argv, timeoutS) {
  const [exe, ...prefix] = boxFor(ctx, dir);
  return ctx.deps.run(exe, [...prefix, ...argv], { timeoutMs: Math.max(1, Math.round(timeoutS * 1000)) });
}

export function writeMarker(ctx, dir) {
  const { fs } = ctx.deps;
  fs.mkdir(dir);
  fs.writeFile(path.join(dir, MARKER), JSON.stringify({ sessionId: ctx.plan.sessionId, runId: ctx.runId, startedMs: ctx.startedMs }));
}

function readMarker(fs, dir) {
  const bytes = fs.readFile(path.join(dir, MARKER));
  if (bytes === null) return null;
  try {
    return JSON.parse(bytes.toString('utf8'));
  } catch (e) {
    if (e instanceof SyntaxError) return null; // a marker nobody can read is no marker: the directory stays untouched
    throw e;
  }
}

/** Step 0. Removes THIS session's marked leftovers older than the wall limit; a directory without the marker is only reported. */
export function stepSweep(ctx) {
  const { plan, deps } = ctx;
  const removed = [];
  const unmarked = [];
  const young = [];
  for (const name of deps.fs.listDir(plan.scoreDir).filter(isOwnedName).sort()) {
    const dir = path.join(plan.scoreDir, name);
    const marker = readMarker(deps.fs, dir);
    if (marker === null) unmarked.push(name);
    else if (ctx.startedMs - marker.startedMs > plan.limits.wallS * 1000) { deps.fs.removeTree(dir, plan.scoreDir); removed.push(name); }
    else young.push(name);
  }
  return { removed, unmarked, young };
}

/** Step 1. `sourceDir` -> `<score>/copy` (marker first), plus the scratch HOME, TMPDIR and cases directories. */
export function stepCopy(ctx, sweep) {
  const { plan, deps } = ctx;
  if (deps.fs.stat(plan.sourceDir)?.isDir !== true) return fail(`source directory ${plan.sourceDir} does not exist`);
  if (deps.fs.stat(plan.copyDir) !== null) {
    const why = sweep.young.includes('copy') ? 'another scoring of this session is in progress (a younger marked copy)' : 'it carries no scorer marker, so it is not ours to remove';
    return fail(`${plan.copyDir} already exists: ${why}`);
  }
  writeMarker(ctx, plan.copyDir);
  deps.fs.copyTree(plan.sourceDir, plan.copyDir, plan.stack);
  for (const dir of [plan.homeDir, plan.tmpDir, plan.casesDir]) if (deps.fs.stat(dir) === null) writeMarker(ctx, dir);
  return ok({ copied: plan.copyDir });
}

/** The copy's own SPEC and test files and the hashes.txt entry each must equal (the root SPECs are byte copies of upstream/'s). */
const PINNED_AS = Object.freeze({
  'SPEC-v1.txt': 'upstream/SPEC-v1.txt', 'SPEC-v2.txt': 'upstream/SPEC-v2.txt', 'test-v1.sh': 'test-v1.sh', 'test-v2.sh': 'test-v2.sh' });

/** Step 2. Which of the input files the copy carries differ from the pinned ones, and which it does not carry. */
export function stepInputs(ctx) {
  const { plan, deps } = ctx;
  const byPath = new Map(deps.inputs.entries.map(e => [e.path, e.sha256]));
  const entries = Object.entries(PINNED_AS).map(([copyRel, pinned]) => ({ path: copyRel, sha256: byPath.get(pinned) }));
  const rows = verifyFiles(plan.copyDir, entries, deps.fs.readFile);
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
    if (!r.ok) return { exitCode: 127, timedOut: false, log: [...log, r.reason] };
    log.push(`$ ${argv.join(' ')}\n${r.value.stdout}${r.value.stderr}`);
    if (r.value.timedOut) return { exitCode: 124, timedOut: true, log };
    // a failed chmod is not a build failure: a missing or non-executable minigit is judged below
    if (r.value.code !== 0 && argv[0] !== 'chmod') return { exitCode: r.value.code ?? 1, timedOut: false, log };
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
    const dir = path.join(plan.scoreDir, name);
    if (readMarker(deps.fs, dir)?.runId !== ctx.runId) continue;
    try {
      deps.fs.removeTree(dir, plan.scoreDir);
      removed.push(name);
    } catch (e) {
      failed.push({ name, reason: e.message });
    }
  }
  return { removed, failed };
}
