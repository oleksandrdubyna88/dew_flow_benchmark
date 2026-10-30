#!/usr/bin/env node
// The thin bare-arm runner (E3b.S1): one command runs one bare PAIR (or the A/A, or the Node pilot), v1 then v2, barrier, $6 cap, scoring, voids and the one re-run, resume, records.
//
//   node spikes/minigit-bench/bare-runner.mjs init --series-id s-yyyymmdd-xxxx [--dry-run]       pins the series (CLI copy, run-map, ledger)
//   node spikes/minigit-bench/bare-runner.mjs snapshot --stack csharp|node [--register]         makes one cache snapshot (registries only, free); --register writes its record into inputs/cache-snapshots.json
//   node spikes/minigit-bench/bare-runner.mjs seed-commit | seed-reveal                         the bare seed as commit-reveal
//   node spikes/minigit-bench/bare-runner.mjs run --series <dir> --what aa|pilot-node|pair:<k> [--dry-run] [--accept-noisy <reason>]    SPENDS MONEY (unless --dry-run)
//   node spikes/minigit-bench/bare-runner.mjs run --series <dir> --what rehearse-v2 [--dry-run]                                       FREE: the v2 path on an A/A twin's tree with a stub CLI and a fake token (G-14)
//   node spikes/minigit-bench/bare-runner.mjs resume --series <dir> [--what ...] [--accept-harness <identity> --amendment <date>] [--after-stop <rule> --reason <text>]
//   node spikes/minigit-bench/bare-runner.mjs status --series <dir>
//
// Exit codes: 0 done | 2 a series stop, or the spend gate | 3 a resumable halt (the machine not quiet, the preflight, a scorer environment failure or timeout) | 4 usage or
// configuration (the pre-registration guard) | 5 the runner or the scorer failed | 130 / 143 cancelled by SIGINT / SIGTERM.
// The runner re-executes itself under `taskset -c 6,7` (an env marker stops a loop), so its broker, receiver, proxy and sampler sit on core 3, the harness's (parent 4.1).
import { spawn } from 'node:child_process';
import { randomBytes } from 'node:crypto';
import { readFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { parseArgs } from 'node:util';
import { fileURLToPath } from 'node:url';
import { spinProbe } from './lib/cpu-sampler.mjs';
import { EXIT, reconcile, runLoop } from './lib/bare-series.mjs';
import { applyInit, describePlan, initPlan } from './lib/bare-init.mjs';
import { maskFor, pilotRid, ridOf, seriesId as makeSeriesId } from './lib/bare-plan.mjs';
import { buildRun, REPO, sampleOnceFor, SPIKE } from './lib/bare-config.mjs';
import { loadRecipe, makeSnapshot, registerSnapshot, snapshotRecord } from './lib/cache-snapshot.mjs';
import { readLedger } from './lib/ledger.mjs';
import { checkPins, recordAfterStop } from './lib/bare-pins.mjs';
import { rehearsalRefusal, rehearseV2, twinOf } from './lib/rehearsal.mjs';
import { harnessIdentity } from './lib/prereg-guard.mjs';
import { calibrateIfQuiet, loadQuiet } from './lib/quiet.mjs';
import { planSession, readRunMap } from './lib/run-map.mjs';
import { commitSeed, revealSeed } from './lib/seed.mjs';
import { spendGate, spendTotals } from './lib/spend-gate.mjs';
import { runBounded } from './lib/run.mjs';

const OPTIONS = {
  series: { type: 'string' }, what: { type: 'string' }, 'dry-run': { type: 'boolean' }, 'accept-noisy': { type: 'string' }, 'accept-harness': { type: 'string' }, amendment: { type: 'string' },
  'after-stop': { type: 'string' }, reason: { type: 'string' }, stack: { type: 'string' }, 'series-id': { type: 'string' }, 'series-root': { type: 'string' }, 'versions-dir': { type: 'string' },
  'toolchains-dir': { type: 'string' }, register: { type: 'boolean' }, 'inputs-dir': { type: 'string' }, 'seed-file': { type: 'string' }, out: { type: 'string' }, 'work-root': { type: 'string' },
};
const SERIES_STOPS = Object.freeze(['stop: security', 'stop: auth', 'stop: alias drift', 'T1 breach', 'T2 breach', 'second void: contaminated']);
/** The operator's default folders, read from the CURRENT home at each call (never captured when the module loads, so a test's fake HOME is honoured). */
const defaults = () => {
  const home = os.homedir();
  return {
    seriesRoot: path.join(home, 'rsd', 'minigit'), toolchainsDir: path.join(home, 'rsd', 'minigit', 'toolchains'), versionsDir: path.join(home, '.local', 'share', 'claude', 'versions'),
    seedFile: path.join(home, 'rsd', 'minigit', 'seeds', 'bare.seed'), workRoot: path.join(home, 'rsd', 'minigit', 'snapshot-work'),
  };
};

const PAIR = /^pair:([1-3])$/;
export const parseWhat = what => (what === 'rehearse-v2' ? { kind: 'rehearsal' } : what === 'aa' || what === 'pilot-node' ? { kind: what === 'aa' ? 'aa' : 'pilot' } : PAIR.test(what ?? '') ? { kind: 'data', pair: Number(PAIR.exec(what)[1]) } : null);

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

async function cmdInit(v, write) {
  const inputsDir = v['inputs-dir'] ? path.resolve(v['inputs-dir']) : path.join(SPIKE, 'inputs');
  const tag = await runBounded('git', ['tag', '--list', 'prereg/minigit-bare-*'], { cwd: REPO, timeoutMs: 30_000 });
  const tags = tag.ok ? tag.value.stdout.split('\n').filter(Boolean) : [];
  if (tags.length !== 1) { write(`init: expected exactly one prereg/minigit-bare-* tag, found ${tags.length}: tag the pre-registration commit first`); return EXIT.config; }
  const commit = await runBounded('git', ['rev-parse', `${tags[0]}^{commit}`], { cwd: REPO, timeoutMs: 30_000 });
  const identity = await harnessIdentity(REPO);
  if (!commit.ok || commit.value.code !== 0 || !identity.ok) { write(`init: cannot read the tag or the harness identity: ${commit.reason ?? identity.reason ?? commit.value.stderr}`); return EXIT.config; }
  const plan = initPlan({
    inputsDir, seriesRoot: path.resolve(v['series-root'] ?? defaults().seriesRoot), seriesId: v['series-id'] ?? makeSeriesId({ date: new Date(), rand: randomBytes(2).toString('hex') }),
    versionsDir: v['versions-dir'] ?? defaults().versionsDir, toolchainsDir: v['toolchains-dir'] ?? defaults().toolchainsDir, facts: { preregTag: tags[0], preregCommit: commit.value.stdout.trim(), harnessIdentity: identity.value },
  });
  if (!plan.ok) { write(`init: ${plan.reason}`); return EXIT.config; }
  describePlan(plan.value).forEach(write);
  if (v['dry-run']) { write('dry run: nothing was written'); return EXIT.done; }
  const quiet = loadQuiet(JSON.parse(readFileSync(path.join(inputsDir, 'quiet.json'), 'utf8')));
  const probe = async i => { const r = await spinProbe({ iterations: quiet.value.spinIterations, cpu: quiet.value.spinCpus[i % 2] }); if (!r.ok) throw new Error(r.reason); return r.value; };
  const calibrated = await calibrateIfQuiet({ thresholds: quiet.value, sampleOnce: await sampleOnceFor({ thresholds: quiet.value, probe }), sleep: ms => new Promise(r => setTimeout(r, ms)), probe });
  if (!calibrated.ok) { write(`init: ${calibrated.reason}`); return EXIT.halt; }
  const cal = calibrated.value.calibration;
  const done = applyInit({ plan: plan.value, spin: cal });
  write(done.ok ? `init done: ${plan.value.seriesDir}; spin median ${cal.medianMs.toFixed(2)} ms (spread ${cal.spreadMs.toFixed(2)} ms over ${cal.runs} runs)` : `init: ${done.reason}`);
  return done.ok ? EXIT.done : EXIT.config;
}

async function cmdSnapshot(v, write) {
  if (!['csharp', 'node'].includes(v.stack)) { write('usage error: --stack csharp|node'); return EXIT.config; }
  const recipe = loadRecipe(JSON.parse(readFileSync(path.join(SPIKE, 'inputs', 'cache-recipe.json'), 'utf8')));
  if (!recipe.ok) { write(recipe.reason); return EXIT.config; }
  const made = await makeSnapshot({ stack: v.stack, recipe: recipe.value, root: path.resolve(v['work-root'] ?? defaults().workRoot), out: path.resolve(v.out ?? defaults().toolchainsDir) });
  if (!made.ok) { write(`snapshot: ${made.reason}`); return EXIT.failed; }
  const s = made.value;
  write(`snapshot ${v.stack}: ${s.dir}\n  tree sha256 ${s.treeSha256}\n  size ${s.sizeBytes} bytes\n  parts ${JSON.stringify(s.parts)}\n  packages ${s.packages.length}\n  record ${JSON.stringify(snapshotRecord(s))}`);
  if (!v.register) return EXIT.done;
  const registered = registerSnapshot({ file: path.join(SPIKE, 'inputs', 'cache-snapshots.json'), stack: v.stack, record: snapshotRecord(s), madeOn: new Date().toISOString().slice(0, 10) });
  write(registered.ok ? 'registered in inputs/cache-snapshots.json (commit it with the pre-registration)' : `register: ${registered.reason}`);
  return registered.ok ? EXIT.done : EXIT.config;
}

async function cmdRun(v, write, { resume }, seams = {}) {
  const early = resume ? afterStopRefusal(v) : null;
  if (early) { write(early); return EXIT.config; }
  const seriesDir = path.resolve(v.series ?? '');
  const read = readRunMap(path.join(seriesDir, 'run-map.json'));
  if (!v.series || !read.ok) { write(`usage error: --series <dir> with a run-map.json (${read.reason ?? 'missing'})`); return EXIT.config; }
  const what = parseWhat(v.what ?? whatOfMap(read.value));
  if (!what) { write('usage error: --what aa | pilot-node | rehearse-v2 | pair:<1-3>'); return EXIT.config; }
  const dry = v['dry-run'] === true;
  const rehearsing = what.kind === 'rehearsal';
  if (rehearsing && rehearsalRefusal(read.value)) { write(rehearsalRefusal(read.value)); return EXIT.config; }
  const inputsDir = v['inputs-dir'] ? path.resolve(v['inputs-dir']) : path.join(SPIKE, 'inputs');
  let map = read.value;
  if (!dry) {
    // the pins are read from DISK now and compared with the run-map (step 11), at every paid run and resume, before anything is built or read
    const pins = await checkPins({
      map, inputsDir, repo: seams.repo ?? REPO, acceptHarness: v['accept-harness'] ? { identity: v['accept-harness'], amendmentDate: v.amendment } : null,
      runMapFile: path.join(seriesDir, 'run-map.json'), ledgerFile: path.join(seriesDir, 'ledger.ndjson'),
    });
    if (!pins.ok) { write(`${resume ? 'resume' : 'run'} refused: ${pins.reason}`); return EXIT.config; }
    map = pins.value.map;
    if (resume) map = recordAfterStop({ map, rule: v['after-stop'], reason: v.reason, runMapFile: path.join(seriesDir, 'run-map.json'), ledgerFile: path.join(seriesDir, 'ledger.ndjson') });
  }
  const built = await buildRun({ seriesDir, map, kind: what.kind, pair: what.pair ?? 0, args: { inputsDir: v['inputs-dir'] ? inputsDir : undefined, acceptNoisy: v['accept-noisy'] }, paid: !dry && !rehearsing, seams }); // a rehearsal never reads the token file
  if (!built.ok) { write(`cannot start: ${built.reason}`); return EXIT.config; }
  const { ctx, deps, abort } = built.value;
  if (dry && rehearsing) { write(`rehearse-v2: would rehearse the v2 path on the tree of twin ${twinOf(map).rid}: a v2 workspace filled from it, a stub CLI in the real box behind the real broker (a loopback stub upstream, a fake token), the real scorer at phase v2, then G-14; nothing is paid and no real token is read`); write('dry run: nothing was claimed, started or spent'); return EXIT.done; }
  if (dry) { dryRunLines({ map, ctx, what }).forEach(write); write('dry run: nothing was claimed, started or spent'); return EXIT.done; }
  const ctl = new AbortController();
  for (const [sig, code] of [['SIGINT', 130], ['SIGTERM', 143]]) process.on(sig, () => { abort.reason = sig; abort.exit = code; ctl.abort(); });
  if (resume) {
    const settled = await reconcile({ ctx, deps, map, say: write });
    if (!settled.ok) { write(`resume: ${settled.reason}`); return settled.exit ?? EXIT.failed; }
    map = settled.value;
  }
  if (rehearsing) {
    const rehearsed = await rehearseV2({ ctx, deps, map, signal: ctl.signal, say: write });
    write(`${rehearsed.exit === EXIT.done ? 'done' : 'halted'}: ${rehearsed.message}`);
    return rehearsed.exit;
  }
  if (what.kind !== 'data' && Object.values(map.sessions).every(s => s.kind !== what.kind)) map = planFixed(map, what.kind);
  const r = await runLoop({ ctx, deps: { ...deps, phase: deps.phase }, map, signal: ctl.signal, say: write });
  write(`${r.exit === EXIT.done ? 'done' : 'halted'}: ${r.message}`);
  return r.exit;
}

/** `--what` for a resume without one: the first fixed list or pair with a session not yet scored. */
function whatOfMap(map) {
  const open = Object.values(map.sessions).find(s => s.status !== 'scored' && !(s.status === 'ran' && s.data === false));
  return open ? (open.kind === 'data' ? `pair:${open.pair}` : open.kind === 'aa' ? 'aa' : 'pilot-node') : undefined;
}

/** `--after-stop <rule> --reason <text>` passes a HALT; the series stops of section 5 cannot be passed this way, and a pass without a reason is refused. */
export function afterStopRefusal(v) {
  const rule = v['after-stop'];
  if (!rule) return null;
  if (SERIES_STOPS.some(s => rule.startsWith(s))) return `--after-stop ${rule}: a series stop cannot be passed this way: fix the cause and start a new series with an A/A`;
  return v.reason ? null : `--after-stop ${rule}: needs --reason <text>`;
}

function cmdStatus(v, write) {
  const seriesDir = path.resolve(v.series ?? '');
  const read = readRunMap(path.join(seriesDir, 'run-map.json'));
  if (!v.series || !read.ok) { write(`usage error: --series <dir> with a run-map.json (${read.reason ?? 'missing'})`); return EXIT.config; }
  const map = read.value;
  const allowed = /^Cpus_allowed_list:\s*(\S+)/m.exec(readFileSync('/proc/self/status', 'utf8'))?.[1];
  write(`harness core: Cpus_allowed_list ${allowed} (re-exec marker ${process.env.BENCH_RUNNER_PINNED === '1' ? 'set' : 'not set'})`);
  write(`series ${map.seriesId}${map.stopped ? `, STOPPED: ${map.stopped.rule}` : ''}; prereg ${map.preregTag}; harness ${map.harnessIdentity.slice(0, 12)}`);
  const t = spendTotals(readLedger(path.join(seriesDir, 'ledger.ndjson')));
  write(`spend: ceiling-side $${t.ceilingUsd.toFixed(4)} of $${map.ceilingUsd}, bare $${t.bareUsd.toFixed(4)} of $${map.bareBudgetUsd}`);
  for (const s of Object.values(map.sessions)) write(`  ${s.rid} pair ${s.pair} ${s.stack} ${s.phase} a${s.attempt} [${s.kind}] ${s.status}${s.class ? `: ${s.class}` : ''}${s.label ? ` (${s.label})` : ''}`);
  return EXIT.done;
}

/** The whole command; `write` receives each output line. `seams` (`{ repo, home }`) are for tests only: a run uses this repository and the operator's home. Resolves to the exit code. */
export async function main(argv, write = line => process.stdout.write(`${line}\n`), seams = {}) {
  const [command, ...rest] = argv;
  let values;
  try { values = parseArgs({ args: rest, options: OPTIONS, allowPositionals: false }).values; } catch (e) { write(`usage error: ${e.message}`); return EXIT.config; }
  const table = {
    init: () => cmdInit(values, write), snapshot: () => cmdSnapshot(values, write), status: () => cmdStatus(values, write), run: () => cmdRun(values, write, { resume: false }, seams), resume: () => cmdRun(values, write, { resume: true }, seams),
    'seed-commit': () => { const r = commitSeed({ seedFile: values['seed-file'] ?? defaults().seedFile, inputsDir: path.resolve(values['inputs-dir'] ?? path.join(SPIKE, 'inputs')) }); write(r.ok ? `seed committed: its hash ${r.value.hash} is in inputs/seed.sha256; the seed itself is in ${values['seed-file'] ?? defaults().seedFile} (mode 600)` : `seed-commit: ${r.reason}`); return r.ok ? EXIT.done : EXIT.config; },
    'seed-reveal': () => { const r = revealSeed({ seedFile: values['seed-file'] ?? defaults().seedFile, inputsDir: path.resolve(values['inputs-dir'] ?? path.join(SPIKE, 'inputs')) }); write(r.ok ? 'seed revealed to inputs/seed.txt: commit and push it before pair 1' : `seed-reveal: ${r.reason}`); return r.ok ? EXIT.done : EXIT.config; },
  };
  if (!Object.hasOwn(table, command ?? '')) { write(`usage error: the commands are ${Object.keys(table).join(', ')}`); return EXIT.config; }
  return table[command]();
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

