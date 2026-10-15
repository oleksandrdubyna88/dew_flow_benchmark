// Fallback cost when a cell has no result envelope (wall timeout). USD per million tokens.
// ESTIMATE ONLY — these rates can go stale. A cell's reported cost is ALWAYS the CLI envelope's total; this table prices a cell
// without an envelope (a killed one) and feeds the cost watchdog, and both are marked `estimated` wherever they surface.
const RATES = { opus: [15, 75, 1.5, 18.75], fable: [15, 75, 1.5, 18.75], sonnet: [3, 15, 0.3, 3.75], haiku: [1, 5, 0.1, 1.25] };

const rateFor = model => RATES[Object.keys(RATES).find(k => model.toLowerCase().includes(k)) ?? 'opus'];

/** Pure: sum the LAST usage of each message id (the stream repeats a message per content block). */
export function costFromMessages(events) {
  const byId = new Map();
  for (const ev of events.filter(e => e.type === 'assistant' && e.message?.usage)) byId.set(ev.message.id ?? `anon-${byId.size}`, ev.message);
  const tokensByModel = {};
  let usd = 0;
  for (const m of byId.values()) {
    const u = m.usage;
    const t = (tokensByModel[m.model] ??= { input: 0, output: 0, cacheRead: 0, cacheWrite: 0 });
    t.input += u.input_tokens ?? 0; t.output += u.output_tokens ?? 0;
    t.cacheRead += u.cache_read_input_tokens ?? 0; t.cacheWrite += u.cache_creation_input_tokens ?? 0;
    const [i, o, cr, cw] = rateFor(m.model ?? '');
    usd += ((u.input_tokens ?? 0) * i + (u.output_tokens ?? 0) * o + (u.cache_read_input_tokens ?? 0) * cr + (u.cache_creation_input_tokens ?? 0) * cw) / 1e6;
  }
  return { messages: byId.size, tokensByModel, usd: Math.round(usd * 1e4) / 1e4, estimated: true };
}
