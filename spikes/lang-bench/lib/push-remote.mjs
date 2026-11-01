// `push-check.mjs --setup-remote`: parent §4 step 1. The remote set idempotently, a fetch, and a check that `origin/main` holds LICENSE.
// Nothing is pushed and no local branch is touched: a fetch only writes `refs/remotes/origin/*`. The fetch goes over HTTPS with gh's
// credential helper for this one command (port 22 to GitHub times out from this host; the helper is the owner's account and needs no new credential).
import { EXIT } from './push-exit.mjs';
import { runGit } from './push-git.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';

export const TASK_REPO_URL = 'https://github.com/oleksandrdubyna88/dew_flow_ai_language_comparison.git';
export const FETCH_CONFIG = Object.freeze(['credential.helper=', 'credential.helper=!gh auth git-credential']);
const FETCH_TIMEOUT_MS = 120_000;
// `git remote get-url` on a missing remote: 2 in git 2.x, 128 in builds that fold it into the generic fatal exit
const NO_SUCH_REMOTE = [2, 128];

/** ok(url) of origin, ok(null) when there is no origin (exit 2, or 128 with git's own "No such remote"), else the failure of git itself. */
async function originUrl(repo, extra = []) {
  const got = await runGit(repo, ['remote', 'get-url', ...extra, 'origin'], { accept: [0, ...NO_SUCH_REMOTE] });
  if (!got.ok) return got;
  const { code, stdout, stderr } = got.value;
  if (code === 0) return ok(stdout.trim());
  return code === 2 || /no such remote/i.test(stderr) ? ok(null) : fail(`git remote get-url exited ${code} in ${repo}: ${stderr.trim().slice(-300)}`, { kind: 'env', code });
}

/** ok(url) when origin's fetch and push URLs are both `expected`; otherwise a `config` fail saying which is not. */
export async function verifyOrigin(repo, expected = TASK_REPO_URL) {
  const fetchUrl = await originUrl(repo);
  if (!fetchUrl.ok) return fetchUrl;
  if (fetchUrl.value === null) return fail(`${repo} has no origin: run --setup-remote first`, { kind: 'config' });
  const pushUrl = await originUrl(repo, ['--push']);
  if (!pushUrl.ok) return pushUrl;
  const notExpected = [['URL', fetchUrl.value], ['push URL', pushUrl.value]].find(([, url]) => url !== expected);
  return notExpected === undefined ? ok(expected) : fail(`origin's ${notExpected[0]} is ${notExpected[1]}, not the task repository ${expected}: the range is measured against origin, so a wrong origin hides commits`, { kind: 'config' });
}

/** The `refs/remotes/origin/*` refs (HEAD excluded) as a Map(refname -> sha); a git failure is a value. */
async function trackingRefs(repo) {
  const refs = await runGit(repo, ['for-each-ref', '--format=%(refname) %(objectname)', 'refs/remotes/origin']);
  return refs.ok ? ok(new Map(refs.value.stdout.split('\n').filter(l => l !== '' && !l.startsWith('refs/remotes/origin/HEAD ')).map(l => l.split(' ')))) : refs;
}

/** The remote's branches as Map(refname -> sha), spelled like the tracking refs. */
async function remoteHeads(repo, config) {
  const heads = await runGit(repo, ['ls-remote', '--heads', 'origin'], { timeoutMs: FETCH_TIMEOUT_MS, config });
  const entries = (heads.ok ? heads.value.stdout.split('\n') : []).filter(Boolean).map(l => l.split('\t')).map(([sha, ref]) => [ref.replace('refs/heads/', 'refs/remotes/origin/'), sha]);
  return heads.ok ? ok(new Map(entries)) : heads;
}

/** The refs on which the two sides disagree, in both directions: a local ref the remote lacks or has elsewhere, a remote ref not held locally. */
const disagreements = (local, remote) => [
  ...[...local].filter(([ref, sha]) => remote.get(ref) !== sha).map(([ref]) => ref),
  ...[...remote.keys()].filter(ref => !local.has(ref)),
];

/**
 * ok(null) when the local `refs/remotes/origin/*` refs and the remote's branches are the same set at the same shas, checked both ways.
 * The push range is `--not --remotes=origin`: a ref the remote no longer has, one behind it, or a remote branch never fetched makes the range
 * differ from the push, so it is refused (exit 4) until `--setup-remote` (fetch --prune) has run. A failing ls-remote is an `env` fail.
 */
export async function verifyFresh(repo, config = FETCH_CONFIG) {
  const [local, remote] = [await trackingRefs(repo), await remoteHeads(repo, config)];
  if (!local.ok || !remote.ok) return local.ok ? remote : local;
  const stale = [...new Set(disagreements(local.value, remote.value))];
  return stale.length === 0 ? ok(null) : fail(`${stale.join(', ')} ${stale.length === 1 ? 'is' : 'are'} not what the remote has: run --setup-remote (fetch --prune) first, the push range is measured against these`, { kind: 'config' });
}

const done = (exit, lines) => ({ exit, lines });

/** The remote step: returns the line it printed and whether git still agrees; a git failure is a value. */
async function setRemote(repo, url) {
  const current = await originUrl(repo);
  if (!current.ok) return current;
  const old = current.value;
  if (old === null) {
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
