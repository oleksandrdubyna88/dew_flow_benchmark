#!/usr/bin/env bash
# ram_guard_all.sh <pid> [min_mb]: while process <pid> lives, read Windows "Available MBytes" every 15 s. Below the
# limit: stop every vllmprtest-* container and send TERM to <pid> (its EXIT trap restores the operator's server).
set -u
pid="$1"; min="${2:-4096}"; W="/mnt/c/Program Files/WSL/wslc.exe"; i=0
while kill -0 "$pid" 2>/dev/null; do
  mb=$(powershell.exe -NoProfile -Command '[int](Get-Counter "\Memory\Available MBytes").CounterSamples[0].CookedValue' 2>/dev/null | tr -d '\r\n ')
  if [[ "$mb" =~ ^[0-9]+$ ]] && (( mb < min )); then
    echo "$(date '+%T') RAM GUARD: available $mb MB < $min MB, stopping tests"
    for c in $("$W" list --no-trunc 2>/dev/null | tr -d '\r' | awk 'NR>1{print $NF}' | grep -E '^vllmprtest-' || true); do "$W" stop "$c" >/dev/null 2>&1; done
    kill -TERM "$pid"; exit 1
  fi
  (( i % 20 == 0 )) && echo "$(date '+%T') available RAM ${mb} MB"
  i=$((i+1)); sleep 15
done
