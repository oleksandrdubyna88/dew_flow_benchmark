# Common to every task (each brief points here; read it first)

- Repository: the clone root (`git rev-parse --show-toplevel`). Spec: `todo/PLAN_vibe_and_kimi_runtimes_E5.md` §E5.5, §E5.6 (read only your story's table).
- Before you edit: `node .agents/conventions/tools/rules.mjs check --repo .` then
  `node .agents/conventions/tools/rules.mjs explain --repo . --task implement --task test --file <each file of your task>` and read the selected rules
  (`read` with the same scope). If `check` fails: stop and report "instruction loading incomplete".
- Build: `node .agents/conventions/tools/low-priority.mjs dotnet build dew_flow_connect_other_ais.slnx -c Debug -m:4 --disable-build-servers`
- One class: `node .agents/conventions/tools/low-priority.mjs ./src_mcp/tests/bin/Debug/net10.0/CoaiMcp.Tests --filter-class 'CoaiMcp.Tests.<Class>'`
  (Linux: no `.exe`). NEVER `dotnet test`.
- Whole suite at the end: the same executable without a filter. **Known failures at `b4e2ea96` on this Linux host (not yours, do not fix):**
  `KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn`,
  `KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed`. Windows-only tests skip.
- Extension (only t1): `cd src_vs_code && node ../.agents/conventions/tools/low-priority.mjs npm test`. **Known failure at start:** `the baseline names no
  release that was never tagged` (the clone has no git tags) — not yours.
- `dotnet format dew_flow_connect_other_ais.slnx --verify-no-changes` before done.
- Red first: write the test, build, run it, copy the ASSERTION red message into your summary. A compile red is not a red: add a stub, then see the
  assertion red the table names.
- Cyclomatic complexity ≤ 4 per method. Match the comment density and naming of the file you edit (`<summary>` on public members, decisions cited by number).
- Every folder a test makes: `TempDir.For("testsToDelete-<what>-")`. No junction/symlink to anything outside the test's own folder. No real path, user
  name or token in a test (public repository).
- Kimi on Linux: the Kimi launch control refuses every host but Windows (D18). Tests stand in Windows with `WindowsStandIn.Begin()` and
  `KimiSeams.Using(HostKind.Windows, seams)` (copy `StandIn` from `src_mcp/tests/AKimiConsultationScenarioTests.cs:55-62`). Do NOT add
  `Assert.SkipUnless(OperatingSystem.IsWindows())` to a Kimi test that the stand-in lets run.
- Do not commit. Do not touch a file that is not in your task's list. If a scenario cannot be made green without another file, stop and report which and why.
