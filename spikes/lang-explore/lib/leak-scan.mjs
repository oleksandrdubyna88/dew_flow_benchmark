// The leak scan of the olympiad pilot (D41), redesigned for size. v1's `leakcheck` matched every needle against every file (`needles x bytes`); a task's millions of numeric case lines made that run for hours, so this module owns
// the file loop and reads each haystack text ONCE, in tiers (see module_lang_explore.md): substring, exact line, window of lines, chunk of a long line, then the private notes' shingles. v1's `heldout-leak.mjs` is imported
// for what it does well (the list of committable files, the git history texts, the default range, the shingle matcher) and is not changed. A finding names WHERE (file, line, task, case file, case line range), never the text.
import { lstatSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { defaultRange, historyTexts, listCommittable, matchText } from '../../minigit-bench/lib/heldout-leak.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { listOf } from './idlist.mjs';
import { findSubstrings } from './leak-substring.mjs';
import { chunkTier } from './leak-chunks.mjs';
import { lineTier } from './leak-lines.mjs';
import { windowTier } from './leak-windows.mjs';
import { isBinary, normLine } from './leak-text.mjs';
import { applyBaseline } from './leak-baseline.mjs';
import { verifyCandidates, verifyChunks } from './leak-verify.mjs';

/** The 0-based line of position `pos` in a text whose lines are joined by `\n`. */
function lineAt(text, pos) {
  let line = 0;
  for (let at = text.indexOf('\n'); at !== -1 && at < pos; at = text.indexOf('\n', at + 1)) line += 1;
  return line;
}

/** A finding of the substring tier: where in the haystack, and which needle (task, source file, line) matched; the needle's text is not kept. */
const substringFinding = ({ name, lines, text, hit }) => {
  const line = lineAt(text, hit.pos);
  return { tier: 'substring', file: name, line: line + 1, task: hit.needle.task, caseFile: hit.needle.source, fromLine: hit.needle.line, toLine: hit.needle.line, raws: [lines[line]] };
};

/** Tier 1 over one text: the normalised text (every line normalised, blank ones kept so the line numbers stay physical) searched for every needle at once. */
function substringFindings({ name, lines, material }) {
  const text = lines.map(normLine).join('\n');
  return findSubstrings(text, material.substringIndex).map(hit => substringFinding({ name, lines, text, hit }));
}

/** The private notes' shingles in one text, by v1's matcher: `{ tier: 'shingle', file, task }` per shingle found (no line: v1 windows the whole normalised text). */
function shingleFindings({ name, raw, material }) {
  if (material.shingleTexts.length === 0) return [];
  const found = matchText(raw, { literals: [], shingles: material.shingleTexts });
  return found.shingles.map(n => ({ tier: 'shingle', file: name, line: null, task: material.shingleOwners[n - 1], caseFile: 'private notes', fromLine: null, toLine: null, raws: [] }));
}

/**
 * One haystack text through the tiers. `diff` marks `git log -p` text (its lines carry a `+`, `-` or space prefix). A binary file (a NUL in its first 8 KB) skips the text tiers (substring and shingles) and is reported as
 * "line tiers only". -> `{ findings, binary }`.
 */
export function scanText({ name, raw, diff = false, material }) {
  const binary = isBinary(raw);
  const lines = raw.split('\n');
  const text = binary ? [] : [...substringFindings({ name, lines, material }), ...shingleFindings({ name, raw, material })];
  const norms = lines.map(normLine);
  const tier2 = lineTier({ name, lines, norms, diff, material });
  const chunks = verifyChunks(chunkTier({ name, lines, norms, diff, material }), material.caseSources);
  return { findings: [...text, ...tier2.findings, ...chunks], pending: [...tier2.pending, ...windowTier({ name, lines, norms, diff, material })], binary, diff };
}

/** What a listed path is, WITHOUT letting a failure escape: `dir`, `file`, or `error` when it cannot be examined (gone, a parent not searchable, ...). */
function kindOf(full) {
  try { return lstatSync(full, { throwIfNoEntry: false })?.isDirectory() ? 'dir' : 'file'; } catch { return 'error'; }
}

/** A listed path read without letting a failure escape: `{ skip: 'dir' }`, `{ skip: 'unreadable' }` or `{ bytes }`. */
function readListed(full) {
  const kind = kindOf(full);
  if (kind === 'dir') return { skip: 'dir' };
  const bytes = kind === 'error' ? null : readOrNull(full);
  return bytes === null ? { skip: 'unreadable' } : { bytes };
}

const readOrNull = file => { try { return readFileSync(file); } catch { return null; } };

/** The committable files through `scanText`: `{ findings, binary, unreadable, skippedDirs }` (a directory is a submodule's root: another repository, scanned by its own checks). */
export function scanFiles({ root, files, material }) {
  const out = { findings: [], pending: [], binary: [], unreadable: [], skippedDirs: [] };
  for (const file of files) scanOne({ root, file, material, out });
  return out;
}

/** One listed file into `out`: a directory (a submodule's root) is skipped, a file that is gone is unreadable, anything else is scanned. */
function scanOne({ root, file, material, out }) {
  const read = readListed(path.join(root, file));
  if (read.skip === 'dir') { out.skippedDirs.push(file); return; }
  if (read.skip === 'unreadable') { out.unreadable.push(file); return; }
  const r = scanText({ name: file, raw: read.bytes.toString('latin1'), material });
  out.findings.push(...r.findings);
  out.pending.push(...r.pending);
  if (r.binary) out.binary.push(file);
}

/** The failure sentence: the places that hold private text, and/or the listed files the scan could not read (an incomplete scan is never clean). */
function failureSentence(findings, unreadable) {
  const leaks = findings.length > 0 ? `held-out text found in ${findings.length} place(s): ${listOf(findings.map(describeFinding))}` : '';
  const gaps = unreadable.length > 0 ? `the scan is incomplete: ${unreadable.length} listed file(s) could not be read (${listOf(unreadable)}): a check that skipped files is not clean` : '';
  return [leaks, gaps].filter(Boolean).join('; ');
}

/** One finding per place and per private line: the same line found by two tiers is reported once (the first tier, in the order they ran). */
function uniqueFindings(findings) {
  const seen = new Set();
  return findings.filter(f => { const key = `${f.file}\0${f.line}\0${f.task}\0${f.caseFile}\0${f.fromLine}`; return !seen.has(key) && seen.add(key); });
}

/** A finding as it leaves this module: without the haystack lines it was found in (they are what a leak looks like, and are only for the baseline check). */
const publicFinding = ({ raws, ...finding }) => finding;

/** The sentence of a finding: where, never what. */
export function describeFinding(f) {
  const where = f.line === null ? f.file : `${f.file}:${f.line}`;
  const range = f.fromLine === null ? '' : ` lines ${f.fromLine === f.toLine ? f.fromLine : `${f.fromLine}-${f.toLine}`}`;
  return `${where} (${f.tier}, task ${f.task}, ${f.caseFile}${range})`;
}

/**
 * The scan of a repository and a branch range for the material. -> `{ ok: true, scanned, history, binary, ... }` when clean, else a failure naming every place (`reason`, `findings`). `range` as v1's: undefined for the
 * default (what the branch adds to main), `null` for no diff.
 */
export async function scanRepository({ repoRoot, material, range, progress = () => {} }) {
  const files = await listCommittable(repoRoot);
  if (!files.ok) return files;
  progress(`scanning ${files.value.length} files`);
  const scanned = scanFiles({ root: repoRoot, files: files.value, material });
  const history = await readHistory({ repoRoot, range, progress });
  return history.ok ? verdictOf({ repoRoot, scanned, history: history.value, material, total: files.value.length, progress }) : history;
}

/** The history texts of the range (default: what the branch adds to main), announced before they are scanned. */
async function readHistory({ repoRoot, range, progress }) {
  const asked = range === undefined ? await defaultRange(repoRoot) : range;
  const history = await historyTexts(repoRoot, asked);
  if (history.ok) progress(`scanning the git history of ${asked ?? 'HEAD'} (${history.value.length} texts)`);
  return history;
}

/** The result of a scan: clean, or a failure naming where (the files and the history texts), never what. */
async function verdictOf({ repoRoot, scanned, history, material, total, progress }) {
  const texts = history.map(h => scanText({ name: h.name, raw: h.text, diff: h.name.startsWith('git log -p'), material }));
  const pending = [...scanned.pending, ...texts.flatMap(t => t.pending)];
  progress(`verifying ${pending.length} candidate(s)`);
  const verified = verifyCandidates(pending, material);
  const all = uniqueFindings([...scanned.findings, ...texts.flatMap(t => t.findings), ...verified]);
  const { kept, exempt, problems } = await applyBaseline({ repoRoot, findings: all, baselines: material.baselines });
  const findings = kept.map(publicFinding);
  const count = total - scanned.unreadable.length - scanned.skippedDirs.length;
  const incomplete = scanned.unreadable.length > 0;
  if (findings.length > 0 || incomplete) return { ...fail(failureSentence(findings, scanned.unreadable)), findings, scanned: count, exempt, incomplete, unreadable: scanned.unreadable, baselineProblems: problems };
  return { ...ok(null), scanned: count, history: history.map(h => h.name), binary: scanned.binary, skipped: material.skipped, exempt, incomplete: false, unreadable: [], baselineProblems: problems };
}
