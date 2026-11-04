// FORMAT.md's class table and the parts it cuts the document into (E1.S9, T-L56). Every section of FORMAT has one class (`common`, `olympiad`, `daemon`, `web`,
// `daemon-web`), or, where it mixes kinds of task, every subsection has one and the lead text of the section (before its first subsection) is `common`.
// The table is the markdown table directly after the `**Section classes.**` paragraph of the preamble, rows `| <heading without ##/###> | <class> |`.
import { headingByNumber, numberOf, parseFormat, preambleSlice, sliceFrom } from './format-structure.mjs';

export const CLASSES = Object.freeze(['common', 'olympiad', 'daemon', 'web', 'daemon-web']);
/** The classes whose parts do not change after the freeze of the first group of tasks. */
export const PROTECTED = Object.freeze(['common', 'olympiad']);
export const isProtected = cls => PROTECTED.includes(cls);

const ok = value => ({ ok: true, ...value });
const fail = reason => ({ ok: false, reason });
const PARAGRAPH = '**Section classes.**';
const ROW = /^\| ([^|]+?) \| ([^|]+?) \|$/;
const SEPARATOR = /^\|\s*-+\s*\|\s*-+\s*\|$/;

/** `ok({ rows })` — `{ raw, no, heading, cls }` per data row, `heading` and `cls` null for a row that is not a two-cell row — or `fail(reason)` when the table cannot be found. */
export function readTable(format) {
  const preamble = format.lines.filter(line => line.start < preambleSlice(format).end);
  const marked = preamble.filter(line => line.text.startsWith(PARAGRAPH));
  if (marked.length !== 1) return fail(`the preamble has ${marked.length} paragraphs starting \`${PARAGRAPH}\`, exactly one is required`);
  const after = preamble.slice(preamble.indexOf(marked[0]));
  const blank = after.findIndex(line => line.text === '');
  const start = blank === -1 ? -1 : after.findIndex((line, index) => index > blank && line.text !== '');
  const table = [];
  for (const line of start === -1 ? [] : after.slice(start)) {
    if (!line.text.startsWith('|')) break;
    table.push(line);
  }
  if (table.length < 3 || !SEPARATOR.test(table[1].text)) return fail(`no class table (a header row, a separator row and the rows) directly after the \`${PARAGRAPH}\` paragraph`);
  const rows = table.slice(2).map(line => {
    const cells = ROW.exec(line.text);
    return { raw: line.text, no: line.no, heading: cells?.[1] ?? null, cls: cells?.[2] ?? null };
  });
  return ok({ rows });
}

/** Every refusal of the class rules as `{ heading, no, rule }`: a row that names no heading or a class outside the five, a heading with two rows, a section without its row. */
export function tableProblems(format, rows) {
  const problems = [];
  const byText = new Map(format.headings.map(heading => [heading.text, heading]));
  const seen = new Set();
  for (const row of rows) {
    const shown = row.heading ?? row.raw;
    if (row.heading === null) problems.push({ heading: shown, no: row.no, rule: 'not a `| heading | class |` row' });
    else if (!byText.has(row.heading)) problems.push({ heading: shown, no: row.no, rule: 'no such heading in the document (a row names a heading by its whole text, without ## or ###)' });
    else if (seen.has(row.heading)) problems.push({ heading: shown, no: row.no, rule: 'more than one row for this heading' });
    seen.add(row.heading);
    if (row.cls !== null && !CLASSES.includes(row.cls)) problems.push({ heading: shown, no: row.no, rule: `class \`${row.cls}\` is not one of ${CLASSES.join(', ')}` });
  }
  return [...problems, ...sectionProblems(format, rows)];
}

function sectionProblems(format, rows) {
  const rowed = new Set(rows.map(row => row.heading));
  const problems = [];
  for (const section of format.headings.filter(heading => heading.level === 2)) {
    const subs = subsectionsOf(format, section);
    const rowedSubs = subs.filter(sub => rowed.has(sub.text));
    if (rowed.has(section.text) && rowedSubs.length > 0) {
      problems.push({ heading: section.text, no: section.no, rule: `the section has a row of its own and so has its subsection ${rowedSubs[0].text}: a mixed section takes one row per subsection and none of its own` });
    } else if (!rowed.has(section.text) && rowedSubs.length === 0) {
      problems.push({ heading: section.text, no: section.no, rule: 'no row for the section, and none of its subsections has one' });
    } else if (!rowed.has(section.text)) {
      for (const sub of subs.filter(other => !rowed.has(other.text))) problems.push({ heading: sub.text, no: sub.no, rule: 'no row, and the section is mixed: every subsection takes one row' });
    }
  }
  return problems;
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
