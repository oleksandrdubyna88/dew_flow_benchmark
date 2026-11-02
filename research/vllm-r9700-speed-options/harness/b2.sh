#!/usr/bin/env bash
# b2.sh: phase B2 of the speed-options plan (option 4), started 2026-11-01 (operator: "phase B2 ... начинай").
# Full search was ~11 h for bs 24 alone (2,100 configs at ~19.5 s), so the operator chose the coarse search
# (2026-11-01 ~11:40): one chunk, int4-coarse-mid, keys 24, 32, 64, 128 on coarse_candidates.json (82 configs, as the
# coarse-largeM chunk of 2026-10-26), the Triton cache cleared after it; then merge_b2.py and the kernel A/B (moe_kernel_ab.sh with AB_RULE=b2) against today's production file.
# Prediction (plan, written before the runs): kernel −5..−20 % at those sizes; end-to-end c=1/c=4 unchanged
# (--max-num-seqs 4 keeps decode at M <= 16), so no end-to-end A/B. Each step runs in its own process group under the
# RAM guard (2 GB); no deadline (tuning loads no model weights). The production server is restored once, at the exit,
# with restore_args (fcombo).
set -u
D="$(cd "$(dirname "$0")" && pwd)"; W="/mnt/c/Program Files/WSL/wslc.exe"; IMG=rocm/vllm:rocm10.1.0_ubuntu24.04_py3.14-pytorch_2.13.0_vllm-0.29.0
M=/mnt/c/Users/strug/vllm/tmp/vllm-pr-work/work/moe
trap 'rm -f "$D/skip_restore"; "$D/restore_server.sh"' EXIT
guarded() {  # guarded <log> <cmd...>
  local log="$1"; shift
  setsid "$@" >> "$log" 2>&1 & local p=$!
  "$D/ram_guard_all.sh" "$p" 2048 >> "${log%.log}_ramguard.log" 2>&1 & local g=$!
  wait "$p"; local rc=$?; kill "$g" 2>/dev/null
  echo "$(date '+%F %T') $(basename "$log") $* rc=$rc"; return $rc
}
clear_cache() { "$W" run --rm --name vllmprtest-moe-clear -v "vllm-cache:/root/.cache/vllm" "$IMG" sh -c 'du -sh /root/.cache/vllm/triton-moe-tune 2>/dev/null; rm -rf /root/.cache/vllm/triton-moe-tune' 2>&1 | tr -d '\r' | sed 's/^/cache before clear: /'; }
echo "$(date '+%F %T') B2 START"
"$W" stop vllm >/dev/null 2>&1; sleep 10
for try in 1 2; do  # a RAM-guard stop is retried once; tune_chunk.sh deletes a half-written chunk
  CANDIDATES=/moe/coarse_candidates.json guarded "$D/b2_tune.log" "$D/tune_chunk.sh" int4-coarse-mid 24 32 64 128 && break
  echo "chunk int4-coarse-mid try $try failed"; [ "$try" = 2 ] && { echo "STOP: chunk int4-coarse-mid failed twice"; clear_cache; exit 1; }
done
grep -E "^chunk int4-coarse-mid " "$D/b2_tune.log" | tail -1
clear_cache
python3 -I "$D/merge_b2.py" "$M/tune" "$M/tune/merged_b2" int4-coarse-mid || { echo "STOP: merge failed"; exit 1; }
AB_DTYPE=int4_w4a16 AB_ACT= AB_A=/moe/tune/merged AB_B=/moe/tune/merged_b2 AB_RULE=b2 \
  AB_BS="8 16 20 24 32 48 64 96 128 192 256" guarded "$D/b2_kernel_ab.log" "$D/moe_kernel_ab.sh" "$D/speedB2k"
tail -1 "$D/b2_kernel_ab.log"
echo "$(date '+%F %T') B2 DONE"
