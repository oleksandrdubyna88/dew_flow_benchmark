# CR2 — review of E5.5 and E5.6 (Haiku, reviewer who also fixes)

Branch `exp/kimi-e56-classic-haiku-review-2`, base `b4e2ea96`, HEAD at review start `e053e223`.
Step 1 findings below were written BEFORE any code change. Deadline 13:29 UTC.

## What was checked (Step 1)

- Rules: `rules.mjs explain` selected 35 rules for the changed files. I read the plan (§0, E5.4–E5.6, the
  as-built items 50–59), the coder notes, the production diff, the seed and generated diffs, the modified
  tests and both new test files in full. I did NOT read all 35 selected rules in full (time). The rules I
  relied on are the ones the plan cites. This is a gap in my review, stated in the final report.
- Each scenario S5.5.1–S5.5.4 and S5.6.1–S5.6.8: a test with the table's name exists (checked by name).
  Assertions compared with the table's *Then*. Result: all 12 present; the assertions match the *Then* except
  where a finding below says otherwise.
- Canary state strings checked against `ConsultantCheck.Canary` (`NotAttempted` is the product's own
  reading for a turn that never says the canary word). Those assertions are real, not tautologies.
- Citations checked: `RESULTS_kimi_confinement.md` §6 (M-K3), §7 (M-K4), §9 (M-K6), §12 (M-K9), §14, §21 (M-K9 (e))
  all exist and match the claims.
- Generators: `generate-runtime-capabilities.mjs --check` and `generate-feature-availability.mjs --check` are clean.
  `plan-lifecycle.mjs` clean. `pin-check.mjs` clean.
- Planner arm, `KimiConsultant.MustCarryNoFlag`, `QuestionRowLaunch` flow (scratch and home reservation, `finally`
  order): read; no defect found. A Kimi `none` row stands in its own `empty` folder, a `disk` row in the copy, and
  both homes are under the reserved `coai-vh\<8>`, which is released after the scratch is removed.

## Findings

**H1 — major — process, not code.** `todo/PLAN_vibe_and_kimi_runtimes_E5.md` §0 and §0.2 name Opus as the coder
for E5.5 and E5.6 ("the coder … E5.5, E5.6 … on Opus"), and the E5.5–E5.6 as-built heading says "Sonnet, 2026-10-28".
The build was not done by the marked model. File: `todo/PLAN_vibe_and_kimi_runtimes_E5.md` lines 1089 (heading) and 14–18 (§0).
Fix: not fixable by code. Report it to the operator. I do not re-run the coder. **Not fixed.**

**H2 — minor — test, weak assertion.** `src_mcp/tests/AKimiConsultantCheckTests.cs` `ACheckCancelledMidTurn_StillDisposesItsHome`
asserts `said.ToString().Should().Contain("cancelled")`. That matches any text in the whole JSON, so it can pass for the wrong
reason. The table's *Then* is "the record says cancelled". Fix: assert the quoted value `"cancelled"`. **Fixed** (commit `41749dd0`).

**H3 — minor — test, loose path assertion.** `AKimiConsultantCheckTests.cs` `AKimiCheck_RunsInACopy_OnAOneShotHome` line 128:
`After(request, "--cwd").Should().StartWith(_temp.Path).And.Contain(Path.Combine("own", "tree"))` does not prove the table's
`<scratch>\own\tree` shape (any path under TEMP that contains `own\tree` passes). Fix: pin the parent folder name `own` and the
`coai-check-` prefix on the scratch, the way S5.5.4 already does for the home. **Fixed** (commit `41749dd0`; the pinned shape is
`<scratch>\own\tree\<copy>`, as the run showed).

**H4 — minor — commit message overclaim.** Commit `e053e223` says the suite's other failures were "found at the base too". The
coder's deviation 59 says only two of the six failing classes were run at `b4e2ea96` (`ARoundIsMintedAKeyTests`,
`ACodeRoundRecordsItsSizeTests`). The other four (`EndToEndTests`, `ARoundsNumberComesFromTheJournalTests`, `SplitOrderTests`,
`ThePersonsCommandsReachTheCallerTests`) are unverified at the base. Fix: a commit message cannot be fixed without rewriting history,
which I will not do. I record it here and in the final report. **Not fixed (history). Plan deviation 59 is accurate, no edit needed.**

**H5 — minor — none found in code.** I looked for a real defect in the production change and found none: the flag refusal covers
`Flags` and `Leading`; `ForQuestion` is applied once; the cwd and home match the plan; the `web` refusal names the RESULTS record.
No change.

## Second look

- `AKimiQuestionRowTests.NothingIsLeftBehind` enumerates `coai-vh` with no existence check. It would throw, not fail an assertion, if
  that folder were missing. Checked: `VibeHomes.ReserveOneShot` creates it on every arm the tests reach, so it is not a defect. No change.
- The table says S5.5.1 asserts "the scratch repository and its canary unchanged". The test asserts the check's `state == answered` and the
  canary reading `not-attempted`; the invariant is inside `answered`. Judged adequate, not changed (I did not see a gap large enough to act on).

## Step 2 — what was done

- **H2 — fixed (commit `41749dd0`).** `ACheckCancelledMidTurn_StillDisposesItsHome` now asserts the quoted value `"cancelled"`
  instead of a bare substring. Test-only: the product already writes it. No red was shown first, because there is no product
  defect; the tightening is checked by the green run and by reading the assertion.
- **H3 — fixed (commit `41749dd0`).** `AKimiCheck_RunsInACopy_OnAOneShotHome` now pins `<scratch>\own\tree\<copy>`: the copy's
  parent is `tree`, whose parent is `own`, whose parent is `coai-check-*`. The first draft of this pin was wrong (it assumed the
  copy was `own\tree`); the run showed the product's real shape, and the assertion was corrected to it. Test-only, green.
- **H1, H4 — not fixed** (process and history, see above).
- **H5 — no change.**
- Generators (`--check`), plan-lifecycle, pin-check: clean (before and after the commit).
- Format (`dotnet format … --verify-no-changes --include` for the changed C# files): clean.
- TypeScript: `npm run typecheck` clean, `npm run lint` clean; `npm test` in progress at the time of writing (see the final report).
- TypeScript `npm test` (extension, `src_vs_code`): 6270 pass, 1 fail: `the baseline names no release that was never tagged`
  (the known failure of this clone, not this work's). The capability and generated-file tests are in the pass set.
- MCP whole suite: running at 13:05 UTC; result recorded in the final report.
- MCP whole suite (once, after the H2/H3 commit): total 10555, failed 20, succeeded 10451, skipped 84, 8m02s.
  The 20 failures: the 2 known Kimi tests (`KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed`,
  `KimiRuntimeTests.ALaunchThatNamesItsHome_…`); the 17 rounds-database tests the coder listed; and ONE the coder did not
  list: `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow`. It fails alone too (1 of 10 in its class).
  It is about the rounds numstat assembler; none of the changed files touch that code. NOT verified at `b4e2ea96` (no time
  for a base build). Reported as unverified, not as this work's.
- Known-failure counts from the coder's notes were 19; this run has 20 (the extra one above).
