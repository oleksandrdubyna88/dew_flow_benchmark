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
/** The forms a kinds cell's condition may take (2.1): anything else is a cell the reader cannot read exactly. */
const CONDITION_FORMS = /^(?:unless \S|instead of F-[A-Z0-9-]+ when \S|whose \S|that \S|the default$)/i;
/** The ids the checker's own rules name, and how many `text` blocks each sentence has (one, but F-HIDDEN's two). */
export const NAMED_IDS = Object.freeze(['F-REQ', 'F-INVALID', 'F-ERRORS', 'F-BYTES-O', 'F-BYTES-O-N', 'F-READY', 'F-STOP', 'F-EOF', 'F-TIMING', 'F-PORT', 'F-JSON', 'F-HIDDEN', 'F-SAMPLES-O', 'F-SAMPLES-D']);
const EXPECTED_PARTS = Object.freeze({ 'F-HIDDEN': 2 });

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

const unknownSlot = name => fail(`the sentence template has the slot {${name}}, which the checker does not know (it knows ${Object.keys(SLOT_PATTERNS).join(', ')})`);

/** The pattern of a slot: the first use is a named group, a repeated use must repeat the first value. */
function slotSource(name, seen) {
  const repeated = seen.has(name);
  seen.add(name);
  return repeated ? `\\k<${name}>` : `(?<${name}>${SLOT_PATTERNS[name]})`;
}

const longestOf = pieces => pieces.reduce((longest, piece) => (piece.length > longest.length ? piece : longest), '');

/** `{ regex, slots, fingerprints }` for a sentence template (collapsed text with `{slot}` holes), or a fail naming the slot the checker does not know. */
export function compileTemplate(template) {
  const pieces = template.split(/\{([A-Za-z]+)\}/);
  const slotNames = pieces.filter((_, index) => index % 2 === 1);
  const unknown = slotNames.find(name => SLOT_PATTERNS[name] === undefined);
  if (unknown !== undefined) return unknownSlot(unknown);
  const seen = new Set();
  const source = pieces.map((piece, index) => (index % 2 === 0 ? escapeRegExp(piece) : slotSource(piece, seen))).join('');
  return ok({ regex: new RegExp(`(?<![A-Za-z0-9])${source}(?![A-Za-z0-9])`, 'g'), slots: [...seen], fingerprints: fingerprintsOf(longestOf(pieces.filter((_, index) => index % 2 === 0))) });
}

/** Every place `text` (collapsed) holds the compiled sentence: `{ index, end, slots }`. */
export const findAll = (compiled, text) => [...text.matchAll(compiled.regex)].map(hit => ({ index: hit.index, end: hit.index + hit[0].length, slots: { ...hit.groups } }));

function splitCondition(lower) {
  const found = CONDITION.exec(lower);
  return found === null ? { head: lower, condition: '' } : { head: lower.slice(0, found.index), condition: lower.slice(found.index).trim() };
}

const kindWords = head => head.split(/[ ,]+/).filter(word => word !== '' && word !== 'and');
const knownKindWords = words => words.length > 0 && words.every(word => word === 'all' || KINDS.includes(word));
const isRequired = condition => condition === '' || condition === 'the default';
const readableCondition = condition => condition === '' || CONDITION_FORMS.test(condition);

/** The kinds a table cell names and whether the sentence is required for them: plain kind words are required, a condition (`whose`, `unless`, `instead`, `that`) makes it optional, `the default` does not. */
export function parseKinds(cell) {
  const { head, condition } = splitCondition(cell.toLowerCase());
  const words = kindWords(head);
  if (!knownKindWords(words) || !readableCondition(condition)) return null;
  return { kinds: new Set(words.includes('all') ? KINDS : words), required: isRequired(condition) };
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

/** ok(row) for one row of 2.1's table: `{ id, kinds, required, where }`, or a fail naming what cannot be read. */
function rowOf(row) {
  const [id, kindsCell, whereCell] = cellsOf(row.line);
  const kinds = parseKinds(kindsCell ?? '');
  const where = parsePlacement(whereCell ?? '');
  if (kinds === null) return fail(`FORMAT 2.1: the kinds of ${id} ("${kindsCell}") are not words the checker knows`);
  return where === null ? fail(`FORMAT 2.1: where ${id} stands ("${whereCell}") is not a form the checker can read`) : ok({ id, ...kinds, where });
}

/** The first fail of `results`, else `ok(make(values))`. */
const allOk = (results, make) => results.find(result => !result.ok) ?? ok(make(results.map(result => result.value)));

/** ok(rows) of the lines of 2.1's table, or a fail when the section or the table is not there. */
function tableLines(format) {
  const heading = headingByNumber(format, '2.1');
  if (heading === undefined) return fail('FORMAT has no section 2.1');
  const { start, end } = sliceFrom(format, heading, 3);
  const table = tableOf(format.text.slice(start, end));
  return !table.found || table.rows.length === 0 ? fail('FORMAT 2.1 has no table of fixed sentences') : ok(table.rows);
}

/** ok(rows) of 2.1's table as `{ id, kinds, required, where }`, or a fail naming the row that cannot be read. */
function readTable(format) {
  const lines = tableLines(format);
  const rows = lines.ok ? allOk(lines.value.map(rowOf), parsed => parsed) : lines;
  return rows.ok ? resolveAliases(rows.value) : rows;
}

/** ok(row) with an `as F-X` placement replaced by F-X's own, or a fail when F-X is not a row or is itself an alias. */
function resolveOne(row, byId) {
  if (row.where.alias === undefined) return ok(row);
  const target = byId.get(row.where.alias);
  return target === undefined || target.where.alias !== undefined ? fail(`FORMAT 2.1: ${row.id} stands "as ${row.where.alias}", which is not a row with a placement of its own`) : ok({ ...row, where: target.where });
}

/** The rows with every `as F-X` placement replaced by F-X's own; the failing result when one cannot be. */
function resolveAliases(rows) {
  const byId = new Map(rows.map(row => [row.id, row]));
  return allOk(rows.map(row => resolveOne(row, byId)), resolved => resolved);
}

/** The kinds in `(…)` right after the id a paragraph starts with, or null. */
function variantKinds(text, startsWithId) {
  const listed = startsWithId ? /^\S+\s*\(([a-z, ]+)\)/.exec(text)?.[1] : undefined;
  const words = (listed ?? '').split(/[ ,]+/).filter(word => KINDS.includes(word));
  return words.length > 0 ? new Set(words) : null;
}

/** The id a paragraph labels: the one it starts with, else the last one it names; with the kinds in `(…)` right after a starting id. */
function leadOf(text, known) {
  const first = new RegExp(`^(${IDS})`).exec(text)?.[1];
  if (first !== undefined && !known.has(first)) return { id: first, kinds: null, unknown: true };
  return knownLead(text, known, first);
}

function knownLead(text, known, first) {
  const named = [...text.matchAll(ID)].map(hit => hit[0]).filter(id => known.has(id));
  if (named.length === 0) return null;
  const id = known.has(first) ? first : named.at(-1);
  return { id, kinds: variantKinds(text, id === first) };
}

const insideOf = range => line => line.start >= range.start && line.start < range.end;

const leadItems = (format, inside, known) => paragraphs(format.lines).filter(p => !p.isTable && inside(p.lines[0]))
  .map(p => ({ no: p.startNo, lead: leadOf(collapsed(p.lines.map(line => line.text).join(' ')), known) }));

const blockItems = (format, inside) => fencedBlocks(format.lines).filter(b => b.info === 'text' && inside(format.lines[b.openNo - 1])).map(b => ({ no: b.openNo, block: b }));

/** A lead starts a new group of blocks; a block joins the group of the lead above it. */
function addItem(state, item) {
  if (item.lead) {
    const lead = { ...item.lead, parts: [] };
    return { current: lead, leads: [...state.leads, lead] };
  }
  if (item.block !== undefined && state.current !== null) state.current.parts.push(collapsed(item.block.content));
  return state;
}

const byIdOf = leads => leads.filter(lead => lead.parts.length > 0 && !lead.unknown).reduce((map, lead) => map.set(lead.id, [...(map.get(lead.id) ?? []), { kinds: lead.kinds, parts: lead.parts }]), new Map());

/** The sentence leads of section 2 with their `text` blocks: `{ byId: Map(id -> [{ kinds, parts: [collapsed text] }]), strays }`, `strays` the ids of labelled blocks that have no row. */
function readBlocks(format, ids) {
  const inside = insideOf(sliceFrom(format, headingByNumber(format, '2'), 2));
  const items = [...leadItems(format, inside, new Set(ids)), ...blockItems(format, inside)].sort((a, b) => a.no - b.no);
  const leads = items.reduce(addItem, { current: null, leads: [] }).leads;
  return { byId: byIdOf(leads), strays: leads.filter(lead => lead.unknown && lead.parts.length > 0).map(lead => lead.id) };
}

/** ok(variant) with its parts compiled, or the fail of the first part that cannot be. */
function compileVariant(variant, id) {
  const parts = variant.parts.map(template => ({ template, compiled: compileTemplate(template) }));
  const bad = parts.find(part => !part.compiled.ok);
  return bad === undefined ? ok({ kinds: variant.kinds, parts: parts.map(part => ({ template: part.template, ...part.compiled.value })) }) : fail(`${id}: ${bad.compiled.reason}`);
}

const blocksText = count => `${count} text block${count === 1 ? '' : 's'}`;

/** The reader's expectations of the blocks of one sentence: every variant has the expected number of parts, and several variants name disjoint kinds. */
const kindsOfVariant = variant => [...(variant.kinds ?? KINDS)];

function overlapFault(id, found) {
  const kinds = found.flatMap(kindsOfVariant);
  return found.length > 1 && new Set(kinds).size !== kinds.length ? `${id} has two text blocks for the same kinds` : null;
}

function blockFault(id, found) {
  const wrong = found.find(variant => variant.parts.length !== (EXPECTED_PARTS[id] ?? 1));
  return wrong === undefined ? overlapFault(id, found) : `${id} has ${blocksText(wrong.parts.length)}, and 2.1 expects ${EXPECTED_PARTS[id] ?? 1}`;
}

function sentenceOf(row, blocks) {
  const found = blocks.get(row.id);
  if (found === undefined) return fail(`FORMAT has no \`text\` block for the sentence ${row.id} of 2.1's table`);
  const fault = blockFault(row.id, found);
  return fault === null ? allOk(found.map(variant => compileVariant(variant, row.id)), variants => ({ ...row, variants })) : fail(fault);
}

/**
 * ok(Map(id -> { id, kinds, required, where, variants: [{ kinds, parts: [{ template, compiled }] }] })) for every id of 2.1's table, or a fail: a row or a placement that cannot be read, an id with no
 * `text` block, a slot the checker does not know.
 */
export function readSentences(format) {
  const rows = readTable(format);
  if (!rows.ok) return rows;
  const lacking = NAMED_IDS.find(id => !rows.value.some(row => row.id === id));
  if (lacking !== undefined) return fail(`FORMAT 2.1 has no row for ${lacking}, which the checker names`);
  const { byId, strays } = readBlocks(format, rows.value.map(row => row.id));
  if (strays.length > 0) return fail(`FORMAT has a text block labelled ${strays[0]} and no row for it in 2.1's table`);
  return allOk(rows.value.map(row => sentenceOf(row, byId)), sentences => new Map(sentences.map(sentence => [sentence.id, sentence])));
}


/** The variant of `entry` that applies to `kind` (one that names no kinds applies to all), or undefined. */
export const variantFor = (entry, kind) => entry.variants.find(variant => variant.kinds === null || variant.kinds.has(kind));

/** The section number `entry` stands in for `kind`; null for the preamble. */
export function sectionOf(entry, kind) {
  const { where } = entry;
  return where.mode === 'preamble' ? null : where.section ?? where.sections[kind] ?? null;
}
