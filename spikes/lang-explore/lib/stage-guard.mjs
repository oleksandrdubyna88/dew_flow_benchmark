// The guard before a paid launch of Stage 1 (F3.S1 (a), PR 4; stories document section 5, brief item 1): `stageGuard({ repo, series, taskRepo, paid, requireSeed, deps })` -> `ok(null)` | `fail(reason, { problems })`, every problem named,
// in this order, and a refusal happens before anything is planned or paid:
//   1. the frozen record        `freezeProblems` over the three files is empty (the freeze is `accepted`, the memory value is registered with its twelve references) and every one of the twelve references is the one `packages.json` names;
//   2. the task repository      at the record: each of the six spec blobs at the tag (`specBlobMatches`, T-L20's launch half), the workspace commit W (`workspaceCommitProblems`, T-L67's launch half), the checkout is at W, `PINNED_TASK_SHA` is
//                               the record's W and the hash list is the record's (`freeze.hashesLangSha256`). The checkout comes from `--task-repo` or `LANG_V2_TASK_REPO`;
//   3. the identities           every identity the pre-registration freezes, each read from the place that owns it, against the manifest (`stage-identity.mjs`);
//   4. paid only                PR 5's guard: the tag, the registered blobs, the clean tree, the harness identity and the seed for a data slot. PR 5's function is handed in as `deps.preregGuard`; none handed in refuses a paid launch.
// `deps` are the seams of the tests and of PR 3/PR 5: `env`, `home`, `manifest`, `live`, `readers`, `harnessAccepted`, `preregGuard`, `pinnedTaskSha`. `workspacesProblems` is `stage-init`'s check (T-L67's init half).
import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { PINNED_TASK_SHA } from '../../lang-bench/lib/task-repo.mjs';
import { specBlobMatches, workspaceCommitProblems } from '../../lang-bench/lib/spec-freeze.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { freezeProblems } from './freeze-consumers.mjs';
import { liveIdentities, identityProblems } from './stage-identity.mjs';
import { HASHES_PATH, PACKAGES_PATH, PREREG_DIR, RECORD_PATHS, readRecord } from './stage-record.mjs';
import { checkWorkspaces } from './workspaces-check.mjs';
import { FREEZE_STACKS, FREEZE_TASKS, commitOf } from './workspaces-sources.mjs';

export { RECORD_PATHS };
const SHORT = 12;
const short = value => String(value).slice(0, SHORT);
const sha256 = bytes => createHash('sha256').update(bytes).digest('hex');
const readJson = file => { try { return JSON.parse(readFileSync(file, 'utf8')); } catch { return null; } };

// ---- 1. the frozen record --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

const referenceKey = ref => `${ref.task} ${ref.stack}`;

/** The sentence for one registered reference that is not one of the twelve or whose tree hash is not the one `packages.json` names, or null. */
function referenceSentence({ ref, packages }) {
  if (!FREEZE_TASKS.includes(ref.task) || !FREEZE_STACKS.includes(ref.stack)) return `reference of task ${referenceKey(ref)} is not one of the twelve (tasks ${FREEZE_TASKS.join(', ')}, stacks ${FREEZE_STACKS.join(', ')})`;
  const named = packages?.[ref.task]?.refsTreeSha256?.[ref.stack];
  return named === ref.treeSha256 ? null : `reference task ${referenceKey(ref)}: the memory value was registered with ${short(ref.treeSha256)}, ${PACKAGES_PATH} names ${short(named ?? 'nothing')}`;
}

/** The sentences for a registered reference that is not one of the twelve, or whose tree hash is not the one `packages.json` names (the consumer half of F2's scenario 25), and for a reference of the twelve that is not registered. */
export function referenceProblems({ memoryLimit, packages }) {
  const refs = memoryLimit?.registration?.references;
  if (!Array.isArray(refs)) return [];
  const registered = new Set(refs.map(referenceKey));
  const missing = FREEZE_TASKS.flatMap(task => FREEZE_STACKS.map(stack => `${task} ${stack}`)).filter(key => !registered.has(key)).map(key => `reference of task ${key} is not registered with the memory value`);
  return [...refs.map(ref => referenceSentence({ ref, packages })).filter(Boolean), ...missing];
}

/** `{ record, problems }`: the record's own sentences (a file that cannot be read, a field the table lacks, a reference that is not the registered one). */
function recordStep(repo) {
  const { record, problems } = readRecord(repo);
  const unread = Object.keys(RECORD_PATHS).filter(name => record[name] === undefined);
  const fields = freezeProblems(record).filter(p => !unread.includes(p.split(':')[0]));
  const packages = readJson(path.join(repo, PACKAGES_PATH));
  return { record, problems: [...problems, ...fields, ...referenceProblems({ memoryLimit: record.memoryLimit, packages })] };
}

// ---- 2. the task repository at the record ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

/** The sentences for one spec whose blob at the tag is not the record's pinned sha256 (T-L20, the launch half). */
async function specProblem({ taskRepo, id, states }) {
  const row = states.specs?.[id];
  if (row === undefined) return [];
  const matched = await specBlobMatches({ repo: taskRepo, tag: states.freeze.tag, path: row.path, sha256: row.sha256, states });
  return matched.ok ? [] : [`task ${id}: ${matched.reason}`];
}

const pinProblem = ({ pinned, freeze }) => (pinned === freeze.workspaceCommit ? [] : [`PINNED_TASK_SHA (spikes/lang-bench/lib/task-repo.mjs) is ${short(pinned)}, the record's workspace commit W is ${short(freeze.workspaceCommit)}: the bare harness fetches at W`]);

/** The sha256 of the hash list in the repository, or `{ problem }` when it cannot be read (a missing list is a problem the guard names, never a throw). */
function hashesRead(repo) {
  try {
    return { have: sha256(readFileSync(path.join(repo, HASHES_PATH))) };
  } catch (e) {
    return { problem: `${HASHES_PATH} cannot be read (${e.code ?? e.message}): the hash list the record pins is not there` };
  }
}

const hashesProblem = ({ repo, freeze }) => {
  const read = hashesRead(repo);
  if (read.problem !== undefined) return [read.problem];
  return read.have === freeze.hashesLangSha256 ? [] : [`${HASHES_PATH} has sha256 ${short(read.have)}, the record's freeze.hashesLangSha256 is ${short(freeze.hashesLangSha256)}`];
};

async function headProblem({ taskRepo, freeze }) {
  const head = await commitOf(taskRepo, 'HEAD');
  if (!head.ok) return [`the task repository ${taskRepo} has no HEAD to judge (${head.reason})`];
  return head.value === freeze.workspaceCommit ? [] : [`the task repository checkout is at ${short(head.value)}, not the record's workspace commit W ${short(freeze.workspaceCommit)}: a session is launched with what W publishes`];
}

/** The sentences for the task repository against the record: the six spec blobs at the tag, W's rule, the checkout at W, the pinned commit and the hash list. `[]` when the record cannot be judged (the first step said why). */
export async function taskRepoProblems({ repo, taskRepo, record, pinnedTaskSha = PINNED_TASK_SHA }) {
  const states = record.specStates;
  if (states?.freeze === undefined) return [];
  const specs = (await Promise.all(FREEZE_TASKS.map(id => specProblem({ taskRepo, id, states })))).flat();
  const rule = (await workspaceCommitProblems({ repo: taskRepo, tag: states.freeze.tag, commit: states.freeze.workspaceCommit, states })).map(p => `workspace commit: ${p}`);
  const own = [...pinProblem({ pinned: pinnedTaskSha, freeze: states.freeze }), ...hashesProblem({ repo, freeze: states.freeze }), ...await headProblem({ taskRepo, freeze: states.freeze })];
  return [...new Set([...own, ...specs, ...rule])];
}

/**
 * The twelve workspaces built again from the checkout's HEAD against the committed ones, the one check `stage-init` and `workspacesProblems` share: `ok({ commit, problems })` (`problems` is empty when the workspaces are what the task repository publishes,
 * a changed byte is named by its file; `workspaceCommit`, when given, is the record's W and the checkout must be at it) | `fail(reason)` when the twelve cannot be checked. `check` is a seam for the tests (`checkWorkspaces`'s shape).
 */
export async function checkedWorkspaces({ taskRepo, ctx, workspaceCommit = null, check = checkWorkspaces }) {
  const checked = await check({ root: taskRepo, ctx });
  if (!checked.ok) return checked;
  const atW = workspaceCommit === null || checked.value.commit === workspaceCommit ? [] : [`the task repository checkout is at ${short(checked.value.commit)}, not the record's workspace commit ${short(workspaceCommit)}`];
  return ok({ commit: checked.value.commit, problems: [...atW, ...checked.value.findings] });
}

/**
 * `stage-init`'s check (T-L67's init half): the twelve workspaces built again from the checkout's HEAD equal the committed ones by tree hash, a changed byte is named by its file, and the checkout is at the record's W. `ctx` is `freezeContext`'s value.
 * -> `[sentence]` (empty when the workspaces are what the task repository publishes).
 */
export async function workspacesProblems(args) {
  const checked = await checkedWorkspaces(args);
  return checked.ok ? checked.value.problems : [`the twelve workspaces cannot be checked: ${checked.reason}`];
}

// ---- 3. the identities, 4. the paid checks --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

const manifestOf = (repo, deps) => deps.manifest ?? readJson(path.join(repo, PREREG_DIR, 'manifest.json'));

/** The identities' step: `{ problems, harness }`, `harness` the live harness identity this step compared (null when it could not be read). */
async function identityStep({ repo, taskRepo, record, deps }) {
  const manifest = manifestOf(repo, deps);
  if (manifest === null) return { harness: null, problems: [`${path.join(PREREG_DIR, 'manifest.json')} is not there or cannot be read: nothing is registered to compare the identities with (the pre-registration's manifest is PR 6's file)`] };
  const live = deps.live ? await deps.live() : await liveIdentities({ repo, home: deps.home ?? os.homedir(), taskRepo, record, readers: deps.readers });
  return { problems: identityProblems(manifest, live, { harnessAccepted: deps.harnessAccepted ?? [] }), harness: live.harness?.value ?? null };
}

const NO_PREREG_GUARD = 'the pre-registration guard (PR 5: the tag, the registered blobs, the clean tree, the harness identity and the seed for a data slot) is not wired into this guard: a paid launch is refused without it';

const paidStep = async ({ repo, series, requireSeed, deps }) => (deps.preregGuard ? deps.preregGuard({ repo, series, requireSeed }) : [NO_PREREG_GUARD]);

const NO_TASK_REPO = 'no task repository checkout was given: name it with --task-repo or the variable LANG_V2_TASK_REPO (a checkout of the task repository at the record\'s workspace commit)';

/** The sentences of step 2: the task repository against the record, or the sentence for a checkout that was not given. */
async function taskStep({ repo, given, rec, deps }) {
  return given === undefined ? [NO_TASK_REPO] : taskRepoProblems({ repo, taskRepo: given, record: rec.record, pinnedTaskSha: deps.pinnedTaskSha });
}

/**
 * `stageGuard({ repo, series, taskRepo, paid, requireSeed, deps })` -> `ok({ harness })` | `fail(reason, { problems })` with every problem named (see the head of this file). `harness` is the live harness identity the guard just accepted (the plan of the first data session records it). `repo` is the bench repository's root, `series` the series folder,
 * `paid` whether the launch spends (then PR 5's checks are asked as well), `requireSeed` whether it is a data slot.
 */
export async function stageGuard({ repo, series, taskRepo, paid = false, requireSeed = false, deps = {} }) {
  const given = taskRepo ?? (deps.env ?? process.env).LANG_V2_TASK_REPO;
  const rec = recordStep(repo);
  const task = await taskStep({ repo, given, rec, deps });
  const identities = await identityStep({ repo, taskRepo: given, record: rec.record, deps });
  const spends = paid ? await paidStep({ repo, series, requireSeed, deps }) : [];
  const problems = [...rec.problems, ...task, ...identities.problems, ...spends];
  return problems.length === 0 ? ok({ harness: identities.harness }) : fail(`${problems.length} problem(s) before a ${paid ? 'paid ' : ''}launch: ${problems.join('; ')}`, { problems });
}
