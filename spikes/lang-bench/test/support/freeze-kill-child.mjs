// A freeze verb in a child process that kills itself at a planted point, as a real kill would: SIGKILL, so no `finally` runs and nothing is cleaned up. `argv[0]` is the point (`before-rename`, the only one the
// writers have: the temporary file is complete and fsynced, the rename has not happened); the rest is the verb's command line. The date is fixed, as the fixtures' `now` is.
import { runFreeze } from '../../lib/freeze-cli.mjs';

const [point, ...argv] = process.argv.slice(2);
const kill = () => process.kill(process.pid, 'SIGKILL');
const io = point === 'before-rename' ? { renameSync: kill } : {};
const result = await runFreeze(argv, { io, now: () => new Date('2026-12-01T10:00:00Z') });
process.stdout.write(result.stdout);
process.stderr.write(result.stderr);
process.exit(result.exit);
