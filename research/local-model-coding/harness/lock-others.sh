#!/usr/bin/env bash
# Part 5c (owner's choice, 2026-10-28): while the CR/CH arms run, lock everything that holds another arm's work, so a session
# cannot read it (the arms run as this user, and chmod is not in their allowed tools). Modes are saved; unlock-others.sh
# restores them. Kept open: base, the CR/CH arms and their notes/results/work folders, the scripts, the briefs, chain-z.log.
set -u
E=/home/jinx/git/coai-exp; S=$E/locked-modes.txt; P=/home/jinx/.claude/projects
[ -s "$S" ] && { echo "already locked ($S exists)"; exit 1; }
targets=()
for d in "$E"/arm-* "$E"/aborted-* "$E"/judge "$E"/judge-abs "$E"/judge-v1 "$E"/mut "$E"/wt "$E"/haiku-cwd; do
  case $(basename "$d") in arm-CR[0-9]*|arm-CH[0-9]*) ;; *) [ -e "$d" ] && targets+=("$d") ;; esac; done
for d in "$E"/results/* "$E"/vllm/work/*; do case $(basename "$d") in CR[0-9]*|CH[0-9]*) ;; *) targets+=("$d") ;; esac; done
for f in "$E"/notes/*; do [ -f "$f" ] && targets+=("$f"); done
for f in "$E"/RESULTS.md "$E"/PROTOCOL.md "$E"/fix-outcomes.json "$E"/metrics.json "$E"/metrics.txt "$E"/mutate.mjs "$E"/compare.mjs \
         "$E"/judge*.out "$E"/chain.log "$E"/chain-y.log "$E"/vllm/requests-*.jsonl; do [ -e "$f" ] && targets+=("$f"); done
for d in "$P"/-home-jinx-git-coai-exp*; do case $(basename "$d") in *-arm-CR[0-9]*|*-arm-CH[0-9]*) ;; *) targets+=("$d") ;; esac; done
lists=("$E" "$E/notes" "$E/results" "$E/vllm" "$E/vllm/work")   # 300: the owner (the arms run as the owner) can enter and create, not list
for t in "${targets[@]}" "${lists[@]}"; do stat -c '%a	%n' "$t"; done > "$S"
chmod 000 "${targets[@]}"; chmod 300 "${lists[@]}"
echo "locked ${#targets[@]} paths; listing closed on ${#lists[@]} folders; modes in $S"
