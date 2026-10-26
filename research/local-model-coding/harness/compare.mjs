#!/usr/bin/env node
// The final comparison of every arm: time, Claude cost and tokens (metrics.json), the objective gate, the absolute blind
// judge (judge-abs/scores.json, mean of its runs) and, for the Q arms, the fill and the survival of the local model's code.
// The classic arms C, C2, C3 and C4 are one configuration run four times: their min–mean–max is the band every other arm is
// read against.   node compare.mjs [--md]
import { existsSync, readFileSync } from 'node:fs';

const E = '/home/jinx/git/coai-exp';
const read = (f) => (existsSync(f) ? JSON.parse(readFileSync(f, 'utf8')) : null);
const { report } = read(`${E}/metrics.json`);
const scores = read(`${E}/judge-abs/scores.json`) ?? {};
const ARMS = ['C', 'C2', 'C3', 'C4', 'V', 'VT', 'Q1', 'Q2', 'Q3', 'Q4', 'H1', 'H2', 'H3', 'H4'].filter((a) => report[a]);
const CLASSIC = ['C', 'C2', 'C3', 'C4'];
const mean = (xs) => (xs.length ? xs.reduce((a, b) => a + b, 0) / xs.length : null);

const rows = ARMS.map((a) => {
  const r = report[a];
  const out = Object.values(r.models).reduce((n, u) => n + u.output, 0);
  const sonnetOut = r.models['claude-sonnet-5-5']?.output ?? 0;
  const o = read(`${E}/results/${a}/objective.json`);
  const s = scores[a] ?? [];
  const sv = read(`${E}/results/${a}/survival.json`);
  return {
    arm: a, minutes: Math.round(r.wallSeconds / 60), cost: r.cost, costEstimated: r.costEstimated,
    outK: out / 1e3, sonnetOutK: sonnetOut / 1e3, cacheReadM: Object.values(r.models).reduce((n, u) => n + u.cacheRead, 0) / 1e6,
    vllm: r.vllm ? `${r.vllm.requests} req, ${((r.vllm.promptTokens + r.vllm.completionTokens) / 1e3).toFixed(0)}k tok, ${Math.round(r.vllm.busySeconds / 60)} min GPU` : '—',
    objective: o ? (o.build && o.format !== false && o.generatorCheck && !o.mcp.failedNew.length && !o.ts.failedNew.length
      && !(o.markersLeft?.length) && !(o.placeholdersLeft?.length) ? 'pass' : 'FAIL') : 'n/a',
    scenarios: o?.scenariosPresentCount ?? null,
    judge: s.length ? mean(s.map((v) => v.overall)) : null, judgeRuns: s.map((v) => v.overall).join('/'),
    defects: s.length ? mean(s.map((v) => (v.defects ?? []).filter((d) => d.severity !== 'minor').length)) : null,
    survival: sv ? { prod: sv.byKind.prod.final, test: sv.byKind.test.final, compileErrors: [sv.fill.test?.compileErrors, sv.fill.prod?.compileErrors].join('/'),
      rejected: (sv.fill.test?.rejected.length ?? 0) + (sv.fill.prod?.rejected.length ?? 0), polishLines: sv.diffLines.polish, fillLines: sv.diffLines.fill } : null,
    steps: Object.fromEntries(Object.entries(r.steps).map(([k, at]) => [k, { min: Math.round(at.reduce((n, x) => n + (x.wallSeconds ?? 0), 0) / 60),
      cost: at.reduce((n, x) => n + (x.cost ?? x.costEst ?? 0), 0) }])),
  };
});
const band = (k) => { const xs = rows.filter((r) => CLASSIC.includes(r.arm) && r[k] != null).map((r) => r[k]);
  return xs.length ? { min: Math.min(...xs), mean: mean(xs), max: Math.max(...xs) } : null; };
const BAND = Object.fromEntries(['minutes', 'cost', 'outK', 'sonnetOutK', 'cacheReadM', 'judge', 'defects'].map((k) => [k, band(k)]));
const where = (k, v) => { const b = BAND[k]; if (!b || v == null) return ''; return v < b.min ? ' ↓' : v > b.max ? ' ↑' : ' ·'; };
const f = (v, d = 1) => (v == null ? 'n/a' : Number(v).toFixed(d));

if (process.argv.includes('--json')) { console.log(JSON.stringify({ band: BAND, rows }, null, 2)); process.exit(0); }
console.log('Classic band (C, C2, C3, C4: min / mean / max). Marks: · inside the band, ↓ below it, ↑ above it.');
for (const [k, b] of Object.entries(BAND)) if (b) console.log(`  ${k.padEnd(11)} ${f(b.min, 2)} / ${f(b.mean, 2)} / ${f(b.max, 2)}`);
console.log('\n| Arm | Work min | Claude $ | Sonnet out (k) | All out (k) | Cache read (M) | vLLM | Objective | Scen. | Judge (runs) | Major+ defects | Q: survival prod/test, compile errors A/B, rejected, polish/fill lines |');
console.log('|---|---|---|---|---|---|---|---|---|---|---|---|');
for (const r of rows) {
  const q = r.survival ? `${Math.round((r.survival.prod ?? 0) * 100)} % / ${Math.round((r.survival.test ?? 0) * 100)} %, ${r.survival.compileErrors}, ${r.survival.rejected}, ${r.survival.polishLines}/${r.survival.fillLines}` : '';
  console.log(`| ${r.arm} | ${r.minutes}${where('minutes', r.minutes)} | ${f(r.cost, 2)}${r.costEstimated ? '*' : ''}${where('cost', r.cost)} | ${f(r.sonnetOutK)}${where('sonnetOutK', r.sonnetOutK)} | ${f(r.outK)}${where('outK', r.outK)} | ${f(r.cacheReadM)}${where('cacheReadM', r.cacheReadM)} | ${r.vllm} | ${r.objective} | ${r.scenarios ?? 'n/a'}/12 | ${f(r.judge)}${where('judge', r.judge)} (${r.judgeRuns}) | ${f(r.defects)}${where('defects', r.defects)} | ${q} |`);
}
console.log('\n* part of the cost estimated (an attempt interrupted by a crash; tokens from its transcript, priced with the fitted unit prices).');
console.log('\nPer step (min, $):');
for (const r of rows) console.log(`  ${r.arm.padEnd(3)} ` + Object.entries(r.steps).map(([k, s]) => `${k} ${s.min} min $${s.cost.toFixed(2)}`).join(' | '));
