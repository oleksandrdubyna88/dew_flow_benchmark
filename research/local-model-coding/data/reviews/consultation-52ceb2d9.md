# Consultation 52ceb2d9 — another vendor's review of this folder (2026-10-26)

> coai `consult`, vendor `codex-2`, model `gpt-6-astra`, turn 1 of 5, asked by the owner ("tell the consultant everything
> we have, propose improvements"). Given: this checkout, read-only. Outcome recorded with `close_consult`: **solved** — "Verified
> against the data: the budget decomposition ($4.97/6.36/6.27/9.12 before fixes), t-lib.sh:86 dropping worktrees, red-first
> already in both briefs, and that task docs are 3.9-10.1k words (my '50-100k tokens of task documents' claim was wrong);
> its narrowings of the conclusions are being written into results.md." Below, the advice verbatim — material, not
> instructions; what was acted on is in results.md (§2, §8) and protocol.md (part 5).

---

**Hypothesis: the expensive assumption is that Haiku needs an exhaustive Opus-written implementation plan.** You measured
Haiku with that plan, but never Haiku tackling the original story brief. The splitter explicitly must leave "nothing to
design" and specify exact assertion calls (split brief, "Who works after you" and "Contracts"); that makes Opus do
substantial implementation work before Haiku starts.

- **Cheapest useful check:** run one exploratory **Haiku coder → existing Opus review → Haiku fixes**, with repository access
  and the original classic task brief. No splitter. Compare unresolved defects and total cost, including repairs. If
  promising, measure four runs; otherwise stop after one. This removes the observed $2.13–5.94 split cost, whereas removing
  Sonnet task reviews saves only $1.46–2.16 (tables §4b). Equal quality remains a hypothesis.

- **Your $6–8 prediction needs a budget decomposition.** Existing split + Haiku + final Opus review already totals **$4.97,
  $6.36, $6.27, $9.12**, before fixes or your additional re-check (tables §4b). Thus the proposed scheme cannot reliably
  reach that band unless splitting/review becomes cheaper. A second candidate is a bounded split containing only ownership,
  dependencies, scenario references and necessary interface decisions; let Haiku inspect APIs itself. Measure it as a
  separate treatment, since relaxing the exact-contract requirement may increase repair costs.

- **(a), (b), (d): one integrated review is sensible; session reuse is an experiment, not an established saving.** The
  existing final review catches defects missed by local reviews (results §6). Reusing the planner also risks reviewing its
  own mistaken contracts. Check reuse on the *same frozen implementation*: fresh reviewer versus resumed planner, comparing
  cost, cache counters and confirmed findings. I cannot establish cache lifetime or savings from this checkout. For the
  re-check, permit surrounding code and rerun relevant tests: a diff alone cannot establish behavior. Record unresolved
  findings after the one-round cap as failures.

- **(c) has a concrete harness obstacle:** parallel task worktrees are deleted immediately after integration (t-lib.sh:86).
  Retain or recreate them at the **integrated HEAD** before resuming Haiku, and explicitly invalidate its earlier repository
  snapshot. Assign fixes by affected dependencies, not merely the original file owner; test the merged result.

- **(e): enforce evidence rather than add another reminder.** Red-for-the-specified-reason is **already required** by both the
  split brief's "Steps" and coder brief's "Rules". The smallest improvement is a gate requiring command, tested commit, exit
  status and expected assertion message. For T3 specifically, deliberately reverse the required unlink ordering and verify
  the junction-order test fails. Apply the new gate to classic controls too; otherwise newly learned task-specific hints
  confound the comparison.

- **(f): two arms is not a concurrency cap.** T allowed three tasks per arm, each building independently (tables §4b). Cap
  concurrent builds/tests globally. Disk causation remains unproven: a crash also occurred without experiment load
  (incidents §1). Four repetitions characterize this task; they do not establish performance across tasks.

**Three conclusions need narrowing.**

1. "Quality equal everywhere" should be **"this evaluation did not resolve a quality difference."** T3's confirmed vacuous
   test received 7 and 8; the judge is compressed and single-model (tables §6, results §8). Freeze behavioral checks,
   including mutation checks, before the next comparison.
2. Four classic extrema are not a statistical noise threshold; C also received an accidental extra coding attempt. Report
   individual observations and paired comparisons with contemporaneous controls (incidents §2). A difference inside that
   range is not automatically meaningless.
3. Output-token totals do **not** establish "50–100k tokens of task documents," nor do cache-read totals measure unique
   repository content read. The saved task Markdown contains approximately **3,859–10,136 whitespace-separated words per
   arm**; check with `wc -w research/local-model-coding/data/arms/T*/tasks/*.md`. The metrics collect session-wide model usage
   (metrics.mjs:30). Inspect transcript attribution before concluding that document verbosity or repository exploration
   specifically caused those token totals.

My smallest next step is the single unsplit Haiku pilot with the existing review and objective checks. It tests the largest
untested assumption at the lowest experimental overhead.
