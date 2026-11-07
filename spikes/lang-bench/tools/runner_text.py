"""What the text of a runner file says, read by Python's own tokenizer and parser (standard library only; start it as `python3 -I -B runner_text.py <mode> <file>...`).

  items  per file: the comments (by `tokenize`), the docstrings (by `ast`) and every other string literal (by `ast`), each with its line.
  dump   per file: `ast.dump` of the tree with every docstring taken out and the value of `RUNNER_VERSION` masked, so two versions of a module
         that differ only in comments, docstrings and the version constant dump equal.

The result is one JSON object on standard output: `{ "<file>": ... }`. A file that does not parse is `{ "error": "<message>" }`.
"""
import ast
import io
import json
import sys
import tokenize

VERSION_NAME = "RUNNER_VERSION"
MASK = "<version>"
DOCSTRING_OWNERS = (ast.Module, ast.ClassDef, ast.FunctionDef, ast.AsyncFunctionDef)


def _docstring_node(owner):
    """The `Expr` statement that is the docstring of a module, class or function, or None."""
    body = getattr(owner, "body", None)
    if not body:
        return None
    first = body[0]
    holds_text = isinstance(first, ast.Expr) and isinstance(first.value, ast.Constant) and isinstance(first.value.value, str)
    return first if holds_text else None


def _docstrings(tree):
    owners = [n for n in ast.walk(tree) if isinstance(n, DOCSTRING_OWNERS)]
    return [d for d in (_docstring_node(o) for o in owners) if d is not None]


def _comments(text):
    tokens = tokenize.generate_tokens(io.StringIO(text).readline)
    return [{"line": t.start[0], "text": t.string} for t in tokens if t.type == tokenize.COMMENT]


def _strings(tree, docstrings):
    taken = {id(d.value) for d in docstrings}
    return [{"line": n.lineno, "value": n.value} for n in ast.walk(tree) if isinstance(n, ast.Constant) and isinstance(n.value, str) and id(n) not in taken]


def items_of(text, path):
    tree = ast.parse(text, filename=path)
    docs = _docstrings(tree)
    return {
        "comments": _comments(text),
        "docstrings": [{"line": d.value.lineno, "text": d.value.value} for d in docs],
        "strings": _strings(tree, docs),
    }


def _strip(tree):
    """The tree without its docstrings and with the version constant's value masked."""
    for owner in (n for n in ast.walk(tree) if isinstance(n, DOCSTRING_OWNERS)):
        if _docstring_node(owner) is not None:
            owner.body = owner.body[1:]
    for node in ast.walk(tree):
        if isinstance(node, ast.Assign) and any(isinstance(t, ast.Name) and t.id == VERSION_NAME for t in node.targets):
            node.value = ast.Constant(value=MASK)
    return tree


def dump_of(text, path):
    return ast.dump(_strip(ast.parse(text, filename=path)))


def main(argv):
    if len(argv) < 2 or argv[0] not in ("items", "dump"):
        sys.stderr.write("usage: runner_text.py items|dump <file>...\n")
        return 2
    mode, files = argv[0], argv[1:]
    out = {}
    for path in files:
        try:
            with open(path, encoding="utf-8") as handle:
                text = handle.read()
            out[path] = items_of(text, path) if mode == "items" else {"dump": dump_of(text, path)}
        except (OSError, SyntaxError, tokenize.TokenError, UnicodeDecodeError) as error:
            out[path] = {"error": f"{type(error).__name__}: {error}"}
    sys.stdout.write(json.dumps(out))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
