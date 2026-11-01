// The needle families of the push check. Every finding is `{ path, line, what }`.
//
//  - the private aids: the six-word shingles of their prose and an optional list of literal tokens, both read by the caller at run
//    time from outside git. A finding says which aid (a name the public plan already gives) or which token NUMBER - never a word.
//  - the known-task needles (parent D18, change 4.1): case-sensitive on word boundaries, ASCII letters and digits as the word
//    characters, plus exact substrings for names that are not words. These are public (they are in the plan), so a finding names the needle.
import { parseTokens, SHINGLE_WORDS } from '../../minigit-bench/lib/heldout-leak.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { lineIndex, readings, wordsWithLines } from './push-text.mjs';

const shinglesOfReading = (text, invisible) => {
  const words = wordsWithLines(text, invisible).map(w => w.word);
  return Array.from({ length: Math.max(0, words.length - SHINGLE_WORDS + 1) }, (_, i) => words.slice(i, i + SHINGLE_WORDS).join(' '));
};

/** The six-word shingles of `text` under every reading of its invisible characters and entities (see push-text.mjs). */
const shinglesOf = text => new Set(readings(text).flatMap(invisible => shinglesOfReading(text, invisible)));

/**
 * ok({ shingles, allShingles, publicShingles, literals }) or a fail naming the aid that holds no six words - or none that is not already public.
 * `shingles` (Map shingle -> aid name) leaves out what the approved public texts already hold: the BENCH rule (`mode: 'self'`), under which
 * a published run cannot leak anything. `allShingles` keeps every aid run and `publicShingles` is what the public texts hold: the TASK
 * repository rule (`mode: 'task'`) exempts a public run only in the narrow case of `exemptInTask`. An aid with no run of its own would check
 * nothing and is refused.
 */
export function aidNeedles({ aids, tokensText = '', publicTexts = [] }) {
  const publicShingles = new Set(publicTexts.flatMap(text => [...shinglesOf(text)]));
  const shingles = new Map();
  const allShingles = new Map();
  for (const aid of aids) {
    const all = [...shinglesOf(aid.text)];
    const own = all.filter(s => !publicShingles.has(s));
    if (own.length === 0) return fail(`aid ${aid.name} holds no ${SHINGLE_WORDS}-word run that is not already public: it would check nothing`);
    for (const s of own) if (!shingles.has(s)) shingles.set(s, aid.name);
    for (const s of all) if (!allShingles.has(s)) allShingles.set(s, aid.name);
  }
  const tokens = tokensText.trim() === '' ? ok([]) : parseTokens(tokensText);
  return tokens.ok ? ok({ shingles, allShingles, publicShingles, literals: tokens.value.map(t => t.toLowerCase()) }) : tokens;
}

/** The most whitespace tokens a window may span and still be exempt in the task repository: a path, a URL, a repository name beside a path. */
export const TASK_EXEMPT_TOKENS = 2;

/** Task rule: a public window is exempt only when it spans at most TASK_EXEMPT_TOKENS tokens of the checked text. */
const exemptInTask = (window, key, publicShingles) => publicShingles.has(key) && new Set(window.map(w => w.token)).size <= TASK_EXEMPT_TOKENS;

function shingleHitsOfReading(path, text, needles, mode, invisible) {
  const words = wordsWithLines(text, invisible);
  const task = mode === 'task';
  const lookup = task ? needles.allShingles : needles.shingles;
  const hits = [];
  for (let i = 0; i + SHINGLE_WORDS <= words.length; i += 1) {
    const window = words.slice(i, i + SHINGLE_WORDS);
    const key = window.map(w => w.word).join(' ');
    const aid = task && exemptInTask(window, key, needles.publicShingles) ? undefined : lookup.get(key);
    if (aid !== undefined) hits.push({ path, line: words[i].line, what: `shingle of ${aid}`, tokens: new Set(window.map(w => w.token)).size });
  }
  return hits;
}

/** One finding per (line, aid), over every reading of the text: a Set keyed by line and finding, so it stays linear however many findings there are. */
function shingleHits(path, text, needles, mode) {
  const seen = new Set();
  return readings(text).flatMap(invisible => shingleHitsOfReading(path, text, needles, mode, invisible)).filter(h => {
    const key = `${h.line}\0${h.what}`;
    return seen.has(key) ? false : seen.add(key) && true;
  });
}

function literalHits(path, text, literals) {
  return text.split('\n').flatMap((line, i) => {
    const lower = line.toLowerCase();
    return literals.flatMap((token, n) => (lower.includes(token) ? [{ path, line: i + 1, what: `leak token ${n + 1}` }] : []));
  });
}

/**
 * Every aid shingle and literal token found in `text`, ordered by line. `mode` is 'self' (the bench's own branch: every published run is
 * exempt, the default) or 'task' (the task repository and its PR bodies: only a public window of at most two tokens is).
 */
export function findAidHits(path, text, needles, { mode = 'self' } = {}) {
  return [...shingleHits(path, text, needles, mode), ...literalHits(path, text, needles.literals)].sort((a, b) => a.line - b.line);
}

const escapeRegExp = s => s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
// the words of a multi-word needle are joined by any run of whitespace, so a needle wrapped over lines is still the needle
const wordPattern = needle => needle.split(/\s+/).map(escapeRegExp).join('\\s+');
const WORD_EDGE = ['(?<![A-Za-z0-9])', '(?![A-Za-z0-9])'];

/** ok({ words, substrings, matchers }) from the parsed needle file, or a fail naming what is wrong with it. */
export function parseKnownNeedles(file) {
  const lists = ['words', 'substrings'];
  if (file?.schema !== 1) return fail('the needle file is not schema 1');
  for (const key of lists) {
    if (!Array.isArray(file[key]) || file[key].some(n => typeof n !== 'string' || n.trim() === '')) return fail(`the needle file's ${key} must be a list of non-empty strings`);
  }
  const matchers = [
    ...file.words.map(n => ({ name: n, re: new RegExp(`${WORD_EDGE[0]}${wordPattern(n)}${WORD_EDGE[1]}`, 'g') })),
    ...file.substrings.map(n => ({ name: n, re: new RegExp(escapeRegExp(n), 'g') })),
  ];
  return ok({ words: file.words, substrings: file.substrings, runBaseOnly: file.runBaseOnly ?? [], matchers });
}

/** Every known-task needle found in `text` (over the whole text, so a wrapped needle counts): one finding per needle per starting line. */
export function findKnownNeedles(path, text, { matchers }) {
  const lineOf = lineIndex(text);
  const seen = new Set();
  return matchers
    .flatMap(m => [...text.matchAll(m.re)].map(hit => ({ path, line: lineOf(hit.index), what: `needle ${m.name}` })))
    .filter(f => { const key = `${f.line}\0${f.what}`; return !seen.has(key) && seen.add(key); })
    .sort((a, b) => a.line - b.line);
}
