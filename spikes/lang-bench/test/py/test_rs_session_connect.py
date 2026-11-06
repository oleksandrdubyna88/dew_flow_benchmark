"""A connection the scenario opens (rs_session `connect`): only a program that is not listening YET is the program's answer (retry until the deadline, then `refused`);
any other error of the socket is the runner's own host failing and is a RunnerFault, never a verdict. No program is started: the socket is a fake."""
import errno
import unittest
from unittest import mock

import runner_path  # noqa: F401
import rs_exec as ex
import rs_session as ss

SCRIPT = b"scenario 2\ndefault within 1s\nstart ready port within 3s\nconnect c tcp\nsignal TERM\nwait-exit within 1s\n"
TRANSCRIPT = b"transcript 2\nready\nexit 0\n"
FAR_NS = 10 * 10 ** 9


def session():
    s = ss.Session(ss.Case("c1", SCRIPT, TRANSCRIPT, "c1.in", "c1.out"), config=None)
    s.wait_until = lambda *args, **kwargs: False  # no pump, no waiting: the loop under test is the one around open_socket
    return s


def opener(*outcomes):
    """A fake open_socket answering each call with the next outcome: an exception instance is raised, anything else is returned."""
    calls = iter(outcomes)

    def open_socket(rcvbuf):
        outcome = next(calls)
        if isinstance(outcome, BaseException):
            raise outcome
        return outcome
    return open_socket


class ConnectTests(unittest.TestCase):
    def test_A_refusal_before_the_deadline_is_retried_and_the_socket_that_opens_is_returned(self):
        s = session()
        s.open_socket = opener(ConnectionRefusedError(), TimeoutError(), "sock")
        self.assertEqual(s.connect_by(ex.now_ns() + FAR_NS, None, "c"), "sock")

    def test_Each_error_that_means_the_program_is_not_listening_yet_ends_at_the_deadline_in_refused_and_an_unanswered_connect_in_timeout(self):
        for error, word in ((ConnectionRefusedError(), "refused"), (TimeoutError(), "timeout"), (ConnectionResetError(), "refused"), (ConnectionAbortedError(), "refused")):
            s = session()
            s.open_socket = opener(error)
            with self.assertRaises(ss.Abort, msg=type(error).__name__):
                s.connect_by(ex.now_ns() - 1, None, "c")
            self.assertEqual(s.entries[-1].kind, word, type(error).__name__)

    def test_An_error_of_the_hosts_own_resources_is_a_runner_fault_and_no_verdict(self):
        for code in (errno.EMFILE, errno.ENFILE, errno.EADDRNOTAVAIL, errno.ENOBUFS, errno.ENOMEM, errno.EACCES, errno.ENETUNREACH):
            s = session()
            s.open_socket = opener(OSError(code, "host"))
            with self.assertRaises(ss.RunnerFault, msg=errno.errorcode[code]):
                s.connect_by(ex.now_ns() + FAR_NS, None, "c")
            self.assertEqual(s.entries, [], "a harness failure records no observation of the program")

    def test_A_host_error_after_some_refusals_is_still_a_runner_fault(self):
        s = session()
        s.open_socket = opener(ConnectionRefusedError(), OSError(errno.EMFILE, "too many open files"))
        with mock.patch.object(ss.Session, "wait_until", return_value=False):
            with self.assertRaises(ss.RunnerFault):
                s.connect_by(ex.now_ns() + FAR_NS, None, "c")


class ReadinessProbeTests(unittest.TestCase):
    def probe(self, error):
        s = session()
        s.port, s.next_probe = 1, 0
        with mock.patch.object(ss.socket, "create_connection", side_effect=error):
            return s.probe_port()

    def test_A_program_not_listening_yet_is_only_not_ready_for_this_probe(self):
        for error in (ConnectionRefusedError(), ConnectionResetError(), ConnectionAbortedError(), TimeoutError()):
            self.assertFalse(self.probe(error), type(error).__name__)

    def test_An_error_of_the_hosts_own_resources_in_the_probe_is_a_runner_fault_and_no_verdict(self):
        for code in (errno.EMFILE, errno.ENFILE, errno.EADDRNOTAVAIL, errno.ENOBUFS, errno.ENOMEM):
            with self.assertRaises(ss.RunnerFault, msg=errno.errorcode[code]):
                self.probe(OSError(code, "host"))


if __name__ == "__main__":
    unittest.main()
