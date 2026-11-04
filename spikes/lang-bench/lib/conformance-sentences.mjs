// The fixed sentences of FORMAT.md, read from the document itself (E1.S8): 2.1's table says which sentence is required for which kind and where it stands, and the sentence's own
// text is the `text` block that follows its label in section 2. Nothing here retypes a sentence: a FORMAT that changes one word changes what the checker demands, and a FORMAT that
// lacks a sentence, a placement it can read or a slot it knows makes the reader fail (the checker then exits 4 and passes no spec).
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { collapsed, fencedBlocks, paragraphs } from './conformance-blocks.mjs';
import { tableOf } from './format-compare.mjs';
import { headingByNumber, sliceFrom } from './format-structure.mjs';

export const KINDS = Object.freeze(['olympiad', 'daemon', 'web']);
const ID = /F-[A-Z0-9]+(?:-[A-Z0-9]+)*/g;
const CONDITION = /\b(?:unless|instead|whose|that|the default)\b/;

const RANGE = 'R[1-9]\\d*–R[1-9]\\d*';
const ITEM = 'R[1-9]\\d*(?:–R[1-9]\\d*)?';
const NUMBER = '[1-9]\\d*';
/** The values each slot allows (FORMAT 2.1 to 2.5): the sentence's own text fixes everything else. */
export const SLOT_PATTERNS = Object.freeze({
  ref: `(?:R[1-9]\\d*|any of (?:${RANGE}|${ITEM}(?:, ${ITEM})* and ${ITEM}))`,
  eref: `(?:${ITEM}(?:, ${ITEM})* and ${ITEM}|${ITEM})`,
  n: NUMBER,
  k: '(?:\\d+|[a-z]+(?:-[a-z]+)?)',
  T: NUMBER,
  G: NUMBER,
  V: NUMBER,
  duration: NUMBER,
  D: '(?:10|5)',
  what: '(?:readiness|the grace period)',
  which: 'A test(?: that does not end at the end of its input)?',
  readiness: 'it accepts a TCP connection on that port|it has written the line `[^`\\n]+` to standard output',
  reason: '.+?',
  subject: '[^.]+?',
  size: '.+?',
  normalisations: '.+?',
});

const escapeRegExp = text => text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
const wordsOf = text => text.split(' ').filter(word => word !== '');

/** The first and the last words (four, fewer in a short sentence) of the longest fixed stretch of a sentence: where either stands, the sentence is there even if a word inside it was changed. */
function fingerprintsOf(literal) {
  const words = wordsOf(literal.trim());
  const edge = Math.min(4, Math.max(2, Math.floor(words.length / 2)));
  return [...new Set([words.slice(0, edge).join(' '), words.slice(-edge).join(' ')])].filter(text => text !== '');
}

/** `{ regex, slots, fingerprints }` for a sentence template (collapsed text with `{slot}` holes), or a fail naming the slot the checker does not know. */
export function compileTemplate(template) {
  const pieces = template.split(/\{([A-Za-z]+)\}/);
  const seen = new Set();
  let source = '(?<![A-Za-z0-9])';
  let longest = '';
  for (const [index, piece] of pieces.entries()) {
    if (index % 2 === 0) {
      source += escapeRegExp(piece);
      if (piece.length > longest.length) longest = piece;
    } else if (SLOT_PATTERNS[piece] === undefined) {
      return fail(`the sentence template has the slot {${piece}}, which the checker does not know (it knows ${Object.keys(SLOT_PATTERNS).join(', ')})`);
    } else {
      source += seen.has(piece) ? `\\k<${piece}>` : `(?<${piece}>${SLOT_PATTERNS[piece]})`;
      seen.add(piece);
    }
  }
  return ok({ regex: new RegExp(source, 'g'), slots: [...seen], fingerprints: fingerprintsOf(longest) });
}

/** Every place `text` (collapsed) holds the compiled sentence: `{ index, end, slots }`. */
export const findAll = (compiled, text) => [...text.matchAll(compiled.regex)].map(hit => ({ index: hit.index, end: hit.index + hit[0].length, slots: { ...hit.groups } }));

/** The kinds a table cell names and whether the sentence is required for them: plain kind words are required, a condition (`whose`, `unless`, `instead`, `that`) makes it optional, `the default` does not. */
export function parseKinds(cell) {
  const lower = cell.toLowerCase();
  const found = CONDITION.exec(lower);
  const head = found === null ? lower : lower.slice(0, found.index);
  const condition = found === null ? '' : lower.slice(found.index).trim();
  const words = head.split(/[ ,]+/).filter(word => word !== '' && word !== 'and');
  if (words.length === 0 || words.some(word => word !== 'all' && !KINDS.includes(word))) return null;
  return { kinds: new Set(words.includes('all') ? KINDS : words), required: condition === '' || condition === 'the default' };
}

const IDS = 'F-[A-Z0-9]+(?:-[A-Z0-9]+)*';
const PLACEMENTS = Object.freeze([
  [/^before section 1, its own paragraph$/, () => ({ mode: 'preamble' })],
  [/^section (\d+) \(([a-z, ]+)\) or (\d+) \(([a-z, ]+)\), in an R$/, m => ({ mode: 'in-r', sections: { ...forKinds(m[2], m[1]), ...forKinds(m[4], m[3]) }, sameAs: null, after: false })],
  [/^section (\d+), in an R$/, m => ({ mode: 'in-r', sections: forKinds('olympiad, daemon, web', m[1]), sameAs: null, after: false })],
  [new RegExp(`^section (\\d+), in the same R as (${IDS}(?: or ${IDS})*)(, after it)?$`), m => ({ mode: 'in-r', sections: forKinds('olympiad, daemon, web', m[1]), sameAs: m[2].split(' or '), after: m[3] !== undefined })],
  [/^section (\d+), first paragraph$/, m => ({ mode: 'first-paragraph', section: Number(m[1]) })],
  [/^section (\d+), in an R: the first sentence opens the R, the second closes it$/, m => ({ mode: 'opens-closes', sections: forKinds('olympiad, daemon, web', m[1]) })],
]);
const forKinds = (list, section) => Object.fromEntries(list.split(/[ ,]+/).filter(word => KINDS.includes(word)).map(kind => [kind, Number(section)]));

/** The placement a `Where` cell says, `{ alias }` for `as F-X`, or null when the checker cannot read it. */
export function parsePlacement(cell) {
  const alias = new RegExp(`^as (${IDS})$`).exec(cell);
  if (alias !== null) return { alias: alias[1] };
  for (const [pattern, make] of PLACEMENTS) {
    const found = pattern.exec(cell);
    if (found !== null) return make(found);
  }
  return null;
}

const cellsOf = line => line.split('|').slice(1, -1).map(cell => cell.trim());

/** ok(rows) of 2.1's table as `{ id, kinds, required, where }`, or a fail naming the row that cannot be read. */
function readTable(format) {
  const heading = headingByNumber(format, '2.1');
  if (heading === undefined) return fail('FORMAT has no section 2.1');
  const { start, end } = sliceFrom(format, heading, 3);
  const table = tableOf(format.text.slice(start, end));
  if (!table.found || table.rows.length === 0) return fail('FORMAT 2.1 has no table of fixed sentences');
  const rows = [];
  for (const row of table.rows) {
    const [id, kindsCell, whereCell] = cellsOf(row.line);
    const kinds = parseKinds(kindsCell ?? '');
    const where = parsePlacement(whereCell ?? '');
    if (kinds === null) return fail(`FORMAT 2.1: the kinds of ${id} ("${kindsCell}") are not words the checker knows`);
    if (where === null) return fail(`FORMAT 2.1: where ${id} stands ("${whereCell}") is not a form the checker can read`);
    rows.push({ id, ...kinds, where });
  }
  return resolveAliases(rows);
}

/** The rows with every `as F-X` placement replaced by F-X's own, or a fail when F-X is not a row or is itself an alias. */
function resolveAliases(rows) {
  const byId = new Map(rows.map(row => [row.id, row]));
  const resolved = [];
  for (const row of rows) {
    if (row.where.alias === undefined) { resolved.push(row); continue; }
    const target = byId.get(row.where.alias);
    if (target === undefined || target.where.alias !== undefined) return fail(`FORMAT 2.1: ${row.id} stands "as ${row.where.alias}", which is not a row with a placement of its own`);
    resolved.push({ ...row, where: target.where });
  }
  return ok(resolved);
}

/** The id a paragraph labels: the one it starts with, else the last one it names; with the kinds in `(…)` right after a starting id. */
function leadOf(text, known) {
  const named = [...text.matchAll(ID)].map(hit => hit[0]).filter(id => known.has(id));
  if (named.length === 0) return null;
  const first = new RegExp(`^(${IDS})`).exec(text)?.[1];
  const id = known.has(first) ? first : named.at(-1);
  const listed = id === first ? /^\S+\s*\(([a-z, ]+)\)/.exec(text)?.[1] : undefined;
  const words = listed === undefined ? [] : listed.split(/[ ,]+/).filter(word => KINDS.includes(word));
  return { id, kinds: words.length > 0 ? new Set(words) : null };
}

/** The sentence leads of section 2 with their `text` blocks: `Map(id -> [{ kinds, parts: [collapsed text] }])`, in document order. */
function readBlocks(format, ids) {
  const section = headingByNumber(format, '2');
  const { start, end } = sliceFrom(format, section, 2);
  const inside = line => line.start >= start && line.start < end;
  const known = new Set(ids);
  const items = [
    ...paragraphs(format.lines).filter(p => !p.isTable && inside(p.lines[0])).map(p => ({ no: p.startNo, lead: leadOf(collapsed(p.lines.map(l => l.text).join(' ')), known) })),
    ...fencedBlocks(format.lines).filter(b => b.info === 'text' && inside(format.lines[b.openNo - 1])).map(b => ({ no: b.openNo, block: b })),
  ].sort((a, b) => a.no - b.no);
  const leads = new Map();
  let current = null;
  for (const item of items) {
    if (item.lead !== undefined && item.lead !== null) {
      current = { ...item.lead, parts: [] };
      leads.set(item.no, current);
    } else if (item.block !== undefined && current !== null) current.parts.push(collapsed(item.block.content));
  }
  const byId = new Map();
  for (const lead of leads.values()) if (lead.parts.length > 0) byId.set(lead.id, [...(byId.get(lead.id) ?? []), { kinds: lead.kinds, parts: lead.parts }]);
  return byId;
}

/**
 * ok(Map(id -> { id, kinds, required, where, variants: [{ kinds, parts: [{ template, compiled }] }] })) for every id of 2.1's table, or a fail: a row or a placement that cannot be read, an id with no
 * `text` block, a slot the checker does not know.
 */
export function readSentences(format) {
  const rows = readTable(format);
  if (!rows.ok) return rows;
  const blocks = readBlocks(format, rows.value.map(row => row.id));
  const sentences = new Map();
  for (const row of rows.value) {
    const found = blocks.get(row.id);
    if (found === undefined) return fail(`FORMAT has no \`text\` block for the sentence ${row.id} of 2.1's table`);
    const variants = [];
    for (const variant of found) {
      const parts = variant.parts.map(template => ({ template, compiled: compileTemplate(template) }));
      const bad = parts.find(part => !part.compiled.ok);
      if (bad !== undefined) return fail(`${row.id}: ${bad.compiled.reason}`);
      variants.push({ kinds: variant.kinds, parts: parts.map(part => ({ template: part.template, ...part.compiled.value })) });
    }
    sentences.set(row.id, { ...row, variants });
  }
  return ok(sentences);
}

/** The variant of `entry` that applies to `kind` (one that names no kinds applies to all), or undefined. */
export const variantFor = (entry, kind) => entry.variants.find(variant => variant.kinds === null || variant.kinds.has(kind));

/** The section number `entry` stands in for `kind`; null for the preamble. */
export function sectionOf(entry, kind) {
  const { where } = entry;
  return where.mode === 'preamble' ? null : where.section ?? where.sections[kind] ?? null;
}
