# Fixer notes (hfix)

Branch `feat/quota-cooldown`. Each fix started with a test that failed for the real symptom (seen red, then green).

| Id | Verdict | Commit | What |
|---|---|---|---|
| A1 | fixed | d398446b | `DeepseekRuntime.QuotaKeyOf` answers empty. Test `ASpentDeepseekRow_ParksNoCodexSignIn_AndItsAnswerLiftsNone` was red: `Expected store.Standing("codex", Now) not to be <null>` (the Deepseek `Ok` lifted the Codex park). |
| A2 | fixed | d398446b | `Judged`: a `SpentWithoutReset` bucket goes through `Unreadable(...)`, so an explicit refusal is still parked on the text, an undecided one is "could not check" and not parked. Test `AnUndecidedExhaustion_WhenTheSpentBucketNamesNoReset_IsNotParked` was red: `found QuotaCooldownEntry … Source = "guess"`. |
| A3 | fixed | d398446b | `ConsultFailures.Ended`: `(r.Spent ?? RateLimit.Hopeless(r.Reason))`. Test `ASpentOutcome_IsQuota_WhateverTheWordsSay_…` was red: `Expected … Quota, but found … RateLimited`. |
| A4 | fixed | 6777dbb0 | `SecurityRoster.Refusal` asks a `coolingReasonOf` func (`PanelService.CoolingReasonOf`, passed through `RosterBuilder`) before it says "credentials unavailable". Test `ACoolingRow_IsExcludedFromTheSecurityLane_WithThisSidesCooldownSentence` was red: `Expected work.Excluded "reviewer runtime or credentials unavailable" to contain "is spent — back at …"`. (The new method `Unavailable` keeps `Refusal` under the complexity cap, CA1502.) |
| A5 | fixed | 4ed32b38 | `AgyError.Parse` reads an `AGY_ERROR` block: from the marker line to the next marker, the shortest run of lines (at most 40) whose JSON parses. Test `TheIssuesPrettyPrintedLine_IsASpentVerdict_…` (the owner's verbatim sample) was red: `Expected verdict.Spent to be True, but found <null>`. |
| A6 | fixed | 82f00c41 | `QuotaCooldowns.Effective`: a Lift for a key with nothing standing is dropped before `Mutate`, so no lock and no refused-write notice. Test `AnAnsweredRound_WithNothingStanding_TakesNoLock_AndRefusesNothing` (data dir is a file) was red: the notice "A spent quota could not be remembered: the data directory … cannot be created". A park into the same dir still refuses (same test). |
| A7 | fixed by amending the plan (code was right) | 82f00c41 | `ParkedOrNot` passes `standing.Consecutive + 1`. The existing S39 test says "without a gap it doubles" (30, then 60) and S21 says a park of a standing guess carries `consecutive + 1`. The literal D5 text would give 30, 30, 60 — no doubling at the first repeat. D5 now says the count is the one THIS park carries (+1 while a guess stands). No code change. |
| A8 | fixed | eb79feef | The rounds row (`roundsBlock.ts` `rowLine`) says `resetSaid`: the cooldown line while one stands, else the ledger's reset — never both, as the card does. `RoundsBlockInput` gained `cooldownOf` and `nowMs`; `panelProvider` passes the reported cooldown and `Date.now()`. Test in `statedReset.test.ts` was red (reverted line): `actual … quota out — resets in 89 h`, expected `quota spent — back …`. |
| F1 | fixed | dd2d3cc3 | The note-cap test writes ten 4,000-character notes (about 41.8 KB without the cap; seen red with the cap removed: `found 41855L`) and reads each stored note back, at most `NoteCap`. |
| F2 | fixed | 6777dbb0 | `AnotherGroupOnTheSameCli_StillRuns` now asserts `StartWith("antigravity: its Gemini quota on Antigravity is spent")`. |

Also corrected in the plan (82f00c41): the coder's deviation 9 said a resetless spent bucket on an undecided refusal is parked ("the plan is silent"). D5/D10 are not silent: it is "cannot check", not parked. That is A2.

Not wrong, none skipped. The decisions the reviewers marked as holding (D2, D6, D7, D8, D11, D12) were not touched.

Also: e0c5c6ac — a whitespace error in my A3 edit (`?new`), found by the format check, fixed.

## Final results (host: Linux, WSL2)

- Build (`dotnet build … -c Debug`): 0 warnings, 0 errors (after e0c5c6ac).
- Changed test classes: all green (`TheRoundParksAndLiftsTheQuotaTests`, `ConsultFailureTests`, `ACoolingRowStaysOutOfTheRoundTests`, `SecurityRosterCharacterizationTests`, `SecurityLaneRoundTests`, `AnAgyQuotaIsReadFromItsStructuredErrorTests`, `ASpentQuotaIsRememberedUntilItsResetTests`).
- Whole MCP suite, first run (before e0c5c6ac): 13,885 total, 13,767 passed, 117 skipped (Windows-only), **1 failed**: `AFeatureIsReviewedEndToEndTests.AnAnswerToAnEarlierHold_DoesNotReopenALaterOne_AndTheNewAnswerDoes`. My `tail` cut the failure message, so I do not know it. Re-run alone: passed (25 s). The whole class `AFeatureIsReviewedEndToEndTests` alone: 32 of 32 passed. It touches none of the files I changed. I did not prove it is a flake; a second full run was started with the whole output kept in `notes/suite2.txt` (result below, if it finished before the deadline).
- Format (`dotnet format … --verify-no-changes --no-restore`): clean for `src_mcp` after e0c5c6ac; only the IL2026/IL3050 lines from `src_server`/`src_bugs` remain (as the coder's notes say; not touched).
- Extension (`npm test` in `src_vs_code`, after eb79feef): exit 0, all files pass.

## Second full-suite run (after e0c5c6ac, whole output in `notes/suite2.txt`)

13,885 total, 13,767 passed, 117 skipped, **1 failed**: `RegularFilesTests.A_fifo_a_socket_and_a_device_are_refused_at_once_never_waited_on_nor_streamed`.
Message: `ArgumentOutOfRangeException: The path '…/tmp/testsToDelete-coai-run-…/testsToDelete-regular-files-…/shown.sock' is of an invalid length for use with domain sockets`.
It fails alone too (`--filter-class "*RegularFilesTests"`: 30 of 32 passed... see below), so it is not a flake: it is the long temp path of THIS session
(`/home/jinx/git/coai-exp2/r/4650daee202d/tmp/...`) against the Unix socket path limit. It is not in code I changed.

Correction to the first-run note above: the first run's line `1 failed | active: AFeatureIsReviewedEndToEndTests…` names the test that was
ACTIVE when the line was printed, not the failed one. The first run's failed test was almost certainly this same socket test (same counts: 13,885 / 13,767 / 117 / 1).
`AFeatureIsReviewedEndToEndTests` passed in the second run and alone.

NOT done (deadline 23:56): a run with a short `TMPDIR` (for example `TMPDIR=/tmp`) to show the socket test passes there. The base commit's note
("13,807 pass") was probably taken with a shorter temp path. The next reviewer should run `RegularFilesTests` with a short `TMPDIR`.
Note: the single RegularFilesTests run alone printed `total: 32, failed: 1, succeeded: 30` (the 32nd is skipped).
