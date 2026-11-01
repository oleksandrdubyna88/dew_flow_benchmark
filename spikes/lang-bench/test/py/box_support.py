"""Shared fixtures of the part ii-a unit tests: a temporary workspace whose run.sh is the stub under test, fake launchers (shell
one-liners that write bwrap's status lines), and the real transient user scope the kill tests need. Stubs are toys, never a task."""
import glob
import json
import os
import shutil
import subprocess
import tempfile
import time
import uuid

import runner_path  # noqa: F401
import rs_exec as ex
import rs_olympiad as ro

ANSWER = b"7 22 11 34 17 52 26 13 40 20 10 5 16 8 4 2 1\n"
STATUS_FD = 7
BASH_OWN_KEYS = {"PWD", "OLDPWD", "SHLVL", "_"}


class Workspace:
    """A temporary workspace (and scratch folder) whose run.sh is the stub under test."""

    def __init__(self, test, run_sh, files=None):
        holder = tempfile.TemporaryDirectory()
        test.addCleanup(holder.cleanup)
        self.root = holder.name
        self.dir, self.scratch = os.path.join(self.root, "ws"), os.path.join(self.root, "scratch")
        os.mkdir(self.dir)
        os.mkdir(self.scratch)
        for name, data in {"run.sh": run_sh, **(files or {})}.items():
            self.write(self.dir, name, data)

    def write(self, folder, name, data):
        with open(os.path.join(folder, name), "wb") as f:
            f.write(data if isinstance(data, bytes) else data.encode())

    def make_dir(self, name):
        path = os.path.join(self.root, name)
        os.mkdir(path)
        return path

    def config(self, **overrides):
        return ex.Config(workspace=self.dir, scratch=self.scratch, **{"limit_ms": 20_000, **overrides})

    def run(self, case, **overrides):
        return ro.run_case(case, self.config(**overrides))


def case(expected=ANSWER, stdin=b"7\n", ident="s03", fixtures=None):
    return ro.Case(ident, stdin, expected, (), None, fixtures)


def reporting_prefix(setup="", linger=None):
    """A fake box: `{"child-pid": P}` on descriptor 7 after `setup` (a shell fragment), then the program as a child, then `{"exit-code": C}`,
    then `linger` seconds of teardown. The launcher stays the program's parent, so it is a launcher and not the program, and (it is not
    bwrap) no program pid is found: the end of the case is the `exit-code` record."""
    tail = f"\nsleep {linger}" if linger else ""
    script = f'{setup}\nprintf \'{{"child-pid": %s}}\\n\' $$ >&{STATUS_FD}\n"$@"\nc=$?\nprintf \'{{"exit-code": %s}}\\n\' $c >&{STATUS_FD}{tail}\nexit $c'
    return ["sh", "-c", script, "sh"]


def fake_cgroup(ws, name="cg", files=None):
    """A folder that stands in for a cgroup: an empty `cgroup.procs` (nothing in it yet, as the runner requires) and the given files."""
    path = ws.make_dir(name)
    for file_name, text in {"cgroup.procs": "", **(files or {})}.items():
        ws.write(path, file_name, text)
    return path


def fake_box(test):
    """A COPY of bash named `bwrap` in an owned folder: the stand-in for the box binary. It is a bwrap process by identity (device and
    inode of the file the prefix names), which a second copy of bash named bwrap in a workspace is not."""
    holder = tempfile.TemporaryDirectory()
    test.addCleanup(holder.cleanup)
    path = os.path.join(holder.name, "bwrap")
    shutil.copy(shutil.which("bash"), path)
    return path


def box_prefix(box, cgroup=None, linger=None, setup=""):
    """The fake box as a launch prefix: the program runs as ITS child (so its parent is a bwrap process), `child-pid` and `exit-code` go to
    descriptor 7, and with `cgroup` the pids of the box and of the program are written to that folder's fake `cgroup.procs`, the program's
    leaving it when the program ends and the box's when the box does (a real cgroup does the same, a fake file must be told to)."""
    file = f"{cgroup}/cgroup.procs" if cgroup else None
    listing = f'printf "%s\\n%s\\n" $$ $pid > "{file}"' if file else ":"
    boxed = f'printf "%s\\n" $$ > "{file}"' if file else ":"
    gone = f': > "{file}"' if file else ":"
    tail = f"\nsleep {linger}" if linger else ""
    script = (f'{setup}\nprintf \'{{"child-pid": %s}}\\n\' $$ >&{STATUS_FD}\n"$@" <&0 &\npid=$!\n{listing}\nwait $pid\nc=$?\n{boxed}\n'
              f'printf \'{{"exit-code": %s}}\\n\' $c >&{STATUS_FD}{tail}\n{gone}\nexit $c')
    return [box, "-c", script, "bwrap"]


def failing_prefix():
    return ["sh", "-c", "echo boom: the box could not be made >&2; exit 1", "sh"]


# ---- the real transient scope ---------------------------------------------------------------------------------------------------------

def _scope_probe():
    """(reason it is unavailable or None, the cgroup directory every scope of this user manager lives in)."""
    probe = ["systemd-run", "--user", "--scope", "--quiet", "--collect", "-p", "Delegate=yes", "--", "cat", "/proc/self/cgroup"]
    try:
        done = subprocess.run(probe, capture_output=True, timeout=30)
    except (OSError, subprocess.TimeoutExpired) as e:
        return f"no systemd-run with a user manager ({type(e).__name__})", None
    line = done.stdout.decode("ascii", "replace").strip()
    if done.returncode != 0 or not line.startswith("0::/"):
        return "systemd-run --user --scope does not work here (no user manager)", None
    return None, "/sys/fs/cgroup" + os.path.dirname(line[3:])


_PROBE = []


def scope_environment():
    if not _PROBE:
        _PROBE.append(_scope_probe())
    return _PROBE[0]


def _unavailable(test, reason):
    """FAIL under BENCH_REQUIRE_SANDBOX=1 (a skipped proof is not a proof), else SKIP with the reason."""
    if os.environ.get("BENCH_REQUIRE_SANDBOX") == "1":
        test.fail(reason)
    test.skipTest(reason)


def require_scope(test):
    """The cgroup directory scopes live in; the test skips with the reason when there is no `systemd-run --user`."""
    reason, base = scope_environment()
    if reason:
        _unavailable(test, reason)
    return base


def require_bwrap(test):
    if subprocess.run(["sh", "-c", "command -v bwrap"], capture_output=True).returncode != 0:
        _unavailable(test, "bwrap is not installed")


class Scope:
    """One transient user scope named in the owner form (`testsToDelete-u<uid>-bench-scope-<id>`); the test stops it when it ends."""

    def __init__(self, test):
        base = require_scope(test)
        require_bwrap(test)
        self.unit = f"testsToDelete-u{os.getuid()}-bench-scope-{uuid.uuid4().hex[:10]}"
        self.cgroup = f"{base}/{self.unit}.scope"
        test.addCleanup(self.stop)

    def prefix(self, workspace_dir, new_session=True, per_case=False, status_fd=None):
        """`systemd-run --user --scope … bwrap --unshare-pid --new-session … --`: the program behind bwrap, in a session of its own.
        `per_case` gives each case its own scope (`<unit>-{case}`, whose cgroup is `cgroup_template`), `status_fd` the bwrap status descriptor."""
        status = [] if status_fd is None else ["--json-status-fd", str(status_fd)]
        box = ["bwrap", *status, "--unshare-pid", *(["--new-session"] if new_session else []), "--ro-bind", "/usr", "/usr", "--symlink", "usr/bin", "/bin",
               "--symlink", "usr/lib", "/lib", "--symlink", "usr/lib64", "/lib64", "--proc", "/proc", "--dev", "/dev",
               "--bind", workspace_dir, "/ws", "--chdir", "/ws", "--"]
        unit = f"{self.unit}-{{case}}" if per_case else self.unit
        return ["systemd-run", "--user", "--scope", "--quiet", "--collect", f"--unit={unit}", "-p", "Delegate=yes", "--", *box]

    @property
    def cgroup_template(self):
        return f"{self.cgroup[:-len('.scope')]}-{{case}}.scope"

    def pids(self):
        try:
            with open(os.path.join(self.cgroup, "cgroup.procs")) as f:
                return [int(line) for line in f.read().split()]
        except OSError:
            return []

    def stop(self):
        for directory in glob.glob(f"{self.cgroup[:-len('.scope')]}*.scope"):
            try:
                with open(os.path.join(directory, "cgroup.kill"), "w") as f:
                    f.write("1")
            except OSError:
                pass
        subprocess.run(["systemctl", "--user", "stop", f"{self.unit}*.scope"], capture_output=True, timeout=30)


def wait_until(predicate, seconds=5):
    deadline = time.monotonic() + seconds
    while time.monotonic() < deadline:
        if predicate():
            return True
        time.sleep(0.01)
    return predicate()


def read_json(path):
    with open(path, "r", encoding="utf-8") as f:
        return json.load(f)
