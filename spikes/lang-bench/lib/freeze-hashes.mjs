// The hash list of a task repository's commit (F2.S2 (d), `freeze.mjs hashes`): `inputs/hashes-lang.txt`, one `sha256  path` line per file under `csharp/`, `node/`, `runner/` and `tasks/`, plus FORMAT and the six
// specs, sorted by path. The list is read from the commit's own blobs (`git ls-tree -r`, `git cat-file`), never from a checkout, so a modified tracked file or an untracked file in the working tree changes nothing.
// A link or a submodule under a root is refused: `fetchTaskRepo` refuses it too, so a list that names one could never be verified.
import { createHash } from 'node:crypto';
import path from 'node:path';
import { cleanupScope, ownedFor } from '../../../.agents/conventions/tools/lib/owned.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { blobsOf, commitOf, FORMAT_PATH, treeAt } from './freeze-git.mjs';
import { failWith, specSet, unless } from './freeze-inputs.mjs';
import { writeOutput } from './freeze-write.mjs';
import { fetchTaskRepo } from './task-repo.mjs';

export const HASHES_FILE = 'hashes-lang.txt';
const owned = ownedFor({ token: 'bench' });

/** The folders whose every file the list names (the same four as `fetchTaskRepo`'s). */
export const LIST_ROOTS = Object.freeze(['csharp', 'node', 'runner', 'tasks']);
const REGULAR_MODES = new Set(['100644', '100755']);

const underRoot = file => LIST_ROOTS.some(root => file.startsWith(`${root}/`));

/** The sentence for a tree entry under a root that is not a regular file (a symbolic link, a submodule), or null. */
function oddProblem(file, entry) {
  if (REGULAR_MODES.has(entry.mode)) return null;
  return `${file} is ${entry.mode === '120000' ? 'a symbolic link' : `a ${entry.type} entry of mode ${entry.mode}`}: a listed tree holds regular files only`;
}

/** The sentences for every file under a root that is not a regular file (a link, a submodule). */
const oddProblems = tree => [...tree.entries()].filter(([file]) => underRoot(file)).flatMap(([file, entry]) => oddProblem(file, entry) ?? []);

const byPath = (a, b) => (a < b ? -1 : 1);

/** The paths the list names, or the problems (links, a missing spec). */
function listedPaths(tree) {
  const specs = specSet(tree);
  if (!specs.ok) return specs;
  const problems = [...unless(tree.get(FORMAT_PATH)?.type === 'blob', `${FORMAT_PATH} is not a file of that commit`), ...oddProblems(tree)];
  if (problems.length > 0) return failWith(problems);
  return ok([...[...tree.keys()].filter(underRoot), FORMAT_PATH, ...specs.value.map(spec => spec.path)].sort(byPath));
}

/** ok(the tree of `commit`), or the fail that says why there is none. */
async function treeOfCommit({ repo, commit }) {
  const sha = await commitOf(repo, commit);
  return sha.ok ? treeAt(repo, sha.value) : sha;
}

/** ok({ paths, blobs }): the paths the list names and their blobs. */
async function pathsAndBlobs({ repo, tree }) {
  const paths = listedPaths(tree);
  if (!paths.ok) return paths;
  const blobs = await blobsOf(repo, tree, paths.value);
  return blobs.ok ? ok({ paths: paths.value, blobs: blobs.value }) : blobs;
}

/** ok({ text, entries }) for `commit`: the list's text (a final line feed) and its `{ sha256, path }` entries; a failure names the files the list cannot hold. */
export async function hashList({ repo, commit }) {
  const tree = await treeOfCommit({ repo, commit });
  if (!tree.ok) return tree;
  const read = await pathsAndBlobs({ repo, tree: tree.value });
  if (!read.ok) return read;
  const entries = read.value.paths.map(file => ({ sha256: createHash('sha256').update(read.value.blobs.get(file)).digest('hex'), path: file }));
  return ok({ text: entries.map(entry => `${entry.sha256}  ${entry.path}\n`).join(''), entries });
}

/** ok(true) when `fetchTaskRepo`'s own rules accept the list for `commit`: every listed file has its bytes and no file under a root is off the list. The fetch is made in an owned cache folder, removed after. */
async function verifyList({ repo, commit, text }) {
  const scope = cleanupScope();
  try {
    const fetched = await fetchTaskRepo({ sha: commit, cacheDir: owned.ownedDir(scope, 'hashcache'), repo, hashesText: text });
    return fetched.ok ? ok(true) : fail(`fetchTaskRepo does not accept the list: ${fetched.reason}`);
  } finally {
    await scope.close();
  }
}

/** `--write`: the list of `commit` published as `<inputs>/hashes-lang.txt` through an owned temporary file, a rename and the verification above. ok({ file, entries, text, notes }) | a fail. */
export async function writeHashList({ repo, commit, inputs, io }) {
  const list = await hashList({ repo, commit });
  if (!list.ok) return list;
  const sha = await commitOf(repo, commit);
  const file = path.join(inputs, HASHES_FILE);
  const written = await writeOutput({ dir: inputs, target: file, text: list.value.text, what: 'hashes', validate: held => verifyList({ repo, commit: sha.value, text: held }), io });
  return written.ok ? ok({ file, entries: list.value.entries.length, text: list.value.text, notes: written.value.notes }) : written;
}
