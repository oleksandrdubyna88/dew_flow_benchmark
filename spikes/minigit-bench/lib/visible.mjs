// The visible test scripts' output, and the per-invocation timeout shim that stands in for `minigit` while they run.
import { createHash } from 'node:crypto';
import { chmodSync, existsSync, readFileSync, renameSync, statSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fail, ok } from './result.mjs';

export const REAL_BINARY = '.minigit-scored-real';
const SUMMARY = /^PASSED: (\d+)\nFAILED: (\d+)\nTOTAL: +(\d+)$/gm;

const allFailed = (expected, extra) => ({ summaryCaptured: false, passed: 0, failed: expected, total: expected, buildFailed: false, failLines: [], ...extra });

/**
 * Reads a visible script's stdout. The summary is the LAST `PASSED/FAILED/TOTAL` block (a binary may print look-alikes earlier) and
 * it must add up and equal `expectedTotal`. No usable summary is scored as every test failed, never as "0 of 0". A `BUILD FAILED`
 * line is every test failed too.
 */
export function parseVisibleOutput(stdout, expectedTotal) {
  const failLines = stdout.split('\n').filter(l => l.startsWith('FAIL: '));
  if (/^BUILD FAILED$/m.test(stdout)) return allFailed(expectedTotal, { buildFailed: true, failLines });
  const blocks = [...stdout.matchAll(SUMMARY)];
  const last = blocks.at(-1);
  if (!last) return allFailed(expectedTotal, { failLines });
  const [passed, failed, total] = last.slice(1).map(Number);
  if (passed + failed !== total || total !== expectedTotal) return allFailed(expectedTotal, { failLines });
  return { summaryCaptured: true, passed, failed, total, buildFailed: false, failLines };
}

const shimText = seconds => `#!/bin/sh\nexec timeout -k 2 ${seconds} "$(dirname "$0")/${REAL_BINARY}" "$@"\n`;
const sha256 = text => createHash('sha256').update(text).digest('hex');

/** Renames `minigit` aside and writes the timeout shim in its place (mode 0755). Returns the shim's sha256. */
export function installShim(copyDir, seconds = 10) {
  const binary = path.join(copyDir, 'minigit');
  if (!existsSync(binary)) return fail(`no minigit in ${copyDir}: nothing to put a timeout shim in front of`);
  renameSync(binary, path.join(copyDir, REAL_BINARY));
  const text = shimText(seconds);
  writeFileSync(binary, text, { mode: 0o755 });
  chmodSync(binary, 0o755);
  return ok({ sha256: sha256(text) });
}

/** After the run: is `minigit` still the shim? The scripts re-run the build, and a build step can overwrite it. */
export function shimIntact(copyDir, shimSha256) {
  const binary = path.join(copyDir, 'minigit');
  return existsSync(binary) && statSync(binary).isFile() && sha256(readFileSync(binary, 'utf8')) === shimSha256;
}
