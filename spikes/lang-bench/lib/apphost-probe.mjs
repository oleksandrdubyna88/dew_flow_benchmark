// E1.S5: the apphost probe. A program launched through its own apphost executable does not look at PATH: it resolves a dotnet root from DOTNET_ROOT,
// then the machine's install_location file, then the default locations. A box whose PATH is clean can still run such a program on the distribution's runtime.
// The probe builds a tiny console app with the PINNED SDK inside the box (offline: the SDK bundles the targeting and apphost packs), runs its apphost with
// COREHOST_TRACE=1 and reads which dotnet root it used. Any root other than the pinned directory is a problem.
import { mkdirSync, writeFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { findOnPath } from '../../minigit-bench/lib/find-on-path.mjs';
import { boxArgv, runInBox } from './preflight-box.mjs';

const BOX_DIR = '/tmp/apphost-probe';
const BUILD_MS = 180_000;
const ROOT_LINE = /Using dotnet root path \[([^\]\n]*)\]/g;
const CSPROJ = `<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <UseAppHost>true</UseAppHost>
    <AssemblyName>probe</AssemblyName>
    <NuGetAudit>false</NuGetAudit>
  </PropertyGroup>
</Project>
`;
const PROGRAM = 'System.Console.WriteLine("ok");\n';

/** Problems in an apphost's COREHOST_TRACE text (stderr), against the pinned dotnet directory. `null` trace: the probe did not run. */
export function apphostRootProblems(trace, pinnedDir) {
  if (trace === null || trace === undefined) return ['the apphost probe did not run (or failed) inside the box, so the dotnet root an apphost-launched program uses is unknown'];
  const roots = [...trace.matchAll(ROOT_LINE)].map(m => m[1]);
  if (roots.length === 0) return ['the apphost probe printed no dotnet root (COREHOST_TRACE had no "Using dotnet root path" line)'];
  return [...new Set(roots)].filter(root => path.resolve(root) !== path.resolve(pinnedDir))
    .map(root => `an apphost-launched program used the dotnet root ${root}, the pin is ${pinnedDir}: the distribution's runtime is reachable (hide /usr/lib/dotnet and /etc/dotnet, set DOTNET_ROOT)`);
}

const tail = text => (text ?? '').trim().slice(-300);

/**
 * `runApphostProbe({ stacks, dirs, boxPath, operatorHome, protect })` -> `{ problems, root }`. `root` is the dotnet root the apphost used (`null` when unread).
 * `protect: false` runs the same probe in a box WITHOUT the hiding and DOTNET_ROOT (a test's way to show the open road).
 */
export async function runApphostProbe({ stacks, dirs, boxPath, operatorHome = os.homedir(), protect = true, hostPath = process.env.PATH ?? '', onWorkFolder = () => {} }) {
  void stacks;
  const bwrap = findOnPath('bwrap', hostPath);
  if (bwrap === null) return { problems: ['bwrap is not installed: the apphost probe cannot run'], root: null };
  const owned = await ownedHelper();
  if (!owned.ok) return { problems: [owned.reason], root: null };
  const scope = owned.scope;
  try {
    // From the owned-name helper (`common.test-leftovers`): the claim beside the folder lets the next start-of-run sweep collect it if this run is killed during the build.
    const work = owned.helper.ownedDir(scope, 'apphost');
    onWorkFolder(work);
    mkdirSync(path.join(work, 'out'));
    writeFileSync(path.join(work, 'probe.csproj'), CSPROJ);
    writeFileSync(path.join(work, 'Program.cs'), PROGRAM);
    return await buildAndRun({ work, dirs, boxPath, operatorHome, protect, bwrap });
  } finally {
    await scope.close();
  }
}

const OWNED_URL = new URL('../../../.agents/conventions/tools/lib/owned.mjs', import.meta.url);

async function ownedHelper() {
  try {
    const owned = await import(OWNED_URL.href);
    return { ok: true, helper: owned.ownedFor({ token: 'bench' }), scope: owned.cleanupScope() };
  } catch (e) {
    return { ok: false, reason: `the conventions mount's owned-name helper is needed for the probe's scratch folder and cannot be loaded (${e.code ?? e.message}): ${fileURLToPath(OWNED_URL)}` };
  }
}

async function buildAndRun({ work, dirs, boxPath, operatorHome, protect, bwrap }) {
  const common = { bwrap, dirs, boxPath, operatorHome, protect, rwBinds: [{ host: work, box: BOX_DIR }], workdir: BOX_DIR };
  const build = await runInBox(boxArgv({ ...common, extraEnv: { MSBUILDDISABLENODEREUSE: '1', UseSharedCompilation: 'false' } }), ['dotnet', 'build', 'probe.csproj', '-c', 'Release', '-o', `${BOX_DIR}/out`, '--nologo', '-v', 'q'], { timeoutMs: BUILD_MS });
  if (build === null || build.code !== 0) return { problems: [`the apphost probe did not build with the pinned SDK: ${tail(build?.stdout || build?.stderr)}`], root: null };
  const run = await runInBox(boxArgv({ ...common, extraEnv: { COREHOST_TRACE: '1' } }), [`${BOX_DIR}/out/probe`]);
  const roots = [...(run?.stderr ?? '').matchAll(ROOT_LINE)].map(m => m[1]);
  return { problems: apphostRootProblems(run === null ? null : run.stderr, dirs.dotnetSdk), root: roots.at(-1) ?? null };
}
