// The pre-push check (parent §4 step 3): the named checks over exactly the commits a push would add, a PR or review body, and the
// bench's own branch. Every finding is `{ commit?, path, line, what }` - where and which, never the matched text. Exit codes:
// 0 clean, 1 refused, 3 the environment (git, the network, an unreadable aid), 4 configuration (a branch that is not there).
import { ok } from '../../minigit-bench/lib/result.mjs';
import { checkCreditTerms, carriesKnownText } from './push-credits.mjs';
import { EXIT } from './push-exit.mjs';
import { branchTip, changesOf, newCommits, readBlobs, readFileAt, tipTree } from './push-range.mjs';
import { TASK_REPO_URL, verifyFresh, verifyOrigin } from './push-remote.mjs';
import { attributionFindings, COMMIT_MESSAGE, entryFindings, flatten, keptFindings, messageFindings, scanEntries, scanText, SUBMODULE } from './push-scan.mjs';
import { findSubmoduleProblems, findSymlinks } from './push-shape.mjs';
import { findStackFolderFiles } from './push-visible.mjs';

export { EXIT };
export { COMMIT_MESSAGE };
const NAMES = Object.freeze(['leakcheck', 'secrets', 'attribution', 'run-base-text', 'family-names', 'known-task-credits', 'stack-folders', 'tree-shape']);
const SELF_NAMES = Object.freeze(['leakcheck', 'secrets']);
const MAX_PRINTED = 100;

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

/** ok({ commits: [sha], tip }) of a branch: the commits a push would add and the tip it ends at. */
async function rangeAndTip(repo, branch) {
  const range = await newCommits(repo, branch);
  const tip = range.ok ? await branchTip(repo, branch) : range;
  return tip.ok ? ok({ commits: range.value, tip: tip.value }) : tip;
}

/** ok({ commits, tip, found, files, tipTree }): the range, the tip it ends at, and the tagged findings of every message and blob in it. */
async function scanBranch({ repo, branch, inputs, refuseNul }) {
  const head = await rangeAndTip(repo, branch);
  const changes = head.ok ? await changesOf(repo, head.value.commits) : head;
  if (!changes.ok) return changes;
  const { files, messages } = changes.value;
  const blobs = await readBlobs(repo, files.filter(f => f.mode !== SUBMODULE).map(f => f.oid));
  const tree = await tipTree(repo, branch);
  const bad = firstFailure(blobs, tree);
  if (bad) return bad;
  const entries = scanEntries(files, tree.value);
  const found = keptFindings([...messageFindings(messages, inputs), ...entries.flatMap(e => entryFindings(e, blobs.value.get(e.oid), inputs, { refuseNul }))]);
  return ok({ commits: head.value.commits.length, tip: head.value.tip, found, files, tree: tree.value });
}

/** Branch-level findings by check name: the README's structure, the credit terms, the stack folders and the tree's shape. */
async function tipFindings({ repo, branch, inputs, scan }) {
  const readme = await readFileAt(repo, branch, 'README.md');
  const modules = await readFileAt(repo, branch, '.gitmodules');
  const failure = firstFailure(readme, modules);
  if (failure) return failure;
  const anyCommit = firstCommitMap(scan.files, () => true);
  const atTip = firstCommitMap(scan.files, f => scan.tree.get(f.path)?.oid === f.oid);
  const tipEntries = new Map([...atTip.keys()].map(p => [p, scan.tree.get(p)]));
  const added = [...anyCommit.keys()];
  const withCommit = (commits, f) => ({ commit: commits.get(f.path), ...f });
  return ok({
    attribution: attributionFindings(readme.value, added, carriesKnownText),
    'known-task-credits': checkCreditTerms({ credits: inputs.credits, paths: added, readmeText: readme.value }).map(f => withCommit(anyCommit, f)),
    'stack-folders': findStackFolderFiles(tipEntries, { templateFiles: inputs.templateFiles }).map(f => withCommit(atTip, f)),
    'tree-shape': [...findSubmoduleProblems({ tip: scan.tree, gitmodulesText: modules.value }), ...findSymlinks(tipEntries)].map(f => withCommit(atTip, f)),
  });
}

/** The tagged findings grouped under the named checks, in the named order. */
const groupChecks = (names, findings) => names.map(name => ({ name, findings: findings.filter(f => f.check === name).map(({ check, atTip, ...f }) => f) }));

/** ok(null) when origin is the task repository and the tracking refs are what it has now. */
async function verifyRemote(repo, expectedOrigin) {
  const origin = await verifyOrigin(repo, expectedOrigin);
  return origin.ok ? verifyFresh(repo) : origin;
}

/** The check of a branch's new commits (§4 step 3). `inputs`: aids, known, credits (parseCredits' answer), templateFiles. */
export async function checkBranch({ repo, branch, inputs, expectedOrigin = TASK_REPO_URL }) {
  const label = `${repo} ${branch}`;
  const origin = await verifyRemote(repo, expectedOrigin);
  const scan = origin.ok ? await scanBranch({ repo, branch, inputs, refuseNul: true }) : origin;
  const tip = scan.ok ? await tipFindings({ repo, branch, inputs, scan: scan.value }) : scan;
  if (!tip.ok) return errorResult(label, tip);
  const all = [...scan.value.found, ...flatten(tip.value, undefined)];
  return verdict(groupChecks(NAMES, all), label, { commits: scan.value.commits, tip: scan.value.tip, branch, tokens: tokenLine(inputs) });
}

/** The bench's own branch (consult C6b, D2): only the leak and the secret scans, over the new commits. */
export async function checkSelf({ repo, branch, inputs }) {
  const label = `${repo} ${branch} (bench branch)`;
  const scan = await scanBranch({ repo, branch, inputs, refuseNul: false });
  if (!scan.ok) return errorResult(label, scan);
  return verdict(groupChecks(SELF_NAMES, scan.value.found), label, { commits: scan.value.commits, tip: scan.value.tip, branch, tokens: tokenLine(inputs) });
}

/**
 * A PR or review body (consult C6b): the leak, secret and placement scans on the text; links are allowed in a body. With `self`
 * (a body of the bench's own PR) only the leak and secret scans run, like the bench branch's.
 */
export function checkText({ text, label, inputs, self = false }) {
  const names = self ? SELF_NAMES : ['leakcheck', 'secrets', 'run-base-text', 'family-names'];
  const found = flatten(scanText(label, text, inputs, { body: true }), undefined);
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
