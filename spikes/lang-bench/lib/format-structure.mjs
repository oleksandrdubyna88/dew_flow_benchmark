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

/** The column where the content of a list item starts, for a marker line `rest` at column `col`, or null when `rest` is no list item. */
function listContentColumn(col, rest) {
  const marker = LIST_MARKER.exec(rest);
  if (marker === null || THEMATIC_BREAK.test(rest)) return null;
  const gap = marker[2].replaceAll('\t', '    ').length;
  return col + marker[1].length + (rest.length === marker[0].length || gap > 4 ? 1 : gap);
}

/** The reason when `rest` starts an HTML block that CommonMark renders as raw HTML, else null; a complete tag alone on its line starts one only where no paragraph is open (`freshBlock`). */
function htmlBlockStart(rest, freshBlock) {
  if (!rest.startsWith('<')) return null;
  const type = (HTML_BLOCK_1.test(rest) || rest.startsWith('<!--') || rest.startsWith('<?') || /^<![A-Za-z]/.test(rest) || rest.startsWith('<![CDATA[') || HTML_BLOCK_6.test(rest) || (freshBlock && HTML_BLOCK_7.test(rest)));
  return type ? 'an HTML block start: CommonMark renders raw HTML here, and the reader cannot read through it' : null;
}

const ATX_START = /^ {0,3}#{1,6}(?:[ \t]|$)/;
const SETEXT_UNDERLINE = /^ {0,3}(?:=+|-+)[ \t]*$/;
const QUOTE_MARKER = /^ {0,3}> ?/;

/**
 * The reason when the line opens a heading or a fence behind a prefix the reader does not look through: after one or more block quote markers (`> ## x`, `> ```x`) or list
 * markers (`- ## x`, `1. ```x`), else null. A renderer shows the heading or the fence there; the reader sees a line of text.
 */
function hiddenStart(rest) {
  let text = rest;
  let prefix = null;
  for (;;) {
    const quote = QUOTE_MARKER.exec(text);
    const marker = quote === null && !THEMATIC_BREAK.test(text) ? LIST_MARKER.exec(text) : null;
    if (quote === null && (marker === null || marker[0].length === text.length)) break;
    prefix ??= quote === null ? 'a list marker' : 'a block quote';
    text = text.slice((quote ?? marker)[0].length);
  }
  return prefix !== null && (ATX_START.test(text) || FENCE.test(text.trimStart())) ? `an ATX heading or a fence opener after ${prefix} on the same line: a renderer shows it, the reader does not` : null;
}

/** The reason when the raw line is an ATX heading in a form the reader does not take (it takes `## ` and `### ` at column 0), else null. */
function strangeHeading(text) {
  if (/^ {1,3}#{1,6}(?:[ \t]|$)/.test(text)) return 'an ATX heading with 1 to 3 leading spaces: the reader takes a heading only at column 0';
  if (/^#{1,6}\t/.test(text)) return 'an ATX heading with a tab after the hashes: the reader takes `#` and a space';
  if (/^#{1,6} *$/.test(text)) return 'an ATX heading with nothing after the hashes';
  return null;
}

/**
 * The lines outside fenced code blocks and the first line that CommonMark would read otherwise than this reader does: `{ kept, fault }` (`fault` is `{ no, reason }` or null). A fence
 * opens at column 0 (inside a list item: at the item's content column, up to three columns more) with three or more backticks or tildes (a backtick fence has no backtick in its info
 * string) and closes with a line of the same character, at least as long, up to three columns in, and nothing else. A fence line indented 1 to 3 spaces outside a list item, an HTML block
 * start at columns 0 to 3 and an ATX heading in a form the reader does not take are faults: the reader would see another document than a renderer. A fence in a list item ends where the item does.
 */
export function scanBlocks(lines) {
  const kept = [];
  const lists = [];
  let fence = null;
  let freshBlock = true;
  let paragraphOpen = false;
  for (const line of lines) {
    const { col, rest } = indentOf(line.text);
    const blank = rest === '';
    if (fence !== null && !(fence.inList && !blank && col < fence.base)) {
      const closer = FENCE.exec(rest);
      if (col - fence.base <= 3 && closer !== null && closer[1][0] === fence.char && closer[1].length >= fence.length && closer[2].trim() === '') fence = null;
      freshBlock = fence === null;
      paragraphOpen = false;
      continue;
    }
    fence = null;
    const inItem = lists.length > 0;
    if (!blank) while (lists.length > 0 && col < lists.at(-1)) lists.pop();
    const base = lists.at(-1) ?? 0;
    const opened = FENCE.exec(rest);
    if (opened !== null && col - base <= 3) {
      if (opened[1][0] === '`' && opened[2].includes('`')) return { kept, fault: { no: line.no, reason: 'a backtick fence opener whose info string holds a backtick: CommonMark reads it as text, the reader as a fence' } };
      if (lists.length === 0 && col >= 1) return { kept, fault: { no: line.no, reason: `a fence indented ${col} space${col === 1 ? '' : 's'} outside a list item: CommonMark opens a fence here and the reader does not` } };
      fence = { char: opened[1][0], length: opened[1].length, base, inList: lists.length > 0 };
      freshBlock = false;
      continue;
    }
    if (col <= 3 && paragraphOpen && SETEXT_UNDERLINE.test(line.text)) {
      const reason = inItem && rest.startsWith('-') ? 'a dash line under a list item (a thematic break or a setext underline: refused)' : 'a setext heading underline under a paragraph line: a renderer shows a heading, the reader does not';
      return { kept, fault: { no: line.no, reason } };
    }
    const reason = col <= 3 ? hiddenStart(rest) ?? htmlBlockStart(rest, freshBlock) ?? strangeHeading(line.text) : null;
    if (reason !== null) return { kept, fault: { no: line.no, reason } };
    if (!blank) {
      const content = listContentColumn(col, rest);
      if (content !== null) lists.push(content);
    }
    kept.push(line);
    freshBlock = blank || /^ {0,3}#{1,6}[ \t]/.test(line.text);
    paragraphOpen = !blank && !ATX_START.test(line.text) && !THEMATIC_BREAK.test(rest) && (col <= 3 || paragraphOpen);
  }
  return { kept, fault: null };
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

/** The first structural fault of the headings as `{ no, text }`, or null: `## 1.` to `## 7.` once each ascending, `### k.1`, `k.2` … under `## k`. */
function structureFault(headings) {
  let section = 0;
  let sub = 0;
  for (const heading of headings) {
    const marker = '#'.repeat(heading.level);
    if (heading.level === 2) {
      section += 1;
      sub = 0;
      const found = SECTION_NUMBER.exec(heading.text);
      if (section > SECTION_COUNT || found === null || Number(found[1]) !== section) {
        return { no: heading.no, text: `${marker} ${heading.text}`, rule: section > SECTION_COUNT ? `more than ${SECTION_COUNT} sections` : `expected section ${section} (\`## ${section}.\`)` };
      }
      continue;
    }
    sub += 1;
    const found = SUB_NUMBER.exec(heading.text);
    if (section === 0 || found === null || Number(found[1]) !== section || Number(found[2]) !== sub) {
      return { no: heading.no, text: `${marker} ${heading.text}`, rule: section === 0 ? 'a subsection before section 1' : `expected subsection ${section}.${sub} (\`### ${section}.${sub}\`)` };
    }
  }
  return section < SECTION_COUNT ? { no: 0, text: 'the end of the file', rule: `only ${section} of ${SECTION_COUNT} sections are present` } : null;
}

/** `{ ok: true, format }` for FORMAT's bytes — `{ bytes, text, lines, headings, sha256 }` — or `{ ok: false, line, reason }` for a format error (exit 4). */
export function parseFormat(bytes) {
  const checked = checkBytes(bytes);
  if (!checked.ok) return checked;
  const lines = linesOf(checked.text);
  const scanned = scanBlocks(lines);
  if (scanned.fault !== null) return fail(scanned.fault.no, scanned.fault.reason);
  const headings = headingsOf(lines);
  const fault = structureFault(headings);
  if (fault !== null) return fail(fault.no === 0 ? lines.length : fault.no, `${fault.text}: ${fault.rule}`);
  return ok({ format: { bytes, text: checked.text, lines, headings, sha256: sha256Hex(bytes) } });
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
