// L3 (fix batch 8): the strays no stale lock names, swept at the probe's START in the folders it will use (`inputs/` and the folder of an `--out` file). Three kinds, DEAD pids only:
//   `.stamp.lock.link-<pid>`   a probe that died between creating its link file and the `link()`;
//   `.stamp.lock.retire-<pid>` a probe that threw (or died) during a retire: if its content says `committing` the pins MAY be stamped without a record (the caller prints that warning);
//   `.exec-probe-record.tmp-<pid>` a probe that died with its record's temporary file, whether or not it stamped.
// A live pid's files, and every other file, are never touched. Nothing here throws.
import * as fs from 'node:fs';
import path from 'node:path';
import { processProbe } from '../../../../.agents/conventions/tools/lib/owned.mjs';

const KINDS = /^\.(stamp\.lock\.(?:link|retire)|exec-probe-record\.tmp)-(\d+)$/;

/** `{ removed: [path], committing: [path], errors: [text] }`; `dirs` may repeat a folder (it is looked at once). `probe` and `io` are the test seams. */
export function retireStartupStrays({ dirs, probe = processProbe, io = fs }) {
  const report = { removed: [], committing: [], errors: [] };
  [...new Set(dirs)].forEach(dir => {
    let names;
    try {
      names = io.readdirSync(dir);
    } catch (error) {
      if (error.code !== 'ENOENT') report.errors.push(`${dir}: ${error.code ?? error.message}`);
      return;
    }
    names.map(name => ({ name, found: KINDS.exec(name) })).filter(({ found }) => found !== null && probe(Number(found[2])).state === 'dead').forEach(({ name, found }) => {
      const file = path.join(dir, name);
      try {
        const committing = found[1].endsWith('retire') && /\ncommitting\s*$/.test(io.readFileSync(file, 'utf8'));
        io.rmSync(file, { force: true });
        report.removed.push(file);
        if (committing) report.committing.push(file);
      } catch (error) {
        report.errors.push(`${file}: ${error.code ?? error.message}`);
      }
    });
  });
  return report;
}
