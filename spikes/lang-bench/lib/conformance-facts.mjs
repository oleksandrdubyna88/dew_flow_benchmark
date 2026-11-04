// What the conformance check reads from FORMAT.md besides the fixed sentences (E1.S8): the file-name pattern (1.1), the header rows and the kind phrases (1.2), the eight headings of each
// kind (1.4), the series defaults T and G (2.5) and the closed keyword list (4.5). All of it is read from the document; a FORMAT the reader cannot find one of them in makes the checker
// refuse to run (exit 4), never pass every spec.
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { headingByNumber, sliceFrom } from './format-structure.mjs';

const TABLE_SEPARATOR = /^\|[-| :]+\|$/;
const cellsOf = line => line.split('|').slice(1, -1).map(cell => cell.trim());
const codeSpans = text => [...text.matchAll(/`([^`]+)`/g)].map(hit => hit[1]);

/** The text of subsection or section `number` of the document (to the next heading of its level or above), or null. */
function textOf(format, number) {
  const heading = headingByNumber(format, number);
  if (heading === undefined) return null;
  const { start, end } = sliceFrom(format, heading, heading.level);
  return format.text.slice(start, end);
}

const isTableHead = (lines, first) => (line, index) => cellsOf(line)[0] === first && TABLE_SEPARATOR.test(lines[index + 1] ?? '');

/** The rows (cell lists) of the first table in `text` whose header row's first cell is `first`, or null. */
function tableRows(text, first) {
  const lines = text.split('\n');
  const head = lines.findIndex(isTableHead(lines, first));
  if (head === -1) return null;
  const body = lines.slice(head + 2);
  const end = body.findIndex(line => !line.startsWith('|'));
  return (end === -1 ? body : body.slice(0, end)).map(cellsOf);
}

/** The rows of the table headed by `first` in subsection `number`, or null. */
const rowsIn = (format, number, first) => {
  const text = textOf(format, number);
  return text === null ? null : tableRows(text, first);
};

const wrongSize = (rows, count) => rows === null || rows.length !== count;

/** ok({ pattern }) from 1.1: the file-name pattern in its code span. */
function readNamePattern(format) {
  const found = /pattern `(\^[^`]+\$)`/.exec(textOf(format, '1.1') ?? '');
  return found === null ? fail('FORMAT 1.1 does not state the file-name pattern') : ok(new RegExp(found[1]));
}

const verdictsOf = (rows, labels) => codeSpans((/the verdict being (.*)$/.exec(rows[labels.indexOf('Gate verdict')]?.[2] ?? '') ?? [])[1] ?? '');

function headerParts(rows, phrases) {
  const labels = rows.map(cells => codeSpans(cells[0])[0]);
  return {
    labels,
    placeholders: Object.fromEntries(rows.map((cells, index) => [labels[index], codeSpans(cells[1])[0]])),
    verdicts: verdictsOf(rows, labels),
    kindPhrases: Object.fromEntries(phrases.map(cells => [cells[0], codeSpans(cells[1])[0]])),
  };
}

const headerReadable = parts => !parts.labels.includes(undefined) && parts.verdicts.length > 0 && !Object.values(parts.kindPhrases).includes(undefined);

/** ok({ labels, placeholders, verdicts, kindPhrases }) from 1.2's two tables. */
function readHeader(format) {
  const rows = rowsIn(format, '1.2', 'Row');
  const phrases = rowsIn(format, '1.2', 'Kind');
  if (wrongSize(rows, 5)) return fail('FORMAT 1.2 does not hold the table of the five header rows');
  if (wrongSize(phrases, 3)) return fail('FORMAT 1.2 does not hold the table of the three kind phrases');
  const parts = headerParts(rows, phrases);
  return headerReadable(parts) ? ok(parts) : fail('FORMAT 1.2: a header row, the verdict words or a kind phrase cannot be read');
}

/** ok({ olympiad: [heading], 'daemon and web': [heading] }) from 1.4's table: the eight `##` lines of each kind. */
function readSections(format) {
  const rows = rowsIn(format, '1.4', '#');
  if (wrongSize(rows, 8)) return fail('FORMAT 1.4 does not hold the table of the eight sections');
  const [olympiad, daemonWeb] = [1, 2].map(column => rows.map(cells => codeSpans(cells[column])[0]));
  return olympiad.includes(undefined) || daemonWeb.includes(undefined) ? fail('FORMAT 1.4: a section heading cannot be read') : ok({ olympiad, daemon: daemonWeb, web: daemonWeb });
}

/** ok({ T, G }) from 2.5: the series defaults in seconds, `**T = 10 seconds**` and `**G = 5 seconds**`. */
function readDefaults(format) {
  const text = textOf(format, '2.5') ?? '';
  const t = /\*\*T = (\d+) seconds\*\*/.exec(text);
  const g = /\*\*G = (\d+) seconds\*\*/.exec(text);
  return t === null || g === null ? fail('FORMAT 2.5 does not state the series defaults as **T = <n> seconds** and **G = <n> seconds**') : ok({ T: Number(t[1]), G: Number(g[1]) });
}

/** ok([word]) from 4.5: the closed keyword list of the scenario script. */
function readKeywords(format) {
  const words = codeSpans(textOf(format, '4.5') ?? '');
  return words.length < 10 ? fail('FORMAT 4.5 does not hold the closed keyword list') : ok(words);
}

/** ok(facts) or the first fail. */
export function readFacts(format) {
  const parts = { namePattern: readNamePattern(format), header: readHeader(format), sections: readSections(format), defaults: readDefaults(format), keywords: readKeywords(format) };
  const bad = Object.values(parts).find(part => !part.ok);
  return bad ?? ok(Object.fromEntries(Object.entries(parts).map(([name, part]) => [name, part.value])));
}
