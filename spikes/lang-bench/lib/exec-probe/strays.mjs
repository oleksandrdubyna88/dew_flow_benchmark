// L3 and D2 (fix batches 8 and 9): the strays of dead probes, swept at the probe's START — explicitly, in the command's start sequence. It lists ONLY the `.exec-probe-tmp/` folders beside the files the probe will
// write (`<dir>/.exec-probe-tmp`), never `inputs/` itself or a shared `--out` folder, and removes the files whose owner is dead: the pid is gone, or it is alive with another start time (pid reuse), read from the
// file's name. A live probe's files and every file outside a tmp folder are never touched. An emptied tmp folder is removed (`specs.d` must hold no stray folder). Nothing here throws.
import * as fs from 'node:fs';
import path from 'node:path';
import { processProbe } from '../../../../.agents/conventions/tools/lib/owned.mjs';
import { dropTmpFolder, parseTmpName, TMP_DIR, whyUnsafeFolder } from './tmp-files.mjs';

const isDead = (owner, probe) => {
  const now = probe(owner.pid);
  if (now.state === 'dead') return true;
  return now.state === 'live' && owner.start !== '-' && now.start !== undefined && String(now.start) !== owner.start;
};

/** Remove one dead probe's file: only a regular file inside the (checked) folder, found by `lstat`, never a link. */
function removeOne(folder, name, io) {
  const file = path.join(folder, name);
  try {
    if (!io.lstatSync(file).isFile()) return { removed: [], errors: [`${file}: is not a regular file: skipped`] };
    io.rmSync(file, { force: true });
    return { removed: [file], errors: [] };
  } catch (error) {
    return { removed: [], errors: [`${file}: ${error.code ?? error.message}`] };
  }
}

/** The outcome of sweeping one `<dir>/.exec-probe-tmp`: `{ removed, errors }`. */
function sweepOne(dir, probe, uid, io) {
  const folder = path.join(dir, TMP_DIR);
  let names;
  try {
    const why = whyUnsafeFolder(folder, uid, io);
    if (why !== null) return { removed: [], errors: [`${folder} ${why}: skipped`] };
    names = io.readdirSync(folder);
  } catch (error) {
    return { removed: [], errors: error.code === 'ENOENT' ? [] : [`${folder}: ${error.code ?? error.message}`] };
  }
  const outcomes = names.map(name => ({ name, owner: parseTmpName(name) })).filter(({ owner }) => owner !== null && isDead(owner, probe)).map(({ name }) => removeOne(folder, name, io));
  dropTmpFolder(dir, io);
  return { removed: outcomes.flatMap(o => o.removed), errors: outcomes.flatMap(o => o.errors) };
}

/** `{ removed: [path], errors: [text] }` for the tmp folders beside `dirs` (a repeated folder is looked at once). `probe`, `io` and `uid` are the test seams. */
export function retireStartupStrays({ dirs, probe = processProbe, io = fs, uid = process.getuid?.() }) {
  const outcomes = [...new Set(dirs)].map(dir => sweepOne(dir, probe, uid, io));
  return { removed: outcomes.flatMap(o => o.removed), errors: outcomes.flatMap(o => o.errors) };
}
