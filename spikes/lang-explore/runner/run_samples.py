"""The one judge of the exploratory pilot (olympiad only).

Two modes, one file (`python3 -I` adds no script directory to the import path, so this file is self-contained):

  visible    python3 -I runner/run_samples.py samples/
             Run in a workspace. Builds with `bash build.sh`, runs every `<id>.in` / `<id>.out` pair of the directory
             with `bash run.sh`, prints one PASS or FAIL line per case. Time is judged against the 1 second floor of the
             statement; memory is not judged.

  heldout    python3 -I run_samples.py --heldout CONFIG.json
             The harness's mode, on the host. CONFIG.json names a pristine tree, the work directory this judge may write
             under, the case directories, the limits, and the bwrap argv prefixes of the build and the run (the harness
             builds them; the placeholders TREE, HOME and TMP in them are replaced per case). The build runs once; the
             built tree is made read-only; each case then runs in its own fresh copy of that tree, in its own scope with
             a memory limit, and the copy is removed after the case. The result is one JSON file.

It creates no temporary path of its own: it writes only under the directories it is given.
"""
import json
import os
import re
import shutil
import signal
import stat
import subprocess
import sys
import threading
import time
import uuid

FLOOR_TIME_MS = 1000
OUTPUT_CAP = 1 << 20
TAIL = 400
KINDS = {
    "olympiad": "a program that reads standard input once and writes standard output once",
}
VERDICTS = ("pass", "wrong answer", "time limit", "memory limit", "runtime error", "build failed")
SAVED_OUTPUT_CAP = 64 * 1024
NOT_LOADED = ("not loaded", "not found", "no such unit")


# ---- cases -------------------------------------------------------------------------------------------------------

def read_cases(directory):
    """[(id, stdin bytes, expected bytes)] of the `<id>.in` / `<id>.out` pairs in `directory`, sorted by id."""
    ids = sorted({name[:-3] for name in os.listdir(directory) if name.endswith(".in")})
    cases = []
    for case_id in ids:
        with open(os.path.join(directory, case_id + ".in"), "rb") as f:
            stdin = f.read()
        with open(os.path.join(directory, case_id + ".out"), "rb") as f:
            expected = f.read()
        cases.append((case_id, stdin, expected))
    return cases


def first_difference(expected, actual):
    """The index of the first byte that differs, or None when they are equal."""
    if expected == actual:
        return None
    for i in range(min(len(expected), len(actual))):
        if expected[i] != actual[i]:
            return i
    return min(len(expected), len(actual))


def tail(data):
    return data[-TAIL:].decode("utf-8", "replace")


# ---- files -------------------------------------------------------------------------------------------------------

def make_writable(root):
    for base, dirs, files in os.walk(root):
        for name in dirs + files:
            path = os.path.join(base, name)
            if not os.path.islink(path):
                os.chmod(path, os.stat(path).st_mode | stat.S_IWUSR)
    os.chmod(root, os.stat(root).st_mode | stat.S_IWUSR)


def make_read_only(root):
    for base, dirs, files in os.walk(root, topdown=False):
        for name in files + dirs:
            path = os.path.join(base, name)
            if not os.path.islink(path):
                os.chmod(path, os.stat(path).st_mode & ~0o222)
    os.chmod(root, os.stat(root).st_mode & ~0o222)


def remove_tree(root):
    if os.path.lexists(root):
        make_writable(root)
        shutil.rmtree(root)


def copy_tree(source, target):
    shutil.copytree(source, target, symlinks=True)
    make_writable(target)


# ---- running one process, bounded --------------------------------------------------------------------------------

class Capture:
    """A reader thread that keeps the first `cap` bytes of a stream and drains the rest."""

    def __init__(self, stream, cap, on_overflow=None):
        self.data = bytearray()
        self.overflow = False
        self._stream, self._cap, self._on_overflow = stream, cap, on_overflow
        self._thread = threading.Thread(target=self._run, daemon=True)
        self._thread.start()

    def _run(self):
        while True:
            chunk = self._stream.read(65536)
            if not chunk:
                return
            room = self._cap - len(self.data)
            if room > 0:
                self.data += chunk[:room]
            if len(chunk) > room and not self.overflow:
                self.overflow = True
                if self._on_overflow:
                    self._on_overflow()

    def join(self, timeout):
        self._thread.join(timeout)
        return bytes(self.data)


def run_plain(argv, cwd, stdin, timeout_s, env=None):
    """Run argv with stdin bytes, its own process group; on timeout the group is killed. -> dict."""
    started = time.monotonic()
    proc = subprocess.Popen(argv, cwd=cwd, env=env, stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                            stderr=subprocess.PIPE, start_new_session=True)
    out = Capture(proc.stdout, OUTPUT_CAP)
    err = Capture(proc.stderr, OUTPUT_CAP)
    feed_stdin(proc, stdin)
    timed_out = False
    try:
        proc.wait(timeout=timeout_s)
    except subprocess.TimeoutExpired:
        timed_out = True
        try:
            os.killpg(proc.pid, signal.SIGKILL)
        except ProcessLookupError:
            pass
        proc.wait()
    wall_ms = int((time.monotonic() - started) * 1000)
    return {"code": proc.returncode, "timedOut": timed_out, "wallMs": wall_ms,
            "stdout": out.join(5), "stderr": err.join(5), "stdoutOverflow": out.overflow}


def feed_stdin(proc, data):
    def write():
        try:
            proc.stdin.write(data)
        except (BrokenPipeError, OSError):
            pass
        finally:
            try:
                proc.stdin.close()
            except OSError:
                pass
    threading.Thread(target=write, daemon=True).start()


# ---- visible mode ------------------------------------------------------------------------------------------------

def statement_kind(workspace):
    """The kind phrase of STATEMENT.md's `Kind` row, or None."""
    try:
        with open(os.path.join(workspace, "STATEMENT.md"), encoding="utf-8") as f:
            for line in f:
                m = re.match(r"^\|\s*Kind\s*\|\s*(.*?)\s*\|\s*$", line)
                if m:
                    return m.group(1)
    except OSError:
        return None
    return None


def visible(samples_dir):
    workspace = os.getcwd()
    kind = statement_kind(workspace)
    if kind != KINDS["olympiad"]:
        print("FAIL \u2014 the Kind row of STATEMENT.md is not an olympiad kind this runner judges")
        return 1
    built = run_plain(["bash", "build.sh"], workspace, b"", 600)
    if built["timedOut"] or built["code"] != 0:
        print("FAIL build \u2014 `bash build.sh` %s" % ("did not finish in 600 s" if built["timedOut"] else "exited %s" % built["code"]))
        print(tail(built["stderr"] + built["stdout"]))
        return 1
    failed = 0
    for case_id, stdin, expected in read_cases(samples_dir):
        line = judge_visible(workspace, case_id, stdin, expected)
        print(line)
        failed += line.startswith("FAIL")
    return 1 if failed else 0


def judge_visible(workspace, case_id, stdin, expected):
    r = run_plain(["bash", "run.sh"], workspace, stdin, FLOOR_TIME_MS / 1000.0)
    if r["timedOut"]:
        return "FAIL %s \u2014 time limit: more than %d ms" % (case_id, FLOOR_TIME_MS)
    if r["code"] != 0:
        return "FAIL %s \u2014 exit status %s" % (case_id, r["code"])
    at = first_difference(expected, r["stdout"])
    if at is not None or r["stdoutOverflow"]:
        return "FAIL %s \u2014 output differs, first difference at byte %s" % (case_id, at)
    return "PASS %s" % case_id


# ---- held-out mode -----------------------------------------------------------------------------------------------

def substitute(argv, mapping):
    """Replace each placeholder wherever it occurs in an element (`@@HOME@@` is also the prefix of NUGET_PACKAGES and npm_config_cache). The placeholders are distinctive, never part of a real value."""
    out = []
    for a in argv:
        for name, value in mapping.items():
            a = a.replace(name, value)
        out.append(a)
    return out


def cgroup_dir_of_scope(unit):
    """The cgroup directory of a transient scope by name, or None when it is gone."""
    uid = os.getuid()
    path = "/sys/fs/cgroup/user.slice/user-%d.slice/user@%d.service/app.slice/%s.scope" % (uid, uid, unit)
    return path if os.path.isdir(path) else None


# GNU time reports ru_maxrss (from wait4, of the box's own process, so it UNDER-reports the program) beside the cgroup's memory.peak: description only, the peak certifies.
WRAPPER = (
    'out=$1; shift; /usr/bin/time -f "%M" -o "$out.rss" "$@"; rc=$?; '
    'cg=/sys/fs/cgroup$(sed -n "s/^0:://p" /proc/self/cgroup); '
    '{ echo "peak $(cat "$cg/memory.peak")"; echo "rc $rc"; sed "s/^/ev /" "$cg/memory.events"; } > "$out" 2>/dev/null; '
    'exit $rc'
)


def parse_maxrss(path):
    """The last numeric line of GNU time's output file (KiB), or None."""
    try:
        with open(path, encoding="ascii") as f:
            numbers = [line.strip() for line in f if line.strip().isdigit()]
    except OSError:
        return None
    return int(numbers[-1]) if numbers else None


def parse_measure(path):
    """-> (peak bytes or None, {event: count})"""
    peak, events = None, {}
    try:
        with open(path, encoding="ascii") as f:
            for line in f:
                parts = line.split()
                if parts[:1] == ["peak"] and len(parts) == 2 and parts[1].isdigit():
                    peak = int(parts[1])
                elif parts[:1] == ["ev"] and len(parts) == 3 and parts[2].isdigit():
                    events[parts[1]] = int(parts[2])
    except OSError:
        pass
    return peak, events


def systemctl(*args):
    return subprocess.run(["systemctl", "--user", *args], capture_output=True, text=True, timeout=30)


def kill_scope(unit):
    """Kill every process of the scope. A unit that already ended ("not loaded", "not found") is no failure.
    -> None, or a sentence when the scope could not be killed."""
    r = systemctl("kill", "--signal=KILL", unit + ".scope")
    if r.returncode != 0:
        text = (r.stderr + r.stdout).lower()
        if not (r.returncode in (1, 5) and any(s in text for s in NOT_LOADED)):
            return "systemctl kill %s exited %d: %s" % (unit, r.returncode, tail((r.stderr + r.stdout).encode()))
    return None


def scope_is_gone(unit, wait_s=10):
    """Poll until the scope has no process left or the unit is gone (it may take a moment). -> bool"""
    deadline = time.monotonic() + wait_s
    while time.monotonic() < deadline:
        active = systemctl("is-active", "--quiet", unit + ".scope").returncode == 0
        cg = cgroup_dir_of_scope(unit)
        procs = ""
        if cg:
            try:
                with open(os.path.join(cg, "cgroup.procs"), encoding="ascii") as f:
                    procs = f.read().strip()
            except OSError:
                procs = ""
        if not active and not procs:
            return True
        time.sleep(0.05)
    return False


def run_in_scope(run_box, case_dir, stdin, limits):
    """One run of `bash run.sh` in its own scope. -> a result dict, or raises HarnessFailure."""
    unit = "lang-explore-%s" % uuid.uuid4().hex[:12]
    measure = os.path.join(case_dir, "measure.txt")
    # OOMPolicy=continue: with systemd's default the scope is stopped when the kernel kills a process in it, which ends the wrapper before it reads memory.events (observed 2026-10-31)
    argv = ["systemd-run", "--user", "--scope", "--quiet", "--collect", "--unit=" + unit,
            "-p", "MemoryMax=%d" % limits["memoryBytes"], "-p", "MemorySwapMax=0", "-p", "OOMPolicy=continue",
            "sh", "-c", WRAPPER, "sh", measure, *run_box]
    started = time.monotonic()
    proc = subprocess.Popen(argv, stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                            start_new_session=True)
    state = {"killed_for": None, "killed": False, "problem": None}
    lock = threading.Lock()

    def kill_for(reason):
        """Kill the whole scope. A failed `systemctl kill` (or one that raises) falls back to a SIGKILL of the process group; the failure is kept, and the case ends as a harness failure. Callable from the
        output-cap reader thread and again from the time limit: the first reason stays, but a kill that did not take is tried again."""
        with lock:
            if state["killed_for"] is None:
                state["killed_for"] = reason
            if state["killed"]:
                return
            try:
                problem = kill_scope(unit)
            except Exception as e:  # noqa: BLE001 - any failure of the kill is a harness failure, never a lost thread
                problem = "systemctl kill raised %s: %s" % (type(e).__name__, e)
            state["problem"] = problem
            state["killed"] = problem is None
            if problem is not None:
                try:
                    os.killpg(proc.pid, signal.SIGKILL)
                except ProcessLookupError:
                    pass

    out = Capture(proc.stdout, OUTPUT_CAP, on_overflow=lambda: kill_for("output"))
    err = Capture(proc.stderr, OUTPUT_CAP)
    feed_stdin(proc, stdin)
    try:
        proc.wait(timeout=limits["timeMs"] / 1000.0)
    except subprocess.TimeoutExpired:
        kill_for("time")
        wait_after_kill(proc, unit)
    wall_ms = int((time.monotonic() - started) * 1000)
    if state["killed_for"] is not None:
        wait_after_kill(proc, unit)
        if state["problem"] or not scope_is_gone(unit):
            raise HarnessFailure("scope %s: a process remains after the kill%s" % (unit, " (%s)" % state["problem"] if state["problem"] else ""))
    peak, events = parse_measure(measure)
    return {"code": proc.returncode, "killedFor": state["killed_for"], "wallMs": wall_ms,
            "stdout": out.join(5), "stderr": err.join(5), "stdoutOverflow": out.overflow,
            "peakBytes": peak, "maxRssKb": parse_maxrss(measure + ".rss"), "events": events, "unit": unit}


def wait_after_kill(proc, unit):
    """The wait after a kill is bounded: a process that does not end 10 s after SIGKILL is a harness failure, not a hang."""
    try:
        proc.wait(timeout=10)
    except subprocess.TimeoutExpired:
        raise HarnessFailure("scope %s: the process did not end 10 s after the kill" % unit)


class HarnessFailure(Exception):
    pass


def classify(result, expected, limits):
    """The verdict of one run: (verdict, detail)."""
    oom = result["events"].get("oom_kill", 0) > 0 or result["events"].get("oom", 0) > 0
    if result["killedFor"] == "time":
        return "time limit", "more than %d ms" % limits["timeMs"]
    if result["killedFor"] == "output" or result["stdoutOverflow"]:
        return "wrong answer", "output over the cap of %d bytes" % OUTPUT_CAP
    if oom or (result["peakBytes"] is not None and result["peakBytes"] >= limits["memoryBytes"]):
        return "memory limit", "the scope reached %d bytes" % limits["memoryBytes"]
    if result["peakBytes"] is None:
        raise HarnessFailure("memory-not-certifiable: memory.peak could not be read from the case's scope, and no other measurement of memory passes or fails a test")
    if result["code"] != 0:
        return "runtime error", "exit status %s" % result["code"]
    at = first_difference(expected, result["stdout"])
    if at is not None:
        return "wrong answer", "first difference at byte %d" % at
    return "pass", ""


def save_output(cfg, set_name, case_id, data):
    """Keep the case's stdout (the first 64 KiB) where a hand check can read it: the directory the harness gave (mode 0700, a file 0600).
    A failure to keep it (no room, no rights, a path in the way) is a harness failure with its message, not a traceback."""
    directory = cfg.get("outputsDir")
    if not directory:
        return
    name = "%s-%s.out" % (set_name, case_id)
    try:
        os.makedirs(directory, mode=0o700, exist_ok=True)
        fd = os.open(os.path.join(directory, name), os.O_WRONLY | os.O_CREAT | os.O_TRUNC, 0o600)
        try:
            f = os.fdopen(fd, "wb")
        except BaseException:
            os.close(fd)
            raise
        with f:
            f.write(data[:SAVED_OUTPUT_CAP])
    except OSError as e:
        raise HarnessFailure("could not keep the output of %s in %s: %s" % (name, directory, e))


def progress(cfg, text):
    """One line of progress for the harness to say while the judge runs (appended to a file it tails)."""
    path = cfg.get("progress")
    if path:
        with open(path, "a", encoding="utf-8") as f:
            f.write(text + "\n")


def run_case(cfg, built_tree, set_name, case_id, stdin, expected):
    work = cfg["work"]
    case_dir = os.path.join(work, "case-%s-%s" % (set_name, case_id))
    remove_tree(case_dir)
    os.makedirs(case_dir)
    tree, home, tmp = (os.path.join(case_dir, n) for n in ("tree", "home", "tmp"))
    try:
        copy_tree(built_tree, tree)
        os.makedirs(home)
        os.makedirs(tmp)
        ph = cfg["placeholders"]
        run_box = substitute(cfg["runBox"], {ph["tree"]: tree, ph["home"]: home, ph["tmp"]: tmp}) + ["bash", "run.sh"]
        result = run_in_scope(run_box, case_dir, stdin, cfg["limits"])
        verdict, detail = classify(result, expected, cfg["limits"])
        save_output(cfg, set_name, case_id, result["stdout"])
        progress(cfg, "%s/%s %s %d ms" % (set_name, case_id, verdict, result["wallMs"]))
        return {"set": set_name, "id": case_id, "verdict": verdict, "detail": detail, "wallMs": result["wallMs"],
                "peakBytes": result["peakBytes"], "maxRssKb": result["maxRssKb"], "events": result["events"], "exit": result["code"],
                "stderrTail": tail(result["stderr"]), "stdoutBytes": len(result["stdout"]), "expectedBytes": len(expected)}
    finally:
        remove_tree(case_dir)


def build_once(cfg):
    """Build in a scratch copy of the pristine tree, then make it read-only. -> (built tree path, build dict)."""
    work = cfg["work"]
    built = os.path.join(work, "built")
    remove_tree(built)
    copy_tree(cfg["tree"], built)
    home, tmp = os.path.join(work, "build-home"), os.path.join(work, "build-tmp")
    for d in (home, tmp):
        remove_tree(d)
        os.makedirs(d)
    ph = cfg["placeholders"]
    box = substitute(cfg["buildBox"], {ph["tree"]: built, ph["home"]: home, ph["tmp"]: tmp})
    r = run_plain([*box, "bash", "build.sh"], None, b"", cfg["limits"]["buildTimeoutMs"] / 1000.0)
    for d in (home, tmp):
        remove_tree(d)
    ok = not r["timedOut"] and r["code"] == 0
    info = {"ok": ok, "timedOut": r["timedOut"], "exit": r["code"], "wallMs": r["wallMs"],
            "outputTail": tail(r["stderr"] + r["stdout"])}
    if ok:
        make_read_only(built)
    return built, info


def heldout(config_path):
    with open(config_path, encoding="utf-8") as f:
        cfg = json.load(f)
    os.makedirs(cfg["work"], exist_ok=True)
    built, build = build_once(cfg)
    progress(cfg, "build %s in %d ms" % ("ok" if build["ok"] else "FAILED", build["wallMs"]))
    results, harness_failure = [], None
    cases = [(s["name"], c) for s in cfg["sets"] for c in read_cases(s["dir"])]
    try:
        for set_name, (case_id, stdin, expected) in cases:
            if not build["ok"]:
                results.append({"set": set_name, "id": case_id, "verdict": "build failed", "detail": "the build did not succeed: no case ran", "wallMs": 0,
                                "peakBytes": None, "events": {}, "exit": None, "stderrTail": "", "stdoutBytes": 0,
                                "expectedBytes": len(expected)})
                continue
            results.append(run_case(cfg, built, set_name, case_id, stdin, expected))
    except HarnessFailure as e:
        harness_failure = str(e)
    finally:
        remove_tree(built)
    out = {"build": build, "limits": cfg["limits"], "cases": results, "harnessFailure": harness_failure}
    tmp_path = cfg["out"] + ".part"
    with open(tmp_path, "w", encoding="utf-8") as f:
        json.dump(out, f, indent=2)
        f.write("\n")
    os.replace(tmp_path, cfg["out"])
    return 3 if harness_failure else 0


def main(argv):
    if len(argv) == 3 and argv[1] == "--heldout":
        return heldout(argv[2])
    if len(argv) == 2 and not argv[1].startswith("-"):
        return visible(argv[1])
    sys.stderr.write("usage: run_samples.py samples/   |   run_samples.py --heldout CONFIG.json\n")
    return 2


if __name__ == "__main__":
    sys.exit(main(sys.argv))
