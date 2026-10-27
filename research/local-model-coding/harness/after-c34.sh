#!/usr/bin/env bash
# After C3 and C4 finish: their objective checks and the metrics. Re-runnable (markers in chain/).
set -u
E=/home/jinx/git/coai-exp; M=$E/chain; mkdir -p "$M"
log() { echo "$(date -u +%FT%TZ) $*"; }
for a in C3 C4; do
  until grep -q '"kind":"arm-done"' "$E/results/$a/timing.jsonl" 2>/dev/null; do
    kill -0 "$(cat "$E/results/$a/run.pid")" 2>/dev/null || { log "arm $a stopped without finishing — resume it (run-all.sh $a), then start me again"; exit 2; }
    sleep 30
  done
done
log "C3 and C4 are done"
for a in C3 C4; do
  [ -f "$M/objective-$a" ] && continue
  "$E/objective-checks.sh" $a > "$E/results/$a/objective.out" 2>&1; touch "$M/objective-$a"; log "objective checks of $a: $(tail -1 "$E/results/$a/objective.out")"
done
node "$E/metrics.mjs" > "$E/metrics.txt" 2>&1; node "$E/metrics.mjs" --json > "$E/metrics.json" 2>&1
touch "$M/c34-done"; log "C3/C4 DONE"
