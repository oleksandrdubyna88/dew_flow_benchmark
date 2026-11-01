"""Starting a case through the launch prefix (E1.S8a part ii-a; B5, B6 clock, V11): the launch status, the clock start, a launch that fails,
the empty program, and the program's environment."""
import fcntl
import os
import signal
import subprocess
import unittest
from unittest import mock

import runner_path  # noqa: F401
import rs_exec as ex
import rs_launch as rl
import rs_layer as layer
import rs_olympiad as ro
import rs_pump
import rs_report as rp
from box_support import (ANSWER, BASH_OWN_KEYS, STATUS_FD, Workspace, box_prefix, case, failing_prefix, fake_box, fake_cgroup, reporting_prefix,
                         sleepers, wait_until)

REPLAY = "cat answer.bin\n"
FILES = {"answer.bin": ANSWER}


def fd_is_open(number):
    try:
        fcntl.fcntl(number, fcntl.F_GETFD)
    except OSError:
        return False
    return True


def open_descriptors():
    return sorted(int(name) for name in os.listdir("/proc/self/fd"))


class LaunchFailedTests(unittest.TestCase):
    def test_A_launch_prefix_that_exits_before_run_sh_is_launch_failed_never_a_case_verdict(self):
        ws = Workspace(self, REPLAY, FILES)
        with self.assertRaises(rl.LaunchFailed) as raised:
            ws.run(case(), prefix=failing_prefix(), status_fd=STATUS_FD)
        failed = raised.exception
        self.assertEqual(failed.case_id, "s03")
        self.assertIn("exited before the sandbox reported child-pid", failed.reason)
        self.assertIn("boom: the box could not be made", failed.stderr, "the prefix's own standard error travels with the failure")
        self.assertTrue(os.path.exists(os.path.join(ws.dir, "answer.bin")))

    def test_A_launch_that_failed_is_exit_3_with_the_stderr_and_no_verdict_line_through_the_command_line(self):
        import test_run_samples as cli  # the command line of part i, driven here with the prefix of the layer
        box = cli.Box(self)
        out_json = os.path.join(box.out, "r.json")
        import json
        code, out, err = box.cli("--launch-prefix", json.dumps(failing_prefix()), "--launch-status", str(STATUS_FD), "--json", out_json)
        self.assertEqual(code, 3, err)
        self.assertIn("launch failed", err)
        self.assertIn("boom: the box could not be made", err)
        self.assertNotIn("PASS", out + err)
        self.assertNotIn("FAIL", out + err)
        record = cli.read_json(out_json)
        self.assertEqual(record["launch"]["failed"], True)
        self.assertIn("boom", record["launch"]["stderr"])
        self.assertEqual(record["cases"], [])

    def test_The_stderr_in_a_launch_failure_is_bounded(self):
        ws = Workspace(self, REPLAY, FILES)
        loud = ["sh", "-c", "head -c 300000 /dev/zero | tr '\\0' 'x' >&2; exit 1", "sh"]
        with self.assertRaises(rl.LaunchFailed) as raised:
            ws.run(case(), prefix=loud, status_fd=STATUS_FD)
        self.assertLessEqual(len(raised.exception.stderr), ex.TAIL_BYTES)

    def test_A_launcher_that_never_reports_and_never_exits_is_killed_at_the_ceiling_and_nothing_is_left(self):
        ws = Workspace(self, REPLAY, FILES)
        silent = ["sh", "-c", "sleep 30 & echo $! > pids; wait", "sh"]
        with mock.patch.object(ex, "LAUNCH_CEILING_MS", 300):
            with self.assertRaises(rl.LaunchFailed) as raised:
                ws.run(case(), prefix=silent, status_fd=STATUS_FD)
        self.assertIn("no child-pid within", raised.exception.reason)
        with open(os.path.join(ws.dir, "pids")) as f:
            child = int(f.read())
        self.assertTrue(wait_until(lambda: not os.path.exists(f"/proc/{child}")), "the launcher's own child died with its group")

    def test_A_launcher_that_reports_child_pid_is_no_launch_failure_even_when_the_program_then_fails(self):
        result = Workspace(self, "exit 3\n").run(case(expected=b""), prefix=reporting_prefix(), status_fd=STATUS_FD)
        self.assertEqual((result.status, result.reason), ("FAIL", "exit status 3"))

    def test_The_status_descriptor_and_the_pipe_are_closed_after_a_case_and_after_a_launch_failure(self):
        ws = Workspace(self, REPLAY, FILES)
        before = open_descriptors()
        ws.run(case(), prefix=reporting_prefix(), status_fd=STATUS_FD)
        with self.assertRaises(rl.LaunchFailed):
            ws.run(case(), prefix=failing_prefix(), status_fd=STATUS_FD)
        self.assertEqual(open_descriptors(), before)
        self.assertFalse(fd_is_open(STATUS_FD))


class ClockTests(unittest.TestCase):
    def test_The_case_clock_starts_at_child_pid_so_the_set_up_of_the_box_is_not_in_the_wall(self):
        ws = Workspace(self, REPLAY, FILES)
        result = ws.run(case(), prefix=reporting_prefix("sleep 1.5"), status_fd=STATUS_FD, limit_ms=20_000)
        self.assertEqual(result.status, "PASS")
        self.assertGreaterEqual(result.launch_ms, 1500)
        self.assertLess(result.wall_ms, result.launch_ms, f"wall {result.wall_ms} ms must not contain the 1.5 s the launcher took before child-pid")

    def test_A_set_up_slower_than_the_limit_is_still_no_time_limit(self):
        ws = Workspace(self, REPLAY, FILES)
        result = ws.run(case(), prefix=reporting_prefix("sleep 1.5"), status_fd=STATUS_FD, limit_ms=1000)
        self.assertEqual((result.status, result.reason), ("PASS", ""))

    def test_Without_a_launch_status_the_clock_starts_at_the_spawn_and_the_launch_time_is_unknown_not_zero(self):
        result = Workspace(self, REPLAY, FILES).run(case())
        self.assertIsNone(result.launch_ms)
        self.assertEqual(rp.case_record(result)["launchMs"]["known"], False)

    def test_With_a_launch_status_the_record_carries_launchMs_beside_wallMs(self):
        result = Workspace(self, REPLAY, FILES).run(case(), prefix=reporting_prefix(), status_fd=STATUS_FD)
        record = rp.case_record(result)
        self.assertIsInstance(record["launchMs"], int)
        self.assertIsInstance(record["wallMs"], int)

    def test_child_pid_is_the_launch_signal_and_not_the_pid_authority(self):
        bogus = ["sh", "-c", f'printf \'{{"child-pid": 1}}\\n\' >&{STATUS_FD}\n"$@"', "sh"]
        result = Workspace(self, REPLAY, FILES).run(case(), prefix=bogus, status_fd=STATUS_FD)
        self.assertEqual(result.status, "PASS")
        self.assertNotEqual(result.lifecycle["signalled"]["pid"], 1)

    def test_The_program_status_is_the_exit_code_line_when_there_is_one_and_the_launchers_own_when_there_is_not(self):
        liar = ["sh", "-c", f'printf \'{{"child-pid": 5}}\\n\' >&{STATUS_FD}\n"$@"\nprintf \'{{"exit-code": 3}}\\n\' >&{STATUS_FD}\nexit 0', "sh"]
        result = Workspace(self, REPLAY, FILES).run(case(), prefix=liar, status_fd=STATUS_FD)
        self.assertEqual((result.status, result.reason), ("FAIL", "exit status 3"))
        silent = ["sh", "-c", f'printf \'{{"child-pid": 5}}\\n\' >&{STATUS_FD}\n"$@"\nexit 4', "sh"]
        result = Workspace(self, REPLAY, FILES).run(case(), prefix=silent, status_fd=STATUS_FD)
        self.assertEqual((result.status, result.reason), ("FAIL", "exit status 4"))


class StatusRuleTests(unittest.TestCase):
    """P2: before child-pid the status stream follows its rules or the launch has failed; after it everything is the case."""

    def launch(self, before, after="", run_sh=REPLAY):
        script = f'{before}\nprintf \'{{"child-pid": 5}}\\n\' >&{STATUS_FD}\n{after}\n"$@"'
        return Workspace(self, run_sh, FILES).run(case(), prefix=["sh", "-c", script, "sh"], status_fd=STATUS_FD)

    def failed(self, before, after=""):
        with self.assertRaises(rl.LaunchFailed) as raised:
            self.launch(before, after)
        return raised.exception

    def test_A_malformed_record_before_child_pid_is_launch_failed_never_a_verdict(self):
        failure = self.failed(f"echo 'this is not json' >&{STATUS_FD}")
        self.assertIn("the launch status is malformed", failure.reason)

    def test_A_record_that_is_not_an_object_is_launch_failed_too(self):
        self.assertIn("malformed", self.failed(f"echo '[1, 2]' >&{STATUS_FD}").reason)

    def test_A_duplicate_child_pid_is_launch_failed(self):
        failure = self.failed("", f'printf \'{{"child-pid": 6}}\\n\' >&{STATUS_FD}')
        self.assertIn("doubled", failure.reason)

    def test_An_unknown_record_before_child_pid_is_launch_failed(self):
        failure = self.failed(f'printf \'{{"something": 1}}\\n\' >&{STATUS_FD}')
        self.assertIn("unknown record before child-pid", failure.reason)

    def test_After_child_pid_junk_and_unknown_records_are_the_case_and_no_launch_failure(self):
        result = self.launch("", f"echo junk >&{STATUS_FD}; printf '{{\"x\": 1}}\\n' >&{STATUS_FD}")
        self.assertEqual((result.status, result.reason), ("PASS", ""))

    def test_A_launcher_that_reports_and_exits_at_once_without_a_program_is_an_ordinary_end_judged_by_the_output(self):
        ws = Workspace(self, REPLAY, FILES)
        prefix = ["sh", "-c", f'printf \'{{"child-pid": 5}}\\n\' >&{STATUS_FD}\nexit 0', "sh"]
        result = ws.run(case(), prefix=prefix, status_fd=STATUS_FD)
        self.assertEqual((result.status, result.reason), ("FAIL", "output ends early"))


class InterruptWindowTests(unittest.TestCase):
    """No interrupt window may leave a launcher or a program alive (Opus 11, 18)."""

    def config(self, ws, **overrides):
        return ws.config(prefix=["sh", "-c", 'exec "$@"', "sh"], **overrides)

    def start(self, ws):
        return rl.start(self.config(ws), "s03", ex.PlainLifecycle(), os.path.join(ws.scratch, "s.err"), ws.dir)

    def test_A_launcher_is_registered_from_the_moment_it_exists_and_abandon_active_stops_it(self):
        seconds = "71.%d" % (os.getpid() % 1000 + 1000)
        ws = Workspace(self, f"exec sleep {seconds}\n")
        launched = self.start(ws)
        self.addCleanup(launched.close)
        self.assertEqual([entry[0] for entry in rl.ACTIVE], [launched.proc])
        self.assertTrue(wait_until(lambda: sleepers(seconds)))
        rl.abandon_active()
        self.assertEqual(rl.ACTIVE, [])
        self.assertTrue(wait_until(lambda: sleepers(seconds) == []), "the launcher and its program are stopped")
        launched.close()

    def test_A_case_that_ends_is_forgotten_so_abandon_active_never_touches_a_finished_launcher(self):
        ws = Workspace(self, REPLAY, FILES)
        launched = self.start(ws)
        launched.close()
        self.assertEqual(rl.ACTIVE, [])
        launched.proc.wait()

    def test_An_interrupt_right_after_the_spawn_leaves_the_launcher_registered_and_abandon_active_kills_it(self):
        seconds = "72.%d" % (os.getpid() % 1000 + 1000)
        ws = Workspace(self, f"exec sleep {seconds}\n")
        with mock.patch.object(rl, "_begin_or_abandon", side_effect=KeyboardInterrupt):  # the interrupt lands outside every handler
            with self.assertRaises(KeyboardInterrupt):
                self.start(ws)
        self.assertEqual(len(rl.ACTIVE), 1, "still registered")
        self.assertTrue(wait_until(lambda: sleepers(seconds)))
        rl.abandon_active()
        self.assertTrue(wait_until(lambda: sleepers(seconds) == []))

    def test_An_interrupt_while_the_pump_is_made_leaves_no_program_running(self):
        seconds = "73.%d" % (os.getpid() % 1000 + 1000)
        ws = Workspace(self, f"exec sleep {seconds}\n")
        with mock.patch.object(rs_pump.Pump, "__init__", side_effect=KeyboardInterrupt):
            with self.assertRaises(KeyboardInterrupt):
                ws.run(case(expected=b""))
        self.assertTrue(wait_until(lambda: sleepers(seconds) == []), "the pump is made inside the try: the program is killed")
        self.assertEqual(rl.ACTIVE, [])

    def test_The_stopping_signals_wait_while_the_launcher_is_spawned_and_come_back_after(self):
        blocked = lambda: signal.pthread_sigmask(signal.SIG_BLOCK, [])
        self.assertFalse(set(rl.STOPPING_SIGNALS) & blocked())
        with rl._deferred_signals():
            self.assertEqual(set(rl.STOPPING_SIGNALS) & blocked(), set(rl.STOPPING_SIGNALS))
        self.assertFalse(set(rl.STOPPING_SIGNALS) & blocked())

    def test_A_launcher_that_was_reaped_is_never_sent_a_group_kill_and_one_that_was_not_is(self):
        reaped = subprocess.Popen(["true"])
        reaped.wait()
        running = subprocess.Popen(["sleep", "30"], start_new_session=True)
        self.addCleanup(lambda: (running.kill(), running.wait()))
        with mock.patch.object(ex, "signal_group") as sent:
            rl._abandon(reaped, ex.PlainLifecycle())
            self.assertEqual(sent.call_count, 0, "the pid of a reaped launcher may be someone else's by now")
        with mock.patch.object(ex, "signal_group", wraps=ex.signal_group) as sent:
            rl._abandon(running, ex.PlainLifecycle())
            self.assertEqual(sent.call_count, 1)
        self.assertIsNotNone(running.poll() if running.poll() is not None else running.wait())


class SeekTests(unittest.TestCase):
    def test_A_program_that_is_never_found_does_not_hold_back_its_input_or_output(self):
        ws = Workspace(self, "cat\n")
        cgroup = fake_cgroup(ws)  # cgroup.procs stays empty: the pid is never found
        data = os.urandom(200_000)
        result = ws.run(case(expected=data, stdin=data), prefix=box_prefix(fake_box(self)), status_fd=STATUS_FD, cgroup=cgroup, limit_ms=5000)
        self.assertEqual((result.status, result.reason), ("PASS", ""))
        self.assertLess(result.wall_ms, 900, "the old seek waited up to a second before the first byte moved")
        self.assertIsNone(result.lifecycle["signalled"]["pid"])


class EmptyProgramTests(unittest.TestCase):
    def timed(self, ws, **overrides):
        return rl.time_empty(ws.config(**overrides), lambda config, case_id: config.lifecycle, os.path.join(ws.scratch, "empty.stderr"))

    def ms(self, ws, **overrides):
        return self.timed(ws, **overrides).wall_ms

    def test_An_empty_program_is_timed_through_the_same_prefix_and_clock(self):
        ws = Workspace(self, REPLAY, FILES)
        self.assertIsInstance(self.ms(ws), int)
        run = self.timed(ws, prefix=reporting_prefix("sleep 1.5"), status_fd=STATUS_FD)
        self.assertGreaterEqual(run.launch_ms, 1500)
        self.assertLess(run.wall_ms, 1000, "from child-pid on, so the 1.5 s set-up is no part of it")
        self.assertEqual(run.program["pid"], None, "no bwrap, no pid: the end was the exit-code record")

    def test_The_empty_program_gets_the_prefix_with_its_own_case_name_and_no_workspace_files_are_run(self):
        ws = Workspace(self, "echo ran > ran.txt\n")
        marker = ["sh", "-c", f'echo "$0" > "{ws.root}/name.txt"\nexec "$@"', "{case}"]
        self.timed(ws, prefix=marker)
        with open(os.path.join(ws.root, "name.txt")) as f:
            self.assertEqual(f.read().strip(), ex.EMPTY_CASE)
        self.assertFalse(os.path.exists(os.path.join(ws.dir, "ran.txt")))

    def test_A_failing_launch_of_the_empty_program_is_a_launch_failure_too(self):
        with self.assertRaises(rl.LaunchFailed):
            self.timed(Workspace(self, REPLAY, FILES), prefix=failing_prefix(), status_fd=STATUS_FD)

    def test_The_empty_program_says_which_pid_it_waited_on_and_how_long_the_launch_took(self):
        run = self.timed(Workspace(self, REPLAY, FILES))
        self.assertEqual((run.program["pid"], run.program["launcher"]), (run.program["launcher"], run.program["launcher"]), "no box: the launcher is the program")
        self.assertIsNone(run.launch_ms, "no launch status: unknown, not zero")
        record = layer.empty_record(run)
        self.assertEqual(sorted(record), ["comm", "found", "launchMs", "ms", "pid"])

    def test_The_JSON_result_carries_emptyProgramMs_once_per_run(self):
        import test_run_samples as cli
        box = cli.Box(self)
        out_json = os.path.join(box.out, "r.json")
        self.assertEqual(box.cli("--json", out_json)[0], 0)
        record = cli.read_json(out_json)
        self.assertIsInstance(record["emptyProgramMs"], int)
        self.assertEqual(sorted(record["emptyProgram"]), ["comm", "found", "launchMs", "ms", "pid"])
        self.assertEqual(record["launch"], {"failed": False, "stderr": ""})


def env_of(ws):
    with open(os.path.join(ws.dir, "env.bin"), "rb") as f:
        return {k: v for k, v in (item.split(b"=", 1) for item in f.read().split(b"\0") if item)}


def unexpected_keys(printed, runner_env, allowed=BASH_OWN_KEYS):
    """The keys of the program's printed environment that are neither the runner's own nor bash's own: V11 says there are none."""
    own = {k.encode() for k in runner_env} | {k.encode() for k in allowed}
    return sorted(set(printed) - own)


class EnvironmentTests(unittest.TestCase):
    def test_The_program_environment_is_exactly_the_V11_set(self):
        ws = Workspace(self, "env -0 > env.bin\n")
        with mock.patch.dict(os.environ, {"BENCH_V11_OWN": "yes", "LC_ALL": "de_DE.UTF-8"}):
            ws.run(case(expected=b""))
            printed = env_of(ws)
            runner_env = dict(os.environ)
        self.assertEqual(unexpected_keys(printed, runner_env), [], "nothing outside the runner's own set and bash's")
        self.assertEqual(sorted(k for k in runner_env if k.encode() not in printed and k not in BASH_OWN_KEYS), [], "and nothing of the runner's own is dropped")
        for key, value in runner_env.items():
            if key not in BASH_OWN_KEYS:
                self.assertEqual(printed[key.encode()], value.encode(), key)

    def test_A_planted_extra_key_is_still_found(self):
        ws = Workspace(self, "export BENCH_PLANTED=1\nenv -0 > env.bin\n")
        ws.run(case(expected=b""))
        self.assertEqual(unexpected_keys(env_of(ws), dict(os.environ)), [b"BENCH_PLANTED"])

    def test_A_key_the_runner_has_reaches_the_program_and_one_it_lacks_does_not(self):
        ws = Workspace(self, "env -0 > env.bin\n")
        with mock.patch.dict(os.environ, {"BENCH_HAS": "1"}):
            os.environ.pop("BENCH_LACKS", None)
            ws.run(case(expected=b""))
        printed = env_of(ws)
        self.assertEqual((b"BENCH_HAS" in printed, b"BENCH_LACKS" in printed), (True, False))

    def test_The_environment_is_the_runners_own_plus_PORT_plus_the_scenario_lines_and_nothing_else(self):
        with mock.patch.dict(os.environ, {"BENCH_OWN": "1"}, clear=True):
            env = ex.program_env({"PORT": "8080", "SCENARIO_KEY": "v"})
            self.assertEqual(env, {"BENCH_OWN": "1", "PORT": "8080", "SCENARIO_KEY": "v"})
            self.assertEqual(ex.program_env(), {"BENCH_OWN": "1"})
            self.assertIsNot(ex.program_env(), os.environ, "a copy: later changes do not reach a program already started")

    def test_The_launcher_gets_the_same_environment_as_the_program_would(self):
        ws = Workspace(self, "env -0 > env.bin\n")
        prefix = ["env", "-0"]
        self.assertEqual(ex.launch_argv(prefix, "s03")[:2], ["env", "-0"])
        ws.run(case(expected=b""), prefix=["sh", "-c", 'exec "$@"', "sh"])
        self.assertEqual(unexpected_keys(env_of(ws), dict(os.environ)), [])


class StreamTests(unittest.TestCase):
    """V12 for an olympiad case: standard error is always a file, the 16 MiB cap names what is kept, and truncation is recorded."""

    def test_A_standard_error_over_16_MiB_is_recorded_as_truncated_and_the_report_keeps_only_a_tail(self):
        result = Workspace(self, "head -c 17000000 /dev/zero >&2\ncat answer.bin\n", FILES).run(case())
        stderr = result.lifecycle["stderr"]
        self.assertEqual((result.status, stderr["bytes"], stderr["truncated"]), ("PASS", 17_000_000, True))
        self.assertLessEqual(len(stderr["tail"]), ex.TAIL_BYTES * 4)

    def test_A_standard_error_under_the_cap_is_not_truncated(self):
        stderr = Workspace(self, "echo oops >&2\ncat answer.bin\n", FILES).run(case()).lifecycle["stderr"]
        self.assertEqual((stderr["bytes"], stderr["truncated"], stderr["tail"]), (5, False, "oops\n"))

    def test_The_caps_are_the_ones_of_V12(self):
        self.assertEqual((ex.CAPTURE_CAP, ex.TEST_CEILING_MS), (16 * 1024 * 1024, 600_000))

    def test_A_registered_limit_above_the_600_s_bound_for_one_test_is_refused_with_exit_4(self):
        import test_run_samples as cli
        box = cli.Box(self)
        box.write(box.root, "limits.json", '{"timeS": 601}')
        self.assertEqual(box.cli("--limits", os.path.join(box.root, "limits.json"))[0], 4)
        box.write(box.root, "limits.json", '{"timeS": 600}')
        self.assertEqual(box.cli("--limits", os.path.join(box.root, "limits.json"))[0], 0)


class CommandLineTests(unittest.TestCase):
    """The options of the execution layer as the command line takes them."""

    def test_The_status_option_needs_the_prefix_a_descriptor_number_and_a_free_one(self):
        import test_run_samples as cli
        box = cli.Box(self)
        for args in (("--launch-status", "7"), ("--launch-status", "x", "--launch-prefix", "[]"), ("--launch-status", "1", "--launch-prefix", "[]"),
                     ("--launch-status", "-3", "--launch-prefix", "[]")):
            code, _, err = box.cli(*args)
            self.assertEqual(code, 4, args)
            self.assertIn("--launch-status", err)

    def test_The_layer_options_together_give_a_PASS_and_a_result_with_every_new_field(self):
        import json
        import test_run_samples as cli
        box = cli.Box(self)
        views, cgroups = os.path.join(box.root, "views"), os.path.join(box.root, "cg")
        os.mkdir(views)
        os.mkdir(cgroups)
        for sub in (views, cgroups):
            os.mkdir(os.path.join(sub, "s03"))
            os.mkdir(os.path.join(sub, "empty-program"))  # the control's {case}: the layer provides one by that name
        for name in ("run.sh", "build.sh", "answer.bin"):
            with open(os.path.join(box.ws, name), "rb") as src, open(os.path.join(views, "s03", name), "wb") as dst:
                dst.write(src.read())
        for case_name in ("s03", "empty-program"):
            for name, text in (("memory.peak", "5000"), ("memory.events", "oom_kill 0\n"), ("cgroup.procs", "")):
                with open(os.path.join(cgroups, case_name, name), "w") as f:
                    f.write(text)
        out_json = os.path.join(box.out, "r.json")
        code, out, err = box.cli("--launch-prefix", json.dumps(reporting_prefix()), "--launch-status", str(STATUS_FD), "--cgroup-of", os.path.join(cgroups, "{case}"),
                                 "--case-view", os.path.join(views, "{case}"), "--json", out_json)
        self.assertEqual(code, 0, err)
        self.assertIn("  memory: peak 5000 bytes", out)
        record = cli.read_json(out_json)
        case_record = record["cases"][0]
        self.assertIsInstance(case_record["launchMs"], int)
        self.assertEqual(case_record["lifecycle"]["memory"]["peakBytes"], 5000)
        self.assertEqual(sorted(case_record["lifecycle"]), ["capture", "fork", "kill", "leftover", "leftoverEvidence", "memory", "signalled", "stderr", "stop"])
        self.assertEqual(set(case_record["lifecycle"]["signalled"]), {"signal", "pid", "found", "comm", "launcher"})
        self.assertEqual((record["launch"], isinstance(record["emptyProgramMs"], int)), ({"failed": False, "stderr": ""}, True))

    def test_An_oom_kill_in_the_cgroup_is_memory_limit_and_exit_1_on_the_command_line(self):
        import test_run_samples as cli
        box = cli.Box(self)
        cg = os.path.join(box.root, "cg")
        os.makedirs(os.path.join(cg, "s03"))
        for name, text in (("memory.peak", "9"), ("memory.events", "oom_kill 1\n"), ("cgroup.procs", "")):
            with open(os.path.join(cg, "s03", name), "w") as f:
                f.write(text)
        code, out, _ = box.cli("--cgroup-of", os.path.join(cg, "{case}"))
        self.assertEqual(code, 1)
        self.assertIn("FAIL s03 — memory limit", out)

    def test_A_case_view_that_is_missing_is_exit_3_the_environment(self):
        import test_run_samples as cli
        box = cli.Box(self)
        code, _, err = box.cli("--case-view", os.path.join(box.root, "views", "{case}"))
        self.assertEqual(code, 3)
        self.assertIn("is not a directory", err)


if __name__ == "__main__":
    unittest.main()
