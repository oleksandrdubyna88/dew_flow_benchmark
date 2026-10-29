// One session's cost with the basis stated: from the envelope (E), else from the OTel requests (O) flagged `no envelope`, else unknown.
// Never a fitted rate: this module and everything it imports price from the dated published table only (T-C1 scans for it).
import { headlineUsd } from './claude-usage.mjs';
import { sumSources } from './otel-decode.mjs';

const NO_ENVELOPE = Object.freeze({ known: false, why: 'no envelope' });

/** O's per-model sums (all sources) in the shape `headlineUsd` reads. Cost is not carried: nothing here is priced from `cost_usd`. */
function envelopeShapedFrom(O) {
  const perModel = Object.fromEntries(Object.entries(O.perModel).map(([model, entry]) => {
    const s = sumSources(entry);
    const whole = s.cacheCreation > 0 && s.splitRequests === s.requests;
    return [model, { input: s.input, output: s.output, cacheRead: s.cacheRead, cacheWrite: s.cacheCreation, costUsd: null, otelSplit: whole ? { w5: s.cacheCreation5m, w1: s.cacheCreation1h } : null }];
  }));
  return { known: true, perModel };
}

/** Why no figure can be given from O, or null: nothing complete at all, or a model whose every record was left out (it would read as free). */
function unpriceable(O) {
  if (Object.keys(O.perModel).length === 0) return 'no complete api_request record';
  const dropped = [...new Set(O.requests.map(r => r.model).filter(Boolean))].filter(m => !(m in O.perModel));
  return dropped.length > 0 ? `no complete api_request record for ${dropped.join(', ')}` : null;
}

function fromOtel(O, T, table) {
  const flags = ['no envelope', ...(O.incomplete > 0 ? [`${O.incomplete} otel records lack a token field and are left out: a lower bound`] : [])];
  const why = unpriceable(O);
  if (why) return { known: false, why, flags, cliTotalUsd: NO_ENVELOPE };
  const headline = headlineUsd(envelopeShapedFrom(O), T, table);
  const lowerBound = O.incomplete > 0;
  const basis = `otel api_request tokens at published prices (${table.readOn}); 5m/1h split from the otel cache_creation_5m/1h_tokens, else transcripts, else an interval`;
  return { ...headline, ...(headline.known ? { basis, exact: headline.exact && !lowerBound } : {}), flags: [...flags, ...(headline.flags ?? [])], cliTotalUsd: NO_ENVELOPE, lowerBound };
}

/** `{ known, usd, low, high, exact, basis, perModel, cliTotalUsd, flags }` or `{ known: false, why }`. */
export function sessionCost({ E, O, T, table }) {
  if (E?.known) {
    const headline = headlineUsd(E, T, table);
    return { ...headline, cliTotalUsd: E.totalCostUsd, flags: headline.flags ?? [] };
  }
  if (O?.present) return fromOtel(O, T, table);
  return { known: false, why: 'no envelope and no otel records' };
}
