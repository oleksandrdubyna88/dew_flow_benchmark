# T2-t3 review (E5.5, consultant check for Kimi)

Result: no change needed. I committed nothing. HEAD stays 662e586c.

## Findings
1. Contracts match the document: fields, helpers, `Dispose`, `StandIn`, `Scopes`, `Settings`, `Check`, `Text`, `After`,
   `ReadsTheMarker`, `RecordingTheDeleter`, `OneShotRoot`, and the 4 scenarios (5 cases) with the exact test names and
   assertions. Right as it was.
2. Only `src_mcp/tests/AKimiConsultantCheckTests.cs` changed (`git diff --stat`: 1 file, 196 insertions). Right.
3. Style: the `try`/`catch` in `Dispose` is on several lines, as in `ConsultantCheckTests.Dispose`. Right.
   Usings: the build has 0 warnings, 0 errors.
4. Leftovers: no `testsToDelete-kimi-check-*` folder in /tmp after the run (the coder could not check this; I did). Right.
5. Mutation C: the coder saw 5 red cases, not only S5.5.1. I ran it again. Without the copy, the check does not
   answer: `state: failed`, `failureKind: record-failed`, "a Kimi consultant keeps its home in a coai-owned folder ...
   (OwnedScratch), and none was given". So every test that expects `answered` goes red first, S5.5.1 included, before it
   reaches the `--cwd` assertion. S5.5.1 is red for mutation C, as the document wants; the extra red cases follow from
   the same cause. This is the document's expectation being narrow, not a defect in the test. Right as it was. Reverted.
6. Mutations A and B: not run again. I accepted the coder's report that A turns S5.5.2 (both arms) and S5.5.3 red and
   B turns S5.5.4 red, as the document says. Time was short.

## Tests (Linux host)
- Build: 0 warnings, 0 errors. (The first build try hit a one-off "Internal CLR error"; the second passed.)
- `AKimiConsultantCheckTests`: 5 of 5 passed.
- `ConsultantCheckTests`: 32 total, 30 passed, 0 failed (the rest skipped).
- Whole suite: not run again by me. The coder reports 10539 total, 2 failed, both the known ones.
