// The ground of the Stage 1 series tests: a throw-away home, the real pins, and fakes for every seam of `stage-init` (the guard, the published workspaces, the per-task slices). Nothing reads the real held-out side or the task repository. Not a test file.
import '../../minigit-bench/test/fake-home.mjs';
import { createHash } from 'node:crypto';
import { ok } from '../../minigit-bench/lib/result.mjs';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { loadExplore } from '../lib/inputs.mjs';
import { quickCalibration } from '../lib/quick.mjs';
import { pinsOfTask, resolveTask } from '../lib/tasks.mjs';
import { REHEARSAL_PILOT, stageInit } from '../lib/stage-init.mjs';
import { FREEZE_TASKS } from '../lib/workspaces-sources.mjs';

export const EXPLORE = loadExplore().value;
export const NOW = new Date('2026-11-20T10:00:00.000Z');
export const COMMIT = 'a'.repeat(40);
const sha = text => createHash('sha256').update(text).digest('hex');
const PROMPTS = { source: 'fixture', template: 't', templateSha256: sha('t'), prompts: { csharp: { text: 'c', sha256: sha('c') }, node: { text: 'n', sha256: sha('n') } } };
const LIMITS = quickCalibration(EXPLORE).limits;

/** The slice of a task as a fake `sliceFn` makes it: the pins, a held-out identity of its own, hashes of its own, the prompts and the limits. */
export const sliceOfTask = task => ({
  taskPins: pinsOfTask(task), prompts: PROMPTS, limits: LIMITS, packagesMdSha256: { csharp: sha(`${task.id}:pc`), node: sha(`${task.id}:pn`) },
  generatedSha256: { csharp: { 'STATEMENT.md': sha(`${task.id}:sc`), 'samples/load.json': sha(`${task.id}:lc`) }, node: { 'STATEMENT.md': sha(`${task.id}:sn`), 'samples/load.json': sha(`${task.id}:ln`) } },
  heldoutQuick: { treeSha256: sha(`${task.id}:held`), caseCount: 7, trapCount: 2, parsed: 'fixture' },
});

export const fakeSlice = async ({ task }) => ok(sliceOfTask(task));
export const PRE = Object.freeze({ preregTag: 'prereg/lang-v2-20261120', preregCommit: COMMIT, seedSha256: sha('seed'), manifestSha256: sha('manifest'), pilot: REHEARSAL_PILOT });

/** The `io` of a stage-init over a fresh home: every seam a fake. `over` replaces seams (`stage.*`) or fields. */
export function initIo(over = {}) {
  const { stage = {}, ...rest } = over;
  return { home: tmpDir('stage-home'), say: () => {}, now: () => NOW, env: {}, stage: { initGuardFn: async () => ok(PRE), workspacesFn: async () => ok({ commit: COMMIT, findings: [] }), sliceFn: fakeSlice, rand: () => 'ab12', ...stage }, ...rest };
}

/** A paid-named Stage 1 series made through `stage-init` with the fakes: `{ io, dir, made }`. */
export async function makeSeries(over = {}, values = { 'task-repo': '/fake/task-repo' }) {
  const io = initIo(over);
  const made = await stageInit({ values, io });
  return { io, dir: made.dir, made };
}

export const TASKS = FREEZE_TASKS.map(id => resolveTask(EXPLORE, id).value);
