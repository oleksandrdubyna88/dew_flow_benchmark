// The one reader of a secret kept in a file: a regular file of mode 600 in a directory of mode 700 under the operator's home, never the
// environment (an environment variable has no mode to check, so it would let a run skip the very check that keeps the secret private).
// Extracted from capture/capture-env.mjs; the capture, the credential broker and (E2.S4) the PAT reader all go through it.
// A value is never in a reason: reasons name the file and the mode only.
import { existsSync, lstatSync, readFileSync, realpathSync, statSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fail, ok } from './result.mjs';

export const PAT_FILE_PARTS = Object.freeze(['.config', 'minigit-bench', 'github-pat']);

// A test run must never reach the operator's real secret files (E3b review N18: runner tests that called `main` without a seam could, and one RED check did). So `readSecretFile`
// refuses, by THROWING, when it runs under the test runner (`node --test` sets NODE_TEST_CONTEXT; the tests' fake-home helper sets BENCH_UNDER_TEST) and `home` is the real
// account home. The test is on the home, before any file is looked at, so the refusal itself touches no secret. A test whose whole subject is the real token opts in by name.
let optedIn = false;
const underTestRunner = () => process.env.NODE_TEST_CONTEXT !== undefined || process.env.BENCH_UNDER_TEST === '1';
const canonical = dir => { try { return realpathSync(dir); } catch { return path.resolve(dir); } };

/** A test that must read the operator's real secret (smoke-real: the committed fixture holds no real token) says so, with its reason. Per process; a no-op's reason is still required. */
export function allowRealSecretUnderTest(reason) {
  if (typeof reason !== 'string' || reason.trim() === '') throw new Error('allowRealSecretUnderTest needs a reason: say why this test reads the operator\'s real secret file');
  optedIn = true;
}

function refuseRealHomeUnderTest(home, label) {
  if (optedIn || !underTestRunner()) return;
  let real;
  try { real = os.userInfo().homedir; } catch { return; }
  if (canonical(home) === canonical(real)) {
    throw new Error(`the ${label} reader was called under the test runner with the operator's real home: a test must run with a fake HOME (test/fake-home.mjs) and pass that home; a test whose subject is the real secret calls allowRealSecretUnderTest(reason)`);
  }
}

/** The file or why it cannot be used: `{ file } | { why }`. */
function secretFileOf({ home, parts, label }) {
  const file = path.join(home, ...parts);
  if (!existsSync(file)) return { why: `no ${label}: put one in ${file} with mode 600, in a directory of mode 700` };
  const dirMode = statSync(path.dirname(file)).mode & 0o777;
  if ((dirMode & 0o077) !== 0) return { why: `the ${label} directory ${path.dirname(file)} has mode ${dirMode.toString(8)}: it must be 700 (not accessible to group or others)` };
  const stat = lstatSync(file);
  if (!stat.isFile()) return { why: `the ${label} file ${file} is not a regular file (a link is refused): its mode could not be trusted` };
  const mode = stat.mode & 0o777;
  if ((mode & 0o077) !== 0) return { why: `the ${label} file ${file} has mode ${mode.toString(8)}: it must be 600 (not readable by group or others)` };
  return { file };
}

/** The secret, from the mode-600 file only. `{ ok: true, value, source: 'file' } | { ok: false, reason }`. */
export function readSecretFile({ home, parts, label = 'secret' }) {
  refuseRealHomeUnderTest(home, label);
  const found = secretFileOf({ home, parts, label });
  if (!found.file) return fail(found.why);
  const text = readFileSync(found.file, 'utf8').trim();
  if (text === '') return fail(`the ${label} file ${found.file} is empty`);
  return { ...ok(text), source: 'file' };
}

/** Is a secret available? Looks at the file's existence, type and mode and the directory's mode only: the value is not read. */
export function secretStatus({ home, parts, label = 'secret' }) {
  const found = secretFileOf({ home, parts, label });
  return found.file ? { present: true, source: 'file' } : { present: false, why: found.why };
}
