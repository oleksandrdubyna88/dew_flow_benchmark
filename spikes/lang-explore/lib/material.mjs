// A calibration is bound to the material it checked (review F1): `calibration-NN.json` records the sha256 of the task's `manifest.json`, `brute.py` and `handcheck.md` as they were when the oracle, the references and
// the mutants were checked, and every later reader (`loadCalibration`, so `init`, `rehearse` and `calibrationFor`) refuses when the folder on disk no longer hashes to them. Pure but for reading three files.
import { createHash } from 'node:crypto';
import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { taskPaths } from './tasks.mjs';

/** The recorded key of each file of the held-out folder the calibration is bound to. */
export const BOUND_FILES = Object.freeze({ manifestSha256: 'manifest.json', bruteSha256: 'brute.py', handcheckSha256: 'handcheck.md' });

const hashOf = file => (existsSync(file) ? createHash('sha256').update(readFileSync(file)).digest('hex') : null);

/** `{ manifestSha256, bruteSha256, handcheckSha256 }` of a task's folder under `home` (`null` for a file that is not there). */
export function materialOf(task, home) {
  const dir = taskPaths(task, home).heldoutDir;
  return Object.fromEntries(Object.entries(BOUND_FILES).map(([key, file]) => [key, hashOf(path.join(dir, file))]));
}

/**
 * The sentence refusing a calibration whose material is not the folder's now, or null. A calibration of a DRAFT task must record its material; task 1's, made before the binding existed, records none and is not
 * refused for that (a calibration that records any is checked like the others).
 */
export function materialProblem({ calibration, task, home }) {
  if (calibration?.material === undefined) return task.draft ? `the calibration of task ${task.id} records no material hash: calibrate again (\`explore.mjs calibrate --task ${task.id}\`)` : null;
  const now = materialOf(task, home);
  const moved = Object.entries(BOUND_FILES).filter(([key]) => calibration.material[key] !== now[key]).map(([, file]) => file);
  return moved.length === 0 ? null : `the held-out material of task ${task.id} changed since it was calibrated (${moved.join(', ')}): calibrate again before any series is made or run`;
}
