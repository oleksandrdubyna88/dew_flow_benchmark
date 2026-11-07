// Files that are published whole or not at all (F1.S2 review). A record of the scoring (an attempt, an event, the completion record, the score) is first written to a temporary name of its owner beside its final name, flushed to
// disk, and then put at the final name by a HARD LINK, which fails when the name is taken: so the record is complete the moment it exists, a second writer never replaces it, and a kill in the middle of a write leaves a temporary
// file and no truncated record. A temporary file (`*.tmp`) is never read as a record; the scorer that starts next removes the ones a dead scorer left (it holds the scorer lock, so no live writer owns them).
import { randomBytes } from 'node:crypto';
import * as nodeFs from 'node:fs';
import path from 'node:path';

const tempNameOf = file => path.join(path.dirname(file), `.${path.basename(file)}.${process.pid}.${randomBytes(4).toString('hex')}.tmp`);

function writeFlushed({ temp, text, fs }) {
  const fd = fs.openSync(temp, 'wx', 0o644);
  try {
    fs.writeSync(fd, text);
    fs.fsyncSync(fd);
  } finally {
    fs.closeSync(fd);
  }
}

/** Publish `text` at `file`, whole, once. The error is `EEXIST` when the name is taken (the first record stays as it was). A failure in the middle leaves the temporary file, which nothing reads. */
export function publishOnce({ file, text, fs = nodeFs }) {
  fs.mkdirSync(path.dirname(file), { recursive: true });
  const temp = tempNameOf(file);
  writeFlushed({ temp, text, fs });
  try {
    fs.linkSync(temp, file);
  } catch (e) {
    if (e.code === 'EEXIST') fs.unlinkSync(temp);
    throw e;
  }
  fs.unlinkSync(temp);
  return file;
}

/** Publish `text` under the first free name of `names(n)` (n = 1, 2, ...): a name taken in the same instant gets the next. -> the name that was used. */
export function publishNamed({ dir, names, text, fs = nodeFs }) {
  for (let n = 1; ; n += 1) {
    const name = names(n);
    try {
      publishOnce({ file: path.join(dir, name), text, fs });
      return name;
    } catch (e) {
      if (e.code !== 'EEXIST') throw e;
    }
  }
}

const TEMP_FOLDERS = Object.freeze(['.', 'passes', 'events']);

/** Remove the temporary files a dead scorer left in the scoring folder. -> the paths removed. */
export function removeStaleTemps(work, fs = nodeFs) {
  const found = TEMP_FOLDERS.flatMap(sub => {
    const dir = path.join(work, sub);
    return fs.existsSync(dir) ? fs.readdirSync(dir).filter(f => f.endsWith('.tmp')).map(f => path.join(dir, f)) : [];
  });
  found.forEach(f => fs.rmSync(f, { force: true }));
  return found;
}
