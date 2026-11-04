// The restricted grammar of the unprotected parts of FORMAT.md (E1.S9, fix batch 2), and two rules for the whole file. Patching the reader one CommonMark form at a time cannot close
// the class "the reader and a renderer read a candidate differently" (a deletion can turn a literal inside a fence into a heading; a reference definition acts at a distance), so
// the guarantee is by construction: a candidate's unprotected parts may only be made of the line forms below, and no line of any part may start a reference definition or an HTML block.
// The argument: every protected part starts at a column-0 heading, where CommonMark starts a fresh block; no unprotected construct can stay open across a part boundary (a fence
// closes inside its part, there is no HTML block, no list continues across a heading); and nothing acts at a distance (no reference definition): so protected bytes render as protected bytes.
import { isProtected, partsOf, readClassed } from './format-class-table.mjs';
import { unfencedLines } from './format-structure.mjs';

const SECTION_HEADING = /^## \d+\. \S(?:.*\S)?$/;
const SUB_HEADING = /^### \d+\.\d+ \S(?:.*\S)?$/;
const FENCE_OPEN = /^```[a-z]*$/;
const FENCE_LINE = /^ {0,3}(?:`{3,}|~{3,})/;
const LIST_ITEM = /^(- |\d{1,3}\. )(.*)$/;
const TABLE_ROW = /^\|.*\|$/;
const REFERENCE_DEFINITION = /^\s*\[(?:[^\]\\]|\\.)*\]:/;
const BLOCK_STARTERS = '#<>|~=-+_]';

const shown = text => (text.length > 50 ? `${text.slice(0, 50)}…` : text);

/** How a paragraph line (or the text of a list item) may not start, as a phrase after "that", or null: it must not start a block of any kind, in either reading. */
export function paragraphStartProblem(text) {
  const first = text[0];
  if (first === undefined) return 'is empty';
  if (first === ' ' || first === '\t') return 'starts with a space or a tab';
  if (BLOCK_STARTERS.includes(first)) return `starts with \`${first}\` (a block starter)`;
  if (text.startsWith('```')) return 'starts with three backticks (a fence has its own form: an opener of three backticks and lower-case letters, a closer of three backticks, both at column 0)';
  if (first === '*' && (!text.startsWith('**') || text.startsWith('***'))) return 'starts with `*` that is not `**` (a list marker or a thematic break)';
  if (/^\d{1,9}[.)](?: |$)/.test(text)) return 'starts with an ordered list marker';
  if (first === '[' && !/^\[[^\]\\]*\](?!:)/.test(text)) return 'starts with `[` and may start a reference definition';
  return null;
}

/**
 * The refusals in the text of one unprotected part: `{ no, form }` per line that is not one of the allowed forms: a blank line; a heading in the exact form (`## k. Title`, `### k.n Title`);
 * a paragraph line; a list item (`- ` or one to three digits and `. `, then text that is a paragraph line; its continuation lines indented by exactly the marker width, the same rule
 * for the text; no nested list, no fence or heading in an item); a table row (`|` … `|`); a fenced block (an opener at column 0 that is `` ``` `` or `` ``` `` and lower-case letters, a
 * closer `` ``` `` at column 0, both in the part; no line of the block that is a fence marker line (up to three spaces of indent) but the closer).
 */
export function partRefusals(text, firstLine) {
  const step = (state, line, index) => {
    const no = firstLine + index;
    const refuse = form => ({ ...state, refusals: [...state.refusals, { no, form: `${form}: ${shown(line)}` }] });
    if (state.fenceAt !== null) {
      if (line === '```') return { ...state, fenceAt: null };
      return FENCE_LINE.test(line) ? refuse('a fence marker line inside a fenced block that is not its closer (the block is closed by a line of exactly three backticks)') : state;
    }
    if (line === '') return state;
    if (SECTION_HEADING.test(line) || SUB_HEADING.test(line)) return { ...(/ #+$/.test(line) ? refuse('a heading with a closing sequence of `#`') : state), item: null };
    if (FENCE_OPEN.test(line)) return { ...state, fenceAt: no, item: null };
    if (TABLE_ROW.test(line)) return { ...(/\|\s*\[[^\]]+\]:/.test(line) ? refuse('a table row that holds a reference definition') : state), item: null };
    const listed = LIST_ITEM.exec(line);
    if (listed !== null) {
      const problem = paragraphStartProblem(listed[2]);
      return { ...(problem === null ? state : refuse(`a list item whose text ${problem}`)), item: listed[1].length };
    }
    if (line.startsWith(' ')) {
      if (state.item === null || !line.startsWith(' '.repeat(state.item))) return refuse(state.item === null ? 'an indented line, and no list item is open (an indent is only the continuation of a list item)' : `an indented line that is not a continuation of the open list item (a continuation is indented by exactly ${state.item} spaces)`);
      const problem = paragraphStartProblem(line.slice(state.item));
      return problem === null ? state : refuse(`a continuation line whose text ${problem}`);
    }
    const problem = paragraphStartProblem(line);
    return { ...(problem === null ? state : refuse(`a paragraph line that ${problem}`)), item: null };
  };
  const end = text.split('\n').reduce((state, line, index) => step(state, line, index), { refusals: [], fenceAt: null, item: null });
  return end.fenceAt === null ? end.refusals : [...end.refusals, { no: end.fenceAt, form: 'a fenced block that is not closed in its part' }];
}

/** The refusals of the two rules for the whole file, outside fences: no reference definition, and no line whose first non-space character is `<` (an HTML block start): `{ no, form }`. */
export function wholeFileRefusals(format) {
  return unfencedLines(format.lines).flatMap(line => {
    if (REFERENCE_DEFINITION.test(line.text)) return [{ no: line.no, form: `a link reference definition (nothing may act at a distance): ${shown(line.text)}` }];
    if (/^\s*</.test(line.text)) return [{ no: line.no, form: `a line whose first non-space character is \`<\` (an HTML block start): ${shown(line.text)}` }];
    return [];
  });
}

const titleOf = text => text.replace(/^[\d.]+\s*/, '').trim();

/**
 * Every refusal of the restricted grammar for one file (`{ format, parts }`, the parts cut by its own class rows): the whole-file rules, and the allowed line forms in each part that is
 * not protected, and an added heading in such a part that carries the title of a protected heading of `reference` (the frozen file: no look-alike of a protected section).
 * `{ no, heading, cls, form }`.
 */
export function grammarRefusals(side, reference = side) {
  const partOf = no => side.parts.find(part => part.no <= no && no < part.no + side.format.text.slice(part.start, part.end).split('\n').length) ?? side.parts[0];
  const known = new Set(unprotectedTitles(reference));
  const protectedTitles = new Set(reference.parts.filter(part => isProtected(part.cls) && part.kind !== 'preamble').map(part => titleOf(part.heading)));
  const inPart = part => side.format.headings.filter(heading => heading.start >= part.start && heading.start < part.end);
  const looks = part => inPart(part).filter(heading => protectedTitles.has(titleOf(heading.text)) && !known.has(titleOf(heading.text)))
    .map(heading => ({ no: heading.no, part, form: `an added heading with the title of a protected heading (no look-alike of a protected section): ${shown(heading.text)}` }));
  const found = [
    ...wholeFileRefusals(side.format).map(entry => ({ ...entry, part: partOf(entry.no) })),
    ...side.parts.filter(part => !isProtected(part.cls)).flatMap(part => [...partRefusals(side.format.text.slice(part.start, part.end), part.no).map(entry => ({ ...entry, part })), ...looks(part)]),
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
export const unprotectedTitles = side => side.parts.filter(part => !isProtected(part.cls)).flatMap(part => side.format.headings.filter(heading => heading.start >= part.start && heading.start < part.end).map(heading => titleOf(heading.text)));
