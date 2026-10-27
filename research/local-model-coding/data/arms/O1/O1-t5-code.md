# O1-t5 — code notes (the E5 plan: status and deviations for E5.5 and E5.6)

Commit: ea62bb1e on exp/kimi-e56-opus-split-haiku-1. Not pushed. Only `todo/PLAN_vibe_and_kimi_runtimes_E5.md` changed.

## Done
- Status line (`:3`): `E5.5–E5.6 not started.` replaced by `E5.5–E5.6 implemented 2026-10-26; deviations 47–57, *E5.5–E5.6 as built*, not yet reviewed.` Rest of the block unchanged.
- New section `### E5.5–E5.6 as built (Haiku, 2026-10-26)` after `### E5.7 as built` and before `## Plan gate 2026-10-16`.
  - Deviations 47–50 as the contract gives them, with 50 corrected (see below).
  - Deviations 51–57 from the t1–t4 reports (red messages, test-file edits, recorder semantics, the refused arm of S5.6.7, the spelling of `AcpWords.SessionFlag`, the kimi record in `CapabilityMatrixTests`).
  - The live checks (J2, RESULTS §31) are stated as NOT done.
  - An "Open, not explained" paragraph for the two suite failures outside the known list.
- `plan-lifecycle.mjs`: clean, exit 0.

## Differences from the document
1. **Deviation 50 was wrong in the document.** It said S5.6.5 was green before the planner arm. The t2 report says it failed at the disk row's WorkingDirectory (scratch, not copy) until the arm was added. The section records the t2 finding.
2. **Deviation 51 added.** The document expects reds for E5.5; t3 saw S5.5.1–S5.5.4 green at first (no production fix). The contract asks for "any scenario green at first".
3. **Deviation 48 wording.** The contract's "records KimiHome.AfterUnlink" is kept. The t3/t4 finding that the recorder fires on every normal unlink is in 55.
4. The t4 `refused` arm and t3 recorder change are recorded as deviations (55, 56), since the document's steps did not work as written.

## Not done / left
- The live checks (J2, RESULTS §31). Stated in the section.
- Not explained: `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow` (t1, t2) and `AReviewerThatAsksForSourceIsAskedAgainTests` (t3). Neither was checked on the base commit. Both are in the section's open paragraph.
- Process note from t4 (not in the plan): the `refused` arm's work used `git checkout --` on `src_mcp/src/Server/QuestionConsult/QuestionRowLaunch.cs` to undo a mutation. It was clean at the time, but the owner should know.
- A peer message (session "vllm fix") asked that no GPU or vLLM work start while the R9700 is in use. This task ran no GPU work.
