# Common to every task (t1–t6) — read this with your task document

Repository: the clone you work in (commit `b4e2ea96`, branch `exp/kimi-e56-tasks-1`). Spec: `todo/PLAN_vibe_and_kimi_runtimes_E5.md`,
sections **E5.5** and **E5.6** (read only the section your task names).

## Rules
- Do exactly your task. Change ONLY the files your task lists. Another task works on the other files AT THE SAME TIME.
- Do not commit. Do not push.
- Match the surrounding code: XML `<summary>` on each test and each new member, FluentAssertions with a `because` text, file-scoped
  `namespace CoaiMcp.Tests;` in tests, xUnit v3 (`TestContext.Current.CancellationToken`, `Assert.SkipUnless`).
- English only in code, comments and messages.
- Red first: write the test, run it, see it fail for the reason the task gives, THEN write the production code.

## Commands (always through the low-priority wrapper; run from the repository root)
```
node .agents/conventions/tools/low-priority.mjs dotnet build dew_flow_connect_other_ais.slnx -c Debug -m:4 --disable-build-servers
node .agents/conventions/tools/low-priority.mjs ./src_mcp/tests/bin/Debug/net10.0/CoaiMcp.Tests --filter-class 'CoaiMcp.Tests.<Class>'
```
On Windows the test host is `CoaiMcp.Tests.exe`. NEVER `dotnet test` (no VSTest host; it aborts).
The whole MCP suite (only when the task says): the same executable without `--filter-class`.
Extension (only t1): `cd src_vs_code && node ../.agents/conventions/tools/low-priority.mjs npm test`.

## Known failures at `b4e2ea96` on this host (Linux, WSL2) — NOT yours, do not fix them
- `KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn` — fails.
- `KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed` — fails.
- Extension: `the baseline names no release that was never tagged` — fails (the clone has no git tags).
- Tests marked Windows-only (`Assert.SkipUnless(OperatingSystem.IsWindows(), …)`) SKIP here. A skip is not a pass:
  say in your report which of YOUR tests skipped.

## Report at the end
The files you changed; each test you added with red reason seen (copy the failure line) and green; anything the task
said and the code did not match (file:line and what you found). If a test you were told is "green already" is RED, do
NOT change production code that your task does not list: leave the test as written and report the failure line.
