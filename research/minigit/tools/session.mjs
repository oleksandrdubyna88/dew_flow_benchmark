// Reduces one session directory of a minigit bare series to a plain object.
// READ-ONLY: it opens record.json, meta.json, broker.json and stream.ndjson (the result envelope only,
// for a session the series stopped before it was recorded). It never opens score/ (held-out cases),
// never copies a canary or a dummy token, and keeps held-out results as counts and percentages only.
import { existsSync, readFileSync } from 'node:fs';
import { join } from 'node:path';

const readJson = file => (existsSync(file) ? JSON.parse(readFileSync(file, 'utf8')) : null);
const known = cell => (cell && cell.known ? cell.value : null);
const sum = xs => xs.reduce((a, b) => a + b, 0);

/** Replaces the operator's home prefix so no private absolute path leaves the machine. */
export const tidyPath = (text, home) => (typeof text === 'string' && home ? text.split(home).join('~') : text);

/** The last `result` line of the stream: the CLI's envelope (used only when no record exists). */
function envelopeOf(dir) {
  const file = join(dir, 'stream.ndjson');
  if (!existsSync(file)) return null;
  const lines = readFileSync(file, 'utf8').split('\n').filter(l => l.includes('"type":"result"'));
  return lines.length ? JSON.parse(lines[lines.length - 1]) : null;
}

/** The broker's settled requests, summed: B's own view of the tokens and the 5m / 1h cache-write split. */
function brokerView(broker) {
  if (!broker) return null;
  const settled = broker.settled ?? [];
  const u = k => sum(settled.map(s => s.usage?.[k] ?? 0));
  const split = k => sum(settled.map(s => s.usage?.cache_creation?.[k] ?? 0));
  return {
    forwarded: broker.forwarded,
    refused: broker.refused,
    refusedByReason: broker.refusedByReason,
    unsettled: broker.unsettled,
    spentUsd: broker.spentUsd,
    lowerBound: broker.lowerBound,
    settledCount: settled.length,
    statuses: [...new Set(settled.map(s => s.status))],
    models: [...new Set(settled.map(s => s.model))],
    tokens: {
      input: u('input_tokens'),
      output: u('output_tokens'),
      cacheRead: u('cache_read_input_tokens'),
      cacheWrite: u('cache_creation_input_tokens'),
      cacheWrite5m: split('ephemeral_5m_input_tokens'),
      cacheWrite1h: split('ephemeral_1h_input_tokens'),
    },
  };
}

function costOf(rec, meta) {
  const c = rec.cost;
  const head = known(c.headlineUsd);
  return {
    headlineUsd: head ? head.usd : null,
    headlineExact: head ? head.exact : null,
    headlineBasis: head ? head.basis : null,
    eUsd: known(c.cliTotalUsd),
    bUsd: known(c.brokerUsd),
    oUsd: meta?.receiver?.usd ?? null,
    stopRuleUsd: known(c.stopRuleUsd),
    censoredAtUsd: known(c.censoredAtUsd),
    firstTurnCacheRead: known(c.firstTurnCacheRead),
    checks: Object.fromEntries(Object.entries(known(c.checks) ?? {}).map(([k, v]) => [k, v.status])),
    perModel: Object.fromEntries(Object.entries(known(c.perModel) ?? {}).map(([m, v]) => [m, { usd: v.usd, exact: v.exact, share1h: known(v.share1h) }])),
  };
}

function tokensOf(rec) {
  const tokens = known(rec.cost.tokens) ?? {};
  const perModel = known(rec.cost.perModel) ?? {};
  return Object.fromEntries(Object.entries(tokens).map(([m, t]) => {
    const share = known(perModel[m]?.share1h);
    const w1h = share === null || share === undefined ? null : Math.round(t.cacheWrite * share);
    return [m, { ...t, cacheWrite1h: w1h, cacheWrite5m: w1h === null ? null : t.cacheWrite - w1h }];
  }));
}

function timeOf(rec) {
  const t = rec.time;
  const part = known(t.partition);
  const cats = part ? part.categories : {};
  const desc = known(t.descriptive);
  return {
    wallMs: known(t.wallMs),
    modelApiMs: known(t.modelApiMs),
    activeMs: known(t.activeMs),
    activeMinusModelApiMs: known(t.activeMinusModelApiMs),
    buildTestMs: part ? cats.build_test : null,
    otherToolMs: part ? cats.other_tool : null,
    hooksMs: part ? cats.hooks : null,
    unattributedMs: part ? part.unattributed : null,
    partitionSumMs: part ? sum(Object.values(cats)) : null,
    durationApiMs: desc ? known(desc.durationApiMs) : null,
    turns: known(t.turns)?.envelope ?? null,
    turnsAgree: known(t.turns)?.agree ?? null,
    subagentsLaunched: known(t.children)?.launched ?? null,
  };
}

function qualityOf(rec) {
  const q = rec.quality;
  const held = known(q.heldout);
  const lat = known(q.latency);
  return {
    scored: q.buildOk?.known === true,
    notScoredWhy: q.buildOk?.known ? null : q.buildOk?.why ?? null,
    buildOk: known(q.buildOk),
    visible: (known(q.visible) ?? []).map(v => ({ script: v.script, passed: v.passed, failed: v.failed, expectedTotal: v.expectedTotal })),
    heldout: held ? { percent: held.percent, defects: held.defects?.count ?? null, blocked: held.blocked, failed: held.failed, total: held.total, primary100: held.primary100 } : null,
    upstreamOriginal: (known(q.upstreamOriginal) ?? []).map(v => ({ script: v.script, passed: v.passed, expectedTotal: v.expectedTotal })),
    latency: lat ? { command: lat.command, runs: lat.runs, medianMs: lat.medianMs, boxBaselineMedianMs: lat.boxBaselineMedianMs } : null,
  };
}

function hostOf(rec, home) {
  const egress = known(rec.egress);
  const pk = known(rec.packages);
  const quiet = known(rec.run.quiet);
  const contamination = rec.detail?.contamination?.high ?? null;
  return {
    mask: known(rec.run.mask),
    maskObserved: known(rec.run.maskObserved),
    startSkewMs: known(rec.run.startSkewMs),
    quietBefore: quiet ? { quiet: quiet.quiet, flags: quiet.flags, waitedMs: quiet.waitedMs ?? null, ...quiet.numbers } : null,
    harnessIdentity: known(rec.run.harnessIdentity),
    loc: known(rec.loc),
    egress: egress ? { allowed: egress.allowed, refused: egress.refused, nonConnect: egress.nonConnect, bytes: egress.bytes, cacheGrowthBytes: egress.cacheGrowthBytes } : null,
    packages: pk ? { total: pk.packages.length, added: pk.added, flagged: pk.flagged } : null,
    workspaceExcluded: known(rec.workspace)?.excludedDirs ?? null,
    voidDetail: rec.class.startsWith('void') ? {
      high: rec.detail?.high ?? null,
      canary: rec.detail?.canary ?? null,
      tokenHits: rec.detail?.tokenHits ?? null,
      hits: contamination ? contamination.map(h => ({ rule: h.rule, source: h.source, where: h.where, excerpt: tidyPath(h.excerpt, home) })) : null,
    } : null,
  };
}

function metaOf(meta) {
  if (!meta) return null;
  const s = meta.samplers ?? {};
  return {
    startedAt: meta.startedAt,
    exitedAt: meta.exitedAt ?? null,
    endedAt: meta.endedAt,
    wallMs: meta.wallMs,
    exitCode: meta.exit?.code ?? null,
    stopped: meta.stopped,
    envelopeCostUsd: meta.envelopeCostUsd,
    receiverUsd: meta.receiver?.usd ?? null,
    receiverRequests: meta.receiver?.requests ?? null,
    realTokenSeen: (s.realInCmdline ?? 0) + (s.realInEnviron ?? 0),
    samplerSamples: s.samples ?? null,
    overCap: meta.broker?.overCap ?? null,
    unsettledAtClose: meta.unsettledAtClose?.count ?? null,
  };
}

/** A session the series stopped before it was recorded: what the envelope, meta and broker still say. */
function unrecorded(dir, mapEntry, meta, broker) {
  const env = envelopeOf(dir);
  const mu = env?.modelUsage ?? {};
  return {
    recorded: false,
    recordMissingWhy: 'the series stopped before this session was recorded and scored',
    cost: { headlineUsd: null, eUsd: env?.total_cost_usd ?? null, bUsd: broker?.spentUsd ?? null, oUsd: meta?.receiver?.usd ?? null, checks: {} },
    tokens: Object.fromEntries(Object.entries(mu).map(([m, t]) => [m, {
      input: t.inputTokens, output: t.outputTokens, cacheRead: t.cacheReadInputTokens, cacheWrite: t.cacheCreationInputTokens,
      cacheWrite5m: env?.usage?.cache_creation?.ephemeral_5m_input_tokens ?? null, cacheWrite1h: env?.usage?.cache_creation?.ephemeral_1h_input_tokens ?? null,
      source: 'envelope',
    }])),
    time: { wallMs: meta?.wallMs ?? null, modelApiMs: null, activeMinusModelApiMs: null, durationApiMs: env?.duration_api_ms ?? null, turns: env?.num_turns ?? null },
    quality: { scored: false, notScoredWhy: 'the series stopped before this session was scored' },
    host: { mask: mapEntry.mask ?? null },
  };
}

/** One session: the run-map entry joined with its files. */
export function readSession(seriesDir, rid, mapEntry, home) {
  const dir = join(seriesDir, 's', rid);
  const rec = readJson(join(dir, 'record.json'));
  const meta = readJson(join(dir, 'meta.json'));
  const broker = readJson(join(dir, 'broker.json'));
  const ids = {
    rid, kind: mapEntry.kind, stack: mapEntry.stack, phase: mapEntry.phase, attempt: mapEntry.attempt,
    pair: mapEntry.pair, label: mapEntry.label, status: mapEntry.status, class: mapEntry.class, data: mapEntry.data,
    mapFlags: mapEntry.run?.flags ?? [], startedAt: mapEntry.startedAt, runMapUsd: mapEntry.usd,
  };
  const body = rec
    ? { recorded: true, flags: rec.flags, discrepancies: rec.discrepancies, reclassified: rec.reclassified, cost: costOf(rec, meta), tokens: tokensOf(rec), time: timeOf(rec), quality: qualityOf(rec), host: hostOf(rec, home) }
    : unrecorded(dir, mapEntry, meta, broker);
  return { ...ids, ...body, meta: metaOf(meta), broker: brokerView(broker) };
}
