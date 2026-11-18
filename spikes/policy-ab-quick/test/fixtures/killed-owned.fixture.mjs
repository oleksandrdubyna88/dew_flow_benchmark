// A test that makes an owned folder, prints its path and kills its own process with SIGKILL: what a killed policy-ab-quick test leaves behind. `start-sweep.test.mjs` runs it under a private TMPDIR (it is not named `*.test.mjs`, so no suite runs it itself).
import { test } from 'node:test';
import { ownedFor } from '../../../../.agents/conventions/tools/lib/owned.mjs';

const owned = ownedFor({ token: 'bench' });

test('killed', t => {
  const dir = owned.ownedDir(t, 'pabkilled');
  process.stdout.write(`KILLED_FOLDER=${dir}\n`);
  process.kill(process.pid, 'SIGKILL');
});
