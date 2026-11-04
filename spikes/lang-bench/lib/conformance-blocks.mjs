// The blocks of a Markdown document as the conformance check reads them (E1.S8): fenced code blocks, paragraphs and tables, and a whitespace-collapsed text that keeps the
// line of every character. The line scan itself is E1.S9's (`scanBlocks` of format-structure.mjs); a document that scan refuses is never read here.
import { scanBlocks } from './format-structure.mjs';

const FENCE = /^(`{3,}|~{3,})(.*)$/;

/** The fenced code blocks of `lines`: `{ openNo, closeNo, info, content, before }`. `content` is the lines joined with LF, each ended by a LF (a block of no line is ''). `before` is the nearest non-blank line above the opener (its text). */
export function fencedBlocks(lines) {
  const kept = new Set(scanBlocks(lines).kept.map(line => line.no));
  const blocks = [];
  let open = null;
  const close = (closeNo) => {
    blocks.push({ openNo: open.openNo, closeNo, info: open.info, content: open.content.map(text => `${text}\n`).join(''), before: open.before });
    open = null;
  };
  for (const line of lines) {
    if (kept.has(line.no)) {
      if (open !== null) close(line.no - 1);
      continue;
    }
    const found = FENCE.exec(line.text.trimStart());
    if (open === null) {
      if (found !== null) open = { openNo: line.no, char: found[1][0], length: found[1].length, info: found[2].trim(), content: [], before: lineAbove(lines, line.no) };
    } else if (found !== null && found[1][0] === open.char && found[1].length >= open.length && found[2].trim() === '') close(line.no);
    else open.content.push(line.text);
  }
  return blocks;
}

/** The text of the nearest non-blank line above line `no`, or ''. */
function lineAbove(lines, no) {
  for (let at = no - 2; at >= 0; at -= 1) if (lines[at].text.trim() !== '') return lines[at].text;
  return '';
}

/** `text` with every run of whitespace made one space and the ends trimmed, with `lineAt(index)` giving the document line of the character at `index` of the result. `parts` are `{ no, text }` lines. */
export function collapse(parts) {
  let text = '';
  const lineOf = [];
  for (const part of parts) {
    for (const char of part.text) {
      if (/\s/.test(char)) {
        if (text !== '' && !text.endsWith(' ')) { text += ' '; lineOf.push(part.no); }
      } else {
        text += char;
        lineOf.push(part.no);
      }
    }
    if (text !== '' && !text.endsWith(' ')) { text += ' '; lineOf.push(part.no); }
  }
  if (text.endsWith(' ')) { text = text.slice(0, -1); lineOf.pop(); }
  return { text, lineAt: index => lineOf[Math.min(Math.max(index, 0), Math.max(lineOf.length - 1, 0))] ?? 1 };
}

/** The collapsed text alone. */
export const collapsed = text => collapse([{ no: 1, text }]).text;

/** The paragraphs of the lines outside fences: runs of non-blank lines on adjacent line numbers, `{ startNo, endNo, lines, isTable }`. */
export function paragraphs(lines) {
  const result = [];
  let current = null;
  for (const line of scanBlocks(lines).kept) {
    if (line.text.trim() === '') { current = null; continue; }
    if (current !== null && current.endNo + 1 === line.no) {
      current.lines.push(line);
      current.endNo = line.no;
    } else {
      current = { startNo: line.no, endNo: line.no, lines: [line] };
      result.push(current);
    }
  }
  return result.map(entry => ({ ...entry, isTable: entry.lines.every(line => line.text.startsWith('|')) }));
}
