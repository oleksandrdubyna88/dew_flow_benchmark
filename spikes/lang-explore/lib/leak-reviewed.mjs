// The person-reviewed list of the leak check. `~/rsd/lang-v2/explore/leak-reviewed.json` lives OUTSIDE git, beside the held-out folders (the home is an argument). It holds the sha256 of private lines and of note phrases that a
// person READ and found to be generic text (FORMAT grammar, language boilerplate, a public URL, a code idiom): `{ "entries": [{ "sha256", "kind": "line" | "shingle", "reason", "reviewedAt", "by" }] }`. A person's reading
// is the only way in: there is no pattern, no length, no location and no tool of the check that adds to it. The file holds hashes and the words of a reason, never a text, and a field beyond the five named is refused,
// so the list cannot become a copy of the private side.
//
// The hash is the sha256 (hex) of the UTF-8 bytes of the same normal form the tiers match, as the JS string the tier holds it (the private case files are read as latin1, one character per byte): for a line,
// `normLine`'s result; for a shingle, the six normalised words (`normalise`). `reviewHash` is that function, for the one tool that writes the list. An absent file exempts nothing (the stricter result, no error); a file that
// cannot be read or understood is a refusal, never an empty list. Pure but for the one read.
import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { normalise } from '../../minigit-bench/lib/heldout-leak.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { normLine } from './leak-text.mjs';
import { exploreRoot } from './paths.mjs';

export const REVIEWED_FILE = 'leak-reviewed.json';
export const REVIEW_KINDS = Object.freeze(['line', 'shingle']);
export const REVIEW_FIELDS = Object.freeze(['sha256', 'kind', 'reason', 'reviewedAt', 'by']);

/** Where the list is under `home`. */
export const reviewedPath = home => path.join(exploreRoot(home), REVIEWED_FILE);

const sha256 = text => createHash('sha256').update(text, 'utf8').digest('hex');
const NORMAL_FORM = Object.freeze({ line: normLine, shingle: normalise });

/** The hash of `text` as a `kind` entry holds it: the sha256 of its normal form (see the top of this file). An unknown kind is a programming error and throws, naming the legal ones. */
export function reviewHash(kind, text) {
  if (!Object.hasOwn(NORMAL_FORM, kind)) throw new Error(`a review kind is one of ${REVIEW_KINDS.join(', ')}`);
  return sha256(NORMAL_FORM[kind](text));
}

/** What a list gives the tiers: `hasLine(normalisedLine)`, `hasShingle(sixNormalisedWords)` and the counts of its entries by kind. Nothing here returns a hash or a text. */
function reviewedOf(entries) {
  const of = kind => new Set(entries.filter(e => e.kind === kind).map(e => e.sha256));
  const lines = of('line');
  const shingles = of('shingle');
  return Object.freeze({
    hasLine: n => lines.size > 0 && lines.has(sha256(n)),
    hasShingle: t => shingles.size > 0 && shingles.has(sha256(t)),
    counts: Object.freeze({ lines: lines.size, shingles: shingles.size }),
  });
}

/** The list of a file that is not there: nothing is exempt. */
export const NO_REVIEW = reviewedOf([]);

const SHA256 = /^[0-9a-f]{64}$/;
const UTC_INSTANT = /^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d(?:\.\d+)?Z$/;
const isWords = v => typeof v === 'string' && v.trim() !== '';
const FIELD_CHECKS = Object.freeze([
  ['sha256', v => typeof v === 'string' && SHA256.test(v), 'is not 64 lower-case hex digits'],
  ['kind', v => REVIEW_KINDS.includes(v), `is not one of ${REVIEW_KINDS.join(', ')}`],
  ['reason', isWords, 'is not words'],
  ['reviewedAt', v => typeof v === 'string' && UTC_INSTANT.test(v) && !Number.isNaN(Date.parse(v)), 'is not a UTC instant (yyyy-mm-ddThh:mm:ssZ)'],
  ['by', isWords, 'is not words'],
]);

const isPlainObject = v => v !== null && typeof v === 'object' && !Array.isArray(v);
const beyond = (object, allowed) => Object.keys(object).filter(k => !allowed.includes(k)).length;

/** The sentence about one entry that is wrong, or null. Names the field's place, never a key or a value: a key or a value may be the very text the list must not hold. */
function entryProblem(entry) {
  if (!isPlainObject(entry)) return 'is not an object';
  if (beyond(entry, REVIEW_FIELDS) > 0) return `has ${beyond(entry, REVIEW_FIELDS)} field(s) beyond ${REVIEW_FIELDS.join(', ')}: the list holds hashes, never text`;
  const wrong = FIELD_CHECKS.find(([name, valid]) => !valid(entry[name]));
  return wrong ? `field ${wrong[0]} ${wrong[2]}` : null;
}

/** The first problem of the entries (an entry's own, or a repeat of an earlier one), as a sentence naming the entry by its number, or null. */
function entriesProblem(entries) {
  const seen = new Set();
  for (const [i, entry] of entries.entries()) {
    const own = entryProblem(entry);
    if (own) return `entry ${i + 1} ${own}`;
    const key = `${entry.kind}:${entry.sha256}`;
    if (seen.has(key)) return `entry ${i + 1} repeats an earlier entry`;
    seen.add(key);
  }
  return null;
}

/** The sentence about the file's top level that is wrong, or null. */
function shapeProblem(json) {
  if (!isPlainObject(json)) return 'is not an object with a list of entries';
  if (beyond(json, ['entries']) > 0) return `has ${beyond(json, ['entries'])} field(s) beyond entries: the list holds hashes, never text`;
  return Array.isArray(json.entries) ? null : 'has no list of entries';
}

/** The text of a list -> `ok(list)` | `fail(sentence)`. The sentence never holds a value of the file (a JSON parser's own message can, so it is not used). */
export function parseReviewed(text) {
  let json;
  try { json = JSON.parse(text); } catch { return fail('is not JSON'); }
  const problem = shapeProblem(json) ?? entriesProblem(json.entries);
  return problem ? fail(problem) : ok(reviewedOf(json.entries));
}

/** The list of `home` -> `ok(list)` | `fail(sentence)`: absent is `ok(NO_REVIEW)`; unreadable or malformed is a refusal. */
export function loadReviewed({ home }) {
  const file = reviewedPath(home);
  let text;
  try { text = readFileSync(file, 'utf8'); } catch (e) { return e?.code === 'ENOENT' ? ok(NO_REVIEW) : fail(`the reviewed list ${file} cannot be read (${e?.code ?? 'error'}): a list that cannot be read is no list, and the check will not run as if it were empty`); }
  const parsed = parseReviewed(text);
  return parsed.ok ? parsed : fail(`the reviewed list ${file} is refused: ${parsed.reason}`);
}
