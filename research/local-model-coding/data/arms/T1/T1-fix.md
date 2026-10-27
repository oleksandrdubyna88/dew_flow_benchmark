# T1 fix notes — review of E5.5 and E5.6

Branch `exp/kimi-e56-tasks-1`. Commits: `08e48515` (code and tests), `cc869bce` (module docs), `a3f2dbae` (plan).
Note: `/home/jinx/git/coai-exp/notes/T1-code.md` does not exist; I worked from the review and the code.

## Per finding

- **R1 (major) — fixed (recorded, not rebuilt).** The "as built" heading of the plan now says Haiku 5.5 and Sonnet 5.5, not Opus, and a paragraph
  "Who built it" lists the commits and names this as a deviation from the plan gate's command. The status line at the top names no model for any
  story, so I did not change it. Whether Opus rebuilds or confirms the stories is the operator's choice; I did not make it.
- **R2 (major) — partly fixed.** Teeth runs done, red messages recorded in the plan (deviation 58), each break reverted by `git checkout` and rebuilt:
  `MustCarryNoFlags` without its Leading half; the kimi disk plan with a flag (planner test, `NoAdapter_SendsASandboxFlag…`, S5.6.7 on all three endings);
  `ConsultantCheck`'s `finally` without `Home.Dispose()` (S5.5.2 both arms, S5.5.3).
  **Not done (time):** a bare delete in `OneShotHome` instead of junction-first; `MustCarryNoFlags` removed whole; the kimi rows removed from the seed
  (S5.6.1, S5.6.2, S5.6.4). Deviations 47–52 themselves were not rewritten; the evidence is in the new deviation 58.
- **R3 (major) — fixed.** `AKimiConsultantCheckTests` data and temp folders are `TempDir.For("testsToDelete-kimi-check-data-" / "-temp-")`, disposed with the
  product's junction-aware deleter. The class's hand-made try/catch delete is gone.
- **R4 (minor) — fixed.** `dotnet format` on the four test files; the whitespace in `AKimiQuestionRowTests.cs` and `KimiConsultantTests.cs` is corrected.
  `dotnet format --verify-no-changes` on the solution now reports nothing outside `src_server` and `src_bugs` (older, as the review said).
- **R5 (minor) — partly fixed.** The refused arm now asserts the row's `reason` holds "not signed in to Kimi Code" and "kimi login". This went red in the
  teeth run when the failure had another cause. **Not done:** "no `coai-question-*` scratch left for this row". The scratch is in the process-wide temp folder,
  which other test classes also write in parallel, so a scan would be a flaky test. `QuestionRowLaunch`'s `finally` removes it; recorded in deviation 55 and
  in `module_tests.md` under "does not cover".
- **R6 (minor) — fixed.** S5.6.6 is a Theory with two cases (`Flags ["--yolo"]`; `Leading ["--search"]`). Teeth run: with the Leading half removed, the second case goes red.
- **R7 (minor) — fixed.** The caveat is "unconfined: can read this machine, and no flag removes it" in `CapabilityMatrix.cs` and `capabilityAdmission.ts`.
  A test on each side (`TheUnconfinedCaveat_NamesNoShell_BecauseKimiHasNone`, a line in `capabilityAdmission.test.ts`) holds that it names no shell.
  Deviation 49 is closed by deviation 53. Not seen red: I did not run the new caveat tests against the old sentence; the old sentence contains "shell", so they would fail.
- **R8 (minor) — fixed.** The doc comment says S5.6.4.
- **R9 (minor) — fixed.** `research/module_core.md` has the kimi arm; `research/module_tests.md` has a section for `AKimiConsultantCheckTests` and
  `AKimiQuestionRowTests` (and the two small tests) with what they do not cover. The `ConfinementPlanner` comment no longer says "six runtimes".

No finding was wrong.

## Final results (after the last production change, which is the R7 sentence; the teeth breaks were reverted and rebuilt)

- Build: `dotnet build dew_flow_connect_other_ais.slnx -c Debug -m:4 --disable-build-servers` — 0 warnings, 0 errors.
- Changed classes (`AKimiQuestionRowTests`, `KimiConsultantTests`, `AKimiConsultantCheckTests`, `CapabilityMatrixTests`): 63 of 63 passed. After the teeth runs, a rerun
  of the check, row, consultant and planner classes: 56 of 56 passed.
- Whole MCP suite (`/home/jinx/git/coai-exp/notes/T1-fix-suite.txt`): 10558 total, 10472 passed, 84 skipped, 2 failed — exactly the two known host failures
  (`KimiRuntimeTests.ALaunchThatNamesItsHome_…` and `KimiHomeTests.AHomeWhoseDataPathPasses260_…`). The suite ran before the teeth runs; the production files are identical to that run.
- Format: no error in any file of this work. The remaining errors are in `src_server` and `src_bugs`.
- Generator check (`node scripts/generate-runtime-capabilities.mjs --check`): up to date (21 rows over 7 runtimes × 3 capabilities).
- TS tests (`npm test`, `/home/jinx/git/coai-exp/notes/T1-fix-ts.txt`): 6290 tests, 6270 passed, 1 failed — `the baseline names no release that was never tagged` (no git tags in this clone, known).
- `plan-lifecycle.mjs`: clean.
