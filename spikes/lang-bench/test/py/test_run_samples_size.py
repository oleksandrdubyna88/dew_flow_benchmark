"""The size ceiling of the samples and the way they are read (E1.S8a part i): split from test_run_samples.py to keep it under 800 lines."""
import io
import os
import subprocess
import sys
import unittest

import runner_path  # noqa: F401
import rs_samples as sm
import run_samples as rs
from test_run_samples import ANSWER, Box, read_file

class SampleSizeTests(unittest.TestCase):
    """Every sample is held in memory for the whole run (the program under test must not be able to change what it is compared with): bounded."""

    def run_in_process(self, box, ceiling):
        saved, sm.MAX_SAMPLE_BYTES = sm.MAX_SAMPLE_BYTES, ceiling
        err, real = io.StringIO(), (sys.stdout, sys.stderr)
        sys.stdout, sys.stderr = io.StringIO(), err
        try:
            code = rs.run([box.samples, "--workspace", box.ws])
        finally:
            (sys.stdout, sys.stderr), sm.MAX_SAMPLE_BYTES = real, saved
        return code, err.getvalue()

    def test_Sizes_are_written_in_binary_units_with_one_decimal_and_without_a_float(self):
        for size, text in ((0, "0 B"), (1000, "1000 B"), (1024, "1.0 KiB"), (1536 * 1024, "1.5 MiB"), (1 << 20, "1.0 MiB"), (110 * (1 << 20), "110.0 MiB"),
                           (1 << 30, "1.0 GiB"), (3 * (1 << 30) + (1 << 29), "3.5 GiB")):
            self.assertEqual(sm.human(size), text, size)

    def test_The_too_large_message_gives_human_units_names_the_largest_files_and_suggests_only(self):
        big = 3 * 1024
        pairs = {"s01.in": b"", "s01.out": b"a" * big, "s02.in": b"x" * 2048, "s02.out": b"b" * 100, "s03.in": b"", "s03.out": b"c" * 10,
                 "s04.in": b"", "s04.out": b"d" * 5}
        box = Box(self, pairs=pairs)
        code, err = self.run_in_process(box, 1024)
        self.assertEqual(code, 4)
        self.assertIn("the ceiling is 1.0 KiB", err)
        self.assertNotIn("bytes of .in", err, "units, not a long number of bytes")
        self.assertRegex(err, r"largest: s01\.out 3\.0 KiB, s02\.in 2\.0 KiB, s02\.out 100 B")
        self.assertNotIn("s03.out", err.split("largest:")[1].split("\n")[0], "only the three largest")
        self.assertIn("--only", err)
        self.assertIn("--only s01", err)

    def test_The_message_with_a_default_ceiling_says_GiB(self):
        self.assertIn("1.0 GiB", sm.too_large_message([], 1 << 30))

    def test_The_ceiling_is_one_GiB(self):
        self.assertEqual(sm.MAX_SAMPLE_BYTES, 1 << 30)

    def test_Samples_over_the_ceiling_are_exit_4_and_nothing_is_run(self):
        box = Box(self, run_sh="touch started\n", pairs={"s01.in": b"x" * 600, "s01.out": b"y" * 600, "s02.in": b"", "s02.out": b"z" * 10})
        code, err = self.run_in_process(box, 1000)
        self.assertEqual(code, 4)
        self.assertIn("the samples are too large to hold", err)
        self.assertFalse(os.path.exists(os.path.join(box.ws, "started")))

    def test_Samples_exactly_at_the_ceiling_are_held_and_one_byte_more_is_not(self):
        box = Box(self, pairs={"s01.in": b"", "s01.out": b"a" * 10})
        self.assertEqual(self.run_in_process(box, 10)[0], 1)
        code, err = self.run_in_process(box, 9)
        self.assertEqual(code, 4)

    def test_Only_the_chosen_cases_count_toward_the_ceiling(self):
        box = Box(self, pairs={"s01.in": b"", "s01.out": b"a" * 10, "s02.in": b"", "s02.out": b"b" * 5000})
        saved, sm.MAX_SAMPLE_BYTES = sm.MAX_SAMPLE_BYTES, 100
        try:
            args = rs.build_parser().parse_args([box.samples, "--workspace", box.ws, "--only", "s01"])
            plan = rs.make_plan(args)
        finally:
            sm.MAX_SAMPLE_BYTES = saved
        self.assertIsInstance(plan, rs.Plan)

    def pair_of(self, box, name):
        return sm.Pair(name, os.path.join(box.samples, name + ".in"), os.path.join(box.samples, name + ".out"), name + ".in", name + ".out")

    def test_A_sample_that_cannot_be_opened_is_cannot_read_for_each_file_and_not_a_size_of_zero(self):
        gone = sm.Pair("s09", "/nonexistent/s09.in", "/nonexistent/s09.out", "s09.in", "s09.out")
        _, problems = sm.load_all([gone])
        self.assertEqual(len(problems), 2, "one for each of the two files")
        self.assertTrue(all("cannot read" in p.message for p in problems))
        self.assertIn("s09.in", problems[0].message)
        self.assertIn("s09.out", problems[1].message)

    def test_A_FIFO_swapped_in_after_the_listing_is_refused_by_what_was_opened_and_does_not_block(self):
        box = Box(self, pairs={"s01.in": b"", "s01.out": b"x"})
        os.remove(os.path.join(box.samples, "s01.out"))
        os.mkfifo(os.path.join(box.samples, "s01.out"))
        loaded, problems = sm.load_all([self.pair_of(box, "s01")])
        self.assertEqual([p.message for p in problems], ["s01.out is not a regular file"])

    def test_A_file_is_read_for_at_most_the_budget_left_plus_one_byte_whatever_its_size(self):
        box = Box(self, pairs={"s01.in": b"", "s01.out": b"x" * 3_000_000})
        real, total = os.read, []

        def counting(fd, n):
            data = real(fd, n)
            total.append(len(data))
            return data

        saved, sm.MAX_SAMPLE_BYTES, os.read = sm.MAX_SAMPLE_BYTES, 1000, counting
        try:
            loaded, problems = sm.load_all([self.pair_of(box, "s01")])
        finally:
            os.read, sm.MAX_SAMPLE_BYTES = real, saved
        self.assertEqual(len(problems), 1)
        self.assertIn("too large to hold", problems[0].message)
        self.assertEqual(sum(total), 1001, "the budget and one byte more: the size limit rests on what was read")

    def test_The_budget_is_shared_by_all_the_samples_so_many_small_ones_can_pass_it(self):
        pairs = {f"s{n:02d}.{ext}": b"x" * 100 for n in range(1, 6) for ext in ("in", "out")}
        box = Box(self, pairs=pairs)
        code, err = self.run_in_process(box, 999)
        self.assertEqual(code, 4)
        self.assertIn("too large", err)
        self.assertNotEqual(self.run_in_process(box, 1000)[0], 4)

    def test_A_sample_that_vanishes_after_the_listing_is_exit_4_cannot_read(self):
        box = Box(self)
        saved = sm.discover

        def vanishing(samples_dir):
            pairs, problems = saved(samples_dir)
            os.remove(pairs[0].out_path)
            return pairs, problems

        sm.discover = vanishing
        try:
            code, err = self.run_in_process(box, 1 << 30)
        finally:
            sm.discover = saved
        self.assertEqual(code, 4)
        self.assertIn("cannot read", err)

    def test_A_pair_that_cannot_be_read_is_a_configuration_problem_and_not_a_crash(self):
        box = Box(self)
        os.chmod(os.path.join(box.samples, "s03.out"), 0)
        try:
            code, err = self.run_in_process(box, 1 << 30)
        finally:
            os.chmod(os.path.join(box.samples, "s03.out"), 0o644)
        self.assertEqual(code, 4, err)
        self.assertIn("cannot read", err)


if __name__ == "__main__":
    unittest.main()
