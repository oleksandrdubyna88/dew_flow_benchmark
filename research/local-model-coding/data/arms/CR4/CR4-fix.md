# CR4 — fix notes (Sonnet), review of E5.5 and E5.6

Branch `exp/kimi-e56-classic-haiku-review-4`. Three commits: `0bc9bdd6` (R1), `f15db647` (R2–R5), `0c3dbd3f` (R6). No push. Nothing run on Windows. `kimi` and `vibe` never run.

The review has six findings, all **minor**. No blocking or major finding. I checked each one against the code first. All six are right. None is wrong.

Instruction loading: I ran the resolver (`explain` with `--task implement --task test` and my files) and read `common.testing` fully. I did **not** read the other 30 selected rules completely, because the deadline was 30 minutes. This is an incomplete read.

## Findings

- **R1 — fixed.** The caveat "a shell is always there" was false for Kimi (the shim refuses shell commands; Grep and Glob are the reason, M-K6). New text on both sides: `unconfined: can read this machine — no flag removes it` (`CapabilityMatrix.cs`, `capabilityAdmission.ts`). Test first: `AdmissionFlagsBothRows` (C#) and the TS test `kimi rows admit flagged…` now assert the Kimi caveat has no "shell". C# was red with the old text ("Did not expect flag.Caveat() … to contain "shell""), then green. The TS test needs no separate red run; it asserts the same string, which the same edit changed. I did not edit the codex wording elsewhere: `shared/runtime-capabilities.json` still says "a shell is always there" in the codex note and the standing description, which is true for codex. The vector `why` for codex says the same.
- **R2 — fixed.** The finding is right: both hooks are `AsyncLocal` (`GitScratch.cs:12-18`, `KimiHome.cs:59-66`). Removed `[Collection("unlink-hooks")]` and its comment from the four classes, and removed the `KimiHome.AfterUnlink = null` lines from the two new classes. The two pre-existing classes (`JunctionsTests`, `KimiCanaryTests`) are back to their state before `a01f6a0b` for that line.
- **R3 — fixed.** `AKimiQuestionRowTests` now finds the row's `coai-question-*` folder from the copy path and asserts it is gone; and a played turn records whether the copy has `AGENTS.md`, which must be false (the snapshot exclusions hold `AGENTS.md`). Mutation check: with `Remove(scratch)` commented out in `QuestionRowLaunch`, the test is red (answered and cancelled arms: *Expected Directory.Exists(copy) to be False … found True*). Reverted; the tree is clean of it. Note: the copy assertion went red first, so on this host the new scratch assertion was not reached in that mutation. I did not run a second mutation that removes only the scratch.
- **R4 — fixed.** `APlanWithFlags_IsRefusedForKimi` is a Theory with `flags` and `leading`. Both arms green. I did not run a mutation that deletes `{ Leading.Count: > 0 } or`.
- **R5 — fixed.** `ARootlessCheck_KeepsTheHomeInItsScratch_AndDisposesItJunctionFirst` checks `^kh-[0-9a-f]{10}$` on the home's name. Green.
- **R6 — fixed.** The plan's status line now says both review rounds are in. New deviation 60: the story was built on Sonnet, the first review fixes (`a01f6a0b`, `2790f239`) were by Haiku, this round by Sonnet; §0.2 asks for Opus. New deviation 61: what this review changed.

## Checks

- Build (whole solution, `-m:4 --disable-build-servers`): 0 warnings, 0 errors.
- Changed classes (`CapabilityMatrixTests`, `ConfinementPlannerTests`, `AKimiConsultantCheckTests`, `AKimiQuestionRowTests`, `JunctionsTests`, `KimiCanaryTests`): 139 total, 132 passed, 7 skipped (Windows-only), 0 failed.
- Whole MCP suite: see the end of this file.
- `dotnet format --verify-no-changes --no-restore`: clean on `CoaiMcp.Tests` and `CoaiMcp.Core`. The whole solution was not run (the coder's notes say it fails on AOT analyzers in untouched `src_server` files).
- Generator `--check`: up to date (21 rows over 7 runtimes × 3 capabilities).
- TS: `npm run typecheck` clean. `npm test`: one failure, `the baseline names no release that was never tagged` (known, no git tags). The Kimi test is green.

## Whole MCP suite (final)

10556 total, 10452 passed, 84 skipped, **20 failed** (6m 48s). The coder's run had 19 failed of 10555; the one more test is the new `leading` arm of S5.6.6, which passes.

- 2 known: `KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn`, `KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed`.
- 17 from the coder's notes (rounds database empty on this host): `ACodeRoundRecordsItsSizeTests` (7), `ARoundsNumberComesFromTheJournalTests` (3), `ARoundIsMintedAKeyTests` (2), `EndToEndTests` (3), `ThePersonsCommandsReachTheCallerTests` (1), `SplitOrderTests` (1).
- 1 not in the coder's list: `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow` (a git call with a 1 ms deadline is expected to fail; here it does not). It fails the same way alone, and **it fails on `2790f239`, the commit before my changes** (checked in a scratch worktree, removed after). So it is not mine; it was either flaky or hidden in the coder's run. Not fixed: out of scope, timing on this host.
- None of the Kimi, capability or planner tests failed in the full run.

`plan-lifecycle.mjs`: clean when run from the repository root (the first run, from `src_vs_code` by mistake, checked nothing).
