"""rs_assign: one-to-one assignment by augmenting paths (Kuhn), for `unordered` groups of expected entries and JSON array elements.

`fits(row, col)` is a whole entry comparison, so it is evaluated lazily (only for the pairs the search visits) and at most once per pair
(a dict of the visited pairs, never a dense matrix: a JSON array can hold thousands of elements). A row tries the column of its own
index first, so elements that arrive in the order they were listed cost one evaluation each. The search is iterative: a long chain of
reroutes does not meet the recursion limit."""


def _columns(row, cols):
    """The columns to try for a row: its own index first, then the others in order."""
    if row < cols:
        yield row
    yield from (c for c in range(cols) if c != row)


def _next_column(row, columns, seen, fit):
    """The next column the row may take (not yet seen on this path and fitting), or None."""
    return next((c for c in columns if c not in seen and fit(row, c)), None)


def _commit(owner, row, col, picks):
    """The path ends at a free column: every row on it moves to the column it picked."""
    owner[col] = row
    for taker, taken in picks:
        owner[taken] = taker


def _augment(root, cols, fit, owner, seen):
    """Find an augmenting path from `root` and apply it; True when `root` ends up with a column."""
    stack, picks = [(root, _columns(root, cols))], []
    while stack:
        row, columns = stack[-1]
        col = _next_column(row, columns, seen, fit)
        if col is None:
            stack.pop()
            del picks[max(len(stack) - 1, 0):]
            continue
        seen.add(col)
        if owner[col] is None:
            _commit(owner, row, col, picks)
            return True
        picks.append((row, col))
        stack.append((owner[col], _columns(owner[col], cols)))
    return False


def assign(rows, cols, fits):
    """A largest set of (row, col) pairs with `fits(row, col)` true, no row or column twice: `owner[col]` is the row or None."""
    visited = {}

    def fit(row, col):
        if (row, col) not in visited:
            visited[(row, col)] = bool(fits(row, col))
        return visited[(row, col)]

    owner = [None] * cols
    for r in range(rows):
        _augment(r, cols, fit, owner, set())
    return owner


def unmatched_rows(rows, owner):
    """The rows no column was assigned to."""
    return [r for r in range(rows) if r not in owner]
