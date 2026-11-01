// The needle families of the push check. Every finding is `{ path, line, what }`.
//
//  - the private aids: the six-word shingles of their prose and an optional list of literal tokens, both read by the caller at run
//    time from outside git. A finding says which aid (a name the public plan already gives) or which token NUMBER - never a word.
//  - the known-task needles (parent D18, change 4.1): case-sensitive on word boundaries, ASCII letters and digits as the word
//    characters, plus exact substrings for names that are not words. These are public (they are in the plan), so a finding names the needle.
import { parseTokens, SHINGLE_WORDS } from '../../minigit-bench/lib/heldout-leak.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { wordsWithLines } from './push-text.mjs';

const shinglesOf = text => {
  const words = wordsWithLines(text).map(w => w.word);
  return new Set(Array.from({ length: Math.max(0, words.length - SHINGLE_WORDS + 1) }, (_, i) => words.slice(i, i + SHINGLE_WORDS).join(' ')));
};

/**
 * ok({ shingles: Map(shingle -> aid name), literals: [lower-case token] }) or a fail naming the aid that holds no six words - or none
 * that is not already public. `publicTexts` (the parent plan and the stories document) are public by definition, so a shingle they hold
 * (a path, a title the aid quotes) can leak nothing and is dropped; an aid reduced to nothing would check nothing and is refused.
 */
export function aidNeedles({ aids, tokensText = '', publicTexts = [] }) {
  const publicShingles = new Set(publicTexts.flatMap(text => [...shinglesOf(text)]));
  const shingles = new Map();
  for (const aid of aids) {
    const own = [...shinglesOf(aid.text)].filter(s => !publicShingles.has(s));
    if (own.length === 0) return fail(`aid ${aid.name} holds no ${SHINGLE_WORDS}-word run that is not already public: it would check nothing`);
    for (const s of own) if (!shingles.has(s)) shingles.set(s, aid.name);
  }
  const tokens = tokensText.trim() === '' ? ok([]) : parseTokens(tokensText);
  return tokens.ok ? ok({ shingles, literals: tokens.value.map(t => t.toLowerCase()) }) : tokens;
}

function shingleHits(path, text, shingles) {
  const words = wordsWithLines(text);
  const seen = new Set();
  const hits = [];
  for (let i = 0; i + SHINGLE_WORDS <= words.length; i += 1) {
    const aid = shingles.get(words.slice(i, i + SHINGLE_WORDS).map(w => w.word).join(' '));
    const key = aid === undefined ? null : `${words[i].line}\0${aid}`;
    if (key !== null && !seen.has(key)) { seen.add(key); hits.push({ path, line: words[i].line, what: `shingle of ${aid}` }); }
  }
  return hits;
}

function literalHits(path, text, literals) {
  return text.split('\n').flatMap((line, i) => {
    const lower = line.toLowerCase();
    return literals.flatMap((token, n) => (lower.includes(token) ? [{ path, line: i + 1, what: `leak token ${n + 1}` }] : []));
  });
}

/** Every aid shingle and literal token found in `text`, ordered by line. */
export function findAidHits(path, text, { shingles, literals }) {
  return [...shingleHits(path, text, shingles), ...literalHits(path, text, literals)].sort((a, b) => a.line - b.line);
}

const escapeRegExp = s => s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
const WORD_EDGE = ['(?<![A-Za-z0-9])', '(?![A-Za-z0-9])'];

/** ok({ words, substrings, matchers }) from the parsed needle file, or a fail naming what is wrong with it. */
export function parseKnownNeedles(file) {
  const lists = ['words', 'substrings'];
  if (file?.schema !== 1) return fail('the needle file is not schema 1');
  for (const key of lists) {
    if (!Array.isArray(file[key]) || file[key].some(n => typeof n !== 'string' || n.trim() === '')) return fail(`the needle file's ${key} must be a list of non-empty strings`);
  }
  const matchers = [
    ...file.words.map(n => ({ name: n, re: new RegExp(`${WORD_EDGE[0]}${escapeRegExp(n)}${WORD_EDGE[1]}`) })),
    ...file.substrings.map(n => ({ name: n, re: new RegExp(escapeRegExp(n)) })),
  ];
  return ok({ words: file.words, substrings: file.substrings, runBaseOnly: file.runBaseOnly ?? [], matchers });
}

/** Every known-task needle found in `text`: one finding per needle per line. */
export function findKnownNeedles(path, text, { matchers }) {
  const lines = text.split('\n');
  return lines.flatMap((line, i) => matchers.filter(m => m.re.test(line)).map(m => ({ path, line: i + 1, what: `needle ${m.name}` })));
}

