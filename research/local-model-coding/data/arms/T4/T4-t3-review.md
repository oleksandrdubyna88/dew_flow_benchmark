# T4-t3 — review notes (E5.5 consultant check for Kimi)

Result: no change needed. No commit made by the reviewer. The coder's commit is db2baa45.

## Findings
1. Contracts — right as it was. `src_mcp/tests/AKimiConsultantCheckTests.cs` matches the document line by line: usings, `[Collection("fakecli-env")]`, fields with `testsToDelete-` names, `Dispose()`, `OnKimi`, `Settings`, `StandIn`, `Scopes`, `Check`, `Text`, `ValueOf`, `Reads`. The four test names are exact. S5.5.2 is a `[Theory]` with two `InlineData` rows. Each *Then* is as written. No `Assert.SkipUnless`. Doc comments are on the class and each test.
2. Scope — right. Only the new test file changed. `PlayedKimi.cs` is untouched. No production file changed.
3. Red reason — right. The tests are green on arrival (E5.3 g and E5.4 are in). Teeth are by temporary break, as the document allows.
4. Break 1 — the coder's red message differs from the document. With `NeedsASanitisedSnapshot` forced false, production refuses before any launch ("a Kimi consultant stands in the conversation's sanitised snapshot (Workdir), and none was made"). The check ends `failed`, so the `--cwd` assertion is never reached; S5.5.1 is red on the `state` assertion. This is still a real red. The document's expected text is not reachable. It is not a test fault.
5. Break 4 — I ran it myself (iterate only `root` in `ConsultCheckScratch.KimiHomesDisposed`). S5.5.4 went red: "Expected _unlinkedByTheDeleter to be empty because ConsultCheckScratch.Delete disposed <root>/own junction-first (E5.3 g, R6), but found at least one item {".../coai-check-<id>/own/kh-dd693ddaf6/home/credentials"}." The other four cases stayed green. I reverted it; `git diff -- src_mcp/src src_mcp/runners src_mcp/core` is empty. Breaks 2 and 3 I did not re-run; the coder's messages match the document.
6. Coder's open items:
   - `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow` fails also when run alone. It does not use this file and no production file changed, so it is not from this task. I did not run it at `b4e2ea96`.
   - `AVibeReviewerRunsThroughAFakeVibeTests` passes when run alone (10 of 10 not checked; the class ran 5 tests, 0 failed). The coder's failure was likely an order or load effect, not from this task.
   - `dotnet format --verify-no-changes` on the whole solution fails with IL2026/IL3050 in other projects. For `AKimiConsultantCheckTests.cs` it is clean (`--include` this file: no output).

## Tests run
- Build with `-m:4 --disable-build-servers`: succeeded, 0 warnings, 0 errors.
- `CoaiMcp.Tests.AKimiConsultantCheckTests`: 5 of 5 pass on Linux (twice: before and after my break).
- The whole suite was not run (the task says only the task's classes).
