# T3-t3 — review notes (E5.5, consultant check for Kimi, tests only)

Result: nothing needed a change. No commit made by the reviewer.

## Findings (all "right as it was")
- Scope: the task changes only `src_mcp/tests/AKimiConsultantCheckTests.cs` (1 commit, +205). No production file changed.
- Class: `public sealed class AKimiConsultantCheckTests : IDisposable`, `namespace CoaiMcp.Tests;`, `<summary>` cites E5.5. Right.
- Fixture: `_person`, `_dir` (`testsToDelete-kimi-check-`), `_unlinked`, `_warned`, `_temp`, `_data`; ctor sets `GitScratch.AfterUnlink = _unlinked.Add`;
  `Dispose` nulls both `AfterUnlink` hooks, then disposes `_person`, `_dir`. Right.
- Helpers: `StandIn`/`Scopes` match `AKimiConsultationScenarioTests` WITHOUT `CallingAs(...)`; `Check` matches `ConsultantCheckTests.Check`
  with `--caller claude`; `Settings` matches the spec; `MarkerAnswer` gives the same answer as the Vibe marker test; `ArgOf`, `ThePersonsFolderIsWhole` as specified. Right.
- Tests: names exact; all 5 cases start with the `Assert.SkipUnless(OperatingSystem.IsWindows(), "D18: ...")` line; every assertion of S5.5.1–S5.5.4 is present
  (state "answered", markerRead, `own/tree` cwd, home under `coai-vh`, `kh-` prefix; empty `coai-vh`, `_unlinked` empty, person's folder whole; cancelled -> "failed"/"cancelled";
  rootless home path and no `coai-check-*` left). Right.
- Source check of the cancelled case: `ConsultantCheck.cs:186` catches the caller's cancellation and returns `Failed(... Cancelled ...)`; the home is disposed in `TurnAsync`'s `finally`. The test's expectation fits the code.
- Small deviations from the document, both harmless, kept:
  - `Marked()` factors out the spec's `new PlayedKimi(...) { Turn = ... }` expression (used in 3 cases).
  - `OneShotEntries` treats an absent `coai-vh` folder as empty (the spec's `EnumerateFileSystemEntries` would throw on an absent folder; an absent folder also means "home gone").
- Coder's note on `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow` failing alone: outside this task; not touched.

## Red / green
Not observable on Linux: every case skips (D18). The red-at-`b4e2ea96` check and the one-line mutation proof need Windows; not run. The class compiles against the code at HEAD.

## Results (Linux, WSL2)
- `dotnet build dew_flow_connect_other_ais.slnx -c Debug -m:4 --disable-build-servers`: succeeded, 0 warnings, 0 errors.
- `CoaiMcp.Tests --filter-class CoaiMcp.Tests.AKimiConsultantCheckTests` (through `low-priority.mjs`): total 5, failed 0, succeeded 0, skipped 5 (all with the D18 reason).
- `dotnet format --verify-no-changes --include src_mcp/tests/AKimiConsultantCheckTests.cs`: no output (clean).
- Whole suite not re-run by the reviewer (the task asks only for its own classes).
