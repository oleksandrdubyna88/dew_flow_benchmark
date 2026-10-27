# O4 t4 — code notes (S5.5.1–S5.5.4)

## Done
- New file `src_mcp/tests/AKimiConsultantCheckTests.cs` (5 test cases: S5.5.1, S5.5.2 with two arms, S5.5.3, S5.5.4), committed as `ccad8841`.
- `ConsultantCheck.cs` and `ConsultCheckScratch.cs` are byte-identical to `b4e2ea96` (no production change).
- Build: succeeded, 0 errors, 0 warnings.

## Test results
- Red first: none. The table says these are PINS (the reds were before E5.3/E5.4); all 5 were green on the first run.
- `AKimiConsultantCheckTests`: 5/5 passed, three runs in a row (run 1 at the first build, runs 2 and 3 after restoring the production files).
- `ConsultantCheckTests` + `AOneShotKimiHomeIsDisposedJunctionFirstTests`: 42 total, 0 failed, 40 succeeded (2 skipped, Windows-only).
- Full suite (run 1): total 10539, failed 3, succeeded 10452, skipped 84. The names of the three were NOT captured in run 1 (my grep only showed one line, a `Failed` assertion about `AllChangesAsync` numstat). Run 2 (full suite again): failed 2, the two known failures named in the task (`KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed`, `KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn`). So the third failure in run 1 did not reproduce. It is not yet identified. Needs a check: rerun the suite and capture names with `failed ` lines.
- `dotnet format --verify-no-changes`: NOT verified for my files. The run printed IL errors from `src_server/src/*.cs` (CoaiServer.csproj, AOT/trim analyzers, not my files). I ran out of time before I could read the format verdict for `src_mcp`.

## Teeth mutations (each restored; `git diff --exit-code` confirmed identical)
1. `PlaceAsync`: `turn.Runtime.NeedsASanitisedSnapshot` replaced by `false` (never copy). Result: all 5 red. S5.5.1 message: `state` expected "answered", found "failed" (matches the document's intent).
2. `TurnAsync` finally: `held.Home.Dispose();` replaced by a no-op. Result: 3 red (answered, failed, cancelled arms). Message: `Expected Directory.Exists(...coai-vh/<8>...) to be False because coai-vh/<8> is gone, but found True.` (matches).
3. `ConsultCheckScratch.Delete`: `&& KimiHomesDisposed(root, warn)` removed. Result: 1 red, S5.5.4 only. Message: `Expected _unlinkedByTheDeleter to be empty ... the deleter's unlink recorder must not be invoked ...` (matches the document's intent, with the path to `.../own/kh-…/home/credentials`).

## Differences from the document
- Document expected red messages were "before E5.4 / before E5.3 g"; I did not run the red-before-code step, because the code is already in. The tests pass on `b4e2ea96`, as the document predicted.
- The task file names branch `exp/kimi-e56-opus-split-haiku-4`; the worktree is on `tmp/O4-t4`. I committed on `tmp/O4-t4`. Not pushed.
- The format check was blocked by unrelated `src_server` analyzer errors. Not fixed (out of bounds).

## Left undone
- Identify the third failing test from the run-1 full suite.
- `dotnet format --verify-no-changes` verdict for `src_mcp`.
- Commit has no notes file change (notes live outside the repo).

## Environment note
- A few shell commands were refused by the harness because of quoted or variable text in the command. I split them into simpler calls.
