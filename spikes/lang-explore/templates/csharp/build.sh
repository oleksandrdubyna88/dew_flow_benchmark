#!/usr/bin/env bash
# Builds the program into out/. Offline: nuget.config lists no source, and the program needs no package (PACKAGES.md).
set -euo pipefail
cd "$(dirname "$0")"
rm -rf out
dotnet build src/App.csproj -c Release -o out --nologo -v quiet -m:4
