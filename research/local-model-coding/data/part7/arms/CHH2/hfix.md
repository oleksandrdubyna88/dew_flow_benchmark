# Fixer notes — `feat/quota-cooldown`, reviews hA.json and hB.json

Per finding. Commits: `d0809c4d` (A1–A4); the second commit (B findings) is listed below.

## hA

- **A1 (major) — fixed, `d0809c4d`.** `AgyQuotaProbe.ReadAsync` now catches a non-cancellation exception from the launcher and answers `CannotCheck("could not start: …")`.
  Test first: `AGaugeThatCannotBeLaunched_StillParksTheStatedReset_AndTheRestOfTheRound` (launcher throws `Win32Exception`; one agy row spent, one codex row spent).
  Red before: `Expected standing … not to be <null>`. Green after.
- **A2 (major) — fixed, `d0809c4d`.** `AgyError.Parse` now finds the marker at the start of a line and reads the balanced JSON object after it, on the same line or over the following lines
  (strings and escapes respected). Both one-line shapes still parse. Test first: `TheSameErrorPrintedOverSeveralLines_IsReadToo` (the plan's six-line block).
  Red before: `Expected AgyError.Parse(pretty) to contain a single item, but the collection is empty.` I did not ask the owner (nobody answers); the multi-line form is in the plan's own issue sample, so the class remarks now say so.
  The plan's D2 text is not edited by me.
- **A3 (minor) — fixed, `d0809c4d`.** `ConsultFailures.Ended` uses `r.Spent ?? RateLimit.Hopeless(r.Reason)`. Test: `ASpentVerdict_IsQuota_WhateverTheWordsSay` (Spent true with the spend-limit sentence → `Quota`; Spent false with a spent word → `RateLimited`).
  I did not run it red separately: the old line ignored `Spent`, so the first assertion fails by construction.
- **A4 (major) — fixed, `d0809c4d`.** New `IReviewerRuntime.QuotaKeyOf(model, apiKey)` (default: the model-only key). `ClaudeRuntime` and `CodexRuntime` answer `<key>+key` for a row on a vendor API key (`QuotaCooldowns.KeyedBy`).
  The round decides `apiKey` from the launch's environment (`QuotaCooldowns.RunsOnApiKey`: a non-empty `*_API_KEY` variable); `PanelService.CoolingOf` decides it from the vault key of the row (the same test `SettingsFor` uses to give the CLI its key).
  `QuotaCooldowns.NameOf` has words for `claude+key` and `codex+key`. Tests: `AnApiKeyRowAndASignInRow_ParkTheirOwnQuota` (round side) and `AnApiKeyRowAndASignInRow_DoNotShareAQuota` (panel side).
  **Not done: agy.** `ANTIGRAVITY_API_KEY` also gives agy another account, but the finding names Claude and Codex, agy's key is also the key of the `/quota` group lookup, and the plan measured nothing for it. Left as it is; a later change can use the same overload.
- **A5 (minor) — not fixed, by decision.** D9 says the card's foot draws `cooldown-line` instead of `reset-line`. It does not mention the rounds block, whose reset hint is the history of what a past round said
  (`cardResetLine` of the rounds snapshot), a different surface. The plan names no change there, so I kept the code. If the owner wants one line there too, it is a small change in `roundsBlock.ts`.
- **A6 (minor) — not fixed, owner question.** The code follows D2 as written (an exhausted status without the sentence is `Spent == null`, words read as today, so `exhausted` stops the ladder).
  Plan §5 story 1 says it climbs. The two cannot both hold: D5 needs `null` to ask the gauge, and `false` would never park. The owner must pick; then align §5 or the code.

## hB

- **F1 (major) — fixed.** `AParkAndALift_InterleavedUnderTheLock_EndWhereTheLastWriteSays` now attaches the same write probe to store B too, counts the probed writes and asserts the count is 3
  (run 1: A's park and B's lift; run 2: B's park; A's lift of nothing writes nothing).
- **F2 (major) — fixed.** `aCoolingRowSaysWhenItIsBack.test.ts` asserts `older` and `half` exist before it checks that `cooldown` is absent.
- **F3 (major) — fixed.** New `ARoundThatEndsOnASpentQuotaParksItTests`: a real plan round through `PanelService` on the fake CLI; the codex row exits 1 with codex's usage-limit sentence and a reset five days ahead;
  the cooldown file must hold one `codex` entry with source `stated`. Teeth seen: with the `AfterRoundAsync` call in `RoundEngine.cs` commented out the test fails with
  `Expected File.Exists(file) to be True because the round's end settled the quota it found spent, but found False.` (the call was restored; `RoundEngine.cs` has no diff).
  The lift is **not** driven this way: a cooling row is not launched again, so one row cannot be parked and then lifted by the engine; the lift stays tested over constructed outcomes.
- **F4 (minor) — not fixed.** The reader half carries a different name than the plan's S34 on purpose (the plan's name sits on the round half in `TheRoundParksAndLiftsTheQuotaTests`). Renaming would give two tests one name.
  It is recorded in the plan's §12 by the coder already as a deviation (S34 split); no change.
- **F5 (minor) — fixed.** `ATornFile_ParksNobody_AndSaysSoOnce` asserts the whole log list holds exactly one line.
- **F6 (minor) — fixed.** `AWriteThatCannotHappen_…` writes a known file first and asserts the same text after the refused park.

## Results

Runs finished 23:23 UTC (deadline 00:00).

- **Build** (`dotnet build src_mcp/tests/CoaiMcp.Tests.csproj`): 0 warnings, 0 errors.
- **Changed test classes** (round, cooling row, AGY_ERROR, consult failures, lock test, new round test): all green (62 + 11 tests, run before the last edits to the lock test; the lock test and the new round test were re-run green after).
- **Whole MCP suite** (log: `notes/hfix-suite.log`): 13,882 tests, 13,764 passed, 117 skipped, **1 failed**:
  `RegularFilesTests.A_fifo_a_socket_and_a_device_are_refused_at_once_never_waited_on_nor_streamed` — `ArgumentOutOfRangeException … invalid length for use with domain sockets`.
  The path is `/home/jinx/git/coai-exp2/r/d5399b9cf0ba/tmp/testsToDelete-coai-run-…/…/shown.sock`: over the 108-character Unix socket limit because of this session's long `TMPDIR`.
  The same test failed in the coder's run for the same reason; it touches no file of this branch. I could not re-run it with a short `TMPDIR` (setting the variable needs an approval that nobody can give here),
  so it is **not proved green** on this host. It should pass where the temp path is short (the task brief said the base commit was green on this host, which suggests a shorter `TMPDIR` there).
- **Format**: `dotnet format dew_flow_connect_other_ais.slnx --verify-no-changes` — no output other than the known `IL2026`/`IL3050` lines.
- **Extension** (`npm --prefix src_vs_code test`): 0 failures in every part (main part 6,539 tests, 6,519 passed, 20 skipped). Run after the F2 change.
- `plan-lifecycle.mjs`: clean.

## Left open

- A5 and A6: decisions for the owner (above).
- Agy API-key quota key (A4, agy part).
- The one environmental failure above.
