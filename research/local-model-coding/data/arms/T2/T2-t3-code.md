# T2-t3 code notes (E5.5, consultant check for Kimi)

## Done
- Added src_mcp/tests/AKimiConsultantCheckTests.cs, the only file changed. Committed as 662e586c (not pushed).
- Build: succeeded, 0 warnings, 0 errors.
- AKimiConsultantCheckTests: 5 cases (S5.5.1, S5.5.2 x2 arms, S5.5.3, S5.5.4), all green.
- Full MCP suite: 10539 total, 10453 passed, 84 skipped, 2 failed. The 2 failures are the known ones:
  KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn and
  KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed.

## Mutations (temporary, all reverted; git status shows only the untracked src_vs_code/node_modules, as at start)
- A: ConsultantCheck.cs:232 `held.Home.Dispose();` commented out. Red: S5.5.2 both arms and S5.5.3, with
  "coai-vh/<8> goes when the check ends". Matches the document.
- B: ConsultCheckScratch.cs:116 KimiHomesDisposed(root, warn) replaced by true. Red: only S5.5.4, with
  "the deleter's unlink recorder must not be invoked". Matches the document.
- C: KimiConsultant.cs:37 NeedsASanitisedSnapshot => false. Red: 5 cases, not only S5.5.1 as the document says:
  S5.5.1, S5.5.2 both arms, S5.5.3, S5.5.4. S5.5.1 fails at `state` to be "answered", not at the --cwd check.
  I did not diagnose why the check does not answer without the copy. This differs from the document.

## Notes
- Line numbers in the document differ from the file: the disposal is at ConsultantCheck.cs:232 (not 221-227),
  the Delete condition at ConsultCheckScratch.cs:116.
- The document's Out of bounds section says "no commit unless the run asks"; its Rules section asks for a commit,
  so I committed the new file only.
- The /tmp leftover check (testsToDelete-kimi-check-*) was not run: the shell command was refused by the
  approval gate. The test Dispose removes both folders; this is not verified.

## Left
- Mutation C diagnosis (why S5.5.1's state is not "answered" without the copy) is open for the final review.
