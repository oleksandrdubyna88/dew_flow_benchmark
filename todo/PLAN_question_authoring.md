# PLAN — question authoring: three CLI agents write the bank, three review it

> Status: **steps 1–5 of §4 IMPLEMENTED (1–4 on 2026-08-17, 5 on 2026-08-18); step 6 — the throughput number —
> open.** `bench questions author` drives the Claude CLI inside the target's checkout and stored **two
> questions** for `code-lookup` against `dew_flow_rag_qln@64865c68`, both anchored at members the agent verified
> in the tree (`StoreNaming.KindOf` 45–49, `RrfFusion.Fuse` 27–28). `bench questions vet` then asks every bound
> reviewer slot for a verdict and moves a question's state only under the strict rule.
>
> **Deviations, step 5:**
> - **A reviewer slot names its model as DATA** (`reviewers.ModelKey`, migration `ReviewerModelBinding`, verb
>   `bench questions bind`). The plan said "reviewers are rows" and the rows named nobody — so the self-review
>   rule, which compares a reviewer's model against the question's author model, had nothing to compare. Found
>   by reading the schema on 2026-08-18, a day after seeding three slots and reporting them ready.
> - **The comparison is on the resolved MODEL ID**, never the slot key or the registry key. Two registry rows
>   both resolving to `claude-sonnet-4-6` are one opinion, and comparing keys would call that pairing clean.
> - **`--allow-self-review` prints what it costs**, and the run's report carries the sentence, so a batch marked
>   this way cannot be quoted without it. The escape hatch exists because the operator's decision is three
>   Claude slots against a Claude author while one CLI author is verified; a silent hatch is one nobody
>   remembers taking.
> - **The reviewer is shown the question, not its provenance** (`BankExport.ForReview` omits `authorModel`,
>   `state`, other marks). A mark that knows who wrote the question is partly about the author. The seed is
>   shown, because the reviewer is asked whether its date looks invented.
> - **The verdict has its own wire shape.** Reusing the import's `ReviewFile` was tempting and wrong: its
>   verdict defaults to `Approved`, so an agent answer that lost the field would silently approve everything.
>   Proved by breaking it — with the default restored, `An_answer_with_NO_verdict_field_is_not_an_approval` went
>   red with *"Skipped … the collection is empty"*, i.e. the note-only answer had been taken as an approval.
> - **No threshold knob.** `Promotion.Decide` accepts only on unanimity of every configured reviewer row,
>   rejects on any single rejection, waits otherwise, and refuses to promote anything when the table is EMPTY —
>   "every configured reviewer approved" is vacuously true of none.
> - **The prose-before-JSON extractor is now shared** (`AgentJson`), parameterised by the bracket pair, because
>   the reviewer answers an object where the author answers an array. A second copy of that lesson would have
>   been a second chance to lose it.
> - **The sixth group is now a row.** `data/bank-seed.json` seeds all six groups and the three slots, and
>   `BankSeedTests` holds the count — `code-writing` had never been created, so the bank had five groups while
>   the design has six. The seed file names no reviewer model on purpose: binding one is a local decision with a
>   stated cost, and a committed default would read as this project's recommendation.
>
> **The first real batch ran on 2026-08-18 and step 6 is NOT answered by it.** Four groups × 10 questions
> against `dew_flow_rag_qln@64865c68`: `code-lookup` **10** (6 min), `bug-root-cause` **10** (11 min),
> `semantic-intent` **0**, `adversarial` **0** — the last two burned their whole 900-second wall each. So the bank
> holds 22 questions, of which 2 are `Accepted` (the pair the vetting pass marked) and 20 `Proposed`.
>
> Two blockers, and only the first is ours:
> - **An untrusted checkout waits for a dialog nothing can answer** — the cause of both empty groups, now FIXED:
>   `WorkspaceTrust` pre-trusts the tree (both the worktree and the bare repository its `.git` names) before any
>   launch, scoped to the benchmark's own checkout root, writing exactly one boolean with a staged file and a
>   backup. **Verified**: the re-run of `semantic-intent` printed the two trusted keys and the workspace warning
>   was **gone** from its output (`grep -c "not been trusted"` → 0), against a live config that kept all 51 of its
>   top-level keys and had one boolean flipped in one ten-field entry.
> - **The org's monthly spend limit** — what the re-run hit instead, at 10 minutes in: *"You've hit your org's
>   monthly spend limit · run /usage-credits to ask your admin for a higher limit"*, exit 1, carried into the
>   rejection as a value. **External, and it blocks every further authoring or vetting launch.** Step 6's number
>   cannot be produced until it is raised.
>
> **Step 6's second question is partly answered, and by reading rather than by launching.** The plan asks
> whether a question's properties can be checked mechanically or need a person every time. Measured over the 20
> proposed questions while the spend limit blocked all launches:
> - **Anchor resolution is fully mechanical, and it is what the reviewers spent most of their notes on.**
>   Checking that each expectation's file exists at the pinned commit and that the member's name falls inside the
>   claimed line span: **20 of 20 correct**. All three live reviewer notes led with exactly this check, so a
>   mechanical pre-gate takes the most-cited half of a review off the launch budget entirely — **now built**
>   (`AnchorCheck`, run inside `VettingPass` before any launch; a failing question reports where the name
>   actually is and stays `Proposed`, because a broken anchor is a defect to fix rather than a verdict). Its own
>   proof is the launch counter: a question with an unresolvable anchor costs **0** launches against 3, and the
>   report says how many it did not spend. Building it turned nine of the existing vetting tests red at once —
>   their fixture pointed the pass at `Path.GetTempPath()`, so their ground truth had always been unresolvable
>   and nothing had ever noticed.
> - **"The prompt must not name the identifier" is mechanical only where a brief forbids it.** Seven
>   `code-lookup` prompts name their member outright — and that is the brief working as written, not a defect:
>   `code-lookup` is *"findable by NAME or by an obvious identifier"*, the deliberate control group. The rule
>   belongs to `semantic-intent`, which produced nothing in this batch, so it is still unmeasured.
> - **The panel's own rejections were both substring arithmetic, so they are mechanical too.** Its only two
>   rejections read: *"the Required AnswerContains term 'single line' does not appear in the reference answer …
>   a correct answer modeled on the gold reference would fail this literal substring check"* and *"the term
>   'branch' appears verbatim and repeatedly in the prompt … so any on-topic answer — including a wrong one — is
>   guaranteed to contain it, making it a non-discriminating term."* `QuestionSanity` now checks exactly that,
>   with `OrdinalIgnoreCase` to match `AnswerScoring`, and it **reproduces both rejections for free** — six
>   launches' worth. Run over the whole bank it flags 5 of 22 questions, and on the ten unvetted `code-lookup`
>   candidates it stopped **9 of 30 launches**.
> - **And it contradicts the panel on one question the panel APPROVED.**
>   `gpu-waiter-never-advances-in-queue` is `Accepted` by all three slots, and its required term
>   `'never refreshed'` is absent from its reference answer — the same defect class reviewer-1 used to reject
>   another question minutes earlier. Three instances of one model are not merely correlated: they applied a rule
>   one of them had articulated to one question and not to the next. Left `Accepted` deliberately — a machine
>   check must not silently overturn a recorded judgement — and reported here because it is the sharpest evidence
>   yet for what the one-model panel costs.
> - **One flag is arguable, and the gate errs toward refusing.** `weighted-sum-minmax-equal-scores` is flagged
>   because its required term `weight` occurs inside the type name `WeightedSumFusion` in the prompt. By the
>   panel's own reasoning that IS non-discriminating; whether it should be is judgement. A flagged question stays
>   `Proposed` rather than being rejected, so the cost of erring is bounded to an operator's glance.
> - **The looser version of that check is unusable.** Decomposing a member name into words and looking for any of
>   them fired on 17 of 22 questions: `gpu` in a GPU bug report, `min`/`max` in a question about min-max
>   normalisation, `credential` in one about a credential pool. A gate with that false-positive rate would refuse
>   correct work, which is worse than no gate.
>
> **Four defects the live batch found, in order — none of them visible from a stub:**
> 1. **The agent was launched in the wrong directory.** It refused to write anything, saying it needed read
>    access to the repository at that commit — the correct answer, and a defect in how it was called. Now the
>    pass checks the target out (reusing `ICheckoutProvider`) and launches the agent in that worktree.
> 2. **The launcher merges stderr into stdout.** Right for git, where "what did it print before it failed" does
>    not care which pipe carried it; wrong for an agent whose stdout is the PAYLOAD. The Claude CLI prints a
>    workspace-trust warning beside its answer, so every merged reading began with prose and the parser refused
>    it. `ProcessResult` now carries stdout separately and the agent runtime reads that.
> 3. **A rejection said nothing about WHAT was answered.** The next edit to a prompt is made from exactly that
>    text, so a parse failure now carries a 200-character sample. Every subsequent fix came from reading it.
> 4. **First-bracket-to-last-bracket is not a way to find JSON.** The prose around one answer contained
>    `int[] SourceLine`, so the slice began at a C# array type. Replaced with a balanced scan that only accepts a
>    slice which PARSES as a non-empty array.
>
> **Two environment facts that are NOT fixed**, and the first one blocks a whole group:
> - **Git history is unreadable inside the worktree.** `git worktree` makes `.git` a redirect file, which the
>   agent treats as untrusted and declines to follow — so seed dates came back `unstated`. The `pr-diff` group
>   depends entirely on merge dates, so it cannot be authored this way at all until the author gets a tree whose
>   history it can read.
> - **Prose before JSON is the agent's instinct**, not a prompt defect that one more sentence fixes: it wrote a
>   preface twice after being told twice not to. The extraction handles it and the preface is reported as a
>   note — which is how finding (1) surfaced. The durable fix is probably a FILE handoff (the agent writes
>   `questions.json` into the worktree and the pass reads it), which is how a coding agent naturally works.
>
> Steps 3–4 as designed: Step 3: `prompts/author` and
> `prompts/review`, five reading groups each, shared contract plus per-group brief, hashed. Step 4:
> `AuthoringPass` drives an agent, parses its answer as the shape `bench questions import` already reads, and
> admits it through `QuestionCandidate.Propose` + `Dedup` — no second format and no second admission rule. The
> CLI verb and the first live batch are what remain of step 4; steps 5 (vetting) and 6 (the first real batch's
> numbers) are open.
>
> Deviations, steps 3–4:
> - **Shared contract plus per-group brief**, not one file per group carrying a copy of the JSON shape and the
>   seed rules. Five copies are five things to keep true. The hash covers BOTH, so editing the shared contract
>   changes every group's identity — correct, because the prompt did change.
> - **The author answers as `BankQuestionFile`**, the import's own shape, rather than a shape of its own or
>   `QuestionFile` (which carries no seed, and the seed is the input to the whole memorisation check). An
>   authored batch is therefore literally an importable file.
> - **An unfilled placeholder is refused.** A prompt sent with a literal `{{commit}}` in it does not fail: the
>   agent answers anyway, plausibly, about no particular commit, and its questions look exactly like correct
>   ones.
> - **A code fence is unwrapped and nothing else is.** Agents wrap JSON in a fence often enough that refusing
>   it would reject good work over a habit, while any further repair would start editing the questions.
> - **Two defects found by the first pass, both stored-data bugs.** (1) A seed date written as `2026-05-14` —
>   no zone — deserialises to the reading machine's offset, and Postgres accepts only UTC in a `timestamptz`
>   parameter, so the insert failed. Fixed in `QuestionSeed` itself so every path is correct at once; the bank
>   IMPORT had the same hazard on the same line, unfixed, because nothing had yet imported a file whose seed
>   omitted a zone. (2) The bank reported **"the question id is already in the bank" for ANY write failure**,
>   so that offset error read as a duplicate id and sent the reader hunting a row that did not exist. It now
>   appends the store's own innermost sentence.
>
> Steps 1 and 2 (2026-08-17, earlier): The one launcher grew stdin
> (`ProcessRunner.RunAsync` with an input overload — 200 KB verified through `git hash-object --stdin`, which
> also proves the pipe is CLOSED, since a CLI reading to end-of-input would otherwise hang), and
> `ICliAgentRuntime` + `CliAgentRuntime` can ask a CLI agent one question and read its answer, with every
> failure a recorded value. **Verified against the real Claude CLI 2.1.216**, not only stubbed: a prompt on
> stdin comes back answered, a prompt far larger than an argument list still arrives, and a one-second wall
> fires and kills the child rather than hanging the batch.
>
> Deviations, steps 1–2:
> - **`CliArgv` is a switch over the runtime kind**, not a configured flag string. A wrong flag produces an
>   interactive session waiting on a terminal nobody watches, and the symptom is a timeout rather than a
>   message; a switch fails at a compiler error when a kind is added. Only `claude -p` is verified —
>   `codex exec -` and `gemini -p` are written from documented usage and marked UNVERIFIED in the code, which
>   is where somebody would otherwise trust them.
> - **An agent that exits ZERO and prints nothing is a REFUSAL.** The failure mode that looks like success: an
>   empty answer stored as a candidate would be a question nobody wrote, and it would pass every admission rule
>   that checks shape rather than content.
> - **Stdin is redirected only when there is something to write.** A child that reads stdin and finds an open
>   empty pipe waits forever, and the timeout would then report a hang this launcher caused.
> - **`ProcessRunner` grew an overload rather than gaining a sibling.** An adapter that started its own process
>   because the launcher could not take input is how this family got a duplicated launcher the first time.
>
> Scope: `Bench.Domain/Authoring` (extensions
> only), `Bench.Application` (the authoring and vetting use cases + one new port), `Bench.Infrastructure`
> (a CLI agent adapter over the existing `ProcessRunner`), `hosts/Cli` (two verbs), and a new `prompts/`
> catalog. No new tables: the bank's schema was built to this shape and phase 2 adds VERBS.
>
> Related docs: [PLAN_variant_matrix.md](PLAN_variant_matrix.md) §3.3 (which names this as *"phase 2, a
> follow-up plan, not this one"* — this is that plan), [PLAN_rag_bench_repo.md](PLAN_rag_bench_repo.md)
> (the authoring rules this obeys), [PLAN_code_lane.md](PLAN_code_lane.md) (group 6, whose authoring has
> three extra gates and is deliberately NOT in scope here),
> [PLAN_tool_benchmark.md](PLAN_tool_benchmark.md) §5 step 11 (`CliAgentRuntime` — the boundary in §2a),
> [../research/architecture.md](../research/architecture.md).

## 1. The goal, before any solution

The bank holds questions, reviews them, freezes selections from them, and **nothing writes any**.
Candidates arrive by `bench questions import` from a hand-authored file, which means the only question set
this benchmark can measure is one a person typed.

That is the stated bottleneck of the whole project, in the founding plan's own words: *"Running is cheap and
authoring is not: a machine gets through a thousand questions overnight, a person writes one good one in
half an hour."* Every axis built so far — variants, subjects, lanes, repeats — multiplies over a question
set. With six groups at ~100 questions each as the target, the set is the one factor nothing else can
compensate for.

**The symptom, measured today (2026-08-17).** The first live retrieval comparison ran on **two** questions,
and both of their anchors were taken from the engine's own returned list, because that is the only way to
author an anchor without reading the target repository. Anchor recall was therefore 1.0 by construction. The
instrument works; it has nothing honest to measure. A comparison over questions authored from the engine's
output measures the person who authored them.

**What this plan does NOT try to fix.** Question QUALITY is not a thing a pipeline can assert. What a
pipeline can do is produce volume that a review step then filters, and make the filtering cheap enough to
happen at all — which is exactly the shape the bank already has (`Proposed → Accepted | Rejected`, one mark
per reviewer per question).

## 2. What exists today, verified

| Fact | Where |
|---|---|
| Admission rules for a candidate: a non-human source must name its author model, and a question with no retrieval expectation is refused | `src/Bench.Domain/Authoring/QuestionCandidate.cs:75-92` |
| `Accept` / `Reject`, and a rejection without a reason is refused | `src/Bench.Domain/Authoring/QuestionCandidate.cs:96-102` |
| Deduplication by collision key across sources | `src/Bench.Domain/Authoring/AuthoringRules.cs:22-36` |
| Memorisation risk from the SEED against a model's cutoff | `src/Bench.Domain/Authoring/AuthoringRules.cs:64-79` |
| `AuthoringBatch.Promote` — candidates to a frozen suite | `src/Bench.Domain/Authoring/AuthoringRules.cs:89` |
| The bank's five tables + `run_questions`, one mark per reviewer per question held by a unique index | `src/Bench.Infrastructure/Persistence/BenchDbContext.cs` (`Bank`), migration `QuestionBank` |
| Six groups named as keys | `PLAN_variant_matrix.md` §3.3: `code-lookup`, `semantic-intent`, `pr-diff`, `bug-root-cause`, `adversarial`, `code-writing` |
| Reviewers are DATA, not an enum — a fourth reviewer is one row | `reviewers` table; `bench questions review --reviewer <key>` |
| `bench questions import|list|groups|review|accept|reject|move` | `hosts/Cli/QuestionsCommand.cs:26-30` |
| One sanctioned process launcher: exe + argv, never a shell string, with a timeout and a typed `ProcessAttempt` (`Completed`/`TimedOut`/`NotFound`) | `src/Bench.Infrastructure/Process/ProcessRunner.cs:40-60` |
| The registry can NAME a CLI model — `ModelRuntimeKind.CliClaude|CliCodex|CliGemini` with an `ExecutableRef` | `src/Bench.Domain/Registry/ModelConfig.cs` |
| …and nothing can RUN one: a non-OpenAI runtime is refused by name | `src/Bench.Application/Registry/ModelRegistry.cs:79` |
| The Claude CLI is installed on this machine, `2.1.216` | `~/.local/bin/claude` |
| No `prompts/` directory exists in this repository | — |

**So the gap is exactly three things**: something that runs a CLI agent, a use case that turns its output
into candidates, and a use case that turns a candidate into three reviewer marks.

### 2a. The boundary with `PLAN_tool_benchmark.md` — named in both

That plan's step 11 owns `CliAgentRuntime`: a cloud CLI as a measurement SUBJECT, with native tools, turn
budgets, and telemetry correlated per leg. This plan needs a CLI agent as an AUTHOR — one shot, one prompt,
one JSON answer, no tool loop and no telemetry.

| Item | Built here | That plan's part |
|---|---|---|
| Launching a CLI agent once and reading its answer | **§3.1** — `ICliAgentRuntime`, one prompt in, text out, bounded | consumes it |
| A CLI agent as a measured subject: turn ceilings, native tools, `agent-mcp` lane, per-leg telemetry | — | **its step 11** |
| `ModelResolution` learning to resolve a CLI runtime | **§3.1** | extends it with the lane axis |

Stated so that the second one to arrive extends the first rather than writing a second launcher. This
repository has paid for a duplicated process launcher once already (`ProcessRunner`, moved to a shared
project after being written twice).

## 3. The shape — decisions

### 3.1 `ICliAgentRuntime`: one prompt, one answer, bounded

```csharp
public sealed record AgentAsk(string Executable, string Prompt, string WorkingDirectory, TimeSpan Wall);
public sealed record AgentAnswer(string Text, TimeSpan Elapsed, long ResponseBytes);

public interface ICliAgentRuntime
{
    Task<Outcome<AgentAnswer>> AskAsync(AgentAsk ask, CancellationToken cancellationToken);
}
```

- **Over `ProcessRunner`, never a second launcher.** exe + argv, no shell string: this pipeline will be
  handed repository paths and question text, and text concatenated into a shell command is arbitrary code
  execution wearing a prompt.
- **The prompt travels on stdin**, not as an argument. A 4 KB prompt in argv hits the platform's command
  length limit at the worst possible moment — on the machine that has the biggest target repository.
  `ProcessRunner` therefore grows stdin support, which is an extension of the one launcher rather than a
  bypass of it.
- **Headless flags belong to the ADAPTER, one per runtime kind.** `claude -p` is not `codex exec` is not
  `gemini -p`. A single flag string in configuration would be a knob nobody can validate; a mapping from
  `ModelRuntimeKind` to argv is a switch that fails at a compiler error when a kind is added.
- **A refusal is a value**, exactly as `IModelRuntime`'s is: an executable that is not there, a CLI that
  exits non-zero, a run that outlives its wall are all facts the batch records and continues past. One
  agent's bad afternoon must not end an authoring run of six groups.
- **The executable comes from the registry's `ExecutableRef`**, resolved through `ISecretSource` on THIS
  machine — the same discipline that keeps the results database publishable. A reference that resolves to
  nothing here is refused before anything is launched.

### 3.2 The author's answer is JSON, and it is refused rather than repaired

The agent is asked for the exact wire shape the bank already reads (`QuestionJson`'s `QuestionFile`), so an
authored question and an imported one are the same thing by construction — there is no second format and no
mapping to keep true.

- **A malformed answer is a rejected candidate with the parse error as its reason**, never a repair. An
  authoring pass that fixes its author's JSON is a pass whose output nobody can attribute: the question that
  reaches the bank must be the question the model wrote.
- **Every candidate goes through `QuestionCandidate.Propose`**, which already refuses a question with no
  retrieval expectation and demands the author model be named. No second admission rule.
- **The seed is mandatory and comes from the TASK, not from the clock.** The author is asked to name what its
  question is anchored to (a member key, a PR, an issue) and when that thing dates from; a candidate whose
  seed cannot be read gets `unstated` at the beginning of time, which reads as *may recall* rather than as
  safe — the rule the bank import already follows.
- **Deduplication runs over the batch before anything is stored** (`Dedup.Find`), because three authors on
  one group will independently write the same question about the most obvious member in the repository.

### 3.3 Vetting: three reviewers, one mark each, and a rejection must say why

`bench questions vet` walks `Proposed` questions and asks each configured reviewer for a verdict, storing it
through the path `bench questions review` already uses — so a mark written by an agent and one typed by a
person are indistinguishable afterwards, which is correct: a review is a judgement, not a provenance.

- **A reviewer never reviews its own authorship.** The mark records the reviewer key, and the question
  records its author model; the pairing is refused when they are the same model. Self-review is the
  cheapest way to manufacture agreement, and this project already refuses self-judging in the arbiter lane.
- **Only `Accepted` questions are selectable into a test**, which is already enforced by the bank's query.
- **Acceptance is not a vote.** This plan stores marks and does nothing clever with them; what threshold
  promotes a candidate is an operator decision recorded per batch, and the default is the strict one — every
  configured reviewer approves. A majority rule invented here would be a quality claim nobody measured.

### 3.4 Prompts are a catalog, not string literals

A new `prompts/` directory, one file per role per group: `prompts/author/<group-key>.md`,
`prompts/review/<group-key>.md`. Read from disk at run time, and the file's own hash is recorded with the
batch.

The reason is the measured one this project keeps re-learning: **rewriting one ordering instruction moved a
score 16.5 points of 63 where swapping 4 tools for 18 moved 1** (`PLAN_tool_benchmark.md`). A prompt that
lives in a C# string literal is an unversioned axis with the largest measured effect in the system. The hash
on the batch is what makes "these hundred questions were written by that prompt" a fact rather than a
recollection.

### 3.5 What this plan deliberately does not do

- **Group 6 (`code-writing`) authoring.** Its three gates — the bug reproduces, the reference fix works, the
  tree is rebuilt to the buggy state — need a sandbox worktree and a build, and they belong to
  `PLAN_code_lane.md`. This plan authors the five READING groups and refuses group 6 by name.
- **No UI.** API-first is a gate in the sibling plan and it applies here: verbs first.
- **No automatic promotion to a suite.** `AuthoringBatch.Promote` already exists and `bench run --bank-group`
  already freezes a selection; nothing here needs to duplicate that.

## 4. Build order

Each step ships alone, tests green, before the next starts.

1. **`ProcessRunner` grows stdin**, with a test that a prompt larger than the platform's argv limit still
   arrives. The one launcher stays the one launcher.
2. **`ICliAgentRuntime` + `CliAgentRuntime`** — argv per `ModelRuntimeKind`, executable from the registry
   reference, refusal as a value, bounded by a wall. `ModelResolution` learns to resolve a CLI runtime for
   this ROLE without becoming resolvable as a measurement subject (that is step 11 of the other plan).
3. **`prompts/author/*.md` + `prompts/review/*.md`** for the five reading groups, with a loader that hashes
   what it read.
4. **`bench questions author`** — one group, N candidates per author, through `Propose` and `Dedup`, stored
   `Proposed` with the batch's prompt hash and author model. **First exercised with `claude` alone**
   (operator decision 2026-08-17): three authors are the design and one is the first measurement, because a
   pipeline that has never produced one good question does not need three ways to produce none.
5. **`bench questions vet`** — marks from each configured reviewer, self-review refused, rejection reasons
   mandatory.
6. **The first real batch**, and the number the founding plan says can only be learned by running: how many
   accepted questions per week the review step passes, and whether the memorisation-trap property can be
   checked mechanically or needs a person every time.

## 5. Test plan

- xUnit v3 exe (never `dotnet test`), `PostgresFixture` for anything touching the bank.
- Domain: the self-review refusal; a seedless candidate reading as *may recall*; dedup across three authors
  writing the same obvious question.
- Runtime: a fake executable (a script that echoes a fixture) proves argv, stdin, the wall and the
  three refusal shapes — `NotFound`, non-zero exit, `TimedOut` — without a cloud call.
- Authoring: a malformed answer becomes a `Rejected` candidate carrying the parse error, and NOTHING is
  repaired; a well-formed one lands `Proposed` with its author model and prompt hash.
- **A live-trait test with the real `claude` CLI**, skipped when the reference is unset — the lesson of
  2026-08-17 is that a stub agrees with whatever the caller assumed, and only a live run catches a wire
  spelling. It is not optional before this pipeline is called working.

## 6. Definition of Done

- [x] One process launcher still, now with stdin; no shell strings anywhere.
- [x] A CLI agent can be asked one question and its answer read, with every failure a recorded value.
- [x] Prompts live in `prompts/`, are hashed, and the hash is stored with the batch.
- [x] `bench questions author` produces `Proposed` candidates through the EXISTING admission rules, with
      dedup, and refuses group 6 by name.
- [x] `bench questions vet` records one mark per reviewer per question and refuses self-review — and a slot
      names its model as data, without which the refusal had nothing to compare.
- [x] A malformed author answer is a rejection with its reason, never a repair — and so is a reviewer's.
- [x] The live-trait test has actually been RUN against the real `claude` CLI, and the result reported.
- [ ] `research/architecture.md` describes the authoring pipeline (**done** — *Where questions come from* and
      *Vetting*); this plan is promoted to `research/` when step 6 has a throughput number, which is the one
      thing still outstanding.
