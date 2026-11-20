// `stage-init` (F3.S1 (a), PR 3; FREE): makes the ONE series of Stage 1 (`stage-series.mjs`). Before anything is written it takes, in order: the pre-registration's guard (the tag on the remote, a clean tree under `spikes/`, the registered
// blobs, the manifest, the registered prompts, the price file and policy and the pilot: PR 5's `stageInitGuard`, the default of `io.stage.initGuardFn`; before PR 6 there is no tag and no manifest, so a paid series is refused and a rehearsal is not),
// the harness identity against the manifest's, the check of the task repository's twelve
// published workspaces (`checkWorkspaces`; the commit is recorded) and every task's slice (`stage-slices.mjs`; a held-out side that is not the registered one refuses the whole series and names the task). `--rehearsal` makes an `r-`
// series for the free stub round: no tag, fixture held-out cases, a seed of its own.
import { randomBytes } from 'node:crypto';
import path from 'node:path';
import { harnessIdentity } from '../../minigit-bench/lib/prereg-guard.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { systemClock } from './clock.mjs';
import { sha256OfFile } from './file-sha.mjs';
import { subjectOf } from './commands.mjs';
import { harnessPinsNow } from './harness-pin.mjs';
import { loadExplore } from './inputs.mjs';
import { TASK_REPO_ENV } from './leak-repos-setup.mjs';
import { REPO } from './paths.mjs';
import { STAGE_AMENDMENTS_FILE, parseStageAmendments, readStageAmendments } from './stage-amendments.mjs';
import { STAGE_HARNESS_LISTS, STAGE_PILOT } from './stage-lists.mjs';
import { stageInitGuard } from './stage-prereg-guard.mjs';
import { checkedWorkspaces } from './stage-guard.mjs';
import { RECORD_PATHS, readRecord } from './stage-record.mjs';
import { seedProblem } from './stage-schedule.mjs';
import { createStageSeries } from './stage-series.mjs';
import { realSlice, rehearsalSlice, slicesOf } from './stage-slices.mjs';
import { freezeContext } from './workspaces-build.mjs';
import { checkWorkspaces } from './workspaces-check.mjs';
import { runSteps } from './workspaces-pipeline.mjs';

const EXIT = Object.freeze({ done: 0, config: 4 });
/** The test seams of `stage-init` and the places PR 5's guard plugs in: `io.stage`. */
const seam = (io, name, fallback) => io.stage?.[name] ?? fallback;

/** What a rehearsal series records of the pre-registration: nothing (no tag, no commit, no seed hash, no manifest) and the registered pilot (`STAGE_PILOT`, the one the schedule generator uses), so a rehearsal rehearses the pilot that will be run. */
const REHEARSAL_PRE = Object.freeze({ preregTag: null, preregCommit: null, seedSha256: null, manifestSha256: null, pilot: STAGE_PILOT });

/** The registered amendments' text for the guard ('' when none can be read: a dated line that cannot be read accepts nothing). */
const amendmentsOf = io => { const read = readStageAmendments(seam(io, 'amendmentsFile', STAGE_AMENDMENTS_FILE)); return read.ok ? read.value : ''; };

/**
 * The guard `stage-init` takes before it writes anything: a rehearsal's answer is its own (free: no tag, no manifest); a paid series' is PR 5's `stageInitGuard` over this checkout (`io.stage.repo` replaces it in a test).
 * `io.stage.initGuardFn` replaces the whole guard. -> `ok(pre)` | `fail`.
 */
export const defaultInitGuard = ({ rehearsal, base, io, values = {} }) => (rehearsal ? ok(REHEARSAL_PRE) : stageInitGuard({ repo: seam(io, 'repo', REPO), base, home: io.home, text: amendmentsOf(io), preregTag: values['prereg-tag'] }));

/** A rehearsal's own seed: drawn so that its 120 rids are distinct (`seedProblem`); stored in the series' state, public and of no meaning. */
export function rehearsalSeed(random) {
  const seed = `lv2-rehearsal-${random()}`;
  return seedProblem(seed) === null ? seed : rehearsalSeed(random);
}

const guardStep = async ({ io, rehearsal, base, values }) => {
  const guard = await seam(io, 'initGuardFn', defaultInitGuard)({ rehearsal, base, io, values });
  return guard.ok ? ok({ pre: guard.value }) : guard;
};

const short = hash => String(hash).slice(0, 12);
const accepts = (text, live) => parseStageAmendments(text).v1.instrument.some(i => i.identity === short(live));

const isManifests = ({ pre, live }) => pre.harnessIdentity === undefined || pre.harnessIdentity === live;

/** Is the live identity the manifest's, or accepted by a dated `instrument | harness:<12 hex>` line of the registered amendments? */
function identityHolds({ pre, io, live }) {
  const lines = readStageAmendments(seam(io, 'amendmentsFile', STAGE_AMENDMENTS_FILE));
  return isManifests({ pre, live }) || (lines.ok && accepts(lines.value, live));
}

/** The harness identity of this checkout against the manifest's (`pre.harnessIdentity`, when the guard names one). -> `ok({ harnessIdentity })` | `fail`. */
async function identityStep({ pre, io }) {
  const live = await harnessIdentity(seam(io, 'repo', REPO), STAGE_HARNESS_LISTS);
  if (!live.ok) return live;
  return identityHolds({ pre, io, live: live.value }) ? ok({ harnessIdentity: live.value }) : fail(`the harness identity of this checkout (${short(live.value)}) is not the pre-registration manifest's (${short(pre.harnessIdentity)}) and no dated \`instrument | harness:${short(live.value)}\` line accepts it`);
}

const taskRepoStep = ({ values, io }) => {
  const root = values['task-repo'] ?? (io.env ?? process.env)[TASK_REPO_ENV];
  return root ? ok({ root }) : fail(`stage-init needs the task repository: --task-repo <checkout> or ${TASK_REPO_ENV} (its twelve workspaces are compared with what a session is given)`);
};

const findingsProblem = problems => (problems.length > 0 ? `the workspaces the task repository publishes are not what a session is given (${problems.slice(0, 3).join('; ')}): stage-init makes no series` : null);

const checkedProblem = checked => (checked.ok ? findingsProblem(checked.value.problems) : `the task repository's workspaces cannot be checked: ${checked.reason}`);

/** The record's workspace commit W, which a paid series' task repository must be at (`stage-record.mjs`); a rehearsal holds no record to compare with. -> `ok(W | null)` | `fail`. */
function recordedCommit({ io, rehearsal }) {
  if (rehearsal) return ok(null);
  const commit = seam(io, 'recordFn', () => readRecord(REPO).record)()?.specStates?.freeze?.workspaceCommit;
  return typeof commit === 'string' ? ok(commit) : fail(`the frozen record (${RECORD_PATHS.specStates}) names no workspace commit W: a paid series is made over the task repository at W`);
}

/** The check of the twelve workspaces (`checkedWorkspaces`, the one `stageGuard`'s init half shares) over the checkout, at the record's W for a paid series. -> `ok(checked)` | `fail(sentence)`. */
async function checkOf({ root, ctx, io, rehearsal }) {
  const commit = recordedCommit({ io, rehearsal });
  if (!commit.ok) return commit;
  const checked = await checkedWorkspaces({ taskRepo: root, ctx, workspaceCommit: commit.value, check: seam(io, 'workspacesFn', checkWorkspaces) });
  const problem = checkedProblem(checked);
  return problem ? fail(problem) : checked;
}

/** The checkout of the task repository, whose twelve published workspaces are the ones a session is given: `ok({ taskRepo: { root, commit } })` | `fail`. */
async function workspacesStep({ values, io, base, rehearsal }) {
  const root = taskRepoStep({ values, io });
  const ctx = root.ok ? freezeContext({ home: io.home, exploreFn: () => ok(base) }) : root;
  if (!ctx.ok) return ctx;
  const checked = await checkOf({ root: root.value.root, ctx: ctx.value, io, rehearsal });
  return checked.ok ? ok({ taskRepo: { root: root.value.root, commit: checked.value.commit } }) : checked;
}

const makeSlices = ({ io, base, rehearsal, pre }) => slicesOf({ io, base, make: seam(io, 'sliceFn', rehearsal ? rehearsalSlice : realSlice), registered: pre.registeredPrompts });

const V1_POLICY = Object.freeze({ prices: 'spikes/minigit-bench/inputs/prices-2026-10-29.json', policy: 'spikes/minigit-bench/inputs/crosscheck-policy.json' });

/** The price file and the crosscheck policy a session is priced with, pinned by their sha256: the pre-registration's (`pre.policy`, repo-relative), else v1's. -> `ok(pin)` | `fail` naming the file that cannot be read. */
function policyOf({ pre, repo }) {
  const rel = pre.policy ?? V1_POLICY;
  const [prices, policy] = [sha256OfFile(path.join(repo, rel.prices)), sha256OfFile(path.join(repo, rel.policy))];
  if (!prices.ok || !policy.ok) return fail(`the price file or the crosscheck policy the pre-registration names cannot be pinned: ${(prices.ok ? policy : prices).reason}`);
  return ok({ ...rel, pricesSha256: prices.value, policySha256: policy.value });
}

/** The pinned price file and policy, made before the series is: a file that cannot be read refuses and nothing is written. */
const policyPinStep = ({ pre, io }) => { const pin = policyOf({ pre, repo: seam(io, 'repo', REPO) }); return pin.ok ? ok({ policyPin: pin.value }) : pin; };

async function wideOf({ base, pre, taskRepo, harnessIdentity: identity, policyPin }) {
  return { subject: await subjectOf(), python3: base.python3, cli: base.cli, specs: base.specs, ...harnessPinsNow(), harnessIdentity: identity, taskRepo, preregManifestSha256: pre.manifestSha256 ?? null, policy: policyPin };
}

const randomText = () => randomBytes(4).toString('hex');
const rehearsalExtra = (rehearsal, rand) => (rehearsal ? { rehearsalSeed: rehearsalSeed(rand ?? randomText) } : {});

async function makeStep(s) {
  const rand = s.io.stage?.rand;
  const made = createStageSeries({
    home: s.io.home, explore: s.base, pre: { ...s.pre, ...rehearsalExtra(s.rehearsal, rand) }, wide: await wideOf(s), slices: s.slices, date: (s.io.now ?? systemClock)(), prefix: s.rehearsal ? 'r' : 'x', rand,
  });
  return made.ok ? ok({ made: made.value }) : made;
}

const slicesStep = async s => makeSlices(s);

/**
 * `stage-init [--rehearsal] [--task-repo <checkout>] [--prereg-tag <name>]`: the series of Stage 1. `io` = `{ home, now, exploreFn, heldoutArgs, env, stage: { initGuardFn, workspacesFn, sliceFn, amendmentsFile, rand } }` (the last for tests and for PR 5's guard).
 * -> `{ exit, message, dir? }`: 0 made | 4 a refusal, nothing written.
 */
export async function stageInit({ values, io }) {
  const base = (io.exploreFn ?? loadExplore)();
  if (!base.ok) return { exit: EXIT.config, message: base.reason };
  const done = await runSteps([guardStep, identityStep, workspacesStep, slicesStep, policyPinStep, makeStep], { base: base.value, rehearsal: values.rehearsal === true, values, io });
  if (!done.ok) return { exit: EXIT.config, message: done.reason };
  const { made, rehearsal } = done.value;
  return { exit: EXIT.done, dir: made.dir, message: `Stage 1 series ${made.state.seriesId} created at ${made.dir}${rehearsal ? ' (a rehearsal: the stub CLI, fixture held-out cases, a seed of its own)' : ''}; the verbs are stage-run and stage-status` };
}
