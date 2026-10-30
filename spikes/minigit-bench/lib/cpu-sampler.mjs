// The CPU sampler, the foreign-agent finder and the spin probe (E3b.S2). Pure over /proc-shaped values, plus `spinProbe` (a child pinned to one CPU).
//
// Why: a bare pair is a paid, timed measurement and another experiment loading the machine confounds it (V7). Before a phase the machine is
// checked and the phase REFUSED when it is loaded (quiet.mjs); during a phase load is only FLAGGED, because killing on load would make a void
// that parent §11.1 does not list and would discard paid work.
//
// The harness's own ticks are not foreign load: the runner, its broker, receiver, proxy and sampler run on core 3 (CPUs 6-7).
// The foreign-CPU figure is a LOWER bound: a process born and dead between two samples is never seen.
import { existsSync, readdirSync } from 'node:fs';
import path from 'node:path';
import { fail, ok } from './result.mjs';
import { runBounded } from './run.mjs';
import { ownerAlive, readState, stateOf } from './session-state.mjs';
import { maskTokens } from './token-scan.mjs';

const BUSY_FIELDS = Object.freeze(['user', 'nice', 'system', 'irq', 'softirq', 'steal']);
const FIELDS = Object.freeze(['user', 'nice', 'system', 'idle', 'iowait', 'irq', 'softirq', 'steal']);

const median = xs => {
  const s = [...xs].sort((a, b) => a - b);
  return s.length % 2 === 1 ? s[(s.length - 1) / 2] : (s[s.length / 2 - 1] + s[s.length / 2]) / 2;
};
const mean = xs => xs.reduce((a, b) => a + b, 0) / xs.length;

/** `/proc/stat` text -> `[{ user, nice, system, idle, iowait, irq, softirq, steal }]`, one per `cpuN` line (the aggregate `cpu` line is not kept). */
export function parseProcStat(text) {
  return text.split('\n').filter(l => /^cpu\d+ /.test(l)).map(l => {
    const nums = l.split(/\s+/).slice(1).map(Number);
    return Object.freeze(Object.fromEntries(FIELDS.map((f, i) => [f, nums[i] ?? 0])));
  });
}

const sumOf = (c, fields) => fields.reduce((a, f) => a + c[f], 0);

/**
 * Two parsed readings -> `{ known: true, perCpu: [busy share 0..1], cpus }` where `cpus` is the CPU-equivalents (the sum of the shares).
 * Busy = user + nice + system + irq + softirq + steal; idle and iowait are not busy. A CPU with no ticks between the readings makes the answer unknown.
 */
export function hostBusy(prev, cur) {
  if (prev.length !== cur.length) return { known: false, why: `the CPU count changed between readings (${prev.length} -> ${cur.length})` };
  const perCpu = cur.map((c, i) => {
    const total = sumOf(c, FIELDS) - sumOf(prev[i], FIELDS);
    return total > 0 ? (sumOf(c, BUSY_FIELDS) - sumOf(prev[i], BUSY_FIELDS)) / total : null;
  });
  if (perCpu.includes(null)) return { known: false, why: `cpu${perCpu.indexOf(null)} had no ticks between the readings` };
  return { known: true, perCpu, cpus: perCpu.reduce((a, b) => a + b, 0) };
}

const basename = p => String(p ?? '').split('/').at(-1);

function matchesPattern(argv, p) {
  const byExe = (p.exe ?? []).includes(basename(argv[0]));
  const byPath = (p.pathIncludes ?? []).some(s => argv.slice(0, 2).some(a => String(a).includes(s)));
  if (!byExe && !byPath) return false;
  const args = argv.slice(1);
  return (p.argAny === undefined || p.argAny.some(a => args.includes(a))) && (p.argAll === undefined || p.argAll.every(a => args.includes(a)));
}

const carriesCell = (environ, cellIds) => Array.isArray(environ) && cellIds.some(id => environ.includes(`BENCH_CELL=${id}`));

/**
 * The agent runs that are not this series' cells: `procs` is `[{ pid, uid, argv, environ }]` (`environ` null when unreadable). A process
 * whose environment cannot be read is foreign (another user's, usually): it cannot be shown to be ours.
 * -> `[{ pid, pattern, cmdline }]`; `cmdline` is masked (`maskTokens`).
 */
export function foreignAgents(procs, { patterns, cellIds }) {
  return procs.flatMap(p => {
    const pattern = patterns.find(x => matchesPattern(p.argv, x));
    if (!pattern || carriesCell(p.environ, cellIds)) return [];
    return [{ pid: p.pid, pattern: pattern.name, cmdline: maskTokens(p.argv.join(' ')) }];
  });
}

/**
 * CPU-equivalents used by processes that are neither this series' cells nor the harness, since the previous call. `procs` is
 * `[{ pid, ticks, environ }]` (utime + stime in clock ticks); `prev` a `Map` pid -> ticks. -> `{ cpus, lowerBound: true, ticks: Map }`.
 * A pid not in `prev` started since: its whole count is the delta.
 */
export function foreignCpu({ procs, cellIds, harnessPids, prev, dtMs, clockTicks = 100 }) {
  const foreign = procs.filter(p => !harnessPids.includes(p.pid) && !carriesCell(p.environ, cellIds));
  const delta = foreign.reduce((a, p) => a + Math.max(0, p.ticks - (prev.get(p.pid) ?? 0)), 0);
  return { cpus: delta / clockTicks / (dtMs / 1000), lowerBound: true, ticks: new Map(procs.map(p => [p.pid, p.ticks])) };
}

/** The median spin time over `ratio` x the calibrated idle median. */
export const spinMedianRatio = (spinMs, calibratedMs) => median(spinMs) / calibratedMs;

/**
 * The pre-start verdict over a window of samples `{ hostCpus, harnessCpus, foreignAgents, memAvailablePct, liveSessions, spinMs }`:
 * (a) host CPU minus the harness's own: mean <= meanCpuMax and no sample over sampleCpuMax; (b) no foreign agent; (c) MemAvailable >= the
 * minimum share; (d) no live session of any series; (e) the spin median within spinMaxRatio x `calibratedMs`.
 * -> `{ quiet, failures: [{ condition, detail }], numbers }`. Every failure is named; the foreign cmdlines are already masked.
 */
export function quietVerdict(samples, t) {
  const foreign = samples.map(s => Math.max(0, s.hostCpus - s.harnessCpus));
  const ratio = spinMedianRatio(samples.map(s => s.spinMs), t.calibratedMs);
  const agents = samples.flatMap(s => s.foreignAgents);
  const lowestMem = Math.min(...samples.map(s => s.memAvailablePct));
  const live = [...new Set(samples.flatMap(s => s.liveSessions))];
  const failures = [
    ...(mean(foreign) > t.meanCpuMax ? [{ condition: 'host-cpu', detail: `mean foreign CPU ${mean(foreign).toFixed(2)} over ${t.meanCpuMax}` }] : []),
    ...(Math.max(...foreign) > t.sampleCpuMax ? [{ condition: 'host-cpu', detail: `a sample of ${Math.max(...foreign).toFixed(2)} CPU over ${t.sampleCpuMax}` }] : []),
    ...(agents.length > 0 ? [{ condition: 'foreign-agent', detail: [...new Set(agents.map(a => a.cmdline))].join(' | ') }] : []),
    ...(lowestMem < t.memAvailMinPct ? [{ condition: 'memory', detail: `MemAvailable ${lowestMem}% under ${t.memAvailMinPct}%` }] : []),
    ...(live.length > 0 ? [{ condition: 'live-session', detail: live.join(', ') }] : []),
    ...(ratio > t.spinMaxRatio ? [{ condition: 'spin', detail: `spin median ${ratio.toFixed(2)} x idle, limit ${t.spinMaxRatio}` }] : []),
  ];
  const unique = failures.filter((f, i) => failures.findIndex(g => g.condition === f.condition) === i);
  const merged = unique.map(f => ({ ...f, detail: failures.filter(g => g.condition === f.condition).map(g => g.detail).join('; ') }));
  return { quiet: merged.length === 0, failures: merged, numbers: { meanForeignCpus: mean(foreign), maxForeignCpus: Math.max(...foreign), spinRatio: ratio, lowestMemPct: lowestMem, samples: samples.length } };
}

/**
 * The flags of a phase's samples `{ foreignCpus, foreignAgents (count), spinMs }`: `foreign load` when the mean foreign CPU is over the limit or a
 * foreign agent appears; `harness core contended` when the spin median is over its limit. Flags only: nothing here stops a session.
 */
export function duringFlags(samples, t) {
  return [
    ...(mean(samples.map(s => s.foreignCpus)) > t.foreignCpuMax || samples.some(s => s.foreignAgents > 0) ? ['foreign load'] : []),
    ...(spinMedianRatio(samples.map(s => s.spinMs), t.calibratedMs) > t.spinMaxRatio ? ['harness core contended'] : []),
  ];
}

/** `Cpus_allowed_list` of one `/proc/<pid>/status` text, or null. */
const allowedList = text => /^Cpus_allowed_list:\s*(\S+)/m.exec(text)?.[1] ?? null;

/**
 * Is every process of the cell on its mask? `procApi.read(pid, 'status')`. -> `{ observed, perPid: [{ pid, allowed, ok }] }`. An instrument miss
 * when false (G-11). No process to look at is a miss, never a pass.
 */
export function maskObserved({ pids, mask, procApi }) {
  const perPid = pids.map(pid => {
    let allowed = null;
    try { allowed = allowedList(procApi.read(pid, 'status')); } catch (e) { if (!['ENOENT', 'ESRCH'].includes(e.code)) throw e; }
    return { pid, allowed, ok: allowed === mask };
  });
  return { observed: perPid.length > 0 && perPid.every(p => p.ok), perPid };
}

// ---- the spin probe -------------------------------------------------------------------------------------------------------------

const SPIN_SCRIPT = 'const n = Number(process.argv[1]); const t0 = process.hrtime.bigint(); let x = 1; for (let i = 0; i < n; i += 1) x = (Math.imul(x, 1103515245) + 12345) | 0; const ms = Number(process.hrtime.bigint() - t0) / 1e6; process.stdout.write(`${ms} ${x}`);';

/**
 * One timed fixed loop, in a child pinned to `cpu` by `taskset` (the harness core is CPUs 6-7: the probe alternates between them). The loop is
 * timed INSIDE the child, so node's start-up is not in the figure. -> `ok(ms)` | `fail(reason)`.
 */
export async function spinProbe({ iterations, cpu }) {
  const r = await runBounded('taskset', ['-c', String(cpu), process.execPath, '-e', SPIN_SCRIPT, String(iterations)], { timeoutMs: 30_000 });
  if (!r.ok) return r;
  const ms = Number(r.value.stdout.split(' ')[0]);
  return Number.isFinite(ms) && r.value.code === 0 ? ok(ms) : fail(`the spin probe printed no time (exit ${r.value.code}): ${r.value.stderr.slice(-200)}`);
}

/** `runs` probes on an idle machine -> `{ runs, medianMs, minMs, maxMs, spreadMs }`; `probe()` resolves to ms (default: the real one, alternating CPUs 6 and 7). */
export async function calibrateSpin({ runs = 30, probe }) {
  const ms = [];
  for (let i = 0; i < runs; i += 1) ms.push(await probe(i));
  return { runs, medianMs: median(ms), minMs: Math.min(...ms), maxMs: Math.max(...ms), spreadMs: Math.max(...ms) - Math.min(...ms) };
}

// ---- the sampler: the one I/O loop ----------------------------------------------------------------------------------------------

/** `'0-5'` or `'6,7'` or `'0-2,8'` -> `[0, 1, ...]`. */
export function cpusOfMask(mask) {
  return mask.split(',').flatMap(part => {
    const [a, b = a] = part.split('-').map(Number);
    return Array.from({ length: b - a + 1 }, (_, i) => a + i);
  });
}

const GONE = new Set(['ENOENT', 'ESRCH']);

/** Every process the caller may read: `[{ pid, uid, argv, environ | null, ticks }]`. A process that vanishes mid-read is skipped; an unreadable environ is null. */
function readProcs(procApi) {
  return procApi.pids().flatMap(pid => {
    try {
      const argv = procApi.read(pid, 'cmdline').split('\0').filter(Boolean);
      const rest = procApi.read(pid, 'stat').split(/\)\s/).at(-1).split(' ');
      const uid = Number(/^Uid:\s*(\d+)/m.exec(procApi.read(pid, 'status'))?.[1] ?? -1);
      let environ = null;
      try { environ = procApi.read(pid, 'environ').split('\0'); } catch (e) { if (GONE.has(e.code)) return []; }
      return [{ pid: String(pid), uid, argv, environ, ticks: Number(rest[11]) + Number(rest[12]) }];
    } catch (e) {
      if (GONE.has(e.code)) return [];
      return [];
    }
  });
}

const meminfoPct = text => {
  const n = k => Number(new RegExp(`^${k}:\\s+(\\d+)`, 'm').exec(text)?.[1]);
  return (n('MemAvailable') / n('MemTotal')) * 100;
};

/**
 * The sampler's one I/O object. Reads `/proc/stat`, `/proc/meminfo`, `/proc/pressure/cpu` through `readText(path)` and the process table through
 * `procApi`; `liveSessions()` lists sessions of any series that are live; `spin()` resolves to one probe's ms. The baseline is read when it is
 * created, so every call of the returned `sample()` has a previous reading. -> `async sample() -> { t, hostCpus, perCpu, harnessCpus, foreignCpus,
 * foreignCpusLowerBound, foreignAgents, memAvailablePct, psiSomeAvg10, liveSessions, spinMs }`.
 */
export async function createSampler({ procApi, readText, cellIds, harnessPids, liveSessions, spin, now, patterns, clockTicks = 100 }) {
  let prevStat = parseProcStat(readText('/proc/stat'));
  let prevProcs = readProcs(procApi);
  let prevTicks = new Map(prevProcs.map(p => [p.pid, p.ticks]));
  let prevAt = now();
  return async function sample() {
    const t = now();
    const stat = parseProcStat(readText('/proc/stat'));
    const procs = readProcs(procApi);
    const dtMs = t - prevAt;
    const busy = hostBusy(prevStat, stat);
    const harnessTicks = procs.filter(p => harnessPids.includes(p.pid)).reduce((a, p) => a + Math.max(0, p.ticks - (prevTicks.get(p.pid) ?? 0)), 0);
    const foreign = foreignCpu({ procs, cellIds, harnessPids, prev: prevTicks, dtMs, clockTicks });
    const out = {
      t, hostCpus: busy.known ? busy.cpus : null, perCpu: busy.known ? busy.perCpu : null, harnessCpus: harnessTicks / clockTicks / (dtMs / 1000),
      foreignCpus: foreign.cpus, foreignCpusLowerBound: true, foreignAgents: foreignAgents(procs, { patterns, cellIds }),
      memAvailablePct: meminfoPct(readText('/proc/meminfo')), psiSomeAvg10: Number(/some avg10=([\d.]+)/.exec(readText('/proc/pressure/cpu') ?? '')?.[1] ?? NaN),
      liveSessions: liveSessions(), spinMs: await spin(),
    };
    prevStat = stat; prevTicks = foreign.ticks; prevAt = t;
    return out;
  };
}

/** The flags of a phase's samples, looking at every window of `windowSamples` consecutive samples (60 s at 5 s): a loaded minute flags the phase. */
export function phaseFlags(samples, thresholds, windowSamples = 12) {
  const windows = samples.length <= windowSamples ? [samples] : Array.from({ length: samples.length - windowSamples + 1 }, (_, i) => samples.slice(i, i + windowSamples));
  return [...new Set(windows.flatMap(w => duringFlags(w, thresholds)))];
}

/** The session directories `<rsd root>/<series>/s/<rid>` whose owner (pid AND start time) is alive: live work of ANY series, ours or not. */
export function liveSessionDirs(rsdRoot) {
  const dirs = (p) => (existsSync(p) ? readdirSync(p, { withFileTypes: true }).filter(e => e.isDirectory()).map(e => path.join(p, e.name)) : []);
  return dirs(rsdRoot).flatMap(series => dirs(path.join(series, 's'))).filter(dir => {
    const read = readState(path.join(dir, 'state.json'));
    return read.ok && stateOf(read.value, { ownerAlive }) === 'live';
  }).sort();
}
