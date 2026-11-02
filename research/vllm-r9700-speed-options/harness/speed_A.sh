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
export MAXLEN=${MAXLEN:-131072}  # MODEL_DIR/SERVED/MAXSEQS/MAXLEN/PREFILL_LENS: another model (2026-11-01)
SERVED=${SERVED:-qwen3.6-35b-a3b}; PREFILL_LENS=${PREFILL_LENS:-8192 32768}
O7_SRC='C:\Users\strug\vllm\tmp\vllm-pr-work\work\speed\o7\rdna_hybrid_w4a16.py'
O7_DST=/opt/python/lib/python3.14/site-packages/vllm/model_executor/kernels/linear/mixed_precision/rdna_hybrid_w4a16.py
RA_SRC='C:\Users\strug\vllm\tmp\vllm-pr-work\work\speed\ra\rocm_attn.py'
RA_DST=/opt/python/lib/python3.14/site-packages/vllm/v1/attention/backends/rocm_attn.py
# The operator's sampling defaults (temperature 0.3, top_p 0.9, top_k 20, repetition_penalty 1.05): MTP acceptance
# depends on them. The first run without them (model default temperature 1.0) gave 75.9 tok/s and was discarded.
export GENCONFIG='C:\Users\strug\vllm\genconfig'
trap '"$D/restore_server.sh"' EXIT
arm_args() {
  # Final A/B (plan "Final A/B"): fbase = base, fcombo = the kept options together (mtp3 + tattn). Own names, so the
  # round-1 prompts/GSM8K files of phase A are not overwritten.
  # Option 7 (night of 2026-10-31): o7ref = fcombo; o7 = fcombo with the tile patch mounted (mo).
  # 2026-11-01: ra3 = MTP 3 on the default ROCM_ATTN; ra3opt = ra3 + the fused-draft opt-in patch (mo);
  # ta3 = fcombo; ta3mb = fcombo + mnbt8k; q27old / q27new = the 27B model, old vs new start-script defaults.
  local mtp=2; case "$1" in mtp1) mtp=1;; mtp3|fcombo|o7ref|o7|ra3|ra3opt|ta3|ta3mb|q27new) mtp=3;; esac
  echo "--generation-config /genconfig --speculative-config.method mtp --speculative-config.num_speculative_tokens $mtp"
  case "$1" in
    mnbt8k) echo "--max-num-batched-tokens 8192";;
    ta3mb) echo "--attention-backend TRITON_ATTN --max-num-batched-tokens 8192";;
    tattn|fcombo|o7ref|o7|ta3|q27new) echo "--attention-backend TRITON_ATTN";;
    fp8kv) echo "--kv-cache-dtype fp8";;
  esac
}
prefill() {  # prefill <container> <tag> <input-len>
  for r in 1 2 3; do
    "$W" exec "$1" vllm bench serve --backend vllm --base-url http://127.0.0.1:8000 --model "$SERVED" \
      --tokenizer "${MODEL_DIR:-/root/.cache/huggingface/local/Qwen3.6-35B-A3B-GPTQ}" --dataset-name random --random-input-len "$3" \
      --random-output-len 128 --ignore-eos --num-prompts 3 --max-concurrency 1 --seed $r 2>&1 | tr -d '\r' \
      | grep -E "Output token throughput|Mean TTFT|Median TTFT" | sed "s/^/$2 prefill$3 r=$r /"
  done
}
"$W" stop vllm >/dev/null 2>&1; sleep 10
for round in 1 2 3; do for arm in "${ARMS[@]}"; do
  [ -f "$OUT/done_$arm-$round" ] && continue
  [ -f "$OUT/unsupported_$arm" ] && continue
  "$D/disk_gate.sh" check || { echo "STOP: disk gate"; exit 1; }
  # QUIETCHECK=1 (night of 2026-10-31): each arm-round starts only on a quiet PC (1-min WSL load < 4, no coai-exp
  # process); otherwise exit 3, and the driver waits for wait_quiet.sh and resumes.
  if [ "${QUIETCHECK:-0}" = 1 ]; then
    n=$(pgrep -f "coai-exp" | while read -r p; do grep -qa "keep-awake" "/proc/$p/cmdline" 2>/dev/null || echo "$p"; done | wc -l)
    l=$(cut -d' ' -f1 /proc/loadavg)
    if (( n > 0 )) || ! awk "BEGIN{exit !($l < 4)}"; then echo "NOT QUIET: $arm round $round (load $l, coai-exp $n)"; exit 3; fi
  fi
  name="vllmprtest-sp-$arm-$round"; read -r -a a <<< "$(arm_args "$arm" | tr '\n' ' ')"
  # fb1 (phase B1 end-to-end) = base with the merged BF16 draft MoE file next to the production int4 file.
  tf=/work/moe/tune/merged; [ "$arm" = fb1 ] && tf=/work/moe/tune/merged_b1
  mo=(); [ "$arm" = o7 ] && mo=(-v "$O7_SRC:$O7_DST:ro")
  [ "$arm" = ra3opt ] && mo=(-v "$RA_SRC:$RA_DST:ro")
  echo "=== $(date '+%F %T') $arm round $round (WSL load $(cut -d' ' -f1-3 /proc/loadavg)): ${a[*]}"
  if ! "$D/serve.sh" "$name" C "$OUT/server_$arm-$round.log" -e "VLLM_TUNED_CONFIG_FOLDER=$tf" "${mo[@]}" -- "${a[@]}"; then
    echo "RETRY: first start failed"; "$W" stop "$name" >/dev/null 2>&1; sleep 60
    if ! "$D/serve.sh" "$name" C "$OUT/server_$arm-$round.log" -e "VLLM_TUNED_CONFIG_FOLDER=$tf" "${mo[@]}" -- "${a[@]}"; then
      echo "UNSUPPORTED: $arm did not start twice"; grep -E "Error|error:" "$OUT/server_$arm-$round.log" | tail -3 | cut -c1-300
      "$W" stop "$name" >/dev/null 2>&1; touch "$OUT/unsupported_$arm"; sleep 20; continue
    fi
  fi
  grep -E "option 7: wide-N|fused-draft opt-in|Fused multi-step|Using configuration from|Using default MoE config|RDNAHybrid|GPU KV cache size|max_num_scheduled_tokens|Fused multi-step|attention backend|Cannot use ROCm" \
    "$OUT/server_$arm-$round.log" | cut -c1-240 | sort -u
  b0=$(cat "$OUT/bench.txt" 2>/dev/null | wc -l)  # bench lines before this arm-round
  "$D/bench.sh" "$name" "$arm-$round" | tee -a "$OUT/bench.txt"
  for pl in $PREFILL_LENS; do prefill "$name" "$arm-$round" "$pl" | tee -a "$OUT/bench.txt"; done
  "$W" exec "$name" python3 -c "import urllib.request;print(urllib.request.urlopen('http://127.0.0.1:8000/metrics').read().decode())" 2>&1 \
    | tr -d '\r' | grep -E '^vllm:spec_decode' | sed "s/^/$arm-$round /" >> "$OUT/spec_metrics.txt"
  # PREFIXTEST=1: the § 6.1 prefix-cache check, after the measurements (so it does not change them).
  [ "${PREFIXTEST:-0}" = 1 ] && "$W" exec "$name" python3 /work/speed/prefixtest.py http://127.0.0.1:8000 "$SERVED" \
    "$arm-$round" 2>&1 | tr -d '\r' | tail -2 | tee -a "$OUT/prefixtest.txt"
  if [ "$round" = 1 ]; then
    "$W" exec "$name" python3 /work/harness.py collect --url http://127.0.0.1:8000 --model "$SERVED" \
      --out "/work/speed/prompts_$arm.json" 2>&1 | tr -d '\r' | tail -1
    "$W" exec "$name" python3 /work/harness.py gsm8k --url http://127.0.0.1:8000 --model "$SERVED" \
      --data /work/gsm8k/test.jsonl --n 200 --out "/work/speed/gsm8k_$arm.json" 2>&1 | tr -d '\r' | tail -1
  fi
  # The arm-round counts as done only when its server still runs and all six decode bench lines exist (a stopped
  # container makes every later step fail fast; on 2026-10-31 that left a false done marker).
  alive=0; "$W" list --no-trunc 2>/dev/null | tr -d '\r' | grep -qE "[[:space:]]$name\$" && alive=1
  lines=$(tail -n +$((b0 + 1)) "$OUT/bench.txt" | grep -cE "^$arm-$round c=[14] r=[123] Output token throughput")
  "$W" logs "$name" 2>&1 | tr -d '\r' > "$OUT/server_$arm-$round.log"
  "$W" stop "$name" >/dev/null 2>&1
  if (( alive == 0 || lines < 6 )); then echo "LOST: $arm round $round (server alive $alive, bench lines $lines)"; exit 1; fi
  touch "$OUT/done_$arm-$round"; sleep 10
done; done
"$D/disk_gate.sh" check
echo "=== $(date '+%F %T') PHASE A DONE"
