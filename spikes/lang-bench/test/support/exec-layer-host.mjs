// Shared plumbing of the execution layer's tests (E1.S8b): what the host must have, the pinned toolchain directories, and one way to run a child with a ceiling.
// A test that needs a host tool calls `needs` first; a host that lacks it skips with the reason, and under BENCH_REQUIRE_SANDBOX=1 the same case fails (nothing is skipped).
import { existsSync, readFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { findOnPath } from '../../../minigit-bench/lib/find-on-path.mjs';
import { runBounded } from '../../../minigit-bench/lib/run.mjs';
import { storeDirs } from '../../lib/preflight-box.mjs';
import { loadStacksLang } from '../../lib/toolchain-pins.mjs';

export const spikeDir = fileURLToPath(new URL('../..', import.meta.url));
export const runnerDir = path.join(spikeDir, 'runner');
export const fixturesDir = path.join(spikeDir, 'test', 'fixtures');
const stacks = loadStacksLang(JSON.parse(readFileSync(path.join(spikeDir, 'inputs', 'stacks-lang.json'), 'utf8'))).value;

/** The pinned toolchain directories: `{ dotnetSdk, node, typescript }`, full paths under the home (read-only in the box, never the home itself). */
export const toolchains = storeDirs(stacks, path.join(os.homedir(), ...stacks.storeRoot));

const TOOLS = Object.freeze(['bwrap', 'nsenter', 'unshare', 'ip', 'systemd-run', 'systemctl', 'python3']);

function lacking() {
  const missing = TOOLS.filter(tool => findOnPath(tool, `${process.env.PATH}:/usr/sbin:/sbin`) === null);
  if (missing.length > 0) return `${missing.join(', ')} not found on PATH`;
  const bus = `/run/user/${process.getuid()}/bus`;
  if (!existsSync(bus)) return `no user bus at ${bus}: a user scope cannot be started`;
  const absent = Object.values(toolchains).filter(dir => !existsSync(dir));
  return absent.length > 0 ? `the pinned toolchain ${absent.join(', ')} is not installed` : null;
}

/** True when the host can run the layer. Otherwise the test is skipped with the reason — or fails under BENCH_REQUIRE_SANDBOX=1. Use as `if (!needs(t)) return;`. */
export function needs(t, extra = null) {
  const why = lacking() ?? extra;
  if (why === null) return true;
  if (process.env.BENCH_REQUIRE_SANDBOX === '1') throw new Error(`BENCH_REQUIRE_SANDBOX=1 and ${why}`);
  t.skip(why);
  return false;
}

/** `{ code, stdout, stderr }` of one child run with a ceiling; a child that could not start throws (a host fault, never a verdict). */
export async function sh(exe, args, { timeoutMs = 60_000, env = { PATH: '/usr/sbin:/usr/bin:/bin', XDG_RUNTIME_DIR: `/run/user/${process.getuid()}` }, cwd, input } = {}) {
  const run = await runBounded(exe, args, { timeoutMs, env, cwd, input });
  if (!run.ok) throw new Error(run.reason);
  return run.value;
}
