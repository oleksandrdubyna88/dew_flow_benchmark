// `stage-sync --accept-harness <12 hex>` (F3.S1 (b), PR 5; epoch 11, candidate (b) restored): the one way a harness fix reaches the series' own branch. The fix is on `main` first, reviewed there; its dated line
// `YYYY-MM-DD | instrument | harness:<12 hex> | commit:<40 hex> <reason>` carries the harness IDENTITY of the fixed folders (never a commit sha) and begins its reason with the one commit of `main` that holds the fix.
// This module reads the line (PR 3's `parseStageAmendments`, no second parser), asks that the commit is reachable from `origin/main` after a fetch, takes the harness folders of exactly that commit onto the branch (the
// `STAGE_HARNESS_DIRS`, the top-level `*.mjs` of the three spikes and the `.agents/conventions` gitlink) and checks that their identity is the 12 hex. The taking is built in an index file of its own and committed with `commit-tree`, the
// branch is moved with the compare-and-swap form of `update-ref`, and only then the working tree is brought to it (`stage-take.mjs`): so there is nothing to put back, a kill at any step leaves the old branch whole or a branch that the next
// `stage-sync` finishes from its journal, and a lookup that fails refuses (it never removes a file). The series' locks and the refusal once a data session has left `planned` belong to the command (`stage-prereg-commands.mjs`).
// Every function answers a value; nothing here throws for an expected failure and nothing pushes.
import path from 'node:path';
import { git, harnessIdentity, textAt } from '../../minigit-bench/lib/prereg-guard.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { parseStageAmendments } from './stage-amendments.mjs';
import { FETCH_MS, MAIN, SUBMODULE, fetched, refusal, treeRefusal } from './stage-branch.mjs';
import { PREREG_DIR, STAGE_HARNESS_LISTS } from './stage-lists.mjs';
import { builtTree, changedFiles, changesOf, commitTree, entriesAt, gone, isLink, landedTake, linksOf, ownCheckoutProblem, readiedIndex, takeFiles } from './stage-take.mjs';

const AMENDMENTS_FILE = `${PREREG_DIR}/amendments.md`;
const BEGINS = /^commit:([0-9a-f]{40})(?=\s|$)/;
const TOKEN = /(?:^|\s)commit:/g;

// ---- the dated line ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------

/** The commit a reason names: it BEGINS with `commit:<40 hex>` and holds no second `commit:` token. `ok(commit)` | `fail(sentence)`. */
function commitOf({ reason, hex }) {
  const begins = BEGINS.exec(reason);
  if (begins === null) return fail(`the dated line for harness:${hex} does not begin its reason with \`commit:<40 hex>\`: stage-sync reads that token only, so it never guesses a commit from anything else in the line`);
  return (reason.match(TOKEN) ?? []).length === 1 ? ok(begins[1]) : fail(`the dated line for harness:${hex} holds more than one \`commit:\` token: it names one commit`);
}

/** The commit named by the one dated line of the registered amendments `text` whose identity is `hex`: `ok(commit)` | `fail(sentence)`. No line, two lines and a line with no sound token are each refused. */
export function acceptedCommit({ text, hex }) {
  const lines = parseStageAmendments(text).v1.instrument.filter(line => line.identity === hex);
  if (lines.length === 0) return fail(`no dated line \`YYYY-MM-DD | instrument | harness:${hex} | commit:<40 hex> <reason>\` in ${AMENDMENTS_FILE} on origin/main: add it on main by a pull request, then run this again`);
  return lines.length > 1 ? fail(`${lines.length} dated lines name harness:${hex}: one line names one commit`) : commitOf({ reason: lines[0].reason, hex });
}

/** The sentence for a commit that is not reachable from `origin/main` (an unmerged branch, an unknown commit), or null. */
async function reachableProblem({ repo, commit }) {
  const r = await git(repo, ['merge-base', '--is-ancestor', commit, MAIN]);
  return r.ok ? null : `commit ${commit.slice(0, 12)} is not reachable from origin/main: the fix must be merged on main first (a public hash identifies bytes, it does not supply the reviewed source), and the series takes it from there`;
}

/** The commit the dated line for `hex` names on `origin/main` (after the fetch), reachable from it: `ok(commit)` | `fail(sentence)`. */
async function namedCommit({ repo, hex }) {
  const text = await textAt(repo, MAIN, AMENDMENTS_FILE);
  if (text === null) return fail(`${AMENDMENTS_FILE} is not on origin/main: a harness acceptance is a dated line there`);
  const named = acceptedCommit({ text, hex });
  if (!named.ok) return named;
  const problem = await reachableProblem({ repo, commit: named.value });
  return problem === null ? named : fail(problem);
}

// ---- what taking the harness of a commit comes to ----------------------------------------------------------------------------------------------------------------------------------------------

const shapeProblem = ({ head, want }) => (isLink(head.get(SUBMODULE)) === isLink(want.get(SUBMODULE)) ? null : `${SUBMODULE} is a submodule in one of the two trees and a folder or nothing in the other: the harness of that commit cannot be taken onto this branch, nothing was taken`);

/** What taking the harness of `commit` onto `old` comes to, read only: `ok(changes)` | `fail`. */
async function plannedChanges({ repo, commit, old }) {
  const [head, want] = await Promise.all([entriesAt(repo, old), entriesAt(repo, commit)]);
  const unread = [head, want].find(r => !r.ok);
  if (unread) return unread;
  const shape = shapeProblem({ head: head.value, want: want.value });
  return shape === null ? ok(changesOf({ head: head.value, want: want.value })) : fail(shape);
}

/** The sentence for a changed gitlink whose commit the submodule's own checkout does not hold (it cannot be moved there without a fetch of its own), or null. */
async function subProblem({ repo, links }) {
  if (links.length === 0) return null;
  const [link, ...rest] = links;
  const own = await ownCheckoutProblem({ repo, file: link.file });
  if (own !== null) return own;
  const has = await git(path.join(repo, link.file), ['cat-file', '-e', `${link.sha}^{commit}`]);
  return has.ok ? subProblem({ repo, links: rest }) : `the submodule ${link.file} does not hold commit ${link.sha.slice(0, 12)}: fetch it there first, then run this again`;
}

/** The plan, checked: `ok(changes)` | `fail` (a shape that cannot be taken, a submodule that lacks the commit). */
async function checkedPlan({ repo, commit, old }) {
  const planned = await plannedChanges({ repo, commit, old });
  if (!planned.ok) return planned;
  const absent = await subProblem({ repo, links: linksOf(planned.value.put) });
  return absent === null ? planned : fail(absent);
}

// ---- building the commit, then landing it ------------------------------------------------------------------------------------------------------------------------------------------------------

/** The sentence for a taking whose harness identity is not the 12 hex the line names, or null. The identity is read over `tree`, which the temporary index holds: nothing is written to the repository's own index or tree. */
async function identityProblem({ repo, hex, commit, tree }) {
  const identity = await harnessIdentity(repo, STAGE_HARNESS_LISTS, tree);
  if (!identity.ok) return identity.reason;
  return identity.value.startsWith(hex) ? null : `the harness folders of commit ${commit.slice(0, 12)} have the identity ${identity.value.slice(0, 12)}, not the ${hex} the dated line names: nothing was taken`;
}

const subjectOf = ({ hex, commit }) => `chore(series): stage-sync --accept-harness ${hex} takes the harness folders of ${commit.slice(0, 12)} from origin/main`;

/** The commit of the taking on `old` (`ok(null)` when it changes no path), its tree built in the temporary index (made ready first: a stale file or a stale index lock is removed, see `readiedIndex`) and its identity checked: `ok(sha | null)` | `fail`. */
async function builtCommit({ repo, hex, commit, old, changes, files }) {
  const tree = await builtTree({ repo, old, index: files.index, changes });
  if (!tree.ok) return tree;
  const problem = await identityProblem({ repo, hex, commit, tree: tree.value });
  if (problem !== null) return fail(problem);
  const paths = changedFiles(changes);
  return paths.length === 0 ? ok(null) : commitTree({ repo, tree: tree.value, parent: old, subject: subjectOf({ hex, commit }), body: `commit:${commit}\nharness:${hex}\n${paths.join('\n')}` });
}

/** `builtCommit`, the seam named `indexBuilt` after it, and the temporary index removed. */
async function readyCommit(args) {
  const ready = readiedIndex(args.files);
  if (!ready.ok) return ready;
  const made = await builtCommit(args);
  if (made.ok) await args.seam('indexBuilt');
  const cleared = gone(args.files.index);
  return cleared.ok ? made : cleared;
}

/** The branch moved to `next` and the tree brought to it: `ok({ changed, commit, from })` | `fail`. */
async function landedCommit(args) {
  const dirty = await treeRefusal(args.repo);
  if (dirty !== null) return fail(dirty);
  const landed = await landedTake({ repo: args.repo, files: args.files, old: args.old, next: args.next, seam: args.seam });
  return landed.ok ? ok({ changed: changedFiles(args.changes), commit: args.next, from: args.commit }) : landed;
}

/** The commit built and landed (`commit: null` and nothing written when the branch holds the folders already): `ok({ changed, commit, from })` | `fail`. */
async function committedTake(args) {
  const made = await readyCommit(args);
  if (!made.ok) return made;
  return made.value === null ? ok({ changed: [], commit: null, from: args.commit }) : landedCommit({ ...args, next: made.value });
}

/** The taking of the harness of `commit`: plan, build in a temporary index, check, commit-tree, then move the branch and the tree. `ok({ changed, commit, from })` | `fail`. */
async function takenHarness({ repo, hex, commit, seam }) {
  const head = await git(repo, ['rev-parse', 'HEAD']);
  if (!head.ok) return head;
  const old = head.value.trim();
  const planned = await checkedPlan({ repo, commit, old });
  if (!planned.ok) return planned;
  const files = await takeFiles(repo);
  return files.ok ? committedTake({ repo, hex, commit, old, changes: planned.value, files: files.value, seam }) : files;
}

/**
 * `stage-sync --accept-harness <hex>` on the series' own branch (FREE): refuses a branch or tree `stage-sync` refuses (after finishing a take that a kill left), says `stage-sync: fetching origin ...`, fetches, finds the one dated line of
 * `origin/main`'s amendments whose identity is `hex` and the one `commit:` token that begins its reason, refuses a commit not reachable from `origin/main`, takes the harness folders of that commit and commits them. -> `ok({ changed:
 * [paths], commit: sha | null, from })` | `fail(reason)`; nothing is committed when the branch holds those folders already. The caller holds the series locks and has checked that no data session has left `planned`.
 * `seam(step)` is awaited at `indexBuilt`, `journalWritten`, `refMoved`, `worktree:<n>` and `worktreeDone`: a test kills the process there.
 */
export async function acceptHarness({ repo, hex, fetchMs = FETCH_MS, say = () => {}, seam = async () => {}, series }) {
  const refused = await refusal(repo, series);
  if (refused) return fail(refused);
  say('stage-sync: fetching origin ...');
  const got = await fetched(repo, fetchMs);
  if (!got.ok) return got;
  const named = await namedCommit({ repo, hex });
  return named.ok ? takenHarness({ repo, hex, commit: named.value, seam }) : named;
}
