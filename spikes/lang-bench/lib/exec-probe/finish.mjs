// The end of a probe (W3, L3, S1; fix batches 4 to 6): THE RECORD IS THE LAST WRITE. The order:
//   1. the stamp lock; the stamp PREPARED in temporary files beside the pins; the stamp COMMITTED (two renames, the first put back if the second fails). A failure of any of these is a record that says exit 2 with
//      `stampError`, so the file never says 0 when the command says 2;
//   2. the record written ONCE, with its final content, to a temporary file beside the placeholder and renamed over it (a disk-full write leaves no cut-off dated record). If that fails both pins are put back
//      from their saved contents, the placeholder is released, and the outcome says exit 2 and names any pin that could not be put back (it points at a probe with no record);
//   3. only then the reservation is taken, the report printed and the exit is the record's code.
import * as fs from 'node:fs';
import path from 'node:path';
import { shouldStampProbedOn, writeReservedRecord } from './probe.mjs';
import { LOCK_NAME } from './stamp-lock.mjs';
import { acquireLock, commitStamp, discardStamp, LAYER_PIN, prepareStamp, restorePins } from './stamp.mjs';

const exit2 = (record, stampError) => ({ ...record, exit: { code: 2, halt: 'probed-on-stamp-failed', stampError } });
/** `<pin> probedOn=<value>` for the layer pin as it is found (what a retired probe may have stamped), or the reason it cannot be read. */
function pinsProbedOn(inputsDir, io) {
  try {
    return `${LAYER_PIN} probedOn=${JSON.parse(io.readFileSync(path.join(inputsDir, LAYER_PIN), 'utf8')).execLayer.probedOn ?? 'none'}`;
  } catch (error) {
    return `${LAYER_PIN} could not be read (${error.code ?? error.message})`;
  }
}

const unstampedNote = stamped => (stamped.length > 0 ? `; ${stamped.join(' and ')} IS stamped and the put-back failed` : '');

/** The stamp steps: `{ record, prepared, committed, lock, messages }` — the record as it must be written, what was prepared and committed, the lock held, and lines for stderr. */
function stampSteps({ record, stage, io }) {
  let got;
  try {
    got = acquireLock({ inputsDir: stage.inputsDir, io });
  } catch (error) { // every lock error is a record that says exit 2 (K1), never a throw that loses the record
    const lock = path.join(stage.inputsDir, LOCK_NAME);
    const reason = `the stamp lock ${lock} could not be used (${error.code ?? ''} ${error.message})`.replace('( ', '(');
    return { record: exit2(record, reason), prepared: null, committed: false, stillStamped: [], lock: null, messages: [`exec-probe: ${reason}`] };
  }
  if (!got.ok) return { record: exit2(record, got.error), prepared: null, committed: false, stillStamped: [], lock: null, messages: [`exec-probe: ${got.error}`] };
  const messages = got.retired === null ? [] : [`exec-probe: ${got.retired}; the pins MAY be stamped without a record (a SIGKILL between the commit and the record's rename): ${pinsProbedOn(stage.inputsDir, io)}`];
  let prepared;
  try {
    prepared = prepareStamp({ ...stage, record, io });
  } catch (error) {
    return { record: exit2(record, error.message), prepared: null, committed: false, stillStamped: [], lock: got, messages };
  }
  const committed = commitStamp(prepared, io);
  if (committed.ok) return { record, prepared, committed: true, stillStamped: [], lock: got, messages };
  const note = unstampedNote(committed.stamped);
  // K2: the prepared stamp and the pins that ARE stamped are kept: if the record cannot be written either, those pins are put back and the messages come from the real state
  return { record: exit2(record, `${committed.error}${note}`), prepared, committed: false, stillStamped: committed.stamped, lock: got, messages: [...messages, ...(note === '' ? [] : [`exec-probe: the pins are inconsistent: ${committed.stamped.join(' and ')} is stamped, the other is not (exit 2)`])] };
}

const LAYER_AND_SPECS = ['specs.d/exec-layer.json', 'specs.json'];

/** Discard what is still in temporary files after the end (a failed commit leaves its temporary files). */
const tidy = (steps, io) => { if (steps.prepared !== null && !steps.committed) discardStamp(steps.prepared, io); };

/** The messages of a record that could not be written, from the REAL state of the pins (K2): the pins stamped before, which of them could not be put back, and the placeholder. */
function writeFailure({ steps, reservation, error, io }) {
  const stampedBefore = steps.committed ? LAYER_AND_SPECS : steps.stillStamped;
  const back = stampedBefore.length > 0 && steps.prepared !== null ? restorePins(steps.prepared, io) : { stillStamped: [] };
  const stuck = stampedBefore.filter(pin => back.stillStamped.includes(pin));
  reservation.release();
  tidy(steps, io);
  const outcome = stampedBefore.length === 0 ? 'the pins were not changed' : stuck.length === 0 ? 'both pins were put back' : `${stuck.join(' and ')} could not be put back`;
  const lines = stuck.length > 0 ? [`exec-probe: ${stuck.join(' and ')} could not be put back: it points at a probe with NO record (exit 2)`] : [];
  return [`exec-probe: the record could not be written (${error.message}); ${outcome}; the placeholder was released (exit 2)`, ...lines];
}

/** Release the lock (L1: LAST): `[]` or one stderr line; a release that fails never changes the exit. */
function releaseLast(lock) {
  try {
    lock?.release();
    return [];
  } catch (error) {
    return [`exec-probe: the stamp lock could not be released (${error.code ?? ''} ${error.message}): remove .stamp.lock by hand if no probe runs`.replace('( ', '(')];
  }
}

/** The rest of the end: the record written last. `{ code, messages }`. */
function writeLast({ steps, reservation, report, onWritten, io, write }) {
  const file = reservation.file;
  try {
    write(file, steps.record, reservation.held(), io);
  } catch (error) {
    const lines = writeFailure({ steps, reservation, error, io });
    return { code: 2, messages: [...steps.messages, ...lines, ...releaseLast(steps.lock)] };
  }
  onWritten?.(steps.record.exit.code);
  reservation.take(); // written: the file is the record now, never released
  report(steps.record, file);
  tidy(steps, io);
  return { code: steps.record.exit.code, messages: [...steps.messages, ...releaseLast(steps.lock)] }; // the lock is released LAST (L1)
}

/** `{ code, messages }` — the command's exit code and the lines for stderr. `write(file, record, held, io)` is the record writer (a seam), `onWritten(code)` is called once the record is in place. */
export function finishProbe({ record, reservation, report, noSpecs, stage, io = fs, write = writeReservedRecord, onWritten }) {
  const steps = !noSpecs && shouldStampProbedOn(record) ? stampSteps({ record, stage, io }) : { record, prepared: null, committed: false, stillStamped: [], lock: null, messages: [] };
  return writeLast({ steps, reservation, report, onWritten, io, write });
}
