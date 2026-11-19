// The ground of the `stage-run` tests: a paid-named Stage 1 series made through `stage-init` with fakes, a registered amendments file and a revealed seed in a throw-away folder, and a FAKE loop for the seam `io.runSeams.loopFn`
// that commits the plan into the run-map and drives each planned session to `scored` (or, for the stacks a test names, to a void) without a box, a CLI or a model. Nothing is paid, nothing real is read. Not a test file.
import { createHash } from 'node:crypto';
import { mkdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { ok } from '../../minigit-bench/lib/result.mjs';
import { readRunMap, transition, writeRunMap } from '../../minigit-bench/lib/run-map.mjs';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { runMapFileOf } from '../lib/series.mjs';
import { readbackSlots, slotsOf } from '../lib/stage-schedule.mjs';
import { PRE, makeSeries } from './stage-series-fixture.mjs';

export const SEED = 'lv2-0123456789abcdef';
export const SLOTS = slotsOf({ seed: SEED });
export const sha256 = text => createHash('sha256').update(text).digest('hex');
export const GO_PILOT = '2026-11-20 | owner-go | pilot | the owner, 2026-11-20: run the pilot';
export const GO_ALL = '2026-11-21 | owner-go | rounds:1-5 | the owner, 2026-11-21: all five rounds';
export const PILOT_GO_LINE = '2026-11-22 | pilot | GO | verdicts:0123456789ab | the team lead';

/** Drive one session of the map to its end: `scored` with data, or a void that stays `ran` (the schedule offers its one re-run). */
function endSession(map, s, voided) {
  if (s.status === 'ran') return transition(map, s.rid, 'scored').value;
  const launching = s.status === 'launching' ? map : transition(map, s.rid, 'launching', { startedAt: '2026-11-20T10:00:00.000Z' }).value;
  if (voided) return transition(launching, s.rid, 'ran', { class: 'void: upstream', data: false, usd: 0.5 }).value;
  const ran = transition(launching, s.rid, 'ran', { class: 'completed', data: true, usd: 1 }).value;
  return transition(ran, s.rid, 'scored').value;
}

const needsRun = s => ['planned', 'launching'].includes(s.status) || (s.status === 'ran' && s.data === true);

/** The fake loop: commit the plan, then end every open session of the unit. `log` gets one entry per call; `voids(session)` says which sessions end as voids. */
export const fakeLoop = (log, { voids = () => false, hold = null } = {}) => async ({ series, pair, planned, unit }) => {
  writeRunMap(runMapFileOf(series.dir), planned.map);
  const mine = Object.values(planned.map.sessions).filter(s => s.kind === unit.kind && (unit.kind !== 'data' || s.pair === pair)).filter(needsRun);
  log.push({ pair, kind: unit.kind, task: mine[0]?.task, round: mine[0]?.round, rids: mine.map(s => s.rid), stacks: mine.map(s => s.stack), attempts: mine.map(s => s.attempt), planned: planned.planned.map(p => p.rid), resumed: planned.resumed ?? [], guard: unit.guard, unit });
  if (hold) await hold();
  writeRunMap(runMapFileOf(series.dir), mine.reduce((map, s) => endSession(map, s, voids(s)), planned.map));
  return { exit: 0, message: 'done' };
};

/**
 * A Stage 1 series ready to run: `{ io, dir, log, files }`. `lines` are the dated lines of the registered amendments (the default holds the pilot's GO and the owner's go for all five rounds); `seedRevealed: false` leaves the seed file out;
 * `loop` are the options of the fake loop. Every seam is a fake.
 */
export async function runRig({ lines = [GO_PILOT, GO_ALL, PILOT_GO_LINE], seedRevealed = true, loop = {}, stage = {}, values } = {}) {
  const folder = tmpDir('stage-rig');
  const amendmentsFile = path.join(folder, 'amendments.md');
  const seedFile = path.join(folder, 'seed.txt');
  writeFileSync(amendmentsFile, `# amendments\n\n${lines.join('\n')}\n`);
  if (seedRevealed) writeFileSync(seedFile, `${SEED}\n`);
  mkdirSync(folder, { recursive: true });
  const log = [];
  const { io, dir } = await makeSeries({ stage: { initGuardFn: async () => ok({ ...PRE, seedSha256: sha256(SEED) }), guardFn: async () => ok(null), amendmentsFile, seedFile, ...stage } }, values);
  io.runSeams = { prerequisitesFn: async () => ok({ calibration: {}, heldout: {}, world: { quick: false }, token: 'fake-token', prompts: {} }), loopFn: fakeLoop(log, loop) };
  return { io, dir, log, files: { amendmentsFile, seedFile } };
}

/** The dated read-back lines (`holds`) of the read-back slots of rounds 1 and 2, for the scored attempt 1 of both stacks (the fake loop scores them). */
export const readbackHolds = () => readbackSlots({ seed: SEED }).flatMap(({ slot }) => ['csharp', 'node'].map(stack => `2026-11-23 | G-1 | holds | ${SLOTS[slot - 1].rids[stack][0]} | ~/rsd/lang-v2/e.md | the judge`));

export const mapOf = dir => readRunMap(runMapFileOf(dir)).value;
