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
import box_support as bs

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


MIB = 1024 * 1024
CLEAN = "oom 0\noom_kill 0\n"
KILLED = "oom 1\noom_kill 1\n"
SIX = ["abortBelowEvidence", "childAbortLine", "maxBytes", "oomKill", "peakBytes", "runtimeAbort"]


def at_peak(test, stub, peak, limit=256 * MIB, events=CLEAN, expected=b""):
    """One case through the runner whose cgroup (a fake node) holds `memory.peak` = `peak` and `memory.max` = `limit` after the program ran `stub`."""
    ws = bs.Workspace(test, stub)
    return ws.run(box_case(expected), cgroup=bs.fake_cgroup(ws, final={"memory.peak": str(peak), "memory.max": str(limit), "memory.events": events}))


def box_case(expected=b""):
    return ro.Case("s01", b"", expected, (), None, None)


DOTNET_STUB = 'echo "Out of memory." >&2; exit 134\n'
V8_STUB = f'echo "{V8_LINE}" >&2; exit 134\n'


class EvidenceBoundaryTests(unittest.TestCase):
    """T-L50 (D56): L is a whole number of MiB, so L/2 is whole and `peak * 2 >= max` is exact at the byte."""

    def test_An_abort_one_byte_below_half_the_limit_is_a_runtime_error_with_the_flag_and_that_is_the_point(self):
        for limit in (256 * MIB, 512 * MIB):
            for stub, kind in ((DOTNET_STUB, "dotnet"), (V8_STUB, "v8")):
                at_half = at_peak(self, stub, limit // 2, limit)
                self.assertEqual((at_half.status, at_half.reason), ("FAIL", "memory limit"), f"{kind} at L/2 of {limit}: evidence")
                self.assertEqual(at_half.lifecycle["memory"]["runtimeAbort"], kind)
                self.assertFalse(at_half.lifecycle["memory"]["abortBelowEvidence"])
                below = at_peak(self, stub, limit // 2 - 1, limit)
                self.assertEqual((below.status, below.reason), ("FAIL", "exit status 134"), f"{kind} one byte below L/2 of {limit}: no evidence, a runtime error")
                memory = below.lifecycle["memory"]
                self.assertTrue(memory["abortBelowEvidence"], "flagged")
                self.assertIsNone(memory["runtimeAbort"])
                self.assertEqual((memory["peakBytes"], memory["maxBytes"]), (limit // 2 - 1, limit), "the peak and the limit are kept")
                flagged = [line for line in below.lines if line.startswith("  abort-below-evidence:")]
                self.assertEqual(len(flagged), 1, "the flag is also a line of the report")
                self.assertIn(f"peak {limit // 2 - 1} of {limit} bytes", flagged[0])

    def test_The_threshold_is_at_least_half_and_not_more_than_half(self):
        self.assertTrue(rs_abort.has_evidence({"known": True, "peakBytes": 128 * MIB, "maxBytes": 256 * MIB}), ">=, not >")
        self.assertFalse(rs_abort.has_evidence({"known": True, "peakBytes": 128 * MIB - 1, "maxBytes": 256 * MIB}))

    def test_The_line_with_exit_1_or_exit_134_with_another_message_is_a_runtime_error_without_the_flag(self):
        for stub, reason in (('echo "Out of memory." >&2; exit 1\n', "exit status 1"), (f'echo "{V8_LINE}" >&2; exit 1\n', "exit status 1"), ('echo "abort() called" >&2; exit 134\n', "exit status 134")):
            result = at_peak(self, stub, 200 * MIB)
            self.assertEqual((result.status, result.reason), ("FAIL", reason), stub)
            memory = result.lifecycle["memory"]
            self.assertEqual((memory["abortBelowEvidence"], memory["childAbortLine"], memory["runtimeAbort"]), (False, False, None), stub)

    def test_An_oom_kill_is_memory_limit_at_any_peak_and_no_flag_explains_it(self):
        for stub in (DOTNET_STUB, V8_STUB, 'exit 0\n', 'echo "Out of memory." >&2; exit 0\n'):
            result = at_peak(self, stub, 1, events=KILLED)
            self.assertEqual((result.status, result.reason), ("FAIL", "memory limit"), stub)
            memory = result.lifecycle["memory"]
            self.assertEqual((memory["abortBelowEvidence"], memory["childAbortLine"]), (False, False), "the kernel's kill is the cause: nothing is left to flag")


class ChildAbortTests(unittest.TestCase):
    """D56: a runtime's abort line in the stderr of a program that did not abort itself."""

    def test_A_child_abort_line_under_an_exit_0_parent_is_judged_on_its_output(self):
        stub = 'echo "Out of memory." >&2; printf "hi"; exit 0\n'
        passed = at_peak(self, stub, 200 * MIB, expected=b"hi")
        self.assertEqual((passed.status, passed.reason), ("PASS", ""), "the output is right: a pass, never memory limit by the abort rule")
        self.assertTrue(passed.lifecycle["memory"]["childAbortLine"], "flagged for adjudication")
        self.assertTrue(any(line.startswith("  child-abort-line:") for line in passed.lines), "and a line of the report")
        self.assertIsNone(passed.lifecycle["memory"]["runtimeAbort"])
        failed = at_peak(self, stub, 200 * MIB, expected=b"hello")
        self.assertEqual((failed.status, failed.reason), ("FAIL", "output differs"), "judged on its output, fail as well")
        self.assertTrue(failed.lifecycle["memory"]["childAbortLine"])
        v8 = at_peak(self, f'echo "{V8_LINE}" >&2; printf "hi"; exit 0\n', 200 * MIB, expected=b"hi")
        self.assertEqual((v8.status, v8.lifecycle["memory"]["childAbortLine"]), ("PASS", True))

    def test_A_non_zero_parent_exit_with_a_childs_abort_line_is_a_runtime_error_flagged_child_abort_line(self):
        for code in (2, 3, 7):
            result = at_peak(self, f'echo "Out of memory." >&2; exit {code}\n', 200 * MIB)
            self.assertEqual((result.status, result.reason), ("FAIL", f"exit status {code}"), code)
            self.assertTrue(result.lifecycle["memory"]["childAbortLine"], code)
            self.assertIsNone(result.lifecycle["memory"]["runtimeAbort"])

    def test_A_clean_exit_with_no_line_carries_neither_flag(self):
        result = at_peak(self, 'printf "hi"\n', 200 * MIB, expected=b"hi")
        memory = result.lifecycle["memory"]
        self.assertEqual((result.status, memory["abortBelowEvidence"], memory["childAbortLine"]), ("PASS", False, False))

    def test_The_flags_are_computed_by_one_pure_function(self):
        facts = lambda code, text, memory: rs_abort.abort_facts(code, stderr_file(self, text), memory)  # noqa: E731
        self.assertEqual(facts(134, "Out of memory.\n", HIGH), {"runtimeAbort": "dotnet", "abortBelowEvidence": False, "childAbortLine": False})
        self.assertEqual(facts(134, "Out of memory.\n", LOW), {"runtimeAbort": None, "abortBelowEvidence": True, "childAbortLine": False})
        self.assertEqual(facts(0, "Out of memory.\n", HIGH), {"runtimeAbort": None, "abortBelowEvidence": False, "childAbortLine": True})
        self.assertEqual(facts(1, "Out of memory.\n", HIGH), {"runtimeAbort": None, "abortBelowEvidence": False, "childAbortLine": False})
        self.assertEqual(facts(0, "all good\n", HIGH), {"runtimeAbort": None, "abortBelowEvidence": False, "childAbortLine": False})
        self.assertEqual(facts(134, "Out of memory.\n", {"known": False}), {"runtimeAbort": None, "abortBelowEvidence": True, "childAbortLine": False}, "an unread peak is no evidence: flagged, not hidden")
        self.assertEqual(facts(0, "Out of memory.\n", {"known": True, "peakBytes": 1, "maxBytes": 256, "oomKill": 2}), {"runtimeAbort": None, "abortBelowEvidence": False, "childAbortLine": False})


class MemoryRecordTests(unittest.TestCase):
    def test_The_memory_record_names_all_six_fields(self):
        ran = at_peak(self, DOTNET_STUB, 200 * MIB)
        self.assertEqual(sorted(k for k in ran.lifecycle["memory"] if k in SIX), SIX)
        self.assertEqual({k: ran.lifecycle["memory"][k] for k in SIX}, {"peakBytes": 200 * MIB, "maxBytes": 256 * MIB, "oomKill": 0, "runtimeAbort": "dotnet", "abortBelowEvidence": False, "childAbortLine": False})
        plain = Workspace(self, "exit 0\n").run()
        memory = plain.lifecycle["memory"]
        self.assertEqual(sorted(k for k in memory if k in SIX), SIX, "the plain path has all six too")
        self.assertEqual((memory["peakBytes"], memory["maxBytes"], memory["oomKill"]), (None, None, None), "what was not read is unknown (null), never zero")
        self.assertEqual((memory["runtimeAbort"], memory["abortBelowEvidence"], memory["childAbortLine"]), (None, False, False))


if __name__ == "__main__":
    unittest.main()
