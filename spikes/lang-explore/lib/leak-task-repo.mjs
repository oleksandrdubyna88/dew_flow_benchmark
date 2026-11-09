// The task repository's half of `leakcheck` (build step 2b; merge conditions 3 and 5): its tracked files at HEAD, the history of every local branch over the commits its base does not hold, the staged changes and the
// working tree (untracked and modified files), all through the same tiers as the bench's. Every question that cannot be answered is a refusal, never a clean result: a shallow repository, a history with objects missing, a
// repository that is not there. The base is resolved with `git rev-parse --verify`, never assumed: when it cannot be, the history is every commit reachable from every local branch, and the report says so.
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { hasHead, missingProblem, resolveCommit, shallowProblem, walkOf } from './leak-git.mjs';
import { DEFAULT_LIMITS, historyTexts, stagedText, treeTexts } from './leak-git-read.mjs';
import { groupOfTexts, groupOfWorkingTree } from './leak-texts.mjs';

/** The sentence saying how far back the history was read (a part of the report: the result is only as wide as this). */
const scopeOf = (ref, sha) => (sha === null ? `${ref} cannot be resolved: every commit reachable from every local branch was scanned` : `the commits of every local branch that ${ref} (${sha.slice(0, 12)}) does not hold`);

/** The sha the base ref resolves to, or null when it does not (the history is then every commit of every local branch). */
async function baseShaOf(repo, ref) {
  const base = await resolveCommit(repo, ref);
  return base.ok ? base.value : null;
}

/** `ok({ walk, scope })`: the base resolved (or its absence said), the history walk, and the refusals that come before any read (a shallow repository, an object missing). */
export async function historyScope({ repo, ref }) {
  const shallow = await shallowProblem(repo);
  if (shallow !== null) return fail(shallow);
  const sha = await baseShaOf(repo, ref);
  const walk = await walkOf(repo, sha);
  if (!walk.ok) return walk;
  const missing = await missingProblem(repo, walk.value);
  return missing === null ? ok({ walk: walk.value, scope: scopeOf(ref, sha) }) : fail(missing);
}

/** The prefix of this half's progress lines. */
const WHO = 'task repository';

/** What an unborn branch holds at HEAD: nothing, and the report says there is no commit. */
const UNBORN = ok({ texts: [], unreadable: [], modules: [], unborn: true });

/** The tree at HEAD as texts, or nothing for an unborn branch: `ok({ texts, unreadable, modules, unborn? })`. */
async function headTexts({ repo, limits, progress }) {
  return (await hasHead(repo)) ? treeTexts({ repo, commit: 'HEAD', label: 'HEAD', limits, who: WHO, what: 'the tree at HEAD', progress }) : UNBORN;
}

/** The files read at HEAD, or null for an unborn branch (no tree). */
const headCount = head => (head.unborn === true ? null : head.texts.length);

/** The texts of the committed side: HEAD's tree (when there is a commit), the commit messages and patches of the history, the staged changes. `ok({ texts, unreadable, headFiles })`; `headFiles` is null with no commit. */
async function committedTexts({ repo, walk, limits, progress }) {
  const head = await headTexts({ repo, limits, progress });
  const history = head.ok ? await historyTexts({ repo, walk, limits, who: WHO, progress }) : head;
  const staged = history.ok ? await stagedText(repo, limits) : history;
  if (!staged.ok) return staged;
  return ok({ texts: [...head.value.texts, ...history.value, staged.value], unreadable: head.value.unreadable, headFiles: headCount(head.value) });
}

/**
 * The scan of the task repository at `repo` (its real top folder): `{ refused }` with a sentence, or `{ groups, counts, scope }`: the groups of findings (committed side, working tree), what each read, and how far the history went.
 * `ref` is the base to resolve (`--task-base`, else `origin/main`); `limits` bound each read (see leak-git-read.mjs); `progress` gets one line per phase and per batch.
 */
export async function scanTaskRepository({ repo, ref, material, name, limits = DEFAULT_LIMITS, progress = () => {} }) {
  const scope = await historyScope({ repo, ref });
  if (!scope.ok) return { refused: scope.reason };
  progress(`${WHO}: history ${scope.value.scope}`);
  const committed = await committedTexts({ repo, walk: scope.value.walk, limits, progress });
  if (!committed.ok) return { refused: committed.reason };
  const work = await groupOfWorkingTree({ root: repo, material, repo: name, progress });
  if (!work.ok) return { refused: work.reason };
  const group = groupOfTexts({ texts: committed.value.texts, material, repo: name, unreadable: committed.value.unreadable });
  return { groups: [group, work.value.group], counts: { head: committed.value.headFiles, working: work.value.count }, scope: scope.value.scope };
}
