// The pre-push check (parent §4 step 3): the named checks over exactly the commits a push would add, a PR or review body, and the
// bench's own branch. Every finding is `{ commit?, path, line, what }` - where and which, never the matched text. Exit codes:
// 0 clean, 1 refused, 3 the environment (git, the network, an unreadable aid), 4 configuration (a branch that is not there).
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { checkCreditTerms, carriesKnownText } from './push-credits.mjs';
import { EXIT } from './push-exit.mjs';
import { branchTip, changesOf, newCommits, basePairs, newObjects, readBlobs, readFileAt, tipTree, verifyNotMoved } from './push-range.mjs';
import { TASK_REPO_URL, verifyFresh, verifyOrigin } from './push-remote.mjs';
import { sequence } from './push-sequence.mjs';
import { attributionFindings, COMMIT_MESSAGE, entryFindings, flatten, keptFindings, messageFindings, placementEntries, scanEntries, scanText, SUBMODULE } from './push-scan.mjs';
import { findSubmoduleProblems, findSymlinks } from './push-shape.mjs';
import { findStackFolderFiles } from './push-visible.mjs';

export { EXIT };
export { COMMIT_MESSAGE };
const NAMES = Object.freeze(['leakcheck', 'secrets', 'attribution', 'run-base-text', 'family-names', 'known-task-credits', 'stack-folders', 'tree-shape']);
const SELF_NAMES = Object.freeze(['leakcheck', 'secrets']);
const MAX_PRINTED = 100;
/** The default for `progress`: a callback taking one line of text, which a library caller leaves silent and the command writes to stderr. */
const NO_PROGRESS = () => {};

const tokenLine = inputs => `${inputs.aids.literals.length} (${inputs.aids.tokenInfo ?? 'given directly'})`;

const verdict = (checks, label, extra = {}) => ({
  exit: checks.some(c => c.findings.length > 0) ? EXIT.REFUSED : EXIT.PASS,
  label,
  checks,
  ...extra,
});

const errorResult = (label, failure) => ({ exit: failure.kind === 'config' ? EXIT.CONFIG : EXIT.ENVIRONMENT, label, checks: [], error: failure.reason });

const firstFailure = (...results) => results.find(r => !r.ok);

/** The first commit that brought each path, keeping only the files `keep` allows. */
const firstCommitMap = (files, keep) => new Map(files.filter(keep).reverse().map(f => [f.path, f.commit]));

/** Map(blob id -> { path, commit }) of the first place each blob came from, so a refusal can say where an oversize blob is. */
const whereOf = files => new Map([...files].reverse().map(f => [f.oid, { path: f.path, commit: f.commit }]));

/** ok({ commits: [sha], tip }) of a branch: the commits a push would add and the tip it ends at. */
async function rangeAndTip(repo, branch, rev, dest) {
  const tip = await branchTip(repo, branch, rev);
  const range = tip.ok ? await newCommits(repo, branch, tip.value, dest) : tip;
  return range.ok ? ok({ commits: range.value, tip: tip.value }) : range;
}

/** ok({ commits, tip, found, files, tipTree }): the range, the tip it ends at, and the tagged findings of every message and blob in it. */
async function scanBranch({ repo, branch, rev, dest, inputs, refuseNul, progress, placementScan = true }) {
  const head = await rangeAndTip(repo, branch, rev, dest);
  if (head.ok) progress(`scanning ${head.value.commits.length} commits`);
  const changes = head.ok ? await changesOf(repo, head.value.commits) : head;
  if (!changes.ok) return changes;
  const { files, messages } = changes.value;
  const fresh = await newObjects(repo, head.value.tip, dest ?? branch);
  if (!fresh.ok) return fresh;
  const readable = files.filter(f => f.mode !== SUBMODULE && fresh.value.has(f.oid));
  const tree = await tipTree(repo, branch, head.value.tip);
  const pairs = placementScan ? await basePairs(repo, dest ?? branch) : ok(new Set());
  const treeBad = firstFailure(tree, pairs);
  if (treeBad) return treeBad;
  const entries = scanEntries(files, tree.value).map(e => ({ ...e, fresh: fresh.value.has(e.oid) }));
  const covered = new Set(entries.filter(e => e.fresh).map(e => `${e.path}\0${e.oid}`));
  const placement = placementScan ? placementEntries(tree.value, pairs.value, files, covered) : [];
  const wanted = [...readable, ...placement];
  progress(`reading ${new Set(wanted.map(f => f.oid)).size} blobs`);
  const blobs = await readBlobs(repo, wanted.map(f => f.oid), { ...inputs.limits, where: whereOf(wanted) });
  if (!blobs.ok) return blobs;
  progress(`scanning ${entries.length + placement.length} files and ${messages.length} messages`);
  const bytesOf = e => (e.fresh || e.placementOnly ? blobs.value.get(e.oid) : undefined);
  const found = keptFindings([...messageFindings(messages, inputs), ...[...entries, ...placement].flatMap(e => entryFindings(e, bytesOf(e), inputs, { refuseNul }))]);
  return ok({ commits: head.value.commits.length, tip: head.value.tip, found, files, tree: tree.value });
}

/** Branch-level findings by check name: the README's structure, the credit terms, the stack folders and the tree's shape. */
async function tipFindings({ repo, branch, inputs, scan }) {
  const readme = await readFileAt(repo, branch, 'README.md', scan.tip);
  const modules = await readFileAt(repo, branch, '.gitmodules', scan.tip);
  const failure = firstFailure(readme, modules);
  if (failure) return failure;
  const anyCommit = firstCommitMap(scan.files, () => true);
  const atTip = firstCommitMap(scan.files, f => scan.tree.get(f.path)?.oid === f.oid);
  // the whole tip tree is what a run base carries: a known-task text, a symlink or a stack file pushed earlier counts as much as this range's
  const known = [...new Set([...anyCommit.keys(), ...scan.tree.keys()])];
  const withCommit = (commits, f) => ({ commit: commits.get(f.path), ...f });
  return ok({
    attribution: attributionFindings(readme.value, known, carriesKnownText),
    'known-task-credits': checkCreditTerms({ credits: inputs.credits, paths: known, readmeText: readme.value }).map(f => withCommit(anyCommit, f)),
    'stack-folders': findStackFolderFiles(scan.tree, { templateFiles: inputs.templateFiles }).map(f => withCommit(atTip, f)),
    'tree-shape': [...findSubmoduleProblems({ tip: scan.tree, gitmodulesText: modules.value }), ...findSymlinks(scan.tree)].map(f => withCommit(atTip, f)),
  });
}

/** The tagged findings grouped under the named checks, in the named order. */
const groupChecks = (names, findings) => names.map(name => ({ name, findings: findings.filter(f => f.check === name).map(({ check, atTip, always, ...f }) => f) }));

/** ok(null) when origin is the task repository, the tracking refs are what it has now and the branch has not moved off `tip` (when given). */
const FULL_SHA = /^(?:[0-9a-f]{40}|[0-9a-f]{64})$/;

const tipShape = tip => (tip === undefined || FULL_SHA.test(tip) ? ok(null) : fail(`--tip must be a full commit sha (40 or 64 hex digits), not ${JSON.stringify(tip.slice(0, 80))}`, { kind: 'config' }));

/** ok(null) when `--tip` is a full sha, origin is the task repository, the tracking refs are what it has now and the branch has not moved off `tip`. */
const verifyRemote = ({ repo, branch, tip, expectedOrigin }) => sequence([
  () => tipShape(tip),
  () => verifyOrigin(repo, expectedOrigin),
  () => verifyFresh(repo),
  () => (tip === undefined ? ok(null) : verifyNotMoved(repo, branch, tip)),
]);

/**
 * The check of a branch's new commits (§4 step 3). `inputs`: aids, known, credits (parseCredits' answer), templateFiles. `tip` is the
 * sha git is about to publish (the pre-push hook passes it): that exact commit is checked, and the branch moving off it is exit 4.
 */
export async function checkBranch({ repo, branch, inputs, tip, dest, expectedOrigin = TASK_REPO_URL, progress = NO_PROGRESS }) {
  const label = `${repo} ${branch}`;
  progress('verifying origin and the remote-tracking refs');
  const origin = await verifyRemote({ repo, branch, tip, expectedOrigin });
  const scan = origin.ok ? await scanBranch({ repo, branch, rev: tip, dest, inputs: { ...inputs, aidMode: 'task' }, refuseNul: true, progress }) : origin;
  if (scan.ok) progress('checking the tip tree: README, credit terms, stack folders, tree shape');
  const tree = scan.ok ? await tipFindings({ repo, branch, inputs, scan: scan.value }) : scan;
  if (!tree.ok) return errorResult(label, tree);
  const all = [...scan.value.found, ...flatten(tree.value, undefined)];
  return verdict(groupChecks(NAMES, all), label, { commits: scan.value.commits, tip: scan.value.tip, branch, tokens: tokenLine(inputs) });
}

/** The bench's own branch (consult C6b, D2): only the leak and the secret scans, over the new commits. */
export async function checkSelf({ repo, branch, inputs, progress = NO_PROGRESS }) {
  const label = `${repo} ${branch} (bench branch)`;
  const scan = await scanBranch({ repo, branch, inputs, refuseNul: false, progress, placementScan: false });
  if (!scan.ok) return errorResult(label, scan);
  return verdict(groupChecks(SELF_NAMES, scan.value.found), label, { commits: scan.value.commits, tip: scan.value.tip, branch, tokens: tokenLine(inputs) });
}

/**
 * A PR or review body (consult C6b): the leak, secret and placement scans on the text; links are allowed in a body. With `self`
 * (a body of the bench's own PR) only the leak and secret scans run, like the bench branch's.
 */
export function checkText({ text, label, inputs, self = false }) {
  const names = self ? SELF_NAMES : ['leakcheck', 'secrets', 'run-base-text', 'family-names'];
  const found = flatten(scanText(label, text, { ...inputs, aidMode: self ? 'self' : 'task' }, { body: true }), undefined);
  return verdict(groupChecks(names, found), self ? `${label} (bench body)` : label, { tokens: tokenLine(inputs) });
}

const where = f => `${f.path}${f.line === null ? ':' : `:${f.line}`}`;
const span = f => (f.tokens === undefined ? '' : ` [window spans ${f.tokens} tokens]`);
const findingLine = f => `  ${f.commit ? `${f.commit.slice(0, 10)} ` : ''}${where(f)} ${f.what}${span(f)}`;
const plural = n => `${n} new commit${n === 1 ? '' : 's'}`;
const row = (label, value) => `${label.padEnd(20)} ${value}`;
const RESULT_WORDS = Object.freeze({ [EXIT.PASS]: 'PASS', [EXIT.REFUSED]: 'REFUSED', [EXIT.ENVIRONMENT]: 'ENVIRONMENT', [EXIT.CONFIG]: 'CONFIGURATION' });

const headerLines = result => [
  `push-check: ${result.label}${result.commits === undefined ? '' : ` (${plural(result.commits)})`}`,
  ...(result.error ? [`error: ${result.error}`] : []),
  ...(result.tokens ? [row('leak tokens', result.tokens)] : []),
  ...(result.tip ? [row('tip', result.tip), row('push', `git push <url> ${result.tip}:refs/heads/${result.branch}`)] : []),
];

const checkLines = ({ name, findings }) => [
  row(name, findings.length === 0 ? 'PASS' : `FAIL (${findings.length})`),
  ...findings.slice(0, MAX_PRINTED).map(findingLine),
  ...(findings.length > MAX_PRINTED ? [`  ... and ${findings.length - MAX_PRINTED} more`] : []),
];

/** The report: a header, one named line per check with its findings underneath (capped), and the result line. */
export function formatReport(result) {
  return [...headerLines(result), ...result.checks.flatMap(checkLines), row('result', `${RESULT_WORDS[result.exit]} (exit ${result.exit})`), ''].join('\n');
}
