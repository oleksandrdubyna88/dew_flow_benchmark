// The arm <-> neutral cell mapping lives OUTSIDE the runs root (the model can read the runs root).
import fs from 'node:fs';
import path from 'node:path';
import { SPIKE_DIR } from './consts.mjs';

export const RUN_MAP = path.join(SPIKE_DIR, 'run-map.json');

export const readRunMap = (file = RUN_MAP) => (fs.existsSync(file) ? JSON.parse(fs.readFileSync(file, 'utf8')) : {});

/** Pure: a new map with `patch` merged in and `cells[cell]` merged per cell. */
export function mergeRunMap(map, patch) {
  const { cells: patchCells = {}, ...rest } = patch;
  const cells = { ...(map.cells ?? {}) };
  for (const [cell, v] of Object.entries(patchCells)) cells[cell] = { ...(cells[cell] ?? {}), ...v };
  return { ...map, ...rest, cells };
}

export function updateRunMap(patch, file = RUN_MAP) {
  const next = mergeRunMap(readRunMap(file), patch);
  fs.writeFileSync(file, `${JSON.stringify(next, null, 2)}\n`);
  return next;
}

export function armOf(cell, file = RUN_MAP) {
  const arm = readRunMap(file).cells?.[cell]?.arm;
  if (!arm) throw new Error(`no arm recorded for ${cell} in run-map.json`);
  return arm;
}
