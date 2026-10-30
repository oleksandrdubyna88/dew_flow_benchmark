// Reading a finished (or cut-short) session back and classifying how it ENDED (E3b.S1 steps 5, 7): the evidence files of its directory, the scans that decide
// `stop: security` and `void: contaminated`, the alias check, E1's views and the cost, then `bareOutcome`. No model call and no write: the runner writes.
import { existsSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { checkAliases, aliasRecord } from './aliases.mjs';
import { bareOutcome } from './bare-outcome.mjs';
import { contaminationHits } from './contamination.mjs';
import { emptyCounts } from './proc-sampler.mjs';
import { ok } from './result.mjs';
import { loadSessionDir } from './smoke-load.mjs';
import { SCAN_LIMITS, filesUnder, realTokenHits, scanFiles, scanSessionDir } from './session-scan.mjs';
import { sessionCost } from './session-cost.mjs';
import { sessionSpendUsd, stopRuleFigure } from './spend-gate.mjs';
import { tokenHits } from './token-scan.mjs';
import { smokeViews } from './smoke-verdicts.mjs';
import { callsOf } from './stream-calls.mjs';

/** The directories whose files the model could have written or read: what a canary or a credential could show up in (never the caches, never the harness). */
const EVIDENCE = Object.freeze(['stream.ndjson', 'transcripts', path.join('box', 'out'), path.join('box', 'work')]);

/** The evidence files (regular files only, of any size): what the model could have written or read. A link is not followed; the session scan lists links as findings. */
const evidenceFiles = layout => filesUnder(layout.dir).filter(f => f.kind === 'file' && EVIDENCE.some(e => f.rel === e || f.rel.startsWith(`${e}${path.sep}`))).map(f => f.rel);

const disposedMeta = brokerLines => ({ stopped: 'disposed', envelopeCostUsd: null, broker: { spentUsd: sessionSpendUsd(brokerLines), overCap: false }, samplers: emptyCounts() });

/**
 * `launched` is what `launchSession` returned (`ok({ meta })` or `fail`); with a `fail` and a directory that still holds paid work the session is a `disposed` harness void.
 * -> `{ loaded, views, cost, alias, aliasCheck, outcome, usd, meta, scan }` | `fail(reason)` when the evidence cannot be read.
 */
export function collectSession({ layout, plan, launched, canaryTokens = [], aliasBaseline, handVerdicts = [], retired = false, abortReason = null, partnerDirs = [], table, policy, home = os.homedir(), scanLimits = SCAN_LIMITS }) {
  const loaded = loadSessionDir(layout.dir);
  if (!loaded.ok) return loaded;
  const { events, brokerLines } = loaded.value;
  const meta = launched?.ok ? launched.value.meta : loaded.value.meta ?? disposedMeta(brokerLines);
  const views = smokeViews(loaded.value, { table, policy });
  const cost = sessionCost({ E: views.E, O: views.O, T: views.T, table });
  const alias = aliasRecord({ events });
  const baseline = aliasBaseline ?? (alias.ok ? { aliases: alias.value.aliases, internal: alias.value.internal } : null); // the first session of a series SETS the baseline (D12)
  const aliasCheck = alias.ok ? checkAliases(baseline, alias.value) : { verdict: 'stop', reasons: [alias.reason] };
  const scan = scanSessionDir(layout.dir, { real: plan.token, dummy: plan.dummy });
  // every evidence file goes through the streamed scan of the session scan (whole up to its limit, windows above it): a big file is the one an agent would hide a token in
  const files = evidenceFiles(layout);
  const canaryScan = canaryTokens.length > 0 ? scanFiles(layout.dir, files, text => tokenHits([{ source: '', text }], canaryTokens).filter(h => h.kind === 'own-value'), scanLimits) : { hits: [], unreadable: [] };
  const tokenScan = scanFiles(layout.dir, files, text => realTokenHits([{ source: '', text }], { real: plan.token, dummy: plan.dummy }), scanLimits);
  const unscanned = [...new Map([...canaryScan.unreadable, ...tokenScan.unreadable].map(u => [u.rel, u])).values()].map(u => ({ source: u.rel, kind: 'unscanned', why: u.why }));
  const ctx = { rid: layout.rid, treeRoot: layout.work, home, harnessPaths: [layout.harness, layout.net], partnerPaths: partnerDirs, arm: 'bare', ownSecrets: [plan.token, plan.dummy] };
  const contamination = {
    high: contaminationHits({ calls: callsOf(events), ctx }).filter(h => h.severity === 'high'),
    canary: canaryScan.hits, // own values only: a credential SHAPE is `tokenHits` below
    tokenHits: [...tokenScan.hits, ...unscanned], // evidence no scan could read is never a quiet pass: it is a hit, named by file and reason
  };
  const recomputeUsd = cost.known ? cost.usd : null;
  const classed = bareOutcome({ meta, events, brokerLines, aliasCheck, scan: { hits: scan.hits.length }, contamination, handVerdicts, retired, abortReason, recomputeUsd });
  // T1 (the envelope disagrees with itself) under policy `stop` is a SERIES stop beside the class (registered, review M4): the class is how the session ended and stays, the flag says why the series stopped
  const t1Stop = views.c.t1.verdict === 'stop' ? { rule: 'T1 breach', detail: views.c.t1.detail } : null;
  const t2 = views.c.t2;
  const t2Incomplete = t2.status === 'breach' || (t2.status === 'unchecked' && /otel|lower bound/.test(t2.why ?? '')); // T2: O is not the envelope, or is incomplete: a flag, never a class; the cell stays and is marked
  const figure = stopRuleFigure({ envelopeUsd: meta.envelopeCostUsd ?? null, recomputeUsd, brokerUsd: meta.broker?.spentUsd ?? null });
  const measured = stopRuleFigure({ envelopeUsd: meta.envelopeCostUsd ?? null, recomputeUsd, brokerUsd: null }); // the broker figure of a session with no meta is a synthetic 0 (`disposedMeta`)
  // A spend of 0 is a MEASUREMENT only when some basis exists: a figure, or a broker ledger that saw no request. With neither, 0 is a gap in the evidence and is flagged, never silent.
  const costCaptured = measured !== null || existsSync(layout.files.brokerLog);
  const outcome = { ...classed, flags: [...classed.flags, ...(t1Stop ? ['T1 breach'] : []), ...(t2Incomplete ? ['otel incomplete'] : []), ...(costCaptured ? [] : ['cost not captured'])] };
  const usd = Math.max(figure ?? 0, sessionSpendUsd(brokerLines));
  return ok({ loaded: loaded.value, views, cost, alias, aliasCheck, baselineWas: aliasBaseline, outcome, t1Stop, usd, costCaptured, meta, scan: { scanned: scan.scanned, tooLarge: scan.tooLarge, hits: scan.hits.length } });
}
