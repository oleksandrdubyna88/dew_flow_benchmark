#!/usr/bin/env bash
# Runs one arm of the experiment: step 1 code (sonnet), step 2 review (opus, read-only), step 3 fixes (sonnet).
# Each step is one headless `claude -p` session in the arm's clone; its whole event stream is kept per attempt, so the
# host's own numbers (duration, cost, tokens per model) can be read afterwards.
#
# RESUMABLE: run it again after any crash and it continues where it stopped.
#   - a step with results/<arm>/<step>.done is skipped;
#   - an interrupted step resumes ITS OWN Claude session (the id is fixed before the first attempt), with the time that
#     is left of its budget; only if that session never wrote a transcript does the step start again from its brief;
#   - every attempt has its own stream file (<step>.a<N>.jsonl) and its own lines in timing.jsonl.
# Usage: run-arm.sh V|C|C2|VT|C3|C4|Q1..Q4  (a Q arm: skeleton → fill tests → fill code → polish → review → fixes)
set -u
ARM=$1
E=/home/jinx/git/coai-exp
DIR=$E/arm-$ARM
case $ARM in V) BRANCH=exp/kimi-e56-vllm; PARA=$E/briefs/arm-V.md ;;
             C) BRANCH=exp/kimi-e56-classic; PARA=$E/briefs/arm-C.md ;;
             C2) BRANCH=exp/kimi-e56-classic-2; PARA=$E/briefs/arm-C.md ;;
             VT) BRANCH=exp/kimi-e56-vllm-think; PARA=$E/briefs/arm-VT.md ;;
             C3) BRANCH=exp/kimi-e56-classic-3; PARA=$E/briefs/arm-C.md ;;
             C4) BRANCH=exp/kimi-e56-classic-4; PARA=$E/briefs/arm-C.md ;;
             Q1|Q2|Q3|Q4) BRANCH=exp/kimi-e56-skeleton-${ARM#Q}; PARA=$E/briefs/arm-C.md ;;  # PARA unused: the Q briefs are whole
             H1|H2|H3|H4) BRANCH=exp/kimi-e56-skeleton-haiku-${ARM#H}; PARA=$E/briefs/arm-C.md ;;  # the Q scheme, Haiku as the filler
             *) echo "usage: run-arm.sh V|C|C2|VT|C3|C4|Q1..Q4|H1..H4" >&2; exit 1 ;; esac
OUT=$E/results/$ARM; NOTES=$E/notes; WORK=$E/vllm/work/$ARM
mkdir -p "$OUT" "$NOTES" "$WORK"
export COAI_EXP_VLLM_LOG=$E/vllm/requests-$ARM.jsonl
[ "$ARM" = VT ] && export COAI_EXP_FORCE_THINK=1
case $ARM in H*) export COAI_EXP_FILLER=haiku; FILLER="haiku (Claude)" ;; *) FILLER="qwen (vLLM)" ;; esac   # the H arms' one variable against Q   # VT's one variable against V, pinned here, not only in its brief
log() { echo "$(date -u +%FT%TZ) [$ARM] $*"; }

WRITE_TOOLS=(Read Edit Write Glob Grep TodoWrite Agent
  "Bash(git *)" "Bash(dotnet *)" "Bash(node *)" "Bash(npm *)" "Bash(npx *)" "Bash(ls *)" "Bash(cat *)" "Bash(head *)"
  "Bash(tail *)" "Bash(grep *)" "Bash(rg *)" "Bash(find *)" "Bash(sed *)" "Bash(awk *)" "Bash(wc *)" "Bash(diff *)"
  "Bash(mkdir *)" "Bash(cd *)" "Bash(echo *)" "Bash(pwd)" "Bash(sort *)" "Bash(uniq *)" "Bash(tee *)" "Bash(cp *)"
  "Bash(mv *)" "Bash(test *)" "Bash(date *)" "Bash(timeout *)" "Bash(xargs *)" "Bash(./src_mcp/tests/bin/Debug/net10.0/CoaiMcp.Tests *)")
READ_TOOLS=(Read Glob Grep TodoWrite
  "Bash(git *)" "Bash(dotnet *)" "Bash(node *)" "Bash(npm *)" "Bash(ls *)" "Bash(cat *)" "Bash(head *)" "Bash(tail *)"
  "Bash(grep *)" "Bash(rg *)" "Bash(find *)" "Bash(sed *)" "Bash(awk *)" "Bash(wc *)" "Bash(diff *)" "Bash(cd *)"
  "Bash(echo *)" "Bash(pwd)" "Bash(sort *)" "Bash(test *)" "Bash(./src_mcp/tests/bin/Debug/net10.0/CoaiMcp.Tests *)")

fill() { # template deadline notes -> filled brief on stdout
  node -e '
    const fs=require("fs"); const [tpl,para,branch,deadline,notes,review,codeNotes,arm]=process.argv.slice(1);
    let s=fs.readFileSync(tpl,"utf8");
    s=s.replaceAll("{{ARM_PARAGRAPH}}",fs.readFileSync(para,"utf8").trim()).replaceAll("{{BRANCH}}",branch)
       .replaceAll("{{DEADLINE}}",deadline).replaceAll("{{NOTES}}",notes).replaceAll("{{REVIEW}}",review)
       .replaceAll("{{CODE_NOTES}}",codeNotes).replaceAll("{{ARM}}",arm);
    process.stdout.write(s);' "$1" "$PARA" "$BRANCH" "$2" "$3" "$NOTES/$ARM-review.json" "$NOTES/$ARM-code.md" "$ARM"
}

has_result() { grep -q '"type":"result"' "$1" 2>/dev/null; }
transcript_of() { ls ~/.claude/projects/*/"$1".jsonl 2>/dev/null | head -1; }
# seconds of a finished-or-crashed attempt: its timing lines, else its stream file's first and last write
used_seconds() {
  node -e '
    const fs=require("fs"); const [timing,step]=process.argv.slice(1); let used=0;
    const rows=fs.existsSync(timing)?fs.readFileSync(timing,"utf8").trim().split("\n").filter(Boolean).map(JSON.parse):[];
    for (const r of rows) if (r.step===step && r.kind==="end") used+=r.wall_s;
    for (const r of rows) if (r.step===step && r.kind==="start" && !rows.some(e=>e.step===step&&e.kind==="end"&&e.attempt===r.attempt)) {
      const f=r.stream; if (fs.existsSync(f)) used+=Math.max(0,Math.round((fs.statSync(f).mtimeMs-Date.parse(r.start_utc))/1000));
    }
    process.stdout.write(String(used));' "$OUT/timing.jsonl" "$1"
}

wait_vllm() { # arms V and VT only: never start or resume a coding attempt while the local model is down
  case $ARM in V|VT|Q*) ;; *) return 0 ;; esac
  local t0; t0=$(date +%s)
  until /mnt/c/Windows/System32/curl.exe -s -m 5 -o /dev/null -w '%{http_code}' http://localhost:8000/health 2>/dev/null | grep -q 200; do
    [ $(( $(date +%s) - t0 )) -gt 900 ] && { log "vLLM down for 15 min — stopping; run me again when it is up"; exit 3; }
    sleep 15
  done
}

step() { # name model minutes template notes mode needs_vllm
  local name=$1 model=$2 minutes=$3 tpl=$4 notes=$5 mode=$6 needs_vllm=$7
  [ -f "$OUT/$name.done" ] && { log "$name already done — skipped"; return 0; }
  # a session that FINISHED (its stream has the result) but whose runner died before writing .done is done, not resumable:
  # resuming it would tell a finished agent it was interrupted and buy it extra time (arm C, 2026-10-25)
  local last_stream; last_stream=$(ls "$OUT"/$name.a*.jsonl 2>/dev/null | sort -V | tail -1)
  if [ -n "$last_stream" ] && has_result "$last_stream"; then
    echo "{\"kind\":\"end\",\"arm\":\"$ARM\",\"step\":\"$name\",\"attempt\":$(basename "$last_stream" | sed -E 's/.*\.a([0-9]+)\.jsonl/\1/'),\"end_utc\":\"$(date -u -d "@$(stat -c %Y "$last_stream")" +%FT%TZ)\",\"wall_s\":null,\"exit\":null,\"result\":true,\"recovered\":true,\"head\":\"$(git -C "$DIR" rev-parse --short HEAD)\"}" >> "$OUT/timing.jsonl"
    echo "{\"attempts\":\"recovered\",\"stream\":\"$last_stream\"}" > "$OUT/$name.done"
    log "$name had finished before its runner died — marked done, not resumed"; return 0
  fi
  [ "$needs_vllm" = yes ] && wait_vllm
  local attempt=$(( $(ls "$OUT"/$name.a*.jsonl 2>/dev/null | wc -l) + 1 ))
  local sid_file=$OUT/$name.session sid used remaining t0 deadline
  t0=$(date -u +%s)
  used=$(used_seconds "$name"); remaining=$(( minutes * 60 - used )); [ $remaining -lt 300 ] && remaining=300
  deadline="$(date -u -d "@$((t0 + remaining))" '+%H:%M UTC') ($(date -d "@$((t0 + remaining))" '+%H:%M %Z'))"
  local tools=("${WRITE_TOOLS[@]}") extra=(--permission-mode acceptEdits)
  if [ "$mode" = read ]; then tools=("${READ_TOOLS[@]}"); extra=(--permission-mode default --disallowedTools Edit Write NotebookEdit); fi
  local session_args prompt_file=$OUT/$name.a$attempt.prompt.md how
  if [ -f "$sid_file" ] && [ -n "$(transcript_of "$(cat "$sid_file")")" ]; then
    sid=$(cat "$sid_file"); session_args=(--resume "$sid"); how=resume
    cat > "$prompt_file" <<EOF
This session was interrupted (the machine or the harness stopped) and is now resumed. Nothing you did before is lost unless
it was not saved: check \`git status\` and \`git log --oneline b4e2ea96..HEAD\` first, then continue the same task from where
you stopped. Your new deadline, for the time that is left of your budget, is: **$deadline**. The rest of your brief is unchanged.
EOF
  else
    sid=$(cat /proc/sys/kernel/random/uuid); echo "$sid" > "$sid_file"; session_args=(--session-id "$sid"); how=fresh
    fill "$tpl" "$deadline" "$notes" > "$prompt_file"
  fi
  local stream=$OUT/$name.a$attempt.jsonl
  echo "{\"kind\":\"start\",\"arm\":\"$ARM\",\"step\":\"$name\",\"attempt\":$attempt,\"how\":\"$how\",\"session\":\"$sid\",\"model\":\"$model\",\"start_utc\":\"$(date -u -d @$t0 +%FT%TZ)\",\"budget_s\":$remaining,\"deadline\":\"$deadline\",\"head\":\"$(git -C "$DIR" rev-parse --short HEAD)\",\"stream\":\"$stream\"}" >> "$OUT/timing.jsonl"
  log "$name attempt $attempt ($how, session $sid, budget ${remaining}s)"
  (cd "$DIR" && timeout -s INT -k 60 $(( remaining + 600 )) claude -p --model "$model" --output-format stream-json --verbose \
      "${session_args[@]}" "${extra[@]}" --add-dir "$NOTES" --add-dir "$WORK" --allowedTools "${tools[@]}" \
      < "$prompt_file" > "$stream" 2> "$OUT/$name.a$attempt.err")
  local rc=$? t1; t1=$(date -u +%s)
  echo "{\"kind\":\"end\",\"arm\":\"$ARM\",\"step\":\"$name\",\"attempt\":$attempt,\"end_utc\":\"$(date -u -d @$t1 +%FT%TZ)\",\"wall_s\":$((t1 - t0)),\"exit\":$rc,\"result\":$(has_result "$stream" && echo true || echo false),\"head\":\"$(git -C "$DIR" rev-parse --short HEAD)\"}" >> "$OUT/timing.jsonl"
  if has_result "$stream" || [ $rc -eq 124 ] || [ $rc -eq 137 ]; then
    echo "{\"attempts\":$attempt,\"exit\":$rc,\"stream\":\"$stream\"}" > "$OUT/$name.done"   # finished, or stopped at its hard deadline
    log "$name done (exit $rc)"
  else
    log "$name INTERRUPTED (exit $rc, no result) — run me again to resume it"; exit 2
  fi
}

fill_step() { # name kind minutes — the local model fills the skeleton's regions of one kind (fill.mjs; resumable)
  local name=$1 kind=$2 minutes=$3
  [ -f "$OUT/$name.done" ] && { log "$name already done — skipped"; return 0; }
  wait_vllm
  local attempt=$(( $(cat "$OUT/timing.jsonl" 2>/dev/null | grep -c "\"kind\":\"start\",\"arm\":\"$ARM\",\"step\":\"$name\"") + 1 )) t0; t0=$(date -u +%s)
  echo "{\"kind\":\"start\",\"arm\":\"$ARM\",\"step\":\"$name\",\"attempt\":$attempt,\"how\":\"script\",\"model\":\"$FILLER\",\"start_utc\":\"$(date -u -d @$t0 +%FT%TZ)\",\"head\":\"$(git -C "$DIR" rev-parse --short HEAD)\",\"stream\":\"\"}" >> "$OUT/timing.jsonl"
  log "$name attempt $attempt (fill $kind, limit ${minutes} min)"
  timeout -s INT -k 30 $(( minutes * 60 )) node "$E/fill.mjs" "$ARM" "$kind" >> "$OUT/$name.log" 2>&1
  local rc=$? t1; t1=$(date -u +%s)
  echo "{\"kind\":\"end\",\"arm\":\"$ARM\",\"step\":\"$name\",\"attempt\":$attempt,\"end_utc\":\"$(date -u -d @$t1 +%FT%TZ)\",\"wall_s\":$((t1 - t0)),\"exit\":$rc,\"result\":$([ $rc -eq 0 ] && echo true || echo false),\"head\":\"$(git -C "$DIR" rev-parse --short HEAD)\"}" >> "$OUT/timing.jsonl"
  echo "{\"attempts\":$attempt,\"exit\":$rc}" > "$OUT/$name.done"   # a fill that ran out of time leaves placeholders; the polish writes them
  log "$name done (exit $rc): $(tail -1 "$OUT/$name.log")"
}

review_json() { # the review's final message is the JSON; keep it where the fixer reads it
  [ -s "$NOTES/$ARM-review.json" ] && return 0
  node -e '
    const fs=require("fs"); const f=JSON.parse(fs.readFileSync(process.argv[1],"utf8")).stream;
    const r=fs.readFileSync(f,"utf8").trim().split("\n").map(l=>{try{return JSON.parse(l)}catch{return null}}).filter(x=>x&&x.type==="result").pop();
    const text=r?.result??""; const m=text.match(/\{[\s\S]*\}/);
    fs.writeFileSync(process.argv[2], m?m[0]:JSON.stringify({verdict:"unparsed",raw:text}));' "$OUT/$1.done" "$NOTES/$ARM-review.json"
}

case $ARM in
  Q*|H*)
    step 1-skeleton sonnet 30 "$E/briefs/q1-skeleton.template.md" "$NOTES/$ARM-code.md" write no
    fill_step 2a-fill test 20
    fill_step 2b-fill prod 20
    step 3-polish sonnet 40 "$E/briefs/q3-polish.template.md" "$NOTES/$ARM-code.md" write no
    step 4-review opus 20 "$E/briefs/2-review.template.md" "-" read no
    review_json 4-review
    step 5-fix sonnet 30 "$E/briefs/3-fix.template.md" "$NOTES/$ARM-fix.md" write no
    ;;
  *)
    step 1-code sonnet 75 "$E/briefs/1-code.template.md" "$NOTES/$ARM-code.md" write yes
    step 2-review opus 20 "$E/briefs/2-review.template.md" "-" read no
    review_json 2-review
    step 3-fix sonnet 30 "$E/briefs/3-fix.template.md" "$NOTES/$ARM-fix.md" write no
    ;;
esac
echo "{\"kind\":\"arm-done\",\"arm\":\"$ARM\",\"done_utc\":\"$(date -u +%FT%TZ)\"}" >> "$OUT/timing.jsonl"
log "arm done"
