# O3 t2 — code notes (planner kimi arm and KimiConsultant on a planned launch)

Commit: `55302503` on `exp/kimi-e56-opus-split-haiku-3` (not pushed). Files: `src_mcp/core/QuestionConsult/ConfinementPlanner.cs`, `src_mcp/runners/Kimi/KimiConsultant.cs`, `src_mcp/tests/ConfinementPlannerTests.cs`.

## What was done

- `ConfinementPlanner`: `"kimi" => Kimi(grant, flag)` in `Fragments`; new `Kimi` arm after `Vibe`. Disk → `CwdKind.Snapshot` with `SnapshotSources = grant.Roots`; none → `CwdKind.Scratch`; no flags, no leading, no add-dirs. `SandboxVocabulary` unchanged.
- `KimiConsultant.Build`: `MustCarryNoFlags(launch)` after `MustBeLaunchable`; the method is private static, as in the document.
- Tests: `ThePlannerArm_IsACopyForDisk_AnEmptyFolderForNone_AndRefusesWeb`, `AKimiQuestionLaunch_IsTheShimRequest_InItsFolderOrItsCopy_OnTheRowsHome`, `APlanWithFlags_IsRefusedForKimi`; `[InlineData("kimi", Capability.Web, "not been measured")]`; unknown-runtime sentence now ends `vibe, kimi`; helper `KimiAdapter()` used by `Adapters()` and the new launch test.

## Red messages (before the production change)

1. `ThePlannerArm…`: `Expected disk.Cwd to be CwdKind.Snapshot … but found CwdKind.Scratch.` (the `none` assertions passed; the document's sentence matches).
2. `APlanWithFlags_IsRefusedForKimi`: `Expected a <System.ArgumentException> to be thrown, but no exception was thrown.` — as the document says.
3. `AKimiQuestionLaunch…` (before the arm existed): `Expected disk.Request.WorkingDirectory to be … RowTree … but they differ … "/tmp/coai-scratch-…"`.

## Deviations from the document

- **Test 2 was not green on arrival.** The document expected it green once the planner arm existed, but its disk row fails on the arm too: without the Snapshot arm the planned cwd is Scratch, so the disk row's working folder is the scratch folder. It went green after the arm. The `none` row was already green.
- **The places argument in test 2 is the `--home` value itself, not its parent.** The document said `ForShim(<the --home value's parent>, …)`. The adapter's seam receives the home path (`KimiPlaces.ForShim(root, …)` puts `home` under `root`), and `KimiConsultantTests.cs:82` passes the home too. With the parent, `Refusal` reported `KIMI_CODE_HOME is '…/kh-…/home', not the owned home '…/home'`. Changed to `ForShim(home, …)`.
- Test 2 teeth check: with `Cwd` returning `SnapshotOf(launch)` only, the red was `Expected none.Request.WorkingDirectory to be "<Scratch>" … but they differ at index 10` (as the document predicted). Reverted before the commit.

## Results

- Build: 0 errors, 0 warnings.
- `ConfinementPlannerTests` 35/35; `KimiConsultantTests` 12/12; `CapabilityMatrixTests` 39/39; `AKimiConsultationScenarioTests` 12/12.
- Whole suite (`CoaiMcp.Tests`, no filter): 10552 total, 3 failed, 84 skipped.
  - `KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed` — known failure.
  - `KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn` — known failure.
  - `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow` — **not on the known list, not reviewed by me.** Message: `… and not an empty, successful numstat, but found False.` The test uses a 1 ms timeout (`oneMillisecond`) in `DiffSplitter`/`RoundSize`-area code I did not touch, so it looks timing-related. I did not run it on a pre-change tree (that would disturb the shared working tree), so "pre-existing" is not verified.
- `dotnet format --verify-no-changes` exits 2 from IL2026/IL3050 analyzer errors in other projects (`src_bugs`, `src_server`, …). None names my three files (checked with grep on the output). Not fixed: outside my files.

## Left

- Nothing in my scope. The unlisted numstat failure needs a look by whoever owns it.
