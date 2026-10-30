// Pair, A/A, ledger and prediction tables of the results record, built only from research/minigit/data/<series>.json.
import { DASH, h12, int, ratio, sec, short, stackName, table, usd } from './format.mjs';

const MEAS = [['costUsd', 'cost (headline)', x => usd(x)], ['wallMs', 'wall s', sec], ['modelApiMs', 'model_api s', sec], ['activeMinusModelApiMs', 'active − model_api s', sec]];
const ratioCell = r => (r.known ? `${r.bound ? `${r.bound} ` : ''}${ratio(r.value)}` : `— (${r.why})`);

export function pairsPhase(d) {
  const byRid = Object.fromEntries(d.sessions.map(s => [s.rid, s]));
  const get = (rid, k) => (rid ? (k === 'costUsd' ? byRid[rid].cost.headlineUsd : byRid[rid].time[k]) : null);
  return table(
    ['pair', 'phase', 'C# rid', 'TS rid', ...MEAS.flatMap(([, n]) => [`C# ${n}`, `TS ${n}`, 'C#/TS'])],
    d.pairs.flatMap(p => p.phases.map(ph => [p.pair, ph.phase, ph.csharp ?? 'missing', ph.node ?? 'missing',
      ...MEAS.flatMap(([k, , f]) => [f(get(ph.csharp, k)), f(get(ph.node, k)), ratio(ph.ratios[k])])])),
  );
}

export function pairsCell(d) {
  const cellVal = (c, k, f) => (c.missing ? 'missing' : f(c.values[k]));
  return table(
    ['pair', 'C# cell (v1 + v2)', 'TS cell (v1 + v2)', ...MEAS.flatMap(([, n]) => [`C# ${n}`, `TS ${n}`, `C#/TS ${n.split(' ')[0]}`]), 'labels'],
    d.pairs.map(p => {
      const c = p.cells.csharp;
      const t = p.cells.node;
      const labels = [p.unpaired ? '`ran unpaired` cell' : null, p.flagged ? `flagged: ${p.flags.join(', ')} (${p.flaggedBy.join(', ')})` : null,
        c.missing ? `C# missing: ${c.why}` : null, t.missing ? `TS missing: ${t.why}` : null, !t.missing && t.incomplete.length ? `TS incomplete: ${t.incomplete.join(', ')} not captured` : null].filter(Boolean).join('; ') || DASH;
      return [p.pair, `${c.v1 ?? DASH} + ${c.v2 ?? DASH}`, `${t.v1 ?? DASH} + ${t.v2 ?? DASH}`, ...MEAS.flatMap(([k, , f]) => [cellVal(c, k, f), cellVal(t, k, f), ratioCell(p.ratios[k])]), labels];
    }),
  );
}

export function spendWithVoids(d) {
  return table(
    ['pair', 'C# charged, voids included', 'C# voids', 'TS charged, voids included', 'TS voids', 'C#/TS'],
    d.pairs.map(p => [p.pair, usd(p.cells.csharp.spendInclVoidsUsd), p.cells.csharp.voids.join(', ') || DASH, usd(p.cells.node.spendInclVoidsUsd), p.cells.node.voids.join(', ') || DASH, ratio(p.spendRatioInclVoids)]),
  );
}

export function medians(d) {
  return table(
    ['way', 'pairs used', 'excluded', 'missing', ...MEAS.map(([, n]) => `${n.split(' ')[0]}: median C#/TS (range); C# > TS in k of n`)],
    Object.entries(d.summary).map(([way, w]) => [`(${way}) ${w.title}`, w.pairs.join(', ') || 'none', w.excluded.join(', ') || DASH, w.missing.join(', ') || DASH,
      ...MEAS.map(([k]) => { const m = w.per[k]; return m.n ? `${ratio(m.median)} (${ratio(m.min)}–${ratio(m.max)}); ${m.csharpHigher} of ${m.n}` : 'n = 0'; })]),
  );
}

export function perStackTable(d) {
  return table(
    ['stack', ...MEAS.map(([, n]) => `${n}: median (range), n`)],
    Object.entries(d.perStack).map(([st, per]) => [stackName(st), ...MEAS.map(([k, , f]) => { const m = per[k]; return m.n ? `${f(m.median)} (${f(m.min)}–${f(m.max)}), ${m.n}` : 'n = 0'; })]),
  );
}

/** Section 7: overlap of the per-stack ranges = not distinguished. */
export function overlap(d) {
  return table(
    ['measure', 'C# range (n)', 'TS range (n)', 'ranges overlap', 'reading'],
    MEAS.map(([k, n, f]) => {
      const c = d.perStack.csharp[k];
      const t = d.perStack.node[k];
      const ov = c.n && t.n ? c.min <= t.max && t.min <= c.max : null;
      const reading = ov === null ? 'not decidable' : ov ? 'not distinguished' : `C# ${c.max < t.min ? 'below' : 'above'} TS in every cell reached (n = ${Math.min(c.n, t.n)} per stack: not a finding)`;
      return [n, `${f(c.min)}–${f(c.max)} (${c.n})`, `${f(t.min)}–${f(t.max)} (${t.n})`, ov === null ? '—' : ov ? 'yes' : 'no', reading];
    }),
  );
}

export function qualitySummary(d) {
  return table(
    ['stack', 'phase', 'data sessions', 'scored', 'not scored', 'held-out percent: median (min)', 'defects: median (max)', 'accepted'],
    Object.entries(d.quality).map(([k, q]) => { const [st, ph] = k.split('-'); return [stackName(st), ph, q.sessions, q.scored, q.unscored.join(', ') || DASH, q.scored ? `${q.percentMedian} (${q.percentMin})` : DASH, q.scored ? `${q.defectsMedian} (${q.defectsMax})` : DASH, `${q.accepted} of ${q.scored}`]; }),
  );
}

export function aaTable(d) {
  if (!d.aa.present) return `No G table: ${d.aa.why}.`;
  const head = `Verdict **${d.aa.verdict}**${d.aa.stops.length ? `, stops: ${d.aa.stops.join(', ')}` : ''}; computed ${d.aa.computedAt} at harness \`${h12(d.aa.harnessIdentity)}\`; T3 rule: \`${d.aa.t3Rule}\`.`;
  return `${head}\n\n${table(['G', 'must show', 'verdict', 'observed', 'replacing evidence'], d.aa.rows.map(r => [r.id, r.title, r.verdict, r.observed, r.replacing ?? DASH]))}`;
}

export function aaSpread(all) {
  const rows = all.filter(d => d.aa.present).map(d => {
    const g9 = d.aa.rows.find(r => r.id === 'G-9')?.recorded;
    return g9 ? [d.series.seriesId, ratio(g9.costRatio), ratio(g9.wallRatio), ratio(g9.modelApiRatio), ratio(g9.activeMinusModelApiRatio), int(g9.heldoutPercentDiff), int(g9.heldoutDefectsDiff), Object.entries(g9.cpuPerMask).map(([m, v]) => `${m}: ${v.toFixed(3)}`).join(', ')] : null;
  }).filter(Boolean);
  return table(['series', 'cost twin ratio', 'wall', 'model_api', 'active − model_api', 'held-out percent diff', 'defects diff', 'CPU per mask'], rows);
}

export function handcheck(d) {
  return d.handcheck.map(h => `\`${h.file}\` (sha256 \`${h.sha256.slice(0, 12)}\`):\n\n\`\`\`text\n${h.text.trim()}\n\`\`\``).join('\n\n');
}

/** Every paid line once: the registered prior spend once, then each series' own lines. */
export function ledger(all) {
  const prior = all[0].ledger.lines.filter(l => l.kind === 'prior');
  const own = all.flatMap(d => d.ledger.lines.filter(l => l.kind !== 'prior' && !l.fromSeries).map(l => ({ ...l, series: d.series.seriesId })));
  const rows = [...prior.map(l => [l.date, 'before E3b', 'prior', DASH, `${l.what}${l.approx && !l.what.includes('approximate') ? ' (approximate)' : ''}`, usd(l.usd)]),
    ...own.map(l => [l.date, l.series, l.kind, l.rid, l.what, usd(l.usd)])];
  return table(['when (UTC for series lines)', 'series', 'kind', 'rid', 'what', 'charged'], rows);
}

export function ledgerTotals(all) {
  const sum = xs => xs.reduce((a, l) => a + l.usd, 0);
  const prior = all[0].ledger.lines.filter(l => l.kind === 'prior');
  const per = all.map(d => { const own = d.ledger.lines.filter(l => l.kind !== 'prior' && !l.fromSeries); return [d.series.seriesId, own.filter(l => l.usd > 0).length, usd(sum(own))]; });
  const bare = sum(all.flatMap(d => d.ledger.lines.filter(l => l.kind !== 'prior' && !l.fromSeries)));
  const last = all[all.length - 1];
  const budget = last.series.bareBudgetUsd;
  const ceiling = last.series.ceilingUsd;
  const agree = Math.abs(bare - last.ledger.bareUsdIncludingCarried) < 1e-6 ? 'equal' : 'DIFFERENT';
  return [
    table(['series', 'paid sessions', 'bare spend (own lines)'], per),
    '',
    table(['total', 'value', 'limit', 'share of limit'], [
      ['prior instrument spend (registered, before E3b)', `${usd(sum(prior))} over ${prior.length} sessions`, DASH, DASH],
      ['bare spend, every series', usd(bare), usd(budget, 0), `${((100 * bare) / budget).toFixed(1)} %`],
      ['all spend (prior + bare)', usd(bare + sum(prior)), usd(ceiling, 0), `${((100 * (bare + sum(prior))) / ceiling).toFixed(2)} %`],
    ]),
    '',
    `Check: the sum of the series' own lines is ${agree} to the carried total in the last series' ledger (${usd(last.ledger.bareUsdIncludingCarried)}).`,
  ].join('\n');
}

export { short };
