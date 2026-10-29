// D12: the alias -> exact model id record of one session, and the check of a session against the series' baseline.
// An alias resolving to a different id mid-series changes what the series measures, so it STOPS; a first use only extends the record.
import { agentTimeline } from '../../policy-ab-quick/lib/parallel.mjs';
import { initInfo } from '../../policy-ab-quick/lib/summary.mjs';
import { fail, ok } from './result.mjs';

const isFallback = e => e?.type === 'system' && e.subtype === 'model_refusal_fallback';

function fallbacksOf(events) {
  return events.filter(isFallback).map(e => ({
    parent: e.parent_tool_use_id ?? null, original: e.original_model ?? null, fallback: e.fallback_model ?? null, category: e.api_refusal_category ?? null,
  }));
}

/** Alias claims in the order they were made: the argv alias for the init model, then each Agent launch's requested -> resolved model. */
function claimsOf(events, argvModel) {
  const init = initInfo(events);
  const main = init.present && init.model ? [{ alias: argvModel, id: init.model, from: 'argv/init' }] : [];
  const timeline = agentTimeline(events);
  const children = timeline.filter(t => t.requestedModel && t.resolvedModel).map(t => ({ alias: t.requestedModel, id: t.resolvedModel, from: `Agent launch ${t.id}` }));
  const unresolved = timeline.filter(t => !t.requestedModel || !t.resolvedModel).map(t => ({ id: t.id, requestedModel: t.requestedModel, why: t.requestedModel ? 'no resolvedModel in the Agent result' : 'the launch named no model' }));
  return { claims: [...main, ...children], unresolved, mainId: main[0]?.id ?? null, childIds: children.map(c => c.id) };
}

/** `{ ok, value: { aliases, internal, fallbacks, unresolved } } | { ok: false, reason }` for one session's stream events. */
export function aliasRecord({ events, argvModel = 'opus' }) {
  const { claims, unresolved, mainId, childIds } = claimsOf(events, argvModel);
  const aliases = {};
  for (const c of claims) {
    if (aliases[c.alias] !== undefined && aliases[c.alias] !== c.id) {
      return fail(`alias '${c.alias}' resolved to two ids inside one session: ${aliases[c.alias]} and ${c.id} (${c.from})`);
    }
    aliases[c.alias] = c.id;
  }
  const fallbacks = fallbacksOf(events);
  const known = new Set([mainId, ...childIds, ...fallbacks.flatMap(f => [f.original, f.fallback])]);
  const modelUsage = events.findLast(e => e.type === 'result')?.modelUsage ?? {};
  const internal = Object.keys(modelUsage).filter(id => !known.has(id));
  return ok({ aliases, internal, fallbacks, unresolved });
}

/** Baseline against current -> `{ verdict: 'ok' | 'stop' | 'flag', reasons, extend }`. `extend` is applied by the caller in a dated write. */
export function checkAliases(baseline, current) {
  const drift = Object.entries(current.aliases).filter(([a, id]) => baseline.aliases[a] !== undefined && baseline.aliases[a] !== id)
    .map(([a, id]) => `alias '${a}' resolved to ${baseline.aliases[a]} in the baseline and to ${id} now`);
  const internal = current.internal.filter(id => !baseline.internal.includes(id)).map(id => `internal model ${id} is not in the baseline (${baseline.internal.join(', ') || 'none'})`);
  const fallbacks = current.fallbacks.map(f => `model_refusal_fallback: ${f.original} -> ${f.fallback} (${f.category})`);
  const extend = Object.fromEntries(Object.entries(current.aliases).filter(([a]) => baseline.aliases[a] === undefined));
  const stops = [...drift, ...internal];
  const verdict = stops.length > 0 ? 'stop' : fallbacks.length > 0 ? 'flag' : 'ok';
  return { verdict, reasons: [...stops, ...fallbacks], extend };
}
