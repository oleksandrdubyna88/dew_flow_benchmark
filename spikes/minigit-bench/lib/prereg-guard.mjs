// The pre-registration guard (E3b.S1 step 10): EVERY paid launch passes it, no override. The analysis is fixed in public history before the first paid
// minigit session, so a number cannot choose its own analysis. Checks, in order (all problems are collected and named):
//   the tag      exists on origin (`git ls-remote --tags origin`) and peels to `runMap.preregCommit`
//   the blobs    every PREREG_FILES blob at HEAD equals its blob at the tagged commit, except `amendments.md` (the tag's text must be a prefix),
//                `seed.txt` (absent at the tag; when present it must hash to `seed.sha256`; required when `requireSeed`) and `crosscheck-policy.json`
//                (may differ only in t2, t3 and their dated reason: S4's T3 commit); `quiet.json` and `package-flags.json` may differ only through a dated
//                amendments.md line `input:<file>:<12 hex of the new content's sha256>` (contract section 10: the instrument may change after the A/A)
//   consistency  a C# prompt names the SDK that stacks.json pins (`prereg-consistency.mjs`)
//   paid only    `git status --porcelain -- spikes/` is empty (untracked included) and `harnessIdentity` is the run-map's or an accepted one
// `harnessIdentity` hashes git TREE ids, never HEAD: committing an amendment or the seed leaves it alone, a code change does not.
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
const SEED_FILE = 'seed.txt';
const POLICY_FREE = Object.freeze(['t2', 't3', 'decidedOn', 'reason']);
const HARNESS_DIRS = Object.freeze(['spikes/minigit-bench/lib', 'spikes/minigit-bench/harness', 'spikes/minigit-bench/capture', 'spikes/minigit-bench/prereg', 'spikes/policy-ab-quick/lib', 'spikes/policy-ab-quick/hooks']);
const GIT_MS = 60_000;
const GIT_ENV = Object.freeze({ ...process.env, GIT_TERMINAL_PROMPT: '0' });
const sha256 = text => createHash('sha256').update(text).digest('hex');

/** `ok(stdout)` or `fail`: git as exe + argv, bounded. A non-zero exit is a value (`fail`) carrying the exit code. */
async function git(repo, args) {
  const r = await runBounded('git', args, { cwd: repo, env: GIT_ENV, timeoutMs: GIT_MS });
  if (!r.ok) return r;
  return r.value.code === 0 ? ok(r.value.stdout) : fail(`git ${args[0]} exited ${r.value.code}: ${r.value.stderr.trim().slice(-200)}`, { code: r.value.code });
}

/** The tree id of a path at HEAD, or `absent`. */
const treeIdAt = async (repo, p) => { const r = await git(repo, ['rev-parse', `HEAD:${p}`]); return r.ok ? r.value.trim() : 'absent'; };

/** sha256 over the git tree ids of the harness's code: the spike's lib, harness, capture and prereg, its top-level *.mjs, policy-ab-quick's lib and hooks. */
export async function harnessIdentity(repo) {
  const ids = await Promise.all(HARNESS_DIRS.map(async d => `${d}\t${await treeIdAt(repo, d)}`));
  const listed = await git(repo, ['ls-tree', 'HEAD', 'spikes/minigit-bench/']);
  if (!listed.ok) return listed;
  const tops = listed.value.split('\n').filter(l => /^\d+ blob [0-9a-f]+\t.*\.mjs$/.test(l)).map(l => { const [meta, file] = l.split('\t'); return `${file}\t${meta.split(' ')[2]}`; });
  return ok(sha256([...ids, ...tops].sort().join('\n')));
}

/** The commit the tag peels to on origin, or a fail saying the tag is not there. */
async function remotePeel(repo, tag) {
  const r = await git(repo, ['ls-remote', '--tags', 'origin', `refs/tags/${tag}`, `refs/tags/${tag}^{}`]);
  if (!r.ok) return fail(`cannot read the tags of origin: ${r.reason}`);
  const rows = r.value.split('\n').filter(Boolean).map(l => l.split('\t'));
  const row = rows.find(x => x[1].endsWith('^{}')) ?? rows[0];
  return row ? ok(row[0]) : fail(`the tag ${tag} is not on origin: push it before any paid launch`);
}

const blobAt = async (repo, rev, rel) => { const r = await git(repo, ['rev-parse', `${rev}:${INPUTS}/${rel}`]); return r.ok ? r.value.trim() : null; };
const textAt = async (repo, rev, rel) => { const r = await git(repo, ['show', `${rev}:${INPUTS}/${rel}`]); return r.ok ? r.value : null; };

/** The fields of the crosscheck policy that differ between two texts and are not free to change (`POLICY_FREE`). */
const changedPolicyFields = (a, b) => [...new Set([...Object.keys(a), ...Object.keys(b)])].filter(k => !POLICY_FREE.includes(k) && JSON.stringify(a[k]) !== JSON.stringify(b[k]));

async function policyProblem(repo, commit) {
  const [tagged, head] = await Promise.all([textAt(repo, commit, 'crosscheck-policy.json'), textAt(repo, 'HEAD', 'crosscheck-policy.json')]);
  if (tagged === null || head === null) return null;
  const fields = changedPolicyFields(JSON.parse(tagged), JSON.parse(head));
  return fields.length > 0 ? `crosscheck-policy.json differs from the tag in ${fields.join(', ')}: only t2, t3 and their dated reason may change (S4's T3 commit)` : null;
}

async function amendmentsProblem(repo, commit) {
  const [tagged, head] = await Promise.all([textAt(repo, commit, 'amendments.md'), textAt(repo, 'HEAD', 'amendments.md')]);
  if (tagged === null) return 'amendments.md is missing at the tagged commit';
  return head !== null && head.startsWith(tagged) ? null : 'amendments.md at HEAD is not the tagged text with lines appended (a registered amendment file is append-only)';
}

const hashMatches = (head, hash) => hash !== null && sha256(head.trim()) === hash.trim();

/** The sentence for the seed file at HEAD (absent, or not the committed one), or null. */
function seedFileProblem({ head, hash, requireSeed }) {
  if (head === null) return requireSeed ? 'seed.txt is required for a data pair and is absent at HEAD: run seed-reveal after the A/A' : null;
  return hashMatches(head, hash) ? null : 'seed.txt does not hash to seed.sha256';
}

async function seedProblem(repo, commit, requireSeed) {
  if (await blobAt(repo, commit, SEED_FILE) !== null) return 'seed.txt exists at the tagged commit: the seed must be revealed AFTER the tag';
  const [head, hash] = await Promise.all([textAt(repo, 'HEAD', SEED_FILE), textAt(repo, 'HEAD', 'seed.sha256')]);
  return seedFileProblem({ head, hash, requireSeed });
}

/** The registered files must not contradict each other: today, the SDK a C# prompt names against the pin. */
async function consistencyProblems(repo) {
  const [promptsText, stacksText] = await Promise.all([textAt(repo, 'HEAD', 'prompts-bare.json'), textAt(repo, 'HEAD', 'stacks.json')]);
  if (promptsText === null || stacksText === null) return [];
  const r = sdkCheckOfTexts({ promptsText, stacksText });
  return r.ok ? [] : [r.reason];
}

/** An amendable input that differs from the tag needs a dated line of HEAD's amendments.md naming `input:<file>:<12 hex of its sha256>`. */
async function amendedInputProblem(repo, file) {
  const [content, amendments] = await Promise.all([textAt(repo, 'HEAD', file), textAt(repo, 'HEAD', 'amendments.md')]);
  const hash12 = sha256(content ?? '').slice(0, 12);
  const named = parseAmendments(amendments ?? '').inputs.some(i => i.file === file && i.hash12 === hash12);
  return named ? null : `${file} differs from the tagged commit and no dated amendments.md line names it: add \`YYYY-MM-DD | instrument | input:${file}:${hash12} | <reason, with its G-id>\``;
}

/** The sentence for a registered file that is not the tagged one: deleted, differs, or an amendable input that needs its dated line. */
const changedBlobProblem = (repo, f, head) => (head === null ? `${f} is deleted at HEAD` : AMENDABLE_INPUTS.includes(f) ? amendedInputProblem(repo, f) : `${f} differs from the tagged commit`);

async function plainBlobProblem(repo, commit, f) {
  const [tagged, head] = await Promise.all([blobAt(repo, commit, f), blobAt(repo, 'HEAD', f)]);
  if (tagged === null) return `${f} is missing at the tagged commit`;
  return head === tagged ? null : changedBlobProblem(repo, f, head);
}

async function blobProblems(repo, commit, requireSeed) {
  const special = new Set(['amendments.md', 'crosscheck-policy.json']);
  const plain = await Promise.all(PREREG_FILES.filter(f => !special.has(f)).map(f => plainBlobProblem(repo, commit, f)));
  return [...plain, await amendmentsProblem(repo, commit), await policyProblem(repo, commit), await seedProblem(repo, commit, requireSeed), ...await consistencyProblems(repo)].filter(Boolean);
}

/** The problems of the working tree under `spikes/`: it must be clean for a paid launch. */
async function cleanTreeProblems(repo) {
  const status = await git(repo, ['status', '--porcelain', '--untracked-files=all', '--', 'spikes/']);
  return !status.ok ? [status.reason] : status.value.trim() === '' ? [] : [`the tree under spikes/ is not clean (${status.value.trim().split('\n').slice(0, 5).join('; ')})`];
}

/** The problems of the harness identity: it is the run-map's or an accepted one. */
async function harnessProblems(repo, runMap) {
  const identity = await harnessIdentity(repo);
  if (!identity.ok) return [identity.reason];
  const accepted = identity.value === runMap.harnessIdentity || (runMap.harnessAccepted ?? []).includes(identity.value);
  return accepted ? [] : [`harnessIdentity ${identity.value.slice(0, 12)} is not the run-map's ${String(runMap.harnessIdentity).slice(0, 12)} and no amendment accepted it (--accept-harness)`];
}

const paidProblems = async (repo, runMap) => [...await cleanTreeProblems(repo), ...await harnessProblems(repo, runMap)];

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

/** -> `ok(null)` | `fail(reason, { problems })`, every problem named (a file or a field). `paid` adds the clean-tree and harness checks. */
export async function preregGuard({ repo, runMap, paid, requireSeed = false }) {
  const refused = await tagProblem(repo, runMap);
  if (refused) return refused;
  const problems = [...await blobProblems(repo, runMap.preregCommit, requireSeed), ...(paid ? await paidProblems(repo, runMap) : [])];
  return problems.length === 0 ? ok(null) : fail(`the pre-registration guard refuses: ${problems.join('; ')}`, { problems });
}
