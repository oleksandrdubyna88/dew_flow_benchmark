# O4 t5 — code notes (the E5 plan: status and E5.5–E5.6 as built)

Commit: ccc3bf3b on exp/kimi-e56-opus-split-haiku-4. One file: todo/PLAN_vibe_and_kimi_runtimes_E5.md. Not pushed.

## Done
- Status line 3 replaced with the contract text (E5.1–E5.7 implemented; live runs and M-K17 not run).
- Paragraph appended after the status block's last line: E5.5–E5.6 implemented 2026-10-27; deviations 31–47; not yet reviewed.
- Section `### E5.5–E5.6 as built (2026-10-27)` inserted after deviation 46, before `## Plan gate 2026-10-16`.
  Its first line says the numbering continues from 47 (nine numbers, 31–39, were not enough).
- Deviations written: 31 (check, pins + 3 teeth), 32 (question row, refused arm via credentials folder, status `failed`, teeth
  `Release(homes);`), 33 (S5.6.5 red before t2's planner arm), 34 (MustCarryNoFlags), 35 (markerRead not asserted), 36 (rows'
  date/version/record), 37 (RUNTIMES via generated file), 38 (Records table entry + :273 red), 39 (two red messages worded
  differently), 47 (KimiAdapter helper, refusal theory row).

## Checks
- `plan-lifecycle.mjs`: clean, exit 0.
- `CapabilityMatrixTests` (smoke, existing binary built 01:58 UTC, just before the t3 commit): 39 total, 0 failed.
  The binary was not rebuilt; no production file was touched by this task.

## Sources checked against the diff
- `git diff b4e2ea96 -- src_mcp shared src_vs_code`: no change to ConsultantCheck.cs / ConsultCheckScratch.cs / QuestionRowLaunch.cs.
- Coder summaries: notes/O4-t1-code.md … O4-t4-code.md.
- Test names and assertions in AKimiConsultantCheckTests.cs, AKimiQuestionRowTests.cs, ConfinementPlannerTests.cs, CapabilityMatrixTests.cs.

## Left undone / for the owner
- Deviation 33: no teeth mutation was run for S5.6.5 (the contract asked for one). The text says so.
- Deviation 37 claims a picker effect only as far as the diff shows: the admission module reads the generated list and table;
  I did not trace the picker UI.
- Unidentified test failure, NOT recorded in the plan (it is not a build deviation):
  `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow` failed in full-suite runs of t1, t2, t3 and once
  in t4 (a 1 ms git deadline, timing-dependent); it passed on a re-run in t2. Not checked at `b4e2ea96`. Someone should check.
- t4's run 1 had a third failure whose name was not captured; not reproduced in run 2.
- The known failures named in the task (KimiRuntimeTests…, KimiHomeTests…) were not re-run by me.
- Nothing about the live runs (RESULTS §31) and nothing in the parent plan was touched.
