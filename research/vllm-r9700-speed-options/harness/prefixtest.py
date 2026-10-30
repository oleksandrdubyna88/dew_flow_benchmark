"""prefixtest.py <url> <model> <tag>: the § 6.1 hypothesis — is a full prefix-cache hit ~12 s slow on this model?

Random token-id prompts (new per run, so no hit from an earlier run), max_tokens=1, sent one at a time:
8192 tokens: A cold, A again (full hit), A again, B cold, B again, A with its last 64 tokens changed (partial hit);
then the same with 1024 tokens. Prints the wall time of each request (≈ TTFT with one output token).
"""
import json
import random
import sys
import time
import urllib.request

url, model, tag = sys.argv[1:4]
rng = random.Random(time.time_ns())


def ask(ids):
    body = json.dumps({"model": model, "prompt": ids, "max_tokens": 1, "temperature": 0}).encode()
    req = urllib.request.Request(url + "/v1/completions", body, {"Content-Type": "application/json"})
    t = time.perf_counter()
    urllib.request.urlopen(req, timeout=900).read()
    return time.perf_counter() - t


def prompt(n):
    return [rng.randrange(1000, 150000) for _ in range(n)]


for n in (8192, 1024):
    a, b = prompt(n), prompt(n)
    a_changed = a[:-64] + prompt(64)
    steps = [("A cold", a), ("A hit", a), ("A hit", a), ("B cold", b), ("B hit", b), ("A partial", a_changed)]
    times = [(name, round(ask(ids), 2)) for name, ids in steps]
    print(f"{tag} prefixtest len={n} " + " ".join(f"{name}={s}s" for name, s in times), flush=True)
