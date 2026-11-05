// The restricted grammar of the unprotected parts of FORMAT.md (E1.S9, fix batch 2), and two rules for the whole file. Patching the reader one CommonMark form at a time cannot close
// the class "the reader and a renderer read a candidate differently" (a deletion can turn a literal inside a fence into a heading; a reference definition acts at a distance), so
// the guarantee is by construction: a candidate's unprotected parts may only be made of the line forms below, and no line of any part may start a reference definition or an HTML block.
// The argument: every protected part starts at a column-0 heading, where CommonMark starts a fresh block; no unprotected construct can stay open across a part boundary (a fence
// closes inside its part, there is no HTML block, no list continues across a heading); and nothing acts at a distance (no reference definition, no raw inline HTML, fix batch 4): so
// a renderer shows the protected bytes as it shows them in the frozen file. This defends a HUMAN reviewer who reads a rendered page against a misleading amendment. Agents read FORMAT
// and GRAMMAR.md as raw bytes; the byte comparison protects the bytes of the protected parts (with its listed exceptions) and does not judge what an unprotected part says: a free part can
// still contradict protected text in plain words (`Notwithstanding 2.6, any normalisation is allowed for olympiad tasks.` in 2.5 passes), a point for human review of any amendment.
// It was checked against these forms with markdown-it in CommonMark mode and is not a proof about every renderer.
import { isProtected, partsOf, readClassed } from './format-class-table.mjs';
import { SECTION_COUNT, unfencedLines } from './format-structure.mjs';

const SECTION_HEADING = /^## \d+\. \S(?:.*\S)?$/;
const SUB_HEADING = /^### \d+\.\d+ \S(?:.*\S)?$/;
const FENCE_OPEN = /^```[a-z]*$/;
const FENCE_LINE = /^ {0,3}(?:`{3,}|~{3,})/;
const LIST_ITEM = /^(- |\d{1,3}\. )(.*)$/;
const TABLE_ROW = /^\|.*\|$/;
const REFERENCE_DEFINITION = /^\s*\[(?:[^\]\\]|\\.)*\]:/;
const BLOCK_STARTERS = '#<>|~=-+_]';
const ORDERED_NOT_ONE = 'ordered list item whose number is not 1 directly after a paragraph line (CommonMark reads it as text of that paragraph, so a code span can pair across it)';

const shown = text => (text.length > 50 ? `${text.slice(0, 50)}…` : text);

/** How a paragraph line (or the text of a list item) may not start, as a phrase after "that", or null: it must not start a block of any kind, in either reading (the first rule that holds names it). */
const START_RULES = Object.freeze([
  (text, first) => (first === undefined ? 'is empty' : null),
  (text, first) => (first === ' ' || first === '\t' ? 'starts with a space or a tab' : null),
  (text, first) => (/^[-+*]\t/.test(text) ? 'has a tab after a list marker (a renderer reads a list item here)' : null),
  (text, first) => (BLOCK_STARTERS.includes(first) ? `starts with \`${first}\` (a block starter)` : null),
  (text, first) => (text.startsWith('```') ? 'starts with three backticks (a fence has its own form: an opener of three backticks and lower-case letters, a closer of three backticks, both at column 0)' : null),
  (text, first) => (first === '*' && (!text.startsWith('**') || text.startsWith('***')) ? 'starts with `*` that is not `**` (a list marker or a thematic break)' : null),
  (text, first) => (/^\d{1,9}[.)](?: |\t|$)/.test(text) ? 'starts with an ordered list marker' : null),
  (text, first) => (first === '[' && !/^\[[^\]\\]*\](?!:)/.test(text) ? 'starts with `[` and may start a reference definition' : null),
]);

export const paragraphStartProblem = text => START_RULES.reduce((found, rule) => found ?? rule(text, text[0]), null);

const refuse = (state, no, line, form) => ({ ...state, refusals: [...state.refusals, { no, form: `${form}: ${shown(line)}` }] });
const RESET = Object.freeze({ item: null, last: null });

function inFence(state, line, no) {
  if (state.fenceAt === null) return null;
  if (line === '```') return { ...state, fenceAt: null };
  return FENCE_LINE.test(line) ? refuse(state, no, line, 'a fence marker line inside a fenced block that is not its closer (the block is closed by a line of exactly three backticks)') : state;
}

const blankLine = (state, line) => (line === '' ? { ...state, last: null } : null);

function headingLine(state, line, no) {
  if (!SECTION_HEADING.test(line) && !SUB_HEADING.test(line)) return null;
  return { ...(/ #+$/.test(line) ? refuse(state, no, line, 'a heading with a closing sequence of `#`') : state), ...RESET };
}

const fenceOpener = (state, line, no) => (FENCE_OPEN.test(line) ? { ...state, fenceAt: no, ...RESET } : null);

function tableRow(state, line, no) {
  if (!TABLE_ROW.test(line)) return null;
  return { ...(/\|\s*\[[^\]]+\]:/.test(line) ? refuse(state, no, line, 'a table row that holds a reference definition') : state), ...RESET };
}

const interruptsParagraph = (state, listed, ordered) => ordered && Number(listed[1].slice(0, -2)) !== 1 && (state.last === 'paragraph' || state.last === 'bullet');

/** The state after the refusal a list item earns, if any: an ordered number that is not 1 right after a paragraph line, or text that starts a block. */
function listRefusal(state, listed, line, no) {
  if (interruptsParagraph(state, listed, listed[1] !== '- ')) return refuse(state, no, line, `an ${ORDERED_NOT_ONE}`);
  const problem = paragraphStartProblem(listed[2]);
  return problem === null ? state : refuse(state, no, line, `a list item whose text ${problem}`);
}

function listItem(state, line, no) {
  const listed = LIST_ITEM.exec(line);
  if (listed === null) return null;
  const kind = listed[1] === '- ' ? 'bullet' : 'ordered';
  return { ...listRefusal(state, listed, line, no), item: listed[1].length, kind, last: kind };
}

const continues = (state, line) => state.item !== null && line.startsWith(' '.repeat(state.item));
const strayIndent = state => (state.item === null ? 'an indented line, and no list item is open (an indent is only the continuation of a list item)' : `an indented line that is not a continuation of the open list item (a continuation is indented by exactly ${state.item} spaces)`);

function continuationLine(state, line, no) {
  const problem = paragraphStartProblem(line.slice(state.item));
  return { ...(problem === null ? state : refuse(state, no, line, `a continuation line whose text ${problem}`)), last: state.kind };
}

function indentedLine(state, line, no) {
  if (!line.startsWith(' ')) return null;
  return continues(state, line) ? continuationLine(state, line, no) : refuse(state, no, line, strayIndent(state));
}

function paragraphLine(state, line, no) {
  const problem = paragraphStartProblem(line);
  return { ...(problem === null ? state : refuse(state, no, line, `a paragraph line that ${problem}`)), item: null, last: 'paragraph' };
}

/** The line forms in the order they are tried: the first that applies reads the line; a paragraph line applies to any. */
const LINE_FORMS_IN_ORDER = Object.freeze([inFence, blankLine, headingLine, fenceOpener, tableRow, listItem, indentedLine, paragraphLine]);

const lineStep = firstLine => (state, line, index) => LINE_FORMS_IN_ORDER.reduce((next, form) => next ?? form(state, line, firstLine + index), null);

/**
 * The refusals in the text of one unprotected part: `{ no, form }` per line that is not one of the allowed forms: a blank line; a heading in the exact form (`## k. Title`, `### k.n Title`);
 * a paragraph line; a list item (`- ` or one to three digits and `. `, then text that is a paragraph line; its continuation lines indented by exactly the marker width, the same rule
 * for the text; no nested list, no fence or heading in an item); a table row (`|` … `|`); a fenced block (an opener at column 0 that is `` ``` `` or `` ``` `` and lower-case letters, a
 * closer `` ``` `` at column 0, both in the part; no line of the block that is a fence marker line (up to three spaces of indent) but the closer).
 */
export function partRefusals(text, firstLine) {
  const end = text.split('\n').reduce(lineStep(firstLine), { refusals: [], fenceAt: null, item: null, kind: null, last: null });
  return end.fenceAt === null ? end.refusals : [...end.refusals, { no: end.fenceAt, form: 'a fenced block that is not closed in its part' }];
}

const ESCAPABLE = /[!-/:-@[-`{-~]/;
const TAG_START = /[A-Za-z/!?]/;
const BLOCK_START = /^ {0,3}(?:#{1,6}(?:[ \t]|$)|>|(?:[-+*]|\d{1,9}[.)])(?:[ \t]|$)|([-*_])(?: *\1){2,} *$)/;
const TABLE_LINE = /^ {0,3}\|/;
const HEADING_LINE = /^ {0,3}#{1,6}(?:[ \t]|$)/;

/** The offset just after the run of backticks that starts at `at` (a loop: a run or a paragraph of thousands of them must not grow the stack). */
function runEnd(text, at) {
  let end = at;
  while (text[end] === '`') end += 1;
  return end;
}

/** Where a run of exactly `length` backticks starts at or after `from` (CommonMark's closer of a code span), or -1. */
function closerOf(text, from, length) {
  for (let at = text.indexOf('`', from); at !== -1;) {
    const end = runEnd(text, at);
    if (end - at === length) return at;
    at = text.indexOf('`', end);
  }
  return -1;
}

const isEscape = (text, at) => text[at] === '\\' && ESCAPABLE.test(text[at + 1] ?? '');
const opensTag = (text, at) => text[at] === '<' && TAG_START.test(text[at + 1] ?? '');

/** Where the scan goes on after the code span that starts at `at`: past its closer, or past the run of backticks when it has none. */
function afterSpan(text, at) {
  const end = runEnd(text, at);
  const close = closerOf(text, end, end - at);
  return close === -1 ? end : close + (end - at);
}

/** One step of the scan at `at`: `{ next, open }` (an escape is skipped, a code span is skipped whole, a `<` before a tag character is an opening). */
function scanStep(text, at) {
  if (isEscape(text, at)) return { next: at + 2, open: false };
  if (text[at] === '`') return { next: afterSpan(text, at), open: false };
  return { next: at + 1, open: opensTag(text, at) };
}

function* openingsOf(text) {
  for (let at = 0; at < text.length;) {
    const step = scanStep(text, at);
    if (step.open) yield at;
    at = step.next;
  }
}

/** The offsets of every `<` followed by a letter, `/`, `!` or `?` outside a code span and not escaped, in `text` (CommonMark: a span ends at the next run of exactly as many backticks; a run with no closer is text). */
export const htmlOpenings = text => [...openingsOf(text)];

/**
 * The unfenced lines grouped as a renderer reads them: `{ lines, row }`. A paragraph (a list item with its continuation lines) is one group; a heading, a list marker line, a quote, a
 * thematic break, a blank line or a gap (a fenced block) ends one. A table extension is not certain to be there, so there are two readings: with `rows` false the lines of a table are
 * text of one paragraph; with `rows` true a line that starts with a pipe, and every line after it up to a blank line or a block start, is a row on its own (GFM reads each row alone).
 */
const isRowLine = (rows, starts, line, table) => rows && !starts && (TABLE_LINE.test(line.text) || table);
const adjoins = (last, line) => last.lines.length > 0 && last.lines.at(-1).no === line.no - 1 && !HEADING_LINE.test(last.lines.at(-1).text);
const joinsGroup = (starts, row, last, line) => !starts && !row && adjoins(last, line);
const addToGroups = (groups, last, line, row, joins) => (joins ? [...groups.slice(0, -1), { lines: [...last.lines, line], row: false }] : [...groups, { lines: [line], row }]);

const groupStep = rows => ({ groups, table }, line) => {
  if (line.text.trim() === '') return { groups, table: false };
  const last = groups.at(-1) ?? { lines: [] };
  const starts = BLOCK_START.test(line.text);
  const row = isRowLine(rows, starts, line, table);
  return { groups: addToGroups(groups, last, line, row, joinsGroup(starts, row, last, line)), table: row };
};

function inlineGroups(lines, rows) {
  return lines.reduce(groupStep(rows), { groups: [], table: false }).groups;
}

/** The cells of a table row, cut at every pipe that no backslash escapes (a backslash escapes the character after it, so `\\|` is a cut: the cut is the safe reading, a span never crosses it). */
function cellsOf(row) {
  const masked = row.replace(/\\[^]/g, pair => '\0'.repeat(pair.length));
  return masked.split('|').reduce(({ cells, at }, part) => ({ cells: [...cells, row.slice(at, at + part.length)], at: at + part.length + 1 }), { cells: [], at: 0 }).cells;
}

/** The line numbers of the openings of one group: a row cell by cell (a span never crosses a cell), a paragraph as one text. */
function groupOpenings(group) {
  if (group.row) return cellsOf(group.lines[0].text).flatMap(cell => htmlOpenings(cell)).map(() => group.lines[0].no);
  const starts = group.lines.reduce((offsets, line, index) => [...offsets, index === 0 ? 0 : offsets[index - 1] + group.lines[index - 1].text.length + 1], []);
  return htmlOpenings(group.lines.map(line => line.text).join('\n')).map(at => group.lines[starts.findLastIndex(start => start <= at)].no);
}

const INLINE_HTML = 'raw inline HTML (a `<` followed by a letter, `/`, `!` or `?` outside a code span and not written `\\<`; a renderer may let it open an element that swallows the text after it)';

const LINE_FORMS = Object.freeze([
  [line => REFERENCE_DEFINITION.test(line.text), line => `a link reference definition (nothing may act at a distance): ${shown(line.text)}`],
  [line => TABLE_LINE.test(line.text) && line.text.includes('\\\\|'), line => `a table row that holds \`\\\\|\` (renderers disagree on whether the pair of backslashes escapes the pipe): ${shown(line.text)}`],
  [line => /^\s*</.test(line.text), line => `a line whose first non-space character is \`<\` (an HTML block start): ${shown(line.text)}`],
]);

/** The refusals of the whole-file rules that read one line alone: a reference definition, a table row with an escaped pipe pair, a line that starts with `<`. */
const lineRefusals = kept => kept.flatMap(line => {
  const form = LINE_FORMS.find(([test]) => test(line));
  return form === undefined ? [] : [{ no: line.no, form: form[1](line) }];
});

const openedLines = kept => [...new Set([false, true].flatMap(rows => inlineGroups(kept, rows).flatMap(groupOpenings)))].sort((a, b) => a - b);
const inlineRefusals = kept => openedLines(kept).map(no => ({ no, form: `${INLINE_HTML}: ${shown(kept.find(line => line.no === no).text)}` }));

/**
 * The refusals of the rules for the whole file, outside fences: no reference definition, no line whose first non-space character is `<` (an HTML block start), no table row that
 * holds `\\|` (fix batch 5) and no raw inline HTML (fix batch 4): `{ no, form }`.
 */
export function wholeFileRefusals(format) {
  const kept = unfencedLines(format.lines);
  return [...lineRefusals(kept), ...inlineRefusals(kept)];
}

const titleOf = text => text.replace(/^[\d.]+\s*/, '').trim().replace(/\s+/g, ' ').toLowerCase();
const CHANGELOG = String(SECTION_COUNT);

/** The headings of a side inside a part's bytes, from `skip` bytes into the part. */
const headingsIn = (side, part, skip = 0) => side.format.headings.filter(heading => heading.start >= part.start + skip && heading.start < part.end);

const partOfLine = (side, no) => side.parts.find(part => part.no <= no && no < part.no + side.format.text.slice(part.start, part.end).split('\n').length) ?? side.parts[0];

const protectedTitlesOf = reference => new Set(reference.parts.filter(part => isProtected(part.cls) && part.kind !== 'preamble').flatMap(part => headingsIn(reference, part).map(heading => titleOf(heading.text))));

const isChangelogPart = part => part.number === CHANGELOG && part.kind === 'section';
const frozenSkip = frozenChangelog => (frozenChangelog === undefined ? 0 : frozenChangelog.end - frozenChangelog.start);

/** The headings of a part of the candidate that count as added: all of an unprotected part, the text appended to the changelog, none of another protected part. */
function addedHeadings(side, part, frozenChangelog) {
  if (!isProtected(part.cls)) return headingsIn(side, part);
  return isChangelogPart(part) ? headingsIn(side, part, frozenSkip(frozenChangelog)) : [];
}

/** The added headings with the title of a protected heading of the reference (no look-alike of a protected section). */
function lookAlikes(side, reference) {
  const known = new Set(unprotectedTitles(reference));
  const protectedTitles = protectedTitlesOf(reference);
  const frozenChangelog = reference.parts.find(part => part.kind === 'section' && part.number === CHANGELOG);
  return side.parts.flatMap(part => addedHeadings(side, part, frozenChangelog).filter(heading => protectedTitles.has(titleOf(heading.text)) && !known.has(titleOf(heading.text)))
    .map(heading => ({ no: heading.no, part, form: `an added heading with the title of a protected heading (no look-alike of a protected section): ${shown(heading.text)}` })));
}

/**
 * Every refusal of the restricted grammar for one file (`{ format, parts }`, the parts cut by its own class rows): the whole-file rules, the allowed line forms in each part that is not
 * protected, and an added heading with the title of ANY heading inside a protected part of `reference` (the frozen file: no look-alike of a protected section or subsection), in an
 * unprotected part or in the text appended to the changelog. `{ no, heading, cls, form }`.
 */
export function grammarRefusals(side, reference = side) {
  const found = [
    ...wholeFileRefusals(side.format).map(entry => ({ ...entry, part: partOfLine(side, entry.no) })),
    ...side.parts.filter(part => !isProtected(part.cls)).flatMap(part => partRefusals(side.format.text.slice(part.start, part.end), part.no).map(entry => ({ ...entry, part }))),
    ...lookAlikes(side, reference),
  ];
  return found.map(entry => ({ no: entry.no, heading: entry.part.heading, cls: entry.part.cls, form: entry.form }));
}

/** `--self`: `{ exit, lines }` for FORMAT's bytes: 0, or 1 with one line per refusal of the grammar (the frozen file's own sanity check, shown before it is frozen); the class table is checked elsewhere. */
export function selfGrammar(bytes) {
  const read = readClassed(bytes);
  if (!read.ok) return { exit: 4, lines: [`format-classes: ${read.reason}`] };
  const found = grammarRefusals({ format: read.format, parts: partsOf(read.format, read.rows) });
  return found.length === 0 ? { exit: 0, lines: [] } : { exit: 1, lines: found.map(entry => `format-classes: ${entry.heading} (class ${entry.cls}), line ${entry.no}: ${entry.form}`) };
}

/** The titles of the headings in the unprotected parts of a side, for `grammarRefusals`' reference. */
export const unprotectedTitles = side => side.parts.filter(part => !isProtected(part.cls)).flatMap(part => headingsIn(side, part).map(heading => titleOf(heading.text)));
