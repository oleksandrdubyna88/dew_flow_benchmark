// The word lists of the no-needle check (E1.S8 batch 3, C3 and C4): the stack words (`inputs/stack-words-lang.json`) and the known-task needles (`inputs/needles-lang.json`), both of the shape
// `{ schema: 1, words, substrings, caseSensitive? }`, and the author's literals. The conformance check has its own matcher so the shared push rules do not move: a word matches on word
// boundaries and a substring anywhere, both without regard to case, except the words listed in `caseSensitive` (the English word `node`). A text is scanned whitespace-collapsed, so a needle
// wrapped over two lines is found, and a hit is reported on the line where it starts.
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { collapse } from './conformance-blocks.mjs';

const escapeRegExp = text => text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
const wordSource = word => word.split(/\s+/).map(escapeRegExp).join('\\s+');
const isStrings = list => Array.isArray(list) && list.every(item => typeof item === 'string' && item.trim() !== '');

/** ok({ words, substrings, caseSensitive }) for a parsed word file, or a fail naming what is wrong. */
export function parseWordList(file) {
  const caseSensitive = file?.caseSensitive ?? [];
  if (file?.schema !== 1) return fail('the word file is not schema 1');
  return [file.words, file.substrings, caseSensitive].every(isStrings) ? ok({ words: file.words, substrings: file.substrings, caseSensitive }) : fail('the word file needs `words`, `substrings` and `caseSensitive` as lists of non-empty strings');
}

const wordMatcher = (word, caseSensitive) => ({ name: word, re: new RegExp(`(?<![A-Za-z0-9])${wordSource(word)}(?![A-Za-z0-9])`, caseSensitive.includes(word) ? 'g' : 'gi') });
const substringMatcher = text => ({ name: text, re: new RegExp(escapeRegExp(text), 'gi') });

/** The matchers of a parsed word list: `{ name, re }`. */
export const matchersOf = list => [...list.words.map(word => wordMatcher(word, list.caseSensitive)), ...list.substrings.map(substringMatcher)];

/** One finding `{ line, what }` per matcher and line, over the whitespace-collapsed text. */
export function wordFindings(path, text, matchers) {
  const { text: flat, lineAt } = collapse(text.split('\n').map((line, index) => ({ no: index + 1, text: line })), /\s/);
  const hits = matchers.flatMap(({ name, re }) => [...flat.matchAll(re)].map(hit => ({ line: lineAt(hit.index), what: `needle ${name}` })));
  return [...new Map(hits.map(hit => [`${hit.line}\0${hit.what}`, hit])).values()].sort((a, b) => a.line - b.line);
}

/** One finding per literal and line (named by the literal's number, never by its words), over the whitespace-collapsed text, without regard to case. */
export function literalFindings(path, text, literals) {
  const { text: flat, lineAt } = collapse(text.split('\n').map((line, index) => ({ no: index + 1, text: line })), /\s/);
  const lower = flat.toLowerCase();
  const collapsedLiteral = token => token.toLowerCase().replace(/\s+/g, ' ').trim();
  const hits = literals.flatMap((token, number) => {
    const needle = collapsedLiteral(token);
    return needle === '' ? [] : allIndexes(lower, needle).map(index => ({ line: lineAt(index), what: `leak token ${number + 1}` }));
  });
  return [...new Map(hits.map(hit => [`${hit.line}\0${hit.what}`, hit])).values()].sort((a, b) => a.line - b.line);
}

function allIndexes(text, needle) {
  const found = [];
  for (let at = text.indexOf(needle); at !== -1; at = text.indexOf(needle, at + 1)) found.push(at);
  return found;
}
