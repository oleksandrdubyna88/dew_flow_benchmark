# SR1-t6 — coder notes (record E5.5 and E5.6 as built)

Commit: `78fd81ad` on `exp/kimi-e56-sonnet-split-haiku-review-1`. Not pushed.
Files (only these three): `todo/PLAN_vibe_and_kimi_runtimes_E5.md`, `research/module_tests.md`, `research/module_core.md`.
No code, test, seed or other document was changed.

## What I did
1. Read the t1–t5 coder notes (`notes/SR1-t1-code.md` … `SR1-t5-code.md`) and `git diff b4e2ea96 --stat` (12 files, +764/−8). Read the kimi rows, `ConfinementPlanner.Kimi`, `KimiConsultant.MustCarryNoFlags` and the three new test files (names only where needed).
2. Plan edits:
   - Status line: E5.5 and E5.6 implemented 2026-10-27; only the live checks of RESULTS §31 and M-K17 remain. The "reviewed by Opus" part is taken from the task document; I could not verify it from the repository.
   - New block `E5.5 and E5.6 as built` with items 50–55 as the document asks, plus items 56–58 for deviations I found (S5.6.5 argv shape, S5.6.7 prompt name, and an unknown failure in the t5 full-suite run).
   - Definition of Done: no box ticked (reason in item 55).
3. Module notes:
   - `module_tests.md`: three rows added to the Kimi table (`AKimiConsultantCheckTests`, `AKimiQuestionRowTests`, `KimiQuestionLaunchTests`) and one `CapabilityMatrixTests (+ kimi)` row.
   - `module_core.md`: `RuntimeCapabilities` row (kimi is the seventh runtime; `none`/`disk` unconfined, `web` unmeasured); `ConfinementPlanner` row (the kimi arm; no flag; `MustCarryNoFlags`); the seed row count updated from 18 to 21 (7 runtimes × 3), because the old figure was no longer true.
4. `plan-lifecycle.mjs` through `low-priority.mjs`: **clean** (`plan-lifecycle: clean.`, exit 0).
5. No code build was run (not needed for documents).

## Points I could not confirm from the repository
- Item 50 says the task list table is in the git history of the branch. I did not check that.
- The status line says "reviewed by Opus". Not checked.

## Deviations from the document
- Item 53 records what the summaries say. Two points differ from the document's own predictions:
  - S5.5.1: the break I took from the t4 summary (snapshot check in `PlaceAsync`) turned test 1 red on the adapter's refusal, not on `--cwd`.
  - S5.6.5: the break that drops `ConsultantLaunches.ForQuestion` stayed green (no tooth), as t3 reported.
- Test host: on this Linux host the test binary is `src_mcp/tests/bin/Debug/net10.0/CoaiMcp.Tests` (no `.exe`), as the t1–t5 coders noted. No test was run by t6.

## Left undone
- Live cells: RESULTS §31 (three-turn consult, one question row per admitted capability, one `--check-consultant`) and M-K17. Not run here (the task was documents only).
- Open failure: `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow` failed in the t5 full-suite run and in isolation. It is not on the known list and I did not check it at `b4e2ea96`. Recorded in plan item 58.
- Definition of Done line "Every scenario of E5.2–E5.7 is a test that was watched red…" left unticked (plan item 55).
