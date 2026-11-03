// The probe's stop path (W2, W5; fix batch 4): what a SIGINT or SIGTERM does. It says at once that it is stopping and how many runs are open (W5), releases the record's placeholder, ends every open run and
// every child the probe started (bounded), and says when it ended. A cleanup that hits its bound, or ends with something left, is NOT clean (W2): it names the runs, scopes, holders and views still
// there, leaves the run markers and the owned folders as they are (the next start's sweep finds and retires them) and exits with the distinct code 75, never with the signal's own code.
import { printable } from './printable.mjs';

/** The exit code of a stop whose cleanup did not finish or left something behind (EX_TEMPFAIL): the next start's sweep retires what is left. */
export const INCOMPLETE_EXIT = 75;

/**
 * Wait for `promise` for at most `ms`: `{ value }`, `{ error }` or `{ timedOut: true }`. The timer is NOT unref'd (C3): it is what guarantees that a stuck cleanup still reaches exit 75 even when nothing
 * else holds the event loop open; it is cleared as soon as the race settles.
 */
async function settle(promise, ms) {
  let timer;
  const timeout = new Promise(resolve => { timer = setTimeout(() => resolve({ timedOut: true }), ms); });
  try {
    return await Promise.race([promise.then(value => ({ value }), error => ({ error })), timeout]);
  } finally {
    clearTimeout(timer);
  }
}

const describe = runs => runs.map(run => `run ${run.id} (scope units ${run.unitPrefix}-*, holder ${run.unitPrefix.replace('-lsc-', '-lhold-')}, folder ${run.dir})`).join('; ');
const leftOf = reports => reports.flatMap(report => report?.left ?? []).map(item => `${item.kind ?? 'resource'} ${item.name ?? item.path ?? JSON.stringify(item)}${item.reason ? ` (${item.reason})` : ''}`);
const SWEEP = "the next start's sweep retires them";

/**
 * Stop the probe cleanly: `code` is the signal's exit code (130 or 143), `bound` the wait for the cleanup in ms and `closeBound` the wait for the owned folders' close. The seams: `say` (stderr), `exit`,
 * `release` (the record's placeholder), `describeLive` (the open runs), `cleanLive` (ends them and the tracked children, resolves their reports) and `closeScope` (the owned folders). Resolves after it has
 * called `exit`. Only a cleanup AND a close that both finished with nothing left print "nothing is left" and exit with the signal's code (C1).
 */
export async function stopCleanly({ code, bound, closeBound = 5000, say, exit, release, describeLive, cleanLive, closeScope }) {
  say(`exec-probe: stopping — cleaning up ${describeLive().length} open run(s)`);
  release();
  const cleaned = await settle(cleanLive(), bound);
  if (cleaned.timedOut) {
    say(printable(`exec-probe: cleanup incomplete after ${bound} ms: still there: ${describe(describeLive()) || 'a run or child that did not end'}; the run markers stay, ${SWEEP}`)); // the runs still open AFTER the attempt
    return exit(INCOMPLETE_EXIT);
  }
  const left = [...leftOf(cleaned.value ?? []), ...(cleaned.error ? [`the cleanup failed: ${cleaned.error.message}`] : [])];
  if (left.length > 0) {
    say(printable(`exec-probe: cleanup incomplete: still there: ${left.join('; ')}; ${SWEEP}`));
    return exit(INCOMPLETE_EXIT);
  }
  const closed = await settle(Promise.resolve().then(closeScope), closeBound);
  if (closed.timedOut || closed.error) {
    say(printable(`exec-probe: cleanup incomplete: the owned folders did not finish closing (${closed.timedOut ? `did not finish within ${closeBound} ms` : closed.error.message}); ${SWEEP}`));
    return exit(INCOMPLETE_EXIT);
  }
  say('exec-probe: cleanup finished: nothing is left');
  return exit(code);
}

/** `stopCleanly` that can never reject or hang (C4): a failure of the stop itself is one stderr line and exit 75. */
export async function runStop(options) {
  try {
    await stopCleanly(options);
  } catch (error) {
    options.say(printable(`exec-probe: the stop itself failed (${error?.message ?? error}); exit ${INCOMPLETE_EXIT}: the next start's sweep retires what is left`));
    options.exit(INCOMPLETE_EXIT);
  }
}
