#!/usr/bin/env bash
# night2.sh: the rest of the 2026-10-28 night after night.sh stopped (RAM guard at the first A/B model load, 22:26, while
# coai-exp2 ran ~30 processes at load ~18). Model loads wait for a quiet machine (operator decision: wait until coai-exp
# is done); runs that load no model go on meanwhile. In order:
#  1 phase C (no model load);
#  2 loop: when the final A/B is not done, coai-exp is gone, wait_quiet confirms it and the A/B still ends before the
#    deadline: run the A/B (resumable, at most 2 tries); else run the next B1 chunk that fits; else wait 5 min.
# Each step runs under the 2 GB RAM guard and the deadline guard; the server is restored once, at the exit.
set -u
D="$(cd "$(dirname "$0")" && pwd)"; W="/mnt/c/Program Files/WSL/wslc.exe"
DEADLINE=$(date -d '07:15' +%s); (( DEADLINE < $(date +%s) )) && DEADLINE=$(date -d 'tomorrow 07:15' +%s); export DEADLINE
AB_SECONDS=9000  # 6 arm-rounds of ~23 min plus margin
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
declare -A EST=([draft-coarse-largeM]=1500 [draft-bs-1]=3500 [draft-bs-2]=3500 [draft-bs-4]=5000 [draft-bs-12]=8500)
CHUNKS=(draft-coarse-largeM draft-bs-1 draft-bs-2 draft-bs-4 draft-bs-12)
declare -A FAILED=()  # a chunk that stopped once is not run again tonight
next_chunk() { for c in "${CHUNKS[@]}"; do chunk_done "$c" && continue; [ -n "${FAILED[$c]:-}" ] && continue; (( $(date +%s) + EST[$c] < DEADLINE )) && echo "$c"; return; done; }
echo "$(date '+%F %T') NIGHT2 START, deadline $(date -d "@$DEADLINE" '+%F %T')"
guarded "$D/speed_CB_night.log" env ONLY=none "$D/speed_CB.sh"
ab_tries=0
while true; do
  now=$(date +%s)
  if ! ab_done && (( ab_tries < 2 && now + AB_SECONDS < DEADLINE )) && (( $(coai_count) == 0 )); then
    timeout 900 "$D/wait_quiet.sh" >> "$D/wait_quiet_night.log" 2>&1; q=$?
    echo "$(date '+%F %T') quiet check rc=$q"
    if (( q == 0 )); then
      ab_tries=$((ab_tries + 1))
      guarded "$D/speed_F.log" "$D/speed_A.sh" "$D/speedF" fbase fcombo && echo "$(date '+%F %T') FINAL AB DONE"
      continue
    fi
  fi
  c=$(next_chunk)
  if [ -n "$c" ]; then guarded "$D/speed_CB_night.log" env ONLY="$c" "$D/speed_CB.sh" || { echo "chunk $c stopped"; FAILED[$c]=1; }; continue; fi
  if ! ab_done && (( ab_tries < 2 && now + AB_SECONDS < DEADLINE )); then timeout 300 tail -f /dev/null; continue; fi
  break
done
echo "$(date '+%F %T') NIGHT2 DONE (A/B done: $(ab_done && echo yes || echo no), tries $ab_tries)"
