# T2 fix notes — review of E5.5 and E5.6

Branch `exp/kimi-e56-tasks-2`. Commits: `1aa5b913` (tests), `ecf63f6b` (module tests catalogue and plan).
Note: `/home/jinx/git/coai-exp/notes/T2-code.md` does not exist. I used `T2-t1-code.md` … `T2-t5-code.md` and the code.

## Per finding

- **R1 (major) — fixed.** I ran `dotnet format --verify-no-changes` on the file first: it failed at lines 161–183 as the review says. I ran
  `dotnet format` on `AKimiQuestionRowTests.cs` and `KimiConsultantTests.cs`; the check on both files is now clean.
- **R2 (major) — fixed.** `research/module_tests.md` has a new last section "The Kimi consultant check and question row — E5.5–E5.6".
  Each test has what it pins and its red. Where the as-built notes do not hold the red message, the cell says "not recorded". I did not invent one.
  "Not covered" names: a Kimi `none` row through `QuestionRowLaunch`, the live check and live rows (RESULTS §31, J2), M-K17, and the scratch check (see R3).
- **R3 (minor) — partly fixed.** The refused arm of S5.6.7 now checks: the status is `failed` (the launch control's refusal), the reason
  holds the `KimiModels.RefusalFor` sentence for `not-a-kimi-model`, the shim never started, and `OneShotRoot` exists (the one-shot
  folder was reserved before the refusal). The existing end checks then prove it is empty. This showed that plan deviation 51 was wrong
  ("no home is made in that arm"); I corrected it. `Release(homes)` removal already turned this arm red (T2 t4 notes).
  **Not done:** "the row's scratch (`coai-question-*`) is gone". The scratch is in the process-wide temp folder, which other test classes write
  in parallel, so a scan would be a flaky test, and the arm has no path of the scratch to check (the shim never starts).
  Recorded in `module_tests.md` under "Not covered".
- **R4 (minor) — fixed.** `APlanWithFlags_IsRefusedForKimi` is now a Theory over capability × (trailing flag, leading flag). Teeth run: with
  `|| planned.Plan.Leading.Count > 0` removed from `KimiConsultant.MustCarryNoFlags`, the two `leading: True` cases went red
  (*Expected a <System.ArgumentException> to be thrown, but no exception was thrown.*); the break was reverted with `git checkout`, and the project rebuilt.

No finding was wrong.

## Final results

All commands ran through `.agents/conventions/tools/low-priority.mjs`. Build: `-m:4 --disable-build-servers`.

- **Build** (`dew_flow_connect_other_ais.slnx`, Debug): 0 warnings, 0 errors.
- **Changed classes** (`AKimiQuestionRowTests`, `KimiConsultantTests`): 20 of 20 passed (after a clean rebuild).
- **Whole MCP suite** (`T2-fix-suite.txt`): 10559 total, 10472 passed, 84 skipped, **3 failed**:
  - the two known host failures: `KimiRuntimeTests.ALaunchThatNamesItsHome_…` and `KimiHomeTests.AHomeWhoseDataPathPasses260_…`;
  - **`TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow` — NEW, cause not found.** It fails at the last assertion
    (`AllChangesAsync(parent, head, 1 ms)` must be `Failed`; it was not). It fails every time in this clone (suite, class alone, after a clean rebuild).
    It passes on the base `b4e2ea96` and on this branch's HEAD `ecf63f6b`, built and run in a clean worktree under the scratchpad (10 of 10 each time).
    So it is not the branch's code: the branch changes no file the test uses, and the test builds its own temp repo.
    I did not find what differs in this clone, and I did not change the test. It is a 1 ms deadline, so a timing or host cause is likely.
- **Format** (`dotnet format dew_flow_connect_other_ais.slnx --verify-no-changes --no-restore`, `T2-fix-format.txt`): exit 2. All 54 errors are in
  `src_bugs/src` and `src_server/src` (not touched by this branch, old). None in `src_mcp`. The two changed test files are clean.
- **Generator check** (`node src_vs_code/scripts/generate-runtime-capabilities.mjs --check`): up to date, 21 rows over 7 runtimes × 3 capabilities, exit 0.
- **TS tests** (`npm test` in `src_vs_code`, `T2-fix-ts.txt`): 6270 passed, 1 failed — the known `the baseline names no release that was never tagged` (no git tags in this clone).
