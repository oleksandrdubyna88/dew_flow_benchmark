# CH4 — review of E5.5 and E5.6 (Haiku), 2026-10-28

Scope: `git diff b4e2ea96..HEAD` on `exp/kimi-e56-classic-haiku-review-haiku-fix-4`.
Read: AGENTS.md, ENTRY.md, PROJECT.md, resolver explain output, E5 plan §E5.5 and §E5.6, the coder notes.
Read in full: `KimiConsultant.cs`, the Kimi arm of `ConfinementPlanner.cs`, `AKimiConsultantCheckTests.cs`,
`AKimiQuestionRowTests.cs`, the new `CapabilityMatrixTests` and `ConfinementPlannerTests` cases, the TS admission test,
`shared/*.json`, the generated TS file.
Not yet read: the 33 selected rules as a whole. I read the rule list; `testing.md`, `scenario-tests.md` and
`test-leftovers.md` are checked against the new tests after these findings are written.

## Verified without a finding

- All 12 scenario test names exist. S5.5.1–S5.5.4 are in `AKimiConsultantCheckTests.cs`. S5.6.2 is `TheVectorsCoverKimi`, a new
  test kept beside the existing `TheVectorsActuallyLoaded…`. S5.6.7 is a 3-ending Theory.
- Planner arm: `kimi` goes to `Kimi(grant, flag)`. Disk is a snapshot of the roots, none is an empty scratch, no flags.
- Standings: `none` and `disk` unconfined and flagged, `web` unmeasured.
- A plan with flags is refused by `KimiConsultant.MustCarryNoFlags` (Leading, Flags, AddDirs).
- Rows, vectors and `RuntimeCapabilities.Runtimes` carry `kimi`.
- Citations: RESULTS_kimi_confinement.md §6 (M-K3), §7 (M-K4), §9 (M-K6), §12 (M-K9), §21 (M-K9 e) and §14 (web) exist and match.
- Generator: `generate-runtime-capabilities.mjs --check` reports up to date (21 rows, 7 runtimes × 3 capabilities).
- Security: no test or production path touches a folder outside the test's own `testsToDelete-` folders; no flag reaches Kimi's argv.

## Findings

H1 — major — `src_mcp/tests/AKimiConsultantCheckTests.cs:111-112` (S5.5.1)
The table's *Then* requires the scratch repository and its canary unchanged. The test asserts only `state` and `markerRead`.
Fix: assert `canary` is `not-attempted`, as `ConsultantCheckTests.cs:115` does for the same answer text.

H2 — minor — `src_mcp/tests/AKimiQuestionRowTests.cs:166` (S5.6.7)
`Directory.EnumerateFileSystemEntries(<homes>/coai-vh)` throws if that folder was never created, which may happen in the
refused-before-launch ending. The test could then fail for the wrong reason. To settle by running the class.

H3 — minor — `src_mcp/tests/AKimiConsultantCheckTests.cs:129-137` (S5.5.2)
The table asks for the person's folder bytes and read-only bits. The test uses `_person.Hash()`. To settle: check that the hash covers read-only bits.

## Outcome of the findings

- H1 — **fixed** (commit `f3cccd1c`). Added `canary == not-attempted` to S5.5.1. Build after the edit; `AKimiConsultantCheckTests` 5/5 green, `AKimiQuestionRowTests` 3/3 green.
- H2 — **wrong on a second look**. The `coai-vh` folder exists in the refused ending too, so the assertion at line 166 is not vacuous and does not throw. Class green, as above.
- H3 — **wrong on a second look**. `KimiFixture.Hash()` records `File.GetAttributes(path)` per entry, so the ReadOnly bit is covered.
- Format check (changed C# files, `dotnet format --verify-no-changes --include …`): exit 0.
- Generator check: up to date.

## Final results (after commit f3cccd1c)

- Build (Debug, `-m:4 --disable-build-servers`): 0 warnings, 0 errors.
- Changed Kimi classes: `AKimiConsultantCheckTests` 5/5, `AKimiQuestionRowTests` 3/3.
- Whole MCP suite: total 10556, failed 19, succeeded 10453, skipped 84. The 19 are the coder's list: 2 named Kimi host tests and 17 rounds-database tests that also fail at baseline `b4e2ea96`. None is in the E5.5/E5.6 code.
- Format check on changed C# files: exit 0.
- Generator `--check`: up to date.
- TS `npm test`: 6290 tests, 6270 pass, 1 fail (`the baseline names no release that was never tagged`, known, no git tags).
- TS `typecheck` and `lint`: run, no errors printed (exit codes not captured).

## Process

The coder read none of the 33 selected rules in full (plan deviation 39). This review reads the rule list and the three named rules
after the findings are written.
