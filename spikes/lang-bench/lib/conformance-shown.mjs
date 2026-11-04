// The samples as a spec shows them in section 6, and the samples folder (E1.S8): the olympiad table with its cells decoded by FORMAT 3.4's code-span forms, and the daemon and web
// `### <id>` blocks. The comparison with the files' bytes is in conformance-check-samples.mjs.
import { lstatSync, readdirSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';

const MAX_SAMPLE_BYTES = 4 * 1024 * 1024;
const MAX_CELL_BYTES = 200;

/** One entry of the folder: `{ odd }` for what is not a regular file or is too large, else `{ name, bytes }`. */
function classify(dir, entry) {
  const full = path.join(dir, entry.name);
  const stat = lstatSync(full);
  if (!stat.isFile()) return { odd: `${entry.name} is not a regular file` };
  if (stat.size > MAX_SAMPLE_BYTES) return { odd: `${entry.name} is larger than ${MAX_SAMPLE_BYTES} bytes` };
  return { name: entry.name, bytes: readFileSync(full) };
}

/** ok({ names, files, odd }) for a samples folder: the sorted names, the bytes of each regular file, and the names that are not regular files or are too large. */
export function readSamplesFolder(dir) {
  let entries;
  try {
    entries = readdirSync(dir, { withFileTypes: true });
  } catch (error) {
    return fail(`the samples folder ${dir} cannot be read (${error.code ?? error.message})`);
  }
  const classified = [...entries].sort((a, b) => (a.name < b.name ? -1 : 1)).map(entry => classify(dir, entry));
  const regular = classified.filter(entry => entry.bytes !== undefined);
  return ok({ names: entries.map(entry => entry.name).sort(), files: new Map(regular.map(entry => [entry.name, entry.bytes])), odd: classified.filter(entry => entry.odd !== undefined).map(entry => entry.odd) });
}

const printable = byte => byte === 0x0a || (byte >= 0x20 && byte <= 0x7e);
const sizeShowable = length => length >= 1 && length <= MAX_CELL_BYTES;

/** Why FORMAT 3.4 lets a sample's bytes be shown by their size only (more than 200 bytes, only spaces, two backticks in a row), or null when they can be shown as bytes. */
export function sizeOnlyReason(bytes) {
  const text = bytes.toString('latin1');
  if (!sizeShowable(bytes.length)) return `has ${bytes.length} bytes`;
  if (/^ +$/.test(text)) return 'has only spaces';
  return text.includes('``') ? 'holds two backticks in a row' : null;
}

/** Whether FORMAT 3.4 lets a sample's bytes be shown as bytes: printable ASCII and line feeds, 1 to 200 bytes, not only spaces, no two backticks in a row. */
export function showable(bytes) {
  const text = bytes.toString('latin1');
  return sizeShowable(bytes.length) && [...bytes].every(printable) && !/^ +$/.test(text) && !text.includes('``');
}

const FALLBACK = /^(\d+) bytes, see samples\/(s\d\d)\.(in|out)$/;
const SINGLE = /^`([^`]+)`$/;
const DOUBLE = /^``( .+ )``$/;

const spanOf = cell => {
  const single = SINGLE.exec(cell);
  const double = single === null ? DOUBLE.exec(cell) : null;
  if (single !== null) return { inner: single[1], double: false };
  return double === null ? null : { inner: double[1].slice(1, -1), double: true };
};

const printableText = text => [...text].every(char => char === '\n' || (char >= ' ' && char <= '~'));
const needsDouble = text => text.includes('`') || (text.startsWith(' ') && text.endsWith(' '));
const formProblem = (span, text) => (span.double === needsDouble(text) ? undefined : needsDouble(text) ? 'must be a code span of two backticks with a space inside each end' : 'uses two backticks where one is enough');
const textProblem = (span, text) => (printableText(text) ? formProblem(span, text) : 'shows a character that is not printable ASCII, the space or ⏎');

/** What a cell of the olympiad table stands for: `{ bytes }`, `{ size, file }` for the size fallback, or `{ error }`. */
export function decodeCell(cell) {
  const fallback = FALLBACK.exec(cell);
  if (fallback !== null) return { size: Number(fallback[1]), file: `${fallback[2]}.${fallback[3]}` };
  const span = spanOf(cell);
  if (span === null) return { error: 'is not one code span, and not the form "<size> bytes, see samples/<id>.<in|out>"' };
  const text = span.inner.replaceAll('\\|', '|').replaceAll('⏎', '\n');
  const problem = textProblem(span, text);
  return problem === undefined ? { bytes: Buffer.from(text, 'utf8') } : { error: problem };
}

const cellsOf = line => line.split(/(?<!\\)\|/).slice(1, -1).map(cell => cell.trim());
const idOf = cell => /^`(.+)`$/.exec(cell)?.[1] ?? cell;

const inSix = (spec, no) => spec.sectionAt(no) === 6;

/** What an olympiad spec's section 6 must not hold besides its one table: a second table, a fenced block, a `###` heading. */
function sixProblems(spec) {
  const tables = spec.tables.filter(entry => inSix(spec, entry.startNo));
  return [
    ...(tables.length > 1 ? [`line ${tables[1].startNo}: section 6 holds more than one table; the samples are one table`] : []),
    ...spec.blocks.filter(block => inSix(spec, block.openNo)).map(block => `line ${block.openNo}: section 6 of an olympiad spec holds a fenced code block`),
    ...spec.headings.filter(heading => heading.level === 3 && inSix(spec, heading.no)).map(heading => `line ${heading.no}: section 6 of an olympiad spec holds a ### heading`),
  ];
}

/** The olympiad table of section 6: `{ rows: [{ id, no, input, output }], problems }`. */
function olympiadRows(spec) {
  const table = spec.tables.find(entry => inSix(spec, entry.startNo));
  if (table === undefined) return { rows: [], problems: ['section 6 holds no table of samples', ...sixProblems(spec)] };
  const [head, separator, ...rest] = table.lines;
  const heading = cellsOf(head.text);
  const problems = heading.join('|') === 'Id|Standard input|Expected standard output' && /^\|[-| :]+\|$/.test(separator?.text ?? '') ? [] : [`line ${head.no}: the table must start with \`| Id | Standard input | Expected standard output |\` and a separator row`];
  const rows = rest.map(line => ({ no: line.no, cells: cellsOf(line.text) })).map(row => ({ id: idOf(row.cells[0] ?? ''), no: row.no, input: row.cells[1], output: row.cells[2], width: row.cells.length }));
  return { rows, problems: [...problems, ...rows.filter(row => row.width !== 3).map(row => `line ${row.no}: a sample row has three cells`), ...sixProblems(spec)] };
}

/** The `### <id>` subsections of section 6 with their fenced blocks: `{ rows: [{ id, no, blocks }], problems }`. */
function scenarioRows(spec) {
  const six = spec.headings.filter(heading => heading.level === 3 && spec.sectionAt(heading.no) === 6);
  const stops = spec.headings.map(heading => heading.no);
  const rows = six.map(heading => {
    const end = stops.find(no => no > heading.no) ?? spec.lastNo + 1;
    return { id: heading.text.trim(), no: heading.no, blocks: spec.blocks.filter(block => block.openNo > heading.no && block.openNo < end) };
  });
  const problems = rows.flatMap(row => (row.blocks.length === 2 ? [] : [`line ${row.no}: sample ${row.id} must show two fenced code blocks, the .in and the .out; it shows ${row.blocks.length}`]));
  return { rows, problems };
}

/** What section 6 shows: `{ ids, rows, problems }` for the kind. */
export function shownSamples(spec, kind) {
  const read = kind === 'olympiad' ? olympiadRows(spec) : scenarioRows(spec);
  return { ...read, ids: read.rows.map(row => row.id) };
}
