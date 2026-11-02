// B2 (fix batch 4): the one-way flag of a process that is stopping (a SIGINT or SIGTERM handler sets it before it cleans up). Every launch path of the layer — a run, a scope prefix — checks it, so work
// still in flight cannot start a scope, a holder or a run folder after the cleanup passes have looked: such a unit would outlive the cleanup and the process.
let stopped = false;

/** Mark this process as stopping. There is no way back: a stopping process exits. */
export function markStopped() {
  stopped = true;
}

export const isStopped = () => stopped;

/** Throws when this process is stopping: called at the start of every launch path. */
export function assertRunning(what) {
  if (stopped) throw new Error(`this process is stopping: ${what} is not started`);
}
