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

const short = value => (value === null || value === undefined ? 'none' : String(value).slice(0, 12));
const movedClause = ([what, recorded, today]) => `${what} moved since the series was made (recorded ${short(recorded)}, now ${short(today)})`;
const unrecordedClause = ([what, , today]) => `the series recorded nothing for ${what} (now ${short(today)})`;

/** The harness of today, as `harnessMovedProblem` compares with it: read once for a whole check. */
export const currentHarness = () => ({ runnerPin: runnerPinNow(), templatesIdentity: templatesIdentity() });

/** What to do about a moved harness: run the series where it was made, or read a kept tree against the series' history. */
const advice = inputs => `a series is run on one harness: run it on the harness it recorded (${inputs?.subject?.sha ? `its subject commit ${short(inputs.subject.sha)}` : 'no subject commit was recorded'}), or use \`rescore\`, which reads a kept tree against the series' history`;

/**
 * Why the harness of today is not the one the series was made with, or null. `files` is the workspace of the session's stack (`world.filesFor(stack)`). `now` is the harness of today (`currentHarness()`), the seam of the tests.
 * -> `harness-moved: ...` naming what moved or was not recorded, recorded against current by short hashes, and what to do.
 */
export function harnessMovedProblem({ inputs, stack, files, now = currentHarness() }) {
  const all = comparisons({ inputs, stack, files, now });
  const said = [
    ...all.filter(([, recorded, today]) => recorded !== undefined && recorded !== today).map(movedClause),
    ...all.filter(([, recorded]) => recorded === undefined).map(unrecordedClause),
  ];
  return said.length === 0 ? null : `harness-moved: ${said.join('; ')}: ${advice(inputs)}`;
}

/** The same comparison for every stack of the series' workspaces (`world.filesFor(stack)`), the first problem found, or null; the current harness is read once (`now` is the seam of the tests). For the check before a pair is launched. */
export const harnessMovedForStacks = ({ inputs, world, stacks, now = currentHarness() }) => stacks.map(stack => harnessMovedProblem({ inputs, stack, files: world.filesFor(stack), now })).find(Boolean) ?? null;
