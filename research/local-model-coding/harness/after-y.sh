#!/usr/bin/env bash
# Part 5b (owner, 2026-10-27): S and D again, four each, with ONE change — after Haiku's code, a FRESH Haiku session reviews
# the whole stories and fixes what it found (hr-review-fix), and only then the Opus review and the Haiku fixes, as before.
# SR = Sonnet splits, Haiku codes each task, Haiku reviews+fixes, Opus checks, Haiku fixes; DR = Haiku codes the stories,
# Haiku reviews+fixes, Opus checks, Haiku fixes. Four rounds of two (SR_n with DR_n), a guard keeping Windows awake, then the
# objective checks, Haiku's survival (SR), the absolute judge (new arms only), the frozen mutation checks on the new arms,
# the metrics. Re-runnable: markers in chain/.
set -u
branch_of() { case $1 in SR*) echo "exp/kimi-e56-sonnet-split-haiku-review-${1#SR}" ;; DR*) echo "exp/kimi-e56-haiku-direct-review-${1#DR}" ;; esac; }
E=/home/jinx/git/coai-exp; M=$E/chain; mkdir -p "$M"
log() { echo "$(date -u +%FT%TZ) $*"; }
arm_done() { grep -q '"kind":"arm-done"' "$E/results/$1/timing.jsonl" 2>/dev/null; }
alive() { [ -f "$E/results/$1/run.pid" ] && kill -0 "$(cat "$E/results/$1/run.pid")" 2>/dev/null; }
awake_guard() { # every 2 min: a keep-awake must be running
  while :; do
    if ! timeout 30 pwsh.exe -NoProfile -ExecutionPolicy Bypass -File 'C:\Users\strug\AppData\Local\Temp\coai-exp\check-keep-awake.ps1' >/dev/null 2>&1; then
      setsid nohup pwsh.exe -NoProfile -ExecutionPolicy Bypass -File 'C:\Users\strug\AppData\Local\Temp\coai-exp\keep-awake.ps1' >> "$E/keep-awake.log" 2>&1 < /dev/null &
    fi
    sleep 120
  done
}
awake_guard & GUARD=$!
trap 'kill $GUARD 2>/dev/null' EXIT
ARMS="SR1 DR1 SR2 DR2 SR3 DR3 SR4 DR4"
for batch in "SR1 DR1" "SR2 DR2" "SR3 DR3" "SR4 DR4"; do
  for a in $batch; do
    [ -f "$M/made-$a" ] && continue
    rm -rf "${E:?}/arm-$a"; cp -a "$E/base" "$E/arm-$a"; git -C "$E/arm-$a" checkout -q -b "$(branch_of $a)"
    (cd "$E/arm-$a" && node .agents/conventions/tools/low-priority.mjs dotnet build dew_flow_connect_other_ais.slnx -c Debug -m:4 \
       --disable-build-servers > "$E/prep-build-$a.log" 2>&1) || { log "warm build of $a failed"; exit 3; }
    touch "$M/made-$a"; log "arm $a made and built"
  done
  start=""; for a in $batch; do arm_done $a || alive $a || start="$start $a"; done
  [ -n "$start" ] && { "$E/run-all.sh" $start; log "started:$start"; }
  for a in $batch; do
    until arm_done $a; do alive $a || { log "arm $a stopped without finishing — resume it (run-all.sh $a), then start me again"; exit 2; }; sleep 30; done
    log "arm $a done"
  done
done
for a in $ARMS; do
  [ -f "$M/objective-$a" ] && continue
  "$E/objective-checks.sh" $a > "$E/results/$a/objective.out" 2>&1
  case $a in SR*) node "$E/t-survival.mjs" $a > "$E/results/$a/t-survival.out" 2>&1 ;; *) echo "no tasks" > "$E/results/$a/t-survival.out" ;; esac
  touch "$M/objective-$a"; log "checks of $a: $(tail -1 "$E/results/$a/objective.out") | $(tail -1 "$E/results/$a/t-survival.out")"
done
if [ ! -f "$M/judged-abs-y" ]; then "$E/judge-abs.sh" > "$E/judge-abs-y.out" 2>&1; touch "$M/judged-abs-y"; log "absolute judge (SR, DR arms) done"; fi
set -- $ARMS   # the frozen mutation checks, two arms at a time (mut-all.sh's way)
while [ $# -gt 0 ]; do
  pair="$1 ${2:-}"; shift; [ $# -gt 0 ] && shift
  pids=""
  for a in $pair; do
    [ -s "$E/results/$a/mutation.json" ] && grep -q '"M6-' "$E/results/$a/mutation.json" && continue
    ( node "$E/mutate.mjs" "$a" > "$E/results/$a/mutation.out" 2>&1; log "mutation $a: $(tail -1 "$E/results/$a/mutation.out")" ) &
    pids="$pids $!"
  done
  [ -n "$pids" ] && wait $pids   # a bare wait also waits for the awake guard, which never ends
done
node "$E/metrics.mjs" > "$E/metrics.txt" 2>&1; node "$E/metrics.mjs" --json > "$E/metrics.json" 2>&1
kill $GUARD 2>/dev/null
timeout 60 pwsh.exe -NoProfile -ExecutionPolicy Bypass -File 'C:\Users\strug\AppData\Local\Temp\coai-exp\stop-keep-awake.ps1' >> "$E/chain.log" 2>&1
touch "$M/y-done"; log "Y DONE"
