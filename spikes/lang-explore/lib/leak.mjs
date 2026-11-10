// The leak check of the pilot (D41: no case value, no mutant, no trap name, no reference line and no private-note sentence ever enters git). It reads EVERY task's private material: for each task whose held-out folder is
// there, the expected-output lines, the reference, mutant and oracle lines (`brute.py`, `handcheck.md`) and the task's private notes' six-word shingles are the needles of v1's leak check over the repository and the branch
// range. The needles of task NN come from its own `heldout-NN/` only (`leakNeedles` takes one folder). A code needle that is also a line of the committed agent-visible runner is dropped
// (a collision, `committedRunnerLines`); case lines and note shingles never are. Plans in `todo/` and `research/` and the pins file are public by decision.
import { existsSync, readFileSync, readdirSync } from 'node:fs';
import path from 'node:path';
import { normalise } from '../../minigit-bench/lib/heldout-leak.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { runBounded } from '../../minigit-bench/lib/run.mjs';
import { loadHeldout } from './heldout.mjs';
import { sealedAbsentProblem } from './heldout-quick.mjs';
import { withOpenedHeldout } from './heldout-lifecycle.mjs';
import { isQuick } from './quick.mjs';
import { leakGates } from './leak-gates.mjs';
import { quickManifestIds } from './package-manifest.mjs';
import { listOf } from './idlist.mjs';
import { buildMaterial, materialCounts } from './leak-material.mjs';
import { loadReviewed } from './leak-reviewed.mjs';
import { scanRepository } from './leak-scan.mjs';
import { normLine } from './leak-text.mjs';
import { REPO } from './paths.mjs';
import { resolveTask, taskIds, taskPaths } from './tasks.mjs';

const RUNNER_DIR = 'spikes/lang-explore/runner';
/**
 * Where the quick tasks' grammar and boilerplate lines are read from: committed files every agent holds a copy of or that show the FORMAT itself: the runner and the templates of the workspace, the FORMAT's worked
 * examples, the hello programs and samples, the rehearsal programs. A held-out line that is also a line of one of them is FORMAT grammar or boilerplate by construction (the authors were told to write in that grammar,
 * and the same line is already public), not held-out content. Only the quick tasks use this set, and they use it ALONE (the runner is one of its directories): the olympiad tasks keep the runner exemption they always had, read at HEAD.
 * The set is read at `PUBLIC_REF` (what `main` already holds), never at HEAD: a line committed on the branch under scan cannot exempt itself. A ref that cannot be read gives no exemption, the stricter check.
 */
export const PUBLIC_REF = 'origin/main';
export const PUBLIC_DIRS = Object.freeze(['spikes/lang-bench/runner', 'spikes/lang-bench/templates', 'spikes/lang-bench/test/fixtures/runner/format', 'spikes/lang-bench/test/hello', 'spikes/lang-explore/rehearsal', RUNNER_DIR]);

/**
 * The lines (normalised like the material, blank ones dropped) of the pilot's own agent-visible runner files as COMMITTED at `ref` (HEAD): the exemption of code needles that collide with them. Every agent holds a copy of `runner/` in its workspace and
 * the file was public before any held-out generator existed, so a generic line both share is a collision, not a leak. Read through git, never from the working copy: an uncommitted edit creates no exemption. Not pinned by sha in the
 * pins, so it is HEAD's. Nothing readable (no repository, no runner, git failing) gives no exemption, the stricter check. A set.
 */
export const committedRunnerLines = ({ repoRoot = REPO, ref = 'HEAD' }) => committedLinesIn({ repoRoot, ref, dirs: [RUNNER_DIR] });

/** The quick tasks' grammar set: the public directories as `main` holds them (`io.publicRef` and `io.gitRun` are for tests). */
export const publicLines = ({ repoRoot, io = {} }) => committedLinesIn({ repoRoot, ref: io.publicRef ?? PUBLIC_REF, dirs: PUBLIC_DIRS, ...(io.gitRun ? { run: io.gitRun } : {}) });

/** The normalised lines of the files committed at `ref` under `dirs` (see `committedRunnerLines`; the same stricter-on-failure rule). */
export async function committedLinesIn({ repoRoot = REPO, ref = 'HEAD', dirs, run = runBounded }) {
  const listed = await run('git', ['ls-tree', '-r', '--name-only', ref, '--', ...dirs], { cwd: repoRoot, timeoutMs: 30_000 });
  const files = listed.ok && listed.value.code === 0 ? listed.value.stdout.split('\n').filter(Boolean) : [];
  const texts = await Promise.all(files.map(f => run('git', ['show', `${ref}:${f}`], { cwd: repoRoot, timeoutMs: 30_000 })));
  const lines = texts.filter(r => r.ok && r.value.code === 0).flatMap(r => r.value.stdout.split('\n')).map(normLine).filter(l => l !== '');
  return new Set(lines);
}

/** The mutant ids and trap ids of a task's manifest: short names (`M-03`, `h007`) that are no needles but must not appear as words in a public note. */
export const manifestIds = manifest => [...new Set([...Object.keys(manifest.mutants ?? {}), ...(manifest.traps ?? [])])];

/** A quick task's manifest ids (its witness ids and trap names, F2.S1): `ok([ids])`; a manifest that is there and is not JSON is a `fail`, never "no ids" (the check would scan for less than the package names). */
function quickManifestIdsOf(held) {
  const file = path.join(held.dir, 'manifest.json');
  if (!existsSync(file)) return ok([]);
  try { return ok(quickManifestIds(JSON.parse(readFileSync(file, 'utf8')))); } catch { return fail(`${file} is not valid JSON: the witness and trap ids of the package cannot be scanned for`); }
}

/** The ids of a held-out side: `ok([ids])`. An olympiad task's manifest names mutants and traps; a quick task names its trap cases (`traps.txt`) and its manifest's witness ids and trap names. */
const idsOfHeldout = held => {
  if (held.manifest) return ok(manifestIds(held.manifest));
  const quick = quickManifestIdsOf(held);
  return quick.ok ? ok([...new Set([...(held.traps ?? []), ...quick.value])]) : quick;
};

/** The ids of every task present, or the first refusal. */
function idsOfAll(present) {
  const lists = present.map(l => idsOfHeldout(l.held.value));
  return lists.find(r => !r.ok) ?? ok([...new Set(lists.flatMap(r => r.value))]);
}

/** The public files (`research/lang-v2/*.md` under `repo`) in which any of `ids` occurs as a whole word: `[file name]`, never the id. */
export function leakIds({ repo = REPO, ids }) {
  const dir = path.join(repo, 'research', 'lang-v2');
  const files = existsSync(dir) ? readdirSync(dir).filter(f => f.endsWith('.md')) : [];
  const words = ids.map(id => new RegExp(`(?<![\\w-])${id.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}(?![\\w-])`));
  return files.filter(f => { const text = readFileSync(path.join(dir, f), 'utf8'); return words.some(w => w.test(text)); });
}

/**
 * v1's shingles of the private test notes: every window of six consecutive words (normalised), one per line, except the windows that already occur in `publicText` (the plans and the pins file, whose
 * wording the team lead made public: the notes were written beside them and share phrases such as the spec's path).
 */
export function leakShingles(notesText, publicText = '') {
  const words = normalise(notesText).split(' ').filter(Boolean);
  const windows = new Set(Array.from({ length: Math.max(0, words.length - 5) }, (_, i) => words.slice(i, i + 6).join(' ')));
  const open = normalise(publicText);
  return [...windows].filter(w => !open.includes(w)).join('\n');
}

/** The normalised text of the files whose wording is public by decision: the plans (`todo/PLAN_*.md`, and `research/PLAN_*.md` once promoted) and this spike's pins. */
export function decidedPublicText(repo = REPO) {
  const plansIn = dir => (existsSync(path.join(repo, dir)) ? readdirSync(path.join(repo, dir)).filter(f => /^PLAN_.*\.md$/.test(f)).map(f => path.join(repo, dir, f)) : []);
  const plans = [...plansIn('todo'), ...plansIn('research')];
  return [...plans, path.join(repo, 'spikes', 'lang-explore', 'inputs', 'explore.json')].filter(existsSync).map(f => readFileSync(f, 'utf8')).join('\n');
}

const notesOf = (task, home) => { const f = taskPaths(task, home).notesFile; return existsSync(f) ? readFileSync(f, 'utf8') : null; };

/** The tasks of the pins to scan (`only` is one id, or null for every task) as `{ task, held }` with the held-out side loaded. */
const loadTasks = (home, explore, only) => (only ? [only] : taskIds(explore)).map(id => resolveTask(explore, id).value).map(task => ({ task, held: loadHeldout(home, task) }));

/** The sentence refusing the scan because a task whose clear folder is absent is sealed (`sealedAbsentProblem`, the one decision of every reader), or null. */
const sealedProblemOf = ({ loaded, home, frozenFile }) => loaded.filter(l => !l.held.ok).map(l => sealedAbsentProblem({ home, task: l.task, frozenFile })).find(p => p !== null) ?? null;

/** The sentence refusing the scan because a task of it has no folder (never skipped: the scan would say "clean" over less than it names), or null. When none is present, the check would scan for nothing. */
function absentProblem({ loaded, home }) {
  const gone = loaded.filter(l => !l.held.ok);
  const folders = gone.map(l => taskPaths(l.task, home).heldoutDir).join(', ');
  if (gone.length === loaded.length) return `no held-out folder is present (${folders}): the leak check would scan for nothing`;
  if (gone.length === 0) return null;
  return `the held-out folder of task ${listOf(gone.map(l => l.task.id))} is not present (${folders}): a task is never skipped, because a clean result must cover every task it names; name one task with --task, or restore the folder`;
}

/** The sentence refusing the scan, or null: a folder that does not match its manifest (the check would be reading something else), a sealed task with no clear folder, or a task with no folder at all. */
function scanProblem(loaded, home, frozenFile) {
  const broken = loaded.find(l => !l.held.ok && !l.held.absent);
  if (broken) return `task ${broken.task.id}: ${broken.held.reason}`;
  return sealedProblemOf({ loaded, home, frozenFile }) ?? absentProblem({ loaded, home });
}

/** The sentence refusing the scan, or null: a reviewed list that is unreadable or malformed (the check would not know what it exempts), else the scan's own problem. */
const refusalOf = ({ loaded, home, reviewed, frozenFile }) => (reviewed.ok ? scanProblem(loaded, home, frozenFile) : reviewed.reason);

/**
 * The structured material of the tasks to scan (`only` is one task id, or null for EVERY task of the pins): `ok({ material, ids, scanned, absent })` | `fail`. A task whose folder is not there has nothing to leak and is
 * listed in `absent`. `runnerLines` are the committed runner's normalised lines: a code needle that collides with one is dropped. The person-reviewed list of `home` (leak-reviewed.mjs) drops the lines and phrases it holds from the
 * needles and keys of every tier; `reviewed` in the result says how many entries it holds, by kind (counts only).
 */
export function leakMaterial({ home, explore, only = null, publicText = decidedPublicText(), runnerLines = new Set(), grammarLines = new Set(), frozenFile }) {
  const loaded = loadTasks(home, explore, only);
  const reviewed = loadReviewed({ home });
  const problem = refusalOf({ loaded, home, reviewed, frozenFile });
  if (problem) return fail(problem);
  const present = loaded.filter(l => l.held.ok);
  const notes = present.map(l => ({ id: l.task.id, text: notesOf(l.task, home) }));
  const missing = notes.filter(n => n.text === null).map(n => n.id);
  if (missing.length > 0) return fail(`the private notes of task ${listOf(missing)} are missing (${listOf(missing.map(id => taskPaths(resolveTask(explore, id).value, home).notesFile))}): the leak check would read no shingles of a task whose material is there`);
  const tasks = present.map((l, i) => ({ id: l.task.id, pins: l.task, heldout: l.held.value, shinglesText: leakShingles(notes[i].text, publicText), baselineCommit: l.task.baselineCommit, heldoutBorn: l.task.heldoutBorn, quick: l.task.quick === true }));
  const built = buildMaterial({ tasks, runnerLines, grammarLines, reviewed: reviewed.value, home, frozenFile });
  return built.ok ? withIds({ built: built.value, present, tasks, loaded, reviewed: reviewed.value }) : built;
}

/** The leak material with the ids of every task present (a manifest that cannot be read is a refusal): `ok({ material, ids, scanned, absent, reviewed })` | `fail`. */
function withIds({ built, present, tasks, loaded, reviewed }) {
  const ids = idsOfAll(present);
  return ids.ok ? ok({ material: built, ids: ids.value, scanned: tasks.map(x => x.id), absent: loaded.filter(l => !l.held.ok).map(l => l.task.id), reviewed: reviewed.counts }) : ids;
}

const orNone = ids => (ids.length === 0 ? 'none' : listOf(ids));
const skippedText = skipped => (skipped.length === 0 ? 'none' : skipped.map(s => `task ${s.task}: ${s.lines}`).join(', '));
const exemptText = exempt => (Object.keys(exempt ?? {}).length === 0 ? 'none' : Object.entries(exempt).map(([task, n]) => `task ${task}: ${n}`).join(', '));

/** The result line with the count of what a person's reading exempted, when there is any (counts only, never a text): every run with an exemption says so. */
export const withReviewed = (text, m) => {
  const { lines, shingles } = m.reviewed ?? { lines: 0, shingles: 0 };
  return lines + shingles === 0 ? text : `${text}; ${lines} private line(s) and ${shingles} phrase(s) exempt by a person's reading`;
};

export const leakLine = (r, m) => withReviewed(r.ok
  ? `leakcheck clean: ${r.scanned} files (${r.binary.length} binary: line tiers only) and ${r.history.join(' + ') || 'no history'} scanned for the held-out case lines, reference, mutant, generator and oracle lines, test-note shingles and mutant and trap ids of tasks ${orNone(m.scanned)} (held-out folder not present: ${orNone(m.absent)}; short case lines left to the other tiers: ${skippedText(r.skipped)}; collisions with the baseline commit (not leaks): ${exemptText(r.exempt)}; substring needles: ${m.material.substrings.length})`
  : `leakcheck FAILED: ${r.reason}`, m);

/** The material of the command: the named task's, or every task's. -> `ok(material)` | `fail`. */
function materialOf({ values, io, explore, runnerLines, grammarLines }) {
  const named = values.task === undefined ? ok(null) : resolveTask(explore, values.task);
  return named.ok ? leakMaterial({ home: io.home, explore, only: values.task ?? null, runnerLines, grammarLines, frozenFile: io.frozenFile }) : named;
}

/** The failure of the id scan (the files that name a mutant or trap id as a word), or null. */
const idProblem = (r, material) => {
  const named = r.ok ? leakIds({ ids: material.ids }) : [];
  return named.length > 0 ? withReviewed(`leakcheck FAILED: a mutant or trap id of the held-out material is a word in ${listOf(named)}`, material) : null;
};

/** The failure of the gate-record scan (`leak-gates.mjs`): lines of `spikes/lang-bench/inputs/gates/` that hold a case id, a witness or trap id, a case line or a window of the notes, by file, line and kind; or null. */
const gateProblem = ({ repoRoot, material }) => {
  const found = leakGates({ repoRoot, ids: material.ids, material: material.material });
  return found.length === 0 ? null : withReviewed(`leakcheck FAILED: ${found.length} line(s) of the gate records hold what a gate record must not: ${found.map(f => `${f.file}:${f.line} (${f.kind})`).join(', ')}`, material);
};

/** The progress sink of a command: `io.progress`, else one line per call on stderr (counts and names only). */
export const progressOf = io => io.progress ?? (line => process.stderr.write(`${line}\n`));

/** The held-out context of the command (`io.heldoutArgs`), with the record the command was given. */
const heldoutArgsOf = io => ({ ...(io.heldoutArgs ?? {}), ...(io.frozenFile === undefined ? {} : { frozenFile: io.frozenFile }) });

/**
 * `fn` run with every sealed quick task of the scan opened for this reader and closed after it, whether `fn` returns or throws (`withOpenedHeldout`: the task's lock is held for the whole read, the stamp is this process's). A task
 * that is not sealed, or whose folder is held, is read as it is. -> what `fn` returns | `fail(sentence)` when a folder cannot be opened.
 */
function withSealedOpened({ values, io, explore }, fn) {
  const ids = values.task === undefined ? taskIds(explore) : [values.task];
  const tasks = ids.map(id => resolveTask(explore, id)).filter(t => t.ok && isQuick(t.value)).map(t => t.value);
  return tasks.reduceRight((inner, task) => () => withOpenedHeldout({ ...heldoutArgsOf(io), home: io.home, task, fn: inner }), fn)();
}

/**
 * The material of the command, built once for every repository it scans: `ok({ material, ids, scanned, absent, reviewed })` | `fail(sentence)`. The runner's committed lines and the quick tasks' grammar set are read from the
 * bench repository (`io.repoRoot`), as always.
 */
export async function commandMaterial({ values, io, explore }) {
  const repoRoot = io.repoRoot ?? REPO;
  const progress = progressOf(io);
  progress(`building the material of tasks ${values.task ?? taskIds(explore).join(', ')}`);
  const material = await withSealedOpened({ values, io, explore }, async () => materialOf({ values, io, explore, runnerLines: await committedRunnerLines({ repoRoot }), grammarLines: await publicLines({ repoRoot, io }) }));
  if (material.ok) progress(`material: ${materialCounts(material.value.material)}`);
  return material;
}

/** The bench half of the command: the bench repository's files and branch range through the tiers, then the id scan; says its lines and gives the exit code (see `exitOf`). `material` is `commandMaterial`'s value. */
export async function benchHalf({ values, io, material, say }) {
  const repoRoot = io.repoRoot ?? REPO;
  const r = await scanRepository({ repoRoot, material: material.material, range: rangeOf(values).range, progress: progressOf(io) });
  const named = idProblem(r, material) ?? gateProblem({ repoRoot, material });
  say(named ?? leakLine(r, material));
  for (const [task, why] of Object.entries(r.baselineProblems ?? {})) say(`baseline of task ${task} gives no exemption: ${why}`);
  return { exit: exitOf(r, named) };
}

/** 0 clean; 5 a finding (a leak, or a mutant or trap id in a public note); 4 an incomplete scan (listed files that could not be read: not a leak, not clean). */
const exitOf = (r, named) => (r.ok && !named ? 0 : (incompleteOnly(r, named) ? 4 : 5));

const incompleteOnly = (r, named) => r.incomplete === true && (r.findings ?? []).length === 0 && !named;

const rangeOf = values => (values.range ? { range: values.range } : {});
