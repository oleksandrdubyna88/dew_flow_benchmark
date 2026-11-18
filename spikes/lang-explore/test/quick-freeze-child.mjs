// A freeze verb run as its own process that kills itself at a planted point (F2.S2 (d), the interruption tests): `node quick-freeze-child.mjs '<json>'`, the json `{ argv, hook, at }`. The verb runs through `main` with the invented pins
// and a seam `hooks[hook](i)` that sends SIGKILL to this process when `i` equals `at` (or at the first call when `at` is absent), so the owner of what it was making is truly gone. Not a test file.
import { ok } from '../../minigit-bench/lib/result.mjs';
import { main } from '../explore.mjs';
import { HOME, pinsFor } from './quick-freeze-fixture.mjs';

const { argv, hook, at } = JSON.parse(process.argv[2]);
const kill = i => { if (at === undefined || i === at) process.kill(process.pid, 'SIGKILL'); };
process.exitCode = await main(argv, { home: HOME, say: () => {}, exploreFn: () => ok(pinsFor()), workspaces: { hooks: { [hook]: kill } } });
