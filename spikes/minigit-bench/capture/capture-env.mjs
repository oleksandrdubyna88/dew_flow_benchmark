// The environment the capture needs, checked at the edge: the token, the executable, the out directory. Each check is a value.
import { accessSync, constants, existsSync, readFileSync, readdirSync, statSync } from 'node:fs';
import path from 'node:path';
import { fail, ok } from '../lib/result.mjs';

export const TOKEN_FILE_PARTS = Object.freeze(['.config', 'minigit-bench', 'claude-oauth-token']);
const HELDOUT_PARTS = Object.freeze(['rsd', 'minigit', 'heldout-src']);

/** The subscription token (D16): `CLAUDE_CODE_OAUTH_TOKEN` in the env, else the operator's mode-600 file. Never printed, never in a reason. */
export function readToken({ env, home }) {
  const fromEnv = (env.CLAUDE_CODE_OAUTH_TOKEN ?? '').trim();
  if (fromEnv !== '') return { ok: true, value: fromEnv, source: 'env' };
  const file = path.join(home, ...TOKEN_FILE_PARTS);
  if (!existsSync(file)) return fail(`no token: set CLAUDE_CODE_OAUTH_TOKEN, or put one (from \`claude setup-token\`) in ${file} with mode 600`);
  if ((statSync(file).mode & 0o077) !== 0) return fail(`the token file ${file} has mode ${(statSync(file).mode & 0o777).toString(8)}: it must be 600 (not readable by group or others)`);
  const text = readFileSync(file, 'utf8').trim();
  if (text === '') return fail(`the token file ${file} is empty`);
  return { ok: true, value: text, source: 'file' };
}

/** Is a token available, and from where? Looks at the env and the file's existence and mode only: the token itself is not read. */
export function tokenStatus({ env, home }) {
  if ((env.CLAUDE_CODE_OAUTH_TOKEN ?? '').trim() !== '') return { present: true, source: 'env' };
  const file = path.join(home, ...TOKEN_FILE_PARTS);
  if (!existsSync(file)) return { present: false, why: `no token: ${file} does not exist` };
  const mode = statSync(file).mode & 0o777;
  if ((mode & 0o077) !== 0) return { present: false, why: `${file} has mode ${mode.toString(8)}: it must be 600` };
  return { present: true, source: 'file' };
}

/** The first executable called `name` on a PATH string, or null. */
export function findOnPath(name, pathVar) {
  for (const dir of (pathVar ?? '').split(path.delimiter).filter(Boolean)) {
    const candidate = path.join(dir, name);
    try {
      accessSync(candidate, constants.X_OK);
      if (statSync(candidate).isFile()) return candidate;
    } catch {
      continue; // not here, or not executable: the next PATH entry
    }
  }
  return null;
}

function insideGitTree(dir) {
  let here = path.resolve(dir);
  for (;;) {
    if (existsSync(path.join(here, '.git'))) return here;
    const parent = path.dirname(here);
    if (parent === here) return null;
    here = parent;
  }
}

/** Reasons the out directory cannot be used (empty = fine): relative, inside a git tree, under the held-out sources, or already used. */
export function outProblems(out, { home }) {
  if (!path.isAbsolute(out)) return [`the out dir must be an absolute path, got '${out}'`];
  const problems = [];
  const git = insideGitTree(out);
  if (git) problems.push(`the out dir is inside the git tree ${git}: a capture holds raw paths and must live outside every repository`);
  const heldout = path.join(home, ...HELDOUT_PARTS);
  if (path.resolve(out) === heldout || path.resolve(out).startsWith(heldout + path.sep)) problems.push(`the out dir is under ${heldout}: nothing of a capture may sit beside the held-out sources`);
  if (existsSync(out) && readdirSync(out).length > 0) problems.push(`the out dir ${out} is not empty: a capture never overwrites another`);
  return problems;
}
