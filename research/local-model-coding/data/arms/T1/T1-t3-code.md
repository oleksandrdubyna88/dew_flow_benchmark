# T1-t3 — code notes

Task: `/home/jinx/git/coai-exp/vllm/work/T1/t3.md` (S5.6.5, S5.6.6). Commit `f65ca527` on branch `tmp/T1-t3`.

## Done
- `src_mcp/runners/Kimi/KimiConsultant.cs`: `<remarks>` sentence for a question row; `MustCarryNoFlags(launch)` called
  first after `MustBeLaunchable` in `Build`; placed between `Build` and `ModelOf`. Exact message from the document.
- `src_mcp/tests/KimiConsultantTests.cs`: `using CoaiMcp.Core.QuestionConsult;`; helpers `RowFolder`, `RowTree`,
  `RowScratch`, `RootA`, `RootB`, `Row(...)`; tests `AKimiQuestionLaunch_IsTheShimRequest_InItsFolderOrItsCopy_OnTheRowsHome`
  (S5.6.5) and `APlanWithFlags_IsRefusedForKimi` (S5.6.6).

## Red then green
- Red (before production change): `KimiConsultantTests` 14 total, 1 failed.
  `APlanWithFlags_IsRefusedForKimi` — "Expected a <System.ArgumentException> to be thrown, but no exception was thrown."
  Same as the document.
- S5.6.5 was green before the change, as the document expects. No production change beyond `KimiConsultant.cs`.
- Green (after): `KimiConsultantTests` 14/14 passed, 0 skipped. `ConfinementPlannerTests` 31/31 passed, 0 skipped.
- Solution build: 0 errors, 0 warnings.

## Differences from the document
- Git: the worktree is on `tmp/T1-t3`, not `exp/kimi-e56-tasks-1` as the brief says. I committed on the current branch.
- `common.md` says "do not commit", but the coder brief says commit. I followed the brief.

## Left
- Nothing for t3.
