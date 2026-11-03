// The probe's empty-program tree: a `run.sh` that only `cat`s, and one samples pair (the launch-time control). Made through the stop guard (S4).
import { writeFileSync } from 'node:fs';
import path from 'node:path';
import { ownedDirRunning } from '../exec-layer/stopping.mjs';

/** `{ tree, samplesDir }` in two owned folders of `scope`. */
export function emptyProgramTree(owned, scope) {
  const tree = ownedDirRunning(owned, scope, 'emptytree');
  const samplesDir = ownedDirRunning(owned, scope, 'emptysamples');
  writeFileSync(path.join(tree, 'run.sh'), '#!/bin/bash\nexec cat\n', { mode: 0o755 });
  writeFileSync(path.join(samplesDir, 'p1.in'), 'hello\n');
  writeFileSync(path.join(samplesDir, 'p1.out'), 'hello\n');
  return { tree, samplesDir };
}
