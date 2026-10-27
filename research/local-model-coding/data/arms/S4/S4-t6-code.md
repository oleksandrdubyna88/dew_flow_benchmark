# S4-t6 — E5 plan status, deviations and module notes (docs only)

Commit: `ac7a3abd` on `exp/kimi-e56-sonnet-split-haiku-4`. Three files, added by name. Not pushed.

## Done
- `todo/PLAN_vibe_and_kimi_runtimes_E5.md`
  - Status line: now "E5.1–E5.7 implemented; the live runs (RESULTS §31) and the Opus review of E5.5–E5.6 are owed."
  - One sentence added at the end of the first paragraph, naming deviations 50–62.
  - New section `E5.5–E5.6 as built (Opus, 2026-10-16)` before `### E5.1–E5.2 as built`, items 50–62.
  - DoD line "Every scenario of E5.2–E5.7 is a test that was watched red…" ticked, because every scenario of E5.5 and E5.6 is a test in the tree. Item 61 says so. The line also names format and generator clauses that are NOT all met (item 60). The user may want to untick it.
- `research/module_core.md`: subsection "Kimi rows — E5.6" added at the end of the question-consultant section (before "Feature availability").
- `research/module_tests.md`: subsection "E5.5–E5.6 — the check and the question row on Kimi" appended at the end, one row per test, names taken from the test files.

## Checks
- `node .agents/conventions/tools/low-priority.mjs node .agents/conventions/tools/plan-lifecycle.mjs` → "plan-lifecycle: clean.", exit 0.
- `git diff --stat` before commit: the three files only.
- No code, test, JSON or generated file touched.

## Facts checked against the tree
- Commits since `b4e2ea96`: the five t1–t5 commits. Stat: 11 files, all in the expected areas.
- Coder notes in `/home/jinx/git/coai-exp/notes/S4-t{1..5}-code.md` were read to get the red messages and the test results. The brief says the final answers are not visible; the notes are the record the coders left.

## Differences from the brief
- The brief's module-note exclusion said `module_runners.md` and `module_extension.md` have no E5.5–E5.6 content. The tree disagrees: `src_mcp/runners/Kimi/KimiConsultant.cs` and `src_vs_code/src/runtimeCapabilities.generated.ts` changed. Written in item 62. Those two module notes were NOT updated (out of bounds for this task).
- Item 58 of the brief's red messages: S5.6.1 and S5.6.4 went red with other text than the table (t1). Recorded in item 58.
- Red messages of S5.6.5 differ in wording (FluentAssertions "differ at index 10"). Recorded in item 57.

## Test results reported by the coders (not re-run here)
- MCP: two known failures (`KimiRuntimeTests.ALaunchThatNamesItsHome_…`, `KimiHomeTests.AHomeWhoseDataPathPasses260_…`) in every full run.
- MCP: `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow` failed in the t1 and t3 full runs, not in the t4 and t5 runs. Not in the brief's known list. Cause not found. Item 59.
- Extension: `npm test` failed only `the baseline names no release that was never tagged` (no git tags on this host), per t2.
- `dotnet format --verify-no-changes`: not clean at repo level (IL2026/IL3050 in `src_server` and `src_bugs`). Not verified at base. Item 60.

## Left
- Nothing for t6's scope.
- Owed by the epic (already in the status line): live runs in RESULTS §31; Opus review of E5.5–E5.6.
- Open for whoever owns them: the numstats flake; the format diagnostics; the two known MCP failures.
