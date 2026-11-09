// The setup of `leakcheck` over both repositories (leak-repos.mjs): the options checked before anything is read, and the repositories resolved to their CANONICAL top folders (`repoRootOf`). Every comparison of two repositories
// (the task repository against the bench, a candidate's repository against either) is made on canonical paths only: a raw option, a raw variable or the bench's raw `repoRoot` is never handed to `sameRepository`.
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { repoRootOf, resolveCommit, sameRepository } from './leak-git.mjs';

export const TASK_REPO_ENV = 'LANG_V2_TASK_REPO';
export const DEFAULT_BASE_REF = 'origin/main';

/** An option's value when it is a non-empty string (an empty one names nothing: it would be read as the current folder). */
export const given = value => (typeof value === 'string' && value !== '' ? value : undefined);

/** True for `--candidate` without `--task-repo`: only that commit is scanned. */
export const isAlone = values => values.candidate !== undefined && values['task-repo'] === undefined;

const looksLikeOption = value => typeof value === 'string' && value.startsWith('-');

const aloneProblem = values => (isAlone(values) && (values['task-base'] !== undefined || values.range !== undefined) ? '--task-base and --range belong to the whole scan, not to --candidate alone: give --task-repo too, or drop them' : null);

/** The option, or the variable, whose value starts with `-` (a path or a ref that looks like an option), as `{ label, value, kind }`, or undefined. */
function dashedOption(values, env) {
  const named = [['task-repo', 'path'], ['candidate-repo', 'path'], ['task-base', 'ref'], ['candidate', 'ref']].find(([name]) => looksLikeOption(values[name]));
  if (named !== undefined) return { label: `--${named[0]}`, value: values[named[0]], kind: named[1] };
  return looksLikeOption(env[TASK_REPO_ENV]) ? { label: TASK_REPO_ENV, value: env[TASK_REPO_ENV], kind: 'path' } : undefined;
}

/** The sentence refusing the options before anything is read (a pair broken, a path or a ref that looks like an option, an option of the whole scan beside a candidate alone), or null. */
export function optionsProblem(values, env) {
  if ((values.candidate === undefined) !== (values['candidate-repo'] === undefined)) return 'give --candidate-repo <path> and --candidate <commit> together';
  const dashed = dashedOption(values, env);
  return dashed === undefined ? aloneProblem(values) : `${dashed.label} ${dashed.value} looks like an option, not a ${dashed.kind}`;
}

/** `ok({ root, given })` of the task repository, or the refusal: none given, not a repository's top folder, or the bench itself (a second look at the bench is no look at the task repository). `bench` is the bench's canonical root. */
async function taskRepoOf({ values, env, bench }) {
  const path = given(values['task-repo']) ?? given(env[TASK_REPO_ENV]);
  if (path === undefined) return fail(`no task repository is given: pass --task-repo <path>, or set ${TASK_REPO_ENV}; or scan one commit alone with --candidate-repo <path> --candidate <commit>. A check that scans one repository is a pass it did not earn`);
  const root = await repoRootOf(path);
  if (!root.ok) return fail(`the task repository ${root.reason}`);
  return (await sameRepository(root.value, bench)) ? fail(`the task repository ${path} is the bench repository itself (or a worktree of it): the task repository is another repository`) : ok({ root: root.value, given: path });
}

/** The name a candidate's repository is reported under: the bench, the task repository (its canonical root is `hint`), or its path. */
async function repoNameOf({ root, path, bench, hint }) {
  if (await sameRepository(root, bench)) return 'bench repository';
  return hint !== undefined && (await sameRepository(root, hint)) ? 'task repository' : `repository ${path}`;
}

/** `ok({ root, sha, name })` of the candidate, or the refusal (the repository is not a top folder, or the commit is not a commit of it). */
async function candidateOf({ values, bench, hint }) {
  const path = values['candidate-repo'];
  const root = await repoRootOf(path);
  if (!root.ok) return fail(`the candidate repository ${root.reason}`);
  const sha = await resolveCommit(root.value, values.candidate);
  if (!sha.ok) return fail(`the candidate ${values.candidate} is not a commit of ${path}: ${sha.reason}`);
  const name = await repoNameOf({ root: root.value, path, bench, hint });
  return ok({ root: root.value, sha: sha.value, name, bench: name === 'bench repository' });
}

const taskSetup = args => (isAlone(args.values) ? Promise.resolve(ok(null)) : taskRepoOf(args));
const candidateSetup = args => (args.values.candidate === undefined ? Promise.resolve(ok(null)) : candidateOf(args));

/** The canonical root of the task repository when something names it (the option, or the variable), else undefined: a raw value that is no top folder names nothing. */
async function hintOf({ task, env }) {
  if (task !== null) return task.root;
  const raw = given(env[TASK_REPO_ENV]);
  const root = raw === undefined ? null : await repoRootOf(raw);
  return root?.ok ? root.value : undefined;
}

/** `ok({ task, candidate })`: the repositories the run reads, resolved to canonical top folders and checked before the material is built (a refusal costs nothing). */
export async function setupOf({ values, env, repoRoot }) {
  const bench = await repoRootOf(repoRoot);
  if (!bench.ok) return fail(`the bench repository ${bench.reason}`);
  const task = await taskSetup({ values, env, bench: bench.value });
  if (!task.ok) return task;
  const candidate = await candidateSetup({ values, bench: bench.value, hint: await hintOf({ task: task.value, env }) });
  return candidate.ok ? ok({ task: task.value, candidate: candidate.value }) : candidate;
}
