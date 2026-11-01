#!/usr/bin/env bash
# night4_extra.sh: series after the final A/B, run by night4.sh on a quiet PC.
#  option 7 end-to-end: o7ref (= fcombo) vs o7 (fcombo + the wide-N tile patch, work/speed/o7/), 3 rounds.
#  Prediction (written before the run): c=4 tok/s +2..5 % (M 16 at MTP 3: ~0.5-0.7 ms of a ~12 ms step);
#  c=1 equal (M 4 stays on the HIP op); TTFT equal; GSM8K within 2 x stderr; 20 prompts identical (max diff 0.0).
set -u
D="$(cd "$(dirname "$0")" && pwd)"
export PREFIXTEST=0
left=0; for r in 1 2 3; do for a in o7ref o7; do [ -f "$D/speedO7/done_$a-$r" ] || left=$((left+1)); done; done
(( $(date +%s) + left * 1500 < DEADLINE )) || { echo "option 7 series: $left arm-rounds do not fit"; exit 0; }
"$D/speed_A.sh" "$D/speedO7" o7ref o7
