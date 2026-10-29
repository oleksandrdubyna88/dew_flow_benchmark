// The leak check: every distinctive literal of the clear suite (`leak-tokens.txt`) must be absent from every file this repository
// could commit (`git ls-files --cached --others --exclude-standard`). A hit names the FILE and the token's NUMBER, never the token.
import { lstatSync } from 'node:fs';
import path from 'node:path';
import { readFileOrNull } from './inputs.mjs';
import { fail, ok } from './result.mjs';
import { runBounded } from './run.mjs';

export const MIN_TOKEN = 8;

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

/** The files git would commit: tracked, plus untracked and not ignored. A failure of git is a failure, never an empty list. */
export async function listCommittable(repoRoot) {
  const r = await runBounded('git', ['ls-files', '--cached', '--others', '--exclude-standard', '-z'], { cwd: repoRoot, timeoutMs: 60_000 });
  if (!r.ok) return r;
  if (r.value.timedOut || r.value.code !== 0) return fail(`git ls-files failed in ${repoRoot} (exit ${r.value.code}): ${r.value.stderr.trim().slice(-200)}`);
  return ok([...new Set(r.value.stdout.split('\0').filter(Boolean))].sort());
}

/**
 * Over the files' bytes. `unreadable` lists a listed file that is gone (a tracked file deleted in the working tree); `skippedDirs` lists a
 * listed directory (a submodule's root: another repository, scanned by its own checks).
 */
export function scanFiles(root, files, tokens) {
  const needles = tokens.map(t => Buffer.from(t, 'utf8'));
  const hits = [];
  const unreadable = [];
  const skippedDirs = [];
  for (const file of files) {
    if (lstatSync(path.join(root, file), { throwIfNoEntry: false })?.isDirectory()) { skippedDirs.push(file); continue; }
    const bytes = readFileOrNull(path.join(root, file));
    if (bytes === null) { unreadable.push(file); continue; }
    needles.forEach((needle, i) => { if (bytes.includes(needle)) hits.push({ file, tokenNumber: i + 1 }); });
  }
  return { hits, unreadable, skippedDirs };
}

/** ok({ scanned, unreadable }) when clean; otherwise a fail naming every file that holds a token. */
export async function leakcheck({ repoRoot, tokensText }) {
  const tokens = parseTokens(tokensText);
  if (!tokens.ok) return tokens;
  const files = await listCommittable(repoRoot);
  if (!files.ok) return files;
  const { hits, unreadable, skippedDirs } = scanFiles(repoRoot, files.value, tokens.value);
  if (hits.length === 0) return { ...ok(null), scanned: files.value.length - unreadable.length - skippedDirs.length, unreadable, skippedDirs };
  const named = [...new Set(hits.map(h => h.file))];
  return { ...fail(`held-out text found in ${named.length} file(s): ${named.join(', ')}`), hits, scanned: files.value.length };
}
