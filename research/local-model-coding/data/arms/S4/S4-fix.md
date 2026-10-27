# S4 fix — review of E5.5 and E5.6

Branch `exp/kimi-e56-sonnet-split-haiku-4`. Commits of this fix, none pushed:

- `60a864d7` docs(kimi): R1, R4 (`todo/PLAN_vibe_and_kimi_runtimes_E5.md`)
- `283ebf3c` test(kimi): R3 (`src_mcp/tests/AKimiQuestionRowTests.cs`)

## Per finding

### R1 — major — DoD line ticked too early: FIXED
- The line "Every scenario of E5.2–E5.7 is a test that was watched red…" is now `[ ]` (was `[x]`).
- The line now names what is still not true: the E5.5/E5.6 scenarios that were green on arrival (items 55, 56), S5.4.15's red never produced (item 26), suites not green on this host (item 59), format not clean at repository level (item 60).
- `plan-lifecycle.mjs`: clean.

### R2 — minor — stale sentence in `research/module_runners.md` and missing rows note in `research/module_extension.md`: NOT FIXED
- Reason: `research/` is out of scope for this task (brief: "Out of scope as before: `research/`"). Not changed.
- Still owed: remove the stale "still refused … until E5.6" sentence in `research/module_runners.md` (around line 3718); add the `MustCarryNoFlag` / planned-launch note; add the note on the Kimi rows of `runtimeCapabilities.generated.ts` in `research/module_extension.md`.

### R3 — minor — disk-row test checks the wrong thing: FIXED (test-side)
- Answered arm: the prompt must name the copy under the row's snapshot. The check is `StdIn` contains `<cwd>/snapshot-`. The first attempt used a guessed folder name (`…/src`) and failed; the real name is `snapshot-<hash>`, so that was a wrong guess in my fix, corrected before commit. The copy folder is removed when the row ends, so the test cannot list it afterwards.
- Cancelled arm: `StdIn` must not contain the original root (was only the argv check). The argv check stays.
- Red: the stronger check was first run with the wrong name and failed (`StdIn` did not contain the guessed path). No production defect was involved, so this is a test-side hardening, not a red of a defect.
- Result: `AKimiQuestionRowTests` 3 of 3 green. `dotnet format --verify-no-changes --include` on this file: clean.

### R4 — minor — test name not recorded: FIXED
- Deviation 58 now records the rename: table name `AdmissionFlagsBothRows`, test `AdmissionFlagsBothRows_AndWebIsRefusedNamingItsRecord`.

## Results

- Build (`dotnet build dew_flow_connect_other_ais.slnx -c Debug -m:4 --disable-build-servers`): 0 errors, 0 warnings (after the R3 change).
- Changed test class (`AKimiQuestionRowTests`): 3 of 3 passed.
- Whole MCP suite (`CoaiMcp.Tests`, once, after the R3 commit): total 10555, passed 10468, skipped 84, failed 3:
  - `KimiRuntimeTests.ALaunchThatNamesItsHome_…` — known host failure (brief).
  - `KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed` — known host failure (brief).
  - `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow` — the flake of plan item 59. It touches no Kimi file. Not fixed here: cause not found, not in the review.
  - The extension test `the baseline names no release that was never tagged` is the only extension failure (see below).
- Run note: the suite was slow (8 min 30 s). Some tests were long; the progress line stalled at times, and each stalled test passed when run alone.
- Format (scoped to the changed file, `dotnet format --verify-no-changes --include src_mcp/tests/AKimiQuestionRowTests.cs`): clean.
- Format (repo level, `dotnet format dew_flow_connect_other_ais.slnx --verify-no-changes`): NOT clean, exit 2. 54 `error IL…` trimming/AOT diagnostics: 76 lines in `src_server/src/Jobs/ReviewEndpoints.cs` and others, 32 in `src_bugs`. None is in a file of E5.5 or E5.6. Same finding as item 60 of the plan; not fixed here (not a file of this story, and out of time).
- Generator checks: `generate-runtime-capabilities.mjs --check` up to date (21 rows); `generate-feature-availability.mjs --check` up to date.
- Extension TS tests (`npm --prefix src_vs_code test`, full output captured): 6270 passed, 1 failed, npm exit 1. The one failure is `the baseline names no release that was never tagged` (no git tags on this host), as the brief says.
- `plan-lifecycle.mjs`: clean.

## Shared-host note

- While my runs were going, another session was running `CoaiMcp.Tests` from this same checkout (`notes/O4-fix-suite.txt`). Both used the same built binaries. My suite results are from my own run; the other session's results are its own.
