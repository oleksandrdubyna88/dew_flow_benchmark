// A temporary repository in the shape of Stage 1's pre-registration (F3.S1 (b), PR 5): every registered file of `STAGE_PREREG_FILES` (the folder entries with a file or two), the harness folders and the top-level files of the three spikes, one
// annotated tag on the first commit, a bare origin that holds `main` and the tag, and the series' own branch cut at the tag. Nothing here is a real input of the benchmark.
import { createHash } from 'node:crypto';
import path from 'node:path';
import { commit, git, write } from '../../minigit-bench/test/prereg-fixtures.mjs';
import { harnessIdentity } from '../../minigit-bench/lib/prereg-guard.mjs';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { PREREG_DIR, STAGE_HARNESS_DIRS, STAGE_HARNESS_TOP_DIRS, STAGE_PREREG_FILES } from '../lib/stage-lists.mjs';
import { STAGE_PREREG_CONFIG } from '../lib/stage-prereg.mjs';

export const TAG = 'prereg/lang-v2-20261120';
export const SEED = 'lv2-0123456789abcdef';
export const sha256 = text => createHash('sha256').update(text).digest('hex');
export const POLICY = Object.freeze({ t1: 'stop', t2: 'otel-incomplete', t3: 'stop', t5: 'investigate', t3OutputSource: 'transcript', join: 'request_id', decidedOn: '2026-11-19', reason: 'registered' });
export const EXPLORE = Object.freeze({ cli: { version: '2.1.330' }, caps: { capUsd: 50, wallMs: 4_500_000, stallMs: 2_700_000 }, locale: 'de_DE.UTF-8' });
export const AMENDMENTS = '# Dated amendments\n';
export const PRICE_FILE = `${PREREG_DIR}/prices-2026-11-19.json`;
export const EXPLORE_FILE = 'spikes/lang-explore/inputs/explore.json';
export const QUIET_FILE = 'spikes/minigit-bench/inputs/quiet.json';
export const POLICY_FILE = `${PREREG_DIR}/crosscheck-policy.json`;
export const AMENDMENTS_FILE = `${PREREG_DIR}/amendments.md`;
export const SEED_FILE = `${PREREG_DIR}/seed.txt`;
const SPIKES = ['minigit-bench', 'lang-explore', 'lang-bench'];

const content = file => ({
  [`${PREREG_DIR}/seed.sha256`]: `${sha256(SEED)}\n`, [POLICY_FILE]: `${JSON.stringify(POLICY)}\n`, [AMENDMENTS_FILE]: AMENDMENTS, [EXPLORE_FILE]: `${JSON.stringify(EXPLORE)}\n`,
})[file] ?? `registered ${file}\n`;

const SAMPLES = Object.freeze({ [PREREG_DIR]: ['prices-2026-11-19.json'], 'spikes/lang-bench/inputs/specs.d': ['runner.json', 'format.json'] });
/** The files of a plain entry, and a file or two for each folder entry. */
const plainFiles = () => STAGE_PREREG_FILES.flatMap(entry => (typeof entry === 'string' ? [entry] : SAMPLES[entry.dir].map(name => `${entry.dir}/${name}`)));

/** The repository: `{ repo, remote, sha, runMap }`. The working tree is on the series' branch `series`, cut at the tag; `main` and the tag are on the bare origin. */
export async function stageFixture({ extra = () => {} } = {}) {
  const remote = tmpDir('stage-remote');
  git(remote, 'init', '-q', '--bare');
  const repo = tmpDir('stage-repo');
  git(repo, 'init', '-q', '-b', 'main');
  git(repo, 'remote', 'add', 'origin', remote);
  git(repo, 'config', 'user.name', 'tester');
  git(repo, 'config', 'user.email', 'tester@example.invalid');
  for (const file of plainFiles()) write(repo, file, content(file));
  for (const dir of STAGE_HARNESS_DIRS) write(repo, `${dir}/x.mjs`, `// ${dir}\n`);
  for (const dir of STAGE_HARNESS_TOP_DIRS) write(repo, `${dir}top.mjs`, `// top of ${dir}\n`);
  write(repo, 'spikes/lang-explore/test/t.mjs', '// a test, outside the harness\n');
  write(repo, 'spikes/lang-explore/inputs/probes.json', '{}\n');
  write(repo, 'docs/notes.md', 'notes\n');
  await extra(repo);
  const sha = commit(repo, 'the pre-registration');
  git(repo, 'tag', '-a', TAG, '-m', 'prereg', sha);
  git(repo, 'push', '-q', 'origin', 'main', TAG);
  git(repo, 'checkout', '-q', '-b', 'series');
  const identity = (await harnessIdentity(repo, STAGE_PREREG_CONFIG)).value;
  return { repo, remote, sha, runMap: { preregTag: TAG, preregCommit: sha, harnessIdentity: identity, harnessAccepted: [] } };
}

/** `change()` made on `main` (a commit pushed to the origin) while the working tree is on the series' branch, which it comes back to. Returns the commit. */
export function onMain(f, change, message = 'a commit of main') {
  git(f.repo, 'checkout', '-q', 'main');
  change();
  const sha = commit(f.repo, message);
  git(f.repo, 'push', '-q', 'origin', 'main');
  git(f.repo, 'checkout', '-q', 'series');
  return sha;
}

/** `change(clone)` made in ANOTHER clone of the origin and pushed to `main`: this repository's `origin/main` does not know it until a fetch. */
export function onOtherClone(f, change, message = 'a commit of main from another clone') {
  const other = tmpDir('stage-other');
  git(other, 'clone', '-q', '-b', 'main', f.remote, '.');
  git(other, 'config', 'user.name', 'other');
  git(other, 'config', 'user.email', 'other@example.invalid');
  change(other);
  commit(other, message);
  git(other, 'push', '-q', 'origin', 'HEAD:main');
}

/** A commit on the series' branch. */
export const onSeries = (f, change, message = 'a commit of the series') => { change(); return commit(f.repo, message); };
export const headBlob = (repo, file) => git(repo, 'rev-parse', `HEAD:${file}`);
export const pathsOf = (f, rev) => git(f.repo, 'diff', '--name-only', f.sha, rev).split('\n').filter(Boolean);
export { git, write, path };
