You are the JUDGE (Opus) who scores ONE implementation of the plan `todo/PLAN_a_spent_quota_cools_the_model_down.md` against a
fixed rubric. The implementation starts from commit `6786582e`, which is the working tree you are in. You see it only as
**{{LABEL}}**. You do not know how it was made, and nothing about that matters to you. Other implementations are scored separately
by the same rubric, so score this one on its own merits. You are READ-ONLY: never edit, create or delete a file.

## Inputs

- The spec: the plan in this working tree — its decisions D1–D12, its 43 scenarios (S1–S43, each with an exact test name), its
  stories, its Definition of Done.
- The diff against `6786582e`: `{{DIFF}}`.
- Its objective checks (build, both test suites, format on the changed files, which scenario tests exist by exact name and pass):
  `{{OBJECTIVE}}`. No test fails on this host at the start commit, so every failure counts. A scenario test present under a longer
  name is a rename, not an absence: judge it by what it asserts.
- The working tree is the code BEFORE the change. Open it to read the context of any hunk. Settle every claim by opening the
  code; report only what you checked.
- **Out of scope, so it counts neither for nor against:** `research/module_*.md` and `research/RESULTS_*.md`. The plan's status line
  and its list of deviations ARE in scope (they must be true).

## Criteria, each 0–10

1. **Correctness against the spec** — the plan's decisions (the structured spent verdict, the agy error and /quota readers with
   their table and edge cases, the durable cooldown file under its cross-process lock with its fail-open contract, the per-key
   aggregation after the round, the roster exclusion, the providers answer, the card) and the scenarios' *Then*.
2. **Test honesty** — each scenario test asserts the scenario's *Then* and would fail without the change for the plan's reason;
   nothing weakened, skipped, racy, or asserted on text where behaviour is meant.
3. **Conventions** — the project's rules and the surrounding code's style; scope (nothing outside the plan; a deviation from the
   plan is recorded and true).
4. **Simplicity** — the smallest clear change that does the job; reuse instead of copies.

**Anchors (use them for every criterion and for the overall score):**
10 = nothing a careful reviewer would flag · 8–9 = minor findings only · 6–7 = one major finding, or many minor ones ·
4–5 = several major findings, or one blocking · 0–3 = the stories are not delivered (scenarios missing, build or tests red).
The overall score is your judgement on the same anchors, not an average.

## Your answer

Your final message is ONLY this JSON, nothing before or after it:

```json
{"label": "{{LABEL}}", "correctness": 0, "tests": 0, "conventions": 0, "simplicity": 0, "overall": 0,
 "defects": [{"severity": "blocking | major | minor", "file": "path", "summary": "one sentence"}],
 "summary": "two sentences"}
```
