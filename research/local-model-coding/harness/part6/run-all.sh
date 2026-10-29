#!/usr/bin/env bash
# Starts (or, after a crash or a stop, RESUMES) arms as detached processes. An arm whose runner is alive is left alone.
E=/home/jinx/git/coai-exp2
for a in "$@"; do
  pidf=$E/pids/$a.pid
  if [ -f "$pidf" ] && kill -0 "$(cat "$pidf")" 2>/dev/null; then echo "$a: already running (pid $(cat "$pidf"))"; continue; fi
  setsid nohup "$E/run-arm.sh" "$a" >> "$E/pids/$a.log" 2>&1 < /dev/null &
  echo $! > "$pidf"; echo "$a: started (pid $!)"
done
