#!/usr/bin/env bash
# The rest of the experiment, detached: after V, C and C2 finish, make and start arm VT (alone on the GPU), run the
# objective checks of V, C and C2 while VT works, then VT's checks, the blind judge over every arm, and the metrics.
# Re-runnable: a finished stage leaves a marker in chain/ and is skipped.   Start: setsid nohup after-three.sh &
set -u
E=/home/jinx/git/coai-exp; M=$E/chain; mkdir -p "$M"
log() { echo "$(date -u +%FT%TZ) $*"; }
arm_done() { grep -q '"kind":"arm-done"' "$E/results/$1/timing.jsonl" 2>/dev/null; }
alive() { [ -f "$E/results/$1/run.pid" ] && kill -0 "$(cat "$E/results/$1/run.pid")" 2>/dev/null; }

for a in V C C2; do
  until arm_done $a; do
    alive $a || { log "arm $a stopped without finishing — resume it (run-all.sh $a), then start me again"; exit 2; }
    sleep 30
  done
done
log "V, C and C2 are done"

if [ ! -f "$M/vt-made" ]; then
  rm -rf "$E/arm-VT"; cp -a "$E/base" "$E/arm-VT"; git -C "$E/arm-VT" checkout -q -b exp/kimi-e56-vllm-think
  (cd "$E/arm-VT" && node .agents/conventions/tools/low-priority.mjs dotnet build dew_flow_connect_other_ais.slnx -c Debug -m:4 \
     --disable-build-servers > "$E/prep-build-VT.log" 2>&1) || { log "VT warm build failed — see prep-build-VT.log"; exit 3; }
  touch "$M/vt-made"; log "arm VT made and built"
fi
if [ ! -f "$M/vt-started" ]; then
  "$E/run-all.sh" VT; touch "$M/vt-started"; log "arm VT started"
fi
for a in V C C2; do
  [ -f "$M/objective-$a" ] && continue
  "$E/objective-checks.sh" $a > "$E/results/$a/objective.out" 2>&1; touch "$M/objective-$a"; log "objective checks of $a: $(tail -1 "$E/results/$a/objective.out")"
done
until arm_done VT; do
  alive VT || { log "arm VT stopped without finishing — resume it (run-all.sh VT), then start me again"; exit 2; }
  sleep 30
done
log "VT is done"
if [ ! -f "$M/objective-VT" ]; then
  "$E/objective-checks.sh" VT > "$E/results/VT/objective.out" 2>&1; touch "$M/objective-VT"; log "objective checks of VT: $(tail -1 "$E/results/VT/objective.out")"
fi
if [ ! -f "$M/judged" ]; then
  "$E/judge.sh" > "$E/judge.out" 2>&1; touch "$M/judged"; log "judge: $(tr '\n' ' ' < "$E/judge.out")"
fi
node "$E/metrics.mjs" > "$E/metrics.txt" 2>&1; node "$E/metrics.mjs" --json > "$E/metrics.json" 2>&1
touch "$M/all-done"; log "ALL DONE"
