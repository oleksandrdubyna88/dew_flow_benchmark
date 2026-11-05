#!/usr/bin/env bash
# The program's sources are under src/ and it starts at src/main.ts. Packages come from the local cache, as package-lock.json pins them; tsc checks the types and writes nothing.
# These files are this stack's pin files and stay as they are: .npmrc, tsconfig.json, eslint.config.mjs.
set -euo pipefail
cd "$(dirname "$0")"
npm ci --offline
npx --offline tsc --noEmit -p tsconfig.json
