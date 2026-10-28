# vLLM 0.29 on the AMD R9700: fixes, tuning, and how to apply them

> Status: **record of work done 2026-10-25 to 2026-10-27; the fixes run in the operator's local vLLM
> setup.** Scope: the `rocm/vllm` image with vLLM 0.29, the Radeon AI PRO R9700 (gfx1201), and the
> Qwen3.6-35B-A3B GPTQ Int4 checkpoint. Copies of this file:
> `vllm_pr_work · research/RESULTS_vllm_029_r9700_fixes.md` (next to the evidence) and
> `dew_flow_benchmark · research/RESULTS_vllm_029_r9700_fixes.md`. The two copies are the same text.

## 1. Summary

The model started on this GPU only with two workarounds: a slow linear kernel and no MTP
(speculative decoding). We found the cause of each problem. For two problems, other people already
had open upstream pull requests; we tested them on this GPU. For the third problem (MoE kernel speed),
we tuned a config file ourselves. All three now run in the operator's setup as local patches.

| # | Problem | Fix | Upstream state (2026-10-27) | Local state |
|---|---|---|---|---|
| 1 | The fast `RDNAHybridW4A16LinearKernel` crashes on every symmetric GPTQ checkpoint | vLLM PR #52637 (author `tangzzycc`), 4-line loader part | open, not merged. Our issue #63597. Our test-only PR #63825 | backport active |
| 2 | MTP cannot load: the MTP layer of this checkpoint is stored unquantized (BF16) | vLLM PR #49539 (author `pjdurden`) | open, not merged. Our issue #63598 | backport active |
| 3 | No tuned fused-MoE config for this GPU and shape, so vLLM uses a default rule | our tuned file `E=256,N=512,device_name=AMD_Radeon_R9700,dtype=int4_w4a16.json` | not proposed yet (needs the operator's approval) | file active |

Speed of the operator's server (`vllm bench serve`, random 1,024 tokens in / 512 out, median of 3):

| Setup | 1 request, tok/s | 4 requests, tok/s | Date |
|---|---|---|---|
| Old: Triton linear kernel, no MTP (the workarounds) | 37.8 | 96.2 | 2026-10-26 |
| Fix 1 only: hybrid kernel, no MTP | 60.1 | 143.4 | 2026-10-26 |
| Fixes 1 + 2: hybrid kernel + MTP, 131,072 context | 96.8 | 236.1 | 2026-10-26 |
| Fixes 1 + 2 + 3: the same + tuned MoE file | **101.5** | **256.0** | 2026-10-27 |

The rows are on different settings or days. Compare neighbouring rows only. The clean A/B proofs are
in section 5.

Quality for fixes 1 and 3 did not change in a way we could measure: GSM8K (200 samples, greedy) is
87.0% with the Triton kernel, 86.5% with the hybrid kernel (same day), and 88.5% before and after the MoE
file (same day). **Fix 2 (MTP) was not measured on GSM8K.** With MTP, 15 of 20 greedy outputs are
identical to no-MTP, below our pre-registered limit of 17/20 (section 5.3); task accuracy with MTP is
not verified.

## 2. Environment

| Item | Value |
|---|---|
| GPU | AMD Radeon AI PRO R9700, 32 GB, `gfx1201` (RDNA4), in an eGPU dock |
| Other GPU | Radeon 890M iGPU. It is visible in containers. Set `HIP_VISIBLE_DEVICES=0` |
| Host | Windows 11 + WSL2. Containers run with `wslc` (`C:\Program Files\WSL\wslc.exe`), not Docker |
| Image | `rocm/vllm:rocm10.1.0_ubuntu24.04_py3.14-pytorch_2.13.0_vllm-0.29.0` |
| vLLM in the image | `0.29.1.dev0+g98dff2a81` |
| Triton in the image | 3.8.0 |
| Checkpoint | `llmfan46/Qwen3.6-35B-A3B-uncensored-heretic-Native-MTP-Preserved-GPTQ-Int4`: GPTQ 4-bit, group 128, `sym: true`, `desc_act: false`. The MTP layer is BF16 |
| Path in the container | `/root/.cache/huggingface/local/Qwen3.6-35B-A3B-GPTQ` (volume `hf-cache`) |
| Start script | `C:\Users\strug\vllm\start-vllm.ps1` |
| User guide | `C:\Users\strug\vllm\VLLM-PARALLEL.md` |

## 3. The fixes in detail

### 3.1 Fix 1: symmetric GPTQ on the RDNA hybrid kernel (PR #52637)

**Symptom.** On gfx1201, vLLM selects `RDNAHybridW4A16LinearKernel` for GPTQ. Every symmetric GPTQ
checkpoint then fails at engine start:

```text
Using RDNAHybridW4A16LinearKernel for AutoGPTQLinearMethod
  File ".../rdna_hybrid_w4a16.py", line 228, in triton_w4a16_skinny_fmt_gemm
    assert zp.shape == (N, num_groups), (
AssertionError: zp shape mismatch: torch.Size([16, 1536]) vs (12288, 16)
```

The old workaround was `VLLM_DISABLED_KERNELS=RDNAHybridW4A16LinearKernel` (it selects the slower
`TritonW4A16LinearKernel`). Upstream closed an earlier report (#51971) with a workaround and no code
change.

**Cause.** The GPTQ loader always registers a `qzeros` tensor. For a symmetric checkpoint the kernel
config says "no zero points", but the stale `qzeros` still reaches the hybrid kernel. On the image the
kernel then fails a shape check. On vLLM `main`, the zero-point layout is different: when K/G equals
N/8, the stale tensor passes the shape check and the kernel silently uses zero point 7 instead of 8
(we found this by reading the code, not by a run on `main`).

**Fix.** PR #52637 deletes the unused zero-point tensor before the kernel processes the weights. We
ported only this loader part (the PR also changes repacking, which we did not port). The backport, in
`AutoGPTQLinearMethod.process_weights_after_loading` of `auto_gptq.py`:

```python
        # Backport of vllm-project/vllm#52637 (head 3e550f77be), loader fix only.
        if not self.kernel.config.zero_points:
            zp_name = self.kernel.w_zp_name
            if zp_name is not None and hasattr(layer, zp_name):
                delattr(layer, zp_name)
        self.kernel.process_weights_after_loading(layer)
```

The PR head moved after we ported it (head on 2026-10-27: `1e3e92a81`). Our backport is from
`3e550f77be`. A second, draft PR (#61891, author `xiaotia1`) fixes the same bug.

**Our upstream work.**

- Issue #63597 (opened by us): the bug report.
- PR #63825 (ours, commit `1d3d61e14`): **tests only**, no fix. A layer test through the real
  `AutoGPTQLinearMethod` path (shapes (256,128,128) and (2048,128,128); M = 1 for the HIP skinny path
  and M = 64 for the Triton path) and an end-to-end test with `JunHowie/Qwen3-0.6B-GPTQ-Int4` (hybrid vs
  Triton, logprobs compared, kernel asserted). Result on the R9700: 5/5 fail without the fix,
  5/5 pass with it. It depends on #52637 and needs a maintainer label (`pre-run-check`) and a review.
- Evidence comments on #52637 and #63597 (2026-10-26).

### 3.2 Fix 2: MTP with unquantized MTP experts (PR #49539)

**Symptom.** With `--speculative-config.method mtp`:

```text
AttributeError: Layer mtp.layers.0.mlp.experts has no parameter 'w2_weight'
for checkpoint weight 'mtp.layers.0.mlp.experts.down_proj'
```

**Cause.** The MTP drafter is built with the target model's GPTQ config. The GPTQ MoE code gives every
routed-expert layer a quantized method, so it creates `w2_qweight`. This checkpoint stores the MTP
experts unquantized (BF16 fused 3D tensors `mtp.layers.0.mlp.experts.gate_up_proj [256, 1024, 2048]`
and `down_proj [256, 2048, 512]`), so the checkpoint has `w2_weight`. The only existing exception is a
`dynamic` key in the quantization config; this checkpoint has none.

**Fix.** PR #49539 (head `57f39e4f`) adds `is_moe_layer_gptq_quantized(prefix, quantized_layers)` in
`gptq_utils.py`. `auto_gptq.py` uses it: when the checkpoint does not quantize the experts of a layer,
that layer gets `UnquantizedFusedMoEMethod`. We ported the PR to the image files without other changes.

**Check before the run.** Inside the container we proved that the mounted backport is the code that
loads, and we ran the PR's matcher on the real checkpoint: 30,970 header modules, 0 `mtp.*` entries in
the quantized list, main experts quantized 40/40, MTP experts not quantized.

**Result.** MTP loads and runs. Draft acceptance with 2 draft tokens: 79.5% (903/1136); position 0
86.8%, position 1 72.2%. Speed with the Triton kernel: 37.8 → 45.0 tok/s at 1 request.

**Our upstream work.** Issue #63598 (opened by us) and an evidence comment on #49539 (2026-10-26).

### 3.3 Fix 3: tuned fused-MoE config for the R9700 (ours)

**Symptom.** The server log says `Using default MoE config. Performance might be sub-optimal!`.
vLLM has no Triton config file for E=256 experts, N=512, `int4_w4a16`, on `AMD_Radeon_R9700`.

**How vLLM uses the file.** `get_moe_configs` (in `fused_moe.py`) looks first in
`VLLM_TUNED_CONFIG_FOLDER`, then in the image's own `configs` folder. The file maps batch sizes to
Triton tile sizes. For a batch size between two keys, vLLM uses the nearest key. A model with another
shape (other E, N or dtype) looks for another file name, so the file has no effect on other models.

**The file** (9 keys: 1, 2, 4, 8, 12, 16, 256, 1024, 2048; `triton_version` 3.8.0). All small batch
sizes use `BLOCK_SIZE_M 16, BLOCK_SIZE_N 16, BLOCK_SIZE_K 64, num_warps 1, num_stages 2`; only
`waves_per_eu` (2 or 4) and `GROUP_SIZE_M` (1 or 4) change. Large batch sizes use bigger tiles:
1024 → M64/N64/K32, 8 warps; 2048 → M128/N64/K32, 8 warps.

**How we tuned it.** vLLM's `benchmarks/kernels/benchmark_moe.py --tune` (from upstream `main`
`2c880f1e6`), in the image container, `--tp-size 1 --dtype int4_w4a16`, in chunks (one batch size per
chunk, so a stop loses only one chunk):

| Chunk | Batch sizes | Configs tried | Time |
|---|---|---|---|
| pilot | 1 | 304 | ~28 min |
| coarse, large M | 256, 1024, 2048 | 82 hand-picked candidates | 24 min |
| full | 2 / 4 / 8 / 12 / 16 | the full pruned space each | 58 / 83 / 142 / 140 / 214 min |

About 11.5 hours in total, GPU at full load. We made two local changes to our copy of
`benchmark_moe.py`:

1. **Ray 2.59 and ROCm.** The script renames `HIP_VISIBLE_DEVICES` to `ROCR_VISIBLE_DEVICES`. Ray 2.59
   then stops with "Please use HIP_VISIBLE_DEVICES instead of ROCR_VISIBLE_DEVICES". Without a
   visible-devices variable, Ray sees 2 GPUs (the iGPU too). We removed the rename and set
   `HIP_VISIBLE_DEVICES=0`, and we checked that the worker runs on the R9700. This is an upstream
   incompatibility; it is not reported yet.
2. **Candidate list.** A hook reads `MOE_TUNE_CANDIDATES` (a JSON list) and replaces the search space.
   We used it only for the large-M chunk (82 candidates: BLOCK_SIZE_M/N/K in {32, 64, 128}, warps in
   {2, 4, 8}, plus the batch-size-1 winner), because the full space at large M takes many hours.

The chunk files were merged into one file with a script that fails on a missing chunk, a duplicate
key, a different `triton_version` or a missing field.

## 4. How to apply (the operator's setup)

### 4.1 Where the files are

| File | Mounted at (read-only) | Fix |
|---|---|---|
| `C:\Users\strug\vllm\patches\auto_gptq.py` | `/opt/python/lib/python3.14/site-packages/vllm/model_executor/layers/quantization/auto_gptq.py` | 1 and 2 |
| `C:\Users\strug\vllm\patches\gptq_utils.py` | `.../quantization/utils/gptq_utils.py` | 2 |
| `C:\Users\strug\vllm\patches\moe\E=256,N=512,device_name=AMD_Radeon_R9700,dtype=int4_w4a16.json` | folder mounted at `/vllm-tuned-moe` | 3 |
| `C:\Users\strug\vllm\patches\IMAGE.txt` | — | the image tag the patches are for |
| `C:\Users\strug\vllm\patches\README.md` | — | what each file is and when to remove it |

The two `.py` files are the image's own files plus the backports, nothing else (sha256 of
`auto_gptq.py` `420703fd…651a`, `gptq_utils.py` `82d51028…9902`). **`auto_gptq.py` carries fixes 1 and 2
together**; section 4.4 says how to keep only one of them.

**To set up the patches on a new machine** (same image), copy the files from the evidence in
`vllm_pr_work` (branch `docs/pr-plans`):

```bash
# in WSL; P is the operator's vllm folder as seen from WSL
E=~/git/vllm_pr_work-wt/docs-pr-plans/research
P=/mnt/c/Users/strug/vllm
mkdir -p "$P/patches/moe"
cp "$E/2026-10-26_r9700_vllm_fixes/backports/C/auto_gptq.py" "$E/2026-10-26_r9700_vllm_fixes/backports/C/gptq_utils.py" "$P/patches/"
cp "$E/2026-10-27_r9700_moe_tuning/config/E=256,N=512,device_name=AMD_Radeon_R9700,dtype=int4_w4a16.json" "$P/patches/moe/"
cp "$E/2026-10-27_r9700_moe_tuning/config/operator_patches_README.md" "$P/patches/README.md"
printf '%s\n' 'rocm/vllm:rocm10.1.0_ubuntu24.04_py3.14-pytorch_2.13.0_vllm-0.29.0' > "$P/patches/IMAGE.txt"
sha256sum "$P/patches/"*.py   # must print 420703fd…651a and 82d51028…9902
```

`start-vllm.ps1` must contain the patch block shown in 4.2. If it does not, restore it from the backup
`C:\Users\strug\vllm\tmp\vllm-pr-work\` or add the block by hand, right after the `foreach ($e in $Env)`
line.

### 4.2 What the start script does

`start-vllm.ps1` reads `patches\IMAGE.txt`. If it names the same image as the script, it adds:

```powershell
"-v", "$(Join-Path $PatchDir 'auto_gptq.py'):$quant/auto_gptq.py:ro",
"-v", "$(Join-Path $PatchDir 'gptq_utils.py'):$quant/utils/gptq_utils.py:ro",
"-v", "$(Join-Path $PatchDir 'moe'):/vllm-tuned-moe:ro",
"-e", "VLLM_TUNED_CONFIG_FOLDER=/vllm-tuned-moe",
"-e", "PYTHONPYCACHEPREFIX=/tmp/pyc-patched",
"-e", "PYTHONDONTWRITEBYTECODE=1"
```

If the image is different, it writes a warning and starts **without** the patches. `-NoPatches`
starts without them too. The two `PYTHON*` variables stop Python from using old bytecode of the
replaced files.

Start command (from `VLLM-PARALLEL.md`):

```powershell
& C:\Users\strug\vllm\start-vllm.ps1 -Model /root/.cache/huggingface/local/Qwen3.6-35B-A3B-GPTQ -ServedName qwen3.6-35b-a3b -MaxModelLen 131072 -MaxNumSeqs 4 -Detach
```

No `-NoMtp` and no `VLLM_DISABLED_KERNELS`: both workarounds are now not necessary.

### 4.3 How to check that the fixes are active

Read the container log (`wslc logs vllm`). These lines must be there:

| Line | Means |
|---|---|
| `vLLM patches active from C:\Users\strug\vllm\patches: ...` (printed by the script) | the mounts were added |
| `Using RDNAHybridW4A16LinearKernel for AutoGPTQLinearMethod` | fix 1: the fast kernel is used and did not crash |
| `SpeculativeConfig(method='mtp' ...` and no `w2_weight` error | fix 2: MTP loaded |
| `Using configuration from /vllm-tuned-moe/E=256,N=512,device_name=AMD_Radeon_R9700,dtype=int4_w4a16.json for MoE layer.` | fix 3: the tuned file is used |

One `Using default MoE config ... E=256,N=512,device_name=AMD_Radeon_R9700.json` warning is normal: it
is the MTP draft layer, whose experts are BF16 (no dtype in the file name). That layer keeps the default
config. It could be tuned separately.

### 4.4 When and how to remove a patch

| Patch | Remove when |
|---|---|
| `auto_gptq.py`, `gptq_utils.py` | the image is upgraded to a vLLM release that contains **both** #52637 and #49539. Until then they are needed for the fast kernel and for MTP |
| `moe\...json` | the image ships a config file with the same name, or Triton changes (a new image). On a new image, tune again or measure first: tile sizes that are fast on Triton 3.8.0 can be slow on another version |

The image-tag check makes an image upgrade safe: on a new image tag, the script skips **all** patches
and writes a warning. The patch files are then still on disk but are not used.

**Steps at an image upgrade:**

1. Change `$Image` in `start-vllm.ps1`. Start once and read the log (section 4.3). The script says
   `Patches in ... are for '<old image>', not '<new image>'. Starting without them.`
2. If the log shows `Using RDNAHybridW4A16LinearKernel` and no crash, the new vLLM contains fix 1. If MTP
   loads (no `w2_weight` error), it contains fix 2. If it crashes, use the fallback start below.
3. If the new vLLM contains **both** fixes: delete `patches\auto_gptq.py` and `patches\gptq_utils.py`.
4. If it contains **only one** fix: the file for the other fix must be made again from the **new**
   image's own `auto_gptq.py` and `gptq_utils.py` (never mount files from the old image over a new
   one). Single-fix backports for the old image are in the evidence as a guide: `backports/A` (#49539
   only) and `backports/B` (#52637 only); `harness/make_backports.py` shows how they were made.
5. For the MoE file: if the new image has
   `fused_moe/configs/E=256,N=512,device_name=AMD_Radeon_R9700,dtype=int4_w4a16.json`, delete
   `patches\moe\`. If not, measure the old file against the default (`harness/cross_merged.sh`) before
   you keep it.
6. `patches\IMAGE.txt` holds one tag for all files in `patches\`. Write the new image tag into it only
   when **every** file that remains there was made or measured for the new image. Start again and check
   the log lines of section 4.3.

**Fallback start** (image without the fixes, or to turn the patches off for a test). This is the old
setup: Triton linear kernel, no MTP, default MoE config; about 38 tok/s at 1 request.

```powershell
& C:\Users\strug\vllm\start-vllm.ps1 -Model /root/.cache/huggingface/local/Qwen3.6-35B-A3B-GPTQ -ServedName qwen3.6-35b-a3b -MaxModelLen 131072 -MaxNumSeqs 4 -NoPatches -NoMtp -Env "VLLM_DISABLED_KERNELS=RDNAHybridW4A16LinearKernel" -Detach
```

`-NoPatches`, `-NoMtp` and `-Env` (a list of `NAME=value` strings, each passed as `-e` to the container)
are parameters of `start-vllm.ps1`. `-NoPatches` is not needed when the image tag differs (the script
skips the patches anyway), but it does no harm.

## 5. Measurements (the A/B proofs)

All runs: same GPU, GPU otherwise idle, the disk gate checked before and after (section 6).

### 5.1 Fix 1: hybrid kernel vs Triton kernel (no MTP, 2026-10-26)

| Measurement | Triton | Hybrid (with fix 1) |
|---|---|---|
| 1 request, tok/s | 37.8 | 60.1 |
| 4 requests, tok/s | 96.2 | 143.4 |
| Time to first token, 1 request (median) | 2193 ms | 227 ms |
| GSM8K-200, greedy | 87.0% ± 2.4 | 86.5% ± 2.4 |
| 20 fixed prompts, identical token sequences | — | 12/20; every first difference is a near-tie |

The two kernels sum in a different order, so near-tie tokens can flip. GSM8K shows no loss.

### 5.2 Fix 3: tuned MoE file vs default (2026-10-27)

**Kernel time** (`benchmark_moe.py` without `--tune`, median of 3, A/B alternating):

| Batch size | 1 | 2 | 3 | 4 | 6 | 8 | 12 | 16 | 24 | 32 | 64 | 128 | 256 | 512 | 1024 | 2048 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Default, µs | 81 | 118 | 207 | 232 | 301 | 366 | 489 | 577 | 773 | 912 | 1559 | 1773 | 1825 | 1882 | 1989 | 2997 |
| Tuned, µs | 66 | 93 | 120 | 144 | 200 | 266 | 357 | 441 | 574 | 680 | 920 | 1053 | 1104 | 1568 | 1848 | 2718 |
| Change | −19% | −21% | −42% | −38% | −34% | −27% | −27% | −24% | −26% | −26% | −41% | −41% | −40% | −17% | −7% | −9% |

(Batch size 9 was also measured: −27%.) No batch size is slower. The run-to-run spread is mostly
below 2%.

**End to end** (no MTP, hybrid kernel, `--max-model-len 9216`; 3 server starts per arm, before and
after alternating, 3 runs per start, so 9 values per arm; median and range):

| Measurement | Default | Tuned | Change |
|---|---|---|---|
| 1 request, tok/s | 60.5 (60.2–61.8) | 62.9 (61.6–63.5) | +3.9% |
| 4 requests, tok/s | 148.0 (140.5–151.4) | 174.2 (169.2–176.8) | +17.7% |
| 4 requests, time per output token | 26.2 ms | 22.0 ms | −15.8% |
| Prefill 8,192 tokens, time to first token | 1913 ms (1895–2001) | 1926 ms (1890–2027) | +0.7% (within the spread) |
| GSM8K-200 | 88.5% | 88.5% | 0 |
| 20 fixed prompts | — | 20/20 identical | — |

Each server log was checked: 3 × `Using configuration from ...` (tuned) and 3 × `Using default MoE
config` (default). The plan's ship rule (5 tests) passes. The gain is largest with 4 requests, because
there the MoE layers do more of the work.

This A/B ran **without MTP and at 9,216 context**. In the production setup (MTP on, 131,072 context) the
MoE file was not tested in an alternating A/B. There we have only two runs on two days: 236.1 → 256.0
tok/s at 4 requests (+8.4%) and 96.8 → 101.5 at 1 request (+4.9%). Do not expect +17.7% in production.

### 5.3 Fix 2: MTP output vs no-MTP output

With MTP, 15 of 20 greedy outputs are identical to no-MTP; in the 5 others, every first difference is
a near-tie (the top two tokens are within 0.5 logprob). No-MTP repeated twice gives 20/20.

The pre-registered limit was 17/20, so this test **failed by its own rule**. We then checked two
configuration differences between the MTP and no-MTP servers (2026-10-27), with the Triton kernel as on
the first run:

| Server (no MTP) | vs no-MTP (block 1056) | vs MTP |
|---|---|---|
| attention block size 1072 (as with MTP) | 20/20 | 15/20 |
| block size 1072 + MTP's CUDA-graph sizes [1,2,3,4,6,8,12,16,24] | 20/20 | 15/20 |

So block size and graph sizes are **not** the cause. The differences come from MTP decoding itself.
The most likely reason: each MTP verify step runs 3 tokens per request (1 + 2 drafts) instead of 1,
so the kernels run with other shapes and sum in another order, and near-tie tokens can flip. This is
the normal behaviour of speculative decoding; vLLM documents no guarantee of equal logprobs. It is
**not proven** directly.

## 6. Findings and pitfalls (read before you repeat this work)

| Finding | What to do |
|---|---|
| The system SSD logged 671 bad-block events on 2026-10-25 while the GPU was busy. Cause found by the operator: PCIe Link State Power Management. Fixed for AC power (0 = Off); DC is still 2 | Before long GPU runs, record the newest Windows System-log event id for `disk`/`stornvme`, and stop if a newer one appears. 0 new events during all runs on 2026-10-26 and 2026-10-27 |
| `wslc list` cuts container names longer than about 20 characters ("vllmprtest-moe-befo…") | In scripts, use `wslc list --no-trunc`. A readiness check without it stopped two healthy servers |
| One test server hung after the CUDA-graph capture (1 of about 10 starts on 2026-10-27; same settings started normally before and after) | Use a start time-out (we used 20 min) and one retry |
| A container stopped while a zombie process held memory gave a KV cache of only 83,558 tokens | Do not trust a KV-cache size measured right after a bad stop. Normal values: ~206K tokens (no MTP, 8,192 context) and 200–228K tokens (MTP, 131,072 context) |
| The iGPU is visible in containers | Always set `HIP_VISIBLE_DEVICES=0` |
| `benchmark_moe.py` fails with Ray 2.59 on ROCm | See section 3.3, change 1 |
| The image has no vLLM test dependencies (for example `tblib`) | Install pinned test packages with `uv pip install` in a throw-away `--rm` container |
| Container names | Use a fixed prefix for test containers (we used `vllmprtest-`) and `--rm`, so leftovers are easy to find |

## 7. What this does NOT prove

| Not proven | Why |
|---|---|
| The PR heads work on a GPU | Only our hand backports on the image (`98dff2a81`) ran |
| vLLM `main` works | `main` was not built (a ROCm image of `main` is about 30–40 GB) |
| The MoE file helps other GPTQ checkpoints of the same shape | Tested only with this checkpoint (GPTQ, group 128, sym, `desc_act` false). Other int4 checkpoints with E=256, N=512 will also load the file |
| The MoE gain in the production setup | The A/B ran without MTP at 9,216 context. With MTP at 131,072 we have only a before/after on two days (+4.9% / +8.4%), not an A/B |
| Task accuracy with MTP | GSM8K was not run with MTP; only the 20-prompt comparison (15/20) |
| The tuned tiles are optimal at bf16 | `benchmark_moe.py` tunes with fp16 activations; bf16 serving was measured end to end only (section 5.2) |
| The cause of the MTP output differences | Configuration causes are excluded; the spec-decode cause is not shown directly |
| That the MTP draft weights are fully right | Acceptance 79.5% shows no severe loss; there is no direct check of the gate/up order in `gate_up_proj` |
| Disk health | SMART was not read (it needs an admin shell) |

## 8. Where the evidence is

Paths, not links: they work only on the operator's machine.

| What | Where |
|---|---|
| Plans | `vllm_pr_work` (branch `docs/pr-plans`): `research/PLAN_rdna_hybrid_w4a16_gptq_sym.md` (fix 1, IMPLEMENTED), `todo/PLAN_qwen3_5_moe_gptq_mtp_load.md` (fix 2), `todo/PLAN_moe_config_r9700_int4.md` (fix 3) |
| Evidence for fixes 1 and 2 | `vllm_pr_work · research/2026-10-26_r9700_vllm_fixes/` (`RESULTS.md`, backports A/B/C, harness, runs, server logs, the texts posted upstream) |
| Evidence for fix 3 and the MTP check | `vllm_pr_work · research/2026-10-27_r9700_moe_tuning/` |
| vLLM worktrees | `~/git/vllm-wt/moe-config-r9700-int4` (branch `perf/moe-config-r9700-int4-e256-n512`), `~/git/vllm-wt/rdna-gptq-sym-tests` (PR #63825) |
| Operator files | `C:\Users\strug\vllm\start-vllm.ps1`, `patches\`, `VLLM-PARALLEL.md`; backups in `C:\Users\strug\vllm\tmp\vllm-pr-work\` |

Upstream links: issue <https://github.com/vllm-project/vllm/issues/63597>, issue
<https://github.com/vllm-project/vllm/issues/63598>, PR <https://github.com/vllm-project/vllm/pull/52637>,
PR <https://github.com/vllm-project/vllm/pull/49539>, PR <https://github.com/vllm-project/vllm/pull/63825>.

## 9. Open items

| Item | Who |
|---|---|
| PR #63825 needs a maintainer label and a review; follow its comments | operator + agent |
| An upstream PR with the tuned MoE file (needs the operator's approval of the text) | operator decides |
| An upstream report of the Ray 2.59 / `ROCR_VISIBLE_DEVICES` problem in `benchmark_moe.py` | operator decides |
| Remove the backports when a new image contains #52637 and #49539 | operator, at the next image upgrade |
| Seven more speed options (MTP 1/3 drafts, batched tokens, FP8 KV, `TRITON_ATTN`, linear kernel at M 6–16, BF16 draft MoE file, int4 keys 24–128): partly measured 2026-10-27/28 — `mtp3` and `TRITON_ATTN` pass; the combined A/B started 2026-10-28 and was paused after its first `base` round. See [RESULTS_vllm_029_r9700_speed_options.md](RESULTS_vllm_029_r9700_speed_options.md) § 11 | resume the final A/B on a quiet PC |
