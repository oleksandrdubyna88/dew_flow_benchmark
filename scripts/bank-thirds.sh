#!/usr/bin/env bash
#
# ONE BUTTON: ten questions per reading group, a third from each of three CLI models, reviewed by the two
# models that did NOT write each question.
#
#   bash scripts/bank-thirds.sh
#
# WHAT YOU MUST DO FIRST (this script checks all of it and stops with a named reason):
#   1. Install and log in to the three CLIs.
#   2. Export one path per CLI:
#        BENCH_CLAUDE_EXE, BENCH_CODEX_EXE, BENCH_GEMINI_EXE
#      The registry stores only the NAMES of these variables — never the paths — so the results database can
#      be published unedited.
#   3. Have the benchmark's Postgres container running. The port is read from Docker here, because it is
#      published ephemerally and changes on every restart.
#
# WHY IT IS SHAPED THIS WAY
#   - It PROBES each CLI first (one trivial question, nothing written). Measured: a wrong headless flag does
#     not fail, it opens an interactive session that waits on a terminal nobody watches, so the symptom is a
#     timeout at the far end of a batch. `codex` and `gemini` argv are marked UNVERIFIED in CliArgv; this is
#     where that gets settled, in ~5 seconds each, instead of costing a 900-second wall per group.
#   - It is RESUMABLE, and that is not optional here: the bank's Postgres is a Docker container, Docker's
#     engine is a WSL distro, and any `wsl --shutdown` takes the database with it. Authoring skips a group
#     that already holds enough from an author; vetting walks only Proposed questions and writes every mark
#     as it is taken. Re-running after an interruption costs the question that was in flight.
#   - Vetting passes NO --allow-self-review. With three authors at a third each, every question's panel is
#     the two models that did not write it: the author is excluded by construction, and the strict rule
#     (unanimity of the ELIGIBLE reviewers) is satisfiable without any self-review.
set -uo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/.." || exit 1

BENCH=./hosts/Cli/bin/Debug/net10.0/bench.exe
CONTAINER=${BENCH_PG_CONTAINER:-postgres-fb96952c}
REPO=${BENCH_TARGET_REPO:-file:///D:/rsd/dew_flow_rag_qln}
COMMIT=${BENCH_TARGET_COMMIT:-64865c6894c5eb4fc22aaefdb2c096dc4ed0f7bb}

# Four of the six groups. `pr-diff` needs merge dates and git history is unreadable inside a worktree;
# `code-writing` has three extra gates and belongs to PLAN_code_lane.
GROUPS=(code-lookup semantic-intent bug-root-cause adversarial)

# One row per model, used as both author and reviewer. The model ID is what the bank records as the author
# and what the eligibility rule compares, so if a CLI turns out to report a different model, add a NEW row
# (there is no edit: a run names the key it measured under) and disable the old one.
AUTHORS=(claude-author codex-author gemini-author)

# 4 + 3 + 3 = 10 per group. The first author writes four because ten does not divide by three.
COUNTS=(4 3 3)

say()  { printf '\n\033[1m%s\033[0m\n' "$*"; }
fail() { printf '\n\033[31m%s\033[0m\n' "$*"; exit 1; }

# ---------------------------------------------------------------- preflight

[[ -x $BENCH ]] || fail "bench.exe is not built — run: dotnet build dew_flow_benchmark.slnx"

for variable in BENCH_CLAUDE_EXE BENCH_CODEX_EXE BENCH_GEMINI_EXE; do
  path=${!variable:-}
  [[ -n $path ]]   || fail "$variable is not set — it must name the CLI's executable on this machine"
  [[ -f $path ]]   || fail "$variable points at '$path', and there is no file there"
done

docker start "$CONTAINER" >/dev/null 2>&1
PORT=$(docker port "$CONTAINER" 2>/dev/null | sed -n 's/.*:\([0-9]\+\)$/\1/p' | head -1)
[[ -n ${PORT:-} ]] || fail "the Postgres container '$CONTAINER' is not running — start Docker, then it"

export BENCH_DB="Host=127.0.0.1;Port=${PORT};Database=bench;Username=postgres;Password=${BENCH_PG_PASSWORD:-bench-local-dev}"
say "database  127.0.0.1:${PORT}  (read from Docker — the published port changes on every restart)"

say "PROBE — does each CLI answer here? Nothing is written."
for author in "${AUTHORS[@]}"; do
  $BENCH models probe --key "$author" --wall-seconds 120 || fail "$author did not answer — fix that before any batch"
done

# ---------------------------------------------------------------- author

say "AUTHOR — 4 + 3 + 3 per group, each question marked with the model that wrote it"
for group in "${GROUPS[@]}"; do
  ordinal=1
  for index in "${!AUTHORS[@]}"; do
    author=${AUTHORS[$index]}
    count=${COUNTS[$index]}
    printf '\n---- %s / %s (%s questions from ordinal %s)\n' "$group" "$author" "$count" "$ordinal"
    $BENCH questions author --group "$group" --authors "$author" \
      --repo "$REPO" --commit "$COMMIT" \
      --count "$count" --ordinal "$ordinal" --wall-seconds 900
    ordinal=$((ordinal + count))
  done
done

# ---------------------------------------------------------------- vet

say "VET — every question judged by the two models that did not write it"
for group in "${GROUPS[@]}"; do
  printf '\n---- %s\n' "$group"
  $BENCH questions vet --group "$group" --repo "$REPO" --commit "$COMMIT" \
    --limit 40 --wall-seconds 300
done

# ---------------------------------------------------------------- report

say "THE BANK"
$BENCH questions groups
$BENCH questions reviewers
$BENCH questions list

say "Accepted questions are selectable by 'bench run --bank-group <key> --bank-from N --bank-to M'."
