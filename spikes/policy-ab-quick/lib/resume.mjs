// Pure (plus one small mover): resuming a series that the account's limit voided. Cells that produced a data point are kept; void cells and
// cells never started are re-prepared and re-run in the original order; a void attempt is kept as history, its spend reported apart.
import fs from 'node:fs';
import path from 'node:path';
import { VOID_OUTCOME } from './ratelimit.mjs';
import { shedCell } from './shed.mjs';

/** Outcomes that are data points. A real crash of the session stays one; so does a kill. */
export const KEEP_OUTCOMES = Object.freeze(['completed', 'killed: wall', 'killed: over cap', 'crashed']);

/** keep = this cell already produced a data point. A crash recorded before the model ever ran (ranModel false) is a harness failure, not a data point. */
export function classifyCell(entry) {
  if (!entry || !KEEP_OUTCOMES.includes(entry.outcome)) return 'run';
  if (entry.outcome === 'crashed' && entry.ranModel === false) return 'run';
  return 'keep';
}

/** The plan must be the one the run map was written for: same story, seed and arms in the same order. */
export function planResume(plan, runMap, { storyId, seed }) {
  if (!runMap?.cells || !runMap.order) throw new Error('--resume: there is no run map to resume from');
  if (runMap.story !== storyId) throw new Error(`--resume: the run map is for story ${runMap.story}, not ${storyId}`);
  if (runMap.seed !== seed) throw new Error('--resume: --order is not the seed the run map was written for');
  if (JSON.stringify(runMap.order) !== JSON.stringify(plan.map(p => p.arm))) throw new Error('--resume: --order/--blocks do not reproduce the arm order in the run map');
  const keep = plan.filter(p => classifyCell(runMap.cells[p.cell]) === 'keep');
  return { keep, run: plan.filter(p => !keep.includes(p)) };
}

/** Refuse when the harness, the probe or the runs root is not the one the series started under. */
export function assertResumable({ runMap, harness, probeRecord, runsRoot }) {
  const was = runMap?.series;
  if (!was) throw new Error('--resume: the run map carries no series record (it was not written by a series start)');
  if (was.runsRoot !== runsRoot) throw new Error(`--resume: the series ran in ${was.runsRoot}, not ${runsRoot}`);
  if (was.harnessSpikeTree !== harness.spikeTree) throw new Error(`--resume: the harness changed since the series started (spike tree ${was.harnessSpikeTree} -> ${harness.spikeTree}): start a fresh series`);
  if ((was.probeCreatedUtc ?? null) !== (probeRecord?.createdUtc ?? null)) throw new Error('--resume: the probe changed since the series started: start a fresh series');
}

/** Pure: the attempt entry for a cell folder that is being moved aside. */
export function attemptEntry(entry, spendUsd, movedTo) {
  const n = (entry?.attempts?.length ?? 0) + 1;
  return { n, outcome: entry?.outcome ?? 'unfinished', void: String(entry?.outcome ?? '').startsWith('void'), spendUsd, movedTo: movedTo(n) };
}

/** The USD a cell folder cost, by its summary, else by its run's meter (an estimate), else 0. */
export function spendOfDir(dir) {
  const read = f => (fs.existsSync(f) ? JSON.parse(fs.readFileSync(f, 'utf8')) : null);
  const summary = read(path.join(dir, 'summary.json'));
  if (summary) return Number(summary.usd ?? summary.result?.total_cost_usd ?? 0) || 0;
  return Number(read(path.join(dir, 'run.json'))?.meterUsdEstimated ?? 0) || 0;
}

/**
 * Move each to-be-rerun cell's folder to <root>/void/<cell>-a<n> (a rename: nothing is deleted; the record stays), free its node_modules
 * through the safe delete, and return the run-map patch (attempt history; outcome cleared so the cell reads as not started).
 */
export function setAsideAttempts(root, cells, runMap, { srcDir, mount }) {
  const patch = {};
  for (const { cell } of cells) {
    const dir = path.join(root, cell);
    if (!fs.existsSync(dir)) continue;
    const entry = runMap.cells?.[cell];
    const attempt = attemptEntry(entry, spendOfDir(dir), n => `void/${cell}-a${n}`);
    fs.mkdirSync(path.join(root, 'void'), { recursive: true });
    const target = path.join(root, attempt.movedTo);
    if (fs.existsSync(target)) throw new Error(`refusing: ${target} already exists`);
    fs.renameSync(dir, target);
    shedCell(root, attempt.movedTo, { srcDir, mount });
    patch[cell] = { attempts: [...(entry?.attempts ?? []), attempt], outcome: null, crash: null, ranModel: null };
  }
  return patch;
}

export const isVoid = outcome => outcome === VOID_OUTCOME;
