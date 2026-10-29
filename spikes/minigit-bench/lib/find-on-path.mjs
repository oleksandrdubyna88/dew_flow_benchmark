// Locating an executable by PATH, the one way (spawn resolves a bare name the same way): the first regular, executable file called
// `name` in the PATH string's directories. bwrap, claude and every other host tool are found here and nowhere else.
import { accessSync, constants, statSync } from 'node:fs';
import path from 'node:path';

/** The first executable called `name` on a PATH string (default: this process's), or null. */
export function findOnPath(name, pathVar = process.env.PATH ?? '') {
  for (const dir of pathVar.split(path.delimiter).filter(Boolean)) {
    const candidate = path.join(dir, name);
    try {
      accessSync(candidate, constants.X_OK);
      if (statSync(candidate).isFile()) return candidate;
    } catch {
      continue; // not here, or not executable: the next PATH entry
    }
  }
  return null;
}
