#!/usr/bin/env bash
# The T arms, detached: Opus splits into tasks, Haiku codes each task, Sonnet reviews each task (t-lib.sh), then the same
# Opus review and Sonnet fixes as every arm. Two arms at a time (T1+T2, then T3+T4: the system NVMe logged bad blocks under
# 3 parallel arms, and each T arm adds parallel worktree builds), a guard keeping Windows awake, then the objective checks
# and Haiku's survival per arm, the absolute judge for the new arms only, the metrics. Re-runnable: markers in chain/.
set -u
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
for batch in "T1 T2" "T3 T4"; do
  for a in $batch; do
    [ -f "$M/made-$a" ] && continue
    rm -rf "${E:?}/arm-$a"; cp -a "$E/base" "$E/arm-$a"; git -C "$E/arm-$a" checkout -q -b "exp/kimi-e56-tasks-${a#T}"
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
for a in T1 T2 T3 T4; do
  [ -f "$M/objective-$a" ] && continue
  "$E/objective-checks.sh" $a > "$E/results/$a/objective.out" 2>&1
  node "$E/t-survival.mjs" $a > "$E/results/$a/t-survival.out" 2>&1
  touch "$M/objective-$a"; log "checks of $a: $(tail -1 "$E/results/$a/objective.out") | $(tail -1 "$E/results/$a/t-survival.out")"
done
if [ ! -f "$M/judged-abs-t" ]; then "$E/judge-abs.sh" > "$E/judge-abs-t.out" 2>&1; touch "$M/judged-abs-t"; log "absolute judge (T arms) done"; fi
node "$E/metrics.mjs" > "$E/metrics.txt" 2>&1; node "$E/metrics.mjs" --json > "$E/metrics.json" 2>&1
kill $GUARD 2>/dev/null
timeout 60 pwsh.exe -NoProfile -ExecutionPolicy Bypass -File 'C:\Users\strug\AppData\Local\Temp\coai-exp\stop-keep-awake.ps1' >> "$E/chain.log" 2>&1
touch "$M/t-done"; log "T DONE"
