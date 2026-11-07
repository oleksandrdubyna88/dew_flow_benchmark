// The scoring of a quick-run session (daemon and web): the agent's tree on a CLEAN copy (its build output and packages removed per stack, every frozen file put back), the frozen `build.sh` once in the build
// box of the stack, then the language comparison's runner over the visible and the held-out scenario pairs through the execution layer (`runCases`: the delegated scope, the box, the loopback-only namespace,
// the memory certification). The verdict of every case is recorded; the result has the shape the olympiad judge returns, so `scoreShape` and everything after it are the pilot's own.
//
// A HARNESS FAILURE IS NEVER A VERDICT. The layer or the runner failing (a launch that failed, a memory read that cannot be certified, a pair the runner refuses, a case missing from its record, a driver killed by the
// ceiling) comes back as `fail(reason, { kind: 'environment' })`: the loop halts resumably and the session stays unscored. Only a build that really exited non-zero, and a case the runner really judged, are verdicts.
// The two seams (`buildFn`, `runFn`) are for tests and for the stub runner; the defaults are the real build box and the real layer.
import { copyFileSync, existsSync, mkdirSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { ownedFor } from '../../../.agents/conventions/tools/lib/owned.mjs';
import { holdForCaseWall } from '../../lang-bench/lib/exec-layer/prefix.mjs';
import { runCases, stableRunsParent } from '../../lang-bench/lib/exec-layer/run.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { attemptRecordProblem } from './attempt-record.mjs';
import { boxBuild } from './build-quick.mjs';
import { runnerRecordPinNow } from './harness-pin.mjs';
import { CASE_VERDICT, badBuildCase } from './verdict.mjs';
import { layerDirs } from './quick.mjs';
import { contractIdentity } from './contract-identity.mjs';
import { quickPristineCopy, splitFrozenChanges } from './copy-quick.mjs';
import { driftDecider } from './template-drift.mjs';
import { removeArea, scoreShape } from './score.mjs';

export const OWNED_TOKEN = 'bench';
/** FORMAT 4.6: one test lasts at most 600 s from the start of the program, sleeps included. The runner enforces it; with each scenario's own deadlines it is the only time bound of a quick case. */
export const RUNNER_TEST_BOUND_MS = 600_000;

/**
 * The bounds that apply, stated in the score. `limits.timeMs` is recorded (a calibration-shaped field) but nothing enforces it for a quick case: a case is bounded by its scenario's deadlines and the runner's test bound. The memory
 * limit and the build bound are enforced as given.
 */
export const enforcedBounds = limits => Object.freeze({ memoryBytes: limits.memoryBytes, buildTimeoutMs: limits.buildTimeoutMs, caseTestBoundMs: RUNNER_TEST_BOUND_MS, timeMsEnforced: false });
const CASE_ID = /^[A-Za-z0-9_-]{1,40}$/;

const idsOf = dir => readdirSync(dir).filter(f => f.endsWith('.in')).map(f => f.slice(0, -3)).sort();
const SAMPLE_FILE = /^samples\/(s\d\d)\.(in|out)$/;

/** Write the visible pairs of the frozen files into `dir` (only the pair `only`, when one is named). -> the visible ids written, sorted. */
const wanted = (m, only) => m !== null && (only === undefined || m[1] === only);

function writeVisible({ files, dir, only }) {
  const visible = [];
  for (const [rel, f] of Object.entries(files)) {
    const m = SAMPLE_FILE.exec(rel);
    if (!wanted(m, only)) continue;
    writeFileSync(path.join(dir, `${m[1]}.${m[2]}`), f.bytes);
    if (m[2] === 'in') visible.push(m[1]);
  }
  return [...visible].sort();
}

/** The held-out ids of a folder that are not `hNNN` or lack their `.out`. */
const badHeldIds = (held, heldoutDir) => held.filter(id => !/^h\d{3}$/.test(id) || !existsSync(path.join(heldoutDir, `${id}.out`)));

/** The lines of the manifest for the cases `held` (all of it when every case is taken): the comparison of a case is where the runner reads it. */
function manifestFor({ file, held, all }) {
  const text = readFileSync(file, 'utf8');
  if (all) return text;
  const kept = text.split('\n').filter(line => held.includes(line.trim().split(/\s+/)[0]));
  return kept.length > 0 ? `${kept.join('\n')}\n` : null;
}

const copyPairs = ({ held, heldoutDir, dir }) => held.forEach(id => ['in', 'out'].forEach(ext => copyFileSync(path.join(heldoutDir, `${id}.${ext}`), path.join(dir, `${id}.${ext}`))));

function copyHeld({ held, heldoutDir, dir, all = true }) {
  copyPairs({ held, heldoutDir, dir });
  const manifest = path.join(heldoutDir, 'cases.txt');
  const text = existsSync(manifest) ? manifestFor({ file: manifest, held, all }) : null;
  if (text !== null) writeFileSync(path.join(dir, 'cases.txt'), text);
}

/** The visible samples of the frozen files and the held-out cases in ONE case folder for the runner (the folder is on the host; only the built tree ever enters a box). -> `ok({ dir, visible: [id], heldout: [id] })` | `fail`. */
export function combinedCases({ files, heldoutDir, dir, only }) {
  mkdirSync(dir, { recursive: true });
  const visible = writeVisible({ files, dir, only });
  const every = existsSync(heldoutDir) ? idsOf(heldoutDir) : [];
  const held = only === undefined ? every : every.filter(id => id === only);
  const bad = badHeldIds(held, heldoutDir);
  if (bad.length > 0) return fail(`held-out case(s) ${bad.join(', ')}: ids are h001..h999 and each needs its .out file`);
  copyHeld({ held, heldoutDir, dir, all: only === undefined });
  return ok({ dir, visible, heldout: held });
}

const listed = list => (list.length > 0 ? list.join(', ') : 'none');

/** Why the runner's record is not a judgement of exactly the expected ids, or null. */
function idsProblem({ got, expected }) {
  const ids = got.map(c => c.id);
  const missing = expected.filter(id => !ids.includes(id));
  const extra = ids.filter(id => !expected.includes(id));
  if (missing.length + extra.length === 0 && new Set(ids).size === ids.length) return null;
  return `the runner's record does not hold exactly the expected cases (missing: ${listed(missing)}; unexpected: ${listed(extra)})`;
}

/** The runner's record -> the pilot's cases: `[{ id, set, verdict, wallMs, peakBytes, exit, reason, ... }]`, or a `fail` when the record is not a complete judgement of exactly the expected ids. */
export function casesOfRecord({ record, expected }) {
  const got = record?.cases;
  if (!Array.isArray(got)) return fail('the runner wrote no case list');
  const problem = idsProblem({ got, expected });
  return problem ? fail(problem) : ok(got.map(c => caseOf(c)).sort((a, b) => (a.id < b.id ? -1 : 1)));
}

const numberOr = (v, other = null) => (typeof v === 'number' && Number.isFinite(v) ? v : other);
const verdictOf = c => (c.status === 'PASS' ? CASE_VERDICT.pass : (/memory/i.test(c.reason ?? '') ? CASE_VERDICT.memoryLimit : CASE_VERDICT.wrongAnswer));

function caseOf(c) {
  const mem = c.lifecycle?.memory ?? {};
  return {
    id: c.id, set: c.id.startsWith('s') ? 'visible' : 'heldout', verdict: verdictOf(c), wallMs: numberOr(c.wallMs), peakBytes: numberOr(mem.peakBytes), memoryMaxBytes: numberOr(mem.maxBytes), exit: null,
    events: mem.events ?? null, reason: c.reason ?? '', rIds: c.rIds ?? [], comparison: c.comparison ?? null, firstMismatch: c.firstMismatch ?? null,
    leftover: c.lifecycle?.leftover ?? null, oomKill: mem.oomKill ?? null,
  };
}

const environment = reason => fail(reason, { kind: 'environment' });
const text = (value, from) => String(value).trim().slice(from);

/** Why the layer's answer holds no judgement, or null. */
function runProblem(ran) {
  if (ran.halt) return `the layer halted: ${ran.halt}: ${text(ran.stderr, 0).slice(0, 400)}`;
  if (![0, 1].includes(ran.exitCode)) return `the runner or the layer failed (exit ${ran.exitCode}): ${text(ran.stderr, -600)}`;
  return ran.record ? null : `the runner exited ${ran.exitCode} and wrote no record: ${text(ran.stderr, -300)}`;
}

/** What a layer result means: the record (a run that judged every case, passing or not) or an environment failure naming why not. */
export function judgedRecord(ran) {
  const problem = runProblem(ran);
  if (problem) return environment(problem);
  return ran.record.launch?.failed ? environment(`the launch failed: ${String(ran.record.launch.stderr).slice(-300)}`) : ok(ran.record);
}

/**
 * The cases of a record whose memory the layer did not measure (`peakBytes` null). The build passed and the layer ran, so a missing reading is the harness failing and never a verdict: the session stays
 * unscored. (A case the program killed by the memory limit still has its reading.) -> `environment` | null.
 */
export function uncertifiedProblem(cases) {
  const missing = cases.filter(c => typeof c.peakBytes !== 'number').map(c => c.id);
  return missing.length === 0 ? null : environment(`the layer ran but read no peak memory for case(s) ${missing.join(', ')}: the memory claim cannot be certified, so there is no verdict`);
}

/** The default run: the layer over the built tree and the combined case folder. */
export function layerRun({ owned, dirs, stack, kind, limits, caseWallMs, runCeilingMs, parent = stableRunsParent(), runCasesFn = runCases, runOptions = {} }) {
  // `holdSeconds`: a daemon or web case may last up to the test bound, far past the default T + G + margin of the scope holder, which would end the scope (and the program) under it
  return async ({ tree, samplesDir, caseIds }) => runCasesFn({
    owned, parent, tree, samplesDir, caseIds, dirs, memoryMaxBytes: limits.memoryBytes, caseWallMs, holdSeconds: holdForCaseWall(caseWallMs), runCeilingMs, stack, kind, data: true, ...runOptions,
  });
}

const badBuildCases = ids => ids.map(badBuildCase);

/** The case folder and its ids, or an environment failure (a malformed held-out folder, an id the runner would refuse). -> `ok({ dir, ids })` | `fail`. */
const heldoutMissing = held => environment(`the held-out folder is not there${held ? ` (${held.dir})` : ' (no held-out set was given)'}: a score without it would be visible-only, so there is no verdict`);

function casesIn({ files, sets, area, only }) {
  const held = sets.find(s => s.name === 'heldout');
  if (!held || !existsSync(held.dir)) return heldoutMissing(held);
  const cases = combinedCases({ files, heldoutDir: held.dir, dir: path.join(area, 'cases'), only });
  return cases.ok ? idsChecked({ cases: cases.value, heldDir: held.dir, only }) : environment(cases.reason);
}

const onlyChecked = ({ cases, ids, only }) => (ids.length === 1 ? ok({ dir: cases.dir, ids }) : environment(`the case ${only} is not among the visible samples or the held-out cases`));

/** The ids the runner would take, and at least one held-out case. -> `ok({ dir, ids })` | `environment`. */
function idsChecked({ cases, heldDir, only }) {
  const ids = [...cases.visible, ...cases.heldout];
  const long = ids.filter(id => !CASE_ID.test(id));
  if (long.length > 0) return environment(`case id(s) ${long.join(', ')} are not letters, digits, '_' and '-' (at most 40)`);
  if (only !== undefined) return onlyChecked({ cases, ids, only });
  return cases.heldout.length === 0 ? environment(`the held-out folder ${heldDir} holds no case: a score without held-out cases would be visible-only, so there is no verdict`) : ok({ dir: cases.dir, ids });
}

const buildLine = b => `build ${b.ok ? 'passed' : `FAILED (exit ${b.exit}${b.timedOut ? ', timed out' : ''})`} in ${b.wallMs} ms`;
const buildRecordOf = b => ({ ran: true, ok: b.ok, status: b.exit, wallMs: b.wallMs, timedOut: b.timedOut, tail: b.tail });

/**
 * The cases whose enforced limit, read back by the runner from the cgroup (`prog/memory.max`), is not the limit the scoring asked for (or was not read at all). The statement, the load configuration and the scorer
 * share one registered value; a case enforced under another is the harness failing, never a verdict. -> `environment` | null.
 */
export function readBackProblem(cases, expectedBytes) {
  const bad = cases.filter(c => c.memoryMaxBytes !== expectedBytes).map(c => `${c.id} (read back ${c.memoryMaxBytes ?? 'nothing'})`);
  return bad.length === 0 ? null : environment(`the memory limit enforced on case(s) ${bad.join(', ')} is not the registered ${expectedBytes} bytes: the limit is not what the statement says, so there is no verdict`);
}

/** Why the runner's record, stringified, is not the complete judgement of exactly these ids (the pass path only), or null. */
const completeProblem = ({ record, ids }) => attemptRecordProblem({ text: JSON.stringify(record), expected: ids, pinnedRunner: runnerRecordPinNow() });

/** The judged cases of a record: held to the complete validator first on the pass path (`validate`). -> `ok(cases)` | `fail`. */
function judgedCases({ record, ids, validate }) {
  const invalid = validate ? completeProblem({ record, ids }) : null;
  return invalid ? fail(invalid) : casesOfRecord({ record, expected: ids });
}

/** The judged cases of a run through the layer. -> `ok({ cases, record, ran })` | `fail`. */
async function runStep({ a, copy, cases, owned, dirs, validate = false }) {
  const runFn = a.runFn ?? layerRun({ owned, dirs, stack: a.stack, kind: a.profile.kind, limits: a.limits, caseWallMs: a.profile.caseWallMs, runCeilingMs: a.profile.runCeilingMs });
  const ran = await runFn({ tree: copy, samplesDir: cases.dir, caseIds: cases.ids });
  const record = judgedRecord(ran);
  if (!record.ok) return record;
  const judged = judgedCases({ record: record.value, ids: cases.ids, validate });
  if (!judged.ok) return environment(judged.reason);
  return uncertifiedProblem(judged.value) ?? readBackProblem(judged.value, a.limits.memoryBytes) ?? ok({ cases: judged.value, record: record.value, ran });
}

/** ONE pass over a committed tree: the layer, the complete validator, the certified peaks, the read-back limit. -> `ok({ cases, record, ran })` | `environment`. */
export const runPass = args => runStep({ ...args, validate: true });

/**
 * The clean copy of the agent's tree at `dest`: -> `ok({ frozen, drift, undecided, left, identity })` (the agent's frozen changes, the harness's drift, the changes that cannot be judged, the entries left out, and the identity of the
 * contract the scoring is made under). `a.driftPossible` and `a.history` (`template-drift.mjs`) decide what is the harness's; the ten-pass path takes the same arguments as the one-pass path, so both judge a change per file.
 */
export function prepareTree(a, dest) {
  const made = quickPristineCopy({ source: a.source, dest, stack: a.stack, files: a.files });
  const decide = driftDecider({ stack: a.stack, kind: a.profile.kind, treeSha: made.treeSha, history: a.history });
  const split = splitFrozenChanges(made.changed, { runnerDrift: a.driftPossible === true, decide });
  return ok({ frozen: split.agent, drift: split.harness, undecided: split.undecided, left: made.left, identity: (a.contractIdentityFn ?? contractIdentity)() });
}

/** The case folder (the visible samples and the held-out cases) under `area`. -> `ok({ dir, ids })` | `environment`. */
export const caseFolder = (a, area) => casesIn({ files: a.files, sets: a.sets, area, only: a.only });

/** The tree copy, its frozen changes (the agent's, and the harness's drift when `driftPossible`), the entries left out and the case folder. -> `ok({ copy, frozen, drift, left, cases })` | `fail`. */
function prepare(a, area) {
  const copy = path.join(area, 'tree');
  const tree = prepareTree(a, copy);
  const cases = caseFolder(a, area);
  return cases.ok ? ok({ copy, ...tree.value, cases: cases.value }) : cases;
}

/** The score with what only a quick copy knows: the harness drift in the frozen files (a rescore on a moved runner), the entries left out as non-source. Absent keys when there is nothing to say. */
function finishScore(score, prep) {
  const extra = { contractIdentity: prep.identity, ...(prep.drift.length > 0 ? { harnessDrift: prep.drift } : {}), ...(prep.undecided.length > 0 ? { undecidedDrift: prep.undecided } : {}), ...(prep.left.length > 0 ? { nonRegularLeftOut: prep.left } : {}) };
  return ok({ ...score, explore: { ...score.explore, enforced: enforcedBounds(score.explore.limits), ...extra } });
}

/** The score of a session from the rollup of its passes (`rollupOf`) and its completion record (`build.json`): the olympiad judge's own shape, so everything after `scoreShape` is the pilot's. -> `ok(score)`. */
export function finishRollup({ rollup, build, traps = [] }) {
  const scored = scoreShape({ result: rollup, traps, frozen: build.frozenChanges ?? [] });
  return finishScore(scored, { drift: build.harnessDrift ?? [], undecided: build.undecidedDrift ?? [], identity: build.contractIdentity, left: build.nonRegularLeftOut ?? [] });
}

/** The build. -> `{ done: <the whole answer> }` when it ends the scoring (an environment failure, or a build that failed: every case is `build failed`), else `{ build }`. */
async function buildStep({ a, prep, owned, dirs }) {
  const build = await (a.buildFn ?? boxBuild({ owned, dirs, snapshots: a.profile.snapshots, stack: a.stack }))({ tree: prep.copy, timeoutMs: a.limits.buildTimeoutMs });
  if (!build.ok) return { done: build };
  (a.say ?? (() => {}))(buildLine(build.value));
  const result = { build: buildRecordOf(build.value), limits: a.limits };
  if (build.value.ok) return { result };
  return { done: finishScore(scoreShape({ result: { ...result, cases: badBuildCases(prep.cases.ids) }, traps: a.traps ?? [], frozen: prep.frozen }), prep) };
}

async function scoreIn(a, area) {
  const prep = prepare(a, area);
  if (!prep.ok) return prep;
  const owned = ownedFor({ token: OWNED_TOKEN });
  const dirs = layerDirs(a.profile.paths);
  const built = await buildStep({ a, prep: prep.value, owned, dirs });
  if (built.done) return built.done;
  (a.say ?? (() => {}))(`running ${prep.value.cases.ids.length} case(s) through the execution layer`);
  const ran = await runStep({ a, copy: prep.value.copy, cases: prep.value.cases, owned, dirs });
  if (!ran.ok) return ran;
  const result = { ...built.result, cases: ran.value.cases, runner: ran.value.record.runner, memoryCertified: ran.value.ran.memoryCertified === true };
  return finishScore(scoreShape({ result, traps: a.traps ?? [], frozen: prep.value.frozen }), prep.value);
}

/**
 * Score a tree. `files` is the FROZEN set (`quickWorkspaceFiles().frozen`), `sets` the extra case folders (`[{ name: 'heldout', dir }]`), `limits` `{ memoryBytes, buildTimeoutMs, ... }`, `profile`
 * `{ explore, paths, snapshots, caseWallMs, runCeilingMs }`. A held-out folder that is not there, or holds no case, is an `environment` failure, never a visible-only score. Anything thrown is an `environment` failure too: a paid session is never left unscored by a crash.
 * `only` (a replay) scores that one case: its pair alone in the case folder. `driftPossible` (a rescore or replay on a moved runner) puts the changes under `runner/` in `explore.harnessDrift` and not in `frozenChanges`; `history` (`template-drift.mjs`) judges the template files and the derived packages list by their bytes: the old template's bytes are `explore.harnessDrift`, any other difference is the agent's, and a file whose old bytes cannot be read is `explore.undecidedDrift`. The score names the contract it was made under in `explore.contractIdentity`. -> `ok(score)` (the pilot's score shape) | `fail(reason, { kind })`.
 */
export async function scoreQuick(args) {
  const area = path.join(args.work, 'judge');
  const clean = args.cleanFn ?? removeArea;
  const result = await scoreGuarded(args, area, clean);
  return flagCleanup(result, cleanupProblem(clean, area));
}

/** The scoring between the cleaning of the scratch folder and its making: whatever is thrown, from the folder's own handling or the scoring, is an `environment` failure. */
async function scoreGuarded(args, area, clean) {
  try {
    clean(area);
    mkdirSync(area, { recursive: true });
    return await scoreIn(args, area);
  } catch (e) {
    return environment(`scoring stopped on an unexpected ${e?.name ?? 'error'}: ${e?.message ?? e}; nothing was scored, so there is no verdict`);
  }
}

/** Why the scratch folder could not be removed after the scoring, or null. */
function cleanupProblem(clean, area) {
  try { clean(area); return null; } catch (e) { return `${e?.name ?? 'error'}: ${e?.message ?? e}`; }
}

/** A failed cleanup loses no score: a score gets `explore.cleanupFailed`, a failure says it in its reason (and keeps its kind). */
function flagCleanup(result, problem) {
  if (problem === null) return result;
  if (!result.ok) return fail(`${result.reason}; the scratch folder could not be removed either (${problem})`, { kind: result.kind });
  return ok({ ...result.value, explore: { ...result.value.explore, cleanupFailed: problem } });
}
