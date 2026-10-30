// The real wiring of a bare run (bare-config.mjs, bare-runner.mjs), through `main` and `buildRun` themselves: nothing of the runner is replaced by a stub here, only the
// paid model (never reached: no token file exists in the fake home) and the network (the fetched-inputs cache and a local bare origin). The review findings M1, M2, M3
// and the guard were missed because the tests stubbed exactly this layer.
import assert from 'node:assert/strict';
import { cpSync, existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
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
  const { sessions = {}, ...fields } = over;
  const map = { ...openSeries({
    seriesId: 's-20261030-ab12', seedSha256: sha256(SEED), harnessIdentity: (await harnessIdentity(repo.repo)).value, preregTag: TAG, preregCommit: repo.sha, pins: stacks.pins,
    stacksSha256: sha256(text(path.join(inputs, 'stacks.json'))), pricesFile: 'prices-2026-10-29.json', pricesSha256: sha256(text(path.join(inputs, 'prices-2026-10-29.json'))),
    snapshots: { csharp: SNAPSHOTS.csharp.treeSha256, node: SNAPSHOTS.node.treeSha256 }, snapshotDirs: { csharp: base.snapshots.csharp.dir, node: base.snapshots.node.dir },
    claude: { path: '/nowhere/claude-2.1.310', sha256: 'x', version: '2.1.310' }, caps: BARE_CAPS, bareBudgetUsd: 40, ceilingUsd: 500, spin: { medianMs: 20 }, ...fields,
  }), sessions };
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
  assert.match(r.out, /amendments\.md has no line dated 2026-11-02 that names harness:/);
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
const busyHost = environ => {
  let ticks = 0;
  return {
    pids: () => ['4242'],
    read: (pid, file) => {
      if (file === 'stat') return `${pid} (worker) S 1 1 1 0 0 0 0 0 0 0 ${(ticks += 100)} 0`;
      return { cmdline: 'node\0worker.js', status: 'Uid:\t1000\t1000\nCpus_allowed_list:\t0-5\n', environ: environ.join('\0') }[file];
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
