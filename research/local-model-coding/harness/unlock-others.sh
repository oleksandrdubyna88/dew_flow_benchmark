#!/usr/bin/env bash
# Restores the modes lock-others.sh saved (reverse order, so the folders open before their contents need it).
set -u
S=/home/jinx/git/coai-exp/locked-modes.txt
[ -s "$S" ] || { echo "nothing locked"; exit 0; }
n=0; while IFS=$'\t' read -r m p; do chmod "$m" "$p" && n=$((n+1)); done < <(tac "$S")
mv "$S" "$S.restored-$(date -u +%Y%m%dT%H%M%SZ)"; echo "restored $n paths"
