# vLLM 0.29 on the R9700 — seven more speed options, measured (2026-10-27/28)

> Status: **partial result, stopped by the operator on 2026-10-28 11:53.** Phase A (server flags) ran 2 of 3
> rounds for five options and 1 round for FP8 KV; phases C (linear kernel), B1 (BF16 draft MoE tuning), B2
> (int4 keys 24–128) and the final combined A/B did not run. Follow-up to
> [RESULTS_vllm_029_r9700_fixes.md](RESULTS_vllm_029_r9700_fixes.md) — read that first for the server, the
> three fixes and the harness. Raw data, server logs, quality files and scripts:
> [vllm-r9700-speed-options/](vllm-r9700-speed-options/). Plan:
> `vllm_pr_work · todo/PLAN_r9700_speed_options.md` (plan gate `review_plan` proceed, 2 reviewers, 5
> findings accepted).

## 1. Summary

The question: after the three fixes (101.5 tok/s at 1 request, 256.0 at 4), what else can vLLM 0.29 give on
one AMD Radeon AI PRO R9700 with Qwen3.6-35B-A3B GPTQ-Int4? Seven options were listed; five could be tested as
server flags.

| # | Option | Verdict | Main number (median vs `base` in the same series) |
|---|---|---|---|
| 1a | MTP draft tokens = 3 (`mtp3`) | **passes** the ship test | decode +7 % at c=1, +3 % at c=4; time per output token −8 %; GSM8K equal |
| 6 | Attention backend `TRITON_ATTN` (`tattn`) | **passes** | +4 % at c=1, +5 % at c=4; time per output token −5 % / −8 %; GSM8K equal |
| 2 | `--max-num-batched-tokens 8192` (`mnbt8k`) | **trade-off, fails the rule as written** | time to first token −30 % (8k prompt) and −24 % (32k prompt); decode −1 % / −4 % |
| 1b | MTP draft tokens = 1 (`mtp1`) | fails | −12 % / −11 %; time per output token +19 % |
| 5 | FP8 KV cache (`fp8kv`) | fails as a speed option; a capacity switch | KV tokens +56 %; decode −12 % / −12 %; 32k prompt +55 % slower |
| 7 | Hybrid linear kernel at 6–16 rows | **not measured** (phase C did not run) | code read: the HIP path is limited to M ≤ 5, so MTP at 4 requests (M = 12) runs Triton |
| 3 | Tuned BF16 MoE file for the MTP draft layer | **not measured** (phase B1 did not run) | the draft layer still uses the default MoE config |
| 4 | int4 MoE keys 24, 32, 64, 128 | **not measured** (phase B2 did not run) | — |

Not tested: the combination (`mtp3` + `tattn`, with and without `mnbt8k`). Nothing was changed in the
operator's `start-vllm.ps1`; the production server runs as before (MTP 2, `ROCM_ATTN`, default batching).

Two environment findings matter more than any single flag (§ 6): in the test servers **one request per
benchmark run waits ~12 s longer** (mean TTFT ~2.1 s where production had 0.2 s; cause not found — decode speed
is equal), and **every vLLM model load drives Windows' "Modified" page list to 30–42 GB**, which stopped three
runs through the RAM guard.

## 2. Setup

| Item | Value |
|---|---|
| GPU | AMD Radeon AI PRO R9700 (gfx1201, 32 GB), PCIe Gen5 x16 link (read from Windows, 2026-10-27) |
| Host | Windows 11, 91.6 GB RAM; containers through `wslc` (WSL containers); dev WSL VM separate |
| Image | `rocm/vllm:rocm10.1.0_ubuntu24.04_py3.14-pytorch_2.13.0_vllm-0.29.0` (vLLM `0.29.1.dev0+g98dff2a81`) |
| Model | Qwen3.6-35B-A3B GPTQ-Int4 (group 128, sym), 40 layers: 10 full attention (head 256, 16 q / 2 kv heads) + 30 linear attention |
| Server shape (all arms) | variant C patches (= production `patches/`, sha256 equal), tuned int4 MoE file (byte-equal to production), `--max-model-len 131072 --max-num-seqs 4 --gpu-memory-utilization 0.85`, `--generation-config /genconfig` (the operator's: temperature 0.3, top_p 0.9, top_k 20, repetition_penalty 1.05), MTP 2 unless the arm changes it |
| Not in the test shape | `--reasoning-parser qwen3`, `--default-chat-template-kwargs.reasoning_effort medium`, the published port — production has them |
| Decode bench | `vllm bench serve`, random 1024 in / 512 out, `--ignore-eos`; c=1 (6 prompts) and c=4 (12 prompts), 3 reps per start |
| Prefill bench | random 8192 / 128 and 32768 / 128 at c=1, 3 prompts, 3 reps; median TTFT |
| Quality (round 1 only) | GSM8K-200 (greedy, temperature 0 sent explicitly — the server's `repetition_penalty 1.05` still applies) and 20 fixed greedy prompts compared token by token with `base` |
| Order | arms alternate inside a round: base, mtp1, mtp3, mnbt8k, tattn, fp8kv |
| Ship test (written before the run) | median better than `base` by more than `base`'s spread at c=1 or c=4; the other concurrency not worse by more than that spread; GSM8K within 2 × stderr; for `mnbt8k` also 32k TTFT lower by more than the spread; `fp8kv` kept only as a switch unless it passes on speed |
| Guards | disk gate (Windows System log, providers disk/stornvme) before every start — no new event in the whole series; RAM guard: stop all test containers when Windows "Available MBytes" < 4096 (from 2026-10-28 10:30: < 2048, operator decision); any stop restarts the production server |

Predictions were written into the plan before the first start (see § 5).

## 3. Results

### 3.1 Decode speed

Median over all valid reps (6 = 2 rounds × 3; `fp8kv` 3 = 1 round). Range in brackets.

| Arm | c=1 tok/s | c=4 tok/s | TPOT c=1 (ms) | TPOT c=4 (ms) |
|---|---|---|---|---|
| **base** (MTP 2) | 76.5 (75.5–78.8) | 180.5 (178.3–183.7) | 8.91 (8.53–8.96) | 13.64 (13.23–14.03) |
| mtp1 | 67.6 (66.6–92.6\*) **−12 %** | 161.0 (157.5–232.7\*) **−11 %** | 10.61 **+19 %** | 16.36 **+20 %** |
| **mtp3** | **81.8** (80.9–87.2) **+7 %** | **186.2** (181.7–192.7) **+3 %** | **8.16 −8 %** | **12.76 −6 %** |
| mnbt8k | 75.9 (74.8–76.7) −1 % | 174.1 (172.9–174.8) −4 % | 9.19 +3 % | 14.05 +3 % |
| **tattn** | **79.2** (77.7–82.0) **+4 %** | **190.2** (188.0–196.2) **+5 %** | **8.46 −5 %** | **12.59 −8 %** |
| fp8kv | 67.2 (66.5–69.9) **−12 %** | 159.0 (157.1–169.1) **−12 %** | 10.86 **+22 %** | 15.35 **+13 %** |

\* Three `mtp1` round-1 runs (c=1 rep 2, c=4 reps 2 and 3) are the only runs in the series without the ~12 s
stall of § 6.1: 92.6 tok/s at c=1 (mean TTFT 197 ms) and 232.7 / 228.9 at c=4 (mean TTFT 479 / 395 ms), with
TPOT unchanged (10.44 / 16.25 / 16.52 ms). They show the size of the stall's effect; the medians of 6 are not
moved by them.

`base` spread: 3.4 tok/s at c=1 (4.4 %), 5.4 tok/s at c=4 (3.0 %). **TPOT is the clean decode number here**:
every tok/s value includes one request per run that waited ~12 s (§ 6.1), which lowers all arms and dilutes
every relative difference.

### 3.2 Prefill (time to first token)

| Arm | 8k prompt, median TTFT | 32k prompt, median TTFT |
|---|---|---|
| base | 1.93 s | 16.03 s |
| mtp1 | 1.88 s | 15.53 s |
| mtp3 | 1.95 s | 16.22 s |
| **mnbt8k** | **1.35 s (−30 %)** | **12.15 s (−24 %)** |
| tattn | 1.98 s | 15.50 s |
| fp8kv | 2.38 s (+23 %) | 24.81 s (+55 %) |

Why `mnbt8k` helps: with MTP, vLLM 0.29 logs `max_num_scheduled_tokens is set to 2048 based on the speculative
decoding settings. This may lead to suboptimal performance`, so a 32k prompt is processed in 16 chunks; with
8192 the chunks are 4× larger.

### 3.3 MTP acceptance

From `/metrics` after the decode and prefill benches of each start (`raw/spec_metrics.txt`).

| Arm-round | Accepted at position 1 / 2 / 3 | Mean tokens per step |
|---|---|---|
| base-1 / base-2 | 0.923 / 0.864 — 0.920 / 0.855 | 2.79 / 2.77 |
| mtp1-1 / mtp1-2 | 0.927 — 0.929 | 1.93 / 1.93 |
| mtp3-1 / mtp3-2 | 0.898 / 0.831 / 0.748 — 0.895 / 0.824 / 0.740 | 3.48 / 3.46 |
| mnbt8k-1 / mnbt8k-2 | 0.890 / 0.788 — 0.899 / 0.796 | 2.68 / 2.69 |
| tattn-1 / tattn-2 | 0.917 / 0.836 — 0.916 / 0.829 | 2.75 / 2.75 |
| fp8kv-1 | 0.911 / 0.851 | 2.76 |

The vLLM warning "num_speculative_tokens > 1 will run multiple times of forward on same MTP layer, which may
result in lower acceptance rate" is real but small: position 3 still accepts 74 %, and 3.47 tokens per step
beat 2.78. The 2026-10-26 run measured 87 % / 72 % on different prompts at temperature 1.0 — acceptance
depends strongly on sampling (§ 6.2).

### 3.4 Quality

| Arm | GSM8K-200 | vs base (limit ±5.1 points = 2 × stderr) | 20 greedy prompts identical with base |
|---|---|---|---|
| base | 84.5 % (169/200) | — | — |
| mtp1 | 84.0 % | −0.5, pass | 13 / 20 |
| mtp3 | 84.5 % | 0.0, pass | 14 / 20 |
| mnbt8k | 85.5 % | +1.0, pass | 12 / 20 |
| tattn | 84.0 % | −0.5, pass | 12 / 20 |
| fp8kv | 88.0 % | +3.5, pass | 11 / 20 |

The 20-prompt comparison is a record, not a gate (as in the plan): MTP's verify step already changes near-ties,
and in this series the divergences are **not** all near-ties (the harness flags `no` for every arm). No
`base`-vs-`base` repeat was run, so the run-to-run identity of `base` itself is unknown — do not read 11–14 / 20
as a defect of any arm. GSM8K here (84.5 %) is lower than the 88.5 % of 2026-10-27, which ran without MTP and
without the operator's `genconfig`; `repetition_penalty 1.05` applies to the greedy GSM8K requests too.

### 3.5 KV cache size

| Arm | Round 1 (2026-10-27, coai-exp running) | Round 2 (2026-10-28, after a WSL restart) |
|---|---|---|
| base | 234,958 | 234,958 (round 2 started 2026-10-27 23:30, before the restart) |
| mtp1 | 211,280 | 249,428 |
| mtp3 | 194,708 | 229,850 |
| mnbt8k | 170,879 | 191,268 |
| tattn | 201,947 | 237,871 |
| fp8kv | 366,647 | 432,183 (start completed, run stopped) |

The KV size depends on free VRAM at start, and other Windows apps hold R9700 memory (Viber, Snagit, `dwm`:
~2.7 GB on 2026-10-27). Rounds differ by up to 38k tokens for the same arm, so **only `fp8kv` (+56 % / +84 %)
and `mnbt8k` (lowest in both rounds) are clear**; the other differences are within that noise.

## 4. Verdict per option

| Option | Ship test | Read |
|---|---|---|
| `mtp3` | c=1 +5.3 tok/s > spread 3.4 ✓; c=4 +5.6 > 5.4 ✓ (just); GSM8K ✓ | **Keep candidate.** Gains on both loads, TPOT −8 %, acceptance 0.90 / 0.83 / 0.74 |
| `tattn` | c=1 +2.7 < 3.4 ✗; c=4 +9.7 > 5.4 ✓; c=1 not worse ✓; GSM8K ✓ | **Keep candidate.** The log confirms `Using TRITON_ATTN backend (selected via --attention-backend)` and the absence of `Fused multi-step draft decode is not supported` — the fused draft steps are on |
| `mnbt8k` | decode not better ✗ (rule 1); c=4 −6.5 tok/s > spread ✗ (rule 2); 32k TTFT −3.9 s ✓ | **Operator's choice.** Long prompts 24–30 % faster for ~4 % decode and the smallest KV cache. Worth it for code / document prompts |
| `mtp1` | worse everywhere | Drop |
| `fp8kv` | worse on speed | Keep only as a switch for very long context or more parallel requests (`-ExtraArgs "--kv-cache-dtype","fp8"`) |

## 5. Predictions against observation

Written in the plan before the run.

| Option | Prediction | Observed | Right? |
|---|---|---|---|
| mtp1 | c=1 −5..−15 %, c=4 within ±5 % | −12 % / −11 % | c=1 yes, c=4 **no** |
| mtp3 | c=1 within ±5 %, c=4 lower | +7 % / +3 % | **no** — more drafts paid off |
| mnbt8k | decode within spread; 32k TTFT −10..−30 % | −1 % / −4 %; 32k −24 %, 8k −30 % | yes (c=4 just outside spread) |
| fp8kv | KV ~2×; decode ±5 %; GSM8K within limit | KV +56 %; decode −12 %; prefill +23..+55 %; GSM8K pass | KV and decode **no** |
| tattn | ±5 %, maybe +3 % at c=1 | +4 % / +5 % | yes |

## 6. Environment findings (read before repeating this)

### 6.1 One request per benchmark run stalls ~12 s in the test servers; production did not — not explained

| c=1, 1024-token prompt | Mean TTFT | Mean TPOT | tok/s |
|---|---|---|---|
| Production server, 2026-10-27 12:17 (`bench_prod_moe`) | 0.20–0.23 s | 8.9–9.5 ms | 101.5 |
| Test `base`, rounds 1–2 | 2.14–2.24 s | 8.5–8.9 ms | 76.5 |

It is **not** a delay on every request. In every prefill run (3 prompts) the mean TTFT is 3.4–5.9 s above the
median (for example `base-1` 8k: mean 5.70 s, median 1.93 s), so one of the three requests waited about
11–13 s more than the others. The c=1 decode numbers fit the same picture: (12 s + 5 × 0.2 s) / 6 ≈ 2.2 s mean
TTFT. Medians are therefore clean (the prefill tables use medians); mean TTFT and every tok/s value carry the
stall. Only three runs in the series were free of it (`mtp1` round 1, § 3.1 footnote), so the stall is
intermittent per `vllm bench serve` invocation, not per server.

Decode itself is equal (TPOT even slightly lower in the tests). Checked equal between test and production: the two patch files
(sha256), the tuned MoE JSON (byte-equal), `vllm.env` (only `HIP_VISIBLE_DEVICES=0` and the Triton cache dir),
the sampling defaults (both logs: `{'repetition_penalty': 1.05, 'temperature': 0.3, 'top_k': 20, 'top_p': 0.9}`),
`--max-model-len`, `--max-num-seqs`. Not equal: production adds `--reasoning-parser qwen3`,
`--default-chat-template-kwargs.reasoning_effort medium` and `-p 8000:8000`; the test sets
`PYTHONPYCACHEPREFIX=/tmp/pyc-fresh`. **Next step:** run `bench.sh` against the production container now —
if it also stalls, something changed on the host since 2026-10-27 12:17 (the RAM behaviour of § 6.5 is a
candidate: a trimmed container VM must page memory back in); if it does not, bisect the three flags. Adding
`--percentile-metrics ttft --metric-percentiles 50,99` to the bench shows the stalled request directly. Until
then, compare arms only with each other and prefer TPOT and median TTFT.

### 6.2 Sampling defaults change MTP speed by ~25 %

The first try (2026-10-27 15:31) started test servers without `--generation-config`, so the model's own
defaults applied (temperature 1.0, top_p 0.95). `base` gave 75.9 tok/s at c=1 against ~101 in production with
temperature 0.3. Any MTP benchmark must use the production sampling defaults; `vllm bench serve` sends no
temperature, so the server's defaults decide. Data: `discarded/temp1.0_bench.txt`.

### 6.3 A 3D app on the R9700 slows vLLM several times

On 2026-10-27 ~16:00 SnagitEditor ran its 3D engine on the R9700 at 28–37 % without pause (Windows GPU engine
counters, LUID of the adapter with 26.7 GB dedicated in use = the R9700), and held ~650 MB of its VRAM; together
with CPU load from another job, `base` fell to 49.6 tok/s at c=1, 53 at c=4, 10.4 s TTFT for 8k and **360 s**
for 32k (`discarded/cpu_snagit_server_base-1.log`; the bench lines were lost with the WSL temp folder, the
numbers here are from the session record). The operator set SnagitEditor and SnagitCapture to "Power saving
(890M)" in Windows Graphics settings; the setting applies at app start. After the PC restart Snagit showed
~1 % 3D work on one adapter; which adapter that LUID is was not checked after the restart. The GPU itself was healthy throughout: the fused-MoE kernel times on
2026-10-27 17:15 matched the morning (bs 1: 74 µs vs 66–68; bs 16: 450 vs 438–468; bs 256: 1085 vs 1101–1106;
bs 2048: 2694 vs 2717–2735).

To see vLLM in Task Manager, select the GPU's **Compute 0** graph — the default 3D graph shows nothing.

### 6.4 CPU load from another job

The coai-exp experiment (Claude sessions + `dotnet build` / tests / eslint in the dev WSL VM) pushed the WSL
load average to 12–20 on 2026-10-27 afternoon and evening; at the start of round 1 (20:58) it was ~11. Round 1
(under that load) and round 2 (mostly on a quiet PC) agree closely per arm — round-median change at c=1 / c=4:
base 0.0 / +0.3 %, mtp3 −0.9 / −3.9 %, mnbt8k −1.4 / 0.0 %, tattn −1.9 / −2.2 %, mtp1 +1.1 / −29.7 % (the last
because two round-1 runs were free of the § 6.1 stall). So CPU load did not explain the gap to production.
`raw/cpuload_round1.txt` has load samples from 2026-10-27 16:05–17:25 (the discarded run of § 6.3).

### 6.5 RAM: every model load drives the "Modified" page list to 30–42 GB

The RAM guard stopped three starts during the model load (2026-10-27 17:56 and 20:20 at 4 GB, 2026-10-28 10:08
at 3.7 GB) — the last one on a quiet PC that had **57 GB available** at start. Sampled every 15 s during the
production server's start at 2026-10-28 10:09:

| Time | Available | Modified page list | `vmmemwslc-cli-strug` (private) | `vmmemWSL` |
|---|---|---|---|---|
| 10:12:53 | 34.5 GB | 1.1 GB | 29.7 GB | 5.6 GB |
| 10:13:27 | 8.4 GB | 30.4 GB | 28.9 GB | 5.6 GB |
| 10:14:51 | 7.2 GB | 41.7 GB | 30.1 GB | 5.6 GB |
| 10:15:42 | 30.1 GB | 29.0 GB | 7.1 GB | 5.6 GB |

`vmmemwslc-cli-strug` is the container VM that `wslc` runs vLLM in. The page file stayed almost unused
(47 MB of 12.8 GB). Likely reading, **not proven**: Windows trims the container VM's working set (~29 → 7 GB)
and parks those pages on the Modified list, which does not count as available. Earlier the same day the
Modified list reached 81.7 GB with 0 GB available. The WSL dev VM was limited by the operator on 2026-10-28
(`.wslconfig`: `memory=44GB`, `autoMemoryReclaim=dropCache`; backup `.wslconfig.bak-2026-10-27`) — that did
not change the container VM's behaviour. With the guard at 2 GB (operator decision) the starts of 2026-10-28
went through.

### 6.6 Code facts for options 6 and 7 (read from the image)

- **Fast ROCm paged attention** (`platforms/rocm.py:411-422`, gfx1x branch) needs head size 128, block size 16,
  q/kv head ratio 3–16, no sliding window, KV dtype `auto`. Qwen3.6-35B-A3B has head 256 and a hybrid layout,
  so vLLM sets the attention block to 1072 tokens (`Setting attention block size to 1072 tokens to ensure that
  attention page size is >= mamba page size`) — it can never use the kernel. The same holds for
  Qwen3.8-27B-AWQ (head 256, 16 full + 48 linear-attention layers, q/kv 24/4) and for both local Gemma 4 GGUF
  files (sliding layers head 256, window 1024, q/kv 2; global layers head 512). A new HIP kernel for head 256
  and large blocks is upstream work of days, not a flag.
- **`TRITON_ATTN`** sets `supports_draft_decode_metadata_update = True` (`triton_attn.py:102`); the default
  `ROCM_ATTN` does not, so with it vLLM logs `Fused multi-step draft decode is not supported ... falling back to
  rebuilding attention metadata between draft steps`. That is the mechanism behind `tattn`'s gain.
- **Hybrid W4A16 linear kernel**: `MAX_SKINNY_BATCH_SIZE = 5` (`rdna_hybrid_w4a16.py:63`) and the HIP op
  `wvSplitK_int4_g` is compiled for N_in 1..5 only (`csrc/rocm/skinny_gemms_int4.cu`, switch with `case 1`..
  `case 5`, default error). With MTP 2 at c=4 the verify step has M = 12 → the Triton path, whose gfx12 tile
  for M ≤ 32 is `16, 16, 128, 4` (tuned on Llama-3.1-8B shapes). Phase C would have timed, per model shape
  ((K, N) = (2048, 12288), (4096, 2048), (2048, 9216), (2048, 1024), (512, 2048)), the HIP op split into chunks
  of ≤ 5 rows against Triton default and Triton with a swept tile — `harness/linear_bench.py`, not run.
- **BF16 draft-layer MoE tuning**: `benchmark_moe.py` forces fp16 activations on ROCm (`resolve_dtype`); the
  local copy got a `MOE_TUNE_ACT_DTYPE=bfloat16` hook so the draft experts could be tuned in BF16 (plan-gate
  finding). Not run.

## 7. Timeline

| When | What | Kept? |
|---|---|---|
| 2026-10-27 15:31 | First start without `genconfig` (temperature 1.0): base 75.9 tok/s | discarded (§ 6.2) |
| 15:43–16:55 | With `genconfig`, under CPU load + Snagit on the R9700: 49.6 / 53 tok/s, 32k TTFT 360 s | discarded (§ 6.3) |
| ~17:25 | Operator restarts the PC | — |
| 17:47–17:56 | Check server; RAM guard stop at 3.2 GB during the bench start | no data |
| 18:00–20:15 | Waiting for coai-exp to end (the wait script first counted coai-exp's `keep-awake.ps1`; fixed) | — |
| 20:15–20:20 | Phase A; RAM guard stop during the model load | no data |
| 20:58–00:27 | Phase A round 1 (all 6 arms) and round 2 `base`, `mtp1` (coai-exp running) | **kept** |
| 2026-10-28 00:27 | Paused by the operator; production restored | — |
| WSL restart | `.wslconfig` change active (dev VM 44 GB, dropCache) | — |
| 10:04–10:08 | Resume; RAM guard stop at 3.7 GB during the model load | no data |
| 10:40–11:53 | Guard at 2 GB; round 2 `mtp3`, `mnbt8k`, `tattn`; `fp8kv` round 2 stopped in its bench | **kept** (partial `fp8kv-2` lines removed) |
| 11:53 | Stopped by the operator; production server restored | — |

## 8. What this does NOT prove

- Only 2 rounds (1 for `fp8kv`), and round 1 ran under another job's CPU load. The ship test's spread comes
  from 6 `base` values.
- The options were tested one at a time. `mtp3` + `tattn` may or may not add up; `mnbt8k` lowers the KV cache
  and may interact with `mtp3`'s larger draft buffers.
- The absolute numbers are below production because of § 6.1; the relative differences are what this measured.
- Requests are random-token prompts with `--ignore-eos`; real chat traffic has different acceptance rates.
- Quality: GSM8K-200 only, once per arm; 20-prompt identity has no `base`-vs-`base` reference.
- The reasoning parser path (chat completions with thinking) was not measured.

## 9. Next steps (not started)

1. Run `harness/bench.sh` against the production container to explain § 6.1.
2. Final A/B on a quiet PC, 3 rounds, GSM8K and prompts in round 1: `base` vs `mtp3 + tattn` vs
   `mtp3 + tattn + mnbt8k`.
3. If a combination passes: add it to `start-vllm.ps1` (`-MtpTokens 3`, `-ExtraArgs "--attention-backend","TRITON_ATTN"`
   and optionally `"--max-num-batched-tokens","8192"`), with a backup of the script first.
4. Phases C, B1, B2 from the plan (`harness/speed_CB.sh`, `harness/tune_chunk.sh`).
5. Upstream (only with the operator's approval of the exact text): a vLLM issue that `TRITON_ATTN` beats the
   ROCm default with MTP on gfx1201 for this model family.

## 10. Files

| Path in [vllm-r9700-speed-options/](vllm-r9700-speed-options/) | What |
|---|---|
| `raw/bench.txt` | Every valid `vllm bench serve` line (throughput, mean TTFT, mean TPOT; prefill median TTFT), tagged `<arm>-<round> c=<n>\|prefill<len> r=<rep>` |
| `raw/spec_metrics.txt` | `vllm:spec_decode_*` counters per arm-round |
| `raw/cpuload_round1.txt` | WSL load average every 30 s, 2026-10-27 16:05–17:25 (the discarded run of § 6.3; the name is historical) |
| `server_logs/` | Full server log per arm-round (`server_fp8kv-2_PARTIAL_stopped.log`: start only) |
| `quality/gsm8k_<arm>.json`, `quality/prompts_<arm>.json` | GSM8K-200 answers and the 20 greedy prompts with token ids and logprobs (round 1) |
| `harness/` | The scripts as run: `speed_A.sh` (series), `serve.sh`, `bench.sh`, `harness.py` (prompts, GSM8K, compare), `disk_gate.sh`, `ram_guard_all.sh`, `restore_server.sh`, `wait_quiet.sh`, `resume_AC.sh`; not run: `speed_CB.sh`, `tune_chunk.sh`, `linear_bench.py` |
| `discarded/` | The temperature-1.0 bench lines and the slow server log of § 6.3 |

Reproduce a comparison: `python3 -I harness/harness.py compare quality/prompts_base.json quality/prompts_mtp3.json`
and `python3 -I harness/harness.py gsm8k-compare quality/gsm8k_base.json quality/gsm8k_mtp3.json`.
