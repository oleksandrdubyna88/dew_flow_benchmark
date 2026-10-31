// The fake HOME of the runner tests (E3b review N18). Import it BEFORE anything of the runner: the first import of a test file that calls `main`, `buildRun`, `runLoop` or `rehearseV2`.
// A test that calls the runner without a seam reads `os.homedir()` (the token file, the toolchain and project folders, the fetched-inputs cache); under a fake HOME and a fake
// XDG_CONFIG_HOME that is an empty directory of this process, never the operator's. The helper also sets BENCH_UNDER_TEST so the token reader's guard is on even for a file run directly.
//
//   - HOME, USERPROFILE and XDG_CONFIG_HOME point into one `testsToDelete-` directory, removed when the process exits (common.test-leftovers).
//   - The one thing it links in from the real home is the PUBLIC fetched-inputs cache (`.cache/minigit-bench/inputs`: the fork's task inputs, verified by hash at every use), so a
//     test does not clone it again; nothing under `.config` or `rsd` is linked.
import { existsSync, mkdirSync, mkdtempSync, rmSync, symlinkSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';

/** The operator's real home, read from the account database (it ignores $HOME): what a test must never touch. */
export const realHome = os.userInfo().homedir;

const dir = mkdtempSync(path.join(os.tmpdir(), 'testsToDelete-mgb-fakehome-'));
mkdirSync(path.join(dir, '.config'), { recursive: true, mode: 0o700 });
const realInputsCache = path.join(realHome, '.cache', 'minigit-bench', 'inputs');
if (existsSync(realInputsCache)) {
  mkdirSync(path.join(dir, '.cache', 'minigit-bench'), { recursive: true });
  symlinkSync(realInputsCache, path.join(dir, '.cache', 'minigit-bench', 'inputs'));
}
process.env.HOME = dir;
process.env.USERPROFILE = dir;
process.env.XDG_CONFIG_HOME = path.join(dir, '.config');
process.env.BENCH_UNDER_TEST = '1';
process.once('exit', () => { try { rmSync(dir, { recursive: true, force: true }); } catch { /* a leftover carries the testsToDelete- prefix */ } });

export const fakeHome = dir;
