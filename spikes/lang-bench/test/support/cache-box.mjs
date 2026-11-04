// E1.S4: what the network test file shares. `realGate(t)` is the one gate of that file (the real build runs only under BENCH_REQUIRE_SANDBOX=1, and there a missing bwrap or toolchain is a failure, never a skip);
// `realBuild()` runs `make-snapshots.mjs` ONCE for both stacks into an owned folder (memoised: the first case past the gate starts it) and hands every case the same record, caches folder and egress lines.
// The build never writes `~/rsd/lang-v2/caches` or `inputs/cache-snapshots-lang.json`: every path of it is a flag that points into the owned folder.
//
// LANG_V2_REUSE_REAL=<folder> (a development loop only, never in a completion run): reuse a folder a finished real build left, instead of building again. The folder is read as it is: its `rec.json`, `caches/`, `run.json`.
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { findOnPath } from '../../../minigit-bench/lib/find-on-path.mjs';
import { makeWritable } from '../../../minigit-bench/lib/cache-snapshot.mjs';
import { runBounded } from '../../../minigit-bench/lib/run.mjs';
import { storeDirs } from '../../lib/preflight-box.mjs';
import { loadStacksLang } from '../../lib/toolchain-pins.mjs';
import { ownedApi } from '../toolchain-fixtures.mjs';

const SPIKE = fileURLToPath(new URL('../..', import.meta.url));
export const REAL_SKIP = 'the real snapshot build (network through the egress proxy, about 10-20 min) runs only under BENCH_REQUIRE_SANDBOX=1';
export const BUILD_TIMEOUT_MS = 40 * 60_000;
/** The parent of every folder a test of this file makes: under the home (the box replaces /tmp), owner only. */
export const TEST_ROOT = path.join(os.homedir(), '.cache', 'lang-v2', 't');
export const ensureTestRoot = () => { mkdirSync(TEST_ROOT, { recursive: true, mode: 0o700 }); return TEST_ROOT; };

/** The checked pins and the store directories of E1.S5's toolchains. */
export function toolchains() {
  const stacks = loadStacksLang(JSON.parse(readFileSync(path.join(SPIKE, 'inputs', 'stacks-lang.json'), 'utf8'))).value;
  return { stacks, dirs: storeDirs(stacks, path.join(os.homedir(), ...stacks.storeRoot)) };
}

const missingToolchain = dirs => [dirs.dotnetSdk, dirs.node, dirs.typescript].find(dir => !existsSync(dir));

/** `true` when the case may run. Without BENCH_REQUIRE_SANDBOX=1 it skips with the reason; with it, a missing bwrap or pinned toolchain throws (the completion run cannot pass on a host that lacks them). */
export function realGate(t) {
  if (process.env.BENCH_REQUIRE_SANDBOX !== '1') { t.skip(REAL_SKIP); return false; }
  if (findOnPath('bwrap') === null) throw new Error('BENCH_REQUIRE_SANDBOX=1 and bwrap is not on PATH: the snapshot cannot be built or used here');
  const missing = missingToolchain(toolchains().dirs);
  if (missing !== undefined) throw new Error(`BENCH_REQUIRE_SANDBOX=1 and the pinned toolchain ${missing} is missing: node spikes/lang-bench/install-toolchains.mjs`);
  return true;
}

const scope = ownedApi?.cleanupScope();
let pending;

const parseLines = (stdout, prefix) => stdout.split('\n').filter(l => l.startsWith(prefix)).map(l => ({ stack: l.slice(prefix.length, l.indexOf(':')), text: l.slice(l.indexOf(':') + 2) }));

/** What the real build left, read from its folder: `{ root, caches, record, run: { code, stdout, stderr, ms } }`. */
function readReal(root) {
  const run = JSON.parse(readFileSync(path.join(root, 'run.json'), 'utf8'));
  const record = existsSync(path.join(root, 'rec.json')) ? JSON.parse(readFileSync(path.join(root, 'rec.json'), 'utf8')) : {};
  const egress = Object.fromEntries(parseLines(run.stdout, 'egress ').map(l => [l.stack, JSON.parse(l.text)]));
  return { root, caches: path.join(root, 'caches'), record, run, egress };
}

async function build() {
  if (process.env.LANG_V2_REUSE_REAL) return readReal(process.env.LANG_V2_REUSE_REAL);
  const root = ownedApi.ownedDir(scope, 'real', { parent: ensureTestRoot() });
  scope.after(() => makeWritable(root)); // after ownedDir: cleanupScope runs last-registered-first, so the read-only snapshots are writable before the folder goes
  const started = Date.now();
  const args = [path.join(SPIKE, 'make-snapshots.mjs'), '--caches', path.join(root, 'caches'), '--record', path.join(root, 'rec.json'), '--gate-lock', path.join(root, 'gate.lock'), '--work', TEST_ROOT];
  const r = await runBounded(process.execPath, args, { timeoutMs: BUILD_TIMEOUT_MS, maxBytes: 64 * 1024 * 1024 });
  const run = r.ok ? { code: r.value.code, stdout: r.value.stdout, stderr: r.value.stderr, timedOut: r.value.timedOut, ms: Date.now() - started } : { code: -1, stdout: '', stderr: r.reason, ms: Date.now() - started };
  writeFileSync(path.join(root, 'run.json'), JSON.stringify(run));
  return readReal(root);
}

/** The shared real build: one run of the command for the whole file. */
export const realBuild = () => { pending ??= build(); return pending; };

/** Close the file's owned folders (the real build's included). Call it in the file's `after`. */
export const closeReal = () => scope?.close();

/** `{ dir, parts }` of a stack's registered snapshot in the real build: what `seedCaches` takes. */
export const snapshotOf = (real, stack) => ({ dir: path.join(real.caches, real.record[stack].dirName), parts: real.record[stack].parts });
