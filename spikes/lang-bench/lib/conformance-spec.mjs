// A spec read as the conformance check needs it (E1.S8): the lines outside fences, the `##` sections, the requirement paragraphs with their spans, the tables and fenced blocks.
// The byte rules and the block scan are FORMAT's own reader (format-structure.mjs, E1.S9): a spec it refuses (a carriage return, a byte order mark, a form CommonMark reads otherwise)
// is not read at all, and the check says why.
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { collapse, fencedBlocks, paragraphs } from './conformance-blocks.mjs';
import { parseFormat, unfencedLines } from './format-structure.mjs';

const R_START = /^\*\*R([1-9]\d*)\.\*\*(?=\s|$)/;
const SECTION = /^(\d+)\. /;

/** The section number whose `##` heading is the last at or before line `no`; 0 before the first. */
function sectionIndex(sections) {
  return no => sections.findLast(section => section.no <= no)?.number ?? 0;
}

const ATX = /^(#{1,6})(?:[ \t]+(.*)|$)/;

/** Every ATX heading of any level outside fences: `{ level, text, no }`. A span ends at any of them. */
const atxOf = kept => kept.flatMap(line => {
  const found = ATX.exec(line.text);
  return found === null ? [] : [{ level: found[1].length, text: (found[2] ?? '').trim(), no: line.no }];
});

const inRanges = (ranges, no) => ranges.some(range => no >= range.startNo && no <= range.endNo);

/** The requirement paragraphs: `{ n, no, startNo, endNo, section, text, lineAt, lines }`; `text` is the span collapsed, from the id on, without the lines of a table (a sentence in a table cell is not a sentence of the requirement). */
function requirementsOf({ kept, atx, tables, lastNo, sectionAt }) {
  const starts = kept.flatMap(line => (R_START.test(line.text) ? [{ n: Number(R_START.exec(line.text)[1]), no: line.no }] : []));
  return starts.map((start, index) => {
    const nextStart = starts[index + 1]?.no ?? lastNo + 1;
    const nextHeading = atx.find(heading => heading.no > start.no)?.no ?? lastNo + 1;
    const endNo = Math.min(nextStart, nextHeading) - 1;
    const lines = kept.filter(line => line.no >= start.no && line.no <= endNo && !inRanges(tables, line.no));
    return { ...start, startNo: start.no, endNo, section: sectionAt(start.no), lines, ...collapse(lines) };
  });
}

/** ok(spec) for a spec's bytes, or a fail with the `line` and the reason the document cannot be read. */
export function parseSpec(bytes) {
  const parsed = parseFormat(bytes, { structure: () => null });
  if (!parsed.ok) return fail(parsed.reason, { line: parsed.line });
  const { lines, headings, text } = parsed.format;
  const kept = unfencedLines(lines);
  const sections = headings.filter(heading => heading.level === 2).map(heading => ({ no: heading.no, title: `## ${heading.text}`, number: SECTION.test(heading.text) ? Number(SECTION.exec(heading.text)[1]) : null }));
  const sectionAt = sectionIndex(sections);
  const lastNo = lines.at(-1).no;
  const blocks = fencedBlocks(lines);
  const tables = paragraphs(lines).filter(paragraph => paragraph.isTable);
  const atx = atxOf(kept);
  const spec = {
    text,
    lines,
    kept,
    headings: headings.map(heading => ({ level: heading.level, text: heading.text, no: heading.no })),
    sections,
    sectionAt,
    atx,
    requirements: requirementsOf({ kept, atx, tables, lastNo, sectionAt }),
    paragraphs: paragraphs(lines),
    tables,
    blocks,
    lastNo,
  };
  return ok(spec);
}

/** The requirement whose span holds line `no`, or undefined. */
export const requirementAt = (spec, no) => spec.requirements.find(entry => no >= entry.startNo && no <= entry.endNo);
