// What the text of the pinned runner may say (F1.S1 (f), parent T-L64): the runner is copied whole into the workspace of both stacks, so a stack word in one of its comments or docstrings tells each agent what the other side is.
// The reading is Python's own (`tools/runner_text.py` under `python3 -I -B`: `tokenize` for comments, `ast` for docstrings and string literals), the words are `inputs/stack-words-lang.json` by that file's own rule, and the
// few string literals that are behaviour and name a stack (a detection string, a runtime label) are exact entries of `inputs/runner-literals-lang.json`, each with its reason. A stack word in any other string literal is a
// finding, and so is an approved entry that no literal equals any more, so the list cannot grow quietly. A finding names the path and the line, never the text.
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { runBounded } from '../../minigit-bench/lib/run.mjs';
import { matchersOf, parseWordList, wordFindings } from './conformance-needles.mjs';

export const HELPER = fileURLToPath(new URL('../tools/runner_text.py', import.meta.url));
export const STACK_WORDS_FILE = fileURLToPath(new URL('../inputs/stack-words-lang.json', import.meta.url));
export const APPROVED_FILE = fileURLToPath(new URL('../inputs/runner-literals-lang.json', import.meta.url));
const PYTHON = '/usr/bin/python3';
const HELPER_TIMEOUT_MS = 60_000;

/** `ok({ "<file>": reading })` of the helper run on `files` in `mode` (`items` or `dump`), or a `fail`. A file that does not parse is `{ error }` in its entry, never a pass. */
export async function pythonReading(mode, files, { python = PYTHON } = {}) {
  const ran = await runBounded(python, ['-I', '-B', HELPER, mode, ...files], { timeoutMs: HELPER_TIMEOUT_MS });
  if (!ran.ok) return fail(`the Python reading could not be started: ${ran.reason}`);
  if (ran.value.code !== 0) return fail(`the Python reading ended with exit ${ran.value.code}: ${ran.value.stderr.trim().slice(0, 300)}`);
  return ok(JSON.parse(ran.value.stdout));
}

/** `ok(matchers)` for the stack words file. */
export function stackMatchers(file = STACK_WORDS_FILE) {
  const parsed = parseWordList(JSON.parse(readFileSync(file, 'utf8')));
  return parsed.ok ? ok(matchersOf(parsed.value)) : parsed;
}

const isEntry = entry => typeof entry?.text === 'string' && entry.text !== '' && typeof entry?.reason === 'string' && entry.reason.trim() !== '';

const noDuplicate = entries => new Set(entries.map(entry => entry.text)).size === entries.length;
const isList = body => body?.schema === 1 && Array.isArray(body.entries) && body.entries.every(isEntry);

/** `ok([{ text, reason }])` for the approved literals file: schema 1, every entry an exact string with a reason, no duplicate. */
export function approvedLiterals(file = APPROVED_FILE) {
  const body = JSON.parse(readFileSync(file, 'utf8'));
  if (!isList(body)) return fail(`${file} is not schema 1 with entries of { text, reason }`);
  return noDuplicate(body.entries) ? ok(body.entries) : fail(`${file} lists an entry twice`);
}

/** The findings `{ path, line, what }` of the words in `text` (a comment or a docstring), the line counted from `line`. */
const wordsIn = ({ path, line, text, what, matchers }) => wordFindings(path, text, matchers).map(hit => ({ path, line: line + hit.line - 1, what }));

/** The stack words in the comments and the docstrings of the readings (`{ "<path>": { comments, docstrings } }`). */
export function commentFindings(reading, matchers) {
  return Object.entries(reading).flatMap(([path, found]) => [
    ...found.comments.flatMap(c => wordsIn({ path, line: c.line, text: c.text, what: 'stack word in a comment', matchers })),
    ...found.docstrings.flatMap(d => wordsIn({ path, line: d.line, text: d.text, what: 'stack word in a docstring', matchers })),
  ]);
}

/** The string literals (not docstrings) that hold a stack word and are not an approved entry, and the approved entries no literal equals. -> `{ findings, stale }`. */
export function literalProblems(reading, matchers, approved) {
  const approvedTexts = new Set(approved.map(e => e.text));
  const literals = Object.entries(reading).flatMap(([path, found]) => found.strings.map(s => ({ path, line: s.line, value: s.value })));
  const findings = literals.filter(l => !approvedTexts.has(l.value) && wordFindings(l.path, l.value, matchers).length > 0).map(l => ({ path: l.path, line: l.line, what: 'stack word in a string literal' }));
  const present = new Set(literals.map(l => l.value));
  return { findings, stale: approved.filter(e => !present.has(e.text)).map(e => e.text) };
}
