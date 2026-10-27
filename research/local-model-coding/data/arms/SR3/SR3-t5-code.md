# SR3 t5 — notes (docs only)

Commit: 09615abb docs(plan): E5.5 and E5.6 as built — the plan's status and deviations 50-54, the module notes for the kimi arm and the Kimi tests

## Done
- `todo/PLAN_vibe_and_kimi_runtimes_E5.md`: the `> Status:` sentence now says E5.5 and E5.6 implemented 2026-10-27
  (Sonnet-split tasks, Opus review pending). `### E5.5–E5.6 as built (2026-10-27)` added before `## Plan gate 2026-10-16`,
  items 50–54. Epic Definition-of-Done boxes not ticked. `plan-lifecycle.mjs`: clean. `pin-check.mjs`: OK.
- `research/module_core.md`: a bullet on the kimi arm (no flags, `Snapshot` for disk, `Scratch` for none, both `Unconfined`; web refused).
- `research/module_runners.md`: a bullet on `MustCarryNoPlanFlags` (`KimiConsultant.Build`).
- `research/module_tests.md`: new section at the end, one row per new test class/test, and `capabilityAdmission.test.ts`.

## Checked against the code
- E5.5 needed no production fix: commit `e6df9dbd` adds `AKimiConsultantCheckTests.cs` only (238 insertions);
  `ConsultantCheck.cs` and `ConsultCheckScratch.cs` are not in `b4e2ea96..HEAD`.
- `kimi` rows, vectors, `Runtimes`, `CapabilityMatrixTests` Kimi record, planner arm, `MustCarryNoPlanFlags`: read from the diff.

## Not verified (the task summaries are not in this worktree)
- The red messages for the new tests, and the teeth breaks for S5.5.x and S5.6.7. The module_tests table says
  "not in this worktree" for these. The "green on arrival" wording for S5.5.1–S5.5.4 comes from the task document, not from a summary I could read.
- Item 53's "teeth runs are in the task summary" is repeated as written; I could not check it.

## Test names as found in the code (two differ from the document)
- The planner's Kimi question-launch test and the flags-refusal test are in `ConfinementPlannerTests.cs`, not in
  `AKimiQuestionRowTests.cs`. The module_tests rows name them under `ConfinementPlannerTests`.
- The `kimi` admission checks in `CapabilityMatrixTests.cs` are `KimiHasThreeRows_EachCitingItsCells` and `AdmissionFlagsBothRows`.

## Left
- Live runs and the Opus review are owed (item 54); not run here.
- No tests were run: docs-only task, and the task said no code or test.
