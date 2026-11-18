// The task repository as the freeze tools read it (F2.S2 (d)): commits, trees, blobs, ancestry and the remote, through the push check's one way to ask git (`push-git.mjs`: exe + argv, no shell, a ceiling on the wait).
// Nothing here writes to a repository. A rule that is not met is a plain `fail(reason)`; a git or network failure is `fail(reason, { env: true })`, so the commands can tell exit 1 or 3 (the rule) from exit 4 (the environment).
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { runBatchCatFile, runGit } from './push-git.mjs';
import { FETCH_CONFIG } from './push-remote.mjs';

export { readTag } from './push-tag.mjs';
export const FORMAT_PATH = 'todo/tasks/FORMAT.md';
const REMOTE_TIMEOUT_MS = 120_000;
const TREE_MAX_BYTES = 64 * 1024 * 1024;
const HEX40 = /^[0-9a-f]{40}$/;
const ENV = Object.freeze({ env: true });

export const isSha = text => HEX40.test(text);

/** The failure of a git call as the tools report it: the environment's, whatever git said. */
const envFail = result => fail(result.reason, ENV);

/** ok(the 40-hex commit) for a revision of the repository that names a commit; a plain fail when it does not. */
export async function commitOf(repo, rev) {
  const found = await runGit(repo, ['rev-parse', '--verify', '--quiet', `${rev}^{commit}`], { accept: [0, 1] });
  if (!found.ok) return envFail(found);
  return found.value.code === 0 ? ok(found.value.stdout.trim()) : fail(`${rev} is not a commit of ${repo}`);
}

/** `<mode> <type> <sha>\t<path>` of `ls-tree -r -z` as `[path, { mode, type, sha }]`. */
function treeEntry(record) {
  const [meta, file] = [record.slice(0, record.indexOf('\t')), record.slice(record.indexOf('\t') + 1)];
  const [mode, type, sha] = meta.split(' ');
  return [file, { mode, type, sha }];
}

/** ok(Map(path -> { mode, type, sha })): every entry of the tree of `commit`, links and submodules included. */
export async function treeAt(repo, commit) {
  const listed = await runGit(repo, ['ls-tree', '-r', '-z', '--full-tree', commit], { maxBytes: TREE_MAX_BYTES });
  return listed.ok ? ok(new Map(listed.value.stdout.split('\0').filter(Boolean).map(treeEntry))) : envFail(listed);
}

/** ok(Map(path -> Buffer)): the blobs of `paths` as `tree` (from `treeAt`) holds them, the bytes untouched; a path the tree lacks is a plain fail. */
export async function blobsOf(repo, tree, paths) {
  const absent = paths.filter(file => tree.get(file)?.type !== 'blob');
  if (absent.length > 0) return fail(`${absent.join(', ')} ${absent.length === 1 ? 'is' : 'are'} not a file of that commit`);
  const read = await runBatchCatFile(repo, paths.map(file => tree.get(file).sha));
  if (!read.ok) return envFail(read);
  return ok(new Map(paths.map(file => [file, read.value.get(tree.get(file).sha)])));
}

/** ok(the one parent of `commit`); a plain fail for a root commit or a merge (a squash merge has exactly one parent). */
export async function parentOf(repo, commit) {
  const listed = await runGit(repo, ['rev-list', '--parents', '-n', '1', commit]);
  if (!listed.ok) return envFail(listed);
  const parents = listed.value.stdout.trim().split(' ').slice(1);
  return parents.length === 1 ? ok(parents[0]) : fail(`${commit} has ${parents.length} parents: a squash-merge commit has exactly one`);
}

/** ok(true | false): whether `ancestor` is `descendant` or one of its ancestors. */
export async function isAncestor(repo, ancestor, descendant) {
  const asked = await runGit(repo, ['merge-base', '--is-ancestor', ancestor, descendant], { accept: [0, 1] });
  return asked.ok ? ok(asked.value.code === 0) : envFail(asked);
}

/** ok([path]): the paths that differ between two commits, renames not followed (a rename is a deletion and an addition). */
export async function changedPaths(repo, before, after) {
  const listed = await runGit(repo, ['diff', '--name-only', '--no-renames', '-z', before, after]);
  return listed.ok ? ok(listed.value.stdout.split('\0').filter(Boolean)) : envFail(listed);
}

/** The lines of `git ls-remote origin <patterns>` as `Map(ref -> sha)`; the remote is asked now, never the local tracking refs. */
async function remoteRefs(repo, patterns) {
  const listed = await runGit(repo, ['ls-remote', 'origin', ...patterns], { timeoutMs: REMOTE_TIMEOUT_MS, config: FETCH_CONFIG });
  if (!listed.ok) return envFail(listed);
  return ok(new Map(listed.value.stdout.split('\n').filter(Boolean).map(line => line.split('\t').reverse())));
}

/** ok({ object, peeled }) of the remote's `refs/tags/<tag>` (`peeled` is the commit an annotated tag points at, null for a lightweight tag), or ok(null) when the remote has no such tag. */
export async function remoteTag(repo, tag) {
  const refs = await remoteRefs(repo, [`refs/tags/${tag}`, `refs/tags/${tag}^{}`]);
  if (!refs.ok) return refs;
  const object = refs.value.get(`refs/tags/${tag}`);
  return ok(object === undefined ? null : { object, peeled: refs.value.get(`refs/tags/${tag}^{}`) ?? null });
}

/** ok(the sha of the remote's `main`), or a plain fail when the remote has none. */
export async function remoteMain(repo) {
  const refs = await remoteRefs(repo, ['refs/heads/main']);
  if (!refs.ok) return refs;
  const main = refs.value.get('refs/heads/main');
  return main === undefined ? fail('the remote has no main branch') : ok(main);
}

/** ok(the URL of `origin`), for a command that must fetch from it into a folder of its own. */
export async function originUrl(repo) {
  const got = await runGit(repo, ['remote', 'get-url', 'origin']);
  return got.ok ? ok(got.value.stdout.trim()) : envFail(got);
}

/** ok(`git status --porcelain` lines): empty for a clean tree (untracked files count as not clean). */
export async function dirtyLines(repo) {
  const status = await runGit(repo, ['status', '--porcelain']);
  return status.ok ? ok(status.value.stdout.split('\n').filter(Boolean)) : envFail(status);
}
