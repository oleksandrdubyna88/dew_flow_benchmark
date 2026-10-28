#!/usr/bin/env bash
# wait_quiet.sh: wait until no coai-exp process runs (its keep-awake.ps1 helper is ignored) and the WSL 1-min load is
# below 4 for three checks in a row (5 min apart). Prints one line per check.
set -u
ok=0
while (( ok < 3 )); do
  n=$(pgrep -f "coai-exp" | while read -r p; do grep -qa "keep-awake" "/proc/$p/cmdline" 2>/dev/null || echo "$p"; done | wc -l)
  load=$(cut -d' ' -f1 /proc/loadavg)
  if (( n == 0 )) && awk "BEGIN{exit !($load < 4)}"; then ok=$((ok+1)); else ok=0; fi
  echo "$(date '+%F %T') coai-exp processes=$n load=$load quiet-checks=$ok/3"
  (( ok < 3 )) && sleep 300
done
echo "$(date '+%F %T') QUIET"
