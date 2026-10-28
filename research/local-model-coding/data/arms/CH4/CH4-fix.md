# CH4 — fix notes (Haiku), review of E5.5 and E5.6

Branch `exp/kimi-e56-classic-haiku-review-haiku-fix-4`. Commits after the review's base: `643efcff` (R2), `43524c8d` (R4), `ff7273c5` (R1). No push, no PR, no `kimi` or `vibe` run, no live check.

Rules: `rules.mjs check` resolved (advisory, pinned at e29883f8). `explain` selected 33 rules for the files I changed. Under the deadline I read the rules that bear on these findings (`testing`, `scenario-tests`, `test-leftovers`, `planning-docs`, `knowledge-base`, `csharp/doctrine`) and did NOT read the other selected rules in full (about 200 KB). That is the same gap the plan's deviation 39 names.

## Per finding

| ID | Severity | Verdict | Commit | Note |
|---|---|---|---|---|
| R1 | major | fixed | `ff7273c5` | `module_core.md`: the seed label now says 21 rows (7 runtimes × 3); a Kimi arm bullet beside Vibe's. `module_tests.md`: a new section for S5.5.1–S5.5.4 and S5.6.1–S5.6.8, with each red as it was seen, and the known gap. `plan-lifecycle.mjs`: clean. |
| R2 | minor | fixed | `643efcff` | Verified: `MustCarryNoFlags` let `CwdKind.Root` through. Red first: a plan `Cwd = Root`, grant `Disk(RootA)` → *Expected a System.ArgumentException to be thrown, but no exception was thrown*. (With no roots the first attempt failed with an index error, which is not the defect, so I granted a root.) Guard now refuses `Cwd: Root`. Deviation from the review's wording: I added a new `[Fact] APlanRootedInTheCheckout_IsRefusedForKimi`, not an InlineData row on `APlanWithFlags_IsRefusedForKimi`, because that test has no cwd parameter. |
| R3 | minor | not fixed | — | Verified: `TheShims` returns early for `RefusedBeforeTheLaunch`, so the row's scratch is not asserted. Not fixed because the scratch comes from `QuestionRowLaunch.MakeScratch`, which the panel service builds and the test cannot reach. Asserting it through a TEMP listing would race with parallel test classes. The gap is written into `module_tests.md` (review's second option). Fixing it properly needs a seam in production code, which is more than a minor fix. |
| R4 | minor | fixed | `43524c8d` | Verified: the test asserted codex, antigravity and claude/local/api only, so the old name claimed "nothing else flagged" while kimi is flagged. Renamed to `TheFlags_AreD13s_CodexUnconfinedEverywhere_AgyDefaultDeny_ClaudeLocalApiConfined`. No other reference to the old name exists. The kimi assertion stays in `AdmissionFlagsBothRows`. |
| R5 | minor | not fixed | — | Verified: the plan's status line says E5.5–E5.6 were built on Sonnet; §0.2 says Opus for these stories. No numbered deviation records it. I did not add one: a deviation saying the operator accepted the builder would claim an acknowledgement I cannot get. Needs the operator's decision, or an Opus confirmation. |

No finding was wrong. All five claims checked out against the code or the docs.

## Checks

Results are in the section below.

## Results (run after the last commit `ff7273c5`, 15:50–15:58 UTC)

- Build: `dotnet build dew_flow_connect_other_ais.slnx -c Debug -m:4 --disable-build-servers` — succeeded, 0 warnings, 0 errors.
- Changed test classes: `ConfinementPlannerTests` 35/35, `CapabilityMatrixTests` 47/47 (after the R2 and R4 edits). `AKimi*` filter: total 37, failed 0, succeeded 25; the other 12 were skipped (Windows-only). Skip count not yet checked against the plan.
- Whole MCP suite (one run): total 10557, **failed 20**, succeeded 10453, skipped 84.
  - 2 known (brief): `KimiRuntimeTests.ALaunchThatNamesItsHome…`, `KimiHomeTests.AHomeWhoseDataPathPasses260…`.
  - 17 the coder recorded as red at baseline `b4e2ea96` (ACodeRoundRecordsItsSizeTests, ARoundsNumberComesFromTheJournalTests, EndToEndTests, ARoundIsMintedAKeyTests, SplitOrderTests, ThePersonsCommandsReachTheCallerTests). Not re-checked against baseline by me.
  - 1 not in the coder's list: `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow`. Rerun alone: 10/10 pass. Judged flaky under full-suite load; not confirmed against baseline.
  - So the count is 20, not the coder's 19.
- Format: `dotnet format --verify-no-changes --no-restore --include` on the 3 changed C# files: exit 0, no errors. Whole-solution format not rerun (the coder saw `src_server` analyzer noise, not from this story).
- Generator: `src_vs_code/scripts/generate-runtime-capabilities.mjs --check`: up to date, 21 rows over 7 runtimes × 3. Exit 0.
- TS (`npm test` in `src_vs_code`): main run 6290 tests, 6270 pass, 1 fail, 19 skipped. The fail is `the baseline names no release that was never tagged` (known, no git tags). Overall exit 1 from that test alone.
- Plan lifecycle: clean.
