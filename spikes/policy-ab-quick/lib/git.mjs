import { runOk } from './proc.mjs';

export const gitEnv = (extra = {}) => ({
  ...process.env,
  GIT_CONFIG_NOSYSTEM: '1',
  GIT_TERMINAL_PROMPT: '0',
  ...extra,
});

export const AUTHOR_ENV = Object.freeze({
  GIT_AUTHOR_NAME: 'policy-ab', GIT_AUTHOR_EMAIL: 'policy-ab@invalid',
  GIT_COMMITTER_NAME: 'policy-ab', GIT_COMMITTER_EMAIL: 'policy-ab@invalid',
});

/** git with exe+args, returning stdout as a Buffer. */
export async function git(cwd, args, env = gitEnv()) {
  return (await runOk('git', args, { cwd, env })).stdout;
}

export async function gitText(cwd, args, env) {
  return (await git(cwd, args, env)).toString('utf8');
}

/** `git show <rev>:<path>` as raw bytes. */
export const gitShow = (clone, rev, file) => git(clone, ['show', `${rev}:${file}`]);

/** The gitlink sha of `p` at `rev`, from `git ls-tree`. */
export async function gitlinkAt(clone, rev, p) {
  return parseGitlink(await gitText(clone, ['ls-tree', rev, p]));
}

export function parseGitlink(lsTreeLine) {
  const m = /^160000 commit ([0-9a-f]{40})\t/.exec(lsTreeLine.trim());
  if (!m) throw new Error(`not a gitlink: ${lsTreeLine.trim()}`);
  return m[1];
}
