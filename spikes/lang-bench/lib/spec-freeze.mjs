// The freeze guards (F2.S2 (d)): what F3.S1's launch half asks of the task repository before a series starts, and what `freeze.mjs check-workspace` asks of the workspace commit W. Both read the tag the RECORD names:
// `inputs/specs.d/spec-states.json` holds one freeze, `freeze.tag`, and a tag in `freeze.superseded` is never read as the freeze. The record is handed in as `states` (the parsed file); with none handed in, only the
// repository is asked, which is the case while the freeze is still being made (no record exists before W is accepted).
//   specBlobMatches({ repo, tag, path, sha256, states })   T-L20's guard half: the blob of the spec at the tag has the pinned sha256.            -> ok(true) | fail(reason)
//   workspaceCommitProblems({ repo, tag, commit, states }) T-L67: C (the tag's commit) is an ancestor of W, every spec and FORMAT blob at W is C's, and
//                                                          `git diff --name-only C W` lists only files of the twelve workspace folders.      -> [sentence] ([] when W passes)
import { createHash } from 'node:crypto';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { blobsOf, commitOf, changedPaths, FORMAT_PATH, isAncestor, treeAt } from './freeze-git.mjs';
import { problemsOf, specSet } from './freeze-inputs.mjs';

const LISTED_PATHS = 12;
const STACKS = Object.freeze(['csharp', 'node']);

/** The sentence when `tag` is not the one freeze the record names (a superseded tag, or any other); null when it is, or when there is no record to ask. */
export function recordProblem(tag, states) {
  if (states === undefined || states === null) return null;
  const { freeze } = states;
  if ((freeze?.superseded ?? []).some(entry => entry.tag === tag)) return `the tag ${tag} is superseded by ${freeze.tag}: only the record's freeze.tag is the freeze, and a superseded tag is never read as one`;
  return freeze?.tag === tag ? null : `the tag ${tag} is not the record's freeze tag (${freeze?.tag ?? 'none'})`;
}

const short = sha => sha.slice(0, 10);
const listed = paths => `${paths.slice(0, LISTED_PATHS).join(', ')}${paths.length > LISTED_PATHS ? ` and ${paths.length - LISTED_PATHS} more` : ''}`;

/** ok(true) when the blob of `file` at `tag` has `sha256`; a fail naming the file, the tag and both hashes otherwise (a path or a tag the repository lacks is a fail too). */
export async function specBlobMatches({ repo, tag, path: file, sha256, states }) {
  const refused = recordProblem(tag, states);
  if (refused !== null) return fail(refused);
  const commit = await commitOf(repo, `refs/tags/${tag}`);
  const tree = commit.ok ? await treeAt(repo, commit.value) : commit;
  const blobs = tree.ok ? await blobsOf(repo, tree.value, [file]) : tree;
  if (!blobs.ok) return fail(`${file} at the tag ${tag}: ${blobs.reason}`);
  const actual = createHash('sha256').update(blobs.value.get(file)).digest('hex');
  return actual === sha256 ? ok(true) : fail(`${file} at the tag ${tag} has sha256 ${actual}, not the pinned ${sha256}`);
}

/** The blobs of the six specs and FORMAT that W holds differently from C: a sentence per path. */
function blobProblems({ tree, specs }) {
  const paths = [...specs.map(spec => spec.path), FORMAT_PATH];
  return paths.flatMap(file => (tree.w.get(file)?.sha === tree.c.get(file)?.sha ? [] : [`${file} at W has another blob than at C (${short(tree.c.get(file)?.sha ?? 'none')} at C, ${short(tree.w.get(file)?.sha ?? 'none')} at W): a workspace commit never changes a spec or FORMAT`]));
}

/** The changed paths that are not files of the twelve workspace folders: `csharp/<NN-slug>/…` and `node/<NN-slug>/…` of the six tasks. */
function pathProblems({ changed, specs }) {
  const folders = new Set(specs.flatMap(spec => STACKS.map(stack => `${stack}/${spec.slug}/`)));
  const outside = changed.filter(file => ![...folders].some(folder => file.startsWith(folder)));
  return outside.length === 0 ? [] : [`${outside.length} path(s) changed between C and W outside the twelve workspace folders (csharp/<NN-slug>/ and node/<NN-slug>/ of the six tasks): ${listed(outside)}`];
}

/** ok([sentence]) for W against C: the T-L67 rule as sentences, `[]` when W passes; a git failure is a fail. `base` is C, a commit of the repository. */
export async function workspaceProblems({ repo, base, commit }) {
  const [c, w] = [await commitOf(repo, base), await commitOf(repo, commit)];
  const found = [c, w].find(entry => !entry.ok);
  if (found !== undefined) return found;
  const [ancestor, trees, changed] = [await isAncestor(repo, c.value, w.value), { c: await treeAt(repo, c.value), w: await treeAt(repo, w.value) }, await changedPaths(repo, c.value, w.value)];
  const broken = [ancestor, trees.c, trees.w, changed].find(entry => !entry.ok);
  if (broken !== undefined) return broken;
  const specs = specSet(trees.c.value);
  if (!specs.ok) return ok(problemsOf(specs).map(problem => `at C: ${problem}`));
  const walk = ancestor.value ? [] : [`C ${short(c.value)} is not an ancestor of W ${short(w.value)}: W is not a descendant of C (a tag on the freeze branch's head instead of the squash commit C is on no path of main)`];
  return ok([...walk, ...blobProblems({ tree: { c: trees.c.value, w: trees.w.value }, specs: specs.value }), ...pathProblems({ changed: changed.value, specs: specs.value })]);
}

/** The sentences for W (`commit`) against the tag's commit; `[]` when W passes. A repository that cannot be read is one sentence, never a pass. */
export async function workspaceCommitProblems({ repo, tag, commit, states }) {
  const refused = recordProblem(tag, states);
  if (refused !== null) return [refused];
  const base = await commitOf(repo, `refs/tags/${tag}`);
  if (!base.ok) return [`the tag ${tag}: ${base.reason}`];
  const found = await workspaceProblems({ repo, base: base.value, commit });
  return found.ok ? found.value : [`the workspace commit ${commit} cannot be judged: ${found.reason}`];
}
