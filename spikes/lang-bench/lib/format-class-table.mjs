// FORMAT.md's class table and the parts it cuts the document into (E1.S9, T-L56). Every section of FORMAT has one class (`common`, `olympiad`, `daemon`, `web`,
// `daemon-web`), or, where it mixes kinds of task, every subsection has one and the lead text of the section (before its first subsection) is `common`.
// The table is the markdown table directly after the `**Section classes.**` paragraph of the preamble, rows `| <heading without ##/###> | <class> |`.
import { headingByNumber, numberOf, parseFormat, preambleSlice, SECTION_COUNT, sliceFrom } from './format-structure.mjs';

export const CLASSES = Object.freeze(['common', 'olympiad', 'daemon', 'web', 'daemon-web']);
/** The classes whose parts do not change after the freeze of the first group of tasks. */
export const PROTECTED = Object.freeze(['common', 'olympiad']);
export const isProtected = cls => PROTECTED.includes(cls);

const ok = value => ({ ok: true, ...value });
const fail = reason => ({ ok: false, reason });
const PARAGRAPH = '**Section classes.**';
const ROW = /^\| ([^|]+?) \| ([^|]+?) \|$/;
const SEPARATOR = /^\|\s*-+\s*\|\s*-+\s*\|$/;

const preambleLines = format => format.lines.filter(line => line.start < preambleSlice(format).end);

/** The index (in `after`, the lines from the paragraph on) of the first non-blank line after the paragraph's blank line, or -1. */
function tableStart(after) {
  const blank = after.findIndex(line => line.text === '');
  return blank === -1 ? -1 : after.findIndex((line, index) => index > blank && line.text !== '');
}

/** The lines of the table that follows the paragraph: from the first non-blank line after the blank one, while they start with `|`. */
function tableLinesAfter(after) {
  const start = tableStart(after);
  const rest = start === -1 ? [] : after.slice(start);
  const end = rest.findIndex(line => !line.text.startsWith('|'));
  return end === -1 ? rest : rest.slice(0, end);
}

const rowOf = line => {
  const cells = ROW.exec(line.text);
  return { raw: line.text, no: line.no, heading: cells?.[1] ?? null, cls: cells?.[2] ?? null };
};

/** `ok({ rows })` — `{ raw, no, heading, cls }` per data row, `heading` and `cls` null for a row that is not a two-cell row — or `fail(reason)` when the table cannot be found. */
export function readTable(format) {
  const preamble = preambleLines(format);
  const marked = preamble.filter(line => line.text.startsWith(PARAGRAPH));
  if (marked.length !== 1) return fail(`the preamble has ${marked.length} paragraphs starting \`${PARAGRAPH}\`, exactly one is required`);
  const table = tableLinesAfter(preamble.slice(preamble.indexOf(marked[0])));
  if (table.length < 3 || !SEPARATOR.test(table[1].text)) return fail(`no class table (a header row, a separator row and the rows) directly after the \`${PARAGRAPH}\` paragraph`);
  return ok({ rows: table.slice(2).map(rowOf) });
}

/** The rule a row breaks about its heading, or null: not a two-cell row, no such heading, a second row for it. */
function headingRule(row, byText, seen) {
  if (row.heading === null) return 'not a `| heading | class |` row';
  if (!byText.has(row.heading)) return 'no such heading in the document (a row names a heading by its whole text, without ## or ###)';
  return seen.has(row.heading) ? 'more than one row for this heading' : null;
}

const classRule = row => (row.cls !== null && !CLASSES.includes(row.cls) ? `class \`${row.cls}\` is not one of ${CLASSES.join(', ')}` : null);

function rowProblems(row, byText, seen) {
  const rules = [headingRule(row, byText, seen), classRule(row)].filter(rule => rule !== null);
  seen.add(row.heading);
  return rules.map(rule => ({ heading: row.heading ?? row.raw, no: row.no, rule }));
}

/** Every refusal of the class rules as `{ heading, no, rule }`: a row that names no heading or a class outside the five, a heading with two rows, a section without its row. */
export function tableProblems(format, rows) {
  const byText = new Map(format.headings.map(heading => [heading.text, heading]));
  const seen = new Set();
  return [...rows.flatMap(row => rowProblems(row, byText, seen)), ...sectionProblems(format, rows), ...changelogProblems(format, rows)];
}

const CHANGELOG_RULE = 'the changelog (the last section) must be one whole `common` row in the class table, with no row for a subsection: it only grows, and no class may avoid that';
const changelogOf = format => format.headings.find(heading => heading.level === 2 && numberOf(heading) === String(SECTION_COUNT));

function isWholeCommon(format, changelog, rows) {
  const own = rows.filter(row => row.heading === changelog.text);
  const subs = subsectionsOf(format, changelog).filter(sub => rows.some(row => row.heading === sub.text));
  return own.length === 1 && own[0].cls === 'common' && subs.length === 0;
}

/** The changelog (the last section) is one whole `common` row: nothing may class it otherwise, so the rule that it only grows cannot be avoided by a class. */
function changelogProblems(format, rows) {
  const changelog = changelogOf(format);
  if (changelog === undefined || isWholeCommon(format, changelog, rows)) return [];
  return [{ heading: changelog.text, no: changelog.no, rule: CHANGELOG_RULE }];
}

/** What a section's rows say: a row of its own and rows for subsections (`both`), a row of its own (`whole`), no row at all (`none`), rows for some subsections (`mixed`). */
const sectionState = (own, rowedSubs) => (own ? (rowedSubs.length > 0 ? 'both' : 'whole') : rowedSubs.length === 0 ? 'none' : 'mixed');

const SECTION_RULES = Object.freeze({
  both: ({ section, rowedSubs }) => [{ heading: section.text, no: section.no, rule: `the section has a row of its own and so has its subsection ${rowedSubs[0].text}: a mixed section takes one row per subsection and none of its own` }],
  whole: () => [],
  none: ({ section }) => [{ heading: section.text, no: section.no, rule: 'no row for the section, and none of its subsections has one' }],
  mixed: ({ subs, rowed }) => subs.filter(other => !rowed.has(other.text)).map(sub => ({ heading: sub.text, no: sub.no, rule: 'no row, and the section is mixed: every subsection takes one row' })),
});

function oneSectionProblems(format, section, rowed) {
  const subs = subsectionsOf(format, section);
  const rowedSubs = subs.filter(sub => rowed.has(sub.text));
  return SECTION_RULES[sectionState(rowed.has(section.text), rowedSubs)]({ section, subs, rowedSubs, rowed });
}

function sectionProblems(format, rows) {
  const rowed = new Set(rows.map(row => row.heading));
  return format.headings.filter(heading => heading.level === 2).flatMap(section => oneSectionProblems(format, section, rowed));
}

/** The `###` headings of a `##` section, in order. */
export function subsectionsOf(format, section) {
  const rest = format.headings.slice(format.headings.indexOf(section) + 1);
  const end = rest.findIndex(heading => heading.level === 2);
  return (end === -1 ? rest : rest.slice(0, end)).filter(heading => heading.level === 3);
}

/**
 * The parts the class rule cuts FORMAT into, in order: the preamble (class `common`); for a section classed as a whole, the section from its `##` line to the byte before
 * the next `##` (its subsections belong to it); for a mixed section, its lead text (class `common`) and each subsection up to the next `###` or `##`. `{ key, kind, number,
 * heading, cls, start, end, no }`; no byte is in two parts or in none.
 */
export function partsOf(format, rows) {
  const classOf = new Map(rows.map(row => [row.heading, row.cls]));
  const parts = [{ key: 'preamble', kind: 'preamble', number: '', heading: 'preamble', cls: 'common', no: 1, ...preambleSlice(format) }];
  for (const section of format.headings.filter(heading => heading.level === 2)) {
    const number = numberOf(section);
    if (classOf.has(section.text)) {
      parts.push({ key: number, kind: 'section', number, heading: section.text, cls: classOf.get(section.text), no: section.no, ...sliceFrom(format, section, 2) });
      continue;
    }
    parts.push({ key: `${number}:lead`, kind: 'lead', number, heading: section.text, cls: 'common', no: section.no, ...sliceFrom(format, section, 3) });
    for (const sub of subsectionsOf(format, section)) {
      parts.push({ key: numberOf(sub), kind: 'sub', number: numberOf(sub), heading: sub.text, cls: classOf.get(sub.text), no: sub.no, ...sliceFrom(format, sub, 3) });
    }
  }
  return parts;
}

/** The part of the same shape as `part` (a part of another file) in `format`, found by the heading's number: the slice to compare, or null when no such heading. */
export function sliceLike(format, part) {
  if (part.kind === 'preamble') return { heading: null, ...preambleSlice(format) };
  const heading = headingByNumber(format, part.number);
  return heading === undefined ? null : { heading, ...sliceFrom(format, heading, part.kind === 'section' ? 2 : 3) };
}

/** `ok({ format, rows })` for FORMAT's bytes, or `{ ok: false, exit: 4, reason }` when the bytes, the headings or the table cannot be read. */
export function readClassed(bytes, label = 'FORMAT') {
  const parsed = parseFormat(bytes);
  if (!parsed.ok) return { ok: false, exit: 4, reason: `${label} line ${parsed.line}: ${parsed.reason}` };
  const table = readTable(parsed.format);
  return table.ok ? ok({ format: parsed.format, rows: table.rows }) : { ok: false, exit: 4, reason: `${label}: ${table.reason}` };
}

const lineFor = (label, problem) => `format-classes: ${problem.heading}, line ${problem.no}: ${problem.rule}`;

/** `--self`: `{ exit, lines }` — 0 when the class table is complete and every row names a heading, 1 with one line per refusal, 4 for a format error. */
export function checkSelf(bytes) {
  const read = readClassed(bytes);
  if (!read.ok) return { exit: 4, lines: [`format-classes: ${read.reason}`] };
  const problems = tableProblems(read.format, read.rows);
  return problems.length === 0 ? { exit: 0, lines: [`format-classes: the class table is complete (${read.rows.length} rows)`] } : { exit: 1, lines: problems.map(problem => lineFor('FORMAT', problem)) };
}
