// E1.S4: how a build runs from a snapshot ALONE. The box options of both stacks (one function, so the build and every offline box cannot drift), the exact offline npm invocation, the offline NuGet source, and the
// check of what a restore really used against the registered manifest. `openBox` (the fresh box that holds only a seeded snapshot) is at the end; the rest is pure.
//
// Why a fresh box, and why the flags: a registry that the box can reach fills any hole in the snapshot silently, so a missing package would only show on the first run without network. Copying the cache is not
// enough either: npm still asks the registry unless it is told not to (`--offline`, `npm_config_offline`, `offline=true`), and NuGet adds the user-level nuget.org source back unless every inherited source is cleared.
// The two not-in-cache errors that name the package are npm's `ENOTCACHED` and NuGet's `NU1101`; a generic network error is neither.
import { randomBytes } from 'node:crypto';
import { appendFileSync, copyFileSync, existsSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { inBox, seedCaches } from '../../minigit-bench/lib/cache-snapshot.mjs';
import { egressProxy, egressSummary } from '../../minigit-bench/lib/egress.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { sessionLayout } from '../../minigit-bench/lib/sandbox.mjs';
import { DISTRIBUTION_DOTNET_PATHS } from './preflight-box.mjs';

/**
 * The box fields of every box that runs a pinned-toolchain program, from `storeDirs(...)` of E1.S5: the SDK and TypeScript directories bound read-only (Node is `nodeDir: dirs.node`, which the agent box binds itself),
 * the SDK and TypeScript on PATH, the distribution's dotnet hidden by an empty tmpfs, and `DOTNET_ROOT` set to the pinned SDK (an apphost does not use PATH, so hiding alone leaves a road to `/usr/lib/dotnet`).
 * -> `{ toolchains, dotnetDir, tsDir, hide, env }`, the `box` of v1's `makeSnapshot` and of `inBox`.
 */
export const boxOptionsFor = dirs => Object.freeze({
  toolchains: Object.freeze([dirs.dotnetSdk, dirs.typescript]),
  dotnetDir: dirs.dotnetSdk,
  tsDir: dirs.typescript,
  hide: DISTRIBUTION_DOTNET_PATHS,
  env: Object.freeze({ DOTNET_ROOT: dirs.dotnetSdk }),
});

// ---- npm ------------------------------------------------------------------------------------------------------------------------

/** The `.npmrc` of an offline workspace (parent D5: every Node workspace's `.npmrc` sets it; S3 reuses it). */
export const NPMRC_OFFLINE = 'offline=true\n';
/** The extra environment of an offline npm run. The cache is `npm_config_cache`, which v1's base environment already points at the seeded `box/npm`. */
export const NPM_OFFLINE_ENV = Object.freeze({ npm_config_offline: 'true', npm_config_update_notifier: 'false', npm_config_logs_max: '0' });
/** The argv of an offline install of `specs` (`name@version` each). */
export const npmOfflineArgv = specs => ['npm', 'install', '--offline', '--no-audit', '--no-fund', '--no-update-notifier', ...specs];

// ---- NuGet ----------------------------------------------------------------------------------------------------------------------

const SOURCE_PATH = /^[A-Za-z0-9/._-]+$/;

const nugetConfigText = source => `<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="offline" value="${source}" />
  </packageSources>
  <fallbackPackageFolders>
    <clear />
  </fallbackPackageFolders>
</configuration>
`;

/**
 * The `nuget.config` of an offline workspace: every inherited source and fallback folder cleared, exactly one local source (the seeded global packages folder). -> `ok(text)` | `fail` for a path outside
 * `[A-Za-z0-9/._-]` (it goes into XML). S3 reuses this function for the workspaces' `nuget.config`.
 */
export function offlineNugetConfig(source) {
  if (typeof source !== 'string' || !SOURCE_PATH.test(source)) return fail(`the offline NuGet source path ${JSON.stringify(source)} must match ${SOURCE_PATH}: it is written into XML`);
  return ok(nugetConfigText(source));
}

/** The source of a workspace's `nuget.config`: NuGet expands `%NAME%` from the environment on Linux, so the text is the same in every session and holds no path (the box sets `NUGET_PACKAGES`). */
export const WORKSPACE_NUGET_SOURCE = '%NUGET_PACKAGES%';

/** The `nuget.config` of every C# workspace template: the offline config over `%NUGET_PACKAGES%`. A fixed text, so it needs no check of a path. */
export const workspaceNugetConfig = () => nugetConfigText(WORKSPACE_NUGET_SOURCE);

// ---- what a restore used, against the manifest -----------------------------------------------------------------------------------

const unique = list => [...new Set(list)].sort();
const afterLastModules = key => key.slice(key.lastIndexOf('node_modules/') + 'node_modules/'.length);
const isInstalledPackage = ([key, entry]) => key.startsWith('node_modules/') && entry.link !== true && typeof entry.version === 'string';

/** `name@version` of every `packages["node_modules/..."]` entry of a `package-lock.json` (a nested one by its own name; the root and a link are not packages). */
export const usedNpm = lockJson => unique(Object.entries(lockJson.packages ?? {}).filter(isInstalledPackage).map(([key, entry]) => `${afterLastModules(key)}@${entry.version}`));

/** Lower-case `name@version` of every `libraries` entry of `obj/project.assets.json` whose `type` is `package` (a project reference is not a package). */
export const usedNuget = assetsJson => unique(Object.entries(assetsJson.libraries ?? {}).filter(([, lib]) => lib.type === 'package').map(([key]) => key.replace('/', '@').toLowerCase()));

const manifestId = p => (p.ecosystem === 'nuget' ? `${p.name}@${p.version}`.toLowerCase() : `${p.name}@${p.version}`);

/** The used `name@version` that the registered manifest does not list (of `ecosystem` when given). A non-empty answer is a failure that names each. */
export function outsideManifest(used, manifest, ecosystem = undefined) {
  const listed = new Set(manifest.filter(p => ecosystem === undefined || p.ecosystem === ecosystem).map(manifestId));
  return used.filter(id => !listed.has(id));
}

// ---- the fresh box ---------------------------------------------------------------------------------------------------------------

const V1_SPIKE = fileURLToPath(new URL('../../minigit-bench/', import.meta.url));
/** The environment every C# box of this story has besides the box's own: the build servers off, the certificate revocation check offline (the proxy carries CONNECT to :443 only), no first-run noise. */
const STACK_ENV = Object.freeze({
  csharp: Object.freeze({ DOTNET_CLI_USE_MSBUILD_SERVER: '0', MSBUILDDISABLENODEREUSE: '1', UseSharedCompilation: 'false', NUGET_CERT_REVOCATION_MODE: 'offline', DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE: '1', TESTINGPLATFORM_TELEMETRY_OPTOUT: '1' }),
  node: Object.freeze({}),
});

const readLines = file => (existsSync(file) ? readFileSync(file, 'utf8').split('\n').filter(Boolean).map(l => JSON.parse(l)) : []);

const WRAPPER_SCRIPT = 'out="$1"; code="$2"; shift 2; "$@" > "$out" 2>&1; echo $? > "$code"';

/** The argv of the fixed wrapper: the output and exit-code paths and the command are POSITIONAL arguments, never part of the script text (a path with a quote or `$(...)` would otherwise be run as shell). */
export const wrappedArgv = ({ command, out, code }) => ['sh', '-c', WRAPPER_SCRIPT, 'sh', out, code, ...command];

/** What the wrapper left in `box/out`: `ok({ code, output })`, or a typed `fail` naming the file(s) that are missing (the box ran but the wrapper did not finish) or an exit code that is not a number. */
export function readWrapped({ out, code }) {
  const missing = [out, code].filter(file => !existsSync(file));
  if (missing.length > 0) return fail(`the box ran but its wrapper left no ${missing.join(' and no ')}: the command's result is not known`);
  const exit = Number(readFileSync(code, 'utf8').trim());
  return Number.isInteger(exit) ? ok({ code: exit, output: readFileSync(out, 'utf8') }) : fail(`the exit-code file ${code} does not hold a number`);
}

/**
 * One command in the box, with its WHOLE output and exit code: v1's `inBox` keeps only the last 600 characters of a failure, and the not-in-cache error that names the package must be read in full. The command runs
 * through a fixed `sh -c` wrapper (the command is positional arguments, never a string to parse) that writes its output and its exit code into the box's `out` folder, which the host reads after the box ends.
 * -> `ok({ code, output })` | `fail` (the box did not run, or timed out).
 */
async function runWrapped({ layout, dirs, stack, n, command, env, timeoutMs }) {
  const token = `${n}-${randomBytes(4).toString('hex')}`; // unique per call: a file of an earlier call can never be mistaken for this one's
  const [out, code] = [path.join(layout.out, `o${token}`), path.join(layout.out, `c${token}`)];
  const box = boxOptionsFor(dirs);
  const wrapped = wrappedArgv({ command, out, code });
  const r = await inBox({ layout, nodeDir: dirs.node, command: wrapped, timeoutMs, name: layout.rid, ...box, env: { ...box.env, ...STACK_ENV[stack], ...env } });
  return r.ok ? readWrapped({ out, code }) : r;
}

/**
 * A FRESH box that holds only the seeded snapshot (accepted plan finding 1): a new layout under `root` (`rid` is never reused), the stack's snapshot copied in by v1's `seedCaches` (each part re-hashed against the
 * record; `box/nuget`, `box/home/.dotnet`, `box/npm`), `HOME` and `DOTNET_CLI_HOME` the fresh `box/home` (no host cache, no CLI home but the snapshot's), and the egress proxy started with `allow` (none by default: every
 * registry is refused, and the layout's `egress.ndjson` says so). `allow` exists only so the refuted shape, a box that reaches the registry, can be built beside the right one. The box adds no offline flag of its own:
 * `npmOfflineArgv`, `NPM_OFFLINE_ENV`, `NPMRC_OFFLINE` and `offlineNugetConfig` are the caller's, so a test can also show the box without them.
 * -> `ok({ layout, run(command, { env, timeoutMs }), egress(), close() })` | `fail`.
 */
export async function openBox({ stack, snapshot, root, rid, dirs, allow = [] }) {
  const made = sessionLayout(root, rid);
  if (!made.ok) return made;
  const layout = made.value;
  copyFileSync(path.join(V1_SPIKE, 'harness', 'box-init.mjs'), layout.files.boxInit);
  const seeded = seedCaches({ layout, snapshots: { [stack]: snapshot } });
  if (!seeded.ok) return seeded;
  const proxy = await egressProxy({ socketPath: layout.sockets.proxy, allow, log: line => appendFileSync(layout.files.egressLog, `${JSON.stringify(line)}\n`) });
  let n = 0;
  const run = (command, { env = {}, timeoutMs = 600_000 } = {}) => runWrapped({ layout, dirs, stack, n: (n += 1), command, env, timeoutMs });
  return ok({ layout, run, egress: () => egressSummary(readLines(layout.files.egressLog)), close: () => proxy.close() });
}
