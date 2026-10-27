# T1-t6 — code notes (the E5 plan records E5.5–E5.6 as built)

Commit: `f2913e9f` on branch `exp/kimi-e56-tasks-1`. Not pushed. Only `todo/PLAN_vibe_and_kimi_runtimes_E5.md` changed (21 insertions, 1 deletion).
No code, no build, no tests (the task has no commands).

## Done
- Status line (`:3`): `E5.5–E5.6 not started.` replaced with the contract's text, dated 2026-10-26. Deviations named 47–51.
- New section `### E5.5–E5.6 as built (2026-10-26)` after `### E5.7 as built`, before `## Plan gate 2026-10-16`. Items 47–51.
- Open questions: one line `- J2 for E5.5–E5.6: …` added after item 6.
- Definition of Done: no change. The live-check item was already unticked (`[ ]`) and stays so.

## Sources used for the items
- Items 47, 50, 51: the task reports `T1-t3-code.md`, `T1-t5-code.md`, `T1-t4-code.md`.
- Item 48: the contract, checked against `shared/runtime-capabilities.json:340-388`. The cells match the file, including `M-K9` and `M-K9 (e)` (both are in the file, so the list was kept as written).
- Item 49: `shared/runtime-capabilities.json:11`, `:104`, `src_mcp/core/QuestionConsult/CapabilityMatrix.cs:33`, `src_vs_code/src/capabilityAdmission.ts:60` (the grep for "a shell is always there"). The Kimi `note` is at `shared/runtime-capabilities.json:340-388`.
- Line anchors: `src_mcp/runners/Kimi/KimiConsultant.cs:64-66` (`MustCarryNoFlags`) and `src_mcp/runners/Kimi/KimiLaunch.cs:194` (the credentials check, copied from the t5 report).

## Differences from the task document
- The item 49 anchor for the Kimi row's `note` is a range (`:340-388`), not a single line, because the row spans several lines.
- `common.md` says "do not commit"; the task prompt says commit. I followed the prompt, as the t3 report did.
- The worktree is on `exp/kimi-e56-tasks-1` (the prompt's branch), so the commit landed there.
- The task reports' own deviations that the contract does not list (not in items 47–51) are not in the plan: the `Records` list in `CapabilityMatrixTests.cs` (t1, deviation 1), the TypeScript red step that was not observed (t1), the bite checks (t4, t5), and the unidentified second skip in `ConsultantCheckTests` (t4). They are recorded here only.
- t5's note of an unrelated red: `TheRoundsNumstatsCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow` (not in `common.md`, fails in the full MCP run). It is not in the plan. Needs an owner.

## Left
- J2 (the live checks) is not run. RESULTS §31 is not written. Both are recorded in the plan, as the contract asks.
- Item 49's shared caveat sentence is unchanged, as the contract asks. It is an open item for the review.
