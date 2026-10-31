#!/usr/bin/env bash
# The type check. Offline: the one type package is restored from the local cache (.npmrc), then tsc checks src/.
set -euo pipefail
cd "$(dirname "$0")"
npm install --offline --no-audit --no-fund --no-package-lock --ignore-scripts > /dev/null
exec tsc --noEmit
