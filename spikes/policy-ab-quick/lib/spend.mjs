// Pure: the spend stop of a series, and the files a scored cell may drop.
import path from 'node:path';

/** USD a cell cost: the summary's envelope total when present, else its message estimate, else 0 (a cell that never ran). */
export const costOfSummary = summary => Number(summary?.usd ?? summary?.result?.total_cost_usd ?? 0) || 0;

export const round2 = n => Math.round(n * 100) / 100;

/**
 * The stop decision before a cell. `stopAfter` is a CEILING: the next cell may cost up to `cap`, so it is not started when
 * what is spent plus that cap would pass the ceiling.
 */
export function spendDecision(spentUsd, stopAfterUsd, capUsd = 0) {
  const stop = spentUsd + capUsd > stopAfterUsd;
  return { stop, line: stop
    ? `STOP: spent $${round2(spentUsd)} + cap $${round2(capUsd)} would pass --stop-after $${round2(stopAfterUsd)}; no further cell is started`
    : `spend so far $${round2(spentUsd)} + cap $${round2(capUsd)} stays within --stop-after $${round2(stopAfterUsd)}` };
}

/**
 * The exact paths a scored cell sheds to free disk: its score/ copies and its tree/node_modules (and the
 * conventions' node_modules in the resolver layout). Every one is built from the runs root + the cell name, nothing else.
 */
export function shedTargets(runsRoot, cell, { srcDir, mount } = {}) {
  const cellDir = path.join(path.resolve(runsRoot), cell);
  const tree = path.join(cellDir, 'tree');
  return [path.join(cellDir, 'score'), path.join(tree, 'node_modules'),
    ...(srcDir ? [path.join(tree, ...srcDir.split('/'), 'node_modules')] : []),
    ...(mount ? [path.join(tree, ...mount.split('/'), 'node_modules')] : [])];
}

/** A path is deletable only when it sits strictly below the runs root and its last segment is one we shed. */
export function isShedPath(target, runsRoot) {
  const rel = path.relative(path.resolve(runsRoot), path.resolve(target));
  if (rel === '' || rel.startsWith('..') || path.isAbsolute(rel)) return false;
  const parts = rel.split(path.sep);
  return parts.length >= 2 && ['score', 'node_modules'].includes(parts.at(-1));
}
