// The frozen file against a candidate (E1.S9, T-L56): after the freeze of the first group of tasks the `common` and `olympiad` parts of FORMAT do not change, and a
// `daemon`, `web` or `daemon-web` part may. The protected set comes from the FROZEN file's class table only. Parts are compared as raw bytes; a boundary blank line belongs to the
// part before it. Exceptions, each named in the rule: the preamble's three header rows and class-table rows appended after the last row; section 7 (the changelog) may only grow;
// 2.1's fixed-sentence table is compared row by row.
import { checkSelf, isProtected, partsOf, readClassed, sliceLike, tableProblems } from './format-class-table.mjs';
import { numberOf, sha256Hex, unfencedLines } from './format-structure.mjs';

const HEADER_LABELS = Object.freeze(['Format version', 'Gate verdict', 'Freeze tag']);
const HEADER_ROW = new RegExp(`^(${HEADER_LABELS.join('|')}): `);
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

/** The place of the first class-table row of `rowsB` that breaks the rule, or -1: A's protected rows stay as they were and in order, A's other rows may change or go, new rows come after A's last. */
function firstBadRow(rowsA, rowsB) {
  let at = 0;
  for (const row of rowsA) {
    if (rowsB[at]?.raw === row.raw) at += 1;
    else if (isProtected(row.cls)) return at;
    else if (rowsB[at]?.heading === row.heading) at += 1;
  }
  return -1;
}

/** The preamble without the three header rows and the table's data rows must be equal; the data rows follow `firstBadRow`'s rule. */
function preambleRefusals(frozen, candidate) {
  const plain = side => {
    const dropped = new Set(side.rows.map(row => row.no));
    return side.format.text.slice(0, side.format.headings[0].start).split('\n').map((text, index) => ({ text, no: index + 1 })).filter(line => !dropped.has(line.no) && !HEADER_ROW.test(line.text));
  };
  const b = candidate.format.text.slice(0, candidate.format.headings[0].start).split('\n');
  const refusals = HEADER_LABELS.filter(label => b.filter(text => text.startsWith(`${label}: `)).length !== 1)
    .map(label => refusal('preamble', 'common', here(1), `the header row \`${label}\` is missing or appears more than once`));
  const plainA = plain(frozen);
  const plainB = plain(candidate);
  const differs = (line, index) => plainB[index]?.text !== line.text;
  const text = plainA.length === plainB.length && !plainA.some(differs) ? null : (plainB[Math.max(0, plainA.findIndex(differs))] ?? plainB.at(-1)).no;
  const row = firstBadRow(frozen.rows, candidate.rows);
  const bad = text ?? (row === -1 ? null : (candidate.rows[row] ?? candidate.rows.at(-1)).no);
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

/** 2.1's table: the lines that are no row must be equal, and each row equal unless its sentence is defined only in non-protected parts. */
function rowRefusals(part, textA, textB, ctx, at) {
  const split = text => ({ rows: new Map(text.split('\n').filter(line => F_ROW.test(line)).map(line => [F_ROW.exec(line)[1], line])), rest: text.split('\n').filter(line => !F_ROW.test(line)).join('\n') });
  const a = split(textA);
  const b = split(textB);
  const name = rule => refusal(labelOf(part), part.cls, here(at), rule);
  const refusals = a.rest === b.rest ? [] : [name(`protected bytes differ (first difference at candidate line ${firstDifference(a.rest, b.rest, at)})`)];
  for (const [id, line] of a.rows) {
    if (b.rows.get(id) !== line && !isFree(ctx.frozen, id)) refusals.push(name(`row ${id} ${b.rows.has(id) ? 'differs' : 'is missing'}, and its sentence is defined in a protected part`));
  }
  for (const id of [...b.rows.keys()].filter(key => !a.rows.has(key))) {
    if (!isFree(ctx.candidate, id)) refusals.push(name(`row ${id} is added, and its sentence is not defined in a daemon, web or daemon-web part`));
  }
  return refusals;
}

/** The number the title of `part` has in the candidate, inside the same section; null when no heading of that level there has the title. */
function movedTo(part, candidate) {
  const near = candidate.format.headings.find(heading => heading.level === (part.kind === 'sub' ? 3 : 2) && titleOf(heading.text) === titleOf(part.heading) && numberOf(heading).split('.')[0] === part.number.split('.')[0]);
  return near === undefined ? null : numberOf(near);
}

/** The refusals for one protected part of the frozen file. */
function partRefusals(part, ctx) {
  const { frozen, candidate } = ctx;
  const found = sliceLike(candidate.format, part);
  const moved = movedTo(part, candidate);
  if (found === null) return [refusal(labelOf(part), part.cls, there(part.no), moved === null ? 'missing: the protected part was deleted' : `heading number changed (now ${moved})`)];
  const at = found.heading === null ? 1 : found.heading.no;
  if (found.heading.text !== part.heading && moved !== null && moved !== part.number) return [refusal(labelOf(part), part.cls, here(at), `heading number changed (now ${moved})`)];
  const textA = frozen.format.text.slice(part.start, part.end);
  const textB = candidate.format.text.slice(found.start, found.end);
  if (part.number === '7' && part.kind === 'section') {
    return textB.startsWith(textA) ? [] : [refusal(labelOf(part), part.cls, here(at), `the changelog must start with the frozen bytes (first difference at candidate line ${firstDifference(textA, textB, at)})`)];
  }
  if (/^\| F-/m.test(textA)) return rowRefusals(part, textA, textB, ctx, at);
  return textA === textB ? [] : [refusal(labelOf(part), part.cls, here(at), `protected bytes differ (first difference at candidate line ${firstDifference(textA, textB, at)})`)];
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

/** A candidate part classed common or olympiad that is not one of the frozen file's own protected parts (the lead text of a section that was one daemon-web whole is the one exception). */
function addedRefusals(frozen, candidate) {
  const refusals = [];
  for (const part of candidate.parts.filter(entry => isProtected(entry.cls) && entry.kind !== 'preamble')) {
    const before = frozen.parts.find(entry => entry.key === part.key && entry.heading === part.heading);
    const whole = frozen.parts.find(entry => entry.kind === 'section' && entry.number === part.number);
    if ((before !== undefined && isProtected(before.cls)) || (part.kind === 'lead' && whole !== undefined && !isProtected(whole.cls))) continue;
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
