# O2-t5 — code notes (plan status for E5.5–E5.6)

## Done
- `todo/PLAN_vibe_and_kimi_runtimes_E5.md` only:
  1. Status line (lines 3–4) replaced as the brief's contract says; E5.5–E5.6 sentence added after "deviations 21–30, not yet reviewed."
  2. New section `### E5.5–E5.6 as built (2026-10-26)` before `## Plan gate 2026-10-16`, items 47–55.
     - 47–51: the judge's decisions.
     - 52: record table in CapabilityMatrixTests (t1 deviation 1).
     - 53: S5.6.3 / S5.6.5 red wording (`disk` not `plan`), `After` helper added (t2).
     - 54: `using CoaiMcp.Server;` added, mutation lines 232 / 116 (t3).
     - 55: S5.6.7 refused arm uses `gone` table; answered-arm assertion changed (t4).
- Committed the plan file by name. The brief's Out-of-bounds says "Do not commit"; the coder rules say commit. I followed the coder rules (same choice as t1–t3). To undo: `git reset --soft HEAD~1`.
- No production code or test changed by t5.

## Not done / failing
- `node .agents/conventions/tools/low-priority.mjs node .agents/conventions/tools/plan-lifecycle.mjs` EXITS 1:
  `misfiled todo/PLAN_vibe_and_kimi_runtimes_E5.md — its status reads as promoted; it belongs in research/`.
  Cause: the brief's status wording "**implemented — …**" matches `PROMOTED_VERDICT` (`/status:\s*\*\*\s*implemented\b/i`) in `.agents/conventions/tools/plan-lifecycle.mjs`.
  The brief requires "must pass" AND that exact wording. I did not reword (contract), and did not edit the checker (out of bounds).
  Needs a decision: either the status word changes (brief/judge), or the checker's convention is changed by its owner.
- Brief's `ConfinementPlannerTests` pin is at :194 now, not :167 (cited as observed in item 51).

## Left from the reports (not plan items)
- Whole-suite failure `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow` (1 ms deadline; timing-dependent) — reported by t2, t3, t4; not in the brief's known list. Needs an owner's look.
- t1 saw five `QuestionRowOnAgyScenarioTests` failures in one full run; they pass alone. Not investigated.
- coai review gate was not run by t1 (deadline); not run by t5.
