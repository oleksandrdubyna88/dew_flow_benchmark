// The real wiring of a bare run (bare-config.mjs, bare-runner.mjs), through `main` and `buildRun` themselves: nothing of the runner is replaced by a stub here, only the
// paid model (never reached: no token file exists in the fake home) and the network (the fetched-inputs cache and a local bare origin). The review findings M1, M2, M3
// and the guard were missed because the tests stubbed exactly this layer.
import { realHome } from './fake-home.mjs'; // first: the runner reads os.homedir(); a fake HOME keeps every test off the operator's files (N18)
import assert from 'node:assert/strict';
import { cpSync, existsSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { main } from '../bare-runner.mjs';
import { BARE_CAPS } from '../lib/bare-plan.mjs';
import { buildRun } from '../lib/bare-config.mjs';
import { readLedger } from '../lib/ledger.mjs';
import { harnessIdentity } from '../lib/prereg-guard.mjs';
import { openSeries, readRunMap, writeRunMap } from '../lib/run-map.mjs';
import { INPUTS, SEED, TAG, commit, fixture, sha256, write } from './prereg-fixtures.mjs';
import { spikeDir, tmpDir } from './helpers.mjs';
import { context, sleep } from './bare-ctx.mjs';
import { SCORE, lay } from './bare-fixture.mjs';
import { ridOf } from '../lib/bare-plan.mjs';
import { runLoop } from '../lib/bare-series.mjs';

const REGISTERED = ['stacks.json', 'prices-2026-10-29.json', 'crosscheck-policy.json', 'quiet.json', 'package-flags.json'];
const SNAPSHOTS = { csharp: { treeSha256: 'f'.repeat(64), parts: { nuget: 'a' }, packages: [] }, node: { treeSha256: '1'.repeat(64), parts: { npm: 'b' }, packages: [] } };
const AMENDMENTS = '# Dated amendments\n';
const text = file => readFileSync(file, 'utf8');

/** A git repo (tagged, with a bare origin), an inputs folder as a run reads it, a fake home with NO token and a series whose run-map pins exactly what is on disk. */
async function wiring({ map: over = {}, amendments = AMENDMENTS, quiet = {} } = {}) {
  const repo = fixture();
  const base = context();
  const inputs = tmpDir('inputs');
  for (const f of REGISTERED) cpSync(path.join(spikeDir, 'inputs', f), path.join(inputs, f));
  writeFileSync(path.join(inputs, 'quiet.json'), JSON.stringify({ ...JSON.parse(text(path.join(inputs, 'quiet.json'))), ...quiet }));
  writeFileSync(path.join(inputs, 'cache-snapshots.json'), JSON.stringify({ csharp: { ...SNAPSHOTS.csharp, parts: base.snapshots.csharp.parts }, node: { ...SNAPSHOTS.node, parts: base.snapshots.node.parts } }));
  writeFileSync(path.join(inputs, 'seed.sha256'), `${sha256(SEED)}\n`);
  writeFileSync(path.join(inputs, 'seed.txt'), `${SEED}\n`);
  writeFileSync(path.join(inputs, 'amendments.md'), amendments);
  const stacks = JSON.parse(text(path.join(inputs, 'stacks.json')));
  const home = tmpDir('home');
  const seriesDir = path.join(tmpDir('rsd'), 's-20261030-ab12');
  for (const d of ['s', 'pairs', 'canaries']) mkdirSync(path.join(seriesDir, d), { recursive: true });
  const { sessions = {}, stopped = null, ...fields } = over;
  const map = { ...openSeries({
    seriesId: 's-20261030-ab12', seedSha256: sha256(SEED), harnessIdentity: (await harnessIdentity(repo.repo)).value, preregTag: TAG, preregCommit: repo.sha, pins: stacks.pins,
    stacksSha256: sha256(text(path.join(inputs, 'stacks.json'))), pricesFile: 'prices-2026-10-29.json', pricesSha256: sha256(text(path.join(inputs, 'prices-2026-10-29.json'))),
    quietSha256: sha256(text(path.join(inputs, 'quiet.json'))), packageFlagsSha256: sha256(text(path.join(inputs, 'package-flags.json'))),
    snapshots: { csharp: SNAPSHOTS.csharp.treeSha256, node: SNAPSHOTS.node.treeSha256 }, snapshotDirs: { csharp: base.snapshots.csharp.dir, node: base.snapshots.node.dir },
    claude: { path: '/nowhere/claude-2.1.310', sha256: 'x', version: '2.1.310' }, caps: BARE_CAPS, bareBudgetUsd: 40, ceilingUsd: 500, spin: { medianMs: 20 }, ...fields,
  }), sessions, stopped };
  writeRunMap(path.join(seriesDir, 'run-map.json'), map);
  return { ...repo, inputs, home, seriesDir, map, runMapFile: path.join(seriesDir, 'run-map.json'), ledgerFile: path.join(seriesDir, 'ledger.ndjson') };
}
const resume = async (w, extra = []) => {
  const lines = [];
  const code = await main(['resume', '--series', w.seriesDir, '--what', 'pair:1', '--inputs-dir', w.inputs, ...extra], l => lines.push(l), { repo: w.repo, home: w.home });
  return { code, out: lines.join('\n') };
};

test('A_resume_compares_the_run_map_with_what_is_on_disk_now', async () => {
  const control = await wiring();
  const ok = await resume(control);
  assert.doesNotMatch(ok.out, /resume refused/, `control: nothing changed, so nothing is refused: ${ok.out}`);
  const drifts = {
    stacksSha256: w => writeFileSync(path.join(w.inputs, 'stacks.json'), `${text(path.join(w.inputs, 'stacks.json'))} `),
    pricesSha256: w => writeFileSync(path.join(w.inputs, 'prices-2026-10-29.json'), `${text(path.join(w.inputs, 'prices-2026-10-29.json'))} `),
    snapshots: w => writeFileSync(path.join(w.inputs, 'cache-snapshots.json'), JSON.stringify({ ...SNAPSHOTS, node: { ...SNAPSHOTS.node, treeSha256: '2'.repeat(64) } })),
    seedSha256: w => writeFileSync(path.join(w.inputs, 'seed.sha256'), `${sha256('mg-ffffffffffffffff')}\n`),
    pins: w => writeFileSync(path.join(w.inputs, 'stacks.json'), text(path.join(w.inputs, 'stacks.json')).replace('"2.1.310"', '"2.1.311"')),
    quietSha256: w => writeFileSync(path.join(w.inputs, 'quiet.json'), `${text(path.join(w.inputs, 'quiet.json'))} `),
    packageFlagsSha256: w => writeFileSync(path.join(w.inputs, 'package-flags.json'), `${text(path.join(w.inputs, 'package-flags.json'))} `),
  };
  for (const [field, change] of Object.entries(drifts)) {
    const w = await wiring();
    change(w);
    const r = await resume(w);
    assert.equal(r.code, 4, `${field}: ${r.out}`);
    assert.match(r.out, new RegExp(`resume refused: ${field} differs from the run-map`), field);
  }
  const noTag = await wiring({ map: { preregTag: 'prereg/minigit-bare-20990101' } });
  assert.match((await resume(noTag)).out, /resume refused: preregTag differs/, 'the run-map names a tag this clone does not hold');
  const code = await wiring();
  write(code.repo, 'spikes/minigit-bench/lib/x.mjs', '// a code change after init\n');
  commit(code.repo, 'a harness change');
  assert.match((await resume(code)).out, /resume refused: harnessIdentity changed .*--accept-harness/, 'the harness on disk is not the one the run-map pinned');
});

test('An_accepted_harness_is_written_to_the_run_map_and_the_ledger_once_and_the_guard_reads_it', async () => {
  const w = await wiring();
  write(w.repo, 'spikes/minigit-bench/lib/x.mjs', '// a harness fix after the A/A\n');
  commit(w.repo, 'harness fix');
  const now = (await harnessIdentity(w.repo)).value;
  writeFileSync(path.join(w.inputs, 'amendments.md'), `${AMENDMENTS}2026-11-02 | instrument | harness:${now.slice(0, 12)} | the sampler fix (G-11)\n`);
  const args = ['--accept-harness', now, '--amendment', '2026-11-02'];
  const first = await resume(w, args);
  assert.doesNotMatch(first.out, /resume refused/, first.out);
  assert.deepEqual(readRunMap(w.runMapFile).value.harnessAccepted, [now], 'the accepted identity is in the run-map');
  const lines = () => readLedger(w.ledgerFile).filter(l => l.event === 'accept-harness');
  assert.equal(lines().length, 1);
  assert.equal(lines()[0].to, now);
  assert.equal(lines()[0].amendment, '2026-11-02');
  await resume(w, args);
  assert.equal(lines().length, 1, 'accepting the same identity twice writes one ledger line');
  const again = await resume(w);
  assert.doesNotMatch(again.out, /resume refused/, 'an accepted identity needs no flag on the next resume');
  // through buildRun: the guard reads the run-map FROM DISK at every call, and the context holds the identity that is running, not the pinned one
  const built = await buildRun({ seriesDir: w.seriesDir, map: readRunMap(w.runMapFile).value, kind: 'data', pair: 1, args: { inputsDir: w.inputs }, paid: false, seams: { repo: w.repo, home: w.home } });
  assert.equal(built.ok, true, built.reason);
  assert.equal(built.value.ctx.harnessIdentity, now, 'the identity recorded with a session is the one that ran');
  const guard = await built.value.deps.guard({ paid: true, requireSeed: false });
  assert.equal(guard.ok, true, guard.reason);
});

test('A_changed_harness_without_an_accepting_amendment_line_is_refused', async () => {
  const w = await wiring();
  write(w.repo, 'spikes/minigit-bench/lib/x.mjs', '// changed\n');
  commit(w.repo, 'harness change');
  const now = (await harnessIdentity(w.repo)).value;
  const r = await resume(w, ['--accept-harness', now, '--amendment', '2026-11-02']);
  assert.match(r.out, /amendments\.md has no instrument line dated 2026-11-02 that names harness:/);
  assert.equal(readRunMap(w.runMapFile).value.harnessAccepted, undefined, 'a refused acceptance writes nothing');
  assert.equal(existsSync(w.ledgerFile), false);
});

// ---- the limits of the series (review m2) -------------------------------------------------------------------------------------------

const run = async (w, extra = []) => {
  const lines = [];
  const code = await main(['run', '--series', w.seriesDir, '--what', 'pair:1', '--inputs-dir', w.inputs, ...extra], l => lines.push(l), { repo: w.repo, home: w.home });
  return { code, out: lines.join('\n') };
};

test('Every_run_checks_the_run_map_caps_and_budgets_against_the_registered_values', async () => {
  const control = await run(await wiring());
  assert.doesNotMatch(control.out, /refused/, `control: the registered values pass: ${control.out}`);
  const tampered = {
    'caps differ': { caps: { ...BARE_CAPS, capUsd: 60 } },
    'bareBudgetUsd is 400': { bareBudgetUsd: 400 },
    'ceilingUsd is 5000': { ceilingUsd: 5000 },
  };
  for (const [why, map] of Object.entries(tampered)) {
    const r = await run(await wiring({ map }));
    assert.equal(r.code, 4, why);
    assert.match(r.out, /run refused: the run-map .*registered/, `${why}: ${r.out}`);
  }
});

const RAISE = '2026-11-05 | budget-raise | pair:1 | n:2 | usd:13.2 | the two v1 voids of pair 1 are re-run';
const startedPair1 = { sessions: { aaaaaa: { rid: 'aaaaaa', pair: 1, stack: 'csharp', phase: 'v1', attempt: 1, kind: 'data', status: 'ran', data: false } } };

async function budgetOf({ amendments, map = startedPair1, pair = 1 }) {
  const w = await wiring({ amendments, map });
  return buildRun({ seriesDir: w.seriesDir, map: readRunMap(w.runMapFile).value, kind: 'data', pair, args: { inputsDir: w.inputs }, paid: false, seams: { repo: w.repo, home: w.home } });
}

test('The_bare_budget_is_raised_once_by_the_worst_case_of_the_re_runs_of_a_pair_already_started_as_a_dated_line', async () => {
  const plain = await budgetOf({});
  assert.equal(plain.value.ctx.bareBudgetUsd, 40, 'no line, no raise');
  const raised = await budgetOf({ amendments: `${AMENDMENTS}${RAISE}\n` });
  assert.equal(raised.ok, true, raised.reason);
  assert.equal(raised.value.ctx.bareBudgetUsd, 53.2, '40 + 2 re-runs x 1.1 x $6');
  const other = await budgetOf({ amendments: `${AMENDMENTS}${RAISE}\n`, pair: 2 });
  assert.equal(other.value.ctx.bareBudgetUsd, 40, 'the raise finishes the re-runs of ITS pair only');
  const wrong = await budgetOf({ amendments: `${AMENDMENTS}2026-11-05 | budget-raise | pair:1 | n:2 | usd:30 | more room\n` });
  assert.equal(wrong.ok, false);
  assert.match(wrong.reason, /budget-raise.*13\.2/, 'only the worst case of the re-runs, never a free amount');
  const twice = await budgetOf({ amendments: `${AMENDMENTS}${RAISE}\n2026-11-06 | budget-raise | pair:1 | n:1 | usd:6.6 | once more\n` });
  assert.equal(twice.ok, false);
  assert.match(twice.reason, /raised once/, 'the $40 is raised ONCE');
  const notStarted = await budgetOf({ amendments: `${AMENDMENTS}${RAISE}\n`, map: {} });
  assert.equal(notStarted.ok, false);
  assert.match(notStarted.reason, /already started/, 'a pair that has not started has no re-runs to finish');
});

// ---- the sampler of a phase (review M1) ---------------------------------------------------------------------------------------------

/** A process table with ONE busy process whose environment is `environ` (every read of its stat adds a CPU-second): the host side of the sampler, stubbed. */
const busyHost = (environ, cmdline = 'node\0worker.js') => {
  let ticks = 0;
  return {
    pids: () => ['4242'],
    read: (pid, file) => {
      if (file === 'stat') return `${pid} (worker) S 1 1 1 0 0 0 0 0 0 0 ${(ticks += 100)} 0`;
      return { cmdline, status: 'Uid:\t1000\t1000\nCpus_allowed_list:\t0-5\n', environ: environ.join('\0') }[file];
    },
    signal: () => {},
  };
};

/** One v1 phase through the REAL buildRun wiring and runLoop; only the model (a stub launch), the scorer and the host's process table are replaced. Resolves to the v1 sessions' run-map entries. */
async function onePhase({ environOf }) {
  const w = await wiring({ quiet: { intervalMs: 20, spinIterations: 1000, windowMs: 100, recheckMs: 20 } });
  const procApi = busyHost(environOf(ridOf({ seed: SEED, pair: 1, stack: 'csharp', phase: 'v1', attempt: 1 })));
  const built = await buildRun({ seriesDir: w.seriesDir, map: readRunMap(w.runMapFile).value, kind: 'data', pair: 1, args: { inputsDir: w.inputs }, paid: false, seams: { repo: w.repo, home: w.home, procApi } });
  assert.equal(built.ok, true, built.reason);
  const { ctx, deps } = built.value;
  const driven = {
    ...deps, guard: async () => ({ ok: true }), retireStale: async () => {}, preflight: async () => ({ ok: true }), quiet: async () => ({ ok: true, value: { quiet: true, flags: [] } }),
    scoreSession: async () => ({ kind: 'environment', reason: 'stop after the launch' }),
    phase: {
      maskCheckMs: 1, procApi, listProjectDirs: () => [], spawn: () => ({}),
      launchSession: async (plan, d) => { d.spawnBox(['bwrap'], {}); lay(plan.layout, undefined); await sleep(250); return { ok: true, value: { meta: JSON.parse(readFileSync(plan.layout.files.meta, 'utf8')), exit: { code: 0, signal: null } } }; },
    },
  };
  const r = await runLoop({ ctx: { ...ctx, ceilingUsd: 500, bareBudgetUsd: 40 }, deps: driven, map: readRunMap(w.runMapFile).value });
  const samples = readFileSync(path.join(w.seriesDir, 'pairs', 'p1-v1-cpu.ndjson'), 'utf8').split('\n').filter(Boolean).map(l => JSON.parse(l));
  return { r, samples, map: readRunMap(w.runMapFile).value };
}

test('The_sampler_counts_the_cells_of_the_phase_as_ours_and_a_stranger_as_foreign', async () => {
  const ours = await onePhase({ environOf: rid => [`BENCH_CELL=cell-${rid}`] });
  assert.ok(ours.samples.length >= 2, `the phase sampler wrote ${ours.samples.length} samples`);
  assert.ok(ours.samples.every(s => s.foreignCpus < 0.5), `a process of the phase's own cell is not foreign load: ${ours.samples.map(s => s.foreignCpus.toFixed(1)).join()}`);
  assert.ok(Object.values(ours.map.sessions).every(s => !s.run.flags.includes('foreign load')), 'no foreign load flag on a quiet host');
  const stranger = await onePhase({ environOf: () => ['BENCH_CELL=cell-someone-elses'] });
  assert.ok(stranger.samples.some(s => s.foreignCpus > 1), 'control: the same process under another cell id IS foreign');
  assert.ok(Object.values(stranger.map.sessions).every(s => s.run.flags.includes('foreign load')));
});

test('A_wait_for_a_quiet_machine_tells_the_operator_each_retry_through_the_output_callback', async () => {
  const w = await wiring({ quiet: { intervalMs: 10, windowMs: 20, recheckMs: 15, maxWaitMs: 800, spinIterations: 1000 } });
  const procApi = busyHost([], 'claude\0-p\0someone elses session');
  const built = await buildRun({ seriesDir: w.seriesDir, map: readRunMap(w.runMapFile).value, kind: 'data', pair: 1, args: { inputsDir: w.inputs }, paid: false, seams: { repo: w.repo, home: w.home, procApi } });
  assert.equal(built.ok, true, built.reason);
  const told = [];
  const r = await built.value.deps.quiet({ phase: 'v1', v1Quiet: null, cellIds: [], say: l => told.push(l) });
  assert.equal(r.ok, false, 'a foreign claude -p never goes away: the wait gives up');
  assert.ok(told.length >= 1, 'the operator was told while it waited');
  assert.ok(told.every(l => /not quiet/.test(l) && /foreign-agent/.test(l) && /waited \d+ s/.test(l) && /next check in \d+ s/.test(l)), told.join('\n'));
});

test('A_cancel_during_the_quiet_wait_of_the_real_wiring_ends_it_at_once', async () => {
  const w = await wiring({ quiet: { intervalMs: 1000, windowMs: 60000, recheckMs: 60000, maxWaitMs: 1800000, spinIterations: 1000 } });
  const procApi = busyHost([], 'claude\0-p\0someone elses session');
  const built = await buildRun({ seriesDir: w.seriesDir, map: readRunMap(w.runMapFile).value, kind: 'data', pair: 1, args: { inputsDir: w.inputs }, paid: false, seams: { repo: w.repo, home: w.home, procApi } });
  assert.equal(built.ok, true, built.reason);
  const ctl = new AbortController();
  setTimeout(() => ctl.abort(), 150);
  const started = Date.now();
  const r = await built.value.deps.quiet({ phase: 'v1', v1Quiet: null, cellIds: [], say: () => {}, signal: ctl.signal });
  assert.ok(Date.now() - started < 5000, `the 60 s window and the 30 min wait were cut short (${Date.now() - started} ms)`);
  assert.equal(r.ok, false);
  assert.match(r.reason, /cancelled/);
});

test('A_halt_passed_with_after_stop_is_recorded_in_the_run_map_and_the_ledger_once', async () => {
  const w = await wiring();
  const args = ['--after-stop', 'void: rate limit', '--reason', 'the subscription window reset at 05:00'];
  const first = await resume(w, args);
  assert.doesNotMatch(first.out, /resume refused|needs --reason/, first.out);
  const stops = () => readRunMap(w.runMapFile).value.afterStops ?? [];
  assert.equal(stops().length, 1);
  assert.equal(stops()[0].rule, 'void: rate limit');
  assert.equal(stops()[0].reason, 'the subscription window reset at 05:00');
  const events = () => readLedger(w.ledgerFile).filter(l => l.event === 'after-stop');
  assert.equal(events().length, 1);
  assert.equal(events()[0].rule, 'void: rate limit');
  await resume(w, args);
  assert.equal(stops().length, 1, 'the same pass repeated (a crash, a retry) is recorded once');
  assert.equal(events().length, 1);
  const refused = await resume(w, ['--after-stop', 'stop: auth', '--reason', 'it was a typo']);
  assert.match(refused.out, /series stop cannot be passed/);
  assert.equal(stops().length, 1, 'a refused pass writes nothing');
});

test('A_paid_build_with_the_operators_real_home_is_refused_by_the_token_reader_before_any_file_is_looked_at', async () => {
  const w = await wiring();
  const build = home => buildRun({ seriesDir: w.seriesDir, map: readRunMap(w.runMapFile).value, kind: 'data', pair: 1, args: { inputsDir: w.inputs }, paid: true, seams: { repo: w.repo, home } });
  await assert.rejects(build(realHome), /test runner.*real home/s, 'the real home through the real wiring');
  const fake = await build(w.home);
  assert.equal(fake.ok, false, 'control: the fake home has no token file');
  assert.match(fake.reason, /no token/);
});

// ---- an input amendment through the real wiring (contract section 10; review N5) --------------------------------------------------------

const editQuiet = w => { const file = path.join(w.inputs, 'quiet.json'); writeFileSync(file, `${JSON.stringify({ ...JSON.parse(text(file)), recheckMs: 30_000 }, null, 2)}\n`); return sha256(text(file)); };

test('An_amended_quiet_file_is_accepted_through_resume_once_recorded_in_the_run_map_and_the_ledger_and_named_by_every_record', async () => {
  const w = await wiring();
  const sha = editQuiet(w);
  const refused = await resume(w);
  assert.equal(refused.code, 4);
  assert.match(refused.out, /resume refused: quietSha256 differs from the run-map/);
  assert.match(refused.out, new RegExp(`input:quiet\\.json:${sha.slice(0, 12)}`), 'the refusal prints the line to add');
  writeFileSync(path.join(w.inputs, 'amendments.md'), `${AMENDMENTS}2026-11-02 | instrument | input:quiet.json:${sha.slice(0, 12)} | the sampler window was too tight (G-11)\n`);
  const first = await resume(w);
  assert.doesNotMatch(first.out, /resume refused/, first.out);
  const map = readRunMap(w.runMapFile).value;
  assert.deepEqual(map.inputsAccepted.map(i => [i.file, i.sha256, i.date]), [['quiet.json', sha, '2026-11-02']], 'the run-map says which thresholds applied');
  assert.notEqual(map.quietSha256, sha, 'the pin itself stays the init value; the accepted content is listed beside it');
  const events = readLedger(w.ledgerFile).filter(l => l.event === 'accept-input');
  assert.deepEqual(events.map(e => [e.file, e.sha256, e.amendment]), [['quiet.json', sha, '2026-11-02']]);
  await resume(w);
  assert.equal(readLedger(w.ledgerFile).filter(l => l.event === 'accept-input').length, 1, 'accepting the same content twice writes one line');
  // the run's context names the thresholds that applied; a session record carries them
  const built = await buildRun({ seriesDir: w.seriesDir, map: readRunMap(w.runMapFile).value, kind: 'data', pair: 1, args: { inputsDir: w.inputs }, paid: false, seams: { repo: w.repo, home: w.home } });
  assert.equal(built.ok, true, built.reason);
  assert.equal(built.value.ctx.inputHashes.quietSha256, sha, 'the quiet.json that applied to the sessions of this run');
});

test('An_input_amendment_after_a_data_session_exists_is_refused_by_resume', async () => {
  const w = await wiring({ map: { sessions: { aaaaaa: { rid: 'aaaaaa', pair: 1, stack: 'csharp', phase: 'v1', attempt: 1, kind: 'data', label: 'paired', status: 'planned' } } } });
  const sha = editQuiet(w);
  writeFileSync(path.join(w.inputs, 'amendments.md'), `${AMENDMENTS}2026-11-02 | instrument | input:quiet.json:${sha.slice(0, 12)} | the sampler window (G-11)\n`);
  const r = await resume(w);
  assert.equal(r.code, 4);
  assert.match(r.out, /data session exists.*before pair 1/s);
  assert.equal(readRunMap(w.runMapFile).value.inputsAccepted, undefined, 'a refused amendment writes nothing');
});

test('Resume_settle_only_is_not_refused_on_a_stopped_series_launches_nothing_and_a_plain_resume_is_refused', async () => {
  const w = await wiring({ map: { stopped: { rule: 'stop: auth', reason: '{}', at: 't' } } });
  const plain = await resume(w);
  assert.equal(plain.code, 4);
  assert.match(plain.out, /the series is stopped \(stop: auth\).*NEW series/s);
  const tokenDir = path.join(w.home, '.config', 'minigit-bench');
  mkdirSync(tokenDir, { recursive: true, mode: 0o700 });
  writeFileSync(path.join(tokenDir, 'claude-oauth-token'), 'sk-ant-fake-wiring-token-0123456789\n', { mode: 0o600 }); // settling scans the evidence for the real token, so the run reads it (a fake one, in a fake home)
  const before = readRunMap(w.runMapFile).value;
  const settle = await resume(w, ['--settle-only']);
  assert.equal(settle.code, 0, settle.out);
  assert.match(settle.out, /settle-only: .*nothing was launched/);
  assert.match(settle.out, /series remains stopped \(stop: auth\)/);
  assert.deepEqual(readRunMap(w.runMapFile).value.sessions, before.sessions);
  const run = async extra => { const lines = []; const code = await main(['run', '--series', w.seriesDir, '--what', 'pair:1', '--inputs-dir', w.inputs, ...extra], l => lines.push(l), { repo: w.repo, home: w.home }); return { code, out: lines.join('\n') }; };
  const misuse = await run(['--settle-only']);
  assert.equal(misuse.code, 4, '--settle-only belongs to resume');
  assert.match(misuse.out, /--settle-only/);
});

// ---- one runner per series (review N13) ---------------------------------------------------------------------------------------------

test('A_run_or_resume_takes_the_series_lock_refuses_while_it_is_held_and_leaves_none_behind', async () => {
  const w = await wiring();
  const lock = path.join(w.seriesDir, 'series.lock');
  const { processStartTime } = await import('../lib/session-state.mjs');
  writeFileSync(lock, `${JSON.stringify({ pid: process.pid, startTime: processStartTime(process.pid), at: 't' })}\n`); // another runner, alive
  for (const verb of ['run', 'resume']) {
    const lines = [];
    const code = await main([verb, '--series', w.seriesDir, '--what', 'pair:1', '--inputs-dir', w.inputs], l => lines.push(l), { repo: w.repo, home: w.home });
    assert.equal(code, 4, verb);
    assert.match(lines.join('\n'), new RegExp(`${verb}: the series ${path.basename(w.seriesDir)} is in use .*pid ${process.pid}`), verb);
  }
  const dry = await run(w, ['--dry-run']);
  assert.equal(dry.code, 0, 'a dry run writes nothing and takes no lock');
  const status = [];
  assert.equal(await main(['status', '--series', w.seriesDir], l => status.push(l)), 0, 'status only reads');
  rmSync(lock);
  const free = await run(w);
  assert.doesNotMatch(free.out, /is in use/, free.out);
  assert.equal(existsSync(lock), false, 'the lock is released when the command ends, whatever it ended with');
});
