#!/usr/bin/env bash
# Quick run W1: the hello programs of both stacks and both kinds through the real runner (plain path, no box). Builds in a scratch copy of the workspace template
# with the registered toolchains and caches, then runs the passing and the failing sample pair: the passing one must exit 0, the failing one 1.
#   bash spikes/lang-bench/test/hello/prove.sh [node|csharp] [daemon|web]     (default: all four)
set -uo pipefail
here="$(cd "$(dirname "$0")" && pwd)"; lb="$(cd "$here/../.." && pwd)"; R="$HOME/rsd/lang-v2"
work="$(mktemp -d)"; trap 'rm -rf "$work"' EXIT
cp -r "$R"/caches/node-*/npm "$work/npm"; cp -r "$R"/caches/csharp-*/nuget "$work/nuget"
mkdir -p "$work/clihome"; cp -r "$R"/caches/csharp-*/dotnet "$work/clihome/.dotnet"; chmod -R u+w "$work"
node_bin="$R/toolchains/node-v26.13.0-linux-x64/bin"; dn="$R/toolchains/dotnet-sdk-10.0.403"
status=0
for stack in ${1:-node csharp}; do for kind in ${2:-daemon web}; do
  ws="$work/$stack-$kind"; cp -r "$lb/templates/$stack/$kind" "$ws"
  if [ "$stack" = node ]; then mkdir -p "$ws/src"; cp "$here/$stack-$kind/main.ts" "$ws/src/main.ts"; else cp "$here/$stack-$kind/Program.cs" "$ws/Program.cs"; fi
  export PATH="$node_bin:$dn:$PATH" DOTNET_ROOT="$dn" NUGET_PACKAGES="$work/nuget" DOTNET_CLI_HOME="$work/clihome" npm_config_cache="$work/npm" \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 DOTNET_CLI_USE_MSBUILD_SERVER=0 MSBUILDDISABLENODEREUSE=1 UseSharedCompilation=false NUGET_CERT_REVOCATION_MODE=offline
  (cd "$ws" && bash build.sh >"$work/build-$stack-$kind.log" 2>&1) || { echo "BUILD FAILED $stack $kind (see $work/build-$stack-$kind.log)"; tail -20 "$work/build-$stack-$kind.log"; status=1; continue; }
  for want in pass:0 fail:1; do
    python3 -I "$lb/runner/run_samples.py" --kind "$kind" --workspace "$ws" --no-build "$here/samples/$kind-${want%%:*}" >"$work/out.txt" 2>/dev/null; code=$?
    if [ "$code" = "${want#*:}" ]; then echo "ok   $stack $kind ${want%%:*} (exit $code)"; else echo "BAD  $stack $kind ${want%%:*} exit $code, wanted ${want#*:}"; cat "$work/out.txt"; status=1; fi
  done
done; done
exit $status
