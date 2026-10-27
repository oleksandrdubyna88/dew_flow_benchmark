# H4 — CODER fix of the review of E5.5 and E5.6

Branch `exp/kimi-e56-skeleton-haiku-4`. Commits: `effa0098` (tests), `aae7d3b3` (plan, deviation 60).
Only test files and the plan changed. No production file changed (the teeth run changed `KimiHome.cs` for one run and it was reverted; `git status` is clean).

## Per finding

| Id | Severity | Result |
|---|---|---|
| R1 | major | **Fixed.** Checked first: the finding is right. The tests only said "the deleter never met the junction". If no home or no junction was made, they stayed green. Now `AKimiConsultantCheckTests` (S5.5.1–S5.5.4) and `AKimiQuestionRowTests` (S5.6.7, answered and cancelled arms) also record `KimiHome.AfterUnlink`. They assert it saw exactly one link: `<home>\home\credentials`, where `<home>` is the `--home` the shim got. The refused arm asserts the recorder is empty (no home is made there). **Teeth run done:** `KimiHome.DisposeAllUnder` made a no-op (this removes it from `VibeHomes.EmptiedUnderItsLock`, `ConsultCheckScratch.KimiHomesDisposed` and the row's `KimiHomesDisposed` together). 7 of 8 tests went red; the refused arm stayed green, as it should. Red message: `Expected _unlinkedByKimiHome to be equal to {".../coai-vh/c7bf4a3f/kh-dd693ddaf6/home/credentials"} because the home had a credentials junction and KimiHome unlinked it first, but found empty collection.` and, for the row, `Expected _unlinkedByKimiHome to contain a single item because the home the row made had a credentials junction, and KimiHome unlinked it first, but the collection is empty.` Mutation reverted. The message is written in the plan, deviation 60. The reviewer said to assert the path as `<home>\credentials`; the real link is `<home>\home\credentials` (`KimiHome.Unlinked`: `Path.Combine(root, "home", "credentials")`), so I assert that. |
| R2 | minor | **Fixed, one part not.** Refused arm: now asserts `status == RowOutcomes.Refused` and that the reason contains `not inside a Git work tree`. Cancelled arm: now pins `OperationCanceledException`, as the Vibe sibling does (it was green on the first run, so the service gives the exception). **Not fixed:** "the row's scratch is gone in the refused arm". The row's scratch is made by `QuestionRowLaunch.MakeScratch` in the system temp folder. `PanelService` gives no way to see its path, and a scan of `coai-question-*` in the shared temp folder would race with other tests. To see it, the test would have to drive `QuestionRowLaunch` directly, as the Vibe test at line 292 does. That is more code than a minor finding needs. In the answered and cancelled arms the row folder is still checked (it was before). |
| R3 | minor | **Fixed.** S5.5.1 asserts the one-shot folder name matches `^[0-9a-f]{8}$`. S5.5.4 asserts that the folder two levels above `--home` is `coai-check-*` and that its parent is the test's own temp folder. |
| R4 | minor | **Fixed.** `During` now lists the files the agent can read (the row's tree, while the shim runs). The test asserts `Mailer.cs` is in the copy and no `AGENTS.md` is. The fixture file `src/AGENTS.md` is now used. |

No finding was wrong.

## Results

- Build (`-m:4 --disable-build-servers`, through `low-priority.mjs`): succeeded, 0 warnings, 0 errors.
- Changed classes `AKimiConsultantCheckTests` and `AKimiQuestionRowTests`: 8 tests, 8 passed.
- Whole MCP suite, once: 10557 total, 10470 passed, 84 skipped, 3 failed:
  - the two known failures (`KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed`, `KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn`);
  - `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow` — **not known, not mine.** It failed in the whole run and in the first run of its class, then passed 10/10 in a second run of its class. It uses a 1 ms deadline for a git call (line 258–261), so it depends on timing. None of the 12 files changed since `b4e2ea96` is near it. I did not change it.
- Format: the two changed files: `dotnet format --verify-no-changes --no-restore --include …` exit 0. The whole solution still reports AOT analyzer errors (IL2026/IL3050) in `src_bugs/src/AdminApi.cs` and `src_server/src/*Endpoints.cs`. Those files are not touched by this branch.
- Generator: `generate-runtime-capabilities.mjs --check` exit 0, "up to date (21 rows over 7 runtimes × 3 capabilities)".
- TS tests (`npm test` in `src_vs_code`): 6290 tests, 6270 passed, 19 skipped, 1 failed — the known `the baseline names no release that was never tagged`.

## What remains

- The refused arm's row scratch is not checked (R2, one part). See above.
- Out of scope as before: J2 live checks, M-K17, RESULTS §31, `research/module_*.md`, the coai gate and the Opus review.
