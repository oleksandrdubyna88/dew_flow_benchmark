// The series' own branch (F3.S1 (b), PR 5; stories document section 3): the series runs from one worktree on a local branch cut at the tag's commit, which moves only by `stageSync`, and a paid launch refuses a branch that differs from
// the tag in any path off `AMENDABLE_PATHS` or that holds a registered file that is not `origin/main`'s. So a harness change that reaches `main` after the tag neither reaches the series nor stops it, and every dated line the series acts on is
// public first. Nothing here pushes, resets or deletes a branch: `stageSync` fetches, takes the listed files and commits on the series' branch. It never takes a harness folder and never writes an acceptance: a harness acceptance is the
// dated `instrument | harness:<12 hex> | commit:<40 hex> ...` line of the registered amendments, which `stageSync` takes from `origin/main` like any listed file; the harness folders of the commit it names come by
// `stage-sync --accept-harness <12 hex>` (`stage-accept.mjs`, epoch 11: candidate (b) restored; it reuses this module's fetch, refusal and restore).
import { mkdirSync, rmSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { git, blobAt, harnessIdentity, textAt } from '../../minigit-bench/lib/prereg-guard.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { AMENDABLE_PATHS, STAGE_HARNESS_DIRS, STAGE_HARNESS_LISTS, STAGE_HARNESS_TOP_DIRS } from './stage-lists.mjs';
import { notCheckedOut, pendingProblem, recoverPending } from './stage-take.mjs';

/** The remote ref of `main` by its FULL name: the short name `origin/main` is ambiguous (a local branch or tag of that name wins over the remote one), and every read of what the origin holds names the full ref. */
export const MAIN = 'refs/remotes/origin/main';
const EXPLORE_FILE = 'spikes/lang-explore/inputs/explore.json';
/** The shared rules' submodule: the scoring code the harness imports. */
export const SUBMODULE = '.agents/conventions';
/** How long the fetch of the origin may take before it is stopped and refused by name: a check never goes on against a stale local `origin/main`. */
export const FETCH_MS = 120_000;

const isHarnessPath = file => STAGE_HARNESS_DIRS.some(dir => file === dir || file.startsWith(`${dir}/`)) || STAGE_HARNESS_TOP_DIRS.some(dir => file.startsWith(dir) && /^[^/]+\.mjs$/.test(file.slice(dir.length)));

/** `ok(null)` after a fetch of the origin, or the `fail` (a non-zero exit and a timeout are both a refusal that names the fetch). */
export async function fetched(repo, fetchMs) {
  const r = await git(repo, ['fetch', '-q', 'origin'], { timeoutMs: fetchMs });
  return r.ok ? ok(null) : fail(`cannot fetch origin: ${r.reason}`);
}

// ---- the shared rules' submodule ---------------------------------------------------------------------------------------------------------------------------------------------------------------

/** The commit the superproject records for the submodule at HEAD, or null when the path is not a submodule there (a vendored copy is a tree, and its tree id is in the harness identity). */
async function recordedCommit(repo) {
  const listed = await git(repo, ['ls-tree', 'HEAD', SUBMODULE]);
  return listed.ok && listed.value.startsWith('160000 commit ') ? listed.value.split(/\s+/)[2] : null;
}

const atProblem = ({ at, recorded }) => (at.value.trim() === recorded ? null : `the submodule ${SUBMODULE} is at ${at.value.trim().slice(0, 12)}, the superproject records ${recorded.slice(0, 12)}: a paid launch runs the scoring code of the recorded commit`);
const dirtyProblem = status => (status.value.trim() === '' ? null : `the submodule ${SUBMODULE} has local changes: ${status.value.trim().split('\n').slice(0, 3).join('; ')}: a paid launch runs the scoring code of the commit the superproject records, as it is`);

/** The sentence for a submodule checkout that is not what the superproject records, or null: not checked out, at another commit, or with local changes. */
async function checkoutProblem({ repo, recorded }) {
  const dir = path.join(repo, SUBMODULE);
  const [top, at, status] = await Promise.all([git(dir, ['rev-parse', '--show-toplevel']), git(dir, ['rev-parse', 'HEAD']), git(dir, ['status', '--porcelain', '--untracked-files=all'])]);
  return notCheckedOut({ dir, top, name: SUBMODULE }) ?? atProblem({ at, recorded }) ?? dirtyProblem(status);
}

/** `[sentence]` for the shared rules' submodule: at the commit the superproject records and with no local change (its commit is in the harness identity, and the scoring code imports it). */
async function submoduleProblems(repo) {
  const recorded = await recordedCommit(repo);
  return recorded === null ? [] : [await checkoutProblem({ repo, recorded })].filter(Boolean);
}

// ---- the paid launch's two checks --------------------------------------------------------------------------------------------------------------------------------------------------------------

/** The paths that differ between the tag's commit and HEAD, or the `fail`. */
async function changedSinceTag(repo, commit) {
  const r = await git(repo, ['diff', '--name-only', commit, 'HEAD']);
  return r.ok ? ok(r.value.split('\n').filter(Boolean)) : r;
}

/** One sentence per changed path that is neither amendable nor, under an accepted harness, a harness path. */
function offListProblems(paths, harnessAccepted) {
  return paths.filter(p => !AMENDABLE_PATHS.includes(p) && !(harnessAccepted && isHarnessPath(p))).map(p => `the series' branch differs from the tag in ${p}, which is not an amendable path`);
}

// ---- explore.json: only its two caps ---------------------------------------------------------------------------------------------------------------------------------------------------------------

/** The two caps an `explore.json` text holds, `{ wallMs, stallMs }`, or null when it is not JSON or holds no such caps. */
function capsInText(text) {
  try {
    const caps = JSON.parse(text).caps;
    return Number.isFinite(caps?.wallMs) && Number.isFinite(caps?.stallMs) ? { wallMs: caps.wallMs, stallMs: caps.stallMs } : null;
  } catch {
    return null;
  }
}

const sameCaps = (a, b) => JSON.stringify(a) === JSON.stringify(b);
const withoutCaps = text => JSON.stringify(JSON.parse(text), (key, value) => (key === 'wallMs' || key === 'stallMs' ? undefined : value));

/** `text` with the values of `caps.wallMs` and `caps.stallMs` replaced and every other byte as it was (the file keeps the layout the tag gave it): `ok(text)` | `fail` when the block is not there or the result differs in anything else. */
export function withWallAndStall(text, caps) {
  const block = /"caps"\s*:\s*\{[^{}]*\}/.exec(text);
  if (block === null) return fail('explore.json holds no flat "caps" block to write the wall and the stall into');
  const replaced = block[0].replace(/("wallMs"\s*:\s*)\d+/, `$1${caps.wallMs}`).replace(/("stallMs"\s*:\s*)\d+/, `$1${caps.stallMs}`);
  const next = `${text.slice(0, block.index)}${replaced}${text.slice(block.index + block[0].length)}`;
  return sameCaps(capsInText(next), caps) && withoutCaps(next) === withoutCaps(text) ? ok(next) : fail('the rewritten explore.json differs from the series\' own in more than caps.wallMs and caps.stallMs');
}

/** `explore.json` at HEAD and at `origin/main`, as `{ head, main, now, want }`: the texts and the caps each holds (null when unreadable). */
async function exploreAtBoth(repo) {
  const [head, main] = await Promise.all([textAt(repo, 'HEAD', EXPLORE_FILE), textAt(repo, MAIN, EXPLORE_FILE)]);
  return { head, main, now: capsInText(head ?? ''), want: capsInText(main ?? '') };
}

/** The sentence for `explore.json` whose two caps at HEAD are not `origin/main`'s, or null. Every other field of the file is the tag's and is never compared: main may re-pin the cli or bump an SDK without touching the series. */
async function capsBlobProblem(repo) {
  const { now, want } = await exploreAtBoth(repo);
  return sameCaps(now, want) ? null : `${EXPLORE_FILE}: caps.wallMs and caps.stallMs at HEAD are not origin/main's: a dated line the series acts on is public first, so take them from main with stage-sync`;
}

/** The sentence for an amendable file whose blob at HEAD is not `origin/main`'s (both absent is equal), or null; for `explore.json` only the two caps are compared. */
async function mainBlobProblem(repo, file) {
  if (file === EXPLORE_FILE) return capsBlobProblem(repo);
  const [head, main] = await Promise.all([blobAt(repo, 'HEAD', file), blobAt(repo, MAIN, file)]);
  return head === main ? null : `${file} at HEAD is not origin/main's blob: a dated line the series acts on is public first, so take it from main with stage-sync`;
}

/**
 * The paid launch's checks of the series' branch (`config.branch` of `STAGE_PREREG_CONFIG`): after a fetch, the branch differs from the tag's commit in amendable paths only (and, when the run-map accepted the harness the branch holds, in harness
 * paths), and each amendable file's blob at HEAD is `origin/main`'s. -> `[sentence]`.
 */
export async function branchProblems(repo, runMap, { fetchMs = FETCH_MS } = {}) {
  const got = await fetched(repo, fetchMs);
  if (!got.ok) return [got.reason];
  const changed = await changedSinceTag(repo, runMap.preregCommit);
  if (!changed.ok) return [changed.reason];
  const identity = await harnessIdentity(repo, STAGE_HARNESS_LISTS);
  const accepted = identity.ok && (runMap.harnessAccepted ?? []).includes(identity.value);
  const blobs = await Promise.all(AMENDABLE_PATHS.map(file => mainBlobProblem(repo, file)));
  return [...offListProblems(changed.value, accepted), ...blobs.filter(Boolean), ...await submoduleProblems(repo), ...[await pendingProblem(repo)].filter(Boolean)];
}

// ---- stage-sync --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

/** The sentence for a branch that no Stage 1 series records as its own, or null. `series` is `[{ branch, preregCommit }]` of the paid series of the explore root: the series' branch is the one a series recorded at `stage-init`, and the tag's commit of that series is in its history (the merge-base of the two is the tag's commit). */
async function seriesBranchProblem({ repo, name, series }) {
  if (series.length === 0) return 'no Stage 1 series is made under the explore root: there is nothing to sync for (a rehearsal series has no branch of its own)';
  const own = series.filter(s => s.branch === name);
  if (own.length === 0) return `the branch ${name} is not the series' own branch: a Stage 1 series records ${[...new Set(series.map(s => s.branch ?? '(no branch)'))].join(', ')}: stage-sync moves only that branch, so change to it`;
  const inHistory = await Promise.all(own.map(s => git(repo, ['merge-base', '--is-ancestor', s.preregCommit ?? 'x', 'HEAD'])));
  return inHistory.some(r => r.ok) ? null : `the tag's commit ${String(own[0].preregCommit).slice(0, 12)} of the series is not in the history of the branch ${name}: the series' branch is cut at the tag's commit`;
}

/** The sentence for a branch `stageSync` must not work on, or null: never `main` or a detached head and, when the Stage 1 series are known (`series`), only the series' own branch. */
async function branchRefusal(repo, series) {
  const branch = await git(repo, ['rev-parse', '--abbrev-ref', 'HEAD']);
  if (!branch.ok) return branch.reason;
  const name = branch.value.trim();
  if (['main', 'HEAD'].includes(name)) return 'stage-sync runs on the series\' branch, not on main (nor a detached head): the series\' branch is a local branch cut at the tag\'s commit';
  return series === undefined ? null : seriesBranchProblem({ repo, name, series });
}

/** The paths of a `git status --porcelain` text that are staged or changed (or untracked under `spikes/`): an untracked file outside `spikes/` is nobody's change to the tree and is never committed (the commit names its files). */
const dirtyPaths = text => text.split('\n').filter(line => line !== '' && !(line.startsWith('?? ') && !line.slice(3).startsWith('spikes/'))).map(line => line.slice(3));

/** The sentence for a tree with a staged or changed file anywhere in the repository, or null: `stageSync` commits only what it takes, and a file of a person's must not be swept into that commit or overwritten by the taking. */
export async function treeRefusal(repo) {
  const status = await git(repo, ['status', '--porcelain', '--untracked-files=all']);
  if (!status.ok) return status.reason;
  const dirty = dirtyPaths(status.value);
  return dirty.length === 0 ? null : `the tree is not clean: ${dirty.join(', ')} ${dirty.length === 1 ? 'is' : 'are'} staged or changed: stage-sync commits only what it takes from origin/main, so a file of a person's is never swept into its commit`;
}

/** The sentence for an interrupted `--accept-harness` take (a kill between the move of the branch and the update of its tree) that cannot be finished, or null: it is finished here, before the tree is looked at (`stage-take.mjs`). */
async function pendingRefusal(repo) {
  const recovered = await recoverPending(repo);
  return recovered.ok ? null : recovered.reason;
}

/** The sentence for a branch or tree `stageSync` must not work on, or null. A take that a kill left half done is finished first: its tree is behind HEAD, which the clean-tree check below would refuse. */
export async function refusal(repo, series) {
  return await branchRefusal(repo, series) ?? await pendingRefusal(repo) ?? await treeRefusal(repo);
}

/**
 * What taking `file` from `origin/main` comes to, read and computed in memory and written nowhere: `ok(null)` when it is equal, else `ok({ file, inHead, text })`: the bytes the branch's file is to hold (`null`: `main` has none, so the
 * file is removed) and whether HEAD holds the file now (a restore needs it).
 */
async function planBlob(repo, file) {
  const [head, main] = await Promise.all([blobAt(repo, 'HEAD', file), blobAt(repo, MAIN, file)]);
  if (head === main) return ok(null);
  const text = main === null ? ok(null) : await mainText(repo, file);
  return text.ok ? ok({ file, inHead: head !== null, text: text.value }) : text;
}

/** The text of `file` at `origin/main`: `ok(text)`, or the `fail` for a file that is there and cannot be read (it must never be taken for a removal). */
async function mainText(repo, file) {
  const text = await textAt(repo, MAIN, file);
  return text === null ? fail(`${file} cannot be read at origin/main: nothing was taken`) : ok(text);
}

/** The `fail` for an `explore.json` that holds no caps at HEAD or at `origin/main` (`both` is `exploreAtBoth`'s answer), or null. */
const noCaps = both => (both.now === null || both.want === null ? fail(`${EXPLORE_FILE} cannot be read with caps.wallMs and caps.stallMs at HEAD and at origin/main: nothing was taken`) : null);

/** The same for `explore.json`: the tag's own bytes with the two caps of `origin/main`'s, computed in memory; `fail` when a file holds no caps. */
async function planCaps(repo) {
  const both = await exploreAtBoth(repo);
  const missing = noCaps(both);
  if (missing) return missing;
  if (sameCaps(both.now, both.want)) return ok(null);
  const next = withWallAndStall(both.head, both.want);
  return next.ok ? ok({ file: EXPLORE_FILE, inHead: true, text: next.value }) : next;
}

/** What taking one amendable file comes to: `explore.json` as its two caps only, every other file whole. */
const planFile = (repo, file) => (file === EXPLORE_FILE ? planCaps(repo) : planBlob(repo, file));

/** The plans of `files`, in order, added to `plans`, nothing written: `ok([plan of each file that differs])` | `fail`. */
async function planFrom(repo, files, plans = []) {
  if (files.length === 0) return ok(plans);
  const [file, ...rest] = files;
  const r = await planFile(repo, file);
  if (!r.ok) return r;
  return planFrom(repo, rest, r.value === null ? plans : [...plans, r.value]);
}

/** Writes `text` whole into `file` of the repository (a write that throws is a `fail`). */
function writeText(repo, file, text) {
  try {
    mkdirSync(path.dirname(path.join(repo, file)), { recursive: true });
    writeFileSync(path.join(repo, file), text);
    return ok(true);
  } catch (e) {
    return fail(`cannot write ${file}: ${e.message}`);
  }
}

/** Writes one plan into the tree and the index: a removal is `git rm`, anything else a write and `git add`. `ok(true)` | `fail`. */
async function applyPlan(repo, { file, text }) {
  if (text === null) return git(repo, ['rm', '-q', '--', file]);
  const written = writeText(repo, file, text);
  return written.ok ? git(repo, ['add', '--', file]) : written;
}

/** The plans written in order, the first failure stops: `ok(true)` | `fail`. */
async function applyAll(repo, plans) {
  if (plans.length === 0) return ok(true);
  const [plan, ...rest] = plans;
  const r = await applyPlan(repo, plan);
  return r.ok ? applyAll(repo, rest) : r;
}

const subjectOf = count => `chore(series): stage-sync takes ${count} registered file${count === 1 ? '' : 's'} from origin/main`;

/** The commit that records what was taken: -> `ok(sha)` | `fail`. */
async function commitTaken({ repo, changed }) {
  const made = await git(repo, ['commit', '-q', '-m', subjectOf(changed.length), '-m', changed.join('\n'), '--', ...changed]);
  if (!made.ok) return made;
  const head = await git(repo, ['rev-parse', 'HEAD']);
  return head.ok ? ok(head.value.trim()) : head;
}

/** Commits what was taken: `ok({ changed, commit })`, with `commit: null` and nothing committed when nothing differed. */
async function committed({ repo, changed }) {
  if (changed.length === 0) return ok({ changed: [], commit: null });
  const commit = await commitTaken({ repo, changed });
  return commit.ok ? ok({ changed, commit: commit.value }) : commit;
}

/** Writes the plans and commits them: `ok({ changed, commit })` | `fail` (the tree is then in between: `restored` puts it back). */
async function takenAll({ repo, plans }) {
  const applied = await applyAll(repo, plans);
  return applied.ok ? committed({ repo, changed: plans.map(p => p.file) }) : applied;
}

/**
 * Puts exactly the paths of `plans` back as HEAD has them, in the tree and in the index (`git checkout HEAD -- <paths>` and `git reset -q -- <paths>`; a path HEAD does not hold is unstaged and its file removed): so a failed taking
 * leaves a clean tree and the next `stage-sync` is not refused for the files this one left staged. -> `fail(the original reason, and what was restored)`.
 */
export async function restored({ repo, plans, failure }) {
  const [inHead, notInHead] = [plans.filter(p => p.inHead).map(p => p.file), plans.filter(p => !p.inHead).map(p => p.file)];
  const back = inHead.length === 0 ? ok('') : await git(repo, ['checkout', 'HEAD', '--', ...inHead]);
  const unstaged = await git(repo, ['reset', '-q', '--', ...inHead, ...notInHead]);
  for (const file of notInHead) rmSync(path.join(repo, file), { force: true });
  const problem = [back, unstaged].find(r => !r.ok);
  return fail(problem === undefined ? `${failure.reason}: every file it had taken is restored as HEAD has it, and nothing is staged` : `${failure.reason}: restoring the taken files failed too (${problem.reason}): run git checkout HEAD -- and git reset -q -- on ${plans.map(p => p.file).join(' ')}`);
}

/** Plans the whole change, writes it and commits it; when that fails, restores the tree: `ok({ changed, commit })` | `fail`. */
async function takenFrom(repo) {
  const planned = await planFrom(repo, AMENDABLE_PATHS);
  if (!planned.ok) return planned;
  const taken = await takenAll({ repo, plans: planned.value });
  return taken.ok ? taken : restored({ repo, plans: planned.value, failure: taken });
}

/**
 * The one way the series' branch moves (FREE): fetch, prepare the whole change in memory (every blob read from `origin/main`, the new `explore.json` bytes computed), and only then write the listed amendable files (a file `main` does not
 * have is removed) and commit them on the series' branch. A write or a commit that fails puts every taken file and index entry back as HEAD has them and refuses. It takes nothing else and never pushes.
 * `say` prints the one line before the fetch, which may wait up to `fetchMs`. -> `ok({ changed: [amendable paths], commit: sha | null })` | `fail(reason)`; nothing is committed when nothing differs.
 */
export async function stageSync({ repo, fetchMs = FETCH_MS, say = () => {}, series }) {
  const refused = await refusal(repo, series);
  if (refused) return fail(refused);
  say('stage-sync: fetching origin ...');
  const got = await fetched(repo, fetchMs);
  return got.ok ? takenFrom(repo) : got;
}
