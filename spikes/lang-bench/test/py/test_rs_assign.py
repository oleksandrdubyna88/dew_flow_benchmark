"""One-to-one assignment (Kuhn): a largest matching, found without calling the fit function more than once per pair."""
import itertools
import random
import time
import tracemalloc
import unittest

import runner_path  # noqa: F401
import rs_assign as ra


def assign_with_calls(matrix):
    calls = []

    def fits(r, c):
        calls.append((r, c))
        return matrix[r][c]

    return ra.assign(len(matrix), len(matrix[0]), fits), calls


class AssignTests(unittest.TestCase):
    def test_No_pair_is_evaluated_more_than_once_however_often_the_search_revisits_it(self):
        n = 12
        chain = [[c <= r for c in range(n)] for r in range(n)]
        owner, calls = assign_with_calls(chain)
        self.assertEqual(ra.unmatched_rows(n, owner), [])
        self.assertEqual(len(calls), len(set(calls)), "each pair at most once: the pairs the search visits are cached")
        self.assertLess(len(calls), n * n, "and only the pairs it visits are evaluated: no dense matrix")

    def test_Two_thousand_in_order_elements_are_assigned_quickly_without_a_dense_matrix(self):
        n, calls = 2000, []
        tracemalloc.start()
        started = time.monotonic()
        owner = ra.assign(n, n, lambda r, c: calls.append(1) or r == c)
        elapsed = time.monotonic() - started
        _, peak = tracemalloc.get_traced_memory()
        tracemalloc.stop()
        self.assertEqual(ra.unmatched_rows(n, owner), [])
        self.assertLess(len(calls), 4 * n, f"{len(calls)} evaluations for {n} in-order rows: the search tries the element in the same place first")
        self.assertLess(peak, 5_000_000, f"{peak} bytes allocated: a 2000 x 2000 matrix would be several times that")
        self.assertLess(elapsed, 2.0)

    def test_A_search_that_has_to_reroute_still_evaluates_each_pair_once(self):
        matrix = [[True, True, False], [True, False, False], [False, True, True]]
        owner, calls = assign_with_calls(matrix)
        self.assertEqual(len(calls), len(set(calls)))
        self.assertEqual(ra.unmatched_rows(3, owner), [])

    def test_The_assignment_is_a_largest_matching_against_brute_force_over_random_matrices(self):
        rng = random.Random(7)
        for _ in range(3000):
            n, p = rng.randint(1, 6), rng.random()
            matrix = [[rng.random() < p for _ in range(n)] for _ in range(n)]
            owner, calls = assign_with_calls(matrix)
            self.assertEqual(len(calls), len(set(calls)), matrix)
            matched = [o for o in owner if o is not None]
            valid = all(o is None or matrix[o][c] for c, o in enumerate(owner)) and len(matched) == len(set(matched))
            best = max(sum(matrix[r][perm[r]] for r in range(n)) for perm in itertools.permutations(range(n)))
            self.assertTrue(valid)
            self.assertEqual(len(matched), best, matrix)

    def test_A_group_of_64_chained_entries_is_assigned_without_a_recursion_error(self):
        owner, _ = assign_with_calls([[c <= r for c in range(64)] for r in range(64)])
        self.assertEqual(ra.unmatched_rows(64, owner), [])

    def test_Rows_nothing_fits_are_the_unmatched_ones(self):
        owner, _ = assign_with_calls([[True, False], [True, False]])
        self.assertEqual(len(ra.unmatched_rows(2, owner)), 1)


if __name__ == "__main__":
    unittest.main()
