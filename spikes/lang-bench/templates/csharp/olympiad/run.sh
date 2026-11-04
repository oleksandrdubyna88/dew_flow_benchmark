#!/usr/bin/env bash
# Starts the built program once. `exec` makes this process become the program.
set -euo pipefail
cd "$(dirname "$0")"
exec dotnet bin/Release/net10.0/App.dll
