#!/usr/bin/env bash
# Starts the program once. Node strips the types itself, so there is no build output. `exec` makes this process become the program.
set -euo pipefail
cd "$(dirname "$0")"
exec node src/main.ts
