// Shared test plumbing: where the spike lives, the fetched (hash-verified) inputs, and the sandbox gate.
import { existsSync, mkdtempSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { fetchInputs } from '../lib/fetch-inputs.mjs';

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

/** A fresh temp directory the caller owns. */
export const tmpDir = prefix => mkdtempSync(path.join(os.tmpdir(), `mgb-${prefix}-`));

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
