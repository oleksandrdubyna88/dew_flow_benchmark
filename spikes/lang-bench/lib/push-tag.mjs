// `push-check.mjs --tag <name>`: what the freeze procedure runs BEFORE a tag is pushed (a tag is pushed alone, so no branch check reads it).
//   - an annotated tag's MESSAGE is text a push publishes: it gets the task-repository body rules (leak, secrets, needles, family names);
//   - the commit the tag points at (annotated or lightweight) must already be held by a commit the remote has NOW (git ls-remote, never the local tracking refs, which
//     go stale) - checked when its branch was pushed - or the tag would
//     carry unchecked commits to the remote: `tag-target` refuses it, naming the commit, never its content.
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { checkText } from './push-check.mjs';
import { EXIT } from './push-exit.mjs';
import { runBatchCheck, runGit } from './push-git.mjs';
import { FETCH_CONFIG } from './push-remote.mjs';

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

export const FETCH_FIRST = 'the tagged commit is not on any remote ref this clone can see, and some current remote commits are not present locally (not fetched), so whether one of them holds it cannot be decided: fetch first (the check never fetches for you)';

/** ok([sha]) of the remote's CURRENT heads and tags (`git ls-remote`, peeled tag entries included): never the local tracking refs, which go stale when the remote moves or deletes a branch. */
async function remoteShas(repo) {
  const listing = await runGit(repo, ['ls-remote', '--heads', '--tags', 'origin'], { timeoutMs: 120_000, config: FETCH_CONFIG });
  if (!listing.ok) return fail(`git ls-remote origin failed: ${listing.reason}`, { kind: 'env' });
  return ok([...new Set(listing.value.stdout.split('\n').map(line => line.split('\t')[0]).filter(Boolean))]);
}

/** The shas among `shas` that this clone has (a commit or a tag object), and how many it lacks; ok({ present, absent }). */
async function presentAmong(repo, shas) {
  const sizes = await runBatchCheck(repo, shas);
  if (!sizes.ok) return sizes;
  const present = shas.filter(sha => ['commit', 'tag'].includes(sizes.value.get(sha)?.type));
  return ok({ present, absent: shas.length - present.length });
}

const finding = (commit, what) => [{ commit, path: 'tag', line: null, what }];

/**
 * The findings of `tag-target`: the tagged commit must be an ancestor of (or equal to) a commit the remote holds NOW. A remote commit this clone lacks cannot be
 * tested; when nothing present holds the tag's commit and some are absent, the answer is "fetch first", not a guess either way.
 */
async function targetFindings(repo, commit) {
  const remote = await remoteShas(repo);
  const have = remote.ok ? await presentAmong(repo, remote.value) : remote;
  if (!have.ok) return have;
  const unheld = await runGit(repo, ['rev-list', '-1', commit, '--stdin'], { input: have.value.present.map(sha => `^${sha}\n`).join('') });
  if (!unheld.ok) return unheld;
  if (unheld.value.stdout.trim() === '') return ok([]);
  return ok(finding(commit, have.value.absent > 0 ? FETCH_FIRST : UNCHECKED_TARGET));
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
