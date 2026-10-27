# SR4 t5 — the E5 plan status, the E5.5–E5.6 as-built section, the module docs (coder notes)

Branch: exp/kimi-e56-sonnet-split-haiku-review-4. Commit: 86c34b3a (not pushed).
Task document: /home/jinx/git/coai-exp/vllm/work/SR4/t5.md

## What I did

1. Baseline: `node .agents/conventions/tools/plan-lifecycle.mjs` → `plan-lifecycle: clean.`
   (output in /home/jinx/git/coai-exp/notes/t5-lifecycle-baseline.txt).
2. Verified every item 50–55 against the tree with grep and reads. Results:
   - 50 (E5.5 tests only): true. `AKimiConsultantCheckTests` exists. The commit `73ef212c`
     adds only that file. CORRECTED in the text: S5.5.2 is a two-arm Theory (five test
     cases for four scenarios). S5.5.3 is NOT a `cancelled` state: `ConsultCheckStates`
     has `checking`, `answered`, `failed`, `unavailable`. The record is `failed` with
     `failureKind` `cancelled`, and the check does not throw.
   - 51 (standings, cells, why header, Runtimes, generated file): true, as written.
   - 52 (`MustCarryNoFlags`, exact message): true, as written. The E5.6 commit changed
     only that method in `KimiConsultant.cs`.
   - 53 (where the scenarios live): true. The `Records` list has the `M-K` entry. The
     vectors test body is unchanged (the diff touches only the `Records` comment and
     list, plus the new tests). Test names checked against the files.
   - 54 (every OS, no skip): true. Both files use `WindowsStandIn` and `KimiSeams.Using`,
     and neither has a skip. Disposal asserts use `person.Hash()`. The teeth are recorded
     in the t3 and t4 notes, not in the plan, so the text now names those notes.
   - 55 (M-K17): true. The `none` and `disk` notes still say WebSearch and FetchURL are
     announced and unmeasured.
3. Plan edits (`todo/PLAN_vibe_and_kimi_runtimes_E5.md`):
   - Status line 3 replaced exactly as the contract gives. Everything else of the line is
     kept, including "M-K17 (§30) not run" and "deviations 21–30, not yet reviewed".
   - No "E5.5 next" text exists in the status block, so no second title edit was needed.
   - New subsection `### E5.5–E5.6 as built (2026-10-27)` before `### E5.7 as built`, with
     items 50–55.
   - Definition-of-Done checkboxes, the ledger line and other sections are untouched.
4. Module documents, one addition each:
   - `research/module_core.md`: the `RuntimeCapabilities` row (Runtimes gains `kimi`, the
     three rows and standings, their cells) and the `ConfinementPlanner` row (the kimi arm).
   - `research/module_runners.md`: a "planned launch" bullet in the KimiConsultant section
     (flag refusal, cwd by plan, no `--session`). I also corrected one stale sentence in
     that section: it said a question row on Kimi "is still refused ... until E5.6". That
     is no longer true, so it now says it is admitted since E5.6.
   - `research/module_tests.md`: one paragraph after the E5.4 section, one line per suite,
     with the teeth stated for the disposal asserts.
5. Lifecycle after edits: `plan-lifecycle: clean.` (notes/t5-lifecycle-after.txt).
6. Commit 86c34b3a: the four files, added by name. Conventional message, Co-Authored-By
   line as the system reminder gives. Nothing pushed.

## Deviations from the document

- **"No commit" versus the brief.** The document's opening line and Out of bounds say
  "No commit". The brief says commit with a conventional message and never push. I
  followed the brief, as t1–t4 did. If "no commit" was the real rule, the commit can be
  undone with `git reset --soft HEAD~1`. Nothing is pushed.
- **Module_runners stale sentence.** Changed one sentence outside the "one addition"
  (see step 4). It was false after E5.6.

## Left undone

- Nothing from the contracts. The live checks (RESULTS §31) and M-K17 are owed, as the
  status line now says. I did not run them, and the document says not to.
- Nothing was built or tested. This task is documentation only.
