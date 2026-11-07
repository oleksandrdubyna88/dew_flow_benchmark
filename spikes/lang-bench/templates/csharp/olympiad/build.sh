#!/usr/bin/env bash
# The program's sources are the .cs files in this folder and its subfolders, except tests/ (tests go there, outside this build) and any folder named bin, obj or node_modules, in any letter case; App.csproj builds them into bin/Release/net10.0/.
# These fail the build: a nullable warning, a package version problem (NU1603, NU1605) and a use of BinaryFormatter (SYSLIB0011). Every other warning is shown and the build still passes.
# These files are this stack's pin files and stay as they are: global.json, nuget.config, Directory.Build.props.
set -euo pipefail
cd "$(dirname "$0")"
dotnet build App.csproj -c Release --nologo -m:4 --no-incremental -p:TreatWarningsAsErrors=false "-p:WarningsAsErrors=nullable%3BNU1603%3BNU1605%3BSYSLIB0011" -p:Nullable=enable -p:WarningsNotAsErrors= -p:WarningLevel=10 "-p:NoWarn=1701%3B1702"
