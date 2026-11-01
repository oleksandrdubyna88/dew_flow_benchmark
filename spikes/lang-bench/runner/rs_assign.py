"""rs_assign: one-to-one assignment by augmenting paths (Kuhn), for `unordered` groups of expected entries and JSON array elements.

At most 64 entries stand in a group (a larger one is a format error), so the recursion is shallow."""


def _candidate(r, c, fits, seen):
    return c not in seen and fits(r, c)


def _try_column(r, c, cols, fits, owner, seen):
    seen.add(c)
    if owner[c] is None or _augment(owner[c], cols, fits, owner, seen):
        owner[c] = r
        return True
    return False


def _augment(r, cols, fits, owner, seen):
    for c in range(cols):
        if _candidate(r, c, fits, seen) and _try_column(r, c, cols, fits, owner, seen):
            return True
    return False


def assign(rows, cols, fits):
    """A largest set of (row, col) pairs with `fits(row, col)` true, no row or column twice: `owner[col]` is the row or None."""
    owner = [None] * cols
    for r in range(rows):
        _augment(r, cols, fits, owner, set())
    return owner


def unmatched_rows(rows, owner):
    """The rows no column was assigned to."""
    return [r for r in range(rows) if r not in owner]
