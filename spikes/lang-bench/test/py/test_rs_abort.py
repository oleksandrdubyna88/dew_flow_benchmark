"""A runtime's own out-of-memory abort is `memory limit`, exactly like a kernel oom_kill (plan change 5.1, parent D15's OP-K) — but only WITH evidence (fix batch 1, O7): `memory.peak` at least half of
`memory.max`. .NET: exit status 134 with the stderr line exactly `Out of memory.`, or with an `Unhandled exception.` line and `System.OutOfMemoryException` anywhere in the tail (also one wrapped in an
`AggregateException`); V8: exit status 134 with a line holding `JavaScript heap out of memory`. The record says which. Never from exit 134 alone, never from a message alone, never without the peak."""
import os
import tempfile
import unittest

import runner_path  # noqa: F401
import rs_abort
import rs_exec as ex
import rs_olympiad as ro

V8_LINE = "FATAL ERROR: Reached heap limit Allocation failed - JavaScript heap out of memory"
WRAPPED = ("Unhandled exception. System.AggregateException: One or more errors occurred. (Exception of type 'System.OutOfMemoryException' was thrown.)\n"
           " ---> System.OutOfMemoryException: Exception of type 'System.OutOfMemoryException' was thrown.\n   at Program.<Main>$(String[] args)\n")
HIGH = {"known": True, "peakBytes": 200, "maxBytes": 256}
LOW = {"known": True, "peakBytes": 10, "maxBytes": 256}


def stderr_file(test, text):
    holder = tempfile.TemporaryDirectory()
    test.addCleanup(holder.cleanup)
    path = os.path.join(holder.name, "err")
    with open(path, "w") as f:
        f.write(text)
    return path


class Workspace:
    """A temporary workspace whose run.sh is the stub under test (the plain path: no cgroup, so no evidence)."""

    def __init__(self, test, run_sh):
        holder = tempfile.TemporaryDirectory()
        test.addCleanup(holder.cleanup)
        self.dir, self.scratch = os.path.join(holder.name, "ws"), os.path.join(holder.name, "scratch")
        os.mkdir(self.dir)
        os.mkdir(self.scratch)
        with open(os.path.join(self.dir, "run.sh"), "w") as f:
            f.write(run_sh)

    def run(self):
        return ro.run_case(ro.Case("s01", b"", b""), ex.Config(workspace=self.dir, scratch=self.scratch, limit_ms=20_000))


class AbortRuleTests(unittest.TestCase):
    def test_A_dotnet_abort_at_a_high_peak_is_a_dotnet_abort(self):
        self.assertEqual(rs_abort.runtime_abort(134, stderr_file(self, "Out of memory.\n"), HIGH), "dotnet")

    def test_A_V8_heap_abort_at_a_high_peak_is_a_v8_abort(self):
        text = f"<--- Last few GCs --->\n{V8_LINE}\n----- Native stack trace -----\n"
        self.assertEqual(rs_abort.runtime_abort(134, stderr_file(self, text), HIGH), "v8")

    def test_A_faked_out_of_memory_line_at_a_low_peak_stays_a_runtime_failure(self):
        self.assertIsNone(rs_abort.runtime_abort(134, stderr_file(self, "Out of memory.\n"), LOW), "a program that prints the line and aborts at 10 of 256 bytes is not out of memory")
        self.assertIsNone(rs_abort.runtime_abort(134, stderr_file(self, V8_LINE + "\n"), LOW))

    def test_Exactly_half_of_memory_max_is_enough_and_one_byte_less_is_not(self):
        self.assertEqual(rs_abort.runtime_abort(134, stderr_file(self, "Out of memory.\n"), {"known": True, "peakBytes": 128, "maxBytes": 256}), "dotnet")
        self.assertIsNone(rs_abort.runtime_abort(134, stderr_file(self, "Out of memory.\n"), {"known": True, "peakBytes": 127, "maxBytes": 256}))

    def test_Without_a_read_peak_or_a_numeric_memory_max_there_is_no_evidence(self):
        path = stderr_file(self, "Out of memory.\n")
        for memory in ({"known": False, "why": "not certified"}, {"known": True, "peakBytes": 200}, {"known": True, "peakBytes": 200, "maxBytes": None}, {}):
            self.assertIsNone(rs_abort.runtime_abort(134, path, memory), memory)

    def test_An_unhandled_OutOfMemoryException_wrapped_in_an_AggregateException_at_a_high_peak_is_a_dotnet_abort(self):
        self.assertEqual(rs_abort.runtime_abort(134, stderr_file(self, WRAPPED), HIGH), "dotnet")
        self.assertEqual(rs_abort.runtime_abort(134, stderr_file(self, "Unhandled exception. System.OutOfMemoryException: x\n"), HIGH), "dotnet")
        self.assertIsNone(rs_abort.runtime_abort(134, stderr_file(self, WRAPPED), LOW), "a wrapped one at a low peak stays a runtime failure")

    def test_The_exception_needs_the_unhandled_line_and_a_message_other_than_that_alone_does_not_count(self):
        self.assertIsNone(rs_abort.runtime_abort(134, stderr_file(self, "caught System.OutOfMemoryException and went on\n"), HIGH), "no Unhandled exception. line")
        self.assertIsNone(rs_abort.runtime_abort(134, stderr_file(self, "Unhandled exception. System.InvalidOperationException: x\n"), HIGH))

    def test_An_abort_with_another_message_stays_a_runtime_failure(self):
        self.assertIsNone(rs_abort.runtime_abort(134, stderr_file(self, "abort() called\n"), HIGH))

    def test_The_dotnet_line_must_be_the_whole_line(self):
        self.assertIsNone(rs_abort.runtime_abort(134, stderr_file(self, "Out of memory. probably\nx Out of memory.\n"), HIGH))

    def test_The_message_alone_without_exit_134_is_no_abort(self):
        self.assertIsNone(rs_abort.runtime_abort(1, stderr_file(self, f"Out of memory.\n{V8_LINE}\n"), HIGH))
        self.assertIsNone(rs_abort.runtime_abort(133, stderr_file(self, "Out of memory.\n"), HIGH))

    def test_On_the_plain_path_with_no_cgroup_an_abort_line_is_a_runtime_failure_and_the_record_says_none(self):
        result = Workspace(self, 'echo "Out of memory." >&2; exit 134\n').run()
        self.assertEqual((result.status, result.reason), ("FAIL", "exit status 134"))
        self.assertIsNone(result.lifecycle["memory"]["runtimeAbort"])

    def test_A_clean_run_records_no_abort(self):
        result = Workspace(self, "exit 0\n").run()
        self.assertEqual((result.status, result.lifecycle["memory"]["runtimeAbort"]), ("PASS", None))

    def test_The_rule_reads_the_end_of_the_stderr_file_whatever_its_size(self):
        long = "x\n" * 100_000 + "Out of memory.\n"
        self.assertEqual(rs_abort.runtime_abort(134, stderr_file(self, long), HIGH), "dotnet")
        self.assertIsNone(rs_abort.runtime_abort(134, os.path.join(tempfile.gettempdir(), "testsToDelete-no-such-stderr"), HIGH))


if __name__ == "__main__":
    unittest.main()
