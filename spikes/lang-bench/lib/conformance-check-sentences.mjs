// Check 5, the fixed sentences, and check 6, T and G (E1.S8). Each check returns the list of its findings as text; an empty list is `ok`.
import { sectionOf } from './conformance-sentences.mjs';

const WORDS = new Map(Object.entries({ one: 1, two: 2, three: 3, four: 4, five: 5, six: 6, seven: 7, eight: 8, nine: 9, ten: 10, eleven: 11, twelve: 12, thirteen: 13, fourteen: 14, fifteen: 15, sixteen: 16, seventeen: 17, eighteen: 18, nineteen: 19, twenty: 20, thirty: 30, forty: 40, fifty: 50, sixty: 60, seventy: 70, eighty: 80, ninety: 90 }));
const XOR = Object.freeze(['F-INVALID', 'F-ERRORS']);
export const REFUSED = Object.freeze({ 'F-BYTES-O-N': 'F-BYTES-O-N is refused for this series: no task uses a normalised output and the runner does not build it (CP1 F12)' });
const MAX_SECONDS = 600;

/** The number a `{k}` slot says, in digits or words up to ninety-nine; null when it is neither. */
export function numberOf(text) {
  if (/^\d+$/.test(text)) return Number(text);
  const [tens, units, more] = text.split('-');
  if (more !== undefined || !WORDS.has(tens)) return null;
  return units === undefined ? WORDS.get(tens) : WORDS.get(tens) >= 20 && WORDS.has(units) && WORDS.get(units) < 10 ? WORDS.get(tens) + WORDS.get(units) : null;
}

const where = hit => `line ${hit.line}`;

/** The problems of the R-ids a `{ref}` or `{eref}` slot names: each must be a requirement of the spec, and a range must ascend. */
function refProblems(id, slot, text, nMax, hit) {
  const problems = [];
  for (const found of text.matchAll(/R([1-9]\d*)(?:–R([1-9]\d*))?/g)) {
    const [low, high] = [Number(found[1]), Number(found[2] ?? found[1])];
    if (low > nMax || high > nMax) problems.push(`${id} (${where(hit)}): {${slot}} names ${found[0]}, and the spec has requirements R1 to R${nMax} only`);
    if (found[2] !== undefined && high <= low) problems.push(`${id} (${where(hit)}): {${slot}} has the range ${found[0]}, which does not ascend`);
  }
  return problems;
}

/** The overrides T and G imply: `{ what, D, V }` for each value that is not the series default. */
export function expectedOverrides(T, G, defaults) {
  return [
    ...(T !== defaults.T ? [{ what: 'readiness', D: String(defaults.T), V: String(T) }] : []),
    ...(G !== defaults.G ? [{ what: 'the grace period', D: String(defaults.G), V: String(G) }] : []),
  ];
}

const slotOf = (ctx, id, name) => ctx.found.get(id)?.hits[0]?.slots[name];
export const timesOf = ctx => ({ T: Number(slotOf(ctx, 'F-READY', 'T')), G: Number(slotOf(ctx, 'F-STOP', 'G')) });

/** The problems of F-TIMING: one sentence per overridden value, naming the series default and the task's value, and no other. */
function timingProblems(ctx) {
  const f = ctx.found.get('F-TIMING');
  const { T, G } = timesOf(ctx);
  if (f === undefined || Number.isNaN(T) || Number.isNaN(G)) return [];
  const wanted = expectedOverrides(T, G, ctx.facts.defaults);
  const key = slots => `${slots.what}|${slots.D}|${slots.V}`;
  const problems = f.hits.filter(hit => !wanted.some(want => key(want) === key(hit.slots))).map(hit => `F-TIMING (${where(hit)}): says the default for ${hit.slots.what} is ${hit.slots.D} s and this task uses ${hit.slots.V} s, which is not an override of T = ${T} s and G = ${G} s`);
  const lacking = wanted.filter(want => !f.hits.some(hit => key(hit.slots) === key(want)));
  return [...problems, ...lacking.map(want => `F-TIMING is missing for ${want.what}: the task uses ${want.V} s and the series default is ${want.D} s`)];
}

/** The problems of the slot values of one hit of sentence `id`. */
function slotProblems(ctx, id, hit) {
  const { slots } = hit;
  const problems = [];
  if (slots.ref !== undefined) problems.push(...refProblems(id, 'ref', slots.ref, ctx.maxId, hit));
  if (slots.eref !== undefined) problems.push(...refProblems(id, 'eref', slots.eref, ctx.maxId, hit));
  if (slots.k !== undefined && numberOf(slots.k) !== ctx.shownIds.length) problems.push(`${id} (${where(hit)}): says ${slots.k} samples, and section 6 shows ${ctx.shownIds.length}`);
  const eof = (ctx.found.get('F-EOF')?.hits.length ?? 0) > 0;
  if (slots.which !== undefined && (slots.which !== 'A test') !== eof) {
    problems.push(`${id} (${where(hit)}): {which} is "${slots.which}", and it must be ${eof ? '"A test that does not end at the end of its input" because F-EOF is present' : '"A test" because F-EOF is absent'}`);
  }
  for (const name of ['T', 'G', 'duration']) if (slots[name] !== undefined && Number(slots[name]) > MAX_SECONDS) problems.push(`${id} (${where(hit)}): {${name}} is ${slots[name]} s, and a duration is at most ${MAX_SECONDS} s`);
  return problems;
}

/** The problems of where the hits of a required or present sentence stand. */
function placementProblems(ctx, f) {
  const { entry, hits } = f;
  const id = entry.id;
  const problems = [];
  if (hits.length > 1 && id !== 'F-TIMING') problems.push(`${id} stands ${hits.length} times (${hits.map(where).join(', ')}); FORMAT says it once`);
  const hit = hits[0];
  const section = sectionOf(entry, ctx.kind);
  if (section !== null && hit.section !== section && entry.where.mode !== 'first-paragraph') problems.push(`${id} (${where(hit)}) stands in section ${hit.section}, and 2.1 puts it in section ${section}`);
  const { sameAs, after } = entry.where;
  if (sameAs !== null && sameAs !== undefined) problems.push(...sameProblems(ctx, f, hit, sameAs, after));
  if (entry.where.mode === 'opens-closes') problems.push(...opensClosesProblems(f));
  return [...problems, ...slotProblems(ctx, id, hit)];
}

function sameProblems(ctx, f, hit, sameAs, after) {
  const mates = sameAs.flatMap(other => (ctx.found.get(other)?.hits ?? []).filter(mate => mate.r === hit.r));
  if (mates.length === 0) return [`${f.entry.id} (${where(hit)}) must stand in the same requirement as ${sameAs.join(' or ')}`];
  return after && !mates.some(mate => mate.index < hit.index) ? [`${f.entry.id} (${where(hit)}) must come after ${sameAs.join(' or ')} in the requirement`] : [];
}

/** F-HIDDEN: the first sentence opens the requirement (right after its id) and the second closes it. */
function opensClosesProblems(f) {
  const [first, second] = f.partHits;
  const hit = first[0];
  const lead = hit.r.text.slice(0, hit.index);
  const problems = [];
  if (!/^\*\*R[1-9]\d*\.\*\* ?$/.test(lead)) problems.push(`${f.entry.id} (${where(hit)}): the first sentence does not open the requirement`);
  const last = second.find(candidate => candidate.r === hit.r);
  if (last === undefined) return [...problems, `${f.entry.id} (${where(hit)}): the second sentence is not in the same requirement`];
  return last.end === hit.r.text.length ? problems : [...problems, `${f.entry.id} (${where(last)}): the second sentence does not close the requirement`];
}

/** The findings about one sentence (missing, altered, misplaced); `present` says whether it counts as carried. */
function sentenceProblems(ctx, f) {
  const { entry, hits, altered } = f;
  if (hits.length === 0) {
    if (altered !== null) return [`${entry.id} is altered at line ${altered}: it does not match FORMAT's sentence word for word (after whitespace is collapsed)`];
    return entry.required && !XOR.includes(entry.id) ? [`${entry.id} is missing: 2.1 requires it in section ${sectionOf(entry, ctx.kind) ?? 'before section 1'}`] : [];
  }
  return placementProblems(ctx, f);
}

/** F-INVALID xor F-ERRORS: exactly one stands. */
function xorProblems(ctx) {
  const present = XOR.filter(id => (ctx.found.get(id)?.hits.length ?? 0) > 0);
  const altered = XOR.some(id => ctx.found.get(id)?.altered !== null && ctx.found.get(id)?.altered !== undefined);
  if (present.length === 1 || (present.length === 0 && altered)) return [];
  return [present.length === 0 ? 'neither F-INVALID nor F-ERRORS stands: a spec carries exactly one' : 'F-INVALID and F-ERRORS both stand: a spec carries exactly one'];
}

/** Check 5: every fixed sentence the kind requires, word for word, in its place, with its slots filled by allowed values. */
export function sentenceFindings(ctx) {
  const findings = [];
  for (const [id, f] of ctx.found) {
    if (REFUSED[id] !== undefined) {
      if (f.hits.length > 0) findings.push(`${REFUSED[id]} (${where(f.hits[0])})`);
      continue;
    }
    findings.push(...sentenceProblems(ctx, f));
  }
  return [...findings, ...xorProblems(ctx), ...timingProblems(ctx)];
}

const DURATION = /^(\d+)(ms|s)$/;
const showMs = ms => (ms % 1000 === 0 ? `${ms / 1000} s` : `${ms} ms`);
const msOf = text => { const found = DURATION.exec(text); return found === null ? null : Number(found[1]) * (found[2] === 's' ? 1000 : 1); };

/** The waits of a scenario script: `{ start: [ms], exit: ms|null }` from its `start … within <d>` lines and its first `wait-exit within <d>`. */
export function scenarioWaits(text) {
  const lines = text.split('\n').map(line => line.trim()).filter(line => line !== '' && !line.startsWith('#'));
  const within = line => msOf((/\bwithin (\S+)$/.exec(line) ?? [])[1] ?? '');
  const starts = lines.filter(line => /^start\s/.test(line)).map(within);
  const exit = lines.find(line => /^wait-exit\s/.test(line));
  return { start: starts, exit: exit === undefined ? null : within(exit) };
}

function waitProblems(id, waits, T, G) {
  const problems = [];
  if (waits.start.length === 0) problems.push(`sample ${id} has no start line`);
  for (const ms of waits.start) if (ms !== T * 1000) problems.push(`sample ${id} starts within ${ms === null ? 'an unreadable time' : showMs(ms)}, the spec says ${T} s`);
  if (waits.exit === null) problems.push(`sample ${id} has no wait-exit within`);
  else if (waits.exit !== G * 1000) problems.push(`sample ${id} waits ${showMs(waits.exit)}, the spec says ${G} s`);
  return problems;
}

/** Check 6: T and G are stated in F-READY and F-STOP (and F-EOF's G equals F-STOP's), and every visible scenario waits for exactly those times. Olympiad tasks have neither. */
export function timeFindings(ctx) {
  if (ctx.kind === 'olympiad') return [];
  const { T, G } = timesOf(ctx);
  const findings = [];
  if (Number.isNaN(T)) findings.push('T is not stated: F-READY was not found whole');
  if (Number.isNaN(G)) findings.push('G is not stated: F-STOP was not found whole');
  const eof = ctx.found.get('F-EOF')?.hits[0];
  if (eof !== undefined && !Number.isNaN(G) && Number(eof.slots.G) !== G) findings.push(`F-EOF (${where(eof)}) says G is ${eof.slots.G} s, and F-STOP says ${G} s`);
  if (Number.isNaN(T) || Number.isNaN(G)) return findings;
  for (const [id, bytes] of ctx.scenarios) findings.push(...waitProblems(id, scenarioWaits(bytes.toString('utf8')), T, G));
  return findings;
}
