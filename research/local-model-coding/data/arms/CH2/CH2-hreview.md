# CH2 — HAIKU REVIEW + FIX of E5.5 and E5.6

Branch `exp/kimi-e56-classic-haiku-review-haiku-fix-2`, reviewing `b4e2ea96..HEAD`. Written 2026-10-28, before any code change.
Deadline 13:23 UTC.

## Step 1 — findings (written before any change)

| id | severity | file:line | finding | outcome |
|----|----------|-----------|---------|---------|
| H1 | major (process) | todo/PLAN_vibe_and_kimi_runtimes_E5.md:1, :6, :1089 | The plan's §0.2 and the E5.5/E5.6 story headers say the coder is **Opus**. The status line and the "as built" section say **Sonnet**. | Not fixed: a review pass cannot change who built the story. Reported to the user. |
| H2 | minor | src_mcp/tests/AKimiConsultantCheckTests.cs:83-102 | Table S5.5.1 says "the scratch repository and its canary unchanged". The test asserts only the check's reported canary state (`not-attempted`), not the scratch repository. | Not fixed: the check copies, never writes to the scratch, and a before-snapshot needs a seam into the check's scratch. Gap recorded. |
| H3 | minor | src_mcp/tests/AKimiQuestionRowTests.cs:148-149 | The refused arm of S5.6.7 does not assert the row's scratch is gone (coder deviation 55). | Not fixed: no seam reaches the row's temp folder through the service. Documented in the plan. |
| H4 | minor | src_mcp/tests/AKimiQuestionRowTests.cs:129 | `OneShotFolder` is `coai-vh`, not `coai-vh\<8>`. | Wrong on a second look: a leftover `coai-vh\<8>` is an entry in `coai-vh`, so `BeEmpty` still fails for it. No change. |
| H5 | minor | src_mcp/tests/AKimiConsultantCheckTests.cs (S5.5.1–S5.5.4 reds) | The table's expected reds were not seen; teeth were shown by mutation (plan deviation 50). | Accepted. |

No blocking or major code finding was found that could be fixed by a code change. H1 is a process finding and H2 is a gap I did not close in time. No production or test code was changed, so there is nothing to commit in the repository.

## Checked and found right

- Citations: RESULTS_kimi_confinement.md §6 (M-K3, 0 of 9), §7 (M-K4), §9 (M-K6), §12 (M-K9), §21 (M-K9 (e)), §14 (web unmeasured). All exist.
- Planner arm: `disk` is `Snapshot` + `SnapshotSources`; `none` is `Scratch`; no flags.
- `MustCarryNoFlag` refuses `Leading` or `Flags`.
- `runtime-capabilities.json` and vectors: three kimi rows, 2.1.1, cells as cited.
- Generator `--check`: up to date (21 rows, 7 runtimes × 3 capabilities).
- The person's hash covers file attributes, so the read-only bits are checked.

## Step 2 — results (final)

- **Build** (`dotnet build dew_flow_connect_other_ais.slnx -c Debug -m:4 --disable-build-servers`): succeeded, 0 warnings, 0 errors.
- **Changed test classes** (`AKimiConsultantCheckTests`, `AKimiQuestionRowTests`, `CapabilityMatrixTests`, `ConfinementPlannerTests`): 92 total, 92 passed.
- **Whole MCP suite** (one run): 10560 total, 10457 passed, 84 skipped, **19 failed**.
  - 2 known, not ours: `KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn`, `KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed`.
  - 17 rounds-database tests: `ThePersonsCommandsReachTheCallerTests` ×1, `ARoundIsMintedAKeyTests` ×2, `ARoundsNumberComesFromTheJournalTests` ×3, `EndToEndTests` ×3, `ACodeRoundRecordsItsSizeTests` ×7, `SplitOrderTests` ×1. Failure text is "found empty collection" from a rounds-database query, consistent with the coder's note. Not re-run at `b4e2ea96` by me.
  - The coder's notes listed 18 rounds-database failures (20 total). This run shows 17 (19 total). The difference is `TheRoundsNumstatsAreCountedTests`, which did not fail here.
- **Format** (`dotnet format ... --verify-no-changes --no-restore --include` on the 8 changed `.cs` files): clean, exit 0.
- **Generator `--check`**: up to date.
- **TS tests** (`npm --prefix src_vs_code test`): 6290 tests, 6270 pass, 19 skipped, 1 fail = `the baseline names no release that was never tagged` (known).
