#!/usr/bin/env bash
# Builds and tests the experiment's start commit once, and records the times.
set -u
B=/home/jinx/git/coai-exp/base; LP="node $B/.agents/conventions/tools/low-priority.mjs"; L=/home/jinx/git/coai-exp/prep-base.log
cd $B
t0=$(date +%s); $LP dotnet build dew_flow_connect_other_ais.slnx -c Debug -m:4 --disable-build-servers > /home/jinx/git/coai-exp/base-build.log 2>&1; rc=$?; t1=$(date +%s)
echo "build rc=$rc s=$((t1-t0))" >> $L
(cd src_vs_code && npm ci --no-audit --no-fund > /home/jinx/git/coai-exp/base-npm.log 2>&1); echo "npm ci rc=$? s=$(( $(date +%s)-t1 ))" >> $L
t2=$(date +%s); $LP ./src_mcp/tests/bin/Debug/net10.0/CoaiMcp.Tests > /home/jinx/git/coai-exp/base-test.log 2>&1; echo "mcp tests rc=$? s=$(( $(date +%s)-t2 ))" >> $L
t3=$(date +%s); (cd src_vs_code && $LP npm test > /home/jinx/git/coai-exp/base-ts-test.log 2>&1); echo "ts tests rc=$? s=$(( $(date +%s)-t3 ))" >> $L
echo DONE >> $L
