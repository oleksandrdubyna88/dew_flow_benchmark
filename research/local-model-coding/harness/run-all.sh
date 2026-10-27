#!/usr/bin/env bash
# Starts (or, after a crash, RESUMES) every arm as a detached process, so the arms do not depend on the session that
# started them. An arm whose run-arm.sh is still alive is left alone.   Usage: run-all.sh [V C C2]
E=/home/jinx/git/coai-exp
ARMS=("$@"); [ ${#ARMS[@]} -eq 0 ] && ARMS=(V C C2)
for a in "${ARMS[@]}"; do
  mkdir -p "$E/results/$a"
  pidf=$E/results/$a/run.pid
  if [ -f "$pidf" ] && kill -0 "$(cat "$pidf")" 2>/dev/null; then echo "$a: already running (pid $(cat "$pidf"))"; continue; fi
  setsid nohup "$E/run-arm.sh" "$a" >> "$E/results/$a/run.log" 2>&1 < /dev/null &
  echo $! > "$pidf"; echo "$a: started (pid $!)"
done
