#!/usr/bin/env node
// Read-only extractor for the minigit bare series records.
//
//   node research/minigit/tools/extract.mjs [--runs <dir>] [--series <id> ...]
//
// --runs defaults to $MINIGIT_RUNS or ~/rsd/minigit. --series defaults to every finished series named in
// SERIES below. For each series it reads run-map.json, ledger.ndjson, aa-verdicts.json, rehearsal-v2.json,
// pairs/*.json, pairs/*-cpu.ndjson, handcheck/*.txt and every session's record.json, meta.json and
// broker.json, and writes research/minigit/data/<series>.json. It writes nothing under --runs.
// It never reads score/ (held-out cases), ~/.config, or a token; it drops canary tokens and dummy tokens
// from the run-map entries, and replaces the operator's home prefix with "~".
// Then: node research/minigit/tools/render.mjs (rewrites the generated blocks of the results record).
import { createHash } from 'node:crypto';
import { existsSync, mkdirSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import { homedir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { readSession, tidyPath } from './session.mjs';
import { buildPairs, perStack, qualityByStack, summarise } from './pairs.mjs';

export const SERIES = ['s-20261030-92a9', 's-20261030-0c1a', 's-20261030-bf89', 's-20261030-b82e'];
const HERE = dirname(fileURLToPath(import.meta.url));
const OUT = resolve(HERE, '..', 'data');

const readJson = file => (existsSync(file) ? JSON.parse(readFileSync(file, 'utf8')) : null);
const readLines = file => (existsSync(file) ? readFileSync(file, 'utf8').split('\n').filter(Boolean).map(l => JSON.parse(l)) : []);
const sha256 = file => createHash('sha256').update(readFileSync(file)).digest('hex');
const round6 = x => Math.round(x * 1e6) / 1e6;

/** A series id is one path component of the registered shape, so it can neither leave --runs nor name a file outside data/. */
const SERIES_ID = /^s-\d{8}-[0-9a-f]{4}$/;
function seriesId(id) {
  if (!SERIES_ID.test(String(id))) throw new Error(`--series ${JSON.stringify(id)} is not a series id (s-YYYYMMDD-xxxx)`);
  return id;
}

function args(argv) {
  const out = { runs: process.env.MINIGIT_RUNS || join(homedir(), 'rsd', 'minigit'), series: [] };
  for (let i = 0; i < argv.length; i++) {
    if (argv[i] === '--runs') out.runs = argv[++i];
    else if (argv[i] === '--series') out.series = [...out.series, seriesId(argv[++i])];
    else throw new Error(`unknown argument ${argv[i]}; legal: --runs <dir>, --series <id>`);
  }
  return { ...out, series: out.series.length ? out.series : SERIES };
}

function header(map) {
  return {
    seriesId: map.seriesId, preregTag: map.preregTag, preregCommit: map.preregCommit, seedSha256: map.seedSha256,
    harnessIdentityAtInit: map.harnessIdentity, harnessAccepted: map.harnessAccepted ?? [], pins: map.pins,
    claude: { version: map.claude?.version, sha256: map.claude?.sha256 }, caps: map.caps, bareBudgetUsd: map.bareBudgetUsd,
    ceilingUsd: map.ceilingUsd, spin: map.spin, stopped: map.stopped ?? null, pricesFile: map.pricesFile,
    snapshots: map.snapshots, stacksSha256: map.stacksSha256, quietSha256: map.quietSha256, packageFlagsSha256: map.packageFlagsSha256,
    pairBarriers: map.pairs ?? {},
  };
}

/** The ledger: own lines, lines carried from earlier series, and the registered prior spend. */
function ledgerOf(seriesDir) {
  const lines = readLines(join(seriesDir, 'ledger.ndjson'));
  const spend = lines.filter(l => typeof l.usd === 'number');
  const total = xs => round6(xs.reduce((a, l) => a + l.usd, 0));
  const byKind = xs => Object.fromEntries([...new Set(xs.map(l => l.kind))].map(k => [k, total(xs.filter(l => l.kind === k))]));
  const prior = spend.filter(l => l.kind === 'prior');
  const bare = spend.filter(l => l.kind !== 'prior');
  const own = bare.filter(l => !l.fromSeries);
  return {
    lines: spend.map(l => ({ kind: l.kind, usd: l.usd, what: l.what, rid: l.rid ?? null, fromSeries: l.fromSeries ?? null, approx: l.approx ?? false, date: l.date ?? l.t ?? null })),
    events: lines.filter(l => l.event).map(l => ({ event: l.event, from: l.from, to: l.to, t: l.t })),
    priorUsd: total(prior), priorSessions: prior.length,
    ownBareUsd: total(own), ownByKind: byKind(own),
    bareUsdIncludingCarried: total(bare), bareByKind: byKind(bare),
    ceilingUsdSpent: total(spend),
  };
}

function aaOf(seriesDir, home) {
  const a = readJson(join(seriesDir, 'aa-verdicts.json'));
  if (!a) return { present: false, why: 'no aa-verdicts.json in this series: the tool (commit 2c2555a) was written after the series ran' };
  return {
    present: true, harnessIdentity: a.harnessIdentity, computedAt: a.computedAt, verdict: a.verdict, stops: a.stops, t3Rule: a.t3Rule,
    rows: a.rows.map(r => ({ id: r.id, title: r.title, verdict: r.verdict, observed: tidyPath(r.observed, home), replacing: tidyPath(r.replacing ?? null, home), recorded: r.recorded ?? null })),
  };
}

function cpuOf(seriesDir) {
  const dir = join(seriesDir, 'pairs');
  if (!existsSync(dir)) return [];
  return readdirSync(dir).filter(f => f.endsWith('-cpu.ndjson')).sort().map(f => {
    const xs = readLines(join(dir, f));
    const rids = [...new Set(xs.flatMap(x => x.liveSessions ?? []).map(p => p.split('/').pop()))];
    const stat = ys => (ys.length ? { samples: ys.length, meanForeignCpus: round6(ys.reduce((a, y) => a + y.foreignCpus, 0) / ys.length), maxForeignCpus: round6(Math.max(...ys.map(y => y.foreignCpus))), foreignAgentSamples: ys.filter(y => y.foreignAgents?.length).length, maxSpinMs: round6(Math.max(...ys.map(y => y.spinMs ?? 0))) } : null);
    const perSession = Object.fromEntries(rids.map(r => [r, stat(xs.filter(x => (x.liveSessions ?? []).some(p => p.endsWith(`/${r}`))))]));
    return { file: f, ...stat(xs), perSession };
  });
}

function handcheckOf(seriesDir, home) {
  const dir = join(seriesDir, 'handcheck');
  if (!existsSync(dir)) return [];
  return readdirSync(dir).sort().map(f => ({ file: f, sha256: sha256(join(dir, f)), text: tidyPath(readFileSync(join(dir, f), 'utf8'), home) }));
}

function pairRecords(seriesDir) {
  const dir = join(seriesDir, 'pairs');
  if (!existsSync(dir)) return {};
  return Object.fromEntries(readdirSync(dir).filter(f => /^p\d+\.json$/.test(f)).map(f => {
    const p = readJson(join(dir, f));
    return [p.pair, { cells: Object.fromEntries(Object.entries(p.cells).map(([k, c]) => [k, Object.fromEntries(Object.entries(c).filter(([x]) => x.endsWith('Usd') || x.endsWith('Ms')).map(([x, v]) => [x, v.value]))])), ratiosWith: Object.fromEntries(Object.entries(p.ratios).map(([k, r]) => [k, r.with?.value ?? null])), flags: p.flags }];
  }));
}

/** The harness's own pair records against this extractor's recomputation: a disagreement is a finding. */
function crosscheck(pairs, recorded) {
  const out = [];
  for (const p of pairs) {
    const r = recorded[p.pair];
    if (!r) { out.push({ pair: p.pair, ok: null, note: 'no pairs/p<k>.json (the series stopped before the pair record was written)' }); continue; }
    for (const st of ['csharp', 'node']) for (const [k, v] of Object.entries(p.cells[st].values ?? {})) {
      const theirs = r.cells[st]?.[k];
      if (theirs !== undefined && Math.abs(theirs - v) > 1e-5) out.push({ pair: p.pair, stack: st, measure: k, ours: v, theirs, ok: false });
    }
    for (const [k, rr] of Object.entries(p.ratios)) if (rr.known && r.ratiosWith[k] !== null && Math.abs(rr.value - r.ratiosWith[k]) > 1e-5) out.push({ pair: p.pair, measure: k, ours: rr.value, theirs: r.ratiosWith[k], ok: false });
    out.push({ pair: p.pair, ok: !out.some(x => x.pair === p.pair && x.ok === false), recordedFlags: r.flags });
  }
  return out;
}

/**
 * What each paid session was charged, from the run-map (the stop-rule figure), beside the record's stop-rule figure and
 * the broker's settled spend, and whether this series' own ledger holds a line for it. Series b82e reused the seed, so
 * its rids repeat bf89's; its ledger skipped its own charges for every rid it had copied from bf89 (fixed in 1bdbe75).
 */
function chargesOf(sessions, ledger) {
  const ownRids = new Set(ledger.lines.filter(l => !l.fromSeries && l.rid).map(l => l.rid));
  const rows = sessions.filter(s => s.kind !== 'rehearsal').map(s => ({
    rid: s.rid, kind: s.kind, class: s.class, runMapUsd: s.runMapUsd, recordStopRuleUsd: s.cost?.stopRuleUsd ?? null,
    brokerUsd: s.broker?.spentUsd ?? null, inOwnLedger: ownRids.has(s.rid),
  }));
  const total = xs => round6(xs.reduce((a, r) => a + (r.runMapUsd ?? 0), 0));
  const disagree = rows.filter(r => [r.recordStopRuleUsd, r.brokerUsd].some(v => v !== null && Math.abs(v - r.runMapUsd) > 1e-6)).map(r => r.rid);
  return { rows, fromRecordsUsd: total(rows), missingFromLedger: rows.filter(r => !r.inOwnLedger).map(r => r.rid), missingUsd: total(rows.filter(r => !r.inOwnLedger)), disagree };
}

function sources(seriesDir) {
  const files = ['run-map.json', 'ledger.ndjson', 'aa-verdicts.json', 'rehearsal-v2.json'].filter(f => existsSync(join(seriesDir, f)));
  return Object.fromEntries(files.map(f => [f, sha256(join(seriesDir, f))]));
}

export function extractSeries(runs, id, home) {
  const seriesDir = join(runs, id);
  const map = readJson(join(seriesDir, 'run-map.json'));
  if (!map) throw new Error(`${seriesDir}/run-map.json is missing`);
  const sessions = Object.entries(map.sessions).map(([rid, e]) => readSession(seriesDir, rid, e, home))
    .sort((a, b) => (a.startedAt ?? '').localeCompare(b.startedAt ?? ''));
  const pairs = buildPairs(sessions);
  const ledger = ledgerOf(seriesDir);
  return {
    extractedFrom: { runsRoot: tidyPath(runs, home), sources: sources(seriesDir), tool: 'research/minigit/tools/extract.mjs' },
    series: header(map),
    sessions,
    pairs,
    summary: pairs.length ? summarise(pairs) : null,
    perStack: pairs.length ? perStack(pairs) : null,
    quality: qualityByStack(sessions),
    pairCrosscheck: crosscheck(pairs, pairRecords(seriesDir)),
    aa: aaOf(seriesDir, home),
    rehearsalV2: readJson(join(seriesDir, 'rehearsal-v2.json')),
    cpu: cpuOf(seriesDir),
    handcheck: handcheckOf(seriesDir, home),
    ledger,
    charges: chargesOf(sessions, ledger),
  };
}

function main() {
  const { runs, series } = args(process.argv.slice(2));
  const home = homedir();
  mkdirSync(OUT, { recursive: true });
  for (const id of series) {
    const data = extractSeries(runs, id, home);
    const file = join(OUT, `${id}.json`);
    writeFileSync(file, `${JSON.stringify(data, null, 1)}\n`);
    const bad = data.pairCrosscheck.filter(c => c.ok === false);
    console.log(`${id}: ${data.sessions.length} sessions, ${data.pairs.length} pairs, pair cross-check ${bad.length ? `DISAGREES (${bad.length})` : 'agrees'} -> ${file}`);
  }
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) main();
