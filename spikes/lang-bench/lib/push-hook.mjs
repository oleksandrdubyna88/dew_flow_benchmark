// The enforced push path of the TASK repository: a `pre-push` hook that runs the push check on exactly the sha git is about to publish,
// for every ref of the push, and refuses the push (exit 1) when a check fails or the bench checkout cannot be found. `--install-hook`
// writes it, idempotently; a hook that is not ours is never overwritten without `--force-hook` (the old one is kept beside it); and
// `--uninstall-hook` removes only a hook that carries our marker line.
//
// Never installed into the bench repository: the bench is shared by other sessions, and its rule stays the manual
// `push-check.mjs --self --branch <b>` run before a push. A repository that holds `spikes/lang-bench/push-check.mjs`, or whose origin is
// not the task repository, is refused (exit 4).
import { chmodSync, existsSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { EXIT } from './push-exit.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { runGit } from './push-git.mjs';
import { TASK_REPO_URL, verifyOrigin } from './push-remote.mjs';
import { sequence } from './push-sequence.mjs';

export const HOOK_MARKER = '# lang-v2-push-check-hook v1';
const BENCH_CHECKER = ['spikes', 'lang-bench', 'push-check.mjs'];

/** `text` as one POSIX single-quoted word. */
export const shellQuote = text => `'${String(text).replaceAll("'", "'\\''")}'`;

/** The hook's text: a POSIX `sh` script with the checker, the repository and the baked options as single-quoted words. */
export function hookScript({ checker, repo, originUrl, extraArgs = [] }) {
  const baked = ['--origin-url', originUrl, ...extraArgs].map(shellQuote).join(' ');
  return `#!/bin/sh
${HOOK_MARKER}
# Installed by push-check.mjs --install-hook; removed by --uninstall-hook. It checks the sha git pushes, per ref, and refuses the push if a
# check fails. A branch is pushed as: git push <url> <sha>:refs/heads/<branch>. A tag is not a branch: check it by hand, then --no-verify.
CHECKER=${shellQuote(checker)}
REPO=${shellQuote(repo)}
if [ ! -f "$CHECKER" ]; then
  echo "push-check hook: bench checkout not found at $CHECKER: push refused" >&2
  exit 1
fi
if ! command -v node >/dev/null 2>&1; then
  echo "push-check hook: node is not on PATH: push refused" >&2
  exit 1
fi
status=0
while read -r local_ref local_sha remote_ref remote_sha; do
  case "$local_sha" in *[!0]*) ;; *) continue ;; esac
  case "$remote_ref" in
    refs/heads/*) ;;
    *) echo "push-check hook: $remote_ref is not a branch: push refused (check it by hand, then push with --no-verify)" >&2; status=1; continue ;;
  esac
  case "$local_ref" in
    refs/heads/*) branch="\${local_ref#refs/heads/}" ;;
    *) branch="detached/\${remote_ref#refs/heads/}" ;;
  esac
  node "$CHECKER" --repo "$REPO" --branch "$branch" --tip "$local_sha" ${baked} 1>&2 || status=1
done
exit $status
`;
}

const done = (exit, ...lines) => ({ exit, lines });
const refuse = (...lines) => done(EXIT.CONFIG, ...lines);

/** ok(path) of the repository's pre-push hook file, where git itself will look (core.hooksPath and worktrees honoured). */
async function hookFile(repo) {
  const where = await runGit(repo, ['rev-parse', '--git-path', 'hooks/pre-push']);
  return where.ok ? ok(path.resolve(repo, where.value.stdout.trim())) : where;
}

const isOurs = file => readFileSync(file, 'utf8').split('\n').includes(HOOK_MARKER);

/** ok(repo), or a `config` fail when this repository must not get the hook: the bench, or one whose origin is not the task repository. */
async function checkRepo(repo, originUrl) {
  if (existsSync(path.join(repo, ...BENCH_CHECKER))) return fail(`${repo} holds spikes/lang-bench/push-check.mjs: it is the bench, which never gets the hook (its rule is the manual --self run before a push)`, { kind: 'config' });
  const origin = await verifyOrigin(repo, originUrl);
  return origin.ok ? ok(repo) : origin;
}

const failure = (what, failed) => done(failed.kind === 'config' ? EXIT.CONFIG : EXIT.ENVIRONMENT, `hook: ${what}${failed.reason}`);

/** What is at the hook path: nothing, our unchanged hook, our hook with other content, or somebody else's. */
function stateOf(file, script) {
  if (!existsSync(file)) return 'absent';
  if (!isOurs(file)) return 'foreign';
  return readFileSync(file, 'utf8') === script ? 'unchanged' : 'ours';
}

/** `{ exit, lines }` for writing `script` at `file`, given what is there; a foreign hook is kept beside ours when forced. */
function write(file, script, verb, backup) {
  if (backup) writeFileSync(`${file}.foreign`, readFileSync(file));
  writeFileSync(file, script);
  chmodSync(file, 0o755);
  return done(EXIT.PASS, `hook: ${verb} ${file}`);
}

const FOREIGN = file => `hook: ${file} exists and is not ours: left alone (--force-hook replaces it and keeps it as ${file}.foreign)`;

/** `{ exit, lines }`: write (or leave, or update) our hook in the task repository `repo`; a foreign hook is refused unless `force`. */
export async function installHook({ repo, checker, originUrl = TASK_REPO_URL, extraArgs = [], force = false }) {
  const file = await sequence([() => checkRepo(repo, originUrl), () => hookFile(repo)]);
  if (!file.ok) return failure('not installed: ', file);
  const script = hookScript({ checker: path.resolve(checker), repo: path.resolve(repo), originUrl, extraArgs });
  const actions = {
    absent: () => write(file.value, script, 'installed', false),
    ours: () => write(file.value, script, 'updated', false),
    unchanged: () => done(EXIT.PASS, `hook: unchanged ${file.value}`),
    foreign: () => (force ? write(file.value, script, 'replaced (the old one kept beside it)', true) : refuse(FOREIGN(file.value))),
  };
  return actions[stateOf(file.value, script)]();
}

/** `{ exit, lines }`: remove the hook only when it carries our marker; nothing there is not an error. */
export async function uninstallHook({ repo }) {
  const file = await hookFile(repo);
  if (!file.ok) return done(EXIT.ENVIRONMENT, `hook: ${file.reason}`);
  if (!existsSync(file.value)) return done(EXIT.PASS, `hook: none at ${file.value}`);
  if (!isOurs(file.value)) return refuse(`hook: ${file.value} is not ours: not removed`);
  rmSync(file.value);
  return done(EXIT.PASS, `hook: removed ${file.value}`);
}
