# SR4 review of E5.5 and E5.6 (Haiku, reviewer and fixer)

Branch `exp/kimi-e56-sonnet-split-haiku-review-4`, range `b4e2ea96..HEAD` (5 commits, 15 files). Written 2026-10-27 17:13 UTC, before any code change.

Rules resolved for the changed files: `local.common.review-gate`, `local.common.vendor-routing`, `common.coding-style`, `common.knowledge-base`, `common.security`, `common.testing`, `common.scenario-tests`, `common.generated-code-tests`, `common.test-leftovers`, `common.planning-docs`, `csharp.doctrine`, `typescript.doctrine`, and others (`resolver check` run, output not blocking).

## Checked and found right (no finding)

- Production: `ConfinementPlanner.Kimi` (`ConfinementPlanner.cs:205-215`) matches E5.6 decision 1 exactly: disk → Snapshot with `SnapshotSources = grant.Roots`; none → Scratch; no flags, no leading argument; web never reaches it.
- `KimiConsultant.MustCarryNoFlags` (`KimiConsultant.cs:85-93`) refuses any `Flags` or `Leading` on a planned launch (decision 3). No production change in `ConsultantCheck` (E5.5 decision 1 holds).
- `RuntimeCapabilities.Runtimes` and `shared/runtime-capabilities.json`: kimi rows `none` and `disk` unconfined with cells M-K6, M-K3, M-K4 and M-K6, M-K9, M-K9 (e), M-K3, M-K4; `web` unmeasured with no cells. Matches decision 2.
- `shared/capability-matrix-vectors.json`: kimi none/disk admitted, flag `unconfined`; web refused. Matches decision 4.
- Generated `runtimeCapabilities.generated.ts`: the diff is the generator's output shape for the three new rows and the `kimi` runtime (generator check still to run).
- Fixtures: `KimiFixture` builds its person under `testsToDelete-kimi-`; both new classes use `testsToDelete-` temp folders; `KimiFixture.Hash()` includes file attributes, so the `ReadOnly` part of S5.5.2's Then is covered.
- S5.5.1, S5.5.2 (both arms), S5.5.3, S5.5.4, S5.6.1, S5.6.2, S5.6.3, S5.6.4, S5.6.5, S5.6.6, S5.6.7 (three endings), S5.6.8: each has a test with the planned name or its suffix-extended name; each asserts the table's *Then*.

## Findings

**H1 — major.** `src_mcp/tests/AKimiQuestionRowTests.cs:126-223` (and `src_mcp/tests/AKimiConsultantCheckTests.cs:118-160`, `:206-242`). The plan's rule for every story is cyclomatic complexity ≤ 4 per method (plan §0, "For every story"). `AKimiDiskRow_ReadsItsCopies_AndLeavesNothingBehind` branches on the ending in four places (the ternary, `if`/`else if`/`else` twice), and `AfterTheCheck_TheHomeIsGone_JunctionFirst` branches on the arm twice. Fix: one Fact per ending (or per arm), with the shared asserts in a helper.

**H2 — minor.** `src_mcp/tests/AKimiConsultantCheckTests.cs:151-155`, `:195-199`. The `if (Directory.Exists(coaiVh))` guard makes "the one-shot folder is gone" pass when no one-shot folder was ever made. The failed-exit-70 arm (S5.5.2) does not prove a home existed during the turn. Fix: capture the home from the turn's request and assert it existed while the turn ran, then assert it is gone after. Do this in the H1 split.

**H3 — minor.** `src_mcp/tests/AKimiQuestionRowTests.cs:198-202`. The refused-before-the-launch arm asserts no shim started, but not that the row's scratch and home are gone. Not verified whether the refusal happens before any folder is made. Fix in the H1 split if the folder exists by then; otherwise note it.

**H4 — minor, not fixed.** `GitScratch.AfterUnlink` is a process-wide static. The two new classes set it, and so do existing classes (`KimiCanaryTests`, `JunctionsTests`, `AOneShotKimiHomeIsDisposedJunctionFirstTests`), none in a `[Collection]`. `xunit.runner.json` sets `maxParallelThreads: 8`, so one class can clear another's recorder. Pre-existing pattern; a fix belongs in its own change, not in this review.

**H5 — minor (documentation).** `todo/PLAN_vibe_and_kimi_runtimes_E5.md:53`. The as-built line puts `KimiHasThreeRows_EachCitingItsCells` at S5.6.4. It covers S5.6.1. S5.6.4 is `AdmissionFlagsBothRows_AndRefusesTheWeb_NamingItsRecord`. Fix: correct the mapping.

## Verified by test or build (see the final report)

The build, the changed test classes, the whole MCP suite, the format check, the generator checks, the TS tests, and the plan-lifecycle check. Results go in the final report, not here.

## Outcome (17:20 UTC)

- **H1 — fixed** (commit `c4b5737b`). `AKimiDiskRow…` is now three Facts (answered, refused, cancelled); `AfterTheCheck` is two Facts over one helper. Each method is a straight line with no branch on the ending. Complexity drop is by construction; no product code changed, so the red-first rule for product defects does not apply. The refactor was green on the first run.
- **H2 — fixed** (commit `c4b5737b`). The helper captures the one-shot home in the `During` hook (which runs before `Turn`, `PlayedKimi.cs:64`) and asserts it existed during the turn. The vacuous `if (Directory.Exists(coaiVh))` guard is gone. The first run was green. This is a test-strengthening change, so the teeth are the new assertion itself: it reads a state the old test never checked.
- **H3 — fixed** (commit `c4b5737b`). The refused arm now asserts `coai-vh` has no entries. The first run failed on the empty `coai-vh` parent container, which the plan's *Then* does not require to be removed. The assertion was narrowed to the `<8>` contents, and the rerun passed. The refusal's own folder is gone.
- **H4 — not fixed.** The `GitScratch.AfterUnlink` static hook is shared by `KimiCanaryTests`, `JunctionsTests`, `AOneShotKimiHomeIsDisposedJunctionFirstTests` and the new classes, none in a `[Collection]`. Pre-existing; fixing it needs a shared collection or a per-test hook, which is a separate change.
- **H5 — fixed** (commit `36ce5ff9`). The as-built line now maps S5.6.1 to `KimiHasThreeRows_EachCitingItsCells` and S5.6.4 to `AdmissionFlagsBothRows_AndRefusesTheWeb_NamingItsRecord`.

No production finding. The planner arm, `MustCarryNoFlags`, the rows, the vectors and the generated table were checked against E5.6 decisions 1–4 and found right.

## Final results (17:24 UTC)

- Build (Debug, `-m:4 --disable-build-servers`): 0 warnings, 0 errors, after the test edits.
- Changed classes: `AKimiConsultantCheckTests` 5/5, `AKimiQuestionRowTests` 3/3.
- Full MCP suite (`CoaiMcp.Tests`): total 10556, succeeded 10469, skipped 84, failed 3.
  - `KimiRuntimeTests.ALaunchThatNamesItsHome_…` and `KimiHomeTests.AHomeWhoseDataPathPasses260_…`: the two known pre-existing failures on this host.
  - `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow`: not on the brief's list. It times a git call against a 1 ms deadline and failed while format and TS checks ran alongside. It passes alone (10/10). No rounds code changed in this range, so I classify it as a load-sensitive flake. Not caused by this work.
- Format (`dotnet format --verify-no-changes`, per project): tests, core and runners all exit 0. The whole-solution run exits 2 only from AOT analyzer messages in `src_server`, which this range does not touch; the same messages appear in earlier stories' logs.
- Generators: `generate-runtime-capabilities.mjs --check` and `generate-feature-availability.mjs --check` both up to date.
- Plan lifecycle: exit 0.
- TS: typecheck and lint clean. `npm test`: 1 failure, `the baseline names no release that was never tagged`, the known pre-existing failure. `kimi rows admit flagged, web refuses…` passes.
