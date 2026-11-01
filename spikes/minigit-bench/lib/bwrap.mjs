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
 * The paths both boxes replace with an empty tmpfs: the operator's home (always), /mnt, /run, /tmp and /var/tmp (when the host has them;
 * a directory the host lacks cannot be mounted over on a read-only root). One list, so the scoring box and the agent box cannot drift.
 */
export const hiddenPaths = ({ operatorHome, exists = existsSync }) =>
  Object.freeze([operatorHome, '/mnt', '/run', '/tmp', '/var/tmp'].filter(p => p === operatorHome || exists(p)));

const LOCALE_NAME = /^[A-Za-z]{2,3}(_[A-Za-z]{2,3})?(\.[A-Za-z0-9-]{1,16})?(@[a-z]{1,16})?$/;

/**
 * The locale seam (the exploratory pilot, W2 and W3): `{}` when none is given (every default stays as it was), else `{ LANG, LC_ALL }` set to that locale. The value goes into an
 * environment, so a string that is not a locale name (language, optional `_TERRITORY`, optional `.charset`) is refused, naming the shape.
 */
export function localeEnv(locale) {
  if (locale === undefined) return {};
  if (typeof locale !== 'string' || !LOCALE_NAME.test(locale)) throw new Error(`locale ${typeof locale === 'string' ? `'${locale.slice(0, 40)}'` : typeof locale} is not a locale name: language, optional _TERRITORY, optional .charset (for example de_DE.UTF-8)`);
  return { LANG: locale, LC_ALL: locale };
}

/**
 * The scoring environment: a fixed allow-list, never the operator's. `nugetCache` / `npmCache` are the per-scoring copies of the
 * seeded offline caches; with none given each cache lives inside `home`. `locale` (seam W3) replaces `LANG` and adds `LC_ALL`; with none the environment is what it always was.
 */
export function scoreEnv({ home, tmp, toolchainDirs = [], nugetCache, npmCache, locale }) {
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
    ...localeEnv(locale),
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
export function scoreBoxArgv({ copyDir, casesDir, home, tmp, toolchainDirs = [], nugetCache, npmCache, roFiles = [], cwd, operatorHome = os.homedir(), exists = existsSync, locale }) {
  const hidden = hiddenPaths({ operatorHome, exists });
  return Object.freeze([
    'bwrap',
    ...roRoot(),
    ...devProc(),
    ...hidden.flatMap(tmpfs),
    ...toolchainDirs.flatMap(roBind),
    ...[copyDir, casesDir, home, tmp, nugetCache, npmCache].filter(Boolean).flatMap(bind),
    ...roFiles.flatMap(roBind),
    ...isolation(),
    ...setenvAll(scoreEnv({ home, tmp, toolchainDirs, nugetCache, npmCache, locale })),
    ...(cwd ? chdir(cwd) : []),
    '--',
  ]);
}
