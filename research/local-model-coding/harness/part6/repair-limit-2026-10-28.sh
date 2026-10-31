#!/usr/bin/env bash
# One-off repair, 2026-10-29 (kept as the record). At ~21:45 UTC on 2026-10-28 the Claude session limit was reached
# ("You've hit your individual spend limit … your session limit resets 1:20am (Europe/Paris)", api_error_status 429).
# run-arm.sh's limit detector missed that wording (fixed: it reads api_error_status / api_error first, and an error result is
# never "done"), so every step after it was marked done in seconds, and round 2 started and burned the same way.
# This undoes ONLY what the limit produced; every step whose own last result is not an error is kept as it ran.
#   Round 1 (C1, CR1, SRS1): remove the .done of each step whose stream's last result is an error (its session resumes),
#     the arm-done line, and what was computed from a bogus step (review.json parsed from an error, before/diff snapshots
#     taken at the wrong commit, SRS1's batch markers and Haiku diffs of t4, t5, t7). Commits and uncommitted work stay.
#   Round 2 (C2, CR2, SRS2): no work was done (0 commits, clean); reset their out/notes/work to a fresh start.
set -u
E=/home/jinx/git/coai-exp2
# shellcheck source=/dev/null
source "$E/exp.env"
tok() { node -e 'console.log(require(process.argv[1])[process.argv[2]])' "$MAP" "$1"; }
errored_stream() { node -e 'const fs=require("fs"); const r=fs.readFileSync(process.argv[1],"utf8").split("\n").map(l=>{try{return JSON.parse(l)}catch{return null}}).filter(x=>x&&x.type==="result").pop(); process.exit(r&&r.is_error?0:1)' "$1"; }
drop_arm_done() { local t=$1/timing.jsonl; [ -f "$t" ] && { grep -v '"kind":"arm-done"' "$t" > "$t.tmp"; mv "$t.tmp" "$t"; }; }

for a in C1 CR1 SRS1; do
  R=$E/r/$(tok "$a"); O=$R/out
  for d in "$O"/*.done; do
    case $(basename "$d") in batch-*) continue ;; esac
    s=$(node -e 'try{console.log(JSON.parse(require("fs").readFileSync(process.argv[1],"utf8")).stream)}catch{}' "$d")
    if [ -n "$s" ] && errored_stream "$s"; then rm -f "$d"; echo "$a: $(basename "$d" .done) → resumes"; fi
  done
  drop_arm_done "$O"
  # a review.json parsed from an error result is not a review
  if [ -f "$R/notes/review.json" ] && node -e 'const j=require(process.argv[1]); process.exit(j.verdict==="unparsed"||!Array.isArray(j.findings)?0:1)' "$R/notes/review.json"; then
    rm -f "$R/notes/review.json"; echo "$a: review.json (parsed from an error) removed"; fi
  # snapshots taken at a bogus step's end, or before a step that will now run at another commit
  for n in 2-hreview 3-hreview 4-gate 5-gate 6-gate; do
    [ -f "$O/$n.done" ] && continue
    case $n in *hreview) rm -f "$O/$n.diff" ;; *) rm -f "$O/$n.before" "$O/$n.diff" ;; esac
  done
  [ "$a" = SRS1 ] && { rm -f "$O/3-hreview.before" "$O/batch-2.done" "$O/batch-3.done" "$O/batch-4.done"
    for t in t4 t5 t7; do rm -f "$O/task-$t-haiku.head" "$O/task-$t-haiku.diff"; done; echo "SRS1: batches 2–4 and their Haiku snapshots reset"; }
done
for a in C2 CR2 SRS2; do
  R=$E/r/$(tok "$a")
  [ "$(git -C "$R/arm" rev-list --count "$BASE"..HEAD)" = 0 ] && [ -z "$(git -C "$R/arm" status --porcelain)" ] || { echo "$a has work — NOT reset"; continue; }
  find "$R/out" -mindepth 1 ! -name prep-build.log -delete; find "$R/notes" "$R/work" -mindepth 1 -delete
  echo "$a: reset to a fresh start"
done
