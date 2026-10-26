# T4 t5 — code notes (the docs: E5 status, `module_core.md`, `module_tests.md`)

Spec: `vllm/work/T4/t5.md`. Branch `exp/kimi-e56-tasks-4`. Documents only; no code, no test, no build run (none needed).

## Done

1. `todo/PLAN_vibe_and_kimi_runtimes_E5.md`
   - Status line 3: now "E5.1–E5.7 implemented (E5.5–E5.6 on 2026-10-26); not yet promoted — the live runs (J2, RESULTS §31) are owed."
   - Status block: sentence "E5.5–E5.6 implemented 2026-10-26 … deviations 31–39, *E5.5–E5.6 as built*." added after the E5.7 sentence.
   - New section `### E5.5–E5.6 as built (Opus, 2026-10-26)` between E5.4's item 30 and `### E5.1–E5.2 as built`. Items 31–39 (nine items, the maximum).
   - Definition of Done: NOT changed. Nothing is ticked. Reason: the live runs (RESULTS §31), `module_server.md`, `module_runners.md`, `module_extension.md` are not done; the full suite and `dotnet format` are not clean on this host.
2. `research/module_core.md`
   - Table row line 84: `kimi` has an arm since S5 (no flags).
   - New bullet `**Kimi's arm (S5, 2026-10-26).**` after the Vibe arm bullet, before `**Refuse, never redact`.
   - Count bullet: ", twenty-one since S5" (18 + 3 new rows = 21; the generated table has 7 runtimes × 3 = 21 rows).
3. `research/module_tests.md`
   - Five new rows at the end of the Vibe-plan suite table (after the `helpCoverage`/TS row, before "**The flows:**"): AKimiConsultantCheckTests, AKimiQuestionRowTests, CapabilityMatrixTests (Kimi), ConfinementPlannerTests (Kimi), TS `kimi rows admit flagged, web refuses naming its record`.

## Checks

- `node .agents/conventions/tools/low-priority.mjs node .agents/conventions/tools/plan-lifecycle.mjs`: "clean", exit 0.
- `node .agents/conventions/tools/low-priority.mjs node .agents/conventions/tools/pin-check.mjs`: OK, exit 0.

## Where the document (t5.md) and the record disagree (all written in the plan as built, items 33–39)

- S5.6.5 was red on arrival at the `disk` arm, not green. The red came from the planner's default arm, not from `Build`.
- S5.6.7's Refused arm: a re-login never refuses (a fresh one-shot home is written from the current sign-in). The review fix (`fb24643c`) uses a private decorator in the test file, `TamperedAtVersion`. `Service` takes `IProcessLauncher`. No production change.
- S5.5.1 break 1 gives the check's "none was made" refusal before any launch, so the `--cwd` assertion is never reached. S5.5.3 waits 60 s under the same break.
- The S5.5.2/S5.5.3/S5.5.4 "before E5.3 g" reds and the S5.6.7 `Directory.Exists` red were not produced; E5.3 g was already in.
- Build was by Haiku-model coders (commit trailers), not Opus as the story header says. The header keeps the contract's wording; item 39 records the difference.

## Red messages: where they come from

- Every verbatim red in the new section and in `module_tests.md` is copied from `notes/T4-t1-code.md`, `T4-t2-code.md`, `T4-t3-code.md`, `T4-t4-code.md`, or the reviewers' notes `T4-t1-review.md`, `T4-t3-review.md`, `T4-t4-review.md`.
- "not recorded": the S5.6.7 Refused arm's pre-fix status failure (only a paraphrase exists: `answered`, not `failed`), and S5.5.3's break-3 message.
- The S5.6.7 Refused arm's red under break (b), after the fix, is from the t4 reviewer's run and is the same message as the other two arms.

## Left / not done

- DoD not ticked (see above). The live runs are owed.
- Three full-suite failures seen during the epic are NOT verified at `b4e2ea96`: `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow`, `QuestionRowLookupScenarioTests`, `AVibeReviewerRunsThroughAFakeVibeTests`. Item 38 records them as unverified.
- No Windows run was done for this epic (host: Linux, WSL2).
