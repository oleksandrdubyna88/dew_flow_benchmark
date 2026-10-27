# SR3 review of E5.5 and E5.6 (Haiku, reviewer and fixer), 2026-10-27

Scope: `git diff b4e2ea96..HEAD` on `exp/kimi-e56-sonnet-split-haiku-review-3`.
Reviewed against `todo/PLAN_vibe_and_kimi_runtimes_E5.md` §E5.5, §E5.6 and "E5.5–E5.6 as built".
Rules read: `.agents/PROJECT.md`, `common.testing` and the other selected rules as listed by the resolver (the rule text was read
for the ones this change touches). No coder notes file `SR3-code.md` exists in `notes/`; the coder's `SR3-t*-code.md` were not used.

## Scenario check (12 scenarios)

| Scenario | Test | Result |
|---|---|---|
| S5.5.1 | `AKimiConsultantCheckTests.AKimiCheck_RunsInACopy_OnAOneShotHome` | name and assertions match the *Then*; the scratch and canary "unchanged" part is implied by `state == answered` (see H2) |
| S5.5.2 | `AfterTheCheck_TheHomeIsGone_JunctionFirst` (Theory, 2 arms) | the non-fail arm does not assert "passed", the fail arm does not assert exit 70 (see H1) |
| S5.5.3 | `ACheckCancelledMidTurn_StillDisposesItsHome` | matches the table |
| S5.5.4 | `ARootlessCheck_KeepsTheHomeInItsScratch_AndDisposesItJunctionFirst` | matches the table |
| S5.6.1 | `CapabilityMatrixTests.KimiHasThreeRows_EachCitingItsCells` | matches |
| S5.6.2 | existing `TheVectorsActuallyLoaded_AndCoverEveryPairOfTheTable` | covered by the new vectors and `Records` entry |
| S5.6.3 | `ConfinementPlannerTests.ThePlannerArm_IsACopyForDisk_AnEmptyFolderForNone_AndRefusesWeb` | matches |
| S5.6.4 | `CapabilityMatrixTests.AdmissionFlagsBothRows` | matches |
| S5.6.5 | `ConfinementPlannerTests.AKimiQuestionLaunch_IsTheShimRequest_InItsFolderOrItsCopy_OnTheRowsHome` | matches |
| S5.6.6 | `ConfinementPlannerTests.APlanWithFlags_IsRefusedForKimi` (three plans: flag, leading, add-dir) | matches |
| S5.6.7 | `AKimiQuestionRowTests` (answered, refused, cancelled, plus a `none` row) | matches; the name carries `_WhenAnswered` |
| S5.6.8 | `capabilityAdmission.test.ts` `kimi rows admit flagged, web refuses naming its record` | matches |

Teeth: the coder's notes say each E5.5 scenario got teeth by a temporary break named in a task summary, which I cannot see.
I did not re-run those breaks (time). That is an unverified claim, not a finding.

## Decisions check

- Planner arm (`ConfinementPlanner.cs:157`, `:205-215`): `disk` → Snapshot with the roots copied; `none` → Scratch; `web` refused by the matrix. Correct.
- No flags for kimi: planner carries none; `KimiConsultant.MustCarryNoPlanFlags` refuses a plan with flags, leading or add-dirs. Correct.
- One-shot home disposed junction-first: `ConsultantCheck.TurnAsync` (`finally` disposes), `QuestionRowLaunch` untouched, tests assert `GitScratch.AfterUnlink` is never invoked. Correct.
- Rows and vectors: `shared/runtime-capabilities.json`, `shared/capability-matrix-vectors.json` and `RuntimeCapabilities.Runtimes` agree on `kimi`. The cited cells (M-K3, M-K4, M-K6, M-K9, M-K9 (e), §14 for web) exist in `research/RESULTS_kimi_confinement.md`.
- Generated table: `runtimeCapabilities.generated.ts` matches the generator (`--check` passes).
- Security: `none` and `disk` are `Unconfined` and flagged in the matrix and the TS admission. Nothing is admitted unconfined without its flag. Tests touch only their own `testsToDelete-` folders.

## Findings

H1 | minor | `src_mcp/tests/AKimiConsultantCheckTests.cs:144-157` | S5.5.2's *Then* says the non-fail arm "passed" and the fail arm "exit 70", but the test asserts neither (only `state != answered` on the fail arm).
Fix: assert `state == answered` on the non-fail arm and capture the exit code and assert `70` on the fail arm.

H2 | minor | `src_mcp/tests/AKimiConsultantCheckTests.cs:102-113` | S5.5.1's *Then* names "the scratch repository and its canary unchanged"; the test does not assert it directly.
Fix: none needed. `ConsultantCheck.Judged` (`ConsultantCheck.cs:309-311`) returns `TreeChanged` on any breach, so `state == answered` already implies both unchanged. Wrong on second look; I will not add an assertion for it.

H3 | minor | `todo/PLAN_vibe_and_kimi_runtimes_E5.md` E5.5 table, "Expected red today" column | stale: the coder's own deviation 50 says so. Documentation only.
Fix: not fixed here; the plan's owner should update the column. Not code.

H4 | minor | `shared/runtime-capabilities.json:22` | a bare `""` entry in the header notes array produces a blank line; cosmetic.
Fix: not fixed; it reads as an intentional paragraph break, and changing it is not worth the churn.

No blocking or major findings.

## Outcomes

- H1 — FIXED in part. Added the `state == answered` assertion on the success arm of `AfterTheCheck_TheHomeIsGone_JunctionFirst`
  (`AKimiConsultantCheckTests.cs`). I first also asserted the check exits 70 on the failing arm; that was WRONG on a second look.
  The check's own exit code is 0 whenever it ran (the shim's 70 is the turn's exit, not the check's); the test failed on it
  (`Expected code to be 70 … found 0`), and I removed that assertion. The failing arm keeps `state != answered`. The "exit 70" in the
  table therefore is not asserted; the check's record is what the test can see.
- H2 — wrong on second look: no assertion added. `ConsultantCheck.Judged` (`:309-311`) turns any breach into `TreeChanged`, so
  `state == answered` already means the scratch repo and the canary are unchanged.
- H3 — not fixed (documentation: the plan owner should update the stale "Expected red" column).
- H4 — not fixed (cosmetic blank entry in the JSON notes array; it reads as a paragraph break).
- Teeth of S5.5.1–S5.5.4 and S5.6.7 — not re-verified by temporary breaks in this review (no time; the claim rests on the coder's task summary, which I could not read).

## Results (this review)

- Build (`dew_flow_connect_other_ais.slnx`, Debug, -m:4 --disable-build-servers): 0 errors, after the H1 edit.
- Kimi classes (`--filter-class "*Kimi*"`): 318 total, 295 passed, 21 skipped (Windows-only), 2 failed — both the known host failures
  (`KimiRuntimeTests.ALaunchThatNamesItsHome…`, `KimiHomeTests.AHomeWhoseDataPathPasses260…`).

## Full results (after the H1 commit 7a654649)

- MCP suite, once (`notes/SR3-hreview-suite.txt`): total 10557, succeeded 10470, skipped 84, failed 3:
  1. `KimiRuntimeTests.ALaunchThatNamesItsHome_…` — known host failure.
  2. `KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed` — known host failure.
  3. `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow` — NOT on the known list. It fails alone too (1 of 10).
     Its 1 ms git deadline gives "recorded" where it expects "unavailable: git…". No changed file touches the rounds numstats code.
     I did not prove it fails on `b4e2ea96`; that is unverified. It looks host-timing dependent, so the next reviewer should check it.
- Format (`dotnet format whitespace --verify-no-changes` on the changed C# files): no output, no changes reported.
  I did not capture its exit code, so treat that as "no output", not as a confirmed pass.
- Generator check (`scripts/generate-runtime-capabilities.mjs --check`): up to date, exit 0.
- TS tests (`npm test` in `src_vs_code`, `notes/SR3-hreview-ts.txt`): 6290 tests, 6270 pass, 1 fail, 19 skipped. The 1 fail is
  `the baseline names no release that was never tagged` (the known no-tags test).
