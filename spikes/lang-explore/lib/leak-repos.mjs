// `explore.mjs leakcheck` over BOTH repositories (build step 2b; merge conditions 3 and 5): the bench repository (leak.mjs, unchanged) and the task repository (leak-task-repo.mjs), and, with `--candidate-repo` and `--candidate`, ONE
// commit about to be published (leak-candidate.mjs). It is the command line's whole `leakcheck`: the bench half alone is never the command, because a check that scans one repository is a pass it did not earn.
//
//   leakcheck [--task NN] [--range A..B] [--task-repo <path>] [--task-base <ref>] [--candidate-repo <path> --candidate <commit>]
//
// The task repository's path is `--task-repo`, else the variable `LANG_V2_TASK_REPO`; with neither, the check refuses (exit 4). Its base is `--task-base`, else `origin/main`, resolved with `git rev-parse --verify`. `--candidate`
// with no `--task-repo` is a candidate alone: only that commit is scanned (its tree, its message, its diff). Exit 0 clean | 5 a finding in any repository | 4 an incomplete scan or a refusal. A line names the repository and the
// place of a finding, and never a text.
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { listOf } from './idlist.mjs';
import { scanCandidate } from './leak-candidate.mjs';
import { repoRootOf, resolveCommit, sameRepository } from './leak-git.mjs';
import { describeFinding } from './leak-scan.mjs';
import { scanTaskRepository } from './leak-task-repo.mjs';
import { benchHalf, commandMaterial, progressOf } from './leak.mjs';
import { REPO } from './paths.mjs';
import { resolveTask } from './tasks.mjs';

export const TASK_REPO_ENV = 'LANG_V2_TASK_REPO';
export const DEFAULT_BASE = 'origin/main';

const refused = sentence => ({ exit: 4, message: `leakcheck refused: ${sentence}` });

/** An option's value when it is a non-empty string (an empty one names nothing: it would be read as the current folder). */
const given = value => (typeof value === 'string' && value !== '' ? value : undefined);

/** True for `--candidate` without `--task-repo`: only that commit is scanned. */
const isAlone = values => values.candidate !== undefined && values['task-repo'] === undefined;

const aloneProblem = values => (isAlone(values) && (values['task-base'] !== undefined || values.range !== undefined) ? '--task-base and --range belong to the whole scan, not to --candidate alone: give --task-repo too, or drop them' : null);

/** The sentence refusing the options before anything is read (a pair broken, a ref that looks like an option, an option of the whole scan beside a candidate alone), or null. */
function optionsProblem(values) {
  if ((values.candidate === undefined) !== (values['candidate-repo'] === undefined)) return 'give --candidate-repo <path> and --candidate <commit> together';
  const dashed = ['task-base', 'candidate'].find(name => typeof values[name] === 'string' && values[name].startsWith('-'));
  return dashed === undefined ? aloneProblem(values) : `--${dashed} ${values[dashed]} looks like an option, not a ref`;
}

/** `ok({ root, given })` of the task repository, or the refusal: none given, not a repository's top folder, or the bench itself (a second look at the bench is no look at the task repository). */
async function taskRepoOf({ values, env, repoRoot }) {
  const path = given(values['task-repo']) ?? given(env[TASK_REPO_ENV]);
  if (path === undefined) return fail(`no task repository is given: pass --task-repo <path>, or set ${TASK_REPO_ENV}. A check that scans one repository is a pass it did not earn`);
  const root = await repoRootOf(path);
  if (!root.ok) return fail(`the task repository ${root.reason}`);
  return (await sameRepository(root.value, repoRoot)) ? fail(`the task repository ${path} is the bench repository itself (or a worktree of it): the task repository is another repository`) : ok({ root: root.value, given: path });
}

/** The name a candidate's repository is reported under: the bench, the task repository (given by the option or the variable), or its path. */
async function repoNameOf({ root, path, repoRoot, hint }) {
  if (await sameRepository(root, repoRoot)) return 'bench repository';
  return hint !== undefined && (await sameRepository(root, hint)) ? 'task repository' : `repository ${path}`;
}

/** `ok({ root, sha, name })` of the candidate, or the refusal (the repository is not a top folder, or the commit is not a commit of it). */
async function candidateOf({ values, repoRoot, hint }) {
  const path = values['candidate-repo'];
  const root = await repoRootOf(path);
  if (!root.ok) return fail(`the candidate repository ${root.reason}`);
  const sha = await resolveCommit(root.value, values.candidate);
  if (!sha.ok) return fail(`the candidate ${values.candidate} is not a commit of ${path}: ${sha.reason}`);
  return ok({ root: root.value, sha: sha.value, name: await repoNameOf({ root: root.value, path, repoRoot, hint }) });
}

const taskSetup = args => (isAlone(args.values) ? Promise.resolve(ok(null)) : taskRepoOf(args));
const candidateSetup = args => (args.values.candidate === undefined ? Promise.resolve(ok(null)) : candidateOf(args));

/** `ok({ task, candidate })`: the repositories the run reads, resolved and checked before the material is built (a refusal costs nothing). */
async function setupOf({ values, env, repoRoot }) {
  const task = await taskSetup({ values, env, repoRoot });
  if (!task.ok) return task;
  const candidate = await candidateSetup({ values, repoRoot, hint: task.value?.root ?? given(env[TASK_REPO_ENV]) });
  return candidate.ok ? ok({ task: task.value, candidate: candidate.value }) : candidate;
}

/** The text of the bench half's line, with the repository named. */
const benchLine = line => (line.startsWith('leakcheck ') ? line.replace('leakcheck ', 'leakcheck [bench repository] ') : `[bench repository] ${line}`);

/** The groups of a repository's scan: every finding, and every name that could not be read. */
const findingsOf = groups => groups.flatMap(g => g.findings);
const unreadableOf = groups => groups.flatMap(g => g.unreadable);

/** The failure sentence of a repository: the places that hold private text, and/or the files the scan could not read. */
function failureOf(groups) {
  const found = findingsOf(groups);
  const gone = unreadableOf(groups);
  const leaks = found.length > 0 ? `held-out text found in ${found.length} place(s): ${listOf(found.map(describeFinding))}` : '';
  const gaps = gone.length > 0 ? `the scan is incomplete: ${gone.length} file(s) could not be read (${listOf(gone)}): a check that skipped files is not clean` : '';
  return [leaks, gaps].filter(Boolean).join('; ');
}

/** 5 for a finding, 4 for an incomplete scan, else 0. */
const exitOfGroups = groups => (findingsOf(groups).length > 0 ? 5 : (unreadableOf(groups).length > 0 ? 4 : 0));

const binaryCount = groups => groups.reduce((n, g) => n + g.binary.length, 0);

/** The line of a scanned repository (`label` names the repository, or the candidate and its repository): FAILED with the places, or clean with what was read. `read` says what, in counts; `scope` how far the history went. */
function scannedLine({ label, groups, read, scope = null }) {
  const problem = failureOf(groups);
  const reach = scope === null ? '' : `; history read: ${scope}`;
  return problem === '' ? `leakcheck [${label}] clean: ${read}; ${binaryCount(groups)} binary file(s): line tiers only` : `leakcheck [${label}] FAILED: ${problem}${reach}`;
}

/** What the task repository's scan read, in words and counts. */
const taskRead = ({ counts, scope }) => `${counts.head === null ? 'no commit yet (an unborn branch), so no tree at HEAD' : `${counts.head} files at HEAD`}, ${counts.working} files of the working tree, the staged changes and history (${scope}) scanned`;

/** The task repository's part: says its line, returns its exit code. */
async function taskPart({ task, values, material, io, say }) {
  const label = `task repository ${task.given}`;
  const r = await scanTaskRepository({ repo: task.root, ref: given(values['task-base']) ?? DEFAULT_BASE, material: material.material, name: 'task repository', progress: progressOf(io) });
  if (r.refused !== undefined) { say(`leakcheck [${label}] REFUSED: ${r.refused}`); return 4; }
  say(scannedLine({ label, groups: r.groups, read: taskRead(r), scope: r.scope }));
  return exitOfGroups(r.groups);
}

/** The candidate's part: says its line, returns its exit code. */
async function candidatePart({ candidate, material, say }) {
  const label = `candidate ${candidate.sha.slice(0, 12)} of ${candidate.name}`;
  const r = await scanCandidate({ repo: candidate.root, sha: candidate.sha, material: material.material, name: candidate.name });
  if (r.refused !== undefined) { say(`leakcheck [${label}] REFUSED: ${r.refused}`); return 4; }
  say(scannedLine({ label, groups: [r.group], read: `${r.count} texts (its tree, its message and its diff against its parent or the empty tree) scanned` }));
  return exitOfGroups([r.group]);
}

/** The exit codes of the parts that run, in order: the bench and the task repository unless a candidate is alone, then the candidate when there is one. */
async function runParts({ setup, values, io, material, say }) {
  const exits = [];
  if (!isAlone(values)) {
    exits.push((await benchHalf({ values, io, material: material.value, say: line => say(benchLine(line)) })).exit);
    exits.push(await taskPart({ task: setup.task, values, material: material.value, io, say }));
  }
  if (setup.candidate !== null) exits.push(await candidatePart({ candidate: setup.candidate, material: material.value, say }));
  return exits;
}

/** The refusal of a `--task` that is not a task of the pins (named before anything else is read), or null. */
function taskProblem({ values, explore }) {
  const named = values.task === undefined ? ok(null) : resolveTask(explore, values.task);
  return named.ok ? null : refused(named.reason);
}

/** The refusal that comes before any repository is read: an unknown task, or options that do not fit together; or null. */
function earlyRefusal({ values, explore }) {
  const problem = optionsProblem(values);
  return taskProblem({ values, explore }) ?? (problem === null ? null : refused(problem));
}

/** The command: `{ exit, message? }` (a refusal carries its sentence in `message`; every other line is said). */
export async function leakcheckRepos({ values, io, explore, say }) {
  const early = earlyRefusal({ values, explore });
  if (early !== null) return early;
  const repoRoot = io.repoRoot ?? REPO;
  const setup = await setupOf({ values, env: io.env ?? process.env, repoRoot });
  if (!setup.ok) return refused(setup.reason);
  const material = await commandMaterial({ values, io, explore });
  if (!material.ok) return { exit: 4, message: material.reason };
  return { exit: Math.max(0, ...(await runParts({ setup: setup.value, values, io, material, say }))) };
}
