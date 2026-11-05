"""GPU check: the ROCm custom paged-attention kernel (ops.paged_attention_rocm) under fused multi-step draft decode.

One arm per process (each its own run), on the vLLM 0.29 image, in-process (VLLM_ENABLE_V1_MULTIPROCESSING=0):

  python3 custom_kernel_check.py --arm A                  no speculation
  python3 custom_kernel_check.py --arm B --expect-fused 0 speculation, unchanged 0.29 code (no fused decode)
  python3 custom_kernel_check.py --arm C --expect-fused 1 speculation, opt-in mounted (fused decode)
  python3 custom_kernel_check.py --compare                 token identity across the three arms

Why method "eagle" and not "draft_model": at 0.29 the Model Runner V2 speculator factory
(vllm/v1/worker/gpu/spec_decode/__init__.py) has no draft_model branch, and
VllmConfig._get_v2_model_runner_unsupported_features() sends method draft_model to the V1 runner, which has no fused
multi-step decode. The fused path exists only in AutoRegressiveSpeculator (eagle / eagle3 / mtp). The draft here is a
tiny random EAGLE-1 head (Llama layer, same attention shape as the target), see gen_models.py.

Every assertion is collected; any failure prints FAIL lines and exits non-zero. Nothing is weakened or retried.
"""
import argparse
import json
import logging
import math
import os
import signal
import sys
import threading
import time
import traceback

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "out")
TARGET = os.path.join(HERE, "models", "target")
DRAFT = os.path.join(HERE, "models", "draft")
MAX_MODEL_LEN = 2048
NUM_SPEC = 3
NEW_TOKENS = 64
PARTITION = 256  # _PARTITION_SIZE_ROCM in chunked_prefill_paged_decode.py
PHASE_TIMEOUT_S = 900
# (name, prompt lengths). A phase is one llm.generate() call, so c = len(lengths) concurrent requests.
PHASES = [
    ("c1_len240", [240]),
    ("c1_len500", [500]),
    ("c4_240_500_100_255", [240, 500, 100, 255]),
    ("mixed6_17_129_240_256_500_777", [17, 129, 240, 256, 500, 777]),
]


def prompt_ids(phase_idx, i, n):
    import numpy as np

    return np.random.RandomState(20261105 + 1000 * phase_idx + i).randint(100, 50000, size=n).tolist()


# ----------------------------------------------------------------------------------------------- compare
def compare_legacy(v2=False, sabotage=None):
    arms = {}
    for a in "ABC":
        p = os.path.join(OUT, f"arm_{a}.json")
        if not os.path.exists(p):
            print(f"FAIL: {p} missing")
            return 1
        arms[a] = json.load(open(p))
    bad = 0
    for a in "ABC":
        lp = os.path.join(OUT, f"log_{a}.txt")
        txt = open(lp, errors="replace").read() if os.path.exists(lp) else ""
        n = txt.count("Cannot use ROCm custom paged attention kernel")
        print(f"arm {a}: raw container log {lp}: fallback-warning lines = {n}, log bytes = {len(txt)}")
        if n or not txt:
            bad += 1
            print(f"FAIL: arm {a} raw log has the fallback warning or is empty")
    for a in "ABC":
        print(f"arm {a}: status={arms[a]['status']} requests={sum(len(v) for v in arms[a]['tokens'].values())}")
        if arms[a]["status"] != "PASS":
            bad += 1
            print(f"FAIL: arm {a} did not pass on its own: {arms[a]['failures']}")
    for other in "BC":
        same = diff = total = 0
        for ph, reqs in arms["A"]["tokens"].items():
            for i, ref in enumerate(reqs):
                total += 1
                got = arms[other]["tokens"].get(ph, [None] * len(reqs))[i]
                if got == ref:
                    same += 1
                else:
                    diff += 1
                    k = next((j for j, (x, y) in enumerate(zip(ref, got or [])) if x != y), None)
                    print(f"  DIFF arm {other} vs A: phase {ph} request {i}: first divergence at token {k}")
        print(f"arm {other} vs A: {same}/{total} requests token-identical, {diff} differ")
        bad += diff
    for ea in ("B_eager", "C_eager"):
        pe = os.path.join(OUT, f"arm_{ea}.json")
        if not os.path.exists(pe):
            continue
        er = json.load(open(pe))
        print(f"arm {ea}: status={er['status']} requests={sum(len(v) for v in er['tokens'].values())}")
        if er["status"] != "PASS":
            bad += 1
            print(f"FAIL: arm {ea} did not pass: {er['failures']}")
        same = sum(1 for ph, reqs in arms["A"]["tokens"].items() for i, ref in enumerate(reqs) if er["tokens"].get(ph, [None] * len(reqs))[i] == ref)
        tot = sum(len(v) for v in arms["A"]["tokens"].values())
        print(f"arm {ea} vs A: {same}/{tot} requests token-identical")
        bad += tot - same
    same = diff = 0
    for ph, reqs in arms["B"]["tokens"].items():
        for i, ref in enumerate(reqs):
            if arms["C"]["tokens"].get(ph, [None] * len(reqs))[i] == ref:
                same += 1
            else:
                diff += 1
    print(f"arm C vs B: {same}/{same + diff} requests token-identical, {diff} differ")
    bad += diff
    print("COMPARE:", "PASS" if bad == 0 else "FAIL")
    rc = 0 if bad == 0 else 1
    if v2:
        print("--- acceptance and distinct generated tokens per arm (v2 models)")
        for nm in ("A", "B", "C", "B_eager", "C_eager"):
            pe = os.path.join(OUT, f"arm_{nm}.json")
            if not os.path.exists(pe):
                continue
            r = json.load(open(pe))
            m = r.get("spec_metrics") or {}
            acc, drf = m.get("vllm:spec_decode_num_accepted_tokens"), m.get("vllm:spec_decode_num_draft_tokens")
            d = r.get("distinct_tokens_per_request", [])
            rate = "n/a (no speculation)" if not drf else f"{acc}/{drf} = {acc / drf:.4f}"
            print(f"arm {nm}: accepted/drafted {rate}; per-position {m.get('vllm:spec_decode_num_accepted_tokens_per_pos')}; "
                  f"distinct tokens per request min/mean/max = {min(d)}/{sum(d) / len(d):.1f}/{max(d)} over {len(d)} requests")
    if sabotage:
        print(f"--- NEGATIVE CONTROL: sabotage={sabotage}")
        ps = os.path.join(OUT, f"arm_C_eager_sabotage_{sabotage}.json")
        if not os.path.exists(ps):
            print(f"FAIL: {ps} missing")
            return 1
        sr = json.load(open(ps))
        print(f"sabotaged arm: status={sr['status']} failures={len(sr['failures'])} n_violations={sr['n_violations']} "
              f"kernel_calls={sr['kernel_calls']} kernel_calls_with_violation={sr.get('kernel_calls_with_violation')}")
        if sabotage in ("seqlens", "seqlens_half"):
            ce = json.load(open(os.path.join(OUT, "arm_C_eager.json")))
            res_cmp = {}
            for refname, ref in (("A", arms["A"]), ("C_eager (same arm, unsabotaged)", ce)):
                tot = same = 0
                for ph, reqs in ref["tokens"].items():
                    for i, r0 in enumerate(reqs):
                        tot += 1
                        got = sr["tokens"].get(ph, [None] * len(reqs))[i] if sr["tokens"] else None
                        same += got == r0
                res_cmp[refname] = (same, tot)
                print(f"sabotaged C_eager vs {refname}: {same}/{tot} requests token-identical, {tot - same} differ")
            sm = sr.get("spec_metrics") or {}
            print(f"sabotaged arm accepted/drafted: {sm.get('vllm:spec_decode_num_accepted_tokens')}/{sm.get('vllm:spec_decode_num_draft_tokens')}")
            same, tot = res_cmp["C_eager (same arm, unsabotaged)"]
            if same == tot:
                print("NEGATIVE CONTROL: FAILED TO DETECT. The sabotage did not change any token relative to the unsabotaged arm, "
                      "so the token comparison is too weak for this model.")
                return 1
            print(f"COMPARE (sabotaged arm included): FAIL on {tot - same}/{tot} requests vs the unsabotaged arm -> the comparison can fail. NEGATIVE CONTROL: DETECTED")
        else:
            fired = sr["n_violations"] > 0 and (sr.get("kernel_calls_with_violation") == 0)
            print("assertion (c) fired in the wrapper before any kernel call with the undersized max_seq_len:", fired)
            if not fired:
                print("NEGATIVE CONTROL: maxseq sabotage did not trip (c) before the kernel call")
                return 1
    return rc



def _load(name):
    p = os.path.join(OUT, f"arm_{name}.json")
    return json.load(open(p)) if os.path.exists(p) else None


def _tok_ident(x, y):
    same = tot = 0
    for ph, reqs in x["tokens"].items():
        for i, r0 in enumerate(reqs):
            tot += 1
            same += (y["tokens"].get(ph, [None] * len(reqs))[i] == r0) if y.get("tokens") else 0
    return same, tot


def _trace_ident(x, y):
    tx, ty = x.get("draft_trace") or [], y.get("draft_trace") or []
    if not tx or not ty:
        return None
    same = sum(1 for a, b in zip(tx, ty) if a.get("drafts") == b.get("drafts") and a.get("phase") == b.get("phase"))
    return same, max(len(tx), len(ty)), len(tx), len(ty)


def compare2():
    """v2 comparison. Final tokens of a speculative arm are fixed by the target's verification, and in a speculative
    step the target's multi-token verify goes through the Triton prefill path, not ops.paged_attention_rocm. The custom
    kernel therefore only feeds the DRAFT; the sensitive observable for the fused-draft path is the draft tokens
    (draft_trace) and the acceptance counts. Final tokens are the safety property."""
    bad = 0
    names = ["A", "B", "C", "A_eager", "B_eager", "C_eager"]
    arms = {n: _load(n) for n in names}
    for n in names:
        if arms[n] is None:
            print(f"FAIL: arm_{n}.json missing")
            return 1
    for n in names:
        a = arms[n]
        lp = os.path.join(OUT, f"log_{ {'A_eager': 'Ae', 'B_eager': 'Be', 'C_eager': 'Ce'}.get(n, n) }.txt")
        txt = open(lp, errors="replace").read() if os.path.exists(lp) else ""
        nwarn = txt.count("Cannot use ROCm custom paged attention kernel")
        print(f"arm {n}: status={a['status']} raw-log fallback-warning lines={nwarn} (log {len(txt)} bytes) "
              f"kernel_calls={a['kernel_calls']}")
        if a["status"] != "PASS" or nwarn or not txt:
            bad += 1
            print(f"FAIL: arm {n}: {a['failures'] if a['status'] != 'PASS' else 'raw log empty or has the fallback warning'}")
    print("--- final tokens (64 new tokens x 12 requests)")
    for x, y in (("B", "A"), ("C", "A"), ("C", "B"), ("B_eager", "A_eager"), ("C_eager", "A_eager"), ("C_eager", "B_eager"),
                 ("A_eager", "A"), ("B_eager", "A"), ("C_eager", "A")):
        same, tot = _tok_ident(arms[x], arms[y])
        hard = (x, y) in (("B", "A"), ("C", "A"), ("C", "B"), ("B_eager", "A_eager"), ("C_eager", "A_eager"), ("C_eager", "B_eager"))
        print(f"{x} vs {y}: {same}/{tot} requests token-identical" + ("" if hard else "   (informational: graph vs eager numerics)"))
        if hard:
            bad += tot - same
    print("--- draft tokens recorded at every speculator.propose() (the kernel feeds these)")
    for x, y in (("C", "B"), ("C_eager", "B_eager")):
        t = _trace_ident(arms[x], arms[y])
        if t is None:
            print(f"FAIL: no draft trace for {x}/{y}")
            bad += 1
            continue
        print(f"{x} vs {y}: {t[0]}/{t[1]} propose() calls with identical draft tokens (entries {t[2]} vs {t[3]})")
        bad += t[1] - t[0]
    t = _trace_ident(arms["B"], arms["B_eager"])
    print(f"informational B(graph) vs B_eager: {t[0]}/{t[1]} propose() calls with identical draft tokens (graph vs eager numerics)")
    print("--- acceptance, distinct tokens")
    for n in names:
        r = arms[n]
        m = r.get("spec_metrics") or {}
        acc, drf = m.get("vllm:spec_decode_num_accepted_tokens"), m.get("vllm:spec_decode_num_draft_tokens")
        d = r["distinct_tokens_per_request"]
        rate = "n/a" if not drf else f"{acc}/{drf} = {acc / drf:.4f}"
        print(f"arm {n}: accepted/drafted {rate}; per-position {m.get('vllm:spec_decode_num_accepted_tokens_per_pos')}; "
              f"distinct tokens per request min/mean/max = {min(d)}/{sum(d) / len(d):.1f}/{max(d)}")
    print("COMPARE:", "PASS" if bad == 0 else "FAIL", f"({bad} mismatches)")
    # ---- negative controls
    ctl_bad = 0
    print("--- NEGATIVE CONTROLS")
    for mode in ("seqlens", "seqlens_half"):
        for fam in ("C", "A"):
            sr = _load(f"{fam}_eager_sabotage_{mode}")
            if sr is None:
                continue
            base = arms[f"{fam}_eager"]
            same, tot = _tok_ident(sr, base)
            line = f"sabotage={mode} on arm {fam}_eager: final tokens vs unsabotaged {fam}_eager: {same}/{tot} identical"
            sm, bm = sr.get("spec_metrics") or {}, base.get("spec_metrics") or {}
            if fam == "C":
                t = _trace_ident(sr, base)
                line += f"; draft-token propose() calls identical: {t[0]}/{t[1]}"
                line += (f"; accepted/drafted {sm.get('vllm:spec_decode_num_accepted_tokens')}/{sm.get('vllm:spec_decode_num_draft_tokens')} "
                         f"(unsabotaged {bm.get('vllm:spec_decode_num_accepted_tokens')}/{bm.get('vllm:spec_decode_num_draft_tokens')})")
            print(line)
            tok_det = same < tot
            drf_det = fam == "C" and t[0] < t[1]
            print(f"  -> final-token comparison detects it: {tok_det}; draft-token comparison detects it: {drf_det if fam == 'C' else 'n/a'}")
            if not (tok_det or drf_det):
                ctl_bad += 1
                print("  -> NOT DETECTED: the comparison is too weak for this sabotage")
    sr = _load("C_eager_sabotage_maxseq")
    if sr is not None:
        fired = sr["n_violations"] > 0 and sr.get("kernel_calls_with_violation") == 0
        print(f"sabotage=maxseq on C_eager: assertion (c) fired in the wrapper before the kernel call: {fired} "
              f"(violations {sr['n_violations']}, wrapper entries {sr['kernel_calls']['n']}, kernel calls with a violation {sr.get('kernel_calls_with_violation')})")
        ctl_bad += 0 if fired else 1
    print("CONTROLS:", "PASS" if ctl_bad == 0 else "FAIL")
    return 0 if (bad == 0 and ctl_bad == 0) else 1

# ----------------------------------------------------------------------------------------------- probe in the worker
def probe(worker):
    """Runs inside the (in-process) worker. Reads real attributes from the objects."""
    w = getattr(worker, "worker", worker)
    mr = w.model_runner
    info = {"model_runner": f"{type(mr).__module__}.{type(mr).__name__}"}
    sp = getattr(mr, "speculator", None)
    import builtins

    builtins._ck_speculator = sp  # same process (VLLM_ENABLE_V1_MULTIPROCESSING=0): lets main() wrap propose() for --trace-drafts
    info["speculator"] = None if sp is None else f"{type(sp).__module__}.{type(sp).__name__}"
    if sp is not None:
        info["use_fused_multi_step_decode"] = getattr(sp, "use_fused_multi_step_decode", "<absent>")
        info["num_speculative_steps"] = getattr(sp, "num_speculative_steps", None)
        info["advance_draft_positions"] = getattr(sp, "advance_draft_positions", None)
        info["draft_max_seq_len_last"] = getattr(sp, "draft_max_seq_len", None)
        info["speculator_max_model_len"] = getattr(sp, "max_model_len", None)
        groups = []
        for gs in getattr(sp, "attn_groups", []):
            for g in gs:
                row = {"backend": g.backend.get_name()}
                try:
                    row["supports_draft_decode_metadata_update"] = g.supports_draft_decode_metadata_update
                except Exception as e:  # noqa: BLE001
                    row["supports_draft_decode_metadata_update"] = f"<{type(e).__name__}: {e}>"
                try:
                    row["builder"] = f"{type(g.metadata_builders[0]).__module__}.{type(g.metadata_builders[0]).__name__}"
                except Exception as e:  # noqa: BLE001
                    row["builder"] = f"<{type(e).__name__}>"
                try:
                    row["block_size"] = g.kv_cache_spec.block_size
                except Exception as e:  # noqa: BLE001
                    row["block_size"] = f"<{type(e).__name__}>"
                groups.append(row)
        info["draft_attn_groups"] = groups
    blocks = []
    try:
        for kg in mr.kv_cache_config.kv_cache_groups:
            blocks.append({"layers": len(kg.layer_names), "block_size": kg.kv_cache_spec.block_size,
                           "head_size": kg.kv_cache_spec.head_size, "num_kv_heads": kg.kv_cache_spec.num_kv_heads})
    except Exception as e:  # noqa: BLE001
        blocks = [f"<{type(e).__name__}: {e}>"]
    info["kv_cache_groups"] = blocks
    info["cache_config_block_size"] = mr.vllm_config.cache_config.block_size
    info["cudagraph_mode"] = str(mr.vllm_config.compilation_config.cudagraph_mode)
    return info


# ----------------------------------------------------------------------------------------------- the run
def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--arm", choices="ABC")
    ap.add_argument("--expect-fused", type=int, choices=(0, 1), default=None)
    ap.add_argument("--gpu-util", type=float, default=0.08)
    ap.add_argument("--compare", action="store_true")
    ap.add_argument("--tag", default="", help="suffix for this run's output file (diagnostic reruns)")
    ap.add_argument("--trace-drafts", action="store_true", help="record the draft tokens returned by every speculator.propose()")
    ap.add_argument("--v2", action="store_true",
                    help="restricted-lm_head models (models2/), outputs in out2/, and assert nonzero acceptance + non-constant output")
    ap.add_argument("--sabotage", choices=("seqlens", "seqlens_half", "maxseq"), default=None,
                    help="negative control, eager arm C only. seqlens: the wrapper gives the kernel seq_lens-1 (copy, >=1). "
                         "maxseq: the wrapper treats max_seq_len as too small; assertion (c) must fire BEFORE the kernel call")
    ap.add_argument("--compare-sabotage", choices=("seqlens", "seqlens_half", "maxseq"), default=None,
                    help="with --compare: also compare the sabotaged eager arm against A and report whether the comparison catches it")
    ap.add_argument("--eager", action="store_true", help="enforce_eager: no CUDA graphs, so the draft decode calls the kernel from Python at every step")
    args = ap.parse_args()
    global OUT, TARGET, DRAFT
    if args.v2:
        OUT = os.path.join(HERE, os.environ.get("CK_OUT", "out2"))
        TARGET = os.path.join(HERE, os.environ.get("CK_MODELS", "models2"), "target")
        DRAFT = os.path.join(HERE, os.environ.get("CK_MODELS", "models2"), "draft")
    if args.compare:
        sys.exit(compare2() if args.v2 else compare_legacy(False, args.compare_sabotage))
    if args.sabotage:
        assert args.eager and args.arm in ("A", "C"), "--sabotage is for the eager arms A and C only"
        if args.arm == "A":
            assert args.sabotage != "maxseq"
    assert args.arm, "--arm is required"
    if args.arm == "A":
        assert args.expect_fused is None
    else:
        assert args.expect_fused is not None, "arms B and C need --expect-fused 0|1"

    os.environ.setdefault("VLLM_ENABLE_V1_MULTIPROCESSING", "0")
    os.environ.setdefault("HF_HUB_OFFLINE", "1")
    assert os.environ["VLLM_ENABLE_V1_MULTIPROCESSING"] == "0", "must run in-process"
    os.makedirs(OUT, exist_ok=True)

    import random

    import numpy as np
    import torch

    random.seed(0)
    np.random.seed(0)
    torch.manual_seed(0)

    failures: list[str] = []

    def fail(msg):
        failures.append(msg)
        print(f"FAIL: {msg}", flush=True)

    # ---- log capture (warning must be absent / info present)
    log_msgs: list[str] = []

    class Cap(logging.Handler):
        def emit(self, record):
            try:
                log_msgs.append(record.getMessage())
            except Exception:  # noqa: BLE001
                pass

    import vllm
    import vllm._custom_ops as ops
    import vllm.v1.attention.backends.rocm_attn as rocm_attn_mod

    # After the import: vllm.logger reconfigures the "vllm" logger with dictConfig at import time and would drop an
    # earlier handler (the first arm-B run lost its log capture this way). Self-checked below.
    logging.getLogger("vllm").addHandler(Cap())

    res: dict = {
        "arm": args.arm,
        "vllm_file": vllm.__file__,
        "vllm_version": vllm.__version__,
        "rocm_attn_file": rocm_attn_mod.__file__,
        "builder_flag_class_attr": getattr(rocm_attn_mod.RocmAttentionMetadataBuilder,
                                           "supports_draft_decode_metadata_update", "<absent>"),
        "gpu_util_requested": args.gpu_util, "eager": args.eager,
    }
    print("vllm.__file__ =", vllm.__file__, vllm.__version__, flush=True)
    print("rocm_attn.__file__ =", rocm_attn_mod.__file__, flush=True)
    print("RocmAttentionMetadataBuilder.supports_draft_decode_metadata_update =",
          res["builder_flag_class_attr"], flush=True)
    # The flag on the class has to match the arm before anything runs.
    if args.arm == "C" and res["builder_flag_class_attr"] is not True:
        fail("arm C: the opt-in is not mounted (class flag is not True)")
    if args.arm in ("A", "B") and res["builder_flag_class_attr"] is True:
        fail(f"arm {args.arm}: the opt-in is present but this arm must run the unchanged 0.29 code")

    # ---- wrapper around the custom kernel
    calls = {"n": 0, "eager": 0, "captured": 0}
    raw_samples: list[dict] = []
    violations: list[dict] = []
    cap_ms, cap_partitions = set(), set()
    eager_stats = {"max_seqlen_seen": 0, "max_partitions_needed": 0, "max_partitions_alloc": 0,
                   "max_max_seq_len": 0, "min_slack_partitions": None, "min_margin_tokens": None, "n_calls_max_seq_len_lt_2048": 0, "min_margin_tokens_lt_2048": None, "q_tokens_set": set()}
    orig = ops.paged_attention_rocm

    guard = {"raised": 0, "kernel_calls_with_violation": 0}
    armed = {"on": False}  # sabotage starts after engine init (warmup/profile runs are not part of the control)

    def wrapped(*a, **kw):
        tmp_output = a[3] if len(a) > 3 else kw["tmp_out"]
        seq_lens = kw["seq_lens"] if "seq_lens" in kw else a[10]
        max_seq_len = kw["max_seq_len"] if "max_seq_len" in kw else a[13]
        query = a[4] if len(a) > 4 else kw["query"]
        capturing = torch.cuda.is_current_stream_capturing()
        rec = {"capturing": capturing, "max_seq_len": int(max_seq_len),
               "tmp_output_shape": list(tmp_output.shape), "q_tokens": int(query.shape[0]),
               "num_seqs": int(seq_lens.shape[0])}
        calls["n"] += 1
        if capturing:
            calls["captured"] += 1
            cap_ms.add(int(max_seq_len))
            cap_partitions.add(int(tmp_output.shape[2]))
            rec["seq_lens_max"] = None
            if int(max_seq_len) != MAX_MODEL_LEN:
                violations.append({"kind": "capture max_seq_len != max_model_len", **rec})
        else:
            calls["eager"] += 1
            smax = int(seq_lens.max().item())
            rec["seq_lens_max"] = smax
            need = math.ceil(smax / PARTITION)
            have = int(tmp_output.shape[2])
            eager_stats["max_seqlen_seen"] = max(eager_stats["max_seqlen_seen"], smax)
            eager_stats["max_partitions_needed"] = max(eager_stats["max_partitions_needed"], need)
            eager_stats["max_partitions_alloc"] = max(eager_stats["max_partitions_alloc"], have)
            eager_stats["max_max_seq_len"] = max(eager_stats["max_max_seq_len"], int(max_seq_len))
            eager_stats["q_tokens_set"].add(int(query.shape[0]))
            eff_max = int(max_seq_len)
            if armed["on"] and args.sabotage == "maxseq":
                # Negative control: pretend the eager max_seq_len handed to the kernel is half the real longest sequence.
                eff_max = max(1, smax // 2)
                rec["sabotaged_max_seq_len"] = eff_max
            margin = eff_max - smax
            if eager_stats["min_margin_tokens"] is None or margin < eager_stats["min_margin_tokens"]:
                eager_stats["min_margin_tokens"] = margin
            if eff_max < MAX_MODEL_LEN:
                eager_stats["n_calls_max_seq_len_lt_2048"] += 1
                if eager_stats["min_margin_tokens_lt_2048"] is None or margin < eager_stats["min_margin_tokens_lt_2048"]:
                    eager_stats["min_margin_tokens_lt_2048"] = margin
            slack = have - need
            if eager_stats["min_slack_partitions"] is None or slack < eager_stats["min_slack_partitions"]:
                eager_stats["min_slack_partitions"] = slack
            if have < need or eff_max < smax or (armed["on"] and args.sabotage == "maxseq" and math.ceil(eff_max / PARTITION) < need):
                v = {"kind": "eager partitions do not cover seq_lens", "need": need, "have": have, **rec}
                violations.append(v)
                # Never call the kernel with undersized scratch or max_seq_len: it would write out of bounds on a shared GPU.
                guard["raised"] += 1
                raise AssertionError(f"(c) scratch sizing violated BEFORE the kernel call, kernel not called: {v}")
            if armed["on"] and args.sabotage in ("seqlens", "seqlens_half"):
                # Negative control: the kernel sees shorter sequences (a copy, never below 1). Smaller lengths stay in bounds.
                kw = dict(kw)
                kw["seq_lens"] = ((seq_lens - 1) if args.sabotage == "seqlens" else (seq_lens - seq_lens // 2)).clamp(min=1)
        if (not capturing and rec.get("seq_lens_max", 0) and rec["seq_lens_max"] > 200 and len(raw_samples) < 60):
            raw_samples.append(rec)
        elif len(raw_samples) < 12 or (capturing and len(raw_samples) < 40):
            raw_samples.append(rec)
        return orig(*a, **kw)

    ops.paged_attention_rocm = wrapped

    replays = {"n": 0}
    try:
        _orig_replay = torch.cuda.CUDAGraph.replay

        def _replay(self, *a, **kw):
            replays["n"] += 1
            return _orig_replay(self, *a, **kw)

        torch.cuda.CUDAGraph.replay = _replay
    except Exception as e:  # noqa: BLE001
        res["replay_counter"] = f"not installed: {type(e).__name__}: {e}"

    # ---- VRAM sampler (device-wide free; production is on the same card)
    vram = {"free_min_gib": None, "free_start_gib": None, "total_gib": None}
    stop_vram = threading.Event()

    def vram_loop():
        while not stop_vram.is_set():
            try:
                free, total = torch.cuda.mem_get_info()
                f = free / 2**30
                vram["total_gib"] = total / 2**30
                if vram["free_start_gib"] is None:
                    vram["free_start_gib"] = f
                vram["free_min_gib"] = f if vram["free_min_gib"] is None else min(vram["free_min_gib"], f)
            except Exception:  # noqa: BLE001
                pass
            stop_vram.wait(1.0)

    torch.cuda.init()
    th_v = threading.Thread(target=vram_loop, daemon=True)
    th_v.start()

    from vllm import LLM, SamplingParams

    kwargs = dict(
        model=TARGET, tokenizer=TARGET, dtype="float16", max_model_len=MAX_MODEL_LEN,
        gpu_memory_utilization=args.gpu_util, max_num_seqs=8, max_num_batched_tokens=2048,
        attention_backend="ROCM_ATTN", block_size=16, seed=0, enable_prefix_caching=False,
        disable_log_stats=False, enforce_eager=args.eager,
    )
    if args.arm != "A":
        kwargs["speculative_config"] = {"method": "eagle", "model": DRAFT, "num_speculative_tokens": NUM_SPEC}
    print("LLM kwargs:", json.dumps(kwargs, default=str), flush=True)
    t0 = time.time()
    try:
        llm = LLM(**kwargs)
    except BaseException:  # noqa: BLE001
        fail("LLM() construction raised:\n" + traceback.format_exc())
        res.update(status="FAIL", failures=failures, tokens={}, log_has_fallback_warning=None,
                   vram=vram, calls=calls, violations=violations[:10])
        stop_vram.set()
        json.dump(res, open(os.path.join(OUT, f"arm_{args.arm}{'_eager' if args.eager else ''}{'_sabotage_' + args.sabotage if args.sabotage else ''}{('_' + args.tag) if args.tag else ''}.json"), "w"), indent=1, default=str)
        print("RESULT: FAIL", flush=True)
        sys.exit(1)
    res["init_seconds"] = round(time.time() - t0, 1)
    time.sleep(1.5)
    res["vram_after_init_free_gib"] = torch.cuda.mem_get_info()[0] / 2**30

    armed["on"] = True
    trace: list = []
    cur_phase = {"name": None}

    # ---- (a) real attributes from the speculator object
    info = llm.collective_rpc(probe)[0]
    res["probe"] = info
    if (args.trace_drafts or args.v2) and args.arm != "A":
        import builtins

        spk = builtins._ck_speculator
        _orig_propose = spk.propose

        def _traced(*a, **kw):
            out = _orig_propose(*a, **kw)
            try:
                ns, ls = kw.get("num_sampled"), kw.get("last_sampled")
                trace.append({"phase": cur_phase["name"], "drafts": out.cpu().tolist(),
                              "num_sampled": None if ns is None else ns.cpu().tolist(),
                              "last_sampled": None if ls is None else ls[: out.shape[0]].cpu().tolist()})
            except Exception as e:  # noqa: BLE001
                trace.append({"phase": cur_phase["name"], "error": f"{type(e).__name__}: {e}"})
            return out

        spk.propose = _traced
    print("probe:", json.dumps(info, default=str), flush=True)
    res["vllm_config_block_size"] = info["cache_config_block_size"]
    for g in info["kv_cache_groups"]:
        if not isinstance(g, dict) or g["block_size"] != 16 or g["head_size"] != 128:
            fail(f"kv cache group is not head 128 / block 16: {g}")
    if args.arm == "A":
        if info["speculator"] is not None:
            fail(f"arm A has a speculator: {info['speculator']}")
    else:
        if info["speculator"] is None:
            fail("no speculator object")
        else:
            got = info.get("use_fused_multi_step_decode")
            if got is not bool(args.expect_fused):
                fail(f"(a) arm {args.arm}: use_fused_multi_step_decode is {got!r}, expected {bool(args.expect_fused)}")
            for gr in info["draft_attn_groups"]:
                if gr["backend"] != "ROCM_ATTN":
                    fail(f"draft attention backend is {gr['backend']}, expected ROCM_ATTN")
                if gr["supports_draft_decode_metadata_update"] is not bool(args.expect_fused):
                    fail(f"(a) draft attn group supports_draft_decode_metadata_update={gr['supports_draft_decode_metadata_update']!r}")
    # target backend check
    if not any("ROCM_ATTN" in m for m in log_msgs):
        fail("log capture is broken: no vllm log record mentioning ROCM_ATTN was seen")
    sp_no = [m for m in log_msgs if "attention backend" in m.lower() or "ROCM_ATTN" in m]
    res["backend_log_lines"] = sp_no[:6]

    # ---- workload
    sp_main = SamplingParams(temperature=0.0, max_tokens=NEW_TOKENS, ignore_eos=True)
    tokens: dict = {}
    phase_status: dict = {}

    class PhaseTimeout(Exception):
        pass

    def on_alarm(signum, frame):
        raise PhaseTimeout()

    signal.signal(signal.SIGALRM, on_alarm)

    def gen(prompts, sp, timeout):
        signal.alarm(timeout)
        try:
            return llm.generate([{"prompt_token_ids": p} for p in prompts], sp, use_tqdm=False)
        finally:
            signal.alarm(0)

    def canary(tag):
        """After a failure (and once at the end) the same process serves one more request."""
        ids = np.random.RandomState(777).randint(100, 50000, size=32).tolist()
        try:
            o = gen([ids], SamplingParams(temperature=0.0, max_tokens=8, ignore_eos=True), 120)
            ok = len(o[0].outputs[0].token_ids) == 8 and o[0].outputs[0].finish_reason == "length"
            return {"tag": tag, "served": bool(ok), "tokens": list(o[0].outputs[0].token_ids)}
        except BaseException:  # noqa: BLE001
            return {"tag": tag, "served": False, "error": traceback.format_exc()[-600:]}

    canaries = []
    for pi, (name, lens) in enumerate(PHASES):
        prompts = [prompt_ids(pi, i, n) for i, n in enumerate(lens)]
        cur_phase["name"] = name
        t1 = time.time()
        try:
            outs = gen(prompts, sp_main, PHASE_TIMEOUT_S)
        except PhaseTimeout:
            fail(f"(e) phase {name}: timeout after {PHASE_TIMEOUT_S}s")
            phase_status[name] = "TIMEOUT"
            canaries.append(canary(f"after_timeout_{name}"))
            continue
        except BaseException:  # noqa: BLE001
            fail(f"(e) phase {name}: exception\n" + traceback.format_exc())
            phase_status[name] = "EXCEPTION"
            canaries.append(canary(f"after_exception_{name}"))
            continue
        toks, ok = [], True
        for i, o in enumerate(outs):
            c = o.outputs[0]
            toks.append(list(c.token_ids))
            if len(c.token_ids) != NEW_TOKENS or c.finish_reason != "length":
                ok = False
                fail(f"(e) phase {name} request {i}: {len(c.token_ids)} tokens, finish_reason={c.finish_reason!r}")
        tokens[name] = toks
        phase_status[name] = "OK" if ok else "BAD_OUTPUT"
        if not ok:
            canaries.append(canary(f"after_bad_output_{name}"))
        print(f"phase {name}: c={len(lens)} {phase_status[name]} in {time.time() - t1:.1f}s", flush=True)
    canaries.append(canary("final"))
    for c in canaries:
        if not c["served"]:
            fail(f"(e) follow-up request not served: {c}")
    res["canaries"] = canaries
    if trace:
        res["draft_trace"] = trace
    distinct = [len(set(t)) for toks_ in tokens.values() for t in toks_]
    res["distinct_tokens_per_request"] = distinct
    print("distinct generated tokens per request:", distinct, flush=True)
    res["phase_status"] = phase_status
    res["tokens"] = tokens

    # ---- spec decode metrics (accepted / drafted), when readable
    try:
        mets = {}
        for m in llm.get_metrics():
            if "spec_decode" in m.name:
                mets[m.name] = getattr(m, "value", None) if hasattr(m, "value") else str(getattr(m, "values", ""))
        res["spec_metrics"] = mets
    except Exception as e:  # noqa: BLE001
        res["spec_metrics"] = f"unreadable: {type(e).__name__}: {e}"
    res["probe_after"] = llm.collective_rpc(probe)[0]
    if args.v2 and not args.sabotage:
        if not distinct or min(distinct) < 2:
            fail(f"(d) generated output is constant for some request (distinct tokens per request {distinct}); the token comparison would be vacuous")
        if args.arm != "A":
            sm = res["spec_metrics"]
            if not isinstance(sm, dict) or "vllm:spec_decode_num_accepted_tokens" not in sm:
                fail(f"spec decode metrics unreadable: {sm}")
            else:
                acc = sm["vllm:spec_decode_num_accepted_tokens"]
                drf = sm["vllm:spec_decode_num_draft_tokens"]
                res["acceptance"] = {"accepted": acc, "drafted": drf, "rate": (acc / drf if drf else None)}
                print(f"acceptance: {acc}/{drf} = {acc / drf if drf else float('nan'):.4f}", flush=True)
                if not (acc and acc > 0):
                    fail(f"(d) num_accepted_tokens is {acc}; accepted tokens were not tested")

    # ---- (b) kernel really ran, fallback warning absent
    res["kernel_calls"] = dict(calls)
    res["guard"] = dict(guard)
    res["kernel_calls_with_violation"] = guard["kernel_calls_with_violation"]
    res["sabotage"] = args.sabotage
    res["graph_replays"] = replays["n"]
    fallback = [m for m in log_msgs if "Cannot use ROCm custom paged attention kernel" in m]
    res["log_has_fallback_warning"] = bool(fallback)
    res["log_has_fused_unsupported_info"] = any("Fused multi-step draft decode is not supported" in m for m in log_msgs)
    if calls["n"] == 0:
        fail("(b) ops.paged_attention_rocm was never called")
    if calls["eager"] == 0:
        fail("(b) no eager call of ops.paged_attention_rocm")
    if fallback:
        fail(f"(b) fallback warning present: {fallback[0]}")
    if args.arm == "A" and res["log_has_fused_unsupported_info"]:
        fail("arm A logged the fused-unsupported message")
    if args.arm == "B" and not res["log_has_fused_unsupported_info"]:
        fail("arm B did not log that fused multi-step draft decode is unsupported")
    if args.arm == "C" and res["log_has_fused_unsupported_info"]:
        fail("arm C logged that fused multi-step draft decode is unsupported")

    # ---- (c) scratch sizing
    eager_stats["q_tokens_set"] = sorted(eager_stats["q_tokens_set"])
    res["eager_stats"] = eager_stats
    res["captured_max_seq_len_values"] = sorted(cap_ms)
    res["captured_tmp_output_partitions"] = sorted(cap_partitions)
    res["raw_call_samples"] = raw_samples
    res["violations"] = violations[:20]
    res["n_violations"] = len(violations)
    if violations:
        fail(f"(c) {len(violations)} scratch-sizing violations, first: {violations[0]}")
    if args.arm != "A":
        if calls["captured"] == 0 and not args.eager:
            fail("(c) no captured (graph-capture) call was seen, cannot check max_seq_len at capture")
    if args.eager and calls["captured"]:
        fail("(c) --eager but a graph capture was seen")
    if cap_ms and cap_ms != {MAX_MODEL_LEN}:
        fail(f"(c) captured max_seq_len values {sorted(cap_ms)} != {{max_model_len={MAX_MODEL_LEN}}}")

    stop_vram.set()
    res["vram"] = vram
    res["status"] = "FAIL" if failures else "PASS"
    res["failures"] = failures
    json.dump(res, open(os.path.join(OUT, f"arm_{args.arm}{'_eager' if args.eager else ''}{'_sabotage_' + args.sabotage if args.sabotage else ''}{('_' + args.tag) if args.tag else ''}.json"), "w"), indent=1, default=str)
    summary = {k: res[k] for k in ("arm", "status", "kernel_calls", "graph_replays", "log_has_fallback_warning",
                                   "captured_max_seq_len_values", "captured_tmp_output_partitions", "eager_stats",
                                   "n_violations", "vram", "phase_status")}
    print("SUMMARY:", json.dumps(summary, default=str), flush=True)
    print("RESULT:", res["status"], flush=True)
    sys.stdout.flush()
    os._exit(0 if not failures else 1)  # skip engine teardown hangs; results are on disk


if __name__ == "__main__":
    main()
