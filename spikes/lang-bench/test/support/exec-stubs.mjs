// The toy programs of the execution layer's tests (E1.S8b): one ping -> PING server per stack under test/fixtures/exec-stubs/<stack>/, each with a `run.sh` that ends with `exec`.
// A stub's view is a fresh owned folder holding a copy of the stub; the C# stub is built offline from its project (no package: the SDK's own packs are enough).
import { cpSync, existsSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { boxArgv } from '../../lib/exec-layer/box.mjs';
import { boxEnv } from '../../lib/exec-layer/env.mjs';
import { fixturesDir, sh, toolchains } from './exec-layer-host.mjs';
import { owned } from './owned.mjs';

export const STACKS = Object.freeze(['python', 'node', 'csharp']);
/** What `comm` says the program is, per stack: the stub's `exec` makes the program the runtime itself, with no bash above it. */
export const PROGRAM_COMM = Object.freeze({ python: /^python3/, node: /^node/, csharp: /^dotnet/ });

/**
 * Build the C# stub INSIDE the box: the pinned SDK is bound read-only there. Run unboxed, `dotnet build` with `DOTNET_ROOT` on the sealed SDK made a `metadata` folder in the SDK's own
 * root (2026-11-02), which breaks the toolchain store's seal and its recorded tree hash. The box's `/tmp` takes the NuGet and CLI folders; the view takes `bin/` and `obj/`.
 */
async function buildCsharp(view) {
  const extra = { NUGET_PACKAGES: '/tmp/pk', DOTNET_CLI_USE_MSBUILD_SERVER: '0', MSBUILDDISABLENODEREUSE: '1', UseSharedCompilation: 'false' };
  const box = boxArgv({ view, dirs: toolchains, env: boxEnv({ dirs: toolchains, extra }) });
  const built = await sh(box[0], [...box.slice(1), 'dotnet', 'build', 'server.csproj', '-c', 'Release', '-o', 'bin', '--nologo', '-v', 'q'], { timeoutMs: 180_000 });
  if (built.code !== 0) throw new Error(`the C# stub did not build offline: ${built.stdout}${built.stderr}`);
}

/** A fresh owned folder holding the stub of `stack` (the C# one built): the view a program of that stack starts in. */
export async function stubView(t, stack) {
  const view = owned.ownedDir(t, `stub${stack}`);
  cpSync(path.join(fixturesDir, 'exec-stubs', stack), view, { recursive: true });
  if (stack === 'csharp') await buildCsharp(view);
  return view;
}

/** The port a stub wrote to `ready.port` in `view`, or null when it did not within `ms`. */
export async function readyPort(view, ms = 15_000) {
  const file = path.join(view, 'ready.port');
  for (let waited = 0; waited < ms; waited += 50) {
    if (existsSync(file)) return Number(readFileSync(file, 'utf8'));
    await new Promise(resolve => setTimeout(resolve, 50));
  }
  return null;
}

/** The driver's side of ping -> PING: a python client run by `driverArgv` (a prefix such as `nsenter … --preserve-credentials`); the reply, or the error text. */
export async function ping(driverArgv, port) {
  const client = `import socket,sys\ntry:\n s=socket.create_connection(("127.0.0.1",${port}),timeout=5);s.sendall(b"ping\\n");print(s.makefile().readline().strip())\nexcept OSError as e:\n print("ERR",e.__class__.__name__)`;
  const run = await sh(driverArgv[0], [...driverArgv.slice(1), 'python3', '-I', '-c', client]);
  return run.stdout.trim();
}
