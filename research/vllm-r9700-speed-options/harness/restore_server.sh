#!/usr/bin/env bash
# Restore the operator's own vLLM server ('vllm') with start-vllm.ps1 in its production shape of
# 2026-10-27: patches (hybrid kernel + MTP load + tuned MoE file), MTP 2, max-model-len 131072, 4 seqs.
# RESTORE_ARGS adds start-script arguments (for example kept options). Idempotent: does nothing when
# 'vllm' already runs.
set -u
W="/mnt/c/Program Files/WSL/wslc.exe"
# A planned restart of a phase (operator not waiting) leaves skip_restore; it is used once.
if [ -f "$(dirname "$0")/skip_restore" ]; then rm -f "$(dirname "$0")/skip_restore"; echo "restore: skipped once (planned restart)"; exit 0; fi
# Stop every test container first, so the operator's server can take the GPU.
for c in $("$W" list --no-trunc 2>/dev/null | tr -d '\r' | awk 'NR>1{print $NF}' | grep -E '^vllmprtest-' || true); do
  echo "restore: stopping test container $c"; "$W" stop "$c" >/dev/null 2>&1 || true
done
if "$W" list --no-trunc 2>/dev/null | tr -d '\r' | grep -qE '\svllm$'; then echo "restore: vllm already running"; exit 0; fi
read -r -a extra <<< "${RESTORE_ARGS:-}"
pwsh.exe -NoProfile -File 'C:\Users\strug\vllm\start-vllm.ps1' \
  -Model /root/.cache/huggingface/local/Qwen3.6-35B-A3B-GPTQ -ServedName qwen3.6-35b-a3b \
  -MaxModelLen 131072 -MaxNumSeqs 4 "${extra[@]}" -Detach 2>&1 | tr -d '\r' | tail -5
echo "restore: started at $(date -Is)"
