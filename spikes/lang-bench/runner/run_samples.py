"""run_samples: the baseline visible-sample runner (E1.S8a; FORMAT v2 grammar 2).

    python3 -I run_samples.py [--workspace DIR] [--kind olympiad|daemon|web] [--no-build] [--limits FILE] [--launch-prefix JSON]
                              [--json OUT] [--only ID] [--parse-only] SAMPLES_DIR

Exit 0 every case passed, 1 a case failed (a failing build fails every case), 3 the environment (no workspace, no bash),
4 configuration (usage, a malformed sample with its file and line, a version or limits problem), 5 the runner itself failed.
Without --limits the olympiad time limit is the floor (1 s) and every time verdict says so; without a cgroup every case says
its memory is not certified. The daemon and web kinds run in part ii of the story; here they parse (--parse-only).
"""
import atexit
import os
import shutil
import sys
import tempfile

# No bytecode cache in the runner's tree is ever read: the pin hashes the sources, and an unchecked-hash .pyc planted beside them would run
# instead. Every module imported from here on (the siblings included) is compiled from its source into a fresh folder removed at exit.
sys.dont_write_bytecode = True
sys.pycache_prefix = tempfile.mkdtemp(prefix="runner-pyc-")
atexit.register(shutil.rmtree, sys.pycache_prefix, True)
if __name__ == "__main__":  # under -I the script's own directory is not on sys.path
    sys.path.insert(0, os.path.dirname(os.path.realpath(__file__)))

import argparse
import json
import signal
import traceback
from dataclasses import dataclass
from typing import Any, Optional

import rs_exec as ex
import rs_json
import rs_olympiad as ro
import rs_pair
import rs_report as rp
import rs_samples as sm
import rs_script
import rs_transcript
from rs_lex import Problem

EXIT_OK, EXIT_FAILED, EXIT_ENVIRONMENT, EXIT_CONFIG, EXIT_RUNNER = 0, 1, 3, 4, 5
KINDS = ("olympiad", "daemon", "web")
PART_TWO_OPTIONS = ("cgroup_of", "launch_status", "case_view")


class Failure:
    """A way to end: an exit code and the lines to print on standard error."""

    def __init__(self, code, lines):
        self.code, self.lines = code, lines


class _Usage(Exception):
    pass


class _Parser(argparse.ArgumentParser):
    def error(self, message):
        raise _Usage(message)


def configure_streams(*streams):
    """UTF-8 with backslashreplace on every stream: `-I` implies `-E`, so PYTHONIOENCODING and PYTHONUTF8 are ignored (B10)."""
    for stream in streams:
        stream.reconfigure(encoding="utf-8", errors="backslashreplace")


def build_parser():
    p = _Parser(prog="run_samples.py", description=__doc__.split("\n")[0])
    p.add_argument("samples_dir", metavar="SAMPLES_DIR")
    p.add_argument("--workspace", default=".")
    p.add_argument("--kind", choices=KINDS)
    p.add_argument("--no-build", action="store_true")
    p.add_argument("--limits")
    p.add_argument("--launch-prefix")
    p.add_argument("--json", dest="json_out")
    p.add_argument("--only")
    p.add_argument("--parse-only", action="store_true")
    for name in ("--cgroup-of", "--launch-status", "--case-view"):
        p.add_argument(name)
    return p


@dataclass
class Plan:
    kind: str
    args: Any
    pairs: list
    limits: sm.Limits
    prefix: Optional[list]


# ---- configuration ----------------------------------------------------------------------------------------------------------------

def _is_string_list(value):
    return isinstance(value, list) and all(isinstance(x, str) for x in value)


def _prefix_of(text):
    """The launch prefix as a list of strings, or None when `text` is not a JSON array of strings."""
    parsed = rs_json.parse(text)
    return parsed.value if parsed.ok and _is_string_list(parsed.value) else None


def _refused_options(args):
    return [f"--{name.replace('_', '-')} is not supported by this build (the execution layer's options come with part ii)"
            for name in PART_TWO_OPTIONS if getattr(args, name) is not None]


def _argument_problems(args):
    problems = _refused_options(args)
    problems += [] if os.path.isdir(args.samples_dir) else [f"{sm.shown(args.samples_dir)} is not a directory"]
    problems += [] if args.launch_prefix is None or _prefix_of(args.launch_prefix) is not None else ["--launch-prefix must be a JSON array of strings"]
    return problems


def _statement_kind(args):
    if not os.path.isdir(args.workspace):
        return None, Failure(EXIT_ENVIRONMENT, [f"workspace {sm.shown(args.workspace)} is not a directory"])
    kind, problems = sm.kind_from_statement(args.workspace)
    return kind, Failure(EXIT_CONFIG, _texts(problems)) if problems else None


def _kind_of(args):
    """(kind, failure): from --kind, else the Kind row of the workspace statement."""
    return (args.kind, None) if args.kind else _statement_kind(args)


def _texts(problems):
    return [p.text() for p in problems]


def _chosen(pairs, only):
    return [p for p in pairs if only in (None, p.id)]


def _empty_selection(args, chosen):
    if chosen:
        return []
    return [Problem(sm.shown(args.samples_dir), 0, f"no sample matches {args.only!r}" if args.only else "no sample pairs")]


def _with_manifest(pair, manifest):
    comparison, rids = manifest.get(pair.id, (None, ()))
    return pair._replace(r_ids=rids, comparison=comparison)


def _load_all(chosen, manifest):
    """Every chosen pair with its bytes read once, within the memory budget, and the problems of those that could not be read."""
    return sm.load_all([_with_manifest(p, manifest) for p in chosen])


def _pairs_of(args):
    pairs, problems = sm.discover(args.samples_dir)
    manifest, more = sm.cases_manifest(args.samples_dir, {p.id for p in pairs})
    chosen = _chosen(pairs, args.only)
    loaded, unreadable = _load_all(chosen, manifest)
    return loaded, problems + more + _empty_selection(args, chosen) + unreadable


def _limits_of(args):
    return sm.load_limits(args.limits) if args.limits else (sm.Limits(), [])


def _launch_prefix(args):
    return _prefix_of(args.launch_prefix) if args.launch_prefix else None


def _beyond_bytes(pair):
    return pair.comparison is not None and pair.comparison != "bytes"


def _unsupported(kind, pairs):
    """The olympiad comparison is byte-exact only: a manifest line that names normalised, json or relational (alone or joined) would be
    compared byte for byte all the same, so it is refused."""
    if kind != "olympiad":
        return []
    return [Problem("cases.txt", 0, f"{p.id}: the comparison {p.comparison!r} is not supported for olympiad (only bytes is built)")
            for p in pairs if _beyond_bytes(p)]


def make_plan(args):
    """A Plan, or a Failure naming what is wrong with the command line, the statement, the pairs or the limits."""
    problems = _argument_problems(args)
    if problems:
        return Failure(EXIT_CONFIG, problems)
    kind, failure = _kind_of(args)
    pairs, pair_problems = _pairs_of(args)
    limits, limit_problems = _limits_of(args)
    found = _texts(pair_problems + limit_problems + _unsupported(kind, pairs))
    if failure:
        return failure
    return Failure(EXIT_CONFIG, found) if found else Plan(kind, args, pairs, limits, _launch_prefix(args))


# ---- parsing ----------------------------------------------------------------------------------------------------------------------

def _scenario_problems(pair):
    scenario, p1 = rs_script.parse_script(pair.stdin, pair.in_name)
    transcript, p2 = rs_transcript.parse_transcript(pair.expected, pair.out_name)
    both = rs_pair.validate_pair(scenario, transcript) if scenario and transcript else []
    return p1 + p2 + both


def parse_problems(plan):
    """Every Problem of every pair: for daemon and web the script, the transcript and their correspondence."""
    if plan.kind == "olympiad":
        return []
    return [p for pair in plan.pairs for p in _scenario_problems(pair)]


# ---- running ----------------------------------------------------------------------------------------------------------------------

def _limit_and_basis(plan):
    return (plan.limits.time_ms, "registered") if plan.limits.time_ms else (ex.FLOOR_MS, "floor")


def _build_failed(plan, done):
    note = "the build timed out" if done.timed_out else f"bash build.sh exited {done.status}"
    tail = tuple("  " + line for line in done.tail.splitlines()[-10:])
    return [rp.CaseResult(p.id, "FAIL", "build failed", 0, p.r_ids, (f"  {note}",) + tail, comparison=p.comparison) for p in plan.pairs]


def progress(message):
    """One progress line on standard error, at once: standard output stays the report."""
    print(f"run_samples: {message}", file=sys.stderr, flush=True)


def _run_one(pair, config):
    progress(f"{pair.id} started")
    result = ro.run_case(ro.Case(pair.id, pair.stdin, pair.expected, pair.r_ids, pair.comparison), config)
    progress(f"{pair.id} {result.status} ({result.wall_ms} ms)")
    return result


def _run_cases(plan, scratch):
    limit, basis = _limit_and_basis(plan)
    config = ex.Config(os.path.abspath(plan.args.workspace), scratch, plan.prefix, limit, basis)
    return [_run_one(p, config) for p in plan.pairs]


def _build_outcome(done):
    return "timed out" if done.timed_out else f"exit {done.status}"


def _execute(plan, scratch):
    """(build record, results): the build first unless --no-build; a build that fails fails every case."""
    if plan.args.no_build:
        return {"ran": False}, _run_cases(plan, scratch)
    progress("build started")
    done = ex.build(os.path.abspath(plan.args.workspace), scratch)
    progress(f"build finished ({_build_outcome(done)}, {done.wall_ms} ms)")
    record = {"ran": True, "status": done.status, "wallMs": done.wall_ms, "timedOut": done.timed_out}
    return record, (_run_cases(plan, scratch) if done.status == 0 and not done.timed_out else _build_failed(plan, done))


def _report(plan, build, results):
    for result in results:
        print("\n".join(rp.render_case(result)))
    print(rp.summary_line(results))
    if plan.args.json_out:
        with open(plan.args.json_out, "w", encoding="utf-8") as f:
            json.dump(rp.run_record(plan.kind, build, [rp.case_record(r) for r in results]), f, indent=2, ensure_ascii=False)


def _scratch_run(plan):
    scratch = tempfile.mkdtemp(prefix="runner-")
    try:
        return _execute(plan, scratch)
    finally:
        shutil.rmtree(scratch, ignore_errors=True)


def _exit_code(results):
    return EXIT_OK if all(r.status == "PASS" for r in results) else EXIT_FAILED


def _run_olympiad(plan):
    if not os.path.isdir(plan.args.workspace):
        return Failure(EXIT_ENVIRONMENT, [f"workspace {sm.shown(plan.args.workspace)} is not a directory"])
    try:
        build, results = _scratch_run(plan)
    except OSError as e:
        return Failure(EXIT_ENVIRONMENT, [f"cannot start a program: {type(e).__name__}: {e}"])
    _report(plan, build, results)
    return _exit_code(results)


def _finish(outcome):
    if isinstance(outcome, Failure):
        print("\n".join(outcome.lines), file=sys.stderr)
        return outcome.code
    return outcome


def _parsed_args(argv):
    try:
        return build_parser().parse_args(argv)
    except _Usage as e:
        return Failure(EXIT_CONFIG, [f"usage: {e}"])


def run(argv):
    args = _parsed_args(argv)
    plan = args if isinstance(args, Failure) else make_plan(args)
    if isinstance(plan, Failure):
        return _finish(plan)
    problems = parse_problems(plan)
    return _finish(Failure(EXIT_CONFIG, _texts(problems)) if problems else _after_parsing(plan))


def _after_parsing(plan):
    if plan.args.parse_only:
        print(f"{len(plan.pairs)} pairs parsed")
        return EXIT_OK
    if plan.kind != "olympiad":
        return Failure(EXIT_RUNNER, [f"running a {plan.kind} sample is part ii of this story and is not built in this runner (use --parse-only)"])
    return _run_olympiad(plan)


class _Interrupted(BaseException):
    """The runner itself was stopped by a signal. A BaseException, so no handler of a case swallows it."""

    def __init__(self, signum):
        super().__init__(signum)
        self.signum = signum


def _on_signal(signum, frame):
    for other in (signal.SIGTERM, signal.SIGINT):  # the cleanup that follows must not be interrupted again
        signal.signal(other, signal.SIG_IGN)
    raise _Interrupted(signum)


def _install_handlers():
    for sig in (signal.SIGTERM, signal.SIGINT):
        signal.signal(sig, _on_signal)


def _stopped(stopped):
    print(f"run_samples: interrupted by {signal.Signals(stopped.signum).name}: no verdict was reached", file=sys.stderr)
    return EXIT_RUNNER


def main(argv=None):
    configure_streams(sys.stdout, sys.stderr)
    _install_handlers()
    try:
        return run(sys.argv[1:] if argv is None else argv)
    except _Interrupted as stopped:
        return _stopped(stopped)
    except Exception:
        traceback.print_exc()
        return EXIT_RUNNER


if __name__ == "__main__":
    sys.exit(main())
