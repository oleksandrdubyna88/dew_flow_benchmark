// A dated `refuted` by-hand verdict reclassifies a session that already ended (E3b review N4; analysis contract section 2: "a by-hand verdict can reclassify a session to `void: harness`
// and to nothing else. It is applied after row 4 and before row 5"). `collectSession` applies the lines known when a session ENDS; a line dated later (the pair-1 read-back, G-13) reaches the
// sessions that ran before it, and this is where: at the start of every run and resume, before the schedule is asked for the next step, so the schedule plans the one re-run by its registered
// rule. Pure over values: the runner writes the map, the ledger event and the record.
const BY_HAND = 'void: harness';
/** Rows 1 to 4 of the outcome table come BEFORE the hand verdict: a stop and a contaminated void keep their class. */
const COMES_FIRST = cls => String(cls).startsWith('stop:') || cls === 'void: contaminated';

/**
 * `handVerdictsFor(rid)` -> the valid by-hand verdicts about that session. -> `{ map, changes: [{ rid, from: { class, data }, to: { class, data }, g, date, evidence, who, dependents }] }`.
 * Only a session that ENDED (`ran` or `scored`), whose class is not a stop or a contaminated void and not already `void: harness`, and that no earlier pass reclassified. The entry keeps its status
 * (a scored session stays scored) and its charge, and gets `reclassified` (what it was, which line); `dependents` names the data sessions of the same cell's v2 that ran from a v1 tree this
 * line refuted: they are NOT reclassified here (a line names one session), the operator refutes them by their own line.
 */
export function reclassifyByHand({ map, handVerdictsFor }) {
  const changes = [];
  const sessions = { ...map.sessions };
  for (const s of Object.values(map.sessions)) {
    if (!['ran', 'scored'].includes(s.status) || s.reclassified !== undefined || COMES_FIRST(s.class) || s.class === BY_HAND) continue;
    const refuted = handVerdictsFor(s.rid).filter(v => v.verdict === 'refuted');
    if (refuted.length === 0) continue;
    const line = refuted.at(-1);
    const dependents = s.phase === 'v1' && s.data === true
      ? Object.values(map.sessions).filter(x => x.pair === s.pair && x.stack === s.stack && x.kind === s.kind && x.phase === 'v2' && x.data === true && ['ran', 'scored'].includes(x.status)).map(x => x.rid) : [];
    const change = { rid: s.rid, from: { class: s.class, data: s.data }, to: { class: BY_HAND, data: false }, g: refuted.map(v => v.g).join(', '), date: line.date, evidence: line.evidence, who: line.who, dependents };
    sessions[s.rid] = Object.freeze({ ...s, class: BY_HAND, data: false, reclassified: { from: change.from, g: change.g, date: change.date, evidence: change.evidence, who: change.who } });
    changes.push(change);
  }
  return { map: changes.length === 0 ? map : Object.freeze({ ...map, sessions: Object.freeze(sessions) }), changes };
}

const ENDED = ['ran', 'scored'];
const wasData = s => s.data === true || s.reclassified !== undefined; // data when a later session started: still data, or refuted by hand since
const startedBefore = (a, b) => !a.startedAt || !b.startedAt || a.startedAt < b.startedAt; // no start time: counted
const isOwnV2 = v2 => v2.phase === 'v2' && v2.data === true && ENDED.includes(v2.status);

/** The v1 session a data v2 of `mine` ran from: the highest attempt among the v1 sessions of its stack that were data when it started and started before it. */
function sourceOf(mine, v2) {
  const earlier = s => s.phase === 'v1' && s.stack === v2.stack && wasData(s) && startedBefore(s, v2);
  return mine.filter(earlier).sort((a, b) => a.attempt - b.attempt).at(-1);
}

/**
 * The rids of the data v2 sessions of `pair` that ran from a v1 tree a hand verdict refuted LATER, and that have no refuted line of their own (review N4 follow-up): `reclassifyByHand` names them as `dependents` and
 * does not reclassify them (a line names one session), so without this the pair record would show a clean v2 on a refuted tree. The pair record carries the list and the flag `v2 from refuted v1`.
 */
export function v2FromRefutedV1(map, pair) {
  const mine = Object.values(map.sessions).filter(s => s.pair === pair && s.kind === 'data');
  return mine.filter(v2 => isOwnV2(v2) && v2.reclassified === undefined && sourceOf(mine, v2)?.reclassified !== undefined).map(v2 => v2.rid);
}
