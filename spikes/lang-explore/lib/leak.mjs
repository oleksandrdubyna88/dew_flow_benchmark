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
import { listOf } from './idlist.mjs';
import { buildMaterial, materialCounts } from './leak-material.mjs';
import { scanRepository } from './leak-scan.mjs';
import { normLine } from './leak-text.mjs';
import { REPO } from './paths.mjs';
import { resolveTask, taskIds, taskPaths } from './tasks.mjs';

const RUNNER_DIR = 'spikes/lang-explore/runner';

/**
 * The lines (normalised like the material, blank ones dropped) of the pilot's own agent-visible runner files as COMMITTED at `ref` (HEAD): the exemption of code needles that collide with them. Every agent holds a copy of `runner/` in its workspace and
 * the file was public before any held-out generator existed, so a generic line both share is a collision, not a leak. Read through git, never from the working copy: an uncommitted edit creates no exemption. Not pinned by sha in the
 * pins, so it is HEAD's. Nothing readable (no repository, no runner, git failing) gives no exemption, the stricter check. A set.
 */
export async function committedRunnerLines({ repoRoot = REPO, ref = 'HEAD' }) {
  const listed = await runBounded('git', ['ls-tree', '-r', '--name-only', ref, '--', RUNNER_DIR], { cwd: repoRoot, timeoutMs: 30_000 });
  const files = listed.ok && listed.value.code === 0 ? listed.value.stdout.split('\n').filter(Boolean) : [];
  const texts = await Promise.all(files.map(f => runBounded('git', ['show', `${ref}:${f}`], { cwd: repoRoot, timeoutMs: 30_000 })));
  const lines = texts.filter(r => r.ok && r.value.code === 0).flatMap(r => r.value.stdout.split('\n')).map(normLine).filter(l => l !== '');
  return new Set(lines);
}

/** The mutant ids and trap ids of a task's manifest: short names (`M-03`, `h007`) that are no needles but must not appear as words in a public note. */
export const manifestIds = manifest => [...new Set([...Object.keys(manifest.mutants ?? {}), ...(manifest.traps ?? [])])];

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

/** The sentence refusing the scan, or null: a folder that does not match its manifest (the check would be reading something else), or no folder at all (a check over nothing proves nothing). */
function scanProblem(loaded, home) {
  const broken = loaded.find(l => !l.held.ok && !l.held.absent);
  if (broken) return `task ${broken.task.id}: ${broken.held.reason}`;
  return loaded.some(l => l.held.ok) ? null : `no held-out folder is present (${loaded.map(l => taskPaths(l.task, home).heldoutDir).join(', ')}): the leak check would scan for nothing`;
}

/**
 * The structured material of the tasks to scan (`only` is one task id, or null for EVERY task of the pins): `ok({ material, ids, scanned, absent })` | `fail`. A task whose folder is not there has nothing to leak and is
 * listed in `absent`. `runnerLines` are the committed runner's normalised lines: a code needle that collides with one is dropped.
 */
export function leakMaterial({ home, explore, only = null, publicText = decidedPublicText(), runnerLines = new Set() }) {
  const loaded = loadTasks(home, explore, only);
  const problem = scanProblem(loaded, home);
  if (problem) return fail(problem);
  const present = loaded.filter(l => l.held.ok);
  const notes = present.map(l => ({ id: l.task.id, text: notesOf(l.task, home) }));
  const missing = notes.filter(n => n.text === null).map(n => n.id);
  if (missing.length > 0) return fail(`the private notes of task ${listOf(missing)} are missing (${listOf(missing.map(id => taskPaths(resolveTask(explore, id).value, home).notesFile))}): the leak check would read no shingles of a task whose material is there`);
  const tasks = present.map((l, i) => ({ id: l.task.id, heldout: l.held.value, shinglesText: leakShingles(notes[i].text, publicText), baselineCommit: l.task.baselineCommit, heldoutBorn: l.task.heldoutBorn }));
  const built = buildMaterial({ tasks, runnerLines });
  if (!built.ok) return built;
  return ok({ material: built.value, ids: [...new Set(present.flatMap(l => manifestIds(l.held.value.manifest)))], scanned: tasks.map(x => x.id), absent: loaded.filter(l => !l.held.ok).map(l => l.task.id) });
}

const orNone = ids => (ids.length === 0 ? 'none' : listOf(ids));
const skippedText = skipped => (skipped.length === 0 ? 'none' : skipped.map(s => `task ${s.task}: ${s.lines}`).join(', '));
const exemptText = exempt => (Object.keys(exempt ?? {}).length === 0 ? 'none' : Object.entries(exempt).map(([task, n]) => `task ${task}: ${n}`).join(', '));
export const leakLine = (r, m) => (r.ok
  ? `leakcheck clean: ${r.scanned} files (${r.binary.length} binary: line tiers only) and ${r.history.join(' + ') || 'no history'} scanned for the held-out case lines, reference, mutant, generator and oracle lines, test-note shingles and mutant and trap ids of tasks ${orNone(m.scanned)} (held-out folder not present: ${orNone(m.absent)}; short case lines left to the other tiers: ${skippedText(r.skipped)}; collisions with the baseline commit (not leaks): ${exemptText(r.exempt)}; substring needles: ${m.material.substrings.length})`
  : `leakcheck FAILED: ${r.reason}`);

/** The material of the command: the named task's, or every task's. -> `ok(material)` | `fail`. */
function materialOf({ values, io, explore, runnerLines }) {
  const named = values.task === undefined ? ok(null) : resolveTask(explore, values.task);
  return named.ok ? leakMaterial({ home: io.home, explore, only: values.task ?? null, runnerLines }) : named;
}

/** The failure of the id scan (the files that name a mutant or trap id as a word), or null. */
const idProblem = (r, material) => {
  const named = r.ok ? leakIds({ ids: material.ids }) : [];
  return named.length > 0 ? `leakcheck FAILED: a mutant or trap id of the held-out material is a word in ${listOf(named)}` : null;
};

/**
 * `leakcheck [--task NN] [--range A..B]`: v1's leak check over the repository and the branch range with the needles of EVERY task (the default: a leak of task 02's case in a task-03 commit is still a leak), or of one
 * task when `--task` names it; then the scan of the public notes for the mutant and trap ids. Exit 0 clean | 5 a hit or a failure | 4 the material could not be read.
 */
export async function leakcheckCommand({ values, io, explore, say }) {
  const repoRoot = io.repoRoot ?? REPO;
  const progress = io.progress ?? (line => process.stderr.write(`${line}\n`));
  progress(`building the material of tasks ${values.task ?? taskIds(explore).join(', ')}`);
  const material = materialOf({ values, io, explore, runnerLines: await committedRunnerLines({ repoRoot }) });
  if (!material.ok) return { exit: 4, message: material.reason };
  progress(`material: ${materialCounts(material.value.material)}`);
  const r = await scanRepository({ repoRoot, material: material.value.material, range: rangeOf(values).range, progress });
  const named = idProblem(r, material.value);
  say(named ?? leakLine(r, material.value));
  for (const [task, why] of Object.entries(r.baselineProblems ?? {})) say(`baseline of task ${task} gives no exemption: ${why}`);
  return { exit: exitOf(r, named) };
}

/** 0 clean; 5 a finding (a leak, or a mutant or trap id in a public note); 4 an incomplete scan (listed files that could not be read: not a leak, not clean). */
const exitOf = (r, named) => (r.ok && !named ? 0 : (incompleteOnly(r, named) ? 4 : 5));

const incompleteOnly = (r, named) => r.incomplete === true && (r.findings ?? []).length === 0 && !named;

const rangeOf = values => (values.range ? { range: values.range } : {});
