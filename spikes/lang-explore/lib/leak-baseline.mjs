// The pre-material baseline of the leak scan (a collision rule for EVERY tier): text that was already in the repository before a task's private material was created cannot be a leak of it. Each task of the pins names a
// `baselineCommit`, a main commit made before the task's held-out folder was born. A finding in a FILE is exempt when every one of its haystack lines also occurs, byte for byte, in the same path at that commit (read through
// git, never the working copy) and the working file holds that line no more often than the baseline did, so a copy at another path, a changed line and an added duplicate are all new text and stay hits. Findings in the git
// history texts and findings without a line (shingles) are never exempt. A baseline that cannot be read gives no exemption: the stricter check. The count per task is reported, never the text.
import path from 'node:path';
import { runBounded } from '../../minigit-bench/lib/run.mjs';
import { readLines, splitLines } from './leak-text.mjs';

const BLOB_MAX_BYTES = 512 * 1024 * 1024;
const COMMIT = /^[0-9a-f]{7,40}$/;

/** A latin1-decoded line as the UTF-8 text its bytes spell (git prints a blob as UTF-8; a finding's lines come from the latin1 reading of the haystack). */
const utf8 = raw => Buffer.from(raw, 'latin1').toString('utf8');

/** How often each of the `wanted` lines occurs in `lines`: `Map(line -> count)` over the wanted lines ONLY (never a map of every line of a big file). */
export function countWanted(lines, wanted) {
  const counts = new Map();
  for (const line of lines) if (wanted.has(line)) counts.set(line, (counts.get(line) ?? 0) + 1);
  return counts;
}

/** The wanted-line counts of `file` at `commit`, or null when the commit is not a commit id, the file is not there or it is too big to read. */
async function baselineCounts({ repoRoot, commit, file, wanted }) {
  if (typeof commit !== 'string' || !COMMIT.test(commit)) return null;
  const r = await runBounded('git', ['show', `${commit}:${file}`], { cwd: repoRoot, timeoutMs: 60_000, maxBytes: BLOB_MAX_BYTES });
  return readable(r) ? countWanted(splitLines(r.value.stdout), wanted) : null;
}

const readable = r => r.ok && r.value.code === 0 && !r.value.truncated;

const git = (repoRoot, args) => runBounded('git', args, { cwd: repoRoot, timeoutMs: 60_000 });
const exitsZero = async (repoRoot, args) => { const r = await git(repoRoot, args); return r.ok && r.value.code === 0; };

const asDate = text => { const d = new Date(text); return Number.isFinite(d.getTime()) ? d : null; };

/** The committer time of a commit, as a Date, or null when git cannot say. */
async function committedAt(repoRoot, commit) {
  const r = await git(repoRoot, ['show', '-s', '--format=%cI', commit]);
  return r.ok && r.value.code === 0 ? asDate(r.value.stdout.trim()) : null;
}

const idProblem = ({ commit }) => (typeof commit === 'string' && COMMIT.test(commit) ? null : `baselineCommit ${commit} is not a commit id`);
const existsProblem = async (repoRoot, { commit }) => (await exitsZero(repoRoot, ['cat-file', '-e', `${commit}^{commit}`]) ? null : `baselineCommit ${commit} does not exist in this repository`);
const ancestorProblem = async (repoRoot, { commit }) => (await exitsZero(repoRoot, ['merge-base', '--is-ancestor', commit, 'HEAD']) ? null : `baselineCommit ${commit} is not an ancestor of HEAD`);

/** `born` as a UTC time (an ISO string ending in Z), or null. */
const utcTime = born => (typeof born === 'string' && /Z$/.test(born) ? asDate(born) : null);

async function ageProblem(repoRoot, { commit, born }) {
  const bornAt = utcTime(born);
  if (bornAt === null) return `heldoutBorn ${born} is not a UTC time`;
  const made = await committedAt(repoRoot, commit);
  return made !== null && made < bornAt ? null : `baselineCommit ${commit} committed at ${made?.toISOString() ?? 'an unknown time'} is not before the held-out folder was born (${born})`;
}

/** The sentence refusing a baseline, or null: it must be a commit id, exist, be an ancestor of HEAD and be older than the held-out folder (`born`, a UTC time). The first failing check speaks. */
async function baselineProblem(repoRoot, entry) {
  const steps = [() => idProblem(entry), () => existsProblem(repoRoot, entry), () => ancestorProblem(repoRoot, entry), () => ageProblem(repoRoot, entry)];
  for (const step of steps) {
    const problem = await step();
    if (problem) return problem;
  }
  return null;
}

/** `{ valid: { task: commit }, problems: { task: sentence } }`: the baselines that may exempt, and why the others may not (a task without a baseline has neither). */
export async function checkBaselines({ repoRoot, baselines }) {
  const valid = {};
  const problems = {};
  for (const [task, entry] of Object.entries(baselines)) {
    if (!entry?.commit) continue;
    const problem = await baselineProblem(repoRoot, entry);
    if (problem) problems[task] = problem; else valid[task] = entry.commit;
  }
  return { valid, problems };
}

const isFileFinding = f => !f.file.startsWith('git log') && f.raws.length > 0;

/** True when every line of the finding is in the baseline and the working file holds it no more often. `base` and `current` are the wanted-line counts. */
const exempt = (f, base, current) => base !== null && f.raws.every(raw => { const line = utf8(raw); return base.has(line) && (current.get(line) ?? 0) <= base.get(line); });

/** The lines each path's findings need counted: `Map(file -> Set(line))`, in the UTF-8 form git prints. Only the finding's own lines. */
function wantedByFile(eligible) {
  const map = new Map();
  for (const f of eligible) {
    const set = map.get(f.file) ?? new Set();
    for (const raw of f.raws) set.add(utf8(raw));
    map.set(f.file, set);
  }
  return map;
}

/** The distinct `{ commit, file }` pairs whose baseline blobs are needed. */
const pairsOf = (eligible, baselines) => [...new Map(eligible.map(f => [`${baselines[f.task]}\0${f.file}`, { commit: baselines[f.task], file: f.file }])).values()];

/**
 * `{ kept, exempt: { task: count }, problems }`: the findings minus the collisions with a VERIFIED baseline, how many of those there were per task, and why a task's baseline was refused. Each path's working file is read
 * ONCE (as UTF-8, the way git's blob is read; `readWorking` is a seam for the tests) however many tasks have findings in it, and only the findings' own lines are counted.
 */
export async function applyBaseline({ repoRoot, findings, baselines: declared, readWorking = file => readLines(path.join(repoRoot, file), 'utf8') }) {
  const { valid: baselines, problems } = await checkBaselines({ repoRoot, baselines: declared });
  const eligible = findings.filter(f => isFileFinding(f) && baselines[f.task]);
  const wanted = wantedByFile(eligible);
  const blobs = new Map(await Promise.all(pairsOf(eligible, baselines).map(async p => [`${p.commit}\0${p.file}`, await baselineCounts({ repoRoot, ...p, wanted: wanted.get(p.file) })])));
  const current = new Map([...wanted].map(([file, lines]) => [file, countWanted(readWorking(file), lines)]));
  const gone = new Set(eligible.filter(f => exempt(f, blobs.get(`${baselines[f.task]}\0${f.file}`), current.get(f.file))));
  const perTask = {};
  for (const f of gone) perTask[f.task] = (perTask[f.task] ?? 0) + 1;
  return { kept: findings.filter(f => !gone.has(f)), exempt: perTask, problems };
}
