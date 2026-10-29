// The one reader of a secret kept in a file: a regular file of mode 600 in a directory of mode 700 under the operator's home, never the
// environment (an environment variable has no mode to check, so it would let a run skip the very check that keeps the secret private).
// Extracted from capture/capture-env.mjs; the capture, the credential broker and (E2.S4) the PAT reader all go through it.
// A value is never in a reason: reasons name the file and the mode only.
import { existsSync, lstatSync, readFileSync, statSync } from 'node:fs';
import path from 'node:path';
import { fail, ok } from './result.mjs';

export const PAT_FILE_PARTS = Object.freeze(['.config', 'minigit-bench', 'github-pat']);

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
