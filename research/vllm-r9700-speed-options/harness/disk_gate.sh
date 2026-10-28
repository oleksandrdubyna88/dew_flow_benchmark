#!/usr/bin/env bash
# Disk gate: 'baseline' records the newest System-log RecordId for providers disk/stornvme;
# 'check' exits 0 only when no newer matching event exists. Exit 1 = new event, 2 = query failed or log reset.
set -u
state="$(dirname "$0")/disk_gate.state"
newest() {
  powershell.exe -NoProfile -Command '$e = Get-WinEvent -FilterHashtable @{LogName="System"; ProviderName="disk","stornvme"} -MaxEvents 1 -ErrorAction Stop; $e.RecordId' 2>/dev/null | tr -d '\r\n '
}
decide() {  # decide <baseline> <current>  -> echo verdict, return code
  local base="$1" cur="$2"
  if ! [[ "$cur" =~ ^[0-9]+$ ]]; then echo "STOP: query failed"; return 2; fi
  if (( cur < base )); then echo "STOP: log reset (newest $cur < baseline $base)"; return 2; fi
  if (( cur > base )); then echo "STOP: new disk event (record $cur > baseline $base)"; return 1; fi
  echo "OK: no new disk event (newest $cur)"; return 0
}
case "${1:-}" in
  baseline) id="$(newest)"; [[ "$id" =~ ^[0-9]+$ ]] || { echo "STOP: query failed"; exit 2; }; echo "$id" > "$state"; echo "baseline $id";;
  check) decide "$(cat "$state")" "$(newest)"; exit $?;;
  selftest)
    decide 100 101 >/dev/null; [ $? -eq 1 ] || { echo "selftest FAIL new"; exit 3; }
    decide 100 100 >/dev/null; [ $? -eq 0 ] || { echo "selftest FAIL same"; exit 3; }
    decide 100 ""  >/dev/null; [ $? -eq 2 ] || { echo "selftest FAIL query"; exit 3; }
    decide 100 99  >/dev/null; [ $? -eq 2 ] || { echo "selftest FAIL reset"; exit 3; }
    echo "selftest OK";;
  *) echo "usage: $0 baseline|check|selftest"; exit 64;;
esac
