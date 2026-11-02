// Tier 2 of the leak scan, the exact-line tier: every case line of 30 characters or more (normalised, not one repeated character) is a 53-bit key in a sorted typed array, and so is every private code line; each haystack line,
// and each whitespace token of 30 characters or more in it, is looked up by its key. A `git log -p` line is looked up as it is AND without its one-character diff prefix. A key that hits is only a CANDIDATE: the verify step
// (lib/leak-verify.mjs) finds the case line it came from and compares the text, so a hash collision is never a finding.
import { caseFilesOf, caseLinesOf } from './leak-case-files.mjs';
import { KeyIndex, normLine, polyHash, stripDiffPrefix, usable } from './leak-text.mjs';

export const LINE_MIN = 30;

/** The keys of every case line of 30 characters or more of the folders `[{ casesDir }]` (one pass over each case file; a sorted index, not a JS object per line). */
export function buildLineKeys(sources, hash = polyHash) {
  const keys = new KeyIndex();
  for (const { casesDir } of sources) {
    for (const name of caseFilesOf(casesDir)) {
      for (const raw of caseLinesOf(casesDir, name)) addLine(keys, normLine(raw), hash);
    }
  }
  return keys.freeze();
}

const addLine = (keys, n, hash) => { if (usable(n, LINE_MIN)) keys.add(hash(n)); };

/** The private code needles by key: `Map(key -> [needle])` (a few thousand at most). */
export function codeByKey(needles, hash = polyHash) {
  const map = new Map();
  for (const needle of needles) { const k = hash(needle.text); map.set(k, [...(map.get(k) ?? []), needle]); }
  return map;
}

/** The normalised forms of one haystack line a lookup tries: the line, and for `git log -p` text also the line without its diff prefix. */
const formsOf = (raw, n, diff) => (diff ? [...new Set([n, normLine(stripDiffPrefix(raw))])] : [n]);

/** The texts of a normalised line a lookup tries: the whole line and each of its whitespace tokens of 30 characters or more. */
const textsOf = form => (form.includes(' ') ? [form, ...form.split(' ').filter(t => t.length >= LINE_MIN)] : [form]);

/** A code needle that equals `text`, if any (the key found it; the text decides). */
const codeMatch = (material, key, text) => (material.codeByKey.get(key) ?? []).find(n => n.text === text);

/** One looked-up text: a verified code finding, a candidate for the verify step, or nothing. */
function lookup({ text, name, line, raw, material, out }) {
  if (!usable(text, LINE_MIN)) return;
  const key = material.hash(text);
  const code = codeMatch(material, key, text);
  if (code) out.findings.push({ tier: 'line', file: name, line, task: code.task, caseFile: code.source, fromLine: code.line, toLine: code.line, raws: [raw] });
  else if (material.lineKeys.has(key)) candidate({ out, key, found: { tier: 'line', file: name, line, key, text, raws: [raw] } });
}

/** A case-line candidate, once per distinct key in the text (the first place it stands). */
function candidate({ out, key, found }) {
  if (out.seen.has(key)) return;
  out.seen.add(key);
  out.pending.push(found);
}

/** Tier 2 over one text's lines (one candidate per distinct key): `{ findings, pending }` (code findings are final; the case-line candidates need verifying). `norms` are the lines already normalised. */
export function lineTier({ name, lines, norms, diff, material }) {
  const out = { findings: [], pending: [], seen: new Set() };
  lines.forEach((raw, i) => {
    for (const form of formsOf(raw, norms[i], diff)) for (const text of textsOf(form)) lookup({ text, name, line: i + 1, raw, material, out });
  });
  return out;
}
