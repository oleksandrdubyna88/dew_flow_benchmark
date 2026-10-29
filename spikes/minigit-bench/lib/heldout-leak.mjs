// The leak check. Two kinds of needle, both from the clear suite: LITERALS (`leak-tokens.txt`: salted strings, expected hashes, reference
// identifiers, mutant lines) matched case-insensitively, and SHINGLES (`leak-shingles.txt`: every 6 consecutive words of every case row and
// mutant clause, lower-cased, punctuation and whitespace collapsed) matched through the same normalisation. Haystacks: every file git would
// commit (`git ls-files --cached --others --exclude-standard`), the commit messages and every patch of the branch range. A hit names the FILE
// and the needle's NUMBER, never the needle. A paraphrase that shares no six consecutive words is undetectable by design.
import { lstatSync } from 'node:fs';
import path from 'node:path';
import { readFileOrNull } from './inputs.mjs';
import { fail, ok } from './result.mjs';
import { runBounded } from './run.mjs';

export const MIN_TOKEN = 8;
export const SHINGLE_WORDS = 6;
const HISTORY_MAX_BYTES = 512 * 1024 * 1024;

/** One literal per line, blank lines ignored, each at least MIN_TOKEN characters. */
export function parseTokens(text) {
  const tokens = [];
  const lines = text.split('\n');
  for (let i = 0; i < lines.length; i += 1) {
    if (lines[i].trim() === '') continue;
    if (lines[i].length < MIN_TOKEN) return fail(`leak-tokens line ${i + 1} is shorter than ${MIN_TOKEN} characters: too short to be distinctive`);
    tokens.push(lines[i]);
  }
  return tokens.length > 0 ? ok(tokens) : fail('leak-tokens holds no token');
}

/** lower-case, every run of non-alphanumerics becomes one space. */
export const normalise = text => text.toLowerCase().replace(/[^a-z0-9]+/g, ' ').trim();

/** One shingle per line, each already six normalised words. A line of another length is a fail naming it. */
export function parseShingles(text) {
  const shingles = [];
  const lines = text.split('\n');
  for (let i = 0; i < lines.length; i += 1) {
    if (lines[i].trim() === '') continue;
    if (normalise(lines[i]).split(' ').length !== SHINGLE_WORDS || normalise(lines[i]) !== lines[i]) return fail(`leak-shingles line ${i + 1} is not ${SHINGLE_WORDS} normalised words`);
    shingles.push(lines[i]);
  }
  return ok(shingles);
}

/** The files git would commit: tracked, plus untracked and not ignored. A failure of git is a failure, never an empty list. */
export async function listCommittable(repoRoot) {
  const r = await runBounded('git', ['ls-files', '--cached', '--others', '--exclude-standard', '-z'], { cwd: repoRoot, timeoutMs: 60_000 });
  if (!r.ok) return r;
  if (r.value.timedOut || r.value.code !== 0) return fail(`git ls-files failed in ${repoRoot} (exit ${r.value.code}): ${r.value.stderr.trim().slice(-200)}`);
  return ok([...new Set(r.value.stdout.split('\0').filter(Boolean))].sort());
}

/** The default range: what this branch adds to `main`; with no `main` to compare, null (messages of the whole history, no diff). */
export async function defaultRange(repoRoot) {
  const r = await runBounded('git', ['merge-base', 'HEAD', 'main'], { cwd: repoRoot, timeoutMs: 20_000 });
  return r.ok && r.value.code === 0 && r.value.stdout.trim() !== '' ? `${r.value.stdout.trim()}..HEAD` : null;
}

/** [{ name, text }] -- the commit messages and the diff of `range` (no diff when `range` is null). A git failure is a fail. */
export async function historyTexts(repoRoot, range) {
  const head = await runBounded('git', ['rev-parse', '--verify', '-q', 'HEAD'], { cwd: repoRoot, timeoutMs: 20_000 });
  if (head.ok && head.value.code === 1 && range === null) return ok([]); // a repository with no commit has no history to read
  const run = args => runBounded('git', args, { cwd: repoRoot, timeoutMs: 120_000, maxBytes: HISTORY_MAX_BYTES });
  const log = await run(['log', '--format=%B', ...(range ? [range] : [])]);
  const parts = [{ name: `git log messages of ${range ?? 'HEAD'}`, run: log }];
  // every commit's patch, not the net diff: content added by one commit and removed by the next is in the history that a push publishes
  if (range) parts.push({ name: `git log -p ${range}`, run: await run(['log', '-p', '--no-color', '--no-ext-diff', '--format=%H', range]) });
  for (const p of parts) {
    if (!p.run.ok || p.run.value.timedOut || p.run.value.code !== 0) return fail(`${p.name} could not be read (git exit ${p.run.value?.code ?? 'n/a'}): the history is not checked, so the check is not clean`);
    if (p.run.value.truncated) return fail(`${p.name} is larger than ${HISTORY_MAX_BYTES} bytes: not scanned, so not clean`);
  }
  return ok(parts.map(p => ({ name: p.name, text: p.run.value.stdout })));
}

/** Pure. The needles' numbers (1-based) found in one haystack: { literals: [n], shingles: [n] }. */
export function matchText(raw, { literals, shingles }) {
  const lower = raw.toLowerCase();
  const found = { literals: [], shingles: [] };
  literals.forEach((needle, i) => { if (lower.includes(needle)) found.literals.push(i + 1); });
  if (shingles.length > 0) {
    const words = normalise(raw).split(' ');
    const windows = new Set();
    for (let i = 0; i + SHINGLE_WORDS <= words.length; i += 1) windows.add(words.slice(i, i + SHINGLE_WORDS).join(' '));
    shingles.forEach((needle, i) => { if (windows.has(needle)) found.shingles.push(i + 1); });
  }
  return found;
}

const hitsOf = (name, found) => [
  ...found.literals.map(tokenNumber => ({ file: name, tokenNumber, kind: 'literal' })),
  ...found.shingles.map(tokenNumber => ({ file: name, tokenNumber, kind: 'shingle' })),
];

/**
 * Over the files' bytes. `unreadable` lists a listed file that is gone (a tracked file deleted in the working tree); `skippedDirs` lists a
 * listed directory (a submodule's root: another repository, scanned by its own checks).
 */
export function scanFiles(root, files, tokens, shingles = []) {
  const needles = { literals: tokens.map(t => t.toLowerCase()), shingles };
  const hits = [];
  const unreadable = [];
  const skippedDirs = [];
  for (const file of files) {
    if (lstatSync(path.join(root, file), { throwIfNoEntry: false })?.isDirectory()) { skippedDirs.push(file); continue; }
    const bytes = readFileOrNull(path.join(root, file));
    if (bytes === null) { unreadable.push(file); continue; }
    hits.push(...hitsOf(file, matchText(bytes.toString('latin1'), needles)));
  }
  return { hits, unreadable, skippedDirs };
}

/** ok({ scanned, ... }) when clean; otherwise a fail naming every file (or git text) that holds a needle. */
export async function leakcheck({ repoRoot, tokensText, shinglesText = '', range }) {
  const tokens = parseTokens(tokensText);
  if (!tokens.ok) return tokens;
  const shingles = parseShingles(shinglesText);
  if (!shingles.ok) return shingles;
  const files = await listCommittable(repoRoot);
  if (!files.ok) return files;
  const { hits, unreadable, skippedDirs } = scanFiles(repoRoot, files.value, tokens.value, shingles.value);
  const history = await historyTexts(repoRoot, range === undefined ? await defaultRange(repoRoot) : range);
  if (!history.ok) return history;
  const needles = { literals: tokens.value.map(t => t.toLowerCase()), shingles: shingles.value };
  const all = [...hits, ...history.value.flatMap(h => hitsOf(h.name, matchText(h.text, needles)))];
  const scanned = files.value.length - unreadable.length - skippedDirs.length;
  if (all.length === 0) return { ...ok(null), scanned, unreadable, skippedDirs, history: history.value.map(h => h.name) };
  const named = [...new Set(all.map(h => h.file))];
  return { ...fail(`held-out text found in ${named.length} place(s): ${named.join(', ')}`), hits: all, scanned };
}
