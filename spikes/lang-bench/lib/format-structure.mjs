// The shape of FORMAT.md as the two FORMAT tools read it (E1.S9): the raw bytes, the headings outside fenced code blocks, the section numbers, and
// the cut points of the parts. FORMAT is read as it is: UTF-8, LF only, no byte order mark, nothing normalised; a slice of the text is a slice of
// the file's bytes (valid UTF-8 survives decode and encode unchanged), and FORMAT's sha256 is taken over the raw bytes.
import { createHash } from 'node:crypto';

export const SECTION_COUNT = 7;
export const sha256Hex = bytes => createHash('sha256').update(bytes).digest('hex');

const ok = value => ({ ok: true, ...value });
const fail = (line, reason) => ({ ok: false, line, reason });
const BOM = [0xef, 0xbb, 0xbf];

/** `ok({ text })` for bytes that are UTF-8 with LF only and no byte order mark, else `fail(line, reason)` naming the first line that is not. */
export function checkBytes(bytes) {
  if (BOM.every((byte, index) => bytes[index] === byte)) return fail(1, 'the file starts with a byte order mark');
  const cr = bytes.indexOf(0x0d);
  if (cr !== -1) return fail(1 + bytes.subarray(0, cr).filter(byte => byte === 0x0a).length, 'a carriage return: FORMAT is LF only');
  try {
    return ok({ text: new TextDecoder('utf-8', { fatal: true, ignoreBOM: true }).decode(bytes) });
  } catch {
    return fail(firstBadLine(bytes), 'the bytes are not valid UTF-8');
  }
}

function firstBadLine(bytes) {
  const decoder = new TextDecoder('utf-8', { fatal: true, ignoreBOM: true });
  let line = 1;
  let from = 0;
  while (from <= bytes.length) {
    const end = bytes.indexOf(0x0a, from) === -1 ? bytes.length : bytes.indexOf(0x0a, from);
    try {
      decoder.decode(bytes.subarray(from, end));
    } catch {
      return line;
    }
    line += 1;
    from = end + 1;
  }
  return line;
}

/** The lines of `text` as `{ no, start, text }` (`start` is the offset of the line's first character; the last entry is the text after the final LF, empty or not). */
export function linesOf(text) {
  const lines = [];
  let start = 0;
  for (let no = 1; start <= text.length; no += 1) {
    const found = text.indexOf('\n', start);
    const end = found === -1 ? text.length : found;
    lines.push({ no, start, text: text.slice(start, end) });
    start = end + 1;
  }
  return lines;
}

const FENCE = /^(`{3,}|~{3,})(.*)$/;
const LIST_MARKER = /^([-+*]|\d{1,9}[.)])( +|\t|$)/;
const THEMATIC_BREAK = /^([-*_])(?:[ \t]*\1){2,}[ \t]*$/;
const HTML_BLOCK_6 = /^<\/?(?:address|article|aside|base|basefont|blockquote|body|caption|center|col|colgroup|dd|details|dialog|dir|div|dl|dt|fieldset|figcaption|figure|footer|form|frame|frameset|h[1-6]|head|header|hr|html|iframe|legend|li|link|main|menu|menuitem|nav|noframes|ol|optgroup|option|p|param|search|section|summary|table|tbody|td|tfoot|th|thead|title|tr|track|ul)(?:[ \t]|\/?>|$)/i;
const HTML_BLOCK_1 = /^<(?:script|pre|style|textarea)(?:[ \t>]|$)/i;
const HTML_BLOCK_7 = /^(?:<[A-Za-z][A-Za-z0-9-]*(?:[ \t][^<>]*)?\/?>|<\/[A-Za-z][A-Za-z0-9-]*[ \t]*>)[ \t]*$/;

/** The indent of `text` in columns (a tab goes to the next multiple of four) and the text after it. */
function indentOf(text) {
  let col = 0;
  let at = 0;
  for (; at < text.length; at += 1) {
    if (text[at] === ' ') col += 1;
    else if (text[at] === '\t') col += 4 - (col % 4);
    else break;
  }
  return { col, rest: text.slice(at) };
}

const gapOf = marker => marker[2].replaceAll('\t', '    ').length;
const contentGap = (marker, rest) => (rest.length === marker[0].length || gapOf(marker) > 4 ? 1 : gapOf(marker));

/** The column where the content of a list item starts, for a marker line `rest` at column `col`, or null when `rest` is no list item. */
function listContentColumn(col, rest) {
  const marker = LIST_MARKER.exec(rest);
  if (marker === null || THEMATIC_BREAK.test(rest)) return null;
  return col + marker[1].length + contentGap(marker, rest);
}

/** The ways a line can start an HTML block whatever is open (CommonMark's types 1 to 6). */
const HTML_STARTS = Object.freeze([
  rest => HTML_BLOCK_1.test(rest), rest => rest.startsWith('<!--'), rest => rest.startsWith('<?'), rest => /^<![A-Za-z]/.test(rest), rest => rest.startsWith('<![CDATA['), rest => HTML_BLOCK_6.test(rest),
]);
const startsHtmlBlock = (rest, freshBlock) => HTML_STARTS.some(test => test(rest)) || (freshBlock && HTML_BLOCK_7.test(rest));

/** The reason when `rest` starts an HTML block that CommonMark renders as raw HTML, else null; a complete tag alone on its line starts one only where no paragraph is open (`freshBlock`). */
function htmlBlockStart(rest, freshBlock) {
  if (!rest.startsWith('<')) return null;
  return startsHtmlBlock(rest, freshBlock) ? 'an HTML block start: CommonMark renders raw HTML here, and the reader cannot read through it' : null;
}

const ATX_START = /^ {0,3}#{1,6}(?:[ \t]|$)/;
const SETEXT_UNDERLINE = /^ {0,3}(?:=+|-+)[ \t]*$/;
const QUOTE_MARKER = /^ {0,3}> ?/;

const markerOf = (text, quote) => (quote === null && !THEMATIC_BREAK.test(text) ? LIST_MARKER.exec(text) : null);
const endsPrefixes = (quote, marker, text) => quote === null && (marker === null || marker[0].length === text.length);

/** One block quote or list marker at the start of `text`: `{ prefix, rest }` (`rest` is the text after it), or null when there is none. */
function prefixStep(text) {
  const quote = QUOTE_MARKER.exec(text);
  const marker = markerOf(text, quote);
  if (endsPrefixes(quote, marker, text)) return null;
  return { prefix: quote === null ? 'a list marker' : 'a block quote', rest: text.slice((quote ?? marker)[0].length) };
}

/** `text` without its block quote and list markers, and the kind of the first one (null when there is none). */
function stripPrefixes(rest) {
  let state = { text: rest, prefix: null };
  for (let step = prefixStep(rest); step !== null; step = prefixStep(state.text)) state = { text: step.rest, prefix: state.prefix ?? step.prefix };
  return state;
}

const opensBlock = text => ATX_START.test(text) || FENCE.test(text.trimStart());

/**
 * The reason when the line opens a heading or a fence behind a prefix the reader does not look through: after one or more block quote markers (`> ## x`, `> ```x`) or list
 * markers (`- ## x`, `1. ```x`), else null. A renderer shows the heading or the fence there; the reader sees a line of text.
 */
function hiddenStart(rest) {
  const { text, prefix } = stripPrefixes(rest);
  return prefix !== null && opensBlock(text) ? `an ATX heading or a fence opener after ${prefix} on the same line: a renderer shows it, the reader does not` : null;
}

/** The reason when the raw line is an ATX heading in a form the reader does not take (it takes `## ` and `### ` at column 0), else null. */
function strangeHeading(text) {
  if (/^ {1,3}#{1,6}(?:[ \t]|$)/.test(text)) return 'an ATX heading with 1 to 3 leading spaces: the reader takes a heading only at column 0';
  if (/^#{1,6}\t/.test(text)) return 'an ATX heading with a tab after the hashes: the reader takes `#` and a space';
  if (/^#{1,6} *$/.test(text)) return 'an ATX heading with nothing after the hashes';
  return null;
}

const viewOf = line => {
  const { col, rest } = indentOf(line.text);
  return { line, col, rest, blank: rest === '' };
};

const closerMatches = (closer, fence) => closer !== null && closer[1][0] === fence.char && closer[1].length >= fence.length && closer[2].trim() === '';
const closes = (col, fence, closer) => col - fence.base <= 3 && closerMatches(closer, fence);

/** An open fence goes on over the line unless it is a list item's fence and the line is a non-blank line left of the item's content. */
const staysInFence = (st, v) => st.fence !== null && !(st.fence.inList && !v.blank && v.col < st.fence.base);

function insideFence(st, v) {
  if (closes(v.col, st.fence, FENCE.exec(v.rest))) st.fence = null;
  st.freshBlock = st.fence === null;
  st.paragraphOpen = false;
  return null;
}

const popLists = (st, v) => {
  while (!v.blank && st.lists.length > 0 && v.col < st.lists.at(-1)) st.lists.pop();
};

const spaces = col => `${col} space${col === 1 ? '' : 's'}`;
const backtickInfoFault = opened => (opened[1][0] === '`' && opened[2].includes('`') ? 'a backtick fence opener whose info string holds a backtick: CommonMark reads it as text, the reader as a fence' : null);
const indentedFenceFault = (st, v) => (st.lists.length === 0 && v.col >= 1 ? `a fence indented ${spaces(v.col)} outside a list item: CommonMark opens a fence here and the reader does not` : null);

/** A line that opens a fence: `{ fault }` (null when it opens cleanly), or null when the line opens none. */
function tryOpen(st, v, base) {
  const opened = FENCE.exec(v.rest);
  if (opened === null || v.col - base > 3) return null;
  const reason = backtickInfoFault(opened) ?? indentedFenceFault(st, v);
  if (reason !== null) return { fault: { no: v.line.no, reason } };
  st.fence = { char: opened[1][0], length: opened[1].length, base, inList: st.lists.length > 0 };
  st.freshBlock = false;
  return { fault: null };
}

const underlinesParagraph = (st, v) => v.col <= 3 && st.paragraphOpen && SETEXT_UNDERLINE.test(v.line.text);
const setextReason = (inItem, rest) => (inItem && rest.startsWith('-') ? 'a dash line under a list item (a thematic break or a setext underline: refused)' : 'a setext heading underline under a paragraph line: a renderer shows a heading, the reader does not');
const setextFault = (st, v, inItem) => (underlinesParagraph(st, v) ? setextReason(inItem, v.rest) : null);

/** A line that CommonMark would read otherwise than this reader: `{ fault }`, or null. */
function lineFault(st, v, inItem) {
  const reason = setextFault(st, v, inItem) ?? (v.col <= 3 ? hiddenStart(v.rest) ?? htmlBlockStart(v.rest, st.freshBlock) ?? strangeHeading(v.line.text) : null);
  return reason === null ? null : { fault: { no: v.line.no, reason } };
}

function pushListItem(st, v) {
  const content = v.blank ? null : listContentColumn(v.col, v.rest);
  if (content !== null) st.lists.push(content);
}

const plainLine = v => !v.blank && !ATX_START.test(v.line.text) && !THEMATIC_BREAK.test(v.rest);
const opensParagraph = (st, v) => plainLine(v) && (v.col <= 3 || st.paragraphOpen);

function keepLine(st, v) {
  pushListItem(st, v);
  st.kept.push(v.line);
  st.freshBlock = v.blank || /^ {0,3}#{1,6}[ \t]/.test(v.line.text);
  st.paragraphOpen = opensParagraph(st, v);
}

/** The fault of one line (null when there is none); a line outside the fault cases is kept. */
function scanLine(st, line) {
  const v = viewOf(line);
  if (staysInFence(st, v)) return insideFence(st, v);
  st.fence = null;
  const inItem = st.lists.length > 0;
  popLists(st, v);
  const outcome = tryOpen(st, v, st.lists.at(-1) ?? 0) ?? lineFault(st, v, inItem);
  if (outcome !== null) return outcome.fault;
  keepLine(st, v);
  return null;
}

/**
 * The lines outside fenced code blocks and the first line that CommonMark would read otherwise than this reader does: `{ kept, fault }` (`fault` is `{ no, reason }` or null). A fence
 * opens at column 0 (inside a list item: at the item's content column, up to three columns more) with three or more backticks or tildes (a backtick fence has no backtick in its info
 * string) and closes with a line of the same character, at least as long, up to three columns in, and nothing else. A fence line indented 1 to 3 spaces outside a list item, an HTML block
 * start at columns 0 to 3 and an ATX heading in a form the reader does not take are faults: the reader would see another document than a renderer. A fence in a list item ends where the item does.
 */
export function scanBlocks(lines) {
  const st = { kept: [], lists: [], fence: null, freshBlock: true, paragraphOpen: false };
  for (const line of lines) {
    const fault = scanLine(st, line);
    if (fault !== null) return { kept: st.kept, fault };
  }
  return { kept: st.kept, fault: null };
}

/** The lines outside fenced code blocks (a fence's own lines are inside it); the faults of `scanBlocks` are not looked at here. */
export const unfencedLines = lines => scanBlocks(lines).kept;

/** The `##` and `###` headings outside fenced code blocks, in order: `{ level, text, no, start }` (`text` is the heading without its marker). */
export function headingsOf(lines) {
  const headings = [];
  for (const line of unfencedLines(lines)) {
    if (line.text.startsWith('## ')) headings.push({ level: 2, text: line.text.slice(3), no: line.no, start: line.start });
    else if (line.text.startsWith('### ')) headings.push({ level: 3, text: line.text.slice(4), no: line.no, start: line.start });
  }
  return headings;
}

const SECTION_NUMBER = /^(\d+)\./;
const SUB_NUMBER = /^(\d+)\.(\d+)(?:\s|$)/;

/** Each heading with the number of the section it is in (`section`) and its place among the subsections of that section (`sub`). */
function numbered(headings) {
  return headings.reduce((all, heading) => {
    const last = all.at(-1) ?? { section: 0, sub: 0 };
    all.push({ heading, section: last.section + (heading.level === 2 ? 1 : 0), sub: heading.level === 2 ? 0 : last.sub + 1 });
    return all;
  }, []);
}

const faultAt = (heading, rule) => ({ no: heading.no, text: `${'#'.repeat(heading.level)} ${heading.text}`, rule });
const sectionRule = section => (section > SECTION_COUNT ? `more than ${SECTION_COUNT} sections` : `expected section ${section} (\`## ${section}.\`)`);
const subRule = (section, sub) => (section === 0 ? 'a subsection before section 1' : `expected subsection ${section}.${sub} (\`### ${section}.${sub}\`)`);

function sectionFault({ heading, section }) {
  const found = SECTION_NUMBER.exec(heading.text);
  return section > SECTION_COUNT || found === null || Number(found[1]) !== section ? faultAt(heading, sectionRule(section)) : null;
}

const subIsOff = (found, section, sub) => section === 0 || found === null || Number(found[1]) !== section || Number(found[2]) !== sub;

function subFault({ heading, section, sub }) {
  return subIsOff(SUB_NUMBER.exec(heading.text), section, sub) ? faultAt(heading, subRule(section, sub)) : null;
}

const endFault = section => (section < SECTION_COUNT ? { no: 0, text: 'the end of the file', rule: `only ${section} of ${SECTION_COUNT} sections are present` } : null);

/** The first structural fault of the headings as `{ no, text }`, or null: `## 1.` to `## 7.` once each ascending, `### k.1`, `k.2` … under `## k`. */
function structureFault(headings) {
  const all = numbered(headings);
  const first = all.map(entry => (entry.heading.level === 2 ? sectionFault(entry) : subFault(entry))).find(fault => fault !== null);
  return first ?? endFault(all.at(-1)?.section ?? 0);
}

const structureFail = (fault, lines) => fail(fault.no === 0 ? lines.length : fault.no, `${fault.text}: ${fault.rule}`);

/**
 * `{ ok: true, format }` for FORMAT's bytes — `{ bytes, text, lines, headings, sha256 }` — or `{ ok: false, line, reason }` for a format error (exit 4).
 * `structure` is the rule for the headings (FORMAT's own: seven numbered sections); another document with the same byte and block rules, a spec, gives its own, or `() => null`.
 */
export function parseFormat(bytes, { structure = structureFault } = {}) {
  const checked = checkBytes(bytes);
  if (!checked.ok) return checked;
  const lines = linesOf(checked.text);
  const scanned = scanBlocks(lines);
  if (scanned.fault !== null) return fail(scanned.fault.no, scanned.fault.reason);
  const headings = headingsOf(lines);
  const fault = structure(headings);
  return fault === null ? ok({ format: { bytes, text: checked.text, lines, headings, sha256: sha256Hex(bytes) } }) : structureFail(fault, lines);
}

/** The heading's number: `3` for `## 3. …`, `2.4` for `### 2.4 …`. */
export const numberOf = heading => (heading.level === 2 ? SECTION_NUMBER.exec(heading.text)[1] : SUB_NUMBER.exec(heading.text).slice(1, 3).join('.'));

/** The heading with this number (`'3'`, `'2.4'`), or undefined. */
export const headingByNumber = (format, number) => format.headings.find(heading => numberOf(heading) === number);

/** `{ start, end }` of the text from the heading line to the byte before the next heading of level `untilLevel` or higher (2: a whole section; 3: the lead text of a section, or one subsection). */
export function sliceFrom(format, heading, untilLevel) {
  const next = format.headings.slice(format.headings.indexOf(heading) + 1).find(other => other.level <= untilLevel);
  return { start: heading.start, end: next === undefined ? format.text.length : next.start };
}

/** The text before the first `##` heading: the preamble. */
export const preambleSlice = format => ({ start: 0, end: format.headings[0].start });

/** The header text of the file, `Format version: <v>`: the version, or null. */
export const versionOf = format => /^Format version: (\S+)$/m.exec(format.text.slice(0, format.headings[0].start))?.[1] ?? null;

/** The line number (1-based) of the character at `offset`. */
export const lineNumberAt = (format, offset) => format.lines.findLast(line => line.start <= offset)?.no ?? 1;
