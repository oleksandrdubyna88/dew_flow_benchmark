// The private material of the leak scan as STRUCTURED data, never one joined string (a task's held-out inputs can be millions of lines): per tier the keys or needles it matches, each needle with where it came from
// (task, source file, line), so a finding names WHERE and never the text. Tier 1 (substring): the private CODE lines (references, mutants, gen.py, oracle files; at least 30 characters, minus the lines the committed
// runner shares) and the short CASE lines (8 to 199 characters) of a task that has at most 2,000 of them. The other tiers add their keys in the modules that own them.
import { existsSync, lstatSync, readFileSync, readdirSync, statSync } from 'node:fs';
import path from 'node:path';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { parseShingles } from '../../minigit-bench/lib/heldout-leak.mjs';
import { caseFilesOf, caseLinesOf } from './leak-case-files.mjs';
import { buildLineKeys, codeByKey } from './leak-lines.mjs';
import { buildChunkKeys } from './leak-chunks.mjs';
import { buildWindowKeys } from './leak-windows.mjs';
import { sealedAbsentProblem } from './heldout-quick.mjs';
import { BITMAP_SLOTS, buildSubstringIndex } from './leak-substring.mjs';
import { NO_REVIEW } from './leak-reviewed.mjs';
import { isBinary, normLine, polyHash, usable } from './leak-text.mjs';

export const SHORT_LINE_MIN = 8;
export const SHORT_LINE_MAX = 199;
export const TASK_SHORT_LINES_MAX = 2000;
/** The most substring needles a scan takes: a quarter of the prefix table's slots (see leak-substring.mjs), so the cost stays the haystack's size. It was 3000 for the olympiad's three tasks; the quick run's six tasks and their reference solutions need more. */
export const SUBSTRING_NEEDLES_MAX = BITMAP_SLOTS / 4;
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

/** The links under one private folder, as `prefix` plus the path below it (a link met anywhere, or the folder itself being a link, named as `prefix` alone). A link is never read through and never skipped in silence. */
function linksIn(dir, prefix) {
  const self = lstatSync(dir, { throwIfNoEntry: false });
  if (self === undefined) return [];
  if (self.isSymbolicLink()) return [prefix.replace(/\/$/, '')];
  return readdirSync(dir, { recursive: true, withFileTypes: true }).filter(e => e.isSymbolicLink()).map(e => `${prefix}${path.relative(dir, path.join(e.parentPath, e.name))}`);
}

/** The links under a task's private code folders (`refs`, `mutants`, and a quick task's `refs-NN`, which a finding names as `refs/`): relative paths, never text. */
export function privateLinks({ root, refsDir = null }) {
  const quickRefs = refsDir === null ? [] : linksIn(refsDir, 'refs/');
  return [...linksIn(path.join(root, 'refs'), 'refs/'), ...linksIn(path.join(root, 'mutants'), 'mutants/'), ...quickRefs];
}

/** The sentence refusing the scan because a private folder holds a link (`task NN: path`), or null. */
const linkProblem = tasks => {
  const found = tasks.flatMap(t => privateLinks({ root: t.heldout.dir, refsDir: t.heldout.refsDir }).map(rel => `task ${t.id}: ${rel}`));
  return found.length === 0 ? null : `the private side holds a link, which the check does not read through and will not skip: ${found.join(', ')}`;
};

/** The sentence refusing a sealed task whose clear folder is gone before the link check reads it (the same function as the task loading: `sealedAbsentProblem`), or null. A task given without its pins (`pins`) is not asked. */
const sealedProblem = ({ tasks, home, frozenFile }) => tasks.filter(t => t.pins !== undefined).map(t => sealedAbsentProblem({ home, task: t.pins, frozenFile })).find(p => p !== null) ?? null;

const BUILD_OUTPUT = /(?:^|\/)(?:__pycache__|node_modules|bin|obj)\//;
const SOURCE_MAX_BYTES = 2_000_000;

/** A reference solution file a person wrote: not build output, not binary, not huge. */
const isSourceFile = file => !BUILD_OUTPUT.test(file) && statSync(file).size <= SOURCE_MAX_BYTES && !isBinary(readFileSync(file, 'latin1'));

/** The reference files of a quick task (`refs-NN`, every source file under it, however nested): `[{ base, rel, source }]`, `source` being how a finding names the file. */
function quickRefFiles(refsDir) {
  if (!refsDir || !existsSync(refsDir)) return [];
  return filesUnder(refsDir, refsDir).filter(rel => isSourceFile(path.join(refsDir, rel))).map(rel => ({ base: refsDir, rel, source: `refs/${rel}` }));
}

/** One code line as a needle, or null: normalised, at least 30 characters, not an import or bare punctuation, not a line the committed runner has. */
const codeNeedle = (raw, { source, line, task, runnerLines, collided }) => {
  const text = normLine(raw);
  if (!usable(text, CODE_LINE_MIN) || BOILERPLATE.test(text)) return null;
  if (runnerLines.has(text)) { collided?.add(text); return null; }
  return { text, task, source, line, kind: 'code' };
};

/** The code needles of a task's folder (`{ text, task, source, line }`, the first of each text); a quick task's `refsDir` adds its reference files. `collided` (a `Set`), when given, collects the texts dropped because a public file (the runner, a template) holds the same line: the collisions, counted and never needles. */
export function codeNeedles({ root, task, runnerLines = new Set(), refsDir = null, collided }) {
  const files = [...codeFiles(root).map(rel => ({ base: root, rel, source: rel })), ...quickRefFiles(refsDir)];
  const seen = new Set();
  const out = [];
  for (const { base, rel, source } of files) {
    readFileSync(path.join(base, rel), 'latin1').split('\n').forEach((raw, i) => {
      const needle = codeNeedle(raw, { source, line: i + 1, task, runnerLines, collided });
      if (needle && !seen.has(needle.text)) { seen.add(needle.text); out.push(needle); }
    });
  }
  return out;
}

/** `[{ n, line }]` for the lines of one case file that the substring tier takes: normalised, 8 to 199 characters, not noise, not a line of `grammar` (`line` is the 1-based physical line). */
function shortLinesOf(file, grammar) {
  const out = [];
  readFileSync(file, 'latin1').split('\n').forEach((raw, i) => {
    const n = normLine(raw);
    if (usable(n, SHORT_LINE_MIN) && n.length <= SHORT_LINE_MAX && !grammar.has(n)) out.push({ n, line: i + 1 });
  });
  return out;
}

/**
 * The short case lines of a task as needles, or `{ skipped: count }` when it has more than 2,000 (a task with millions of short numeric lines is covered by the window tier; its short lines are not substring needles).
 * Counting stops collecting at the cap, so a huge task costs a count, not memory.
 */
export function shortCaseNeedles({ casesDir, task, grammar = new Set() }) {
  const state = { count: 0, needles: [] };
  for (const name of caseFilesOf(casesDir)) tally(state, { name, task, lines: shortLinesOf(path.join(casesDir, name), grammar) });
  return state.count > TASK_SHORT_LINES_MAX ? { skipped: state.count } : { needles: state.needles };
}

/** Counts the lines of one case file, and keeps them as needles while the task is still within its cap. */
function tally(state, { name, task, lines }) {
  for (const { n, line } of lines) {
    state.count += 1;
    if (state.count <= TASK_SHORT_LINES_MAX) state.needles.push({ text: n, task, source: `cases/${name}`, line, kind: 'case' });
  }
}

/** One task's needles into `byText` (the first of each text), its skipped short-line count into `skipped`. A line a person reviewed (`reviewed`, see leak-reviewed.mjs) is no needle; it still counts toward the short-line cap. */
function addTask({ byText, skipped, collisions, task: { id, heldout }, runnerLines, grammar, reviewed }) {
  const short = shortCaseNeedles({ casesDir: heldout.casesDir, task: id, grammar });
  if (short.skipped !== undefined) skipped.push({ task: id, lines: short.skipped });
  const collided = new Set();
  for (const needle of [...codeNeedles({ root: heldout.dir, task: id, runnerLines, refsDir: heldout.refsDir, collided }), ...(short.needles ?? [])]) if (isNewNeedle(needle, { byText, reviewed })) byText.set(needle.text, needle);
  collisions[id] = collided.size;
}

/** A needle is new when no earlier one has its text and no person reviewed it. */
const isNewNeedle = (needle, { byText, reviewed }) => !byText.has(needle.text) && !reviewed.hasLine(needle.text);

/** The substring needles of the tasks: code and short case lines, deduplicated by text; a total above the cap is a failure, never a slow run. -> `ok({ needles, skipped, collisions })` (`collisions`: per task, the code lines dropped because a public file holds them) | `fail`. */
export function substringNeedles(tasks, runnerLines, grammarLines = new Set(), maxNeedles = SUBSTRING_NEEDLES_MAX, reviewed = NO_REVIEW) {
  const byText = new Map();
  const skipped = [];
  const collisions = {};
  for (const task of tasks) addTask({ byText, skipped, collisions, task, reviewed, ...(task.quick === true ? { runnerLines: grammarLines, grammar: grammarLines } : { runnerLines, grammar: new Set() }) });
  const needles = [...byText.values()];
  if (needles.length > maxNeedles) return fail(`the substring tier holds ${needles.length} needles, over its cap of ${maxNeedles}: it would be a slow run, so the check refuses; reduce the private code or the short case lines`);
  return ok({ needles, skipped, collisions });
}

/** The size of each tier's material, as the progress line says it: counts only. */
export const materialCounts = m => `substring needles ${m.substrings.length}, exact-line keys ${m.lineKeys.size}, window keys ${m.windowKeys.size}, chunk keys ${m.chunkKeys.size}, shingles ${m.shingleTexts.length}`;

/** The shingles of every task as parallel arrays (the text of each, and the task it came from): `ok({ texts, tasks })` | `fail` (v1's parser refuses a line that is not six normalised words). A phrase a person reviewed (see leak-reviewed.mjs) is left out. */
export function shinglesOf(tasks, reviewed = NO_REVIEW) {
  const texts = [];
  const owners = [];
  for (const { id, shinglesText } of tasks) {
    const parsed = parseShingles(shinglesText ?? '');
    if (!parsed.ok) return fail(`the shingles of task ${id}: ${parsed.reason}`);
    for (const text of parsed.value.filter(t => !reviewed.hasShingle(t))) { texts.push(text); owners.push(id); }
  }
  return ok({ texts, owners });
}

/**
 * The structured material of the tasks to scan. `tasks` is `[{ id, heldout, shinglesText }]` (`heldout` as `loadHeldout` returns it); `runnerLines` the committed runner's normalised lines (code needles that collide with
 * them are dropped); `reviewed` the person-reviewed list (leak-reviewed.mjs): a private line or phrase on it is dropped before the needles and keys of EVERY tier are built. -> `ok(material)` | `fail(sentence)`.
 */
function buildTiers({ tasks, runnerLines = new Set(), grammarLines = new Set(), hash = polyHash, maxNeedles = SUBSTRING_NEEDLES_MAX, reviewed = NO_REVIEW }) {
  const sub = substringNeedles(tasks, runnerLines, grammarLines, maxNeedles, reviewed);
  const shingles = sub.ok ? shinglesOf(tasks, reviewed) : sub;
  if (!shingles.ok) return shingles;
  const sources = tasks.map(t => ({ id: t.id, casesDir: t.heldout.casesDir, reviewed, ...(t.quick === true ? { grammar: grammarLines } : {}) }));
  return ok({
    substrings: sub.value.needles, substringIndex: buildSubstringIndex(sub.value.needles), skipped: sub.value.skipped, collisions: sub.value.collisions, shingleTexts: shingles.value.texts, shingleOwners: shingles.value.owners, tasks: tasks.map(t => t.id), baselines: Object.fromEntries(tasks.map(t => [t.id, { commit: t.baselineCommit ?? null, born: t.heldoutBorn ?? null }])),
    caseSources: sources, hash, lineKeys: buildLineKeys(sources, hash), windowKeys: buildWindowKeys(sources, hash), chunkKeys: buildChunkKeys(sources), codeByKey: codeByKey(sub.value.needles.filter(n => n.kind === 'code'), hash),
  });
}

/** The material of the tasks, or the refusal when a private folder of any of them holds a link (see `buildTiers` for the options). -> `ok(material)` | `fail(sentence)`. */
export function buildMaterial(options) {
  const problem = sealedProblem(options) ?? linkProblem(options.tasks);
  return problem === null ? buildTiers(options) : fail(problem);
}
