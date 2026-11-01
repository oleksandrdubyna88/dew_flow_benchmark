#!/usr/bin/env bash
# night.sh: the night run of 2026-10-28 (operator: "запускай тесты, делай что нужно. буду утром"). In order:
#  1 prodcheck against the operator's server (no restart);
#  2 wait up to 60 min for a quiet machine (coai-exp), then the final A/B: fbase vs fcombo (mtp3 + TRITON_ATTN), 3 rounds;
#  3 phase C and the B1 chunks that end before DEADLINE.
# Each step runs under the 2 GB RAM guard (operator decision 2026-10-28) and the deadline guard. The steps' own
# restores are skipped; this script restores the operator's server once, at its exit (restore_args adds kept options).
set -u
D="$(cd "$(dirname "$0")" && pwd)"; W="/mnt/c/Program Files/WSL/wslc.exe"
export DEADLINE; DEADLINE=$(date -d 'tomorrow 07:15' +%s)
trap 'rm -f "$D/skip_restore"; "$D/restore_server.sh"' EXIT
stop_tests() { for c in $("$W" list --no-trunc 2>/dev/null | tr -d '\r' | awk 'NR>1{print $NF}' | grep -E '^vllmprtest-' || true); do "$W" stop "$c" >/dev/null 2>&1; done; }
guarded() {  # guarded <log> <cmd...>
  local log="$1"; shift
  touch "$D/skip_restore"
  "$@" > "$log" 2>&1 & local p=$!
  "$D/ram_guard_all.sh" "$p" 2048 > "${log%.log}_ramguard.log" 2>&1 & local g=$!
  ( while kill -0 "$p" 2>/dev/null; do
      if (( $(date +%s) >= DEADLINE )); then echo "$(date '+%T') DEADLINE: stopping tests" >> "$log"; stop_tests; kill -TERM "$p"; exit 0; fi
      sleep 30
    done ) & local t=$!
  wait "$p"; local rc=$?; kill "$g" "$t" 2>/dev/null
  echo "$(date '+%F %T') $(basename "$log") rc=$rc"; return $rc
}
echo "$(date '+%F %T') NIGHT START, deadline $(date -d "@$DEADLINE" '+%F %T')"
guarded "$D/prodcheck.log" "$D/prodcheck.sh" "$D/prodcheck" || echo "prodcheck failed, the night continues"
timeout 3600 "$D/wait_quiet.sh" > "$D/wait_quiet_night.log" 2>&1
echo "$(date '+%F %T') quiet wait rc=$? (124: not quiet after 60 min, the A/B runs anyway; load is in each arm line)"
guarded "$D/speed_F.log" "$D/speed_A.sh" "$D/speedF" fbase fcombo || { echo "STOP after the final A/B"; exit 1; }
echo "$(date '+%F %T') FINAL AB DONE"
guarded "$D/speed_CB_night.log" "$D/speed_CB.sh" || { echo "STOP after phase C + B1"; exit 1; }
echo "$(date '+%F %T') NIGHT DONE"
