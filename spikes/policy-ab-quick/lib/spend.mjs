// Pure: the spend stop of a series, and the files a scored cell may drop.
import path from 'node:path';

/** USD a cell cost: the summary's envelope total when present, else its message estimate, else 0 (a cell that never ran). */
export const costOfSummary = summary => Number(summary?.usd ?? summary?.result?.total_cost_usd ?? 0) || 0;

export const round2 = n => Math.round(n * 100) / 100;

const round9 = n => Math.round(n * 1e9) / 1e9;

/**
 * The stop decision before a cell (or a phase). `stopAfter` is a CEILING: what starts next may cost up to its cap(s), so it is not started when
 * what is spent plus the live spend of running sessions plus `factor` x every cap about to start would pass the ceiling.
 * `caps` is one number (today's form: `spentUsd + capUsd`) or an array of caps; `liveUsd` is the running sessions' sum (default 0), `factor`
 * widens every cap (default 1; the minigit bench uses 1.1, its soft-cap overshoot) and `label` names the limit in the line (default `--stop-after`).
 * -> `{ stop, totalUsd, line }`.
 */
export function spendDecision(spentUsd, stopAfterUsd, caps = 0, { liveUsd = 0, factor = 1, label = '--stop-after' } = {}) {
  const capList = Array.isArray(caps) ? caps : [caps];
  const capsUsd = round9(capList.reduce((a, c) => a + factor * c, 0));
  const totalUsd = round9(spentUsd + liveUsd + capsUsd);
  const stop = totalUsd > stopAfterUsd;
  const head = `$${round2(spentUsd)}${liveUsd > 0 ? ` + live $${round2(liveUsd)}` : ''} + ${Array.isArray(caps) ? 'caps' : 'cap'} $${round2(capsUsd)}`;
  return { stop, totalUsd, line: stop
    ? `STOP: spent ${head} would pass ${label} $${round2(stopAfterUsd)}; no further cell is started`
    : `spend so far ${head} stays within ${label} $${round2(stopAfterUsd)}` };
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
