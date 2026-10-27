# D3 — fix notes: review of E5.5 and E5.6

Branch `exp/kimi-e56-haiku-direct-3`. Review: `/home/jinx/git/coai-exp/notes/D3-review.json` (verdict fix-then-ready; 0 blocking, 0 major, 7 minor).

Commits added on top of `7dcafeaa`:
- `e9694d48` test(kimi): the E5.5–E5.6 checks assert what the table says (R1–R5)
- `42548c17` docs(kimi): the seed's why cites the M-K cells of the Kimi record (R6)

## Per finding

### R1 — S5.5.3 does not check the record or the junction-first disposal (minor)
- **Fixed.** Set `GitScratch.AfterUnlink` recorder; assert state `failed`, `failureKind` `cancelled`, and `_unlinked` empty.
- **Teeth:** with `KimiHome.DisposeAllUnder` removed from the check's disposal, the test went red: "...even when the check was cancelled, but found ... /home/credentials". Reverted.

### R2 — S5.6.7 does not check the TEMP scratch; refusal arm does not reach a junction (minor)
- **Partly fixed.** Added `Directory.Exists(launch.WorkingDirectory).Should().BeFalse(...)` for the copies on the answered and cancelled arms.
- **Not fixed:** the refusal arm that makes the home and then refuses the launch (sign-in gate). The refusal stops at the copy, so that arm does not show junction-first disposal. Not written: needs a new arm, and time ran short.

### R3 — S5.6.5 does not check `KimiEnvironment.Refusal` is empty (minor)
- **Fixed.** Added `KimiEnvironment.Refusal(request, _person.PlacesAt(home))` empty, as S5.4.2 does. Removed the one-element `ContainInConsecutiveOrder("--home")`.

### R4 — S5.5.1 does not check the canary or the scratch repository (minor)
- **Partly fixed.** Added `canary` not `read` and `confinement` empty (decision 3).
- **Not fixed:** the scratch repository's own bytes are not checked. Not written: the table's wording should be read first, and a deviation may be the right answer.

### R5 — test name differs from the table's `AdmissionFlagsBothRows` (minor)
- **Fixed.** Renamed to `AdmissionFlagsBothRows`. No other references to the old name (grep over `src_mcp`, `src_vs_code`, `todo`).

### R6 — seed `why` does not say the kimi rows cite the M-K cells (minor)
- **Fixed.** One clause added to `shared/runtime-capabilities.json` `why`. Generator `--check`: up to date (21 rows over 7 runtimes × 3 capabilities). The TS needs no change.

### R7 — no teeth run for E5.5; S5.6.8 red not watched (minor)
- **Partly fixed.** Teeth run for E5.5: both disposal sites broken (`VibeHomes.EmptiedUnderItsLock` and `ConsultCheckScratch.KimiHomesDisposed`). Red as expected:
  - `AKimiConsultantCheckTests`: `AfterTheCheck_TheHomeIsGone_JunctionFirst` (both theory rows), `ACheckCancelledMidTurn_StillDisposesItsHome`, `ARootlessCheck_KeepsTheHomeInItsScratch…`
  - `AKimiQuestionRowScenarioTests`: `AKimiDiskRow_ReadsItsCopies_AndLeavesNothingBehind` (theory rows)
  - `AOneShotKimiHomeIsDisposedJunctionFirstTests`: also red (existing tests, same mechanism)
  - 12 failures in total across the three classes. Reverted with `git checkout`; tree clean.
- **Written:** the teeth result is in deviation 48 of `todo/PLAN_vibe_and_kimi_runtimes_E5.md` (commit `0ecd72c4`). `plan-lifecycle`: clean.
- **Not done:** the S5.6.8 red against the old table (deviation 47 still says "not watched").

## Results (this run)

- Build (`dotnet build dew_flow_connect_other_ais.slnx -c Debug -m:4 --disable-build-servers`): 0 warnings, 0 errors.
- Changed classes (`AKimiConsultantCheckTests`, `AKimiQuestionRow*`, `CapabilityMatrixTests`): 56 of 56 passed.
- Full MCP suite: see the final report.
- Format (`dotnet format … --verify-no-changes --no-restore --include src_mcp`): exit 0.
- Generator (`generate-runtime-capabilities.mjs --check`): up to date.
- TS (`npm test` in `src_vs_code`): one fail, `the baseline names no release that was never tagged` (known, no git tags on this clone). `kimi rows admit flagged, web refuses naming its record` passes.

## Not run, on purpose

- No `kimi` or `vibe` run. No push, no PR, no merge of `main`, no `research/` edit, no live check.
- ConnectOtherAIs review gate not called (paid reviewers; out of scope for this brief).

## Full MCP suite (one run, after the fixes)

- Total 10555, failed 3, succeeded 10468, skipped 84, duration 7m 13s.
- Failed: `KimiRuntimeTests.ALaunchThatNamesItsHome_…` and `KimiHomeTests.AHomeWhoseDataPathPasses260_…` (both known Linux reds, not mine).
- Failed: `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow` (the same timing flake as the coder's run 3). Run alone: 10 of 10 pass. Not caused by this work (no numstats code touched). Not proven as a baseline flake.
- The log printed "the log's disk is full" during the run. The disk is not full (`/` has 860 GB free, `/tmp` 21 GB free). Likely a test that simulates a full disk. Not investigated.
