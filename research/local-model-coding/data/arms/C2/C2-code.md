# C2 (Sonnet) — E5.5 and E5.6

Branch `exp/kimi-e56-classic-2`. Commits after `b4e2ea96`:
`b3da13d2` (E5.6 rows, vectors, planner arm, flag refusal, TS test, regenerated table), `b1fbee66` (E5.5 check tests),
`add3b2dd` (S5.6.7), then the plan commit (status line + deviations 50-55). Nothing pushed.

## Scenarios
| Scenario | Red seen | Green |
|---|---|---|
| S5.5.1 AKimiCheck_RunsInACopy_OnAOneShotHome | by teeth only (NeedsASanitisedSnapshot=false -> record-failed) | yes |
| S5.5.2 AfterTheCheck_TheHomeIsGone_JunctionFirst (2 arms) | by teeth: recorder red as table says | yes |
| S5.5.3 ACheckCancelledMidTurn_StillDisposesItsHome | by teeth (finally dispose removed) | yes |
| S5.5.4 ARootlessCheck_... | by teeth: recorder red as table says | yes |
| S5.6.1 KimiHasThreeRows_EachCitingItsCells | yes (Of(...) null) | yes |
| S5.6.2 TheVectorsCoverKimi (existing test) | yes, after rows in file, before vectors | yes |
| S5.6.3 ThePlannerArm_... | yes, but message is "found Refused" (matrix refuses first), not "found Scratch" | yes |
| S5.6.4 AdmissionFlagsBothKimiRows | yes (found Refused, as table) | yes |
| S5.6.5 AKimiQuestionLaunch_... | red only as "found Refused" (helper Plan()); the stub-ignoring-plan red can not be made: KimiConsultant already honours the plan | yes |
| S5.6.6 APlanWithFlags_IsRefusedForKimi | yes, "no exception was thrown" (after S5.6.3 arm) | yes |
| S5.6.7 AKimiDiskRow_ReadsItsCopies_... (3 arms) | by teeth: recorder red; coai-vh not empty (Release removed) | yes |
| S5.6.8 TS kimi rows admit flagged... | yes (AssertionError, runtimes list / not a runtime) | yes |

Scenarios marked "by teeth" were green on arrival: E5.3/E5.4 code already existed. Each got a temporary break, ran red, was reverted
(see plan deviation 52). Test names are in the plan's tables; S5.6.8 title is "kimi rows admit flagged, web refuses naming its record".

## Results
- Whole MCP suite: 10555 total, 84 skipped, 4 failed. Two are the known ones (KimiRuntimeTests..., KimiHomeTests...). The other two:
  `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow` (1 ms git-timeout test; fails again alone; not
  touched by me, NOT checked at b4e2ea96) and `AReviewerThatAsksForSourceIsAskedAgainTests.AConversationThatOutlivesItsCap...`
  (failed under load, passed when its class ran alone). Full log: `/home/jinx/git/coai-exp/notes/C2-full-suite.txt`.
- `dotnet format --verify-no-changes --no-restore`: no formatting diff reported; exit is non-zero only because of 54 IL2026/IL3050
  analyzer errors in `src_server` (files I did not touch). Log: `C2-format.txt`.
- Generator: written, then `--check` says up to date (21 rows over 7 runtimes).
- TS (`npm test`): only the known `the baseline names no release that was never tagged` fails. Log: `C2-ts.txt`.
- `plan-lifecycle.mjs`: clean.

## Decisions
- `none` and `disk` kimi rows both `unconfined` (flagged), `web` `unmeasured`; date 2026-10-13, version 2.1.1.
- Flag refusal lives in `KimiConsultant.Build` (`MustCarryNoFlags`): Leading, Flags or AddDirs non-empty -> ArgumentException naming the shim.
- Planner arm `Kimi(grant, flag)`: no flags ever; disk -> Snapshot with SnapshotSources, none -> Scratch.
- `CapabilityMatrixTests.Records` gained the Kimi record (prefix `M-K`); the "unknown runtime" assertions now list `kimi` last.

## Not done / remains
- Windows run of the new service/check/row tests (done on Linux via WindowsStandIn only).
- Live checks (J2), M-K17, review by Opus: out of scope.
- A stray `todo/PLAN_...E5.md.tmp.*` file (a half-written plan edit from the interrupted session) was removed; the plan was edited again by hand.
