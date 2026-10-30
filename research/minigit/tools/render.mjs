#!/usr/bin/env node
// Rewrites the generated blocks of the results record from research/minigit/data/<series>.json.
//
//   node research/minigit/tools/render.mjs [--doc <path>]
//
// A block is the text between `<!-- generated:<name> -->` and `<!-- /generated:<name> -->`. Every number in a
// block comes from the data files; the prose outside the blocks holds no measured number. An unknown block
// name, or a data file that is missing, fails the run naming it.
import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { SERIES } from './extract.mjs';
import { ratio, sec, usd } from './format.mjs';
import { amendmentLines, defects, prediction } from './findings.mjs';
import * as S from './tables-sessions.mjs';
import * as P from './tables-pairs.mjs';

const HERE = dirname(fileURLToPath(import.meta.url));
const REPO = resolve(HERE, '..', '..', '..');
const DATA = resolve(HERE, '..', 'data');
const DOC = resolve(HERE, '..', 'RESULTS_bare_2026-10-30.md');

function load() {
  return SERIES.map(id => {
    const file = join(DATA, `${id}.json`);
    if (!existsSync(file)) throw new Error(`${file} is missing: run extract.mjs first`);
    return JSON.parse(readFileSync(file, 'utf8'));
  });
}

function headline(all) {
  const d = all.find(x => x.series.seriesId === 's-20261030-bf89');
  const a = d.summary.a;
  const p1 = d.pairs.find(p => p.pair === 1);
  const spend = all.reduce((x, s) => x + s.ledger.ownBareUsd, 0);
  const walls = d.sessions.filter(s => s.kind === 'data' && s.time?.wallMs).map(s => s.time.wallMs);
  return [
    `- Every paid data session of the series (voids included) ran ${sec(Math.min(...walls))}–${sec(Math.max(...walls))} s wall, spawn to exit; recorded sessions that launched a subagent: ${d.sessions.filter(s => s.recorded && s.time.subagentsLaunched > 0).length}.`,
    `- Series ${d.series.seriesId}: ${a.per.costUsd.n} complete pair(s) of 3 (pair(s) ${a.pairs.join(', ')}); missing: pair ${a.missing.join(', ')}. Stopped by \`${d.series.stopped?.rule}\`.`,
    `- Cost C#/TS (headline recompute, cell = v1 + v2): ${d.pairs.filter(p => p.ratios.costUsd.known).map(p => `pair ${p.pair} ${ratio(p.ratios.costUsd.value)}`).join(', ')}; C# > TS in ${a.per.costUsd.csharpHigher} of ${a.per.costUsd.n}.`,
    `- Wall C#/TS: ${d.pairs.filter(p => p.ratios.wallMs.known).map(p => `pair ${p.pair} ${ratio(p.ratios.wallMs.value)}`).join(', ')}; model_api: ${d.pairs.filter(p => p.ratios.modelApiMs.known).map(p => `pair ${p.pair} ${ratio(p.ratios.modelApiMs.value)}`).join(', ')}; active − model_api: ${d.pairs.filter(p => p.ratios.activeMinusModelApiMs.known).map(p => `pair ${p.pair} ${ratio(p.ratios.activeMinusModelApiMs.value)}`).join(', ')}.`,
    `- The only pair that is neither flagged nor holds a \`ran unpaired\` cell is pair ${d.summary.c.pairs.join(', ') || 'none'}: cost ${ratio(p1.ratios.costUsd.value)}, wall ${ratio(p1.ratios.wallMs.value)}, model_api ${ratio(p1.ratios.modelApiMs.value)}, active − model_api ${ratio(p1.ratios.activeMinusModelApiMs.value)}.`,
    `- A cell (v1 + v2) cost ${usd(d.perStack.csharp.costUsd.min, 2)}–${usd(d.perStack.csharp.costUsd.max, 2)} in C# and ${usd(d.perStack.node.costUsd.min, 2)}–${usd(d.perStack.node.costUsd.max, 2)} in TS; a C# cell's wall was ${sec(d.perStack.csharp.wallMs.min)}–${sec(d.perStack.csharp.wallMs.max)} s.`,
    `- Quality: ${Object.values(d.quality).every(q => q.accepted === q.scored) ? 'every' : 'NOT every'} scored data session was accepted (built, passed the visible tests, held-out primary 100 %); the lowest held-out percent was ${Math.min(...Object.values(d.quality).filter(q => q.scored).map(q => q.percentMin))} % (accepted: ${Object.values(d.quality).reduce((x, q) => x + q.accepted, 0)} of ${Object.values(d.quality).reduce((x, q) => x + q.scored, 0)} scored).`,
    `- Spend: ${usd(spend)} bare over all three series (of $40), ${usd(spend + all[0].ledger.priorUsd)} with the prior instrument spend (of $500).`,
  ].join('\n');
}

const bf = all => all.find(x => x.series.seriesId === 's-20261030-bf89');
const byId = (all, id) => all.find(x => x.series.seriesId === id);

const BLOCKS = {
  headline,
  conditions: S.conditions,
  'sessions-cost': S.sessionsCost,
  'sessions-tokens': S.sessionsTokens,
  'sessions-time': S.sessionsTime,
  'sessions-quality': S.sessionsQuality,
  rehearsals: S.rehearsals,
  voids: S.voids,
  cpu: S.cpu,
  'pairs-phase': all => P.pairsPhase(bf(all)),
  'pairs-cell': all => P.pairsCell(bf(all)),
  'pairs-spend-voids': all => P.spendWithVoids(bf(all)),
  medians: all => P.medians(bf(all)),
  'per-stack': all => P.perStackTable(bf(all)),
  overlap: all => P.overlap(bf(all)),
  'quality-summary': all => P.qualitySummary(bf(all)),
  prediction: all => prediction(bf(all)),
  'aa-92a9': all => P.aaTable(byId(all, 's-20261030-92a9')),
  'aa-0c1a': all => P.aaTable(byId(all, 's-20261030-0c1a')),
  'aa-bf89': all => P.aaTable(bf(all)),
  'aa-spread': P.aaSpread,
  handcheck: all => P.handcheck(bf(all)),
  ledger: P.ledger,
  'ledger-totals': P.ledgerTotals,
  defects: () => defects(REPO),
  amendments: () => amendmentLines(REPO),
};

export function render(text, all) {
  const seen = new Set();
  const out = text.replace(/<!-- generated:([a-z0-9-]+) -->[\s\S]*?<!-- \/generated:\1 -->/g, (_, name) => {
    const f = BLOCKS[name];
    if (!f) throw new Error(`unknown generated block "${name}"; legal: ${Object.keys(BLOCKS).join(', ')}`);
    seen.add(name);
    return `<!-- generated:${name} -->\n${f(all)}\n<!-- /generated:${name} -->`;
  });
  const unused = Object.keys(BLOCKS).filter(b => !seen.has(b));
  return { out, unused };
}

function main() {
  const i = process.argv.indexOf('--doc');
  const doc = i > 0 ? resolve(process.argv[i + 1]) : DOC;
  const { out, unused } = render(readFileSync(doc, 'utf8'), load());
  writeFileSync(doc, out);
  console.log(`rendered ${doc}${unused.length ? `; blocks not in the document: ${unused.join(', ')}` : ''}`);
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) main();
