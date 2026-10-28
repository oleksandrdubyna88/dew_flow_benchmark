# CR1 — review of E5.5 and E5.6 (Haiku reviewer), 2026-10-28

Branch `exp/kimi-e56-classic-haiku-review-1`, reviewed `b4e2ea96..HEAD`. Findings written before any code change.

## Findings

**H1 — major.** `src_mcp/tests/AKimiQuestionRowTests.cs:171`. The disk row's "never the root" check (S5.6.7) only asserts `NotContain(granted)`, where `granted` is `Repo\src`. A run whose working folder were the checkout root `Repo` would still pass, because the prompt would name `Repo` and not `Repo\src`. The test therefore cannot see the D6 breach it claims to guard.
  *Fix:* assert the working folder is not the repository nor under it, and that the copy holds the granted file (`src\Mailer.cs`). Prove it with a temporary production break that makes the row run in the checkout, watched red, then reverted.

**H2 — major.** `src_mcp/tests/AKimiConsultantCheckTests.cs:37, 131, 154, 180` and `AKimiQuestionRowTests.cs:53, 117`. `GitScratch.AfterUnlink` is a process-wide static. `JunctionsTests.cs:384`, `KimiHomeTests.cs:350` and `KimiCanaryTests.cs:132` also write it, and none is in the `fakecli-env` collection, so xUnit may run them in parallel with these classes. A parallel writer can replace the recorder, which makes the "recorder never invoked" assertions pass vacuously. Each `Dispose` also sets the hook to null, which can clear another test's hook. This may be the intermittent `AVibeReviewerRunsThroughAFakeVibeTests` failure the coder reported, but that is not proven.
  *Fix:* not fixed here. The pattern predates this work and spans four classes outside the stories. Fixing it properly needs a repository-wide decision (one collection, or a per-test seam), which is outside the stories' scope.

**H3 — minor.** `AKimiConsultantCheckTests.cs:127-144`. The table's S5.5.2 says "the turn fails exit 70". The test asserts only `state == failed` and never checks an exit code.
  *Second look: wrong.* Exit 70 is the shim's exit (`session_lost`, the `PlayedKimi.Lost` case). The check exits 0 for every classified outcome (`ConsultantCheckMode.cs:19-20`). The test is correct; the table's wording is misleading. Not fixed.

**H4 — minor.** `AKimiConsultantCheckTests.cs:102-120`. The S5.5.1 "scratch repository and canary unchanged" is not asserted directly.
  *Second look: covered.* The check ends a changed repository or canary as `tree-changed` and `failed` (`ConsultantCheck.cs:263`, `:299`). The test asserts `state == answered` at `:110`, which carries that invariant. Not fixed.

**H5 — minor.** `AKimiQuestionRowTests.cs:172`. The scratch-removal assertion checks `Directory.Exists(GetDirectoryName(WorkingDirectory))`, which names the parent of the copy, not the copy itself.
  *Fix:* assert the copy's own folder (`WorkingDirectory`) no longer exists. This is stronger, and it is folded into the H1 change.

## Checked and found right

- The 12 scenarios exist, with the names the plan gives, and assert the table's Then. Teeth were checked in CR1-code.md. Exception: the S5.5.2 exit code (H3).
- Planner arm (`ConfinementPlanner.cs:205-213`), `none`/`disk` standings, `web` refused, the `kimi` rows and vectors, and the `RESULTS_kimi_confinement.md` section cites (§6, §7, §9, §12, §14, §21) all match the file and the plan.
- `MustCarryNoFlag` (`KimiConsultant.cs:60-68`) is correct, and its pattern precedence is right (`is {…} or {…} or {…}`). It also refuses `Leading` and `AddDirs`, a widening documented as deviation 54.
- Generated TS and the shared table are consistent: 21 rows over 7 runtimes; the TS admission test is right.
- No secret, person's folder or root is touched by the new tests beyond their own `testsToDelete-` folders.

## Outcome per finding

- **H1 — fixed** (commit `96d935ed`). The row's prompt and working folder are now checked against the checkout, and the copy's own folder against existence. Teeth: a temporary break that made the prompt name the checkout root (`QuestionRowLaunch.cs`, `PromptNaming([.. place.Roots, input.Repo])`) turned the new assertion red in the answered and cancelled endings (*because never the checkout either…*); reverted, row tests 3/3 green. A first break (prompt naming only the checkout) was too broad: the old assertion failed first, so it was not used as the proof.
- **H5 — fixed** in the same commit (the copy's own folder is asserted gone).
- **H2 — not fixed.** The shared static `GitScratch.AfterUnlink` is written by four classes outside `fakecli-env` (`JunctionsTests`, `KimiHomeTests`, `KimiCanaryTests`, `AOneShotKimiHomeIsDisposedJunctionFirstTests`). A parallel writer can replace the recorder and make "recorder never invoked" pass vacuously. Fixing it needs a repository-wide decision on collections, which is outside these stories. It may explain the intermittent Vibe failure; that is unproven and needs a look by the second reviewer.
- **H3 — wrong on a second look.** Exit 70 is the shim's exit; the check exits 0 for every classified outcome (`ConsultantCheckMode.cs:19-20`). The test is right; the plan table's wording is misleading. Not changed.
- **H4 — wrong on a second look.** The check itself ends a changed repository or canary as `tree-changed` and `failed` (`ConsultantCheck.cs:263`, `:299`), and the test asserts `state == answered` (`AKimiConsultantCheckTests.cs:110`). The invariant is covered.

## Results

- Build: 0 errors (after the fix commit `96d935ed`).
- MCP suite, whole, once: 10555 total, 10452 passed, 84 skipped, **19 failed**. Output: `/home/jinx/git/coai-exp/notes/CR1/suite-run1.txt`.
  - 2 are the known host failures (`KimiRuntimeTests.ALaunchThatNamesItsHome…`, `KimiHomeTests.AHomeWhoseDataPathPasses260…`).
  - 17 are not Kimi: `ACodeRoundRecordsItsSizeTests` (7), `EndToEndTests` (3), `ARoundsNumberComesFromTheJournalTests` (3), `ARoundIsMintedAKeyTests` (2), `ThePersonsCommandsReachTheCallerTests` (1), `SplitOrderTests` (1).
  - **Checked against the base commit `b4e2ea96`** (built in a throwaway worktree, since removed): each of those six classes fails the same number of times there, so they predate this work. Not caused by the stories.
- Format (`dotnet format --verify-no-changes --no-restore --include` over the 7 changed C# files): clean.
- Generator (`src_vs_code/scripts/generate-runtime-capabilities.mjs --check`): clean, 21 rows over 7 runtimes × 3 capabilities.
- TS tests (`npm test` in `src_vs_code`, through `low-priority.mjs`): 6290 tests, 6270 pass, 19 skipped, **1 fail** — `the baseline names no release that was never tagged` (no tags in this clone; known). The new `kimi rows admit flagged, web refuses naming its record` passes. Output: `/home/jinx/git/coai-exp/notes/CR1/ts-run1.txt` was not written (the run went to the task output instead).
- Working tree: clean after `96d935ed`. Not pushed. No PR.
