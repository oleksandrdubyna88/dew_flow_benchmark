"""F2.S2 (c) P: the time the runner's current parse takes for a chunked body of S bytes sent in 4 KiB chunks and read in
64 KiB pulls (the runner's CHUNK), the whole response parsed again after each pull as the runner does. Median of five.
Usage: python3 -I spikes/lang-bench/test/chunk_parse_p.py spikes/lang-bench/runner <S bytes>   (on the scoring CPUs: taskset -c 6,7)
F2.S2 (c) of the language comparison v2 stories document; the result goes into a dated line of inputs/checkpoints.md."""
import statistics
import sys
import time

USAGE = "usage: python3 -I chunk_parse_p.py <runner dir> <S bytes, a whole number above 0>"
if len(sys.argv) != 3 or not sys.argv[2].isdigit() or int(sys.argv[2]) < 1:
    raise SystemExit(USAGE)

sys.path.insert(0, sys.argv[1])
import rs_conn as rc  # noqa: E402  (the runner under test, by path)

S = int(sys.argv[2])
SEND = 4096


def response():
    parts = [b"HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n"]
    left = S
    while left > 0:
        n = min(SEND, left)
        parts.append(b"%x\r\n" % n + b"x" * n + b"\r\n")
        left -= n
    parts.append(b"0\r\n\r\n")
    return b"".join(parts)


def one_run(whole):
    start = time.perf_counter()
    got = None
    for end in range(rc.CHUNK, len(whole) + rc.CHUNK, rc.CHUNK):
        got = rc.parse_response(whole[: min(end, len(whole))], False)
        if got[0] != rc.CONTINUE:
            break
    took = time.perf_counter() - start
    if got is None or got[0] == rc.CONTINUE:
        raise SystemExit("the response never parsed whole")
    return took


whole = response()
runs = [one_run(whole) for _ in range(5)]
print(f"S {S} bytes, {len(whole)} response bytes, pulls {-(-len(whole) // rc.CHUNK)}, "
      f"P median {statistics.median(runs) * 1000:.1f} ms (runs {', '.join(f'{r * 1000:.1f}' for r in runs)} ms)")
