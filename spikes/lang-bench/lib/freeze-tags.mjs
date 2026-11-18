// The freeze tag before and after its push (F2.S2 (d), `check-local-tag` and `check-tag`), and the rule that C is the squash-merge commit on the remote's `main`.
//   checkLocalTag  BEFORE the push: the tag exists only locally, is annotated, points at exactly C, its message is the intended annotation byte for byte, its name and date are the rows', and C is the freeze commit on main.
//   checkRemoteTag AFTER the push: the remote's peeled tag is C; the tag object, fetched from the remote into a folder of its own, is annotated, names the tag and C and has the message; its id is the local tag object's.
// A freeze tag that never reached the remote may be deleted and made again under the same name; once pushed it is superseded by a later-date tag, never moved (the tag ruleset).
import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { cleanupScope, ownedFor } from '../../../.agents/conventions/tools/lib/owned.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { checkFreezeCommit } from './freeze-commit.mjs';
import { blobsOf, commitOf, FORMAT_PATH, isAncestor, originUrl, parentOf, readTag, remoteMain, remoteTag, treeAt } from './freeze-git.mjs';
import { failWith, isEnv, problemsOf, unless } from './freeze-inputs.mjs';
import { tagDate } from './freeze-rows.mjs';
import { runGit } from './push-git.mjs';
import { FETCH_CONFIG } from './push-remote.mjs';

const owned = ownedFor({ token: 'bench' });
const FETCH_TIMEOUT_MS = 120_000;
const ENV = Object.freeze({ env: true });

/** The sentence when the tag's name is not `spec-frozen-B-<a real day>`, else an empty list. The checker accepts only that form, so a suffixed name (`-r2`) is never made. */
const nameProblems = tag => (tagDate(tag) === null ? [`the tag name ${tag} is not spec-frozen-B-<yyyymmdd> of a real day: the checker accepts only \`spec-frozen-[AB]-\\d{8}\`, so a suffix such as -r2 is never used`] : []);

/** ok([sentence]) once the remote's `main` is known to be `main`: C must be that commit or below it. */
async function belowMain({ repo, commit, main }) {
  const below = await isAncestor(repo, commit, main);
  if (!below.ok) return ok([`${commit} cannot be compared with the remote's main ${main} (${below.reason}): fetch first`]);
  return ok(unless(below.value, `${commit} is not on the remote's main (${main}): a tag on the freeze branch's head or on any commit outside main's history is refused; the tag goes on the squash-merge commit C`));
}

/** ok([sentence]): C must be an ancestor of the remote's `main`, asked of the remote now. */
export async function mainProblems({ repo, commit }) {
  const main = await remoteMain(repo);
  if (isEnv(main)) return main;
  return main.ok ? belowMain({ repo, commit, main: main.value }) : ok([`${commit} cannot be placed on the remote's main: ${main.reason}`]);
}

/** The sentences when the freeze commit's rows name another tag than `tag` (the rows' name and date are the tag's). */
const rowsProblems = ({ freeze, tag }) => unless(freeze.value.tag === tag, `the Freeze tag rows of the specs at C say ${freeze.value.tag} (date ${freeze.value.date}), not ${tag}: the tag's name and date must be the rows'`);

/** ok({ problems, freeze }) for a commit with one parent: `check-commit` between the parent and C, and the rows against the tag. */
async function withFreeze({ base, repo, commit, parent, tag, pins }) {
  const freeze = await checkFreezeCommit({ repo, before: parent, after: commit, pins });
  if (isEnv(freeze)) return freeze;
  const asCommit = freeze.ok ? rowsProblems({ freeze, tag }) : problemsOf(freeze).map(problem => `${commit} is not the freeze commit: ${problem}`);
  return ok({ problems: [...base, ...asCommit], freeze });
}

/** ok({ problems, freeze }): the sentences of the name, the squash commit, the rows and the main rule for tag `tag` on `commit`; `freeze` is the result of `check-commit` between C's parent and C. */
async function commitProblems({ repo, commit, tag, pins }) {
  const [parent, main] = [await parentOf(repo, commit), await mainProblems({ repo, commit })];
  const bad = [parent, main].find(isEnv);
  if (bad !== undefined) return bad;
  const base = [...nameProblems(tag), ...main.value];
  if (!parent.ok) return ok({ problems: [...base, `${commit} is not the freeze pull request's squash commit: ${parent.reason}`], freeze: parent });
  return withFreeze({ base, repo, commit, parent: parent.value, tag, pins });
}

/** The sentences for a local tag read from the repository: absent, lightweight, on another commit, with another message. */
function localProblems({ read, tag, commit, message }) {
  if (!read.ok) return [read.reason];
  const found = read.value;
  return [
    ...unless(found.type === 'tag', `the tag ${tag} is lightweight (not annotated): it must be made with git tag -a`),
    ...unless(found.commit === commit, `the tag ${tag} points at ${found.commit}, not at C ${commit}`),
    ...unless(found.type !== 'tag' || found.message === message, `the tag ${tag}'s message differs from the intended annotation byte for byte`),
  ];
}

/** ok({ remote, read, sha }): what `check-local-tag` asks first, or the environment's failure, or the sentence for a commit that is none. */
async function localReads({ repo, tag, commit }) {
  const [remote, read, sha] = [await remoteTag(repo, tag), await readTag(repo, tag), await commitOf(repo, commit)];
  const bad = [remote, read, sha].find(isEnv);
  if (bad !== undefined) return bad;
  return sha.ok ? ok({ remote: remote.value, read, sha: sha.value }) : failWith([sha.reason]);
}

/** `check-local-tag`: ok({ tag, commit }) | failWith(sentences) | an environment fail. */
export async function checkLocalTag({ repo, tag, commit, messageFile, pins }) {
  const reads = await localReads({ repo, tag, commit });
  if (!reads.ok) return reads;
  const { remote, read, sha } = reads.value;
  const onRemote = unless(remote === null, `the tag ${tag} is already on the remote (git ls-remote --tags origin): a pushed tag is never moved or deleted; make a tag with a later date instead`);
  const about = await commitProblems({ repo, commit: sha, tag, pins });
  if (isEnv(about)) return about;
  const problems = [...onRemote, ...localProblems({ read, tag, commit: sha, message: readFileSync(messageFile, 'utf8') }), ...about.value.problems];
  return problems.length > 0 ? failWith(problems) : ok({ tag, commit: sha });
}

/** ok(true) once the tag is fetched from `url` into the bare folder `dir`; the environment's fail otherwise. */
async function fetchInto({ dir, url, tag }) {
  const init = await runGit(dir, ['init', '-q', '--bare']);
  const fetched = init.ok ? await runGit(dir, ['fetch', '-q', '--no-tags', url, `+refs/tags/${tag}:refs/tags/${tag}`], { timeoutMs: FETCH_TIMEOUT_MS, config: FETCH_CONFIG }) : init;
  return fetched.ok ? ok(true) : fail(`fetching the tag ${tag} from the remote failed: ${fetched.reason}`, ENV);
}

/** ok({ type, object, commit, message, header }) read from the folder the tag was fetched into. */
async function readFetched({ dir, tag }) {
  const [read, raw] = [await readTag(dir, tag), await runGit(dir, ['cat-file', 'tag', `refs/tags/${tag}`], { accept: [0, 128] })];
  if (!read.ok) return fail(read.reason, ENV);
  return ok({ ...read.value, header: raw.ok ? raw.value.stdout.split('\n\n')[0] : '' });
}

/** ok({ type, object, commit, message, header }) for the remote's tag: fetched into a bare folder of its own (owned, removed after) and read there. */
async function fetchedTag({ repo, tag }) {
  const url = await originUrl(repo);
  if (!url.ok) return url;
  const scope = cleanupScope();
  try {
    const dir = owned.ownedDir(scope, 'tagobj');
    const fetched = await fetchInto({ dir, url: url.value, tag });
    return fetched.ok ? await readFetched({ dir, tag }) : fetched;
  } finally {
    await scope.close();
  }
}

/** The sentences when the remote's tag object does not name the tag or does not point at C. */
function headerProblems({ fetched, tag, commit }) {
  const header = fetched.header.split('\n');
  return [...unless(header.includes(`tag ${tag}`), `the remote's tag object does not name the tag ${tag}`), ...unless(header.includes(`object ${commit}`), `the remote's tag object does not point at C ${commit}`)];
}

/** The sentences about the tag object the remote holds, against the local tag (required: with none there is nothing to compare, and the record would name an object this checkout never made) and the intended message when it is given. */
function objectProblems({ fetched, tag, commit, local, message }) {
  const header = headerProblems({ fetched, tag, commit });
  if (!local.ok) return [...header, `there is no local tag ${tag}: check-tag compares the remote's tag object with the local one, so the local tag must exist (git fetch origin refs/tags/${tag}:refs/tags/${tag})`];
  const wanted = [local.value.message, ...(message === undefined ? [] : [message])];
  return [
    ...header,
    ...unless(local.value.object === fetched.object, `the remote's tag object ${fetched.object} differs from the local tag object ${local.value.object}`),
    ...unless(wanted.every(expected => expected === fetched.message), 'the remote tag\'s message differs from the intended annotation'),
  ];
}

/** The one sentence when the remote's tag ref is absent, lightweight or peels to another commit than C, else null. */
function peelProblem({ remote, tag, commit }) {
  if (remote === null) return `the remote has no tag ${tag}`;
  if (remote.peeled === null) return `the remote's tag ${tag} is lightweight: it points straight at ${remote.object} and is not an annotated tag object`;
  return remote.peeled === commit ? null : `the remote's tag ${tag} (peeled) points at ${remote.peeled}, not at C ${commit}`;
}

/** The sentences about the remote's tag ref and object: present, annotated, peeled to C, the object as expected. */
async function remoteProblems({ repo, tag, commit, message }) {
  const remote = await remoteTag(repo, tag);
  if (!remote.ok) return remote;
  const early = peelProblem({ remote: remote.value, tag, commit });
  if (early !== null) return ok([early]);
  const [fetched, local] = [await fetchedTag({ repo, tag }), await readTag(repo, tag)];
  if (!fetched.ok) return fetched;
  return ok([...unless(fetched.value.type === 'tag', `the remote's tag ${tag} is not an annotated tag object`), ...objectProblems({ fetched: fetched.value, tag, commit, local, message })]);
}

/** ok(FORMAT's blob at C), or a fail naming it. */
async function formatBytes({ repo, commit }) {
  const tree = await treeAt(repo, commit);
  return tree.ok ? blobsOf(repo, tree.value, [FORMAT_PATH]) : tree;
}

/** ok({ problems, sha256 }): FORMAT's blob at C against the step-2a pin DIRECTLY, never against a hash taken from the freeze commit (a FORMAT changed before the freeze commit would be self-consistent there). */
async function formatProblems({ repo, commit, pin }) {
  const blobs = await formatBytes({ repo, commit });
  if (!blobs.ok) return isEnv(blobs) ? blobs : ok({ problems: [`${FORMAT_PATH} at C: ${blobs.reason}`], sha256: null });
  const sha256 = createHash('sha256').update(blobs.value.get(FORMAT_PATH)).digest('hex');
  return ok({ sha256, problems: unless(sha256 === pin.sha256, `${FORMAT_PATH} at C has sha256 ${sha256}, but the step-2a pin (entry B of ${pin.file}) is ${pin.sha256}: FORMAT's bytes are compared with the pin, not with the freeze commit's own hash`) });
}

/** ok({ sha, remote, about, format }): the three answers `check-tag` judges, or the environment's failure, or the sentence for a commit that is none. */
async function remoteReads({ repo, tag, commit, message, pin, pins }) {
  const sha = await commitOf(repo, commit);
  if (!sha.ok) return isEnv(sha) ? sha : failWith([sha.reason]);
  const [remote, about, format] = [await remoteProblems({ repo, tag, commit: sha.value, message }), await commitProblems({ repo, commit: sha.value, tag, pins }), await formatProblems({ repo, commit: sha.value, pin })];
  const bad = [remote, about, format].find(isEnv);
  return bad === undefined ? ok({ sha: sha.value, remote: remote.value, about: about.value, format: format.value }) : bad;
}

/** The fail of `check-tag` when it has sentences, or when the freeze commit itself is not one; else null. */
function tagFailure({ remote, about, format }) {
  const problems = [...remote, ...about.problems, ...format.problems];
  if (problems.length > 0) return failWith(problems);
  return about.freeze.ok ? null : failWith([about.freeze.reason]);
}

/** `check-tag`: ok({ tag, commit, tagObject, freeze, formatSha256 }) | failWith(sentences) | an environment fail. `message` is the intended annotation when it is given in addition to the local tag; `pin` is the step-2a pin of FORMAT (`loadPin`). */
export async function checkRemoteTag({ repo, tag, commit, message, pin, pins }) {
  const reads = await remoteReads({ repo, tag, commit, message, pin, pins });
  if (!reads.ok) return reads;
  const failure = tagFailure(reads.value);
  if (failure !== null) return failure;
  const { sha, about, format } = reads.value;
  return ok({ tag, commit: sha, tagObject: (await remoteTag(repo, tag)).value.object, freeze: about.freeze.value, formatSha256: format.sha256 });
}
