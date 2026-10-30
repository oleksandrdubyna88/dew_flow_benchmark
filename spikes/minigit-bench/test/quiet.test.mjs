// The quiet check (E3b.S2): wait for a quiet machine before a phase, give up resumably, the noisy override, the sampler's one I/O loop.
import assert from 'node:assert/strict';
import { mkdirSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { cpusOfMask, createSampler, liveSessionDirs, phaseFlags, startPhaseSampler } from '../lib/cpu-sampler.mjs';
import { loadQuiet, noisyOverride, quietWindow, waitForQuiet } from '../lib/quiet.mjs';
import { processStartTime, writeState } from '../lib/session-state.mjs';
import { tmpDir } from './helpers.mjs';

const QUIET_JSON = JSON.parse(readFileSync(new URL('../inputs/quiet.json', import.meta.url), 'utf8'));

test('The_registered_quiet_thresholds_load_and_name_every_limit', () => {
  const q = loadQuiet(QUIET_JSON);
  assert.equal(q.ok, true, q.reason);
  assert.deepEqual([q.value.windowMs, q.value.intervalMs, q.value.meanCpuMax, q.value.sampleCpuMax, q.value.foreignCpuMax, q.value.memAvailMinPct, q.value.spinMaxRatio, q.value.maxWaitMs], [60_000, 5_000, 0.5, 1.5, 1.0, 25, 1.3, 1_800_000]);
  assert.ok(Number.isInteger(q.value.spinIterations) && q.value.spinIterations > 0);
  assert.ok(q.value.agentPatterns.length >= 2);
  const broken = loadQuiet({ ...QUIET_JSON, meanCpuMax: 'half' });
  assert.equal(broken.ok, false);
  assert.match(broken.reason, /meanCpuMax/);
});

/** A fake clock whose `sleep` advances it; `check` costs one window (60 s) of it. */
function clock() {
  let t = 0;
  return { now: () => t, sleep: async ms => { t += ms; }, advance: ms => { t += ms; } };
}

test('The_wait_retries_then_gives_up_resumably', async () => {
  const c = clock();
  const bad = { quiet: false, failures: [{ condition: 'host-cpu', detail: 'x' }] };
  const never = await waitForQuiet({ check: async () => { c.advance(60_000); return bad; }, maxWaitMs: 1_800_000, recheckMs: 60_000, now: c.now, sleep: c.sleep });
  assert.equal(never.quiet, false);
  assert.equal(never.gaveUp, true);
  assert.deepEqual(never.verdict, bad, 'the last verdict, naming what failed');
  assert.ok(never.waitedMs >= 1_800_000 && never.waitedMs < 1_800_000 + 130_000, `waited ${never.waitedMs}`);

  const c2 = clock();
  const drops = await waitForQuiet({ check: async () => { c2.advance(60_000); return c2.now() > 7 * 60_000 ? { quiet: true, failures: [] } : bad; }, maxWaitMs: 1_800_000, recheckMs: 60_000, now: c2.now, sleep: c2.sleep });
  assert.equal(drops.quiet, true);
  assert.ok(drops.waitedMs >= 7 * 60_000 && drops.waitedMs <= 9 * 60_000, `started after ${drops.waitedMs}`);
});

test('A_quiet_machine_starts_at_once', async () => {
  const c = clock();
  const r = await waitForQuiet({ check: async () => { c.advance(60_000); return { quiet: true, failures: [] }; }, maxWaitMs: 1_800_000, recheckMs: 60_000, now: c.now, sleep: c.sleep });
  assert.equal(r.quiet, true);
  assert.equal(r.waitedMs, 60_000, 'one window and no more');
});

test('Noisy_override_is_flagged_and_refused_for_a_v2_after_a_quiet_v1', () => {
  const v1 = noisyOverride({ phase: 'v1', v1Quiet: null, reason: 'the operator accepts it' });
  assert.equal(v1.ok, true);
  assert.deepEqual(v1.value.flags, ['started not quiet']);
  const v2 = noisyOverride({ phase: 'v2', v1Quiet: true, reason: 'the operator accepts it' });
  assert.equal(v2.ok, false);
  assert.match(v2.reason, /v1 was quiet/);
  assert.equal(noisyOverride({ phase: 'v2', v1Quiet: false, reason: 'v1 was noisy too' }).ok, true);
  assert.equal(noisyOverride({ phase: 'v1', v1Quiet: null, reason: '' }).ok, false, 'a reason is required');
});

test('The_quiet_window_takes_one_sample_per_interval_and_decides_on_them', async () => {
  const c = clock();
  let taken = 0;
  const sampleOnce = async () => { taken += 1; return { hostCpus: 0.1, harnessCpus: 0.05, foreignAgents: [], memAvailablePct: 60, liveSessions: [], spinMs: 20 }; };
  const v = await quietWindow({ thresholds: { ...QUIET_JSON, calibratedMs: 20 }, sampleOnce, sleep: c.sleep, windowMs: 60_000, intervalMs: 5_000 });
  assert.equal(taken, 12);
  assert.equal(c.now(), 55_000, 'the interval is slept between samples, not after the last');
  assert.equal(v.quiet, true);
});

// ---- the sampler's I/O, over fake /proc files -----------------------------------------------------------------------------------

const STAT_A = 'cpu  0 0 0 0 0 0 0 0\ncpu0 0 0 0 100 0 0 0 0\ncpu1 0 0 0 100 0 0 0 0\n';
const STAT_B = 'cpu  0 0 0 0 0 0 0 0\ncpu0 40 0 10 140 0 0 0 0\ncpu1 0 0 0 200 0 0 0 0\n'; // cpu0: 50 busy of 90 ticks

test('The_sampler_reads_the_kernel_files_and_labels_a_foreign_load_as_a_lower_bound', async () => {
  let stat = STAT_A;
  let ticks = 0;
  const files = () => ({
    '/proc/stat': stat,
    '/proc/meminfo': 'MemTotal:       1000 kB\nMemAvailable:    600 kB\n',
    '/proc/pressure/cpu': 'some avg10=1.50 avg60=0.10 avg300=0.00 total=1\nfull avg10=0.00 avg60=0.00 avg300=0.00 total=0\n',
  });
  const procApi = {
    pids: () => ['5', '6'],
    read: (pid, file) => {
      if (pid === '5') return { cmdline: 'node\0runner\0', environ: 'A=1\0', stat: `5 (node) S 1 1 1 0 0 0 0 0 0 0 ${ticks} 0 0 0 0 0`, status: 'Uid:\t1000\t1000\t1000\t1000\n' }[file];
      return { cmdline: 'make\0all\0', environ: 'X=1\0', stat: `6 (make) S 1 1 1 0 0 0 0 0 0 0 ${ticks * 2} 0 0 0 0 0`, status: 'Uid:\t1000\t1000\t1000\t1000\n' }[file];
    },
  };
  const c = clock();
  const sampler = await createSampler({ procApi, readText: p => files()[p], cellIds: ['cell-a'], harnessPids: ['5'], liveSessions: () => [], spin: async () => 20, now: c.now, patterns: QUIET_JSON.agentPatterns });
  stat = STAT_B;
  ticks = 100;
  c.advance(5_000);
  const s = await sampler();
  assert.equal(s.perCpu[0], 50 / 90);
  assert.ok(Math.abs(s.hostCpus - 50 / 90) < 1e-12);
  assert.equal(s.memAvailablePct, 60);
  assert.equal(s.psiSomeAvg10, 1.5);
  assert.ok(Math.abs(s.harnessCpus - 100 / 100 / 5) < 1e-12, 'the harness pid: 100 ticks over 5 s');
  assert.ok(Math.abs(s.foreignCpus - 200 / 100 / 5) < 1e-12, 'pid 6 is neither a cell nor the harness');
  assert.equal(s.foreignCpusLowerBound, true);
  assert.equal(s.spinMs, 20);
  assert.deepEqual(s.foreignAgents, []);
});

test('Mask_groups_are_parsed_and_the_phase_flags_look_at_every_60_s_window', () => {
  assert.deepEqual(cpusOfMask('0-5'), [0, 1, 2, 3, 4, 5]);
  assert.deepEqual(cpusOfMask('6,7'), [6, 7]);
  const t = { foreignCpuMax: 1.0, spinMaxRatio: 1.3, calibratedMs: 20 };
  const quiet = Array.from({ length: 24 }, () => ({ foreignCpus: 0, foreignAgents: 0, spinMs: 20 }));
  const burst = quiet.map((s, i) => (i >= 12 ? { ...s, foreignCpus: 2 } : s)); // only the second minute is loaded
  assert.deepEqual(phaseFlags(quiet, t), []);
  assert.deepEqual(phaseFlags(burst, t), ['foreign load'], 'a loaded minute flags the phase although the whole-phase mean is 1.0 or less');
});

// ---- the live sessions of every series -------------------------------------------------------------------------------------------

test('A_session_of_any_series_with_a_living_owner_is_live_and_a_dead_owner_is_not', () => {
  const root = tmpDir('live');
  const put = (series, rid, state) => {
    mkdirSync(path.join(root, series, 's', rid), { recursive: true });
    writeState(path.join(root, series, 's', rid, 'state.json'), state);
  };
  const base = { phase: 'running', cell: 'c', startedAt: 'x', heartbeatAt: 'x' };
  put('s-1', 'aaaaaa', { ...base, hostPid: process.pid, hostStartTime: processStartTime(process.pid) });
  put('s-2', 'bbbbbb', { ...base, hostPid: 2 ** 22 - 1, hostStartTime: '1' }); // an owner that is gone
  put('s-2', 'cccccc', { ...base, phase: 'closed', hostPid: process.pid, hostStartTime: processStartTime(process.pid) });
  mkdirSync(path.join(root, 'toolchains'), { recursive: true }); // not a series: no s/ directory
  assert.deepEqual(liveSessionDirs(root), [path.join(root, 's-1', 's', 'aaaaaa')]);
});

/** A sleep that advances the fake clock AND yields to the event loop (a loop of bare microtasks would starve the test's own polling). */
const yielding = c => async ms => { c.advance(ms); await new Promise(r => setImmediate(r)); };

// ---- the phase sampler loop ------------------------------------------------------------------------------------------------------

test('The_phase_sampler_appends_a_line_per_interval_and_reports_its_flags_when_stopped', async () => {
  const dir = tmpDir('cpulog');
  const file = path.join(dir, 'p1-cpu.ndjson');
  const c = clock();
  let n = 0;
  const sample = async () => { n += 1; return { t: c.now(), hostCpus: 3, foreignCpus: 2, foreignAgents: [], spinMs: 20, perCpu: [0.5, 0.5], psiSomeAvg10: 1 }; };
  const sampler = startPhaseSampler({ sample, file, intervalMs: 5000, thresholds: { foreignCpuMax: 1.0, spinMaxRatio: 1.3, calibratedMs: 20 }, sleep: yielding(c) });
  while (n < 14) await new Promise(r => setImmediate(r));
  const done = await sampler.stop();
  const lines = readFileSync(file, 'utf8').split('\n').filter(Boolean).map(l => JSON.parse(l));
  assert.ok(lines.length >= 14, `${lines.length} lines`);
  assert.equal(lines[0].foreignCpus, 2);
  assert.deepEqual(done.flags, ['foreign load'], 'twelve samples of 2 CPU foreign load: a loaded minute');
  assert.equal(done.samples, lines.length);
  assert.equal(done.error, null);
});

test('A_sampler_that_throws_is_recorded_and_never_stops_the_phase', async () => {
  const c = clock();
  let n = 0;
  const sampler = startPhaseSampler({ sample: async () => { n += 1; throw new Error('proc vanished'); }, file: path.join(tmpDir('cpulog'), 'x.ndjson'), intervalMs: 5000, thresholds: { foreignCpuMax: 1, spinMaxRatio: 1.3, calibratedMs: 20 }, sleep: yielding(c) });
  while (n < 1) await new Promise(r => setImmediate(r));
  const done = await sampler.stop();
  assert.match(done.error, /proc vanished/);
  assert.equal(done.samples, 0);
  assert.deepEqual(done.flags, [], 'no sample, no flag; the record says the sampler failed');
});
