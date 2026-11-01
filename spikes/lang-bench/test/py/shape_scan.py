"""The AST scans over runner/ that the shape tests (test_rs_shape.py) and their companions use: standard library only."""
import ast
import os
import sys

RUNNER_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "runner")
MAX_FUNCTION_LINES = 50
MAX_COMPLEXITY = 4
MAX_FILE_LINES = 800


def runner_files():
    return sorted(os.path.join(RUNNER_DIR, n) for n in os.listdir(RUNNER_DIR) if n.endswith(".py"))


def parse(path):
    with open(path, encoding="utf-8") as f:
        return ast.parse(f.read(), filename=path)


def line_count(path):
    with open(path, encoding="utf-8") as f:
        return len(f.read().splitlines())


def sibling_names():
    return {os.path.splitext(n)[0] for n in os.listdir(RUNNER_DIR) if n.endswith(".py")}


def imported_modules(tree):
    """The top-level module name of every import in a tree."""
    names = []
    for node in ast.walk(tree):
        if isinstance(node, ast.Import):
            names += [a.name.split(".")[0] for a in node.names]
        elif isinstance(node, ast.ImportFrom) and node.level == 0:
            names.append((node.module or "").split(".")[0])
    return names


def foreign_imports(tree):
    allowed = set(sys.stdlib_module_names) | sibling_names()
    return sorted({n for n in imported_modules(tree) if n not in allowed})


def complexity(fn):
    """McCabe-style: 1 + branches, boolean operands, comprehension clauses and handlers; nested functions count on their own."""
    return 1 + sum(_weight(n) for n in _own_nodes(fn))


def _own_nodes(fn):
    stack = list(ast.iter_child_nodes(fn))
    while stack:
        node = stack.pop()
        if isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef, ast.Lambda, ast.ClassDef)):
            continue
        yield node
        stack.extend(ast.iter_child_nodes(node))


def _weight(node):
    if isinstance(node, (ast.If, ast.IfExp, ast.For, ast.AsyncFor, ast.While, ast.ExceptHandler, ast.match_case)):
        return 1
    if isinstance(node, ast.BoolOp):
        return len(node.values) - 1
    if isinstance(node, ast.comprehension):
        return 1 + len(node.ifs)
    return 0


def functions(tree):
    return [n for n in ast.walk(tree) if isinstance(n, (ast.FunctionDef, ast.AsyncFunctionDef))]


def function_length(fn):
    return fn.end_lineno - fn.lineno + 1


def float_uses(tree):
    """Every `float` name and every float literal in a tree, as (line, what)."""
    found = []
    for node in ast.walk(tree):
        if isinstance(node, ast.Name) and node.id == "float":
            found.append((node.lineno, "float"))
        elif isinstance(node, ast.Constant) and isinstance(node.value, float):
            found.append((node.lineno, repr(node.value)))
        elif isinstance(node, ast.BinOp) and isinstance(node.op, ast.Div):
            found.append((node.lineno, "true division"))
    return found


def json_loads_calls(tree):
    """Each `json.loads(...)` call as (line, set of keyword names)."""
    return [
        (n.lineno, {k.arg for k in n.keywords})
        for n in ast.walk(tree)
        if isinstance(n, ast.Call) and isinstance(n.func, ast.Attribute) and n.func.attr in ("loads", "load")
        and isinstance(n.func.value, ast.Name) and n.func.value.id == "json"
    ]
