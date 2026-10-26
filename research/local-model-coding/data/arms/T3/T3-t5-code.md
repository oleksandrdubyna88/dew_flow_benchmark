# T3 / t5 — code notes (the E5 plan: status line and E5.5–E5.6 deviations)

Task: `vllm/work/T3/t5.md` (with `common.md`). Branch `exp/kimi-e56-tasks-3`. Not pushed.
Commit: `659504c6` — only `todo/PLAN_vibe_and_kimi_runtimes_E5.md`.

## What I did
1. Status block (line 3): replaced `E5.5–E5.6 not started.` with the contract text, dated 2026-10-26, deviations 31–43.
2. New section `### E5.5–E5.6 as built (Opus, 2026-10-26)` after `### E5.7 as built`, before `## Plan gate 2026-10-16`. Numbered 31–43, one bullet each:
   - 31–33: the contract's three bullets (`none` unconfined; shared caveat; `MustCarryNoFlags` refuses `Leading`).
   - 34: E5.5 and S5.6.7 skip on Linux (D18). Green not observed on any host: t3 and t4 ran on Linux only.
   - 35–38, 43: deviations from the t2, t1, t4 and t3 summaries (S5.6.5 red before the planner arm; `Records` entry; `38ebb5f3` removed the `Directory.Exists` guard; `OneShotEntries` treats absent folder as empty; helper and `_homes` field).
   - 39: red-message wording differences (t1, t2 notes).
   - 40: suite not green except the two known failures (see below).
   - 41: `dotnet format --verify-no-changes` exits 2 on the solution (analyzer errors in `src_server` / `src_bugs`).
   - 42: the Linux test host has no `.exe`.
3. `node .agents/conventions/tools/low-priority.mjs node .agents/conventions/tools/plan-lifecycle.mjs` → `plan-lifecycle: clean.`, exit 0.

## Points to check
- The contract text says "(Opus)". The coder of this task was Haiku 5.5. I used the contract's text as written.
- Item 37 (`38ebb5f3`): the t4 summary describes the guard as present. The commit at the head of the branch removes it. I wrote the plan from the commit, not from the summary.
- Item 40: the two `QuestionRowOnAgyScenarioTests` failures and the `TheRoundsNumstatsAreCountedTests` failure are not on the known list. None was checked against `b4e2ea96`. Not investigated by me.

## Test results
None run. This task changes docs only. No build or test commands were run.

## Left
- Nothing in the contract left undone.
- The "(Opus)" label, if it should say the coder's real model, needs a change by the person who owns the plan.
