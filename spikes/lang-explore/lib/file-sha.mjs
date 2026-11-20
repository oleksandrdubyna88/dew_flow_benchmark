// The sha256 of a file that may not be there: a missing or unreadable file is a `fail` naming the path and the error, never a throw (a pinned price file or policy that was moved away is a refusal with nothing changed).
import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';

/** -> `ok(hex)` | `fail('<file> cannot be read (<error>)')`. */
export function sha256OfFile(file) {
  try {
    return ok(createHash('sha256').update(readFileSync(file)).digest('hex'));
  } catch (e) {
    return fail(`${file} cannot be read (${e.message})`);
  }
}
