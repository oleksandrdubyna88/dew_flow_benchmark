// The janitor and the graceful stop (E2.S2 D-14). A cell's processes are found by the BENCH_CELL=<id> marker in their environment,
// exactly, never by name: a process called `claude` outside a cell is none of this module's business. The process table is `procApi`
// (`pids()`, `read(pid, file)`, `signal(pid, sig)`), so every decision is testable against a fake.
//
// The stop is graceful first, by the only road that reaches inside: the box runs under --new-session, so a signal to the process GROUP
// reaches only the outer bwrap, which dies on it and takes the inside down with SIGKILL (--die-with-parent). A SIGTERM goes to each INNER
// process by its host pid; the CLI then exits itself with 143. Only after the grace does killTree end the group.
import { readFileSync, readdirSync } from 'node:fs';
import path from 'node:path';

const GONE = new Set(['ENOENT', 'ESRCH']);

export const realProcApi = Object.freeze({
  pids: () => readdirSync('/proc').filter(n => /^\d+$/.test(n)),
  read: (pid, file) => readFileSync(`/proc/${pid}/${file}`, 'latin1'),
  signal: (pid, sig) => process.kill(Number(pid), sig),
});

/** `{ environ, argv0 }` of a pid, `{ gone: true }` when it vanished, `{ unreadable: true }` when it is not ours to read. */
function inspect(pid, procApi) {
  try {
    const environ = procApi.read(pid, 'environ').split('\0');
    const argv0 = procApi.read(pid, 'cmdline').split('\0')[0];
    return { environ, argv0 };
  } catch (e) {
    return GONE.has(e.code) ? { gone: true } : { unreadable: true };
  }
}

/**
 * The cell's processes -> `{ pids, unreadable }`. `inner` drops the outer `bwrap` and its pid-namespace init (both carry the marker, and the
 * whole inner command on their cmdline): they are not the processes a graceful stop signals. Unreadable entries are counted, never guessed.
 */
export function cellProcesses(cellId, procApi, { inner = false } = {}) {
  const marker = `BENCH_CELL=${cellId}`;
  const seen = procApi.pids().map(pid => [pid, inspect(pid, procApi)]);
  const mine = seen.filter(([pid, i]) => !i.gone && !i.unreadable && i.environ.includes(marker) && String(pid) !== String(process.pid));
  const kept = inner ? mine.filter(([, i]) => path.basename(i.argv0) !== 'bwrap') : mine;
  return { pids: kept.map(([pid]) => String(pid)), unreadable: seen.filter(([, i]) => i.unreadable).length };
}

/** utime + stime (clock ticks) of the listed pids; a pid that vanished adds nothing. Fields 14 and 15 of /proc/<pid>/stat, counted after the LAST ')' (the name may hold one). */
export function cpuTicksOf(pids, procApi) {
  return pids.reduce((total, pid) => {
    try {
      const rest = procApi.read(pid, 'stat').split(/\)\s/).at(-1).split(' ');
      return total + Number(rest[11]) + Number(rest[12]); // field 3 is rest[0]
    } catch (e) {
      if (GONE.has(e.code)) return total;
      throw e;
    }
  }, 0);
}

/** Is the pid a live process (a zombie is not)? */
function alive(pid, procApi) {
  try {
    const state = procApi.read(pid, 'stat').split(/\)\s/).at(-1).split(' ')[0];
    return state !== 'Z' && state !== 'X';
  } catch (e) {
    if (GONE.has(e.code)) return false;
    throw e;
  }
}

function sendAll(pids, signal, procApi) {
  const sent = [];
  for (const pid of pids) {
    try {
      procApi.signal(pid, signal);
      sent.push(pid);
    } catch (e) {
      if (!GONE.has(e.code)) throw e; // a pid that vanished between the scan and the signal is the goal
    }
  }
  return sent;
}

/** SIGKILL every process still carrying the cell's marker -> how many it signalled. Never by name. */
export async function endSurvivors(cellId, { killTree, procApi = realProcApi, sleep = ms => new Promise(r => setTimeout(r, ms)) }) {
  const { pids } = cellProcesses(cellId, procApi);
  pids.forEach(pid => killTree(Number(pid)));
  if (pids.length > 0) await sleep(100);
  return pids.length;
}

/** Wait until `done()` or `graceMs` is spent; polls every `pollMs`. -> whether it was done. */
async function waitUntil(done, graceMs, { sleep, now, pollMs }) {
  const end = now() + graceMs;
  while (now() < end) {
    if (done()) return true;
    await sleep(pollMs);
  }
  return done();
}

/**
 * Stop a cell: (1) SIGTERM to every inner process; (2) wait `graceMs` for the bwrap child to go (or, with no bwrapPid, for the cell's inner
 * processes to go); (3) killTree(bwrapPid); (4) the janitor ends anything still carrying the marker.
 * -> `{ signalled, closedWithinGrace, killed, survivorsEnded }`.
 */
export async function endCell({
  cellId, bwrapPid = null, graceMs = 5000, killTree, procApi = realProcApi, sleep = ms => new Promise(r => setTimeout(r, ms)), now = Date.now, pollMs = 25,
}) {
  const signalled = sendAll(cellProcesses(cellId, procApi, { inner: true }).pids, 'SIGTERM', procApi);
  const gone = bwrapPid === null ? () => cellProcesses(cellId, procApi, { inner: true }).pids.length === 0 : () => !alive(bwrapPid, procApi);
  const closedWithinGrace = await waitUntil(gone, graceMs, { sleep, now, pollMs });
  const killed = !closedWithinGrace && bwrapPid !== null;
  if (killed) killTree(bwrapPid);
  if (killed) await waitUntil(() => !alive(bwrapPid, procApi), 2000, { sleep, now, pollMs });
  const survivorsEnded = await endSurvivors(cellId, { killTree, procApi, sleep });
  return { signalled, closedWithinGrace, killed, survivorsEnded };
}
