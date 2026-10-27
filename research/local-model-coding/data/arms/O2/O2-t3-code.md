# O2 t3 — coder notes: the consultant check on Kimi (S5.5.1–S5.5.4)

## What I did

- Added `src_mcp/tests/AKimiConsultantCheckTests.cs`: one class, five cases (S5.5.1, S5.5.2 as two arms, S5.5.3, S5.5.4), as the brief's contracts give them.
- No production code changed. The class compiles against the existing code with the usings `CoaiMcp.Server` added (`ConsultantCheck.OwnName`, `ConsultantCheckMode`, `PanelSettings` live there; the brief's using list did not include it).
- The test file was the only change I committed.

## Test results

- Build (`dew_flow_connect_other_ais.slnx`, Debug): succeeded, 0 errors, 0 warnings.
- `AKimiConsultantCheckTests` at first run: 5 of 5 green (as the brief expected).
- `ConsultantCheckTests`: 32 total, 30 passed, 2 skipped (the Vibe cases, `D18: Vibe is refused on every host but Windows`, skipped on this Linux host).

## Mutation reds (step 2), each reverted with `git checkout --` and rebuilt

a) `ConsultantCheck.cs` line 232 (`held.Home.Dispose();`, commented out). Result: 3 failed, 2 passed.
   - S5.5.2 both arms and S5.5.3 red with `Expected Directory.Exists(Path.GetDirectoryName(home)) to be False ..., but found True.` Matches the brief.
   - The brief said line 225; the line is now 232. Matched by content.
b) `ConsultCheckScratch.cs` line 116 (`KimiHomesDisposed(root, warn)` replaced by `true`). Result: 1 failed, 4 passed.
   - S5.5.4 red with `Expected _unlinkedByTheDeleter to be empty ... the deleter's unlink recorder must not be invoked ...`. Matches the brief.
   - The brief said line 111; the line is now 116. Matched by content.
c) S5.5.1 red before E5.4 was not shown (it needs E5.4 reverted). Pinned by the green run: S5.5.1 green at first run — pins E5.4's registration.

After each revert: `git status --short` showed only the new test file (plus the untracked `src_vs_code/node_modules` that was there before I started).

## Whole MCP suite (step 3)

- Result: total 10539, failed 3, succeeded 10452, skipped 84.
- The two known failures (confirmed by name when I ran their classes alone):
  - `KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn`
  - `KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed`
- A THIRD failure that the brief did not name: `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow` (`TheRoundsNumstatsAreCountedTests.cs:262`). Assertion: `(await _assembler.AllChangesAsync(_repo, parent, head, Ct, oneMillisecond)).Failed.Should().BeTrue("and not an empty, successful numstat")`. A 1 ms deadline on a git call; git returned in time, so the call did not fail. This looks timing-dependent. I re-ran that class alone and it failed again (1 of 10). I did not change it: it is not my file, and it is not my task.
- The brief says to run the whole suite "once". I ran it once; the names of the failing tests were not captured by that run. I took them from the source and from the single-class re-runs. I did not run the whole suite a second time.

## Things that differed from the brief or need a decision

- The brief's Out-of-bounds list says "Do not commit", but the coder rules say to commit my file by name. I followed the coder rules and committed only `src_mcp/tests/AKimiConsultantCheckTests.cs`. Please confirm.
- The brief's S5.5.3 words (`failureKind` `cancelled`, state `failed`) held at first run. No change was needed to the assertions.
- Nothing left undone from the contracts.
