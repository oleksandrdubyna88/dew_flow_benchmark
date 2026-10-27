# S3-t4 — code notes: the Kimi consultant check (S5.5.1–S5.5.4)

Task document: /home/jinx/git/coai-exp/vllm/work/S3/t4.md
Worktree: /home/jinx/git/coai-exp/wt/S3-t4 (branch tmp/S3-t4)

## What I did

- Added `src_mcp/tests/AKimiConsultantCheckTests.cs` (namespace `CoaiMcp.Tests`, `[Collection("fakecli-env")]`, four `[Fact]` tests):
  - `AKimiCheck_RunsInACopy_OnAOneShotHome` (S5.5.1)
  - `AfterTheCheck_TheHomeIsGone_JunctionFirst` (S5.5.2, two arms: answered; shim exits 70)
  - `ACheckCancelledMidTurn_StillDisposesItsHome` (S5.5.3)
  - `ARootlessCheck_KeepsTheHomeInItsScratch_AndDisposesItJunctionFirst` (S5.5.4)
- Commit `affd73f3` (adds only that file). No production file changed.

## Deviations from the document (all in the test file, none in production)

- Fixture folders `_data` and `_temp` are `TempDir` (not raw `Directory.CreateTempSubdirectory`), so they are disposed by the same deleter as `_homes`. `string` conversion is implicit, so the contracts still read the same.
- The `StandIn` helper has three scopes, as the document says. It has its own nested `Scopes` class, since the one in `AKimiConsultationScenarioTests` is private.
- No `ExecutablePath` was needed. The Kimi row preflight accepted `new("my-kimi", Runtime: "kimi")` as it is.
- S5.5.3 used the record path, not the throw path. The cancelled check returns a record with `failureKind` = `ConsultFailureKinds.Cancelled`, which matches the document.
- The Dispose method disposes `_temp`, `_data`, `_homes` and `_person` in that order, and clears both recorders.

## Results

- Build (`dew_flow_connect_other_ais.slnx`, Debug): succeeded, 0 warnings, 0 errors.
- `AKimiConsultantCheckTests` on arrival: 4 passed, 0 failed (expected: green on arrival, production unchanged).
- `ConsultantCheckTests` after my change: 30 passed, 0 failed, 2 skipped (the two Windows-only Vibe tests, `D18`).
- The two known MCP failures and the extension failure were not run; they are not in my scope.

## Teeth runs (each reverted at once; `git status` shows only the test file at the end)

(a) `src_mcp/runners/Kimi/KimiConsultant.cs:40` `HomesFolder => string.Empty`:
  - S5.5.1 red: `--home` parent's parent is the check's scratch (`...imi-check-temp-...`), not `...imi-check-homes-...`.
  - S5.5.2 red (both arms) and S5.5.3 red: `DirectoryNotFoundException` on `<homes>\coai-vh`. This differs from the document. With no reservation the `coai-vh` folder is never made, so the enumeration throws, rather than finding items. The test is still red for the right reason (no home was reserved under `coai-vh`).
  - S5.5.4 green (it is the rootless path, which this break does not touch).
(b) `src_mcp/src/Server/Consultation/ConsultantCheck.cs:232` `held.Home.Dispose();` commented out:
  - S5.5.2 red: `Expected ...coai-vh to be empty ..., but found at least one item {".../coai-vh/cfc8a102"}`.
  - S5.5.3 red: `Expected ...coai-vh to be empty ..., but found at least one item {".../coai-vh/499bd531"}`.
  - Matches the document.
(c) `src_mcp/src/Server/Consultation/ConsultCheckScratch.cs` `KimiHomesDisposed`: the `foreach` over `new[] { own, root }` replaced by `foreach (var folder in Array.Empty<string>())`, so `DisposeAllUnder` is never called and the method returns true:
  - S5.5.4 red: `Expected _unlinked to be empty ..., but found at least one item {".../coai-check-.../own/kh-dd693ddaf6/home/credentials"}`. The deleter's recorder was invoked on a junction inside the home. Matches the document ("the recorder is invoked").
  - Note: my first attempt at (c) (a `return true;` placed above the old `return !stopped;`) would leave unreachable code and fail the build under `TreatWarningsAsErrors`. I reverted it before running and used the `Array.Empty` form above.

## Final state

- `git status --short`: `?? src_mcp/tests/AKimiConsultantCheckTests.cs` and `?? src_vs_code/node_modules` (the latter was there at the start; not mine, not touched).
- Nothing pushed.

## Left undone

- Nothing in the t4 scope.
- Not verified: the two MCP tests and the extension test that the document names as failing on this host were not run (out of scope).
