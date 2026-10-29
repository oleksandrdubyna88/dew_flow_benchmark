// bubblewrap argv primitives (pure: arrays in, arrays out) and the scoring box built from them. E2's session sandbox composes the same
// primitives. Why a box here: the scorer runs agent-written code as the operator after the session, possibly while the partner cell
// still runs; without it that code could read the held-out suite, the key or another cell's tree, and reach the network.
// Why --unshare-pid --die-with-parent: a SIGKILL of the bwrap PID ends the namespace's init and with it every grandchild, so a
// timed-out child cannot keep a pipe open.
import { existsSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';

export const roRoot = () => ['--ro-bind', '/', '/'];
export const tmpfs = p => ['--tmpfs', p];
export const bind = p => ['--bind', p, p];
export const roBind = p => ['--ro-bind', p, p];
export const devProc = () => ['--dev', '/dev', '--proc', '/proc'];
export const isolation = () => ['--unshare-net', '--unshare-pid', '--die-with-parent', '--new-session'];
export const chdir = p => ['--chdir', p];

/** `--clearenv`, then one `--setenv` per key, keys sorted so the argv is stable. */
export const setenvAll = env => ['--clearenv', ...Object.keys(env).sort().flatMap(k => ['--setenv', k, String(env[k])])];

/**
 * The scoring environment: a fixed allow-list, never the operator's. `nugetCache` / `npmCache` are the per-scoring copies of the
 * seeded offline caches; with none given each cache lives inside `home`.
 */
export function scoreEnv({ home, tmp, toolchainDirs = [], nugetCache, npmCache }) {
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
    NUGET_PACKAGES: nugetCache ?? path.join(home, '.nuget', 'packages'),
    npm_config_cache: npmCache ?? path.join(home, '.npm'),
    npm_config_offline: 'true',
  });
}

/**
 * The argv prefix (ending in `--`) of the scoring box: the whole filesystem read-only; the operator's home, /mnt, /run (the session
 * bus and every service socket under it: a process that reaches the user manager can start a unit OUTSIDE the box), /tmp and /var/tmp
 * (the host's build-server sockets) replaced by empty tmpfs; then only what scoring needs bound back in, AFTER the tmpfs so they show:
 * toolchains read-only (node lives under the home); the copy, HOME, TMPDIR and the cache copies read-write; `casesDir` only for the
 * held-out step; `roFiles` read-only OVER the writable copy (the harness test script the tree must not rewrite). No network, own pid
 * namespace, an environment of the allow-list only. A directory the host lacks is not mounted over (bwrap cannot create it on a ro root).
 */
export function scoreBoxArgv({ copyDir, casesDir, home, tmp, toolchainDirs = [], nugetCache, npmCache, roFiles = [], cwd, operatorHome = os.homedir(), exists = existsSync }) {
  const hidden = [operatorHome, '/mnt', '/run', '/tmp', '/var/tmp'].filter(p => p === operatorHome || exists(p));
  return Object.freeze([
    'bwrap',
    ...roRoot(),
    ...devProc(),
    ...hidden.flatMap(tmpfs),
    ...toolchainDirs.flatMap(roBind),
    ...[copyDir, casesDir, home, tmp, nugetCache, npmCache].filter(Boolean).flatMap(bind),
    ...roFiles.flatMap(roBind),
    ...isolation(),
    ...setenvAll(scoreEnv({ home, tmp, toolchainDirs, nugetCache, npmCache })),
    ...(cwd ? chdir(cwd) : []),
    '--',
  ]);
}

/** Resolve an executable on PATH the way spawn does, or null. */
export function findOnPath(name, pathValue = process.env.PATH ?? '') {
  for (const dir of pathValue.split(path.delimiter).filter(Boolean)) {
    const candidate = path.join(dir, name);
    if (existsSync(candidate)) return candidate;
  }
  return null;
}
