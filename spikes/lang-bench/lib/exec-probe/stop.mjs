// The probe's stop path (W2, W5; fix batch 4): what a SIGINT or SIGTERM does. It says at once that it is stopping and how many runs are open (W5), releases the record's placeholder, ends every open run and
// every child the probe started (bounded), and says when it ended. A cleanup that hits its bound, or ends with something left, is NOT clean (W2): it names the runs, scopes, holders and views still
// there, leaves the run markers and the owned folders as they are (the next start's sweep finds and retires them) and exits with the distinct code 75, never with the signal's own code.
import { printable } from './printable.mjs';

/** The exit code of a stop whose cleanup did not finish or left something behind (EX_TEMPFAIL): the next start's sweep retires what is left. */
export const INCOMPLETE_EXIT = 75;

const timeoutAfter = ms => new Promise(resolve => { setTimeout(() => resolve('timeout'), ms).unref(); });

const describe = runs => runs.map(run => `run ${run.id} (scope units ${run.unitPrefix}-*, holder ${run.unitPrefix.replace('-lsc-', '-lhold-')}, folder ${run.dir})`).join('; ');
const leftOf = reports => reports.flatMap(report => report?.left ?? []).map(item => `${item.kind ?? 'resource'} ${item.name ?? item.path ?? JSON.stringify(item)}${item.reason ? ` (${item.reason})` : ''}`);

/**
 * Stop the probe cleanly: `code` is the signal's exit code (130 or 143), `bound` the wait for the cleanup in ms. The seams: `say` (stderr), `exit`, `release` (the record's placeholder), `describeLive`
 * (the open runs), `cleanLive` (ends them and the tracked children, resolves their reports) and `closeScope` (the owned folders). Resolves after it has called `exit`.
 */
export async function stopCleanly({ code, bound, say, exit, release, describeLive, cleanLive, closeScope }) {
  const live = describeLive();
  say(`exec-probe: stopping — cleaning up ${live.length} open run(s)`);
  release();
  const outcome = await Promise.race([cleanLive().then(reports => ({ reports }), error => ({ reports: [], error })), timeoutAfter(bound)]);
  if (outcome === 'timeout') {
    say(printable(`exec-probe: cleanup incomplete after ${bound} ms: still there: ${describe(live) || 'a run or child that did not end'}; the run markers stay, the next start's sweep retires them`));
    return exit(INCOMPLETE_EXIT);
  }
  const left = leftOf(outcome.reports);
  if (outcome.error || left.length > 0) {
    say(printable(`exec-probe: cleanup incomplete: still there: ${[...left, ...(outcome.error ? [`the cleanup failed: ${outcome.error.message}`] : [])].join('; ')}; the next start's sweep retires them`));
    return exit(INCOMPLETE_EXIT);
  }
  await Promise.race([closeScope(), timeoutAfter(5000)]);
  say('exec-probe: cleanup finished: nothing is left');
  return exit(code);
}
