#!/usr/bin/env bash
# bench.sh <container> <tag>: vllm bench serve, random 1024/512, concurrency 1 (6 prompts) and 4 (12 prompts), 3 reps each.
set -u
name="$1" tag="$2"; W="/mnt/c/Program Files/WSL/wslc.exe"; D="$(dirname "$0")"
for c in 1 4; do for r in 1 2 3; do
  touch "$D/session.heartbeat"; n=$(( c==1 ? 6 : 12 ))
  "$W" exec "$name" vllm bench serve --backend vllm --base-url http://127.0.0.1:8000 --model qwen3.6-35b-a3b \
    --tokenizer /root/.cache/huggingface/local/Qwen3.6-35B-A3B-GPTQ --dataset-name random --random-input-len 1024 \
    --random-output-len 512 --ignore-eos --num-prompts $n --max-concurrency $c --seed $r 2>&1 | tr -d '\r' \
    | grep -E "Output token throughput|Mean TTFT|Mean TPOT" | sed "s/^/$tag c=$c r=$r /"
done; done
