#!/usr/bin/env bash
# The end-to-end A/B of the "512" MoE entry, 2026-11-05. Approved by the operator, including the stop of production.
E=/mnt/c/Users/strug/vllm/tmp/vllm-pr-work/work/moe512/e2e
cd /home/jinx/git/wt/r9700-accept || exit 2
export R9700_ARMS_CONF=$E/arms.conf R9700_QUIET_CHECKS=1
bin/r9700 series $E/qwen3.6-35b-a3b-gptq-m512.env ta3mb ta3mb512 --rounds 3 --out e2e512
echo "SERIES-EXIT $? $(date -Is)"
