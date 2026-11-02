#!/usr/bin/env bash
# moe_kernel_ab.sh <outdir>: MoE kernel A/B (phase B1; phase B2 with the AB_* variables below) — the BF16 draft-layer fused MoE with the default config ("default")
# against the merged tuned file ("tuned", VLLM_TUNED_CONFIG_FOLDER=/moe/tune/merged_b1). benchmark_moe.py without
# --tune times the config get_moe_configs() picks (100 iterations); A B A B A B containers, no model load.
# Decision rule (written before the run): the end-to-end arm fb1 runs only when "tuned" is faster by more than 3 % at
# both bs 1 and bs 4 (the draft layer's M at 1 and 4 requests); moe_kernel_ab.py prints "B1 KERNEL: faster|not faster".
set -u
OUT="$1"; mkdir -p "$OUT"; D="$(dirname "$0")"; W="/mnt/c/Program Files/WSL/wslc.exe"
IMG=rocm/vllm:rocm10.1.0_ubuntu24.04_py3.14-pytorch_2.13.0_vllm-0.29.0; WT='C:\Users\strug\vllm\tmp\vllm-pr-work'
# Phase B2 (2026-11-01) sets: AB_DTYPE=int4_w4a16 AB_ACT= AB_A=/moe/tune/merged AB_B=/moe/tune/merged_b2 AB_BS="..." AB_RULE=b2.
AB_DTYPE=${AB_DTYPE:-auto}; AB_ACT=${AB_ACT-bfloat16}; AB_A=${AB_A:-}; AB_B=${AB_B:-/moe/tune/merged_b1}
AB_BS=${AB_BS:-1 2 3 4 6 8 12 16}; export AB_RULE=${AB_RULE:-b1}
"$W" stop vllm >/dev/null 2>&1
for rep in 1 2 3; do for arm in default tuned; do
  "$D/disk_gate.sh" check || { echo "STOP: disk gate"; exit 1; }
  e=(); [ -n "$AB_ACT" ] && e=(-e "MOE_TUNE_ACT_DTYPE=$AB_ACT")
  if [ "$arm" = tuned ]; then e+=(-e "VLLM_TUNED_CONFIG_FOLDER=$AB_B"); elif [ -n "$AB_A" ]; then e+=(-e "VLLM_TUNED_CONFIG_FOLDER=$AB_A"); fi
  echo "=== $(date '+%F %T') $arm rep $rep"
  timeout 1800 "$W" run --rm --name "vllmprtest-moeab-$arm-$rep" --gpus all --shm-size 16g -e HIP_VISIBLE_DEVICES=0 \
    "${e[@]}" -e TRITON_CACHE_DIR=/root/.cache/vllm/triton-moe-ab \
    -v "hf-cache:/root/.cache/huggingface" -v "vllm-cache:/root/.cache/vllm" -v "$WT\\work\\moe:/moe" -w /moe "$IMG" \
    python3 benchmark_moe.py --model /root/.cache/huggingface/local/Qwen3.6-35B-A3B-GPTQ --tp-size 1 --dtype "$AB_DTYPE" \
    --batch-size $AB_BS --trust-remote-code 2>&1 | tr -d '\r' > "$OUT/$arm-$rep.log"
  echo "rc=$? $(grep -c 'Kernel time' "$OUT/$arm-$rep.log") kernel times"; grep -E "Error|Traceback" "$OUT/$arm-$rep.log" | tail -2
done; done
AB_RULE="$AB_RULE" python3 -I "$D/moe_kernel_ab.py" "$OUT"
