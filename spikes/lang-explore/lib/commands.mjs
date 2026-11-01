// The commands of `explore.mjs` (everything but `prepare` and `calibrate`, which live with their modules). Each takes the parsed flags and an `io` and returns an exit code: 0 done | 2 a stop |
// 3 a resumable halt | 4 usage, configuration or a refusal | 5 failed. `io.say` prints a line; `io.home` is the operator's home; nothing here reads the token except `run` (through `io.tokenFn`).
import { existsSync, mkdirSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { harnessIdentity } from '../../minigit-bench/lib/prereg-guard.mjs';
import { leakcheck } from '../../minigit-bench/lib/heldout-leak.mjs';
import { readLedger } from '../../minigit-bench/lib/ledger.mjs';
import { runBounded } from '../../minigit-bench/lib/run.mjs';
import { makeWritable } from '../../minigit-bench/lib/cache-snapshot.mjs';
import { loadHeldout } from './heldout.mjs';
import { loadExplore } from './inputs.mjs';
import { loadPrompts } from './plan.mjs';
import { readbackOf } from './readback.mjs';
import { exploreRoot, REPO } from './paths.mjs';
import { noteFor, rehearse } from './rehearse.mjs';
import { loadCalibration, readToken, runPair } from './run.mjs';
import { createSeries, latestSeries, loadSeries, pairComplete, stackState } from './series.mjs';
import { loadWorld } from './world.mjs';
import { machineFor, pilotQuiet } from './machine.mjs';

const EXIT = Object.freeze({ done: 0, stop: 2, halt: 3, config: 4, failed: 5 });
const git = async (...args) => { const r = await runBounded('git', args, { cwd: REPO, timeoutMs: 30_000 }); return r.ok && r.value.code === 0 ? r.value.stdout.trim() : null; };

/** Where the git state of the subject stands: the branch, the head sha, the tree id of this spike and v1's harness identity. */
export async function subjectOf() {
  const [branch, sha, pilot, v1] = await Promise.all([git('rev-parse', '--abbrev-ref', 'HEAD'), git('rev-parse', 'HEAD'), git('rev-parse', 'HEAD:spikes/lang-explore'), harnessIdentity(REPO)]);
  return { branch: branch ?? 'unknown', sha: sha ?? 'unknown', harnessPath: 'spikes/lang-explore/explore.mjs', harnessIdentity: v1.ok ? v1.value.slice(0, 12) : 'unknown', pilotIdentity: pilot ?? 'unknown' };
}

/** The series directory a command works on: `--series`, else the newest. */
export function seriesDirOf(values, home) {
  const dir = values.series ? path.resolve(values.series) : latestSeries(home);
  return dir ? { ok: true, value: dir } : { ok: false, reason: `there is no series under ${exploreRoot(home)}: run \`explore.mjs init\` first` };
}

const refused = (io, reason) => { io.say(reason); return EXIT.config; };
const firstBad = (...results) => results.find(r => !r.ok);

/** What the series' inputs record besides the limits: the subject, the interpreter, the CLI and the task pins. */
async function initExtra(explore) {
  return { subject: await subjectOf(), python3: explore.python3, cli: explore.cli, specs: explore.specs };
}

/** `init`: create a series (the mask draw is made and recorded here). Needs `prepare` and `calibrate` done. */
export async function cmdInit(values, io) {
  const explore = loadExplore();
  const calibration = loadCalibration(io.home);
  const bad = firstBad(explore, calibration);
  if (bad) return refused(io, bad.reason);
  const made = createSeries({ home: io.home, explore: explore.value, calibration: calibration.value, inputsExtra: await initExtra(explore.value) });
  if (!made.ok) return refused(io, made.reason);
  io.say(`series ${made.value.state.seriesId} created at ${made.value.dir}`);
  io.say(`the mask draw (recorded): C# starts on ${made.value.state.draw.csharpHigh ? 'CPUs 8-13' : 'CPUs 0-5'} in pair 1; the stacks swap every pair`);
  return EXIT.done;
}

/** `run --pair k [--rerun <stack>] [--accept-noisy <reason>]`: ONE pair. SPENDS MONEY. */
export async function cmdRun(values, io) {
  if (!values.series) return refused(io, 'run needs --series <dir>: a paid pair is never started on a series this command guessed (`explore.mjs init` prints the directory)');
  const dir = seriesDirOf(values, io.home);
  if (!dir.ok) return refused(io, dir.reason);
  const r = await runPair({ seriesDir: dir.value, pair: Number(values.pair), rerun: values.rerun ?? null, io: { ...io, worldFn: io.worldFn ?? loadWorld, tokenFn: io.tokenFn ?? (async home => readToken(home)), acceptNoisy: values['accept-noisy'] ?? null } });
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

/** `rehearse`: the $0 end-to-end run with the stub CLI. */
export async function cmdRehearse(values, io) {
  const r = await rehearse({ io: { ...io, worldFn: io.worldFn ?? loadWorld, subject: await subjectOf() } });
  sayRehearsal(io, r);
  return r.exit;
}

const sessionLine = s => `  ${s.rid} pair ${s.pair} ${s.stack} a${s.attempt} mask ${s.mask} ${s.status}${s.class ? `: ${s.class}` : ''} (${s.label})`;
const spentOf = dir => readLedger(path.join(dir, 'ledger.ndjson')).filter(l => l.event === undefined).reduce((a, l) => a + l.usd, 0);
const pairLine = (map, p) => `pair ${p}: ${pairComplete(map, p) ? 'finished' : ['csharp', 'node'].map(s => `${s} ${stackState(map, p, s)}`).join(', ')}`;

/** The lines of `status`: the series, the pairs, every session. */
export function statusLines({ dir, map, state }) {
  const head = `series ${state.seriesId}${map.stopped ? `, STOPPED: ${map.stopped.rule}` : ''}; spend $${spentOf(dir).toFixed(4)}; draw: C# ${state.draw.csharpHigh ? '8-13' : '0-5'} in pair 1`;
  return [head, ...Array.from({ length: state.pairs }, (_, i) => pairLine(map, i + 1)), ...Object.values(map.sessions).map(sessionLine)];
}

/** `status`: the sessions, the pairs and the spend of a series. */
export function cmdStatus(values, io) {
  const dir = seriesDirOf(values, io.home);
  if (!dir.ok) return refused(io, dir.reason);
  const series = loadSeries(dir.value);
  if (!series.ok) return refused(io, series.reason);
  statusLines({ dir: dir.value, ...series.value }).forEach(l => io.say(l));
  return EXIT.done;
}

/** `note [--out file]`: the public note, generated from the series' records. */
export async function cmdNote(values, io) {
  const dir = seriesDirOf(values, io.home);
  if (!dir.ok) return refused(io, dir.reason);
  const series = loadSeries(dir.value);
  const world = await (io.worldFn ?? loadWorld)({ home: io.home, work: path.join(dir.value, 'private') });
  const calibration = loadCalibration(io.home);
  const bad = firstBad(series, world, calibration);
  if (bad) return refused(io, bad.reason);
  const text = noteFor({ series: series.value.state, seriesDir: dir.value, map: series.value.map, world: { ...world.value, calibration: calibration.value }, pins: world.value.explore, rehearsal: false, subject: await subjectOf() });
  const out = values.out ?? path.join(REPO, 'research', 'lang-v2', `EXPLORE_task1_${new Date().toISOString().slice(0, 10)}.md`);
  mkdirSync(path.dirname(out), { recursive: true });
  writeFileSync(out, text);
  io.say(`note written: ${out}`);
  return EXIT.done;
}

const readbackLines = (session, rb) => [
  `${session.rid} (${session.stack}): ${rb.ok ? 'ok' : 'REFUTED'}; prompt sha256 planned ${rb.plan.promptSha256.slice(0, 12)}, in the transcript ${rb.transcriptPromptSha256?.slice(0, 12) ?? 'not recorded'}; env keys ${rb.envKeys.length}`,
  ...rb.checks.map(c => `  ${c.ok === null ? 'n/a ' : c.ok ? 'PASS' : 'FAIL'}  ${c.id}`),
];

const plannedSessions = (dir, map) => Object.values(map.sessions).filter(s => existsSync(path.join(dir, 's', s.rid, 'plan.json')));
const allPass = read => read.length > 0 && read.every(x => x.rb.ok);
const readSession = (dir, s, prompts) => ({ s, rb: readbackOf({ sessionDir: path.join(dir, 's', s.rid), registered: prompts[s.stack] }) });

/** `readback [--series dir]`: what each session was really launched with, against the registered prompt and environment (read back for every session, so one cell per stack is covered). */
export function cmdReadback(values, io) {
  const dir = seriesDirOf(values, io.home);
  if (!dir.ok) return refused(io, dir.reason);
  const series = loadSeries(dir.value);
  if (!series.ok) return refused(io, series.reason);
  const read = plannedSessions(dir.value, series.value.map).map(s => readSession(dir.value, s, loadPrompts().prompts));
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

/** `prune`: remove the cache copies and scoring copies of every session (reproducible); the records, streams and transcripts stay. */
export function cmdPrune(values, io) {
  const dir = seriesDirOf(values, io.home);
  if (!dir.ok) return refused(io, dir.reason);
  const root = path.join(dir.value, 's');
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

/** The literals the leak check looks for: every held-out expected output line of at least 8 characters (v1's minimum). */
export function leakNeedles(heldout) {
  const lines = readdirSync(heldout.casesDir).filter(f => f.endsWith('.out')).flatMap(f => readFileSync(path.join(heldout.casesDir, f), 'utf8').split('\n'));
  return [...new Set(lines.filter(l => l.length >= 8))].join('\n');
}

const leakLine = r => (r.ok ? `leakcheck clean: ${r.scanned} files and ${r.history.join(' + ') || 'no history'} scanned for the pilot's held-out outputs` : `leakcheck FAILED: ${r.reason}`);

/** `leakcheck [--range A..B]`: v1's leak check over the repository and the branch range, with the pilot's held-out expected outputs as the needles. */
export async function cmdLeakcheck(values, io) {
  const heldout = loadHeldout(io.home);
  if (!heldout.ok) return refused(io, heldout.reason);
  const r = await leakcheck({ repoRoot: REPO, tokensText: leakNeedles(heldout.value), ...(values.range ? { range: values.range } : {}) });
  io.say(leakLine(r));
  return r.ok ? EXIT.done : EXIT.failed;
}

export { EXIT };
