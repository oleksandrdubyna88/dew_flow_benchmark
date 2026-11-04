// Checks 1 to 4 (E1.S8): the file and its title, the header rows, the eight sections of the kind and the amendments, and the requirement ids with their spans. Each returns its findings as
// text; an empty list is `ok`. FORMAT's facts (the name pattern, the placeholders, the kind phrases, the headings) come from conformance-facts.mjs, read from the document.
import { requirementAt } from './conformance-spec.mjs';

const TITLE = /^# Task (\d{2}) — \S.*$/;
const SEPARATOR = /^\|\s*-+\s*\|\s*-+\s*\|$/;
const DATE = /^\d{4}-\d{2}-\d{2}$/;
const ITEM = /^- A(\d+) · (\d{4}-\d{2}-\d{2}) · ([^—]+?) — \S/;
const cellsOf = line => line.split('|').slice(1, -1).map(cell => cell.trim());

/** Check 1: the file name matches FORMAT 1.1, and the title is `# Task NN — <Title>` with the file's NN. */
export function fileFindings(ctx) {
  const name = `${ctx.task}.md`;
  const findings = ctx.facts.namePattern.test(name) ? [] : [`the file name ${name} does not match ${ctx.facts.namePattern}`];
  const title = TITLE.exec(ctx.spec.lines[0].text);
  if (title === null) return [...findings, 'line 1 is not the title `# Task NN — <Title>` (NN two digits, an em dash)'];
  return title[1] === name.slice(0, 2) ? findings : [...findings, `line 1: the title says task ${title[1]}, and the file name says ${name.slice(0, 2)}`];
}

/** The header table: `{ rows: [{ label, value, no }], problems }`. It is the table that starts the first non-blank line under the title, with an empty header row, a separator and five rows. */
export function headerRows(spec) {
  const first = spec.lines.find(line => line.no > 1 && line.text.trim() !== '');
  const table = spec.tables.find(entry => entry.startNo === first?.no);
  if (table === undefined) return { rows: [], problems: ['directly under the title there is no two-column table of the header rows'] };
  const [head, separator, ...rest] = table.lines;
  const problems = [];
  if (cellsOf(head.text).some(cell => cell !== '') || cellsOf(head.text).length !== 2) problems.push(`line ${head.no}: the header row of the table must be empty (\`| | |\`)`);
  if (!SEPARATOR.test(separator?.text ?? '')) problems.push(`line ${separator?.no ?? head.no}: the second line of the header table must be the separator \`|---|---|\``);
  return { rows: rest.map(line => ({ label: cellsOf(line.text)[0], value: cellsOf(line.text)[1] ?? '', no: line.no })), problems };
}

/** The header rows' later forms: the value patterns FORMAT 1.2 gives for each row once the gate and the freeze have happened. */
function laterForm(label, facts) {
  const verdict = new RegExp(`^\`(?:${facts.header.verdicts.join('|')})\` · \\d{4}-\\d{2}-\\d{2}$`);
  return {
    'Gate verdict': value => verdict.test(value) && DATE.test(value.slice(-10)),
    'Freeze tag': value => /^`spec-frozen(?:-[A-Z])?-\d{8}`$/.test(value),
    Amendments: value => /^A1 \(section 8\)$/.test(value) || /^A1–A(?:[2-9]|[1-9]\d+) \(section 8\)$/.test(value),
  }[label];
}

/** Check 2: five rows in order, each value its placeholder or its later form; `Kind` one of the three phrases; the `Freeze rule` sentence exact. */
export function headerFindings(ctx) {
  const { facts } = ctx;
  const { rows, problems } = headerRows(ctx.spec);
  const findings = [...problems];
  if (rows.length !== facts.header.labels.length || rows.some((row, index) => row.label !== facts.header.labels[index])) {
    return [...findings, `the header table must hold exactly the rows ${facts.header.labels.join(', ')} in this order; it holds ${rows.map(row => row.label).join(', ') || 'none'}`];
  }
  for (const row of rows) {
    const placeholder = facts.header.placeholders[row.label];
    const later = laterForm(row.label, facts);
    const good = row.label === 'Kind' ? Object.values(facts.header.kindPhrases).includes(row.value)
      : row.label === 'Freeze rule' ? row.value === placeholder
        : row.value === placeholder || later(row.value);
    if (!good) findings.push(`line ${row.no}: the value of ${row.label} is "${row.value}", which is neither ${placeholder === undefined ? 'one of the three kind phrases' : `its placeholder "${placeholder}"`}${later === undefined ? '' : ' nor its later form'}`);
  }
  return findings;
}

/** The kind the Kind row names (`olympiad`, `daemon` or `web`), or null. */
export function kindOf(spec, facts) {
  const value = headerRows(spec).rows.find(row => row.label === 'Kind')?.value;
  return Object.entries(facts.header.kindPhrases).find(([, phrase]) => phrase === value)?.[0] ?? null;
}

/** Section 8: `{ none, items: [{ k, ids, no }], problems }`. */
export function amendmentsOf(spec) {
  const section = spec.sections.find(entry => entry.number === 8);
  if (section === undefined) return { none: false, items: [], problems: [] };
  const lines = spec.kept.filter(line => line.no > section.no && line.text.trim() !== '');
  if (lines.length === 1 && lines[0].text === 'None yet.') return { none: true, items: [], problems: [] };
  const items = [];
  const problems = [];
  for (const line of lines) {
    const found = ITEM.exec(line.text);
    if (found !== null) items.push({ k: Number(found[1]), ids: [...found[3].matchAll(/R([1-9]\d*)/g)].map(hit => Number(hit[1])), no: line.no });
    else if (!line.text.startsWith('  ') || items.length === 0) problems.push(`line ${line.no}: section 8 holds "None yet." or items \`- A<k> · <YYYY-MM-DD> · <R-ids> — <what>\`; this line is neither`);
  }
  return { none: false, items, problems };
}

function amendmentRowProblems(rows, amendments) {
  const row = rows.find(entry => entry.label === 'Amendments');
  if (row === undefined) return [];
  const k = amendments.items.length;
  const expected = k === 0 ? 'none yet' : k === 1 ? 'A1 (section 8)' : `A1–A${k} (section 8)`;
  return row.value === expected ? [] : [`line ${row.no}: the Amendments row says "${row.value}", and section 8 holds ${k === 0 ? 'no amendment' : `${k} item${k === 1 ? '' : 's'}`} (it must say "${expected}")`];
}

/** Check 3: exactly the eight `##` headings of the kind in order; section 8 is `None yet.` or the items A1 … Ak without gaps, as many as the Amendments row says. */
export function sectionFindings(ctx) {
  const expected = ctx.facts.sections[ctx.kind];
  const found = ctx.spec.sections;
  const findings = [];
  for (let at = 0; at < Math.max(expected.length, found.length); at += 1) {
    if (found[at]?.title !== expected[at]) findings.push(found[at] === undefined ? `section ${at + 1} is missing: expected "${expected[at]}"` : `line ${found[at].no}: expected "${expected[at] ?? 'no further ## section'}", found "${found[at].title}"`);
  }
  const amendments = amendmentsOf(ctx.spec);
  const gap = amendments.items.findIndex((item, index) => item.k !== index + 1);
  if (gap !== -1) findings.push(`line ${amendments.items[gap].no}: the amendments must be numbered A1, A2, … without a gap; A${amendments.items[gap].k} stands in place ${gap + 1}`);
  return [...findings, ...amendments.problems, ...amendmentRowProblems(headerRows(ctx.spec).rows, amendments)];
}

function idListProblems(ids, frozen) {
  const problems = [];
  const seen = new Set();
  for (const [index, entry] of ids.entries()) {
    if (seen.has(entry.n)) problems.push(`line ${entry.no}: R${entry.n} stands more than once`);
    seen.add(entry.n);
    if (!frozen && index > 0 && entry.n < ids[index - 1].n) problems.push(`line ${entry.no}: R${entry.n} stands after R${ids[index - 1].n}; before the freeze the ids ascend`);
  }
  const max = Math.max(0, ...ids.map(entry => entry.n));
  for (let n = 1; n <= max; n += 1) if (!seen.has(n)) problems.push(`R${n} is missing: the ids must be exactly R1 … R${max} without a gap`);
  return problems;
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

/** Check 4: the requirement ids (`**R<n>.**`, unique, exactly R1 … Rn, ascending before the freeze), F-REQ's n, the ids the amendments name, and the spans of tables and fenced blocks. */
export function idFindings(ctx) {
  const { spec } = ctx;
  const rows = headerRows(spec).rows;
  const frozen = rows.find(row => row.label === 'Freeze tag')?.value !== ctx.facts.header.placeholders['Freeze tag'];
  const findings = spec.lookalikes.map(entry => `line ${entry.no}: "${entry.text.slice(0, 20)}" looks like a requirement id and is not \`**R<n>.**\``);
  findings.push(...idListProblems(spec.requirements, frozen));
  const req = ctx.found.get('F-REQ')?.hits[0];
  if (req !== undefined && Number(req.slots.n) !== ctx.maxId) findings.push(`F-REQ (line ${req.line}) says R${req.slots.n}, and the highest requirement is R${ctx.maxId}`);
  for (const item of amendmentsOf(spec).items) for (const id of item.ids) if (id > ctx.maxId) findings.push(`line ${item.no}: amendment A${item.k} names R${id}, which does not exist`);
  return [...findings, ...spanProblems(spec)];
}
