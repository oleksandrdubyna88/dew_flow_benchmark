#!/usr/bin/env bash
# The H arms, detached: the Q scheme with Haiku as the filler. Make and build H1–H4 one after another (the disk peak), start
# all four together (owner: in parallel), keep Windows awake while they run (the Q chain stops every keep-awake when IT
# ends), then the objective checks and the survival of each H arm, the absolute judge for the new arms only (after the Q
# chain's own judge run, so the two never write the key at once), the metrics. Re-runnable: markers in chain/.
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
for a in H1 H2 H3 H4; do
  [ -f "$M/made-$a" ] && continue
  rm -rf "$E/arm-$a"; cp -a "$E/base" "$E/arm-$a"; git -C "$E/arm-$a" checkout -q -b "exp/kimi-e56-skeleton-haiku-${a#H}"
  (cd "$E/arm-$a" && node .agents/conventions/tools/low-priority.mjs dotnet build dew_flow_connect_other_ais.slnx -c Debug -m:4 \
     --disable-build-servers > "$E/prep-build-$a.log" 2>&1) || { log "warm build of $a failed"; exit 3; }
  touch "$M/made-$a"; log "arm $a made and built"
done
start=""; for a in H1 H2 H3 H4; do arm_done $a || alive $a || start="$start $a"; done
[ -n "$start" ] && { "$E/run-all.sh" $start; log "started:$start"; }
for a in H1 H2 H3 H4; do
  until arm_done $a; do alive $a || { log "arm $a stopped without finishing — resume it (run-all.sh $a), then start me again"; exit 2; }; sleep 30; done
  log "arm $a done"
done
for a in H1 H2 H3 H4; do
  [ -f "$M/objective-$a" ] && continue
  "$E/objective-checks.sh" $a > "$E/results/$a/objective.out" 2>&1
  node "$E/survival.mjs" $a > "$E/results/$a/survival.out" 2>&1
  touch "$M/objective-$a"; log "checks of $a: $(tail -1 "$E/results/$a/objective.out") | $(tail -1 "$E/results/$a/survival.out")"
done
until [ -f "$M/q-done" ]; do sleep 30; done   # the Q chain's judge run first: one writer of judge-abs/key.json at a time
if [ ! -f "$M/judged-abs-h" ]; then "$E/judge-abs.sh" > "$E/judge-abs-h.out" 2>&1; touch "$M/judged-abs-h"; log "absolute judge (H arms) done"; fi
node "$E/metrics.mjs" > "$E/metrics.txt" 2>&1; node "$E/metrics.mjs" --json > "$E/metrics.json" 2>&1
kill $GUARD 2>/dev/null
timeout 60 pwsh.exe -NoProfile -ExecutionPolicy Bypass -File 'C:\Users\strug\AppData\Local\Temp\coai-exp\stop-keep-awake.ps1' >> "$E/chain.log" 2>&1
touch "$M/h-done"; log "H DONE"
