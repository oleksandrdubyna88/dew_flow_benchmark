"""Quick run W1: the daemon and web cases of the runner (rs_session) against toy programs, through the plain path (no box; the boxed path is
test/exec-layer-session.test.mjs). A harness failure is never a verdict; what a program does wrong is an entry of the actual transcript."""
import os
import unittest

import runner_path  # noqa: F401
from test_run_samples import DAEMON, FORMAT, WEB, Box, read_file

HERE = os.path.dirname(os.path.abspath(__file__))
TOYS = os.path.join(HERE, "..", "fixtures", "runner", "toys")


def toy(name):
    return read_file(os.path.join(TOYS, name))


def run_sh(script):
    return f"#!/usr/bin/env bash\nexec python3 -u {script}\n"


def scenario(*lines, head="scenario 2\ndefault within 1s\nstart ready port within 3s\n"):
    return head + "".join(line + "\n" for line in lines)


class SessionBox(Box):
    def __init__(self, test, kind, toy_name, script, transcript, name="s01"):
        super().__init__(test, kind=kind, run_sh=run_sh(toy_name), pairs={f"{name}.in": script, f"{name}.out": transcript})
        self.write(self.ws, toy_name, toy(toy_name))


class FormatExamples(unittest.TestCase):
    def test_The_daemon_example_of_format_passes_against_a_toy_store(self):
        box = SessionBox(self, DAEMON, "toy_store.py", read_file(os.path.join(FORMAT, "x2.in")), read_file(os.path.join(FORMAT, "x2.out")), "x2")
        code, out, err = box.cli("--no-build")
        self.assertEqual(code, 0, out + err)
        self.assertIn("PASS x2", out)

    def test_The_web_example_of_format_passes_against_a_toy_order_endpoint(self):
        box = SessionBox(self, WEB, "toy_orders.py", read_file(os.path.join(FORMAT, "x3.in")), read_file(os.path.join(FORMAT, "x3.out")), "x3")
        code, out, err = box.cli("--no-build")
        self.assertEqual(code, 0, out + err)

    def test_A_toy_that_answers_wrongly_fails_with_the_mismatch_and_exit_1(self):
        out_text = read_file(os.path.join(FORMAT, "x2.out")).replace(b'"0004|kiwi"', b'"0004|lime"', 1)
        box = SessionBox(self, DAEMON, "toy_store.py", read_file(os.path.join(FORMAT, "x2.in")), out_text, "x2")
        code, out, _ = box.cli("--no-build")
        self.assertEqual(code, 1)
        self.assertIn("FAIL x2", out)
        self.assertIn("entry 4 of", out)


class LifecycleTests(unittest.TestCase):
    def test_A_program_that_never_listens_is_not_ready_and_the_case_fails(self):
        script = scenario("connect c tcp", "signal TERM", "wait-exit within 1s", head="scenario 2\ndefault within 1s\nstart ready port within 1s\n")
        box = SessionBox(self, DAEMON, "toy_deaf.py", script, "transcript 2\nready\nexit 0\n")
        code, out, _ = box.cli("--no-build")
        self.assertEqual(code, 1)
        self.assertIn("not-ready", out)

    def test_A_program_that_ignores_SIGTERM_is_killed_after_the_grace_period_and_fails(self):
        script = scenario("signal TERM", "wait-exit within 1s")
        box = SessionBox(self, DAEMON, "toy_stubborn.py", script, "transcript 2\nready\nexit 0\n")
        code, out, _ = box.cli("--no-build")
        self.assertEqual(code, 1)
        self.assertIn("exit-timeout", out)

    def test_A_program_that_exits_before_the_end_trigger_is_an_early_exit(self):
        script = scenario("connect c tcp", "recv c bytes 1 within 3s", "signal TERM", "wait-exit within 1s")
        box = SessionBox(self, DAEMON, "toy_quitter.py", script, "transcript 2\nready\nrecv c \"x\"\nexit 0\n")
        code, out, _ = box.cli("--no-build")
        self.assertEqual(code, 1)
        self.assertIn("eof c", out)  # the connection closes with the exit: that is seen before the exit itself

    def test_A_process_left_running_after_the_exit_fails_the_case_even_when_the_transcript_matches(self):
        script = scenario("signal TERM", "wait-exit within 2s")
        box = SessionBox(self, DAEMON, "toy_leaver.py", script, "transcript 2\nready\nexit 0\n")
        code, out, err = box.cli("--no-build")
        self.assertEqual(code, 1, out + err)
        self.assertIn("leftover", out)

    def test_Stdin_stdout_env_and_file_fixtures_and_the_end_of_input_as_the_trigger(self):
        script = ('scenario 2\ndefault within 2s\nenv GREETING "hey"\nfile note "from a file"\nenv NOTE_PATH "${note}"\nstart ready stdout "go" within 3s\n'
                  'send stdin "one\\n"\nrecv stdout lines 2\nclose stdin\nrecv stdout until "bye\\n"\nwait-exit within 2s\n')
        out_text = 'transcript 2\nready\nrecv stdout "hey one\\n"\nrecv stdout "file: from a file\\n"\nrecv stdout "bye\\n"\nexit 0\n'
        box = SessionBox(self, DAEMON, "toy_stdio.py", script, out_text)
        code, out, err = box.cli("--no-build")
        self.assertEqual(code, 0, out + err)
        self.assertFalse(os.path.exists(os.path.join(box.ws, ".scenario")), "the plain path removes the fixtures it wrote")

    def test_A_quiet_connection_that_receives_bytes_is_noise(self):
        script = scenario("connect c tcp", "send c \"x\"", "expect c quiet for 300ms", "signal TERM", "wait-exit within 1s")
        box = SessionBox(self, DAEMON, "toy_echo.py", script, "transcript 2\nready\nquiet c\nexit 0\n")
        code, out, _ = box.cli("--no-build")
        self.assertEqual(code, 1)
        self.assertIn("noise", out)

    def test_A_chunked_http_reply_with_a_header_and_a_reconnect_after_a_close(self):
        script = scenario("connect a http", "request a GET \"/chunked\"", "recv a", "request a GET \"/close\"", "recv a", "request a GET \"/again\"", "recv a",
                          "signal TERM", "wait-exit within 1s")
        out_text = ('transcript 2\nready\nreply a 200\n  header "x-k" "v"\n  body "hello world"\nreply a 200\n  body "closing"\nreply a 200\n  body "again"\nexit 0\n')
        box = SessionBox(self, WEB, "toy_raw_http.py", script, out_text)
        code, out, err = box.cli("--no-build")
        self.assertEqual(code, 0, out + err)


if __name__ == "__main__":
    unittest.main()
