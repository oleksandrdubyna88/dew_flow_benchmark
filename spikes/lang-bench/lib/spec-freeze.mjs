// The freeze guards (F2.S2 (d)): what F3.S1's launch half asks of the task repository before a series starts, and what `freeze.mjs check-workspace` asks of the workspace commit W. Both read the tag the RECORD names:
// `inputs/specs.d/spec-states.json` holds one freeze, `freeze.tag`, and a tag in `freeze.superseded` is never read as the freeze. The record is handed in as `states` (the parsed file); with none handed in, only the
// repository is asked, which is the case while the freeze is still being made (no record exists before W is accepted).
//   specBlobMatches({ repo, tag, path, sha256, states, inputs })   T-L20's guard half: the blob of the spec at the tag has the pinned sha256.            -> ok(true) | fail(reason)
//   workspaceCommitProblems({ repo, tag, commit, states, inputs }) T-L67: C (the tag's commit) is an ancestor of W, every spec and FORMAT blob at W (mode too) is C's, and
//                                                                  `git diff --name-only C W` lists only files of the twelve workspace folders.      -> [sentence] ([] when W passes)
// `states` is the record: omitted, the guard reads `<inputs>/specs.d/spec-states.json` itself (and refuses when there is none: no freeze is accepted); `null` says, on purpose, that the freeze is still being made
// and there is no record yet. Given a record, the LOCAL tag ref must be the recorded tag object and peel to the recorded commit, not merely carry the recorded name.
import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { blobsOf, commitOf, changedPaths, FORMAT_PATH, isAncestor, readTag, treeAt } from './freeze-git.mjs';
import { INPUTS_DIR, problemsOf, specSet, unless } from './freeze-inputs.mjs';

const LISTED_PATHS = 12;
const STACKS = Object.freeze(['csharp', 'node']);

const noRecord = states => states === undefined || states === null;
const nameOf = freeze => freeze?.tag ?? 'none';
const supersededBy = (freeze, tag) => (freeze?.superseded ?? []).some(entry => entry.tag === tag);

/** The sentence when `tag` is not the one freeze the record names (a superseded tag, or any other); null when it is, or when there is no record to ask. */
export function recordProblem(tag, states) {
  if (noRecord(states)) return null;
  const { freeze } = states;
  if (supersededBy(freeze, tag)) return `the tag ${tag} is superseded by ${freeze.tag}: only the record's freeze.tag is the freeze, and a superseded tag is never read as one`;
  return nameOf(freeze) === tag ? null : `the tag ${tag} is not the record's freeze tag (${nameOf(freeze)})`;
}

const short = sha => sha.slice(0, 10);
const listed = paths => `${paths.slice(0, LISTED_PATHS).join(', ')}${paths.length > LISTED_PATHS ? ` and ${paths.length - LISTED_PATHS} more` : ''}`;

/** ok(the record, or null for the freeze in progress): `states` as given, else `<inputs>/specs.d/spec-states.json` read here; no readable record is a fail. */
export function recordOf({ states, inputs = INPUTS_DIR }) {
  if (states !== undefined) return ok(states);
  const file = path.join(inputs, 'specs.d', 'spec-states.json');
  try {
    return ok(JSON.parse(readFileSync(file, 'utf8')));
  } catch (error) {
    return fail(`no accepted freeze is recorded: ${file} cannot be read (${error.code ?? error.message}); a caller that is making the freeze says so with states: null`);
  }
}

/** The sentences when the LOCAL tag is not the recorded object or does not peel to the recorded commit. */
async function localTagProblems({ repo, tag, freeze }) {
  const read = await readTag(repo, tag);
  if (!read.ok) return [`the tag ${tag}: ${read.reason}`];
  const object = read.value.object === freeze.tagObjectSha ? [] : [`the local tag ${tag} is the tag object ${read.value.object}, not the recorded ${freeze.tagObjectSha}`];
  const peeled = read.value.commit === freeze.commitSha ? [] : [`the local tag ${tag} peels to the commit ${read.value.commit}, not the recorded ${freeze.commitSha}`];
  return [...object, ...peeled];
}

/** The sentences when `tag` is not the freeze the record names, name first (a superseded tag, another tag) and then the local tag's object and commit; `[]` for no record (`null`). */
export async function recordProblems({ repo, tag, states }) {
  if (states === null) return [];
  const named = recordProblem(tag, states);
  return named === null ? localTagProblems({ repo, tag, freeze: states.freeze }) : [named];
}

/** The sentences when `tag` is not the accepted freeze of the record (given, or read from the inputs): none, or the record's own refusals. */
async function recordRefusals({ repo, tag, states, inputs }) {
  const record = recordOf({ states, inputs });
  return record.ok ? recordProblems({ repo, tag, states: record.value }) : [record.reason];
}

/** ok(the bytes of `file` at `tag`) | a fail naming the file and the tag. */
export async function blobAtTag({ repo, tag, file }) {
  const commit = await commitOf(repo, `refs/tags/${tag}`);
  const tree = commit.ok ? await treeAt(repo, commit.value) : commit;
  const blobs = tree.ok ? await blobsOf(repo, tree.value, [file]) : tree;
  return blobs.ok ? ok(blobs.value.get(file)) : fail(`${file} at the tag ${tag}: ${blobs.reason}`);
}

/** ok(true) when the blob of `file` at `tag` has `sha256`; a fail naming the file, the tag and both hashes otherwise (a path or a tag the repository lacks is a fail too). */
export async function specBlobMatches({ repo, tag, path: file, sha256, states, inputs }) {
  const [refused] = await recordRefusals({ repo, tag, states, inputs });
  if (refused !== undefined) return fail(refused);
  const blob = await blobAtTag({ repo, tag, file });
  if (!blob.ok) return blob;
  const actual = createHash('sha256').update(blob.value).digest('hex');
  return actual === sha256 ? ok(true) : fail(`${file} at the tag ${tag} has sha256 ${actual}, not the pinned ${sha256}`);
}

/** The blobs of the six specs and FORMAT that W holds differently from C: a sentence per path. */
function blobProblems({ tree, specs }) {
  const paths = [...specs.map(spec => spec.path), FORMAT_PATH];
  const modes = paths.filter(file => tree.w.get(file)?.mode !== tree.c.get(file)?.mode).map(file => `${file} at W has another mode (${tree.w.get(file)?.mode ?? 'none'}) than at C (${tree.c.get(file)?.mode ?? 'none'}): a workspace commit never changes a spec or FORMAT`);
  return [...modes, ...blobsDiffer({ tree, paths })];
}

/** The paths whose blob at W is not C's. */
function blobsDiffer({ tree, paths }) {
  return paths.flatMap(file => (tree.w.get(file)?.sha === tree.c.get(file)?.sha ? [] : [`${file} at W has another blob than at C (${short(tree.c.get(file)?.sha ?? 'none')} at C, ${short(tree.w.get(file)?.sha ?? 'none')} at W): a workspace commit never changes a spec or FORMAT`]));
}

/** The changed paths that are not files of the twelve workspace folders: `csharp/<NN-slug>/…` and `node/<NN-slug>/…` of the six tasks. */
function pathProblems({ changed, specs }) {
  const folders = new Set(specs.flatMap(spec => STACKS.map(stack => `${stack}/${spec.slug}/`)));
  const outside = changed.filter(file => ![...folders].some(folder => file.startsWith(folder)));
  return outside.length === 0 ? [] : [`${outside.length} path(s) changed between C and W outside the twelve workspace folders (csharp/<NN-slug>/ and node/<NN-slug>/ of the six tasks): ${listed(outside)}`];
}

/** ok({ c, w, ancestor, trees, changed }): the two commits, whether C is an ancestor of W, both trees and the changed paths; the first failure otherwise. */
async function readHistory({ repo, base, commit }) {
  const [c, w] = [await commitOf(repo, base), await commitOf(repo, commit)];
  const bad = [c, w].find(entry => !entry.ok);
  if (bad !== undefined) return bad;
  const [ancestor, tc, tw, changed] = [await isAncestor(repo, c.value, w.value), await treeAt(repo, c.value), await treeAt(repo, w.value), await changedPaths(repo, c.value, w.value)];
  return [ancestor, tc, tw, changed].find(entry => !entry.ok) ?? ok({ c: c.value, w: w.value, ancestor: ancestor.value, trees: { c: tc.value, w: tw.value }, changed: changed.value });
}

/** ok([sentence]) for W against C: the T-L67 rule as sentences, `[]` when W passes; a git failure is a fail. `base` is C, a commit of the repository. */
export async function workspaceProblems({ repo, base, commit }) {
  const read = await readHistory({ repo, base, commit });
  if (!read.ok) return read;
  const { c, w, ancestor, trees, changed } = read.value;
  const specs = specSet(trees.c);
  if (!specs.ok) return ok(problemsOf(specs).map(problem => `at C: ${problem}`));
  const walk = unless(ancestor, `C ${short(c)} is not an ancestor of W ${short(w)}: W is not a descendant of C (a tag on the freeze branch's head instead of the squash commit C is on no path of main)`);
  return ok([...walk, ...blobProblems({ tree: trees, specs: specs.value }), ...pathProblems({ changed, specs: specs.value })]);
}

export async function workspaceCommitProblems({ repo, tag, commit, states, inputs }) {
  const refused = await recordRefusals({ repo, tag, states, inputs });
  if (refused.length > 0) return refused;
  const base = await commitOf(repo, `refs/tags/${tag}`);
  if (!base.ok) return [`the tag ${tag}: ${base.reason}`];
  const found = await workspaceProblems({ repo, base: base.value, commit });
  return found.ok ? found.value : [`the workspace commit ${commit} cannot be judged: ${found.reason}`];
}
