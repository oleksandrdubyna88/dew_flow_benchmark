#!/usr/bin/env bash
# Starts the built program once; the process the tests signal and measure is the program itself (exec).
cd "$(dirname "$0")"
exec dotnet out/App.dll
