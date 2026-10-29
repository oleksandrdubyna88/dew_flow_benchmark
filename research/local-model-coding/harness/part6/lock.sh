#!/usr/bin/env bash
# Part 6: while the arms run — (1) the old experiment (coai-exp) is closed (000); (2) the folders above the arms and the
# Claude Code folders that would name or hold another arm's session (projects, file-history, session-env, shell-snapshots)
# cannot be LISTED (300: the owner can still enter and create by name). The arms run as this user; chmod is not in their
# tools. Modes are saved and `lock.sh unlock` restores them. Claude Code's /tmp folder is not touched (it refuses to start
# without it); each arm has its own TMPDIR instead.   Usage: lock.sh lock|unlock
set -u
E=/home/jinx/git/coai-exp2; S=$E/.locked-modes
case ${1:-} in
  lock)
    [ -s "$S" ] && { echo "already locked"; exit 1; }
    closed=(/home/jinx/git/coai-exp)
    unlisted=("$E" "$E/r" "$E/pids" "$HOME/.claude/projects" "$HOME/.claude/file-history" "$HOME/.claude/session-env" "$HOME/.claude/shell-snapshots")
    for p in "${closed[@]}" "${unlisted[@]}"; do stat -c '%a	%n' "$p"; done > "$S"
    chmod 000 "${closed[@]}" && chmod 300 "${unlisted[@]}" && echo "locked: closed ${#closed[@]}, unlisted ${#unlisted[@]}" ;;
  unlock)
    [ -s "$S" ] || { echo "nothing locked"; exit 0; }
    n=0; while IFS=$'\t' read -r m p; do chmod "$m" "$p" && n=$((n+1)); done < <(tac "$S")
    mv "$S" "$S.restored-$(date -u +%Y%m%dT%H%M%SZ)"; echo "unlocked: restored $n" ;;
  *) echo "usage: lock.sh lock|unlock" >&2; exit 1 ;;
esac
