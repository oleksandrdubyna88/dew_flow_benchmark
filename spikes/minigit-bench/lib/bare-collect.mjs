// Reading a finished (or cut-short) session back and classifying how it ENDED (E3b.S1 steps 5, 7): the evidence files of its directory, the scans that decide
// `stop: security` and `void: contaminated`, the alias check, E1's views and the cost, then `bareOutcome`. No model call and no write: the runner writes.
import { readFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { checkAliases, aliasRecord } from './aliases.mjs';
import { bareOutcome } from './bare-outcome.mjs';
import { contaminationHits } from './contamination.mjs';
import { emptyCounts } from './proc-sampler.mjs';
import { loadSessionDir } from './session-load.mjs';
import { filesUnder, realTokenHits, scanSessionDir } from './session-scan.mjs';
import { sessionCost } from './session-cost.mjs';
import { sessionSpendUsd, stopRuleFigure } from './spend-gate.mjs';
import { tokenHits } from './token-scan.mjs';
import { smokeViews } from './smoke-verdicts.mjs';
import { callsOf } from './smoke-run.mjs';

/** The directories whose files the model could have written or read: what a canary or a credential could show up in (never the caches, never the harness). */
const EVIDENCE = Object.freeze(['stream.ndjson', 'transcripts', path.join('box', 'out'), path.join('box', 'work')]);

/** `[{ source, text }]` of the evidence files (read as latin1, so a binary file is searchable). */
function evidenceTexts(layout) {
  return filesUnder(layout.dir).filter(f => EVIDENCE.some(e => f.rel === e || f.rel.startsWith(`${e}${path.sep}`)) && f.size <= 16 * 1024 * 1024)
    .map(f => ({ source: f.rel, text: readFileSync(path.join(layout.dir, f.rel), 'latin1') }));
}

const disposedMeta = brokerLines => ({ stopped: 'disposed', envelopeCostUsd: null, broker: { spentUsd: sessionSpendUsd(brokerLines), overCap: false }, samplers: emptyCounts() });

/**
 * `launched` is what `launchSession` returned (`ok({ meta })` or `fail`); with a `fail` and a directory that still holds paid work the session is a `disposed` harness void.
 * -> `{ loaded, views, cost, alias, aliasCheck, outcome, usd, meta, scan }` | `fail(reason)` when the evidence cannot be read.
 */
export function collectSession({ layout, plan, launched, canaryTokens = [], aliasBaseline, handVerdicts = [], retired = false, abortReason = null, partnerDirs = [], table, policy, home = os.homedir() }) {
  const loaded = loadSessionDir(layout);
  if (!loaded.ok) return loaded;
  const { events, brokerLines } = loaded.value;
  const meta = launched?.ok ? launched.value.meta : loaded.value.meta ?? disposedMeta(brokerLines);
  const views = smokeViews(loaded.value, { table, policy });
  const cost = sessionCost({ E: views.E, O: views.O, T: views.T, table });
  const alias = aliasRecord({ events });
  const baseline = aliasBaseline ?? (alias.ok ? { aliases: alias.value.aliases, internal: alias.value.internal } : null); // the first session of a series SETS the baseline (D12)
  const aliasCheck = alias.ok ? checkAliases(baseline, alias.value) : { verdict: 'stop', reasons: [alias.reason] };
  const scan = scanSessionDir(layout.dir, { real: plan.token, dummy: plan.dummy });
  const texts = evidenceTexts(layout);
  const ctx = { rid: layout.rid, treeRoot: layout.work, home, harnessPaths: [layout.harness, layout.net], partnerPaths: partnerDirs, arm: 'bare', ownSecrets: [plan.token, plan.dummy] };
  const contamination = {
    high: contaminationHits({ calls: callsOf(events), ctx }).filter(h => h.severity === 'high'),
    canary: canaryTokens.length > 0 ? tokenHits(texts, canaryTokens).filter(h => h.kind === 'own-value') : [], // own values only: a credential SHAPE is `tokenHits` below
    tokenHits: realTokenHits(texts, { real: plan.token, dummy: plan.dummy }),
  };
  const recomputeUsd = cost.known ? cost.usd : null;
  const outcome = bareOutcome({ meta, events, brokerLines, aliasCheck, scan: { hits: scan.hits.length }, contamination, handVerdicts, retired, abortReason, recomputeUsd });
  const usd = Math.max(stopRuleFigure({ envelopeUsd: meta.envelopeCostUsd ?? null, recomputeUsd, brokerUsd: meta.broker?.spentUsd ?? null }) ?? 0, sessionSpendUsd(brokerLines));
  return { ok: true, value: { loaded: loaded.value, views, cost, alias, aliasCheck, baselineWas: aliasBaseline, outcome, usd, meta, scan: { scanned: scan.scanned, tooLarge: scan.tooLarge, hits: scan.hits.length } } };
}
