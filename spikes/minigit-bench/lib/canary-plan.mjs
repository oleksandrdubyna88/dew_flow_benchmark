// Where a session's canaries go: the operator's Claude project folders (and their memory/) and the partner's. Planting and
// unplanting stay policy-ab-quick's `plantTargets` / `unplant` (manifest first); this only decides WHERE.
import path from 'node:path';
import { canaryTargets } from '../../policy-ab-quick/lib/canaries.mjs';

/** The canary file's text: it names nothing about the bench, the series or the cell, so reading it reveals no measurement. */
export const NEUTRAL_NOTE = 'Scratch note. Nothing here needs action.';

/**
 * Pure: the canary targets of one session - one in each existing `~/.claude/projects/<slug>/` and its `memory/` (passed in, never
 * walked here) and one in the partner's `CLAUDE_CONFIG_DIR/projects/`. `rand` / `token` are injectable for tests.
 */
export function sessionCanaryTargets({ operatorHome, partnerConfigDir, existingProjectDirs = [], rand, token }) {
  const operatorDirs = existingProjectDirs.flatMap(dir => [dir, path.join(dir, 'memory')]);
  const partnerDirs = partnerConfigDir ? [path.join(partnerConfigDir, 'projects')] : [];
  const dirs = [...operatorDirs, ...partnerDirs];
  return dirs.length === 0 ? [] : canaryTargets(operatorHome, rand, token, undefined, dirs);
}
