// The pre-registration guard (E3b.S1 step 10): EVERY paid launch passes it, no override. The analysis is fixed in public history before the first paid
// minigit session, so a number cannot choose its own analysis. Checks, in order (all problems are collected and named):
//   the tag      exists on origin (`git ls-remote --tags origin`) and peels to `runMap.preregCommit`
//   the blobs    every PREREG_FILES blob at HEAD equals its blob at the tagged commit, except `amendments.md` (the tag's text must be a prefix),
//                `seed.txt` (absent at the tag; when present it must hash to `seed.sha256`; required when `requireSeed`) and `crosscheck-policy.json`
//                (may differ only in t2, t3 and their dated reason: S4's T3 commit)
//   paid only    `git status --porcelain -- spikes/` is empty (untracked included) and `harnessIdentity` is the run-map's or an accepted one
// `harnessIdentity` hashes git TREE ids, never HEAD: committing an amendment or the seed leaves it alone, a code change does not.
import { createHash } from 'node:crypto';
import { fail, ok } from './result.mjs';
import { runBounded } from './run.mjs';

const INPUTS = 'spikes/minigit-bench/inputs';
/** The registered files, in `spikes/minigit-bench/inputs/` (E3b.S3's table; a test compares this list with it). */
export const PREREG_FILES = Object.freeze([
  'statement.md', 'analysis-contract.md', 'checklist.json', 'heldout-prereg.json', 'prediction.md', 'prompts-bare.json', 'cache-recipe.json', 'cache-snapshots.json',
  'package-flags.json', 'quiet.json', 'spend-prior.json', 'seed.sha256', 'crosscheck-policy.json', 'amendments.md',
]);
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

async function policyProblem(repo, commit) {
  const [tagged, head] = await Promise.all([textAt(repo, commit, 'crosscheck-policy.json'), textAt(repo, 'HEAD', 'crosscheck-policy.json')]);
  if (tagged === null || head === null) return null;
  const [a, b] = [JSON.parse(tagged), JSON.parse(head)];
  const fields = [...new Set([...Object.keys(a), ...Object.keys(b)])].filter(k => !POLICY_FREE.includes(k) && JSON.stringify(a[k]) !== JSON.stringify(b[k]));
  return fields.length > 0 ? `crosscheck-policy.json differs from the tag in ${fields.join(', ')}: only t2, t3 and their dated reason may change (S4's T3 commit)` : null;
}

async function amendmentsProblem(repo, commit) {
  const [tagged, head] = await Promise.all([textAt(repo, commit, 'amendments.md'), textAt(repo, 'HEAD', 'amendments.md')]);
  if (tagged === null) return 'amendments.md is missing at the tagged commit';
  return head !== null && head.startsWith(tagged) ? null : 'amendments.md at HEAD is not the tagged text with lines appended (a registered amendment file is append-only)';
}

async function seedProblem(repo, commit, requireSeed) {
  if (await blobAt(repo, commit, SEED_FILE) !== null) return 'seed.txt exists at the tagged commit: the seed must be revealed AFTER the tag';
  const [head, hash] = await Promise.all([textAt(repo, 'HEAD', SEED_FILE), textAt(repo, 'HEAD', 'seed.sha256')]);
  if (head === null) return requireSeed ? 'seed.txt is required for a data pair and is absent at HEAD: run seed-reveal after the A/A' : null;
  return hash !== null && sha256(head.trim()) === hash.trim() ? null : 'seed.txt does not hash to seed.sha256';
}

async function blobProblems(repo, commit, requireSeed) {
  const special = new Set(['amendments.md', 'crosscheck-policy.json']);
  const plain = await Promise.all(PREREG_FILES.filter(f => !special.has(f)).map(async f => {
    const [tagged, head] = await Promise.all([blobAt(repo, commit, f), blobAt(repo, 'HEAD', f)]);
    if (tagged === null) return `${f} is missing at the tagged commit`;
    return head === tagged ? null : `${f} ${head === null ? 'is deleted at HEAD' : 'differs from the tagged commit'}`;
  }));
  return [...plain, await amendmentsProblem(repo, commit), await policyProblem(repo, commit), await seedProblem(repo, commit, requireSeed)].filter(Boolean);
}

async function paidProblems(repo, runMap) {
  const status = await git(repo, ['status', '--porcelain', '--untracked-files=all', '--', 'spikes/']);
  const problems = !status.ok ? [status.reason] : status.value.trim() === '' ? [] : [`the tree under spikes/ is not clean (${status.value.trim().split('\n').slice(0, 5).join('; ')})`];
  const identity = await harnessIdentity(repo);
  if (!identity.ok) return [...problems, identity.reason];
  const accepted = identity.value === runMap.harnessIdentity || (runMap.harnessAccepted ?? []).includes(identity.value);
  return accepted ? problems : [...problems, `harnessIdentity ${identity.value.slice(0, 12)} is not the run-map's ${String(runMap.harnessIdentity).slice(0, 12)} and no amendment accepted it (--accept-harness)`];
}

/** -> `ok(null)` | `fail(reason, { problems })`, every problem named (a file or a field). `paid` adds the clean-tree and harness checks. */
export async function preregGuard({ repo, runMap, paid, requireSeed = false }) {
  const peeled = await remotePeel(repo, runMap.preregTag);
  if (!peeled.ok) return fail(peeled.reason, { problems: [peeled.reason] });
  if (peeled.value !== runMap.preregCommit) {
    const why = `the tag ${runMap.preregTag} on origin peels to ${peeled.value.slice(0, 12)}, the run-map pinned ${String(runMap.preregCommit).slice(0, 12)}`;
    return fail(why, { problems: [why] });
  }
  const local = await git(repo, ['cat-file', '-e', `${runMap.preregCommit}^{commit}`]);
  if (!local.ok) return fail(`the tagged commit ${runMap.preregCommit.slice(0, 12)} is not in this clone: fetch the tag`, { problems: ['tagged commit not fetched'] });
  const problems = [...await blobProblems(repo, runMap.preregCommit, requireSeed), ...(paid ? await paidProblems(repo, runMap) : [])];
  return problems.length === 0 ? ok(null) : fail(`the pre-registration guard refuses: ${problems.join('; ')}`, { problems });
}
