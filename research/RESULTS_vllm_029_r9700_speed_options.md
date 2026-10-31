# vLLM 0.29 on the R9700 — seven more speed options, measured (2026-10-27/28)

> Status: **partial result.** Phase A (server flags) ran 2 of 3 rounds for five options and 1 round for FP8 KV
> (stopped by the operator on 2026-10-28 11:53). A night run on 2026-10-28 (§ 11, paused by the operator at
> 23:35) added the production stall check, a phase C run without a verdict, 3 of 5 BF16 draft tuning chunks
> (not yet tested) and round 1 of the final A/B for `base` only. A second night run on 2026-10-30 (§ 12) gave
> phase C a valid timer (option 7: tuned Triton tiles −13–14 % at 6, 8 and 16 rows) and finished the BF16 tuning
> chunks; the final A/B did not resume (another job ran all night). Phase B2 (int4 keys 24–128) did not run.
> Follow-up to
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
| 7 | Hybrid linear kernel at 6–16 rows | **kernel gain, no end-to-end test** (§ 12.1, CUDA-graph timer) | tuned Triton tiles −13 % / −14 % / −14 % of linear time at M 6 / 8 / 16, −1 % at M 12; the split HIP op is slower at every M ≥ 6 |
| 3 | Tuned BF16 MoE file for the MTP draft layer | **not measured** (all 5 tuning chunks done, § 11.4 and § 12.2; no merge, no A/B) | the draft layer still uses the default MoE config |
| 4 | int4 MoE keys 24, 32, 64, 128 | **not measured** (phase B2 did not run) | — |

Not tested yet: the combination (`mtp3` + `tattn`, with and without `mnbt8k`); its A/B started on 2026-10-28
and was paused after the first `base` round (§ 11.2). Nothing was changed in the operator's `start-vllm.ps1`;
the production server runs as before (MTP 2, `ROCM_ATTN`, default batching).

Two environment findings matter more than any single flag (§ 6): **one request per benchmark run waits ~12 s
longer** (mean TTFT ~2.1 s where production had 0.2 s on 2026-10-27). On 2026-10-28 the production server showed
the same stall, always on the first measured request, which repeats the prompt of the bench's own warm-up
request — probably a slow prefix-cache hit (§ 6.1, a hypothesis; decode speed is equal). And **every vLLM model
load drives Windows' "Modified" page list to 30–42 GB**, which stopped four runs through the RAM guard.

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
| "Spread" (as computed here) | the **range** (max − min) of `base`'s values for that metric over all its valid reps — 6 values (2 rounds × 3 reps). Output tok/s: c=1 75.46 … 78.81 → 3.35 tok/s; c=4 178.25 … 183.66 → 5.41 tok/s. 32k median TTFT: 15.77 … 16.13 s → 0.35 s. An arm's difference is its 6-value median minus `base`'s 6-value median (c=1 76.53, c=4 180.54, 32k 16.03 s) |
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
| `mtp3` | c=1 81.84 − 76.53 = +5.31 > 3.35 ✓; c=4 186.16 − 180.54 = +5.62 > 5.41 ✓ (just); GSM8K ✓ | **Keep candidate.** Gains on both loads, TPOT −8 %, acceptance 0.90 / 0.83 / 0.74 |
| `tattn` | c=1 79.20 − 76.53 = +2.67 < 3.35 ✗; c=4 190.23 − 180.54 = +9.69 > 5.41 ✓; c=1 not worse ✓; GSM8K ✓ | **Keep candidate.** The log confirms `Using TRITON_ATTN backend (selected via --attention-backend)` and the absence of `Fused multi-step draft decode is not supported` — the fused draft steps are on |
| `mnbt8k` | decode not better ✗ (rule 1); c=4 174.05 − 180.54 = −6.49, worse by more than 5.41 ✗ (rule 2); 32k TTFT 12.15 − 16.03 = −3.88 s, more than 0.35 s ✓ | **Operator's choice.** Long prompts 24–30 % faster for ~4 % decode and the smallest KV cache. Worth it for code / document prompts |
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

### 6.1 One request per benchmark run stalls ~12 s — production too since 2026-10-28; probably a prefix-cache hit

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
`PYTHONPYCACHEPREFIX=/tmp/pyc-fresh`.

**Production check, 2026-10-28 21:03–21:19** (`harness/prodcheck.sh`, against the operator's running `vllm`
container, up 9 h, no restart; WSL load 0.4 at the start; data `night-2026-10-28/raw/prodcheck.txt`). Production
now stalls the same way, so the test containers and their three different flags are **not** the cause:

| Production, 2026-10-28 | Result |
|---|---|
| c=1, 1024/512, 3 runs | 57.9 / 78.7 / 75.8 tok/s; mean TTFT 4.33 / 2.16 / 2.18 s; mean TPOT 8.83 / 8.51 / 8.94 ms |
| c=4, 1024/512, 3 runs | 178.2 / 172.3 / 165.1 tok/s; mean TTFT 4.42 / 4.57 / 5.13 s; mean TPOT 13.48 / 13.82 / 13.93 ms |
| prefill 8192/128, TTFT of the 3 requests in send order | [13.52, 1.94, 1.93] · [14.63, 1.99, 2.01] · [15.39, 2.07, 1.98] s |
| prefill 32768/128, same | [27.67, 16.20, 16.46] · [30.08, 16.82, 16.93] · [31.52, 16.31, 16.72] s |

The per-request TTFTs (`--save-detailed`) show the stalled request: **in all 6 prefill runs it is the first
one**, ~11–15 s slower than the other two. `vllm bench serve` first sends a warm-up request ("Starting initial
single prompt test run", `vllm/benchmarks/serve.py:831-836`) with `input_requests[0]` — the same prompt the
measured run then sends first. So the stalled request is the one whose whole prompt is already in the prefix
cache, and the server log of `fbase-1` shows non-zero prefix-cache hit rates (2–4 %) during the prefill runs.
**Hypothesis, not proven:** a full prefix-cache hit on this hybrid model (attention + linear-attention layers,
MTP) takes a slow path of ~12 s. It does not explain why production showed 0.2 s on 2026-10-27 12:17 with the
same bench; that run may have had different prompts or cache state. Decode is not affected (TPOT equal).
**Next step:** send one 8k prompt twice by hand and time both; then one A/B on the same server shape with and
without `--no-enable-prefix-caching`. Until then, compare arms only with each other and prefer TPOT and median
TTFT; mean TTFT and every tok/s value carry the stall.

### 6.2 Sampling defaults probably change MTP speed — not isolated

The first try (2026-10-27 15:31) started test servers without `--generation-config`, so the model's own
defaults applied (temperature 1.0, top_p 0.95). `base` gave 75.9 tok/s at c=1 (mean TTFT 0.31 s, TPOT 12.59 ms)
against 101.5 in production with temperature 0.3 (TPOT 8.9–9.5 ms). The TPOT gap of ~30 % is a decode effect
and fits lower MTP acceptance at temperature 1.0, but this is a **hypothesis**: it compares a test server with
production on different hours, and SnagitEditor was very likely already using the R9700 then (§ 6.3). A
same-environment A/B that changes only `--generation-config` was not run. The rule that follows either way:
an MTP benchmark must use the production sampling defaults, because `vllm bench serve` sends no temperature
and the server's defaults decide. Data: `discarded/temp1.0_bench.txt`.

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
daytime went through. On 2026-10-28 22:26, with another job (coai-exp2, ~30 processes, WSL load ~18) running,
the guard stopped the first final-A/B start at **190 MB** available (30 GB at the start of the load). The
production restore right after it went down to **6 MB** available at 22:29:17 and came back to 30 GB at 22:29:58,
when the load ended; Windows survived. So the dip is short (~1.5 min) but deep, and other memory users make it
cross the guard.

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
| 21:03–21:19 | Production stall check (§ 6.1) | **kept** |
| 21:19–22:19 | Waiting for a quiet PC (coai-exp2 running); not quiet after 60 min | — |
| 22:20–22:26 | Final A/B `fbase-1`: RAM guard stop at 190 MB during the model load (§ 6.5) | no data |
| 22:32–22:43 | Phase C, under coai-exp2 load (§ 11.3) | kept, no verdict |
| 22:43–23:00 | Phase B1 chunks coarse, bs 1, bs 2 (§ 11.4) | **kept** |
| 23:10–23:34 | coai-exp2 gone, PC quiet; final A/B `fbase-1` (§ 11.2) | **kept** |
| 23:34–23:35 | `fcombo-1` start; paused by the operator; production restored | no data |

## 8. What this does NOT prove

- Only 2 rounds (1 for `fp8kv`), and round 1 ran under another job's CPU load. The ship test's spread comes
  from 6 `base` values.
- The options were tested one at a time. `mtp3` + `tattn` may or may not add up; `mnbt8k` lowers the KV cache
  and may interact with `mtp3`'s larger draft buffers.
- The absolute numbers are below production because of § 6.1; the relative differences are what this measured.
- Requests are random-token prompts with `--ignore-eos`; real chat traffic has different acceptance rates.
- Quality: GSM8K-200 only, once per arm; 20-prompt identity has no `base`-vs-`base` reference.
- The reasoning parser path (chat completions with thinking) was not measured.

## 9. Next steps

1. § 6.1: test the prefix-cache hypothesis (`harness/prefixtest.py`: the same 8k prompt twice, then a changed
   tail; it runs in each final-A/B arm with `PREFIXTEST=1`; then an A/B with `--no-enable-prefix-caching`). The
   production check itself is done.
2. Resume the final A/B on a quiet PC (`harness/night3.sh` resumes it; `fbase-1` is done; the 5 remaining
   arm-rounds take ~2 h): `fbase` vs `fcombo` (`mtp3 + tattn`), 3 rounds. `mtp3 + tattn + mnbt8k` was not in
   the night runs.
3. If the combination passes: start the production server with `-MtpTokens 3 -ExtraArgs
   --attention-backend=TRITON_ATTN` (a `pwsh -File` call binds that as one string, checked 2026-10-28). Do not
   change the defaults in `start-vllm.ps1`: they are for Qwen3.8-27B, not this model.
4. Option 7 (§ 12.1): a local patch of the Triton tile choice for M 6–16, then an end-to-end A/B — after the
   final A/B, because the gain depends on M (MTP 3 at 4 requests gives M 16, MTP 2 gives M 12). Upstream idea:
   the HIP op ends at 5 rows, and the default jumps +48 % from M 5 to M 6.
5. Phase B1 (§ 12.2): merge the five chunks into one file, then a kernel A/B against the default config and an
   end-to-end A/B. Phase B2: the int4 chunks took 1–3.6 h each on 2026-10-26, so keys 24–128 need more than one
   night.
6. Upstream (only with the operator's approval of the exact text): a vLLM issue that `TRITON_ATTN` beats the
   ROCm default with MTP on gfx1201 for this model family; possibly the prefix-cache stall, if confirmed.

## 10. Files

| Path in [vllm-r9700-speed-options/](vllm-r9700-speed-options/) | What |
|---|---|
| `raw/bench.txt` | Every valid `vllm bench serve` line (throughput, mean TTFT, mean TPOT; prefill median TTFT), tagged `<arm>-<round> c=<n>\|prefill<len> r=<rep>` |
| `raw/spec_metrics.txt` | `vllm:spec_decode_*` counters per arm-round |
| `raw/cpuload_round1.txt` | WSL load average every 30 s, 2026-10-27 16:05–17:25 — **the discarded run of § 6.3, not the kept round 1** (the name is historical). No load log exists for the kept round 1 (20:58–00:27); the ~11 at its start in § 6.4 is a single reading from the session record |
| `server_logs/` | Full server log per arm-round (`server_fp8kv-2_PARTIAL_stopped.log`: start only) |
| `quality/gsm8k_<arm>.json`, `quality/prompts_<arm>.json` | GSM8K-200 answers and the 20 greedy prompts with token ids and logprobs (round 1) |
| `harness/` | The scripts as run: `speed_A.sh` (series; final-A/B arms `fbase`, `fcombo` added for the night), `serve.sh`, `bench.sh`, `harness.py` (prompts, GSM8K, compare), `disk_gate.sh`, `ram_guard_all.sh`, `restore_server.sh` (reads an optional `restore_args` file), `wait_quiet.sh`, `resume_AC.sh`; night run: `night.sh`, `night2.sh` (drivers with RAM and deadline guards), `prodcheck.sh`, `speed_CB.sh` (with `DEADLINE` and `ONLY=<chunk>`), `tune_chunk.sh`, `linear_bench.py` |
| `discarded/` | The temperature-1.0 bench lines and the slow server log of § 6.3 |
| `night-2026-10-28/raw/` | `prodcheck.txt` (§ 6.1), `bench_final_ab.txt` and `spec_metrics_final_ab.txt` (`fbase-1`), `driver_log.txt` (both drivers), `wait_quiet.txt`, `ram_guard.txt` |
| `night-2026-10-28/server_logs/` | `server_fbase-1.log` |
| `night-2026-10-28/quality/` | `gsm8k_fbase.json`, `prompts_fbase.json` |
| `night-2026-10-28/phaseC/` | `linear_bench.json` (50 rows), `linear_bench.log` |
| `night-2026-10-28/phaseB1/` | The tuned BF16 draft configs per chunk (`<chunk>.json`, keys 1, 2, 256, 1024, 2048) and the tuning logs without progress bars |
| `night-2026-10-30/phaseC2/` | `linear_bench_graph.json` (50 rows, CUDA-graph timer) and `.log` |
| `night-2026-10-30/phaseB1/` | `draft-bs-4.json`, `draft-bs-12.json` and their tuning logs without progress bars |
| `night-2026-10-30/raw/` | `driver_log.txt` (`night3.sh`), `ram_guard.txt` |
| `harness/` (night 2026-10-30) | `night3.sh` (driver), `linear_bench_graph.py` (phase C2 timer over `linear_bench.py`), `prefixtest.py` (§ 6.1 check), `speed_A.sh` with the `PREFIXTEST` hook |

Reproduce a comparison: `python3 -I harness/harness.py compare quality/prompts_base.json quality/prompts_mtp3.json`
and `python3 -I harness/harness.py gsm8k-compare quality/gsm8k_base.json quality/gsm8k_mtp3.json`.

## 11. Night run of 2026-10-28 (partial, paused by the operator at 23:35)

The operator asked to run the open steps overnight. Drivers: `harness/night.sh` (21:03), then
`harness/night2.sh` (22:31) after the RAM guard stop. Guards: RAM 2 GB; a deadline at 07:15 that stops any test
(so the production server is back by ~07:30); the production server is restored once, at the driver's exit.
Model loads waited for a quiet PC (operator decision of 2026-10-27); runs that load no model (phase C, tuning)
went on meanwhile.

### 11.1 Production stall check

See § 6.1: the production server stalls too, always on the first measured request.

### 11.2 Final A/B — round 1 of `fbase` only

`fbase` is `base` under a new name, so phase A's round-1 quality files are kept. Started 23:10 on a quiet PC
(WSL load 0.2, coai-exp2 gone). `fcombo` (`mtp3 + tattn`) was starting when the operator paused the run.

| | `fbase-1` (2026-10-28) | phase A `base` (2 rounds, § 3) |
|---|---|---|
| c=1 tok/s, median of 3 | 76.08 (74.29 / 78.72 / 76.08) | 76.53 |
| c=4 tok/s, median of 3 | 178.39 (178.39 / 181.10 / 177.96) | 180.54 |
| c=1 mean TPOT, median of 3 | 8.85 ms | 8.91 ms |
| prefill 8k, median TTFT (median of 3 runs) | 1.93 s | 1.93 s |
| prefill 32k, median TTFT (median of 3 runs) | 16.16 s | 16.03 s |
| GSM8K-200 | 85.5 % (171/200, stderr 2.5) | 84.5 % |
| MTP acceptance, positions 0 / 1 | 0.92 / 0.85 (9911 and 9232 of 10814 drafts) | 0.92 / 0.86 |
| GPU KV cache | 234,958 tokens | 234,958 tokens |

`base` repeats across days within the spread (c=1 −0.6 %, c=4 −1.2 %; spread 3.35 / 5.41 tok/s). No verdict on
the combination yet.

### 11.3 Phase C (option 7) — ran, no verdict (timed again in § 12.1)

`harness/linear_bench.py`: the five quantized linear shapes × M = 1–6, 8, 10, 12, 16; per point the default
dispatch, Triton with its default tile, the HIP op split into ≤ 5-row chunks, and (at M 6, 8, 12, 16) Triton with
the best of 144 swept tiles. It ran 22:32–22:43 while coai-exp2 still loaded the CPU (WSL load ~16 at 22:28).

**Why no verdict:** `triton.testing.do_bench` timed eager calls, so each value includes host launch and Python
wrapper work. Summed over the layers that use each shape, the default path needs 23.8 ms per forward at M = 4
and 25.5 ms at M = 6 — but a whole decode step (all layers, MoE, attention) takes 8.5–8.9 ms under CUDA graphs.
At M = 1 one call took 42–165 µs; the largest weight (2048 × 12288 int4, 12.6 MB) needs ~20 µs at 640 GB/s.

What the run still shows (the same overhead is in every column):

| M | default (ms per forward, 5 shapes × layers) | split HIP | Triton, best swept tile |
|---|---|---|---|
| 6 | 25.45 | 38.06 | 13.38 |
| 8 | 27.99 | 31.18 | 13.35 |
| 12 | 20.85 | 34.25 | 13.78 |
| 16 | 21.69 | 45.04 | 11.04 |

- The split HIP op is slower than the default at every M ≥ 6, as predicted (more launches, weights read again).
- The swept tiles look 1.5–2.5× faster, but that call launches the Triton kernel directly and skips the wrapper,
  so part or all of the gap can be wrapper work. Not a kernel gain until timed under CUDA graphs.
- Correctness: split max abs difference ≤ 0.0078 (BF16 rounding); swept tiles 0.0 against the default.
- **Next:** time with a captured CUDA graph (many replays) or the profiler's kernel time, on a quiet PC.

### 11.4 Phase B1 (option 3) — 3 of 5 tuning chunks, not yet tested

`benchmark_moe.py --dtype auto` with `MOE_TUNE_ACT_DTYPE=bfloat16` (set by `tune_chunk.sh`; the tuning log does
not print it). The output file name, `E=256,N=512,device_name=AMD_Radeon_R9700.json`, is the exact name the
server looks for and does not find today (`Using default MoE config ... Config file not found at
/work/moe/tune/merged/E=256,N=512,device_name=AMD_Radeon_R9700.json`). Ran 22:43–23:00 under coai-exp2 load.

| Chunk | Keys | Search | Time | Chosen config (`BLOCK_M/N/K`, warps, stages, waves_per_eu) |
|---|---|---|---|---|
| `draft-coarse-largeM` | 256, 1024, 2048 | 82 coarse candidates | 374 s | 32/32/128, 4, 2, 0 · 64/64/128, 8, 2, 0 · 128/64/64, 8, 2, 0 |
| `draft-bs-1` | 1 | full space, pruned | 298 s | 16/16/256, 1, 2, 4 |
| `draft-bs-2` | 2 | full space, pruned (the progress bar shows 352 configs) | 309 s | 16/16/256, 4, 2, 1 |

The BF16 chunks took ~5 min where the int4 chunks of 2026-10-26 took 1–3.6 h (int4 bs 2: 3489 s), because the
pruned BF16 space is much smaller. The `draft-bs-2` log ends with `ImportError: sys.meta_path is None, Python is
likely shutting down` after `Completed tuning` and the saved JSON — a shutdown message, the chunk is complete.
**Not done:** bs 4 and 12, the merge, a kernel A/B against the default config, an end-to-end A/B.

### 11.5 State after the pause

Nothing was changed in `start-vllm.ps1`; the production server was restored at 23:35 in the same shape as
before (no extra arguments). The resume markers (`speedF/done_fbase-1`) live in the WSL temp folder and are lost
on a WSL restart; all data is in `night-2026-10-28/`.

## 12. Night run of 2026-10-30 (resumed by the operator at 00:10)

Driver: `harness/night3.sh`, 00:12–05:02, the same guards as § 11 (RAM 2 GB, deadline 07:15, one restore at the
exit). Model loads waited until no coai-exp process ran and `wait_quiet.sh` confirmed a quiet PC; runs that load
no model went on meanwhile. Before the start, the production server had stopped by itself (exit code 255 at
00:10:31, empty log; cause unknown).

### 12.1 Phase C2 (option 7) — CUDA-graph timer

`harness/linear_bench_graph.py` runs `linear_bench.py` (same shapes, M values, tile sweep and checks as § 11.3)
with one change: each call is captured 20 times into one CUDA graph, and the graph replay is timed with CUDA
events (median of 50 replays). 00:12–00:18, while coai-exp2 ran (graph replay is less sensitive to CPU load than
eager calls). Random weights.

Sum over the five shapes × their layers, ms per forward:

| M | default dispatch | Triton, default tile | split HIP (≤ 5-row chunks) | Triton, best of 144 tiles |
|---|---|---|---|---|
| 1 | 2.48 (HIP) | 4.97 | 2.79 | — |
| 4 | 2.95 (HIP) | 4.73 | 3.27 | — |
| 5 | 3.31 (HIP) | 4.45 | 3.63 | — |
| 6 | 4.91 (Triton) | 4.79 | 5.89 | **4.28 (−13 %)** |
| 8 | 5.16 | 5.20 | 6.28 | **4.45 (−14 %)** |
| 10 | 5.14 | 5.05 | 6.87 | — |
| 12 | 4.42 | 4.46 | 8.94 | 4.39 (−1 %) |
| 16 | 6.93 | 6.69 | 11.67 | **5.95 (−14 %)** |

- The timer is now plausible: at M = 1 the linear layers take 2.5 ms of an 8.5–8.9 ms decode step.
- **The split HIP op fails:** slower than the default at every M ≥ 6, by up to 68 % at M 16.
- **Tuned tiles help at M 6, 8 and 16** (0.6–1.0 ms per forward), not at M 12. Today's production (MTP 2, 4
  requests) runs at M 12, so it gains nothing; `mtp3` at 4 requests runs at M 16 and could gain ~1 ms per step.
  The chosen tiles: `BLOCK 16/16/32, 1 warp` for the two QKV input projections, `16/16/128, 4 warps` for
  `out_proj` / `o_proj` and the shared-expert `gate_up`; the shared-expert `down_proj` varies with M.
- At M 1–5 the HIP op is ~2× faster than Triton; the default jumps +48 % from M 5 (HIP) to M 6 (Triton). A HIP
  kernel that also takes 6–8 rows could remove most of that step — upstream kernel work, not a flag.
- Correctness: tuned tiles 0.0 max abs difference against the default; split ≤ 0.0078 (BF16 rounding).
- **Not shown:** an end-to-end gain. The next step is a local patch of the tile choice and an end-to-end A/B.

### 12.2 Phase B1 (option 3) — tuning chunks complete

| Chunk | Key | Configs searched (pruned) | Time | Chosen config (`BLOCK_M/N/K`, warps, stages, waves_per_eu) |
|---|---|---|---|---|
| `draft-bs-4` | 4 | 1000 | 1554 s | 16/16/256, 4, 2, 1 |
| `draft-bs-12` | 12 | 1580 | 2511 s | 16/16/256, 2, 2, 2 |

Both ran 00:19–01:27 under coai-exp2 load; both logs end with the same harmless shutdown `ImportError` as
`draft-bs-2` (§ 11.4). With § 11.4, all five chunks are done (keys 1, 2, 4, 12, 256, 1024, 2048). **Not done:** the
merge into one `E=256,N=512,device_name=AMD_Radeon_R9700.json`, the kernel A/B, the end-to-end A/B.

### 12.3 Final A/B — did not resume

coai-exp2 kept one process alive the whole night (`run-arm.sh CHH1`, waiting on a usage limit in 15-min sleeps),
so the gate "no coai-exp process" never opened. At 05:02 the remaining 5 arm-rounds (~2 h) no longer fitted
before the deadline, and the driver ended. No prefix-cache check ran (it is part of each A/B arm).

### 12.4 State after the run

The production server was restored at 05:02 with no extra arguments (MTP 2, `ROCM_ATTN`). Nothing was changed
in `start-vllm.ps1`.
