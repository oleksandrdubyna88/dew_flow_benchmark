// The progress of a long operation of the recovery path (F3.S1 (a), PR 4, coai code round 1 C2): `stage-archive` and `stage-restore` read and copy gigabytes, so each phase says where it is. The module has no output of its own: it calls
// a `say(sentence)` the command wires to `io.say`, and without one it says nothing.
export const PROGRESS_EVERY = 500;

/**
 * `items.map(fn)` that says `<phase>: <done> of <total> file(s)` through `report.say` before the first item, after every `report.every`th item and after the last. `report` is `{ say, every }`; none is a silent run.
 */
export function mapSaying({ items, phase, report = {}, fn }) {
  const { say = () => {}, every = PROGRESS_EVERY } = report;
  const line = done => say(`${phase}: ${done} of ${items.length} file(s)`);
  const result = items.map((item, i) => {
    if (i % every === 0) line(i);
    return fn(item);
  });
  line(items.length);
  return result;
}
