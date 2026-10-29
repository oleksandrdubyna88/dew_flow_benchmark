// The environment the capture needs, checked at the edge: the token, the out directory. Each check is a value.
// The executable is found by lib/find-on-path.mjs, the held-out sources' location comes from lib/heldout-run.mjs.
import { existsSync, readdirSync } from 'node:fs';
import path from 'node:path';
import { heldoutSrcDir, realpathDeep } from '../lib/heldout-run.mjs';
import { readSecretFile, secretStatus } from '../lib/secrets.mjs';

export const TOKEN_FILE_PARTS = Object.freeze(['.config', 'minigit-bench', 'claude-oauth-token']);

/** The token, from the mode-600 file only (D16). Never printed, never in a reason. The checks live in lib/secrets.mjs. */
export const readToken = ({ home }) => readSecretFile({ home, parts: TOKEN_FILE_PARTS, label: 'token' });

/** Is a token available? Looks at the file's existence, type and mode and the directory's mode only: the token itself is not read. */
export const tokenStatus = ({ home }) => secretStatus({ home, parts: TOKEN_FILE_PARTS, label: 'token' });

function insideGitTree(dir) {
  let here = dir;
  for (;;) {
    if (existsSync(path.join(here, '.git'))) return here;
    const parent = path.dirname(here);
    if (parent === here) return null;
    here = parent;
  }
}

/**
 * Reasons the out directory cannot be used (empty = fine): relative, inside a git tree, under the held-out sources, or already used.
 * Every boundary comparison and every message uses the CANONICAL path (links resolved), so a link cannot lead a capture into a
 * repository or beside the held-out sources, and the message names where it would really have gone.
 */
export function outProblems(out, { home }) {
  if (!path.isAbsolute(out)) return [`the out dir must be an absolute path, got '${out}'`];
  const canonical = realpathDeep(out);
  const problems = [];
  const git = insideGitTree(canonical);
  if (git) problems.push(`the out dir ${canonical} is inside the git tree ${git}: a capture holds raw paths and must live outside every repository`);
  const heldout = realpathDeep(heldoutSrcDir(home));
  if (canonical === heldout || canonical.startsWith(heldout + path.sep)) problems.push(`the out dir ${canonical} is under the held-out sources ${heldout}: nothing of a capture may sit beside them`);
  if (existsSync(canonical) && readdirSync(canonical).length > 0) problems.push(`the out dir ${canonical} is not empty: a capture never overwrites another`);
  return problems;
}
