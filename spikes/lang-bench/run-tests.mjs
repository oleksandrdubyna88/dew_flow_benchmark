#!/usr/bin/env node
// The one way to run this spike's tests (common.local-cpu-budget): lowered priority, at most 8 workers.
//
//   node spikes/lang-bench/run-tests.mjs [glob...]        default glob: test/**/*.test.mjs
//   BENCH_REQUIRE_SANDBOX=1 node spikes/lang-bench/run-tests.mjs   a toolchain-needing skip becomes a failure
//
// Python units (test/py/test_*.py) are not started here: a Node test file starts them under `python3 -I -m unittest`, so the
// lowered priority of this process reaches them and each Node test file names the Python it needs and skips with its reason.
// A glob that matches no file ends the run with exit 2 (a runner that passes over a missing test proves nothing).
// The cap is min(8, CPUs - 1), never a bare 8: --test-concurrency is exact and a bare 8 would RAISE a small machine.
// Written locally on purpose, in v1's shape: the mount's runner is not imported (a spike must not depend on its checkout layout).
import { spawnSync } from 'node:child_process';
import { globSync } from 'node:fs';
import { availableParallelism, constants } from 'node:os';
import { fileURLToPath } from 'node:url';

await import('./lib/lower-priority.mjs');

const CAP = 8;
const DEFAULT_GLOB = 'test/**/*.test.mjs';
const NO_MATCH_EXIT = 2;

/** `node --test`'s worker count for a machine with `threads` hardware threads. */
const testConcurrency = (threads = availableParallelism()) => Math.max(1, Math.min(CAP, threads - 1));

/** The argv of the one `node --test` this runner starts, over the files the globs resolved to. */
const testArgs = (files, threads = availableParallelism()) => ['--test', `--test-concurrency=${testConcurrency(threads)}`, ...files];

/** The child's own exit code, or 128 + the signal's number, never a plain 1 a caller could not tell from a failing test. */
function exitCodeFor(status, signal) {
  if (status !== null && status !== undefined) return status;
  if (signal) return 128 + (constants.signals[signal] ?? 0);
  return 1;
}

/** Each glob resolved against `cwd`; `unmatched` lists the ones that found nothing. */
function resolveGlobs(globs, cwd) {
  const found = globs.map(glob => ({ glob, files: globSync(glob, { cwd }).sort() }));
  return { files: [...new Set(found.flatMap(entry => entry.files))], unmatched: found.filter(entry => entry.files.length === 0).map(entry => entry.glob) };
}

const spikeDir = fileURLToPath(new URL('.', import.meta.url));
const requested = process.argv.slice(2);
const { files, unmatched } = resolveGlobs(requested.length > 0 ? requested : [DEFAULT_GLOB], spikeDir);
if (unmatched.length > 0) {
  process.stderr.write(`run-tests: no test file matches ${unmatched.join(', ')}\n`);
  process.exit(NO_MATCH_EXIT);
}
const result = spawnSync(process.execPath, testArgs(files), { cwd: spikeDir, stdio: 'inherit' });
if (result.error) {
  process.stderr.write(`run-tests: ${result.error.message}\n`);
  process.exit(1);
}
process.exit(exitCodeFor(result.status, result.signal));
