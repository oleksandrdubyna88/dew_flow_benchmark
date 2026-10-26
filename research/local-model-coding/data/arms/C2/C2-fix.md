# C2 (Sonnet) — fixes after the review of E5.5 and E5.6

Branch `exp/kimi-e56-classic-2`. Fix commits after `2b5067e6`:
`2c2b4e6d` (R3, R4 — test code), `a7fec917` (R1 — knowledge base), `af534fb2` (R2, R5 and deviation 54 — flow catalogue and plan). Nothing pushed.

## Per finding

| Id | Severity | Result | What was done |
|---|---|---|---|
| R1 | major | **fixed** | `research/module_core.md`: the diagram says 21 rows (7 runtimes × 3); a new paragraph "Kimi's arm (E5.6)" sits next to Vibe's: no flags, `none` is an empty Scratch folder, `disk` is a Snapshot with SnapshotSources, both rows `unconfined` because of M-K6, `web` unmeasured and refused, `KimiConsultant` refuses a plan with flags. `research/module_runners.md`: the future tense ("will be `unconfined` (S5)") is now the built fact. The claims were checked against `shared/runtime-capabilities.json` (M-K6 text) and the plan's deviations 50–55. |
| R2 | major | **fixed** | `research/module_tests.md`: new section "The Kimi check and the Kimi question row — E5.5 and E5.6" after the E5.4 section. It has one row per test (S5.5.1–S5.5.4, S5.6.1–S5.6.8) with what it asserts and its red or teeth (from deviations 50–54), and a "NOT covered" line: Windows was not run, only Linux through `WindowsStandIn`. One note: the plan's label `TheVectorsCoverKimi` for S5.6.2 is not the real test name; the real test is `TheVectorsActuallyLoaded_AndCoverEveryPairOfTheTable`. The catalogue says so. |
| R3 | minor | **fixed** | The finding is right: the arm used a root outside any git work tree, so the row was refused at the copy, before a home existed. `PlayedKimi` got two init seams: `VersionExit` (exit code of `kimi --version`, default 0) and `VersionHomesThere` (was the home folder there when `--version` ran). The theory `AKimiDiskRow_ReadsItsCopies_AndLeavesNothingBehind` now has four arms: answered; refused at the copy (the old non-git arm, renamed); **refused by the launch control** (granted git root, `--version` exit 1); cancelled. The new arm asserts: the home existed when `--version` ran, then it is gone, the shim never started, `coai-vh` is empty, the deleter's recorder is empty, the person's folder is unchanged. Detail: the row status for this arm is `failed`, not `refused` (the Vibe sibling test asserts `Failed` too); my first draft asserted `Refused` and went red for that reason — the test was wrong, not the code. Deviation 54 is rewritten. No teeth run for the new arm (time). |
| R4 | minor | **fixed** | The test is renamed to `AdmissionFlagsBothRows`, as the table says. Deviation 57 records it. |
| R5 | minor | **fixed** | Deviation 56: E5.5–E5.6 were built on Sonnet, not Opus; the repository records no reason except that the task brief of this experiment arm named Sonnet; the review fixes are by Sonnet too; the owner decides on a rebuild. I did not invent a reason. The status line of the plan now says "reviewed 2026-10-25, review fixes applied (deviations 50–57)". |

No finding was wrong. No finding was left unfixed.

## Results

- Build (`dotnet build dew_flow_connect_other_ais.slnx -c Debug -m:4 --disable-build-servers`, through low-priority): 0 warnings, 0 errors.
- Changed test classes `AKimiQuestionRowTests` and `CapabilityMatrixTests`: 43 tests, all pass (before the final status assertion fix, 1 failed — the R3 draft above; after: `AKimiQuestionRowTests` 4 of 4 pass).
- Whole MCP suite (once, after all commits): 10556 total, 84 skipped, 10470 passed, **2 failed** — exactly the two known host failures (`KimiRuntimeTests.ALaunchThatNamesItsHome_…` and `KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed`). The two flaky tests of the first run (`TheRoundsNumstatsAreCountedTests…`, `AReviewerThatAsksForSourceIsAskedAgainTests…`) passed this time. Log `C2-fix-suite.txt`.
- Format: no formatting diff; exit code 2 only because of the same 54 IL2026/IL3050 analyzer errors in `src_server`/`src_bugs` (log `C2-fix-format.txt`), files I did not touch.
- Generator check (`node scripts/generate-runtime-capabilities.mjs --check`): up to date, 21 rows over 7 runtimes × 3 capabilities.
- TS tests (`npm test`): 1 failure, the known `the baseline names no release that was never tagged` (log `C2-fix-ts.txt`).
