#!/usr/bin/env bash
# Part 6 (owner, 2026-10-28): three schemes × three runs on a real task (issues #878 + the quota half of #765), from the
# plan committed at $BASE, each arm ending in the coai code gate. Rounds of three (C_n, CR_n, SRS_n at the same time).
# Makes the arms (random folder names), locks everything that holds other work, runs the rounds, unlocks, then the
# objective checks. Re-runnable: markers in chain/. The judge, the coai-findings judge and the metrics run after it.
set -u
E=/home/jinx/git/coai-exp2; M=$E/chain; mkdir -p "$M" "$E/pids"
# shellcheck source=/dev/null
source "$E/exp.env"
log() { echo "$(date -u +%FT%TZ) $*"; }
tok() { node -e 'console.log(require(process.argv[1])[process.argv[2]])' "$MAP" "$1"; }
arm_done() { grep -q '"kind":"arm-done"' "$E/r/$(tok "$1")/out/timing.jsonl" 2>/dev/null; }
alive() { [ -f "$E/pids/$1.pid" ] && kill -0 "$(cat "$E/pids/$1.pid")" 2>/dev/null; }
awake_guard() { # every 2 min: a keep-awake must be running (the Windows host must not sleep)
  while :; do
    if ! timeout 30 pwsh.exe -NoProfile -ExecutionPolicy Bypass -File 'C:\Users\strug\AppData\Local\Temp\coai-exp\check-keep-awake.ps1' >/dev/null 2>&1; then
      setsid nohup pwsh.exe -NoProfile -ExecutionPolicy Bypass -File 'C:\Users\strug\AppData\Local\Temp\coai-exp\keep-awake.ps1' >> "$E/keep-awake.log" 2>&1 < /dev/null &
    fi
    sleep 120
  done
}
awake_guard & GUARD=$!
trap 'kill $GUARD 2>/dev/null' EXIT

ROUNDS=("C1 CR1 SRS1" "C2 CR2 SRS2" "C3 CR3 SRS3"); ARMS="C1 CR1 SRS1 C2 CR2 SRS2 C3 CR3 SRS3"
for a in $ARMS; do   # make every arm before the lock: a copy of the built base on the one branch name every arm uses
  [ -f "$M/made-$a" ] && continue
  R=$E/r/$(tok "$a"); rm -rf "$R"; mkdir -p "$R"/{out,notes,work,tmp,coai-data/wsl-ubuntu}
  cp -a "$E/base" "$R/arm"; git -C "$R/arm" checkout -q -b "$BRANCH"
  (cd "$R/arm" && node .agents/conventions/tools/low-priority.mjs dotnet build dew_flow_connect_other_ais.slnx -c Debug -m:4 \
     --disable-build-servers > "$R/out/prep-build.log" 2>&1) || { log "warm build of $a failed"; exit 3; }
  cp "$COAI_SETTINGS" "$R/coai-data/wsl-ubuntu/settings.json"
  echo '{"mcpServers":{}}' > "$R/mcp-none.json"
  node -e 'const [bin,dir]=process.argv.slice(1); console.log(JSON.stringify({mcpServers:{coai:{type:"stdio",command:bin,args:[],env:{COAI_DATA_DIR:dir,COAI_DATA_SIDE:"wsl-ubuntu"}}}},null,1))' \
    "$COAI_BIN" "$R/coai-data" > "$R/mcp-coai.json"
  touch "$M/made-$a"; log "arm $a made and built"
done
if [ ! -f "$M/locked" ]; then "$E/lock.sh" lock >> "$E/lock.log" 2>&1 || { log "the lock failed — see lock.log"; exit 4; }; touch "$M/locked"; log "locked: $(tail -1 "$E/lock.log")"; fi
for batch in "${ROUNDS[@]}"; do
  start=""; for a in $batch; do arm_done $a || alive $a || start="$start $a"; done
  [ -n "$start" ] && { "$E/run-all.sh" $start; log "started:$start"; }
  for a in $batch; do
    until arm_done $a; do alive $a || { log "arm $a stopped without finishing — resume it (run-all.sh $a), then start me again"; exit 2; }; sleep 60; done
    log "arm $a done"
  done
done
if [ ! -f "$M/unlocked" ]; then "$E/lock.sh" unlock >> "$E/lock.log" 2>&1; touch "$M/unlocked"; log "unlocked: $(tail -1 "$E/lock.log")"; fi
for a in $ARMS; do
  [ -f "$M/objective-$a" ] && continue
  "$E/objective-checks.sh" "$a" > "$E/r/$(tok "$a")/out/objective.out" 2>&1
  touch "$M/objective-$a"; log "checks of $a: $(tail -1 "$E/r/$(tok "$a")/out/objective.out")"
done
kill $GUARD 2>/dev/null
timeout 60 pwsh.exe -NoProfile -ExecutionPolicy Bypass -File 'C:\Users\strug\AppData\Local\Temp\coai-exp\stop-keep-awake.ps1' >> "$E/keep-awake.log" 2>&1
touch "$M/arms-done"; log "ARMS DONE — next: judge, coai-findings judge, metrics"
