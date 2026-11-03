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

/**
 * The guard of stdout and stderr (L4, D3). An EPIPE (the reader of a pipe closed: `| head`) is always ignored. Any other error AFTER the record is written (`state.written` is its exit code) is ignored too: the
 * record's code stays, never an uncaught exception. BEFORE the record it is the command's failure: `fail(error)` is called (the command stops with exit 2); the listener itself never throws.
 */
export function guardStreams(proc = process, { state = newState(), fail = () => proc.exit(2) } = {}) {
  [proc.stdout, proc.stderr].forEach(stream => stream.on('error', error => {
    if (error?.code === 'EPIPE' || state.written !== null) return;
    fail(error);
  }));
}

/**
 * Install the guard of the streams FIRST (D3: it is the first thing the command does, before any write), then the SIGINT and SIGTERM handlers of the command on `proc` (the first signal wins; a second one
 * is ignored while the stop runs). A stream error before the record is the stop with exit 2 (the cleanup, then exit 2), through the same once-only gate as a signal.
 */
export function installStopHandlers({ proc = process, exit = code => proc.exit(code), ...rest }) {
  let stopping = false;
  const stop = code => {
    if (stopping) return;
    stopping = true;
    rest.onStopping?.();
    runStop(stopArguments({ code, exit, ...rest }));
  };
  guardStreams(proc, { state: rest.state, fail: () => stop(2) });
  SIGNALS.forEach(([signal, code]) => proc.on(signal, () => stop(code)));
}

/** The start of the command, in this order: the stream guard and the stop handlers (`installStopHandlers`), the release of an unwritten placeholder on exit, and only then `sweep()` (the start sweep writes to stderr). */
export function startCommand({ proc = process, sweep, ...stopOptions }) {
  installStopHandlers({ proc, ...stopOptions });
  proc.on('exit', stopOptions.release); // a path that ends the process any other way still releases an unwritten placeholder (once: the reservation forgets it)
  sweep();
}
