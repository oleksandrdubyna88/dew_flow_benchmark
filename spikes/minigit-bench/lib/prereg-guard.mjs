// The pre-registration guard (E3b.S1 step 10): EVERY paid launch passes it, no override. The analysis is fixed in public history before the first paid
// minigit session, so a number cannot choose its own analysis. Checks, in order (all problems are collected and named):
//   the tag      exists on origin (`git ls-remote --tags origin`) and peels to `runMap.preregCommit`
//   the blobs    every registered file's blob at HEAD equals its blob at the tagged commit, except the amendments file (the tag's text must be a prefix),
//                the seed file (absent at the tag; when present it must hash to the seed hash file; required when `requireSeed`) and the crosscheck policy
//                (may differ only in its free fields: S4's T3 commit); the amendable inputs may differ only through a dated amendments line
//                `input:<file>:<12 hex of the new content's sha256>` (contract section 10: the instrument may change after the A/A); a file with a structured
//                exception (`config.structured`) may differ in the way that exception allows
//   consistency  a C# prompt names the SDK that stacks.json pins (`prereg-consistency.mjs`)
//   paid only    `git status --porcelain -- spikes/` is empty (untracked included), `harnessIdentity` is the run-map's or an accepted one, and the configuration's own
//                branch checks hold (`config.branch`)
// `harnessIdentity` hashes git TREE ids, never HEAD: committing an amendment or the seed leaves it alone, a code change does not.
// The lists the guard works over are ONE configuration (`V1_PREREG_CONFIG`, v1's own and the default). A later series with other files, folders and rules (language comparison v2's Stage 1,
// `spikes/lang-explore/lib/stage-prereg.mjs`) passes its own and is answered by this same code: no second guard exists. A configuration names every path from the repository's root.
import { createHash } from 'node:crypto';
import { fail, ok } from './result.mjs';
import { parseAmendments } from './amendments.mjs';
import { sdkCheckOfTexts } from './prereg-consistency.mjs';
import { runBounded } from './run.mjs';

const INPUTS = 'spikes/minigit-bench/inputs';
/** The registered files, in `spikes/minigit-bench/inputs/` (E3b.S3's table; a test compares this list with it). */
export const PREREG_FILES = Object.freeze([
  'statement.md', 'analysis-contract.md', 'checklist.json', 'heldout-prereg.json', 'prediction.md', 'prompts-bare.json', 'cache-recipe.json', 'cache-snapshots.json',
  'package-flags.json', 'quiet.json', 'spend-prior.json', 'seed.sha256', 'crosscheck-policy.json', 'amendments.md',
  // beyond the plan's S3 table (review M3): the inputs a run reads that decide the treatment and the money
  'stacks.json', 'prices-2026-10-29.json', 'hashes.txt',
]);
/** The three files above the plan's table. The plan (todo/) is not edited here; a test pins the difference. */
export const BEYOND_PLAN_TABLE = Object.freeze(['stacks.json', 'prices-2026-10-29.json', 'hashes.txt']);
/** The registered files that may change after the tag, but only through a dated amendments.md line naming the new content: the instrument inputs (contract section 10: the sampler, the quiet thresholds, the counters). */
export const AMENDABLE_INPUTS = Object.freeze(['quiet.json', 'package-flags.json']);
const POLICY_FREE = Object.freeze(['t2', 't3', 'decidedOn', 'reason']);
const HARNESS_DIRS = Object.freeze(['spikes/minigit-bench/lib', 'spikes/minigit-bench/harness', 'spikes/minigit-bench/capture', 'spikes/minigit-bench/prereg', 'spikes/policy-ab-quick/lib', 'spikes/policy-ab-quick/hooks']);
const GIT_MS = 60_000;
const GIT_MAX_BYTES = 8 * 1024 * 1024;
const GIT_ENV = Object.freeze({ ...process.env, GIT_TERMINAL_PROMPT: '0' });
const sha256 = text => createHash('sha256').update(text).digest('hex');

const underInputs = name => `${INPUTS}/${name}`;

/**
 * v1's configuration, the default of both exports. `files` are paths from the repository's root, each a text or a `{ dir, pattern, what, exactlyOne }` entry that names every file of a folder whose name matches (the files
 * the tag holds and the files HEAD holds; `exactlyOne` asks that the tag holds exactly one). `labelBase` is cut from a path in a sentence, so v1's sentences name `quiet.json`, not its whole path. `amendable` maps a registered
 * path to the name its dated `input:` line gives it. `policy` is `{ file, free, rule, why }` or `null` (a later series adds `required: true`, so a policy absent at the tag or deleted at HEAD is a refusal, and `dated: <name>`, so a change of its free fields needs a dated `input:<name>:<12 hex>` line like an amended input; v1's has neither). A policy that is not JSON is a refusal, never a throw. `structured` maps a registered path to `async ({ repo, commit, label }) => sentence | null`, the exception that file's
 * difference may fall under. `harnessAcceptance` is the words that end the sentence of a harness identity nothing accepted (v1's name its flag; Stage 1 names the dated line). `consistency`, `branch` and `frozenInputs` are `async (repo, runMap) => [sentence]` (`frozenInputs`: the amended inputs that a later series freezes at its first data session; v1's asks nothing here, its own boundary lives in `run-map.mjs`).
 */
export const V1_PREREG_CONFIG = Object.freeze({
  labelBase: `${INPUTS}/`,
  files: Object.freeze(PREREG_FILES.map(underInputs)),
  amendments: underInputs('amendments.md'),
  seedFile: underInputs('seed.txt'),
  seedHash: underInputs('seed.sha256'),
  amendable: Object.freeze(Object.fromEntries(AMENDABLE_INPUTS.map(name => [underInputs(name), name]))),
  policy: Object.freeze({ file: underInputs('crosscheck-policy.json'), free: POLICY_FREE, rule: 't2, t3 and their dated reason', why: 'S4\'s T3 commit' }),
  structured: Object.freeze({}),
  harnessAcceptance: '(--accept-harness)',
  harnessDirs: HARNESS_DIRS,
  harnessTopDirs: Object.freeze(['spikes/minigit-bench/']),
  consistency: v1Consistency,
  branch: async () => [],
  frozenInputs: async () => [],
});

const labelOf = (config, file) => (file.startsWith(config.labelBase) ? file.slice(config.labelBase.length) : file);

/** The answer of one finished git run as a value: a run stopped by its timeout and an output cut at the size limit are failures that say so (a shorter text is never an answer); a non-zero exit carries the exit code. */
function answered(r, args, timeoutMs) {
  if (r.value.timedOut) return fail(`git ${args[0]} timed out after ${timeoutMs} ms and was stopped`, { code: null, timedOut: true });
  if (r.value.truncated) return fail(`git ${args[0]} output was truncated at ${GIT_MAX_BYTES} bytes: a shorter text is no answer`, { code: null, truncated: true });
  return r.value.code === 0 ? ok(r.value.stdout) : fail(`git ${args[0]} exited ${r.value.code}: ${r.value.stderr.trim().slice(-200)}`, { code: r.value.code });
}

/** `ok(stdout)` or `fail`: git as exe + argv, bounded by `timeoutMs` (GIT_MS unless a caller names another; exported: a later series' own checks run git the same way). A non-zero exit is a value (`fail`) carrying the exit code; a run stopped by its timeout is one too, and says so (never "exited null"), and so is an output cut at `GIT_MAX_BYTES`. `env` adds to the environment (a series' own take builds a commit in an index file of its own: `GIT_INDEX_FILE`) and `input` is written to git's stdin. */
export async function git(repo, args, { timeoutMs = GIT_MS, env = {}, input } = {}) {
  const r = await runBounded('git', args, { cwd: repo, env: { ...GIT_ENV, ...env }, timeoutMs, input, maxBytes: GIT_MAX_BYTES });
  return r.ok ? answered(r, args, timeoutMs) : r;
}

/** The tree id of a path at `rev`, or `absent`. */
const treeIdAt = async (repo, rev, p) => { const r = await git(repo, ['rev-parse', `${rev}:${p}`]); return r.ok ? r.value.trim() : 'absent'; };

/** The top-level `*.mjs` blobs of one folder at `rev`: `<file>\t<blob id>` lines. */
async function topFilesAt(repo, rev, dir) {
  const listed = await git(repo, ['ls-tree', rev, dir]);
  if (!listed.ok) return listed;
  return ok(listed.value.split('\n').filter(l => /^\d+ blob [0-9a-f]+\t.*\.mjs$/.test(l)).map(l => { const [meta, file] = l.split('\t'); return `${file}\t${meta.split(' ')[2]}`; }));
}

/** sha256 over the git tree ids of the harness's code (the configuration's folders) and the top-level *.mjs of its spikes: v1's is the spike's lib, harness, capture and prereg, its top-level *.mjs, policy-ab-quick's lib and hooks. `rev` is HEAD unless a commit is named. */
export async function harnessIdentity(repo, config = V1_PREREG_CONFIG, rev = 'HEAD') {
  const ids = await Promise.all(config.harnessDirs.map(async d => `${d}\t${await treeIdAt(repo, rev, d)}`));
  const tops = await Promise.all(config.harnessTopDirs.map(dir => topFilesAt(repo, rev, dir)));
  const refused = tops.find(t => !t.ok);
  if (refused) return refused;
  return ok(sha256([...ids, ...tops.flatMap(t => t.value)].sort().join('\n')));
}

/** The commit the tag peels to on origin, or a fail saying the tag is not there. */
async function remotePeel(repo, tag) {
  const r = await git(repo, ['ls-remote', '--tags', 'origin', `refs/tags/${tag}`, `refs/tags/${tag}^{}`]);
  if (!r.ok) return fail(`cannot read the tags of origin: ${r.reason}`);
  const rows = r.value.split('\n').filter(Boolean).map(l => l.split('\t'));
  const row = rows.find(x => x[1].endsWith('^{}')) ?? rows[0];
  return row ? ok(row[0]) : fail(`the tag ${tag} is not on origin: push it before any paid launch`);
}

export const blobAt = async (repo, rev, file) => { const r = await git(repo, ['rev-parse', `${rev}:${file}`]); return r.ok ? r.value.trim() : null; };
export const textAt = async (repo, rev, file) => { const r = await git(repo, ['show', `${rev}:${file}`]); return r.ok ? r.value : null; };

/** The names of a folder at `rev` that match a `{ dir, pattern }` entry, as paths from the root; none when the folder is absent. */
async function namesAt(repo, rev, entry) {
  const listed = await git(repo, ['ls-tree', '--name-only', rev, `${entry.dir}/`]);
  return listed.ok ? listed.value.split('\n').filter(p => p !== '' && entry.pattern.test(p.slice(entry.dir.length + 1))) : [];
}

/** The sentence for an `exactlyOne` entry that does not match exactly one file at the tag, or null. */
const exactlyOneProblem = (entry, tagged) => (entry.exactlyOne !== true || tagged.length === 1 ? null : `${entry.what} must be exactly one file of ${entry.dir}/ at the tagged commit (found ${tagged.length}${tagged.length === 0 ? '' : `: ${tagged.join(', ')}`})`);

/** What one folder entry stands for: the files of the tag and of HEAD together (a file added after the tag is a registered file the tag lacks), and the sentence for a count the entry forbids. */
async function expandEntry(repo, commit, entry) {
  const [tagged, head] = await Promise.all([namesAt(repo, commit, entry), namesAt(repo, 'HEAD', entry)]);
  return { files: [...new Set([...tagged, ...head])].sort(), problem: exactlyOneProblem(entry, tagged) };
}

/** Every registered file of the configuration, the folder entries expanded: `{ files, problems }`. */
async function registeredFiles(repo, commit, config) {
  const parts = await Promise.all(config.files.map(f => (typeof f === 'string' ? { files: [f], problem: null } : expandEntry(repo, commit, f))));
  return { files: parts.flatMap(p => p.files), problems: parts.map(p => p.problem).filter(Boolean) };
}

/** The fields of the crosscheck policy that differ between two texts and are not free to change (`free`). */
const changedPolicyFields = (a, b, free) => [...new Set([...Object.keys(a), ...Object.keys(b)])].filter(k => !free.includes(k) && JSON.stringify(a[k]) !== JSON.stringify(b[k]));

/** `ok(object)` of a policy text, or the sentence for a text that is not JSON (`where` says whether it is the tag's or HEAD's). */
function parsedPolicy({ text, where, label }) {
  try {
    return ok(JSON.parse(text));
  } catch {
    return fail(`${label} ${where} is not JSON: the policy cannot be compared with the tag`);
  }
}

/** The sentence for a policy file that a configuration that REQUIRES one does not have (deleted at HEAD, or absent at the tag), or null. v1's does not require it. */
function missingPolicyProblem({ tagged, head, config, label }) {
  if (config.policy.required !== true) return null;
  if (head === null) return `${label} is deleted at HEAD`;
  return tagged === null ? `${label} is missing at the tagged commit` : null;
}

/** The sentence for a policy that differs from the tag: a field that is not free, or (when the configuration asks for a dated line, `policy.dated`) content no dated `input:` line names. */
async function comparedPolicy({ repo, tagged, head, config }) {
  const { file, free, rule, why, dated } = config.policy;
  const label = labelOf(config, file);
  const [was, now] = [parsedPolicy({ text: tagged, where: 'at the tagged commit', label }), parsedPolicy({ text: head, where: 'at HEAD', label })];
  const unreadable = [was, now].find(r => !r.ok);
  if (unreadable) return unreadable.reason;
  const fields = changedPolicyFields(was.value, now.value, free);
  return fields.length > 0 ? `${label} differs from the tag in ${fields.join(', ')}: only ${rule} may change (${why})` : datedPolicyProblem({ repo, tagged, head, dated, config });
}

/** The sentence for a policy whose free fields changed with no dated line naming the new content, when the configuration asks for one (`dated`), or null. */
async function datedPolicyProblem({ repo, tagged, head, dated, config }) {
  return dated && head !== tagged ? datedLineProblem({ repo, name: dated, content: head, config }) : null;
}

async function policyProblem(repo, commit, config) {
  const { file } = config.policy;
  const [tagged, head] = await Promise.all([textAt(repo, commit, file), textAt(repo, 'HEAD', file)]);
  const absent = missingPolicyProblem({ tagged, head, config, label: labelOf(config, file) });
  if (absent || tagged === null || head === null) return absent;
  return comparedPolicy({ repo, tagged, head, config });
}

async function amendmentsProblem(repo, commit, config) {
  const [tagged, head] = await Promise.all([textAt(repo, commit, config.amendments), textAt(repo, 'HEAD', config.amendments)]);
  const name = labelOf(config, config.amendments);
  if (tagged === null) return `${name} is missing at the tagged commit`;
  return head !== null && head.startsWith(tagged) ? null : `${name} at HEAD is not the tagged text with lines appended (a registered amendment file is append-only)`;
}

const hashMatches = (head, hash) => hash !== null && sha256(head.trim()) === hash.trim();

/** The sentence for the seed file at HEAD (absent, or not the committed one), or null. */
function seedFileProblem({ head, hash, requireSeed, config }) {
  const [seed, hashName] = [labelOf(config, config.seedFile), labelOf(config, config.seedHash)];
  if (head === null) return requireSeed ? `${seed} is required for a data pair and is absent at HEAD: run seed-reveal after the A/A` : null;
  return hashMatches(head, hash) ? null : `${seed} does not hash to ${hashName}`;
}

async function seedProblem(repo, commit, requireSeed, config) {
  if (await blobAt(repo, commit, config.seedFile) !== null) return `${labelOf(config, config.seedFile)} exists at the tagged commit: the seed must be revealed AFTER the tag`;
  const [head, hash] = await Promise.all([textAt(repo, 'HEAD', config.seedFile), textAt(repo, 'HEAD', config.seedHash)]);
  return seedFileProblem({ head, hash, requireSeed, config });
}

/** v1's consistency: the registered files must not contradict each other: today, the SDK a C# prompt names against the pin. */
async function v1Consistency(repo) {
  const [promptsText, stacksText] = await Promise.all([textAt(repo, 'HEAD', underInputs('prompts-bare.json')), textAt(repo, 'HEAD', underInputs('stacks.json'))]);
  if (promptsText === null || stacksText === null) return [];
  const r = sdkCheckOfTexts({ promptsText, stacksText });
  return r.ok ? [] : [r.reason];
}

/** The sentence for a file whose content at HEAD no dated `input:<name>:<12 hex of its sha256>` line of HEAD's amendments names, or null. */
async function datedLineProblem({ repo, name, content, config }) {
  const amendments = await textAt(repo, 'HEAD', config.amendments);
  const hash12 = sha256(content ?? '').slice(0, 12);
  const named = parseAmendments(amendments ?? '').inputs.some(i => i.file === name && i.hash12 === hash12);
  return named ? null : `${name} differs from the tagged commit and no dated ${labelOf(config, config.amendments)} line names it: add \`YYYY-MM-DD | instrument | input:${name}:${hash12} | <reason, with its G-id>\``;
}

/** An amendable input that differs from the tag needs a dated line of HEAD's amendments file naming `input:<name>:<12 hex of its sha256>`. */
async function amendedInputProblem(repo, file, config) {
  return datedLineProblem({ repo, name: config.amendable[file], content: await textAt(repo, 'HEAD', file), config });
}

/** The sentence for a registered file that is not the tagged one: deleted, differs, an amendable input that needs its dated line, or a structured exception that says. */
async function changedBlobProblem({ repo, commit, file, head, config }) {
  const label = labelOf(config, file);
  if (head === null) return `${label} is deleted at HEAD`;
  if (Object.hasOwn(config.amendable, file)) return amendedInputProblem(repo, file, config);
  const exception = config.structured[file];
  return exception === undefined ? `${label} differs from the tagged commit` : exception({ repo, commit, label });
}

async function plainBlobProblem({ repo, commit, file, config }) {
  const [tagged, head] = await Promise.all([blobAt(repo, commit, file), blobAt(repo, 'HEAD', file)]);
  if (tagged === null) return `${labelOf(config, file)} is missing at the tagged commit`;
  return head === tagged ? null : changedBlobProblem({ repo, commit, file, head, config });
}

const specialFiles = config => new Set([config.amendments, config.policy?.file].filter(Boolean));

async function blobProblems(repo, commit, requireSeed, config) {
  const registered = await registeredFiles(repo, commit, config);
  const special = specialFiles(config);
  const plain = await Promise.all(registered.files.filter(f => !special.has(f)).map(file => plainBlobProblem({ repo, commit, file, config })));
  const policy = config.policy ? await policyProblem(repo, commit, config) : null;
  return [...registered.problems, ...plain, await amendmentsProblem(repo, commit, config), policy, await seedProblem(repo, commit, requireSeed, config), ...await config.consistency(repo)].filter(Boolean);
}

/** The problems of the working tree under `spikes/`: it must be clean for a paid launch. */
async function cleanTreeProblems(repo) {
  const status = await git(repo, ['status', '--porcelain', '--untracked-files=all', '--', 'spikes/']);
  return !status.ok ? [status.reason] : status.value.trim() === '' ? [] : [`the tree under spikes/ is not clean (${status.value.trim().split('\n').slice(0, 5).join('; ')})`];
}

/** The problems of the harness identity: it is the run-map's or an accepted one. */
async function harnessProblems(repo, runMap, config) {
  const identity = await harnessIdentity(repo, config);
  if (!identity.ok) return [identity.reason];
  const accepted = identity.value === runMap.harnessIdentity || (runMap.harnessAccepted ?? []).includes(identity.value);
  return accepted ? [] : [`harnessIdentity ${identity.value.slice(0, 12)} is not the run-map's ${String(runMap.harnessIdentity).slice(0, 12)} and no amendment accepted it ${config.harnessAcceptance}`];
}

const paidProblems = async (repo, runMap, config) => [...await cleanTreeProblems(repo), ...await harnessProblems(repo, runMap, config), ...await config.branch(repo, runMap), ...await config.frozenInputs(repo, runMap)];

/** The `fail` for a tag that is not on origin, peels to another commit, or is not in this clone; null when the tag is as the run-map pinned it. */
async function tagProblem(repo, runMap) {
  const peeled = await remotePeel(repo, runMap.preregTag);
  if (!peeled.ok) return fail(peeled.reason, { problems: [peeled.reason] });
  if (peeled.value !== runMap.preregCommit) {
    const why = `the tag ${runMap.preregTag} on origin peels to ${peeled.value.slice(0, 12)}, the run-map pinned ${String(runMap.preregCommit).slice(0, 12)}`;
    return fail(why, { problems: [why] });
  }
  const local = await git(repo, ['cat-file', '-e', `${runMap.preregCommit}^{commit}`]);
  return local.ok ? null : fail(`the tagged commit ${runMap.preregCommit.slice(0, 12)} is not in this clone: fetch the tag`, { problems: ['tagged commit not fetched'] });
}

/** -> `ok(null)` | `fail(reason, { problems })`, every problem named (a file or a field). `paid` adds the clean-tree, harness and branch checks. `config` is v1's unless a series passes its own. */
export async function preregGuard({ repo, runMap, paid, requireSeed = false, config = V1_PREREG_CONFIG }) {
  const refused = await tagProblem(repo, runMap);
  if (refused) return refused;
  const problems = [...await blobProblems(repo, runMap.preregCommit, requireSeed, config), ...(paid ? await paidProblems(repo, runMap, config) : [])];
  return problems.length === 0 ? ok(null) : fail(`the pre-registration guard refuses: ${problems.join('; ')}`, { problems });
}
