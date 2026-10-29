// Published prices, the CLI-billed view of them, and the arithmetic that turns tokens into USD. Pure.
// Never a fitted rate: an id the table does not hold is `{ known: false, why }` - no prefix match, no default row
// (policy-ab-quick/lib/pricing.mjs:15 has one; this spike must not). Shapes follow ScoutCost.cs:9-15, :36-48, :51-52.
import { fail, ok } from './result.mjs';

const RATE_KEYS = Object.freeze(['input', 'output', 'cacheRead', 'cacheWrite5m', 'cacheWrite1h']);
const isRate = n => typeof n === 'number' && Number.isFinite(n) && n >= 0;
const isText = s => typeof s === 'string' && s.trim() !== '';

function parsePrice(row) {
  if (!row || !isText(row.modelId)) return fail('a price names the exact model id it is for');
  const bad = RATE_KEYS.find(k => !isRate(row[k]));
  if (bad) return fail(`the price for '${row.modelId}' has a missing or negative '${bad}'`);
  return ok(Object.freeze(Object.fromEntries([['modelId', row.modelId], ...RATE_KEYS.map(k => [k, row[k]])])));
}

function parseModels(rows) {
  if (!Array.isArray(rows) || rows.length === 0) return fail('a price table prices at least one model');
  const parsed = rows.map(parsePrice);
  const refused = parsed.find(p => !p.ok);
  if (refused) return refused;
  const ids = parsed.map(p => p.value.modelId);
  if (new Set(ids).size !== ids.length) return fail('a price table prices each model id once');
  return ok(new Map(parsed.map(p => [p.value.modelId, p.value])));
}

function parseCliRate(row, models) {
  if (!row || !models.has(row.modelId)) {
    return fail(`the CLI rate for '${row?.modelId}' is refused - it must name a priced model (priced: ${[...models.keys()].join(', ')})`);
  }
  if (!isRate(row.cacheRead) || !isText(row.observed)) {
    return fail(`the CLI rate for '${row.modelId}' is refused - it needs a non-negative cacheRead and where it was observed`);
  }
  return ok(Object.freeze({ modelId: row.modelId, cacheRead: row.cacheRead, observed: row.observed }));
}

/** Validate a parsed price file -> `{ ok, value: PriceTable } | { ok: false, reason }`. */
export function loadPrices(json) {
  if (!json || typeof json !== 'object') return fail('a price file is a JSON object');
  if (typeof json.source !== 'string' || !json.source.startsWith('https://')) return fail(`'${json.source}' is not where a price table was read - an https url`);
  if (!isText(json.readOn)) return fail('a price table says the day it was read');
  const models = parseModels(json.models);
  if (!models.ok) return models;
  const rates = (json.cliRates ?? []).map(r => parseCliRate(r, models.value));
  const refused = rates.find(r => !r.ok);
  if (refused) return refused;
  return ok(Object.freeze({ source: json.source, readOn: json.readOn, models: models.value, cliRates: Object.freeze(rates.map(r => r.value)) }));
}

/** Exact id lookup -> `{ known: true, value: Price } | { known: false, why }`. */
export function priceFor(table, modelId) {
  const price = table.models.get(modelId);
  if (price) return { known: true, value: price };
  return { known: false, why: `no price for '${modelId}'; priced: ${[...table.models.keys()].join(', ')}` };
}

/** The table as the CLI bills: each model with an observed CLI rate carries that cache-read rate. A new table; the argument is untouched. */
export function asTheCliBills(table) {
  const byModel = new Map(table.cliRates.map(r => [r.modelId, r.cacheRead]));
  const models = new Map([...table.models].map(([id, p]) => [id, byModel.has(id) ? Object.freeze({ ...p, cacheRead: byModel.get(id) }) : p]));
  return Object.freeze({ ...table, models });
}

const usd = (p, t, write) => (t.input * p.input + t.output * p.output + t.cacheRead * p.cacheRead + write) / 1e6;

/**
 * USD for one model's tokens. A split `Tokens` -> a number. An unsplit `Tokens` (`split.known === false`, one `cacheWrite`) ->
 * `{ low, high }`: every write at the 5m rate / every write at the 1h rate. The two shapes differ so a writer cannot price by accident.
 */
export function priceUsage(price, tokens) {
  if (tokens.split && tokens.split.known === false) {
    return { low: usd(price, tokens, tokens.cacheWrite * price.cacheWrite5m), high: usd(price, tokens, tokens.cacheWrite * price.cacheWrite1h) };
  }
  return usd(price, tokens, tokens.cacheWrite5m * price.cacheWrite5m + tokens.cacheWrite1h * price.cacheWrite1h);
}

/** `((input - cached)*in + cached*cachedRate + output*out) / 1e6` for an OpenAI-shaped usage (cached INSIDE input). Refuses `cached > input`. */
export function priceOpenAiShaped(tokens, rates) {
  if (tokens.cached > tokens.input) return fail(`cached > input (${tokens.cached} > ${tokens.input}): the cached tokens are a subset of the input`);
  return ok(((tokens.input - tokens.cached) * rates.input + tokens.cached * rates.cached + tokens.output * rates.output) / 1e6);
}
