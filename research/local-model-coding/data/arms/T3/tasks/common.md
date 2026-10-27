# Common to every task (t1–t5) — read this file too

- Repository: the clone at `b4e2ea96`, branch `exp/kimi-e56-tasks-3`. Spec: `todo/PLAN_vibe_and_kimi_runtimes_E5.md`, sections
  **E5.5** and **E5.6** (read the section your task names; do not read others).
- Instruction loading: run `node .agents/conventions/tools/rules.mjs check --repo .` and
  `node .agents/conventions/tools/rules.mjs explain --repo . --task implement --task test --file <each file of your task>`, then
  `read` with the same scope. If `check` fails, stop and report.
- Rules from §0 of the plan that bind you: cyclomatic complexity ≤ 4 per method; red first, and the red MESSAGE goes into your
  summary (an assertion message, never a compile error — stub first if a type is missing); every temp folder is
  `TempDir.For("testsToDelete-<what>-")`; no junction to anything outside the test's own `testsToDelete-` folder; no real path,
  name or token of a machine in a test.
- Never `dotnet test`. Build and test (from the repository root):

  ```bash
  node .agents/conventions/tools/low-priority.mjs dotnet build dew_flow_connect_other_ais.slnx -c Debug -m:4 --disable-build-servers
  node .agents/conventions/tools/low-priority.mjs ./src_mcp/tests/bin/Debug/net10.0/CoaiMcp.Tests.exe --filter-class 'CoaiMcp.Tests.<Class>'
  node .agents/conventions/tools/low-priority.mjs ./src_mcp/tests/bin/Debug/net10.0/CoaiMcp.Tests.exe
  node .agents/conventions/tools/low-priority.mjs dotnet format dew_flow_connect_other_ais.slnx --verify-no-changes
  ```

- **This host is Linux (WSL2).** Kimi launches are refused on every host but Windows (D18), so every test that launches the
  played shim starts with `Assert.SkipUnless(OperatingSystem.IsWindows(), "D18: Kimi is refused on every host but Windows");`
  and SKIPS here. For such a test you cannot see the red or the green on this host: say so in your summary ("skipped on
  Linux; red/green not observed"), and make sure it compiles.
- **Known failures at `b4e2ea96` on this host — not yours, do not fix:**
  `KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn`,
  `KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed`, and the extension test
  `the baseline names no release that was never tagged` (the clone has no git tags). The whole suite is "green" when only
  these fail.
- Do not commit. Leave the change in the working tree.
