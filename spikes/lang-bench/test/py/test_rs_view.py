"""The case view of B7 (E1.S8a part ii-a): where a case starts, the `.scenario/` fixtures, and the samples that never lie in a view."""
import os
import tempfile
import unittest

import runner_path  # noqa: F401
import rs_view as vw


def folder(test):
    holder = tempfile.TemporaryDirectory()
    test.addCleanup(holder.cleanup)
    return os.path.realpath(holder.name)


class ViewPathTests(unittest.TestCase):
    def test_The_case_id_is_put_into_the_template_and_a_bad_id_never_reaches_a_path(self):
        self.assertEqual(vw.view_of("/v/{case}/tree", "s03"), "/v/s03/tree")
        for bad in ("../x", "a/b", "", "s 1"):
            with self.assertRaises(ValueError):
                vw.view_of("/v/{case}", bad)

    def test_A_view_that_is_not_there_is_an_error_the_layer_must_answer_for(self):
        with self.assertRaises(vw.ViewError):
            vw.require(os.path.join(folder(self), "missing"))
        self.assertEqual(vw.require(folder(self)).startswith("/"), True)


class OverlapTests(unittest.TestCase):
    def test_The_samples_directory_inside_a_view_or_a_view_inside_the_samples_is_named(self):
        root = folder(self)
        samples = os.path.join(root, "views", "s01", "samples")
        os.makedirs(samples)
        self.assertEqual(vw.overlaps_samples(os.path.join(root, "views", "{case}"), ["s01"], samples), ["the samples directory is inside the view of case s01"])
        held = os.path.join(root, "samples")
        os.mkdir(held)
        self.assertEqual(vw.overlaps_samples(os.path.join(held, "{case}"), ["s02"], held), ["the view of case s02 is inside the samples directory"])

    def test_Disjoint_folders_and_a_look_alike_prefix_do_not_overlap(self):
        root = folder(self)
        for name in ("samples", "samples-copy", "views"):
            os.mkdir(os.path.join(root, name))
        self.assertEqual(vw.overlaps_samples(os.path.join(root, "views", "{case}"), ["a", "b"], os.path.join(root, "samples")), [])
        self.assertEqual(vw.overlaps_samples(os.path.join(root, "samples-copy", "{case}"), ["a"], os.path.join(root, "samples")), [])

    def test_A_link_to_the_samples_inside_a_view_is_seen_through(self):
        root = folder(self)
        samples, views = os.path.join(root, "samples"), os.path.join(root, "views")
        os.mkdir(samples)
        os.makedirs(os.path.join(views, "s01"))
        os.symlink(samples, os.path.join(views, "s01", "link"))
        self.assertEqual(vw.overlaps_samples(os.path.join(views, "{case}", "link"), ["s01"], samples),
                         ["the samples directory is inside the view of case s01", "the view of case s01 is inside the samples directory"])


class FixtureTests(unittest.TestCase):
    def test_Fixtures_are_written_into_the_scenario_folder_of_the_view_and_nowhere_else(self):
        view, other = folder(self), folder(self)
        vw.write_fixtures(view, {"a.txt": b"one", "b.bin": b"\x00\xff"})
        with open(os.path.join(view, ".scenario", "b.bin"), "rb") as f:
            self.assertEqual(f.read(), b"\x00\xff")
        self.assertEqual(sorted(os.listdir(os.path.join(view, ".scenario"))), ["a.txt", "b.bin"])
        self.assertEqual(os.listdir(other), [])

    def test_With_no_fixtures_nothing_is_created(self):
        view = folder(self)
        vw.write_fixtures(view, {})
        vw.write_fixtures(view, None)
        self.assertEqual(os.listdir(view), [])

    def test_A_scenario_folder_that_is_already_there_is_not_ours_and_is_refused(self):
        view = folder(self)
        os.mkdir(os.path.join(view, ".scenario"))
        with self.assertRaises(vw.ViewError):
            vw.write_fixtures(view, {"a": b"x"})

    def test_A_scenario_link_planted_in_the_view_is_refused_and_not_followed(self):
        view, target = folder(self), folder(self)
        os.symlink(target, os.path.join(view, ".scenario"))
        with self.assertRaises(vw.ViewError):
            vw.write_fixtures(view, {"a": b"x"})
        self.assertEqual(os.listdir(target), [])

    def test_A_fixture_name_that_leaves_the_folder_is_refused(self):
        for name in ("../escape", "a/b", "", ".."):
            view = folder(self)
            with self.assertRaises(vw.ViewError):
                vw.write_fixtures(view, {name: b"x"})
            self.assertEqual(os.listdir(os.path.dirname(view)).count("escape"), 0)


if __name__ == "__main__":
    unittest.main()
