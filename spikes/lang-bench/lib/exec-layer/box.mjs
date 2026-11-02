// E1.S8b: the v2 box — bwrap's argv for the program, composed from v1's primitives (`spikes/minigit-bench/lib/bwrap.mjs`: `tmpfs`, `roBind`, `bind`, `devProc`, `setenvAll`).
// Never `roRoot()` (a read-only root with the home hidden still shows `/mnt/c` and can start `cmd.exe` through WSL interop, verified 2026-10-29) and never `isolation()` (it carries
// `--unshare-net`: the netns is the holder's, shared with the driver). The base is `--tmpfs /`; then, read-only, only what a program needs:
//   `/usr` (the locale archive `/usr/lib/locale/locale-archive` is inside it) with the `/bin`, `/lib`, `/lib64`, `/sbin` links; `/etc/passwd`, `/etc/group`, `/etc/nsswitch.conf`,
//   `/etc/localtime`; the pinned toolchain trees by their FULL paths (nothing else of the home exists in the box); `extraRo` for a stack's `/etc/ssl` or `/etc/ca-certificates`
//   when the probe says its runtime reads them. Then `--dev /dev`, `--proc /proc` (bwrap's own mount for its pid namespace), a fresh `--tmpfs /tmp` and the case's view, writable.
// No `$HOME`, no `/mnt`, no `/run` (no user bus, no agent, no `/run/WSL`), no `/var/tmp`, no `/sys/fs/cgroup`. `--unshare-pid --new-session` stay; there is NO `--die-with-parent`:
// `--unshare-user --disable-userns` (the Opus review, O1): a program that could make a nested user namespace could mount a WRITABLE cgroup2 and hide a child in `prog/`; nested `unshare` fails with ENOSPC, uid stays 0.
// the scope is the kill domain and a lingering child must survive until `cgroup.procs` is read (consult B4).
// THE WORKSPACE: with `tree` (the BUILT tree, a run) the box mounts `--overlay-src <tree> --overlay-src <view> --tmp-overlay <view>`: each launch gets a fresh writable layer over the read-only built tree, on a
// tmpfs inside the box, discarded at exit (no copy of the tree per case; `view` is the case's empty placeholder, a second source so a scenario's fixtures written into it show). The writes are charged to
// the program's cgroup, as writes to /tmp always were. Without `tree` (a build in the box) `view` is bound writable.
// THE OWN-CGROUP VIEW (OP-K, parent D15, decided 2026-11-02): `--unshare-cgroup` (so `/proc/self/cgroup` shows `0::/`) and the program's `prog/` bound read-only at `/sys/fs/cgroup`, ALWAYS in a
// run: .NET and Node read their memory limit there, and nothing else of the host's cgroups or `/sys` exists in the box. `prog` is a required decision (`null` only for a box outside any scope, a build). `--cap-drop ALL` is this layer's addition (measured 2026-11-02): the box
// runs inside the holder's user namespace as uid 0 and without it the program holds every capability there, among them the one to take the shared `lo` down.
import { existsSync } from 'node:fs';
import path from 'node:path';
import { bind, chdir, devProc, roBind, setenvAll, tmpfs } from '../../../minigit-bench/lib/bwrap.mjs';
import { DISTRIBUTION_DOTNET_PATHS } from '../preflight-box.mjs';

const USR_LINKS = Object.freeze(['bin', 'lib', 'lib64', 'sbin']);
const ETC_FILES = Object.freeze(['/etc/passwd', '/etc/group', '/etc/nsswitch.conf', '/etc/localtime']);
const NEVER_BOUND = Object.freeze(['/mnt', '/run', '/var/tmp']);

/** The own-cgroup view: its own cgroup namespace and `prog/` read-only at `/sys/fs/cgroup`; nothing for `null`. */
const cgroupView = prog => (prog === null ? [] : ['--unshare-cgroup', '--ro-bind', prog, '/sys/fs/cgroup']);
/** The case's workspace: an overlay over the built tree (a run), or the view bound writable (a build). */
const workspace = (view, tree) => (tree === undefined ? bind(view) : ['--overlay-src', tree, '--overlay-src', view, '--tmp-overlay', view]);
const link = name => ['--symlink', `usr/${name}`, `/${name}`];
const hiddenRuntimes = exists => DISTRIBUTION_DOTNET_PATHS.filter(p => p.startsWith('/usr/') && exists(p));

/**
 * The argv (ending in `--`) of the program's box. `view` is the case's writable copy (`{case}` may stand in it; the runner fills it) and the working directory; `dirs` is the pinned
 * toolchain directories `{ dotnetSdk, node, typescript }`; `env` is the allow-listed environment; `statusFd` is `--json-status-fd`, the same number as the runner's `--launch-status`.
 * `dieWithParent` (B1) defaults to true for a box outside any scope (`prog: null`, a build): bwrap ends with the process that started it, so a build can never outlive the probe. A program's box has none: the runner ends it by its cgroup.
 * The distribution's dotnet under `/usr` is hidden, because an apphost reads `DOTNET_ROOT` and the box must have exactly one runtime (E1.S5's finding).
 */
export function boxArgv({ view, dirs, env, statusFd, extraRo = [], exists = existsSync, bwrap = 'bwrap', prog, tree, dieWithParent = prog === null }) {
  if (prog === undefined) throw new Error('boxArgv needs `prog`, the program\'s own cgroup (a path, `{case}` allowed): the own-cgroup view is the layer\'s box; null only for a box outside any scope');
  return [
    bwrap, ...(statusFd == null ? [] : ['--json-status-fd', String(statusFd)]),
    ...tmpfs('/'), ...roBind('/usr'), ...USR_LINKS.filter(name => exists(`/${name}`)).flatMap(link), ...hiddenRuntimes(exists).flatMap(tmpfs),
    ...[...ETC_FILES, ...Object.values(dirs), ...extraRo].flatMap(roBind),
    ...devProc(), ...tmpfs('/tmp'), ...cgroupView(prog), ...workspace(view, tree), ...chdir(view),
    ...(dieWithParent ? ['--die-with-parent'] : []), '--unshare-user', '--disable-userns', '--unshare-pid', '--new-session', '--cap-drop', 'ALL', ...setenvAll(env), '--',
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
