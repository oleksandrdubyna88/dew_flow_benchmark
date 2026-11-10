// One way to replace a registry file whole (F2.S1): a temporary file of this process beside it, flushed, then renamed over it, so a kill in the middle leaves the old file and a temporary one nothing reads.
import * as nodeFs from 'node:fs';
import path from 'node:path';
import { tempNameOf, writeFlushed } from './publish-once.mjs';

/** `value` as JSON (two-space indent, a final newline) in `file`, replacing it whole. */
export function replaceJson({ file, value, fs = nodeFs }) {
  fs.mkdirSync(path.dirname(file), { recursive: true });
  const temp = tempNameOf(file);
  writeFlushed({ temp, text: `${JSON.stringify(value, null, 2)}\n`, fs });
  try { fs.renameSync(temp, file); } catch (e) { fs.rmSync(temp, { force: true }); throw e; }
}
