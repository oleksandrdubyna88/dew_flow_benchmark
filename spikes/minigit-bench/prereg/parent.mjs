// Reading the parent plan AT A COMMIT of origin/main, for the pre-registration's extraction scripts (E3b.S3): quoted text is never typed, and the
// commit it is read from must be public history (an ancestor of origin/main), so the registered words can be checked by anyone.
import { fail, ok } from '../lib/result.mjs';
import { runBounded } from '../lib/run.mjs';

export const PARENT_FILE = 'todo/PLAN_minigit_stack_bench.md';
const COMMIT = /^[0-9a-f]{7,40}$/;
const GIT_MS = 30_000;

const git = (repo, args) => runBounded('git', args, { cwd: repo, timeoutMs: GIT_MS });

/** `ok(text)` of `file` at `commit`, or `fail(reason)`: an unreadable commit, or one that is not an ancestor of `ref` (default `origin/main`). */
export async function readAtCommit({ repo, commit, file = PARENT_FILE, ref = 'origin/main' }) {
  if (!COMMIT.test(commit)) return fail(`'${commit}' is not a commit sha (7-40 hex)`);
  const ancestor = await git(repo, ['merge-base', '--is-ancestor', commit, ref]);
  if (!ancestor.ok) return ancestor;
  if (ancestor.value.code !== 0) return fail(`${commit} is not an ancestor of ${ref}: a registered quote must come from public history`);
  const shown = await git(repo, ['show', `${commit}:${file}`]);
  if (!shown.ok) return shown;
  return shown.value.code === 0 ? ok(shown.value.stdout) : fail(`git show ${commit}:${file} exited ${shown.value.code}: ${shown.value.stderr.trim().slice(-200)}`);
}

/** The full 40-hex sha of `commit` (so a header can name it unambiguously). */
export async function fullSha({ repo, commit }) {
  const r = await git(repo, ['rev-parse', '--verify', `${commit}^{commit}`]);
  return r.ok && r.value.code === 0 ? ok(r.value.stdout.trim()) : fail(`cannot resolve ${commit}`);
}
