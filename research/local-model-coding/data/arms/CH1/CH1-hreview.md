# CH1 — review notes: E5.5 and E5.6 (Haiku reviewer, branch `exp/kimi-e56-classic-haiku-review-haiku-fix-1`)

Base `b4e2ea96`, reviewed head `5b9ca22b`. Scope read: plan E5 §0, E5.5, E5.6, deviations 50–59, coder notes `CH1-code.md`,
the selected rules (csharp.doctrine, common.testing, common.scenario-tests, common.generated-code-tests read in full).
Baseline build of `5b9ca22b` before any change: Build succeeded, 0 warnings, 0 errors.

## Step 1 — findings (written before any code change)

Scenario check, S5.5.1–S5.5.4 and S5.6.1–S5.6.8: each test exists under the table's name and asserts its *Then*.
Teeth for E5.5 are recorded by the coder (three breaks, each reverted). The E5.6 product code has red-first evidence in the coder's table,
but no recorded teeth break after the fix. Decisions checked: planner arm (`ConfinementPlanner.cs:205-214`), standings (vectors and seed rows
match the plan's cells), no flags for kimi (`MustCarryNoFlags`, `KimiConsultant.cs:59-67`), the planner refuses `web`, the disk snapshot
excludes `.mcp.json` and `.agents/agents` (`SnapshotOptions.cs:62`, M-K9 (e)), the cited M-K cells and the record date `2026-10-13`
all match `research/RESULTS_kimi_confinement.md`. The generator `--check` is clean.

- **H1 — major — not fixed (out of scope).** `research/module_tests.md` does not name the two new flows (`AKimiConsultantCheckTests`,
  `AKimiQuestionRowTests`). `common.scenario-tests` requires the catalogue entry when a flow ships. Why not fixed: the brief puts `research/`
  out of scope, and the plan (E5 §"as built") defers it. The owner must decide.
- **H2 — minor — fixed.** `src_mcp/tests/AKimiQuestionRowTests.cs:174-177`: the scratch-gone assertion sits behind
  `if (played.Shims.TryPeek(...))`. For the answered and cancelled arms the same method already asserts `ContainSingle`, so the guard never
  skips there, but it reads as a silent skip. Fix: move the assertion into the non-refused branch of `TheShimsSeen`, where the shim is known.
- **H3 — minor — not fixed (documented).** `src_mcp/tests/AKimiConsultantCheckTests.cs:121`: S5.5.1's *Then* says "scratch repository and
  canary unchanged"; the test asserts `canary == not-attempted` and infers the scratch invariant from `state == answered`. This is the
  check's own invariant (a breach is `failed`), and deviation 51 records it. Why not fixed: consistent with the check's design.
- **H4 — minor — not fixed (cosmetic).** `AKimiConsultantCheckTests.cs:148`: `(Text(said,"state") == "answered").Should().Be(ending == "passed")`
  is correct but reads indirectly. Why not fixed: harmless, and it is not worth churn in a reviewed test class.

No blocking findings. Security: no kimi row is admitted without its `unconfined` flag (vectors and seed agree); no test writes outside its
`testsToDelete-` folder or the fake person's folder.

## Step 2 — fixes

- H2: test-only change to `AKimiQuestionRowTests.cs`; then the changed classes and the whole suite are re-run.

## Results

(filled in at the end)

### Step 2 — outcomes

- H1 — not fixed (out of scope: `research/` is excluded by the brief; the plan defers it). Owner decision needed.
- H2 — fixed in commit `0767f419`. The scratch-gone assertion now runs in `TheShimsSeen`, where the shim is known. `AKimiQuestionRowTests`: 3 of 3 pass alone.
- H3 — not fixed. Deviation 51 records the design; the check's invariant covers the scratch.
- H4 — not fixed. Cosmetic; correct as written.

## Results (this host, 2026-10-28, UTC)

- Build (Debug, `dew_flow_connect_other_ais.slnx`, `-m:4 --disable-build-servers`): Build succeeded, 0 warnings, 0 errors (after H2).
  One earlier attempt aborted with an internal CLR error; the retry succeeded.
- MCP suite on the code before H2 (`5b9ca22b`): total 10555, failed 20, succeeded 10451, skipped 84.
  - 2 are the known ones: `KimiRuntimeTests.ALaunchThatNamesItsHome…`, `KimiHomeTests.AHomeWhoseDataPathPasses260…`.
  - 18 are round and git tests: `ACodeRoundRecordsItsSizeTests` (7), `ARoundIsMintedAKeyTests` (2), `SplitOrderTests` (1),
    `EndToEndTests` (3), `ARoundsNumberComesFromTheJournalTests` (3), `TheRoundsNumstatsAreCountedTests` (1),
    `ThePersonsCommandsReachTheCallerTests` (1).
  - The 18 also fail when run alone (114 tests, 18 failed). The same 18 fail on base `b4e2ea96` in a clean worktree, built and run the same way.
    They are pre-existing on this host and not caused by E5.5–E5.6. The worktree was removed.
  - Not verified: a full-suite run after H2 (no time). The changed class was run alone after H2: 3 of 3 pass.
- Format (`dotnet format --verify-no-changes --no-restore`): no formatting diagnostic. The exit is 2 from 54 pre-existing IL2026/IL3050 errors in `src_server` and `src_bugs`. The coder recorded the same at base.
- Generator (`src_vs_code/scripts/generate-runtime-capabilities.mjs --check`): up to date, 21 rows over 7 runtimes × 3 capabilities.
- TS (`npm test` in `src_vs_code`, on `5b9ca22b`; H2 is C# only): 6290 tests, 6270 pass, 1 fail, which is the known tag test `the baseline names no release that was never tagged`.
