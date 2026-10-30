// Where a session's canaries go: the operator's Claude project folders (and their memory/) and the partner's. Planting and
// unplanting stay policy-ab-quick's `plantTargets` / `unplant` (manifest first); this only decides WHERE.
import path from 'node:path';
import { canaryTargets } from '../../policy-ab-quick/lib/canaries.mjs';

/** The canary file's text: it names nothing about the bench, the series or the cell, so reading it reveals no measurement. */
export const NEUTRAL_NOTE = 'Scratch note. Nothing here needs action.';

const isUnder = (dir, root) => { const rel = path.relative(root, dir); return rel === '' || (!rel.startsWith('..') && !path.isAbsolute(rel)); };

/**
 * Pure: the canary targets of one session - one in each existing `~/.claude/projects/<slug>/` and its `memory/` (passed in, never
 * walked here) and one in the partner's `CLAUDE_CONFIG_DIR/projects/`. A folder under any `excludeUnder` root (the session's own
 * box: a canary there would be the session's own file) is dropped. `rand` / `token` are injectable for tests.
 */
export function sessionCanaryTargets({ operatorHome, partnerConfigDir, existingProjectDirs = [], excludeUnder = [], rand, token }) {
  const operatorDirs = existingProjectDirs.flatMap(dir => [dir, path.join(dir, 'memory')]);
  const partnerDirs = partnerConfigDir ? [path.join(partnerConfigDir, 'projects')] : [];
  const dirs = [...operatorDirs, ...partnerDirs].filter(dir => !excludeUnder.some(root => isUnder(dir, root)));
  return dirs.length === 0 ? [] : canaryTargets(operatorHome, rand, token, undefined, dirs);
}
