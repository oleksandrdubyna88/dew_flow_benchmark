// Formatting helpers for render.mjs. A value that was not captured renders as "—", never as 0.
export const DASH = '—';
const has = x => x !== null && x !== undefined && !Number.isNaN(x);

export const usd = (x, d = 4) => (has(x) ? `$${x.toFixed(d)}` : DASH);
export const sec = ms => (has(ms) ? (ms / 1000).toFixed(1) : DASH);
export const ratio = x => (has(x) ? x.toFixed(2) : DASH);
export const int = x => (has(x) ? String(x) : DASH);
export const pct = x => (has(x) ? `${x.toFixed(1)} %` : DASH);
export const stackName = s => ({ csharp: 'C#', node: 'TS' }[s] ?? s ?? DASH);
export const short = id => (id ? id.slice(-4) : DASH);
export const h12 = h => (h ? h.slice(0, 12) : DASH);

export function table(head, rows) {
  const esc = c => String(c ?? DASH).replace(/\|/g, '\\|').replace(/\n/g, ' ');
  return [`| ${head.join(' | ')} |`, `|${head.map(() => '---').join('|')}|`, ...rows.map(r => `| ${r.map(esc).join(' | ')} |`)].join('\n');
}

/** T1–T5 as one cell: p = pass, B = breach, u = unchecked, — = not run. */
export function checks(c) {
  const mark = s => ({ pass: 'p', breach: 'B', unchecked: 'u' }[s] ?? DASH);
  return ['t1', 't2', 't3', 't4', 't5'].map(k => mark(c?.[k])).join(' ');
}

export const flagList = s => {
  const all = [...new Set([...(s.flags ?? []), ...(s.mapFlags ?? [])])];
  return all.length ? all.join(', ') : DASH;
};
