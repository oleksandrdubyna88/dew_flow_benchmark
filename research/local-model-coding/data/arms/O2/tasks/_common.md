# Common to every task (each brief repeats what it needs; this file is a copy for the reviewer)

- Repository: the clone at `b4e2ea96`, branch `exp/kimi-e56-opus-split-haiku-2`. Linux (WSL2).
- Build: `node .agents/conventions/tools/low-priority.mjs dotnet build dew_flow_connect_other_ais.slnx -c Debug -m:4 --disable-build-servers`
- Tests: ALWAYS the MTP executable, never `dotnet test`:
  `node .agents/conventions/tools/low-priority.mjs ./src_mcp/tests/bin/Debug/net10.0/CoaiMcp.Tests.exe --filter-class 'CoaiMcp.Tests.<Class>'`
  (on Linux the file may be `CoaiMcp.Tests` without `.exe`; use the one that exists.)
- Known failures at the start (NOT yours, do not fix): `KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn`,
  `KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed`, and the extension test `the baseline names no release that was never tagged` (no git tags in the clone).
- Do not commit. Do not touch a file outside your task's `files` list.
