# SR2 review of E5.5 and E5.6 (branch exp/kimi-e56-sonnet-split-haiku-review-2)

Written before any code change. Scope read: the resolver's selected rules (testing, scenario-tests, test-leftovers,
csharp and typescript doctrine, generated-code-tests, security, coding-style, reuse-first, review-gate),
the E5 plan §0, E5.5, E5.6, "Where the parent plan and the code disagree", and the as-built notes 50–56.

## Findings

H1 — major — `src_mcp/tests/AKimiQuestionRowTests.cs:137` and `:203`
`ScratchFolders()` scans the whole temp folder for `coai-question-*` before and after, and asserts the sets are equal.
`AVibeQuestionRowReadsACopyOfItsRootsTests` makes real `coai-question-*` folders in the same temp folder, and neither
class carries `[Collection("fakecli-env")]`, so they run in parallel. A Vibe row's folder appearing or being removed
inside the Kimi test's window fails it. This is a shared-state defect (testing.md: passes alone, fails in the suite).
Fix: put both classes in `[Collection("fakecli-env")]` so they run one at a time.

H2 — major — `src_mcp/tests/AKimiQuestionRowTests.cs:204` (`_unlinked.Should().BeEmpty`)
The as-built note (item 53) says the recorder assertion was never reached in the teeth run and was never broken on
its own. A "never invoked" assertion with no observed red is unproven. Fix: temporarily make the row's home disposal
call the plain deleter, watch the recorder assertion go red, revert, and record it. If it stays green, the assertion
is vacuous and the fix is to make it bite.

H3 — minor — `research/module_tests.md` (new table) and `src_mcp/tests/CapabilityMatrixTests.cs:80`
S5.6.2 names `TheVectorsCoverKimi`, which does not exist. The covering test is the existing
`TheVectorsActuallyLoaded_AndCoverEveryPairOfTheTable`. Fix: record the real name in the module notes. Renaming the
existing test is out of scope.

H4 — minor — reuse-first — `src_mcp/tests/AKimiConsultantCheckTests.cs:44-59`, `src_mcp/tests/AKimiQuestionRowTests.cs:58-74`
The `Scopes` helper and the `StandIn()` pattern are copied from `AKimiConsultationScenarioTests` (existing). A shared test
helper would be the right move, but it touches an existing test file. Not fixed in this review: named here, to propose
to the owner.

H5 — minor — `src_mcp/tests/AKimiConsultantCheckTests.cs:111` and similar: `Path.Combine(_homes.Path, VibeHomes.OneShotFolder)`
The test reads a Vibe constant for a Kimi home root. The shared folder name is intentional (decision J3), so this is
correct. Recorded only as a naming note. Not a defect; no fix.

## Checked and found correct

- All 12 scenario names exist, except H3 (S5.6.2). The other eleven match the table. S5.6.8 is the TS test
  `kimi rows admit flagged, web refuses naming its record`.
- Planner arm: `none` gives `Scratch`, `disk` gives `Snapshot` with the roots, no flags. `web` is refused by the matrix
  before the arm is reached. The default arm is unchanged for other runtimes.
- `KimiConsultant.MustCarryNoFlags` refuses `Leading`, `Flags` and `AddDirs`, and the test theory covers each.
- Vectors and rows: `unconfined` for `none` and `disk`, `unmeasured` for `web`. The cited M-K cells exist in
  `research/RESULTS_kimi_confinement.md` (M-K6, M-K3, M-K4, M-K9, M-K9 (e)). The file's date is 2026-10-13.
- No test touches a path outside its own `testsToDelete-` folder. The test temp dirs are `TempDir.For(...)`.
- S5.5.1's "scratch repository and canary unchanged" is covered: a breach changes `state` away from `answered`.
- The unlink recorders are `AsyncLocal`, so parallel tests do not cross-talk.

## Fix log

(Filled in as each finding is handled: fixed, not fixed (why), or wrong on a second look (why).)

## Fix log (final)

- H1 FIXED (commit d6f30559): `[Collection("fakecli-env")]` on AKimiQuestionRowTests and AVibeQuestionRowReadsACopyOfItsRootsTests. The race was not reproduced; the fix follows from the shared folder reading.
- H2 FIXED as evidence, no code change: temporary break (row home disposed through the plain deleter) made the recorder assertion red for `answered` and `cancelled mid-turn`. `refused before the launch` never reserves a home, so it cannot show the recorder either way. Break reverted.
- H3 FIXED (in d6f30559): module_tests.md names the real covering test.
- H4 NOT FIXED: the Scopes helper is duplicated across three test classes. A shared helper would touch AKimiConsultationScenarioTests. Proposed, not done.
- H5 NOT FIXED: no defect (folder name is intentional, J3).

## Results

- Build (Debug, -m:4 --disable-build-servers): succeeded, 0 warnings, 0 errors.
- Changed classes: AKimiQuestionRowTests 3/3 pass; AKimiConsultantCheckTests 5/5 pass; AVibeQuestionRowReadsACopyOfItsRootsTests 6 pass, 12 skipped (Windows-only, D18).
- Whole MCP suite (once, after the fix): 10558 total, 10471 passed, 84 skipped, 3 failed:
  - KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed (known, host)
  - KimiRuntimeTests.ALaunchThatNamesItsHome_... (known, host)
  - TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow (NOT in the known list; the coder saw it fail in full runs, pass alone; not investigated)
- Generator check (generate-runtime-capabilities.mjs --check): up to date.
- NOT RUN before the deadline: dotnet format verify, the TS typecheck/lint/npm test, the generate-feature-availability check, and plan-lifecycle. These remain open.
