"""rs_layer: the options of the execution layer (E1.S8b) as the command line takes them: `--launch-status`, `--case-view`, and the
`Config` the olympiad path runs under; plus what a run observed besides its cases (`RunInfo`) and the text of a failed launch."""
import os
from typing import NamedTuple, Optional

import rs_exec as ex
import rs_report as rp
import rs_samples as sm
import rs_status
import rs_view
from rs_lex import Problem


class RunInfo(NamedTuple):
    """What the run observed besides the cases: the empty program's time, and whether a launch failed (with its reason and stderr)."""
    empty_ms: Optional[int] = None
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
                     case_view=args.case_view, out_dir=os.path.dirname(os.path.abspath(args.json_out)) if args.json_out else None)


def status_fd(args):
    return None if args.launch_status is None else fd_number(args.launch_status)


def launch_failure(failed):
    return RunInfo(None, {"failed": True, "stderr": failed.stderr}, failed.reason)


def launch_lines(info):
    """The exit 3 text of a failed launch: the reason, then the bounded tail of the prefix's standard error."""
    return [f"launch failed: {info.reason}"] + ["  " + line for line in info.launch["stderr"].splitlines()]
