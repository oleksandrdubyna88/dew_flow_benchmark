// The pass driver of a quick-run session's scoring (F1.S2, section 3.1): the session is built ONCE at the atomic commit point, then run ten times, five on each CPU profile, each pass tried at most three times, and the
// attempts become one verdict by the rule of `verdict.mjs`. It reads and writes `s/<rid>/score/`: `build.json`, `tree/`, `passes/`, `events/`, `scorer.lock`, `verdict.json` (derived), `score.json`.
//
//   - Every attempt, event and the completion record is a file written once (`wx`); nothing is ever overwritten or removed but the built copy and the case folder, which go when the scoring leaves `open`.
//   - A resume re-reads the files and runs only what is missing; `verdict.json` is rebuilt from them, never read.
//   - The driver proves the SERIES lock is held (it never takes it) and takes the session's own scorer lock.
//   - A halt before an attempt (a launching session, an affinity that was not applied, a build record that does not match its tree, a ceiling) is an event file and an answer of kind `environment`: nothing is scored, the
//     loop halts resumably (exit 3).
//   - A harness failure is never a verdict: an environment attempt counts toward the pass's three and nothing else.
import { existsSync, mkdirSync, readFileSync, rmSync } from 'node:fs';
import path from 'node:path';
import { ok } from '../../minigit-bench/lib/result.mjs';
import { affinityProblem } from './affinity.mjs';
import { reapOrphanChild } from './child-record.mjs';
import { systemClock } from './clock.mjs';
import { commitBuild, committedTree, discardUncommitted, readBuildRecord } from './build-commit.mjs';
import { runnerRecordPinNow } from './harness-pin.mjs';
import { seriesLockProblem, takeScorerLock } from './pass-locks.mjs';
import { publishOnce, removeStaleTemps } from './publish-once.mjs';
import { finishRollup } from './score-quick.mjs';
import { removeArea } from './score.mjs';
import { BUILD_ENVIRONMENT_EVENT, readAttempts, readEvents, rebuildVerdict, writeAttempt, writeEvent } from './score-files.mjs';
import { VOID_UNSCORED } from './series.mjs';
import { PASS_COUNT, passState, profileOfPass, rollupOf } from './verdict.mjs';

const BUILD_TRIES = 3;
const environment = (reason, extra = {}) => ({ kind: 'environment', reason, ...extra });
const treeOf = c => path.join(c.work, 'tree');
const casesOf = c => path.join(c.work, 'cases');

/** Why there is no valid growth record (unless this is the first measurement), or null. */
function growthRefusal(a) {
  if (a.bootstrap === true) return null;
  const growth = a.growth.loaded();
  return growth.ok ? null : growth.reason;
}

/** What stops a scoring before anything is written: the series lock this process does not hold, or no valid growth record. -> an `environment` answer | null. */
function startRefusal(a) {
  const problem = seriesLockProblem({ seriesDir: a.seriesDir, ...(a.pid ? { pid: a.pid } : {}) }) ?? growthRefusal(a);
  return problem ? environment(problem) : null;
}

const lockOptions = a => ({ work: a.work, ...(a.isAlive ? { isAlive: a.isAlive } : {}), ...(a.now ? { now: a.now } : {}) });

/**
 * Score a session: ten passes under two CPU profiles from one committed build. `a` = `{ work, seriesDir, prepare, cases, build, runPass, affinity, seriesState, growth, limits, spec, traps, pinnedRunner, bootstrap, afterAttempt,
 * isAlive, now, signal, say }` (the seams are documented in `test/quick-passes-harness.mjs`). -> `{ kind: 'scored', score, verdict }` | `{ kind: 'void', class, reason, verdict }` | `{ kind: 'environment', reason }` | `{ kind: 'stopped', reason }`.
 */
export async function scorePasses(a) {
  const refused = startRefusal(a);
  if (refused) return refused;
  mkdirSync(a.work, { recursive: true });
  const lock = takeScorerLock(lockOptions(a));
  if (!lock.ok) return environment(lock.reason);
  try {
    removeStaleTemps(a.work); // the scorer lock is ours: a temporary file here is a dead scorer's
    return await underLocks({ now: systemClock, ...a, pinnedRunner: a.pinnedRunner ?? runnerRecordPinNow(), ids: null }, lock.value.takeover);
  } finally {
    lock.value.release();
  }
}

// ---- events ---------------------------------------------------------------------------------------------------------------------------------------

/** Write an event once and refresh the snapshot (when the case ids, which the snapshot is read against, are known). -> the event's file name. */
function noteEvent(c, kind, detail = {}) {
  const file = writeEvent({ work: c.work, now: c.now, kind, detail });
  if (c.ids !== null) rebuildVerdict(c.work, { expectedIds: c.ids, pinnedRunner: c.pinnedRunner });
  return file;
}

/** A halt: an event file, the snapshot, and the answer that makes the loop halt resumably. */
const halted = (c, kind, reason, detail = {}) => environment(reason, { event: noteEvent(c, kind, { reason, ...detail }) });

// ---- one session ----------------------------------------------------------------------------------------------------------------------------------

async function underLocks(a, takeover) {
  const caseFolder = a.cases({ dir: casesOf(a) });
  if (!caseFolder.ok) return environment(caseFolder.reason);
  const c = { ...a, ids: [...caseFolder.value.ids].sort() };
  if (takeover) noteEvent(c, 'takeover', { reason: `took over the scorer lock of pid ${takeover.pid}, which is gone`, deadPid: takeover.pid });
  const orphan = await orphanProblem(c);
  const busy = orphan ?? launchingProblem(c);
  return busy ?? sessionSteps(c);
}

/** A pass child a dead scorer left running is killed and waited for before anything starts; its folder (and so any answer it wrote) goes with it. -> a halt, or null. */
async function orphanProblem(c) {
  const reaped = await (c.reapChild ?? reapOrphanChild)({ work: c.work });
  if (reaped.found) noteEvent(c, 'orphan-child', { reason: `a pass child of a dead scorer ${reaped.found.killed ? 'was running and was killed' : 'is not running'}`, pid: reaped.found.pid, killed: reaped.found.killed });
  if (reaped.problem) return halted(c, 'orphan-child-alive', reaped.problem);
  rmSync(path.join(c.work, 'child'), { recursive: true, force: true });
  return null;
}

/** A halt while another session of the series is still launching: a pass must not disturb it, and it must not disturb the pass. */
function launchingProblem(c) {
  const { launching } = c.seriesState();
  return launching.length === 0 ? null : halted(c, 'halt', `session ${launching.join(', ')} of the series is launching: a pass does not start while a session runs`, { launching });
}

async function sessionSteps(c) {
  const built = await buildStep(c);
  if (built.answer) return built.answer;
  const stopped = built.needsPasses ? await runPasses(c) : null;
  return stopped ?? finish(c);
}

const readAll = c => readAttempts({ work: c.work, expectedIds: c.ids, pinnedRunner: c.pinnedRunner });
const anyOpen = c => readAll(c).some(p => passState(p) === 'open');
/** The completion record: `ok(null)` when there is none yet, `ok(record)`, or a `fail('build-record-invalid: ...')` for a file that is not a whole record. */
const recordedBuild = c => (existsSync(path.join(c.work, 'build.json')) ? readBuildRecord(c.work) : ok(null));
const buildTriesUsed = c => readEvents({ work: c.work }).filter(e => e.kind === BUILD_ENVIRONMENT_EVENT).length;

// ---- the build ------------------------------------------------------------------------------------------------------------------------------------

/** The build's state and what it leaves to do. -> `{ needsPasses }` or `{ answer }` (a halt). */
async function buildStep(c) {
  const recorded = recordedBuild(c);
  if (!recorded.ok) return { answer: halted(c, 'build-record-invalid', recorded.reason) };
  return recorded.value === null ? commitStep(c) : afterRecord(c, recorded.value);
}

/** The runner moved since the build was committed: the sentence, or null. Attempts stay judged against the pin stamped at the commit; a scoring that still has passes to run does not go on under another runner. */
function runnerMoved(record, today) {
  const stamp = record.runnerPin;
  if (!stamp || (stamp.format === today.format && stamp.version === today.version)) return null;
  return `runner-moved: the build was committed under runner format ${stamp.format} version ${stamp.version} and the runner is now format ${today.format} version ${today.version}: the stored attempts are not judged again and no pass runs under another runner`;
}

/** A recorded build: a failed one or a scoring with no pass open is finished from its records (whatever the runner is now); otherwise the runner must be the one of the commit and the tree the one recorded. */
function afterRecord(c, record) {
  if (record.ok === false || !anyOpen(c)) return { needsPasses: false };
  const moved = runnerMoved(record, c.pinnedRunner);
  return moved ? { answer: halted(c, 'runner-moved', moved) } : treeStep(c);
}

/** A recorded build whose passes are not all done: the tree on disk must be the tree the record names. */
function treeStep(c) {
  const tree = committedTree(c.work);
  return tree.ok ? { needsPasses: true } : { answer: halted(c, 'build-record-mismatch', tree.reason) };
}

/** Make the build (no record yet): drop what an earlier build left uncommitted, then build, up to the three tries of a build that never began. */
async function commitStep(c) {
  const dropped = discardUncommitted(c.work);
  if (dropped.length > 0) noteEvent(c, 'discard', { reason: 'uncommitted build state was removed', removed: dropped });
  for (let used = buildTriesUsed(c); used < BUILD_TRIES; used += 1) {
    const made = await commitBuild({ work: c.work, prepare: c.prepare, build: c.build, runnerPin: c.pinnedRunner, now: c.now });
    if (made.ok) return { needsPasses: made.value.ok };
    noteEvent(c, BUILD_ENVIRONMENT_EVENT, { reason: made.reason });
  }
  return { needsPasses: false };
}

// ---- the passes -----------------------------------------------------------------------------------------------------------------------------------

/** All ten passes, each to completion or exhaustion; stops early only on a halt or a stop signal. -> an answer | null. */
async function runPasses(c) {
  for (let n = 1; n <= PASS_COUNT; n += 1) {
    const stop = await runOnePass(c, n);
    if (stop) return stop;
  }
  return null;
}

async function runOnePass(c, n) {
  const pass = readAll(c)[n - 1];
  if (passState(pass) !== 'open') return null;
  const stop = await oneAttempt(c, { n, attempt: pass.attempts.length + 1 });
  return stop ?? runOnePass(c, n);
}

const stoppedBy = (n, attempt) => ({ kind: 'stopped', reason: `stopped by a signal before pass ${n}, attempt ${attempt}: what is written is kept and a run resumes it` });

/** The mask the profile asks for against the one the kernel reports for it: the problem sentence or null, and what was seen. */
async function maskProblem(c, cpus) {
  const seen = await c.affinity.probe(cpus);
  return { observed: seen.ok ? seen.value : null, problem: seen.ok ? affinityProblem({ wanted: cpus, observed: seen.value }) : seen.reason };
}

/** What must hold right before an attempt: no stop signal, no launching session, the mask really is the profile. -> an answer | null. */
async function beforeAttempt(c, { n, attempt }) {
  if (c.signal?.aborted) return stoppedBy(n, attempt);
  const busy = launchingProblem(c);
  if (busy) return busy;
  const { cpus } = profileOfPass(n);
  const mask = await maskProblem(c, cpus);
  return mask.problem ? halted(c, 'affinity', mask.problem, { wanted: cpus, observed: mask.observed }) : null;
}

const specOf = (c, { n, attempt }) => ({
  n, attempt, profile: profileOfPass(n).name, cpus: profileOfPass(n).cpus, tree: treeOf(c), samplesDir: casesOf(c), caseIds: c.ids,
  caseWallMs: c.spec.caseWallMs, holdSeconds: c.spec.holdSeconds, runCeilingMs: c.spec.runCeilingMs, limits: c.limits,
});

/** The pass child's answer, or the environment attempt a throw becomes. */
async function askChild(c, spec) {
  try { return await c.runPass(spec); } catch (e) { return { kind: 'environment', reason: `the pass child stopped on an unexpected ${e?.name ?? 'error'}: ${e?.message ?? e}` }; }
}

async function oneAttempt(c, which) {
  const before = await beforeAttempt(c, which);
  if (before) return before;
  const spec = specOf(c, which);
  const started = c.now();
  const { halt, ...body } = await askChild(c, spec);
  if (halt) return halted(c, halt, body.reason, { wanted: spec.cpus });
  writeAttempt({ work: c.work, n: which.n, profile: spec.profile, attempt: which.attempt, body: { ...body, startedAt: started.toISOString(), ms: c.now() - started } });
  rebuildVerdict(c.work, { expectedIds: c.ids, pinnedRunner: c.pinnedRunner });
  const over = c.afterAttempt?.(which) ?? null;
  return over ? halted(c, 'ceiling', over) : null;
}

// ---- the end --------------------------------------------------------------------------------------------------------------------------------------

/** The scoring has no pass left to run: the snapshot says what it is. A decided state gives the score; an exhausted one the void. */
function finish(c) {
  const snapshot = rebuildVerdict(c.work, { expectedIds: c.ids, pinnedRunner: c.pinnedRunner });
  if (snapshot.scoringState === 'open') return environment(`the scoring stopped with a pass still open: ${snapshot.reason}`);
  [treeOf(c), casesOf(c)].forEach(removeArea);
  if (snapshot.scoringState === 'exhausted') return { kind: 'void', class: VOID_UNSCORED, reason: snapshot.reason, verdict: snapshot };
  return { kind: 'scored', score: scoreOf(c, snapshot), verdict: snapshot };
}

/** The session's score: written once as `score.json` the first time the state leaves `open`, read back ever after. */
function scoreOf(c, snapshot) {
  const file = path.join(c.work, 'score.json');
  if (!existsSync(file)) {
    const build = recordedBuild(c).value;
    const rollup = rollupOf({ build, passes: readAll(c), limits: c.limits, ids: c.ids });
    const finished = finishRollup({ rollup, build, traps: c.traps ?? [] }).value;
    const extra = { verdict: snapshot.verdict, scoringState: snapshot.scoringState, passes: rollup.passes, scoringMs: snapshot.scoringMs, buildTreeSha256: build.treeSha256 };
    publishOnce({ file, text: `${JSON.stringify({ ...finished, explore: { ...finished.explore, ...extra } }, null, 2)}\n` });
  }
  return JSON.parse(readFileSync(file, 'utf8'));
}
