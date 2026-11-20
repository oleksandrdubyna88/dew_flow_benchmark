// The child process of the take's tests (F3.S1 (b), PR 5, coai code round 2): `node stage-take-child.mjs accept <repo> <12 hex> [<kill point>]` runs `acceptHarness` and kills itself with SIGKILL when its seam names the point (no
// handler runs, no `finally`: a kill); `node stage-take-child.mjs sync <repo>` runs `stageSync`. Either prints the answer as one JSON line. A test sets PATH to put its own `git` in front, which a child can have and the test's process cannot.
import { acceptHarness } from '../lib/stage-accept.mjs';
import { stageSync } from '../lib/stage-branch.mjs';

const [, , mode, repo, hex, point] = process.argv;
const seam = async step => {
  if (step !== point) return;
  process.kill(process.pid, 'SIGKILL');
  await new Promise(() => {});
};
const runs = { accept: () => acceptHarness({ repo, hex, seam }), sync: () => stageSync({ repo }) };
console.log(JSON.stringify(await runs[mode]()));
