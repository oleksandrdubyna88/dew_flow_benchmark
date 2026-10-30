// The exit codes of the bare runner and the result shape a loop step returns (E3b.S1): 0 done, 2 a series stop or the budget, 3 a resumable halt, 4 configuration (the guard), 5 the runner or the scorer failed.
// Held in one small module so the loop, the settling of a phase and the gates share them without importing each other.
export const EXIT = Object.freeze({ done: 0, stop: 2, halt: 3, config: 4, failed: 5 });

/** `{ exit, message, ...extra }`: how a step of the loop ended. */
export const done = (exit, message, extra = {}) => ({ exit, message, ...extra });

/** The persisted state of a run, held in one place: every change replaces the map and writes the file (through `write`). */
export function holder({ map, write }) {
  let current = map;
  return {
    get: () => current,
    commit(next) { current = next; write(current); return current; },
    apply(result) { if (!result.ok) throw new Error(result.reason); return this.commit(result.value); },
  };
}

/** The cell ids of every session the live run-map holds: what the sampler must count as ours (the map the run started from holds none of the sessions planned since). */
export const cellIdsOf = h => Object.keys(h.get().sessions).map(rid => `cell-${rid}`);

/** Run `steps` (functions that return a result or null, possibly a promise) in order; the first result ends the chain, `null` when none returns one. */
export async function firstResult(steps) {
  for (const step of steps) {
    const r = await step();
    if (r) return r;
  }
  return null;
}
