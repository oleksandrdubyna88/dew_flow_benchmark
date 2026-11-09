// `explore.mjs leakcheck` over BOTH repositories (build step 2b; merge conditions 3 and 5): the bench repository (leak.mjs, unchanged) and the task repository (leak-task-repo.mjs), and, with `--candidate-repo` and `--candidate`, ONE
// commit about to be published (leak-candidate.mjs). It is the command line's whole `leakcheck`: the library exports no entry point that scans the bench alone, because a check that scans one repository is a pass it did not earn.
//
//   leakcheck [--task NN] [--range A..B] [--task-repo <path>] [--task-base <ref>] [--candidate-repo <path> --candidate <commit>]
//
// The task repository's path is `--task-repo`, else the variable `LANG_V2_TASK_REPO`; with neither, the check refuses (exit 4). Its base is `--task-base`, else `origin/main`, resolved with `git rev-parse --verify`. `--candidate`
// with no `--task-repo` is a candidate alone: only that commit is scanned (its tree, its message, its diff). Exit 0 clean | 5 a finding in any repository | 4 an incomplete scan or a refusal. A line names the repository and the
// place of a finding, and never a text. The result is ALWAYS `{ exit, message }`: the message holds every refusal (it is printed by the command), and is empty when nothing was refused.
import { scanCandidate } from './leak-candidate.mjs';
import { benchLine, exitOfGroups, refusedLine, scannedLine, taskRead } from './leak-repos-lines.mjs';
import { DEFAULT_BASE_REF, given, isAlone, optionsProblem, setupOf } from './leak-repos-setup.mjs';
import { scanTaskRepository } from './leak-task-repo.mjs';
import { benchHalf, commandMaterial, progressOf } from './leak.mjs';
import { REPO } from './paths.mjs';
import { ok } from '../../minigit-bench/lib/result.mjs';
import { resolveTask } from './tasks.mjs';

export { DEFAULT_BASE_REF as DEFAULT_BASE };
export { TASK_REPO_ENV } from './leak-repos-setup.mjs';

const refused = sentence => ({ exit: 4, message: `leakcheck refused: ${sentence}` });

/** The refusal of a `--task` that is not a task of the pins (named before anything else is read), or null. */
function taskProblem({ values, explore }) {
  const named = values.task === undefined ? ok(null) : resolveTask(explore, values.task);
  return named.ok ? null : refused(named.reason);
}

/** The refusal that comes before any repository is read: an unknown task, or options that do not fit together; or null. */
function earlyRefusal({ values, explore, env }) {
  const problem = optionsProblem(values, env);
  return taskProblem({ values, explore }) ?? (problem === null ? null : refused(problem));
}

/** The task repository's part: says its line and gives `{ exit, refusal? }` (a refusal is returned, not said: the command prints it once, as the message). */
async function taskPart({ task, values, material, io, say }) {
  const label = `task repository ${task.given}`;
  const r = await scanTaskRepository({ repo: task.root, ref: given(values['task-base']) ?? DEFAULT_BASE_REF, material: material.material, name: 'task repository', limits: io.gitLimits, progress: progressOf(io) });
  if (r.refused !== undefined) return { exit: 4, refusal: refusedLine(label, r.refused) };
  say(scannedLine({ label, groups: r.groups, read: taskRead(r), scope: r.scope }));
  return { exit: exitOfGroups(r.groups) };
}

/** The candidate's part: says its line and gives `{ exit, refusal? }`. */
async function candidatePart({ candidate, material, io, say }) {
  const label = `candidate ${candidate.sha.slice(0, 12)} of ${candidate.name}`;
  const r = await scanCandidate({ repo: candidate.root, sha: candidate.sha, material: material.material, name: candidate.name, limits: io.gitLimits, progress: progressOf(io) });
  if (r.refused !== undefined) return { exit: 4, refusal: refusedLine(label, r.refused) };
  say(scannedLine({ label, groups: [r.group], read: `${r.count} texts (its tree, its message and its diff against its parent or the empty tree) scanned` }));
  return { exit: exitOfGroups([r.group]) };
}

/** The bench's part: its own lines, with the repository named. */
async function benchPart({ values, material, io, say }) {
  progressOf(io)('bench repository: scanning its files and its history range');
  return benchHalf({ values, io, material: material.value, say: line => say(benchLine(line)) });
}

/** The parts that run, in order, each `{ exit, refusal? }`: the bench and the task repository unless a candidate is alone, then the candidate when there is one. */
async function runParts({ setup, values, io, material, say }) {
  const parts = [];
  if (!isAlone(values)) {
    parts.push(await benchPart({ values, material, io, say }));
    parts.push(await taskPart({ task: setup.task, values, material: material.value, io, say }));
  }
  if (setup.candidate !== null) parts.push(await candidatePart({ candidate: setup.candidate, material: material.value, io, say }));
  return parts;
}

/** The command: `{ exit, message }`, always. `message` holds every refusal (the command prints it); it is empty when nothing was refused. */
export async function leakcheckRepos({ values, io, explore, say }) {
  const env = io.env ?? process.env;
  const early = earlyRefusal({ values, explore, env });
  if (early !== null) return early;
  const setup = await setupOf({ values, env, repoRoot: io.repoRoot ?? REPO });
  if (!setup.ok) return refused(setup.reason);
  const material = await commandMaterial({ values, io, explore });
  if (!material.ok) return { exit: 4, message: material.reason };
  const parts = await runParts({ setup: setup.value, values, io, material, say });
  return { exit: Math.max(0, ...parts.map(p => p.exit)), message: parts.map(p => p.refusal).filter(Boolean).join('\n') };
}
