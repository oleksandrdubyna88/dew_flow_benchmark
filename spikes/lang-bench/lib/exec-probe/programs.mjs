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
import { boxEnv } from '../exec-layer/env.mjs';

const PROGRAMS = fileURLToPath(new URL('./programs/', import.meta.url));
const BUILD_MS = 180_000;
export const STACKS = Object.freeze(['csharp', 'node']);
export const MODES = Object.freeze(['report', 'culture', 'churn', 'overlimit']);
const COMMAND = Object.freeze({ csharp: mode => `exec dotnet bin/mem.dll ${mode}`, node: mode => `exec node mem.mjs ${mode}` });
const BUILD_ARGS = Object.freeze(['dotnet', 'build', 'mem.csproj', '-c', 'Release', '-o', 'bin', '-m:4', '--nologo', '-v', 'q']);

/** `ok(folder)` holding the built C# program (`bin/mem.dll`), made in an owned folder of `scope`, or `fail(reason)`. */
export async function buildCsharp({ owned, scope, dirs }) {
  const folder = owned.ownedDir(scope, 'probebuild');
  cpSync(path.join(PROGRAMS, 'csharp'), folder, { recursive: true });
  const extra = { NUGET_PACKAGES: '/tmp/pk', DOTNET_CLI_USE_MSBUILD_SERVER: '0', MSBUILDDISABLENODEREUSE: '1', UseSharedCompilation: 'false' };
  const box = boxArgv({ view: folder, dirs, env: boxEnv({ dirs, extra }), bwrap: findOnPath('bwrap') ?? 'bwrap' });
  const built = await runBounded(box[0], [...box.slice(1), ...BUILD_ARGS], { timeoutMs: BUILD_MS, env: { PATH: '/usr/bin:/bin' } });
  return built.ok && built.value.code === 0 ? ok(folder) : fail(`the C# probe program did not build in the box: ${built.ok ? built.value.stdout + built.value.stderr : built.reason}`);
}

/** An owned tree holding the program of `stack` and a `run.sh` that ends with `exec` and runs it in `mode`. `csharpBuild` is what `buildCsharp` returned. */
export function programTree({ owned, scope, stack, mode, csharpBuild }) {
  const tree = owned.ownedDir(scope, `probe${stack}`);
  if (stack === 'csharp') cpSync(path.join(csharpBuild, 'bin'), path.join(tree, 'bin'), { recursive: true });
  else cpSync(path.join(PROGRAMS, 'node', 'mem.mjs'), path.join(tree, 'mem.mjs'));
  writeFileSync(path.join(tree, 'run.sh'), `#!/bin/bash\n${COMMAND[stack](mode)}\n`);
  chmodSync(path.join(tree, 'run.sh'), 0o755);
  return tree;
}
