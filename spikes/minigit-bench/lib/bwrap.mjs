// bubblewrap argv primitives (pure: arrays in, arrays out) and the scoring box built from them. E2's session sandbox composes the same
// primitives. Why a box here: the scorer runs agent-written code as the operator after the session, possibly while the partner cell
// still runs; without it that code could read the held-out suite, the key or another cell's tree, and reach the network.
// Why --unshare-pid --die-with-parent: a SIGKILL of the bwrap PID ends the namespace's init and with it every grandchild, so a
// timed-out child cannot keep a pipe open.
import { existsSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';

export const BWRAP_PATH = '/usr/bin/bwrap';
export const roRoot = () => ['--ro-bind', '/', '/'];
export const tmpfs = p => ['--tmpfs', p];
export const bind = p => ['--bind', p, p];
export const roBind = p => ['--ro-bind', p, p];
export const devProc = () => ['--dev', '/dev', '--proc', '/proc'];
export const isolation = () => ['--unshare-net', '--unshare-pid', '--die-with-parent', '--new-session'];
export const chdir = p => ['--chdir', p];

/** `--clearenv`, then one `--setenv` per key, keys sorted so the argv is stable. */
export const setenvAll = env => ['--clearenv', ...Object.keys(env).sort().flatMap(k => ['--setenv', k, String(env[k])])];

const cacheNamed = (cacheDirs, name) => cacheDirs.find(d => path.basename(d) === name);

/**
 * The scoring environment: a fixed allow-list, never the operator's. `cacheDirs` are bound read-write; the one named `nuget` becomes
 * NUGET_PACKAGES and the one named `npm` becomes npm_config_cache (each falls back to a place inside `home`).
 */
export function scoreEnv({ home, tmp, toolchainDirs = [], cacheDirs = [] }) {
  return Object.freeze({
    PATH: [...toolchainDirs.flatMap(d => [`${d}/bin`, d]), '/usr/bin', '/bin'].join(':'),
    HOME: home,
    TMPDIR: tmp,
    LANG: 'C.UTF-8',
    DOTNET_CLI_HOME: home,
    DOTNET_NOLOGO: '1',
    DOTNET_CLI_TELEMETRY_OPTOUT: '1',
    DOTNET_CLI_USE_MSBUILD_SERVER: '0',
    MSBUILDDISABLENODEREUSE: '1',
    UseSharedCompilation: 'false',
    NUGET_PACKAGES: cacheNamed(cacheDirs, 'nuget') ?? path.join(home, '.nuget', 'packages'),
    npm_config_cache: cacheNamed(cacheDirs, 'npm') ?? path.join(home, '.npm'),
    npm_config_offline: 'true',
  });
}

/**
 * The argv prefix (ending in `--`) of the scoring box: the whole filesystem read-only, the operator's home, /mnt and WSL's /run/WSL
 * replaced by empty tmpfs, then only what scoring needs bound back in: toolchains read-only (node lives under the home), the
 * copy, cases, HOME, TMPDIR and the caches read-write. No network, own pid namespace, an environment of the allow-list only.
 */
export function scoreBoxArgv({ copyDir, casesDir, home, tmp, toolchainDirs = [], cacheDirs = [], cwd, operatorHome = os.homedir(), exists = existsSync }) {
  return Object.freeze([
    'bwrap',
    ...roRoot(),
    ...devProc(),
    ...tmpfs(operatorHome),
    ...tmpfs('/mnt'),
    ...(exists('/run/WSL') ? tmpfs('/run/WSL') : []),
    ...toolchainDirs.flatMap(roBind),
    ...[copyDir, casesDir, home, tmp, ...cacheDirs].flatMap(bind),
    ...isolation(),
    ...setenvAll(scoreEnv({ home, tmp, toolchainDirs, cacheDirs })),
    ...(cwd ? chdir(cwd) : []),
    '--',
  ]);
}
