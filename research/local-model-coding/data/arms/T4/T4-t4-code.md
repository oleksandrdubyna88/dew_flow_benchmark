# t4 — notes (coder)

## Done
- Added `src_mcp/tests/AKimiQuestionRowTests.cs` (the only file changed). Class, `Ending` enum, Theory, and helpers as the contract names them. Per-ending assertions moved into `AnsweredFromItsCopy`, `RefusedBeforeAnyShim`, `CancelledInItsShim`, chosen by a switch expression.
- Build: `dew_flow_connect_other_ais.slnx`, 0 warnings, 0 errors.

## Results
- Class `CoaiMcp.Tests.AKimiQuestionRowTests` (run on this Linux host; the runner is `src_mcp/tests/bin/Debug/net10.0/CoaiMcp.Tests`, no `.exe`):
  - `Answered`: PASS
  - `CancelledMidTurn`: PASS
  - `RefusedBeforeTheLaunch`: FAIL — NOT FIXED (see below)
- Whole suite: total 10556, failed 5, succeeded 10467, skipped 84.
  - `KimiRuntimeTests.ALaunchThatNamesItsHome_...` — known, not this task.
  - `KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed` — known, not this task.
  - `AKimiQuestionRowTests ... RefusedBeforeTheLaunch` — mine (above).
  - `QuestionRowLookupScenarioTests.TheLastTurn_StillAsking_EndsOnItsProse_AndSaysTheLookupsWereCapped` — NOT on the known list. Its recorder in the full run shows launches from two other tests' repos (`/tmp/coai-qagy-repo-*`, `/tmp/coai-qlookup-repo-*`) mixed in: cross-test interference. Run alone, the class passes 6/6. My file does not touch that code.
  - `QuestionRowLookupScenarioTests.AContinuationThatNamesNoConversation_...` — same class, same cause. Run alone it passes.
- `dotnet format … --verify-no-changes`: exits non-zero. All 54 error lines are IL2026/IL3050 analyzer errors in `src_server` and `src_bugs`. None are in `src_mcp`, and none in my file.

## Red checks (temporary breaks, both reverted; `git status` shows only the new file)
(a) `src_mcp/core/QuestionConsult/ConfinementPlanner.cs`: removed `"kimi" => Kimi(grant, flag),`.
   Answered red, verbatim: *Expected copies to contain a single item because one copy per granted root, but the collection is empty.*
   Cancelled red, same cause: *Expected copies to contain a single item, but the collection is empty.*
(b) `src_mcp/runners/Vibe/VibeHomes.cs` `EmptiedUnderItsLock`: commented out the `var stopped` line, the `KimiHome.DisposeAllUnder` block and `if (stopped)`. A first attempt kept `var stopped`, which failed the build (CS0219, warnings are errors); the second attempt is the one that ran.
   Answered and Cancelled red, verbatim (pattern): *Expected recorded to be empty because the junction was unlinked by KimiHome first, never met by the bare deleter (E5.3 decision g), but found at least one item {…/home/credentials}.*

## Not done / needs a decision
- **RefusedBeforeTheLaunch premise does not hold.** The contract expects `KimiLaunch` to refuse with "config.toml is not what coai writes" after a re-login. Actual: the row answers (status `answered`), and the shim runs.
  Why: `KimiLaunch` compares the home's `config.toml` with the config rendered from the person's CURRENT sign-in. `KimiHome.Made` writes that config just before the check, on a fresh one-shot home. So a re-login before a row starts is never a mismatch. The refusal fires only when the sign-in changes between turns of a KEPT home (the S5.4.16 shape, AKimiConsultationScenarioTests.cs:244).
  To reach it from one row, something must change the sign-in between `KimiHome.Made` and the verdict check. The contracts give no such seam (PlayedKimi, StandIn and the fixture have none). I did not add one, and I did not change the assertion.
  Decision needed: either (1) a seam (for example, a hook in PlayedKimi's `--version` path, which is t3's file), or (2) change the Refused arm to the S5.4.16 two-turn shape, or (3) drop the Refused arm. This needs the spec owner.
- Linux runner path differs from the contract (`.exe`).
- Unknown whether the two `QuestionRowLookupScenarioTests` failures exist without this change: I did not run the suite without my file.
- The Refused arm's remaining assertions (Failed status, reason, no shim) are unverified because it fails at the first one.

## Commit
- Committed with the new file only (see commit message). It contains the red Refused arm, so the branch test run is not green.
