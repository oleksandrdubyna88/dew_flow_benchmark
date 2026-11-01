// The pre-push check (parent §4 step 3): the named checks over exactly the commits a push would add, a PR or review body, and the
// bench's own branch. Every finding is `{ commit?, path, line, what }` - where and which, never the matched text. Exit codes:
// 0 clean, 1 refused, 3 the environment (git, the network, an unreadable aid), 4 configuration (a branch that is not there).
import { ok } from '../../minigit-bench/lib/result.mjs';
import { attributionSection, outsideAttribution } from './attribution.mjs';
import { checkCreditTerms, carriesKnownText } from './push-credits.mjs';
import { findAidHits } from './push-needles.mjs';
import { changesOf, newCommits, readBlobs, readFileAt } from './push-range.mjs';
import { findSecrets } from './push-secrets.mjs';
import { findStackFolderFiles, findVisibleRules, GROUP_FAMILY } from './push-visible.mjs';

export const EXIT = Object.freeze({ PASS: 0, REFUSED: 1, ENVIRONMENT: 3, CONFIG: 4 });
export const COMMIT_MESSAGE = '(commit message)';
const NAMES = Object.freeze(['leakcheck', 'secrets', 'attribution', 'agent-visible-paths', 'family-names', 'known-task-credits', 'stack-folders']);
const SELF_NAMES = Object.freeze(['leakcheck', 'secrets']);
const MAX_PRINTED = 100;

/** The findings of one text, by named check. `file` decides which placement rules apply. */
function scanText(file, text, inputs, { body = false } = {}) {
  const readme = file === 'README.md';
  const visible = findVisibleRules(file, readme ? outsideAttribution(text) : text, { known: inputs.known, assumeVisible: body, links: !body });
  return {
    leakcheck: findAidHits(file, text, inputs.aids),
    secrets: findSecrets(file, text),
    attribution: readme ? visible.filter(f => f.group !== GROUP_FAMILY) : [],
    'agent-visible-paths': readme ? [] : visible.filter(f => f.group !== GROUP_FAMILY),
    'family-names': visible.filter(f => f.group === GROUP_FAMILY),
  };
}

const emptyChecks = names => Object.fromEntries(names.map(name => [name, []]));

function merge(into, found, commit) {
  for (const [name, findings] of Object.entries(found)) if (name in into) into[name].push(...findings.map(f => ({ commit, ...f })));
}

const verdict = (checks, label, extra = {}) => ({
  exit: checks.some(c => c.findings.length > 0) ? EXIT.REFUSED : EXIT.PASS,
  label,
  checks,
  ...extra,
});

const errorResult = (label, failure) => ({ exit: failure.kind === 'config' ? EXIT.CONFIG : EXIT.ENVIRONMENT, label, checks: [], error: failure.reason });
const toChecks = (names, byName) => names.map(name => ({ name, findings: byName[name] }));

/** Scan every message and every distinct (path, blob) of the range; the first commit that brings a path owns its findings. */
async function scanRange(repo, commits, inputs, names) {
  const changes = await changesOf(repo, commits);
  if (!changes.ok) return changes;
  const blobs = await readBlobs(repo, changes.value.files.map(f => f.oid));
  if (!blobs.ok) return blobs;
  const byName = emptyChecks(names);
  for (const { commit, text } of changes.value.messages) merge(byName, scanText(COMMIT_MESSAGE, text, inputs), commit);
  const seen = new Set();
  const firstCommit = new Map();
  for (const { commit, path, oid } of changes.value.files) {
    if (!firstCommit.has(path)) firstCommit.set(path, commit);
    if (seen.has(`${path}\0${oid}`)) continue;
    seen.add(`${path}\0${oid}`);
    merge(byName, scanText(path, blobs.value.get(oid), inputs), commit);
  }
  return ok({ byName, firstCommit, paths: [...firstCommit.keys()] });
}

/** Branch-level findings: the README's structure, the credit terms and the stack folders, with the commit that brought the path. */
async function tipFindings({ repo, branch, inputs, paths, firstCommit }) {
  const readme = await readFileAt(repo, branch, 'README.md');
  if (!readme.ok) return readme;
  const text = readme.value;
  const structure = text !== null && (carriesKnownText(paths) || /^## Attribution\s*$/m.test(text)) ? attributionSection(text) : ok(null);
  const withCommit = f => ({ commit: firstCommit.get(f.path), ...f });
  return ok({
    attribution: structure.ok ? [] : [{ path: 'README.md', line: null, what: structure.reason }],
    credits: checkCreditTerms({ credits: inputs.credits, paths, readmeText: text }).map(withCommit),
    stack: findStackFolderFiles(paths, { templateFiles: inputs.templateFiles }).map(withCommit),
  });
}

/** The check of a branch's new commits (§4 step 3). `inputs`: aids, known, credits (parseCredits' answer), templateFiles. */
export async function checkBranch({ repo, branch, inputs }) {
  const label = `${repo} ${branch}`;
  const range = await newCommits(repo, branch);
  if (!range.ok) return errorResult(label, range);
  const scanned = await scanRange(repo, range.value, inputs, NAMES);
  if (!scanned.ok) return errorResult(label, scanned);
  const tip = await tipFindings({ repo, branch, inputs, paths: scanned.value.paths, firstCommit: scanned.value.firstCommit });
  if (!tip.ok) return errorResult(label, tip);
  const byName = scanned.value.byName;
  byName.attribution = [...tip.value.attribution, ...byName.attribution];
  byName['known-task-credits'] = tip.value.credits;
  byName['stack-folders'] = tip.value.stack;
  return verdict(toChecks(NAMES, byName), label, { commits: range.value.length });
}

/** The bench's own branch (consult C6b, D2): only the leak and the secret scans, over the new commits. */
export async function checkSelf({ repo, branch, inputs }) {
  const label = `${repo} ${branch} (bench branch)`;
  const range = await newCommits(repo, branch);
  if (!range.ok) return errorResult(label, range);
  const scanned = await scanRange(repo, range.value, inputs, SELF_NAMES);
  if (!scanned.ok) return errorResult(label, scanned);
  return verdict(toChecks(SELF_NAMES, scanned.value.byName), label, { commits: range.value.length });
}

/** A PR or review body (consult C6b): the leak, secret and placement scans on the text; links are allowed in a body. */
export function checkText({ text, label, inputs }) {
  const names = ['leakcheck', 'secrets', 'agent-visible-paths', 'family-names'];
  const byName = emptyChecks(names);
  merge(byName, scanText(label, text, inputs, { body: true }), undefined);
  return verdict(toChecks(names, byName), label);
}

const where = f => `${f.path}${f.line === null ? ':' : `:${f.line}`}`;
const findingLine = f => `  ${f.commit ? `${f.commit.slice(0, 10)} ` : ''}${where(f)} ${f.what}`;

/** The report: one named line per check, its findings underneath (capped), and the result line. */
export function formatReport(result) {
  const out = [`push-check: ${result.label}${result.commits === undefined ? '' : ` (${result.commits} new commit${result.commits === 1 ? '' : 's'})`}`];
  if (result.error) out.push(`error: ${result.error}`);
  for (const { name, findings } of result.checks) {
    out.push(`${name.padEnd(20)} ${findings.length === 0 ? 'PASS' : `FAIL (${findings.length})`}`);
    out.push(...findings.slice(0, MAX_PRINTED).map(findingLine));
    if (findings.length > MAX_PRINTED) out.push(`  ... and ${findings.length - MAX_PRINTED} more`);
  }
  const word = { [EXIT.PASS]: 'PASS', [EXIT.REFUSED]: 'REFUSED', [EXIT.ENVIRONMENT]: 'ENVIRONMENT', [EXIT.CONFIG]: 'CONFIGURATION' }[result.exit];
  out.push(`${'result'.padEnd(20)} ${word} (exit ${result.exit})`, '');
  return out.join('\n');
}
