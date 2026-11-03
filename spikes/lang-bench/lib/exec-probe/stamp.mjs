// L3 (fix batch 5): the passing probe's stamp — `probedOn` and `host` in `inputs/specs.d/exec-layer.json` and the re-assembled `inputs/specs.json` — in TWO phases, so a failure of either file leaves both pins as they were.
//   prepare  the new contents are made in temporary files beside their targets (the assembly is made from a staged copy of `specs.d`, never from the real one); a failure removes what was made;
//   commit   the two temporary files are renamed into place; if the second rename fails the first is put back. Nothing else changes a pin.
// `finishProbe` calls `prepare` BEFORE the record is written (a failure is a record that says exit 2) and `commit` only AFTER the record is written.
import * as fs from 'node:fs';
import path from 'node:path';
import { assemble, canonicalJson } from '../specs-record.mjs';

const LAYER = 'exec-layer.json';

/** `{ commit(), abort() }` for the stamp of `record` in `inputsDir`; `stageDir` is an empty folder the caller owns (the staged copy of `specs.d` lives there). `io` is the test seam (default `node:fs`). Throws when it cannot prepare. */
export function prepareStamp({ inputsDir, record, stageDir, io = fs, tag = String(process.pid) }) {
  const specsD = path.join(inputsDir, 'specs.d');
  const layerFile = path.join(specsD, LAYER);
  const specsFile = path.join(inputsDir, 'specs.json');
  const tmpLayer = `${layerFile}.stamp-${tag}`;
  const tmpSpecs = `${specsFile}.stamp-${tag}`;
  const made = [];
  const abort = () => { for (const file of made.splice(0)) io.rmSync(file, { force: true }); };
  try {
    const current = JSON.parse(io.readFileSync(layerFile, 'utf8'));
    const stamped = canonicalJson({ execLayer: { ...current.execLayer, probedOn: record.probedOn, host: record.host } });
    const staged = path.join(stageDir, 'inputs');
    io.cpSync(inputsDir, staged, { recursive: true });
    io.writeFileSync(path.join(staged, 'specs.d', LAYER), stamped);
    const built = assemble(staged);
    if (!built.ok) throw new Error(built.reason);
    made.push(tmpLayer);
    io.writeFileSync(tmpLayer, stamped);
    made.push(tmpSpecs);
    io.writeFileSync(tmpSpecs, built.text);
  } catch (error) {
    abort();
    throw error;
  }
  const commit = () => {
    const before = io.readFileSync(layerFile, 'utf8');
    io.renameSync(tmpLayer, layerFile);
    try {
      io.renameSync(tmpSpecs, specsFile);
    } catch (error) {
      io.writeFileSync(tmpLayer, before); // put the first pin back: both stay as they were
      io.renameSync(tmpLayer, layerFile);
      abort();
      throw error;
    }
    made.length = 0;
  };
  return { commit, abort };
}
