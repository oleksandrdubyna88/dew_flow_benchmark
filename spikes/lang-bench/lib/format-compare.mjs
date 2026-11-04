// The frozen file against a candidate (E1.S9, T-L56): after the freeze of the first group of tasks the `common` and `olympiad` parts of FORMAT do not change, and a
// `daemon`, `web` or `daemon-web` part may. The protected set comes from the FROZEN file's class table only. Parts are compared as raw bytes; a boundary blank line belongs to the
// part before it. Exceptions, each named in the rule: the preamble's three header rows and class-table rows appended after the last row; section 7 (the changelog) may only grow;
// 2.1's fixed-sentence table is compared row by row. The header rows keep their lines and match a strict pattern; the rest of the preamble is compared as bytes.
import { checkSelf, isProtected, partsOf, readClassed, sliceLike, tableProblems } from './format-class-table.mjs';
import { headingByNumber, numberOf, sha256Hex, sliceFrom, unfencedLines } from './format-structure.mjs';

const HEADER_LABELS = Object.freeze(['Format version', 'Gate verdict', 'Freeze tag']);
const HEADER_ROW = new RegExp(`^(${HEADER_LABELS.join('|')}): `);
/** The value of each header row: a version number; a verdict of one to four lower-case words (letters, digits, `_`, `.`, `-`; no colon; at most 80 characters); `none yet` or a freeze tag. */
export const HEADER_PATTERNS = Object.freeze({
  'Format version': /^\d+(?:\.\d+)?$/,
  'Gate verdict': /^(?=.{1,80}$)[a-z0-9][a-z0-9_.-]*(?: [a-z0-9][a-z0-9_.-]*){0,3}$/,
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

/** The place of the first class-table row of the candidate that breaks the rule, or -1: A's protected rows stay byte for byte and in order, A's other rows may change, be retitled (the row names the same heading number) or go, new rows come after A's last. */
function firstBadRow(frozen, candidate) {
  let at = 0;
  for (const row of frozen.rows) {
    const next = candidate.rows[at];
    if (next?.raw === row.raw) at += 1;
    else if (isProtected(row.cls)) return at;
    else if (next !== undefined && numberOfRow(candidate.format, next) !== null && numberOfRow(candidate.format, next) === numberOfRow(frozen.format, row)) at += 1;
  }
  return -1;
}

const preambleOf = side => side.format.text.slice(0, side.format.headings[0].start).split('\n');

/** The refusals for the three header rows of the candidate: one row each, on the line the frozen file has it, with a value that matches `HEADER_PATTERNS`. */
function headerRefusals(frozen, candidate) {
  const a = preambleOf(frozen);
  const b = preambleOf(candidate);
  const refusals = [];
  for (const label of HEADER_LABELS) {
    const rows = b.flatMap((text, index) => (text.startsWith(`${label}: `) ? [index + 1] : []));
    const was = a.findIndex(text => text.startsWith(`${label}: `)) + 1;
    const fail = (where, rule) => refusals.push(refusal('preamble', 'common', here(where), rule));
    if (rows.length !== 1) fail(1, `the header row \`${label}\` is missing or appears more than once`);
    else if (was > 0 && rows[0] !== was) fail(rows[0], `the header row \`${label}\` is at line ${rows[0]}, the frozen file has it at line ${was}: the header rows keep their lines`);
    else if (!HEADER_PATTERNS[label].test(b[rows[0] - 1].slice(label.length + 2))) fail(rows[0], `the value of the header row \`${label}\` does not match the pattern ${HEADER_PATTERNS[label]}`);
  }
  return refusals;
}

/** The preamble without the three header rows and the table's data rows must be equal; the data rows follow `firstBadRow`'s rule; the header rows follow `headerRefusals`. */
function preambleRefusals(frozen, candidate) {
  const plain = side => {
    const dropped = new Set(side.rows.map(row => row.no));
    return preambleOf(side).map((text, index) => ({ text, no: index + 1 })).filter(line => !dropped.has(line.no) && !HEADER_ROW.test(line.text));
  };
  const refusals = headerRefusals(frozen, candidate);
  const plainA = plain(frozen);
  const plainB = plain(candidate);
  const first = Array.from({ length: Math.max(plainA.length, plainB.length) }, (_, index) => index).find(index => plainA[index]?.text !== plainB[index]?.text);
  const text = first === undefined ? null : (plainB[first] ?? plainB.at(-1) ?? { no: 1 }).no;
  const row = firstBadRow(frozen, candidate);
  const bad = text ?? (row === -1 ? null : (candidate.rows[row] ?? candidate.rows.at(-1) ?? { no: 1 }).no);
  return bad === null ? refusals : [...refusals, refusal('preamble', 'common', here(1), `protected bytes differ (first difference at candidate line ${bad})`)];
}

/** The paragraphs outside fences that are followed, after blank lines, by a fence line (an indented one too): the text that introduces a sentence (`{ start, text }`). */
function introductions(format) {
  const open = new Set(unfencedLines(format.lines).map(line => line.no));
  const found = [];
  let paragraph = [];
  for (const [index, line] of format.lines.entries()) {
    if (open.has(line.no) && line.text !== '') {
      paragraph.push(line);
      continue;
    }
    const next = paragraph.length > 0 ? format.lines.slice(index).find(other => other.text.trim() !== '') : undefined;
    if (next !== undefined && FENCE_LINE.test(next.text)) found.push({ start: paragraph[0].start, text: paragraph.map(entry => entry.text).join('\n') });
    paragraph = [];
  }
  return found;
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

/**
 * 2.1's table in the text of its part: the rows are the contiguous `|` block right under the `| Id |` header row and its separator row. `{ found, rows, rest }`: `rows` are
 * `{ id, line, index }` for the block's `| F-… |` lines, `rest` the other lines as `{ text, index }` (a `| F-` line anywhere else is one of them: plain text of its part).
 */
function tableOf(text) {
  const lines = text.split('\n');
  const head = lines.findIndex((line, index) => TABLE_HEADER.test(line) && TABLE_SEPARATOR.test(lines[index + 1] ?? ''));
  let end = head === -1 ? 0 : head + 2;
  while (head !== -1 && end < lines.length && lines[end].startsWith('|')) end += 1;
  const rows = lines.flatMap((line, index) => (head !== -1 && index >= head + 2 && index < end && F_ROW.test(line) ? [{ id: F_ROW.exec(line)[1], line, index }] : []));
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

/** The rows of 2.1's table: everything outside the rows must be equal; each row equal unless its sentence is defined only in non-protected parts; A's protected rows keep their order; a free or added row may not name `all` or `olympiad`; an F-id has one row. */
function rowRefusals(part, textA, textB, ctx, at) {
  const a = tableOf(textA);
  const b = tableOf(textB);
  const name = (rule, where = here(at)) => refusal(labelOf(part), part.cls, where, rule);
  const duplicates = [...duplicateRows(a, rule => name(rule, there(part.no)), index => there(part.no + index)), ...duplicateRows(b, name, index => here(at + index))];
  if (duplicates.length > 0) return duplicates;
  const rowA = new Map(a.rows.map(row => [row.id, row]));
  const rowB = new Map(b.rows.map(row => [row.id, row]));
  const differ = Array.from({ length: Math.max(a.rest.length, b.rest.length) }, (_, index) => index).find(index => a.rest[index]?.text !== b.rest[index]?.text);
  const refusals = differ === undefined ? [] : [name(`protected bytes differ (first difference at candidate line ${at + (b.rest[differ] ?? b.rest.at(-1) ?? { index: 0 }).index})`)];
  const protectedIds = [];
  for (const [id, row] of rowA) {
    if (isFree(ctx.frozen, id)) continue;
    protectedIds.push(id);
    if (!rowB.has(id)) refusals.push(name(`row ${id} is missing, and its sentence is defined in a protected part`));
    else if (rowB.get(id).line !== row.line) refusals.push(name(`row ${id} differs, and its sentence is defined in a protected part`));
  }
  const kept = protectedIds.filter(id => rowB.has(id));
  const order = b.rows.map(row => row.id).filter(id => kept.includes(id));
  const moved = order.findIndex((id, index) => id !== kept[index]);
  if (moved !== -1) refusals.push(name(`the protected rows are out of order: ${order[moved]} stands where the frozen file has ${kept[moved]} (rows of protected sentences keep their order)`, here(at + rowB.get(order[moved]).index)));
  for (const [id, row] of rowB) {
    const free = isFree(rowA.has(id) ? ctx.frozen : ctx.candidate, id);
    if (!rowA.has(id) && !free) refusals.push(name(`row ${id} is added, and its sentence is not defined in a daemon, web or daemon-web part`, here(at + row.index)));
    else if (free && rowA.get(id)?.line !== row.line && OLYMPIAD_KINDS.test(row.line.split('|')[2] ?? '')) {
      refusals.push(name(`row ${id}${rowA.has(id) ? ' is changed' : ' is added'} and its kinds cell names all or olympiad: a daemon-web change touches no olympiad task`, here(at + row.index)));
    }
  }
  return refusals;
}

/** The number the title of `part` has in the candidate: in the same section, or, with `anySection`, anywhere; null when no heading of that level has the title. */
function movedTo(part, candidate, anySection) {
  const same = candidate.format.headings.filter(heading => heading.level === (part.kind === 'sub' ? 3 : 2) && titleOf(heading.text) === titleOf(part.heading));
  const near = same.find(heading => numberOf(heading).split('.')[0] === part.number.split('.')[0]) ?? (anySection ? same[0] : undefined);
  return near === undefined ? null : numberOf(near);
}

/** The refusals for one protected part of the frozen file. */
function partRefusals(part, ctx) {
  const { frozen, candidate } = ctx;
  const found = sliceLike(candidate.format, part);
  if (found === null) {
    const moved = movedTo(part, candidate, true);
    return [refusal(labelOf(part), part.cls, there(part.no), moved === null ? 'missing: the protected part was deleted' : `heading number changed (now ${moved})`)];
  }
  const at = found.heading.no;
  const moved = movedTo(part, candidate, false);
  if (found.heading.text !== part.heading && moved !== null && moved !== part.number) return [refusal(labelOf(part), part.cls, here(at), `heading number changed (now ${moved})`)];
  const retitled = found.heading.text === part.heading ? '' : `heading text changed (now \`${found.heading.text}\`): `;
  const textA = frozen.format.text.slice(part.start, part.end);
  const textB = candidate.format.text.slice(found.start, found.end);
  if (part.number === '7' && part.kind === 'section') {
    return textB.startsWith(textA) ? [] : [refusal(labelOf(part), part.cls, here(at), `${retitled}the changelog must start with the frozen bytes (first difference at candidate line ${firstDifference(textA, textB, at)})`)];
  }
  if (tableOf(textA).found) return rowRefusals(part, textA, textB, ctx, at);
  return textA === textB ? [] : [refusal(labelOf(part), part.cls, here(at), `${retitled}protected bytes differ (first difference at candidate line ${firstDifference(textA, textB, at)})`)];
}

/** A protected class row of the frozen file that the candidate does not carry as it was. */
function rowClassRefusals(frozen, candidate) {
  const refusals = [];
  for (const row of frozen.rows.filter(entry => isProtected(entry.cls) && candidate.format.headings.some(heading => heading.text === entry.heading))) {
    const found = candidate.rows.find(entry => entry.heading === row.heading);
    if (found === undefined) refusals.push(refusal(row.heading, row.cls, there(row.no), 'class row missing in the candidate'));
    else if (found.cls !== row.cls) refusals.push(refusal(row.heading, row.cls, here(found.no), `class row differs (relabel): the candidate says ${found.cls}`));
  }
  return refusals;
}

/** The lead text of a section that was one unprotected whole may only be its `##` line and blank lines, or the bytes the frozen section had before its first subsection. */
function leadMayBe(frozen, candidate, part) {
  const text = candidate.format.text.slice(part.start, part.end);
  if (text.split('\n').slice(1).every(line => line === '')) return true;
  const was = sliceFrom(frozen.format, headingByNumber(frozen.format, part.number), 3);
  return frozen.format.text.slice(was.start, was.end) === text;
}

/** A candidate part classed common or olympiad that is not one of the frozen file's own protected parts (the lead text of a section that was one unprotected whole is the one exception, within `leadMayBe`). */
function addedRefusals(frozen, candidate) {
  const refusals = [];
  for (const part of candidate.parts.filter(entry => isProtected(entry.cls) && entry.kind !== 'preamble')) {
    const before = frozen.parts.find(entry => entry.key === part.key && entry.heading === part.heading);
    const whole = frozen.parts.find(entry => entry.kind === 'section' && entry.number === part.number);
    if ((before !== undefined && isProtected(before.cls)) || (part.kind === 'lead' && whole !== undefined && !isProtected(whole.cls) && leadMayBe(frozen, candidate, part))) continue;
    const same = frozen.parts.find(entry => entry.number === part.number && entry.kind === part.kind);
    refusals.push(refusal(labelOf(part), same?.cls ?? 'none', here(part.no), `added as ${part.cls}: a part classed common or olympiad is protected, and only the frozen file's own parts are`));
  }
  return refusals;
}

/**
 * `{ exit, lines }` for the frozen file's bytes, the candidate's bytes and the reference sha256 (the record's `A.sha256`): 1 when the frozen file is not the reference or the candidate
 * breaks the class rules or changes a protected part (one line per refusal), 4 for a format error in either file, else 0.
 */
export function checkFrozenCandidate({ frozen, candidate, reference }) {
  const actual = sha256Hex(frozen);
  if (actual !== reference) return { exit: 1, lines: [`format-classes: the frozen reference is not A's recorded FORMAT (its sha256 is ${actual}, the record names ${reference})`] };
  const a = readClassed(frozen, 'the frozen file');
  const b = readClassed(candidate, 'the candidate');
  const unread = [a, b].find(read => !read.ok);
  if (unread !== undefined) return { exit: 4, lines: [`format-classes: ${unread.reason}`] };
  const own = checkSelf(frozen);
  if (own.exit !== 0) return { exit: 4, lines: ['format-classes: the frozen file breaks its own class rules', ...own.lines] };
  const problems = tableProblems(b.format, b.rows);
  if (problems.length > 0) return { exit: 1, lines: problems.map(problem => `format-classes: candidate: ${problem.heading}, line ${problem.no}: ${problem.rule}`) };
  const left = { ...a, parts: partsOf(a.format, a.rows) };
  const right = { ...b, parts: partsOf(b.format, b.rows) };
  const ctx = { frozen: left, candidate: right };
  const lines = [
    ...left.parts.filter(part => isProtected(part.cls)).flatMap(part => (part.kind === 'preamble' ? preambleRefusals(left, right) : partRefusals(part, ctx))),
    ...rowClassRefusals(left, right), ...addedRefusals(left, right),
  ];
  return lines.length === 0 ? { exit: 0, lines: ['format-classes: the protected parts are unchanged'] } : { exit: 1, lines };
}
