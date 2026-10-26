# T3 fix notes (E5.5, E5.6)

Note: `/home/jinx/git/coai-exp/notes/T3-code.md` does not exist (the notes are `T3-t1…t5-code.md`). I worked from the review and the code.

## Findings

- **R1 (blocking) — fixed.** The finding was right: the copies are in the row's own scratch (`<scratch>\tree`), the shim's `--cwd` is that `tree`.
  `AKimiQuestionRowTests` now checks that the prompt contains the shim's `--cwd` and not `Repo\src`. Green on Linux (Windows stand-in).
- **R2 (major) — fixed.** Removed all `Assert.SkipUnless(IsWindows)` from `AKimiConsultantCheckTests` (5) and `AKimiQuestionRowTests` (1), and the
  remarks that said they skip. All cases run green on Linux. Removing the skip showed a second defect: the `refused` ending of S5.6.7 did not
  refuse (the row answered). A changed slot key only changes what a fresh one-shot home is built from. The test now uses a config with an empty
  slot key, which the launch control refuses; green. Red seen with two temporary breaks (reverted, source diff clean):
  `Remove(scratch)` removed → answered and cancelled endings failed on the scratch check; `Leading` check removed → the `Leading` case failed.
  **Not done:** the red for each S5.5 case by a temporary break of its deciding line (only the two above, for the cases I changed), and a run on real
  Windows. Deviation 50 in the plan says so.
- **R3 (minor) — fixed.** The test takes the scratch as the parent of `--cwd`'s `tree` and checks it is gone after the answered and cancelled endings.
  The refused ending launches no shim, so it has no scratch to read; not checked there (said in deviation 53).
- **R4 (minor) — fixed.** S5.5.2 now checks the result state (`answered` for passed, `failed` for exit 70) and that there is one shim with a
  `--home` under `<homes>\coai-vh`, before the disposal checks. Red not seen for this one (no cheap break).
- **R5 (minor) — fixed.** `APlanWithFlags_IsRefusedForKimi` is a Theory (`Flags`, `Leading`). The `Leading` case went red with the check removed.
- **R6 (minor) — fixed.** Status line now says: tests green on the Linux stand-in, mutation proofs and Windows run not done.
- **R7 (minor) — fixed.** Deviations 31–45 are now 47–61 (free range). Removed the pointers to `T3-t1-code.md` / `T3-t2-code.md`, the t1/t3/t4 labels and
  `common.md` (now `.agents/PROJECT.md`). Nothing else in the repository cited the old numbers (checked by grep).
- **R8 (minor) — fixed.** The because-text of `TheOneShotFolderIsEmpty` is now the reason.

## Commits

- `4965f1f4` test(kimi): the E5.5 and S5.6.7 cases run on every host …
- `a6254fdb` docs(plan): E5.5–E5.6 deviations renumbered 47–61 …
- One more plan edit (deviation 56: the final suite result) is committed after these notes; see `git log`.

## Final results

- **Build** (`dotnet build dew_flow_connect_other_ais.slnx -c Debug -m:4 --disable-build-servers`): succeeded, 0 warnings, 0 errors.
- **Changed classes** (`AKimiQuestionRowTests`, `AKimiConsultantCheckTests`, `ConfinementPlannerTests`): 44 total, all passed.
- **Whole MCP suite:** 10557 total, 10470 passed, 84 skipped, 3 failed:
  the two known (`KimiRuntimeTests.ALaunchThatNamesItsHome_…`, `KimiHomeTests.AHomeWhoseDataPathPasses260_…`) and
  `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow` — it fails alone too; it is a git test, it was already in deviation 56
  as failing and unexplained. I did not check it against `b4e2ea96` (no time).
- **Format** (`dotnet format … --verify-no-changes --no-restore --include` the 3 changed files): clean. The whole-solution check was not run again;
  deviation 57 records that it fails on analyzer errors outside these files.
- **Generator check** (`node src_vs_code/scripts/generate-runtime-capabilities.mjs --check`): up to date, exit 0.
- **TS tests** (`npm test --prefix src_vs_code`): 6290 tests, 1 failed — the known `the baseline names no release that was never tagged`.

## Remaining

- The mutation proof of each S5.5 case and a run on real Windows.
- Why `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_…` fails (not related to this work).
