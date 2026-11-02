// E1.S8b: the network-namespace holder (D43, plan "The shape" item 1). `unshare -rn` makes a user and a network namespace; the holder sets `lo` up and becomes a bounded `sleep`
// with an OWNED name as its first argument (`exec -a`), so a sweep can tell it from every other sleep. It owns the namespaces and holds nothing else: the driver and the box's
// bwrap join them with `nsenter -t <holder> -U -n --preserve-credentials`. The bound is the run's ceiling plus a margin: a layer that crashed leaves a holder that still ends.
// Joining works because the holder's user namespace is owned by the same uid (validated 2026-10-31, unprivileged).
import { spawn } from 'node:child_process';
import { findOnPath } from '../../../minigit-bench/lib/find-on-path.mjs';
import { fail, ok } from '../../../minigit-bench/lib/result.mjs';
import { argv0Of, endProcess, identityOf, sleep } from './proc.mjs';

const SBIN_PATH = '/usr/sbin:/usr/bin:/bin:/sbin';
const READY_MS = 5000;
const NAME = /^[A-Za-z0-9_-]{1,80}$/;
const SCRIPT = '"$1" link set lo up && exec -a "$2" sleep "$3"'; // the values are arguments, never text of the script

/** The argv prefix that joins the holder's user and network namespaces (`nsenter` first); the caller adds the command. */
export const nsenterArgv = (holder, nsenter = 'nsenter') => [nsenter, '-t', String(holder.pid), '-U', '-n', '--preserve-credentials'];

function waitReady(child, name, ms) {
  return new Promise(resolve => {
    let stderr = '';
    child.stderr.on('data', chunk => { stderr = (stderr + chunk).slice(-2000); });
    child.once('exit', code => resolve({ ready: false, reason: `unshare -rn ended with ${code} before the holder was ready: ${stderr.trim()}` }));
    (async () => {
      for (let waited = 0; waited < ms; waited += 10) {
        if (argv0Of(child.pid) === name) return resolve({ ready: true });
        await sleep(10);
      }
      return resolve({ ready: false, reason: `the holder was not ready within ${ms} ms: ${stderr.trim()}` });
    })();
  });
}

/**
 * Start the holder: `{ ok, value: { pid, startTicks, argv0 } }` once `lo` is up and the process carries `name`, or `{ ok: false, reason }` (a host without unprivileged user namespaces
 * says so here). `name` is an owned name; `boundSeconds` bounds the sleep.
 */
export async function startHolder({ name, boundSeconds, ip = findOnPath('ip', SBIN_PATH) }) {
  if (!NAME.test(name)) return fail(`the holder name ${JSON.stringify(name)} is not letters, digits, '_' and '-'`);
  if (ip === null) return fail('ip (iproute2) was not found: the holder cannot set lo up');
  const child = spawn('unshare', ['-rn', 'bash', '-c', SCRIPT, 'bash', ip, name, String(Math.ceil(boundSeconds))], { detached: true, stdio: ['ignore', 'ignore', 'pipe'], env: { PATH: SBIN_PATH } });
  child.on('error', () => {});
  const state = await waitReady(child, name, READY_MS);
  child.stderr.destroy();
  child.unref();
  if (state.ready) return ok(identityOf(child.pid));
  await endProcess(identityOf(child.pid) ?? { pid: child.pid, startTicks: -1, argv0: name }, { graceMs: 200 });
  return fail(state.reason);
}

/** End the holder (SIGTERM, SIGKILL after 2 s) if it is still the process it was: `'gone'`, `'terminated'`, `'killed'` or `'stuck'`. Never throws. */
export const stopHolder = identity => endProcess(identity);
