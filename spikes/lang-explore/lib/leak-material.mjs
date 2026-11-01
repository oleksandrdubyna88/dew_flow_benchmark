// The private material of the leak scan as STRUCTURED data, never one joined string (a task's held-out inputs can be millions of lines): per tier the keys or needles it matches, each needle with where it came from
// (task, source file, line), so a finding names WHERE and never the text. Tier 1 (substring): the private CODE lines (references, mutants, gen.py, oracle files; at least 30 characters, minus the lines the committed
// runner shares) and the short CASE lines (8 to 199 characters) of a task that has at most 2,000 of them. The other tiers add their keys in the modules that own them.
import { existsSync, readFileSync, readdirSync } from 'node:fs';
import path from 'node:path';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { parseShingles } from '../../minigit-bench/lib/heldout-leak.mjs';
import { caseFilesOf, caseLinesOf } from './leak-case-files.mjs';
import { buildLineKeys, codeByKey } from './leak-lines.mjs';
import { buildChunkKeys } from './leak-chunks.mjs';
import { buildWindowKeys } from './leak-windows.mjs';
import { buildSubstringIndex } from './leak-substring.mjs';
import { normLine, polyHash, usable } from './leak-text.mjs';

export const SHORT_LINE_MIN = 8;
export const SHORT_LINE_MAX = 199;
export const TASK_SHORT_LINES_MAX = 2000;
export const SUBSTRING_NEEDLES_MAX = 3000;
export const CODE_LINE_MIN = 30;

const BOILERPLATE = /^(?:using|import|from|export)\b|^[{}();,]*$/;
const ORACLE_FILES = Object.freeze(['brute.py', 'handcheck.md']);

/** The files under `dir` (recursively), as paths relative to `base`. */
const filesUnder = (dir, base) => readdirSync(dir, { recursive: true, withFileTypes: true }).filter(e => e.isFile()).map(e => path.relative(base, path.join(e.parentPath, e.name)));

/** The private code files of a held-out folder, relative to it: the references, the mutants, the oracle files and the generator. */
function codeFiles(root) {
  const trees = ['refs', 'mutants'].map(d => path.join(root, d)).filter(existsSync).flatMap(d => filesUnder(d, root));
  return [...trees, ...[...ORACLE_FILES, 'gen.py'].filter(f => existsSync(path.join(root, f)))];
}

/** One code line as a needle, or null: normalised, at least 30 characters, not an import or bare punctuation, not a line the committed runner has. */
const codeNeedle = (raw, { source, line, task, runnerLines }) => {
  const text = normLine(raw);
  return usable(text, CODE_LINE_MIN) && !BOILERPLATE.test(text) && !runnerLines.has(text) ? { text, task, source, line, kind: 'code' } : null;
};

/** The code needles of a task's folder (`{ text, task, source, line }`, the first of each text). */
export function codeNeedles({ root, task, runnerLines = new Set() }) {
  const seen = new Set();
  const out = [];
  for (const source of codeFiles(root)) {
    readFileSync(path.join(root, source), 'latin1').split('\n').forEach((raw, i) => {
      const needle = codeNeedle(raw, { source, line: i + 1, task, runnerLines });
      if (needle && !seen.has(needle.text)) { seen.add(needle.text); out.push(needle); }
    });
  }
  return out;
}

/** `[{ n, line }]` for the lines of one case file that the substring tier takes: normalised, 8 to 199 characters, not noise (`line` is the 1-based physical line). */
function shortLinesOf(file) {
  const out = [];
  readFileSync(file, 'latin1').split('\n').forEach((raw, i) => {
    const n = normLine(raw);
    if (usable(n, SHORT_LINE_MIN) && n.length <= SHORT_LINE_MAX) out.push({ n, line: i + 1 });
  });
  return out;
}

/**
 * The short case lines of a task as needles, or `{ skipped: count }` when it has more than 2,000 (a task with millions of short numeric lines is covered by the window tier; its short lines are not substring needles).
 * Counting stops collecting at the cap, so a huge task costs a count, not memory.
 */
export function shortCaseNeedles({ casesDir, task }) {
  const state = { count: 0, needles: [] };
  for (const name of caseFilesOf(casesDir)) tally(state, { name, task, lines: shortLinesOf(path.join(casesDir, name)) });
  return state.count > TASK_SHORT_LINES_MAX ? { skipped: state.count } : { needles: state.needles };
}

/** Counts the lines of one case file, and keeps them as needles while the task is still within its cap. */
function tally(state, { name, task, lines }) {
  for (const { n, line } of lines) {
    state.count += 1;
    if (state.count <= TASK_SHORT_LINES_MAX) state.needles.push({ text: n, task, source: `cases/${name}`, line, kind: 'case' });
  }
}

/** One task's needles into `byText` (the first of each text), its skipped short-line count into `skipped`. */
function addTask({ byText, skipped, task: { id, heldout }, runnerLines }) {
  const short = shortCaseNeedles({ casesDir: heldout.casesDir, task: id });
  if (short.skipped !== undefined) skipped.push({ task: id, lines: short.skipped });
  for (const needle of [...codeNeedles({ root: heldout.dir, task: id, runnerLines }), ...(short.needles ?? [])]) if (!byText.has(needle.text)) byText.set(needle.text, needle);
}

/** The substring needles of the tasks: code and short case lines, deduplicated by text; a total above the cap is a failure, never a slow run. -> `ok({ needles, skipped })` | `fail`. */
export function substringNeedles(tasks, runnerLines) {
  const byText = new Map();
  const skipped = [];
  for (const task of tasks) addTask({ byText, skipped, task, runnerLines });
  const needles = [...byText.values()];
  if (needles.length > SUBSTRING_NEEDLES_MAX) return fail(`the substring tier holds ${needles.length} needles, over its cap of ${SUBSTRING_NEEDLES_MAX}: it would be a slow run, so the check refuses; reduce the private code or the short case lines`);
  return ok({ needles, skipped });
}

/** The size of each tier's material, as the progress line says it: counts only. */
export const materialCounts = m => `substring needles ${m.substrings.length}, exact-line keys ${m.lineKeys.size}, window keys ${m.windowKeys.size}, chunk keys ${m.chunkKeys.size}, shingles ${m.shingleTexts.length}`;

/** The shingles of every task as parallel arrays (the text of each, and the task it came from): `ok({ texts, tasks })` | `fail` (v1's parser refuses a line that is not six normalised words). */
export function shinglesOf(tasks) {
  const texts = [];
  const owners = [];
  for (const { id, shinglesText } of tasks) {
    const parsed = parseShingles(shinglesText ?? '');
    if (!parsed.ok) return fail(`the shingles of task ${id}: ${parsed.reason}`);
    for (const text of parsed.value) { texts.push(text); owners.push(id); }
  }
  return ok({ texts, owners });
}

/**
 * The structured material of the tasks to scan. `tasks` is `[{ id, heldout, shinglesText }]` (`heldout` as `loadHeldout` returns it); `runnerLines` the committed runner's normalised lines (code needles that collide with
 * them are dropped). -> `ok(material)` | `fail(sentence)`.
 */
export function buildMaterial({ tasks, runnerLines = new Set(), hash = polyHash }) {
  const sub = substringNeedles(tasks, runnerLines);
  const shingles = sub.ok ? shinglesOf(tasks) : sub;
  if (!shingles.ok) return shingles;
  const sources = tasks.map(t => ({ id: t.id, casesDir: t.heldout.casesDir }));
  return ok({
    substrings: sub.value.needles, substringIndex: buildSubstringIndex(sub.value.needles), skipped: sub.value.skipped, shingleTexts: shingles.value.texts, shingleOwners: shingles.value.owners, tasks: tasks.map(t => t.id), baselines: Object.fromEntries(tasks.map(t => [t.id, { commit: t.baselineCommit ?? null, born: t.heldoutBorn ?? null }])),
    caseSources: sources, hash, lineKeys: buildLineKeys(sources, hash), windowKeys: buildWindowKeys(sources, hash), chunkKeys: buildChunkKeys(sources), codeByKey: codeByKey(sub.value.needles.filter(n => n.kind === 'code'), hash),
  });
}
