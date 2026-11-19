// The dated lines of Stage 1 (F3.S1 (a), PR 3): the registered `inputs/prereg/amendments.md`, read by code. Four kinds of line are this module's own; every other line goes to v1's `parseAmendments` (not changed), which reads the by-hand
// verdicts (the read-back lines, the lines that void a session) and the instrument lines:
//   YYYY-MM-DD | owner-go | pilot | <the owner's words>             the owner's go for the pilot's paid sessions (O2)
//   YYYY-MM-DD | owner-go | rounds:<a>-<b> | <the owner's words>    the owner's go for rounds a to b (O3)
//   YYYY-MM-DD | caps | wallMs:<n> | stallMs:<n> | <reason, with G-18>   the one change of the wall and the stall
//   YYYY-MM-DD | pilot | GO | verdicts:<12 hex of the sha256 of the GO table's file> | <who>   the pilot's GO
// A line of these kinds that is not valid is REPORTED (`malformed`, with why), never counted. A date counts only when it is a real calendar date. Pure over the text; `readStageAmendments` is the one edge.
import { existsSync, readFileSync } from 'node:fs';
import { appliesToSeries, parseAmendments } from '../../minigit-bench/lib/amendments.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { STAGE_ROUNDS, isCalendarDate } from './stage-schedule.mjs';
import { INPUTS } from './paths.mjs';
import path from 'node:path';

/** The registered file: Stage 1's amendments, one of the pre-registration's files (PR 6 writes it; the dated lines are appended to it). */
export const STAGE_AMENDMENTS_FILE = path.join(INPUTS, 'prereg', 'amendments.md');

const OWN_KINDS = Object.freeze(['owner-go', 'caps', 'pilot']);
const bad = why => ({ malformed: why });
const rest = (f, from) => f.slice(from).join(' | ');
const WORDS = 'an owner-go line needs the owner\'s words';

/** The fields of a line; a trailing ` |` is an empty last field. */
const fieldsOf = line => (line.endsWith(' |') ? `${line} ` : line).split(' | ').map(x => x.trim());

/** The scope of an `owner-go` line: `'pilot'` or `{ from, to }` of `rounds:<a>-<b>`; null when it is neither. */
const roundsOf = text => {
  const m = /^rounds:(\d+)-(\d+)$/.exec(text ?? '');
  return m === null ? { from: 0, to: 0 } : { from: Number(m[1]), to: Number(m[2]) };
};

const legalRounds = ({ from, to }) => from >= 1 && from <= to && to <= STAGE_ROUNDS;

function scopeOf(text) {
  if (text === 'pilot') return 'pilot';
  const rounds = roundsOf(text);
  return legalRounds(rounds) ? rounds : null;
}

const SCOPE_SENTENCE = `the scope is pilot or rounds:<a>-<b> with 1 <= a <= b <= ${STAGE_ROUNDS}`;

function parseOwnerGo(f) {
  const scope = scopeOf(f[2]);
  if (scope === null) return bad(SCOPE_SENTENCE);
  return f[3] ? { ownerGo: { date: f[0], scope, words: rest(f, 3) } } : bad(WORDS);
}

const wholeNumber = (text, key) => {
  const m = new RegExp(`^${key}:([1-9]\\d*)$`).exec(text ?? '');
  return m === null ? null : Number(m[1]);
};

function parseCaps(f) {
  const [wallMs, stallMs] = [wholeNumber(f[2], 'wallMs'), wholeNumber(f[3], 'stallMs')];
  if (wallMs === null) return bad('a caps line is: date | caps | wallMs:<n> | stallMs:<n> | reason; wallMs is a positive whole number of milliseconds');
  if (stallMs === null) return bad('a caps line is: date | caps | wallMs:<n> | stallMs:<n> | reason; stallMs is a positive whole number of milliseconds');
  return f[4] ? { caps: { date: f[0], wallMs, stallMs, reason: rest(f, 4) } } : bad('a caps line needs its reason (with its G-id)');
}

function parsePilotGo(f) {
  if (f[2] !== 'GO') return bad('a pilot line is: date | pilot | GO | verdicts:<12 hex> | who (the word is GO)');
  const m = /^verdicts:([0-9a-f]{12})$/.exec(f[3] ?? '');
  if (m === null) return bad('a pilot GO line names verdicts:<12 hex of the sha256 of the GO table\'s file>');
  return f[4] ? { pilotGo: { date: f[0], verdicts12: m[1], who: rest(f, 4) } } : bad('a pilot GO line needs who wrote it (who)');
}

const PARSERS = Object.freeze({ 'owner-go': parseOwnerGo, caps: parseCaps, pilot: parsePilotGo });

/** One line of ours: `{ ownerGo } | { caps } | { pilotGo } | { malformed }`. A date that is no calendar date is malformed whatever the rest says. */
function parseOwn(f) {
  if (!isCalendarDate(f[0])) return bad('the line has no real calendar date (YYYY-MM-DD)');
  return PARSERS[f[1]](f);
}

const isOwn = line => /^\d/.test(line) && OWN_KINDS.includes(fieldsOf(line)[1]);

const pick = (parsed, key) => parsed.flatMap(p => (p[key] ? [{ ...p[key], line: p.line }] : []));
const byDate = (a, b) => (a.date < b.date ? -1 : Number(a.date > b.date));

/**
 * The text -> `{ ownerGo: [{ date, scope, words, line }], caps, pilotGo, malformed: [{ line, why }], v1 }`, the lines of each kind in date order (a stable sort: lines of one date keep the file's order). `v1` is v1's `parseAmendments` of every OTHER line, so v1 never
 * sees (and never calls malformed) a line of ours. An undated or impossibly dated line of ours is malformed.
 */
export function parseStageAmendments(text) {
  const lines = String(text ?? '').split('\n').map(l => l.trim());
  const own = lines.filter(isOwn).map(line => ({ line, ...parseOwn(fieldsOf(line)) }));
  return {
    ownerGo: pick(own, 'ownerGo').toSorted(byDate), caps: pick(own, 'caps').toSorted(byDate), pilotGo: pick(own, 'pilotGo').toSorted(byDate),
    malformed: own.filter(p => p.malformed).map(p => ({ line: p.line, why: p.malformed })), v1: parseAmendments(lines.filter(l => !isOwn(l)).join('\n')),
  };
}

const covers = (scope, line) => (line.scope === 'pilot' ? scope.pilot === true : scope.round >= line.scope.from && scope.round <= line.scope.to);
const GO_FORMS = '`YYYY-MM-DD | owner-go | pilot | <the owner\'s words>` or `YYYY-MM-DD | owner-go | rounds:<a>-<b> | <the owner\'s words>`';

/** What a launch's scope is, as the sentence names it. */
const nameOf = scope => (scope.pilot === true ? 'the pilot' : `round ${scope.round}`);

const roundProblem = round => (round >= 1 && round <= STAGE_ROUNDS ? null : `round ${round}: the rounds are 1 to ${STAGE_ROUNDS}`);

function scopeProblem(scope) {
  if (scope?.pilot === true) return null;
  return Number.isInteger(scope?.round) ? roundProblem(scope.round) : 'the scope of a launch is { pilot: true } or { round: n }';
}

/**
 * The owner's go line that covers a launch's scope (`{ pilot: true }` or `{ round: n }`): the earliest dated one. A go for the pilot covers no round, and one for rounds a to b covers no pilot step and no other round. A malformed line is no go.
 * -> `ok({ date, scope, words, line })` | `fail(sentence)` naming the scope and the lines that would cover it.
 */
export function ownerGoLine({ lines, scope }) {
  const problem = scopeProblem(scope);
  if (problem) return fail(problem);
  const found = lines.ownerGo.find(line => covers(scope, line));
  return found ? ok(found) : fail(`${nameOf(scope)}: no owner-go line covers ${nameOf(scope)}: a paid launch needs the owner's own words with a date as a line of the registered amendments (${GO_FORMS})${lines.malformed.length > 0 ? `; ${lines.malformed.length} malformed line(s) of the dated kinds were reported and count for nothing` : ''}`);
}

/** The sentence refusing a launch for want of its owner-go line, or null. */
export function ownerGoProblem(args) {
  const found = ownerGoLine(args);
  return found.ok ? null : found.reason;
}

/**
 * The sentence saying the pilot has no GO, or null: a dated `pilot | GO` line exists. `verdicts12`, the first 12 hex of the sha256 of the GO table's file, is given only by a reader that has the file (the harness never reads it): a GO line that names
 * another hash is then reported, never counted.
 */
export function pilotGoProblem({ lines, verdicts12 = null }) {
  const line = lines.pilotGo[0];
  if (line === undefined) return 'no dated `pilot | GO` line in the registered amendments: the pilot is not GO';
  return verdicts12 === null || line.verdicts12 === verdicts12 ? null : `the GO line of ${line.date} names verdicts:${line.verdicts12}, the GO table's file hashes to ${verdicts12}: the line is not counted`;
}

/** The one `caps` line: `null` when there is none, `{ wallMs, stallMs, date }`, or `{ problem }` when there are two (the wall and the stall change once). */
export function capsOf(lines) {
  if (lines.caps.length === 0) return null;
  return lines.caps.length === 1 ? { wallMs: lines.caps[0].wallMs, stallMs: lines.caps[0].stallMs, date: lines.caps[0].date } : { problem: `${lines.caps.length} caps lines: the wall and the stall change once, by one \`caps\` line` };
}

/** The dated by-hand verdict lines (`{ date, g, verdict, rid, evidence, who }`) the schedule's read-back gate takes as `readbackLines`: v1's reader over the lines that are not ours. */
export const readbackLinesOf = text => parseStageAmendments(text).v1.verdicts;

/** The valid by-hand verdicts about one session (the loop's `handVerdictsFor`): a dated `refuted` line makes the session `void: harness`. `seriesId` makes a line that names another series' evidence not apply. */
export const stageHandVerdictsFor = (text, rid, seriesId = undefined) => parseStageAmendments(text).v1.verdicts.filter(v => v.rid === rid && appliesToSeries(v, seriesId));

/** The dated lines whose text holds the home path written out: an evidence path is written with the home as `~`. -> `[{ line, why }]`. */
export function homePathProblems(text, home) {
  const lines = String(text ?? '').split('\n').map(l => l.trim()).filter(l => /^\d{4}-\d{2}-\d{2} \|/.test(l));
  return lines.filter(l => l.includes(`${home}/`)).map(line => ({ line, why: `the line holds the home path ${home}: an evidence path is written with the home as ~` }));
}

/** The registered file's text: `ok('')` when it is absent (no line), `ok(text)`, or a `fail` when it cannot be read (never taken for an empty file). */
export function readStageAmendments(file = STAGE_AMENDMENTS_FILE) {
  if (!existsSync(file)) return ok('');
  try { return ok(readFileSync(file, 'utf8')); } catch (e) { return fail(`${file} is unreadable: ${e.code ?? e.message}`); }
}
