# O1-fix — applying the review of E5.5 and E5.6

Branch: exp/kimi-e56-opus-split-haiku-1. Base for this fix: ea62bb1e. Commits: 3de66332, 6259edf1, 565e1fef. Not pushed.
Note: `O1-code.md` did not exist. The coder's notes were read from `O1-t1..t5-code.md` (t5 is the last one).

## Per finding

| id | severity | outcome | what was done |
|---|---|---|---|
| R1 | major | **fixed** | `AKimiConsultantCheckTests.ARootlessCheck_…` now sets `GitScratch.AfterUnlink` and asserts it is empty. Red-watch done: with the `KimiHomesDisposed` guard removed in `ConsultCheckScratch.Delete` (mutation, reverted), the test fails on the new assertion ("found at least one item … /home/credentials"). With the guard restored: green (5/5 in the class). |
| R2 | major | **fixed** | Folders renamed to the `testsToDelete-` prefix: `testsToDelete-kimi-check-data-`, `-temp-`, `-argv-` (AKimiConsultantCheckTests) and `testsToDelete-kimi-qrow-` (AKimiQuestionRowTests). The class comment is now true for these folders. Not changed: `ConsultantCheckTests.cs` (non-A) still uses `coai-checktest-*`. It is not in the review and is not a Kimi test. |
| R3 | minor | **not fixed** | The refused arm of S5.6.7 is refused at the copy, so no test covers a refusal after the home exists. Reachability: `KimiLaunch.TakeAsync` (`src_mcp/runners/Kimi/KimiLaunch.cs:87`) does refuse on an unusable gate after `Prepare` has made the home, so the reviewer's point is plausible. I did not write the test: I did not trace how `KimiCredentialsLock` behaves with an unusable root (a file at the lock root, for example), and no time was left for a red-first cycle. Deviation 56 is unchanged. Next step: add the arm, assert `coai-vh` empty, `GitScratch.AfterUnlink` empty and `KimiHome.AfterUnlink` one entry, or correct 56 if the gate really cannot be reached. |
| R4 | minor | **fixed** | `KimiConsultant.MustCarryNoFlags` also refuses a planned launch with `AddDirs` or `CwdKind.Root`, with the same "not a plan coai made for kimi" sentence. New test `ConfinementPlannerTests.APlanWithAddDirsOrTheLiveRoot_IsRefusedForKimi` (added to S5.6.6). Red-first: with the old guard the test fails ("no exception was thrown", ConfinementPlannerTests.cs:420, the add-dir arm). With the new guard: 35/35 in the class. |
| R5 | minor | **fixed** | `shared/runtime-capabilities.json`: the kimi sentence is one sentence, starts with a capital, and the `cells` sentence is rejoined. The "3 of 3" count for kimi is removed: the cells the kimi rows cite are not all 3 of 3 (M-K3 is "0 of 9 wrote" in RESULTS §6). I confirmed M-K3's count; I did not re-check the review's M-K4 figure. The generator was run: its output is unchanged (`up to date`), because the generated TS does not carry this text. |
| R6 | minor | **not fixed (operator's ruling)** | The as-built heading says Haiku and §0.2 and the plan gate say Opus. Which model ran the stories is a fact I cannot settle from the code, and the ruling is the operator's. I did not edit the plan's heading or add a deviation. Next step: the operator rules, then either a deviation entry or a heading fix. |

## Results

- Build: `dotnet build dew_flow_connect_other_ais.slnx -c Debug -m:4 --disable-build-servers`: 0 warnings, 0 errors (run several times, last after restoring the guard).
- Changed test classes (MTP exe `src_mcp/tests/bin/Debug/net10.0/CoaiMcp.Tests`, `--filter-class`): ConfinementPlannerTests 35/35; AKimiConsultantCheckTests 5/5; AKimiQuestionRowTests 3/3.
- Full MCP suite: see the "Suite" line below.
- Format: `dotnet format src_mcp/tests/CoaiMcp.Tests.csproj --verify-no-changes --no-restore --include <the 4 changed .cs files>`: exit 0, no output. The whole-solution form `dotnet format dew_flow_connect_other_ais.slnx --verify-no-changes --no-restore` (CI's command) exits 2 with IL2026/IL3050 trimming and AOT errors, all in `src_bugs/src/*.cs` and `src_server/src/*.cs`, which this change does not touch. I did not check whether these errors also occur on base ea62bb1e.
- Generator: `node src_vs_code/scripts/generate-runtime-capabilities.mjs --check`: up to date (21 rows over 7 runtimes × 3 capabilities).
- TS (`npm test` in src_vs_code, via `low-priority.mjs`): 6270 pass, 1 fail. The one failure is `the baseline names no release that was never tagged`, the known no-tags failure from the brief.

## Suite

Full MCP suite (once, after all three commits): total 10556, failed 3, succeeded 10469 (exit 2). Output: `/home/jinx/git/coai-exp/notes/O1-fix-suite.txt`.
- `KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed`: known host failure (brief).
- `KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn`: known host failure (brief).
- `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow`: NOT on the brief's list. It asserts a 1 ms deadline on a git call and got `recorded` where it expected `unavailable: git…`. It is not Kimi code and this change does not touch it. An earlier coder run (O1-t1, t2, t5 notes) also saw it fail and did not explain it. I did not check it on base ea62bb1e. Needs an owner look: likely timing on this host.

## Not in scope, not touched

- The two known host failures (`KimiRuntimeTests.ALaunchThatNamesItsHome…`, `KimiHomeTests.AHomeWhoseDataPathPasses260…`) are in the suite run and are not mine.
- No push, no PR, no `main` merge, no live check, no `kimi` or `vibe` run, no `research/` edit.
