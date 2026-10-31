# Sourced by run-arm.sh for the SRS arms: Sonnet splits the plan into 4–7 tasks (tasks.json + one document per task, with
# exact contracts); Haiku codes each task; tasks whose dependencies are done run at the same time, each in its own git
# worktree under the arm's own folder, and their commits are cherry-picked back onto the arm branch in order. Then a fresh
# Haiku review-and-fix, the Opus review and the Sonnet fixes. Every stage is resumable.
# Uses run-arm.sh's E, R, DIR, ARM, OUT, WORK, NOTES, log, step, review_json, hreview_step.
MAIN=$DIR

make_worktree() { # id
  local id=$1 wt=$R/wt/$1
  [ -d "$wt" ] && return 0
  mkdir -p "$R/wt"
  git -C "$MAIN" worktree add -q "$wt" -b "tmp/$id" HEAD
  local name   # the rules mount comes from the arm's own checkout, never the network
  name=$(git -C "$MAIN" config -f .gitmodules --get-regexp '^submodule\..*\.path$' | awk '$2 == ".agents/conventions" { n = $1; sub(/^submodule\./, "", n); sub(/\.path$/, "", n); print n }')
  git -C "$MAIN" config "submodule.$name.url" "$MAIN/.agents/conventions"
  (cd "$wt" && git -c protocol.file.allow=always submodule update --init -q .agents/conventions)
  [ -d "$MAIN/.agents/conventions/node_modules" ] && ln -s "$MAIN/.agents/conventions/node_modules" "$wt/.agents/conventions/node_modules"
  [ -d "$MAIN/src_vs_code/node_modules" ] && ln -s "$MAIN/src_vs_code/node_modules" "$wt/src_vs_code/node_modules"
  (cd "$wt" && node .agents/conventions/tools/low-priority.mjs dotnet build dew_flow_connect_other_ais.slnx -c Debug -m:4 \
     --disable-build-servers > "$OUT/wt-build-$id.log" 2>&1) || log "WARNING: the warm build of $id's worktree failed"
  log "worktree for $id ready"
}

pick_back() { # id — the task's commits onto the arm branch; a conflict stops the arm (the files were declared disjoint)
  local id=$1
  [ -f "$OUT/picked-$id" ] && return 0
  local before left commits
  before=$(cat "$OUT/task-$id.before")
  left=$(git -C "$R/wt/$id" status --porcelain --untracked-files=all | grep -v 'node_modules' | wc -l)
  [ "$left" -gt 0 ] && log "WARNING: $id left $left uncommitted change(s) in its worktree — not carried over"
  commits=$(git -C "$MAIN" rev-list --reverse "$before..tmp/$id")
  if [ -n "$commits" ] && ! git -C "$MAIN" cherry-pick $commits >> "$OUT/pick-$id.log" 2>&1; then
    git -C "$MAIN" cherry-pick --abort 2>/dev/null
    log "$id: its commits conflict with the arm branch — stopping (see pick-$id.log)"; return 1
  fi
  touch "$OUT/picked-$id"
  log "$id: $(echo "$commits" | grep -c .) commit(s) carried over"
}

drop_worktree() { git -C "$MAIN" worktree remove --force "$R/wt/$1" 2>/dev/null; git -C "$MAIN" branch -D "tmp/$1" > /dev/null 2>&1; true; }

task_field() { # id field — a list comes back as `a`, `b`
  node -e 'const t = require(process.argv[1]).tasks[process.argv[2]]; const v = t[process.argv[3]];
           console.log(Array.isArray(v) ? v.map((x) => "`" + x + "`").join(", ") : v)' "$WORK/batches.json" "$1" "$2"
}

run_task() { # id dir — Haiku codes the task in that directory
  local id=$1
  DIR=$2
  [ -f "$OUT/task-$id.before" ] || git -C "$DIR" rev-parse HEAD > "$OUT/task-$id.before"
  export TPL_TASK=$id TPL_TASK_BRIEF TPL_TASK_FILES TPL_BEFORE
  TPL_TASK_BRIEF=$(task_field "$id" brief); TPL_TASK_FILES=$(task_field "$id" files); TPL_BEFORE=$(cat "$OUT/task-$id.before")
  step "2-$id-code" haiku 45 "$E/briefs/t2-coder.template.md" "$NOTES/$id-code.md" write
  [ -f "$OUT/task-$id-haiku.head" ] || { git -C "$DIR" rev-parse HEAD > "$OUT/task-$id-haiku.head"; git -C "$DIR" diff "$TPL_BEFORE" HEAD > "$OUT/task-$id-haiku.diff"; }
}

run_batches() {
  local n i batch id p pids failed
  n=$(node -e 'console.log(require(process.argv[1]).batches.length)' "$WORK/batches.json")
  for ((i = 0; i < n; i++)); do
    [ -f "$OUT/batch-$i.done" ] && { log "batch $i already done — skipped"; continue; }
    batch=$(node -e 'console.log(require(process.argv[1]).batches[process.argv[2]].join(" "))' "$WORK/batches.json" "$i")
    log "batch $i: $batch"
    if [ "$(echo $batch | wc -w)" -eq 1 ]; then
      ( run_task "$batch" "$MAIN" ) || exit 2
    else
      for id in $batch; do make_worktree "$id"; done
      pids=""; for id in $batch; do ( run_task "$id" "$R/wt/$id" ) & pids="$pids $!"; done
      failed=0; for p in $pids; do wait "$p" || failed=1; done
      [ $failed -eq 1 ] && { log "a task of batch $i was interrupted — run me again to resume it"; exit 2; }
      for id in $batch; do pick_back "$id" || exit 4; done
      for id in $batch; do drop_worktree "$id"; done
    fi
    touch "$OUT/batch-$i.done"
  done
}

run_t_arm() {
  DIR=$MAIN
  step 1-split sonnet 40 "$E/briefs/x-split.template.md" "$NOTES/split.md" write
  if ! node "$E/tasks-plan.mjs" "$WORK" > "$OUT/tasks-plan.log" 2>&1; then
    export TPL_PROBLEMS; TPL_PROBLEMS=$(cat "$OUT/tasks-plan.log")
    log "the task list has problems — one fix round: $(tr '\n' ' ' < "$OUT/tasks-plan.log")"
    step 1b-split-fix sonnet 15 "$E/briefs/x-split-fix.template.md" "$NOTES/split.md" write
    node "$E/tasks-plan.mjs" "$WORK" > "$OUT/tasks-plan.log" 2>&1 || { log "the task list is still invalid: $(tr '\n' ' ' < "$OUT/tasks-plan.log")"; exit 3; }
  fi
  log "$(cat "$OUT/tasks-plan.log")"
  [ -n "$(git -C "$MAIN" status --porcelain)" ] && log "WARNING: the split left changes in the clone"
  run_batches
  DIR=$MAIN
  hreview_step 3-hreview
  step 4-review opus 40 "$E/briefs/2-review.template.md" "-" read
  review_json 4-review
  step 5-fix sonnet 60 "$E/briefs/3-fix.template.md" "$NOTES/fix.md" write
}
