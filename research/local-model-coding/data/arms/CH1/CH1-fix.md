# CH1 — fix notes: review of E5.5 and E5.6 (Haiku coder)

Branch `exp/kimi-e56-classic-haiku-review-haiku-fix-1`. Inputs: `CH1-review.json` (R1–R3) and `CH1-hreview.md` (H1–H4).
Commits of this round: `58e06eee` (R1), `8a8271a7` (R2), `71a69645` (R3). `0767f419` (H2) was made before this round.

## Per finding

| ID | Severity | Status | Note |
|---|---|---|---|
| R1 | minor | **fixed** (`58e06eee`) | The unconfined caveat said "a shell is always there", which is false for Kimi (M-K4: the shim refuses every shell request). Changed to "unconfined: can read this machine — no setting confines it" in `CapabilityMatrix.cs` and `capabilityAdmission.ts`. Red first: `TheUnconfinedCaveat_ClaimsNoShell_BecauseAKimiRowHasNone` (C#) and a Kimi `doesNotMatch(/shell/)` assertion (TS) both failed on the old text. A Kimi assertion is included. |
| R2 | minor | **fixed** (`8a8271a7`) | Deleted the uncalled helper `TheRowsOneShotFolder` in `AKimiQuestionRowTests.cs`. Checked: no other reference. |
| R3 | minor | **not fixed — owner decision needed** (`71a69645`) | The model rule is an owner question, not a coder fix. I recorded it as deviation 60 in `todo/PLAN_vibe_and_kimi_runtimes_E5.md`, marked "pending the owner's decision". I did not redo the fixes with another model, and I did not accept the deviation. |
| H1 | major | **not fixed — out of scope** | `research/module_tests.md` does not name `AKimiConsultantCheckTests` and `AKimiQuestionRowTests`. The brief puts `research/` out of scope, and the plan defers it. This is the one open major finding. Owner decision needed. |
| H2 | minor | already fixed earlier (`0767f419`) | Not re-done. Checked in this round: the class passes. |
| H3 | minor | not fixed | Deviation 51 records the design. The check's scratch invariant is covered by `state == answered`. Not changed. |
| H4 | minor | not fixed | Cosmetic. The assertion is correct. Not changed. |

No finding was judged wrong. No blocking findings exist.

## Results (this host, 2026-10-28 UTC)

- **Build** (`dotnet build dew_flow_connect_other_ais.slnx -c Debug -m:4 --disable-build-servers`): 0 warnings, 0 errors.
- **Changed classes**: `CapabilityMatrixTests` 40/40 pass; `AKimiQuestionRowTests` 3/3 pass. TS `capabilityAdmission.test.js` 31/31 pass.
- **Whole MCP suite** (after all three commits): total 10556, failed 20, succeeded 10452, skipped 84.
  - 2 known: `KimiRuntimeTests.ALaunchThatNamesItsHome…`, `KimiHomeTests.AHomeWhoseDataPathPasses260…`.
  - 18 round and git tests (`ACodeRoundRecordsItsSizeTests` 7, `ARoundIsMintedAKeyTests` 2, `ARoundsNumberComesFromTheJournalTests` 3, `EndToEndTests` 3, `SplitOrderTests` 1, `TheRoundsNumstatsAreCountedTests` 1, `ThePersonsCommandsReachTheCallerTests` 1). These are the same 18 as before this round; the reviewer recorded them as host-caused and failing on base `b4e2ea96`. Not fixed here.
  - No new failure.
- **Format** (`dotnet format --verify-no-changes --no-restore`): no formatting diagnostic. 54 diagnostics, all IL2026/IL3050 from `src_server`, as at base. Exit is non-zero for those only.
- **Generator** (`src_vs_code/scripts/generate-runtime-capabilities.mjs --check`): up to date, 21 rows over 7 runtimes × 3 capabilities.
- **TS** (`npm test` in `src_vs_code`): 6290 tests, 6270 pass, 1 fail = `the baseline names no release that was never tagged` (no git tags on this clone).

## Open for the owner

1. H1 (major): name the two new flows in `research/module_tests.md`. Out of scope for this round.
2. R3: accept deviation 60, or have the E5.5–E5.6 fixes redone by the model that built the stories.
