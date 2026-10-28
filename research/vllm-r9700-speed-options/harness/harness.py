#!/usr/bin/env python3
"""Determinism and accuracy harness for a running vLLM OpenAI-compatible server."""
import argparse
import json
import math
import re
import sys
import time
import urllib.error
import urllib.request
from concurrent.futures import ThreadPoolExecutor

PROMPTS = [
    "Q: What is the capital of France?\nA:",
    "Q: Who wrote the play Romeo and Juliet?\nA:",
    "Q: What is the chemical symbol for gold?\nA:",
    "Q: How many planets are in the Solar System?\nA:",
    "Q: What is the boiling point of water in Celsius at sea level?\nA:",
    "Compute 17 * 23. Answer:",
    "What is 1234 + 8765? Answer:",
    "What is 144 divided by 12? Answer:",
    "If a train travels 60 km per hour for 3 hours, how far does it go? Answer:",
    "def fibonacci(n):\n    \"\"\"Return the n-th Fibonacci number.\"\"\"\n",
    "def is_palindrome(s):\n    \"\"\"Return True if s reads the same backwards.\"\"\"\n",
    "# Python: read a file and print its lines in reverse order\nwith open('data.txt') as f:\n",
    "List five fruits that are red:\n1.",
    "List four programming languages and one use for each:\n-",
    "Name the days of the week, one per line:\n",
    "Translate to French: 'The weather is very nice today.'\nFrench:",
    "Translate to Spanish: 'Where is the train station?'\nSpanish:",
    "Translate to German: 'I would like a cup of coffee, please.'\nGerman:",
    "Once upon a time, in a small village near the sea, there lived an old fisherman who",
    "The spaceship landed silently on the red planet, and the first thing the crew saw was",
]
GSM_SUFFIX = "\n\nSolve step by step, then give the final answer on the last line as: #### <number>"
TOKEN_RE = re.compile(r"^token_id:(\d+)$")
NUM_RE = re.compile(r"-?\d[\d,]*(?:\.\d+)?")


# ---------- pure functions ----------
def parse_token_id(s):
    m = TOKEN_RE.match(s) if isinstance(s, str) else None
    if not m:
        raise ValueError(f"token is not in 'token_id:N' form: {s!r}")
    return int(m.group(1))


def parse_top(d):
    """{'token_id:N': lp} -> [[N, lp], ...] sorted by logprob desc (ties: lower id first)."""
    return sorted(([parse_token_id(k), float(v)] for k, v in d.items()), key=lambda p: (-p[1], p[0]))


def compare_item(a, b, gap):
    """Compare reference item a with candidate item b (dicts with token_ids, top, finish_reason)."""
    ia, ib = a["token_ids"], b["token_ids"]
    if ia == ib:
        return {"identical": True, "k": None, "kind": "identical", "near_tie": None}
    k = next((i for i in range(min(len(ia), len(ib))) if ia[i] != ib[i]), min(len(ia), len(ib)))
    kind = "token" if k < min(len(ia), len(ib)) else "length"
    top = a["top"][k][:2] if k < len(a["top"]) else []
    b_tok = ib[k] if k < len(ib) else None
    near = (kind == "token" and len(top) == 2 and b_tok in (top[0][0], top[1][0])
            and (top[0][1] - top[1][1]) < gap)
    return {"identical": False, "k": k, "kind": kind, "near_tie": near, "a_top": top,
            "b_token": b_tok, "a_len": len(ia), "b_len": len(ib),
            "a_finish": a["finish_reason"], "b_finish": b["finish_reason"]}


def norm_number(s):
    s = s.replace(",", "").replace("$", "").strip().rstrip(".")
    try:
        f = float(s)
    except ValueError:
        return s
    return str(int(f)) if f == int(f) else str(f)


def extract_number(text):
    """Number after the last '####' if present, else the last number in the text."""
    if "####" in text:
        found = NUM_RE.findall(text.rsplit("####", 1)[1].replace("$", ""))
        if found:
            return norm_number(found[0])
    found = NUM_RE.findall(text.replace("$", ""))
    return norm_number(found[-1]) if found else ""


# ---------- I/O ----------
def post_json(url, payload, timeout=600):
    req = urllib.request.Request(url, json.dumps(payload).encode(), {"Content-Type": "application/json"})
    try:
        with urllib.request.urlopen(req, timeout=timeout) as r:
            return json.loads(r.read())
    except urllib.error.HTTPError as e:
        raise RuntimeError(f"HTTP {e.code} from {url}: {e.read().decode(errors='replace')[:500]}") from e
    except (urllib.error.URLError, TimeoutError) as e:
        raise RuntimeError(f"request to {url} failed: {e}") from e


def load_json(path):
    with open(path) as f:
        return json.load(f)


def save_json(path, obj):
    with open(path, "w") as f:
        json.dump(obj, f, indent=1)


# ---------- collect ----------
def collect_one(args, prompt):
    resp = post_json(args.url.rstrip("/") + "/v1/completions", {
        "model": args.model, "prompt": prompt, "max_tokens": args.max_tokens,
        "temperature": 0, "logprobs": 2, "return_tokens_as_token_ids": True})
    choice = resp["choices"][0]
    lp = choice.get("logprobs")
    if not lp or not lp.get("tokens") or not lp.get("top_logprobs") or not lp.get("token_logprobs"):
        raise RuntimeError(f"response has no logprobs for prompt {prompt[:40]!r}")
    ids = [parse_token_id(t) for t in lp["tokens"]]
    top = [parse_top(d) for d in lp["top_logprobs"]]
    if not (len(ids) == len(top) == len(lp["token_logprobs"])):
        raise RuntimeError("logprobs arrays have different lengths")
    return {"prompt": prompt, "text": choice["text"], "finish_reason": choice["finish_reason"],
            "token_ids": ids, "top": top, "token_logprobs": [float(x) for x in lp["token_logprobs"]]}


def cmd_collect(args):
    started = time.strftime("%Y-%m-%dT%H:%M:%S%z")
    results = []
    for i, p in enumerate(PROMPTS):  # sequential on purpose: batch size 1
        results.append(collect_one(args, p))
        print(f"collected {i + 1}/{len(PROMPTS)}", file=sys.stderr)
    save_json(args.out, {"url": args.url, "model": args.model, "max_tokens": args.max_tokens,
                         "started": started, "finished": time.strftime("%Y-%m-%dT%H:%M:%S%z"),
                         "results": results})
    print(f"wrote {args.out}")
    return 0


# ---------- compare ----------
def fmt_top(top):
    return " ".join(f"{t}({lp:.3f})" for t, lp in top) if top else "-"


def cmd_compare(args):
    ra, rb = load_json(args.a)["results"], load_json(args.b)["results"]
    if len(ra) != len(rb):
        raise RuntimeError(f"result counts differ: {len(ra)} vs {len(rb)}")
    rows = [compare_item(a, b, args.gap) for a, b in zip(ra, rb)]
    print(f"{'#':>3} {'result':<22} {'A top1/top2 at k':<40} {'B tok':>8} near-tie")
    for i, r in enumerate(rows, 1):
        if r["identical"]:
            print(f"{i:>3} {'identical':<22}")
            continue
        what = f"diverge k={r['k']}" if r["kind"] == "token" else f"length mismatch at {r['k']}"
        print(f"{i:>3} {what:<22} {fmt_top(r['a_top']):<40} {str(r['b_token']):>8} "
              f"{'yes' if r['near_tie'] else 'no'}")
        if r["kind"] == "length":
            print(f"      finish A={r['a_finish']} (len {r['a_len']}) B={r['b_finish']} (len {r['b_len']})")
    n_id = sum(r["identical"] for r in rows)
    all_near = all(r["identical"] or r["near_tie"] for r in rows)
    print(f"{n_id} identical of {len(rows)}; divergences all near-tie: {'yes' if all_near else 'no'}")
    if args.out:
        save_json(args.out, {"a": args.a, "b": args.b, "gap": args.gap, "identical": n_id,
                             "total": len(rows), "all_near_tie": all_near, "rows": rows})
    return 0 if all_near else 1


# ---------- gsm8k ----------
def gsm_one(args, idx, row):
    resp = post_json(args.url.rstrip("/") + "/v1/chat/completions", {
        "model": args.model, "messages": [{"role": "user", "content": row["question"] + GSM_SUFFIX}],
        "temperature": 0, "max_tokens": args.max_tokens,
        "chat_template_kwargs": {"enable_thinking": False}})
    choice = resp["choices"][0]
    gold = norm_number(row["answer"].rsplit("####", 1)[1])
    pred = extract_number(choice["message"]["content"] or "")
    return {"idx": idx, "gold": gold, "pred": pred, "correct": pred == gold,
            "finish_reason": choice["finish_reason"]}


def cmd_gsm8k(args):
    with open(args.data) as f:
        rows = [json.loads(line) for line in f if line.strip()][:args.n]
    with ThreadPoolExecutor(max_workers=args.concurrency) as ex:
        items = list(ex.map(lambda ir: gsm_one(args, *ir), enumerate(rows)))
    n, correct = len(items), sum(i["correct"] for i in items)
    p = correct / n
    summary = {"n": n, "correct": correct, "accuracy": p, "stderr": math.sqrt(p * (1 - p) / n)}
    save_json(args.out, {"url": args.url, "model": args.model, "summary": summary, "items": items})
    print(f"gsm8k n={n} correct={correct} accuracy={p:.4f} stderr={summary['stderr']:.4f}")
    return 0


def cmd_gsm8k_compare(args):
    sa, sb = load_json(args.a)["summary"], load_json(args.b)["summary"]
    delta = sb["accuracy"] - sa["accuracy"]
    limit = 2 * max(sa["stderr"], sb["stderr"])
    ok = abs(delta) <= limit
    print(f"accuracy A={sa['accuracy']:.4f} B={sb['accuracy']:.4f} delta={delta:+.4f} "
          f"limit=2*max(stderr)={limit:.4f} pass={'yes' if ok else 'no'}")
    return 0 if ok else 1


# ---------- selftest ----------
def selftest():
    assert parse_token_id("token_id:42") == 42
    for bad in ("42", "token_id:x", "tok", None):
        try:
            parse_token_id(bad)
        except ValueError:
            continue
        raise AssertionError(f"no error for {bad!r}")
    assert parse_top({"token_id:5": -2.0, "token_id:7": -0.1}) == [[7, -0.1], [5, -2.0]]

    def item(ids, tops, fin="length"):
        return {"token_ids": ids, "top": tops, "finish_reason": fin}
    tops = [[[1, -0.1], [2, -3.0]], [[10, -0.50], [11, -0.60]], [[20, -0.1], [21, -2.0]]]
    a = item([1, 10, 20], tops)
    assert compare_item(a, item([1, 10, 20], tops), 0.5)["identical"]
    r = compare_item(a, item([1, 11, 99], tops), 0.5)
    assert r["k"] == 1 and r["near_tie"] is True and r["kind"] == "token"
    assert compare_item(a, item([1, 11, 99], tops), 0.05)["near_tie"] is False  # gap too large
    assert compare_item(a, item([1, 10, 21], tops), 0.5)["near_tie"] is False   # gap 1.9 too large
    assert compare_item(a, item([1, 77, 99], tops), 0.5)["near_tie"] is False   # not in A top-2
    r = compare_item(a, item([1, 10], tops[:2], "stop"), 0.5)
    assert r["kind"] == "length" and r["k"] == 2 and r["near_tie"] is False and r["b_finish"] == "stop"

    assert extract_number("blah\n#### 1,234") == "1234"
    assert extract_number("The answer is $5.00.") == "5"
    assert extract_number("#### 3.5") == "3.5"
    assert extract_number("12 apples, then #### 7 and 9") == "7"
    assert extract_number("no digits") == ""
    print("selftest OK")
    return 0


# ---------- CLI ----------
def build_parser():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--selftest", action="store_true", help="run offline unit checks and exit")
    sub = p.add_subparsers(dest="cmd")
    c = sub.add_parser("collect", help="run 20 fixed prompts sequentially, save tokens and logprobs")
    c.add_argument("--url", required=True)
    c.add_argument("--model", required=True)
    c.add_argument("--out", required=True)
    c.add_argument("--max-tokens", type=int, default=128)
    c.set_defaults(fn=cmd_collect)
    c = sub.add_parser("compare", help="compare run A (reference) with run B (candidate)")
    c.add_argument("a")
    c.add_argument("b")
    c.add_argument("--gap", type=float, default=0.5)
    c.add_argument("--out")
    c.set_defaults(fn=cmd_compare)
    c = sub.add_parser("gsm8k", help="run GSM8K accuracy test")
    c.add_argument("--url", required=True)
    c.add_argument("--model", required=True)
    c.add_argument("--data", required=True)
    c.add_argument("--n", type=int, default=200)
    c.add_argument("--out", required=True)
    c.add_argument("--concurrency", type=int, default=4)
    c.add_argument("--max-tokens", type=int, default=512)
    c.set_defaults(fn=cmd_gsm8k)
    c = sub.add_parser("gsm8k-compare", help="compare two gsm8k result files")
    c.add_argument("a")
    c.add_argument("b")
    c.set_defaults(fn=cmd_gsm8k_compare)
    return p


def main():
    p = build_parser()
    args = p.parse_args()
    if args.selftest:
        return selftest()
    if not args.cmd:
        p.error("a subcommand or --selftest is required")
    return args.fn(args)


if __name__ == "__main__":
    sys.exit(main())
