// The end of a probe (W3, L3; fix batch 5): the stamp is PREPARED before the record is written — a failure is a record that says exit 2 with the error, so the file never says 0 when the command says 2 — the
// record is written, the reservation is taken only after the write succeeded (a write that throws releases the placeholder: no empty `exec-probe-…-N.json` stays) and the stamp is COMMITTED last.
import { shouldStampProbedOn } from './probe.mjs';
import { prepareStamp } from './stamp.mjs';

/**
 * `{ code, error? }`, the command's exit code. `record` is the probe's record, `reservation` its `makeReservation`, `write(file, record, held)` the exclusive writer, `report(record, file)` the printing,
 * `stage` `{ inputsDir, stageDir }` for the stamp (`noSpecs` skips it) and `io` the stamp's test seam. A write that throws is rethrown after the placeholder is released and the prepared stamp discarded.
 */
export function finishProbe({ record, reservation, write, report, noSpecs, stage, io }) {
  let final = record;
  let prepared = null;
  if (!noSpecs && shouldStampProbedOn(record)) {
    try {
      prepared = prepareStamp({ ...stage, record, io });
    } catch (error) {
      final = { ...record, exit: { code: 2, halt: 'probed-on-stamp-failed', stampError: error.message } };
    }
  }
  const file = reservation.file;
  try {
    write(file, final, reservation.held());
  } catch (error) {
    prepared?.abort();
    reservation.release();
    throw error;
  }
  reservation.take(); // written: the file is the record now, never released
  report(final, file);
  if (prepared === null) return { code: final.exit.code };
  try {
    prepared.commit();
  } catch (error) {
    return { code: 2, error: `the pins could not be stamped (${error.message}); both stay as they were` };
  }
  return { code: final.exit.code };
}
