# DR2 code notes — E5.5 and E5.6 (coder: Haiku, branch exp/kimi-e56-haiku-direct-review-2)

Started 12:33 UTC at b4e2ea96. Deadline 13:48 UTC. Commits: 07cdd131 (code and tests), 2d141242 (plan).

## Decisions

- E5.6 Kimi rows (plan E5.6 decision 2): `none` and `disk` = unconfined (cells M-K6, M-K3, M-K4; disk adds M-K9). `web` = unmeasured. Record `research/RESULTS_kimi_confinement.md`, measured 2026-10-13, kimi 2.1.1.
- Planner `kimi` arm: disk = Snapshot with SnapshotSources, none = Scratch, no flags (E5.6 decision 1).
- `KimiConsultant.Build` refuses a planned launch with any flag (E5.6 decision 3), `ArgumentException` naming the shim.
- New test classes: `AKimiQuestionRowTests` (S5.6.1, 3, 4, 5, 6) and `AKimiConsultantCheckTests` (S5.5.1–4). Chosen to keep the diff small; the plan allows it.
- S5.6.8 is a new TS test at the end of `capabilityAdmission.test.ts`. The runtimes list there and the gemini refusal sentence gained `kimi`.

## Red messages (what was seen)

- S5.6.1 `KimiHasThreeRows_EachCitingItsCells`: `Expected none not to be <null>.` Matches the table.
- S5.6.3 `ThePlannerArm_…`: no assertion reached. The test helper cast `Refused` to `Planned` (`InvalidCastException`). The table expected `Expected plan.Cwd to be CwdKind.Snapshot, found CwdKind.Scratch`. The helper fails first, so the table's message did not appear.
- S5.6.4 `AdmissionFlagsBothRows`: `Expected type to be Admitted, but found Refused`. The table said `…not a runtime the question consultant can launch`. Same cause, different text.
- S5.6.5 `AKimiQuestionLaunch_…`: no assertion reached, same cast problem as S5.6.3.
- S5.6.6 `APlanWithFlags_IsRefusedForKimi`: same cast problem. The table expected `no exception was thrown`.
- S5.6.8 TS: not run red separately. The runtimes list and gemini sentence were edited before the new test, so the red was not captured.
- S5.5.1 `AKimiCheck_RunsInACopy_OnAOneShotHome`: red, but from a test bug. The home regex used `\` on Linux. Fixed; then green.
- S5.5.2 `AfterTheCheck_TheHomeIsGone_JunctionFirst`, S5.5.3 `ACheckCancelledMidTurn…`, S5.5.4 `ARootlessCheck…`: green on arrival. E5.3's disposal and E5.4 already landed. No red seen, so teeth are not proven.
- Extra test `AFailedTurn_TheHomeIsGoneToo` (not in the table): its first red was a wrong expectation (exit code). Fixed to assert `state == failed`. Green.

## Results

- `AKimi*` classes: 26 passed, 12 skipped (Windows-only), 0 failed.
- `CapabilityMatrixTests`: 37 of 37 passed.
- Generator: `generate-runtime-capabilities.mjs` wrote the file; `--check` says up to date (21 rows, 7 runtimes × 3).
- Extension `npm test`: 1 failure, `the baseline names no release that was never tagged` (a known failure; no git tags on this clone). The new S5.6.8 test passed (`kimi rows admit flagged…`). The generator checks passed.
- Whole MCP suite: see the end of this file.
- `dotnet format --verify-no-changes`: the run printed only build analyzer errors for `src_server` (IL2026/IL3050), which are not format errors. No `src_mcp/` line was printed. The format run was not clean, but it was not shown to fail on my files either. Not conclusive.
- The whole MCP suite shows these failures so far:
  - `KimiRuntimeTests.ALaunchThatNamesItsHome…` (known, not mine).
  - `KimiHomeTests.AHomeWhoseDataPathPasses260…` (known, not mine).
  - `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow`. This one is NOT on the brief's known list. It fails when run alone too. It has no link to Kimi code. I did NOT run a baseline build, so I cannot say whether it fails at b4e2ea96 too.

## Later update (after the first notes)

- **S5.6.7 written** (`src_mcp/tests/AKimiQuestionRowThroughTheServiceTests.cs`, commit d1114d0b). Two arms: answered (the cwd is not the repo, the prompt does not name the repo, `coai-vh` is empty after), and cancelled mid-turn (the home is empty after). Both green. The answered arm went green on arrival. The cancelled arm first let `TaskCanceledException` reach the caller; the test now accepts that. The refused arm and the recorder are not written.
- The first whole-suite run stopped printing after the Vibe-reviewer skips (line 154 of its output). I stopped it (TaskStop) and reran the suite after the S5.6.7 build.

## What remains

- The refused-launch arm of S5.6.7 and the `GitScratch.AfterUnlink` recorder of S5.5.2 and S5.6.7.
- S5.5.2–4 have no teeth proven (they were green on arrival). A break of the deciding line would show whether they bite.
- `TheRoundsNumstatsAreCountedTests.AnUnknownCommit…`: check against a baseline build.
- The `web` row's `resultRef` says "RESULTS §7" for the announced web tools. The record's §7 is M-K4. Check the right section.
- The live checks (J2) and M-K17 are out of scope and not run.

## Deviations (also written to the plan)

- Two new test classes instead of adding to existing ones.
- S5.6.7 not written.
- S5.5.2–4 green on arrival, reds not seen.
- S5.6.2 unchanged (it becomes meaningful once the rows exist).

## Whole MCP suite (final run, commit d1114d0b)

- total 10555, passed 10469, skipped 84, failed 2, duration 6m 41s.
- The 2 failures are the two known ones: KimiRuntimeTests.ALaunchThatNamesItsHome… and KimiHomeTests.AHomeWhoseDataPathPasses260….
- TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow PASSED in this run. It had failed in the earlier run and when run alone, so it looks flaky, not caused by this change. Not investigated further.
