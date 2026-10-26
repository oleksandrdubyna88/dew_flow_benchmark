#!/usr/bin/env bash
# The O, D and S arms, detached (owner, 2026-10-26): O = Opus splits into tasks, Haiku codes each, Opus checks the whole
# stories once, Haiku fixes; D = Haiku codes the story directly, Opus checks, Haiku fixes; S = as O, with Sonnet splitting.
# Four rounds of three — one arm of each scheme per round, so every scheme meets the same time of day and machine load —
# a guard keeping Windows awake, then the objective checks, Haiku's survival (O, S), the absolute judge for the new arms
# only, the metrics. Re-runnable: markers in chain/.
set -u
branch_of() { case $1 in O*) echo "exp/kimi-e56-opus-split-haiku-${1#O}" ;; S*) echo "exp/kimi-e56-sonnet-split-haiku-${1#S}" ;; D*) echo "exp/kimi-e56-haiku-direct-${1#D}" ;; esac; }
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
for batch in "O1 D1 S1" "O2 D2 S2" "O3 D3 S3" "O4 D4 S4"; do
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
for a in O1 D1 S1 O2 D2 S2 O3 D3 S3 O4 D4 S4; do
  [ -f "$M/objective-$a" ] && continue
  "$E/objective-checks.sh" $a > "$E/results/$a/objective.out" 2>&1
  case $a in O*|S*) node "$E/t-survival.mjs" $a > "$E/results/$a/t-survival.out" 2>&1 ;; *) echo "no tasks" > "$E/results/$a/t-survival.out" ;; esac
  touch "$M/objective-$a"; log "checks of $a: $(tail -1 "$E/results/$a/objective.out") | $(tail -1 "$E/results/$a/t-survival.out")"
done
if [ ! -f "$M/judged-abs-x" ]; then "$E/judge-abs.sh" > "$E/judge-abs-x.out" 2>&1; touch "$M/judged-abs-x"; log "absolute judge (O, D, S arms) done"; fi
node "$E/metrics.mjs" > "$E/metrics.txt" 2>&1; node "$E/metrics.mjs" --json > "$E/metrics.json" 2>&1
kill $GUARD 2>/dev/null
timeout 60 pwsh.exe -NoProfile -ExecutionPolicy Bypass -File 'C:\Users\strug\AppData\Local\Temp\coai-exp\stop-keep-awake.ps1' >> "$E/chain.log" 2>&1
touch "$M/x-done"; log "X DONE"
