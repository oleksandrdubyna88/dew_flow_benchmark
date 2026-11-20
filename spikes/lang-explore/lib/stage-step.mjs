// ONE unit of Stage 1, from the choice to the records (F3.S1 (a), PR 3). In order, and each refusal leaves the run-map as it was: the series and the dated lines, the unit the schedule offers, the owner's go line for its scope, the guard, the task and
// its pins, the series' pinned price file and policy, the agent box's environment, memory certification, the disk reserve and rule (`stage-launch.mjs`, PR 4), the plan. Then the one body both verbs share, `run.mjs`'s `runUnit`, inside the unit's
// clear held-out folder for a paid unit. No pause point is asked (D7, D53).
import path from 'node:path';
import { reclassifyByHand } from '../../minigit-bench/lib/bare-reclass.mjs';
import { appendLedger, readLedger } from '../../minigit-bench/lib/ledger.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { systemClock } from './clock.mjs';
import { sha256OfFile } from './file-sha.mjs';
import { REPO } from './paths.mjs';
import { pinsMoved, runUnit } from './run.mjs';
import { CANONICAL_SERIES, isRehearsal, ledgerFileOf, seriesInputs, seriesRootProblem } from './series.mjs';
import { stageHandVerdictsFor } from './stage-amendments.mjs';
import { finalizerOf } from './stage-final.mjs';
import { finishOwedRecords } from './stage-owed.mjs';
import { guardOf, ownerGoFor, readGates } from './stage-gates.mjs';
import { certifyStep, diskStep, envStep, underHeldout } from './stage-launch.mjs';
import { rehearsalIo } from './stage-rehearsal.mjs';
import { instrumentInputsNow } from './stage-prereg.mjs';
import { DATA_KIND } from './stage-schedule.mjs';
import { loadStageSeries } from './stage-series.mjs';
import { planUnit, stepOfUnit, unitOf } from './stage-unit.mjs';
import { taskFromValues } from './tasks.mjs';
import { runSteps } from './workspaces-pipeline.mjs';

export const EXIT = Object.freeze({ done: 0, stop: 2, halt: 3, config: 4, failed: 5 });
const refused = message => ({ exit: EXIT.config, message });

const canonicalName = (dir, io) => (io.seriesNameOk ?? (n => CANONICAL_SERIES.test(n)))(path.basename(dir));
const NAME_SENTENCE = dir => `${path.basename(dir)} is not a canonical series name (x-yyyymmdd-4 hex): a paid slot runs on a series made by \`explore.mjs stage-init\`, never on a copy or a rename`;

/** The series in `dir` as `stage-run` takes it: a Stage 1 series directly under the explore root, canonically named (a rehearsal's `r-` name is its own). -> `ok(series)` | `fail`. */
export function openStage({ seriesDir, io }) {
  const outside = seriesRootProblem(io.home, seriesDir);
  const series = outside ? fail(outside) : loadStageSeries(seriesDir);
  return series.ok ? nameChecked({ series, seriesDir, io }) : series;
}

const nameChecked = ({ series, seriesDir, io }) => (isRehearsal(seriesDir) || canonicalName(seriesDir, io) ? series : fail(NAME_SENTENCE(seriesDir)));

/** The exit of a schedule refusal: a stopped series is a stop (2), everything else a refusal (4). */
export const exitOfRefusal = sentence => (sentence.includes('is stopped') ? EXIT.stop : EXIT.config);

/** The by-hand verdicts of the registered amendments about one session (every dated line, v1's reader), as the loop's `handVerdictsFor` takes them. */
const handVerdictsOf = ({ series, read }) => rid => stageHandVerdictsFor(read.text, rid, series.state.seriesId);

/** The run-map as the schedule reads it: a session a dated `refuted` line names is already a void (`reclassifyByHand`, v1's own rule, pure here), whichever slot it is in, so its re-run is the next step. The loop writes the same change when it starts. `stage-status` reads the run-map through this same function (coai code round 2, PR 3, finding 5). */
export const mapAsRead = ({ series, read }) => reclassifyByHand({ map: series.map, handVerdictsFor: handVerdictsOf({ series, read }) }).map;

const unitOrRefusal = ({ series, read, what }) => {
  const chosen = unitOf({ what, state: series.state, map: mapAsRead({ series, read }), gates: read.gates });
  if (!chosen.ok) return { refusal: { exit: exitOfRefusal(chosen.reason), message: chosen.reason } };
  return chosen.value === null ? { finished: true } : { series, read, unit: chosen.value };
};

/**
 * Why the Stage 1 series in `dir` is still open, or null when it has ended: stopped (the run-map's `stopped`, the backstop `nextSlot` reads too) or every slot done (`unitOf` offers nothing, `{ finished: true }`). One road with `stage-run`:
 * a gate that refuses (no pilot GO, no seed yet) or a slot still to run is open. A series, its run-map or its gates that cannot be read is open too (fail closed), and the sentence says why.
 */
export function openReasonOf({ dir, io }) {
  const series = openStage({ seriesDir: dir, io });
  if (!series.ok) return unreadableOpen('it', series.reason);
  return series.value.map.stopped ? null : gatesOpenReason({ series: series.value, io });
}

const unreadableOpen = (what, reason) => `${what} cannot be read (${reason}), so it counts as open`;

/** The reason a series that is not stopped is open, or null when the schedule offers nothing more (every slot done). */
function gatesOpenReason({ series, io }) {
  const read = readGates({ series, io });
  return read.ok ? scheduleOpenReason(unitOrRefusal({ series, read: read.value, what: 'next' })) : unreadableOpen('its gates', read.reason);
}

const scheduleOpenReason = chosen => (chosen.finished ? null : `its schedule is not finished (${chosen.refusal ? chosen.refusal.message : 'a slot is still to run'})`);

/** The unit `what` names, with what it reads: `{ refusal }` | `{ finished: true }` | `{ series, read, unit }`. */
function chooseUnit({ dir, what, io }) {
  const series = openStage({ seriesDir: dir, io });
  const read = series.ok ? readGates({ series: series.value, io }) : series;
  return read.ok ? unitOrRefusal({ series: series.value, read: read.value, what }) : { refusal: refused(read.reason) };
}

const goStep = ({ series, unit, read }) => { const go = ownerGoFor({ series, unit, lines: read.lines }); return go.ok ? ok({ go: go.value }) : go; };

async function guardStep({ series, unit, io }) {
  const guard = guardOf({ series, io });
  const answer = await guard({ paid: !isRehearsal(series.dir), requireSeed: unit.kind === DATA_KIND });
  return answer.ok ? ok({ guard, harnessNow: answer.value?.harness ?? null }) : answer;
}

const taskStep = ({ unit, io }) => taskFromValues({ task: unit.task }, io.exploreFn);

const pinnedFiles = pin => ({ prices: path.join(REPO, pin.prices), policy: path.join(REPO, pin.policy) });
const pinChecks = (pin, files) => [['price file', files.prices, pin.pricesSha256], ['crosscheck policy', files.policy, pin.policySha256]];

/** The sentence for a pinned file that cannot be read or is not the one the series pinned, or null. */
function movedPin([label, file, want]) {
  const got = sha256OfFile(file);
  if (!got.ok) return `the ${label} ${got.reason}: nothing was changed`;
  return got.value === want ? null : `the ${label} ${file} is not the one the series pinned (${String(want).slice(0, 12)}): a change is a dated amendment, never a quiet one`;
}

/** The price file and the crosscheck policy the series pinned, as absolute paths, checked against the sha256 the series recorded. -> `ok({ policyFiles })` | `fail`. */
function policyStep({ series }) {
  const inputs = seriesInputs(series.dir);
  const pin = inputs.ok ? inputs.value.policy : undefined;
  if (pin === undefined) return fail(`${series.dir}/inputs.json pins no price file and policy: make the series with \`explore.mjs stage-init\``);
  const moved = pinChecks(pin, pinnedFiles(pin)).map(movedPin).find(Boolean);
  return moved ? fail(moved) : ok({ policyFiles: pinnedFiles(pin) });
}

const pinsStep = ({ series, task }) => { const moved = pinsMoved({ series, task }); return moved ? fail(moved) : ok({}); };
const diskCheck = s => diskStep({ ...s, map: mapAsRead(s) });
/** The instrument inputs a data plan records (`inputsAtFirstData`): read from the checkout the guard judged, for a paid series only (a rehearsal has no dated input line). */
const inputsNowOf = ({ series, unit, io }) => (unit.kind === DATA_KIND && !isRehearsal(series.dir) ? instrumentInputsNow(io.stage?.guardArgs?.repo ?? REPO) : {});

const planStep = async ({ series, unit, io, harnessNow }) => {
  const made = planUnit({ map: series.map, unit, harnessNow, inputsNow: await inputsNowOf({ series, unit, io }), ...(io.stage?.pilotRand ? { rand: io.stage.pilotRand } : {}) });
  return made.ok ? ok({ plan: made.value }) : made;
};

/** The `unit` argument of `runUnit`: the unit's kind, the policy files the series pinned, the guard, `onReady` (the owner-go event, written once the shared body has passed its last refusal check), the Stage 1 parts of the context and the records' finalizer. */
const unitArgs = ({ series, unit, read, go, guard, explore, policyFiles, plan, io }) => ({
  kind: unit.kind, policyFiles, guard, onReady: () => sayGo({ series, unit, go, plan, io }),
  ctx: { stepOf: stepOfUnit(unit), seed: unit.kind === DATA_KIND ? read.gates.seed : null, preregTag: series.state.preregTag, handVerdictsFor: handVerdictsOf({ series, read }) },
  finalize: finalizerOf({ unit, go, alias: explore.cli.model }),
});

/** The sessions of a launch (what the plan plans and what it resumes), by rid: what tells one launch of a unit from another (a void's re-run plans another rid). */
const launchRids = plan => [...plan.planned.map(p => p.rid), ...(plan.resumed ?? [])].sort();

/** Is the go event of this launch of the unit already in the ledger (a process died between the event and the run-map, and the retry calls `onReady` again)? */
const goSaid = ({ file, unit, rids }) => readLedger(file).some(l => l.event === 'owner-go' && l.pair === unit.pair && l.what === unit.what && JSON.stringify(l.rids) === JSON.stringify(rids));

/** The owner's go line a launch used, as an event line of the ledger (an event, never a charge), once for the launch: a retry of a launch that died after the event adds none. */
function sayGo({ series, unit, go, plan, io }) {
  const file = ledgerFileOf(series.dir);
  const rids = launchRids(plan);
  if (go && !goSaid({ file, unit, rids })) appendLedger(file, { event: 'owner-go', pair: unit.pair, what: unit.what, rids, date: go.date, scope: go.scope, words: go.words, t: (io.now ?? systemClock)().toISOString() });
}

/** The run of the unit: the one body `runUnit` over the series with the plan. */
const runBody = s => runUnit({ series: { ...s.series, map: s.plan.map }, pair: s.unit.pair, planned: s.plan, io: s.rig.io, task: s.task, unit: unitArgs(s) });

/** `rig` whose io tells the unit's prerequisites which clear folder its slot opened (`heldoutArgs.slotFolder`): the slot holds that task's lock for the whole unit, so the unit's reader reads the folder and does not take the lock again. */
const withSlotFolder = (rig, folder) => ({ ...rig, io: { ...rig.io, heldoutArgs: { ...rig.io.heldoutArgs, slotFolder: folder } } });

/** The run: the io of the unit (a rehearsal's stub rig) and the one body, a paid unit's inside the clear folder of its own task. The go event is `onReady`'s: a refused run adds no ledger line. */
async function launch(s) {
  const rig = await (isRehearsal(s.series.dir) ? rehearsalIo(s) : { io: s.io, close: async () => {} });
  try {
    return await underHeldout({ series: s.series, task: s.task, explore: s.explore, io: s.io, fn: folder => runBody({ ...s, rig: withSlotFolder(rig, folder) }) });
  } finally {
    await rig.close();
  }
}

/** Before any unit is offered, the records an ended unit still owes (a process died between the last scoring and `finalizerOf`): `null` when nothing is owed or it is all written, else the result that ends the call. A series or its gates that cannot be read is `chooseUnit`'s refusal. */
async function owedRecords({ dir, io }) {
  const series = openStage({ seriesDir: dir, io });
  const read = series.ok ? readGates({ series: series.value, io }) : series;
  return read.ok ? finishOwedRecords({ series: series.value, read: read.value, io }) : null;
}

/**
 * Run the unit `what` names of the series in `dir`. -> `{ exit, message, pairFile?, problems?, unit?, finished? }`: `finished` when the schedule has nothing more to run; `unit` (the slot's number, round, task and whether it is a read-back
 * slot) when one ran.
 */
export async function runStageUnit({ dir, what, io }) {
  const owed = await owedRecords({ dir, io });
  return owed ?? runChosenUnit({ dir, what, io });
}

const CHECKS = [goStep, guardStep, taskStep, policyStep, pinsStep, envStep, certifyStep, diskCheck, planStep];

/** The refusal a failed step ends the call with: its own exit when it names one (a halt of PR 4's checks is 3), else a refusal (4). */
const refusalOf = failed => ({ exit: failed.exit ?? EXIT.config, message: failed.reason });

const flagsOf = flags => (flags.length > 0 ? { flags } : {});

/** What the result tells about the unit that ran: its number, round, task and whether it is a read-back slot. */
const unitFacts = unit => ({ what: unit.what, pair: unit.pair, round: unit.round, task: unit.task, readback: unit.slot?.readback === true });

async function runChosenUnit({ dir, what, io }) {
  const chosen = chooseUnit({ dir, what, io });
  if (chosen.refusal) return chosen.refusal;
  if (chosen.finished) return { exit: EXIT.done, message: 'nothing more to run: every unit the schedule holds is finished', finished: true };
  const prepared = await runSteps(CHECKS, { ...chosen, io });
  if (!prepared.ok) return refusalOf(prepared);
  const result = await launch(prepared.value);
  return { ...result, ...flagsOf(prepared.value.flags), unit: unitFacts(chosen.unit) };
}
