# VT-fix notes (review of E5.5 + E5.6) — branch exp/kimi-e56-vllm-think

Commit: 5fbfd165 (all five findings, one commit). No blocking or major finding in the review; five minor ones, all cheap, all applied.

## Per finding
- **R1 (minor) — fixed.** `AKimiQuestionRowTests`: the `During` hook now records the row's scratch (parent of the shim's `--cwd`).
  New `TheRowsScratchIsGone` asserts the folder name starts with `coai-question-` and the folder does not exist after the row, for the
  answered and cancelled arms (the refused arm launches nothing, so it has no scratch to name).
- **R2 (minor) — fixed.** I first tried the reviewer's first option (`ThrowAsync<OperationCanceledException>`). It was RED: "no exception was thrown".
  So the real ending is that the check RECORDS the cancellation. The test now asserts the returned state is `failed` and `failureKind` is
  `cancelled` (`ConsultCheckStates.Failed`, `ConsultFailureKinds.Cancelled`). The try/catch that accepted both endings is gone.
- **R3 (minor) — fixed.** `KimiQuestionLaunchTests`: in the `foreach`, `KimiEnvironment.Refusal(request, Places(home))` must be empty for the none
  and the disk request. `Places(home)` is a small helper, shared with `Consultant()`.
- **R4 (minor) — fixed.** `shared/runtime-capabilities.json`: the `why` clause now names the kimi rows and the M-K cells of
  `research/RESULTS_kimi_confinement.md` (Windows, 3 of 3, kimi 2.1.1). I put it on the SAME line as the vibe clause, so the `:19-20` anchor of
  the plan does not move. The header is not in the generated TS file: generator `--check` is up to date, the generated file did not change.
- **R5 (minor) — fixed.** `AKimiConsultantCheckTests` S5.5.2: the result is kept; state is `answered` for the passing arm and `failed` for the
  exit-70 arm.

No finding was wrong.

## "Red first"
All five are changes to the assertions of tests whose production code is already right. There was no production defect, so no test could be red
for the old code. R2 was red once, as described, and that red decided the final assertion. I did not run mutation checks for R1/R3/R5 (time).

## Results (final tree)
- Build `-m:4 --disable-build-servers`: 0 warnings, 0 errors.
- Changed classes (AKimiQuestionRowTests, AKimiConsultantCheckTests, KimiQuestionLaunchTests): 10 of 10 pass.
- Whole MCP suite, once: 10556 tests, 10469 passed, 84 skipped, 3 failed:
  the two known ones, and `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow`.
  That third one uses a 1 ms git deadline. It failed again when its class ran alone, and PASSED when it ran alone as one method; the file is
  untouched since 4baba7b2. I judge it timing-sensitive on this host (the suite also ran while the format check and the TS tests used the CPU).
  It was not in the coder's run. NOT fixed: out of scope.
- `dotnet format --verify-no-changes --no-restore`: exit 2, but every line is IL2026/IL3050 in `src_bugs`/`src_server` (files I did not touch);
  zero other diagnostics.
- Generator `--check`: up to date (21 rows over 7 runtimes × 3 capabilities).
- TS tests (`npm test`, src_vs_code): 6290 tests, 6270 pass, 1 fail = the known `the baseline names no release that was never tagged`.
  (The other counts are skipped/other kinds.)

## Instruction loading
`rules.mjs check` ran; `explain --task implement --task test` for the four files ran. I did NOT read every selected rule in the time (about 30 minutes).
Instruction loading was therefore not complete, as in the coder's notes.
