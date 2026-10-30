// Reading a finished (or cut-short) session back and classifying how it ENDED (E3b.S1 steps 5, 7): the evidence files of its directory, the scans that decide
// `stop: security` and `void: contaminated`, the alias check, E1's views and the cost, then `bareOutcome`. No model call and no write: the runner writes.
import { existsSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { checkAliases, aliasRecord } from './aliases.mjs';
import { bareOutcome } from './bare-outcome.mjs';
import { contaminationHits } from './contamination.mjs';
import { seriesStopsOf } from './crosscheck.mjs';
import { emptyCounts } from './proc-sampler.mjs';
import { ok } from './result.mjs';
import { loadSessionDir } from './smoke-load.mjs';
import { SCAN_LIMITS, scanSessionDir } from './session-scan.mjs';
import { sessionCost } from './session-cost.mjs';
import { sessionSpendUsd, stopRuleFigure } from './spend-gate.mjs';
import { tokenHits } from './token-scan.mjs';
import { smokeViews } from './smoke-verdicts.mjs';
import { callsOf } from './stream-calls.mjs';

/** The directories whose files the model could have written or read: what a canary or a credential could show up in (never the caches, never the harness). */
const EVIDENCE = Object.freeze(['stream.ndjson', 'transcripts', path.join('box', 'out'), path.join('box', 'work')]);

/** Is this file (a path relative to the session directory) evidence: what the model could have written or read? A link is not followed; the session scan lists links as findings. */
const isEvidence = rel => EVIDENCE.some(e => rel === e || rel.startsWith(`${e}${path.sep}`));

const disposedMeta = brokerLines => ({ stopped: 'disposed', envelopeCostUsd: null, broker: { spentUsd: sessionSpendUsd(brokerLines), overCap: false }, samplers: emptyCounts() });

/**
 * `launched` is what `launchSession` returned (`ok({ meta })` or `fail`); with a `fail` and a directory that still holds paid work the session is a `disposed` harness void.
 * -> `{ loaded, views, cost, alias, aliasCheck, outcome, usd, meta, scan }` | `fail(reason)` when the evidence cannot be read.
 */
export function collectSession({ layout, plan, launched, canaryTokens = [], aliasBaseline, handVerdicts = [], retired = false, abortReason = null, partnerDirs = [], table, policy, home = os.homedir(), scanLimits = SCAN_LIMITS, scanHooks = {} }) {
  const loaded = loadSessionDir(layout.dir);
  if (!loaded.ok) return loaded;
  const { events, brokerLines } = loaded.value;
  const meta = launched?.ok ? launched.value.meta : loaded.value.meta ?? disposedMeta(brokerLines);
  const views = smokeViews(loaded.value, { table, policy });
  const cost = sessionCost({ E: views.E, O: views.O, T: views.T, table });
  const alias = aliasRecord({ events });
  const baseline = aliasBaseline ?? (alias.ok ? { aliases: alias.value.aliases, internal: alias.value.internal } : null); // the first session of a series SETS the baseline (D12)
  const aliasCheck = alias.ok ? checkAliases(baseline, alias.value) : { verdict: 'stop', reasons: [alias.reason] };
  // ONE streamed pass over every file (review C2): the session scan, the canary scan and the real-token scan ride on the same read of each evidence file, whole up to its limit and in
  // overlapping windows above it (a big file is the one an agent would hide a token in), never skipped; evidence no scan could read is a named `unscanned` hit, not a quiet pass
  const canary = canaryTokens.length > 0 ? [{ name: 'canary', applies: isEvidence, scanText: text => tokenHits([{ source: '', text }], canaryTokens).filter(h => h.kind === 'own-value') }] : [];
  const scan = scanSessionDir(layout.dir, { real: plan.token, dummy: plan.dummy, limits: scanLimits, hooks: scanHooks, extra: canary });
  const canaryHits = scan.extra.canary ?? [];
  const realHits = scan.hits.filter(h => h.kind === 'own-value'); // row 2: the real token itself (a hit of any other kind is a credential SHAPE: row 4, review N8)
  const shapeHits = scan.hits.filter(h => h.kind !== 'own-value'); // in any file of the session directory, not only the evidence files
  const unscanned = scan.unreadable.filter(u => isEvidence(u.rel)).map(u => ({ source: u.rel, kind: 'unscanned', why: u.why }));
  const ctx = { rid: layout.rid, treeRoot: layout.work, home, harnessPaths: [layout.harness, layout.net], partnerPaths: partnerDirs, arm: 'bare', ownSecrets: [plan.token, plan.dummy] };
  const contamination = {
    high: contaminationHits({ calls: callsOf(events), ctx }).filter(h => h.severity === 'high'),
    canary: canaryHits, // own values only: a credential SHAPE is `tokenHits` below
    tokenHits: [...shapeHits, ...unscanned], // evidence no scan could read is never a quiet pass: it is a hit, named by file and reason
  };
  const recomputeUsd = cost.known ? cost.usd : null;
  const classed = bareOutcome({ meta, events, brokerLines, aliasCheck, scan: { hits: realHits.length }, contamination, handVerdicts, retired, abortReason, recomputeUsd });
  // a check whose policy is `stop` and that breaches is a SERIES stop beside the class (T1, T2, T3 and T5 alike, a breach or, failing closed, evidence that is missing: registered, review M4, N3 and N11): the class is how the session ended and stays, the flag says why the series stopped
  const seriesStops = seriesStopsOf(views.c, policy, { enveloped: views.E.known, ended: classed.class });
  const t2 = views.c.t2;
  // T2: O is not the envelope, or is incomplete: the flag `otel incomplete`, never a class; the cell stays and is marked. One flag per cause (review N10): when T2 IS the series stop, `T2 breach` is the flag and this one is not added
  const t2Incomplete = !seriesStops.some(x => x.check === 'T2') && (t2.status === 'breach' || (t2.status === 'unchecked' && /otel|lower bound/.test(t2.why ?? '')));
  const figure = stopRuleFigure({ envelopeUsd: meta.envelopeCostUsd ?? null, recomputeUsd, brokerUsd: meta.broker?.spentUsd ?? null });
  const measured = stopRuleFigure({ envelopeUsd: meta.envelopeCostUsd ?? null, recomputeUsd, brokerUsd: null }); // the broker figure of a session with no meta is a synthetic 0 (`disposedMeta`)
  // A spend of 0 is a MEASUREMENT only when some basis exists: a figure, or a broker ledger that saw no request. With neither, 0 is a gap in the evidence and is flagged, never silent.
  const costCaptured = measured !== null || existsSync(layout.files.brokerLog);
  const outcome = { ...classed, flags: [...classed.flags, ...seriesStops.map(x => x.rule), ...(t2Incomplete ? ['otel incomplete'] : []), ...(costCaptured ? [] : ['cost not captured'])] };
  const usd = Math.max(figure ?? 0, sessionSpendUsd(brokerLines));
  return ok({ loaded: loaded.value, views, cost, alias, aliasCheck, baselineWas: aliasBaseline, outcome, seriesStops, usd, costCaptured, meta, scan: { scanned: scan.scanned, tooLarge: scan.tooLarge, hits: scan.hits.length } });
}
