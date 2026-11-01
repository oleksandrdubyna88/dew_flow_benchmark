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
from unittest import mock

import runner_path  # noqa: F401
import rs_cgroup as cg
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
        self.finals = {}
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

    def run(self, case, real=False, **overrides):
        """Run one case. A fake cgroup folder is no cgroup2 node and no box is in it, so unless the test says `real=True` (a real scope, or
        a test of those very checks) the two checks that would refuse it are let through."""
        self._write_finals(overrides.get("cgroup"))
        if real:
            return ro.run_case(case, self.config(**overrides))
        with mock.patch.object(cg, "is_cgroup2", lambda directory: True), mock.patch.object(cg, "member_of", lambda pid, directory: True):
            return ro.run_case(case, self.config(**overrides))

    def _write_finals(self, cgroup):
        """What the kernel would have in a fake cgroup's counters after the case: the program writes it (the counters are zero before the
        spawn, which the runner insists on)."""
        final = self.finals.pop(cgroup, None) if cgroup else None
        if final:
            lines = "".join(f"printf '%s' '{text}' > \"{cgroup}/{name}\"\n" for name, text in final.items())
            with open(os.path.join(self.dir, "run.sh"), "rb") as f:
                body = f.read()
            self.write(self.dir, "run.sh", lines.encode() + body)


def case(expected=ANSWER, stdin=b"7\n", ident="s03", fixtures=None):
    return ro.Case(ident, stdin, expected, (), None, fixtures)


def reporting_prefix(setup="", linger=None):
    """A fake box: `{"child-pid": P}` on descriptor 7 after `setup` (a shell fragment), then the program as a child, then `{"exit-code": C}`,
    then `linger` seconds of teardown. The launcher stays the program's parent, so it is a launcher and not the program, and (it is not
    bwrap) no program pid is found: the end of the case is the `exit-code` record."""
    tail = f"\nsleep {linger}" if linger else ""
    script = f'{setup}\nprintf \'{{"child-pid": %s}}\\n\' $$ >&{STATUS_FD}\n"$@"\nc=$?\nprintf \'{{"exit-code": %s}}\\n\' $c >&{STATUS_FD}{tail}\nexit $c'
    return ["sh", "-c", script, "sh"]


def fake_cgroup(ws, name="cg", files=None, final=None):
    """A folder that stands in for a cgroup2 node: an empty `cgroup.procs`, a `cgroup.controllers` and a `cgroup.kill` (the runner opens
    it, never creates it) and zero counters (it insists on that before the spawn). `final` names the counters the case leaves."""
    path = ws.make_dir(name)
    for file_name, text in {"cgroup.procs": "", "cgroup.controllers": "", "cgroup.kill": "", **(files or {})}.items():
        ws.write(path, file_name, text)
    if final:
        ws.finals[path] = final
    return path


def fake_box(test):
    """A COPY of bash named `bwrap` in an owned folder: the stand-in for the box binary. It is a bwrap process by identity (device and
    inode of the file the prefix names), which a second copy of bash named bwrap in a workspace is not."""
    holder = tempfile.TemporaryDirectory()
    test.addCleanup(holder.cleanup)
    path = os.path.join(holder.name, "bwrap")
    shutil.copy(shutil.which("bash"), path)
    return path


def _cgroup_fragments(cgroup):
    """(listing, boxed, gone): the shell fragments that keep a fake cgroup.procs as a real one would be (nothing without a cgroup)."""
    if not cgroup:
        return ":", ":", ":"
    file = f"{cgroup}/cgroup.procs"
    return f'printf "%s\\n%s\\n" $$ $pid > "{file}"', f'printf "%s\\n" $$ > "{file}"', f': > "{file}"'


def box_prefix(box, cgroup=None, linger=None, setup="", after_report=""):
    """The fake box as a launch prefix: the program runs as ITS child (so its parent is a bwrap process), `child-pid` and `exit-code` go to
    descriptor 7, and with `cgroup` the pids of the box and of the program are written to that folder's fake `cgroup.procs`, the program's
    leaving it when the program ends and the box's when the box does (a real cgroup does the same, a fake file must be told to)."""
    listing, boxed, gone = _cgroup_fragments(cgroup)
    tail = f"\nsleep {linger}" if linger else ""
    script = (f'{setup}\nprintf \'{{"child-pid": %s}}\\n\' $$ >&{STATUS_FD}\n{after_report}\n"$@" <&0 &\npid=$!\n{listing}\nwait $pid\nc=$?\n{boxed}\n'
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


def _bwrap_argv(workspace_dir, new_session, status_fd):
    status = [] if status_fd is None else ["--json-status-fd", str(status_fd)]
    sessions = ["--new-session"] if new_session else []
    return ["bwrap", *status, "--unshare-pid", *sessions, "--ro-bind", "/usr", "/usr", "--symlink", "usr/bin", "/bin", "--symlink", "usr/lib", "/lib",
            "--symlink", "usr/lib64", "/lib64", "--proc", "/proc", "--dev", "/dev", "--bind", workspace_dir, "/ws", "--chdir", "/ws", "--"]


class Scope:
    """One transient user scope named in the owner form (`testsToDelete-u<uid>-bench-scope-<id>`); the test stops it when it ends."""

    def __init__(self, test):
        base = require_scope(test)
        require_bwrap(test)
        self.unit = f"testsToDelete-u{os.getuid()}-bench-scope-{uuid.uuid4().hex[:10]}"
        self.cgroup = f"{base}/{self.unit}.scope"
        test.addCleanup(self.stop)

    def prefix(self, workspace_dir, new_session=True, per_case=False, status_fd=None, holder=None):
        """`systemd-run --user --scope … bwrap --unshare-pid --new-session … --`: the program behind bwrap, in a session of its own.
        `per_case` gives each case its own scope (`<unit>-{case}`, whose cgroup is `cgroup_template`), `status_fd` the bwrap status descriptor.
        `holder` (a file name, `{case}` allowed) keeps the scope alive after the program: the system removes a scope when its last process exits,
        so a `sleep` is started in it and its pid written to that file, which the runner is told as the wrapper (E1.S8b's scope holder)."""
        unit = f"{self.unit}-{{case}}" if per_case else self.unit
        held = [] if holder is None else ["sh", "-c", 'sleep 40 >/dev/null 2>&1 <&- 7>&- &\necho $! > "$0"\nexec "$@"', holder]
        box = _bwrap_argv(workspace_dir, new_session, status_fd)
        return ["systemd-run", "--user", "--scope", "--quiet", "--collect", f"--unit={unit}", "-p", "Delegate=yes", "--", *held, *box]

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


def sleepers(seconds):
    """The pids whose command line is `sleep <seconds>`: how a test finds out whether a program of its own is still alive."""
    found = []
    for pid in filter(str.isdigit, os.listdir("/proc")):
        try:
            with open(f"/proc/{pid}/cmdline", "rb") as f:
                found += [pid] if f"sleep\0{seconds}".encode() in f.read() else []
        except OSError:
            continue
    return found


def read_json(path):
    with open(path, "r", encoding="utf-8") as f:
        return json.load(f)
