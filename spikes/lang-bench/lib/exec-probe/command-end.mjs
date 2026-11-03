// The end of the probe COMMAND, as a function the command calls and the tests run through its seams (S1): the stamp and the record in the order of `finish.mjs`, the lines for stderr, and the record's exit code.
import * as fs from 'node:fs';
import path from 'node:path';
import { finishProbe } from './finish.mjs';

/**
 * `code`, the command's exit code. `say(text)` prints one stderr line, `report(record, file)` prints the result lines, `onWritten(code)` is called once the record is in place (a stop after it only closes
 * the owned folders and exits with this code). `inputsDir` is the folder of the pins and `stageDir` an empty folder the caller owns.
 */
export function endOfProbe({ record, reservation, noSpecs, inputsDir, stageDir, say, report, onWritten, io = fs, write }) {
  const outcome = finishProbe({ record, reservation, noSpecs, stage: { inputsDir, stageDir: path.resolve(stageDir) }, report, onWritten, io, ...(write === undefined ? {} : { write }) });
  outcome.messages.forEach(say);
  return outcome.code;
}
