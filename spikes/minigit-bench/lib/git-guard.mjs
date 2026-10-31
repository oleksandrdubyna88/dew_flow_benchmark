// Where case text may live. The clear suite, the cases' repositories and the adjudication packets hold case text, so none of them may sit
// inside a git worktree (a stray `git add` would publish them). `git -C <dir> rev-parse --show-toplevel` answers that for a directory,
// resolved from its nearest existing ancestor when it does not exist yet. Nothing here reads the environment.
import { spawnSync } from 'node:child_process';
import { existsSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fail, ok } from './result.mjs';

const GIT_TIMEOUT_MS = 20_000;

const nearestExisting = dir => {
  let cur = path.resolve(dir);
  while (!existsSync(cur) && path.dirname(cur) !== cur) cur = path.dirname(cur);
  return cur;
};

/** ok(null) when `dir` is outside every git worktree; ok(toplevel) when inside one; fail when git could not answer (never a guess). */
export function gitWorktreeOf(dir) {
  const r = spawnSync('git', ['-C', nearestExisting(dir), 'rev-parse', '--show-toplevel'],
    { encoding: 'utf8', timeout: GIT_TIMEOUT_MS, env: { PATH: '/usr/bin:/bin', HOME: os.homedir(), LANG: 'C.UTF-8' } });
  if (r.error) return fail(`could not ask git whether ${dir} is inside a worktree: ${r.error.message}`);
  if (r.status === 0) return ok(r.stdout.trim());
  return r.status === 128 ? ok(null) : fail(`git rev-parse exited ${r.status} for ${dir}`);
}

/** ok(dir) when `dir` is outside every worktree; otherwise a fail that says why. */
export function refuseInsideWorktree(dir, what) {
  const inside = gitWorktreeOf(dir);
  if (!inside.ok) return inside;
  return inside.value === null ? ok(dir) : fail(`${what} ${dir} is inside the git worktree ${inside.value}: case text must never sit where a stray \`git add\` reaches it`);
}
