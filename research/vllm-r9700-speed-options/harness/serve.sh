#!/usr/bin/env bash
# serve.sh <name> <variant A|B|C|none> <log> [-e VAR=val ...] -- [extra vllm serve args]
# Starts a detached test server (--rm), waits until /v1/models answers (max 20 min), saves its log.
# MODEL_DIR, SERVED, MAXSEQS (2026-11-01) select another model; the defaults are the 35B production model.
set -u
name="$1" variant="$2" log="$3"; shift 3
envs=(); while [ $# -gt 0 ] && [ "$1" != "--" ]; do envs+=("$1"); shift; done; shift || true
W="/mnt/c/Program Files/WSL/wslc.exe"; IMG=rocm/vllm:rocm10.1.0_ubuntu24.04_py3.14-pytorch_2.13.0_vllm-0.29.0
Q=/opt/python/lib/python3.14/site-packages/vllm/model_executor/layers/quantization; WT='C:\Users\strug\vllm\tmp\vllm-pr-work'
mounts=(-v "hf-cache:/root/.cache/huggingface" -v "vllm-cache:/root/.cache/vllm" -v "$WT\\work:/work")
# GENCONFIG=<windows dir> mounts the operator's generation config (sampling defaults) at /genconfig; pass --generation-config /genconfig.
[ -n "${GENCONFIG:-}" ] && mounts+=(-v "$GENCONFIG:/genconfig:ro")
if [ "$variant" != none ]; then
  mounts+=(-v "$WT\\$variant\\auto_gptq.py:$Q/auto_gptq.py:ro")
  [ -f "/mnt/c/Users/strug/vllm/tmp/vllm-pr-work/$variant/gptq_utils.py" ] && mounts+=(-v "$WT\\$variant\\gptq_utils.py:$Q/utils/gptq_utils.py:ro")
fi
"$W" run -d --rm --name "$name" --gpus all --shm-size 16g -e HIP_VISIBLE_DEVICES=0 -e TRITON_CACHE_DIR=/root/.cache/vllm/triton \
  -e PYTHONPYCACHEPREFIX=/tmp/pyc-fresh -e PYTHONDONTWRITEBYTECODE=1 "${envs[@]}" "${mounts[@]}" "$IMG" \
  vllm serve "${MODEL_DIR:-/root/.cache/huggingface/local/Qwen3.6-35B-A3B-GPTQ}" --served-model-name "${SERVED:-qwen3.6-35b-a3b}" \
  --host 0.0.0.0 --port 8000 --max-model-len ${MAXLEN:-8192} --gpu-memory-utilization 0.85 --max-num-seqs "${MAXSEQS:-4}" \
  --limit-mm-per-prompt.image 0 --limit-mm-per-prompt.video 0 "$@" 2>&1 | tr -d '\r' | tail -2
exec "$(dirname "$0")/wait_ready.sh" "$name" "$log"
