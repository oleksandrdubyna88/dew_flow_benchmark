# T4 fix notes — review of E5.5 and E5.6

Branch `exp/kimi-e56-tasks-4`. The review had 4 findings, all minor. No blocking, no major.
`notes/T4-code.md` does not exist; I used the plan's items 31–39 instead.

## Findings

- **R1 (minor) — fixed.** `KimiConsultant.MustCarryNoPlannedFlag` counts `Leading` and `Flags`, but no test set `Leading`.
  Test first: `APlanWithFlags_IsRefusedForKimi` is now a Theory, `APlanWithLeadingOrFlags_IsRefusedForKimi`, with two arms
  (`Leading = ["--search"]` and `Flags = ["--yolo"]`). I removed `plan.Leading.Count +` from the guard. The Leading arm failed
  ("Expected a <System.ArgumentException> to be thrown, but no exception was thrown.") and the Flags arm stayed green. I reverted
  the break (`git checkout`); the production file is unchanged. Commit `817563ae`.
  Because the test has a new name, I also updated the two places that name it: plan table row S5.6.6 and `research/module_tests.md`.
- **R2 (minor) — fixed.** Item 34 now says the table's red was against a stub, and the real red came from the planner's default arm
  (Scratch for `disk`), not from `Build`. I checked row S5.6.5 of the table first: the finding is right. Commit `84541397`.
- **R3 (minor) — fixed.** Item 35 now says the decorator rewrites `config.toml` "during the version probe inside
  `KimiLaunch.VerdictAsync`, after the first shape check and before the second". Commit `84541397`.
- **R4 (minor) — fixed.** The heading now names the plan's attribution (Opus), the builder (Haiku 5.5) and the review fixer
  (Sonnet 5.5). Item 39(a) now says the review fixes (`fb24643c`, `ab94c7da`) were made by Sonnet. Commit `84541397`.

No finding was wrong. None was left unfixed.

## Results (Linux/WSL2 host)

- Build, `dotnet build dew_flow_connect_other_ais.slnx -c Debug -m:4 --disable-build-servers`: succeeded, 0 warnings, 0 errors.
- Changed test class `ConfinementPlannerTests`: 36 of 36 passed.
- Whole MCP suite: 10557 tests, 10470 passed, 84 skipped, 3 failed.
  - The two known host failures: `KimiRuntimeTests.ALaunchThatNamesItsHome_…` and `KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed`.
  - `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow`. This is not mine. It is the plan's item 38 (a 1 ms git
    deadline). It failed in the full run and in 3 runs alone on this host. This branch changes neither the test nor the git code under it.
- Format: `dotnet format … --verify-no-changes --no-restore --include src_mcp/tests/ConfinementPlannerTests.cs` exit 0.
  I did not run it over the whole solution: item 38 says it reports IL2026/IL3050 errors in untouched projects.
- Generator checks: `generate-feature-availability.mjs --check` and `generate-runtime-capabilities.mjs --check` both up to date.
- TS tests (`npm test` in `src_vs_code`): 6390 tests, 6369 passed, 1 failed (`the baseline names no release that was never tagged`,
  known: no git tags in this clone).
- `plan-lifecycle.mjs`: clean.
