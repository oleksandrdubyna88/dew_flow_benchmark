#!/usr/bin/env bash
# The program's sources are the .cs files in this folder and its subfolders, except tests/ (tests go there, outside this build); App.csproj builds them into bin/Release/net10.0/.
# A type-safety error (nullable) or a lower package version (NU1603) fails the build; other warnings do not.
# These files are this stack's pin files and stay as they are: global.json, nuget.config, Directory.Build.props.
set -euo pipefail
cd "$(dirname "$0")"
dotnet build App.csproj -c Release --nologo -m:4 -p:TreatWarningsAsErrors=false "-p:WarningsAsErrors=nullable%3BNU1603"
