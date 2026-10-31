#!/usr/bin/env node
// Rewrites the generated blocks of the results record from research/minigit/data/<series>.json and data/replay.json.
//
//   node research/minigit/tools/render.mjs [--doc <path>] [--amendments-ref <git ref>]
//
// A block is the text between `<!-- generated:<name> -->` and `<!-- /generated:<name> -->`. A per-series block ends in the
// series' last four characters (`pairs-cell-b82e`). Every number in a block comes from the data files; the prose outside
// the blocks holds no measured number. An unknown block name, a missing data file, a defect commit that does not exist
// or a harness identity with no dated line fails the run naming it. --amendments-ref reads amendments.md as committed on
// that ref (the branch holding every dated line), because the record's own branch may be behind it.
import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { SERIES } from './extract.mjs';
import { ratio, sec, usd } from './format.mjs';
import { amendmentLines, amendmentsSource, defects, prediction } from './findings.mjs';
import { crossCells, crossMedians, crossPairs, crossPhases, crossQuality } from './cross.mjs';
import * as S from './tables-sessions.mjs';
import * as P from './tables-pairs.mjs';
import * as R from './tables-replay.mjs';

const HERE = dirname(fileURLToPath(import.meta.url));
const REPO = resolve(HERE, '..', '..', '..');
const DATA = resolve(HERE, '..', 'data');
const DOC = resolve(HERE, '..', 'RESULTS_bare_2026-10-30.md');
const REPLAY = join(DATA, 'replay.json');
/** Pairs the operator noted as started on a noisy machine although the records carry no flag for them. */
const NOTED = ['B3'];

function load() {
  return SERIES.map(id => {
    const file = join(DATA, `${id}.json`);
    if (!existsSync(file)) throw new Error(`${file} is missing: run extract.mjs first`);
    return JSON.parse(readFileSync(file, 'utf8'));
  });
}

function loadReplay() {
  if (!existsSync(REPLAY)) throw new Error(`${REPLAY} is missing: run replay.mjs first`);
  return JSON.parse(readFileSync(REPLAY, 'utf8'));
}

const byId = (all, tail) => all.find(x => x.series.seriesId.endsWith(tail));
const list = (pairs, k) => pairs.filter(p => p.ratios[k].known).map(p => `${p.id ?? `pair ${p.pair}`} ${ratio(p.ratios[k].value)}`).join(', ');

function headline(all) {
  const a = byId(all, 'bf89');
  const b = byId(all, 'b82e');
  const cross = crossPairs(all, NOTED);
  const spend = all.reduce((x, s) => x + s.charges.fromRecordsUsd, 0);
  const sb = b.summary.a.per;
  const clean = cross.filter(p => !p.flagged && !p.unpaired && p.ratios.costUsd.known).map(p => p.id);
  const walls = all.flatMap(d => d.sessions.filter(s => s.kind === 'data' && s.time?.wallMs).map(s => s.time.wallMs));
  const quality = [a, b].flatMap(d => Object.values(d.quality));
  return [
    `- Series B (${b.series.seriesId}) ran its three pairs (complete: ${b.summary.a.pairs.join(', ')}; stop: ${b.series.stopped ? b.series.stopped.rule : 'none'}). Series A (${a.series.seriesId}) reached pairs ${a.summary.a.pairs.join(', ')} and stopped by \`${a.series.stopped?.rule}\`.`,
    `- Cost C#/TS per complete pair (cell = v1 + v2, headline recompute): ${list(cross, 'costUsd')}. In series B, C# > TS in ${sb.costUsd.csharpHigher} of ${sb.costUsd.n}; median ${ratio(sb.costUsd.median)}.`,
    `- Wall C#/TS: ${list(cross, 'wallMs')}. Series B: C# > TS in ${sb.wallMs.csharpHigher} of ${sb.wallMs.n}; median ${ratio(sb.wallMs.median)}.`,
    `- model_api C#/TS: ${list(cross, 'modelApiMs')}. Series B: C# > TS in ${sb.modelApiMs.csharpHigher} of ${sb.modelApiMs.n}; median ${ratio(sb.modelApiMs.median)}.`,
    `- active − model_api C#/TS: ${list(cross, 'activeMinusModelApiMs')}. Series B: C# > TS in ${sb.activeMinusModelApiMs.csharpHigher} of ${sb.activeMinusModelApiMs.n}; median ${ratio(sb.activeMinusModelApiMs.median)}.`,
    `- Pairs that are neither flagged nor hold a \`ran unpaired\` cell: ${clean.join(', ') || 'none'}.`,
    `- Every paid data session of A and B (voids included) ran ${sec(Math.min(...walls))}–${sec(Math.max(...walls))} s wall, spawn to exit; recorded sessions that launched a subagent: ${all.flatMap(d => d.sessions).filter(s => s.recorded && s.time.subagentsLaunched > 0).length}.`,
    `- Quality: ${quality.every(q => q.accepted === q.scored) ? 'every' : 'NOT every'} scored data session of A and B was accepted (${quality.reduce((x, q) => x + q.accepted, 0)} of ${quality.reduce((x, q) => x + q.scored, 0)}); the lowest held-out percent was ${Math.min(...quality.filter(q => q.scored).map(q => q.percentMin))} %.`,
    `- Spend from the records: ${usd(spend)} bare over the four series (of $40), ${usd(spend + all[0].ledger.priorUsd)} with the prior instrument spend (of $500). Series B's own ledger missed ${b.charges.missingFromLedger.length} of its charges (${usd(b.charges.missingUsd)}; § 10).`,
  ].join('\n');
}

/** Blocks that take one series, named `<base>-<series tail>`. */
const PER_SERIES = {
  'pairs-phase': P.pairsPhase,
  'pairs-cell': P.pairsCell,
  'pairs-spend-voids': P.spendWithVoids,
  medians: P.medians,
  'per-stack': P.perStackTable,
  overlap: P.overlap,
  'quality-summary': P.qualitySummary,
  'phase-starts': P.phaseStarts,
  prediction,
  aa: P.aaTable,
  handcheck: P.handcheck,
};

function blocks(ref, replay) {
  return {
    headline,
    conditions: S.conditions,
    'sessions-cost': S.sessionsCost,
    'sessions-tokens': S.sessionsTokens,
    'sessions-time': S.sessionsTime,
    'sessions-quality': S.sessionsQuality,
    rehearsals: S.rehearsals,
    voids: S.voids,
    cpu: S.cpu,
    'replay-summary': () => R.replaySummary(replay),
    'replay-voids': () => R.replayVoids(replay),
    'aa-spread': P.aaSpread,
    ledger: P.ledger,
    'ledger-totals': P.ledgerTotals,
    'cross-cells': all => crossCells(crossPairs(all, NOTED)),
    'cross-medians': all => crossMedians(crossPairs(all, NOTED)),
    'cross-phases': all => crossPhases(crossPairs(all, NOTED)),
    'cross-quality': crossQuality,
    defects: all => defects(REPO, ref, all, replay),
    amendments: () => `Read from ${amendmentsSource(REPO, ref)}.\n\n${amendmentLines(REPO, ref)}`,
  };
}

function resolveBlock(name, fixed) {
  if (fixed[name]) return fixed[name];
  const m = name.match(/^(.*)-([0-9a-f]{4})$/);
  if (m && PER_SERIES[m[1]]) return all => { const d = byId(all, m[2]); if (!d) throw new Error(`no series ends in ${m[2]}`); return PER_SERIES[m[1]](d); };
  throw new Error(`unknown generated block "${name}"; legal: ${Object.keys(fixed).join(', ')}, or <${Object.keys(PER_SERIES).join('|')}>-<series tail>`);
}

export function render(text, all, ref, replay) {
  const fixed = blocks(ref, replay);
  return text.replace(/<!-- generated:([a-z0-9-]+) -->[\s\S]*?<!-- \/generated:\1 -->/g, (_, name) => `<!-- generated:${name} -->\n${resolveBlock(name, fixed)(all)}\n<!-- /generated:${name} -->`);
}

function main() {
  const arg = k => { const i = process.argv.indexOf(k); return i > 0 ? process.argv[i + 1] : null; };
  const doc = arg('--doc') ? resolve(arg('--doc')) : DOC;
  writeFileSync(doc, render(readFileSync(doc, 'utf8'), load(), arg('--amendments-ref'), loadReplay()));
  console.log(`rendered ${doc}`);
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) main();
