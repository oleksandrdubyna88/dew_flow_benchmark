#!/usr/bin/env bash
# speed_A.sh <outdir> [arms...]: speed-options plan, phase A. Arms alternate in order, 3 rounds, production shape
# (variant C, tuned int4 MoE file, max-model-len 131072). Per start: bench 1024/512 c=1/c=4 x3, prefill 8192/128
# and 32768/128 at c=1 x3, spec-decode metrics; round 1 also 20 greedy prompts and GSM8K-200.
# A finished arm-round leaves <outdir>/done_<arm>-<round> and is skipped on a rerun. An arm that fails to start
# twice is marked unsupported and skipped. Any exit restores the operator's server (restore_server.sh).
# Predictions are in the plan (todo/PLAN_r9700_speed_options.md), written before this run.
set -u
OUT="$1"; shift; mkdir -p "$OUT"; D="$(dirname "$0")"; W="/mnt/c/Program Files/WSL/wslc.exe"
ARMS=("${@:-base mtp1 mtp3 mnbt8k tattn fp8kv}"); read -r -a ARMS <<< "${ARMS[*]}"
export MAXLEN=131072
# The operator's sampling defaults (temperature 0.3, top_p 0.9, top_k 20, repetition_penalty 1.05): MTP acceptance
# depends on them. The first run without them (model default temperature 1.0) gave 75.9 tok/s and was discarded.
export GENCONFIG='C:\Users\strug\vllm\genconfig'
trap '"$D/restore_server.sh"' EXIT
arm_args() {
  # Final A/B (plan "Final A/B"): fbase = base, fcombo = the kept options together (mtp3 + tattn). Own names, so the
  # round-1 prompts/GSM8K files of phase A are not overwritten.
  local mtp=2; case "$1" in mtp1) mtp=1;; mtp3|fcombo) mtp=3;; esac
  echo "--generation-config /genconfig --speculative-config.method mtp --speculative-config.num_speculative_tokens $mtp"
  case "$1" in
    mnbt8k) echo "--max-num-batched-tokens 8192";;
    tattn|fcombo) echo "--attention-backend TRITON_ATTN";;
    fp8kv) echo "--kv-cache-dtype fp8";;
  esac
}
prefill() {  # prefill <container> <tag> <input-len>
  for r in 1 2 3; do
    "$W" exec "$1" vllm bench serve --backend vllm --base-url http://127.0.0.1:8000 --model qwen3.6-35b-a3b \
      --tokenizer /root/.cache/huggingface/local/Qwen3.6-35B-A3B-GPTQ --dataset-name random --random-input-len "$3" \
      --random-output-len 128 --ignore-eos --num-prompts 3 --max-concurrency 1 --seed $r 2>&1 | tr -d '\r' \
      | grep -E "Output token throughput|Mean TTFT|Median TTFT" | sed "s/^/$2 prefill$3 r=$r /"
  done
}
"$W" stop vllm >/dev/null 2>&1; sleep 10
for round in 1 2 3; do for arm in "${ARMS[@]}"; do
  [ -f "$OUT/done_$arm-$round" ] && continue
  [ -f "$OUT/unsupported_$arm" ] && continue
  "$D/disk_gate.sh" check || { echo "STOP: disk gate"; exit 1; }
  name="vllmprtest-sp-$arm-$round"; read -r -a a <<< "$(arm_args "$arm" | tr '\n' ' ')"
  echo "=== $(date '+%F %T') $arm round $round (WSL load $(cut -d' ' -f1-3 /proc/loadavg)): ${a[*]}"
  if ! "$D/serve.sh" "$name" C "$OUT/server_$arm-$round.log" -e VLLM_TUNED_CONFIG_FOLDER=/work/moe/tune/merged -- "${a[@]}"; then
    echo "RETRY: first start failed"; "$W" stop "$name" >/dev/null 2>&1; sleep 60
    if ! "$D/serve.sh" "$name" C "$OUT/server_$arm-$round.log" -e VLLM_TUNED_CONFIG_FOLDER=/work/moe/tune/merged -- "${a[@]}"; then
      echo "UNSUPPORTED: $arm did not start twice"; grep -E "Error|error:" "$OUT/server_$arm-$round.log" | tail -3 | cut -c1-300
      "$W" stop "$name" >/dev/null 2>&1; touch "$OUT/unsupported_$arm"; sleep 20; continue
    fi
  fi
  grep -E "Using configuration from|Using default MoE config|RDNAHybrid|GPU KV cache size|max_num_scheduled_tokens|Fused multi-step|attention backend|Cannot use ROCm" \
    "$OUT/server_$arm-$round.log" | cut -c1-240 | sort -u
  "$D/bench.sh" "$name" "$arm-$round" | tee -a "$OUT/bench.txt"
  prefill "$name" "$arm-$round" 8192 | tee -a "$OUT/bench.txt"
  prefill "$name" "$arm-$round" 32768 | tee -a "$OUT/bench.txt"
  "$W" exec "$name" python3 -c "import urllib.request;print(urllib.request.urlopen('http://127.0.0.1:8000/metrics').read().decode())" 2>&1 \
    | tr -d '\r' | grep -E '^vllm:spec_decode' | sed "s/^/$arm-$round /" >> "$OUT/spec_metrics.txt"
  if [ "$round" = 1 ]; then
    "$W" exec "$name" python3 /work/harness.py collect --url http://127.0.0.1:8000 --model qwen3.6-35b-a3b \
      --out "/work/speed/prompts_$arm.json" 2>&1 | tr -d '\r' | tail -1
    "$W" exec "$name" python3 /work/harness.py gsm8k --url http://127.0.0.1:8000 --model qwen3.6-35b-a3b \
      --data /work/gsm8k/test.jsonl --n 200 --out "/work/speed/gsm8k_$arm.json" 2>&1 | tr -d '\r' | tail -1
  fi
  "$W" logs "$name" 2>&1 | tr -d '\r' > "$OUT/server_$arm-$round.log"
  "$W" stop "$name" >/dev/null 2>&1; touch "$OUT/done_$arm-$round"; sleep 10
done; done
"$D/disk_gate.sh" check
echo "=== $(date '+%F %T') PHASE A DONE"
