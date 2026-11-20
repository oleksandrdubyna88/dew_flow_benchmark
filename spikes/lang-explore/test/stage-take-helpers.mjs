// The helpers of the take's tests (F3.S1 (b), PR 5, coai code round 2 and the Opus review of the take): a child process that is killed at a step or runs `stage-sync` with a `git` of the test's own first on its PATH, the files of
// a take in the git folder, and the end state of a finished take. Not a test.
import assert from 'node:assert/strict';
import { execFileSync, spawnSync } from 'node:child_process';
import { chmodSync, existsSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { harnessIdentity, preregGuard } from '../../minigit-bench/lib/prereg-guard.mjs';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { STAGE_PREREG_CONFIG } from '../lib/stage-prereg.mjs';
import { FIX_FILE, NEW_FILE, OLD_FILE } from './stage-accept-fixture.mjs';
import { git } from './stage-prereg-fixture.mjs';

const CHILD = fileURLToPath(new URL('./stage-take-child.mjs', import.meta.url));
export const FIXED_TEXT = '// the fix, reviewed on main\n';

/** `stage-take-child.mjs` on `f`: `{ killed, result }`, `killed` when the child died of SIGKILL at `point`, `result` the answer it printed (null when it printed none). */
export function inChild(mode, f, { hex = '', point = '', env = {} } = {}) {
  const r = spawnSync(process.execPath, [CHILD, mode, f.repo, hex, point], { encoding: 'utf8', env: { ...process.env, ...env }, timeout: 120_000 });
  return { killed: r.signal === 'SIGKILL', result: r.stdout.trim() === '' ? null : JSON.parse(r.stdout), stderr: r.stderr };
}

/** The environment that puts a `git` in front that fails every call whose arguments hold `pattern` (`exit 128`, "simulated lookup failure") and runs the real one for all others. */
export function failingGit(pattern) {
  const dir = tmpDir('git-shim');
  const script = path.join(dir, 'git');
  writeFileSync(script, '#!/bin/sh\ncase "$*" in *"$STAGE_SHIM_FAIL"*) echo "fatal: simulated lookup failure" >&2; exit 128;; esac\nexec "$STAGE_SHIM_REAL" "$@"\n');
  chmodSync(script, 0o755);
  return { PATH: `${dir}:${process.env.PATH}`, STAGE_SHIM_REAL: execFileSync('sh', ['-c', 'command -v git'], { encoding: 'utf8' }).trim(), STAGE_SHIM_FAIL: pattern };
}

export const gitFile = (f, name) => path.join(f.repo, '.git', name);
export const leftovers = f => ['stage-take.journal', 'stage-take.index'].filter(name => existsSync(gitFile(f, name)));
export const journalOf = f => existsSync(gitFile(f, 'stage-take.journal'));
export const paidGuard = f => preregGuard({ repo: f.repo, runMap: f.runMap, paid: true, requireSeed: false, config: STAGE_PREREG_CONFIG });

/** What a finished take leaves: one commit on the branch, a clean tree and index, the fix's three files in the tree, the harness identity of the fix and no journal or index file of the take. */
export async function assertTaken({ f, head, identity }) {
  assert.equal(git(f.repo, 'rev-list', '--count', `${head}..HEAD`), '1', 'one commit on the series\' branch');
  assert.equal(git(f.repo, 'status', '--porcelain', '--untracked-files=all'), '', 'a clean tree');
  assert.equal(git(f.repo, 'diff', '--cached', '--name-only'), '', 'nothing staged');
  assert.equal(readFileSync(path.join(f.repo, FIX_FILE), 'utf8'), FIXED_TEXT, 'the edited file is the fix\'s');
  assert.deepEqual([existsSync(path.join(f.repo, OLD_FILE)), existsSync(path.join(f.repo, NEW_FILE))], [false, true], 'the deleted file is gone and the added one is there');
  assert.equal((await harnessIdentity(f.repo, STAGE_PREREG_CONFIG)).value, identity, 'the branch holds exactly the harness of the fix');
  assert.deepEqual(leftovers(f), [], 'no journal and no temporary index');
}

