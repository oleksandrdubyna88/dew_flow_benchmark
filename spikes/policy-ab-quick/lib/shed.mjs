// The only deletion in the spike: a scored cell's score/ copies and node_modules folders, by exact path, under the runs root.
import fs from 'node:fs';
import { isShedPath, shedTargets } from './spend.mjs';

/** Removes each existing shed target of one cell; returns what was removed and what was refused. */
export function shedCell(runsRoot, cell, opts, rm = p => fs.rmSync(p, { recursive: true, force: true })) {
  const removed = [];
  const refused = [];
  for (const target of shedTargets(runsRoot, cell, opts)) {
    if (!isShedPath(target, runsRoot)) { refused.push(target); continue; }
    if (!fs.existsSync(target)) continue;
    rm(target);
    removed.push(target);
  }
  return { removed, refused };
}
