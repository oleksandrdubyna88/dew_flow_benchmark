#!/usr/bin/env bash
# Part 6 (owner, 2026-10-28): one arm of three schemes on a REAL task (issues #878 + the quota half of #765), each ending in
# the coai code gate. Every step is one headless `claude -p` session; its event stream is kept per attempt.
#   C   = Sonnet codes → Opus reviews → Sonnet fixes → the coai gate (Sonnet)
#   CR  = Sonnet codes → a fresh Haiku reviews + fixes → Opus reviews → Sonnet fixes → the coai gate
#   SRS = Sonnet splits → Haiku codes each task → a fresh Haiku reviews + fixes → Opus reviews → Sonnet fixes → the coai gate
# ISOLATION: an arm lives under r/<random token>/ (its clone, notes, work, results, temp, coai data dir); the folders above
# cannot be listed while arms run, every arm is on the same branch name, and no brief names the scheme or another arm.
# RESUMABLE: a step with <step>.done is skipped; an interrupted step resumes ITS OWN session with the time left of its budget.
# LIMITS: a step that ends on a usage limit, a rate limit or an overload is NOT done: the runner waits and resumes the same
# session (LIMIT_WAIT_S apart, at most LIMIT_TRIES times). Waiting is not counted against the step's budget.
# Usage: run-arm.sh C1|CR1|SRS1 ...
set -u
ARM=$1
E=/home/jinx/git/coai-exp2
# shellcheck source=/dev/null
source "$E/exp.env"   # BASE, PLAN, BRANCH, COAI_BIN, MAP, SCEN_COUNT
TOKEN=$(node -e 'const m=require(process.argv[1]); if(!m[process.argv[2]]) process.exit(1); console.log(m[process.argv[2]])' "$MAP" "$ARM") || { echo "no token for $ARM" >&2; exit 1; }
R=$E/r/$TOKEN; DIR=$R/arm; OUT=$R/out; NOTES=$R/notes; WORK=$R/work
export TMPDIR=$R/tmp   # Claude Code's own temp folder per arm: /tmp/claude-<uid> would show every arm's name
mkdir -p "$OUT" "$NOTES" "$WORK" "$TMPDIR"
LIMIT_WAIT_S=${LIMIT_WAIT_S:-900}; LIMIT_TRIES=${LIMIT_TRIES:-40}; GATE_WAIT_S=${GATE_WAIT_S:-1800}; GATE_TRIES=${GATE_TRIES:-8}
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
# the gate step only: the gate's own tools — never consult/ask_consultants (the question rows can read every repository)
GATE_TOOLS=(mcp__coai__providers mcp__coai__open mcp__coai__review_plan mcp__coai__review_code mcp__coai__resolve mcp__coai__status)
# reading another arm's traces through the vendors' or Claude's own homes is refused outright (also scanned afterwards)
DENY=("Read(//home/jinx/.claude/**)" "Read(//home/jinx/.codex/**)" "Read(//home/jinx/.gemini/**)" "Read(//home/jinx/.antigravity/**)"
  "Read(//home/jinx/.vibe/**)" "Read(//proc/**)" "Read(//home/jinx/git/coai-exp/**)" "Read(//mnt/v/**)")

fill() { # template deadline notes -> filled brief on stdout (no arm id, no scheme name ever reaches a brief)
  node -e '
    const fs=require("fs"); const [tpl,deadline,notes,R,branch,plan,base,scen]=process.argv.slice(1);
    let s=fs.readFileSync(tpl,"utf8");
    const v={BRANCH:branch,DEADLINE:deadline,NOTES:notes,PLAN:plan,BASE:base,SCEN_COUNT:scen,
      REVIEW:`${R}/notes/review.json`,CODE_NOTES:`${R}/notes/code.md`,HREVIEW_NOTES:`${R}/notes/hreview.md`,
      FIX_NOTES:`${R}/notes/fix.md`,WORK:`${R}/work`,GATE_JSON:`${R}/notes/gate.json`};
    s=s.replace(/{{([A-Z_]+)}}/g,(m,k)=>v[k]??process.env["TPL_"+k]??m);
    if(/{{[A-Z_]+}}/.test(s)) { console.error("unfilled: "+s.match(/{{[A-Z_]+}}/g).join(" ")); process.exit(1); }
    process.stdout.write(s);' "$1" "$2" "$3" "$R" "$BRANCH" "$PLAN" "$BASE" "$SCEN_COUNT"
}

has_result() { grep -q '"type":"result"' "$1" 2>/dev/null; }
# a usage/rate limit or an overload: the last result is an error that says so, or the stream has no result and stderr says so
limited() { # stream err
  node -e '
    const fs=require("fs"); const [f,e]=process.argv.slice(1); const RX=/usage.?limit|spend limit|session limit|rate.?limit|hit your (\w+ )*limit|limit reached|limit will reset|resets? at|overloaded|\b529\b|\b429\b|too many requests/i;
    const rs=(fs.existsSync(f)?fs.readFileSync(f,"utf8"):"").split("\n").map(l=>{try{return JSON.parse(l)}catch{return null}}).filter(x=>x&&x.type==="result");
    // the structured fields first (2026-10-28: "...hit your individual spend limit", api_error_status 429, api_error
    // usage_limit_reached — the words alone were missed and nine steps were marked done); the words as a fallback
    const r=rs.pop(); if (r) process.exit(r.is_error && ([429,529].includes(r.api_error_status) || RX.test(String(r.api_error??"")) ||
      RX.test(String(r.result??"")+JSON.stringify(r.errors??""))) ? 0 : 1);
    process.exit(RX.test(fs.existsSync(e)?fs.readFileSync(e,"utf8").slice(-4000):"") ? 0 : 1);' "$1" "$2"
}
errored() { # the stream's last result is an error
  node -e 'const fs=require("fs"); const r=(fs.existsSync(process.argv[1])?fs.readFileSync(process.argv[1],"utf8"):"").split("\n").map(l=>{try{return JSON.parse(l)}catch{return null}}).filter(x=>x&&x.type==="result").pop(); process.exit(r&&r.is_error?0:1)' "$1"
}
transcript_of() { local d; d=$(printf '%s' "$1" | sed 's|[^A-Za-z0-9]|-|g'); ls "$HOME/.claude/projects/$d/$2.jsonl" 2>/dev/null; }   # by name: the folder cannot be listed
used_seconds() {
  node -e '
    const fs=require("fs"); const [timing,step]=process.argv.slice(1); let used=0;
    const rows=fs.existsSync(timing)?fs.readFileSync(timing,"utf8").trim().split("\n").filter(Boolean).map(JSON.parse):[];
    for (const r of rows) if (r.step===step && r.kind==="end" && !r.limited) used+=r.wall_s;
    process.stdout.write(String(used));' "$OUT/timing.jsonl" "$1"
}

step() { # name model minutes template notes mode(write|read|gate)
  local name=$1 model=$2 minutes=$3 tpl=$4 notes=$5 mode=$6
  [ -f "$OUT/$name.done" ] && { log "$name already done — skipped"; return 0; }
  local tries=0
  while :; do
    local attempt=$(( $(ls "$OUT"/$name.a*.jsonl 2>/dev/null | wc -l) + 1 ))
    local sid_file=$OUT/$name.session sid used remaining t0 deadline how
    t0=$(date -u +%s)
    used=$(used_seconds "$name"); remaining=$(( minutes * 60 - used )); [ $remaining -lt 300 ] && remaining=300
    deadline="$(date -u -d "@$((t0 + remaining))" '+%H:%M UTC') ($(date -d "@$((t0 + remaining))" '+%H:%M %Z'))"
    local tools=("${WRITE_TOOLS[@]}") extra=(--permission-mode acceptEdits) mcp=$R/mcp-none.json
    [ "$mode" = read ] && { tools=("${READ_TOOLS[@]}"); extra=(--permission-mode default --disallowedTools Edit Write NotebookEdit "${DENY[@]}"); }
    [ "$mode" != read ] && extra+=(--disallowedTools "${DENY[@]}")
    [ "$mode" = gate ] && { tools+=("${GATE_TOOLS[@]}"); mcp=$R/mcp-coai.json; }
    local session_args prompt_file=$OUT/$name.a$attempt.prompt.md
    if [ -f "$sid_file" ] && [ -n "$(transcript_of "$DIR" "$(cat "$sid_file")")" ]; then
      sid=$(cat "$sid_file"); session_args=(--resume "$sid"); how=resume
      cat > "$prompt_file" <<EOF
This session was interrupted (a usage limit, the machine or the harness stopped it) and is now resumed. Nothing you did
before is lost unless it was not saved: check \`git status\` and \`git log --oneline $BASE..HEAD\` first, then continue the
same task from where you stopped. Your new deadline, for the time that is left of your budget, is: **$deadline**. The rest of
your brief is unchanged.
EOF
    else
      sid=$(cat /proc/sys/kernel/random/uuid); echo "$sid" > "$sid_file"; session_args=(--session-id "$sid"); how=fresh
      fill "$tpl" "$deadline" "$notes" > "$prompt_file" || { log "$name: the brief has unfilled fields"; exit 5; }
    fi
    local stream=$OUT/$name.a$attempt.jsonl err=$OUT/$name.a$attempt.err
    echo "{\"kind\":\"start\",\"arm\":\"$ARM\",\"step\":\"$name\",\"attempt\":$attempt,\"how\":\"$how\",\"session\":\"$sid\",\"model\":\"$model\",\"start_utc\":\"$(date -u -d @$t0 +%FT%TZ)\",\"budget_s\":$remaining,\"deadline\":\"$deadline\",\"head\":\"$(git -C "$DIR" rev-parse --short HEAD)\",\"stream\":\"$stream\"}" >> "$OUT/timing.jsonl"
    log "$name attempt $attempt ($how, session $sid, budget ${remaining}s)"
    (cd "$DIR" && timeout -s INT -k 60 $(( remaining + 600 )) claude -p --model "$model" --output-format stream-json --verbose \
        --strict-mcp-config --mcp-config "$mcp" "${session_args[@]}" "${extra[@]}" --add-dir "$NOTES" --add-dir "$WORK" \
        --allowedTools "${tools[@]}" < "$prompt_file" > "$stream" 2> "$err")
    local rc=$? t1 lim=false; t1=$(date -u +%s)
    limited "$stream" "$err" && lim=true
    echo "{\"kind\":\"end\",\"arm\":\"$ARM\",\"step\":\"$name\",\"attempt\":$attempt,\"end_utc\":\"$(date -u -d @$t1 +%FT%TZ)\",\"wall_s\":$((t1 - t0)),\"exit\":$rc,\"result\":$(has_result "$stream" && echo true || echo false),\"limited\":$lim,\"head\":\"$(git -C "$DIR" rev-parse --short HEAD)\"}" >> "$OUT/timing.jsonl"
    if [ "$lim" = true ]; then
      tries=$((tries + 1)); [ $tries -gt "$LIMIT_TRIES" ] && { log "$name: still limited after $LIMIT_TRIES waits — stopping; run me again later"; exit 6; }
      log "$name LIMITED ($(tail -c 200 "$err" | tr '\n' ' ')) — waiting ${LIMIT_WAIT_S}s, then resuming the same session (wait $tries)"
      sleep "$LIMIT_WAIT_S"; continue
    fi
    if errored "$stream"; then   # an error result that is not a limit is never "done": stop, so a person looks
      log "$name ended in an ERROR result that is not a limit ($(node -e 'const l=require("fs").readFileSync(process.argv[1],"utf8").trim().split("\n").map(x=>{try{return JSON.parse(x)}catch{return null}}).filter(x=>x&&x.type==="result").pop(); console.log(String(l?.result??"").slice(0,200))' "$stream")) — stopping; run me again to resume it"; exit 2
    fi
    if has_result "$stream" || [ $rc -eq 124 ] || [ $rc -eq 137 ]; then
      echo "{\"attempts\":$attempt,\"exit\":$rc,\"stream\":\"$stream\"}" > "$OUT/$name.done"
      log "$name done (exit $rc)"; return 0
    fi
    log "$name INTERRUPTED (exit $rc, no result) — run me again to resume it"; exit 2
  done
}

review_json() { # the review's final message is the JSON; keep it where the fixer reads it
  [ -s "$NOTES/review.json" ] && return 0
  node -e '
    const fs=require("fs"); const f=JSON.parse(fs.readFileSync(process.argv[1],"utf8")).stream;
    const r=fs.readFileSync(f,"utf8").trim().split("\n").map(l=>{try{return JSON.parse(l)}catch{return null}}).filter(x=>x&&x.type==="result").pop();
    const text=r?.result??""; const m=text.match(/\{[\s\S]*\}/);
    fs.writeFileSync(process.argv[2], m?m[0]:JSON.stringify({verdict:"unparsed",raw:text}));' "$OUT/$1.done" "$NOTES/review.json"
}

hreview_step() { # name — a FRESH Haiku session reviews the whole stories and fixes what it found; its diff is kept
  local name=$1
  [ -f "$OUT/$name.before" ] || git -C "$DIR" rev-parse HEAD > "$OUT/$name.before"
  step "$name" haiku 60 "$E/briefs/hr-review-fix.template.md" "$NOTES/hreview.md" write
  [ -f "$OUT/$name.diff" ] || git -C "$DIR" diff "$(cat "$OUT/$name.before")" HEAD > "$OUT/$name.diff"
}

gate_step() { # name — the coai code gate, one arm at a time (the providers' quotas); "blocked" in gate.json → wait, resume
  local name=$1 tries=0 st
  [ -f "$OUT/$name.done" ] && { log "$name already done — skipped"; return 0; }
  [ -f "$OUT/$name.before" ] || git -C "$DIR" rev-parse HEAD > "$OUT/$name.before"
  while :; do
    exec 9> "$E/.gate.lock"; log "$name: waiting for the gate lock"; flock 9; log "$name: gate lock taken"
    step "$name" sonnet 150 "$E/briefs/gate.template.md" "$NOTES/gate.md" gate; local rc=$?
    flock -u 9; exec 9>&-
    [ $rc -ne 0 ] && return $rc
    st=$(node -e 'try{console.log(require(process.argv[1]).status??"missing")}catch{console.log("missing")}' "$NOTES/gate.json")
    if [ "$st" = blocked ] && [ $tries -lt "$GATE_TRIES" ]; then
      tries=$((tries + 1)); rm -f "$OUT/$name.done"
      log "$name: the gate was blocked ($(node -e 'try{console.log(require(process.argv[1]).why??"")}catch{}' "$NOTES/gate.json")) — waiting ${GATE_WAIT_S}s, then resuming (wait $tries)"
      sleep "$GATE_WAIT_S"; continue
    fi
    [ -f "$OUT/$name.diff" ] || git -C "$DIR" diff "$(cat "$OUT/$name.before")" HEAD > "$OUT/$name.diff"
    log "$name: gate status $st"; return 0
  done
}

case $ARM in
  C[0-9]*)
    step 1-code sonnet 180 "$E/briefs/1-code.template.md" "$NOTES/code.md" write
    step 2-review opus 40 "$E/briefs/2-review.template.md" "-" read
    review_json 2-review
    step 3-fix sonnet 60 "$E/briefs/3-fix.template.md" "$NOTES/fix.md" write
    gate_step 4-gate
    ;;
  CR[0-9]*)
    step 1-code sonnet 180 "$E/briefs/1-code.template.md" "$NOTES/code.md" write
    hreview_step 2-hreview
    step 3-review opus 40 "$E/briefs/2-review.template.md" "-" read
    review_json 3-review
    step 4-fix sonnet 60 "$E/briefs/3-fix.template.md" "$NOTES/fix.md" write
    gate_step 5-gate
    ;;
  SRS[0-9]*)
    source "$E/t-lib.sh"; run_t_arm
    gate_step 6-gate
    ;;
  *) echo "usage: run-arm.sh C<n>|CR<n>|SRS<n>" >&2; exit 1 ;;
esac
echo "{\"kind\":\"arm-done\",\"arm\":\"$ARM\",\"done_utc\":\"$(date -u +%FT%TZ)\"}" >> "$OUT/timing.jsonl"
log "arm done"
