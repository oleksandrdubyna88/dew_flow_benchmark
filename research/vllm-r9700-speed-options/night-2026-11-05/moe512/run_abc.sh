#!/usr/bin/env bash
# run_abc.sh <tag> <fp16|bf16> <reps>: MoE kernel A/B/C, arms alternating A B C A B C ...
#   A = no tuned file (default config), B = the PR's file (cfgB), C = the PR's file + "512" entry (cfgC).
# benchmark_moe.py without --tune, 100 iterations per batch size, through VLLM_TUNED_CONFIG_FOLDER.
# Runs beside production (container `vllm`); never stops, restarts or execs into it. nice 19 inside the container.
set -u
S="$(cd "$(dirname "$0")" && pwd)"; W="/mnt/c/Program Files/WSL/wslc.exe"
TAG="$1"; ACT="$2"; REPS="$3"; OUT="$S/logs/$TAG"; mkdir -p "$OUT"
IMG=rocm/vllm:rocm10.1.0_ubuntu24.04_py3.14-pytorch_2.13.0_vllm-0.29.0
WT='C:\Users\strug\vllm\tmp\vllm-pr-work\work\moe512'
BS="1 16 128 256 384 512 640 768 896 1024 2048"
actenv=(); [ "$ACT" = bf16 ] && actenv=(-e MOE_TUNE_ACT_DTYPE=bfloat16)
"$S/disk_gate.sh" baseline || exit 3
"$W" list | tr -d '\r' | grep -E ' vllm *$' | grep -q ' Up ' || { echo "STOP: production not Up"; exit 3; }
for rep in 1 2 3 $(seq 4 "$REPS"); do [ "$rep" -gt "$REPS" ] && break
 for arm in A B C; do
  "$S/disk_gate.sh" check || { echo "STOP: disk gate"; exit 4; }
  fe=(); case $arm in B) fe=(-e VLLM_TUNED_CONFIG_FOLDER=/moe/cfgB);; C) fe=(-e VLLM_TUNED_CONFIG_FOLDER=/moe/cfgC);; esac
  "$W" logs vllm 2>&1 | tr -d '\r' > "$OUT/prod_before_$arm-$rep.txt"
  echo "=== $(date '+%F %T') arm $arm rep $rep load: $(cut -d' ' -f1-3 /proc/loadavg)"
  t0=$(date +%s)
  timeout 1800 "$W" run --rm --name "vllmprtest-moe512-$arm-$rep" --gpus all --shm-size 4g -e HIP_VISIBLE_DEVICES=0 \
    "${actenv[@]}" "${fe[@]}" -e TRITON_CACHE_DIR=/root/.cache/vllm/triton-moe512 \
    -v "hf-cache:/root/.cache/huggingface" -v "vllm-cache:/root/.cache/vllm" -v "$WT:/moe" -w /moe "$IMG" \
    nice -n 19 python3 benchmark_moe.py --model /root/.cache/huggingface/local/Qwen3.6-35B-A3B-GPTQ --tp-size 1 --dtype int4_w4a16 \
    --batch-size $BS --trust-remote-code 2>&1 | tr -d '\r' > "$OUT/$arm-$rep.log"
  rc=${PIPESTATUS[0]}; t1=$(date +%s)
  "$W" logs vllm 2>&1 | tr -d '\r' > "$OUT/prod_after_$arm-$rep.txt"
  echo "$arm-$rep start=$t0 end=$t1 rc=$rc kernel_times=$(grep -c 'Kernel time' "$OUT/$arm-$rep.log") new_prod_log_lines=$(( $(wc -l < "$OUT/prod_after_$arm-$rep.txt") - $(wc -l < "$OUT/prod_before_$arm-$rep.txt") )) load_after: $(cut -d' ' -f1-3 /proc/loadavg)"
  grep -E "Traceback|Error" "$OUT/$arm-$rep.log" | tail -2
  [ "$rc" -ne 0 ] && { echo "STOP: rc=$rc"; exit 5; }
  echo "production: $("$W" list | tr -d '\r' | grep -E ' vllm *$' | grep -o 'Up [0-9a-z ]*')"
 done
done
echo "ALL DONE $(date '+%F %T')"
