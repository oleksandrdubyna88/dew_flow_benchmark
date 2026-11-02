// E1.S8b: a process known by IDENTITY, never by its pid alone. The layer signals only a process it started: the pid, the start time (`/proc/<pid>/stat` field 22) and the first
// element of its command line (an owned name, set with `exec -a`) must all still match, else the pid is another process now and nothing is sent. A zombie is gone.
// Node has no pidfd, so the look and the signal are two steps; the window is microseconds and the start time makes a reused pid fail the look.
import { readdirSync, readFileSync } from 'node:fs';

const GRACE_MS = 2000;
const POLL_MS = 20;
const STARTTIME_INDEX = 19; // field 22 of /proc/<pid>/stat, counted from the state (field 3)

/** `{ state, ppid, startTicks }` from `/proc/<pid>/stat`, or null when the process is not there. */
export function statOf(pid) {
  try {
    const text = readFileSync(`/proc/${pid}/stat`, 'utf8');
    const fields = text.slice(text.lastIndexOf(')') + 2).split(' ');
    return { state: fields[0], ppid: Number(fields[1]), startTicks: Number(fields[STARTTIME_INDEX]) };
  } catch {
    return null;
  }
}

/** The first element of the command line (`/proc/<pid>/cmdline`), or null. */
export function argv0Of(pid) {
  try {
    return readFileSync(`/proc/${pid}/cmdline`, 'utf8').split('\0')[0];
  } catch {
    return null;
  }
}

/** `{ pid, startTicks, argv0 }` of a live process, or null. */
export function identityOf(pid) {
  const stat = statOf(pid);
  return stat === null ? null : { pid, startTicks: stat.startTicks, argv0: argv0Of(pid) };
}

/** True while the process this identity names is the one there (alive, not a zombie, same start, same first argument). */
export function sameProcess(identity) {
  const stat = statOf(identity.pid);
  const alive = stat !== null && stat.state !== 'Z' && stat.state !== 'X';
  return alive && stat.startTicks === identity.startTicks && argv0Of(identity.pid) === identity.argv0;
}

/** Send `signal` to the process the identity names; false when it is not that process (any more). Never throws. */
export function signalIfSame(identity, signal) {
  if (!sameProcess(identity)) return false;
  try {
    process.kill(identity.pid, signal);
    return true;
  } catch {
    return false;
  }
}

export const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));

/** True when the process is gone within `ms` (polled every `pollMs`). */
export async function waitGone(identity, ms = GRACE_MS, pollMs = POLL_MS) {
  for (let waited = 0; waited <= ms; waited += pollMs) {
    if (!sameProcess(identity)) return true;
    await sleep(pollMs);
  }
  return !sameProcess(identity);
}

/** SIGTERM, then SIGKILL after `graceMs`: `'gone'` (it was not there), `'terminated'`, `'killed'` or `'stuck'` (still there after the kill). Never throws. */
export async function endProcess(identity, { graceMs = GRACE_MS, pollMs = POLL_MS } = {}) {
  if (!signalIfSame(identity, 'SIGTERM')) return 'gone';
  if (await waitGone(identity, graceMs, pollMs)) return 'terminated';
  signalIfSame(identity, 'SIGKILL');
  return (await waitGone(identity, graceMs, pollMs)) ? 'killed' : 'stuck';
}

/** The command line of a process as an array, or null when it is not readable. */
export function cmdlineOf(pid) {
  try {
    return readFileSync(`/proc/${pid}/cmdline`, 'utf8').split('\0').filter((arg, i, all) => arg !== '' || i < all.length - 1);
  } catch {
    return null;
  }
}

function matched(pid, match) {
  const argv = pid === process.pid ? null : cmdlineOf(pid);
  return argv !== null && argv.length > 0 && match(argv) ? identityOf(pid) : null;
}

/** The identities of every other process whose command line `match(argv)` accepts (a look at `/proc`, never a signal). */
export function scanProcesses(match) {
  return readdirSync('/proc').map(Number).filter(Number.isInteger).map(pid => matched(pid, match)).filter(identity => identity !== null);
}
