// The passing probe's stamp — `probedOn` and `host` in `inputs/specs.d/exec-layer.json` and the re-assembled `inputs/specs.json` — in steps that each say exactly what they changed (S1, fix batch 6). Nothing here
// mutates a value it is given: every step returns a new description.
//   lock      `inputs/.stamp.lock` holds the pid of the probe that is stamping (created with `wx`): two passing probes cannot interleave their stamps; a lock whose pid is dead is stale and is retired with its stray files;
//   prepare   the new contents are made in temporary files BESIDE their targets (a rename across file systems fails); the assembly is staged from ONLY the files `assemble` reads, never a copy of `inputs/`;
//   commit    the two renames; if the second fails the first pin is put back; the outcome names which pins are stamped;
//   restore   both pins put back from the contents saved at prepare time (when the record could not be written).
import * as fs from 'node:fs';
import path from 'node:path';
import { assemble, canonicalJson, WRITER_FILES } from '../specs-record.mjs';
import { acquireLock as acquireStampLock } from './stamp-lock.mjs';

export const LAYER_PIN = 'specs.d/exec-layer.json';
export const SPECS_PIN = 'specs.json';
const STRAY = pid => new RegExp(`\\.(stamp|restore)-${pid}$`);

const pinsOf = inputsDir => ({ layer: path.join(inputsDir, LAYER_PIN), specs: path.join(inputsDir, SPECS_PIN) });
/** Remove the stray `*.stamp-<pid>` and `*.restore-<pid>` files a dead stamping probe left in `inputs/` and `inputs/specs.d/`; the names removed. */
function retireStrays(inputsDir, pid, io) {
  return [inputsDir, path.join(inputsDir, 'specs.d')].flatMap(dir => io.readdirSync(dir).filter(name => STRAY(pid).test(name)).map(name => path.join(dir, name))).map(file => {
    io.rmSync(file, { force: true });
    return file;
  });
}

/** The stamp lock (see `stamp-lock.mjs`), with this module's stray retirement. */
export function acquireLock({ inputsDir, io = fs, ...rest }) {
  return acquireStampLock({ inputsDir, io, ...rest, strays: pid => retireStrays(inputsDir, pid, io) });
}

/** The files `assemble` reads, copied into `stageDir/specs.d` (nothing else of `inputs/`). */
function stage(inputsDir, stageDir, stampedLayer, io) {
  const specsD = path.join(stageDir, 'specs.d');
  io.mkdirSync(specsD, { recursive: true });
  WRITER_FILES.filter(name => io.existsSync(path.join(inputsDir, 'specs.d', name))).forEach(name => io.copyFileSync(path.join(inputsDir, 'specs.d', name), path.join(specsD, name)));
  io.writeFileSync(path.join(specsD, 'exec-layer.json'), stampedLayer);
  return assemble(stageDir);
}

/**
 * The prepared stamp: `{ files, tmp, saved, tag }`, the new contents already in temporary files beside the pins and the old contents saved. Throws (after removing what it made) when it cannot prepare, so the
 * caller can say it in the record.
 */
export function prepareStamp({ inputsDir, record, stageDir, io = fs, tag = String(process.pid) }) {
  const files = pinsOf(inputsDir);
  const tmp = { layer: `${files.layer}.stamp-${tag}`, specs: `${files.specs}.stamp-${tag}` };
  const savedLayer = io.readFileSync(files.layer, 'utf8');
  const saved = { layer: savedLayer, specs: io.readFileSync(files.specs, 'utf8') };
  const stamped = canonicalJson({ execLayer: { ...JSON.parse(savedLayer).execLayer, probedOn: record.probedOn, host: record.host } });
  try {
    const built = stage(inputsDir, stageDir, stamped, io);
    if (!built.ok) throw new Error(built.reason);
    io.writeFileSync(tmp.layer, stamped);
    io.writeFileSync(tmp.specs, built.text);
  } catch (error) {
    io.rmSync(tmp.layer, { force: true });
    io.rmSync(tmp.specs, { force: true });
    throw error;
  }
  return { files, tmp, saved, tag };
}

/** Remove the temporary files of a prepared stamp (nothing is stamped). */
export function discardStamp(prepared, io = fs) {
  io.rmSync(prepared.tmp.layer, { force: true });
  io.rmSync(prepared.tmp.specs, { force: true });
}

/** Put `content` back at `file` through a temporary file beside it and a rename: `true` when it is back. */
function putBack(file, content, tag, io) {
  const tmp = `${file}.restore-${tag}`;
  try {
    io.writeFileSync(tmp, content);
    io.renameSync(tmp, file);
    return true;
  } catch {
    io.rmSync(tmp, { force: true });
    return false;
  }
}

/**
 * The two renames. `{ ok: true, stamped: [both pins] }`; or `{ ok: false, error, stamped }` where `stamped` names the pins that ARE stamped now: none when the first rename failed, none when the second failed and
 * the first pin was put back, and the first pin when the put-back failed as well — never "both stay as they were" unless that is true.
 */
export function commitStamp(prepared, io = fs) {
  try {
    io.renameSync(prepared.tmp.layer, prepared.files.layer);
  } catch (error) {
    return { ok: false, error: error.message, stamped: [] };
  }
  try {
    io.renameSync(prepared.tmp.specs, prepared.files.specs);
    return { ok: true, stamped: [LAYER_PIN, SPECS_PIN] };
  } catch (error) {
    const back = putBack(prepared.files.layer, prepared.saved.layer, prepared.tag, io);
    return { ok: false, error: error.message, stamped: back ? [] : [LAYER_PIN] };
  }
}

/** Put both pins back from the contents saved at prepare time: `{ stillStamped }`, the pins that could not be put back. */
export function restorePins(prepared, io = fs) {
  const results = [[LAYER_PIN, prepared.files.layer, prepared.saved.layer], [SPECS_PIN, prepared.files.specs, prepared.saved.specs]].map(([name, file, content]) => [name, putBack(file, content, prepared.tag, io)]);
  return { stillStamped: results.filter(([, ok]) => !ok).map(([name]) => name) };
}
