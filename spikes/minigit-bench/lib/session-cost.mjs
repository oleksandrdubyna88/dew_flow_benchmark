// One session's cost with the basis stated: from the envelope (E), else from the OTel requests (O) flagged `no envelope`, else unknown.
// Never a fitted rate: this module and everything it imports price from the dated published table only (T-C1 scans for it).
import { headlineUsd } from './claude-usage.mjs';
import { sumSources } from './otel-decode.mjs';

const NO_ENVELOPE = Object.freeze({ known: false, why: 'no envelope' });

/** O's per-model sums (all sources) in the shape `headlineUsd` reads. Cost is not carried: nothing here is priced from `cost_usd`. */
function envelopeShapedFrom(O) {
  const perModel = Object.fromEntries(Object.entries(O.perModel).map(([model, entry]) => {
    const s = sumSources(entry);
    return [model, { input: s.input, output: s.output, cacheRead: s.cacheRead, cacheWrite: s.cacheCreation, costUsd: null }];
  }));
  return { known: true, perModel };
}

function fromOtel(O, T, table) {
  const headline = headlineUsd(envelopeShapedFrom(O), T, table);
  const flags = ['no envelope', ...(O.incomplete > 0 ? [`${O.incomplete} otel records lack a token field and are left out: a lower bound`] : [])];
  return { ...headline, ...(headline.known ? { basis: `otel api_request tokens at published prices (${table.readOn}); 5m/1h split from transcripts where they have it` } : {}), flags, cliTotalUsd: NO_ENVELOPE, lowerBound: O.incomplete > 0 };
}

/** `{ known, usd, low, high, exact, basis, perModel, cliTotalUsd, flags }` or `{ known: false, why }`. */
export function sessionCost({ E, O, T, table }) {
  if (E?.known) return { ...headlineUsd(E, T, table), cliTotalUsd: E.totalCostUsd, flags: [] };
  if (O?.present) return fromOtel(O, T, table);
  return { known: false, why: 'no envelope and no otel records' };
}
