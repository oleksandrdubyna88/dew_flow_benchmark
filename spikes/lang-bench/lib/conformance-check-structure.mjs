// Checks 1 to 4 (E1.S8): the file and its title, the header rows, the eight sections of the kind and the amendments, and the requirement ids with their spans. Each returns its findings as
// text; an empty list is `ok`. FORMAT's facts (the name pattern, the placeholders, the kind phrases, the headings) come from conformance-facts.mjs, read from the document.
import { wholeFileRefusals } from './format-grammar.mjs';
import { requirementAt } from './conformance-spec.mjs';

const TITLE = /^# Task (\d{2}) — \S.*$/;
const SEPARATOR = /^\|\s*-+\s*\|\s*-+\s*\|$/;
const DATE = /^\d{4}-\d{2}-\d{2}$/;
const ITEM = /^- A(\d+) · (\d{4}-\d{2}-\d{2}) · ([^—]+?) — \S/;
const cellsOf = line => line.split('|').slice(1, -1).map(cell => cell.trim());

/** FORMAT's own whole-file rules (E1.S9), run on the spec: no raw inline HTML, no reference definition, no line that starts with `<`. */
const markupProblems = spec => wholeFileRefusals({ lines: spec.lines }).map(entry => `line ${entry.no}: ${entry.form}`);

const nameProblems = ctx => (ctx.facts.namePattern.test(`${ctx.task}.md`) ? [] : [`the file name ${ctx.task}.md does not match ${ctx.facts.namePattern}`]);

function titleProblems(ctx) {
  const title = TITLE.exec(ctx.spec.lines[0].text);
  if (title === null) return ['line 1 is not the title `# Task NN — <Title>` (NN two digits, an em dash)'];
  return title[1] === ctx.task.slice(0, 2) ? [] : [`line 1: the title says task ${title[1]}, and the file name says ${ctx.task.slice(0, 2)}`];
}

/** Check 1: the file name matches FORMAT 1.1, the title is `# Task NN — <Title>` with the file's NN, and the text holds none of FORMAT's markup faults (raw inline HTML, a reference definition). */
export const fileFindings = ctx => [...nameProblems(ctx), ...titleProblems(ctx), ...markupProblems(ctx.spec)];

const emptyHeadProblems = head => (cellsOf(head.text).length === 2 && cellsOf(head.text).every(cell => cell === '') ? [] : [`line ${head.no}: the header row of the table must be empty (\`| | |\`)`]);
const separatorProblems = (head, separator) => (SEPARATOR.test(separator?.text ?? '') ? [] : [`line ${separator?.no ?? head.no}: the second line of the header table must be the separator \`|---|---|\``]);
const rowOf = line => ({ label: cellsOf(line.text)[0], value: cellsOf(line.text)[1] ?? '', no: line.no });

/** The header table: `{ rows: [{ label, value, no }], problems }`. It is the table that starts the first non-blank line under the title, with an empty header row, a separator and five rows. */
export function headerRows(spec) {
  const first = spec.lines.find(line => line.no > 1 && line.text.trim() !== '');
  const table = spec.tables.find(entry => entry.startNo === first?.no);
  if (table === undefined) return { rows: [], problems: ['directly under the title there is no two-column table of the header rows'] };
  const [head, separator, ...rest] = table.lines;
  return { rows: rest.map(rowOf), problems: [...emptyHeadProblems(head), ...separatorProblems(head, separator)] };
}

/** The header rows' later forms: the value patterns FORMAT 1.2 gives for each row once the gate and the freeze have happened. */
function laterForm(label, facts) {
  const verdict = new RegExp(`^\`(?:${facts.header.verdicts.join('|')})\` · \\d{4}-\\d{2}-\\d{2}$`);
  return {
    'Gate verdict': value => verdict.test(value) && DATE.test(value.slice(-10)),
    'Freeze tag': value => /^`spec-frozen(?:-[AB])?-\d{8}`$/.test(value),
    Amendments: value => /^A1 \(section 8\)$/.test(value) || /^A1–A(?:[2-9]|[1-9]\d+) \(section 8\)$/.test(value),
  }[label];
}

const placeholderOf = (row, facts) => facts.header.placeholders[row.label];
const placeholderOrLater = (row, facts) => row.value === placeholderOf(row, facts) || laterForm(row.label, facts)(row.value);
const VALUE_RULES = {
  Kind: (row, facts) => Object.values(facts.header.kindPhrases).includes(row.value),
  'Freeze rule': (row, facts) => row.value === placeholderOf(row, facts),
};
const valueGood = (row, facts) => (VALUE_RULES[row.label] ?? placeholderOrLater)(row, facts);

function badValue(row, facts) {
  const placeholder = placeholderOf(row, facts);
  const meaning = placeholder === undefined ? 'one of the three kind phrases' : `its placeholder "${placeholder}"`;
  return `line ${row.no}: the value of ${row.label} is "${row.value}", which is neither ${meaning}${laterForm(row.label, facts) === undefined ? '' : ' nor its later form'}`;
}

const inOrder = (rows, labels) => rows.length === labels.length && rows.every((row, index) => row.label === labels[index]);

/** True when `text` (`YYYY-MM-DD` or `YYYYMMDD`) is a day of the calendar. */
export function isRealDate(text) {
  const [year, month, day] = text.length === 8 ? [text.slice(0, 4), text.slice(4, 6), text.slice(6)] : text.split('-');
  const date = new Date(Date.UTC(Number(year), Number(month) - 1, Number(day)));
  return date.getUTCFullYear() === Number(year) && date.getUTCMonth() === Number(month) - 1 && date.getUTCDate() === Number(day);
}

const dateOf = value => /(\d{4}-\d{2}-\d{2})`?$/.exec(value)?.[1];
const tagParts = value => /^`spec-frozen(?:-([A-Z]))?-(\d{8})`$/.exec(value);
const notReal = (row, date) => `line ${row.no}: ${date} is not a real date`;

const verdictDateProblems = row => (dateOf(row.value) !== undefined && !isRealDate(dateOf(row.value)) ? [notReal(row, dateOf(row.value))] : []);
const letterProblems = (row, letter) => (letter !== undefined && !['A', 'B'].includes(letter) ? [`line ${row.no}: the group letter ${letter} of the freeze tag is none of A, B`] : []);
const tagDateProblems = (row, parts) => [...letterProblems(row, parts[1]), ...(isRealDate(parts[2]) ? [] : [notReal(row, parts[2])])];
const tagProblems = row => (tagParts(row.value) === null ? [] : tagDateProblems(row, tagParts(row.value)));
const DATED = Object.freeze({ 'Gate verdict': verdictDateProblems, 'Freeze tag': tagProblems });

/** The problems of the dates and the tag a header row holds in its later form. */
const datedProblems = row => (DATED[row.label] ?? (() => []))(row);

/** A freeze tag stands only beside a gated verdict. */
function tagNeedsGate(rows, placeholders) {
  const tag = rows.find(row => row.label === 'Freeze tag');
  const gate = rows.find(row => row.label === 'Gate verdict');
  return tag.value !== placeholders['Freeze tag'] && gate.value === placeholders['Gate verdict'] ? [`line ${tag.no}: a freeze tag needs a gated verdict; the Gate verdict row says "${gate.value}"`] : [];
}

/** Check 2: five rows in order, each value its placeholder or its later form; `Kind` one of the three phrases; the `Freeze rule` sentence exact. */
export function headerFindings(ctx) {
  const { facts } = ctx;
  const { rows, problems } = headerRows(ctx.spec);
  if (!inOrder(rows, facts.header.labels)) return [...problems, `the header table must hold exactly the rows ${facts.header.labels.join(', ')} in this order; it holds ${rows.map(row => row.label).join(', ') || 'none'}`];
  const valueProblems = rows.filter(row => !valueGood(row, facts)).map(row => badValue(row, facts));
  return [...problems, ...valueProblems, ...rows.flatMap(datedProblems), ...tagNeedsGate(rows, facts.header.placeholders)];
}

/** The kind the Kind row names (`olympiad`, `daemon` or `web`), or null. */
export function kindOf(spec, facts) {
  const value = headerRows(spec).rows.find(row => row.label === 'Kind')?.value;
  return Object.entries(facts.header.kindPhrases).find(([, phrase]) => phrase === value)?.[0] ?? null;
}

const itemOf = line => {
  const found = ITEM.exec(line.text);
  return found === null ? null : { k: Number(found[1]), ids: [...found[3].matchAll(/R([1-9]\d*)/g)].map(hit => Number(hit[1])), no: line.no };
};
const continues = (line, items) => line.text.startsWith('  ') && items.length > 0;
const strayItem = line => `line ${line.no}: section 8 holds "None yet." or items \`- A<k> · <YYYY-MM-DD> · <R-ids> — <what>\`; this line is neither`;

const dateProblem = (item, line) => (isRealDate(ITEM.exec(line.text)[2]) ? [] : [`line ${line.no}: amendment A${item.k}: ${ITEM.exec(line.text)[2]} is not a real date`]);

function addAmendmentLine(state, line) {
  const item = itemOf(line);
  if (item !== null) return { ...state, items: [...state.items, item], problems: [...state.problems, ...dateProblem(item, line)] };
  return continues(line, state.items) ? state : { ...state, problems: [...state.problems, strayItem(line)] };
}

const linesAfter = (spec, section) => spec.kept.filter(line => line.no > section.no && line.text.trim() !== '');

const EMPTY_8 = 'section 8 is empty: it holds `None yet.` or the amendment items';
const blocksIn8 = (spec, section) => [
  ...spec.blocks.filter(block => block.openNo > section.no).map(block => `line ${block.openNo}: section 8 holds a fenced code block; it holds \`None yet.\` or the amendment items only`),
  ...spec.tables.filter(table => table.startNo > section.no).map(table => `line ${table.startNo}: section 8 holds a table; it holds \`None yet.\` or the amendment items only`),
];

function itemsIn(spec, lines) {
  const found = lines.reduce(addAmendmentLine, { items: [], problems: [] });
  return { ...found, problems: lines.length === 0 ? [EMPTY_8] : found.problems };
}

/** Section 8: `{ none, items: [{ k, ids, no }], problems }`. */
export function amendmentsOf(spec) {
  const section = spec.sections.find(entry => entry.number === 8);
  if (section === undefined) return { none: false, items: [], problems: [] };
  const lines = linesAfter(spec, section);
  if (lines.length === 1 && lines[0].text === 'None yet.') return { none: true, items: [], problems: blocksIn8(spec, section) };
  const found = itemsIn(spec, lines);
  return { none: false, ...found, problems: [...found.problems, ...blocksIn8(spec, section)] };
}

const expectedRow = k => (k === 0 ? 'none yet' : k === 1 ? 'A1 (section 8)' : `A1–A${k} (section 8)`);
const countText = k => (k === 0 ? 'no amendment' : `${k} item${k === 1 ? '' : 's'}`);

function amendmentRowProblems(rows, amendments) {
  const row = rows.find(entry => entry.label === 'Amendments');
  if (row === undefined) return [];
  const k = amendments.items.length;
  return row.value === expectedRow(k) ? [] : [`line ${row.no}: the Amendments row says "${row.value}", and section 8 holds ${countText(k)} (it must say "${expectedRow(k)}")`];
}

function headingAt(expected, found, at) {
  if (found[at]?.title === expected[at]) return [];
  return [found[at] === undefined ? `section ${at + 1} is missing: expected "${expected[at]}"` : `line ${found[at].no}: expected "${expected[at] ?? 'no further ## section'}", found "${found[at].title}"`];
}

const headingProblems = (expected, found) => Array.from({ length: Math.max(expected.length, found.length) }, (_, at) => headingAt(expected, found, at)).flat();

function gapProblems(amendments) {
  const gap = amendments.items.findIndex((item, index) => item.k !== index + 1);
  return gap === -1 ? [] : [`line ${amendments.items[gap].no}: the amendments must be numbered A1, A2, … without a gap; A${amendments.items[gap].k} stands in place ${gap + 1}`];
}

const subOk = (section, kind) => (section >= 1 && section <= 3) || (section === 6 && kind !== 'olympiad');

/** FORMAT 1.4: `#` is the title alone; `###` is free in sections 1 to 3, and section 6 of a daemon or web spec has one per sample. */
function levelProblems(spec, kind) {
  return [
    ...spec.atx.filter(heading => heading.level === 1 && heading.no !== 1).map(heading => `line ${heading.no}: a # heading at line ${heading.no}; only the title on line 1 is level 1`),
    ...spec.atx.filter(heading => heading.level === 3 && !subOk(spec.sectionAt(heading.no), kind) && spec.sectionAt(heading.no) !== 6).map(heading => `line ${heading.no}: a ### heading in section ${spec.sectionAt(heading.no)}; ### is free in sections 1 to 3 only`),
  ];
}

/** Check 3: exactly the eight `##` headings of the kind in order; section 8 is `None yet.` or the items A1 … Ak without gaps, as many as the Amendments row says. */
export function sectionFindings(ctx) {
  const amendments = amendmentsOf(ctx.spec);
  return [
    ...headingProblems(ctx.facts.sections[ctx.kind], ctx.spec.sections),
    ...gapProblems(amendments),
    ...amendments.problems,
    ...levelProblems(ctx.spec, ctx.kind),
    ...amendmentRowProblems(headerRows(ctx.spec).rows, amendments),
  ];
}

const repeats = (entry, index, ids) => ids.findIndex(other => other.n === entry.n) < index;
const descends = (entry, index, ids, frozen) => !frozen && index > 0 && entry.n < ids[index - 1].n;

function entryProblems(entry, index, ids, frozen) {
  return [
    ...(repeats(entry, index, ids) ? [`line ${entry.no}: R${entry.n} stands more than once`] : []),
    ...(descends(entry, index, ids, frozen) ? [`line ${entry.no}: R${entry.n} stands after R${ids[index - 1].n}; before the freeze the ids ascend`] : []),
  ];
}

function idListProblems(ids, frozen) {
  const max = Math.max(0, ...ids.map(entry => entry.n));
  const gaps = Array.from({ length: max }, (_, at) => at + 1).filter(n => !ids.some(entry => entry.n === n));
  return [...ids.flatMap((entry, index) => entryProblems(entry, index, ids, frozen)), ...gaps.map(n => `R${n} is missing: the ids must be exactly R1 … R${max} without a gap`)];
}

/** The tables and fenced blocks that lie in no requirement's span, outside the header table, section 6 and section 8. */
function spanProblems(spec) {
  const header = spec.lines.find(line => line.no > 1 && line.text.trim() !== '')?.no;
  const elsewhere = no => [6, 8].includes(spec.sectionAt(no)) || requirementAt(spec, no) !== undefined;
  return [
    ...spec.tables.filter(table => table.startNo !== header && !elsewhere(table.startNo)).map(table => `line ${table.startNo}: a table outside any requirement paragraph (a table belongs to the requirement it follows, FORMAT 1.3)`),
    ...spec.blocks.filter(block => !elsewhere(block.openNo)).map(block => `line ${block.openNo}: a fenced code block outside any requirement paragraph (FORMAT 1.3)`),
  ];
}

const isFrozen = ctx => headerRows(ctx.spec).rows.find(row => row.label === 'Freeze tag')?.value !== ctx.facts.header.placeholders['Freeze tag'];

function reqProblems(ctx) {
  const req = ctx.found.get('F-REQ')?.hits[0];
  return req !== undefined && Number(req.slots.n) !== ctx.maxId ? [`F-REQ (line ${req.line}) says R${req.slots.n}, and the highest requirement is R${ctx.maxId}`] : [];
}

const amendmentIdProblems = ctx => amendmentsOf(ctx.spec).items.flatMap(item => item.ids.filter(id => id > ctx.maxId).map(id => `line ${item.no}: amendment A${item.k} names R${id}, which does not exist`));

const OPENING = /^\*\*R[1-9]\d*\.\*\*(?=\s|$)/;
const BOLD_ID = /\*\*R\d/g;
const PLAIN_ID = /^[ \t]*(?:[-*+][ \t]+|\d+[.)][ \t]+)?R\d+\./;
const REFERENCE = /\bR(\d+)\b/g;
const bodyLines = spec => spec.kept.filter(line => line.no >= (spec.sections[0]?.no ?? spec.lastNo + 1));

/** True when a `**R<n>` stands anywhere but as the proper opening at the start of the line. */
const misplacedBold = text => [...text.matchAll(BOLD_ID)].some(found => found.index !== 0 || !OPENING.test(text));
const looksLikeId = line => misplacedBold(line.text) || PLAIN_ID.test(line.text);

const lookalikeProblems = spec => bodyLines(spec).filter(looksLikeId)
  .map(line => `line ${line.no}: "${line.text.slice(0, 20)}" looks like a requirement id and is not \`**R<n>.**\` at the start of a line`);

const opensParagraph = (spec, entry) => {
  const above = spec.lines[entry.startNo - 2];
  return above === undefined || above.text.trim() === '' || /^#{1,6}\s/.test(above.text);
};

const glueProblems = spec => spec.requirements.filter(entry => !opensParagraph(spec, entry)).map(entry => `line ${entry.startNo}: R${entry.n} does not open a paragraph: a blank line must stand above it`);

const WITHDRAWN = /Withdrawn by amendment A(\d+)\b/;

/** `Withdrawn by amendment A<k>` needs the amendment A<k> in section 8. */
const withdrawnProblems = spec => spec.requirements.flatMap(entry => {
  const found = WITHDRAWN.exec(entry.text);
  return found !== null && !amendmentsOf(spec).items.some(item => item.k === Number(found[1])) ? [`line ${entry.startNo}: R${entry.n} says Withdrawn by amendment A${found[1]}, which section 8 does not hold`] : [];
});

const outOfRange = (text, maxId) => !/^[1-9]\d*$/.test(text) || Number(text) > maxId;

/** Every `R<n>` anywhere in the spec must be one of R1 … Rn, written without a leading zero. */
const referenceProblems = (spec, maxId) => [...new Set(spec.kept.flatMap(line => [...line.text.matchAll(REFERENCE)].filter(found => outOfRange(found[1], maxId)).map(found => `line ${line.no}: the spec names R${found[1]}, and the spec has requirements R1 to R${maxId} only`)))];

/** Check 4: the requirement ids (`**R<n>.**`, unique, exactly R1 … Rn, ascending before the freeze), F-REQ's n, the ids the amendments name, and the spans of tables and fenced blocks. */
export const idFindings = ctx => [
  ...lookalikeProblems(ctx.spec), ...glueProblems(ctx.spec), ...referenceProblems(ctx.spec, ctx.maxId), ...withdrawnProblems(ctx.spec), ...idListProblems(ctx.spec.requirements, isFrozen(ctx)), ...reqProblems(ctx), ...amendmentIdProblems(ctx), ...spanProblems(ctx.spec),
];
