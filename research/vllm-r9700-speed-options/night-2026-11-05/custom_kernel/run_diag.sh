#!/usr/bin/env bash
# Diagnostic reruns (v2 models, out2/): run_diag.sh <arm:B|C> <tag> [--eager]. Writes out2/arm_<arm>[_eager]_<tag>.json with the draft trace.
set -u
S="$(cd "$(dirname "$0")" && pwd)"; W="/mnt/c/Program Files/WSL/wslc.exe"
IMG=rocm/vllm:rocm10.1.0_ubuntu24.04_py3.14-pytorch_2.13.0_vllm-0.29.0
WT='C:\Users\strug\vllm\tmp\vllm-pr-work\work\optin-test'
R=/opt/python/lib/python3.14/site-packages/vllm/v1/attention/backends/rocm_attn.py
arm=$1; tag=$2; shift 2
m=(); [ "$arm" = C ] && m=(-v "$WT\\mounts\\rocm_attn_029_optin.py:$R:ro")
[ "$arm" = C ] && ef=1 || ef=0
"$S/disk_gate.sh" baseline || exit 3
timeout 3000 "$W" run --rm --name vllmprtest-diag-$arm-$tag --gpus all --shm-size 4g -e HIP_VISIBLE_DEVICES=0 -e HF_HUB_OFFLINE=1 \
  -e VLLM_ENABLE_V1_MULTIPROCESSING=0 -e PYTHONHASHSEED=0 -e PYTHONDONTWRITEBYTECODE=1 -v "hf-cache:/root/.cache/huggingface" "${m[@]}" \
  -v "$WT:/t" -w /t $IMG sh -c "nice -n 19 python3 custom_kernel_check.py --arm $arm --expect-fused $ef --v2 --gpu-util 0.08 --tag $tag --trace-drafts $*" 2>&1 \
  | tr -d '\r' | tee "$S/out2/log_${arm}_${tag}.txt" | grep -E "^(acceptance|distinct|RESULT|FAIL)"
"$S/disk_gate.sh" check
