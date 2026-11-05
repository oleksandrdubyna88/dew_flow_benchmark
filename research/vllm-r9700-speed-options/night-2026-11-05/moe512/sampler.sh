#!/usr/bin/env bash
# sampler.sh <outfile> <stopfile>: every 10 s one line: epoch, WSL loadavg, Windows total CPU %, GPU dedicated bytes (per adapter), top 3 WSL processes by CPU.
out="$1"; stop="$2"
while [ ! -e "$stop" ]; do
  w=$(powershell.exe -NoProfile -Command '$c=(Get-Counter "\Processor(_Total)\% Processor Time").CounterSamples[0].CookedValue; $g=(Get-Counter "\GPU Adapter Memory(*)\Dedicated Usage").CounterSamples | ? {$_.CookedValue -gt 1e9} | % {[math]::Round($_.CookedValue/1MB)}; "{0:N0}%% gpuMB={1}" -f $c, ($g -join ",")' 2>/dev/null | tr -d '\r' | head -1)
  echo "$(date +%s) $(date +%T) load=$(cut -d' ' -f1-3 /proc/loadavg) win_cpu=$w top=$(ps -eo pcpu,comm --sort=-pcpu | sed -n 2,4p | awk '{printf "%s:%s ",$2,$1}')" >> "$out"
  sleep 8
done
