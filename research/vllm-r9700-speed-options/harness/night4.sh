#!/usr/bin/env bash
# night4.sh: run of 2026-10-31 18:xx (operator: "запускай тесты свои", "буду утром"). Same guards as night3.sh
# (RAM 2 GB, deadline 07:15, one restore at the exit; model loads wait for a quiet PC, other runs go on meanwhile):
#  1 phase B1 kernel A/B (moe_kernel_ab.sh, no model load); its decision adds the arm fb1 to the final A/B;
#  2 the final A/B in speedF (fbase-1 done; PREFIXTEST=1), at most 2 tries;
#  3 then night4_extra.sh, if it exists (later series: option 7, prefix caching off), on a quiet PC.
set -u
D="$(cd "$(dirname "$0")" && pwd)"; W="/mnt/c/Program Files/WSL/wslc.exe"
DEADLINE=$(date -d '07:15' +%s); (( DEADLINE < $(date +%s) )) && DEADLINE=$(date -d 'tomorrow 07:15' +%s); export DEADLINE
export PREFIXTEST=1
ARM_SECONDS=1500  # one arm-round with its server start, ~23 min, plus margin
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
quiet() { (( $(coai_count) == 0 )) && { timeout 900 "$D/wait_quiet.sh" >> "$D/wait_quiet_night4.log" 2>&1; local q=$?; echo "$(date '+%F %T') quiet check rc=$q"; return $q; }; }
echo "$(date '+%F %T') NIGHT4 START, deadline $(date -d "@$DEADLINE" '+%F %T'), coai-exp processes $(coai_count)"
[ -f "$D/speedB1k/decision.txt" ] || { guarded "$D/moe_kernel_ab.log" "$D/moe_kernel_ab.sh" "$D/speedB1k"; tail -1 "$D/moe_kernel_ab.log" > "$D/speedB1k/decision.txt"; }
cat "$D/speedB1k/decision.txt"
ARMS=(fbase fcombo); grep -q "B1 KERNEL: faster" "$D/speedB1k/decision.txt" && ARMS+=(fb1)
echo "final A/B arms: ${ARMS[*]}"
left() { local n=0; for r in 1 2 3; do for a in "${ARMS[@]}"; do [ -f "$D/speedF/done_$a-$r" ] || n=$((n+1)); done; done; echo $n; }
ab_tries=0
while (( $(left) > 0 && ab_tries < 2 )); do
  (( $(date +%s) + $(left) * ARM_SECONDS < DEADLINE )) || { echo "$(date '+%F %T') A/B: $(left) arm-rounds no longer fit"; break; }
  if quiet; then
    ab_tries=$((ab_tries + 1))
    guarded "$D/speed_F.log" "$D/speed_A.sh" "$D/speedF" "${ARMS[@]}"
    (( $(left) == 0 )) && echo "$(date '+%F %T') FINAL AB DONE"
  else
    timeout 300 tail -f /dev/null
  fi
done
if [ -x "$D/night4_extra.sh" ]; then
  until quiet || (( $(date +%s) + 3600 > DEADLINE )); do timeout 300 tail -f /dev/null; done
  (( $(date +%s) + 3600 < DEADLINE )) && guarded "$D/night4_extra.log" "$D/night4_extra.sh"
fi
echo "$(date '+%F %T') NIGHT4 DONE (A/B arm-rounds left: $(left), tries $ab_tries)"
