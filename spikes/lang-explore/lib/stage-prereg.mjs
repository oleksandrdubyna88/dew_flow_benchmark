// The pre-registration guard's configuration for Stage 1 (F3.S1 (b), PR 5): v1's guard (`spikes/minigit-bench/lib/prereg-guard.mjs`) over Stage 1's lists (`stage-lists.mjs`), the one structured exception (the wall and the stall in
// `explore.json`, once, by a dated `caps` line) and the series' own branch (`stage-branch.mjs`). No second guard: `preregGuard({ ..., config: STAGE_PREREG_CONFIG })` is the call.
import { createHash } from 'node:crypto';
import { V1_PREREG_CONFIG, textAt } from '../../minigit-bench/lib/prereg-guard.mjs';
import { capsOf, parseStageAmendments } from './stage-amendments.mjs';
import { branchProblems } from './stage-branch.mjs';
import { AMENDABLE_PATHS, PREREG_DIR, STAGE_HARNESS_DIRS, STAGE_HARNESS_TOP_DIRS, STAGE_POLICY, STAGE_PREREG_FILES } from './stage-lists.mjs';

const EXPLORE_FILE = 'spikes/lang-explore/inputs/explore.json';
const AMENDMENTS_FILE = `${PREREG_DIR}/amendments.md`;
const CAPS_FORMAT = 'YYYY-MM-DD | caps | wallMs:<n> | stallMs:<n> | <reason, with G-18>';
const HARNESS_FORMAT = 'YYYY-MM-DD | instrument | harness:<12 hex> | commit:<40 hex> <reason, with its G-id>';

// ---- the dated caps line ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------
// The one parser of dated lines is PR 3's (`stage-amendments.mjs`: `parseStageAmendments`, `capsOf`); a malformed caps line is reported there and is no line here.

/** `explore.json` without its two cap fields in time, as a new object. */
const withoutWallAndStall = ({ caps, ...rest }) => ({ ...rest, caps: Object.fromEntries(Object.entries(caps ?? {}).filter(([key]) => key !== 'wallMs' && key !== 'stallMs')) });

const parsed = text => { try { return JSON.parse(text); } catch { return null; } };

const noLine = label => `${label} differs from the tagged commit in caps.wallMs or caps.stallMs and no dated amendments line names them: add \`${CAPS_FORMAT}\``;

/** The sentence for a caps line whose values are not the ones `head` holds, or null. */
function valuesProblem({ line, head, label }) {
  const same = head.caps.wallMs === line.wallMs && head.caps.stallMs === line.stallMs;
  return same ? null : `the caps line says wallMs ${line.wallMs} and stallMs ${line.stallMs}, ${label} holds wallMs ${head.caps.wallMs} and stallMs ${head.caps.stallMs}`;
}

/** The sentence for the one caps line against the values `head` holds, or null. `line` is `capsOf`'s answer: null (no line), `{ problem }` (two lines) or `{ wallMs, stallMs }`. */
function capsLineProblem({ line, head, label }) {
  if (line === null) return noLine(label);
  return line.problem === undefined ? valuesProblem({ line, head, label }) : line.problem;
}

const unreadableCaps = label => `${label} is not readable as JSON at the tag and at HEAD, so no cap change can be told from another change`;

/** The sentence for an `explore.json` that differs from the tag outside its two cap fields, or null. */
const frozenFieldsProblem = ({ was, now, label }) => (JSON.stringify(withoutWallAndStall(was)) === JSON.stringify(withoutWallAndStall(now)) ? null : `${label} differs from the tag outside caps.wallMs and caps.stallMs (the cap in dollars and every other field are frozen)`);

/**
 * The structured exception of `explore.json` (stories document F3.S1 (b)): it may differ from the tag in `caps.wallMs` and `caps.stallMs` only, and only once, when ONE dated caps line of HEAD's amendments names the values it now holds. The cap in
 * dollars, every other cap and every other field are frozen. -> a sentence or null.
 */
async function capsProblem({ repo, commit, label }) {
  const [tagged, head, amendments] = await Promise.all([textAt(repo, commit, EXPLORE_FILE), textAt(repo, 'HEAD', EXPLORE_FILE), textAt(repo, 'HEAD', AMENDMENTS_FILE)]);
  const [was, now] = [parsed(tagged), parsed(head)];
  if (was === null || now === null) return unreadableCaps(label);
  return frozenFieldsProblem({ was, now, label }) ?? capsLineProblem({ line: capsOf(parseStageAmendments(amendments)), head: now, label });
}

// ---- the instrument inputs, frozen with the first data plan ----------------------------------------------------------------------------------------------------------------------------------

/** The instrument inputs a dated `input:` line may amend, by path, with the name the line gives each. */
const INSTRUMENT_INPUTS = Object.freeze({ 'spikes/minigit-bench/inputs/quiet.json': 'quiet.json', 'spikes/lang-explore/inputs/package-flags.json': 'package-flags.json' });

/** The 12 hex of the sha256 of each instrument input at HEAD (an absent file as the empty text): `{ 'quiet.json': hex, 'package-flags.json': hex }`. The first data plan records this beside the harness identity (`inputsAtFirstData`). */
export async function instrumentInputsNow(repo) {
  const entries = await Promise.all(Object.entries(INSTRUMENT_INPUTS).map(async ([file, name]) => [name, createHash('sha256').update(await textAt(repo, 'HEAD', file) ?? '').digest('hex').slice(0, 12)]));
  return Object.fromEntries(entries);
}

/**
 * The sentences for an instrument input that is not what the first data plan recorded (`runMap.inputsAtFirstData`). Before that plan nothing is recorded and v1's check (a dated `input:` line) decides; from it on the input the guard accepted is
 * FROZEN: it keeps passing and no later line accepts another (the instrument is frozen with the first data session, like the harness). The policy's t3 and t5 and the caps line are not instrument and are not asked here.
 */
async function frozenInputProblems(repo, runMap) {
  if (runMap.inputsAtFirstData === undefined) return [];
  const now = await instrumentInputsNow(repo);
  return Object.entries(runMap.inputsAtFirstData).filter(([name, then]) => now[name] !== then).map(([name, then]) => `${name} changed to ${now[name]} after the first data session was planned (it was ${then} then): the instrument is frozen with the first data session, so no later line accepts another`);
}

// ---- the configuration -----------------------------------------------------------------------------------------------------------------------------------------------------------------------------

/** Stage 1's configuration of v1's guard: the files, the amendable inputs by their dated lines, the policy's free fields, the caps exception, the harness folders and, for a paid launch, the series' own branch. */
export const STAGE_PREREG_CONFIG = Object.freeze({
  ...V1_PREREG_CONFIG,
  labelBase: `${PREREG_DIR}/`,
  files: STAGE_PREREG_FILES,
  amendments: AMENDMENTS_FILE,
  seedFile: `${PREREG_DIR}/seed.txt`,
  seedHash: `${PREREG_DIR}/seed.sha256`,
  amendable: INSTRUMENT_INPUTS,
  policy: STAGE_POLICY,
  structured: Object.freeze({ [EXPLORE_FILE]: capsProblem }),
  harnessDirs: STAGE_HARNESS_DIRS,
  harnessTopDirs: STAGE_HARNESS_TOP_DIRS,
  consistency: async () => [],
  harnessAcceptance: `(put the fix on origin/main, add the dated line \`${HARNESS_FORMAT}\` to ${AMENDMENTS_FILE} on origin/main, then run stage-sync --accept-harness <12 hex>)`,
  branch: branchProblems,
  frozenInputs: frozenInputProblems,
});

export { AMENDABLE_PATHS };
