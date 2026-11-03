// D2 (fix batch 9): the temporary files of the probe. Every one lives in a `.exec-probe-tmp/` folder BESIDE its target — the same file system, so the rename into place still works — and its name carries the
// owner's `<pid>-<start time>`: `<target name>.<kind>-<pid>-<start>`, kind `link` (the lock's file), `stamp`, `restore` (a pin) or `record` (the record). A sweep can then list ONLY those folders (never `inputs/` or a shared
// `--out` folder) and tell a dead probe's file from a live one's (a dead pid, or a live pid with another start time: pid reuse).
import * as fs from 'node:fs';
import path from 'node:path';
import { processProbe } from '../../../../.agents/conventions/tools/lib/owned.mjs';

export const TMP_DIR = '.exec-probe-tmp';
const NAME = /^.+\.(?:link|stamp|restore|record)-(\d+)-(\d+|-)$/;

/** `<pid>-<start>` of a probe: the start time is `processProbe`'s (`-` when it cannot be read). */
export const ownId = (probe = processProbe, pid = process.pid) => `${pid}-${probe(pid).start ?? '-'}`;

/** The path of a temporary file for `target` of kind `kind` owned by `id`. */
export const tmpPath = (target, kind, id) => path.join(path.dirname(target), TMP_DIR, `${path.basename(target)}.${kind}-${id}`);

/** `{ pid, start }` of a temporary file's name, or null when it is not one of this design's. */
export function parseTmpName(name) {
  const found = NAME.exec(name);
  return found === null ? null : { pid: Number(found[1]), start: found[2] };
}

/** Make the tmp folder of `target` (`recursive`: the target's own folder may be new). */
export function ensureTmpFolder(target, io = fs) {
  io.mkdirSync(path.join(path.dirname(target), TMP_DIR), { recursive: true });
}

const TRIES = 5;

/**
 * Write `content` to the temporary file `file` in the tmp folder of `target`, making the folder first. Probes share the folder and each removes it when it is empty, so the folder can vanish between the `mkdir`
 * and the `open` (ENOENT): the folder is then made again, at most `TRIES` times in all, and the last error is thrown. `options` are the `writeFileSync` options.
 */
export function writeTmp(target, file, content, options, io = fs) {
  for (let attempt = 1; ; attempt += 1) {
    ensureTmpFolder(target, io);
    try {
      return io.writeFileSync(file, content, options);
    } catch (error) {
      if (error.code !== 'ENOENT' || attempt >= TRIES) throw error;
    }
  }
}

/** Remove the tmp folder of `dir` when it is empty (a folder that still holds a file stays; any failure is ignored: it is housekeeping). */
export function dropTmpFolder(dir, io = fs) {
  try {
    io.rmdirSync(path.join(dir, TMP_DIR));
  } catch {
    // not empty, or not there: nothing to do
  }
}
