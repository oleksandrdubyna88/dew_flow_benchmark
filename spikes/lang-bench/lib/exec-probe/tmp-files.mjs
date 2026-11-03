// D2 (fix batch 9): the temporary files of the probe. Every one lives in a `.exec-probe-tmp/` folder BESIDE its target — the same file system, so the rename into place still works — and its name carries the
// owner's `<pid>-<start time>`: `<target name>.<kind>-<pid>-<start>`, kind `link` (the lock's file), `mark` (the lock's committing mark), `stamp`, `restore` (a pin) or `record` (the record). The two pins' files (N1) both live in `inputs/.exec-probe-tmp/`, so `specs.d` never holds a temporary entry. A sweep can then list ONLY those folders (never `inputs/` or a shared
// `--out` folder) and tell a dead probe's file from a live one's (a dead pid, or a live pid with another start time: pid reuse).
import * as fs from 'node:fs';
import path from 'node:path';
import { processProbe } from '../../../../.agents/conventions/tools/lib/owned.mjs';

export const TMP_DIR = '.exec-probe-tmp';
const NAME = /^.+\.(?:link|mark|stamp|restore|record)-(\d+)-(\d+|-)$/;

/** `<pid>-<start>` of a probe: the start time is `processProbe`'s (`-` when it cannot be read). */
export const ownId = (probe = processProbe, pid = process.pid) => `${pid}-${probe(pid).start ?? '-'}`;

/** The path of a temporary file for `target` of kind `kind` owned by `id`, in the tmp folder of `base`. There is NO default base (P4): a caller says where, so a pin's temporary can never land beside the pin by omission. */
export function tmpPath(target, kind, id, base) {
  if (base === undefined) throw new Error(`tmpPath: a base folder is required for the ${kind} temporary of ${target} (use besideTmpPath or pinTmpPath)`);
  return path.join(base, TMP_DIR, `${path.basename(target)}.${kind}-${id}`);
}

/** The temporary file of a target that is NOT a pin (the lock, the record): in the tmp folder of the target's own folder. */
export const besideTmpPath = (target, kind, id) => tmpPath(target, kind, id, path.dirname(target));

/** The temporary file of a pin (`specs.json` or `specs.d/exec-layer.json`, named relative to `inputsDir`): ALWAYS in `inputsDir/.exec-probe-tmp/`, so `specs.d` never holds a temporary entry (N1, P4). */
export const pinTmpPath = (inputsDir, pin, kind, id) => tmpPath(pin, kind, id, inputsDir);

/** `{ pid, start }` of a temporary file's name, or null when it is not one of this design's. */
export function parseTmpName(name) {
  const found = NAME.exec(name);
  return found === null ? null : { pid: Number(found[1]), start: found[2] };
}

const TRIES = 5;

/** Why a tmp `folder` must not be used (a string), or null: it must be a real directory — never a symbolic link — owned by `uid` (P3 and N2: no temporary write and no sweep follows a link). `lstat` is the check. */
export function whyUnsafeFolder(folder, uid, io = fs) {
  const info = io.lstatSync(folder);
  if (info.isSymbolicLink()) return 'is a symbolic link';
  if (!info.isDirectory()) return 'is not a directory';
  return uid !== undefined && info.uid !== uid ? 'is not owned by this user' : null;
}

/**
 * Write `content` to the new temporary file `file` (the flag is always `wx`: a name that exists, a planted symlink included, is refused and never opened), making its folder first and refusing a folder that
 * `whyUnsafeFolder` rejects (an Error naming it). Probes share the folder and each removes it when it is empty, so the folder can vanish between the `mkdir` and the `open` (ENOENT): the folder is then made again,
 * at most `TRIES` times in all, and the last error is thrown. `options` are the other `writeFileSync` options; `uid` is a test seam.
 */
export function writeTmp(file, content, options, io = fs, uid = process.getuid?.()) {
  const folder = path.dirname(file);
  for (let attempt = 1; ; attempt += 1) {
    io.mkdirSync(folder, { recursive: true });
    try {
      const why = whyUnsafeFolder(folder, uid, io);
      if (why !== null) throw new Error(`${folder} ${why}`);
      return io.writeFileSync(file, content, { ...options, flag: 'wx' });
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
