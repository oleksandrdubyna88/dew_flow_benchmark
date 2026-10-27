#!/usr/bin/env bash
# Keeps Windows awake while the mutation chain runs; stops keep-awake after it.
E=/home/jinx/git/coai-exp
MUT=$(cat "$E/chain-mut.pid")
while kill -0 "$MUT" 2>/dev/null; do
  if ! timeout 30 pwsh.exe -NoProfile -ExecutionPolicy Bypass -File 'C:\Users\strug\AppData\Local\Temp\coai-exp\check-keep-awake.ps1' >/dev/null 2>&1; then
    setsid nohup pwsh.exe -NoProfile -ExecutionPolicy Bypass -File 'C:\Users\strug\AppData\Local\Temp\coai-exp\keep-awake.ps1' >> "$E/keep-awake.log" 2>&1 < /dev/null &
    echo "$(date -u +%FT%TZ) mut-awake: keep-awake started" >> "$E/chain.log"
  fi
  sleep 120
done
timeout 60 pwsh.exe -NoProfile -ExecutionPolicy Bypass -File 'C:\Users\strug\AppData\Local\Temp\coai-exp\stop-keep-awake.ps1' >> "$E/chain.log" 2>&1
