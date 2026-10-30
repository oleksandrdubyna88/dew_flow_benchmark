// How a bare session ENDED (E3b.S1 step 7). Pure: `bareOutcome` reads the records a launch leaves and returns one class, first match wins.
// `sessionOutcome` (readback.mjs) labels a watchdog kill `void: stopped`; here a watchdog kill is a DATA point, censored. A class is never decided by
// the exit code and never by a scorer fault (a session is never voided for one).
//
//   1 stop: alias drift   2 stop: security   3 stop: auth (†)   4 void: contaminated   (hand verdict -> void: harness)
//   5 void: harness       6 void: harness (interrupted | disposed) (†)   7 killed: cap   8 void: rate limit   9 void: upstream (†)
//   10 killed: wall | stall   11 completed   12 crashed: <subtype>          († = a registered extension of parent §11.1)
import { rateLimitSignal } from '../../policy-ab-quick/lib/ratelimit.mjs';
import { stopRuleFigure } from './spend-gate.mjs';

const is2xx = s => typeof s === 'number' && s >= 200 && s < 300;

/** The last request the broker finished (by seq): `null` | `{ kind: 'ok' }` | `{ kind: 'refused', reason }` | `{ kind: 'status', status, error }`. */
export function finalRequest(brokerLines) {
  const last = brokerLines.filter(l => l.event === 'settled' || l.event === 'refused').reduce((a, l) => (a === null || l.seq > a.seq ? l : a), null);
  if (last === null) return null;
  if (last.event === 'refused') return { kind: 'refused', reason: last.reason ?? 'unknown', seq: last.seq };
  if (is2xx(last.status) && !last.error) return { kind: 'ok', seq: last.seq };
  return { kind: 'status', status: last.status ?? null, error: last.error ?? null, seq: last.seq };
}

const answered = brokerLines => brokerLines.some(l => l.event === 'settled' && is2xx(l.status) && l.usage);

/** Earlier cap refusals the session survived, with their summed backoff: from each refusal to the next admitted request. */
function refusalsSurvived(brokerLines, final) {
  const refusals = brokerLines.filter(l => l.event === 'refused' && l.seq !== final?.seq);
  const waits = refusals.flatMap(r => {
    const next = brokerLines.find(l => l.event === 'forwarding' && l.seq > r.seq);
    return next ? [Date.parse(next.t) - Date.parse(r.t)] : [];
  });
  return { count: waits.length, backoffMs: waits.reduce((a, b) => a + b, 0) };
}

const upstream429s = (brokerLines, final) => brokerLines.filter(l => l.event === 'settled' && l.status === 429 && l.seq !== final?.seq).length;

/** The flags of a session: facts that never change its class. */
function flagsOf({ brokerLines, final, aliasCheck, meta }) {
  const refusals = refusalsSurvived(brokerLines, final);
  const n429 = upstream429s(brokerLines, final);
  return [
    ...(refusals.count > 0 ? [`broker refused ${refusals.count}`] : []),
    ...(n429 > 0 ? [`upstream 429 ${n429}`] : []),
    ...(aliasCheck.verdict === 'flag' ? ['model_refusal_fallback'] : []),
    ...(meta.broker?.overCap ? ['over cap'] : []),
  ];
}

const result = (cls, { data = false, stop = false, censoredAtUsd = null, detail = {} } = {}) => ({ class: cls, data, stop, censoredAtUsd, detail });

const realTokenSeen = ({ meta, scan }) => scan.hits > 0 || (meta.samplers?.realInCmdline ?? 0) + (meta.samplers?.realInEnviron ?? 0) > 0;
const AUTH = new Set([401, 403]);

/** The rules, in order; each takes the facts and returns a result or null. */
const RULES = [
  ({ aliasCheck }) => (aliasCheck.verdict === 'stop' ? result('stop: alias drift', { stop: true, detail: { reasons: aliasCheck.reasons } }) : null),
  c => (realTokenSeen(c) ? result('stop: security', { stop: true, detail: { reason: 'the real token was counted by a sampler or the session scan' } }) : null),
  ({ final, envelope }) => {
    const status = final?.kind === 'status' ? final.status : envelope?.is_error ? envelope.api_error_status : null;
    return AUTH.has(status) ? result('stop: auth', { stop: true, detail: { status } }) : null;
  },
  ({ contamination }) => (contamination.high.length + contamination.canary.length + contamination.tokenHits.length > 0
    ? result('void: contaminated', { detail: { high: contamination.high.length, canary: contamination.canary.length, tokenHits: contamination.tokenHits.length } }) : null),
  ({ handVerdicts }) => {
    const refuted = handVerdicts.filter(v => v.verdict === 'refuted');
    return refuted.length > 0 ? result('void: harness', { detail: { reason: `hand verdict refuted ${refuted.map(v => v.g).join(', ')}` } }) : null;
  },
  ({ meta, brokerLines }) => (meta.stopped === 'readback' || !answered(brokerLines)
    ? result('void: harness', { detail: { reason: meta.stopped === 'readback' ? 'the init read-back mismatched' : 'no settled 2xx request with parsed usage: the model never answered' } }) : null),
  ({ meta, retired, abortReason }) => {
    if (meta.stopped === 'cancelled') return result('void: harness (interrupted)', { detail: { reason: abortReason ?? 'cancelled, no reason recorded' } });
    return meta.stopped === 'disposed' || retired ? result('void: harness (disposed)', { detail: { reason: 'ended with no recorded reason' } }) : null;
  },
  // Row 7 BEFORE row 8: the broker answers its cap refusal with HTTP 429 `rate_limit_error`, which `rateLimitSignal` alone would void and pay for twice.
  ({ meta, envelope, final, figure }) => {
    const ended = !envelope || envelope.is_error === true;
    const byRefusal = ended && final?.kind === 'refused' && final.reason === 'cap';
    return meta.stopped === 'cost' || envelope?.subtype === 'error_max_budget_usd' || byRefusal
      ? result('killed: cap', { data: true, censoredAtUsd: figure, detail: { by: meta.stopped === 'cost' ? 'settled cost watchdog' : byRefusal ? 'broker cap refusal' : 'the CLI budget stop' } }) : null;
  },
  ({ events, envelope }) => {
    const limit = rateLimitSignal(events);
    return limit.void && (!envelope || envelope.is_error === true) ? result('void: rate limit', { detail: { via: limit.via, status: limit.status, message: limit.message } }) : null;
  },
  ({ envelope, final }) => (envelope?.is_error === true || !envelope) && final?.kind === 'status' && (final.status >= 500 || (final.status === null && final.error))
    ? result('void: upstream', { detail: { status: final.status, error: final.error } }) : null,
  ({ meta, figure }) => (meta.stopped === 'wall' || meta.stopped === 'stall' ? result(`killed: ${meta.stopped}`, { data: true, censoredAtUsd: figure }) : null),
  ({ envelope }) => (envelope?.subtype === 'success' && envelope.is_error === false ? result('completed', { data: true }) : null),
  ({ envelope }) => result(`crashed: ${envelope?.subtype ?? 'no result'}`, { data: true }),
];

/**
 * `{ meta, events, brokerLines, aliasCheck, scan: { hits }, contamination: { high, canary, tokenHits }, handVerdicts, retired, abortReason, recomputeUsd }`
 * -> `{ class, data, stop, censoredAtUsd, flags, detail }`. `data` says whether the session is a data point; `censoredAtUsd` is the stop-rule figure
 * (the largest of the envelope's total, the headline recompute and the broker's `B`) when the session was cut short.
 */
export function bareOutcome(input) {
  const envelope = input.events.findLast(e => e.type === 'result') ?? null;
  const final = finalRequest(input.brokerLines);
  const figure = stopRuleFigure({ envelopeUsd: input.meta.envelopeCostUsd ?? null, recomputeUsd: input.recomputeUsd ?? null, brokerUsd: input.meta.broker?.spentUsd ?? null });
  const facts = { ...input, envelope, final, figure };
  const hit = RULES.map(rule => rule(facts)).find(r => r !== null);
  return { ...hit, flags: flagsOf(facts), detail: { ...hit.detail, brokerRefused: refusalsSurvived(input.brokerLines, final) } };
}
