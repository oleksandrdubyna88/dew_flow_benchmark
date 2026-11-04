// The shape of FORMAT.md as the two FORMAT tools read it (E1.S9): the raw bytes, the headings outside fenced code blocks, the section numbers, and
// the cut points of the parts. FORMAT is read as it is: UTF-8, LF only, no byte order mark, nothing normalised; a slice of the text is a slice of
// the file's bytes (valid UTF-8 survives decode and encode unchanged), and FORMAT's sha256 is taken over the raw bytes.
import { createHash } from 'node:crypto';

export const SECTION_COUNT = 7;
export const sha256Hex = bytes => createHash('sha256').update(bytes).digest('hex');

const ok = value => ({ ok: true, ...value });
const fail = (line, reason) => ({ ok: false, line, reason });
const BOM = [0xef, 0xbb, 0xbf];
const FENCE_OPEN = /^(`{3,}|~{3,})/;

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

/** Whether `line` closes the fence `open` (the same character, at least as many, nothing else but blanks). */
const closes = (open, line) => line.startsWith(open[0].repeat(open.length)) && line.trimEnd().split('').every(char => char === open[0]);

/** The lines outside fenced code blocks (a fence's own marker lines are inside it): a fence opens with a line starting with three or more backticks or tildes and closes with the same marker. */
export function unfencedLines(lines) {
  const kept = [];
  let fence = null;
  for (const line of lines) {
    if (fence !== null) {
      if (closes(fence, line.text)) fence = null;
      continue;
    }
    const opened = FENCE_OPEN.exec(line.text);
    if (opened) fence = opened[1];
    else kept.push(line);
  }
  return kept;
}

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
export const lineNumberAt = (format, offset) => format.lines.findLast(line => line.start <= offset).no;
