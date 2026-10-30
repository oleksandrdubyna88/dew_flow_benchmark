// The watchdogs' decision (E2.S2 step 6). Pure: the launcher feeds it the clock and the counters once per tick (5 s), writes the decision
// to watchdog.ndjson, and acts on a kill through `endCell`. Order of precedence: wall, cost, stall.
//   cost   the broker's SETTLED spend at the cap (a reservation is not spend: admission already stops new spend and D-17a allows the 1.1 x
//          overshoot, so four requests in flight must not kill a healthy session), or O's live sum or the stream meter at 1.1 x the cap (they arrive later or
//          are estimates); any one source over its threshold is a kill, a silent source (null) is unknown, never zero
//   stall  no stream line, no OTel record, no broker line and no CPU-tick growth of the cell's processes for `stallMs`
import { OVER_CAP_FACTOR } from './budget.mjs';

const known = n => typeof n === 'number' && Number.isFinite(n);

/** The most recent sign of life, in ms: a stream line, an OTel record, a broker line, the last CPU growth, or the start. */
function lastProgress({ startedAt, now, lastStreamAt, lastOtelAt, lastBrokerAt, lastCpuGrowthAt = null, cpuTicks, prevCpuTicks }) {
  const growth = known(cpuTicks) && known(prevCpuTicks) && cpuTicks > prevCpuTicks ? now : null;
  return Math.max(startedAt, ...[lastStreamAt, lastOtelAt, lastBrokerAt, lastCpuGrowthAt, growth].filter(known));
}

function costBreach({ brokerSpentUsd, liveUsd, meterUsd, capUsd }) {
  const limit = capUsd * OVER_CAP_FACTOR;
  const hits = [];
  if (known(brokerSpentUsd) && brokerSpentUsd >= capUsd) hits.push(`broker spent ${brokerSpentUsd} >= cap ${capUsd}`);
  if (known(liveUsd) && liveUsd >= limit) hits.push(`otel ${liveUsd} >= ${OVER_CAP_FACTOR} x cap`);
  if (known(meterUsd) && meterUsd >= limit) hits.push(`stream meter ${meterUsd} >= ${OVER_CAP_FACTOR} x cap`);
  return hits;
}

/** `{ kill: false, detail } | { kill: true, reason: 'wall' | 'cost' | 'stall', detail }`. */
export function watchdogDecision(input) {
  const { now, startedAt, wallMs, stallMs, brokerSpentUsd, liveUsd, meterUsd } = input;
  const figures = `broker spent ${brokerSpentUsd ?? 'unknown'}, otel ${liveUsd ?? 'unknown'}, meter ${meterUsd ?? 'unknown'}`;
  if (now - startedAt >= wallMs) return { kill: true, reason: 'wall', detail: `${now - startedAt} ms since the start, limit ${wallMs}` };
  const cost = costBreach(input);
  if (cost.length > 0) return { kill: true, reason: 'cost', detail: cost.join('; ') };
  const quiet = now - lastProgress(input);
  if (quiet >= stallMs) return { kill: true, reason: 'stall', detail: `no stream, otel, broker or cpu progress for ${quiet} ms, limit ${stallMs}` };
  return { kill: false, detail: `${figures}; quiet for ${quiet} ms` };
}
