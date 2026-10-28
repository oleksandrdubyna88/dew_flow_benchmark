# CH4 — coder notes (Sonnet), E5.5 and E5.6

Branch `exp/kimi-e56-classic-haiku-review-haiku-fix-4`, from `b4e2ea96`. No push, no `kimi`/`vibe` run, no live check.
Commits: `e7e304d2` (E5.5 tests), `74cfeece` (E5.6 tests + TS test), `5f91bb0c` (E5.6 code, seed rows, vectors, generated table), and one for the plan.

## Per scenario

| Scenario | Red seen? | Red message vs table | Green |
|---|---|---|---|
| S5.5.1 | No — green on arrival (E5.3/E5.4 already did it). Teeth: `NeedsASanitisedSnapshot=false` → check no longer answers. | n/a | yes |
| S5.5.2 (2 arms) | Teeth only: check's `held.Home.Dispose()` removed → *Expected …EnumerateFileSystemEntries to be empty … found coai-vh/<8>* | table's red was for "before E5.3 g", cannot be produced now | yes |
| S5.5.3 | Teeth only: same break as S5.5.2 | — | yes |
| S5.5.4 | Teeth: `ConsultCheckScratch.Delete` skips `KimiHomesDisposed` → *Expected _unlinkedByTheDeleter to be empty because the deleter's unlink recorder must not be invoked …* (the table's wording) | same | yes |
| S5.6.1 | Yes: *Expected none not to be <null> because Of("kimi", Capability.None)* | as table | yes |
| S5.6.2 | Yes (after the table rows were in): *Expected Vectors … to contain …* naming the pair; the old `TheVectorsActuallyLoaded…` test failed the same way | as table (loader's own message) | yes |
| S5.6.3 | Yes, but at the matrix refusal: *Expected type to be Confinement+Planned, but found Confinement+Refused* | differs: table said `Cwd … found Scratch`. The matrix answers before the planner's default arm, so a Kimi plan was refused first | yes |
| S5.6.4 | Yes: *Expected type to be Admission+Admitted, but found Admission+Refused* | as table in substance | yes |
| S5.6.5 | Yes, same refusal as S5.6.3 | differs: table said `WorkingDirectory … found …` | yes |
| S5.6.6 | Yes, same refusal as S5.6.3 (the hand-made plan was built with the planner helper for the launch) — not "no exception was thrown" | differs | yes |
| S5.6.7 (3 arms) | Yes: Answered/Refused/Cancelled all red while Kimi was no runtime. Then a vacuity found (see below); teeth: `EmptiedUnderItsLock` junction-first disposal broken → Answered and Cancelled red with the recorder message | recorder red as the table | yes |
| S5.6.8 (TS) | Yes (against the old generated file): `'kimi' is not a runtime the question consultant can launch for 'none'`, deep-equal on the runtimes list | as table | yes |

## Decisions

- Test names are the table's names. S5.6.2's test is `TheVectorsCoverKimi`, next to the existing `TheVectorsActuallyLoaded_AndCoverEveryPairOfTheTable`.
- E5.5 needed no production change (decision 1 of the story says so). S5.5.3 asserts `state == failed` and `failureKind` contains `cancel` (there is no `cancelled` state).
- Standings: `none` and `disk` `unconfined`, `web` `unmeasured`; date 2026-10-13, kimi 2.1.1; cells per the story. `RESULTS_kimi_confinement.md` not edited (out of scope), so the `resultRef` text cites section numbers as they stand today.
- A plan with any `Leading`/`Flags`/`AddDirs` is refused in `KimiConsultant.MustCarryNoFlags` (cyclomatic complexity 2).
- `CapabilityMatrixTests.Records` gained the Kimi record; three stale "…api, vibe" runtime lists became "…api, vibe, kimi" (C# ×2, TS ×1, plus the TS runtimes list).
- E5.6 tests run on Linux through `WindowsStandIn`, as E5.4's service rows do.

## Mistakes caught on the way

1. `GitScratch.AfterUnlink` is an `AsyncLocal`. My first `AKimiQuestionRowTests` set the recorder in `InitializeAsync`; it never reached the test flow, so S5.6.7 stayed green with disposal broken. Now set in the test body; teeth shown. `AKimiConsultantCheckTests` sets it in the constructor, which flows.
2. After a teeth break I reverted the source but ran the whole suite on the stale binary: 4 false failures in `AOneShotKimiHomeIsDisposedJunctionFirstTests`. Rebuilt and rerun; they are green.

## Results

- Whole MCP suite (rebuilt, from the repo root): total 10556, **19 failed**, 10453 passed, 84 skipped, 7m20s.
  - The 2 known failures (`KimiRuntimeTests.ALaunchThatNamesItsHome…`, `KimiHomeTests.AHomeWhoseDataPathPasses260…`).
  - **17 more, not in the brief's list, also red at the untouched baseline `b4e2ea96` on this host** (checked in a scratch worktree, then removed): `ACodeRoundRecordsItsSizeTests` (7), `ARoundsNumberComesFromTheJournalTests` (3), `EndToEndTests` (3), `ARoundIsMintedAKeyTests` (2), `SplitOrderTests` (1), `ThePersonsCommandsReachTheCallerTests` (1). Each says the rounds database holds no row (`RoundsQuery.Read(_data).Rounds` empty). I did not look into the cause; not mine, not fixed.
  - All my new tests pass: `AKimiConsultantCheckTests` 5, `AKimiQuestionRowTests` 3, the new planner/matrix tests.
- Extension: `npm test` 6390 tests, 1 failed: `the baseline names no release that was never tagged` (known, no tags). `capabilityAdmission.test.js` 31/31.
- Generator: `generate-runtime-capabilities.mjs` wrote the file (21 rows over 7 runtimes × 3); `--check` is up to date. `plan-lifecycle.mjs`: clean.
- `dotnet format --verify-no-changes --no-restore` on the whole solution prints analyzer errors (IL2026/IL3050) in `src_server` only — files I did not touch; run with `--include` on my 7 changed C# files it reports nothing. Whole-solution exit code was 0 in my run, but its output is dominated by the `src_server` noise, so I do not call the whole solution "clean".
- Not run: `typecheck`/`lint` separately (`npm test` runs `tsc`), `generate-feature-availability.mjs --check` (E5.4's, unchanged).

## What remains

- Review (Opus) of E5.5/E5.6; deviations 31–39 are in the plan.
- I did not read all 33 selected rules (≈200 KB); see deviation 39. The review should check the new tests against `testing.md`, `scenario-tests.md`, `test-leftovers.md`.
- Live checks (J2), M-K17, RESULTS §31, and the `web` admission remain out of scope.
- Tests were only run on this Linux host; Windows-only behaviour of the new tests (real junction) is unverified.
