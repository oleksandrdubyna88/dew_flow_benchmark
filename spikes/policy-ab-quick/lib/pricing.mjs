// Fallback cost when a cell has no result envelope (wall timeout), and the cost watchdog's meter. USD per million tokens, in the order
// [input, output, cache read, cache write]. ESTIMATE ONLY: a cell's reported cost is ALWAYS the CLI envelope's total; every number from
// this table is marked `estimated` wherever it surfaces.
//
// The rates are FITTED to the envelopes of recorded cells (modelUsage tokens against costUSD), not taken from a price list:
//   sonnet  [2, 10, 0.2, 2.5]     exact on 5 model rows (_q1/c3, _q3/c3, _q1probe/c1-c3), residual 0.00 %
//   haiku   [1, 5, 0.1, 1.25]     exact on the 6 one-line probe calls of _q2/_q3
//   opus    [2.5, 12.5, 0.25, 7]  least squares over 12 model rows (_q1, _q2, _q3 c1-c3 and _q1probe c1-c3): real cells within -5.3 % to +2.5 %;
//                                 the three small probe cells (cache-write dominated) -12 % to +7 %. The cache-write rate is a blend (the
//                                 CLI mixes cache tiers), which is why it does not fit a single list price.
//   fable   = the opus row. NO recorded envelope holds a Fable message, so this is an ASSUMPTION, not a fit; re-fit when one exists.
// The old table priced opus at [15, 75, 1.5, 18.75]: about 5x too high, which would have killed an Opus-heavy cell at ~$6.6 real.
const RATES = { opus: [2.5, 12.5, 0.25, 7], fable: [2.5, 12.5, 0.25, 7], sonnet: [2, 10, 0.2, 2.5], haiku: [1, 5, 0.1, 1.25] };

const rateFor = model => RATES[Object.keys(RATES).find(k => model.toLowerCase().includes(k)) ?? 'opus'];

/** Pure: sum the LAST usage of each message id (the stream repeats a message per content block). */
/**
 * The stream's assistant events carry the usage of the message AT ITS START: input and cache tokens are right, output tokens are a handful
 * (measured: 495 seen against 47,483 in the envelope over 45 Opus messages). Thinking is not in the stream at all. So the meter and the
 * envelope-less fallback give every message an output FLOOR of this many tokens - the recorded cells average 790-1050 per message; 850 keeps
 * the replayed cost within a few percent of the envelope. Without it the meter reads ~17 % low (it can then only be late, never early).
 */
export const OUTPUT_FLOOR_PER_MESSAGE = 850;

export function costFromMessages(events, { outputFloor = 0 } = {}) {
  const byId = new Map();
  for (const ev of events.filter(e => e.type === 'assistant' && e.message?.usage)) byId.set(ev.message.id ?? `anon-${byId.size}`, ev.message);
  const tokensByModel = {};
  let usd = 0;
  for (const m of byId.values()) {
    const u = { ...m.usage, output_tokens: Math.max(m.usage.output_tokens ?? 0, outputFloor) };
    const t = (tokensByModel[m.model] ??= { input: 0, output: 0, cacheRead: 0, cacheWrite: 0 });
    t.input += u.input_tokens ?? 0; t.output += u.output_tokens ?? 0;
    t.cacheRead += u.cache_read_input_tokens ?? 0; t.cacheWrite += u.cache_creation_input_tokens ?? 0;
    const [i, o, cr, cw] = rateFor(m.model ?? '');
    usd += ((u.input_tokens ?? 0) * i + (u.output_tokens ?? 0) * o + (u.cache_read_input_tokens ?? 0) * cr + (u.cache_creation_input_tokens ?? 0) * cw) / 1e6;
  }
  return { messages: byId.size, tokensByModel, usd: Math.round(usd * 1e4) / 1e4, estimated: true, ...(outputFloor > 0 ? { outputFloorPerMessage: outputFloor } : {}) };
}
