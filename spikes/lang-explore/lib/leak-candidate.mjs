// A candidate publication commit (build step 2b; F2.S2's freeze commits): the ONE commit that is about to be pushed, read before it is. Its tree (every file it holds), its message and its diff against each parent, against the
// empty tree when it has none (a root commit's whole content is an added line of its diff). The repository may be either of the two. The candidate is named by a commit that `git rev-parse --verify <commit>^{commit}` accepts; an
// unknown one is a refusal, decided by the caller.
import { ok } from '../../minigit-bench/lib/result.mjs';
import { failed, finished, git, gitText } from './leak-git.mjs';
import { DEFAULT_LIMITS, treeTexts } from './leak-git-read.mjs';
import { groupOfTexts } from './leak-texts.mjs';

const short = sha => sha.slice(0, 12);

/** The id of the empty tree in this repository's hash algorithm (the base of a root commit's diff). */
async function emptyTree(repo) {
  const r = await git(repo, ['hash-object', '-t', 'tree', '--stdin'], { input: '' });
  return finished(r) ? ok(r.value.stdout.trim()) : failed('the empty tree', r);
}

/** `ok([parent sha])` of a commit (none for a root commit). */
async function parentsOf(repo, sha) {
  const r = await gitText(repo, ['rev-list', '--parents', '-n', '1', sha, '--'], 'the parents of the candidate');
  return r.ok ? ok(r.value.trim().split(' ').slice(1)) : r;
}

/** `ok([{ id, name }])`: what the candidate is diffed against, one base per parent, or the empty tree for a root commit. */
async function basesOf(repo, sha) {
  const parents = await parentsOf(repo, sha);
  if (!parents.ok) return parents;
  if (parents.value.length > 0) return ok(parents.value.map(p => ({ id: p, name: `parent ${short(p)}` })));
  const empty = await emptyTree(repo);
  return empty.ok ? ok([{ id: empty.value, name: 'the empty tree' }]) : empty;
}

/** The diff texts of a candidate, one per base. */
async function diffTexts({ repo, sha, bases }) {
  const texts = [];
  for (const base of bases) {
    const r = await gitText(repo, ['diff', '--no-color', '--no-ext-diff', '--no-textconv', base.id, sha, '--'], `the diff of the candidate against ${base.name}`);
    if (!r.ok) return r;
    texts.push({ name: `diff of ${short(sha)} against ${base.name}`, raw: r.value, diff: true });
  }
  return ok(texts);
}

/** `ok([texts])`: the candidate's message, then its diffs. */
async function messageAndDiffs({ repo, sha }) {
  const message = await gitText(repo, ['log', '-1', '--format=%B', sha, '--'], 'the message of the candidate');
  if (!message.ok) return message;
  const bases = await basesOf(repo, sha);
  if (!bases.ok) return bases;
  const diffs = await diffTexts({ repo, sha, bases: bases.value });
  return diffs.ok ? ok([{ name: `commit message of candidate ${short(sha)}`, raw: message.value, diff: false }, ...diffs.value]) : diffs;
}

/** The texts of a candidate commit: `ok({ texts, unreadable, modules })` (its message, its diffs, then every file of its tree). The tree is read first: a missing object in it is a refusal whatever the parents hold. */
export async function candidateTexts({ repo, sha, limits = DEFAULT_LIMITS, progress = () => {} }) {
  const who = `candidate ${short(sha)}`;
  const tree = await treeTexts({ repo, commit: sha, label: who, limits, who, what: 'its tree', progress });
  if (!tree.ok) return tree;
  const head = await messageAndDiffs({ repo, sha });
  return head.ok ? ok({ texts: [...head.value, ...tree.value.texts], unreadable: tree.value.unreadable, modules: tree.value.modules }) : head;
}

/** The scan of one candidate commit: `{ group, count }` or `{ refused }`. `name` is the repository's name for the report. */
export async function scanCandidate({ repo, sha, material, name, limits, progress }) {
  const got = await candidateTexts({ repo, sha, limits, progress });
  if (!got.ok) return { refused: got.reason };
  return { group: groupOfTexts({ texts: got.value.texts, material, repo: name, unreadable: got.value.unreadable }), count: got.value.texts.length };
}
