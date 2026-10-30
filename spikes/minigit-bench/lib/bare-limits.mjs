// The registered limits of the series, checked at every paid run (E3b review m2): the caps ($6, 60 min wall, 45 min stall), the bare budget ($40) and the ceiling ($500) in the
// run-map must be the registered values, and the bare budget may be raised ONCE (analysis contract section 9, item 3): only to finish the re-runs of a pair already started,
// only by their worst case (n x 1.1 x the cap), as a dated `budget-raise` line of amendments.md. The raise applies to the run of THAT pair only. Pure over values.
import { parseAmendments } from './amendments.mjs';
import { BARE_CAPS } from './bare-plan.mjs';
import { OVER_CAP_FACTOR } from './budget.mjs';
import { fail, ok } from './result.mjs';

export const REGISTERED_LIMITS = Object.freeze({ bareBudgetUsd: 40, ceilingUsd: 500 });
const round2 = x => Math.round(x * 100) / 100;
const same = (a, b) => JSON.stringify(a) === JSON.stringify(b);

/** `null` when the run-map's caps, bare budget and ceiling are the registered ones, else the sentence naming the first that is not. */
export function limitsProblem(map) {
  if (!same(map.caps, BARE_CAPS)) return `the run-map caps ${JSON.stringify(map.caps)} are not the registered ${JSON.stringify(BARE_CAPS)}`;
  if (map.bareBudgetUsd !== REGISTERED_LIMITS.bareBudgetUsd) return `the run-map bare budget $${map.bareBudgetUsd} is not the registered $${REGISTERED_LIMITS.bareBudgetUsd} (a raise is a dated budget-raise line, never an edit of the map)`;
  if (map.ceilingUsd !== REGISTERED_LIMITS.ceilingUsd) return `the run-map ceiling $${map.ceilingUsd} is not the registered $${REGISTERED_LIMITS.ceilingUsd}`;
  return null;
}

/**
 * The bare budget of a run of `pair` (`null` for the A/A and the pilot): `ok({ bareBudgetUsd, raise })`. At most ONE `budget-raise` line may exist; its pair must have started
 * (a session of it in the map) and its amount must be n x 1.1 x the cap; it raises the budget only for the run of its own pair.
 */
export function effectiveBudget({ map, amendmentsText, pair }) {
  const { raises } = parseAmendments(amendmentsText ?? '');
  if (raises.length === 0) return ok({ bareBudgetUsd: map.bareBudgetUsd, raise: null });
  if (raises.length > 1) return fail(`amendments.md holds ${raises.length} budget-raise lines: the $40 is raised once`);
  const [raise] = raises;
  const worst = round2(raise.n * OVER_CAP_FACTOR * BARE_CAPS.capUsd);
  if (raise.n < 1 || round2(raise.usd) !== worst) return fail(`the budget-raise line of ${raise.date} asks for $${raise.usd}: only the worst case of the re-runs, ${raise.n} x ${OVER_CAP_FACTOR} x $${BARE_CAPS.capUsd} = $${worst}`);
  const started = Object.values(map.sessions ?? {}).some(s => s.kind === 'data' && s.pair === raise.pair);
  if (!started) return fail(`the budget-raise line of ${raise.date} names pair ${raise.pair}, which has not already started: a raise only finishes the re-runs of a pair already started`);
  return ok({ bareBudgetUsd: raise.pair === pair ? round2(map.bareBudgetUsd + worst) : map.bareBudgetUsd, raise });
}
