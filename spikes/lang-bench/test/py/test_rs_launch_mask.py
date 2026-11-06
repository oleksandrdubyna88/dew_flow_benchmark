"""The signal mask of a launched program (rs_launch `_unblock_all`): the runner holds its stopping signals off around the spawn, and the program must not inherit that, or a daemon or web
scenario's SIGTERM would never reach it. `test_The_unblock...` is in-process; `test_A_spawned_...` starts one small python child and reads its own mask (it needs a program: run it after the series)."""
import os
import signal
import subprocess
import sys
import tempfile
import unittest

import runner_path  # noqa: F401
import rs_launch as rl


def blocked():
    return signal.pthread_sigmask(signal.SIG_BLOCK, ())


class MaskTests(unittest.TestCase):
    def test_The_unblock_clears_the_signals_the_runner_holds_off_around_the_spawn(self):
        with rl._deferred_signals():
            self.assertTrue(set(rl.STOPPING_SIGNALS) <= set(blocked()), "the runner blocks its stopping signals around the spawn")
            rl._unblock_all()
            self.assertEqual(set(blocked()), set())

    def test_A_spawned_program_starts_with_its_stopping_signals_unblocked(self):
        code = "import signal; print(sorted(int(s) for s in signal.pthread_sigmask(signal.SIG_BLOCK, ())))"
        with tempfile.TemporaryDirectory() as scratch:
            with rl._deferred_signals():
                proc = rl._spawn([sys.executable, "-I", "-c", code], scratch, os.path.join(scratch, "err"), None, (subprocess.DEVNULL, subprocess.PIPE))
                out, _ = proc.communicate(timeout=30)
        self.assertEqual(out.decode().strip(), "[]")


if __name__ == "__main__":
    unittest.main()
