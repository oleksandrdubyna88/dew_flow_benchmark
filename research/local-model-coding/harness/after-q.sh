#!/usr/bin/env bash
# The Q arms, detached: after C3/C4 are checked, vLLM up, Q1+Q2 together, then Q3+Q4 together (2 at a time: the system NVMe
# logged bad blocks under 3 parallel arms), then the objective checks and the survival of each Q arm, the absolute blind
# judge over all 10 arms, the metrics, and the keep-awake stopped. Re-runnable: markers in chain/.
set -u
E=/home/jinx/git/coai-exp; M=$E/chain; mkdir -p "$M"
log() { echo "$(date -u +%FT%TZ) $*"; }
arm_done() { grep -q '"kind":"arm-done"' "$E/results/$1/timing.jsonl" 2>/dev/null; }
alive() { [ -f "$E/results/$1/run.pid" ] && kill -0 "$(cat "$E/results/$1/run.pid")" 2>/dev/null; }
vllm_up() { /mnt/c/Windows/System32/curl.exe -s -m 5 -o /dev/null -w '%{http_code}' http://localhost:8000/health 2>/dev/null | grep -q 200; }
keep_awake() { setsid nohup pwsh.exe -NoProfile -ExecutionPolicy Bypass -File 'C:\Users\strug\AppData\Local\Temp\coai-exp\keep-awake.ps1' >> "$E/keep-awake.log" 2>&1 < /dev/null & }

until [ -f "$M/c34-done" ]; do sleep 30; done
log "C3/C4 checked — the Q arms start"
if ! vllm_up; then
  log "vLLM is down — starting it"
  (cd /mnt/c/Users/strug && timeout 300 pwsh.exe -NoProfile -Command '& C:\Users\strug\vllm\start-vllm.ps1 -Model /root/.cache/huggingface/local/Qwen3.6-35B-A3B-GPTQ -ServedName qwen3.6-35b-a3b -MaxModelLen 131072 -MaxNumSeqs 4 -NoMtp -Env "VLLM_DISABLED_KERNELS=RDNAHybridW4A16LinearKernel" -Detach' >> "$E/chain.log" 2>&1)
  t0=$(date +%s); until vllm_up; do [ $(( $(date +%s) - t0 )) -gt 600 ] && { log "vLLM did not come up in 10 min — stopping"; exit 3; }; sleep 10; done
fi
for batch in "Q1 Q2" "Q3 Q4"; do
  keep_awake
  for a in $batch; do
    [ -f "$M/made-$a" ] && continue
    rm -rf "$E/arm-$a"; cp -a "$E/base" "$E/arm-$a"; git -C "$E/arm-$a" checkout -q -b "exp/kimi-e56-skeleton-${a#Q}"
    (cd "$E/arm-$a" && node .agents/conventions/tools/low-priority.mjs dotnet build dew_flow_connect_other_ais.slnx -c Debug -m:4 \
       --disable-build-servers > "$E/prep-build-$a.log" 2>&1) || { log "warm build of $a failed"; exit 3; }
    touch "$M/made-$a"; log "arm $a made and built"
  done
  started=""; for a in $batch; do arm_done $a || alive $a || started="$started $a"; done
  [ -n "$started" ] && { "$E/run-all.sh" $started; log "started:$started"; }
  for a in $batch; do
    until arm_done $a; do alive $a || { log "arm $a stopped without finishing — resume it (run-all.sh $a), then start me again"; exit 2; }; sleep 30; done
    log "arm $a done"
  done
done
for a in Q1 Q2 Q3 Q4; do
  [ -f "$M/objective-$a" ] && continue
  "$E/objective-checks.sh" $a > "$E/results/$a/objective.out" 2>&1
  node "$E/survival.mjs" $a > "$E/results/$a/survival.out" 2>&1
  touch "$M/objective-$a"; log "checks of $a: $(tail -1 "$E/results/$a/objective.out") | $(tail -1 "$E/results/$a/survival.out")"
done
if [ ! -f "$M/judged-abs" ]; then
  "$E/judge-abs.sh" > "$E/judge-abs.out" 2>&1; touch "$M/judged-abs"; log "absolute judge done"
fi
node "$E/metrics.mjs" > "$E/metrics.txt" 2>&1; node "$E/metrics.mjs" --json > "$E/metrics.json" 2>&1
timeout 60 pwsh.exe -NoProfile -ExecutionPolicy Bypass -File 'C:\Users\strug\AppData\Local\Temp\coai-exp\stop-keep-awake.ps1' >> "$E/chain.log" 2>&1
touch "$M/q-done"; log "Q DONE"
