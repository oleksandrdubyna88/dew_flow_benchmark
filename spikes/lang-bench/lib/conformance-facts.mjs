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

/** The rows (cell lists) of the first table in `text` whose header row's first cell is `first`, or null. */
function tableRows(text, first) {
  const lines = text.split('\n');
  const head = lines.findIndex((line, index) => cellsOf(line)[0] === first && TABLE_SEPARATOR.test(lines[index + 1] ?? ''));
  if (head === -1) return null;
  const rows = [];
  for (let at = head + 2; at < lines.length && lines[at].startsWith('|'); at += 1) rows.push(cellsOf(lines[at]));
  return rows;
}

/** ok({ pattern }) from 1.1: the file-name pattern in its code span. */
function readNamePattern(format) {
  const found = /pattern `(\^[^`]+\$)`/.exec(textOf(format, '1.1') ?? '');
  return found === null ? fail('FORMAT 1.1 does not state the file-name pattern') : ok(new RegExp(found[1]));
}

/** ok({ labels, placeholders, verdicts, kindPhrases }) from 1.2's two tables. */
function readHeader(format) {
  const text = textOf(format, '1.2');
  const rows = text === null ? null : tableRows(text, 'Row');
  const phrases = text === null ? null : tableRows(text, 'Kind');
  if (rows === null || rows.length !== 5) return fail('FORMAT 1.2 does not hold the table of the five header rows');
  if (phrases === null || phrases.length !== 3) return fail('FORMAT 1.2 does not hold the table of the three kind phrases');
  const labels = rows.map(cells => codeSpans(cells[0])[0]);
  const placeholders = Object.fromEntries(rows.map((cells, index) => [labels[index], codeSpans(cells[1])[0]]));
  const verdicts = codeSpans((/the verdict being (.*)$/.exec(rows[labels.indexOf('Gate verdict')]?.[2] ?? '') ?? [])[1] ?? '');
  const kindPhrases = Object.fromEntries(phrases.map(cells => [cells[0], codeSpans(cells[1])[0]]));
  if (labels.includes(undefined) || verdicts.length === 0 || Object.values(kindPhrases).includes(undefined)) return fail('FORMAT 1.2: a header row, the verdict words or a kind phrase cannot be read');
  return ok({ labels, placeholders, verdicts, kindPhrases });
}

/** ok({ olympiad: [heading], 'daemon and web': [heading] }) from 1.4's table: the eight `##` lines of each kind. */
function readSections(format) {
  const rows = tableRows(textOf(format, '1.4') ?? '', '#');
  if (rows === null || rows.length !== 8) return fail('FORMAT 1.4 does not hold the table of the eight sections');
  const olympiad = rows.map(cells => codeSpans(cells[1])[0]);
  const daemonWeb = rows.map(cells => codeSpans(cells[2])[0]);
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
