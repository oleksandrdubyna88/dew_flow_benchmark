# O2 t4 — code notes (S5.6.7)

Commit e0ecaac1: "test(kimi): a disk question row reads its copies and leaves nothing behind — S5.6.7 (E5.6)".
Only file: src_mcp/tests/AKimiQuestionRowTests.cs. No production code changed. The mutation was reverted with git checkout; git status is clean.

## Results
- AKimiQuestionRowTests (answered, refused, cancelled): 3 passed.
- AVibeQuestionRowReadsACopyOfItsRootsTests + AKimiConsultationScenarioTests: 21 passed, 12 skipped. The skips are the existing Windows-only skips. I added none.
- Mutation (Release(homes) commented out in the finally of QuestionRowLaunch.cs): all three arms went red with "Expected (Directory.Exists(OneShotRoot) ? ... : []) to be empty ... found at least one item {.../homes/coai-vh/<8>}".
- Whole MCP suite (10555 tests): 3 failed, 10468 passed, 84 skipped.
  - KimiRuntimeTests.ALaunchThatNamesItsHome_... (known).
  - KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed (known).
  - TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow: NOT in the known list. It fails every time its class runs alone. It expects State "failed" for a git call past a 1 ms deadline and gets a verdict. This task does not touch it. I did not investigate further.

## Where I adapted the brief
1. Refused arm. The brief's re-login (slot key replaced) does not refuse a one-shot row. The row writes a fresh home at launch, so there is no earlier home to compare. I replaced the config's managed `oauth` table with a `gone` table instead. The reply then shows status `failed` with reason "not started: not signed in to Kimi Code ...". RowOutcomes.Failed is correct, as the brief said.
2. Answered arm. The brief expected shim.WorkingDirectory under the one-shot folder. In fact the row's tree is its own snapshot folder (/tmp/coai-question-*/tree). Only the Kimi home (kh-<10 hex>) is under coai-vh\<8>. I changed the assertion to `NotStartWith(Homes, ...)`. The home-under-OneShotRoot assertion is kept as written.
3. Usings: I kept the brief's list. The build shows 0 warnings. I did not run an unused-using analyzer.

## Left
- Nothing else in t4. The TheRoundsNumstats failure needs its owner's look.
