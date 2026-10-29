# DR2 fix notes — E5.5 and E5.6 (coder: Haiku, branch exp/kimi-e56-haiku-direct-review-2)

Review: /home/jinx/git/coai-exp/notes/DR2-review.json (6 findings: R1–R3 major, R4–R6 minor).
Commits: 1280f9ff (tests), b8137b92 (seed why), 34c066c2 (plan).

## Per finding

### R1 (major) — teeth check for the junction-first assertions: FIXED

- Temporary break (not committed): removed `KimiHome.DisposeAllUnder` from `VibeHomes.EmptiedUnderItsLock` and from `ConsultCheckScratch.KimiHomesDisposed`. Built. Ran the AKimi classes.
- Result: 6 tests went red on `_unlinked` (junction path found in the list):
  - `AfterTheCheck_TheHomeIsGone_JunctionFirst` (S5.5.2)
  - `ACheckCancelledMidTurn_StillDisposesItsHome` (S5.5.3)
  - `ARootlessCheck_KeepsTheHomeInItsScratch_AndDisposesItJunctionFirst` (S5.5.4)
  - `AFailedTurn_TheHomeIsGoneToo` (extra arm)
  - `AKimiDiskRow_ReadsItsCopies_AndLeavesNothingBehind_WhenItAnswers` (S5.6.7)
  - `AKimiDiskRow_CancelledMidTurn_LeavesNothingBehind` (S5.6.7)
- Red message (same in each): `Expected _unlinked to be empty because … but found at least one item {…\kh-…\home\credentials}`.
- Restored both files with `git checkout --`. Rebuilt. AKimi classes green again.
- Not seen red: the refused arm of S5.6.7. It launches nothing, so the break does not reach it. It asserts `_unlinked` empty, but nothing could fail there from this break.

### R2 (major) — S5.6.7 prompt and copy asserts: FIXED

- Answered arm: added `launch.Request.StdIn.Should().Contain(cwd, …)` and `Directory.Exists(ScratchOf(cwd)).Should().BeFalse(…)`.
- Cancelled arm: added the same scratch-gone assert, taken from the one shim launch.
- Helper `ScratchOf` walks up from the `--cwd` copy to the `coai-question-*` folder.
- Refused arm: NOT changed. No copy is made or launched there, so there is no copy path to name and no copy to check. The refused arm still checks the home, the recorder and the person's hash.
- Teeth: the scratch asserts were not part of the R1 break. Not separately proven red.

### R3 (major) — failed-turn arm of S5.5.2: FIXED

- `AFailedTurn_TheHomeIsGoneToo` now uses `PlayedKimi.Lost(PlayedKimi.SessionOf(request))` (exit 70, `session_lost`).
- Added `_unlinked.Should().BeEmpty(…)` and `_person.Hash().Should().Equal(before, …)`.
- Red in the R1 break: yes (see R1).

### R4 (minor) — stale deviations and class remark: FIXED

- Plan block "E5.5 and E5.6 as built": S5.5.2–4 now record the teeth reds. S5.6.7 now says three arms and what each checks. The recorder is no longer described as not added.
- Plan status line (line 3) now points to the block "E5.5 and E5.6 as built" after E5.7's *Done when*. The block was NOT moved.
- Class remark in `AKimiQuestionRowThroughTheServiceTests.cs` now says the refused arm is written.

### R5 (minor) — seed `why` names the kimi cells: FIXED

- `shared/runtime-capabilities.json`: added one clause naming the M-K cells of `research/RESULTS_kimi_confinement.md` (kimi 2.1.1, Windows). Split into two array elements, same style as the rest.
- Generator run: wrote the TS table. `--check`: up to date (21 rows over 7 runtimes × 3). The generated TS did not change (the `why` text is not in it).
- The C# side embeds this seed. The rebuild after the whole suite run will pick it up. The whole suite ran BEFORE this JSON change; see the results section.

### R6 (minor) — S5.6.4 admission reason: FIXED

- `AKimiQuestionRowTests.cs`: reason changed to "Grep and Glob read outside the working folder by absolute path (M-K6)".

## Results

Commands run on this host (Linux). Test entry point is the `.dll`, because the `.exe` does not exist here. Run as `dotnet ./src_mcp/tests/bin/Debug/net10.0/CoaiMcp.Tests.dll`.

- Build (`-m:4 --disable-build-servers`): succeeded, 0 warnings, 0 errors (before and after the teeth break).
- AKimi classes (`--filter-class "*AKimi*"`): total 42, passed 30, failed 0, skipped 12 (Windows-only, D18).
- Whole MCP suite: see the last section (filled in when the run finished).
- Format check, generator check, TS tests: see below.

## Final results (after the seed change, rebuilt)

- Whole MCP suite (run before the rebuild, 13:31–13:40 UTC): total 10556, passed 10470, skipped 84, failed 2, duration 8m 29s. The 2 failures are the known ones: `KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn` and `KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed`.
- Rebuild after the seed change: succeeded, 0 warnings, 0 errors.
- Capability classes (`*CapabilityMatrix*`) after the rebuild: 44 passed, 0 failed.
- AKimi classes after the rebuild: 30 passed, 0 failed, 12 skipped (Windows-only).
- Format (`dotnet format src_mcp/tests/CoaiMcp.Tests.csproj --verify-no-changes`): clean, exit 0. The solution-wide run exits 2 on IL2026/IL3050 analyzer errors in `src_bugs` and `src_server`, which are not format errors and are outside this change.
- Generator (`generate-runtime-capabilities.mjs --check`): up to date, 21 rows over 7 runtimes × 3.
- TS (`src_vs_code`): typecheck and lint clean. `run-tests.mjs`: the only failure is `the baseline names no release that was never tagged` (known, no git tags here).

## Not done

- Live checks, `kimi`/`vibe` runs, push, PR, merge of `main`: out of scope, not run.
- R2 refused arm: no copy assert, because no copy is made there (see R2 above).
- The `KimiRuntimeTests` and `KimiHomeTests` failures were not investigated: they are on the known list.

- Live checks, `kimi`/`vibe` runs, push, PR, merge of `main`: out of scope, not run.
