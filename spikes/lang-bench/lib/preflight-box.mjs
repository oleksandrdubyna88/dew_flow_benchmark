// E1.S5: the preflight's box — bwrap argv from v1's primitives (`spikes/minigit-bench/lib/bwrap.mjs`) until the execution layer's `box.mjs` exists, and the one
// way to run a command in it. Root read-only; home, /mnt, /run, /tmp hidden; the distribution's dotnet hidden and DOTNET_ROOT set to the pinned SDK (see below);
// the pinned directories bound back read-only; no network; a cleared environment.
import { existsSync } from 'node:fs';
import path from 'node:path';
import { chdir, devProc, hiddenPaths, isolation, roBind, roRoot, setenvAll, tmpfs } from '../../minigit-bench/lib/bwrap.mjs';
import { runBounded } from '../../minigit-bench/lib/run.mjs';

const PROBE_MS = 60_000;

/**
 * Where a distribution's dotnet lives and where an apphost looks for one. An apphost-launched program (a built `app` executable) does not use PATH: it reads
 * DOTNET_ROOT, then `/etc/dotnet/install_location`, then the default install locations (reviewer-verified with COREHOST_TRACE=1: `Using dotnet root path [/usr/lib/dotnet]`).
 * So the box hides these paths AND sets DOTNET_ROOT to the pinned SDK; either alone would leave a way to the distribution's runtime.
 */
export const DISTRIBUTION_DOTNET_PATHS = Object.freeze(['/usr/lib/dotnet', '/usr/share/dotnet', '/etc/dotnet']);

/** The directories of the store, by name. */
export const storeDirs = (stacks, storeRoot) => Object.fromEntries(Object.entries(stacks.directories).map(([name, dirName]) => [name, path.join(storeRoot, dirName)]));

/** The box's PATH: the pinned directories alone. The system tools the probes need are named by absolute path in the probe argv. */
export const defaultBoxPath = dirs => [dirs.dotnetSdk, path.join(dirs.node, 'bin'), path.join(dirs.typescript, 'node_modules', '.bin')];

const baseEnv = ({ boxPath, dirs, protect }) => ({
  PATH: boxPath.join(':'), HOME: '/tmp', TMPDIR: '/tmp', LANG: 'C.UTF-8',
  DOTNET_CLI_HOME: '/tmp', DOTNET_NOLOGO: '1', DOTNET_CLI_TELEMETRY_OPTOUT: '1', DOTNET_CLI_USE_MSBUILD_SERVER: '0',
  ...(protect ? { DOTNET_ROOT: dirs.dotnetSdk } : {}),
});

/**
 * The bwrap argv prefix (ending in `--`) of the preflight's box. `extraRoBinds` / `rwBinds` (`{ host, box }`) and `extraEnv`, `workdir` exist for the
 * probes that need a scratch folder and for tests that bind fixtures; `protect: false` builds the box WITHOUT the dotnet hiding and DOTNET_ROOT, only so a test can
 * show what that leaves open.
 */
export function boxArgv({ bwrap, dirs, boxPath, operatorHome, exists = existsSync, extraRoBinds = [], rwBinds = [], extraEnv = {}, workdir, protect = true }) {
  const hidden = protect ? DISTRIBUTION_DOTNET_PATHS.filter(p => exists(p)) : [];
  return [
    bwrap, ...roRoot(), ...devProc(), ...hiddenPaths({ operatorHome, exists }).flatMap(tmpfs), ...hidden.flatMap(tmpfs),
    ...Object.values(dirs).flatMap(roBind), ...extraRoBinds.flatMap(b => ['--ro-bind', b.host, b.box]), ...rwBinds.flatMap(b => ['--bind', b.host, b.box]),
    ...isolation(), ...setenvAll({ ...baseEnv({ boxPath, dirs, protect }), ...extraEnv }), ...(workdir ? chdir(workdir) : []), '--',
  ];
}

/** Run `argv` in the box (the exe is looked up on the BOX's PATH by bwrap, not on the host's): `{ code, stdout, stderr }` or `null` when it could not run or timed out. */
export async function runInBox(prefix, argv, { timeoutMs = PROBE_MS } = {}) {
  const r = await runBounded(prefix[0], [...prefix.slice(1), ...argv], { timeoutMs, env: { PATH: '/usr/bin:/bin', LANG: 'C.UTF-8' } });
  return r.ok && !r.value.timedOut ? { code: r.value.code, stdout: r.value.stdout, stderr: r.value.stderr } : null;
}

