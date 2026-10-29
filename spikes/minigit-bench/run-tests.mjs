#!/usr/bin/env node
// The one way to run this spike's tests (common.local-cpu-budget): lowered priority, at most 8 workers.
//
//   node spikes/minigit-bench/run-tests.mjs [glob...]        default glob: test/**/*.test.mjs
//   BENCH_REQUIRE_SANDBOX=1 node spikes/minigit-bench/run-tests.mjs   a sandbox-needing skip becomes a failure
//
// The cap is min(8, CPUs - 1), never a bare 8: --test-concurrency is exact and a bare 8 would RAISE a small machine.
// Written locally on purpose: the mount's runner is not imported (a spike must not depend on its checkout layout).
import { spawnSync } from 'node:child_process';
import { availableParallelism, constants } from 'node:os';
import { fileURLToPath } from 'node:url';

await import('./lib/lower-priority.mjs');

const CAP = 8;
const DEFAULT_GLOB = 'test/**/*.test.mjs';

/** `node --test`'s worker count for a machine with `threads` hardware threads. */
const testConcurrency = (threads = availableParallelism()) => Math.max(1, Math.min(CAP, threads - 1));

/** The argv of the one `node --test` this runner starts. */
const testArgs = (globs, threads = availableParallelism()) =>
  ['--test', `--test-concurrency=${testConcurrency(threads)}`, ...(globs.length > 0 ? globs : [DEFAULT_GLOB])];

/** The child's own exit code, or 128 + the signal's number, never a plain 1 a caller could not tell from a failing test. */
function exitCodeFor(status, signal) {
  if (status !== null && status !== undefined) return status;
  if (signal) return 128 + (constants.signals[signal] ?? 0);
  return 1;
}

const spikeDir = fileURLToPath(new URL('.', import.meta.url));
const result = spawnSync(process.execPath, testArgs(process.argv.slice(2)), { cwd: spikeDir, stdio: 'inherit' });
if (result.error) {
  process.stderr.write(`run-tests: ${result.error.message}\n`);
  process.exit(1);
}
process.exit(exitCodeFor(result.status, result.signal));
