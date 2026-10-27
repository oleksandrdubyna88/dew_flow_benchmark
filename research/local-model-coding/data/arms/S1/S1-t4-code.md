# S1 t4 — code notes (plan record for E5.5–E5.6)

Date: 2026-10-26. Branch: exp/kimi-e56-sonnet-split-haiku-1. Only file changed: `todo/PLAN_vibe_and_kimi_runtimes_E5.md`.

## What I did

1. Read the diffs of the listed files against `b4e2ea96`, and the two new test classes. Checked each line of the split's list against them.
   - All 11 listed files exist in the diff. The three `kimi` rows, the vectors, the planner arm, the refusal, the widened runtime lists and the test names match the document.
   - The generated TypeScript mirror `src_vs_code/src/runtimeCapabilities.generated.ts` changed too. It was not in the document's list. Recorded as item 57.
2. Status block (lines 3–11): first sentence replaced with the wording of the document. Date is 2026-10-26 (`date -u +%F`). New sentence added at the end of the block, with "deviations 50–58" (the document said 50–56; I added 57 and 58). "M-K17 (§30) not run." is kept.
3. New section `### E5.5–E5.6 as built (smaller model, 2026-10-26)` placed after item 46 and before `## Plan gate 2026-10-16`. Items 50–56 as the document gives them; each checked against the diff. Added:
   - 57: the generated TypeScript mirror was regenerated (not in the split's list).
   - 58: `MustCarryNoFlagsOfAPlan` has cyclomatic complexity 5 (1 + `if` + `&&` + two `||`). The plan's rule (line 89) is ≤ 4 per method. Not reshaped; open for the Opus review.
4. Definition of Done: no box ticked. One bullet added after the last item.
5. `node .agents/conventions/tools/low-priority.mjs node .agents/conventions/tools/plan-lifecycle.mjs`: "plan-lifecycle: clean." Exit 0.

## Checks of the claims

- Teeth runs (item 50, item 56) are backed by `S1-t3-code.md` (temporary breaks a–d) and `S1-t2-code.md` (S5.6.5 and S5.6.7 breaks). I did not re-run them; this task runs no tests.
- Item 52: `CapabilityMatrix.RecordOf` exists (`src_mcp/core/QuestionConsult/CapabilityMatrix.cs:116`) and reads the record from `resultRef`.
- Item 56: `ScratchOf` helper and `QuestionRowLaunch.ScratchPrefix` use are in `AKimiQuestionRowTests.cs`.

## Red messages that differed from the document (not written into the plan)

- S5.6.5 teeth (S1-t2): the document's text was `Expected Request.WorkingDirectory to be "<Scratch>", but found …`. The actual FluentAssertions message: `Expected none.Request.WorkingDirectory to be the same string, but they differ at index 31`. Same assertion.
- S5.5.x break c (S1-t3): the document named `VibeHomes.cs` `OneShotHome.Dispose`. The `KimiHome.DisposeAllUnder` call is in `VibeHomes.EmptiedUnderItsLock`. Break made there.

## Test setup choices in the tests (not in the plan; not written there)

- `AKimiQuestionRowTests` builds the shim adapter with `_person.Bin`, not `RowFolder/persons-bin`. With the other path, `KimiEnvironment.Refusal` is not empty (D19).
- `Repo` is `<person folder>\checkout`, so `Hash()` does not see it.

## Conflict to decide

- The task document says "Do not run `git commit`" (Out of bounds). The coder rules say "Commit with a conventional message". I followed the coder rules and committed only the plan file by name. Not pushed. If the document's rule is the one that holds, undo with `git reset --soft HEAD~1`.

## Left

- Nothing of the task is missing.
- `git diff b4e2ea96 --stat` still lists the other tasks' code files. The branch has later commits (ddf09109 and before) than `b4e2ea96`. The working tree change of this task is the plan file only (`git status`).
- Not run: the test suite, the build (no code change). The two known MCP failures and the one extension failure are not relevant to this task.
