#!/usr/bin/env bash
# The mutation checks on every arm, detached: after the O/D/S arms finish (chain/x-done), mutate.mjs on all 30 arms, two at
# a time (each builds the test project once per mutant), then one summary. Re-runnable: an arm with a mutation.json is skipped.
set -u
E=/home/jinx/git/coai-exp; M=$E/chain
log() { echo "$(date -u +%FT%TZ) $*"; }
until [ -f "$M/x-done" ]; do sleep 60; done
log "mutation checks start"
ARMS="C C2 C3 C4 V VT Q1 Q2 Q3 Q4 H1 H2 H3 H4 T1 T2 T3 T4 O1 O2 O3 O4 D1 D2 D3 D4 S1 S2 S3 S4"
set -- $ARMS
while [ $# -gt 0 ]; do
  pair="$1 ${2:-}"; shift; [ $# -gt 0 ] && shift
  for a in $pair; do
    [ -s "$E/results/$a/mutation.json" ] && grep -q '"M6-' "$E/results/$a/mutation.json" && continue   # done with the frozen set
    ( node "$E/mutate.mjs" "$a" > "$E/results/$a/mutation.out" 2>&1; log "mutation $a: $(tail -1 "$E/results/$a/mutation.out")" ) &
  done
  wait
done
touch "$M/mut-done"; log "MUTATION DONE"
