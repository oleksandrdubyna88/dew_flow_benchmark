#!/usr/bin/env bash
# tune_chunk.sh <name> <bs...>: one tuning chunk into /moe/tune/<name>/ (done only when its JSON exists).
# A chunk dir without JSON is a half-written chunk: it is deleted before the run. Disk gate before and after.
# TUNE_DTYPE (default int4_w4a16) is the benchmark_moe --dtype; TUNE_ACT=bfloat16 sets MOE_TUNE_ACT_DTYPE (local hook).
set -u
name="$1"; shift; D="$(dirname "$0")"; M=/mnt/c/Users/strug/vllm/tmp/vllm-pr-work/work/moe
W="/mnt/c/Program Files/WSL/wslc.exe"; IMG=rocm/vllm:rocm10.1.0_ubuntu24.04_py3.14-pytorch_2.13.0_vllm-0.29.0; WT='C:\Users\strug\vllm\tmp\vllm-pr-work'
if ls "$M/tune/$name"/E=*.json >/dev/null 2>&1; then echo "chunk $name: already done"; exit 0; fi
rm -rf "$M/tune/$name"; mkdir -p "$M/tune/$name"
"$D/disk_gate.sh" check || exit 1
t0=$(date +%s)
extra=(); [ -n "${CANDIDATES:-}" ] && extra=(-e "MOE_TUNE_CANDIDATES=$CANDIDATES")
[ -n "${TUNE_ACT:-}" ] && extra+=(-e "MOE_TUNE_ACT_DTYPE=$TUNE_ACT")
"$W" run --rm --name "vllmprtest-moe-tune-$name" --gpus all --shm-size 16g -e HIP_VISIBLE_DEVICES=0 "${extra[@]}" \
  -e TRITON_CACHE_DIR=/root/.cache/vllm/triton-moe-tune -v "hf-cache:/root/.cache/huggingface" -v "vllm-cache:/root/.cache/vllm" \
  -v "$WT\\work\\moe:/moe" -w /moe "$IMG" python3 benchmark_moe.py --model /root/.cache/huggingface/local/Qwen3.6-35B-A3B-GPTQ \
  --tp-size 1 --dtype "${TUNE_DTYPE:-int4_w4a16}" --tune --batch-size "$@" --save-dir "/moe/tune/$name" --trust-remote-code \
  > "$M/tune/$name.log" 2>&1
rc=$?; t1=$(date +%s)
echo "chunk $name (bs $*): rc=$rc, $((t1-t0)) s, json: $(ls "$M/tune/$name" 2>/dev/null)"
grep -E "Start tuning|Tuning space|Completed tuning|Best config|Kernel time|Error|Traceback" "$M/tune/$name.log" | tail -6
"$D/disk_gate.sh" check || exit 1
