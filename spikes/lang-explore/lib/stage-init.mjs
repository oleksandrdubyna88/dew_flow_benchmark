// `stage-init` (F3.S1 (a), PR 3; FREE): makes the ONE series of Stage 1 (`stage-series.mjs`). Before anything is written it takes, in order: the pre-registration's guard (the tag on the remote, a clean tree under `spikes/`, the registered
// blobs: PR 5's, handed in as `io.stage.initGuardFn`; THE DEFAULT REFUSES EVERY SERIES BUT A REHEARSAL, so no paid series can be made before PR 5 merges), the harness identity against the manifest's, the check of the task repository's twelve
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
import { seedProblem } from './stage-schedule.mjs';
import { createStageSeries } from './stage-series.mjs';
import { realSlice, rehearsalSlice, slicesOf } from './stage-slices.mjs';
import { freezeContext } from './workspaces-build.mjs';
import { checkWorkspaces } from './workspaces-check.mjs';
import { runSteps } from './workspaces-pipeline.mjs';

const EXIT = Object.freeze({ done: 0, config: 4 });
/** The test seams of `stage-init` and the places PR 5's guard plugs in: `io.stage`. */
const seam = (io, name, fallback) => io.stage?.[name] ?? fallback;

/** The pilot a rehearsal series is made with: the pre-registration's answer for the real one (open point 2, answered 2026-11-19: `schedule.json`), here only so the free round can run the pilot's steps. */
export const REHEARSAL_PILOT = Object.freeze({ aa: Object.freeze({ task: '05', stack: 'node' }), daemon: '10', web: '04' });

const PAID_REFUSAL = 'stage-init: the pre-registration guard (the tag on the remote, a clean tree under spikes/, the registered blobs and the harness identity of the manifest) is not merged yet: until it is, stage-init makes a rehearsal series only (--rehearsal)';

/** The guard a paid series needs. PR 5 replaces it with the real one (`io.stage.initGuardFn`); the default refuses every series but a rehearsal's. -> `ok(pre)` | `fail`. */
export const refusingInitGuard = async ({ rehearsal }) => (rehearsal ? ok({ preregTag: null, preregCommit: null, seedSha256: null, manifestSha256: null, pilot: REHEARSAL_PILOT }) : fail(PAID_REFUSAL));

/** A rehearsal's own seed: drawn so that its 120 rids are distinct (`seedProblem`); stored in the series' state, public and of no meaning. */
export function rehearsalSeed(random) {
  const seed = `lv2-rehearsal-${random()}`;
  return seedProblem(seed) === null ? seed : rehearsalSeed(random);
}

const guardStep = async ({ io, rehearsal }) => {
  const guard = await seam(io, 'initGuardFn', refusingInitGuard)({ rehearsal });
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
  const live = await harnessIdentity(REPO);
  if (!live.ok) return live;
  return identityHolds({ pre, io, live: live.value }) ? ok({ harnessIdentity: live.value }) : fail(`the harness identity of this checkout (${short(live.value)}) is not the pre-registration manifest's (${short(pre.harnessIdentity)}) and no dated \`instrument | harness:${short(live.value)}\` line accepts it`);
}

const taskRepoStep = ({ values, io }) => {
  const root = values['task-repo'] ?? (io.env ?? process.env)[TASK_REPO_ENV];
  return root ? ok({ root }) : fail(`stage-init needs the task repository: --task-repo <checkout> or ${TASK_REPO_ENV} (its twelve workspaces are compared with what a session is given)`);
};

const findingsProblem = ({ findings }) => (findings.length > 0 ? `the workspaces the task repository publishes are not what a session is given (${findings.slice(0, 3).join('; ')}): stage-init makes no series` : null);

const checkedProblem = checked => (checked.ok ? findingsProblem(checked.value) : `the task repository's workspaces cannot be checked: ${checked.reason}`);

/** The checkout of the task repository, whose twelve published workspaces are the ones a session is given: `ok({ taskRepo: { root, commit } })` | `fail`. */
async function workspacesStep({ values, io, base }) {
  const root = taskRepoStep({ values, io });
  const ctx = root.ok ? freezeContext({ home: io.home, exploreFn: () => ok(base) }) : root;
  if (!ctx.ok) return ctx;
  const checked = await seam(io, 'workspacesFn', checkWorkspaces)({ root: root.value.root, ctx: ctx.value });
  const problem = checkedProblem(checked);
  return problem ? fail(problem) : ok({ taskRepo: { root: root.value.root, commit: checked.value.commit } });
}

const makeSlices = ({ io, base, rehearsal }) => slicesOf({ io, base, make: seam(io, 'sliceFn', rehearsal ? rehearsalSlice : realSlice) });

const V1_POLICY = Object.freeze({ prices: 'spikes/minigit-bench/inputs/prices-2026-10-29.json', policy: 'spikes/minigit-bench/inputs/crosscheck-policy.json' });

/** The price file and the crosscheck policy a session is priced with, pinned by their sha256: the pre-registration's (`pre.policy`, repo-relative), else v1's. -> `ok(pin)` | `fail` naming the file that cannot be read. */
function policyOf(pre) {
  const rel = pre.policy ?? V1_POLICY;
  const [prices, policy] = [sha256OfFile(path.join(REPO, rel.prices)), sha256OfFile(path.join(REPO, rel.policy))];
  if (!prices.ok || !policy.ok) return fail(`the price file or the crosscheck policy the pre-registration names cannot be pinned: ${(prices.ok ? policy : prices).reason}`);
  return ok({ ...rel, pricesSha256: prices.value, policySha256: policy.value });
}

/** The pinned price file and policy, made before the series is: a file that cannot be read refuses and nothing is written. */
const policyPinStep = ({ pre }) => { const pin = policyOf(pre); return pin.ok ? ok({ policyPin: pin.value }) : pin; };

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
 * `stage-init [--rehearsal] [--task-repo <checkout>]`: the series of Stage 1. `io` = `{ home, now, exploreFn, heldoutArgs, env, stage: { initGuardFn, workspacesFn, sliceFn, amendmentsFile, rand } }` (the last for tests and for PR 5's guard).
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
