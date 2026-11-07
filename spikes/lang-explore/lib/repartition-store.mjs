// The result files of `repartition` (F1.S1 (b)): one file per run whose inputs changed, `<series>/partition-recomputed/<yyyymmddTHHMMSSZ>-<gen>.json`, immutable once written. Nothing here ever edits, renames over or removes a
// result. A result is made in a temporary file of the same folder (named by the owned-name helper, with a sibling claim so its owner can be read), flushed, and put at its final name by a hard link that fails when the name is taken,
// so two runs in the same second never replace each other. A temporary file is never read as a result; a stale one (its owner is gone, `common.test-leftovers`) is removed by the next run, one whose owner still runs is left and named.
import { randomBytes } from 'node:crypto';
import { closeSync, fsyncSync, linkSync, lstatSync, mkdirSync, openSync, readFileSync, readdirSync, rmSync, writeSync } from 'node:fs';
import path from 'node:path';

export const RESULT_DIR = 'partition-recomputed';
const TEMP_WHAT = 'partition';
const MAX_NAME_TRIES = 8;

const isRegular = file => lstatSync(file, { throwIfNoEntry: false })?.isFile() === true;
const namesIn = folder => (lstatSync(folder, { throwIfNoEntry: false })?.isDirectory() === true ? readdirSync(folder).sort() : []);

/** `yyyymmddTHHMMSSZ` in UTC. */
export const stampOf = date => `${date.toISOString().slice(0, 19).replace(/[-:]/g, '')}Z`;

function readBody(file) {
  try {
    return JSON.parse(readFileSync(file, 'utf8'));
  } catch {
    return null;
  }
}

/** The path of a complete result in `folder` with this fingerprint, or null. Only `*.json` regular files are results; a `.tmp` file never is. */
export function findComplete(folder, fingerprint) {
  const match = namesIn(folder).filter(name => name.endsWith('.json')).map(name => path.join(folder, name)).filter(isRegular)
    .find(file => { const body = readBody(file); return body?.complete === true && body.fingerprint === fingerprint; });
  return match ?? null;
}

function stateOf(owned, file) {
  const owner = owned.readOwner(file, 'folder');
  return owned.ownerState(owner, undefined, { nameOwner: owned.classifyName(path.basename(file)).owner, kernelMine: owned.isMine(file) });
}

/**
 * The temporary files of earlier runs: `{ removed, live, unknown }` (paths). Only a regular file of this repository's name, in this folder, is looked at; it is removed with its claim only when its owner is `gone`. `live` and `unknown`
 * (a record that cannot be told) are left exactly as they are.
 */
export function sweepTemps(folder, owned) {
  const prefix = owned.ownedPrefix(TEMP_WHAT);
  const found = namesIn(folder).filter(name => name.startsWith(prefix) && name.endsWith('.tmp')).map(name => path.join(folder, name)).filter(isRegular);
  const states = found.map(file => [file, stateOf(owned, file)]);
  const removed = states.filter(([, state]) => state === 'gone').map(([file]) => file);
  for (const file of removed) { rmSync(file, { force: true }); rmSync(`${file}.owner`, { force: true }); }
  const pick = wanted => states.filter(([, state]) => state === wanted).map(([file]) => file);
  return { removed, live: pick('live'), unknown: states.filter(([, state]) => !['gone', 'live'].includes(state)).map(([file]) => file) };
}

function writeFlushed(file, text) {
  const fd = openSync(file, 'wx', 0o600);
  try {
    writeSync(fd, text);
    fsyncSync(fd);
  } finally {
    closeSync(fd);
  }
}

/** Put `temp` at a name `<stamp>-<gen>.json` in `folder` that nobody has: a hard link fails when the name is taken, so a result is never replaced. -> the path. */
function publish({ folder, temp, now, genId }) {
  for (let attempt = 0; attempt < MAX_NAME_TRIES; attempt += 1) {
    const target = path.join(folder, `${stampOf(now())}-${genId()}.json`);
    try {
      linkSync(temp, target);
      return target;
    } catch (error) {
      if (error.code !== 'EEXIST') throw error;
    }
  }
  throw new Error(`could not find a free result name in ${folder} after ${MAX_NAME_TRIES} tries`);
}

/** Write `body` as a new result in `folder` (made when absent): temporary file with a claim, flushed, linked to its final name, the temporary file and the claim removed. -> the result's path. */
export function writeResult({ folder, body, now, genId = () => randomBytes(4).toString('hex'), owned }) {
  mkdirSync(folder, { recursive: true });
  const temp = path.join(folder, `${owned.ownedName(TEMP_WHAT)}.tmp`);
  const written = owned.writeOwner(temp);
  try {
    writeFlushed(temp, `${JSON.stringify(body, null, 2)}\n`);
    return publish({ folder, temp, now, genId });
  } finally {
    rmSync(temp, { force: true });
    owned.removeClaimIfStill(temp, written);
  }
}
