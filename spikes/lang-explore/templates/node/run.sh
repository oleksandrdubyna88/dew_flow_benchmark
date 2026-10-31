#!/usr/bin/env bash
# Starts the program once; Node strips the types itself, so there is no build output. The process the tests signal and measure is the program itself (exec).
cd "$(dirname "$0")"
exec node src/main.ts
