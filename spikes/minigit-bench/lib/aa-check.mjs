// The small vocabulary of `aa-verdicts` (E3b.S4): a per-session CHECK is `{ rid, status: 'holds' | 'refuted' | 'unknown' | 'explained', note }`, and a ROW of the G table is the combination of the checks of
// the sessions it reads. A leaf of a record is `{ known: true, value }` or `{ known: false, why }`; an unknown is never a hold and never a refutation: it is `not exercised`.
export const holds = note => ({ status: 'holds', note });
export const refuted = note => ({ status: 'refuted', note });
export const unknown = note => ({ status: 'unknown', note });

/** The value of a record leaf at `keys`, or `undefined` when it is absent or not known. */
export function leaf(record, ...keys) {
  let node = record;
  for (const k of keys) node = node?.[k];
  return node?.known ? node.value : undefined;
}

/** A check from a flag that may be missing: `undefined` / `null` is unknown (`missing`), else holds (`yes`) or refuted (`no`). */
export function tri(flag, { yes, no, missing }) {
  if (flag === undefined || flag === null) return unknown(missing);
  return flag ? holds(yes) : refuted(no);
}

/** A check from a crosscheck status: `pass` holds, `breach` is refuted, anything else (`unchecked`, absent) is unknown. */
export const fromStatus = (status, label) => ({ status: { pass: 'holds', breach: 'refuted' }[status] ?? 'unknown', note: `${label} ${status ?? 'missing'}` });

/** A check from a list of problems: none holds (`yes`), else refuted naming them. */
export const fromProblems = (problems, yes) => (problems.length === 0 ? holds(yes) : refuted(problems.join(', ')));

const RANK = Object.freeze({ holds: 0, explained: 1, unknown: 2, refuted: 3 });
const VERDICT_OF = Object.freeze({ holds: 'holds', explained: 'explained', unknown: 'not exercised', refuted: 'refuted' });

/** `{ verdict, observed }` of a row from the checks of the sessions it reads (the worst wins; no session at all is `not exercised`). */
export function combine(checks) {
  if (checks.length === 0) return { verdict: 'not exercised', observed: 'no session to read it from' };
  const worst = checks.reduce((w, c) => (RANK[c.status] > RANK[w] ? c.status : w), 'holds');
  const shown = worst === 'holds' ? checks : checks.filter(c => c.status !== 'holds'); // a row that is not a plain hold shows what is wrong with it, not the sessions that are fine
  return { verdict: VERDICT_OF[worst], observed: shown.map(c => `${c.rid}: ${c.note}`).join('; ') };
}

/** The checks of `sessions` (the facts of each), tagged with the session's rid. */
export const perSession = (sessions, fn) => sessions.map(f => ({ rid: f.rid, ...fn(f) }));

/** The line with the latest date (a later date wins; the file's order breaks a tie), or `undefined`: never the last one in the file. */
export const latestOf = lines => lines.toSorted((a, b) => a.date.localeCompare(b.date)).at(-1);

/** The latest valid dated by-hand line for a G and a session, or `undefined`. */
export const lineFor = (ctx, g, rid) => latestOf(ctx.amendments.verdicts.filter(v => v.g === g && v.rid === rid));

/** A refuted check that a dated `explained` line for the session (naming `needle` in its evidence, when given) turns into `explained`. */
export function withExplain(check, { ctx, g, needle = '' }) {
  const line = check.status === 'refuted' ? lineFor(ctx, g, check.rid) : undefined;
  const covers = line?.verdict === 'explained' && line.evidence.includes(needle);
  return covers ? { ...check, status: 'explained', note: `${check.note} (explained by the dated line: ${line.evidence})` } : check;
}

/** The ‡ rows are also required of the Node pilot: with no pilot session the row has a check that is unknown, so it is `not exercised`, never a hold. */
export const needPilot = ctx => (ctx.pilot.length > 0 ? [] : [{ rid: 'pilot', ...unknown('no Node pilot session') }]);
