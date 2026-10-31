# Fixer notes — findings of hA.json and hB.json

All fixes are in commit 6a0097f5 on feat/quota-cooldown (nothing pushed). Each fix of a defect began with a test seen RED for the right reason.

## hA (does each decision hold)

- **A1 (major, D5) — fixed (6a0097f5).** `QuotaCooldowns.Decided` chose the first Spent-null row. Now it orders by `Rank`: `Spent == true` (0), hopeless words (1), other undecided (2). Test `ATransientRefusalBesideAHopelessOne_NeverHidesIt_InEitherOrder` (Theory, both orders): red `Expected parked not to be <null>` (transient first), green after the fix.
- **A2 (major, D10) — fixed (6a0097f5).** `AntigravityRuntime.ReadResetAsync`: `SpentWithoutReset` parks on the text only when `spent == true`; otherwise `StaysOpen` ("…an undecided refusal is not parked"). Test `AnUndecidedExhaustion_WhoseBucketIsSpentWithoutAResetTime_IsNotParked` (also pins the `Spent == true` case still parks): red `Expected undecided.Standing(...) to be <null>, but found …` (a `stated` entry), green after.
  The test uses QUOTA_REAL with `gemini-weekly` at 0 and its reset removed (`resetTime: ""`), and the plan's TransientReason (the issue's "Too many requests" form).
- **A3 (major, D1) — fixed (6a0097f5).** `ConsultFailures.Ended` now reads `r.Spent ?? RateLimit.Hopeless(r.Reason)`. Test `TheVendorsOwnWord_OutranksTheWords_OnTheConsultationRoadToo`: red `Expected …RateLimited, but found …Quota` (agy transient with "exhausted"). The second half (Claude spend limit, `Spent = true` → `Quota`) was already green by chance of the test sentence: "hit your monthly spend limit" is not in the phrase list but the `Spent` field now decides; it would be red with the old code too (no phrase matches) — it is in the same test, and the first half proved the test red before the fix.
- **A4 (minor, D2) — fixed (6a0097f5).** `AgyError.Parse` now reads the first JSON value after each line-leading `AGY_ERROR` marker with `Utf8JsonReader`/`JsonDocument.ParseValue`, so the object may span lines and trailing text is ignored. Tests `TheIssuesPrettyPrintedLine_IsASpentVerdictToo` (red `Expected True, but found <null>`) and `ALineCutShort_IsNoError`. I did not confirm from a real agy whether it ever prints the pretty form; the plan quotes it, so it is read.
- **A5 (minor, D3) — fixed (6a0097f5).** Chose "empty key, pinned": `TheRuntimesWithNoMeasuredQuota_KeepTheEmptyKey_AndTheOnesWithOneDoNot` asserts `GeminiRuntime`, `DeepseekRuntime` and `CustomCodexRuntime` return an empty key and `CodexRuntime` returns `codex`. Plan §12 item 13(e) records that `GeminiRuntime` is in D3's empty list (the gemini CLI is retired; no measured quota answer). This test is a pin, not a red-first test (the behaviour did not change).

## hB (do the tests test what they claim)

- **B1 (major) — fixed (6a0097f5).** New `ARoundThatEndsOnASpentQuotaParksItTests`: a real plan round through `PanelService` + real git, the codex launches played as a spent allowance (the issue #165 sentence). Asserts one launch, a `guess` entry for `codex` on disk, and `ExcludedFrom` naming the row. Break-it RUN: with the `AfterRoundAsync` line in `RoundEngine` commented out the test goes red. Restored.
- **B2 (major) — fixed (6a0097f5).** `ACoolingAgyRow_IsDealtNoWork_WhileItsSiblingOnAnotherGroupIs` calls `Roster.BuildWork` and asserts only `antigravity-gpt` is dealt. Break-it RUN: `CanRun` replaced by `_ => true` in the `RosterBuilder` construction: red. Restored.
- **B3 (minor) — fixed (6a0097f5).** S9 test gains a constructed case: `gemini-5h` at 0, disabled, earlier reset. Break-it RUN: with the `!b.Disabled` filter removed the S9 test and the "every bucket disabled" edge go red. Restored.

## Docs
`research/module_tests.md` (new class, extended row) and plan §12 item 13 updated.

## Results
See the bottom of this file.

### Correction to A3
The second half of `TheVendorsOwnWord_OutranksTheWords_OnTheConsultationRoadToo` (Claude spend limit, `Spent = true` → `Quota`) was NOT seen red on its own: the test stops at the first failing assertion (the agy half). By reading the code it would be red before the fix (no `Spent` phrase matches "monthly spend limit"), but I did not run it alone.

### Final results (commit 6a0097f5, solution built Debug with 0 warnings, 0 errors)
- Changed test classes (6): 82 tests, all pass.
- Whole MCP suite: 13,886 total, 13,768 passed, 117 skipped, 1 failed — `RegularFilesTests.A_fifo_a_socket_and_a_device_are_refused_at_once_never_waited_on_nor_streamed`. Re-run alone: same `ArgumentOutOfRangeException`, the socket path under this session's temp root (`.../r/5105f06575f7/tmp/testsToDelete-coai-run-…`) is over 108 characters. Environment, not this work; I could not re-run it with a short TMPDIR (writing to /tmp is not allowed in this session).
- `dotnet format whitespace` and `dotnet format style --verify-no-changes` on the 10 files I changed: clean (exit 0). The whole-solution format check cannot run here (IL2026/IL3050 analyzer errors in `src_server`, as the coder noted).
- `plan-lifecycle`: clean.
- Extension tests: not run — `src_vs_code` was not changed by this fix round (the last full run, by the coder: exit 0).
