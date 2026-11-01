"""The look into the cgroup is paced by time, never by I/O (N1); the end of a case is stamped when the record is read (N13); the pidfd of the
program is forgotten once handled (D2); the clock rounds one way for every figure it reports (N11)."""
import os
import subprocess
import time
import unittest
from unittest import mock

import runner_path  # noqa: F401
import rs_exec as ex
import rs_launch as rl
import rs_olympiad as ro
import rs_pump
import rs_scope
import rs_track as tr
from box_support import STATUS_FD, Workspace, case, fake_cgroup
from test_rs_cgroup import Folder

PRINTED = 64 * 1024 * 1024


def printer(test, with_cgroup):
    """A case that prints 64 MiB: many I/O wake-ups, each of which used to cost a look into a cgroup that lists three live processes."""
    ws = Workspace(test, f"head -c {PRINTED} /dev/zero\n")
    kw = {}
    if with_cgroup:
        sleepers = [subprocess.Popen(["sleep", "60"]) for _ in range(3)]
        test.addCleanup(lambda: [(p.kill(), p.wait()) for p in sleepers])
        cgroup = fake_cgroup(ws, files={"memory.peak": "0", "memory.events": "oom_kill 0\n"})
        listing = " ".join(str(p.pid) for p in sleepers)
        ws.write(ws.dir, "run.sh", f'printf "%s\\n" {listing} > "{cgroup}/cgroup.procs"\nhead -c {PRINTED} /dev/zero\n: > "{cgroup}/cgroup.procs"\n')
        kw["cgroup"] = cgroup
    started = time.monotonic()
    result = ws.run(case(expected=b"\0" * PRINTED, stdin=b""), limit_ms=60_000, **kw)
    return result, (time.monotonic() - started) * 1000


class LookPaceTests(unittest.TestCase):
    def test_The_look_count_is_bounded_by_the_wall_over_the_tick_and_not_by_the_number_of_reads(self):
        looks = []
        real = tr.Tracker.look

        def counting(tracker):
            looks.append(1)
            return real(tracker)

        with mock.patch.object(tr.Tracker, "look", counting):
            result, _ = printer(self, True)
        self.assertEqual(result.status, "PASS")
        bound = result.wall_ms // int(ex.TRACK_TICK_S * 1000) + 3
        self.assertLessEqual(len(looks), bound, f"{len(looks)} looks in {result.wall_ms} ms: one per {int(ex.TRACK_TICK_S * 1000)} ms at most, not one per read")

    def test_A_case_with_a_cgroup_is_not_slower_than_the_plain_one_by_the_cost_of_looking(self):
        plain = min(printer(self, False)[0].wall_ms for _ in range(3))
        scoped = min(printer(self, True)[0].wall_ms for _ in range(3))
        self.assertLessEqual(scoped, plain * 1.6 + 40, f"plain {plain} ms, with --cgroup-of {scoped} ms: the look must not tax every read")


class EndStampTests(unittest.TestCase):
    def test_An_exit_code_that_arrives_in_the_same_read_as_child_pid_ends_the_case_at_once_and_the_teardown_is_not_program_time(self):
        started = time.monotonic()
        both = f'printf \'{{"child-pid": 5}}\\n{{"exit-code": 0}}\\n\' >&{STATUS_FD}\n"$@"\nsleep 1.5'
        result = Workspace(self, "exit 0\n").run(case(expected=b""), prefix=["sh", "-c", both, "sh"], status_fd=STATUS_FD, limit_ms=1000)
        self.assertEqual((result.status, result.reason), ("PASS", ""), f"wall {result.wall_ms} ms")
        self.assertLess(result.wall_ms, 1000, "the record was read with child-pid: the 1.5 s of teardown is not the program's")
        self.assertGreaterEqual(time.monotonic() - started, 1.5, "the launcher did linger")

    def test_The_end_instant_is_the_one_stamped_when_the_record_was_read_and_never_before_the_start(self):
        import rs_status as st
        reader = st.StatusReader()
        reader.feed(b'{"child-pid": 5}\n{"exit-code": 0}\n')
        self.assertLessEqual(reader.child_seen_ns, reader.exit_seen_ns)
        self.assertGreater(reader.child_seen_ns, 0)


class WatchedFdTests(unittest.TestCase):
    def test_The_program_pidfd_is_forgotten_once_handled_and_not_registered_again_by_a_later_look(self):
        proc = subprocess.Popen(["true"], stdin=subprocess.PIPE, stdout=subprocess.PIPE)
        self.addCleanup(proc.wait)
        fd = ex.pidfd_open(proc.pid)
        self.addCleanup(ex.close_fd, fd)
        tracker = tr.Tracker(proc, rs_scope.ScopeLifecycle(Folder(self, {"cgroup.procs": ""}).dir), box=True)
        tracker.pidfd = fd
        launched = rl.Launched(proc, None, ex.now_ns(), None, None, None, tracker)
        pump = rs_pump.Pump(launched, b"", ro.Comparer(b"", 1024), os.devnull)
        self.addCleanup(pump.close)
        self.assertEqual(pump.watching, fd)
        pump.program_exited()
        self.assertIsNone(pump.watching, "no stale descriptor stays in the state")
        pump._look()
        self.assertNotIn(fd, pump.sel.get_map(), "a descriptor already handled is not watched again")
        self.assertIsNone(pump.watching)


class RoundingTests(unittest.TestCase):
    def test_Every_reported_millisecond_figure_is_rounded_up_the_same_way(self):
        self.assertEqual([ex.ceil_ms(ns) for ns in (0, 1, 999_999, 1_000_000, 1_000_001)], [0, 1, 1, 1, 2])

    def test_The_launch_time_and_the_empty_programs_wall_use_the_same_rounding_as_the_wall(self):
        self.assertEqual(rl.launch_ms(10, 10 + 1_000_001), 2)
        pump = mock.Mock(exit_ns=2_000_001)
        launched = mock.Mock(clock_ns=1_000_000)
        self.assertEqual(rl._empty_wall("exited", pump, launched), 2)
        self.assertEqual(ro._wall_ms(mock.Mock(wall_ns=1_000_001)), 2)
        self.assertIsNone(rl.launch_ms(5, None))


if __name__ == "__main__":
    unittest.main()
