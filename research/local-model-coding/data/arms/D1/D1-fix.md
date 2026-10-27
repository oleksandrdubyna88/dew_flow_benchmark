# D1 fix notes — review of E5.5 and E5.6 (Haiku, exp/kimi-e56-haiku-direct-1)

Written 2026-10-26, UTC. Deadline 21:11 UTC. Commits: `f74c6ca8`, `179aae80` (no push).

## Findings

| ID | Severity | Outcome | Note |
|---|---|---|---|
| R1 | major | fixed | Folder is now `TempDir.For("testsToDelete-kimi-question-")` (test-leftovers rule). Rename only; no product defect, so no red-first step. |
| R2 | major | fixed | Refused and cancelled row arms now set `GitScratch.AfterUnlink` and assert it stays empty, and assert `PersonsFiles()` is unchanged. S5.5.3 does the same. The S5.6.7 scratch-gone check on the cancel and refused arms is NOT added (the scratch path is not captured there; not done for time). |
| R3 | minor | fixed | S5.5.3 try/catch removed. Asserts `code == 0`, `state == failed`, `failureKind == cancelled` with no condition. |
| R4 | minor | fixed | `KimiHome.AfterUnlink` recorder added in S5.5.2 (both arms), S5.5.4 and the S5.6.7 answered arm. Each asserts it was called at least once before the "deleter never met a junction" assertion. |
| R5 | minor | NOT fixed | Test names still differ from the scenario tables (S5.6.7 is three Facts, S5.6.4 is `KimiAdmissionFlagsBothRows_AndRefusesWeb`). Not done for time. The H4/H8 deviation note should record the renames. |
| R6 | minor | fixed | Disk and web rows assert `MeasuredWith.Version == "2.1.1"`. Web refusal asserts `.And.Contain("RESULTS_kimi_confinement.md")`. |
| R7 | minor | fixed | Seed `why` text: kimi rows cite the M-K cells of `research/RESULTS_kimi_confinement.md` (Windows, kimi 2.1.1); `unconfined` now covers a tool that reads the disk outside the folder. The generator does not copy `why` into the TS output, so it wrote no change; `--check` is clean. |

No finding was judged wrong.

## Red-first

The fixes are test-quality changes, not product defects, so there was no defect test to watch fail first.
New assertions were observed GREEN first. Then a teeth check for R4: I replaced the `AfterUnlink?.Invoke(link);` line in
`src_mcp/runners/Kimi/KimiHome.cs:361` with a no-op, rebuilt, and ran the classes. Red, with the right message
("Expected unlinkedByKimiHome not to be empty because KimiHome unlinked the home's junction…"):
`AfterTheCheck_TheHomeIsGone_JunctionFirst` (both arms), `ARootlessCheck_KeepsTheHomeInItsScratch_AndDisposesItJunctionFirst`,
and `AnAnsweredDiskRow_ReadsItsCopy_NeverTheRoot_AndLeavesNothingBehind`. I then restored the file with `git checkout`, rebuilt
(0 warnings, 0 errors), and re-ran the three classes: 3/3, 5/5, 39/39. The tree is clean at `179aae80`.
R2's new assertions on the refused and cancelled arms have no teeth run; they are only observed green.

## Results

- Build: `dotnet build dew_flow_connect_other_ais.slnx -c Debug -m:4 --disable-build-servers` — succeeded, 0 warnings, 0 errors.
- Changed classes (runner `src_mcp/tests/bin/Debug/net10.0/CoaiMcp.Tests`, `--filter-class`):
  - `AKimiQuestionRowTests`: 3 of 3 passed
  - `AKimiConsultantCheckTests`: 5 of 5 passed
  - `CapabilityMatrixTests`: 39 of 39 passed
- Whole MCP suite (`CoaiMcp.Tests`, once, after all changes): total 10555, passed 10469, skipped 84, failed 2 (exit 2; log `/home/jinx/git/coai-exp/notes/D1-fix-suite.txt`).
  The two failures are the two known host failures: `KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed` and `KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn`. Same counts as the coder's earlier run.
  The log also has one line from the product logger: `the log could not be flushed: the log's disk is full`. `df` now shows 880G free on `/`; I did not find the cause and did not check whether it touched any test.
- Format: `dotnet format --verify-no-changes --no-restore --include` over the three changed C# files: exit 0.
- Generator: `node src_vs_code/scripts/generate-runtime-capabilities.mjs --check`: exit 0, up to date.
- TS (`npm --prefix src_vs_code test`): 6290 tests, 6270 pass, 1 fail — `the baseline names no release that was never tagged` (the known tag failure on this clone). Exit 1 because of that test only.

## Still open

- R4 teeth check (above).
- R5 renames.
- R2 scratch-gone check on the cancel and refused arms.
- The two known host failures (`KimiRuntimeTests.ALaunchThatNamesItsHome…`, `KimiHomeTests.AHomeWhoseDataPathPasses260…`) — status in the suite log.
