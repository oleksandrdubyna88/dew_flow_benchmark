# DR4 review — E5.5 and E5.6 (reviewer and fixer: Haiku, branch exp/kimi-e56-haiku-direct-review-4)

Range reviewed: b4e2ea96..49501c17 (HEAD). Reviewed at 17:03–17:10 UTC, before any code change. Deadline 17:32 UTC.
Note: the DR3 commit 3981fd46 does not exist in this clone, so DR3's fixes are not on this branch. DR3's open findings were re-checked against HEAD below.

## Findings

- **H1 — major — `src_mcp/tests/AKimiQuestionRowTests.cs:126-146` (refused and cancelled arms, `:108-124`, `:126-146`).** The table's Then for S5.6.7 says "the fake person's folder intact" after EVERY ending. Only the answered arm compares the person's folder. Fix: take `KimiStandIn.PersonFolder(person)` before the run and assert `Equal(before)` in the refused and cancelled arms too.

- **H2 — major — `AKimiQuestionRowTests.cs:97-103`.** S5.6.7 says the prompt names the copies; the test checks only negatives (`NotContain(Repo)`). A test with an empty stdin would pass. Fix: assert the question text reaches the shim's stdin (`Contain("Which helper retries")`).

- **H3 — major — `AKimiConsultantCheckTests.cs:138-165` (S5.5.3).** The table's Then says "gone junction-first". The test wires no `KimiStandIn.Unlinks` and never asserts the deleter unlinked nothing. Fix: wire `using var unlinks = new KimiStandIn.Unlinks();` and assert `unlinks.Seen` is empty, as S5.5.2 and S5.5.4 do.

- **H4 — major — `AKimiConsultantCheckTests.cs:18`, `AKimiQuestionRowTests.cs:21`: `[Collection("fakecli-env")]`.** Still open from DR3 H4: the reason is unidentified, so the failure is masked, not explained. Not fixed in this round (needs a suite run without the collection and a trace; not enough time before the deadline). Next step: run the two classes in the full suite without the collection and find which shared state the failing assertion reads.

- **H5 — major — `src_mcp/runners/Kimi/KimiConsultant.cs:50-55`.** E5.6 decision 3 says a plan with ANY flag is refused. The guard checks `Plan.Flags.Count` only; a planned launch with `Leading` args passes. This is also DR3 H8. Fix: refuse when `Leading` or `Flags` is non-empty, after a failing test (a Theory with `Leading = ["--yolo"]`, in `ConfinementPlannerTests.cs` beside `APlanWithFlags_IsRefusedForKimi`).

- **H6 — minor — `AKimiConsultantCheckTests.cs:186`.** A stray blank line before the closing brace of the class. Fix: remove it.

- **H7 — minor — `CapabilityMatrixTests.cs`, `KimiHasThreeRows_EachCitingItsCells`.** The table names S5.6.2 `TheVectorsCoverKimi`, and no such test exists. The existing `TheVectorsActuallyLoaded_AndCoverEveryPairOfTheTable` covers the three pairs. Not fixed: the existing test covers it. Recorded as a naming gap.

- **H8 — minor — S5.5.1 Then "the scratch repository and its canary unchanged".** Not asserted (DR3 H11, accepted there as implied by the product's own `Broken` check). Not fixed.

- **H9 — process — no teeth runs for any of the 12 scenarios.** The coder's notes say S5.5.1–S5.5.4 and S5.6.2, S5.6.6, S5.6.8 were never seen red. DR3 H10 is still open. Not done in this round: no time for the build-and-delete cycle before the deadline. Note: S5.5.1, S5.5.2 and S5.5.4 pass on the first run because E5.3 and E5.4 are already in; their tables' reds need that code removed.

- **H10 — minor — `ConfinementPlannerTests.cs` / `KimiStandIn.cs`.** Homes-root refusal wording says "Vibe homes folder" for every runtime (coder deviation 7). Out of scope for E5.5 and E5.6 and needs a decision on the wording. Not fixed.

## Re-checked from DR3 (against HEAD)

- DR3 H1 (person's folder in question-row facts): still open at HEAD — see H1 above.
- DR3 H2 (positive prompt assertion): still open — see H2 above.
- DR3 H3 (S5.5.3 recorder): still open — see H3 above.
- DR3 H5 (`M-K9 (e)` in the disk cells): present at HEAD (`shared/runtime-capabilities.json`, the disk row). Correct.
- DR3 H6 (version in notes): the notes at HEAD say "Version 2.1.1 ... as in E5.4" in DR4-code, but the `note` strings in `shared/runtime-capabilities.json` do NOT name the version. Minor, see below.
- DR3 H7 (`disk.Should().NotBeNull()`): at HEAD, `KimiHasThreeRows` uses `NotBeNull` on the `Of(...)` calls before `!`. Fine.
- DR3 H9 (duplicated `StandIn`/`Scopes`): resolved at HEAD — both classes use `KimiStandIn`.

## Minor, found on the second pass

- **H11 — minor — `shared/runtime-capabilities.json`, the kimi `none` and `disk` `note` strings.** E5.6 decision 2 says each row's note carries the version (2.1.1). The note names no version; only `measuredWith.version` does. Fix: prefix "kimi 2.1.1:" to both notes, regenerate the TS, check `--check` (DR3 H6). Cheap; do it with the generator.

## Verified, no finding

- All 12 scenario names exist: S5.5.1–S5.5.4 in `AKimiConsultantCheckTests`, S5.6.1, S5.6.3–S5.6.6 in `CapabilityMatrixTests`/`ConfinementPlannerTests`, S5.6.7 in `AKimiQuestionRowTests` (3 arms), S5.6.8 in `capabilityAdmission.test.ts`.
- Planner arm (`ConfinementPlanner.cs:205-213`): no flags, disk is a `Snapshot` of the granted roots, none is an empty `Scratch`, web is refused by the matrix before it arrives.
- Admission flags: none and disk are `unconfined` with flag `unconfined`; web is `unmeasured`, refused, and the refusal names `RESULTS_kimi_confinement.md`.
- Citations: §6 M-K3, §7 M-K4, §9 M-K6, §12 M-K9, §21 M-K9 (e), §7/§14 web — match the coder's notes.
- `KimiStandIn.cs`: the person's folder listing, the played shim and the unlink recorder are shared; no outside path is touched.
- Generated TS and the JSON agree on rows and cells (checked by the generator, see the results below).

## Fix log (commit by commit, Haiku, 2026-10-27)

- **H5 — fixed in 0549de03.** Red first: `APlanWithLeadingArguments_IsRefusedForKimi` failed with "Expected a <System.ArgumentException> to be thrown, but no exception was thrown." Then `KimiConsultant.Build` refuses `Leading` as well as `Flags`. Green.
- **H1 — fixed in 835341e8.** The person's folder is compared in the refused and cancelled question-row arms (and in the cancelled check). Test-gap fix; the assertions pass on the current code.
- **H2 — fixed in 835341e8.** Positive assertion: the question text reaches the shim's stdin.
- **H3 — fixed in 835341e8.** S5.5.3 wires `KimiStandIn.Unlinks` and asserts it stays empty, and also asserts the person's folder is whole.
- **H6 — fixed in 835341e8.** Stray blank line before the class's closing brace removed.
- **H11 — fixed in 8d194f82.** Both kimi notes start with "kimi 2.1.1:". Generator re-run; `--check` up to date (21 rows, 7 runtimes × 3).
- **H4 — NOT fixed (open).** The `[Collection("fakecli-env")]` workaround is unexplained (see H4 above). Needs a suite run without it and a trace. Not done before the deadline.
- **H7 — NOT fixed.** Naming gap only: the existing `TheVectorsActuallyLoaded_AndCoverEveryPairOfTheTable` covers the three pairs. Left as is.
- **H8 — NOT fixed.** S5.5.1's canary and scratch-unchanged clause is not asserted directly; the product's `Broken` check decides it (accepted in DR3 H11).
- **H9 — NOT done.** No teeth runs. Time.
- **H10 — NOT fixed.** Homes-root refusal wording needs a decision; out of scope.

## Results (Haiku, 2026-10-27)

- Build (Debug, `-m:4 --disable-build-servers`, through low-priority): 0 warnings, 0 errors, after the test edits and after the product fix.
- Changed classes after the fixes: AKimiConsultantCheckTests 5/5, AKimiQuestionRowTests 3/3, ConfinementPlannerTests 35/35, CapabilityMatrixTests 39/39. All passed.
- Format: `dotnet format` on `src_mcp/tests`, `src_mcp/core`, `src_mcp/runners` with `--verify-no-changes --no-restore`: exit 0 each. Whole-solution format (`dotnet format dew_flow_connect_other_ais.slnx`) exited 2 with IL trim warnings from `src_server/src/Jobs/ReviewEndpoints.cs`, which I did not touch (as in DR3).
- Generator: `node src_vs_code/scripts/generate-runtime-capabilities.mjs --check`: up to date, 21 rows over 7 runtimes × 3.
- TS: `npm run typecheck` clean. `npm test`: 6290 tests, 6270 pass, 19 skipped, 1 fail. The fail is the known one: "the baseline names no release that was never tagged".
- Whole MCP suite: see the line below once it finishes (`notes/DR4-suite.txt`).
- Whole MCP suite (`notes/DR4-suite.txt`, after all fixes): total 10557, succeeded 10471, failed 2. The two failures are the known ones: `KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn` and `KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed`. `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow`, which failed in the coder's run, passed in this run (it is timing-sensitive; not investigated).
