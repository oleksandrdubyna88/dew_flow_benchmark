// E1.S4: how a build runs from a snapshot ALONE. The box options of both stacks (one function, so the build and every offline box cannot drift), the exact offline npm invocation, the offline NuGet source, and the
// check of what a restore really used against the registered manifest. The functions here are pure.
//
// Why a fresh box, and why the flags: a registry that the box can reach fills any hole in the snapshot silently, so a missing package would only show on the first run without network. Copying the cache is not
// enough either: npm still asks the registry unless it is told not to (`--offline`, `npm_config_offline`, `offline=true`), and NuGet adds the user-level nuget.org source back unless every inherited source is cleared.
// The two not-in-cache errors that name the package are npm's `ENOTCACHED` and NuGet's `NU1101`; a generic network error is neither.
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
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

/**
 * The `nuget.config` of an offline workspace: every inherited source and fallback folder cleared, exactly one local source (the seeded global packages folder). -> `ok(text)` | `fail` for a path outside
 * `[A-Za-z0-9/._-]` (it goes into XML). S3 reuses this function for the workspaces' `nuget.config`.
 */
export function offlineNugetConfig(source) {
  if (typeof source !== 'string' || !SOURCE_PATH.test(source)) return fail(`the offline NuGet source path ${JSON.stringify(source)} must match ${SOURCE_PATH}: it is written into XML`);
  return ok(`<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="offline" value="${source}" />
  </packageSources>
  <fallbackPackageFolders>
    <clear />
  </fallbackPackageFolders>
</configuration>
`);
}

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
