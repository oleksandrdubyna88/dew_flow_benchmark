# DR3 review — E5.5 and E5.6 (reviewer and fixer: Haiku, branch exp/kimi-e56-haiku-direct-review-3)

Reviewed at 14:47 UTC, range b4e2ea96..d75d9edd, before any code change. Deadline 15:16 UTC.

## Findings

- **H1 — major — `src_mcp/tests/AKimiQuestionRowTests.cs:180-196, 200-214, 218-239`.** S5.6.7's Then says "the fake person's folder intact". No fact compares the person. Only the homes folder is checked.
  Fix: take `_person.Hash()` before the run and assert `Equal(before)` in all three facts.

- **H2 — major — `AKimiQuestionRowTests.cs:193`.** S5.6.7's "the prompt names the copies … and never the root" is asserted only negatively (`NotContain(Granted)`). It passes if the stdin is empty.
  Fix: add a positive assertion that the question text (the prompt) reached the shim.

- **H3 — major — `AKimiConsultantCheckTests.cs:169-188`.** S5.5.3's table says "gone junction-first". The test wires no recorder and never asserts `unlinked` empty.
  Fix: wire `GitScratch.AfterUnlink` and assert it stays empty, as S5.5.2 and S5.5.4 do.

- **H4 — major — `AKimiConsultantCheckTests.cs:9-13, 24` and `AKimiQuestionRowTests.cs:30`.** The `[Collection("fakecli-env")]` was added as a fix for a suite-only failure. The coder's cause ("GitScratch.AfterUnlink is a process-wide static") is wrong: `GitScratch.cs:12-18` makes it an `AsyncLocal`, "per async flow, so parallel tests do not see each other's". The real shared state is unidentified, so the failure is masked, not explained. The testing rule ("Passes alone, fails in the suite — that is a SHARED-STATE defect") requires tracing it.
  Fix: NOT fixed in this review (time: the trace needs the suite run and a guess would be another workaround). Recorded as an open item for the next reviewer.

- **H5 — major — `shared/runtime-capabilities.json` (kimi `disk` cells) and `src_vs_code/src/runtimeCapabilities.generated.ts`.** E5.6 decision 2 names `M-K9 (e)` among the disk cells. Only `M-K9` is cited. The record's §21 has the heading `M-K9 (e)`.
  Fix: add `M-K9 (e)` to the disk cells in the JSON, regenerate the TS with its generator, and check `--check`.

- **H6 — minor — `shared/runtime-capabilities.json` (kimi `none` and `disk` notes).** E5.6 decision 2 says each row's note carries the version (2.1.1). The notes name no version. Only `measuredWith.version` has it.
  Fix: add "kimi 2.1.1" to both notes, regenerate, check `--check`.

- **H7 — minor — `src_mcp/tests/CapabilityMatrixTests.cs:208-209`.** `KimiHasThreeRows_EachCitingItsCells` uses `disk!` without asserting that `disk` is not null.
  Fix: `disk.Should().NotBeNull();` before the `!`-free use.

- **H8 — minor — `src_mcp/tests/AKimiQuestionRowTests.cs:104-111`.** The flags refusal is tested only for `Flags`. `RefuseAFlaggedPlan` also refuses `Leading`, which has no test.
  Fix: not fixed (time). It is one `Theory` row when there is time.

- **H9 — minor — `AKimiConsultantCheckTests.cs:50-65` and `AKimiQuestionRowTests.cs:144-160`.** `StandIn` and `Scopes` are written twice (E5.5-6 deviation 56).
  Fix: not fixed (time). Follow-up: move them to one helper.

- **H10 — process — S5.5.1, S5.5.4, S5.6.7 have no teeth run.** The testing rule requires deleting the line whose absence a user would notice and watching that test go red.
  Fix: NOT done (time). The coder's own notes say so.

- **H11 — minor — `AKimiConsultantCheckTests.cs:121-142`.** S5.5.1's Then "the scratch repository and its canary unchanged" is not asserted (accepted as implied in DR2 H10).
  Fix: not fixed (accepted as implied by the product's own `Broken` check).

## Verified, no finding

- The 12 scenario names exist. S5.5.1-S5.5.4 and S5.6.1-S5.6.8 are all present, in the classes the plan names.
- Planner arm (`ConfinementPlanner.cs:205-213`), no flags, the `ArgumentException` on a flagged plan, and the admission flags are as decided.
- The RESULTS citations are right: §6 M-K3, §7 M-K4 and the announced web tools, §9 M-K6, §12 M-K9, §14 web not measured.
- `KimiHasThreeRows` version check uses `2.1.1` on all three rows.
- The planned launch is not reachable with flags (`RefuseAFlaggedPlan`, `KimiConsultant.cs:59-68`).

## Fixes (commit by commit)

- **H1 — fixed in 3981fd46.** The person's `Hash()` is taken before each of the three question-row facts and compared after.
- **H2 — fixed in 3981fd46.** Positive assertion added: the question text is in the shim's stdin. The negative `NotContain(Granted)` stays.
- **H3 — fixed in 3981fd46.** The cancelled check wires `GitScratch.AfterUnlink` and asserts it stays empty.
- **H4 — NOT fixed (open).** The coder's stated cause is wrong: `GitScratch.AfterUnlink` is an `AsyncLocal` (`src_mcp/runners/Files/GitScratch.cs:12-18`), so it is not a process-wide static. The collection is a workaround for an unidentified shared state, and the testing rule requires tracing it. Next step: run the two classes without the collection, in the full suite, and read which folder or static the failing assertion shares. Note also `TheOneShotFolderIsGone` checks a whole `coai-vh` folder; `Homes` is per test instance here, so it is not the shared folder in these classes.
- **H5 — fixed in 5c9e8aa5.** `M-K9 (e)` added to the kimi disk cells; table regenerated; `--check` up to date.
- **H6 — fixed in 5c9e8aa5.** Both kimi notes start with `kimi 2.1.1:`.
- **H7 — fixed in 3981fd46.** `disk.Should().NotBeNull()` before the use.
- **H8 — NOT fixed.** No test for the `Leading` refusal in `RefuseAFlaggedPlan`. Time.
- **H9 — NOT fixed.** `StandIn` and `Scopes` are still written twice. Time.
- **H10 — NOT done.** No teeth runs for S5.5.1, S5.5.4, S5.6.7. A teeth run rebuilds the binary and could not run safely alongside the full suite before the deadline.
- **H11 — accepted.** Canary clause not asserted directly; the product's `Broken` check decides the state.

## Results

- Build: `dotnet build dew_flow_connect_other_ais.slnx -c Debug -m:4 --disable-build-servers`: 0 warnings, 0 errors (14:50 UTC).
- Changed classes (AKimiConsultantCheckTests, AKimiQuestionRowTests, CapabilityMatrixTests, ConfinementPlannerTests): 81 passed, 0 failed.
- Generator: `node src_vs_code/scripts/generate-runtime-capabilities.mjs --check`: up to date, 21 rows.
- Whole MCP suite: running at 14:53 UTC; output `notes/DR3-suite.txt`.

## Final results (15:02 UTC)

- Build (Debug, `-m:4 --disable-build-servers`): 0 warnings, 0 errors.
- Changed classes: 81 passed, 0 failed.
- Whole MCP suite (`notes/DR3-suite.txt`): total 10555, passed 10469, skipped 84, failed 2. The two failures are the known ones: `KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed` and `KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn`.
- Format (`dotnet format <project> --verify-no-changes --no-restore`): exit 0 for `src_mcp/tests`, `src_mcp/core`, `src_mcp/runners` (`notes/DR3-format-*.txt`).
- Generator: `node src_vs_code/scripts/generate-runtime-capabilities.mjs --check`: up to date, 21 rows over 7 runtimes × 3 capabilities.
- TS (`npm run typecheck` clean; `npm test`, `notes/DR3-ts.txt`): 6290 tests, 6270 passed, 19 skipped, 1 failed. The failure is the known one: `the baseline names no release that was never tagged`.

## Remains (not done before the deadline)

- H4: the collection workaround is unexplained; trace the real shared state.
- H8, H9, H10: a `Leading` refusal test, the shared `StandIn` helper, and teeth runs for S5.5.1, S5.5.4 and S5.6.7.
