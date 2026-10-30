// The environment the capture needs, checked at the edge: the token, the out directory. Each check is a value.
// The executable is found by lib/find-on-path.mjs, the held-out sources' location comes from lib/heldout-run.mjs.
import { existsSync, lstatSync, readFileSync, readdirSync, statSync } from 'node:fs';
import path from 'node:path';
import { heldoutSrcDir, realpathDeep } from '../lib/heldout-run.mjs';
import { fail, ok } from '../lib/result.mjs';

export const TOKEN_FILE_PARTS = Object.freeze(['.config', 'minigit-bench', 'claude-oauth-token']);

/**
 * The token file, or why it cannot be used. The subscription token (D16) comes from the operator's file ONLY: the directory must be mode
 * 700 and the file a regular file of mode 600. `CLAUDE_CODE_OAUTH_TOKEN` in the environment is deliberately not read, because an
 * environment variable has no mode to check: it would let a run skip the very check that keeps the token private.
 */
function tokenFileOf(home) {
  const file = path.join(home, ...TOKEN_FILE_PARTS);
  if (!existsSync(file)) return { why: `no token: put one (from \`claude setup-token\`) in ${file} with mode 600, in a directory of mode 700` };
  const dirMode = statSync(path.dirname(file)).mode & 0o777;
  if ((dirMode & 0o077) !== 0) return { why: `the token directory ${path.dirname(file)} has mode ${dirMode.toString(8)}: it must be 700 (not accessible to group or others)` };
  const stat = lstatSync(file);
  if (!stat.isFile()) return { why: `the token file ${file} is not a regular file (a link is refused): its mode could not be trusted` };
  const mode = stat.mode & 0o777;
  if ((mode & 0o077) !== 0) return { why: `the token file ${file} has mode ${mode.toString(8)}: it must be 600 (not readable by group or others)` };
  return { file };
}

/** The token, from the mode-600 file only. Never printed, never in a reason. */
export function readToken({ home }) {
  const found = tokenFileOf(home);
  if (!found.file) return fail(found.why);
  const text = readFileSync(found.file, 'utf8').trim();
  if (text === '') return fail(`the token file ${found.file} is empty`);
  return { ...ok(text), source: 'file' };
}

/** Is a token available? Looks at the file's existence, type and mode and the directory's mode only: the token itself is not read. */
export function tokenStatus({ home }) {
  const found = tokenFileOf(home);
  return found.file ? { present: true, source: 'file' } : { present: false, why: found.why };
}

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
