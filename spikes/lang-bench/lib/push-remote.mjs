// `push-check.mjs --setup-remote`: parent §4 step 1. The remote set idempotently, a fetch, and a check that `origin/main` holds LICENSE.
// Nothing is pushed and no local branch is touched: a fetch only writes `refs/remotes/origin/*`. The fetch goes over HTTPS with gh's
// credential helper for this one command (port 22 to GitHub times out from this host; the helper is the owner's account and needs no new credential).
import { EXIT } from './push-exit.mjs';
import { runGit } from './push-git.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';

export const TASK_REPO_URL = 'https://github.com/oleksandrdubyna88/dew_flow_ai_language_comparison.git';
export const FETCH_CONFIG = Object.freeze(['credential.helper=', 'credential.helper=!gh auth git-credential']);
const FETCH_TIMEOUT_MS = 120_000;
const NO_SUCH_REMOTE = 2;

/** ok(url) when origin's fetch and push URLs are both `expected`; otherwise a `config` fail saying which is not. */
export async function verifyOrigin(repo, expected = TASK_REPO_URL) {
  const fetchUrl = await runGit(repo, ['remote', 'get-url', 'origin'], { accept: [0, NO_SUCH_REMOTE] });
  if (!fetchUrl.ok) return fetchUrl;
  if (fetchUrl.value.code === NO_SUCH_REMOTE) return fail(`${repo} has no origin: run --setup-remote first`, { kind: 'config' });
  const pushUrl = await runGit(repo, ['remote', 'get-url', '--push', 'origin']);
  if (!pushUrl.ok) return pushUrl;
  const notExpected = [['URL', fetchUrl.value.stdout.trim()], ['push URL', pushUrl.value.stdout.trim()]].find(([, url]) => url !== expected);
  return notExpected === undefined ? ok(expected) : fail(`origin's ${notExpected[0]} is ${notExpected[1]}, not the task repository ${expected}: the range is measured against origin, so a wrong origin hides commits`, { kind: 'config' });
}

const done = (exit, lines) => ({ exit, lines });

/** The remote step: returns the line it printed and whether git still agrees; a git failure is a value. */
async function setRemote(repo, url) {
  const current = await runGit(repo, ['remote', 'get-url', 'origin'], { accept: [0, NO_SUCH_REMOTE] });
  if (!current.ok) return current;
  const old = current.value.stdout.trim();
  if (current.value.code === NO_SUCH_REMOTE) {
    const added = await runGit(repo, ['remote', 'add', 'origin', url]);
    return added.ok ? { ...added, line: `remote: added origin ${url}` } : added;
  }
  if (old === url) return { ok: true, line: `remote: origin already ${url}` };
  const set = await runGit(repo, ['remote', 'set-url', 'origin', url]);
  return set.ok ? { ...set, line: `remote: re-pointed origin from ${old} to ${url}` } : set;
}

/** { exit, lines }: 0 when the remote is right and `origin/main` holds LICENSE; 3 when git, the fetch or the remote's content is not. */
export async function setupRemote({ repo, url = TASK_REPO_URL, config = FETCH_CONFIG }) {
  const remote = await setRemote(repo, url);
  if (!remote.ok) return done(EXIT.ENVIRONMENT, [`remote: failed: ${remote.reason}`]);
  const fetched = await runGit(repo, ['fetch', '--prune', '--no-tags', 'origin'], { timeoutMs: FETCH_TIMEOUT_MS, config });
  if (!fetched.ok) return done(EXIT.ENVIRONMENT, [remote.line, `fetch: failed: ${fetched.reason}`, 'no local branch was changed; fix the cause and run --setup-remote again']);
  const license = await runGit(repo, ['rev-parse', '--verify', '--quiet', 'refs/remotes/origin/main:LICENSE'], { accept: [0, 1] });
  const sha = await runGit(repo, ['rev-parse', '--short=12', 'refs/remotes/origin/main'], { accept: [0, 128] });
  if (!license.ok || !sha.ok || license.value.code !== 0) return done(EXIT.ENVIRONMENT, [remote.line, 'fetch: ok', 'origin/main: no LICENSE (or no main) after the fetch; nothing is pushed']);
  return done(EXIT.PASS, [remote.line, 'fetch: ok', `origin/main: ${sha.value.stdout.trim()} holds LICENSE`]);
}
