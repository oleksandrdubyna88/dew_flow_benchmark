"""The launch status of B5 (E1.S8a part ii-a): bwrap's JSON lines, the descriptor number, one pipe per case."""
import fcntl
import os
import unittest

import runner_path  # noqa: F401
import rs_status as st

BWRAP_LINES = b'{ "child-pid": 1713652, "mnt-namespace": 4026532357, "pid-namespace": 4026532359 }\n{ "exit-code": 3 }\n'


class ReaderTests(unittest.TestCase):
    def test_The_child_pid_and_the_exit_code_are_read_from_the_lines_bwrap_writes(self):
        reader = st.StatusReader()
        reader.feed(BWRAP_LINES)
        self.assertEqual((reader.child_pid, reader.exit_code), (1713652, 3))

    def test_A_line_that_is_not_whole_yet_waits_for_its_end(self):
        reader = st.StatusReader()
        reader.feed(b'{ "child-pid": 41')
        self.assertIsNone(reader.child_pid)
        reader.feed(b"2 }\n")
        self.assertEqual(reader.child_pid, 412)

    def test_Nothing_is_known_before_a_line_arrives(self):
        reader = st.StatusReader()
        self.assertEqual((reader.child_pid, reader.exit_code), (None, None))

    def test_Anything_that_is_not_a_status_object_is_dropped_and_the_next_line_still_counts(self):
        reader = st.StatusReader()
        reader.feed(b'garbage\n[1]\n{"child-pid": "9"}\n{"child-pid": 1.5}\n{"child-pid": true}\n{"child-pid": NaN}\n\xff\xfe\n{"child-pid": 5}\n')
        self.assertEqual(reader.child_pid, 5)

    def test_The_first_child_pid_and_the_first_exit_code_stand(self):
        reader = st.StatusReader()
        reader.feed(b'{"child-pid": 5}\n{"child-pid": 6}\n{"exit-code": 0}\n{"exit-code": 9}\n')
        self.assertEqual((reader.child_pid, reader.exit_code), (5, 0))

    def test_An_exit_code_of_zero_is_a_code_and_not_absent(self):
        reader = st.StatusReader()
        reader.feed(b'{"exit-code": 0}\n')
        self.assertEqual(reader.exit_code, 0)

    def test_The_buffer_is_bounded_whatever_a_launcher_writes(self):
        reader = st.StatusReader()
        reader.feed(b"x" * (10 * st.MAX_BUFFER))
        self.assertLessEqual(len(reader.buffer), st.MAX_BUFFER)
        reader.feed(b'\n{"child-pid": 8}\n')
        self.assertEqual(reader.child_pid, 8)


class DescriptorTests(unittest.TestCase):
    def free_number(self):
        probe = os.dup(0)
        os.close(probe)
        return probe

    def test_A_standard_stream_or_an_open_descriptor_cannot_carry_the_status(self):
        for number in (0, 1, 2):
            self.assertIn("standard stream", st.check_number(number))
        held = os.dup(0)
        self.addCleanup(os.close, held)
        self.assertIn("already open", st.check_number(held))
        self.assertIsNone(st.check_number(self.free_number()))

    def test_The_write_end_sits_at_the_number_and_the_read_end_does_not(self):
        number = self.free_number()  # the lowest free number: os.pipe() would hand it out first
        reader_fd = st.open_pipe(number)
        self.addCleanup(os.close, reader_fd)
        self.assertNotEqual(reader_fd, number)
        self.assertTrue(os.get_inheritable(number))
        self.assertFalse(os.get_inheritable(reader_fd))
        os.write(number, b"hello")
        self.assertEqual(os.read(reader_fd, 10), b"hello")
        st.release(number)
        with self.assertRaises(OSError):
            fcntl.fcntl(number, fcntl.F_GETFD)
        reader = st.StatusReader()
        self.assertFalse(st.pull(reader_fd, reader), "with the write end closed the pipe ends")

    def test_A_pipe_with_nothing_in_it_yet_is_still_open(self):
        number = self.free_number()
        reader_fd = st.open_pipe(number)
        self.addCleanup(st.release, number)
        self.addCleanup(os.close, reader_fd)
        self.assertTrue(st.pull(reader_fd, st.StatusReader()))


if __name__ == "__main__":
    unittest.main()
