// Check 5, the fixed sentences, and check 6, T and G (E1.S8). Each check returns the list of its findings as text; an empty list is `ok`.
import { sectionOf } from './conformance-sentences.mjs';

const WORDS = new Map(Object.entries({ one: 1, two: 2, three: 3, four: 4, five: 5, six: 6, seven: 7, eight: 8, nine: 9, ten: 10, eleven: 11, twelve: 12, thirteen: 13, fourteen: 14, fifteen: 15, sixteen: 16, seventeen: 17, eighteen: 18, nineteen: 19, twenty: 20, thirty: 30, forty: 40, fifty: 50, sixty: 60, seventy: 70, eighty: 80, ninety: 90 }));
const XOR = Object.freeze(['F-INVALID', 'F-ERRORS']);
export const REFUSED = Object.freeze({ 'F-BYTES-O-N': 'F-BYTES-O-N is refused for this series: no task uses a normalised output and the runner does not build it (CP1 F12)' });
const MAX_SECONDS = 600;

/** `twenty-one`: the tens word and the units word. */
const hyphenated = (tens, units) => (WORDS.get(tens) >= 20 && WORDS.get(units) < 10 ? WORDS.get(tens) + WORDS.get(units) : null);

function wordNumber(text) {
  const [tens, units, more] = text.split('-');
  if (more !== undefined || !WORDS.has(tens)) return null;
  return units === undefined ? WORDS.get(tens) : hyphenated(tens, units);
}

/** The number a `{k}` slot says, in digits or words up to ninety-nine; null when it is neither. */
export const numberOf = text => (/^\d+$/.test(text) ? Number(text) : wordNumber(text));

const where = hit => `line ${hit.line}`;

const rangeOf = found => [Number(found[1]), Number(found[2] ?? found[1])];

const missingRef = (id, slot, found, nMax, hit) => (rangeOf(found).some(n => n > nMax) ? [`${id} (${where(hit)}): {${slot}} names ${found[0]}, and the spec has requirements R1 to R${nMax} only`] : []);

function descendingRef(id, slot, found, hit) {
  const [low, high] = rangeOf(found);
  return found[2] !== undefined && high <= low ? [`${id} (${where(hit)}): {${slot}} has the range ${found[0]}, which does not ascend`] : [];
}

/** The problems of the R-ids a `{ref}` or `{eref}` slot names: each must be a requirement of the spec, and a range must ascend. */
const refProblems = (id, slot, text, nMax, hit) => [...text.matchAll(/R([1-9]\d*)(?:–R([1-9]\d*))?/g)].flatMap(found => [...missingRef(id, slot, found, nMax, hit), ...descendingRef(id, slot, found, hit)]);

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

const refSlot = (ctx, id, hit, name) => (hit.slots[name] === undefined ? [] : refProblems(id, name, hit.slots[name], ctx.maxId, hit));

const kSlot = (ctx, id, hit) => (hit.slots.k !== undefined && numberOf(hit.slots.k) !== ctx.shownIds.length ? [`${id} (${where(hit)}): says ${hit.slots.k} samples, and section 6 shows ${ctx.shownIds.length}`] : []);

const whichMust = eof => (eof ? '"A test that does not end at the end of its input" because F-EOF is present' : '"A test" because F-EOF is absent');

function whichSlot(ctx, id, hit) {
  const eof = (ctx.found.get('F-EOF')?.hits.length ?? 0) > 0;
  const wrong = hit.slots.which !== undefined && (hit.slots.which !== 'A test') !== eof;
  return wrong ? [`${id} (${where(hit)}): {which} is "${hit.slots.which}", and it must be ${whichMust(eof)}`] : [];
}

const durationSlot = (id, hit, name) => (hit.slots[name] !== undefined && Number(hit.slots[name]) > MAX_SECONDS ? [`${id} (${where(hit)}): {${name}} is ${hit.slots[name]} s, and a duration is at most ${MAX_SECONDS} s`] : []);

/** The problems of the slot values of one hit of sentence `id`. */
const slotProblems = (ctx, id, hit) => [
  ...refSlot(ctx, id, hit, 'ref'), ...refSlot(ctx, id, hit, 'eref'), ...kSlot(ctx, id, hit), ...whichSlot(ctx, id, hit),
  ...['T', 'G', 'duration'].flatMap(name => durationSlot(id, hit, name)),
];

const duplicateProblems = f => (f.hits.length > 1 && f.entry.id !== 'F-TIMING' ? [`${f.entry.id} stands ${f.hits.length} times (${f.hits.map(where).join(', ')}); FORMAT says it once`] : []);

function sectionProblems(ctx, f, hit) {
  const section = sectionOf(f.entry, ctx.kind);
  const wrong = section !== null && hit.section !== section && f.entry.where.mode !== 'first-paragraph';
  return wrong ? [`${f.entry.id} (${where(hit)}) stands in section ${hit.section}, and 2.1 puts it in section ${section}`] : [];
}

const sameAsProblems = (ctx, f, hit) => (Array.isArray(f.entry.where.sameAs) ? sameProblems(ctx, f, hit, f.entry.where.sameAs, f.entry.where.after) : []);

/** The problems of where the hits of a required or present sentence stand. */
function placementProblems(ctx, f) {
  const hit = f.hits[0];
  const closing = f.entry.where.mode === 'opens-closes' ? opensClosesProblems(f) : [];
  return [...duplicateProblems(f), ...sectionProblems(ctx, f, hit), ...sameAsProblems(ctx, f, hit), ...closing, ...slotProblems(ctx, f.entry.id, hit)];
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

/** A sentence with no whole hit: altered where its words stand, missing when it is required. */
function absentProblems(ctx, f) {
  const { entry, altered } = f;
  if (altered !== null) return [`${entry.id} is altered at line ${altered}: it does not match FORMAT's sentence word for word (after whitespace is collapsed)`];
  return entry.required && !XOR.includes(entry.id) ? [`${entry.id} is missing: 2.1 requires it in section ${sectionOf(entry, ctx.kind) ?? 'before section 1'}`] : [];
}

const outsideProblems = f => f.stray.map(hit => `${f.entry.id} (${where(hit)}) stands outside every requirement paragraph; a fixed sentence belongs to a requirement (FORMAT 1.3)`);

/** The findings about one sentence: outside every requirement, missing or altered, misplaced. */
const sentenceProblems = (ctx, f) => [...outsideProblems(f), ...(f.hits.length === 0 ? absentProblems(ctx, f) : placementProblems(ctx, f))];

const carried = (ctx, id) => (ctx.found.get(id)?.hits.length ?? 0) > 0;
const alteredId = (ctx, id) => typeof ctx.found.get(id)?.altered === 'number';
const settled = (present, altered) => present.length === 1 || (present.length === 0 && altered);

/** F-INVALID xor F-ERRORS: exactly one stands. */
function xorProblems(ctx) {
  const present = XOR.filter(id => carried(ctx, id));
  if (settled(present, XOR.some(id => alteredId(ctx, id)))) return [];
  return [present.length === 0 ? 'neither F-INVALID nor F-ERRORS stands: a spec carries exactly one' : 'F-INVALID and F-ERRORS both stand: a spec carries exactly one'];
}

const misapplied = f => (f.hits.length > 0 ? [`${f.entry.id} (${where(f.hits[0])}) is carried by this ${f.kind} spec, and 2.1 lists it for ${[...f.entry.kinds].join(', ')} only`] : []);

/** Check 5: every fixed sentence the kind requires, word for word, in its place, with its slots filled by allowed values. */
const refusedProblems = f => (f.hits.length > 0 ? [`${REFUSED[f.entry.id]} (${where(f.hits[0])})`] : []);

/** The findings of one located sentence: refused for the series, misapplied to the kind, or its own placement and slots. */
function oneSentence(ctx, f) {
  if (REFUSED[f.entry.id] !== undefined) return refusedProblems(f);
  return f.applies ? sentenceProblems(ctx, f) : misapplied({ ...f, kind: ctx.kind });
}

export function sentenceFindings(ctx) {
  const findings = [...ctx.found.values()].flatMap(f => oneSentence(ctx, f));
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

const noStart = (id, waits) => (waits.start.length === 0 ? [`sample ${id} has no start line`] : []);
const showStart = ms => (ms === null ? 'an unreadable time' : showMs(ms));

const startProblems = (id, waits, T) => waits.start.filter(ms => ms !== T * 1000).map(ms => `sample ${id} starts within ${showStart(ms)}, the spec says ${T} s`);

function exitProblems(id, waits, G) {
  if (waits.exit === null) return [`sample ${id} has no wait-exit within`];
  return waits.exit !== G * 1000 ? [`sample ${id} waits ${showMs(waits.exit)}, the spec says ${G} s`] : [];
}

const waitProblems = (id, waits, T, G) => [...noStart(id, waits), ...startProblems(id, waits, T), ...exitProblems(id, waits, G)];

const notStated = (T, G) => [...(Number.isNaN(T) ? ['T is not stated: F-READY was not found whole'] : []), ...(Number.isNaN(G) ? ['G is not stated: F-STOP was not found whole'] : [])];

const eofDiffers = (eof, G) => eof !== undefined && !Number.isNaN(G) && Number(eof.slots.G) !== G;
const eofProblems = (ctx, G) => {
  const eof = ctx.found.get('F-EOF')?.hits[0];
  return eofDiffers(eof, G) ? [`F-EOF (${where(eof)}) says G is ${eof.slots.G} s, and F-STOP says ${G} s`] : [];
};

/** Check 6: T and G are stated in F-READY and F-STOP (and F-EOF's G equals F-STOP's), and every visible scenario waits for exactly those times. Olympiad tasks have neither. */
export function timeFindings(ctx) {
  if (ctx.kind === 'olympiad') return [];
  const { T, G } = timesOf(ctx);
  const findings = [...notStated(T, G), ...eofProblems(ctx, G)];
  if (Number.isNaN(T) || Number.isNaN(G)) return findings;
  return [...findings, ...[...ctx.scenarios].flatMap(([id, bytes]) => waitProblems(id, scenarioWaits(bytes.toString('utf8')), T, G))];
}
