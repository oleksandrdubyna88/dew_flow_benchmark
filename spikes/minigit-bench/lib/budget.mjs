// The cell's own budget, held by the credential broker (E2.S2 D-17). Pure: state in, state out.
// A request is admitted only while settled spend + every in-flight reservation + its own reservation stays within the cap; it reserves
// `reservationFor(state, floor)` (the floor, or the largest charge settled so far in the cell: never sized from the request's max_tokens, which the CLI
// sets to 32k-128k and which would make admission refuse and the CLI retry) until it settles; settlement prices the parsed usage with the SAME table E1's views use (asTheCliBills), so a disagreement
// between the broker's ledger and the envelope is a bug report. Nothing parsed charges the reservation, never 0; an unpriced model charges the dearest known rates - except a request
// the broker KNOWS was not billed (`waive`: an API error answer, or a dial that failed before a byte was sent), which charges 0 and says why.
// The cap is SOFT (D-17a): the real cost is known only at settlement, so concurrent requests can settle above the cap; the overshoot is
// recorded and a session over 1.1 x the cap is flagged, never counted as within budget.
import { asTheCliBills, dearestPrice, priceFor, priceUsage } from './prices.mjs';

export const BARE_RESERVE_USD = 0.05;
export const OVER_CAP_FACTOR = 1.1;
const EPSILON = 1e-9;
const round9 = n => Math.round(n * 1e9) / 1e9;

export const openState = () => Object.freeze({ spentUsd: 0, reservedUsd: 0, inFlight: 0, largestChargeUsd: 0 });

/** What the next request reserves: the floor, or the largest charge settled so far in this cell if that is more. */
export const reservationFor = (state, floorUsd) => Math.max(floorUsd, state.largestChargeUsd);

/** Admit a request? `{ admit: true } | { admit: false, why }`. */
export function admit({ spentUsd, reservedUsd, reserveUsd, capUsd }) {
  const wouldBe = spentUsd + reservedUsd + reserveUsd;
  if (wouldBe > capUsd + EPSILON) {
    return { admit: false, why: `the cell's budget cap is ${capUsd} USD: ${round9(spentUsd)} spent + ${round9(reservedUsd)} reserved + ${reserveUsd} for this request would be ${round9(wouldBe)}` };
  }
  return { admit: true };
}

/** A new state holding one more reservation. */
export const reserve = (state, reserveUsd) => Object.freeze({ ...state, reservedUsd: round9(state.reservedUsd + reserveUsd), inFlight: state.inFlight + 1 });

const isCount = n => Number.isSafeInteger(n) && n >= 0;

/** The usage block as the `Tokens` shape `priceUsage` reads, or null when a count is not a count. A missing 5m/1h split is an interval. */
function tokensOf(u) {
  const counts = [u.input_tokens ?? 0, u.output_tokens ?? 0, u.cache_read_input_tokens ?? 0];
  if (!counts.every(isCount)) return null;
  const [input, output, cacheRead] = counts;
  const c = u.cache_creation;
  if (c && isCount(c.ephemeral_5m_input_tokens) && isCount(c.ephemeral_1h_input_tokens)) {
    return { input, output, cacheRead, cacheWrite5m: c.ephemeral_5m_input_tokens, cacheWrite1h: c.ephemeral_1h_input_tokens };
  }
  const write = u.cache_creation_input_tokens ?? 0;
  return isCount(write) ? { input, output, cacheRead, cacheWrite: write, split: { known: false, why: 'no 5m/1h split' } } : null;
}

const highEnd = priced => (typeof priced === 'number' ? priced : priced.high);

/** `{ known: true, usd[, range] } | { known: false, usd, model, why } (an unpriced model, charged) | { known: false, why }` (nothing to charge but the reservation). An unsplit write is priced at the high end of its interval: a cap is never undercounted. */
function pricedOf({ usage, model, table }) {
  if (!model || !usage) return { known: false, why: 'no usage parsed' };
  const tokens = tokensOf(usage);
  if (!tokens) return { known: false, why: 'the usage block holds a count that is not a count' };
  const price = priceFor(table, model);
  // fail safe: a model the table does not hold is charged at the dearest known rate of every token class, flagged (`known: false` with its `usd`),
  // never at the reservation, which undercounted a 0.116565 request as 0.05. A cap must not err downwards.
  if (!price.known) return { known: false, usd: highEnd(priceUsage(dearestPrice(table), tokens)), model, why: 'unpriced model: dearest known rates' };
  const priced = priceUsage(price.value, tokens);
  return typeof priced === 'number' ? { known: true, usd: priced } : { known: true, usd: priced.high, range: priced };
}

/**
 * Settle one request -> `{ state, line }`: the reservation released, the charge added; `line` is the ledger's pricing part.
 * `waive` is the reason a request is known not to have been billed (the charge is then 0, recorded as known), or absent.
 */
export function settle({ state, entry, usage, model, table, cliVersion, waive = null }) {
  let priced;
  try {
    priced = waive === null ? pricedOf({ usage, model, table: asTheCliBills(table, cliVersion) }) : { known: true, usd: 0, why: waive };
  } catch (e) {
    priced = { known: false, why: `pricing failed: ${e.message}` }; // a pricing bug must cost a reservation, not the broker
  }
  const chargedUsd = priced.usd ?? entry.reserveUsd;
  const next = Object.freeze({
    spentUsd: round9(state.spentUsd + chargedUsd), reservedUsd: Math.max(0, round9(state.reservedUsd - entry.reserveUsd)), inFlight: Math.max(0, state.inFlight - 1),
    largestChargeUsd: Math.max(state.largestChargeUsd, chargedUsd),
  });
  return { state: next, line: { model, usage, priced, chargedUsd, spentUsd: next.spentUsd } };
}

/** Where settled spend stands against the cap: `overCap` only beyond 1.1 x (the series' spend gate), `withinCap` up to the cap itself. */
export function capStatus({ spentUsd, capUsd }) {
  return { overCap: spentUsd > capUsd * OVER_CAP_FACTOR + EPSILON, overshootUsd: round9(Math.max(0, spentUsd - capUsd)), withinCap: spentUsd <= capUsd + EPSILON };
}
