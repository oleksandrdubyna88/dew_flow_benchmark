// The blocks of a Markdown document as the conformance check reads them (E1.S8): fenced code blocks, paragraphs and tables, and a whitespace-collapsed text that keeps the
// line of every character. The line scan itself is E1.S9's (`scanBlocks` of format-structure.mjs); a document that scan refuses is never read here.
import { scanBlocks } from './format-structure.mjs';

const FENCE = /^(`{3,}|~{3,})(.*)$/;

/** The text of the nearest non-blank line above line `no`, or ''. */
function lineAbove(lines, no) {
  const above = lines.slice(0, no - 1).findLast(line => line.text.trim() !== '');
  return above?.text ?? '';
}

/** True when the fence match closes the open block: the same character, at least as long, and nothing after it. */
const closes = (found, open) => found !== null && found[1][0] === open.char && found[1].length >= open.length && found[2].trim() === '';

/** The block made of an open fence and the line it ends at. `content` is its lines each ended by a line feed (a block of no line is ''). */
const finished = (open, closeNo) => ({ openNo: open.openNo, closeNo, info: open.info, content: open.content.map(text => `${text}\n`).join(''), before: open.before });

const opened = (lines, line, found) => ({ openNo: line.no, char: found[1][0], length: found[1].length, info: found[2].trim(), content: [], before: lineAbove(lines, line.no) });

/** A line outside a fence: it may open one. */
const outside = (state, lines, line, found) => (found === null ? state : { ...state, open: opened(lines, line, found) });

/** A line inside a fence: it closes it or is one of its lines. */
function inside(state, line, found) {
  if (closes(found, state.open)) return { blocks: [...state.blocks, finished(state.open, line.no)], open: null };
  return { ...state, open: { ...state.open, content: [...state.open.content, line.text] } };
}

/** A line the fence scan keeps (outside every fence): a fence still open there ended with its list item. */
const kept = (state, line) => (state.open === null ? state : { blocks: [...state.blocks, finished(state.open, line.no - 1)], open: null });

function step(state, lines, line, isKept) {
  if (isKept) return kept(state, line);
  const found = FENCE.exec(line.text.trimStart());
  return state.open === null ? outside(state, lines, line, found) : inside(state, line, found);
}

/** The fenced code blocks of `lines`: `{ openNo, closeNo, info, content, before }`; `before` is the nearest non-blank line above the opener (its text). */
export function fencedBlocks(lines) {
  const keptNos = new Set(scanBlocks(lines).kept.map(line => line.no));
  return lines.reduce((state, line) => step(state, lines, line, keptNos.has(line.no)), { blocks: [], open: null }).blocks;
}

/** Adds a pair to the collapsed pairs unless it is a space that starts the text or follows a space. */
function squeeze(out, pair) {
  if (pair[0] !== ' ' || (out.length > 0 && out.at(-1)[0] !== ' ')) out.push(pair);
  return out;
}

/** The characters of a line as `[char, lineNo]` pairs, whitespace as one space, and one space for the line's end. */
const pairsOf = part => [...[...part.text].map(char => [/\s/.test(char) ? ' ' : char, part.no]), [' ', part.no]];

const clamp = (index, length) => Math.min(Math.max(index, 0), Math.max(length - 1, 0));

/** `text` with every run of whitespace made one space and the ends trimmed, with `lineAt(index)` giving the document line of the character at `index` of the result. `parts` are `{ no, text }` lines. */
export function collapse(parts) {
  const squeezed = parts.flatMap(pairsOf).reduce(squeeze, []);
  const pairs = squeezed.at(-1)?.[0] === ' ' ? squeezed.slice(0, -1) : squeezed;
  return { text: pairs.map(pair => pair[0]).join(''), lineAt: index => pairs[clamp(index, pairs.length)]?.[1] ?? 1 };
}

/** The collapsed text alone. */
export const collapsed = text => collapse([{ no: 1, text }]).text;

/** A line joins the paragraph under way when it is on the next line number; otherwise it starts a new one. */
function addLine(paragraphs, line) {
  const current = paragraphs.at(-1);
  if (current !== undefined && current.endNo + 1 === line.no) {
    current.lines.push(line);
    current.endNo = line.no;
    return paragraphs;
  }
  return [...paragraphs, { startNo: line.no, endNo: line.no, lines: [line] }];
}

/** The paragraphs of the lines outside fences: runs of non-blank lines on adjacent line numbers, `{ startNo, endNo, lines, isTable }`. */
export function paragraphs(lines) {
  const filled = scanBlocks(lines).kept.filter(line => line.text.trim() !== '');
  return filled.reduce(addLine, []).map(entry => ({ ...entry, isTable: entry.lines.every(line => line.text.startsWith('|')) }));
}
