#!/usr/bin/env bash
# prodcheck.sh <outdir>: the TTFT stall check against the operator's running server ('vllm'), no restart. Same bench as
# a phase A arm (bench.sh; prefill 8192/128 and 32768/128 at c=1, 3 reps), plus every request's TTFT from
# --save-detailed, to see which request waits ~12 s longer and whether the stall also happens outside the test containers.
set -u
OUT="$1"; mkdir -p "$OUT"; D="$(dirname "$0")"; W="/mnt/c/Program Files/WSL/wslc.exe"
"$W" list --no-trunc 2>/dev/null | tr -d '\r' | grep -qE '\svllm$' || { echo "STOP: the operator's server does not run"; exit 1; }
echo "=== $(date '+%F %T') prodcheck (WSL load $(cut -d' ' -f1-3 /proc/loadavg))"
"$D/bench.sh" vllm prod | tee -a "$OUT/bench.txt"
for len in 8192 32768; do for r in 1 2 3; do
  f="/tmp/prodcheck/p$len-$r.json"
  "$W" exec vllm vllm bench serve --backend vllm --base-url http://127.0.0.1:8000 --model qwen3.6-35b-a3b \
    --tokenizer /root/.cache/huggingface/local/Qwen3.6-35B-A3B-GPTQ --dataset-name random --random-input-len "$len" \
    --random-output-len 128 --ignore-eos --num-prompts 3 --max-concurrency 1 --seed $r \
    --save-result --save-detailed --result-dir /tmp/prodcheck --result-filename "p$len-$r.json" > /dev/null 2>&1
  "$W" exec vllm python3 -c "import json;d=json.load(open('$f'));print('prod prefill$len r=$r ttfts_s', [round(x,2) for x in d['ttfts']], 'mean', round(d['mean_ttft_ms']/1000,2), 'median', round(d['median_ttft_ms']/1000,2), 'out_tok_s', round(d['output_throughput'],2))" 2>&1 | tr -d '\r'
done; done | tee -a "$OUT/bench.txt"
echo "=== $(date '+%F %T') PRODCHECK DONE"
