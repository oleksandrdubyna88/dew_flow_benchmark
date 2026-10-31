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

/** Every data session's quiet check before its phase started, as the run-map recorded it (§ 8 of the contract). */
export function phaseStarts(d) {
  return table(
    ['pair', 'phase', 'rid', 'stack', 'attempt', 'quiet before the start', 'waited s', 'quiet-check flags', 'session flags'],
    d.sessions.filter(s => s.kind === 'data').map(s => {
      const q = s.host?.quietBefore;
      return [s.pair, s.phase, s.rid, stackName(s.stack), `a${s.attempt}`, q ? String(q.quiet) : DASH, q?.waitedMs === null || q?.waitedMs === undefined ? DASH : sec(q.waitedMs),
        q?.flags?.length ? q.flags.join(', ') : DASH, [...new Set([...(s.flags ?? []), ...(s.mapFlags ?? [])])].join(', ') || DASH];
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

/**
 * Every paid session once, charged from the records (the run-map's stop-rule figure, equal to the record's and the
 * broker's — `charges.disagree` lists any that is not), with whether the series' own ledger holds its line. The
 * registered prior spend comes once, from the ledger.
 */
export function ledger(all) {
  const prior = all[0].ledger.lines.filter(l => l.kind === 'prior');
  const rows = [...prior.map(l => [l.date, 'before E3b', 'prior', DASH, `${l.what}${l.approx && !l.what.includes('approximate') ? ' (approximate)' : ''}`, usd(l.usd), 'yes']),
    ...all.flatMap(d => d.charges.rows.map(r => {
      const s = d.sessions.find(x => x.rid === r.rid);
      return [s.startedAt, d.series.seriesId, r.kind, r.rid, r.class, usd(r.runMapUsd), r.inOwnLedger ? 'yes' : '**no**'];
    }))];
  return table(['when (UTC start for sessions)', 'series', 'kind', 'rid', 'class', 'charged (records)', "line in the series' own ledger"], rows);
}

export function ledgerTotals(all) {
  const sum = xs => xs.reduce((a, x) => a + x, 0);
  const prior = sum(all[0].ledger.lines.filter(l => l.kind === 'prior').map(l => l.usd));
  const per = all.map(d => [d.series.seriesId, d.charges.rows.length, usd(d.charges.fromRecordsUsd), usd(d.ledger.ownBareUsd),
    d.charges.missingFromLedger.length ? `${d.charges.missingFromLedger.length} sessions, ${usd(d.charges.missingUsd)}` : DASH, d.charges.disagree.length ? d.charges.disagree.join(', ') : 'none']);
  const trueBare = sum(all.map(d => d.charges.fromRecordsUsd));
  const ledgerBare = sum(all.map(d => d.ledger.ownBareUsd));
  const last = all[all.length - 1];
  const budget = last.series.bareBudgetUsd;
  const ceiling = last.series.ceilingUsd;
  const share = (x, lim) => `${((100 * x) / lim).toFixed(2)} %`;
  return [
    table(['series', 'paid sessions', 'bare spend from the records', "bare spend in the series' own ledger lines", 'charged but missing from its own ledger', 'records, run-map and broker disagree'], per),
    '',
    table(['total', 'from the records', 'as the ledgers summed it', 'limit', 'share of limit (records)'], [
      ['prior instrument spend (registered, before E3b)', usd(prior), usd(prior), DASH, DASH],
      ['bare spend, every series', usd(trueBare), usd(ledgerBare), usd(budget, 0), share(trueBare, budget)],
      ['all spend (prior + bare)', usd(trueBare + prior), usd(ledgerBare + prior), usd(ceiling, 0), share(trueBare + prior, ceiling)],
    ]),
    '',
    `Check: the last series' ledger, carried lines included, sums its bare lines to ${usd(last.ledger.bareUsdIncludingCarried)}; the money gates of that series read this figure.`,
  ].join('\n');
}

export { short };
