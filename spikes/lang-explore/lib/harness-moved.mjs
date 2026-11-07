// A series is run on ONE harness (parent D70). `init` records what the harness was: the runner's pin, the templates' identity, and the sha256 of the generated files (`STATEMENT.md`, `samples/load.json`) and of the derived
// `PACKAGES.md` of each stack. A `rescore` or a `replay` reads a kept tree against the series' history, file by file (`template-drift.mjs`); `run` cannot see drift, so when the harness of today differs from the recorded one
// the scoring of a session halts (`harness-moved`, resumable) instead of charging the harness's moves to the agent. What a series did not record cannot be seen, so it counts as a difference too.
import { createHash } from 'node:crypto';
import { GENERATED_FILES, generatedHashes, legacyGenerated } from './generated-drift.mjs';
import { runnerDriftPossible, runnerPinNow } from './harness-pin.mjs';
import { templateHistory } from './template-drift.mjs';
import { templatesIdentity } from '../../lang-bench/lib/templates-identity.mjs';

const PACKAGES = 'PACKAGES.md';
const sha256 = bytes => createHash('sha256').update(bytes).digest('hex');

/** What `run` and `rescore` give the scoring so it can tell the harness's moves from the agent's changes: `{ driftPossible, history }`. `inputs` is the series' inputs, `world` the loaded world of the task. */
export const driftSeamsOf = ({ inputs, world }) => ({ driftPossible: runnerDriftPossible({ inputs }), history: templateHistory({ inputs, legacy: legacyGenerated({ world, inputs }) }) });

/** The comparisons: `[what, recorded, now]` (a recorded `undefined` is nothing recorded). */
function comparisons({ inputs, stack, files, now }) {
  const generated = generatedHashes(files);
  return [
    ['the runner pin', inputs?.runnerPin?.manifestSha256, now.runnerPin.manifestSha256],
    ['the templates', inputs?.templatesIdentity, now.templatesIdentity],
    ...GENERATED_FILES.map(rel => [rel, inputs?.generatedSha256?.[stack]?.[rel], generated[rel]]),
    [PACKAGES, inputs?.packagesMdSha256?.[stack], files[PACKAGES] === undefined ? null : sha256(files[PACKAGES].bytes)],
  ];
}

const phrase = (list, say) => (list.length === 0 ? [] : [say(list.join(', '))]);
const namesWhere = (all, test) => all.filter(([, recorded, today]) => test(recorded, today)).map(([what]) => what);

/**
 * Why the harness of today is not the one the series was made with, or null. `files` is the workspace of the session's stack (`world.filesFor(stack)`). `now` is the seam of the tests.
 * -> `harness-moved: ...` naming what moved or was not recorded.
 */
export function harnessMovedProblem({ inputs, stack, files, now = { runnerPin: runnerPinNow(), templatesIdentity: templatesIdentity() } }) {
  const all = comparisons({ inputs, stack, files, now });
  const said = [
    ...phrase(namesWhere(all, (recorded, today) => recorded !== undefined && recorded !== today), what => `${what} moved since the series was made`),
    ...phrase(namesWhere(all, recorded => recorded === undefined), what => `the series recorded no ${what}`),
  ];
  return said.length === 0 ? null : `harness-moved: ${said.join('; ')}: a series is run on one harness, so its sessions are not built or scored here (rescore reads a kept tree against the series' history)`;
}

/** The same comparison for every stack of the series' workspaces (`world.filesFor(stack)`), the first problem found, or null. For the check before a pair is launched. */
export const harnessMovedForStacks = ({ inputs, world, stacks }) => stacks.map(stack => harnessMovedProblem({ inputs, stack, files: world.filesFor(stack) })).find(Boolean) ?? null;
