// The frozen file against a candidate (E1.S9, T-L56): after the freeze of the first group of tasks the `common` and `olympiad` parts of FORMAT do not change, and a
// `daemon`, `web` or `daemon-web` part may. The protected set comes from the FROZEN file's class table only. Parts are compared as raw bytes; a boundary blank line belongs to the
// part before it. Exceptions, each named in the rule: the preamble's three header rows and class-table rows appended after the last row; section 7 (the changelog) may only grow;
// 2.1's fixed-sentence table is compared row by row. The header rows keep their lines and match a strict pattern; the rest of the preamble is compared as bytes.
import { grammarRefusals, partRefusals as formRefusals } from './format-grammar.mjs';
import { checkSelf, isProtected, partsOf, readClassed, sliceLike, tableProblems } from './format-class-table.mjs';
import { headingByNumber, numberOf, sha256Hex, sliceFrom, unfencedLines } from './format-structure.mjs';

const HEADER_LABELS = Object.freeze(['Format version', 'Gate verdict', 'Freeze tag']);
const HEADER_ROW = new RegExp(`^(${HEADER_LABELS.join('|')}): `);
/** The value of each header row: a version number; a verdict of one to four lower-case words (letters, digits, `_`, `.`, `-`; no colon; at most 80 characters) or the spec-header form (a back-quoted verdict word, then optionally ` · ` and a date `yyyy-mm-dd`); `none yet` or a freeze tag. */
export const HEADER_PATTERNS = Object.freeze({
  'Format version': /^\d+(?:\.\d+)?$/,
  'Gate verdict': /^(?=.{1,80}$)(?:[a-z0-9][a-z0-9_.-]*(?: [a-z0-9][a-z0-9_.-]*){0,3}|`[a-z][a-z_-]*`(?: · \d{4}-\d{2}-\d{2})?)$/,
  'Freeze tag': /^(?:none yet|spec-frozen(?:-[A-Z])?-\d{8})$/,
});
const TABLE_HEADER = /^\| Id \|/;
const TABLE_SEPARATOR = /^\|[-| :]+\|$/;
const OLYMPIAD_KINDS = /\b(?:all|olympiad)\b/i;
const FENCE_LINE = /^\s*(`{3,}|~{3,})/;
const F_ROW = /^\| (F-[A-Z0-9]+(?:-[A-Z0-9]+)*) \|/;
const refusal = (heading, cls, where, rule) => `format-classes: ${heading} (class ${cls}), ${where}: ${rule}`;
const here = no => `line ${no}`;
const there = no => `frozen line ${no}`;
const titleOf = text => text.replace(/^[\d.]+\s*/, '').trim();
const labelOf = part => (part.kind === 'lead' ? `${part.heading} (lead text)` : part.heading);

/** The line number of the first line where two texts differ, counted from `firstLine` (the end of the shorter text counts). */
function firstDifference(textA, textB, firstLine) {
  const a = textA.split('\n');
  const b = textB.split('\n');
  const at = a.findIndex((line, index) => line !== b[index]);
  return firstLine + (at === -1 ? a.length : at);
}

/** The number of the heading a class row names in `format`, or null. */
function numberOfRow(format, row) {
  const heading = format.headings.find(entry => entry.text === row.heading);
  return heading === undefined ? null : numberOf(heading);
}

/** The next place in the candidate's rows after the frozen `row` (`at` is the place now), or null when the row breaks the rule. */
function advance(frozen, candidate, row, at) {
  const next = candidate.rows[at];
  if (next?.raw === row.raw) return at + 1;
  if (isProtected(row.cls)) return null;
  return isRetitled(frozen, candidate, row, next) ? at + 1 : at;
}

const isRetitled = (frozen, candidate, row, next) => next !== undefined && numberOfRow(candidate.format, next) !== null && numberOfRow(candidate.format, next) === numberOfRow(frozen.format, row);

/** The place of the first class-table row of the candidate that breaks the rule, or -1: A's protected rows stay byte for byte and in order, A's other rows may change, be retitled (the row names the same heading number) or go, new rows come after A's last. */
function firstBadRow(frozen, candidate) {
  let at = 0;
  for (const row of frozen.rows) {
    const next = advance(frozen, candidate, row, at);
    if (next === null) return at;
    at = next;
  }
  return -1;
}

const preambleOf = side => side.format.text.slice(0, side.format.headings[0].start).split('\n');

const labelRows = (label, lines) => lines.flatMap((text, index) => (text.startsWith(`${label}: `) ? [index + 1] : []));
const lineMoved = (was, row) => was > 0 && row !== was;
const headerFail = (where, rule) => [refusal('preamble', 'common', here(where), rule)];

/** The refusals for one header row of the candidate: present once, on the line the frozen file has it, with a value that matches `HEADER_PATTERNS`. */
function headerRowRefusals(label, a, b) {
  const rows = labelRows(label, b);
  const was = labelRows(label, a)[0] ?? 0;
  if (rows.length !== 1) return headerFail(1, `the header row \`${label}\` is missing or appears more than once`);
  if (lineMoved(was, rows[0])) return headerFail(rows[0], `the header row \`${label}\` is at line ${rows[0]}, the frozen file has it at line ${was}: the header rows keep their lines`);
  return HEADER_PATTERNS[label].test(b[rows[0] - 1].slice(label.length + 2)) ? [] : headerFail(rows[0], `the value of the header row \`${label}\` does not match the pattern ${HEADER_PATTERNS[label]}`);
}

/** The refusals for the three header rows of the candidate: one row each, on the line the frozen file has it, with a value that matches `HEADER_PATTERNS`. */
const headerRefusals = (frozen, candidate) => HEADER_LABELS.flatMap(label => headerRowRefusals(label, preambleOf(frozen), preambleOf(candidate)));

/** The preamble's lines without the header rows and the table's data rows, as `{ text, no }`. */
function plainPreamble(side) {
  const dropped = new Set(side.rows.map(row => row.no));
  return preambleOf(side).map((text, index) => ({ text, no: index + 1 })).filter(line => !dropped.has(line.no) && !HEADER_ROW.test(line.text));
}

/** The first place where two lists of `{ text }` differ (the end of the shorter counts), or undefined. */
const firstDifferingText = (a, b) => Array.from({ length: Math.max(a.length, b.length) }, (_, index) => index).find(index => a[index]?.text !== b[index]?.text);

const lineNoAt = (entries, index) => (entries[index] ?? entries.at(-1) ?? { no: 1 }).no;

/** The candidate line of the first protected byte of the preamble that differs (a plain line, else a class-table row), or null. */
function badPreambleLine(frozen, candidate) {
  const plainB = plainPreamble(candidate);
  const first = firstDifferingText(plainPreamble(frozen), plainB);
  if (first !== undefined) return lineNoAt(plainB, first);
  const row = firstBadRow(frozen, candidate);
  return row === -1 ? null : lineNoAt(candidate.rows, row);
}

/** The preamble without the three header rows and the table's data rows must be equal; the data rows follow `firstBadRow`'s rule; the header rows follow `headerRefusals`. */
function preambleRefusals(frozen, candidate) {
  const refusals = headerRefusals(frozen, candidate);
  const bad = badPreambleLine(frozen, candidate);
  return bad === null ? refusals : [...refusals, refusal('preamble', 'common', here(1), `protected bytes differ (first difference at candidate line ${bad})`)];
}

const isParagraphLine = (open, line) => open.has(line.no) && line.text !== '';

/** Every paragraph of unfenced lines with the index of the line that ended it: `{ paragraph: [line], index }`. */
function paragraphEnds(lines, open) {
  const ends = [];
  let paragraph = [];
  for (const [index, line] of lines.entries()) {
    if (isParagraphLine(open, line)) paragraph.push(line);
    else {
      if (paragraph.length > 0) ends.push({ paragraph, index });
      paragraph = [];
    }
  }
  return ends;
}

/** True when the first non-blank line from `index` on is a fence line. */
function fenceFollows(lines, index) {
  const next = lines.slice(index).find(other => other.text.trim() !== '');
  return next !== undefined && FENCE_LINE.test(next.text);
}

/** The paragraphs outside fences that are followed, after blank lines, by a fence line (an indented one too): the text that introduces a sentence (`{ start, text }`). */
function introductions(format) {
  const open = new Set(unfencedLines(format.lines).map(line => line.no));
  return paragraphEnds(format.lines, open).filter(end => fenceFollows(format.lines, end.index))
    .map(end => ({ start: end.paragraph[0].start, text: end.paragraph.map(entry => entry.text).join('\n') }));
}

/** Where a sentence is defined, as offsets: `F-ID` at the start of a line outside fences (its label); a sentence with no label is defined by the paragraph that introduces a fenced block and names it. */
function definitionStarts(format, id) {
  const label = new RegExp(`^${id}(?![A-Za-z0-9-])`);
  const labelled = unfencedLines(format.lines).filter(line => label.test(line.text)).map(line => line.start);
  const mention = new RegExp(`(^|[^A-Za-z0-9-])${id}(?![A-Za-z0-9-])`);
  return labelled.length > 0 ? labelled : introductions(format).filter(paragraph => mention.test(paragraph.text)).map(paragraph => paragraph.start);
}

/** True when the sentence `id` is defined somewhere outside 2.1's table and every part defining it is not protected. */
function isFree(side, id) {
  const classes = definitionStarts(side.format, id).map(start => side.parts.find(part => part.start <= start && start < part.end)?.cls);
  return classes.length > 0 && classes.every(cls => cls !== undefined && !isProtected(cls));
}

const kindsOf = line => (line.split('|')[2] ?? '').trim();
const wordsOf = cell => cell.toLowerCase().split(/[ ,]+/).filter(word => word !== '');

/** Why a kinds cell is not made of the allowed words (plain ASCII letters, spaces and commas), or null. */
function kindsProblem(cell, allowed) {
  if (!/^[A-Za-z, ]+$/.test(cell)) return 'holds a character that is not an ASCII letter, a comma or a space';
  const odd = wordsOf(cell).find(word => !allowed.has(word));
  return odd === undefined ? null : `holds the word \`${odd}\``;
}

const findHead = lines => lines.findIndex((line, index) => TABLE_HEADER.test(line) && TABLE_SEPARATOR.test(lines[index + 1] ?? ''));

/** The index after the contiguous `|` block that starts under the header row and its separator row. */
function blockEnd(lines, head) {
  let end = head + 2;
  while (end < lines.length && lines[end].startsWith('|')) end += 1;
  return end;
}

const fRows = (lines, head, end) => lines.flatMap((line, index) => (index >= head + 2 && index < end && F_ROW.test(line) ? [{ id: F_ROW.exec(line)[1], line, index }] : []));

/**
 * 2.1's table in the text of its part: the rows are the contiguous `|` block right under the `| Id |` header row and its separator row. `{ found, rows, rest }`: `rows` are
 * `{ id, line, index }` for the block's `| F-… |` lines, `rest` the other lines as `{ text, index }` (a `| F-` line anywhere else is one of them: plain text of its part).
 */
export function tableOf(text) {
  const lines = text.split('\n');
  const head = findHead(lines);
  const rows = head === -1 ? [] : fRows(lines, head, blockEnd(lines, head));
  const taken = new Set(rows.map(row => row.index));
  return { found: head !== -1, rows, rest: lines.flatMap((line, index) => (taken.has(index) ? [] : [{ text: line, index }])), count: lines.length };
}

/** The refusal lines for an F-id that has more than one row in one table. */
function duplicateRows(table, name, lineOf) {
  const ids = [...new Set(table.rows.map(row => row.id))];
  return ids.flatMap(id => {
    const same = table.rows.filter(row => row.id === id);
    return same.length < 2 ? [] : [name(`row ${id} has ${same.length} rows (${same.map(row => lineOf(row.index)).join(' and ')}): an F-id has one row`)];
  });
}

/** What the row checks share: both tables, the rows by id, the part, the places and the refusal maker. */
function rowContext(part, textA, textB, ctx, at) {
  const a = tableOf(textA);
  const b = tableOf(textB);
  return {
    a, b, ctx, part, at,
    rowA: new Map(a.rows.map(row => [row.id, row])),
    rowB: new Map(b.rows.map(row => [row.id, row])),
    name: (rule, where = here(at)) => refusal(labelOf(part), part.cls, where, rule),
  };
}

const duplicateRefusals = rc => [
  ...duplicateRows(rc.a, rule => rc.name(rule, there(rc.part.no)), index => there(rc.part.no + index)),
  ...duplicateRows(rc.b, rc.name, index => here(rc.at + index)),
];

function textDiffRefusals(rc) {
  const differ = firstDifferingText(rc.a.rest, rc.b.rest);
  const shown = differ === undefined ? 0 : (rc.b.rest[differ] ?? rc.b.rest.at(-1) ?? { index: 0 }).index;
  return differ === undefined ? [] : [rc.name(`protected bytes differ (first difference at candidate line ${rc.at + shown})`)];
}

function protectedRowRefusals(rc, ids) {
  return ids.flatMap(id => {
    if (!rc.rowB.has(id)) return [rc.name(`row ${id} is missing, and its sentence is defined in a protected part`)];
    return rc.rowB.get(id).line === rc.rowA.get(id).line ? [] : [rc.name(`row ${id} differs, and its sentence is defined in a protected part`)];
  });
}

function outOfOrderRefusals(rc, ids) {
  const kept = ids.filter(id => rc.rowB.has(id));
  const order = rc.b.rows.map(row => row.id).filter(id => kept.includes(id));
  const moved = order.findIndex((id, index) => id !== kept[index]);
  return moved === -1 ? [] : [rc.name(`the protected rows are out of order: ${order[moved]} stands where the frozen file has ${kept[moved]} (rows of protected sentences keep their order)`, here(rc.at + rc.rowB.get(order[moved]).index))];
}

const freeKindWords = rc => new Set(['daemon', 'web', 'and', 'or', ...[...rc.rowA.keys()].filter(id => isFree(rc.ctx.frozen, id)).flatMap(id => wordsOf(kindsOf(rc.rowA.get(id).line)))]);

/** A row of the candidate that is new or changed, and free: it may not name all or olympiad, and its kinds cell holds only the words the frozen free rows use. */
function changedRowRefusals(rc, id, row, known, kindWords) {
  const verb = known ? ' is changed' : ' is added';
  const where = here(rc.at + row.index);
  if (OLYMPIAD_KINDS.test(row.line.split('|')[2] ?? '')) return [rc.name(`row ${id}${verb} and its kinds cell names all or olympiad: a daemon-web change touches no olympiad task`, where)];
  const odd = kindsProblem(kindsOf(row.line), kindWords);
  return odd === null ? [] : [rc.name(`row ${id}${verb} and its kinds cell ${odd}: only the words daemon, web, and, or and the words the frozen free rows use, as plain ASCII, are allowed`, where)];
}

const isFreeRow = (rc, id, known) => isFree(known ? rc.ctx.frozen : rc.ctx.candidate, id);
const addedNotFree = (known, free) => !known && !free;
const unchangedOrLocked = (free, same) => !free || same;

function candidateRowRefusals(rc, id, row, kindWords) {
  const known = rc.rowA.has(id);
  const free = isFreeRow(rc, id, known);
  if (addedNotFree(known, free)) return [rc.name(`row ${id} is added, and its sentence is not defined in a daemon, web or daemon-web part`, here(rc.at + row.index))];
  if (unchangedOrLocked(free, rc.rowA.get(id)?.line === row.line)) return [];
  return changedRowRefusals(rc, id, row, known, kindWords);
}

/** The rows of 2.1's table: everything outside the rows must be equal; each row equal unless its sentence is defined only in non-protected parts; A's protected rows keep their order; a free or added row may not name `all` or `olympiad`; an F-id has one row. */
function rowRefusals(part, textA, textB, ctx, at) {
  const rc = rowContext(part, textA, textB, ctx, at);
  const duplicates = duplicateRefusals(rc);
  if (duplicates.length > 0) return duplicates;
  const ids = [...rc.rowA.keys()].filter(id => !isFree(ctx.frozen, id));
  const kindWords = freeKindWords(rc);
  return [...textDiffRefusals(rc), ...protectedRowRefusals(rc, ids), ...outOfOrderRefusals(rc, ids), ...[...rc.rowB].flatMap(([id, row]) => candidateRowRefusals(rc, id, row, kindWords))];
}

const levelOfPart = part => (part.kind === 'sub' ? 3 : 2);
const sameTitled = (part, candidate) => candidate.format.headings.filter(heading => heading.level === levelOfPart(part) && titleOf(heading.text) === titleOf(part.heading));

/** The number the title of `part` has in the candidate: in the same section, or, with `anySection`, anywhere; null when no heading of that level has the title. */
function movedTo(part, candidate, anySection) {
  const same = sameTitled(part, candidate);
  const near = same.find(heading => numberOf(heading).split('.')[0] === part.number.split('.')[0]) ?? (anySection ? same[0] : undefined);
  return near === undefined ? null : numberOf(near);
}

/** The refusals for the bytes appended to the changelog: after a blank line (the frozen bytes end with a line feed and the first appended line is empty), and every line one of the allowed forms. */
function appendedRefusals(part, textA, added, at) {
  if (added === '') return [];
  const lines = textA.split('\n').length - 1;
  if (!textA.endsWith('\n') || !added.startsWith('\n')) return [refusal(labelOf(part), part.cls, here(at + lines), 'the text appended to the changelog must start after a blank line (it may not continue the frozen last line or its list item)')];
  return formRefusals(added, at + lines).map(entry => refusal(labelOf(part), part.cls, here(entry.no), `appended to the changelog: ${entry.form}`));
}

function missingPartRefusals(part, ctx) {
  const moved = movedTo(part, ctx.candidate, true);
  return [refusal(labelOf(part), part.cls, there(part.no), moved === null ? 'missing: the protected part was deleted' : `heading number changed (now ${moved})`)];
}

const renumbered = (found, part, moved) => found.heading.text !== part.heading && moved !== null && moved !== part.number;
const isChangelog = part => part.number === '7' && part.kind === 'section';

function changelogRefusals(part, v) {
  if (!v.textB.startsWith(v.textA)) return [refusal(labelOf(part), part.cls, here(v.at), `${v.retitled}the changelog must start with the frozen bytes (first difference at candidate line ${firstDifference(v.textA, v.textB, v.at)})`)];
  return appendedRefusals(part, v.textA, v.textB.slice(v.textA.length), v.at);
}

function compareBodies(part, ctx, v) {
  if (isChangelog(part)) return changelogRefusals(part, v);
  if (tableOf(v.textA).found) return rowRefusals(part, v.textA, v.textB, ctx, v.at);
  return v.textA === v.textB ? [] : [refusal(labelOf(part), part.cls, here(v.at), `${v.retitled}protected bytes differ (first difference at candidate line ${firstDifference(v.textA, v.textB, v.at)})`)];
}

function foundPartRefusals(part, ctx, found) {
  const at = found.heading.no;
  const moved = movedTo(part, ctx.candidate, false);
  if (renumbered(found, part, moved)) return [refusal(labelOf(part), part.cls, here(at), `heading number changed (now ${moved})`)];
  const retitled = found.heading.text === part.heading ? '' : `heading text changed (now \`${found.heading.text}\`): `;
  return compareBodies(part, ctx, { at, retitled, textA: ctx.frozen.format.text.slice(part.start, part.end), textB: ctx.candidate.format.text.slice(found.start, found.end) });
}

/** The refusals for one protected part of the frozen file. */
function partRefusals(part, ctx) {
  const found = sliceLike(ctx.candidate.format, part);
  return found === null ? missingPartRefusals(part, ctx) : foundPartRefusals(part, ctx, found);
}

/** A protected class row of the frozen file that the candidate does not carry as it was. */
function rowClassRefusals(frozen, candidate) {
  return frozen.rows.filter(entry => isProtected(entry.cls) && candidate.format.headings.some(heading => heading.text === entry.heading)).flatMap(row => {
    const found = candidate.rows.find(entry => entry.heading === row.heading);
    if (found === undefined) return [refusal(row.heading, row.cls, there(row.no), 'class row missing in the candidate')];
    return found.cls === row.cls ? [] : [refusal(row.heading, row.cls, here(found.no), `class row differs (relabel): the candidate says ${found.cls}`)];
  });
}

/** The lead text of a section that was one unprotected whole may only be its `##` line and blank lines, or the bytes the frozen section had before its first subsection. */
function leadMayBe(frozen, candidate, part) {
  const text = candidate.format.text.slice(part.start, part.end);
  if (text.split('\n').slice(1).every(line => line === '')) return true;
  const was = sliceFrom(frozen.format, headingByNumber(frozen.format, part.number), 3);
  return frozen.format.text.slice(was.start, was.end) === text;
}

const isNewProtected = entry => isProtected(entry.cls) && entry.kind !== 'preamble';

function wasProtected(frozen, part) {
  const before = frozen.parts.find(entry => entry.key === part.key && entry.heading === part.heading);
  return before !== undefined && isProtected(before.cls);
}

function wasUnprotectedWhole(frozen, part) {
  const whole = frozen.parts.find(entry => entry.kind === 'section' && entry.number === part.number);
  return whole !== undefined && !isProtected(whole.cls);
}

const mayBeLead = (frozen, candidate, part) => part.kind === 'lead' && wasUnprotectedWhole(frozen, part) && leadMayBe(frozen, candidate, part);

function addedRefusal(frozen, part) {
  const same = frozen.parts.find(entry => entry.number === part.number && entry.kind === part.kind);
  return [refusal(labelOf(part), same?.cls ?? 'none', here(part.no), `added as ${part.cls}: a part classed common or olympiad is protected, and only the frozen file's own parts are`)];
}

/** A candidate part classed common or olympiad that is not one of the frozen file's own protected parts (the lead text of a section that was one unprotected whole is the one exception, within `leadMayBe`). */
const addedRefusals = (frozen, candidate) => candidate.parts.filter(isNewProtected)
  .flatMap(part => (wasProtected(frozen, part) || mayBeLead(frozen, candidate, part) ? [] : addedRefusal(frozen, part)));

const readBoth = (frozen, candidate) => ({ a: readClassed(frozen, 'the frozen file'), b: readClassed(candidate, 'the candidate') });

/** The refusal before any part is compared: the frozen file is not the reference, a file cannot be read, or the frozen file breaks its own class rules. */
function headRefusal(frozen, reference, { a, b }) {
  const actual = sha256Hex(frozen);
  if (actual !== reference) return { exit: 1, lines: [`format-classes: the frozen reference is not A's recorded FORMAT (its sha256 is ${actual}, the record names ${reference})`] };
  const unread = [a, b].find(read => !read.ok);
  if (unread !== undefined) return { exit: 4, lines: [`format-classes: ${unread.reason}`] };
  const own = checkSelf(frozen);
  return own.exit === 0 ? null : { exit: 4, lines: ['format-classes: the frozen file breaks its own class rules', ...own.lines] };
}

function classRefusal(b) {
  const problems = tableProblems(b.format, b.rows);
  return problems.length > 0 ? { exit: 1, lines: problems.map(problem => `format-classes: candidate: ${problem.heading}, line ${problem.no}: ${problem.rule}`) } : null;
}

function frozenGrammarRefusal(left) {
  const broken = grammarRefusals(left);
  return broken.length > 0 ? { exit: 4, lines: ['format-classes: the frozen file breaks the restricted grammar', ...broken.map(entry => refusal(entry.heading, entry.cls, there(entry.no), entry.form))] } : null;
}

/** Every refusal of the candidate against the frozen file: its grammar, each protected part, the class rows, the added parts. */
function candidateRefusals(left, right) {
  const ctx = { frozen: left, candidate: right };
  return [
    ...grammarRefusals(right, left).map(entry => refusal(entry.heading, entry.cls, here(entry.no), entry.form)),
    ...left.parts.filter(part => isProtected(part.cls)).flatMap(part => (part.kind === 'preamble' ? preambleRefusals(left, right) : partRefusals(part, ctx))),
    ...rowClassRefusals(left, right), ...addedRefusals(left, right),
  ];
}

/**
 * `{ exit, lines }` for the frozen file's bytes, the candidate's bytes and the reference sha256 (the record's `A.sha256`): 1 when the frozen file is not the reference or the candidate
 * breaks the class rules or changes a protected part (one line per refusal), 4 for a format error in either file, else 0.
 */
export function checkFrozenCandidate({ frozen, candidate, reference }) {
  const both = readBoth(frozen, candidate);
  const early = headRefusal(frozen, reference, both) ?? classRefusal(both.b);
  if (early !== null) return early;
  const left = { ...both.a, parts: partsOf(both.a.format, both.a.rows) };
  const broken = frozenGrammarRefusal(left);
  if (broken !== null) return broken;
  const lines = candidateRefusals(left, { ...both.b, parts: partsOf(both.b.format, both.b.rows) });
  return lines.length === 0 ? { exit: 0, lines: ['format-classes: the protected parts are unchanged'] } : { exit: 1, lines };
}
