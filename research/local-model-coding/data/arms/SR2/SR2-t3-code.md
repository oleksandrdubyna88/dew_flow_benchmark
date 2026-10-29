# SR2-t3 — code notes (Kimi consultant check, S5.5.1–S5.5.4)

Commit: 94871435 — adds src_mcp/tests/AKimiConsultantCheckTests.cs only. No production code changed.

## What was done
- Wrote 5 test cases in AKimiConsultantCheckTests: S5.5.1 (one), S5.5.2 (theory, 2 cases), S5.5.3 (one), S5.5.4 (one).
- All 5 were green on arrival at b4e2ea96 (E5.3/E5.4 already merged).
- One fix during the work: the S5.5.3 cancel test hung when the shim was never launched (it waited on a
  signal that never came). Changed to `Task.WhenAny(started, check)` and assert `started` completed.

## Teeth runs (each break temporary; restored with git checkout; git status clean after each)
- Break 1 (S5.5.1): ConsultantCheck.PlaceAsync forced to Checkout. 3 tests red.
  Red message differs from the document: the check fails before launch with
  "a Kimi consultant stands in the conversation's sanitised snapshot (Workdir), and none was made — the checkout
  is never its working folder (D6)" (record-failed). The --cwd assertion was never reached.
- Break 2 (S5.5.2, S5.5.3): VibeHomes OneShotHome.Dispose replaced by GitScratch.DeleteEvenIfReadOnly. 3 tests red
  on "the deleter's unlink recorder must not be invoked" (recorder hit .../home/credentials). VibeHomes.cs restored.
- Break 3 (S5.5.2, S5.5.3): ConsultantCheck.TurnAsync finally without held.Home.Dispose(). 3 tests red
  on "the home's folder goes when the check ends" (folder left under coai-vh). Restored.
- Break 4 (S5.5.4): ConsultCheckScratch.Delete with KimiHomesDisposed replaced by true. 1 test red on the recorder
  (invoked for own/kh-*/home/credentials). Restored.
- No real defect found with the code unbroken. No production file changed.

## Test results (Linux host)
- AKimiConsultantCheckTests: 5/5 green, not skipped.
- ConsultantCheckTests: 30 passed, 2 skipped (Vibe, Windows-only guard as before).
- Whole MCP suite (10,539 tests): 5 failed.
  - KimiRuntimeTests.ALaunchThatNamesItsHome… — known, not mine.
  - KimiHomeTests.AHomeWhoseDataPathPasses260… — known, not mine.
  - TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow — NOT in the document's list.
    Fails deterministically when its class runs alone. My file cannot affect that run (the filtered run never
    loads my class). Not investigated further (deadline). Needs an owner for numstats.
  - AReviewerThatAsksForSourceIsAskedAgainTests.AConversationCapOnTheWork… — NOT in the list. Passes alone (16/16).
    Fails only in the full run. Possible shared-static interference; not proven. My class sets static
    GitScratch.AfterUnlink / KimiHome.AfterUnlink and resets them in Dispose; not confirmed as the cause.
  - QuestionRowOnAgyScenarioTests.ATreeChangedByTheFirstLaunch… — NOT in the list. Passes alone (5/5).
    Same status as above.
- dotnet format --verify-no-changes: exit 2, with IL2026/IL3050 analyzer errors in src_bugs and src_server
  (not my files). Did not isolate format to my file before the deadline. Not verified as clean.

## Left undone
- Confirm whether the two full-run-only failures (Reviewer, Agy) come from shared static state in the new class:
  run the suite with AKimiConsultantCheckTests excluded and compare.
- Investigate the TheRoundsNumstats failure (deterministic, unrelated to this task).
- Run dotnet format on my file alone.
- Deadline 13:02 UTC passed; the commit was made at about 13:04 UTC.
