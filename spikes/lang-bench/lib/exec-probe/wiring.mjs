// The wiring of the probe COMMAND (K5, fix batch 7): the arguments `exec-probe.mjs` passes to the end of the probe and to the stop, built here so a test can run them. The one link between them is `state.written`:
// `endArguments` sets it when the record is in place (`onWritten`), and `stopArguments` reads it (`recordedCode`), so a SIGINT or SIGTERM after the record exits with the record's code. Dropping either side
// turns the real-signal test red.
import { runStop } from './stop.mjs';

/** The process's one cell of state: the exit code of the record once it is written (`null` before). */
export const newState = () => ({ written: null });

/** The options `endOfProbe` gets. */
export const endArguments = ({ record, reservation, noSpecs, inputsDir, stageDir, say, report, state }) => ({
  record, reservation, noSpecs, inputsDir, stageDir, say, report, onWritten: code => { state.written = code; },
});

/** The options the stop (`runStop`) gets for a signal that exits with `code` when the record is not written yet. */
export const stopArguments = ({ code, state, say, exit, release, describeLive, cleanLive, closeScope, bound = 20_000 }) => ({
  code, recordedCode: () => state.written, bound, say, exit, release, describeLive, cleanLive, closeScope,
});

const SIGNALS = Object.freeze([['SIGINT', 130], ['SIGTERM', 143]]);

/** Install the SIGINT and SIGTERM handlers of the command on `proc` (the first signal wins; a second one is ignored while the stop runs). */
export function installStopHandlers({ proc = process, exit = code => proc.exit(code), ...rest }) {
  let stopping = false;
  SIGNALS.forEach(([signal, code]) => proc.on(signal, () => {
    if (stopping) return;
    stopping = true;
    rest.onStopping?.();
    runStop(stopArguments({ code, exit, ...rest }));
  }));
}
