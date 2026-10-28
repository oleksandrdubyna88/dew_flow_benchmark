#!/usr/bin/env bash
# speed_CB.sh: speed-options plan, phase C (linear-kernel microbench, option 7), then phase B1 (BF16 MoE file for the
# MTP draft layer, option 3): a coarse large-M chunk and full searches at bs 1, 2, 4, 12 (the draft sees M = number of
# requests, and up to 3 x requests on its first step). Chunks are resumable (tune_chunk.sh). Any exit restores the
# operator's server. Phase B2 (int4 keys 24-128, option 4) runs later, after the final A/B.
set -u
D="$(dirname "$0")"; W="/mnt/c/Program Files/WSL/wslc.exe"; IMG=rocm/vllm:rocm10.1.0_ubuntu24.04_py3.14-pytorch_2.13.0_vllm-0.29.0
WT='C:\Users\strug\vllm\tmp\vllm-pr-work'; S=/mnt/c/Users/strug/vllm/tmp/vllm-pr-work/work/speed
trap '"$D/restore_server.sh"' EXIT
clear_cache() { "$W" run --rm --name vllmprtest-moe-clear -v "vllm-cache:/root/.cache/vllm" "$IMG" sh -c 'du -sh /root/.cache/vllm/triton-moe-tune 2>/dev/null; rm -rf /root/.cache/vllm/triton-moe-tune' 2>&1 | tr -d '\r' | sed 's/^/cache before clear: /'; }
run() { echo "=== $(date '+%F %T') chunk $1"; "$@" || { echo "=== STOP: chunk failed or disk gate ($(date '+%T'))"; clear_cache; exit 1; }; clear_cache; }
"$W" stop vllm >/dev/null 2>&1; sleep 10
"$D/disk_gate.sh" check || { echo "STOP: disk gate"; exit 1; }
if [ ! -s "$S/linear_bench.json" ]; then
  echo "=== $(date '+%F %T') phase C: linear_bench"
  timeout 5400 "$W" run --rm --name vllmprtest-linear --gpus all --shm-size 16g -e HIP_VISIBLE_DEVICES=0 \
    -e TRITON_CACHE_DIR=/root/.cache/vllm/triton-linear -v "vllm-cache:/root/.cache/vllm" -v "$WT\\work:/work" "$IMG" \
    python3 /work/speed/linear_bench.py /work/speed/linear_bench.json > "$S/linear_bench.log" 2>&1
  echo "phase C rc=$? rows=$(grep -c '^{' "$S/linear_bench.log")"; grep -E "Error|Traceback" "$S/linear_bench.log" | tail -3
  "$D/disk_gate.sh" check || { echo "STOP: disk gate"; exit 1; }
fi
export TUNE_DTYPE=auto TUNE_ACT=bfloat16
CANDIDATES=/moe/coarse_candidates.json run "$D/tune_chunk.sh" draft-coarse-largeM 256 1024 2048
for bs in 1 2 4 12; do run "$D/tune_chunk.sh" "draft-bs-$bs" "$bs"; done
echo "=== $(date '+%F %T') PHASE C+B1 DONE"
