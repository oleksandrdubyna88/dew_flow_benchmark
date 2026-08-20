# PLAN — the bank's coverage: four groups of six have no questions

> Status: **PARTLY IMPLEMENTED, 2026-08-19 — open work remains, so it stays here.** Done: the author's brief
> carries the target's history (§3.2), the panel is three different models (§3.3, §3.7), the mechanical gate and
> the seed-date derivation ship (§3.4b/§3.4c), a reviewer slot can be retired (§3.9), and the first run under the
> fixed contract is measured (§3.10) — the bank now holds **25 accepted questions across all five reading
> groups**, which answers this plan's §1 symptom; the 32 shifted seed dates are corrected and the three
> gate-blocked questions are settled — one repaired, two retired (§3.10). Open: the first measurement RUN,
> `gemini` has still authored nothing, codex is out of quota until 17 September, and the questions of §3.4/§3.5.
> Extracted from
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

### 3.4b DECIDED 2026-08-18: twenty questions rejected, and the contract fixed instead

The gate, run over the whole bank, flagged **22 of 56 questions**. They are not 22 mistakes — they are three,
and two of them were the CONTRACT's rather than any author's:

| Pattern | Count | What it is |
|---|---|---|
| An `AnswerExcludes` term appears in the question's own reference answer | 8 | The author writes the trap as `AnswerExcludes: "AsyncLocal"` and then helpfully explains that this repository *"uses scoped DI, not AsyncLocal"* — so the gold answer contains the forbidden word and fails its own check |
| A required `AnswerContains` term is absent from the reference | 8 | `"source line"` required while the reference says `"one-line window"` |
| A required term already appears in the prompt | 3 | Any on-topic answer contains it, including a wrong one |
| The seed date is shifted | 3 | See below — the author's, and now impossible |

**Policy applied, uniformly: a question whose own reference answer fails its own required expectations cannot
be measured, so it is rejected — regardless of who approved it.** Twenty rejected, including
`gpu-waiter-never-advances-in-queue`, which three reviewers had ACCEPTED. That overturns a recorded judgement
and is stated here rather than done quietly: its required term `never refreshed` is absent from its own
reference, so a subject answering as well as the gold answer would fail it. A question that cannot be passed
measures nothing, and no number of approvals changes that.

Not edited, because there is no editor: the bank has `import|accept|reject|move` and no way to change a stored
question's terms, and its state has no column for a reason. Rejecting is also the cheaper choice — a rejected
candidate is kept as evidence about the source that produced it, which is the stated purpose, and re-authoring
under a fixed contract costs minutes.

**The two durable fixes, which is where the leverage was:**

1. **The shared contract now states the rule the authors were failing** — every required `AnswerContains` term
   must appear verbatim in the reference, no `AnswerExcludes` term may, and no required term may sit in the
   prompt unless the group brief says the identifier belongs there. It also says these are checked mechanically
   before any reviewer sees the question. The rule was never written down; twelve questions died of its absence.
2. **The seed date is no longer the author's to get wrong.** It named a change and dated it a day early, three
   times, after being told verbatim to copy. Now the author names the CHANGE and the pass dates it from the
   repository (`AuthoringPass.Dated`), the same principle as the harness performing retrieval rather than
   trusting a model's account of it. A disagreement is reported as a `fixed` line, because *"this author shifts
   dates"* stays worth knowing once it can no longer damage a question.

Bank after the decision: **16 accepted, 7 proposed, 33 rejected.**

### 3.4c The contract fix worked — and the "both models shift dates" finding was OURS, retracted

Re-authored the three emptied groups under the fixed contract, 2026-08-18 ~20:00 UTC. Claude and Codex wrote
**17 questions** between them, and the vetting pass's first group reported:

```
---- pr-diff
vetted   6 question(s): 0 accepted, 3 rejected, 3 waiting
```

**No `with broken anchors` line at all** — where the same gate had blocked 3 of 3 pr-diff questions an hour
earlier. Writing the rule down removed the class. That half stands.

#### RETRACTED 2026-08-19: the date shift was the bank's arithmetic, not the models'

The seed-date derivation printed this, identically, for two different model families:

```
claude: fixed seed ad9d2cf: the author dated it 2026-08-16, the repository says 2026-08-17
codex:  fixed seed ad9d2cf: the author dated it 2026-08-16, the repository says 2026-08-17
```

which was written up here as *"two families, handed the correct date and told verbatim to copy it, both write
the same wrong one — an instruction was never going to fix this"*. **It was not a finding about models.**

`SeedFile.At` is a `DateTimeOffset`, and a bare `"at": "2026-08-17"` deserialises to midnight in the READING
machine's offset — `2026-08-17T00:00+02:00` here. `QuestionSeed.At` then normalises the instant to UTC:
`2026-08-16T22:00Z`. Replayed through the real types on 2026-08-19:

| the author wrote | stored as | the report said |
|---|---|---|
| `2026-08-17` *(correct)* | `2026-08-16T22:00Z` | "the author dated it **2026-08-16**" |
| `2026-08-16` *(wrong)* | `2026-08-15T22:00Z` | "the author dated it **2026-08-15**" |

The observed line says `2026-08-16`, so **both authors wrote `2026-08-17` — the date the repository says.** They
copied it correctly, and the harness moved it. The same artifact explains the earlier finding recorded on
2026-08-18: the three questions Codex rejected for "dating a day early" were dated correctly by their author and
shifted afterwards by us, so a reviewer launch was spent on our defect and three questions were thrown away for it.

What this costs: two conclusions in this document and in `research/architecture.md` were wrong and are now marked
so, and nothing measured about author quality on this axis survives. What it does not cost: the derivation itself,
which is kept on the argument that never depended on the finding — the author names the CHANGE and the repository
dates it, the same principle as the harness performing retrieval rather than trusting a model's account of it.

Fixed at the boundary, in `QuestionSeed.Written`: the calendar day an author wrote is stamped UTC instead of
converted into it, on both paths that build a seed from a file (`AuthoringPass` and `BankImport`, which carried
the identical hazard on the identical line, unexercised). The one comparison lives in `SeedCheck.Stated`, because
the two spellings of it were how one could be wrong while the other looked right.

**Rows already in the bank are still shifted by a day** — nothing was back-filled, and that is an operator
decision. Audited 2026-08-19, and the split is exact:

| `SeedKind` | rows | time of day | reading |
|---|---|---|---|
| `commit` | 6 | `00:00` | correct — `Dated` overwrote them from the repository |
| `commit` | **3** | `22:00` | **shifted** — `Dated` never fired for these (no lookup, or the sha did not resolve) |
| `member` | **29** | `22:00` | **shifted, every one** |
| `unstated` | 35 | `-infinity` | correct by design |

**32 rows, not 29** — the first count looked only at `member` and missed three `commit` seeds the derivation never
reached. Exactly `22:00` on all of them, which is local midnight at UTC+2: every one was written on this machine
in summer, so a flat `+ interval '2 hours'` is right for all 32 and no per-row reasoning is needed.

**Corrected 2026-08-19** on the operator's decision — `UPDATE ... + interval '2 hours'` over the 32 rows whose
time of day was `22:00`. Every dated row in the bank now reads `00:00`: 9 `commit`, 29 `member`, and the 35
`unstated` untouched at `-infinity`. The affected ids and their prior values were dumped before the statement ran,
so the shift is reversible row by row rather than by re-running arithmetic over a column that no longer matches
the predicate.

Vetting will not surface most of them: `SeedCheck` checks only `commit` seeds, so the 29 `member` rows are never
looked at, and the three `commit` ones are only re-checked while a question is still `Proposed`. The error runs in
the SAFE direction — an earlier date reads as *may recall* rather than falsely certifying a question as *clear* —
so nothing measured so far is wrong because of it, but 32 questions understate how recent their material is. It is
a data edit, one statement, and it waits for the operator.

Two defects of my own, found in the same output and fixed:

- The correction compared INSTANTS, so it printed a "fix" whose two dates were identical when an author had
  written a time of day. Compares days now — through one shared helper.
- A non-zero exit quoted the START of the output, which for these CLIs is banners — so a Gemini failure reported
  nothing but *"True color (24-bit) support not detected"* three times in a row while its real reason sat at the
  end. Reads the tail now, and so does the timeout branch, which had kept the head reading.

**Gemini has still authored nothing**, across three distinct failures now: its own trust gate (fixed), a git
attempt (fixed by handing it the history), and this exit 1 whose reason the head-reading hid. The next run is the
first that will be able to say why.

### 3.6 The one-button path — what is prepared, and what only you can do

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

### 3.7 MEASURED: cross-model review earns its cost, and gemini needs its own trust escape

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
**Done 2026-08-18** — `question_reviews.ModelId`, migration `ReviewMarkRecordsItsModel`. Marks made before the
column stay empty rather than back-filled: which model held a slot in the past is knowable from a session's
memory, not from the database, and writing a recollection into a column would make it look like a record. Any
per-reviewer statistic over the first forty marks must still be cut by time.

### 3.9 A reviewer slot can be retired — the panel had no way out

Found 2026-08-18, fixed 2026-08-19. Three slots were configured, `reviewer-3` bound to `gemini-3.1-flash`, which
failed on every launch. The strict rule promotes only when every eligible reviewer has approved, so **every
question in the bank waited forever on a mark that could never arrive** — 0 accepted across 20 questions, and the
panel could not finish in principle.

There was no way out, and that is the part worth recording. Unbinding the model does not help: a slot with no
model is a *person*, still eligible and still silent. A reviewer row cannot be deleted either, because the marks
it already made hang off it. So one broken slot froze the whole bank, and the design that made a fourth reviewer
"one insert" had made an insert irreversible.

The registry had already solved exactly this for models — disabled, never deleted, so history stays readable. The
same move: `reviewers.Enabled`, `bench questions disable --reviewer <key>` / `enable`, migration
`ReviewerRetirement`. A retired slot is not launched, not waited for, and keeps its marks. `Eligible` drops
retired slots before the self-review rule runs, because a retired slot is not a rule being relaxed — it is a seat
nobody sits in.

> Note on the migration: the scaffolder wrote `defaultValue: false` for the new column, which would have retired
> every existing slot on deploy — the column meant to unfreeze the panel would have emptied it instead. Backfills
> `true`.

### 3.10 MEASURED 2026-08-19: the first run under the fixed contract

Predictions were recorded in §3.4c and §3.9 before anything was launched; this is what was observed.

**The panel finishes.** Fifteen proposed questions in the three re-authored groups:

| Group | Accepted | Rejected | Waiting | Gate blocked |
|---|---|---|---|---|
| pr-diff | 3 | 0 | 0 | 0 |
| semantic-intent | 2 | 0 | 1 | 1 |
| adversarial | 4 | 0 | 5 | 2 |
| **total** | **9** | **0** | **6** | **3** |

Against **0 accepted out of 20** before. The bank now holds **25 accepted across five groups** (code-lookup 9,
bug-root-cause 7, adversarial 4, pr-diff 3, semantic-intent 2), where it held 16 in two — which is this plan's
§1 symptom answered.

**Zero date corrections across fifteen questions.** Not one `fixed seed` line. The strongest evidence is
incidental: reviewer-1 checked a date by hand and approved —

> *"Commit 46b5409 is real and dated 2026-08-17, the file and MinMax member exist at lines 51-64…"*

That same question, under the pre-fix arithmetic, would have printed *"the author dated it 2026-08-16, the
repository says 2026-08-17"* and been written up as another author shifting dates.

**The retirement works and the migration backfilled correctly.** `bench questions reviewers` read all three slots
as `serving` on the live bank — with the scaffolded `defaultValue: false` it would have read three `retired` and
frozen the bank permanently. After `bench questions disable --reviewer reviewer-3`, gemini was neither launched
nor waited for.

**The remaining blockers are not the contract's.** The three gate-blocked questions are the same three as before
the contract fix — they were authored under the OLD contract, which the fix cannot repair retroactively (§3.4).
The six waiting split as three gate-blocked and three waiting on codex, whose **usage limit runs to 17 September**.
By author: `gpt-5.6-terra` 8 accepted / 0 pending, `claude-sonnet-4-6` 1 accepted / 6 pending — every
codex-authored question completed, because claude reviews them and claude answers. Retiring codex would not help:
claude-authored questions would then have no eligible reviewer at all.

**The tail-reading fix paid for itself the first time it was needed.** Codex's refusal read
`ERROR: You've hit your usage limit … try again at Sep 17th` rather than a colour-support banner.

Two things the plan did not anticipate:

- **The bank's container would not start after a WSL restart** — the Aspire Docker network was gone and the
  container held it by ID. Data was safe in the named volume `bench-postgres-data`; recreating the network under
  the same name, force-dropping the stale endpoint and reconnecting with its aliases brought it back. Worth
  knowing before the next restart, because the symptom reads like a lost database.
- **A question accepted on a mark from an earlier pass.** `gpu-gate-re-entrant-hold` was accepted while both slots
  were silent in this run: codex had approved it on 2026-08-18 at 20:03 UTC, and `Promotion.Decide` reads marks
  from the BANK rather than from the run. Correct, documented, and startling the first time it is seen.

#### The three gate-blocked questions, settled

Two different defects wearing one symptom, and only one of them was an anchor typo.

- **`chunk-completion-countdown` — repaired.** The question is sound: its reference answer matches `ChunkLedger`
  in the target word for word, down to *"a piece that was never in the plan is silently ignored"*, which is what
  the code's own comment says. Only the span was wrong — the anchor claimed `ChunkLedger.Landed@33-42`, and the
  method lives at **54–63**; line 43 is a CALL of `Landed` inside `Wrote`, which is why the gate reported it
  "on line(s) 43, 54". Repointed, and re-vetting the group proves it: the `defect` line and the
  `with broken anchors` count are both gone, and the question now waits only on codex.
- **`worker-liveness-win32-exception-return` and `process-runner-read-cancellation-token` — retired.** Not typos.
  They anchor at `Bench.Infrastructure/Process/WorkerLiveness.cs` and `.../ProcessRunner.cs`, which exist in THIS
  repository and not in the target; the author described the harness it was running inside. The target has a
  `ProcessRunner` too, at `src/Rag.Infrastructure/Processes/`, but it is a different class and both reference
  answers are claims about OUR code — repointing them would mean writing two new questions, not fixing two.
  Rejected, as the class the contract's new *"the repository you are describing is the one you are standing in"*
  section now prevents.

There is **no CLI verb that edits a stored expectation** — `import` refuses a duplicate id, and only
accept/reject/move/review exist — so the repair was one `jsonb_set` against the row, with the prior JSON dumped
first. If anchor repairs turn out to be routine rather than rare, that verb is the thing to build.

Test suite through the day: 773 → 863 → 886, 0 failed at each point (the growth is other work landing in the same
tree). The two new seed tests were verified to have teeth by reverting `QuestionSeed.Written` and watching them go
red with the real symptom — *"the question dates it 2026-08-15"* for a seed written `2026-08-16`.


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
- §3.4c: the seed date read the way JSON delivers it — midnight in a non-UTC offset — is the day it was written,
  and a date that really is a day early is still caught (`SeedCheckTests`, `AuthoringPassTests`).
- §3.9: a slot nothing can complete freezes the question until it is retired, and a retired slot is not launched
  (`VettingPassTests`).

## 6. Definition of Done

- [x] Every reading group (1–5) holds questions, or names the reason it cannot in this document — 25 accepted
      across all five as of 2026-08-19 (§3.10).
- [x] `pr-diff` questions carry real seed dates rather than `unstated` — all three accepted ones are `commit`
      seeds dated from the repository.
- [x] At least two DIFFERENT models sit on the reviewer panel, and the report says which — `claude-sonnet-4-6`
      and `gpt-5.6-terra`; `gemini-3.1-flash` is retired, and codex is out of quota until 17 September.
- [ ] The five flagged questions are fixed, retired, or explicitly kept with a stated reason.
- [ ] `research/architecture.md` and `research/PLAN_question_authoring.md` are updated with whatever the retried
      groups measure.
- [x] A reviewer slot that cannot answer can be taken out of the panel without deleting its marks (§3.9).
- [x] A seed date survives the trip from an author's JSON to the bank as the DAY it was written (§3.4c).
- [x] The seed dates already stored a day early are corrected — 32 rows, 2026-08-19 (§3.4c).
