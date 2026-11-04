#!/usr/bin/env bash
# The program's sources are the .cs files in this folder and its subfolders; App.csproj builds them into bin/Release/net10.0/.
# These files are this stack's pin files and stay as they are: global.json, nuget.config, Directory.Build.props.
set -euo pipefail
cd "$(dirname "$0")"
dotnet build App.csproj -c Release --nologo -m:4
