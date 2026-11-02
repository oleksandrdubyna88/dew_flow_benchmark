// E1.S8b: the v2 box — bwrap's argv for the program, composed from v1's primitives (`spikes/minigit-bench/lib/bwrap.mjs`: `tmpfs`, `roBind`, `bind`, `devProc`, `setenvAll`).
// Never `roRoot()` (a read-only root with the home hidden still shows `/mnt/c` and can start `cmd.exe` through WSL interop, verified 2026-10-29) and never `isolation()` (it carries
// `--unshare-net`: the netns is the holder's, shared with the driver). The base is `--tmpfs /`; then, read-only, only what a program needs:
//   `/usr` (the locale archive `/usr/lib/locale/locale-archive` is inside it) with the `/bin`, `/lib`, `/lib64`, `/sbin` links; `/etc/passwd`, `/etc/group`, `/etc/nsswitch.conf`,
//   `/etc/localtime`; the pinned toolchain trees by their FULL paths (nothing else of the home exists in the box); `extraRo` for a stack's `/etc/ssl` or `/etc/ca-certificates`
//   when the probe says its runtime reads them. Then `--dev /dev`, `--proc /proc` (bwrap's own mount for its pid namespace), a fresh `--tmpfs /tmp` and the case's view, writable.
// No `$HOME`, no `/mnt`, no `/run` (no user bus, no agent, no `/run/WSL`), no `/var/tmp`, no `/sys/fs/cgroup`. `--unshare-pid --new-session` stay; there is NO `--die-with-parent`:
// the scope is the kill domain and a lingering child must survive until `cgroup.procs` is read (consult B4). `--cap-drop ALL` is this layer's addition (measured 2026-11-02): the box
// runs inside the holder's user namespace as uid 0 and without it the program holds every capability there, among them the one to take the shared `lo` down.
import { existsSync } from 'node:fs';
import path from 'node:path';
import { bind, chdir, devProc, roBind, setenvAll, tmpfs } from '../../../minigit-bench/lib/bwrap.mjs';
import { DISTRIBUTION_DOTNET_PATHS } from '../preflight-box.mjs';

const USR_LINKS = Object.freeze(['bin', 'lib', 'lib64', 'sbin']);
const ETC_FILES = Object.freeze(['/etc/passwd', '/etc/group', '/etc/nsswitch.conf', '/etc/localtime']);
const NEVER_BOUND = Object.freeze(['/mnt', '/run', '/var/tmp']);

const link = name => ['--symlink', `usr/${name}`, `/${name}`];
const hiddenRuntimes = exists => DISTRIBUTION_DOTNET_PATHS.filter(p => p.startsWith('/usr/') && exists(p));

/**
 * The argv (ending in `--`) of the program's box. `view` is the case's writable copy (`{case}` may stand in it; the runner fills it) and the working directory; `dirs` is the pinned
 * toolchain directories `{ dotnetSdk, node, typescript }`; `env` is the allow-listed environment; `statusFd` is `--json-status-fd`, the same number as the runner's `--launch-status`.
 * The distribution's dotnet under `/usr` is hidden, because an apphost reads `DOTNET_ROOT` and the box must have exactly one runtime (E1.S5's finding).
 */
export function boxArgv({ view, dirs, env, statusFd, extraRo = [], exists = existsSync, bwrap = 'bwrap' }) {
  return [
    bwrap, ...(statusFd === undefined ? [] : ['--json-status-fd', String(statusFd)]),
    ...tmpfs('/'), ...roBind('/usr'), ...USR_LINKS.filter(name => exists(`/${name}`)).flatMap(link), ...hiddenRuntimes(exists).flatMap(tmpfs),
    ...[...ETC_FILES, ...Object.values(dirs), ...extraRo].flatMap(roBind),
    ...devProc(), ...tmpfs('/tmp'), ...bind(view), ...chdir(view),
    '--unshare-pid', '--new-session', '--cap-drop', 'ALL', ...setenvAll(env), '--',
  ];
}

const BINDS = Object.freeze(['--bind', '--ro-bind', '--bind-try', '--ro-bind-try']);
const inside = (child, parent) => child === parent || child.startsWith(`${parent.replace(/\/$/, '')}/`);

function problemOf(source, home) {
  if (source === '/') return `binds the whole root (${source})`;
  if (inside(home, source)) return `binds the home or a parent of it (${source})`;
  return NEVER_BOUND.some(never => inside(source, never)) ? `binds ${source}, which no box may show` : null;
}

/** What is wrong with the bind sources of an argv (the scan the probe and the tests share): the whole root, the home or a parent of it, `/mnt`, `/run`, `/var/tmp`. `[]` is clean. */
export function unsafeBinds(argv, { home }) {
  const found = [];
  argv.forEach((arg, i) => {
    const problem = BINDS.includes(arg) ? problemOf(path.resolve(argv[i + 1] ?? ''), home) : null;
    if (problem) found.push(problem);
  });
  return found;
}
