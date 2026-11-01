"""The E1 test-runner scaffold's Python smoke test: standard library only, started by test/scaffold.test.mjs under `python3 -I`."""
import sys
import unittest


class ScaffoldTests(unittest.TestCase):
    def test_The_scaffold_runs_python_isolated_from_the_environment_and_the_user_site(self):
        self.assertEqual(sys.flags.isolated, 1)
        self.assertEqual(sys.flags.ignore_environment, 1)
        self.assertEqual(sys.flags.no_user_site, 1)


if __name__ == "__main__":
    unittest.main()
