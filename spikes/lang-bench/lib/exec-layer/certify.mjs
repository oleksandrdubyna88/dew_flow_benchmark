// E1.S8b: the launch check and the certification (D13, D36). Before any run the layer launches an empty program through the real prefix — its own scope, `…-certify`, in an empty view.
// (1) It must run to a clean exit, else the prefix is a failed launch (exit 3, `launch failed`, no case verdict): the runner alone cannot tell, because bwrap writes `child-pid` BEFORE it makes
// its mounts, so a missing bind reaches the runner as a program that exited 1 (measured 2026-11-02; the runner is not changed). (2) `prog/memory.peak` must be readable after its exit,
// else a DATA run is `memory-not-certifiable` (the named halt, exit 3): a `memory limit` or a clean exit could never be backed by the cgroup. A non-data run proceeds and says
// `memory bound not certified`.
import { runBounded } from '../../../minigit-bench/lib/run.mjs';
import { CERTIFY_CASE } from './case-view.mjs';
import { programArgv } from './prefix.mjs';
import { progCgroup, readProg, userEnv } from './scope.mjs';

const CERTIFY_MS = 60_000;
export const HALT = 'memory-not-certifiable';

/** `{ launched, certified, peakBytes? , reason? }`. `read` and `launch` are seams for the tests; the defaults are the real cgroup read and `runBounded`. */
export async function certifyMemory(run, { read = readProg, launch = runBounded } = {}) {
  const argv = programArgv(run, CERTIFY_CASE, ['bash', '-c', ':']);
  const ran = await launch(argv[0], argv.slice(1), { timeoutMs: CERTIFY_MS, env: userEnv() });
  if (!(ran.ok && ran.value.code === 0 && !ran.value.timedOut)) {
    return { launched: false, certified: false, reason: `the empty program did not run through the prefix: ${ran.ok ? ran.value.stderr.trim() || `exit ${ran.value.code}` : ran.reason}` };
  }
  const peak = read(progCgroup(`${run.unitPrefix}-${CERTIFY_CASE}`)).peakBytes;
  return peak === null
    ? { launched: true, certified: false, reason: 'prog/memory.peak cannot be read after the exit of an empty program' }
    : { launched: true, certified: true, peakBytes: peak };
}
