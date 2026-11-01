"""rs_view: the case's view (B7). The execution layer makes, per case and outside the timed window, a fresh writable copy of the built tree
and names it with `--case-view TEMPLATE` (`{case}` filled in). The runner never assumes the program's filesystem is its own or that it
persists from one case to the next: it launches in the view of THIS case, writes a scenario's `.scenario/` fixtures into that view before
the launch, and never reads a result back from it. For a held-out run the samples directory must not lie inside a view (the view is what the
box binds), and a view must not lie inside the samples directory: both are refused before anything runs.
"""
import os

import rs_exec as ex

SCENARIO_DIR = ".scenario"


class ViewError(ex.EnvironmentProblem):
    """The view of a case is missing, or already holds a `.scenario` the runner did not make: the environment, not a verdict."""


def view_of(template, case_id):
    """The case's view directory from the template. A case id is checked (letters, digits, `_`, `-`) before it can reach a path."""
    if not ex.CASE_ID.match(case_id):
        raise ValueError(f"case id {case_id!r} is not letters, digits, underscore and hyphen")
    return template.replace("{case}", case_id)


def _inside(inner, outer):
    inner, outer = os.path.realpath(inner), os.path.realpath(outer)
    return inner == outer or inner.startswith(outer.rstrip(os.sep) + os.sep)


def _overlap_reasons(case_id, view, samples_dir):
    reasons = []
    if _inside(samples_dir, view):
        reasons.append(f"the samples directory is inside the view of case {case_id}")
    if _inside(view, samples_dir):
        reasons.append(f"the view of case {case_id} is inside the samples directory")
    return reasons


def overlaps_samples(template, case_ids, samples_dir):
    """The reasons the samples directory and the views of the given cases overlap, for the configuration check (exit 4)."""
    views = [(case_id, view_of(template, case_id)) for case_id in case_ids]
    return [reason for case_id, view in views for reason in _overlap_reasons(case_id, view, samples_dir)]


def require(view):
    """The view directory, which must exist: the layer makes it before the case starts."""
    if not os.path.isdir(view):
        raise ViewError(f"the case view {view} is not a directory: the layer must make it before the case starts")
    return view


def write_fixtures(view, fixtures):
    """Write `{path: bytes}` into `<view>/.scenario/`, every path confined to that folder. A `.scenario` that is already there is refused (it
    is not ours); with no fixtures nothing is created."""
    if not fixtures:
        return
    folder = os.path.join(view, SCENARIO_DIR)
    try:
        os.mkdir(folder)
    except FileExistsError as e:
        raise ViewError(f"{folder} already exists: a fresh view holds no {SCENARIO_DIR}") from e
    for name, data in fixtures.items():
        confined_write(folder, name, data)


def _refused(name, parts):
    return not parts or name.startswith("/") or ".." in parts or "\0" in name


def _components(name):
    """The path's components, normalised: a fixture path is relative, free of `..` and NUL, and never leaves the folder."""
    parts = [part for part in name.split("/") if part not in ("", ".")]
    if _refused(name, parts):
        raise ViewError(f"fixture path {name!r} is not a relative path inside the case view")
    return parts


def _descend(parent, part):
    """The folder `part` of the open folder `parent` (made when it is not there), opened without following a link; `parent` is closed."""
    try:
        try:
            os.mkdir(part, 0o700, dir_fd=parent)
        except FileExistsError:
            pass
        return os.open(part, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW, dir_fd=parent)
    except OSError as e:
        raise ViewError(f"fixture folder {part!r} is a link or not a folder: {type(e).__name__}") from e
    finally:
        os.close(parent)


def confined_write(base, name, data):
    """Create the file `name` (a relative path) under the folder `base`, one component at a time with O_NOFOLLOW, so that no link on the way
    leads out of `base` and a file that is already there is never written through."""
    parts = _components(name)
    fd = os.open(base, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW)
    for part in parts[:-1]:
        fd = _descend(fd, part)
    try:
        _write_new(fd, parts[-1], data)
    finally:
        os.close(fd)


def _write_new(parent, name, data):
    try:
        fd = os.open(name, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600, dir_fd=parent)
    except OSError as e:
        raise ViewError(f"fixture {name!r} could not be created: {type(e).__name__}") from e
    with os.fdopen(fd, "wb") as f:
        f.write(data)
