"""One-to-one assignment (Kuhn): a largest matching, found without calling the fit function more than once per pair."""
import itertools
import random
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
    def test_Every_pair_is_evaluated_exactly_once_however_often_the_search_revisits_it(self):
        n = 12
        chain = [[c <= r for c in range(n)] for r in range(n)]
        owner, calls = assign_with_calls(chain)
        self.assertEqual(ra.unmatched_rows(n, owner), [])
        self.assertEqual(sorted(calls), sorted(itertools.product(range(n), range(n))), "the matrix is built once: n * n calls, each pair once")

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
            owner, _ = assign_with_calls(matrix)
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
