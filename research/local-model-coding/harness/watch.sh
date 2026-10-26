#!/usr/bin/env bash
# Ends when every arm is done, or when an arm's runner died without finishing (then it says which, to resume it).
E=/home/jinx/git/coai-exp
while :; do
  done_n=0
  for a in V C C2; do
    if grep -q '"kind":"arm-done"' $E/results/$a/timing.jsonl 2>/dev/null; then done_n=$((done_n+1));
    elif ! kill -0 "$(cat $E/results/$a/run.pid)" 2>/dev/null; then echo "ARM $a STOPPED without finishing:"; tail -3 $E/results/$a/run.log; exit 1; fi
  done
  [ $done_n -eq 3 ] && { echo "ALL ARMS DONE"; for a in V C C2; do tail -4 $E/results/$a/run.log; done; exit 0; }
  sleep 30
done
