// The commands of `explore.mjs` (everything but `prepare` and `calibrate`, which live with their modules). Each takes the parsed flags and an `io` and returns an exit code: 0 done | 2 a stop |
// 3 a resumable halt | 4 usage, configuration or a refusal | 5 failed. `io.say` prints a line; `io.home` is the operator's home; nothing here reads the token except `run` (through `io.tokenFn`).
import { existsSync, mkdirSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { dayOf, systemClock } from './clock.mjs';
import { harnessIdentity } from '../../minigit-bench/lib/prereg-guard.mjs';
import { readLedger } from '../../minigit-bench/lib/ledger.mjs';
import { runBounded } from '../../minigit-bench/lib/run.mjs';
import { makeWritable } from '../../minigit-bench/lib/cache-snapshot.mjs';
import { decidedPublicText, leakShingles, leakcheckCommand } from './leak.mjs';
import { loadExplore } from './inputs.mjs';
import { pinsOfTask, seriesTaskProblem, taskFromValues } from './tasks.mjs';
import { loadPrompts } from './plan.mjs';
import { readbackOf } from './readback.mjs';
import { exploreRoot, REPO } from './paths.mjs';
import { noteFor, rehearse } from './rehearse.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { loadCalibration } from './limits.mjs';
import { readToken, runPair } from './run.mjs';
import { createSeries, latestSeries, loadSeries, seriesRootProblem, pairComplete, seriesInputs, seriesPrompts, stackState } from './series.mjs';
import { loadWorld } from './world.mjs';
import { machineFor, pilotQuiet } from './machine.mjs';

const EXIT = Object.freeze({ done: 0, stop: 2, halt: 3, config: 4, failed: 5 });
const git = async (...args) => { const r = await runBounded('git', args, { cwd: REPO, timeoutMs: 30_000 }); return r.ok && r.value.code === 0 ? r.value.stdout.trim() : null; };

/** Where the git state of the subject stands: the branch, the head sha, the tree id of this spike and v1's harness identity. */
export async function subjectOf() {
  const [branch, sha, pilot, v1] = await Promise.all([git('rev-parse', '--abbrev-ref', 'HEAD'), git('rev-parse', 'HEAD'), git('rev-parse', 'HEAD:spikes/lang-explore'), harnessIdentity(REPO)]);
  return { branch: branch ?? 'unknown', sha: sha ?? 'unknown', harnessPath: 'spikes/lang-explore/explore.mjs', harnessIdentity: v1.ok ? v1.value.slice(0, 12) : 'unknown', pilotIdentity: pilot ?? 'unknown' };
}

/** The series directory a command works on: `--series` (which must be one of the explore root's own), else the newest series of `task`. */
export function seriesDirOf(values, home, task) {
  if (values.series) return seriesGiven(path.resolve(values.series), home);
  const dir = latestSeries(home, task.id);
  return dir ? seriesGiven(dir, home) : fail(`there is no series of task ${task.id} under ${exploreRoot(home)}: run \`explore.mjs init --task ${task.id}\` first`);
}

const seriesGiven = (dir, home) => { const outside = seriesRootProblem(home, dir); return outside ? fail(outside) : ok(dir); };

const refused = (io, reason) => { io.say(reason); return EXIT.config; };
const firstBad = (...results) => results.find(r => !r.ok);

/** What the series' inputs record besides the limits: the subject, the interpreter, the CLI and the pins of the series' task (its spec, FORMAT and private notes; the pins file's own `specs` block is task 1's). */
async function initExtra(explore, task) {
  return { subject: await subjectOf(), python3: explore.python3, cli: explore.cli, specs: explore.specs, taskPins: pinsOfTask(task), prompts: loadPrompts() };
}

/** A series is made for a task of the olympiad plan only: task 1's series (`x-20261101-13cc`) is closed and records, notes and analysis rest on it. */
const closedProblem = ({ explore, task }) => (explore.olympiad.planTasks.includes(task.id) ? null : `task ${task.id} is not a task of the olympiad plan (${explore.olympiad.planTasks.join(', ')}): its series is closed and no second one is made`);

/** What `init` needs: the task, the pins and the task's calibration. -> `ok({ explore, task, calibration })` | `fail`. */
function initNeeds(values, io) {
  const given = taskFromValues(values, io.exploreFn);
  const open = given.ok ? openForInit(given.value) : given;
  if (!open.ok) return open;
  const calibration = loadCalibration(io.home, given.value.task, () => ok(given.value.explore));
  return calibration.ok ? ok({ ...given.value, calibration: calibration.value }) : calibration;
}

const openForInit = pins => { const closed = closedProblem(pins); return closed ? fail(closed) : ok(pins); };

/** `init --task NN`: create a series of that task (the mask draw is made and recorded here). Needs `prepare` and `calibrate --task NN` done. */
export async function cmdInit(values, io) {
  const need = initNeeds(values, io);
  if (!need.ok) return refused(io, need.reason);
  const { explore, task, calibration } = need.value;
  const made = createSeries({ home: io.home, explore, calibration, task: task.id, inputsExtra: await initExtra(explore, task) });
  if (!made.ok) return refused(io, made.reason);
  io.say(`series ${made.value.state.seriesId} created at ${made.value.dir}`);
  io.say(`the mask draw (recorded): C# starts on ${made.value.state.draw.csharpHigh ? 'CPUs 8-13' : 'CPUs 0-5'} in pair 1; the stacks swap every pair`);
  io.say(`task ${task.id} (${task.title}): run its pairs with \`run --task ${task.id} --series ${made.value.dir} --pair <k>\``);
  return EXIT.done;
}

/** The series a command works on, opened and checked to be a series of `task`: `ok({ dir, state, map })` | `fail`. */
function openSeriesOf(values, io, task) {
  const dir = seriesDirOf(values, io.home, task);
  const series = dir.ok ? loadSeries(dir.value) : dir;
  const wrong = series.ok ? seriesTaskProblem({ state: series.value.state, task }) : null;
  return wrong ? fail(wrong) : series;
}

/** Each way `run`'s arguments can be wrong, as `[wrong(values), sentence(values)]`. */
const RUN_CHECKS = Object.freeze([
  [v => !v.series, () => 'run needs --series <dir>: a paid pair is never started on a series this command guessed (`explore.mjs init` prints the directory)'],
  [v => !/^[1-9]\d*$/.test(String(v.pair ?? '')), v => `run needs --pair <k>, a positive whole number (got ${v.pair === undefined ? 'nothing' : `'${v.pair}'`})`],
  [v => v['allow-over'] !== undefined && v['allow-over'].trim() === '', () => 'run: --allow-over needs the owner\'s words and the date, e.g. --allow-over "the owner, 2026-11-02: go on"'],
]);

/** Why `run`'s arguments cannot start a paid pair, or null: `--series` is a directory this command did not guess, `--pair` a positive integer, `--allow-over` the owner's words when given. */
const runArguments = values => RUN_CHECKS.find(([wrong]) => wrong(values))?.[1](values) ?? null;

/** What `run` needs before it plans anything: the task, valid arguments and the task's series. -> `ok({ explore, task, series })` | `fail`. */
function runSetup(values, io) {
  const given = taskFromValues(values, io.exploreFn);
  const asked = given.ok ? runArguments(values) : given.reason;
  if (asked) return fail(asked);
  const series = openSeriesOf(values, io, given.value.task);
  return series.ok ? ok({ ...given.value, series: series.value }) : series;
}

/** `run --task NN --pair k [--rerun <stack>] [--accept-noisy <reason>] [--allow-over <text>]`: ONE pair. SPENDS MONEY. */
export async function cmdRun(values, io) {
  const setup = runSetup(values, io);
  if (!setup.ok) return refused(io, setup.reason);
  const { explore, task, series } = setup.value;
  const run = { ...io, worldFn: io.worldFn ?? loadWorld, tokenFn: io.tokenFn ?? (async home => readToken(home)), acceptNoisy: values['accept-noisy'] ?? null, allowOver: values['allow-over'] ?? null };
  const r = await runPair({ seriesDir: series.dir, pair: Number(values.pair), rerun: values.rerun ?? null, explore, task, io: run });
  io.say(r.message);
  (r.problems ?? []).forEach(p => io.say(`record problem: ${p}`));
  if (r.pairFile) io.say(`pair record: ${r.pairFile}`);
  return r.exit;
}

/** The lines a rehearsal prints after it ends. */
function sayRehearsal(io, r) {
  io.say(r.message);
  (r.checks ?? []).forEach(c => io.say(`  ${c.ok ? 'PASS' : 'FAIL'}  ${c.id}: ${c.detail}`));
  (r.problems ?? []).forEach(p => io.say(`  record problem: ${p}`));
  if (r.seriesDir) io.say(`rehearsal series: ${r.seriesDir} (note: rehearsal-note.md)`);
}

/** `rehearse --task NN`: the $0 end-to-end run with the stub CLI, over that task's material. */
export async function cmdRehearse(values, io) {
  const given = taskFromValues(values, io.exploreFn);
  if (!given.ok) return refused(io, given.reason);
  const r = await rehearse({ task: given.value.task, io: { ...io, worldFn: io.worldFn ?? loadWorld, subject: io.subject ?? await subjectOf() } });
  sayRehearsal(io, r);
  return r.exit;
}

const sessionLine = s => `  ${s.rid} pair ${s.pair} ${s.stack} a${s.attempt} mask ${s.mask} ${s.status}${s.class ? `: ${s.class}` : ''} (${s.label})`;
const spentOf = dir => readLedger(path.join(dir, 'ledger.ndjson')).filter(l => l.event === undefined).reduce((a, l) => a + l.usd, 0);
const pairLine = (map, p) => `pair ${p}: ${pairComplete(map, p) ? 'finished' : ['csharp', 'node'].map(s => `${s} ${stackState(map, p, s)}`).join(', ')}`;

/** The lines of `status`: the series, the pairs, every session. */
export function statusLines({ dir, map, state }) {
  const head = `series ${state.seriesId}${state.task ? ` (task ${state.task})` : ''}${map.stopped ? `, STOPPED: ${map.stopped.rule}` : ''}; spend $${spentOf(dir).toFixed(4)}; draw: C# ${state.draw.csharpHigh ? '8-13' : '0-5'} in pair 1`;
  return [head, ...Array.from({ length: state.pairs }, (_, i) => pairLine(map, i + 1)), ...Object.values(map.sessions).map(sessionLine)];
}

/** `status --task NN`: the sessions, the pairs and the spend of a series of that task. */
export function cmdStatus(values, io) {
  const given = taskFromValues(values, io.exploreFn);
  if (!given.ok) return refused(io, given.reason);
  const series = openSeriesOf(values, io, given.value.task);
  if (!series.ok) return refused(io, series.reason);
  statusLines(series.value).forEach(l => io.say(l));
  return EXIT.done;
}

/** Where the note goes: `--out`, else `research/lang-v2/EXPLORE_task<label>_<the date of the SERIES' creation>.md` (never the date the command happens to run on; task 1's label is `1`, as its recorded note is named). */
export const notePathOf = (values, state, task) => values.out ?? path.join(REPO, 'research', 'lang-v2', `EXPLORE_task${task.noteLabel}_${state.createdAt.slice(0, 10)}.md`);

const overwriteProblem = (out, force) => (existsSync(out) && !force ? `${out} exists: the note is not overwritten without --force` : null);

/** What a note needs: the series (of the task), the world, the series' inputs and where to write; or the refusal. */
async function noteInputs(values, io, task) {
  const series = openSeriesOf(values, io, task);
  if (!series.ok) return series;
  const world = await (io.worldFn ?? loadWorld)({ home: io.home, work: path.join(series.value.dir, 'private'), task });
  const inputs = seriesInputs(series.value.dir);
  const bad = firstBad(world, inputs);
  if (bad) return bad;
  const out = notePathOf(values, series.value.state, task);
  const clash = overwriteProblem(out, values.force);
  return clash ? fail(clash) : ok({ dir: series.value.dir, series: series.value, world: world.value, inputs: inputs.value, out });
}

/** `note --task NN [--out file] [--force]`: the public note of that task's series, generated from its records, under the limits the series was created with. An existing file is never overwritten without `--force`. `io.subject` and `io.date` are for tests. */
export async function cmdNote(values, io) {
  const given = taskFromValues(values, io.exploreFn);
  const need = given.ok ? await noteInputs(values, io, given.value.task) : given;
  if (!need.ok) return refused(io, need.reason);
  const { dir, series, world, inputs, out } = need.value;
  mkdirSync(path.dirname(out), { recursive: true });
  writeFileSync(out, noteFor({ series: series.state, seriesDir: dir, map: series.map, world: { ...world, calibration: { limits: inputs.limits } }, pins: world.explore, task: given.value.task, rehearsal: false, subject: io.subject ?? await subjectOf(), date: io.date ?? dayOf(io.now ?? systemClock) }));
  io.say(`note written: ${out}`);
  return EXIT.done;
}

const readbackLines = (session, rb) => [
  `${session.rid} (${session.stack}): ${rb.ok ? 'ok' : 'REFUTED'}; prompt sha256 planned ${rb.plan.promptSha256.slice(0, 12)}, in the transcript ${rb.transcriptPromptSha256?.slice(0, 12) ?? 'not recorded'}; env keys ${rb.envKeys.length}`,
  ...rb.checks.map(c => `  ${c.ok === null ? 'n/a ' : c.ok ? 'PASS' : 'FAIL'}  ${c.id}`),
];

const plannedSessions = (dir, map) => Object.values(map.sessions).filter(s => existsSync(path.join(dir, 's', s.rid, 'plan.json')));
const readAll = (dir, map, prompts) => plannedSessions(dir, map).map(s => readSession(dir, s, prompts));
const allPass = read => read.length > 0 && read.every(x => x.rb.ok);
const readSession = (dir, s, prompts) => ({ s, rb: readbackOf({ sessionDir: path.join(dir, 's', s.rid), registered: prompts[s.stack] }) });

/** The series of the command's task with the prompts frozen in it. -> `ok({ series, prompts })` | `fail`. */
function seriesAndPrompts(values, io) {
  const given = taskFromValues(values, io.exploreFn);
  const series = given.ok ? openSeriesOf(values, io, given.value.task) : given;
  const prompts = series.ok ? seriesPrompts(series.value.dir) : series;
  return prompts.ok ? ok({ series: series.value, prompts: prompts.value.prompts }) : prompts;
}

/** `readback --task NN [--series dir]`: what each session was really launched with, against the prompts frozen in its series and the registered environment (read back for every session, so one cell per stack is covered). */
export function cmdReadback(values, io) {
  const need = seriesAndPrompts(values, io);
  if (!need.ok) return refused(io, need.reason);
  const { series, prompts } = need.value;
  const read = readAll(series.dir, series.map, prompts);
  read.forEach(({ s, rb }) => readbackLines(s, rb).forEach(l => io.say(l)));
  return allPass(read) ? EXIT.done : EXIT.failed;
}

const PRUNED = Object.freeze(['box/nuget', 'box/npm', 'box/home/.dotnet', 'score']);

/** Remove one session's reproducible directories; -> the ones removed. */
function pruneSession(root, rid) {
  return PRUNED.map(rel => path.join(root, rid, rel)).filter(existsSync).map(target => { makeWritable(target); rmSync(target, { recursive: true, force: true }); return path.relative(root, target); });
}

const sessionIds = root => (existsSync(root) ? readdirSync(root) : []);
const prunedLine = removed => `pruned ${removed.length} director${removed.length === 1 ? 'y' : 'ies'}: ${removed.join(', ') || 'nothing to remove'}`;

/** `prune --task NN`: remove the cache copies and scoring copies of every session of that task's series (reproducible); the records, streams, transcripts, ledger, calibration and the held-out folder stay. */
export function cmdPrune(values, io) {
  const given = taskFromValues(values, io.exploreFn);
  if (!given.ok) return refused(io, given.reason);
  const series = openSeriesOf(values, io, given.value.task);
  if (!series.ok) return refused(io, series.reason);
  const root = path.join(series.value.dir, 's');
  io.say(prunedLine(sessionIds(root).flatMap(rid => pruneSession(root, rid))));
  return EXIT.done;
}

const spinOf = home => { const f = path.join(exploreRoot(home), 'spin.json'); return existsSync(f) ? JSON.parse(readFileSync(f, 'utf8')) : null; };

const quietLine = r => (r.ok ? (r.value.quiet ? 'the machine is quiet' : `started anyway: ${r.value.flags.join(', ')} (${r.value.reason})`) : r.reason);

/** `quiet [--accept-noisy <reason>]`: the machine-quiet check alone, as `run` does it before a pair: re-check every 60 s for 30 min, print each verdict, exit 3 when it never is. */
export async function cmdQuiet(values, io) {
  const thresholds = pilotQuiet();
  const spin = spinOf(io.home);
  if (!thresholds.ok) return refused(io, thresholds.reason);
  if (!spin) return refused(io, 'the spin probe is not calibrated: run `explore.mjs prepare` on a quiet machine');
  const machine = machineFor({ thresholds: thresholds.value, calibratedMs: spin.medianMs, seriesDir: exploreRoot(io.home), acceptNoisy: values['accept-noisy'] ?? null, home: io.home, ...(io.machineSeams ?? {}) });
  const r = await machine.quiet({ cellIds: [], say: io.say, signal: io.signal });
  io.say(quietLine(r));
  return r.ok ? EXIT.done : EXIT.halt;
}

/** `leakcheck [--task NN] [--range A..B]`: see `leakcheckCommand`. */
export async function cmdLeakcheck(values, io) {
  const explore = (io.exploreFn ?? loadExplore)();
  if (!explore.ok) return refused(io, explore.reason);
  const r = await leakcheckCommand({ values, io, explore: explore.value, say: io.say });
  if (r.message) io.say(r.message);
  return r.exit;
}

export { EXIT, decidedPublicText, leakShingles };
