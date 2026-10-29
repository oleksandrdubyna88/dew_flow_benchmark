// Shared test plumbing: where the spike lives, the fetched (hash-verified) inputs, and the sandbox gate.
import { existsSync, mkdirSync, mkdtempSync, rmSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { fetchInputs } from '../lib/fetch-inputs.mjs';
import { findOnPath } from '../lib/find-on-path.mjs';
import { defaultKeyPath, readKey } from '../lib/heldout-crypto.mjs';
import { PYTHON } from '../lib/heldout-run.mjs';

export const spikeDir = fileURLToPath(new URL('..', import.meta.url));
export const stubTrees = path.join(spikeDir, 'test', 'fixtures', 'stub-trees');

let fetched;
/**
 * The fork's `inputs` branch at the pinned sha, verified against hashes.txt. Memoised per test file. BENCH_INPUTS_REPO
 * points the clone at a local checkout of the fork (used before the fork's commit is pushed); it never changes the sha.
 */
export function inputs() {
  fetched ??= fetchInputs({ repoUrl: process.env.BENCH_INPUTS_REPO || undefined }).then(r => {
    if (!r.ok) throw new Error(`fetchInputs failed: ${r.reason}`);
    return r.value;
  });
  return fetched;
}

// common.test-leftovers: every directory a test creates is named `testsToDelete-...` and the process removes its own at exit.
const created = [];
process.once('exit', () => {
  for (const dir of created) {
    try {
      rmSync(dir, { recursive: true, force: true });
    } catch (e) {
      process.stderr.write(`could not remove test directory ${dir}: ${e.message}\n`);
    }
  }
});
const track = dir => { created.push(dir); return dir; };

/** A fresh temp directory the caller owns (removed when the test process exits). */
export const tmpDir = prefix => track(mkdtempSync(path.join(os.tmpdir(), `testsToDelete-mgb-${prefix}-`)));

/**
 * A test that needs bwrap calls this first. Without bwrap it skips with its reason, unless BENCH_REQUIRE_SANDBOX=1,
 * which turns the skip into a failure so a host without bwrap cannot report the epic done. Returns whether to go on.
 */
export function sandboxGate(t) {
  if (findOnPath('bwrap') !== null) return true;
  const why = 'bwrap is not on PATH: the scoring box cannot run here';
  if (process.env.BENCH_REQUIRE_SANDBOX === '1') throw new Error(`BENCH_REQUIRE_SANDBOX=1 and ${why}`);
  t.skip(why);
  return false;
}

/** A test that needs python3 calls this first; it skips with its reason, or fails under BENCH_REQUIRE_SANDBOX=1 (like sandboxGate). */
export function pythonGate(t) {
  if (existsSync(PYTHON)) return true;
  const why = `${PYTHON} is missing: the held-out runner cannot run here`;
  if (process.env.BENCH_REQUIRE_SANDBOX === '1') throw new Error(`BENCH_REQUIRE_SANDBOX=1 and ${why}`);
  t.skip(why);
  return false;
}

/** A fresh directory UNDER THE HOME (the clear suite is decrypted only there); removed when the test process exits. */
export const homeDir = prefix => {
  const base = path.join(os.homedir(), '.cache', 'minigit-bench', 'test-work');
  mkdirSync(base, { recursive: true, mode: 0o700 });
  return track(mkdtempSync(path.join(base, `testsToDelete-${prefix}-`)));
};

/**
 * A test that needs the held-out key calls this first. With no key it skips with its reason, unless BENCH_REQUIRE_SANDBOX=1
 * (the epic's completion run), which turns the skip into a failure. Returns whether to go on.
 */
export function keyGate(t) {
  const key = readKey(defaultKeyPath());
  if (key.ok) return true;
  const why = `the held-out key is unusable here: ${key.reason}`;
  if (process.env.BENCH_REQUIRE_SANDBOX === '1') throw new Error(`BENCH_REQUIRE_SANDBOX=1 and ${why}`);
  t.skip(why);
  return false;
}

/**
 * A sibling checkout of this repository's family (e.g. `dew_flow_connect_other_ais`), or null: looked for beside the repository root,
 * and beside the main checkout when this is a git worktree (`commonRoot`). Never a path written down for one machine.
 */
export function siblingCheckout(name, { repoRoot = path.resolve(spikeDir, '..', '..'), commonRoot = mainCheckoutOf(repoRoot) } = {}) {
  const candidates = [repoRoot, commonRoot].filter(Boolean).map(root => path.resolve(root, '..', name));
  return candidates.find(c => existsSync(c)) ?? null;
}

/** The main checkout of a git worktree (the parent of the common git dir), or null when the root is not a linked worktree. */
function mainCheckoutOf(repoRoot) {
  const r = spawnSync('git', ['-C', repoRoot, 'rev-parse', '--path-format=absolute', '--git-common-dir'], { encoding: 'utf8', timeout: 20_000 });
  return r.status === 0 && path.basename(r.stdout.trim()) === '.git' ? path.dirname(r.stdout.trim()) : null;
}
