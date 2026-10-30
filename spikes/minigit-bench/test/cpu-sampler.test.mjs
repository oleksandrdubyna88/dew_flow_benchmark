// The CPU sampler, the foreign-agent finder and the spin probe (E3b.S2). Pure functions over /proc-shaped fixtures, and two effect tests on the real kernel.
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { test } from 'node:test';
import { calibrateSpin, duringFlags, foreignAgents, foreignCpu, hostBusy, maskObserved, parseProcStat, quietVerdict, spinMedianRatio } from '../lib/cpu-sampler.mjs';

const THRESHOLDS = Object.freeze({
  meanCpuMax: 0.5, sampleCpuMax: 1.5, foreignCpuMax: 1.0, memAvailMinPct: 25, spinMaxRatio: 1.3,
  agentPatterns: [
    { name: 'claude', exe: ['claude'], pathIncludes: ['claude/versions/', '@anthropic-ai/claude-code'], argAny: ['-p', '--print'] },
    { name: 'codex', exe: ['codex'], pathIncludes: [], argAll: ['exec'] },
  ],
});

// ---- /proc/stat -----------------------------------------------------------------------------------------------------------------

// fields: user nice system idle iowait irq softirq steal
const statText = (cpus) => `${['cpu  0 0 0 0 0 0 0 0', ...cpus.map((c, i) => `cpu${i} ${c.join(' ')}`)].join('\n')}\nintr 1\n`;

test('Host_busy_is_computed_from_two_stat_readings', () => {
  const before = parseProcStat(statText([[100, 0, 100, 1000, 50, 0, 0, 0], [0, 0, 0, 1000, 0, 0, 0, 0]]));
  // cpu0: +60 user, +10 nice, +20 system, +5 irq, +5 softirq, +20 steal = 120 busy; +400 idle, +100 iowait. cpu1: all idle +500.
  const after = parseProcStat(statText([[160, 10, 120, 1400, 150, 5, 5, 20], [0, 0, 0, 1500, 0, 0, 0, 0]]));
  const b = hostBusy(before, after);
  assert.equal(b.perCpu.length, 2);
  assert.equal(b.perCpu[0], 120 / (120 + 400 + 100), 'busy share of cpu0: steal in, idle and iowait out of the busy count');
  assert.equal(b.perCpu[1], 0);
  assert.ok(Math.abs(b.cpus - 120 / 620) < 1e-12, 'CPU-equivalents are the sum of the per-CPU shares');
});

test('Host_busy_says_unknown_for_a_cpu_that_did_not_tick', () => {
  const s = parseProcStat(statText([[1, 0, 1, 1, 0, 0, 0, 0]]));
  const b = hostBusy(s, s);
  assert.equal(b.known, false);
  assert.match(b.why, /no ticks/);
});

// ---- foreign agents -------------------------------------------------------------------------------------------------------------

const proc = (pid, argv, { uid = 1000, environ = [] } = {}) => ({ pid, uid, argv, environ });

test('A_foreign_claude_print_process_is_found_and_ours_is_not', () => {
  const ours = ['BENCH_CELL=cell-a'];
  const procs = [
    proc(1, ['/home/x/.local/share/claude/versions/2.1.310', '-p', '--model', 'opus']), // 1: foreign, no marker
    proc(2, ['/home/x/.local/share/claude/versions/2.1.310', '-p'], { environ: ours }), // 2: ours
    proc(3, ['claude']), // 3: interactive: not an agent run
    proc(4, ['node', '/usr/lib/node_modules/@anthropic-ai/claude-code/cli.js', '--print', 'hi']), // 4: foreign
    proc(5, ['codex', 'exec', 'review this']), // 5: foreign
    proc(6, ['claude', '-p'], { uid: 1001, environ: null }), // 6: another user's, environ unreadable: foreign
    proc(7, ['vim', 'notes.txt']),
  ];
  const found = foreignAgents(procs, { patterns: THRESHOLDS.agentPatterns, cellIds: ['cell-a'] });
  assert.deepEqual(found.map(f => f.pid), [1, 4, 5, 6]);
});

test('The_foreign_cmdline_is_masked_in_the_verdict', () => {
  const secret = 'sk-ant-oat01-abcdefghijklmnopqrstuvwxyz0123456789';
  const found = foreignAgents([proc(9, ['claude', '-p', `--token=${secret}`])], { patterns: THRESHOLDS.agentPatterns, cellIds: [] });
  assert.equal(found.length, 1);
  assert.doesNotMatch(found[0].cmdline, /abcdefghijkl/);
  assert.match(found[0].cmdline, /\[secret\]/);
});

// ---- the quiet verdict ----------------------------------------------------------------------------------------------------------

const calm = (over = {}) => ({ hostCpus: 0.1, harnessCpus: 0.05, foreignAgents: [], memAvailablePct: 70, liveSessions: [], spinMs: 20, ...over });
const window = (n, over = {}) => Array.from({ length: n }, () => calm(over));

test('Quiet_needs_every_condition', () => {
  const spin = { calibratedMs: 20 };
  const ok = quietVerdict(window(12), { ...THRESHOLDS, ...spin });
  assert.equal(ok.quiet, true, JSON.stringify(ok.failures));
  const cases = [
    ['host-cpu', [...window(11), calm({ hostCpus: 2.2 + 0.05 })]],
    ['foreign-agent', [...window(11), calm({ foreignAgents: [{ pid: 1, cmdline: 'claude -p' }] })]],
    ['memory', window(12, { memAvailablePct: 20 })],
    ['live-session', window(12, { liveSessions: ['/home/x/rsd/minigit/s-1/s/abc'] })],
    ['spin', window(12, { spinMs: 30 })],
  ];
  for (const [condition, samples] of cases) {
    const v = quietVerdict(samples, { ...THRESHOLDS, ...spin });
    assert.equal(v.quiet, false, condition);
    assert.deepEqual(v.failures.map(f => f.condition), [condition], `only ${condition} fails`);
  }
});

test('Quiet_subtracts_the_harness_own_ticks_and_reads_the_mean_as_well_as_the_peak', () => {
  const spin = { calibratedMs: 20 };
  // the harness itself used 0.7 CPU: the host shows 0.75, the foreign part is 0.05
  assert.equal(quietVerdict(window(12, { hostCpus: 0.75, harnessCpus: 0.7 }), { ...THRESHOLDS, ...spin }).quiet, true);
  // a steady 0.6 CPU (below the 1.5 peak limit) breaks the 0.5 mean limit
  const v = quietVerdict(window(12, { hostCpus: 0.65, harnessCpus: 0.05 }), { ...THRESHOLDS, ...spin });
  assert.deepEqual(v.failures.map(f => f.condition), ['host-cpu']);
  assert.match(v.failures[0].detail, /mean/);
});

test('The_spin_probe_calibrates_and_detects_contention', async () => {
  let i = 0;
  const probe = async () => 20 + (i++ % 3 === 0 ? 0.2 : 0); // 30 idle runs near 20 ms
  const cal = await calibrateSpin({ runs: 30, probe });
  assert.equal(cal.runs, 30);
  assert.ok(Math.abs(cal.medianMs - 20) < 0.01, `median ${cal.medianMs}`);
  assert.ok(cal.maxMs >= cal.medianMs && cal.minMs <= cal.medianMs, 'the spread is recorded');
  const spin = { calibratedMs: cal.medianMs };
  assert.equal(quietVerdict(window(12, { spinMs: 21 }), { ...THRESHOLDS, ...spin }).quiet, true, '1.05 x is quiet');
  assert.equal(quietVerdict(window(12, { spinMs: 30 }), { ...THRESHOLDS, ...spin }).quiet, false, '1.5 x is not');
  assert.ok(spinMedianRatio([20, 30, 21], 20) > 1.04 && spinMedianRatio([20, 30, 21], 20) < 1.06, 'the median, not the mean or the max');
});

// ---- during a phase -------------------------------------------------------------------------------------------------------------

const during = (over = {}) => ({ foreignCpus: 0, foreignAgents: 0, spinMs: 20, ...over });

test('Load_during_a_phase_is_flagged_never_killed', () => {
  const t = { ...THRESHOLDS, calibratedMs: 20 };
  assert.deepEqual(duringFlags(Array.from({ length: 12 }, () => during()), t), []);
  assert.deepEqual(duringFlags(Array.from({ length: 12 }, () => during({ foreignCpus: 1.2 })), t), ['foreign load'], 'a 60 s mean over 1.0 CPU');
  assert.deepEqual(duringFlags([...Array.from({ length: 11 }, () => during()), during({ foreignAgents: 1 })], t), ['foreign load'], 'a foreign agent appears');
  assert.deepEqual(duringFlags(Array.from({ length: 12 }, () => during({ spinMs: 30 })), t), ['harness core contended']);
});

test('Foreign_cpu_excludes_our_cells_and_the_harness', () => {
  const procs = [
    { pid: 10, ticks: 500, environ: ['BENCH_CELL=cell-a'] }, // our cell
    { pid: 11, ticks: 900, environ: ['BENCH_CELL=cell-b'] }, // our cell
    { pid: 12, ticks: 400, environ: [] }, // the harness
    { pid: 13, ticks: 1000, environ: ['X=1'] }, // a foreign builder
    { pid: 14, ticks: 100, environ: null }, // unreadable: counted as foreign
  ];
  const prev = new Map([[10, 0], [11, 0], [12, 0], [13, 400], [14, 0]]);
  const r = foreignCpu({ procs, cellIds: ['cell-a', 'cell-b'], harnessPids: [12], prev, dtMs: 5000, clockTicks: 100 });
  // (1000 - 400) + 100 = 700 ticks = 7 s over 5 s = 1.4 CPU
  assert.ok(Math.abs(r.cpus - 1.4) < 1e-9, `got ${r.cpus}`);
  assert.equal(r.lowerBound, true, 'a process born and dead between two samples is missed: this is a lower bound');
  assert.equal(r.ticks.get(13), 1000, 'the next call reads this call\'s ticks as its previous');
});

// ---- the mask, observed ---------------------------------------------------------------------------------------------------------

const procApiWith = (lists) => ({ read: (pid, file) => { assert.equal(file, 'status'); return `Name:\tx\nCpus_allowed_list:\t${lists[pid]}\n`; } });

test('The_mask_is_observed_on_every_inner_cell_process', () => {
  assert.equal(maskObserved({ pids: ['1', '2'], mask: '0-5', procApi: procApiWith({ 1: '0-5', 2: '0-5' }) }).observed, true);
  const miss = maskObserved({ pids: ['1', '2'], mask: '0-5', procApi: procApiWith({ 1: '0-5', 2: '0-13' }) });
  assert.equal(miss.observed, false);
  assert.deepEqual(miss.perPid.filter(p => !p.ok).map(p => p.pid), ['2']);
  assert.equal(maskObserved({ pids: [], mask: '0-5', procApi: procApiWith({}) }).observed, false, 'no process to observe is a miss, never a pass');
});

// ---- the effect, on the real kernel ---------------------------------------------------------------------------------------------

test('The_cell_runs_on_its_mask', () => {
  const grep = ['grep', 'Cpus_allowed_list', '/proc/self/status'];
  const masked = spawnSync('taskset', ['-c', '0', 'nice', '-n', '19', ...grep], { encoding: 'utf8' }).stdout.split(':')[1].trim();
  const control = spawnSync(grep[0], grep.slice(1), { encoding: 'utf8' }).stdout.split(':')[1].trim();
  assert.equal(masked, '0');
  assert.notEqual(control, '0', `the control keeps the full list: ${control}`);
  const real = { read: (pid, file) => readFileSync(`/proc/${pid}/${file}`, 'latin1') };
  assert.equal(maskObserved({ pids: ['self'], mask: control, procApi: real }).observed, true, 'observed against its own list');
  assert.equal(maskObserved({ pids: ['self'], mask: '0', procApi: real }).observed, false, 'and a miss against another');
});
