#!/usr/bin/env bash
# night9.sh: 2026-11-01 22:45 — speed27 only, after night8.sh used its 6 tries: the RAM guard stopped the first
# 27B load at 22:36:29 (195 MB available) and the retry 23 s later at 22:36:57 (575 MB), before the RAM was back.
# Change against night8.sh: before every try, wait (not a failed try) until no vllmprtest container is listed and
# Windows available RAM is >= 24 GB, with a 20 min ceiling (then it counts as a failed try). Up to 2 failed tries (every load now dips to ~100 MB available, production too: bounded attempt).
# night8.sh: 2026-11-01 18:10, operator: "The GPU series: no progress запускай" — night7.sh without the quiet-PC gate
# (it had waited since 12:03 at WSL load 13-24). Each arm-round still logs the WSL load; the RAM guard stays.
# night7.sh: 2026-11-01, operator: "Option C", "давай протестим все". Waits until b2.sh has ended, then:
#  speedRA  — MTP 3 on ROCM_ATTN (ra3), the same with the fused-draft opt-in patch (ra3opt), on TRITON_ATTN (ta3 =
#             fcombo) and TRITON_ATTN + --max-num-batched-tokens 8192 (ta3mb); 3 rounds;
#  speed27  — Qwen3.8-27B-AWQ with the start-script defaults before (q27old: MTP 2, default backend) and after
#             2026-11-01 (q27new: MTP 3 + TRITON_ATTN); --max-model-len 16384, --max-num-seqs 16, prefill 8k only.
# Predictions, written before the run: ra3opt prompts token-identical to ra3 (same kernel, only the metadata path
# changes); ra3opt faster than ra3 by a part of the ta3 gain; ta3mb: TTFT 8k/32k lower than ta3, decode within
# ~4 % (as mnbt8k in phase A); q27new: unknown (not measured on this model).
# Mechanics as night5.sh (below).
# night5.sh: restart of 2026-10-31 ~22:00 after night4.sh ended at 21:43 (3 RAM-guard stops used up its 2 tries).
# Changes against night4.sh: each step runs in its own process group (setsid), so the RAM guard and the deadline stop
# the whole group (night4's guard left an orphan speed_A.sh); speed_A.sh runs with QUIETCHECK=1 (exit 3 = PC not
# quiet: wait, do not count a try) and writes a done marker only for a complete arm-round; up to 6 failed tries.
# Series in order: speedF (final A/B: fbase fcombo fb1, PREFIXTEST=1), then speedO7 (option 7: o7ref o7).
# Same guards: RAM 2 GB, deadline 07:15, the production server restored once, at the exit.
set -u
D="$(cd "$(dirname "$0")" && pwd)"; W="/mnt/c/Program Files/WSL/wslc.exe"
DEADLINE=$(date -d '07:15' +%s); (( DEADLINE < $(date +%s) )) && DEADLINE=$(date -d 'tomorrow 07:15' +%s); export DEADLINE
export QUIETCHECK=0  # night8/9: operator decision, no quiet-PC gate
ARM_SECONDS=1500
SERIES=("speed27:q27old q27new:0:MODEL_DIR=/root/.cache/huggingface/local/Qwen3.8-27B-AWQ SERVED=qwen3.8-27b MAXSEQS=16 MAXLEN=16384 PREFILL_LENS=8192")  # outdir:arms:PREFIXTEST:env
trap 'rm -f "$D/skip_restore"; "$D/restore_server.sh"' EXIT
stop_tests() { for c in $("$W" list --no-trunc 2>/dev/null | tr -d '\r' | awk 'NR>1{print $NF}' | grep -E '^vllmprtest-' || true); do "$W" stop "$c" >/dev/null 2>&1; done; }
guarded() {  # guarded <log> <cmd...>: <cmd> leads its own process group
  local log="$1"; shift
  touch "$D/skip_restore"
  setsid "$@" >> "$log" 2>&1 & local p=$!
  "$D/ram_guard_all.sh" "$p" 2048 >> "${log%.log}_ramguard.log" 2>&1 & local g=$!
  ( while kill -0 "$p" 2>/dev/null; do
      if (( $(date +%s) >= DEADLINE )); then echo "$(date '+%T') DEADLINE: stopping tests" >> "$log"; stop_tests; kill -TERM -- -"$p"; exit 0; fi
      sleep 30
    done ) & local t=$!
  wait "$p"; local rc=$?; kill "$g" "$t" 2>/dev/null
  echo "$(date '+%F %T') $(basename "$log") $* rc=$rc"; return $rc
}
coai_count() { pgrep -f "coai-exp" | while read -r p; do grep -qa "keep-awake" "/proc/$p/cmdline" 2>/dev/null || echo "$p"; done | wc -l; }
avail_mb() { powershell.exe -NoProfile -Command '[int](Get-Counter "\Memory\Available MBytes").CounterSamples[0].CookedValue' 2>/dev/null | tr -d '\r\n '; }
settled() {  # settled: 0 when no test container is left and RAM is back (>= 24 GB), 1 after 20 min
  local end=$(( $(date +%s) + 1200 )) mb
  while (( $(date +%s) < end )); do
    mb=$(avail_mb)
    if ! "$W" list --no-trunc 2>/dev/null | tr -d '\r' | grep -q 'vllmprtest-' && [[ "$mb" =~ ^[0-9]+$ ]] && (( mb >= 24000 )); then
      echo "$(date '+%F %T') settled: no test container, available RAM $mb MB"; return 0; fi
    sleep 30
  done
  echo "$(date '+%F %T') not settled after 20 min (available RAM ${mb:-?} MB)"; return 1
}
quiet() { echo "$(date '+%F %T') quiet gate off (operator), load $(cut -d' ' -f1 /proc/loadavg)"; return 0; }
left() { local n=0 a; for r in 1 2 3; do for a in $2; do [ -f "$D/$1/done_$a-$r" ] || n=$((n+1)); done; done; echo $n; }
"$W" stop vllm >/dev/null 2>&1
echo "$(date '+%F %T') NIGHT9 START, deadline $(date -d "@$DEADLINE" '+%F %T'), coai-exp processes $(coai_count)"
fails=0
for s in "${SERIES[@]}"; do
  IFS=: read -r out arms pt envs <<< "$s"
  while (( $(left "$out" "$arms") > 0 )); do
    (( fails < 2 )) || { echo "$(date '+%F %T') STOP: 2 failed tries"; break 2; }
    (( $(date +%s) + $(left "$out" "$arms") * ARM_SECONDS < DEADLINE )) || { echo "$(date '+%F %T') $out: $(left "$out" "$arms") arm-rounds no longer fit"; break 2; }
    if ! settled; then fails=$((fails + 1)); echo "failed tries: $fails"; continue; fi
    if ! quiet; then timeout 300 tail -f /dev/null; continue; fi
    # shellcheck disable=SC2086
    guarded "$D/speed_night9.log" env PREFIXTEST="$pt" $envs "$D/speed_A.sh" "$D/$out" $arms; rc=$?
    case $rc in 0) ;; 3) echo "$(date '+%F %T') not quiet at an arm-round start, waiting";; *) fails=$((fails + 1)); echo "failed tries: $fails";; esac
  done
  (( $(left "$out" "$arms") == 0 )) && echo "$(date '+%F %T') SERIES $out DONE"
done
echo "$(date '+%F %T') NIGHT9 DONE (failed tries $fails)"
