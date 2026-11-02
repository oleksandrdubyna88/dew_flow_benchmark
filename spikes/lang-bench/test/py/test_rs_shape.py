"""Module and function shape of runner/, measured by an AST scan (E1.S8a acceptance): small functions, small files, a closed import set,
and no float anywhere a comparison happens. Each scan has a companion that finds a known instance, so a scan that matches nothing is noticed."""
import ast
import os
import unittest

import shape_scan as scan

# Modules whose arithmetic is wall time, not a comparison of sample content: the only ones allowed a float or a true division.
TIME_MODULES = {"rs_exec.py", "rs_launch.py", "rs_olympiad.py", "rs_pump.py", "rs_report.py", "run_samples.py"}


def sources():
    return [(os.path.basename(p), scan.parse(p), p) for p in scan.runner_files()]


STALE_WORDING = ("sandbox ready", "box is up", "sandbox is ready", "running sandbox", "sandbox reported", "reported its sandbox", "zero the peak")


class WordingTests(unittest.TestCase):
    """`child-pid` is written right after bwrap clones the sandbox, BEFORE its mounts: it is the start of the case clock and the proof that
    the launcher started, never that the box is ready or up. And `cgroup.kill` does not reset `memory.peak`."""

    def test_No_runner_text_says_child_pid_means_a_box_that_is_ready_or_up(self):
        found = []
        for name, _, path in sources():
            with open(path, encoding="utf-8") as f:
                text = f.read().lower()
            found += [f"{name}: {phrase}" for phrase in STALE_WORDING if phrase in text]
        self.assertEqual(found, [])

    def test_The_wording_scan_still_finds_a_known_phrase(self):
        self.assertIn("box is up", "only child-pid says the box is up".lower())
        self.assertTrue(any(phrase in "the line marks the sandbox ready" for phrase in STALE_WORDING))


TEST_DIR = os.path.dirname(os.path.abspath(__file__))


def test_files():
    return sorted(os.path.join(TEST_DIR, n) for n in os.listdir(TEST_DIR) if n.endswith(".py"))


class TestShapeTests(unittest.TestCase):
    """The tests themselves are held to the size rules (N12): no test file over 800 lines, the entry under 400, and every helper (a function
    that is not a test) short and simple."""

    def test_No_test_file_is_over_800_lines_and_the_entry_is_under_400(self):
        too_long = [(os.path.basename(p), scan.line_count(p)) for p in test_files() if scan.line_count(p) > 800]
        self.assertEqual(too_long, [])
        self.assertLess(scan.line_count(os.path.join(scan.RUNNER_DIR, "run_samples.py")), 400)

    def test_Every_test_helper_is_short_and_simple(self):
        bad = [
            f"{os.path.basename(p)}:{fn.name} {scan.function_length(fn)} lines, complexity {scan.complexity(fn)}"
            for p in test_files() if os.path.basename(p) != "shape_scan.py" for fn in scan.functions(scan.parse(p))  # the scanner itself is tested by its companions
            if not fn.name.startswith("test_") and (scan.function_length(fn) >= scan.MAX_FUNCTION_LINES or scan.complexity(fn) > scan.MAX_COMPLEXITY)
        ]
        self.assertEqual(bad, [])


class ShapeTests(unittest.TestCase):
    def test_Every_runner_import_is_standard_library_or_a_sibling(self):
        bad = {name: scan.foreign_imports(tree) for name, tree, _ in sources() if scan.foreign_imports(tree)}
        self.assertEqual(bad, {})

    def test_The_import_scan_still_finds_a_known_stdlib_import(self):
        lex = [t for name, t, _ in sources() if name == "rs_lex.py"][0]
        self.assertIn("re", scan.imported_modules(lex))
        self.assertEqual(scan.foreign_imports(ast.parse("import json\nimport requests\nfrom rs_lex import Problem")), ["requests"])

    def test_Every_runner_function_is_short_and_simple(self):
        bad = [
            f"{name}:{fn.name} {scan.function_length(fn)} lines, complexity {scan.complexity(fn)}"
            for name, tree, _ in sources() for fn in scan.functions(tree)
            if scan.function_length(fn) >= scan.MAX_FUNCTION_LINES or scan.complexity(fn) > scan.MAX_COMPLEXITY
        ]
        self.assertEqual(bad, [])

    def test_The_shape_scan_still_finds_a_known_complex_function(self):
        tree = ast.parse("def f(a, b):\n    if a and b or a:\n        for x in a:\n            while b:\n                pass\n")
        self.assertEqual(scan.complexity(scan.functions(tree)[0]), 6)

    def test_F9_no_runner_line_is_longer_than_160_characters(self):
        long_lines = []
        for name, _, path in sources():
            with open(path, encoding="utf-8") as f:
                long_lines += [f"{name}:{number} ({len(line.rstrip())})" for number, line in enumerate(f, 1) if len(line.rstrip()) > 160]
        self.assertEqual(long_lines, [])

    def test_Every_runner_file_is_within_the_size_ceiling(self):
        bad = [(name, scan.line_count(p)) for name, _, p in sources()]
        self.assertEqual([x for x in bad if x[1] > scan.MAX_FILE_LINES], [])

    def test_No_float_reaches_a_comparison(self):
        bad = {name: scan.float_uses(tree) for name, tree, _ in sources() if name not in TIME_MODULES and scan.float_uses(tree)}
        self.assertEqual(bad, {})

    def test_The_float_scan_still_finds_a_float_literal_a_float_call_and_a_true_division(self):
        found = scan.float_uses(ast.parse("a = 1.5\nb = float('2')\nc = 3 / 4\n"))
        self.assertEqual(sorted(what for _, what in found), ["1.5", "float", "true division"])

    def test_Every_json_parse_in_the_runner_keeps_numbers_exact(self):
        calls = [(name, line, kw) for name, tree, _ in sources() for line, kw in scan.json_loads_calls(tree)]
        self.assertTrue(calls, "the scan found no json.loads call to check")
        bad = [(n, line) for n, line, kw in calls if not {"parse_float", "parse_int", "parse_constant", "object_pairs_hook"} <= kw]
        self.assertEqual(bad, [])


if __name__ == "__main__":
    unittest.main()
