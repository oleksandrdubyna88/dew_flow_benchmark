# O4 fix notes — review of E5.5 and E5.6

Branch: exp/kimi-e56-opus-split-haiku-4. Commits added by this fix: 93b45234 (R1), f35eb483 (R2, R3). Nothing pushed.

## Findings

### R1 (major) — FIXED
- Defect: `AKimiQuestionRowTests` set `GitScratch.AfterUnlink` inside the async `InitializeAsync`, after an `await`. `GitScratch.AfterUnlink` is AsyncLocal, so the value did not reach the test body, and the check "the recorder is never invoked" could never fail.
- Fix: the recorder is now installed in the constructor (same pattern as `AKimiConsultantCheckTests.cs:26`). `InitializeAsync` only builds the repository.
- Confirmed by reading the code and by a teeth run (below).
- Teeth run: temporarily made `KimiHome.Unlinked` skip the junction-first unlink (`Unlinking(root, link)` replaced by `new Disposal.Clean(false)`), built, ran `--filter-class "*AKimiQuestionRowTests*"`:
  - `answered`: FAILED, recorder not empty (the junction `home/credentials` was unlinked by the deleter).
  - `cancelled`: FAILED, same check.
  - `refused`: passed. The launch control refuses before any home or junction exists, so there is nothing to unlink. The review said all three arms go red; that is not true for `refused`. Deviation 32's text should say two arms, not three. The plan was not edited by this fix.
  - Production code restored (`git diff` on `src_mcp/runners` empty), rebuilt, 3 of 3 green.

### R2 (minor) — FIXED
- Caveat wording changed in `src_mcp/core/QuestionConsult/CapabilityMatrix.cs:33` and `src_vs_code/src/capabilityAdmission.ts:60` to `unconfined: can read this machine, and no flag removes it`.
- Legend in `shared/runtime-capabilities.json` (`unconfined` definition) changed from "(a shell is always there)" to "(it can read this machine, and no flag removes that)".
- Generated file regenerated; `--check` says up to date.
- No failing-test-first: this is a wording change, and no test pins the full sentence (tests match only "can read this machine"). Those tests still pass.
- Left alone on purpose: the `note` for codex's none row ("a shell is always there") is about codex, which does have a shell, so it stays true.
- Left alone: compiled output under `src_vs_code/out/` and `dist/` (build output, not source).

### R3 (minor) — FIXED
- `shared/runtime-capabilities.json`, kimi `none` row `resultRef`: now says only what M-K6 measured — "Read was bounded to the working folder, and Grep and Glob read a file outside it by absolute path 3 of 3". The empty-folder inference stays in `note`, as the review asked.
- Regenerated `src_vs_code/src/runtimeCapabilities.generated.ts` (the one-line `resultRef` change).
- The kimi `disk` row `resultRef` also says "Read inside the working folder served 3 of 3 and bounded by the shim". That matches M-K6 as the review describes it, so I did not change it.

## Results

Commands run through `.agents/conventions/tools/low-priority.mjs`.

- Build (`dotnet build dew_flow_connect_other_ais.slnx -c Debug -m:4 --disable-build-servers`): 0 errors, after R1 and again after R2/R3.
- `AKimiQuestionRowTests` (3 cases): 3 of 3 green after R1; teeth run as above.
- Full MCP suite: see the result section below.
- Format (`dotnet format dew_flow_connect_other_ais.slnx --verify-no-changes --no-restore`, from CI): see below.
- Generator (`node src_vs_code/scripts/generate-runtime-capabilities.mjs --check`): up to date.
- TS (`npm test` in `src_vs_code`): 6290 tests in the main runner, 6270 pass, 1 fail — "the baseline names no release that was never tagged" (the known no-git-tags failure). Other runners: all pass, 0 fail.
- Known host failures not touched: `KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn`, `KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed`.

## Final results (after R1, R2, R3)

- Full MCP suite (`CoaiMcp.Tests`, 10556 total): 10469 passed, 84 skipped, 3 failed.
  - `KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn`: known host failure, not touched.
  - `KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed`: known host failure, not touched.
  - `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow`: the 1 ms git deadline flake the coder flagged in earlier runs. Rerun alone: 10 of 10 passed. Not related to these changes.
- Format (`dotnet format dew_flow_connect_other_ais.slnx --verify-no-changes --no-restore`): exit 2. All 54 diagnostics are IL2026/IL3050 AOT/trimming errors in `src_server/src/CoaiServer.csproj` (38) and `src_bugs/src/CoaiBugs.csproj` (16). None are in a file this fix touched, and the same 54 errors appear in the earlier C2 run, so this is a pre-existing host state. Format is NOT verified as clean on this host.
- Generator `--check`: up to date.
- TS (`npm test` in `src_vs_code`): main runner 6290 tests, 6270 pass, 1 fail (the no-git-tags baseline test). All other runners 0 fail.
- Working tree after the commits: clean.
