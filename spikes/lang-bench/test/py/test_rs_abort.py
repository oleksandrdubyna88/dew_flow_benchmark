"""A runtime's own out-of-memory abort is `memory limit`, exactly like a kernel oom_kill (plan change 5.1, parent D15's OP-K): .NET's exit status 134 with the stderr line exactly `Out of memory.`,
V8's exit status 134 with a stderr line holding `JavaScript heap out of memory`. The record says which. It is never inferred from exit 134 alone, and never from a message alone."""
import os
import tempfile
import unittest

import runner_path  # noqa: F401
import rs_abort
import rs_exec as ex
import rs_olympiad as ro

V8_LINE = "FATAL ERROR: Reached heap limit Allocation failed - JavaScript heap out of memory"


class Workspace:
    """A temporary workspace whose run.sh is the stub under test."""

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


def stderr_file(test, text):
    holder = tempfile.TemporaryDirectory()
    test.addCleanup(holder.cleanup)
    path = os.path.join(holder.name, "err")
    with open(path, "w") as f:
        f.write(text)
    return path


class AbortRuleTests(unittest.TestCase):
    def test_A_dotnet_abort_is_memory_limit_and_the_record_says_dotnet(self):
        result = Workspace(self, 'echo "Out of memory." >&2; exit 134\n').run()
        self.assertEqual((result.status, result.reason), ("FAIL", "memory limit"))
        self.assertEqual(result.lifecycle["memory"]["runtimeAbort"], "dotnet")

    def test_A_V8_heap_abort_is_memory_limit_and_the_record_says_v8(self):
        result = Workspace(self, f'echo "<--- Last few GCs --->" >&2; echo "{V8_LINE}" >&2; echo "----- Native stack trace -----" >&2; exit 134\n').run()
        self.assertEqual((result.status, result.reason), ("FAIL", "memory limit"))
        self.assertEqual(result.lifecycle["memory"]["runtimeAbort"], "v8")

    def test_An_abort_with_another_message_stays_a_runtime_failure(self):
        result = Workspace(self, 'echo "abort() called" >&2; exit 134\n').run()
        self.assertEqual((result.reason, result.lifecycle["memory"]["runtimeAbort"]), ("exit status 134", None))

    def test_The_dotnet_line_must_be_the_whole_line(self):
        result = Workspace(self, 'echo "Out of memory. probably" >&2; echo "x Out of memory." >&2; exit 134\n').run()
        self.assertEqual((result.reason, result.lifecycle["memory"]["runtimeAbort"]), ("exit status 134", None))

    def test_The_message_alone_without_exit_134_is_no_abort(self):
        result = Workspace(self, f'echo "Out of memory." >&2; echo "{V8_LINE}" >&2; exit 1\n').run()
        self.assertEqual((result.reason, result.lifecycle["memory"]["runtimeAbort"]), ("exit status 1", None))

    def test_A_clean_run_records_no_abort(self):
        result = Workspace(self, "exit 0\n").run()
        self.assertEqual((result.status, result.lifecycle["memory"]["runtimeAbort"]), ("PASS", None))

    def test_The_rule_reads_the_end_of_the_stderr_file_whatever_its_size(self):
        long = "x\n" * 100_000 + "Out of memory.\n"
        self.assertEqual(rs_abort.runtime_abort(134, stderr_file(self, long)), "dotnet")
        self.assertIsNone(rs_abort.runtime_abort(133, stderr_file(self, long)))
        self.assertIsNone(rs_abort.runtime_abort(134, os.path.join(tempfile.gettempdir(), "testsToDelete-no-such-stderr")))


if __name__ == "__main__":
    unittest.main()
