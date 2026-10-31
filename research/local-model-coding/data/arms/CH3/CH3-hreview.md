# CH3 — review of E5.5 and E5.6 (Haiku reviewer, 2026-10-28)

Branch `exp/kimi-e56-classic-haiku-review-haiku-fix-3`. Scope: `git diff b4e2ea96..HEAD`. Findings written before any code change.
Deadline 14:34 UTC; reviewed from 14:06 UTC.

## Findings

**H1 — major — `src_mcp/tests/AKimiConsultantCheckTests.cs:199-202` (S5.5.4)**
The assertion `Directory.Exists(homeSeen).Should().BeTrue("the home is made before the shim starts")` sits inside the played shim's `Turn`.
`ConsultantCheck.cs:190` catches every exception (`catch (Exception e) when (e is not OutOfMemoryException)`) and records a failed check, so this assertion can never fail the test.
Fix: capture `Directory.Exists(homeSeen)` in a variable inside `Turn`, and assert on it after `CheckAsync` returns.

**H2 — major — `src_mcp/tests/ConfinementPlannerTests.cs` (S5.6.6) and `src_mcp/runners/Kimi/KimiConsultant.cs:59-66`**
Decision 3 says a plan with any argv fragment is refused: `Leading`, `Flags` and `AddDirs`. Only `Flags` is tested.
Deleting `plan.Leading` or `plan.AddDirs` from `MustCarryNoFlags` keeps every test green, so the guard is not proven.
Fix: make `APlanWithFlags_IsRefusedForKimi` a Theory over a plan with `Leading`, with `Flags`, and with `AddDirs`.

**H3 — minor — `src_mcp/tests/AKimiConsultantCheckTests.cs:116-127` (S5.5.1)**
The table's *Then* says "the scratch repository and its canary unchanged". The test does not assert it directly.
The claim does hold: `ConsultantCheck.cs:223-226` (`Broken`) fails the check on any change, and the test asserts `state == answered`.
Fix: not planned. Covered by the check's own verdict; a direct assertion would only repeat it.

**H4 — minor — `src_mcp/runners/Kimi/KimiConsultant.cs` / `ConfinementPlanner.cs:205-213` (the `web` arm)**
`Kimi(...)` maps `web` to `_` (Scratch, no flags). Reachable only if a caller skips the matrix.
The same shape exists for Vibe, and the matrix refuses `web` first (S5.6.3 and S5.6.4 pin that).
Fix: not fixed. Wrong on a second look as a blocker: it matches Vibe, and the refusal is tested.

**H5 — minor — `todo/PLAN_vibe_and_kimi_runtimes_E5.md` (E5.5–E5.6 as built, decision 51)**
Decision 51 says S5.5.1 to S5.5.4 and S5.6.7 got their teeth by temporary breaks, with recorded output. I did not re-run those breaks in this review.
Fix: not done in this review (time). The breaks are listed in the plan and are the next thing to repeat if there is time.

## Checked and found sound (no finding)

- The 12 scenarios each have a test with the planned name and assert the planned *Then* (S5.5.1–S5.5.4 in `AKimiConsultantCheckTests`; S5.6.1, S5.6.4 in `CapabilityMatrixTests`; S5.6.2 the existing vectors test; S5.6.3, S5.6.5, S5.6.6 in `ConfinementPlannerTests`; S5.6.7 in `AKimiQuestionRowTests`; S5.6.8 in `capabilityAdmission.test.ts`).
- Planner arm: `disk` → Snapshot with the roots, `none` → Scratch, no flags. Vectors and rows carry `unconfined` with `flag: unconfined`, and `web` is `unmeasured` and refused. Matches the plan's decisions 1–4.
- One-shot home: `VibeHomes.ReserveOneShot` and the `finally` in `ConsultantCheck.TurnAsync` dispose the home. Nothing outside a test's own `testsToDelete-` folder is linked.
- `runtimeCapabilities.generated.ts`: the diff matches the JSON rows. The generator `--check` is run below.
- Decision 8 date (2026-10-13) and version (2.1.1) match the plan.

## Fixes and results

(to be filled in below)

### Fixes made (commit `9c33fa4f`)

- **H1 — fixed.** `AKimiConsultantCheckTests.cs`: the `Directory.Exists(homeSeen)` fact is read out of the shim turn into `madeBeforeTheShim`, and asserted after the check returns. The turn no longer asserts.
- **H2 — fixed.** `ConfinementPlannerTests.cs`: `APlanWithFlags_IsRefusedForKimi` is a Theory over `leading`, `flags` and `addDirs`. Each case sets only that fragment.
- **H3 — not fixed.** Covered by the check's own verdict (`ConsultantCheck.cs` `Broken`); a direct assertion would repeat it.
- **H4 — not fixed.** Matches Vibe's `web` arm; the matrix refuses `web` first, and S5.6.3 and S5.6.4 pin that.
- **H5 — not fixed in this review.** The teeth claims of decision 51 are recorded in the plan; they were not re-run here.

### Results (this review)

- Build (`dotnet build dew_flow_connect_other_ais.slnx -c Debug -m:4 --disable-build-servers`): succeeded, 0 warnings, 0 errors.
- Changed MCP classes: `*AKimi*` 25 passed, 12 skipped (Windows-only, D18, pre-existing); `ConfinementPlannerTests` 36/36; `CapabilityMatrixTests` 39/39.
- Generator `--check`: up to date (21 rows over 7 runtimes).
- Extension: `typecheck` clean; `lint` clean; `npm test`: 6290 tests, 6270 pass, 19 skipped, 1 fail (`the baseline names no release that was never tagged`, known).

### Teeth check (H1, H2), done after the fixes

- H2: KimiConsultant.cs guard with plan.Leading removed. APlanWithFlags_IsRefusedForKimi(fragment: "leading") went red, the one case. flags and addDirs stayed green. Reverted.
- H1: madeBeforeTheShim forced to false. ARootlessCheck_KeepsTheHomeInItsScratch_AndDisposesItJunctionFirst went red with "Expected madeBeforeTheShim to be True ... but found False". Reverted.
- After the revert: tree clean at 9c33fa4f, rebuilt, AKimiConsultantCheckTests 5/5 and ConfinementPlannerTests 36/36 green.

### Final runs at 9c33fa4f

- Whole MCP suite (CoaiMcp.Tests, once, 7m 02s): total 10557, failed 19, succeeded 10454, skipped 84.
  - The 2 known: KimiRuntimeTests.ALaunchThatNamesItsHome..., KimiHomeTests.AHomeWhoseDataPathPasses260...
  - 17 rounds-database tests (ACodeRoundRecordsItsSizeTests, ARoundsNumberComesFromTheJournalTests, EndToEndTests, ARoundIsMintedAKeyTests, SplitOrderTests, ThePersonsCommandsReachTheCallerTests). These are the 17 the coder's notes name. I did NOT run a clean-checkout baseline, so I cannot confirm from my own run that they predate this work. None is in a file this work changed.
- Format, the seven changed C# files: dotnet format --verify-no-changes exit 0.
- Format, whole solution: exit 2, 54 errors, all in src_bugs (32) and src_server (76). None in src_mcp.
- Generator generate-runtime-capabilities.mjs --check: up to date, 21 rows over 7 runtimes.
- Extension: typecheck clean, lint clean, npm test 6290 tests: 6270 pass, 19 skipped, 1 fail (the known "the baseline names no release that was never tagged"). The coder's note said 6289 pass; the 19 skips are the difference, and I report the run as it is.
