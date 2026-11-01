"""rs_layer: the options of the execution layer (E1.S8b) as the command line takes them: `--launch-status`, `--case-view`, and the
`Config` the olympiad path runs under; plus what a run observed besides its cases (`RunInfo`) and the text of a failed launch."""
import os
import re
import sys
from typing import NamedTuple, Optional

import rs_exec as ex
import rs_report as rp
import rs_samples as sm
import rs_status
import rs_view
from rs_lex import Problem


class RunInfo(NamedTuple):
    """What the run observed besides the cases: the empty program's control record, and whether a launch failed (with its reason and stderr)."""
    empty: Optional[dict] = None
    launch: dict = rp.NO_LAUNCH
    reason: str = ""


def fd_number(text):
    return int(text) if text.isascii() and text.isdigit() else None


def status_problem(args):
    """What is wrong with `--launch-status`: it needs the prefix that writes it, and a free descriptor number of 3 or more."""
    if args.launch_prefix is None:
        return "--launch-status needs --launch-prefix: the prefix's launcher is what writes the status"
    number = fd_number(args.launch_status)
    return "--launch-status must be a descriptor number" if number is None else rs_status.check_number(number)


def status_problems(args):
    if args.launch_status is None:
        return []
    problem = status_problem(args)
    return [problem] if problem else []


def view_problems(args, pairs):
    """The samples directory and a case view must not contain one another (a held-out run never puts the samples in the box's binds)."""
    if args.case_view is None:
        return []
    return [Problem(sm.shown(args.samples_dir), 0, text) for text in rs_view.overlaps_samples(args.case_view, [p.id for p in pairs], args.samples_dir)]


def config_of(plan, scratch, limit, basis):
    args = plan.args
    return ex.Config(os.path.abspath(args.workspace), scratch, plan.prefix, limit, basis, status_fd=status_fd(args), cgroup=args.cgroup_of,
                     case_view=args.case_view, wrapper_file=args.wrapper_pid_file)


def status_fd(args):
    return None if args.launch_status is None else fd_number(args.launch_status)


def launch_failure(failed):
    return RunInfo(None, {"failed": True, "stderr": failed.stderr}, failed.reason)


def launch_lines(info):
    """The exit 3 text of a failed launch: the reason, then the bounded tail of the prefix's standard error."""
    return [f"launch failed: {info.reason}"] + ["  " + line for line in info.launch["stderr"].splitlines()]


MIN_KERNEL = (5, 14)


def _launch_status_needed(args):
    return args.cgroup_of is not None and args.launch_prefix is not None and args.launch_status is None


def cgroup_problems(args):
    """`--cgroup-of` with a prefix needs the launch status (only `child-pid` says the box is up and starts the clock); a wrapper file
    needs the cgroup it is a process of."""
    problems = []
    if _launch_status_needed(args):
        problems.append("--cgroup-of with --launch-prefix needs --launch-status: only child-pid says the box is up and starts the clock")
    if args.wrapper_pid_file is not None and args.cgroup_of is None:
        problems.append("--wrapper-pid-file needs --cgroup-of")
    return problems


def _unnamed(args):
    named = (("--cgroup-of", args.cgroup_of), ("--case-view", args.case_view), ("--wrapper-pid-file", args.wrapper_pid_file))
    return [flag for flag, value in named if value is not None and "{case}" not in value]


def template_problems(args, pairs):
    """With more than one case a template without `{case}` would give every case the same folder, cgroup or wrapper file."""
    if len(pairs) < 2:
        return []
    return [f"{flag} needs {{case}} in its template: {len(pairs)} cases would share one" for flag in _unnamed(args)]


def plan_problems(args, pairs):
    """Every Problem of the layer's options against the chosen pairs (exit 4): view and samples overlapping, a template without `{case}`,
    the reserved case id."""
    templates = [Problem("arguments", 0, text) for text in template_problems(args, pairs)]
    return view_problems(args, pairs) + templates + id_problems(pairs)


def id_problems(pairs):
    """The empty-program control runs under a case id of its own: no sample may take it."""
    return [Problem("samples", 0, f"the case id {ex.EMPTY_CASE!r} is reserved for the empty-program control") for p in pairs if p.id == ex.EMPTY_CASE]


def _kernel():
    found = re.match(r"(\d+)\.(\d+)", os.uname().release)
    return tuple(int(part) for part in found.groups()) if found else (0, 0)


def _kernel_problem(args):
    if sys.platform.startswith("linux") and _kernel() >= MIN_KERNEL:
        return None
    return f"the host is not Linux {MIN_KERNEL[0]}.{MIN_KERNEL[1]} or newer (cgroup.kill and pidfd_open need it): {sys.platform} {os.uname().release}"


def _pidfd_problem(args):
    try:
        os.close(os.pidfd_open(os.getpid()))
    except (AttributeError, OSError) as e:
        return f"os.pidfd_open is not available ({type(e).__name__}): every wait for an exit needs it"
    return None


def _cgroup2_problem(args):
    if args.cgroup_of is not None and not os.path.exists("/sys/fs/cgroup/cgroup.controllers"):
        return "--cgroup-of needs cgroup v2 mounted at /sys/fs/cgroup (no cgroup.controllers there)"
    return None


def host_problems(args):
    """What the host lacks, named one by one (exit 3): the runner never crashes on a missing prerequisite."""
    found = [check(args) for check in (_kernel_problem, _pidfd_problem, _cgroup2_problem)]
    return [problem for problem in found if problem]


def empty_record(run):
    """The control of F1 as the JSON result keeps it: its time, its launch time, and which pid the wait was on and how that was found."""
    return {"ms": run.wall_ms, "launchMs": run.launch_ms, "pid": run.program["pid"], "found": run.program["found"], "comm": run.program["comm"]}
