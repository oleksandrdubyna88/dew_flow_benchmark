// E1.S8b probe: the toy programs the probe and T-L43 run through the layer — one per stack, one `mode` argument (report, culture, churn, overlimit) — and the trees that hold them.
// The C# program is built INSIDE the box with the pinned SDK bound read-only: an unboxed SDK build with `DOTNET_ROOT` on the sealed SDK writes a `metadata` folder into the SDK's root
// (2026-11-02) and breaks the toolchain store's seal. No package is needed, so no network is. The build is bounded to 4 workers (`-m:4`, common.local-cpu-budget).
import { chmodSync, cpSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { findOnPath } from '../../../minigit-bench/lib/find-on-path.mjs';
import { runBounded } from '../../../minigit-bench/lib/run.mjs';
import { fail, ok } from '../../../minigit-bench/lib/result.mjs';
import { boxArgv } from '../exec-layer/box.mjs';
import { trackChild } from '../exec-layer/cleanup.mjs';
import { assertRunning, ownedDirRunning } from '../exec-layer/stopping.mjs';
import { assertNoCaseKeys, boxEnv } from '../exec-layer/env.mjs';
import { factsScript } from './facts.mjs';

const PROGRAMS = fileURLToPath(new URL('./programs/', import.meta.url));
const BUILD_MS = 180_000;
export const STACKS = Object.freeze(['csharp', 'node']);
export const MODES = Object.freeze(['report', 'culture', 'churn', 'overlimit', 'medium-buffers', 'medium-objects', 'near-limit', 'objects-70', 'mixed', 'overlimit-objects']);
const COMMAND = Object.freeze({ csharp: mode => `exec dotnet bin/mem.dll ${mode}`, node: mode => `exec node mem.mjs ${mode}` });
const buildArgs = marker => Object.freeze(['dotnet', 'build', 'mem.csproj', '-c', 'Release', '-o', 'bin', '-m:4', '--nologo', '-v', 'q', `-p:BenchBuildMarker=${marker}`]); // the owned marker in the argv: a test counts the build processes by it (B1)

const BUILD_EXTRA = Object.freeze({ NUGET_PACKAGES: '/tmp/pk', DOTNET_CLI_USE_MSBUILD_SERVER: '0', MSBUILDDISABLENODEREUSE: '1', UseSharedCompilation: 'false' });

/** The build box's environment: the base for no stack in particular, the build's own extras, and never one of D55's per-case heap keys (T-L14, §4.3) — asserted where it is made. */
export function buildBoxEnv({ dirs }) {
  const env = boxEnv({ dirs, extra: BUILD_EXTRA });
  assertNoCaseKeys(env, 'the build box');
  return env;
}

/** `ok(folder)` holding the built C# program (`bin/mem.dll`), made in an owned folder of `scope`, or `fail(reason)`. */
export async function buildCsharp({ owned, scope, dirs }) {
  try {
    assertRunning('a build'); // L6: a stop before the build begins starts none and makes no folder
  } catch (error) {
    return fail(error.message);
  }
  const folder = ownedDirRunning(owned, scope, 'probebuild');
  cpSync(path.join(PROGRAMS, 'csharp'), folder, { recursive: true });
  const box = boxArgv({ view: folder, dirs, env: buildBoxEnv({ dirs }), bwrap: findOnPath('bwrap') ?? 'bwrap', prog: null });
  const built = await runBounded(box[0], [...box.slice(1), ...buildArgs(path.basename(folder))], { timeoutMs: BUILD_MS, env: { PATH: '/usr/bin:/bin' }, onChild: trackChild });
  return built.ok && built.value.code === 0 ? ok(folder) : fail(`the C# probe program did not build in the box: ${built.ok ? built.value.stdout + built.value.stderr : built.reason}`);
}

/** An owned tree holding the program of `stack` and a `run.sh` that ends with `exec` and runs it in `mode` (`mode` may carry its arguments: `medium-buffers 256`). `csharpBuild` is what `buildCsharp` returned. */
export function programTree({ owned, scope, stack, mode, csharpBuild }) {
  assertRunning('a program tree');
  const tree = ownedDirRunning(owned, scope, `probe${stack}`);
  if (stack === 'csharp') cpSync(path.join(csharpBuild, 'bin'), path.join(tree, 'bin'), { recursive: true });
  else cpSync(path.join(PROGRAMS, 'node', 'mem.mjs'), path.join(tree, 'mem.mjs'));
  writeFileSync(path.join(tree, 'run.sh'), `#!/bin/bash\n${COMMAND[stack](mode)}\n`);
  chmodSync(path.join(tree, 'run.sh'), 0o755);
  return tree;
}

/**
 * The probe's own tree: the facts script (with the `$HOME` sentinel path), the loopback server, the workload of line (f) and both stacks' programs. One tree, one view per probe case;
 * the case's program is whatever argv the probe launches.
 */
export function probeTree({ owned, scope, sentinel, csharpBuild }) {
  assertRunning('the probe tree');
  const tree = ownedDirRunning(owned, scope, 'probetree');
  cpSync(path.join(PROGRAMS, 'python'), tree, { recursive: true });
  cpSync(path.join(PROGRAMS, 'node', 'mem.mjs'), path.join(tree, 'mem.mjs'));
  cpSync(path.join(csharpBuild, 'bin'), path.join(tree, 'bin'), { recursive: true });
  writeFileSync(path.join(tree, 'facts.sh'), factsScript(sentinel));
  return tree;
}
