#!/usr/bin/env bash
# moe_kernel_ab.sh <outdir>: phase B1 kernel A/B — the BF16 draft-layer fused MoE with the default config ("default")
# against the merged tuned file ("tuned", VLLM_TUNED_CONFIG_FOLDER=/moe/tune/merged_b1). benchmark_moe.py without
# --tune times the config get_moe_configs() picks (100 iterations); A B A B A B containers, no model load.
# Decision rule (written before the run): the end-to-end arm fb1 runs only when "tuned" is faster by more than 3 % at
# both bs 1 and bs 4 (the draft layer's M at 1 and 4 requests); moe_kernel_ab.py prints "B1 KERNEL: faster|not faster".
set -u
OUT="$1"; mkdir -p "$OUT"; D="$(dirname "$0")"; W="/mnt/c/Program Files/WSL/wslc.exe"
IMG=rocm/vllm:rocm10.1.0_ubuntu24.04_py3.14-pytorch_2.13.0_vllm-0.29.0; WT='C:\Users\strug\vllm\tmp\vllm-pr-work'
"$W" stop vllm >/dev/null 2>&1
for rep in 1 2 3; do for arm in default tuned; do
  "$D/disk_gate.sh" check || { echo "STOP: disk gate"; exit 1; }
  e=(); [ "$arm" = tuned ] && e=(-e VLLM_TUNED_CONFIG_FOLDER=/moe/tune/merged_b1)
  echo "=== $(date '+%F %T') $arm rep $rep"
  timeout 1800 "$W" run --rm --name "vllmprtest-moeab-$arm-$rep" --gpus all --shm-size 16g -e HIP_VISIBLE_DEVICES=0 \
    -e MOE_TUNE_ACT_DTYPE=bfloat16 "${e[@]}" -e TRITON_CACHE_DIR=/root/.cache/vllm/triton-moe-ab \
    -v "hf-cache:/root/.cache/huggingface" -v "vllm-cache:/root/.cache/vllm" -v "$WT\\work\\moe:/moe" -w /moe "$IMG" \
    python3 benchmark_moe.py --model /root/.cache/huggingface/local/Qwen3.6-35B-A3B-GPTQ --tp-size 1 --dtype auto \
    --batch-size 1 2 3 4 6 8 12 16 --trust-remote-code 2>&1 | tr -d '\r' > "$OUT/$arm-$rep.log"
  echo "rc=$? $(grep -c 'Kernel time' "$OUT/$arm-$rep.log") kernel times"; grep -E "Error|Traceback" "$OUT/$arm-$rep.log" | tail -2
done; done
python3 -I "$D/moe_kernel_ab.py" "$OUT"
