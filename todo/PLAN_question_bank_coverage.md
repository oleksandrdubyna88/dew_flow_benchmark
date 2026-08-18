# PLAN — the bank's coverage: four groups of six have no questions

> Status: **plan only, 2026-08-18.** Extracted from
> [../research/PLAN_question_authoring.md](../research/PLAN_question_authoring.md) when that plan was
> implemented: its pipeline works and its throughput is measured, and what remains is not pipeline work but
> **coverage** — three reading groups nothing has authored, one that cannot be authored this way at all, and a
> reviewer panel that is one model wearing three names.
>
> Scope: `prompts/author/*`, the authoring pass's checkout (`ICheckoutProvider` usage), the reviewer bindings
> (`reviewers.ModelKey`, data only), and three questions the bank already holds that carry a defect.
>
> Related docs: [../research/PLAN_question_authoring.md](../research/PLAN_question_authoring.md),
> [PLAN_code_lane.md](PLAN_code_lane.md) (owns group 6), [PLAN_variant_matrix.md](PLAN_variant_matrix.md) §3.3,
> [../research/architecture.md](../research/architecture.md) (*Where questions come from*, *Vetting*).

## 1. The symptom, measured

After the first real batch (2026-08-18) the bank holds **22 questions in two groups** and **zero in four**:

| # | Group | Questions | Accepted | Why it is empty |
|---|---|---|---|---|
| 1 | `code-lookup` | 12 | 9 | — |
| 2 | `semantic-intent` | **0** | 0 | its authoring call burned a 900 s wall on the workspace-trust gate, now fixed and **never retried** |
| 3 | `pr-diff` | **0** | 0 | **blocked**: it needs merge dates, and git history is unreadable inside a `git worktree` |
| 4 | `bug-root-cause` | 10 | 8 | — |
| 5 | `adversarial` | **0** | 0 | same as group 2 — the trust gate, now fixed and never retried |
| 6 | `code-writing` | **0** | 0 | deliberately not authorable here; three gates need a sandbox and a build ([PLAN_code_lane.md](PLAN_code_lane.md)) |

A measurement over two groups measures two groups. The variant matrix multiplies over the question set, so
coverage is the one axis that cannot be compensated for by running more cells.

## 2. What is already true, verified

| Fact | Where |
|---|---|
| The trust gate that emptied groups 2 and 5 is fixed and verified | `src/Bench.Infrastructure/Models/WorkspaceTrust.cs`; the re-run printed both trusted keys and the warning was gone |
| Authoring and vetting both run live against Claude CLI 2.1.216 | `research/PLAN_question_authoring.md` step 6 |
| A mechanical gate stops a launch on a broken anchor or a non-discriminating term | `src/Bench.Domain/Suites/AnchorCheck.cs`, `QuestionSanity.cs` |
| Three reviewer slots exist and all three name the same model | `reviewers.ModelKey` — all `claude-reviewer` → `claude-sonnet-4-6` |
| The panel produced 2 rejections in 57 marks, both mechanically reproducible, and approved one question with the same defect it rejected another for | `research/PLAN_question_authoring.md` step 6 |

## 3. The work

### 3.1 Retry the two groups the trust gate emptied

`semantic-intent` and `adversarial`, ten questions each, now that `WorkspaceTrust` runs before the launch.
Nothing to build: this is a run. It is first because it is the cheapest coverage available and because it is
the only way to learn whether those briefs produce anything good — `semantic-intent` in particular has the
one rule this project cannot yet check mechanically (*name no identifier from the target code*), which
`QuestionSanity` can only partly enforce.

### 3.2 Give the author a tree whose history it can read

`pr-diff` questions are seeded from merged pull requests and their dates; a `git worktree` makes `.git` a
redirect file, which the agent declines to follow, so every seed came back `unstated` — and `unstated` reads
as *may recall*, which is the input to the whole memorisation check.

Options, cheapest first:
1. **Pass the history in the prompt.** The pass already runs `git`; it can put `git log` output for the pinned
   commit into the brief. No trust change, no new checkout mode, and the dates arrive as data the author cannot
   invent.
2. A full clone rather than a worktree for authoring targets — correct history, but a second checkout mode and
   a disk cost per target.
3. Trust the bare repository path so the agent may follow the redirect — already done by `WorkspaceTrust`, and
   it did **not** unblock the history read, so this option is measured and rejected.

Option 1 is the recommendation, and it makes the seed dates verifiable rather than merely present.

### 3.2a The operator's one-third design — 10 per group, three authors, and the panel that follows from it

Decided 2026-08-18: **ten questions per group now**, a third from each of the three CLI models, each question
marked with the model that wrote it, and review by the others. Scaling to a hundred comes later.

Two properties make this better than "all three review everything":

- **The author is out of its own panel by construction.** With three authors at a third each, every question's
  panel is the two models that did not write it. No self-review, no `--allow-self-review`, and each model reviews
  exactly two thirds of the set. **Two launches per question instead of three** — a third cheaper.
- **Authorship is already stored** (`bank_questions.AuthorModel`), so it also becomes measurable whether a
  subject answers its own model's questions better than the others'. With one author that bias is invisible.

**Both halves are built** (2026-08-18): `Promotion.Decide` now decides over the reviewers ELIGIBLE for a
question rather than every configured row, and the pass computes eligibility from resolved model ids. Without it
the strict rule waits forever on a mark the self-review refusal will never allow — proved by breaking it, where
the red read *"reviewer-1 is claude-sonnet-4-6 and would be reviewing its own authorship"* and the question was
never accepted. And `AuthoringPass` now deduplicates against **the bank**, on a member-level key, because the
old key included the line span: one author over two calls had already left
`StoreNaming.KindOf@33-49` beside `@45-49` and `RrfFusion.Fuse@17-28` beside `@27-28`, both pairs in one group,
both invisible. Three authors on one group make that the normal case.

**What is NOT built, and it is an installation rather than code:** neither `codex` nor `gemini` is on this
machine (`which` finds only `claude`), and their argv shapes are marked UNVERIFIED in `CliArgv`. A wrong headless
flag produces an interactive session waiting on a terminal nobody watches, so the first run of each must be one
question with a short wall — the same failure mode that cost two groups a 900-second wall each.

The two groups that already hold Claude-only questions do not need pruning: **the one-third rule is a property of
the SELECTION, not of the bank.** `code-lookup` holds 9 accepted and `bug-root-cause` 8, all Claude; adding three
from each of the other two authors makes a 4/3/3 selection of ten available, and the surplus stays in the bank.

### 3.3 A second real model on the panel

The measured cost of three slots on one model: two rejections in 57 marks, both reproducible by substring
arithmetic, plus one question approved that carried the same defect class the panel rejected elsewhere. A
fourth slot on the same model would add nothing; a second *model* is what makes a disagreement possible.

`CliArgv` already maps `codex exec -` and `gemini -p`, both marked UNVERIFIED in the code. Verifying one of
them is a live test, not a build: bind it to `reviewer-2` and vet a group that already has marks, so the two
panels can be compared on the same questions.

### 3.4 Fix or retire the three gate-blocked questions

`weighted-sum-minmax-equal-scores` (term `weight` occurs inside `WeightedSumFusion` in the prompt),
`token-window-split-overlong-lines` (`source line` absent from its reference), `credential-pool-selection-order`
(`least recently leased` absent from its reference). Each is one edit to a stored row; the first is arguable
and the operator decides whether the term is weak or the gate is over-eager.

### 3.5 Decide about `gpu-waiter-never-advances-in-queue`

`Accepted` by all three slots, and its required term `never refreshed` is absent from its own reference answer
— the defect reviewer-1 rejected another question for. Left accepted deliberately: a machine check must not
overturn a recorded judgement. The operator's call is whether to fix the reference, drop the term, or re-open
the question.

## 4. Build order

1. §3.1 — retry `semantic-intent` and `adversarial` (a run, not a change).
2. §3.4 and §3.5 — clear the five flagged questions, since they are edits to five rows.
3. §3.2 — `git log` into the author's brief, then author `pr-diff`.
4. §3.3 — verify a second CLI agent and put it on `reviewer-2`.

## 5. Test plan

- §3.2: a unit test that the brief carries the history section, and that a seed date the author states is
  preserved rather than normalised to `unstated`.
- §3.3: the live-trait test that exists for `claude`, pointed at the second runtime's reference. It is the
  same test.
- §3.1, §3.4, §3.5 need no new tests — they are runs and data edits — and the summary reports what they did.

## 6. Definition of Done

- [ ] Every reading group (1–5) holds questions, or names the reason it cannot in this document.
- [ ] `pr-diff` questions carry real seed dates rather than `unstated`.
- [ ] At least two DIFFERENT models sit on the reviewer panel, and the report says which.
- [ ] The five flagged questions are fixed, retired, or explicitly kept with a stated reason.
- [ ] `research/architecture.md` and `research/PLAN_question_authoring.md` are updated with whatever the retried
      groups measure.
