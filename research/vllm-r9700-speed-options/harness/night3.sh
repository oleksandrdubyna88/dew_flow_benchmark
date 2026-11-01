#!/usr/bin/env bash
# night3.sh: resume of 2026-10-30 00:10 (operator: "продолжай тесты"), after the pause of 2026-10-28 23:35. coai-exp2
# runs again, so model loads wait for a quiet PC (operator decision); runs that load no model go on meanwhile:
#  1 phase C2: linear_bench_graph.py (CUDA-graph timer, § 11.3 of the results);
#  2 loop: the final A/B (fbase-1 done; arms with the PREFIXTEST check) when coai-exp is gone, wait_quiet confirms it and
#    the A/B still ends before the deadline (at most 2 tries); else the next B1 chunk that fits; else wait 5 min.
# Each step runs under the 2 GB RAM guard and the deadline guard; the server is restored once, at the exit.
set -u
D="$(cd "$(dirname "$0")" && pwd)"; W="/mnt/c/Program Files/WSL/wslc.exe"; IMG=rocm/vllm:rocm10.1.0_ubuntu24.04_py3.14-pytorch_2.13.0_vllm-0.29.0
WT='C:\Users\strug\vllm\tmp\vllm-pr-work'; SP=/mnt/c/Users/strug/vllm/tmp/vllm-pr-work/work/speed
DEADLINE=$(date -d '07:15' +%s); (( DEADLINE < $(date +%s) )) && DEADLINE=$(date -d 'tomorrow 07:15' +%s); export DEADLINE
export PREFIXTEST=1
AB_SECONDS=8000  # 5 arm-rounds of ~23 min plus margin
trap 'rm -f "$D/skip_restore"; "$D/restore_server.sh"' EXIT
stop_tests() { for c in $("$W" list --no-trunc 2>/dev/null | tr -d '\r' | awk 'NR>1{print $NF}' | grep -E '^vllmprtest-' || true); do "$W" stop "$c" >/dev/null 2>&1; done; }
guarded() {  # guarded <log> <cmd...>
  local log="$1"; shift
  touch "$D/skip_restore"
  "$@" >> "$log" 2>&1 & local p=$!
  "$D/ram_guard_all.sh" "$p" 2048 >> "${log%.log}_ramguard.log" 2>&1 & local g=$!
  ( while kill -0 "$p" 2>/dev/null; do
      if (( $(date +%s) >= DEADLINE )); then echo "$(date '+%T') DEADLINE: stopping tests" >> "$log"; stop_tests; kill -TERM "$p"; exit 0; fi
      sleep 30
    done ) & local t=$!
  wait "$p"; local rc=$?; kill "$g" "$t" 2>/dev/null
  echo "$(date '+%F %T') $(basename "$log") $* rc=$rc"; return $rc
}
coai_count() { pgrep -f "coai-exp" | while read -r p; do grep -qa "keep-awake" "/proc/$p/cmdline" 2>/dev/null || echo "$p"; done | wc -l; }
ab_done() { [ -f "$D/speedF/done_fbase-3" ] && [ -f "$D/speedF/done_fcombo-3" ]; }
chunk_done() { ls /mnt/c/Users/strug/vllm/tmp/vllm-pr-work/work/moe/tune/"$1"/E=*.json >/dev/null 2>&1; }
declare -A EST=([draft-bs-4]=1200 [draft-bs-12]=2400)  # BF16 chunks took ~300 s on 2026-10-28; margin for larger bs
CHUNKS=(draft-bs-4 draft-bs-12)
declare -A FAILED=()  # a chunk that stopped once is not run again in this run
next_chunk() { for c in "${CHUNKS[@]}"; do chunk_done "$c" && continue; [ -n "${FAILED[$c]:-}" ] && continue; (( $(date +%s) + EST[$c] < DEADLINE )) && echo "$c"; return; done; }
phase_c2() {
  "$W" stop vllm >/dev/null 2>&1
  "$D/disk_gate.sh" check || return 1
  timeout 5400 "$W" run --rm --name vllmprtest-linear2 --gpus all --shm-size 16g -e HIP_VISIBLE_DEVICES=0 \
    -e TRITON_CACHE_DIR=/root/.cache/vllm/triton-linear -v "vllm-cache:/root/.cache/vllm" -v "$WT\\work:/work" "$IMG" \
    python3 /work/speed/linear_bench_graph.py /work/speed/linear_bench_graph.json > "$SP/linear_bench_graph.log" 2>&1
  local rc=$?; echo "phase C2 rc=$rc rows=$(grep -c '^{' "$SP/linear_bench_graph.log")"; grep -E "Error|Traceback" "$SP/linear_bench_graph.log" | tail -3
  "$D/disk_gate.sh" check; return $rc
}
echo "$(date '+%F %T') NIGHT3 START, deadline $(date -d "@$DEADLINE" '+%F %T'), coai-exp processes $(coai_count)"
[ -s "$SP/linear_bench_graph.json" ] || guarded "$D/phaseC2.log" phase_c2 || echo "phase C2 failed, the run continues"
ab_tries=0
while true; do
  now=$(date +%s)
  if ! ab_done && (( ab_tries < 2 && now + AB_SECONDS < DEADLINE )) && (( $(coai_count) == 0 )); then
    timeout 900 "$D/wait_quiet.sh" >> "$D/wait_quiet_night3.log" 2>&1; q=$?
    echo "$(date '+%F %T') quiet check rc=$q"
    if (( q == 0 )); then
      ab_tries=$((ab_tries + 1))
      guarded "$D/speed_F.log" "$D/speed_A.sh" "$D/speedF" fbase fcombo && echo "$(date '+%F %T') FINAL AB DONE"
      continue
    fi
  fi
  c=$(next_chunk)
  if [ -n "$c" ]; then guarded "$D/speed_CB_night3.log" env ONLY="$c" "$D/speed_CB.sh" || { echo "chunk $c stopped"; FAILED[$c]=1; }; continue; fi
  if ! ab_done && (( ab_tries < 2 && now + AB_SECONDS < DEADLINE )); then timeout 300 tail -f /dev/null; continue; fi
  break
done
echo "$(date '+%F %T') NIGHT3 DONE (A/B done: $(ab_done && echo yes || echo no), tries $ab_tries)"
