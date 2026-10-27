# T4-t5 — review notes (the docs: E5 plan status, `module_core.md`, `module_tests.md`)

Reviewed commit `00cacd7a` against `vllm/work/T4/t5.md`. Result: four wording fixes in the plan, one commit.

## Findings

### Fixed
1. **Item 32 anchor off by one.** It said `KimiConsultant.cs:63-70`. `MustCarryNoPlannedFlag` runs from line 63 to 69. Now `:63-69`.
2. **Item 36 named the wrong reds.** It called the reds of S5.6.7 "the two recorder reds in item 33". One of the two is the empty `copies` red (planner arm removed). Now "the two in item 33 (the empty `copies`, and the recorder)".
3. **Item 37 had a garbled sentence.** It listed `CapabilityMatrixTests` and `ConfinementPlannerTests` twice. Now one clause: the new facts of both classes, and the TS test, run on every OS.
4. **Item 35 last sentence was unclear.** "The contract's 'no seam' was the reason" now reads "The contract gave no seam; one was needed, and this one is in the test file."

### Right as it was
- Only the three allowed files changed (plan, `module_core.md`, `module_tests.md`). The plan was not moved. Deviations 1–30 and 40–46 untouched. DoD untouched, nothing ticked — right, the live runs and RESULTS §31 are owed.
- Status line and the added sentence: equal to Contract 1, date 2026-10-26. No "not started" text left in the plan.
- New section is between E5.4's item 30 and `### E5.1–E5.2 as built`. Items 31–39 (nine, the maximum). First line as the contract says.
- Every MUST item of Contract 3 is there: split (31), no production change in E5.5 (32), the green-on-arrival rows with their breaks (33), where tests run (37), the failures not of this epic (38), the other differences (34, 35, 36, 39).
- Code anchors checked against the files: `ConfinementPlanner.cs:158` at `b4e2ea96` (default arm), `:157` (kimi arm), `:211-216` (`Kimi`); `KimiConsultant.cs:50`; `VibeHomes.cs:223`; `ConsultantCheck.cs:232`; `ConsultCheckScratch.cs:131`. All right.
- Item 34 (S5.6.5 was red on arrival at the `disk` arm): right. The test asserts `none` first, then `disk`, and the red is on `disk`, so "the `none` arm passed" holds. The t2 reviewer re-ran this red.
- Item 32: `git diff b4e2ea96..HEAD --stat` shows t3 (`44bea5fe`) changed only `AKimiConsultantCheckTests.cs`.
- Red messages: each one in the plan and in `module_tests.md` is in a coder or reviewer note (t1–t4). The two "not recorded" cases (S5.6.7 Refused arm before the fix; S5.5.3 break 3 text) are marked as such.
- `module_core.md`: table row 84, the **Kimi's arm (S5, 2026-10-26)** bullet (version 2.1.1, date 2026-10-13, `MustCarryNoPlannedFlag`, the record name — all equal to the seed), and ", twenty-one since S5" (3 kimi vectors added; 7 runtimes × 3). Right.
- `module_tests.md`: five rows in the same table as `:3944-3945`, three cells each; every test name exists in the code (checked by grep).

## Checks and tests (this host: Linux, WSL2)
- `plan-lifecycle.mjs`: clean, exit 0 (before and after my edits). `pin-check.mjs`: OK, exit 0.
- Build (`-m:4 --disable-build-servers`): 0 warnings, 0 errors.
- `AKimiConsultantCheckTests` 5 of 5. `AKimiQuestionRowTests` 3 of 3. `CapabilityMatrixTests` 39 of 39. `ConfinementPlannerTests` 35 of 35. All 0 skipped.
- `AVibeQuestionRowReadsACopyOfItsRootsTests`: 6 passed, 12 skipped (the Windows-only rows — as item 37 says).
- Not run: the whole suite, the extension suite, a Windows run. No code changed in this task or in this review.
