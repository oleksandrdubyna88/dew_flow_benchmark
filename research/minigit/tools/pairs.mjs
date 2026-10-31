// Pure functions: cells, pair ratios, medians and "in k of n pairs" as analysis-contract.md
// (spikes/minigit-bench/inputs/, sections 1, 6 and 7) registers them. Input: the session objects of session.mjs.

export const MEASURES = [
  ['costUsd', s => s.cost?.headlineUsd],
  ['wallMs', s => s.time?.wallMs],
  ['modelApiMs', s => s.time?.modelApiMs],
  ['activeMinusModelApiMs', s => s.time?.activeMinusModelApiMs],
];

/** Section 7: a pair is flagged when any of its sessions, in either phase, carries one of these. */
export const PAIR_FLAGS = ['foreign load', 'harness core contended', 'started not quiet', 'start skew', 'broker refused', 'upstream 429', 'model_refusal_fallback'];

const STACKS = ['csharp', 'node'];
const round6 = x => (x === null || x === undefined ? null : Math.round(x * 1e6) / 1e6);
const flagsOf = s => [...new Set([...(s.flags ?? []), ...(s.mapFlags ?? [])])];
const isPairFlag = f => PAIR_FLAGS.some(p => f === p || f.startsWith(`${p} `));

export function median(xs) {
  const v = xs.filter(x => x !== null && x !== undefined).sort((a, b) => a - b);
  if (!v.length) return null;
  const m = Math.floor(v.length / 2);
  return v.length % 2 ? v[m] : (v[m - 1] + v[m]) / 2;
}

const dataSession = (sessions, stack, phase) =>
  sessions.filter(s => s.stack === stack && s.phase === phase && s.data).sort((a, b) => b.attempt - a.attempt)[0] ?? null;

function cellOf(pairSessions, stack) {
  const v1 = dataSession(pairSessions, stack, 'v1');
  const v2 = dataSession(pairSessions, stack, 'v2');
  const voids = pairSessions.filter(s => s.stack === stack && !s.data);
  const base = { stack, v1: v1?.rid ?? null, v2: v2?.rid ?? null, voids: voids.map(s => s.rid) };
  // The charged (stop-rule) figure of every session of this stack in the pair, voids included: a spend view, not a registered measure.
  const spendAll = pairSessions.filter(s => s.stack === stack).map(s => s.runMapUsd ?? 0);
  const withVoids = { spendInclVoidsUsd: round6(spendAll.reduce((a, b) => a + b, 0)) };
  if (!v1 || !v2) {
    const why = !v1 ? 'no v1 data session' : `no v2 data session (${voids.filter(s => s.phase === 'v2').map(s => `${s.rid} ${s.class}`).join(', ') || 'never ran'}; the series stopped before its re-run)`;
    return { ...base, ...withVoids, missing: true, why };
  }
  const censored = [v1, v2].some(s => (s.class ?? '').startsWith('killed'));
  const unpaired = [v1, v2].some(s => s.label === 'ran unpaired');
  const values = Object.fromEntries(MEASURES.map(([k, get]) => {
    const a = get(v1);
    const b = get(v2);
    return [k, a === null || a === undefined || b === null || b === undefined ? null : round6(a + b)];
  }));
  const incomplete = MEASURES.filter(([k]) => values[k] === null).map(([k]) => k);
  return { ...base, ...withVoids, missing: false, censored, unpaired, values, incomplete };
}

function ratioOf(cs, ts, key) {
  if (cs.missing || ts.missing) return { known: false, why: 'a cell is missing' };
  const a = cs.values[key];
  const b = ts.values[key];
  if (a === null || b === null) return { known: false, why: 'a cell value is not captured' };
  if (cs.censored && ts.censored) return { known: false, why: 'both censored' };
  const bound = cs.censored ? '>=' : ts.censored ? '<=' : null;
  return { known: true, value: round6(a / b), bound };
}

function phaseRows(pairSessions) {
  return ['v1', 'v2'].map(phase => {
    const [c, t] = STACKS.map(st => dataSession(pairSessions, st, phase));
    const ratios = Object.fromEntries(MEASURES.map(([k, get]) => {
      const a = c ? get(c) : null;
      const b = t ? get(t) : null;
      return [k, a === null || a === undefined || b === null || b === undefined ? null : round6(a / b)];
    }));
    return { phase, csharp: c?.rid ?? null, node: t?.rid ?? null, ratios };
  });
}

export function buildPairs(sessions) {
  const data = sessions.filter(s => s.kind === 'data');
  const ks = [...new Set(data.map(s => s.pair))].sort((a, b) => a - b);
  return ks.map(k => {
    const ps = data.filter(s => s.pair === k);
    const [cs, ts] = STACKS.map(st => cellOf(ps, st));
    const flags = [...new Set(ps.flatMap(flagsOf).filter(isPairFlag))];
    const flaggedBy = ps.filter(s => flagsOf(s).some(isPairFlag)).map(s => s.rid);
    const ratios = Object.fromEntries(MEASURES.map(([key]) => [key, ratioOf(cs, ts, key)]));
    const spendRatioInclVoids = cs.spendInclVoidsUsd && ts.spendInclVoidsUsd ? round6(cs.spendInclVoidsUsd / ts.spendInclVoidsUsd) : null;
    return { pair: k, cells: { csharp: cs, node: ts }, unpaired: Boolean(cs.unpaired || ts.unpaired), flagged: flags.length > 0, flags, flaggedBy, ratios, spendRatioInclVoids, phases: phaseRows(ps) };
  });
}

const WAYS = {
  a: { title: 'every data cell', keep: () => true },
  b: { title: 'without cells holding a `ran unpaired` session', keep: p => !p.unpaired },
  c: { title: 'without flagged pairs', keep: p => !p.flagged },
};

/** Section 7: the ratios over the pairs, their median and "C# > TS in k of n", three ways. */
export function summarise(pairs) {
  return Object.fromEntries(Object.entries(WAYS).map(([way, { title, keep }]) => {
    const kept = pairs.filter(keep);
    const per = Object.fromEntries(MEASURES.map(([key]) => {
      const rs = kept.map(p => p.ratios[key]).filter(r => r.known && r.bound === null).map(r => r.value);
      return [key, { n: rs.length, median: round6(median(rs)), csharpHigher: rs.filter(r => r > 1).length, min: rs.length ? Math.min(...rs) : null, max: rs.length ? Math.max(...rs) : null }];
    }));
    const excluded = pairs.filter(p => !keep(p)).map(p => p.pair);
    const missing = pairs.filter(p => p.cells.csharp.missing || p.cells.node.missing).map(p => p.pair);
    return [way, { title, pairs: kept.map(p => p.pair).filter(k => !missing.includes(k)), excluded, missing, per }];
  }));
}

/** Per stack: median and range of every primary measure over the cells present with every measure known (section 7). */
export function perStack(pairs) {
  return Object.fromEntries(STACKS.map(st => {
    const cells = pairs.map(p => p.cells[st]).filter(c => !c.missing && c.incomplete.length === 0);
    const per = Object.fromEntries(MEASURES.map(([key]) => {
      const vs = cells.map(c => c.values[key]).filter(v => v !== null);
      return [key, { n: vs.length, median: round6(median(vs)), min: vs.length ? Math.min(...vs) : null, max: vs.length ? Math.max(...vs) : null }];
    }));
    return [st, per];
  }));
}

/** Quality per stack and phase over the data sessions that were scored (never summed over phases). */
export function qualityByStack(sessions) {
  const out = {};
  for (const st of STACKS) for (const ph of ['v1', 'v2']) {
    const ss = sessions.filter(s => s.kind === 'data' && s.data && s.stack === st && s.phase === ph);
    const scored = ss.filter(s => s.quality?.scored);
    const pct = scored.map(s => s.quality.heldout?.percent);
    const def = scored.map(s => s.quality.heldout?.defects);
    const accepted = scored.filter(s => s.quality.buildOk && s.quality.visible.every(v => v.failed === 0 && v.passed === v.expectedTotal) && s.quality.heldout?.primary100);
    out[`${st}-${ph}`] = {
      sessions: ss.length, scored: scored.length, unscored: ss.filter(s => !s.quality?.scored).map(s => s.rid),
      percentMedian: median(pct), percentMin: pct.length ? Math.min(...pct) : null,
      defectsMedian: median(def), defectsMax: def.length ? Math.max(...def) : null,
      accepted: accepted.length,
    };
  }
  return out;
}
