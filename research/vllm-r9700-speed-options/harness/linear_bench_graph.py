"""linear_bench_graph.py <out.json>: phase C again (§ 11.3), with a timer that excludes host launch overhead.

The first run timed eager calls with triton.testing.do_bench, so each value held Python and launch work (the sum
per forward was above a whole decode step). Here each call is captured 20 times into one CUDA graph, and the graph
replay is timed with CUDA events (median of 50 replays, divided by 20) — like vLLM's decode under CUDA graphs.
Everything else (shapes, M values, tile sweep, checks) is linear_bench.py.
"""
import sys

import torch

sys.path.insert(0, "/work/speed")
import linear_bench as lb  # noqa: E402

CALLS, REPLAYS = 20, 50


def bench_graph(fn):
    side = torch.cuda.Stream()
    side.wait_stream(torch.cuda.current_stream())
    with torch.cuda.stream(side):
        for _ in range(3):  # compile and warm up outside the capture
            fn()
    torch.cuda.current_stream().wait_stream(side)
    graph = torch.cuda.CUDAGraph()
    with torch.cuda.graph(graph):
        for _ in range(CALLS):
            fn()
    graph.replay()
    torch.cuda.synchronize()
    start, end = torch.cuda.Event(enable_timing=True), torch.cuda.Event(enable_timing=True)
    times = []
    for _ in range(REPLAYS):
        start.record()
        graph.replay()
        end.record()
        end.synchronize()
        times.append(start.elapsed_time(end) * 1000.0 / CALLS)  # us per call
    del graph
    return sorted(times)[len(times) // 2]


lb.bench = bench_graph
lb.main(sys.argv[1])
