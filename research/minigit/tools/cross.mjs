// The cross-series picture: series A (bf89) pairs reached and series B (b82e) pairs, side by side.
// Pooling two series is NOT a registered analysis: every table built here is labelled descriptive.
import { median } from './pairs.mjs';
import { DASH, ratio, stackName, table, usd } from './format.mjs';

const MEAS = [['costUsd', 'cost'], ['wallMs', 'wall'], ['modelApiMs', 'model_api'], ['activeMinusModelApiMs', 'active − model_api']];
const DATA = [['s-20261030-bf89', 'A'], ['s-20261030-b82e', 'B']];

/** Every pair of the two series, tagged with its series letter. Operator-noted pairs come in as `noted`. */
export function crossPairs(all, noted = []) {
  return DATA.flatMap(([id, tag]) => {
    const d = all.find(x => x.series.seriesId === id);
    return d ? d.pairs.map(p => ({ ...p, id: `${tag}${p.pair}`, series: id, noted: noted.includes(`${tag}${p.pair}`) })) : [];
  });
}

const complete = p => !p.cells.csharp.missing && !p.cells.node.missing && MEAS.every(([k]) => p.ratios[k].known);

const WAYS = [
  ['every complete pair', () => true],
  ['without pairs holding a `ran unpaired` cell', p => !p.unpaired],
  ['without flagged pairs', p => !p.flagged],
  ['neither flagged nor unpaired', p => !p.flagged && !p.unpaired],
  ['series B only (the registered complete series)', p => p.series === 's-20261030-b82e'],
  ['series B without pair B3 (the operator-noted noisy start; no flag in the records)', p => p.series === 's-20261030-b82e' && !p.noted],
];

export function crossCells(pairs) {
  return table(
    ['pair', 'C# cell', 'TS cell', 'C# cost', 'TS cost', ...MEAS.map(([, n]) => `C#/TS ${n}`), 'flagged by', 'unpaired', 'note'],
    pairs.map(p => {
      const ok = complete(p);
      const why = p.cells.csharp.missing ? `C# missing: ${p.cells.csharp.why}` : !ok ? 'TS cell incomplete (no record for its v2)' : DASH;
      return [p.id, `${p.cells.csharp.v1 ?? DASH} + ${p.cells.csharp.v2 ?? DASH}`, `${p.cells.node.v1 ?? DASH} + ${p.cells.node.v2 ?? DASH}`,
        p.cells.csharp.missing ? 'missing' : usd(p.cells.csharp.values.costUsd), p.cells.node.missing ? 'missing' : usd(p.cells.node.values.costUsd),
        ...MEAS.map(([k]) => (p.ratios[k].known ? ratio(p.ratios[k].value) : DASH)),
        p.flagged ? `${p.flags.join(', ')} (${p.flaggedBy.join(', ')})` : DASH, p.unpaired ? 'yes' : 'no', p.noted ? `operator-noted noisy start, not in the records${why === DASH ? '' : `; ${why}`}` : why];
    }),
  );
}

export function crossMedians(pairs) {
  return table(
    ['pairs', 'used', ...MEAS.map(([, n]) => `${n}: median (range); C# > TS in k of n`)],
    WAYS.map(([title, keep]) => {
      const kept = pairs.filter(p => complete(p) && keep(p));
      return [title, kept.map(p => p.id).join(', ') || 'none', ...MEAS.map(([k]) => {
        const rs = kept.map(p => p.ratios[k].value);
        return rs.length ? `${ratio(median(rs))} (${ratio(Math.min(...rs))}–${ratio(Math.max(...rs))}); ${rs.filter(r => r > 1).length} of ${rs.length}` : 'n = 0';
      })];
    }),
  );
}

/** Per phase: the phase ratio of every pair where both sessions of that phase are data and recorded. */
export function crossPhases(pairs) {
  const rows = [];
  for (const ph of ['v1', 'v2']) for (const [title, keep] of [WAYS[0], WAYS[3]]) {
    const ps = pairs.filter(keep).map(p => ({ id: p.id, r: p.phases.find(x => x.phase === ph).ratios })).filter(x => MEAS.every(([k]) => x.r[k] !== null));
    rows.push([ph, title, ps.map(x => x.id).join(', ') || 'none', ...MEAS.map(([k]) => {
      const rs = ps.map(x => x.r[k]);
      return rs.length ? `${ratio(median(rs))} (${ratio(Math.min(...rs))}–${ratio(Math.max(...rs))}); ${rs.filter(r => r > 1).length} of ${rs.length}` : 'n = 0';
    })]);
  }
  return table(['phase', 'pairs', 'used', ...MEAS.map(([, n]) => `${n}: median (range); C# > TS in k of n`)], rows);
}

export function crossQuality(all) {
  const rows = [];
  for (const [id, tag] of DATA) {
    const d = all.find(x => x.series.seriesId === id);
    if (!d) continue;
    for (const [k, q] of Object.entries(d.quality)) {
      const [st, ph] = k.split('-');
      rows.push([tag, stackName(st), ph, q.sessions, q.scored, q.unscored.join(', ') || DASH, q.scored ? `${q.percentMedian} (${q.percentMin})` : DASH, q.scored ? `${q.defectsMedian} (${q.defectsMax})` : DASH, `${q.accepted} of ${q.scored}`]);
    }
  }
  return table(['series', 'stack', 'phase', 'data sessions', 'scored', 'not scored', 'held-out percent: median (min)', 'defects: median (max)', 'accepted'], rows);
}
