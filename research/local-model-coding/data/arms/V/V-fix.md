# V-fix notes — review of E5.5 and E5.6 (branch exp/kimi-e56-vllm)

Commits: `8d0d0496` (code and tests), `7bb2572d` (docs). Nothing pushed.

## Instruction loading
- Read ENTRY.md and PROJECT.md. Ran `rules.mjs explain` for implement + test + docs with the changed files; it selected 34 rules.
- I did NOT read all of the selected rules in full (the deadline was 30 minutes from the start). Instruction loading is incomplete by ENTRY.md's words. I worked from the review's fixes, the existing sibling tests and PROJECT.md.
- No coai review round was run.

## Findings
| ID | Severity | Result |
|---|---|---|
| R1 | major | **Fixed.** `research/module_core.md`: a Kimi-arm bullet (7 runtimes; no flags; disk on a Snapshot, none on Scratch; both rows `unconfined`, admitted flagged; web `unmeasured`). `research/module_tests.md`: a section with the E5.5/E5.6 tests, their reds and teeth. The review says "both unconfined and flagged"; this matches the seed rows. |
| R2 | major | **Fixed.** S5.6.7: answered and cancelled arms take the `coai-question-*` ancestor of the shim's `--cwd` and assert it is gone. Refused arm: before/after set of `coai-question-*` folders in the temp root. Weakness (written in deviation 57): no seam names the scratch in that path, so a concurrent test class that makes one in that window could fail it. Not seen red separately (the product already removes the scratch). |
| R3 | major | **Fixed.** Teeth run: `KimiHome.DisposeAllUnder` disabled in `VibeHomes.EmptiedUnderItsLock` and `ConsultCheckScratch.KimiHomesDisposed`; 6 of 8 tests red, messages in deviation 57 and `module_tests.md`; reverted (`git checkout`). Positive check added to each test: `KimiHome.AfterUnlink` fired once (none for the refused arm). I also moved the recorder assignments into the test bodies: they are AsyncLocal, and an assignment in the async `InitializeAsync` does not reach the test. Honest limit: with the break, the Fluent assertion on `_linked` failed first, so the `_unlinked` (deleter) assertion was not seen red on its own. |
| R4 | minor | **Fixed.** S5.5.3 awaits the check without a catch and asserts `failed` / `cancelled`. Not seen red (the product already answers so). |
| R5 | minor | **Fixed.** `TempDir.For("testsToDelete-kcheck-data-")`, `…-temp-`, `testsToDelete-qkimi-`. |
| R6 | minor | **Fixed.** `MustCarryNoFlag` also refuses `AddDirs`. Red first: `APlanWithFlags_IsRefusedForKimi(carried: "AddDirs")` — *Expected a <System.ArgumentException> to be thrown, but no exception was thrown.* Now a Theory over Flags, Leading, AddDirs (the `Leading` arm is green before and after: the product already refused it). |
| R7 | minor | **Fixed.** One clause added to the seed `why`: the kimi rows cite the M-K cells of research/RESULTS_kimi_confinement.md (Windows, kimi 2.1.1). Generator run: the generated TS did not change (the `why` text is not in it); `--check` is up to date. |
| R8 | minor | **Fixed, with a limit.** Status line and deviation 58 say E5.5–E5.6 and the fixes were built on Sonnet, not on the Opus of §0.2. The repository does not record who decided it; I did not claim the operator did. The operator should confirm it or order a rebuild on Opus. |

No finding was wrong.

## Not mine / not done
- Known failures on this host: `KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn`, `KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed`, the extension tag test.
- Out of scope: research/, live checks, merge of main, push, PR. `kimi` and `vibe` were never run.

## Final check results
See the end of this file (written after the runs).

### Results (19:50 UTC)
- Build (`-m:4 --disable-build-servers`): 0 warnings, 0 errors.
- Changed classes (`ConfinementPlannerTests`, `AKimiConsultantCheckTests`, `AKimiQuestionRowTests`): 44 of 44 passed.
- Whole MCP suite: 10557 total, 10470 passed, 84 skipped, **3 failed**. Two are the known ones (`KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed`,
  `KimiRuntimeTests.ALaunchThatNamesItsHome_…`). The third is NEW in this run: `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow` — it fails also when its
  class runs alone (`Expected … AllChangesAsync(…, oneMillisecond).Failed to be True … but found False`). It tests a 1 ms git deadline, so I believe it depends on how fast git runs on
  this host. My diff does not touch git numstat code. I did NOT run it on the base commit to prove that, so this is a belief, not a check.
  (The earlier coder run had a different timing failure, `QuestionRowOnAgyScenarioTests…`, which passed this time.)
- Format (`dotnet format --verify-no-changes`): nothing reported except the known IL2026/IL3050 AOT analyser lines of src_server/src_bugs (filtered out of the output).
- Generator: `generate-runtime-capabilities.mjs` ran (file unchanged), `--check` up to date, 21 rows over 7 runtimes × 3 capabilities.
- `plan-lifecycle.mjs`: clean.
- TS tests (`npm test`): 6290 tests, 6270 pass, 1 fail = the known `the baseline names no release that was never tagged`.
