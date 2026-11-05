// The child process of the die-with-parent case: it starts a build that hangs in the build box of a workspace and waits for it. The test kills THIS process alone with SIGKILL (no clean-up can run in it) and looks
// at what is left of the box. `node hung-build-child.mjs <workspace> <yes|no>`: the second argument is whether the box keeps `--die-with-parent`.
import { inBox } from './workspace-fixtures.mjs';

const [view, keep] = process.argv.slice(2);
await inBox(view, 'csharp', ['bash', 'build.sh'], { timeoutMs: 80_000, ...(keep === 'no' ? { dieWithParent: false } : {}) });
