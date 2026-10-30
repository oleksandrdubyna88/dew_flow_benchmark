// The spend gate of the bare runner (E3b.S1, T-G1): two limits, each one a `spendDecision` call, checked before every phase start.
//   the ceiling      settled data + void + pilot + prior spend + the live sum + 1.1 x every cap about to start <= the ceiling ($500, parent §13)
//   the bare budget  the same WITHOUT the prior spend (instrument work before E3b is in the ceiling and not in the $40)
// Also the one figure every stop rule reads (the largest of the three cost bases) and what an interrupted session cost.
// Pure over values: the ledger lines come from `ledger.ndjson`, the broker lines from a session's `broker.ndjson`.
import { spendDecision } from '../../policy-ab-quick/lib/spend.mjs';
import { BARE_RESERVE_USD, OVER_CAP_FACTOR } from './budget.mjs';

/** The kinds of a ledger line. `prior` is in the ceiling only; every other kind is bare spend. */
export const LEDGER_KINDS = Object.freeze(['prior', 'rehearsal', 'aa', 'pilot', 'data', 'void']);

const sum = lines => lines.reduce((a, l) => a + l.usd, 0);

function assertKinds(ledger) {
  const bad = ledger.find(l => !LEDGER_KINDS.includes(l.kind));
  if (bad) throw new Error(`ledger line of kind '${bad.kind}': the legal kinds are ${LEDGER_KINDS.join(', ')}`);
}

/** `{ ceilingUsd, bareUsd }`: what the ledger holds against each limit. */
export function spendTotals(ledger) {
  assertKinds(ledger);
  return { ceilingUsd: sum(ledger), bareUsd: sum(ledger.filter(l => l.kind !== 'prior')) };
}

/**
 * -> `{ stop, stoppedBy: ['the ceiling' | 'the bare budget'], ceiling, bare, lines }`. `caps` are the caps of the sessions about to start;
 * `liveUsd` the running sessions' sum. Both limits are always evaluated and both lines reported.
 */
export function spendGate({ ledger, liveUsd = 0, caps, ceilingUsd, bareBudgetUsd }) {
  const { ceilingUsd: spent, bareUsd } = spendTotals(ledger);
  const opts = { liveUsd, factor: OVER_CAP_FACTOR };
  const ceiling = spendDecision(spent, ceilingUsd, caps, { ...opts, label: 'the ceiling' });
  const bare = spendDecision(bareUsd, bareBudgetUsd, caps, { ...opts, label: 'the bare budget' });
  return {
    stop: ceiling.stop || bare.stop,
    stoppedBy: [...(ceiling.stop ? ['the ceiling'] : []), ...(bare.stop ? ['the bare budget'] : [])],
    ceiling, bare, lines: [ceiling.line, bare.line],
  };
}

const finite = n => typeof n === 'number' && Number.isFinite(n);
const chargedSum = lines => lines.reduce((a, l) => a + (finite(l.chargedUsd) ? l.chargedUsd : 0), 0);

/**
 * The figure a stop rule reads: the largest of the envelope's total, the headline recompute and the broker's `B`; a figure that could not be
 * taken is skipped, never zero. A session without an envelope is `B` (a killed session's envelope may be missing: B is what it spent).
 */
export function stopRuleFigure({ envelopeUsd, recomputeUsd, brokerUsd }) {
  if (!finite(envelopeUsd)) return brokerUsd;
  return Math.max(...[envelopeUsd, recomputeUsd, brokerUsd].filter(finite));
}

/** A request's reservation: the larger of the floor and the largest charge settled in the cell so far (E2.S2 review, `lib/budget.mjs`). */
export function reservationOf(brokerLines) {
  return Math.max(BARE_RESERVE_USD, ...brokerLines.filter(l => l.event === 'settled' && finite(l.chargedUsd)).map(l => l.chargedUsd));
}

/** What a session cost, interrupted or not: settled `B` plus every forwarded-but-unsettled request at its reservation. */
export function sessionSpendUsd(brokerLines) {
  const settled = brokerLines.filter(l => l.event === 'settled');
  const done = new Set(settled.map(l => l.seq));
  const unsettled = brokerLines.filter(l => l.event === 'forwarding' && !done.has(l.seq)).length;
  return Math.round((chargedSum(settled) + unsettled * reservationOf(brokerLines)) * 1e9) / 1e9;
}
