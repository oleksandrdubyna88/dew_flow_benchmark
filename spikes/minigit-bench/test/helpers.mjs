// Shared test plumbing: where the spike lives, the fetched (hash-verified) inputs, and the sandbox gate.
import { existsSync, mkdirSync, mkdtempSync, rmSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { fetchInputs } from '../lib/fetch-inputs.mjs';
import { defaultKeyPath, readKey } from '../lib/heldout-crypto.mjs';

export const spikeDir = fileURLToPath(new URL('..', import.meta.url));
export const stubTrees = path.join(spikeDir, 'test', 'fixtures', 'stub-trees');
export const BWRAP = '/usr/bin/bwrap';

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
  if (existsSync(BWRAP)) return true;
  const why = `${BWRAP} is missing: the scoring box cannot run here`;
  if (process.env.BENCH_REQUIRE_SANDBOX === '1') throw new Error(`BENCH_REQUIRE_SANDBOX=1 and ${why}`);
  t.skip(why);
  return false;
}

export const PYTHON = '/usr/bin/python3';

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
