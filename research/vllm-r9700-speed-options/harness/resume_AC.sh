#!/usr/bin/env bash
# resume_AC.sh: finish phase A (finished arm-rounds are skipped), then phase C+B1; each under the RAM guard (2 GB, operator decision 2026-10-28).
set -u
D="$(cd "$(dirname "$0")" && pwd)"
guarded() {  # guarded <log> <guardlog> <cmd...>
  local log="$1" glog="$2"; shift 2
  "$@" > "$log" 2>&1 & local p=$!
  "$D/ram_guard_all.sh" "$p" 2048 > "$glog" 2>&1 & local g=$!
  wait "$p"; local rc=$?; kill "$g" 2>/dev/null
  echo "$(date '+%F %T') $(basename "$log") rc=$rc"; return $rc
}
guarded "$D/speed_A_resume.log" "$D/ram_guard_A2.log" "$D/speed_A.sh" "$D/speedA" || { echo "STOP after phase A"; exit 1; }
guarded "$D/speed_CB.log" "$D/ram_guard_CB.log" "$D/speed_CB.sh" || { echo "STOP after phase C+B1"; exit 1; }
echo "$(date '+%F %T') RESUME DONE"
