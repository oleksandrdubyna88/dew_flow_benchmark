// `push-check.mjs --tag <name>`: what the freeze procedure runs BEFORE a tag is pushed (a tag is pushed alone, so no branch check reads it).
//   - an annotated tag's MESSAGE is text a push publishes: it gets the task-repository body rules (leak, secrets, needles, family names);
//   - the commit the tag points at (annotated or lightweight) must already be on a remote ref - checked when its branch was pushed - or the tag would
//     carry unchecked commits to the remote: `tag-target` refuses it, naming the commit, never its content.
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { checkText } from './push-check.mjs';
import { EXIT } from './push-exit.mjs';
import { runGit } from './push-git.mjs';

export const UNCHECKED_TARGET = 'the tagged commit is on no remote ref: it was never checked; push its branch through the check first';

/** ok({ type, message, commit }) for `refs/tags/<tag>`; a `config` fail when there is no such tag. */
async function readTag(repo, tag) {
  const ref = `refs/tags/${tag}`;
  const type = await runGit(repo, ['cat-file', '-t', ref], { accept: [0, 128] });
  if (!type.ok) return type;
  if (type.value.code !== 0) return fail(`there is no tag ${tag} in ${repo}`, { kind: 'config' });
  const target = await runGit(repo, ['rev-parse', '--verify', `${ref}^{commit}`]);
  if (!target.ok) return target;
  const kind = type.value.stdout.trim();
  const raw = kind === 'tag' ? await runGit(repo, ['cat-file', 'tag', ref]) : ok({ stdout: '' });
  // the header of a tag object (object, type, tag, tagger) ends at the first blank line; the message is what follows
  return raw.ok ? ok({ type: kind, message: raw.value.stdout.split('\n\n').slice(1).join('\n\n'), commit: target.value.stdout.trim() }) : raw;
}

/** The finding list of `tag-target`: the commit when no remote ref holds it. */
async function targetFindings(repo, commit) {
  const unpublished = await runGit(repo, ['rev-list', '-1', commit, '--not', '--remotes=origin']);
  if (!unpublished.ok) return unpublished;
  return ok(unpublished.value.stdout.trim() === '' ? [] : [{ commit, path: 'tag', line: null, what: UNCHECKED_TARGET }]);
}

/** The check of a tag: `{ exit, label, checks, tag... }`, in the shape formatReport prints. */
export async function checkTag({ repo, tag, inputs }) {
  const label = `${repo} tag ${tag}`;
  const read = await readTag(repo, tag);
  if (!read.ok) return { exit: read.kind === 'config' ? EXIT.CONFIG : EXIT.ENVIRONMENT, label, checks: [], error: read.reason };
  const target = await targetFindings(repo, read.value.commit);
  if (!target.ok) return { exit: EXIT.ENVIRONMENT, label, checks: [], error: target.reason };
  const text = checkText({ text: read.value.message, label: `tag ${tag}`, inputs });
  const checks = [...text.checks, { name: 'tag-target', findings: target.value }];
  return { ...text, label, checks, exit: checks.some(c => c.findings.length > 0) ? EXIT.REFUSED : EXIT.PASS };
}
