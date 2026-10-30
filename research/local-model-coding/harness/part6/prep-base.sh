#!/usr/bin/env bash
# Builds the base clone (the plan branch of the real repository), its rules submodule from the local checkout, the npm
# dependencies, and records the baseline: the MCP suite and the extension's tests at the base commit, before any arm.
set -u
E=/home/jinx/git/coai-exp2; SRC=/home/jinx/git/dew_flow_connect_other_ais; B=$E/base; L=$E/prep-base.log
BR=docs/plan-a-spent-quota-cools-the-model-down
[ -d "$B/.git" ] || git clone -q --branch "$BR" "$SRC" "$B"
cd "$B"
name=$(git config -f .gitmodules --get-regexp '^submodule\..*\.path$' | awk '$2 == ".agents/conventions" { n = $1; sub(/^submodule\./, "", n); sub(/\.path$/, "", n); print n }')
git config "submodule.$name.url" "$SRC/.agents/conventions"
git -c protocol.file.allow=always submodule update --init -q .agents/conventions
npm ci --ignore-scripts --prefix .agents/conventions > /dev/null 2>&1
LP="node $B/.agents/conventions/tools/low-priority.mjs"
t0=$(date +%s); $LP dotnet build dew_flow_connect_other_ais.slnx -c Debug -m:4 --disable-build-servers > $E/base-build.log 2>&1; echo "build rc=$? s=$(( $(date +%s)-t0 )) head=$(git rev-parse --short HEAD)" >> $L
t1=$(date +%s); (cd src_vs_code && npm ci --no-audit --no-fund > $E/base-npm.log 2>&1); echo "npm ci rc=$? s=$(( $(date +%s)-t1 ))" >> $L
t2=$(date +%s); $LP ./src_mcp/tests/bin/Debug/net10.0/CoaiMcp.Tests --results-directory $E/base-results --report-xunit-ctrf --report-xunit-ctrf-filename mcp.ctrf.json > $E/base-test.log 2>&1; echo "mcp tests rc=$? s=$(( $(date +%s)-t2 ))" >> $L
t3=$(date +%s); (cd src_vs_code && $LP npm test > $E/base-ts-test.log 2>&1); echo "ts tests rc=$? s=$(( $(date +%s)-t3 ))" >> $L
echo DONE >> $L
