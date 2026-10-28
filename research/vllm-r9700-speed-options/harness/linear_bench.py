"""linear_bench.py: speed-options plan, phase C (option 7) — hybrid W4A16 linear kernel at small M.

For the model's quantized non-expert linear shapes (after vLLM merges q/k/v and gate/up), times:
  default  - the op as vLLM dispatches it (HIP skinny for M <= 5, Triton tiles of gfx12x above)
  split    - the HIP skinny op on chunks of <= 5 rows, outputs concatenated (a Python-only change)
  tuned    - the Triton kernel with the best tile found by a small sweep at this M
Checks split and tuned against default (max abs diff). Random int4 weights, bf16 activations, group 128.
"""
import itertools
import json
import sys

import torch
import triton

import vllm._custom_ops as ops
from vllm.model_executor.kernels.linear.mixed_precision import rdna_hybrid_w4a16 as rh
from vllm.utils.platform_utils import num_compute_units

GS = 128
# (name, K, N, layers using it): from the checkpoint's qweight shapes, merged as vLLM loads them.
SHAPES = [
    ("linear_attn.in_proj_qkvz", 2048, 12288, 30),
    ("linear_attn.out_proj / self_attn.o_proj", 4096, 2048, 40),
    ("self_attn.qkv_proj", 2048, 9216, 10),
    ("shared_expert.gate_up_proj", 2048, 1024, 40),
    ("shared_expert.down_proj", 512, 2048, 40),
]
MS = [1, 2, 3, 4, 5, 6, 8, 10, 12, 16]
SWEEP_MS = [6, 8, 12, 16]
TILES = list(itertools.product([16], [16, 32, 64, 128], [32, 64, 128], [1, 2, 4, 8], [None, 1, 2]))


def bench(fn):
    return triton.testing.do_bench(fn, warmup=25, rep=100) * 1000.0  # us


def split_skinny(x, w_q, w_s, cu):
    m = x.shape[0]
    n_chunks = -(-m // rh.MAX_SKINNY_BATCH_SIZE)
    parts = torch.tensor_split(x, n_chunks, dim=0)
    return torch.cat([ops.wvSplitK_int4_g(w_q, p.contiguous(), w_s, cu, GS, None, None) for p in parts], dim=0)


def triton_tile(x, w_q32, w_s, tile):
    bm, bn, bk, warps, stages = tile
    m, k = x.shape
    n = w_q32.shape[0]
    c = torch.empty((m, n), dtype=x.dtype, device=x.device)
    extra = {} if stages is None else {"num_stages": stages}
    rh._triton_w4a16_skinny_fmt_kernel[(triton.cdiv(m, bm), triton.cdiv(n, bn))](
        x, w_q32, w_s, w_s, c, m, n, k, k // 8, k // GS, group_size=GS, ZP_BIAS=8, HAS_ZP=False,
        BLOCK_M=bm, BLOCK_N=bn, BLOCK_K=min(bk, GS), num_warps=warps, **extra)
    return c


def main(out_path):
    torch.manual_seed(0)
    cu = num_compute_units()
    rows = []
    for name, k, n, layers in SHAPES:
        w_q = torch.randint(-128, 127, (n, k // 2), dtype=torch.int8, device="cuda")
        w_s = (torch.rand((n, k // GS), dtype=torch.bfloat16, device="cuda") * 0.02 + 0.001).contiguous()
        w_q32 = w_q.view(torch.int32)
        for m in MS:
            x = torch.randn((m, k), dtype=torch.bfloat16, device="cuda") * 0.1
            ref = torch.ops.vllm.rdna_hybrid_w4a16_apply(x, w_q, w_s, None, None, cu, GS)
            row = {"shape": name, "K": k, "N": n, "layers": layers, "M": m,
                   "default_us": bench(lambda: torch.ops.vllm.rdna_hybrid_w4a16_apply(x, w_q, w_s, None, None, cu, GS)),
                   "triton_default_us": bench(lambda: rh.triton_w4a16_skinny_fmt_gemm(x, w_q32, w_s, GS))}
            if k * min(m, rh.MAX_SKINNY_BATCH_SIZE) <= rh.LDS_CAPACITY_ELEMENTS:
                out = split_skinny(x, w_q, w_s, cu)
                row["split_us"] = bench(lambda: split_skinny(x, w_q, w_s, cu))
                row["split_maxdiff"] = (out.float() - ref.float()).abs().max().item()
            if m in SWEEP_MS:
                timings = []
                for tile in TILES:
                    try:
                        triton_tile(x, w_q32, w_s, tile)
                        timings.append((bench(lambda: triton_tile(x, w_q32, w_s, tile)), tile))
                    except Exception as e:  # a tile Triton cannot compile for this shape is skipped
                        print(f"skip tile {tile}: {type(e).__name__}", file=sys.stderr)
                t, tile = min(timings)
                row["tuned_us"], row["tuned_tile"] = t, list(tile)
                row["tuned_maxdiff"] = (triton_tile(x, w_q32, w_s, tile).float() - ref.float()).abs().max().item()
            rows.append(row)
            print(json.dumps(row), flush=True)
    with open(out_path, "w") as f:
        json.dump(rows, f, indent=1)


if __name__ == "__main__":
    main(sys.argv[1])
