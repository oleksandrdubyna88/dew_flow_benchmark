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

### 3.3 The panel is three models now — DONE, with one tier caveat

Shipped 2026-08-18. The measured cost of the single-model panel was two rejections in 57 marks, both
reproducible by substring arithmetic, plus one question approved that carried the same defect class the panel
had rejected elsewhere. A fourth slot on the same model would have added nothing.

| Slot | Registry row | Model | Probe |
|---|---|---|---|
| `reviewer-1` | `claude-reviewer` | `claude-sonnet-4-6` | 5.5 s |
| `reviewer-2` | `codex-terra` | `gpt-5.6-terra` | 4.7 s |
| `reviewer-3` | `gemini-flash` | `gemini-3.1-flash` | 10.2 s |

**The caveat, recorded rather than hidden.** This account's Gemini entitlement is flash-tier: `gemini-3-pro`
and `gemini-2.5-pro` both answer 404 here. So one third of every group is authored — and one slot of every
panel is judged — by a lighter model than the other two. Operator decision 2026-08-18: proceed on this tier
now, buy proper subscriptions later. That is legitimate because `AuthorModel` is truthful per question, so the
asymmetry is **measurable afterwards** rather than baked in invisibly: "did the flash-authored third produce
easier questions" is a query, not a guess.

#### Upgrading a model when a subscription arrives — three commands, no code

A registry row is never edited: a run names the key it measured under, and rewriting a model id would relabel
questions already authored. So an upgrade is an ADD, a REBIND and a PROBE:

```bash
# 1. what does the CLI actually run now? (the id must be what answered, not what was bought)
echo "hi" | codex exec -            # its header prints `model: …`
echo "hi" | gemini -o json          # its envelope prints stats.models.<id>

# 2. add the row, disable the old one, rebind the slot
bench models add --key gemini-pro --model-id <the id from step 1> --runtime cligemini      --hosting cloud --base-url-ref BENCH_GEMINI_EXE --executable-ref BENCH_GEMINI_EXE --seed 1
bench models disable --key gemini-flash
bench questions bind --reviewer reviewer-3 --model gemini-pro

# 3. prove it answers through our own path before any batch
bench models probe --key gemini-pro
```

Then edit `AUTHORS` in `scripts/bank-thirds.sh` to the new key. Questions already in the bank keep the author
they were written by, which is the point: the bank becomes a record of two tiers, comparable by
`AuthorModel`.

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

## 3.6 The one-button path — what is prepared, and what only you can do

Everything that does not need the two CLIs is done (2026-08-18). What remains for the operator:

```bash
# 1. install and log in to both CLIs, then name their executables
export BENCH_CODEX_EXE="C:/path/to/codex.exe"
export BENCH_GEMINI_EXE="C:/path/to/gemini.exe"
export BENCH_CLAUDE_EXE="C:/Users/strug/.local/bin/claude.exe"

# 2. press the button
bash scripts/bank-thirds.sh
```

The script probes each CLI, authors 4 + 3 + 3 per group across four groups, then vets with the two
non-authoring models per question, and prints the bank. It is **resumable**: authoring skips what a group
already holds from an author, vetting walks only `Proposed`, every mark is written as it is taken — which
matters because the bank's Postgres is a Docker container, Docker's engine is a WSL distro, and any
`wsl --shutdown` takes the database with it.

Prepared and verified:

| Item | State |
|---|---|
| `bench models probe --key <k>` | **new** — asks one CLI a trivial question through the same path authoring uses and writes nothing. Verified live against `claude-author`: 5 s, answered `ready` |
| `codex-author` → `gpt-5-codex`, `gemini-author` → `gemini-3-pro` | registered, references `BENCH_CODEX_EXE` / `BENCH_GEMINI_EXE` |
| Reviewer panel | `reviewer-1` → claude, `reviewer-2` → codex, `reviewer-3` → gemini — **three different models** |
| `scripts/bank-thirds.sh` | committed; its two preflight refusals are verified (an unset variable, and a path with no file there) |
| Eligibility + bank-wide dedup | shipped (§3.2a) |

**One honest gap in the verification.** The probe's purpose is to catch a wrong headless flag, whose symptom
is a hang rather than an error. Trying to simulate that by pointing the codex row at `claude.exe` did **not**
demonstrate it: `claude.exe exec -` answered `ready` in 4.1 s, so the substitution proved only that Claude's
CLI tolerates those arguments. The timeout path is covered by a unit test, not end to end. So the first real
`codex` probe is still the first true test of that argv — which is exactly why it is one question with a
120-second wall and not a batch.

**If a CLI reports a different model id** than the row claims, add a NEW row with the right id and disable the
old one. There is no edit: a run names the key it measured under, and rewriting a model id would relabel
questions already authored.

## 3.7 MEASURED: cross-model review earns its cost, and gemini needs its own trust escape

The first three-author batch ran 2026-08-18 17:16–18:05 UTC. Two findings, and the first answers a question
this plan opened.

**A second real model rejects an order of magnitude more.** On this batch's marks:

| Slot | Model | Approved | Rejected |
|---|---|---|---|
| `reviewer-1` | `claude-sonnet-4-6` | 3 | **0** |
| `reviewer-2` | `gpt-5.6-terra` | 2 | **7** |
| `reviewer-3` | `gemini-3.1-flash` | — | — (blocked, see below) |

For comparison, three slots all on Claude produced **2 rejections in 57 marks**. And codex's reasons are not
substring arithmetic — they are checks no mechanical gate here can perform:

> *"The seed timestamp 2026-08-13 predates the first commit containing QdrantVectorIndex.cs (2026-08-14), so it
> could not match this reference."*

It read the target's **git history** and compared it against the seed date — the one property the memorisation
check rests on, and something Claude never raised in 57 marks. Two more rejections are the same check on
`PassWatch.Beat` and `ChunkBatcher.Build`. So: the panel of one model was not merely correlated, it was
**blind to a whole class of defect**, and the operator's three-model design is measured to be worth its cost.

One codex rejection is a DISAGREEMENT WITH THE BRIEF rather than a defect: *"The prompt explicitly names
ResolveInside, the identifier it is supposed to test retrieval for in the code-lookup group."* The
`code-lookup` brief says questions there are *"findable by NAME or by an obvious identifier"* — the deliberate
control group. Either the brief or the reviewer should change; that is an operator decision, not a bug.

**Gemini authored nothing and reviewed nothing**, in all four groups: exit 55, *"Gemini CLI is not running in a
trusted directory."* It has its OWN trust gate, and `WorkspaceTrust` writes `~/.claude.json` — a different
CLI's file — so it cannot help. Fixed by adding `--skip-trust` to its argv (verified live: exit 0), which is
the right answer for the same reason pre-trusting Claude is: the tree is one this harness created, at a commit
it pinned, from a repository the operator named. **Re-authoring and re-vetting gemini's third is outstanding.**

**And the mechanical gate paid for itself again**: 15 questions blocked across four groups, **45 launches not
spent**. Two of those blocks are a class worth naming — anchors pointing at `Bench.Infrastructure/...` and
`Bench.ServiceDefaults/...`, files of THIS repository rather than the target's. An author invented anchors from
the wrong repository, and nothing but the gate would have caught it before three reviewers were paid to.

### 3.8 A defect this batch exposed: rebinding a slot relabels history

`question_reviews` stores the reviewer **slot**, not the model that answered. That was defensible while a slot
meant one thing forever, and this batch broke it: `reviewer-2` was `claude-sonnet-4-6` for 33 marks and is
`gpt-5.6-terra` now, so "reviewer-2 rejected 7" needed a timestamp filter to attribute honestly. The claim in
`research/architecture.md` that a mark's provenance is its slot is therefore **insufficient**.

Fix: store the resolved model id on the mark. One column, written by the vetting pass, which already holds it.
Until then, any per-reviewer statistic over this bank must be cut by time, and that is a footnote nobody will
remember.

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
