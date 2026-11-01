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

    def test_Anything_that_is_not_a_status_object_is_a_problem_and_the_next_line_is_still_read(self):
        for junk in (b"garbage\n", b"[1]\n", b'{"child-pid": "9"}\n', b'{"child-pid": 1.5}\n', b'{"child-pid": true}\n', b'{"child-pid": NaN}\n', b"\xff\xfe\n"):
            reader = st.StatusReader()
            reader.feed(junk + b'{"child-pid": 5}\n')
            self.assertIsNotNone(reader.problem, junk)
            self.assertEqual(reader.child_pid, 5, junk)

    def test_A_well_formed_stream_has_no_problem_and_a_blank_line_is_none(self):
        reader = st.StatusReader()
        reader.feed(b'\n{ "child-pid": 7, "mnt-namespace": 1 }\n\n{ "exit-code": 0 }\n')
        self.assertEqual((reader.child_pid, reader.exit_code, reader.problem), (7, 0, None))

    def test_The_first_child_pid_and_the_first_exit_code_stand_and_a_second_one_is_a_problem(self):
        for lines in (b'{"child-pid": 5}\n{"child-pid": 6}\n', b'{"child-pid": 5}\n{"exit-code": 0}\n{"exit-code": 9}\n'):
            reader = st.StatusReader()
            reader.feed(lines)
            self.assertEqual((reader.child_pid, reader.problem is not None), (5, True), lines)
        self.assertEqual(reader.exit_code, 0)

    def test_A_record_that_is_neither_before_child_pid_is_a_problem_and_after_it_it_is_not(self):
        before, after = st.StatusReader(), st.StatusReader()
        before.feed(b'{"something": 1}\n')
        after.feed(b'{"child-pid": 5}\n{"something": 1}\n')
        self.assertEqual((before.problem is not None, after.problem), (True, None))

    def test_An_exit_code_of_zero_is_a_code_and_not_absent(self):
        reader = st.StatusReader()
        reader.feed(b'{"exit-code": 0}\n')
        self.assertEqual(reader.exit_code, 0)

    def test_The_incomplete_line_is_bounded_whatever_a_launcher_writes_and_a_line_too_long_before_child_pid_is_a_problem(self):
        reader = st.StatusReader()
        reader.feed(b"x" * (10 * st.MAX_LINE))
        self.assertLessEqual(len(reader.partial), st.MAX_LINE)
        self.assertIn("longer than", reader.problem)
        reader.feed(b'\n{"child-pid": 8}\n')
        self.assertEqual(reader.child_pid, 8)

    def test_Every_complete_line_of_a_big_read_is_consumed_and_none_is_cut_away_by_a_buffer_bound(self):
        reader = st.StatusReader()
        filler = b'{"x": 1}\n' * 20_000  # 180 kB, past any buffer bound
        reader.feed(b'{"child-pid": 8}\n' + filler + b'{"exit-code": 4}\n')
        self.assertEqual((reader.child_pid, reader.exit_code), (8, 4), "the first and the last line of one big read both count")

    def test_Junk_longer_than_the_bound_after_child_pid_is_dropped_without_a_problem_and_the_next_line_counts(self):
        reader = st.StatusReader()
        reader.feed(b'{"child-pid": 8}\n' + b"x" * (3 * st.MAX_LINE))
        self.assertIsNone(reader.problem)
        reader.feed(b'\n{"exit-code": 2}\n')
        self.assertEqual(reader.exit_code, 2)

    def test_The_work_per_read_does_not_grow_with_what_was_read_before(self):
        reader = st.StatusReader()
        reader.feed(b'{"child-pid": 8}\n')
        reader.feed(b"y" * (st.MAX_LINE - 100))
        before = len(reader.partial)
        for _ in range(50):
            reader.feed(b"z")
        self.assertEqual(len(reader.partial), before + 50, "the partial line is appended to, never re-split with everything before it")


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
