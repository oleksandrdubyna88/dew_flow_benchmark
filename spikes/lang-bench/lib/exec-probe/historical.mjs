// Q4 (fix batch 2): the OP-K experiment scripts are a RECORD of measurements made before the own-cgroup view became the layer's box and before D55's 90 % keys. Run as written they would measure a box
// that no longer exists and append to evidence that must stay as it is (T-L53). Each starts with `refuseHistorical`: exit 2, no work, whatever the arguments.
import path from 'node:path';

/** Writes the refusal for `script` (a path or a name) to stderr and ends the process with exit 2. */
export function refuseHistorical(script) {
  process.stderr.write(`${path.basename(script)}: historical, not re-runnable as written: it measured the box as it was before the own-cgroup view and before D55 (the 90 % heap keys); its results are kept in inputs/opk-experiment-2026-11-02.json, which no script may change (T-L53)\n`);
  process.exit(2);
}
