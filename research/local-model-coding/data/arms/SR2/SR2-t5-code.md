# SR2-t5 — code notes (record E5.5 and E5.6 as built)

Commit: 6c5a7200 — three files by name: todo/PLAN_vibe_and_kimi_runtimes_E5.md, research/module_core.md, research/module_tests.md. No code, no test.

## What was done
- Plan status block: now "E5.1–E5.7 implemented; the live checks (J2, RESULTS §31) are owed", plus the sentence that E5.5 and E5.6 landed 2026-10-27 (deviations 50–56).
- Plan: new section "### E5.5–E5.6 as built (2026-10-27)", items 50–56, before "## Plan gate 2026-10-16". Each item checked against the tree and the t1–t4 notes.
- Plan DoD: no box ticked. The "every scenario … watched red" line is not shown true for E5.2–E5.7 here.
- module_core.md: "### Kimi rows in the capability table (2026-10-27, E5.6 …)" after the question-consultant-confinement section.
- module_tests.md: "### The Kimi check and the Kimi question row — E5.5–E5.6 … (2026-10-27)" appended at the end of the file (the E5.4 section was the last one).
- plan-lifecycle.mjs: "clean", exit 0 (run after the last edit).

## Decisions and deviations from the document
- Commit: the document's "Out of bounds" says "Do not commit"; the coder rules say commit. I followed the coder rules and committed the three files by name. Revert with `git reset --soft HEAD~1` if the document is what counts. Not pushed.
- Item 50 teeth: the document says "3 tests red" for the S5.5.1 break. S5.5.1 is a single test; the t3 record counts three red for that break and does not name them. Written as recorded; not re-run.
- Items 53 and 56: the recorder assertion (S5.6.7) was not seen red: "not recorded".
- Item 56: the red for the web sentence naming the RESULTS record was not recorded.
- Contract sentence "deviations 50–5N" written as "50–56" (seven items).
- The contract's "Opus-split, built on a smaller model" wording kept as given.

## Not checked
- No teeth or red re-run in this task (documentation only). Reds are copied from the t1–t4 notes.
- The reds of E5.2–E5.4 were not re-checked, so the DoD line stays unticked.
- The RESULTS §31 live checks are not written (out of bounds).

## Left undone
- Tick the DoD line "Every scenario of E5.2–E5.7 …" only after the E5.2–E5.4 reds are recorded.
- Whole-suite failures named in item 55 (Numstats, and the two full-run-only tests) need an owner.
