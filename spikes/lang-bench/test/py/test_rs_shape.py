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
