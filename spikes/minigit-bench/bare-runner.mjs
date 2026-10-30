#!/usr/bin/env node
// The thin bare-arm runner (E3b.S1): one command runs one bare PAIR (or the A/A, or the Node pilot), v1 then v2, barrier, $6 cap, scoring, voids and the one re-run, resume, records.
//
//   node spikes/minigit-bench/bare-runner.mjs init --series-id s-yyyymmdd-xxxx [--dry-run]       pins the series (CLI copy, run-map, ledger)
//   node spikes/minigit-bench/bare-runner.mjs snapshot --stack csharp|node [--register]         makes one cache snapshot (registries only, free); --register writes its record into inputs/cache-snapshots.json
//   node spikes/minigit-bench/bare-runner.mjs seed-commit | seed-reveal                         the bare seed as commit-reveal
//   node spikes/minigit-bench/bare-runner.mjs run --series <dir> --what aa|pilot-node|pair:<k> [--dry-run] [--accept-noisy <reason>]    SPENDS MONEY (unless --dry-run)
//   node spikes/minigit-bench/bare-runner.mjs run --series <dir> --what rehearse-v2 [--dry-run]                                       FREE: the v2 path on an A/A twin's tree with a stub CLI and a fake token (G-14)
//   node spikes/minigit-bench/bare-runner.mjs resume --series <dir> [--what ...] [--accept-harness <identity> --amendment <date>] [--after-stop <rule> --reason <text>]
//   node spikes/minigit-bench/bare-runner.mjs resume --series <dir> --settle-only                          settles and charges what a stopped series left launching; launches nothing
//   node spikes/minigit-bench/bare-runner.mjs status --series <dir>
//
// Exit codes: 0 done | 2 a series stop, or the spend gate | 3 a resumable halt (the machine not quiet, the preflight, a scorer environment failure or timeout) | 4 usage or
// configuration (the pre-registration guard) | 5 the runner or the scorer failed | 130 / 143 cancelled by SIGINT / SIGTERM.
// The runner re-executes itself under `taskset -c 6,7` (an env marker stops a loop), so its broker, receiver, proxy and sampler sit on core 3, the harness's (parent 4.1).
import { spawn } from 'node:child_process';
import { randomBytes } from 'node:crypto';
import { existsSync, readFileSync, realpathSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { parseArgs } from 'node:util';
import { fileURLToPath } from 'node:url';
import { spinProbe } from './lib/cpu-sampler.mjs';
import { EXIT, reconcile, runLoop } from './lib/bare-series.mjs';
import { applyInit, describePlan, initPlan } from './lib/bare-init.mjs';
import { maskFor, pilotRid, ridOf, seriesId as makeSeriesId } from './lib/bare-plan.mjs';
import { buildRun, REPO, sampleOnceFor, SPIKE } from './lib/bare-config.mjs';
import { CHECK_STOP_RULES } from './lib/crosscheck.mjs';
import { loadRecipe, makeSnapshot, registerSnapshot, snapshotRecord } from './lib/cache-snapshot.mjs';
import { readLedger } from './lib/ledger.mjs';
import { checkPins, recordAfterStop } from './lib/bare-pins.mjs';
import { rehearsalRefusal, rehearseV2, twinOf } from './lib/rehearsal.mjs';
import { harnessIdentity } from './lib/prereg-guard.mjs';
import { calibrateIfQuiet, loadQuiet } from './lib/quiet.mjs';
import { planSession, readRunMap } from './lib/run-map.mjs';
import { fail, ok } from './lib/result.mjs';
import { commitSeed, revealSeed } from './lib/seed.mjs';
import { spendGate, spendTotals } from './lib/spend-gate.mjs';
import { runBounded } from './lib/run.mjs';
import { acquireSeriesLock } from './lib/series-lock.mjs';

const OPTIONS = {
  series: { type: 'string' }, what: { type: 'string' }, 'dry-run': { type: 'boolean' }, 'accept-noisy': { type: 'string' }, 'accept-harness': { type: 'string' }, amendment: { type: 'string' },
  'after-stop': { type: 'string' }, reason: { type: 'string' }, 'settle-only': { type: 'boolean' }, stack: { type: 'string' }, 'series-id': { type: 'string' }, 'series-root': { type: 'string' }, 'versions-dir': { type: 'string' },
  'toolchains-dir': { type: 'string' }, register: { type: 'boolean' }, 'inputs-dir': { type: 'string' }, 'seed-file': { type: 'string' }, out: { type: 'string' }, 'work-root': { type: 'string' },
};
/** The series stops `--after-stop` cannot pass: the stop classes, a breach of any check under a `stop` policy (one list with `collectSession`'s, review N3) and the second contaminated void. */
const SERIES_STOPS = Object.freeze(['stop: security', 'stop: auth', 'stop: alias drift', ...CHECK_STOP_RULES, 'second void: contaminated']);
/** The operator's default folders, read from the CURRENT home at each call (never captured when the module loads, so a test's fake HOME is honoured). */
const defaults = () => {
  const home = os.homedir();
  return {
    seriesRoot: path.join(home, 'rsd', 'minigit'), toolchainsDir: path.join(home, 'rsd', 'minigit', 'toolchains'), versionsDir: path.join(home, '.local', 'share', 'claude', 'versions'),
    seedFile: path.join(home, 'rsd', 'minigit', 'seeds', 'bare.seed'), workRoot: path.join(home, 'rsd', 'minigit', 'snapshot-work'),
  };
};

const PAIR = /^pair:([1-3])$/;
const FIXED_WHAT = { aa: { kind: 'aa' }, 'pilot-node': { kind: 'pilot' }, 'rehearse-v2': { kind: 'rehearsal' } };
/** `--what` as `{ kind, pair? }`, or null for anything the runner does not know. */
export function parseWhat(what) {
  if (Object.hasOwn(FIXED_WHAT, what ?? '')) return { ...FIXED_WHAT[what] };
  const pair = PAIR.exec(what ?? '');
  return pair ? { kind: 'data', pair: Number(pair[1]) } : null;
}

/** The fixed sessions of the A/A (two identical bare C# v1 twins on masks 0-5 and 8-13) or the Node pilot (one bare Node v1), planned once. */
function planFixed(map, kind, random = () => randomBytes(4).toString('hex')) {
  const specs = kind === 'aa' ? [['csharp', '0-5'], ['csharp', '8-13']] : [['node', '0-5']];
  return specs.reduce((m, [stack, mask], i) => {
    const entry = { rid: pilotRid(random()), pair: 0, stack, phase: 'v1', attempt: 1, kind, label: 'paired', mask, twin: kind === 'aa' ? 'ab'[i] : null };
    const planned = planSession(m, entry);
    if (!planned.ok) throw new Error(planned.reason);
    return planned.value;
  }, map);
}

/** What a dry run prints for a pair: the ordered first phase (rids, masks, order) and the spend gate's lines; it claims nothing and starts nothing. */
function dryRunLines({ map, ctx, what }) {
  const gate = (n) => spendGate({ ledger: readLedger(ctx.ledgerFile), liveUsd: 0, caps: Array(n).fill(ctx.caps.capUsd), ceilingUsd: ctx.ceilingUsd, bareBudgetUsd: ctx.bareBudgetUsd });
  if (what.kind !== 'data') return [`${what.kind}: would launch the fixed sessions ${Object.values(map.sessions).filter(s => s.kind === what.kind).map(s => `${s.rid} (${s.stack} ${s.phase}, mask ${s.mask})`).join(', ') || '(planned at the first run)'}`, ...gate(what.kind === 'aa' ? 2 : 1).lines];
  const seed = ctx.seed;
  const first = ['csharp', 'node'].map(stack => ({ stack, rid: ridOf({ seed, pair: what.pair, stack, phase: 'v1', attempt: 1 }), mask: maskFor({ seed, pair: what.pair, stack }) }));
  return [
    `pair ${what.pair}: v1 phase, both stacks started in one tick, then both collected and scored (the barrier), then the v2 phase; voids re-run once by the schedule`,
    ...first.map(s => `  ${s.stack}: rid ${s.rid}, mask ${s.mask}`),
    `cap $${ctx.caps.capUsd}, wall ${ctx.caps.wallMs / 60000} min, stall ${ctx.caps.stallMs / 60000} min`, ...gate(2).lines, 'the pre-registration guard, the quiet check and the preflight run at the real start; no layout is claimed and no broker is started here',
  ];
}

// ---- commands -------------------------------------------------------------------------------------------------------------------

/** The operator's `--inputs-dir`, or this repository's own inputs folder. */
const inputsDirOf = v => (v['inputs-dir'] ? path.resolve(v['inputs-dir']) : path.join(SPIKE, 'inputs'));

/** `ok(tag)` when exactly one `prereg/minigit-bare-*` tag exists, else `fail` saying so. */
async function initTag() {
  const tag = await runBounded('git', ['tag', '--list', 'prereg/minigit-bare-*'], { cwd: REPO, timeoutMs: 30_000 });
  const tags = tag.ok ? tag.value.stdout.split('\n').filter(Boolean) : [];
  return tags.length === 1 ? ok(tags[0]) : fail(`init: expected exactly one prereg/minigit-bare-* tag, found ${tags.length}: tag the pre-registration commit first`);
}

/** `ok(sha)`: the commit the tag points at. */
async function tagCommit(tag) {
  const commit = await runBounded('git', ['rev-parse', `${tag}^{commit}`], { cwd: REPO, timeoutMs: 30_000 });
  return commit.ok && commit.value.code === 0 ? ok(commit.value.stdout.trim()) : fail(`init: cannot read the tag or the harness identity: ${commit.reason ?? commit.value.stderr}`);
}

/** `ok({ preregTag, preregCommit, harnessIdentity })` from git and the harness's own files, or `fail` naming what could not be read. */
async function initFacts() {
  const tag = await initTag();
  if (!tag.ok) return tag;
  const commit = await tagCommit(tag.value);
  if (!commit.ok) return commit;
  const identity = await harnessIdentity(REPO);
  return identity.ok ? ok({ preregTag: tag.value, preregCommit: commit.value, harnessIdentity: identity.value }) : fail(`init: cannot read the tag or the harness identity: ${identity.reason}`);
}

/** The plan of `init`, written nowhere: `ok({ plan, inputsDir })` | `fail(sentence)`. */
async function prepareInit(v) {
  const inputsDir = inputsDirOf(v);
  const facts = await initFacts();
  if (!facts.ok) return facts;
  const plan = initPlan({
    inputsDir, seriesRoot: path.resolve(v['series-root'] ?? defaults().seriesRoot), seriesId: v['series-id'] ?? makeSeriesId({ date: new Date(), rand: randomBytes(2).toString('hex') }),
    versionsDir: v['versions-dir'] ?? defaults().versionsDir, toolchainsDir: v['toolchains-dir'] ?? defaults().toolchainsDir, facts: facts.value,
  });
  return plan.ok ? ok({ plan: plan.value, inputsDir }) : fail(`init: ${plan.reason}`);
}

/** The spin probe's calibration on a quiet machine: `ok({ calibration })` | `fail`. */
async function calibrateInit(inputsDir) {
  const quiet = loadQuiet(JSON.parse(readFileSync(path.join(inputsDir, 'quiet.json'), 'utf8')));
  const probe = async i => { const r = await spinProbe({ iterations: quiet.value.spinIterations, cpu: quiet.value.spinCpus[i % 2] }); if (!r.ok) throw new Error(r.reason); return r.value; };
  return calibrateIfQuiet({ thresholds: quiet.value, sampleOnce: await sampleOnceFor({ thresholds: quiet.value, probe }), sleep: ms => new Promise(r => setTimeout(r, ms)), probe });
}

/** Calibrate, then write the series. */
async function finishInit({ plan, inputsDir, write }) {
  const calibrated = await calibrateInit(inputsDir);
  if (!calibrated.ok) { write(`init: ${calibrated.reason}`); return EXIT.halt; }
  const cal = calibrated.value.calibration;
  const done = applyInit({ plan, spin: cal });
  write(done.ok ? `init done: ${plan.seriesDir}; spin median ${cal.medianMs.toFixed(2)} ms (spread ${cal.spreadMs.toFixed(2)} ms over ${cal.runs} runs)` : `init: ${done.reason}`);
  return done.ok ? EXIT.done : EXIT.config;
}

/** The series `init` writes is a PAID series: the same rule as run and resume (review N17), asked before anything is read; a dry run writes nothing and may read any folder. -> a sentence or null. */
const initRefusal = (v, seams) => (v['dry-run'] ? null : foreignInputsRefusal({ v, verb: 'init', seams }));

async function cmdInit(v, write, seams = {}) {
  const refused = initRefusal(v, seams);
  if (refused) return refuse(write, refused);
  const prepared = await prepareInit(v);
  if (!prepared.ok) return refuse(write, prepared.reason);
  describePlan(prepared.value.plan).forEach(write);
  if (v['dry-run']) { write('dry run: nothing was written'); return EXIT.done; }
  return finishInit({ ...prepared.value, write });
}

/** `ok(recipe)` for `--stack csharp|node` and the registered cache recipe, or `fail(sentence)`. */
function snapshotRecipe(v) {
  if (!['csharp', 'node'].includes(v.stack)) return fail('usage error: --stack csharp|node');
  return loadRecipe(JSON.parse(readFileSync(path.join(SPIKE, 'inputs', 'cache-recipe.json'), 'utf8')));
}

/** The lines `snapshot` prints for a made snapshot. */
const snapshotLines = (stack, s) => `snapshot ${stack}: ${s.dir}\n  tree sha256 ${s.treeSha256}\n  size ${s.sizeBytes} bytes\n  parts ${JSON.stringify(s.parts)}\n  packages ${s.packages.length}\n  record ${JSON.stringify(snapshotRecord(s))}`;

/** `--register`: write the snapshot's record into inputs/cache-snapshots.json. -> the exit code. */
function registerMade(v, s, write) {
  const registered = registerSnapshot({ file: path.join(SPIKE, 'inputs', 'cache-snapshots.json'), stack: v.stack, record: snapshotRecord(s), madeOn: new Date().toISOString().slice(0, 10) });
  write(registered.ok ? 'registered in inputs/cache-snapshots.json (commit it with the pre-registration)' : `register: ${registered.reason}`);
  return registered.ok ? EXIT.done : EXIT.config;
}

async function cmdSnapshot(v, write) {
  const recipe = snapshotRecipe(v);
  if (!recipe.ok) return refuse(write, recipe.reason);
  const made = await makeSnapshot({ stack: v.stack, recipe: recipe.value, root: path.resolve(v['work-root'] ?? defaults().workRoot), out: path.resolve(v.out ?? defaults().toolchainsDir) });
  if (!made.ok) { write(`snapshot: ${made.reason}`); return EXIT.failed; }
  write(snapshotLines(v.stack, made.value));
  return v.register ? registerMade(v, made.value, write) : EXIT.done;
}

/** A usage or configuration refusal: the sentence is written and the exit code is 4. */
const refuse = (write, reason) => { write(reason); return EXIT.config; };
const verbOf = resume => (resume ? 'resume' : 'run');

/** What `run` and `resume` take of the series for the lock: none for a dry run, a missing series or a missing run-map (those are refused later, with their own sentence). */
function lockFor(v) {
  const seriesDir = path.resolve(v.series ?? '');
  const free = v['dry-run'] === true || !v.series || !existsSync(path.join(seriesDir, 'run-map.json'));
  return free ? ok(null) : acquireSeriesLock({ seriesDir });
}

/** `run`, `resume` and `resume --settle-only` hold the series lock for the whole command (review N13); a dry run writes nothing and takes none. */
async function cmdRun(v, write, { resume }, seams = {}) {
  const lock = lockFor(v);
  if (!lock.ok) return refuse(write, `${verbOf(resume)}: ${lock.reason}`);
  try {
    return await runCommand(v, write, { resume }, seams);
  } finally {
    lock.value?.release();
  }
}

const SETTLE_USAGE = 'usage error: --settle-only belongs to resume: it settles and charges the sessions a stopped series left launching, and launches nothing';
/** The usage errors of the flags alone, in order; each takes `(v, resume)` and returns a sentence or null. */
const USAGE_CHECKS = [
  (v, resume) => (resume ? afterStopRefusal(v) : null),
  (v, resume) => (v['settle-only'] === true && !resume ? SETTLE_USAGE : null),
];
const usageProblem = (v, resume) => USAGE_CHECKS.map(check => check(v, resume)).find(Boolean) ?? null;

/** `ok({ seriesDir, map })` when `--series` holds a readable run-map, else `fail(usage error)`. */
function readSeries(v) {
  const seriesDir = path.resolve(v.series ?? '');
  const read = readRunMap(path.join(seriesDir, 'run-map.json'));
  return !v.series || !read.ok ? fail(`usage error: --series <dir> with a run-map.json (${read.reason ?? 'missing'})`) : ok({ seriesDir, map: read.value });
}

/** `--what`, or the one the run-map says is open; a settle-only launches nothing, so its `--what` decides nothing. */
const whatOf = (v, map, settleOnly) => parseWhat(v.what ?? whatOfMap(map)) ?? (settleOnly ? { kind: 'aa', pair: 0 } : null);

/** `ok(what)`, or a `fail` for an unknown `--what` and for a rehearsal the run-map refuses. */
function chooseWhat(v, map, settleOnly) {
  const what = whatOf(v, map, settleOnly);
  if (!what) return fail('usage error: --what aa | pilot-node | rehearse-v2 | pair:<1-3>');
  const refusal = what.kind === 'rehearsal' ? rehearsalRefusal(map) : null;
  return refusal ? fail(refusal) : ok(what);
}

/** Step 1, from the flags and the run-map alone: `ok({ seriesDir, map, settleOnly, what, dry, rehearsing, resume })` | `fail(sentence)`. */
function parseRun(v, resume) {
  const problem = usageProblem(v, resume);
  if (problem) return fail(problem);
  const series = readSeries(v);
  if (!series.ok) return series;
  const settleOnly = v['settle-only'] === true;
  const what = chooseWhat(v, series.value.map, settleOnly);
  return what.ok ? ok({ ...series.value, settleOnly, what: what.value, dry: v['dry-run'] === true, rehearsing: what.value.kind === 'rehearsal', resume }) : what;
}

/** A PAID series reads the registered inputs of THIS repository and no copy of them (review N17): the guard compares the repository's blobs with the tag, so a folder it does not look at could carry other thresholds. -> a sentence or null. */
export function foreignInputsRefusal({ v, verb, seams }) {
  const own = seams.ownInputs ?? path.join(SPIKE, 'inputs');
  const foreign = v['inputs-dir'] && !sameFolder(inputsDirOf(v), own);
  return foreign ? `${verb} refused: --inputs-dir ${inputsDirOf(v)} is not the repository's own inputs folder (${own}): a paid run reads the registered inputs of this repository only; --inputs-dir is for dry runs and tests` : null;
}

/** The refusal of a foreign `--inputs-dir` for a run or resume that pays (a dry run and a rehearsal pay nothing and read no registered input as a rule of the run). */
const inputsRefusal = ({ v, state, seams }) => (state.dry || state.rehearsing ? null : foreignInputsRefusal({ v, verb: verbOf(state.resume), seams }));

/** Step 11: the pins read from DISK now and compared with the run-map, at every paid run and resume, before anything is built or read; a resume then records its `--after-stop`. -> `ok(state)` with the pinned map | `fail`. */
async function pinStep({ v, state, seams }) {
  const { map, seriesDir, resume, settleOnly } = state;
  const runMapFile = path.join(seriesDir, 'run-map.json');
  const ledgerFile = path.join(seriesDir, 'ledger.ndjson');
  const pins = await checkPins({
    map, inputsDir: inputsDirOf(v), repo: seams.repo ?? REPO, acceptHarness: v['accept-harness'] ? { identity: v['accept-harness'], amendmentDate: v.amendment } : null, settleOnly, runMapFile, ledgerFile,
  });
  if (!pins.ok) return fail(`${verbOf(resume)} refused: ${pins.reason}`);
  return ok({ ...state, map: resume ? recordAfterStop({ map: pins.value.map, rule: v['after-stop'], reason: v.reason, runMapFile, ledgerFile }) : pins.value.map });
}

/** Everything before the build: the flags, the inputs folder and the pins. -> `ok(state)` | `fail(sentence)`. */
async function prepareRun({ v, resume, seams }) {
  const parsed = parseRun(v, resume);
  if (!parsed.ok) return parsed;
  const refused = inputsRefusal({ v, state: parsed.value, seams });
  if (refused) return fail(refused);
  return parsed.value.dry ? parsed : pinStep({ v, state: parsed.value, seams });
}

/** The dry run of a rehearsal or a pair, a fixed list: what would start; nothing is claimed, started or spent. */
function dryRun({ state, built, write }) {
  const lines = state.rehearsing
    ? [`rehearse-v2: would rehearse the v2 path on the tree of twin ${twinOf(state.map).rid} for BOTH stacks (C#: bin/ obj/ node_modules/ left out; TypeScript: node_modules/ only): a v2 workspace per stack filled from it, a stub CLI in the real box behind the real broker (a loopback stub upstream, a fake token), the real scorer at phase v2, then G-14; nothing is paid and no real token is read`]
    : dryRunLines({ map: state.map, ctx: built.ctx, what: state.what });
  [...lines, 'dry run: nothing was claimed, started or spent'].forEach(write);
  return EXIT.done;
}

/** SIGINT and SIGTERM abort the run: the reason and the exit code are recorded for the loop, then the controller aborts. */
function armSignals(abort, ctl) {
  for (const [sig, code] of [['SIGINT', 130], ['SIGTERM', 143]]) process.on(sig, () => { abort.reason = sig; abort.exit = code; ctl.abort(); });
}

/** `rid (status, class)` of one settled session. */
const settledOf = (map, rid) => `${rid} (${map.sessions[rid].status}${map.sessions[rid].class ? `, ${map.sessions[rid].class}` : ''})`;

/** The one line `resume --settle-only` ends with. */
const settleLine = ({ waiting, map }) => `settle-only: ${waiting.length === 0 ? 'no session was launching' : `settled ${waiting.map(rid => settledOf(map, rid)).join(', ')}`}; nothing was launched${map.stopped ? `; the series remains stopped (${map.stopped.rule}): start a new series` : ''}`;

/** A resume settles what the last run left before it continues. -> `{ exit }` (done, or a refusal) | `{ map }` (the settled map, to continue with). */
async function settleStep({ state, built, write }) {
  const waiting = Object.values(state.map.sessions).filter(s => s.status === 'launching').map(s => s.rid);
  const settled = await reconcile({ ctx: built.ctx, deps: built.deps, map: state.map, say: write, settleOnly: state.settleOnly });
  if (!settled.ok) { write(`resume: ${settled.reason}`); return { exit: settled.exit ?? EXIT.failed }; }
  if (!state.settleOnly) return { map: settled.value };
  write(settleLine({ waiting, map: settled.value }));
  return { exit: EXIT.done };
}

/** The last line of a run: `done` or `halted`, and the loop's message. -> the loop's exit code. */
function reportLoop(write, r) {
  write(`${r.exit === EXIT.done ? 'done' : 'halted'}: ${r.message}`);
  return r.exit;
}

/** The v2 rehearsal, or the loop over the fixed list (planned once) or the pair. */
async function driveRun({ state, built, map, write, ctl }) {
  const { ctx, deps } = built;
  if (state.rehearsing) return reportLoop(write, await rehearseV2({ ctx, deps, map, signal: ctl.signal, say: write }));
  const { kind } = state.what;
  const planned = kind !== 'data' && Object.values(map.sessions).every(s => s.kind !== kind) ? planFixed(map, kind) : map;
  return reportLoop(write, await runLoop({ ctx, deps: { ...deps, phase: deps.phase }, map: planned, signal: ctl.signal, say: write }));
}

/** Arm the signals, settle (a resume), then drive. */
async function executeRun({ state, built, write }) {
  const ctl = new AbortController();
  armSignals(built.abort, ctl);
  const settled = state.resume ? await settleStep({ state, built, write }) : { map: state.map };
  return settled.exit ?? driveRun({ state, built, map: settled.map, write, ctl });
}

/** The run's configuration and edges; a rehearsal never reads the token file. */
function buildFor({ v, state, seams }) {
  const { seriesDir, map, what } = state;
  return buildRun({ seriesDir, map, kind: what.kind, pair: what.pair ?? 0, args: { inputsDir: v['inputs-dir'] ? inputsDirOf(v) : undefined, acceptNoisy: v['accept-noisy'] }, paid: !state.dry && !state.rehearsing, seams });
}

async function runCommand(v, write, { resume }, seams = {}) {
  const prepared = await prepareRun({ v, resume, seams });
  if (!prepared.ok) return refuse(write, prepared.reason);
  const state = prepared.value;
  const built = await buildFor({ v, state, seams });
  if (!built.ok) return refuse(write, `cannot start: ${built.reason}`);
  return state.dry ? dryRun({ state, built: built.value, write }) : executeRun({ state, built: built.value, write });
}

const WHAT_OF = { data: s => `pair:${s.pair}`, aa: () => 'aa', pilot: () => 'pilot-node', rehearsal: () => 'rehearse-v2' };
/** `--what` for a resume without one: the first fixed list, rehearsal or pair with a session not yet scored. */
export function whatOfMap(map) {
  const open = Object.values(map.sessions).find(s => s.status !== 'scored' && !(s.status === 'ran' && s.data === false));
  return open ? WHAT_OF[open.kind](open) : undefined;
}

/** `--after-stop <rule> --reason <text>` passes a HALT; the series stops of section 5 cannot be passed this way, and a pass without a reason is refused. */
export function afterStopRefusal(v) {
  const rule = v['after-stop'];
  if (!rule) return null;
  if (SERIES_STOPS.some(s => rule.startsWith(s))) return `--after-stop ${rule}: a series stop cannot be passed this way: fix the cause and start a new series with an A/A`;
  return v.reason ? null : `--after-stop ${rule}: needs --reason <text>`;
}

/** `  rid pair 1 csharp v1 a1 [data] status: class (label)`, one session of `status`. */
const sessionLine = s => `  ${s.rid} pair ${s.pair} ${s.stack} ${s.phase} a${s.attempt} [${s.kind}] ${s.status}${s.class ? `: ${s.class}` : ''}${s.label ? ` (${s.label})` : ''}`;
const pinnedNote = () => (process.env.BENCH_RUNNER_PINNED === '1' ? 'set' : 'not set');

/** The lines of `status`: the core, the series, the spend of both limits, every session. */
function statusLines({ seriesDir, map }) {
  const allowed = /^Cpus_allowed_list:\s*(\S+)/m.exec(readFileSync('/proc/self/status', 'utf8'))?.[1];
  const t = spendTotals(readLedger(path.join(seriesDir, 'ledger.ndjson')));
  return [
    `harness core: Cpus_allowed_list ${allowed} (re-exec marker ${pinnedNote()})`,
    `series ${map.seriesId}${map.stopped ? `, STOPPED: ${map.stopped.rule}` : ''}; prereg ${map.preregTag}; harness ${map.harnessIdentity.slice(0, 12)}`,
    `spend: ceiling-side $${t.ceilingUsd.toFixed(4)} of $${map.ceilingUsd}, bare $${t.bareUsd.toFixed(4)} of $${map.bareBudgetUsd}`,
    ...Object.values(map.sessions).map(sessionLine),
  ];
}

function cmdStatus(v, write) {
  const series = readSeries(v);
  if (!series.ok) return refuse(write, series.reason);
  statusLines(series.value).forEach(write);
  return EXIT.done;
}

/** Do two paths resolve (links followed) to the same folder? A path that does not exist is not the same as anything. */
function sameFolder(a, b) {
  try { return realpathSync(a) === realpathSync(b); } catch { return false; }
}

/** `--inputs-dir` and `--seed-file` of the seed commands. */
const seedArgs = v => ({ seedFile: v['seed-file'] ?? defaults().seedFile, inputsDir: inputsDirOf(v) });

function cmdSeedCommit(v, write) {
  const { seedFile, inputsDir } = seedArgs(v);
  const r = commitSeed({ seedFile, inputsDir });
  write(r.ok ? `seed committed: its hash ${r.value.hash} is in inputs/seed.sha256; the seed itself is in ${seedFile} (mode 600)` : `seed-commit: ${r.reason}`);
  return r.ok ? EXIT.done : EXIT.config;
}

function cmdSeedReveal(v, write) {
  const r = revealSeed(seedArgs(v));
  write(r.ok ? 'seed revealed to inputs/seed.txt: commit and push it before pair 1' : `seed-reveal: ${r.reason}`);
  return r.ok ? EXIT.done : EXIT.config;
}

/** The commands, each `(values, write, seams) -> exit code`. */
const COMMANDS = {
  init: cmdInit, snapshot: cmdSnapshot, status: cmdStatus, 'seed-commit': cmdSeedCommit, 'seed-reveal': cmdSeedReveal,
  run: (v, write, seams) => cmdRun(v, write, { resume: false }, seams), resume: (v, write, seams) => cmdRun(v, write, { resume: true }, seams),
};

/** `ok(values)` for the flags of a command, or `fail(usage error)`. */
function parseFlags(rest) {
  try { return ok(parseArgs({ args: rest, options: OPTIONS, allowPositionals: false }).values); } catch (e) { return fail(`usage error: ${e.message}`); }
}

/** The whole command; `write` receives each output line. `seams` (`{ repo, home, ownInputs }`) are for tests only: a run uses this repository, the operator's home and this repository's inputs folder. Resolves to the exit code. */
export async function main(argv, write = line => process.stdout.write(`${line}\n`), seams = {}) {
  const [command, ...rest] = argv;
  const flags = parseFlags(rest);
  if (!flags.ok) return refuse(write, flags.reason);
  if (!Object.hasOwn(COMMANDS, command ?? '')) return refuse(write, `usage error: the commands are ${Object.keys(COMMANDS).join(', ')}`);
  return COMMANDS[command](flags.value, write, seams);
}

/** Re-execute under `taskset -c 6,7` unless the marker says this IS the re-executed process. */
function reexec() {
  const child = spawn('taskset', ['-c', '6,7', process.execPath, ...process.argv.slice(1)], { env: { ...process.env, BENCH_RUNNER_PINNED: '1' }, stdio: 'inherit' });
  for (const sig of ['SIGINT', 'SIGTERM']) process.on(sig, () => child.kill(sig));
  child.on('exit', (code, signal) => process.exit(code ?? (signal === 'SIGINT' ? 130 : 143)));
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  if (process.env.BENCH_RUNNER_PINNED === '1') process.exitCode = await main(process.argv.slice(2));
  else reexec();
}

