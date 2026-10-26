# T1-t6 — review notes (the E5 plan records E5.5–E5.6 as built)

Reviewed `git diff 617326f4..HEAD` (commit `f2913e9f`): one file, `todo/PLAN_vibe_and_kimi_runtimes_E5.md`. No code. No build or tests (the task has no commands).

## Findings
1. **Status line** — right as it was. The text is the contract's text, dated 2026-10-26.
2. **New section placement** — right. `### E5.5–E5.6 as built (2026-10-26)` is after `### E5.7 as built` and before `## Plan gate 2026-10-16`. Numbers start at 47.
3. **Item 47** — right. `MustCarryNoFlags` is at `KimiConsultant.cs:64-66` and tests `Flags` and `Leading`. The tests are in `KimiConsultantTests.cs`.
4. **Item 48** — right. The cells match `shared/runtime-capabilities.json:340-388` (none: M-K6, M-K3, M-K4; disk: also M-K9, M-K9 (e); web: none; 2.1.1; 2026-10-13).
5. **Item 49** — right. The caveat text is at `shared/runtime-capabilities.json:11`, `CapabilityMatrix.cs:33`, `capabilityAdmission.ts:60`. The shared sentence is unchanged, as the contract says.
6. **Item 50** — right, and better than the contract's words. The contract says "no credentials file". The code (`KimiLaunch.cs:194`) refuses only when the credentials FOLDER is missing, and the t5 report shows a file removal still ANSWERED. The coder wrote "folder". Kept.
7. **Item 51** — right. The only build error was `PlayedKimi.During` init-only (CS8852); no test was red for a production reason in E5.5.
8. **Open questions line and Definition of Done** — right. The J2 line is added; the live-check item stays unticked.
9. **FIXED — a deviation from the task reports was missing.** The contract asks for an item for each report item that differs from the spec. t1 reported (and the t1 review accepted) a change not in its brief: a new line in the `Records` list of `CapabilityMatrixTests.cs`. The coder left it out. Added item 52 (`src_mcp/tests/CapabilityMatrixTests.cs:158-164`) and changed the status line from "47–51" to "47–52". `plan-lifecycle.mjs`: clean. Commit: see `git log` (docs(kimi): record the Records-list change as deviation 52).
10. **Not recorded, on purpose** — the TypeScript red step (t1; the t1 review observed it), the red text of t2 (`none.Flag` already passed), the t4 bite checks and the S5.5.3 dead-branch fix (a test fix, not a spec difference), the unrelated red `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow` (not the task's; needs an owner).

## Tests
None to run: documentation only. `node .agents/conventions/tools/plan-lifecycle.mjs`: clean. `git status`: only the plan file changed.
