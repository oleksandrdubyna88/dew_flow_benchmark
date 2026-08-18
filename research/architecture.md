# Architecture — the system as it is

> Status: **current as of 2026-08-17.** Describes what exists and runs, not what is planned; the plan is
> [todo/PLAN_rag_bench_repo.md](../todo/PLAN_rag_bench_repo.md) and the evidence behind the design is
> [MEASURED_LESSONS.md](MEASURED_LESSONS.md). Where the two disagree, this file is wrong and should be
> corrected — a description that has drifted from the code is the failure this convention exists to catch.

## What it is

A benchmark that answers *"is configuration A better than configuration B"* about **any repository, at any
commit, measured by any engine, answered by any model**. Its output is a comparison, not a pass or a fail,
and almost every structure below exists to stop that comparison from being confidently wrong.

## The layers

Ports and adapters, with the domain as a leaf that depends on nothing. `ArchitectureTests` asserts it by
reading assembly references, so a violation is a red build rather than a review comment.

```mermaid
flowchart TB
    subgraph hosts["hosts"]
        cli["Cli — plan · run · judge · sweep · prune<br/>telemetry · variants · questions · models · version/help"]
        apphost["AppHost — Aspire, own Postgres"]
    end
    subgraph app["Bench.Application — use cases + PORTS"]
        runner["LegRunner · LegDrain · LegRecorder"]
        plan["PlanRun / PlanRequestHandler"]
        codecs["MetricCodec · TelemetryCodec · SuiteJsonLoader<br/>QuestionJson · VariantJson · ResponseMetaJson · RagPrompt"]
        ports["IRunStore · IResultStore · IEngine · IRetriever · IModelRuntime<br/>IRunTrace · IJudge · ICheckoutProvider · ITelemetryStore<br/>IVariantCatalog · IQuestionBank · IModelRegistry<br/>IFunnelSink · IHardwareSampler"]
    end
    subgraph dom["Bench.Domain — no packages, no IO"]
        contract["Targets · Suites · Runs · Splitting"]
        scoring["AnswerScoring · RetrievalScoring<br/>Discrimination · PhasePlan"]
        obs["Trace · Telemetry · Models · Retrieval"]
        axes["Variants · Authoring · Engines · Bank · Registry"]
    end
    subgraph infra["Bench.Infrastructure — adapters"]
        pg["Postgres: runs · results · funnels · hits<br/>telemetry · variants · bank · registry"]
        git["GitCheckoutProvider + ProcessRunner"]
        eng["FilesystemEngine · QlnEngine · QlnRetriever"]
        rt["OpenAiCompatibleRuntime"]
        tr["LiveTrace · FixtureTrace"]
    end
    contracts["Bench.Contracts — wire shapes, depends on nothing"]

    cli --> app
    apphost --> pg
    app --> dom
    infra --> app
    app --> contracts
```

Two projects depend on **nothing**: `Bench.Domain` and `Bench.Contracts`. That is not tidiness — a wire
contract able to reference the domain is a contract that leaks it, and a domain able to reference a package
is a domain whose rules cannot be tested without one.

## The measurement contract

One result row is identified by, and comparable only within, this tuple:

```
target   = (repoUrl, commitSha, exclusions[])     -- pinned, never ambient
engine   = (kind, endpoint, version, indexFingerprint)
suite    = (suiteId, suiteVersion)                -- frozen and hashed, from a file or a bank selection
subject  = (modelId, samplingAsSent)              -- 1..N, each with its OWN endpoint (SubjectRoster)
lane     = (toolSurface, preamble)                -- 1..N
variant  = (name, definitionHash)                 -- 1..N, from the catalog; absent on a run planned without it
repeat   = ordinal                                -- n >= 2 to rank anything
```

`ComparisonScope` is the part that must match before two results may be put beside each other: the target
and the suite. Everything else is an axis you compare *along*.

### The variant catalog

A **variant** is one named retrieval configuration — engine, channels, fusion, corpus recipe, reranker,
result limit — held as a row rather than as code, so a new configuration is a catalog entry and not a
migration. Three properties carry the weight:

- **A definition is never edited.** Results name the variant they ran under, so changing a recipe in place
  would relabel numbers already measured. A variant is added and retired; both states resolve forever
  (`RetrievalVariant`, mirroring `Suite`'s freeze).
- **The recipe is hashed.** Two rows with the same hash are the same configuration under two names — a
  duplicate the catalog can detect rather than a coincidence a report has to explain.
- **An axis this build does not know is refused, never dropped.** The definition is stored as JSON and
  read with unknown members disallowed, so a configuration nobody can honour fails by name instead of
  running as something else. The stored shape is camelCase because that row is published with the results.

`VariantSelection` is what a leg carries: `Selected(id, name)` or `NotApplicable` — a distinct state for a
run planned before the catalog existed, deliberately not an empty id that reads as a variant nobody can
look up. `Leg.Canonical` appends the variant only when there is one, so identities stored before the axis
existed still mean what they said.

**Identity is separate from configuration.** `ModelRef` is an id; `ModelEndpoint` holds the address and the
prices. Every aggregate, every discrimination reading and every saturation label is keyed by the id, so
folding an address into it would make the same model at a different port a different subject.

### The retrieval lane (single-shot RAG)

A cell whose variant names a retrieval recipe gets retrieval performed **for** it by the harness, and its
prompt is assembled from what came back. This is the single-shot lane; the agentic loop where a subject
decides what to search and when is a separate measurement (see *What does NOT exist yet*).

Two ports, not one, and the separation is measured rather than stylistic:

| port | what it models | who serves it |
|---|---|---|
| `IEngine` | retrieval as a **surface** a subject works — tools, refusals, whatever it decides to call | `FilesystemEngine`, `QlnEngine` |
| `IRetriever` | retrieval as a **call** the harness makes for one question | `QlnRetriever`, `NoRetriever` |

The same four tools behind a different surface shape once scored 4/63 against 37/63 on identical tasks, so
collapsing these into a single `SearchAsync` would make the surface measurement inexpressible — and folding
the single-shot lane into a tool loop would make its funnel unattributable, since a subject that never
searched produces no funnel at all. `QlnEngine` composes `QlnRetriever` for its search tool: one round trip,
one funnel path, whichever lane asked.

**A recipe becomes a request in exactly one place** (`QlnRequest.From`), which is also where the stored
"asked for" axes come from — so what a run records is literally what went out, not a second mapping that
agrees with the first until somebody edits one. An axis the run did not set is **omitted** from the JSON
rather than sent as a C# default: a call that meant to set only a limit would otherwise send
`rerank: false, rerankPool: 0, rrfK: 0`, and the engine would clamp those into a configuration in no catalog
row.

**What the engine can serve, and what it cannot.** As of 2026-08-17 a recipe reaches it whole: the channels,
both weights, the rank constant, the fusion **algorithm** and its normalization, the reranker and its pool,
the result limit, and the corpus **text shape** (which rides beside the axes, since it selects a collection
rather than a knob). Fusion and normalization arrived that morning in the sibling plan's step 1; before it
they were refused here, because the engine would have accepted the request, ignored the field, and served
rank fusion under a record that said `wsum` — the reranker scar exactly.

Still not request axes: **chunk size and embed model**. The engine derives those from its own configured
recipe, so a search cannot select them — two variants differing only in chunk size are two variants it cannot
yet distinguish.

**They are verified instead of selected.** Before a single cell exists, one GET reads what is actually in the
index a search will reach — collection, point count, text shape, window and overlap tokens, embedder,
tokenizer, and the commit of the newest succeeded pass that wrote it — and a recipe that disagrees ends the
run naming both values (`IRetriever.InspectAsync`, `IndexReadiness`). This was missing for exactly one
measurement: on 2026-08-17 a variant declaring 512 embed tokens was recorded against a 256-token index, every
number in it real and the row naming them describing a corpus that would have produced different ones.

Two comparisons in that check are not verbatim, and both directions matter:

- **The embedder name.** An operator types `bge-m3` into a catalog row and the engine reports
  `BAAI/bge-m3 (dense, FP32)`. Vendor prefix and parenthetical detail are dropped before comparing, because
  verbatim equality would refuse every correct recipe — while a CONTAINMENT rule would accept `bge-m3`
  against a `bge-m3-large` index, and a false accept is indistinguishable from a correct measurement
  afterwards.
- **The commit, in three states.** Matched, differing, or **unstamped** — and the third is not the second.
  Every index built before the engine began recording its commit is unstamped, so strict equality would have
  blocked every cell against every index in existence on the day the stamp landed. Unstamped is refused by
  default and passable with `--allow-unstamped-index`, which keeps printing that the tree is UNVERIFIED — the
  same shape as `--no-checkout`. An index built from a DIRTY tree is refused outright: its stamp then names a
  commit the index does not contain, which is worse than no stamp because it reads as evidence.

Two refusals remain, and both happen **before any cell exists**: a recipe naming another engine, and a
corpus shape this one does not have. `IRetriever.CanServe` is the check — a mapping, not a round trip — asked
once per variant while the run is being planned, for the same reason the model registry is resolved there: a
recipe the engine has no field for would otherwise arrive as ten thousand identical leg failures. Its
success value is the axes it WOULD send, so an operator can read what a variant actually becomes.

**Its axes contract refuses members it does not know** (`JsonUnmappedMemberHandling.Disallow`, landed the
same day). So a stray field in this side's request record is now a broken retrieval rather than harmless
noise — which is exactly how one was found, a computed property that had been serialising itself into the
request all along.

**The variant is looked up per cell, exactly as the subject is.** `VariantRoster.For(cell.Variant)` mirrors
`SubjectRoster.For(cell.SubjectModelId)`: a run holds several recipes because the variant is an axis, and a
cell whose recipe is not in the roster is **settled** rather than measured under another one. A cell planned
without a variant resolves to the control arm, which is what every run planned before this lane existed is.

**The prompt is the artefact.** `RagPrompt` assembles it deterministically in rank order — path, span,
signature and the hit's own text — and stores it on the result. It says when a snippet was truncated and when
the engine returned no text for a hit, because the stored prompt has to describe what the model actually
read. There is deliberately **no cap on the number of hits**: that is the variant's `limit` axis, with a name
and a hash, and a second cap here would be an unnamed axis applied to every arm.

**Anchor recall stops reading *not applicable*.** `RetrievalScoring` matches the returned hits against the
question's anchors and feeds `AnswerScoring`'s existing recall metric — one definition of recall in the
system — then adds recall@5, recall@10, MRR and a first-hit rank. Matching is by the readable `Type.Member`
identity or by **line overlap**, never by member name alone: a suffix rule would let `NoRetry` answer for
`Retry`, and a recall figure inflated that way is indistinguishable from a real one. A leg that performed no
retrieval gets **no** rank metrics at all, so the control arm keeps exactly the metric set it had before this
lane existed instead of entering every retrieval aggregate at the bottom.

### What a retrieval leg stores, and who owns its growth

| surface | holds | owner |
|---|---|---|
| `results.Prompt` / `Answer` / `ThinkingText` | the artefact — a published number re-checked against the text that produced it | **kept forever**; its size is a budget line, not a cleanup target |
| `results.ResponseMetaJson` | tokens in/out, latency, stop reason, response bytes, sampling AS SENT | kept forever; unrecoverable after the fact, since a re-run is a different call |
| `funnels` | one row per leg: contract version, stages, total, absent stages, degraded + reason, payload bytes, elapsed, collection, **requested and applied axes** | kept forever — small, fixed-size, and the white-box evidence |
| `retrieved_hits` | rank, path, span, both member identities, signature, score, ordering, channels, per-channel ranks, snippet | **rolled up**: everything a metric computes from is kept forever, the snippet TEXT is released after a window |

`results.ThinkingReason` sits beside the text and is empty exactly when the text was captured — a model that
hides its reasoning and one that reasoned about nothing are different facts, and an empty string alone
merges them. The same three-state discipline covers a hit's snippet: **present**, **never reported by the
engine**, or **released by retention** — a row whose text was dropped must never read as a hit the engine
sent no text for, so the byte count survives the drop.

Retention is `bench prune` and a pass at every `bench run` startup, beside the crash sweep and for the same
reason: a budget that only runs when somebody remembers to run it is not a budget. Default window seven
days, `--hit-retention-days 0` keeps everything. It is an `ExecuteUpdate` filtered on the hit row's **own**
`CreatedAt` — denormalised from the result on write — so the largest table in the system is never joined to
`results` to decide what is old.

### Where questions come from

Two doors, one set of rules. **Import** takes a hand-authored file; **author** drives a CLI coding agent to
write candidates for one group (`bench questions author`, `PLAN_question_authoring.md`). Both land as the
same rows through the same admission rules, and authored ones are `Proposed` until something vouches for them
— which is what keeps "a machine wrote a thousand overnight" from meaning "a thousand are measurable".

Authoring exists because it is the project's one unavoidable bottleneck, in the founding plan's words:
*"Running is cheap and authoring is not."* Every axis built — variants, subjects, lanes, repeats — multiplies
over a question set, and the set is the one factor nothing else compensates for.

- **`ICliAgentRuntime` is not `IModelRuntime`.** One launches a process to do work FOR the harness; the other
  is a completion endpoint measured AS a subject. The same executable will be measured through the second port
  with turn ceilings and telemetry (`todo/PLAN_tool_benchmark.md` step 11), and conflating them would make
  measuring an agent indistinguishable from using one.
- **Over the one launcher**, which grew stdin for this: a prompt runs to kilobytes and an argument list caps
  out around 32 KB, so argv would have failed on the machine with the biggest target repository.
- **The agent is launched inside the target's checkout.** An author that cannot read the repository cannot
  anchor a question in it — asked to write about a commit it had no access to, the agent refused and said so
  rather than inventing line numbers.
- **The answer is `BankQuestionFile`** — the shape `import` already reads, seed and all — so an authored batch
  is literally an importable file and there is one format, not two that agree until somebody edits one.
- **Nothing repairs an answer.** A malformed reply is a rejection carrying the parse error and a SAMPLE of what
  was said, because the next edit to the prompt is made from exactly that text. The array is EXTRACTED from a
  fence or a prose preface — that changes no question — and anything the author said outside it is reported as
  a note rather than dropped. That note is how the environment finding below became visible at all.
- **Prompts are a hashed catalog** (`prompts/author`, `prompts/review`): shared contract plus per-group brief,
  and the hash covers both, so editing the contract changes every group's identity. A prompt in a string
  literal would be an unversioned axis with the largest measured effect in the system — one rewritten ordering
  instruction moved a score 16.5 points of 63 where swapping 4 tools for 18 moved 1.
- **A seed date is never invented.** Unknown reads as *may recall*; a guessed date reads as *safe*, which is
  the lie the whole memorisation check would rest on. `QuestionSeed` normalises to UTC in the domain, because a
  date written `2026-05-14` deserialises to the reading machine's offset and Postgres accepts only UTC.
- **`code-writing` is refused by name.** Its authoring needs three gates — the bug reproduces, the reference
  fix works, the tree is rebuilt to the buggy state — which need a sandbox worktree and a build
  (`todo/PLAN_code_lane.md`).

Measured on the first live batch (2026-08-17, Claude CLI 2.1.216, target `dew_flow_rag_qln` at `64865c68`):
two questions authored and stored, both anchored at members the agent verified in the checked-out tree. Two
environment facts came out of it and are not yet fixed: **git history is not readable inside the worktree** (a
`git worktree` makes `.git` a redirect file, which the agent treats as untrusted and declines), so seed dates
came back `unstated` — which the `pr-diff` group depends on entirely; and a CLI that prints diagnostics beside
its answer is why the runtime reads **stdout alone** rather than the merged output.

**An untrusted checkout costs the whole wall, so the pass pre-trusts it.** Measured on the first real batch
(2026-08-18): two of four groups produced nothing and burned their full 900-second wall each — half an hour —
because the CLI printed *"Ignoring 5 permissions.allow entries: this workspace has not been trusted"* and then
waited on a dialog no headless run can answer. The other two groups succeeded with the same warning printed, so
the warning is not the failure: the blocked tool call behind it is. `WorkspaceTrust` now sets
`hasTrustDialogAccepted` for the tree before any launch — for **both** keys, the worktree and the bare
repository its `.git` pointer names, because the CLI's own message names the bare path while the agent is
launched in the worktree and the lookup is by string. Three properties, because the file is the operator's and
not ours: only paths **under the benchmark's checkout root** may be trusted (trusting an arbitrary path would
hand any repository this benchmark is pointed at the permissions of a trusted workspace), exactly one boolean is
written with every sibling field preserved and a non-object `projects` refused rather than replaced, and the
write goes through a staged file with a backup. It never fails the verb — a tree that could not be pre-trusted
still runs and the printed `trust` line says so; `--no-trust` skips it. Verified against the live config: 51
top-level keys unchanged, one entry added, one boolean flipped in a ten-field entry, and the workspace warning
gone from the next run's output.

**There are SIX groups, and five of them can be authored.** `code-writing` is a real group of this benchmark —
`data/bank-seed.json` seeds it as a row and `BankSeedTests` holds the count at six — and it is the one
`bench questions author` refuses by name, because its three gates need a sandbox worktree and a build. The row
exists so a hand-authored code task has a home and so every report that groups by key counts six; the seed file
is committed because the number was once answered from memory and was wrong by one.

### Vetting: who marks a machine's questions

`bench questions vet` walks a group's `Proposed` questions and asks every **bound** reviewer slot for a verdict,
storing each mark through the same path `bench questions review` uses — so a mark written by an agent and one
typed by a person are indistinguishable afterwards. That is correct rather than sloppy: a review is a
judgement, and its provenance is the slot, which is recorded either way.

- **The mechanical half runs FIRST, and a broken anchor costs no launches.** `AnchorCheck` verifies every
  retrieval expectation against the checked-out tree — the file is present, the line span does not run past the
  end of the file, the member's name occurs inside the span — before a single reviewer is asked. Measured
  2026-08-18: all three live reviewer notes on the first vetted pair *led* with exactly this check, and the
  review contract's first rejection reason is an expectation pointing at nothing, so three agent launches per
  question were buying arithmetic. A question that fails is reported with where the name actually is (the
  difference between a wrong question and a stale line range is what the operator does next) and stays
  `Proposed`: a broken anchor is a defect to fix, not a verdict. What the check does NOT prove is that the
  member is *defined* there — a name in a comment satisfies it — and it says nothing about whether the question
  is any good. Anchor paths are data an agent wrote, so they resolve through `RootedPath` (shared with
  `FilesystemEngine`, which had the only copy of that arithmetic): a path that escapes the tree reads as
  nothing to find.
- **A reviewer slot names its model, as data.** `reviewers.ModelKey` is a registry key, empty for a person. It
  is a column and not a command-line flag because the self-review rule compares a reviewer's model against the
  question's author model, and "who is reviewer-2" has to be answerable from the bank years later. Three slots
  were seeded on 2026-08-17 naming nobody, and this pass could not be written until they did.
- **A model never marks its own writing** — refused before any launch, comparing the resolved **model id**
  rather than the slot or registry key, because two rows bound to two keys that both resolve to
  `claude-sonnet-4-6` are one opinion. `--allow-self-review` takes the mark anyway and **prints what it costs**;
  the run's report carries that sentence, so a batch marked this way cannot be quoted without it.
- **The reviewer is shown the question, not its provenance.** `BankExport.ForReview` omits `authorModel`,
  `state` and the other reviewers' marks: a mark that knows who wrote the question, or how the others voted, is
  partly about the author and partly about agreement. The seed IS shown — the reviewer is asked whether the date
  looks invented.
- **An unreadable verdict is never an approval.** The pass reads its own wire shape rather than the import's
  `ReviewFile`, whose verdict defaults to `Approved` — right for a file a person wrote, and the failure that
  looks like success for an agent whose answer lost a field. A rejection with no note is refused outright: it is
  the only record of what an author gets wrong.
- **Promotion is the strict rule and there is no threshold knob.** `Promotion.Decide` accepts only when EVERY
  configured reviewer row has approved, rejects on any single rejection (a defect somebody named outranks any
  count of approvals), and otherwise waits. An empty `reviewers` table promotes nothing — "every configured
  reviewer approved" is vacuously true of none, and would accept a whole machine-written bank on an empty table.
  A majority rule would be a quality claim nobody here has measured.
- **A decided question is not re-vetted.** Only `Proposed` rows are walked, so a machine cannot overwrite a
  person's mark.

**Today all three slots are bound to one model** (`claude-reviewer` → `claude-sonnet-4-6`, the operator's
decision of 2026-08-18 while one CLI author is verified). That is *one opinion sampled three times, not three
reviewers*, `bench questions reviewers` says so when it sees identical bindings, and every number derived from
such a batch has to be reported that way.

### The question bank

Questions live in Postgres in named **groups**, with per-**reviewer** marks — both rows rather than enum
members, so a sixth group and a fourth reviewer each cost one insert instead of a migration and a redeploy.
A question carries the suite-facing id every cell and every result quotes, the ordinal an operator selects
by ("group 1, questions 1–10"), what it was authored against, and the **seed** it was derived from with the
date that material entered the world — the memorisation check's only input, and deliberately not the import
date, which would certify every question as clear against every subject's cutoff.

Three properties carry the weight:

- **One way to mint a suite stamp.** A selection from the bank is promoted through the same
  `AuthoringBatch.Promote` + `Suite.Freeze` a file goes through (`BankFreeze`), so a test built either way
  names the same kind of hashed stamp and a result cannot tell which door its questions came through.
  Freezing inherits the refusals already written there: nothing accepted, two questions about the same
  lines, and — added here — one suite-facing id twice.
- **Only what somebody vouched for is selectable.** `BankQuery.Selection` is accepted-only by construction
  rather than by a filter each caller remembers, and admission itself is `QuestionCandidate.Propose`'s rule
  rather than a second one written for the store.
- **Group membership is versioned, and a report does not move.** The current home is a column, the moves
  are rows (`question_group_moves`, refused without a reason), and `run_questions` is the per-test snapshot
  of which group each question was in **when the test was created**. A report reads the snapshot; re-filing
  a question next month cannot move a finished test's numbers into a different column.

`bench questions import|author|vet|list|groups|reviewers|bind|review|accept|reject|move` is the surface;
`bench run --bank-group` freezes a selection instead of reading a suite file. A file-selected run writes no snapshot rows, which is
the honest reading rather than a gap: a file has no groups.

### The model registry

Models are configuration, never constants. A row is a key, a runtime, a hosting, and a configuration that
holds **references, never values** — the NAME of the environment variable that holds an endpoint or a key,
resolved on this machine at use. That is the publication rule with teeth: this database is meant to go out
unedited, and a guarantee scoped to result rows while the registry sits in the same schema would be a
redaction pass nobody has scheduled. Sampling and prices stay as values — neither is secret nor
machine-specific, and a run must be able to say what it asked for and what its tokens cost. `ModelConfig`
refuses a url or an absolute path *by name*, and a test re-reads every stored row through that same rule.

A test chooses its **subjects** and its ordered **arbiters** from the enabled rows, and the choice is
stored on the run (`run_subjects`, `run_judges`): the registry can change afterwards without rewriting
what a finished test says it measured. Resolution happens before a single cell exists — a disabled model,
an unknown key, a runtime this build cannot drive, and a reference that is unset on this machine are each
refused by name, rather than discovered three hours into a sweep as a wall of identical transport
failures. A subject may be ADDED to an existing test (that is how a settled test reopens); removing one is
not, because its settled cells would dangle. An arbiter added later continues the order rather than
restarting it.

**One endpoint per SUBJECT, looked up per cell.** `SubjectRoster` closed a defect the registry uncovered:
the matrix has always planned a list of subjects while the runner held a single endpoint, so a two-subject
run would have sent every leg to the first model and labelled the results with the cell's subject — two
models named, one measured, invisible in every report. A cell whose subject this run cannot reach is
settled with that reason, never redirected.

`bench models add|list|disable|enable` is the surface — the listing says which references resolve *here* —
and `bench run --subjects <keys> [--judges <keys>]` composes a test from them. The ad-hoc `--model` pair
still works for pointing the harness at something once; such a run records no roles, because a role names
a registry key and it named none.

## One leg, end to end

`LegRunner` is the assembly. Every piece it uses existed and was tested separately before it; this is where
the seams are actually proved.

```mermaid
sequenceDiagram
    participant R as LegRunner
    participant S as IRunStore (Postgres)
    participant E as IRetriever
    participant M as IModelRuntime
    participant D as Answer + RetrievalScoring (domain)
    participant V as IResultStore (Postgres)

    R->>S: ClaimNextAsync(run, owner = label@host#pid)
    Note over S: guarded UPDATE — exactly one worker wins
    S-->>R: cell
    R->>V: HasResultAsync(cell)
    Note over R,V: re-entrancy: a leg scored but never settled is FINISHED, not re-measured
    R->>R: Subjects.For(cell.subject) · Variants.For(cell.variant) — looked up, never assumed
    R->>R: LegDeadline.For(budgets, now) — ONE deadline for the whole leg
    R->>E: RetrieveAsync(question, recipe)
    Note over R,E: skipped entirely for the control arm — NotPerformed is a state, not an empty list
    E-->>R: hits · funnel · collection · axes asked and applied
    R->>R: RagPrompt.Assemble(question, context) — the stored prompt IS what was sent
    R->>M: AskAsync(prompt, sampling, deadline.ForCall(now))
    M-->>R: answer · thinking · tokens · latency · samplingAsSent · stopReason
    R->>D: Score(question, answer, observed) + Score(question, context)
    D-->>R: metrics — expectations, anchor recall, recall@k, MRR, first-hit rank
    R->>V: SaveAsync(result + funnel + hits + thinking + meta)
    R->>S: SettleAsync(cell, outcome)
```

**Result first, settle second.** A crash between them leaves the cell claimed rather than settled, so the
sweep hands it back and a retry finishes the interrupted job. Settling first would lose the result
invisibly.

**An answer cut off at a ceiling settles as `CapExceeded`, not `Completed`** — scored as a wrong answer it
would measure the ceiling, and only a recorded cap keeps the leg out of paired deltas.

**One wall budget per LEG, not per call** (`LegDeadline`, `src/Bench.Domain/Runs/LegDeadline.cs`). The
deadline is computed when the leg's model work starts, and every call is handed the REMAINDER through
`ForCall(now)`; a leg that spends it settles `CapExceeded(Wall, …)` and stores no result, while a leg that
failed inside its budget still settles `Crashed`. The distinction is what a per-completion timeout cannot
express: under a 25-turn lane, a 10-minute per-call ceiling is 4 h 10 m of one leg, and a breaker that
fires at twenty consecutive failures needs ~3.5 days to say what the first hang already said. `bench run`
asks for the ceiling with `--leg-wall-seconds` (default 600) and **confirms it with the runtime before any
cell exists** (`BudgetConfirmation`) — a budget the runtime refuses ends the preparation instead of being
believed. When the tool-calling loop arrives it turns inside `LegRunner.AskAsync`, checking
`Exhausted(now)` between turns; nothing else may introduce a second deadline.

## Phases

A leg runs phases, and phases are ours: the adopted evaluation library's unit is a single evaluation with
no notion of one. `TaskKind` picks the plan — `Reading` answers once and is judged; `Fix` runs
**investigate → fix → verify → judge**. A phase cannot start while an earlier one is unfinished, and a
ceiling or a crash stops the **leg**, not just the phase.

## Two vantage points on the same call

| | bench-side trace (`IRunTrace`, `LegRecorder`) | server-side telemetry (`ITelemetryStore`) |
|---|---|---|
| covers | this harness's own legs | **all** traffic, benchmark and real sessions |
| knows the prompt, the answer, the cost | yes | no |
| knows server processing time, the payload returned, the project scope | by inference | exactly |
| shipped by | this repository | `dew_flow_mcp` / `dew_flow_rag_qln`, ingested here from a spool |

The trace port has **two** implementations — live black-box and fixture-replay white-box — because an
interface with one implementation proves nothing about its own shape. The white-box funnel
(collection → embed-query → retrieve → fuse → collapse → rerank → cut) is what answers *"recall failure or
ranking failure"*, and it is no longer a fixture: `dew_flow_rag_qln` emitted its first real one on
2026-08-15 (five of `trace/v0`'s seven drafted stage names were wrong, and the emitter won every
disagreement), and since 2026-08-17 every retrieval leg **persists** it to `funnels` — including a degraded
one, with the reason it could not be read. In the single-shot lane the funnel travels back as part of
`RetrievedContext` rather than through the sink; the sink remains the tool lane's path, where the subject
makes the call and the funnel has no return value to ride on.

Telemetry records carry a **caller-supplied** correlation (leg + phase). The emitter cannot know what a
benchmark leg is, so a real session records as unattributed — and unattributed traffic is excluded from a
leg's totals rather than folded in.

## The one verb that spends money

`bench run` is the only command that reaches a model. It plans the matrix, persists every cell, then drains
the queue leg by leg through `LegRunner` — one claim at a time, so a second process running the same
command is a second worker rather than a duplicate run.

**It checks the target out first.** A bare mirror per url and a worktree per commit, under a cache root
this process owns (`--checkout-root`, default under the user's local application data) — never a directory
anyone works in. A commit that is unpushed, on a fork, or garbage-collected ends the run *there*, by name,
instead of producing a campaign of results labelled with a tree nobody ever saw. The provider had existed,
tested, since the first commits with **no caller**; until it was wired in, every run printed that its
commit was "recorded but unverified" and measured anyway. `--no-checkout` keeps that older behaviour for a
target this machine cannot clone, and keeps the warning, because then it is true.

**It reports; it does not judge.** No bar has been agreed, so the exit code answers *did the measurement
happen* — never *was the subject good*. `0` a run that produced legs, `5` a run that produced none, `3` an
unreachable store or a missing checkout, `4` a malformed invocation. A low score exits `0`, and that is the
whole point of the split: an agent that reads "the model answered badly" as "the harness is broken" will
keep reporting the wrong news.

Its first live execution — Polly, three questions, no tools, two repeats — settled 6 legs and passed 0. That
zero is the SUITE's result, not the model's: it is the mechanical memorisation check, and it is recorded in
[MEASURED_LESSONS.md](MEASURED_LESSONS.md) §4c.

### What the drain survives, and what ends it

The loop is `LegDrain` (`src/Bench.Application/LegDrain.cs`), and it is a separate unit because of what a
campaign of ten thousand cells has to live through overnight:

- **One failed leg is recorded and skipped.** Every leg — its scope, its service resolution and its work —
  runs inside its own `try`. A transient `NpgsqlException` on leg 3 001 fails *that* leg and the remaining
  7 000 still run. Until 2026-08-16 it did not, and one blip took the process with every pending cell.
- **A run of failures ends the campaign.** `--max-consecutive-failures` (default 20) stops a run whose
  environment is broken, with a reason naming the last error, and exits `3`. A leg that merely SCORED badly
  resets the run — the harness still reports rather than judges.
- **A stop is planned.** Ctrl+C / SIGTERM cancel a root token: no further cell is claimed, the leg in flight
  keeps its token for a 30-second grace so it can settle, and the verb exits `5` — the run is resumable, not
  finished, and an orchestrator must be able to tell that from a completed one.
- **Recovery runs first.** Every `bench run` sweeps before it drains, so cells a killed host left `Claimed`
  come back. `bench sweep --db … [--stale-after-minutes 30]` is the same recovery as an operator verb, for
  after a `kill -9`. The store had this from its first commit and *nothing called it*, which is the audit
  finding this whole section exists to prevent repeating.
- **And retention runs beside it**, for the same reason: `bench run` releases hit snippets past the window
  before it drains, and `bench prune --db … [--hit-retention-days 7]` is the same pass as an operator verb.
  A policy that only runs when somebody remembers to run it is the pattern above, one table larger.
- **And it is ownership-checked, because the sweep is now live.** A claim records the worker's LABEL, HOST
  and PID (`WorkerIdentity`, `cells.owner_host` / `cells.owner_pid`); the sweep loads only the stale
  candidates and hands back the ones whose owner is provably gone. Time alone would be wrong the moment a
  second `bench run` starts — the architecture invites exactly that — because "claimed longer than the
  window" also describes a colleague on a slow leg, and requeuing it puts two workers on one measurement
  and refuses the honest one's settle. The window (30 min against a 10-min leg wall) is a MARGIN, not a
  death certificate. Three rules decide: an owner with no host/pid recorded is gone by definition (it
  predates the columns and nothing can vouch for it); an owner on **another machine is left alone** — that
  host's process table is the only one that can answer, and ending a live leg is worse than leaving a stale
  row for its own host's next sweep; a live pid here is not gone, whatever the clock says. Mirrors
  `dew_flow_rag_qln · src/Rag.Infrastructure/Indexing/IndexPassStore.cs:191` (`SweepOrphansAsync`).

The CLI is a host like any other: `run`, `judge` and `sweep` build a container wired to the same Serilog
sinks as the AppHost — coloured console, one file per run under `logs/{yyyy-MM-dd}/`. `help` and `version`
touch nothing and write nothing.

**`logs/` has a named retention owner: the host, at startup.** Creating the logger also retires day-folders
older than `Serilog:RetentionDays` (default 14), best effort — a folder another host holds open is skipped
rather than fatal, and a folder whose name is not a `yyyy-MM-dd` day is never deleted, because the method
removes directory trees. Zero disables it, which is the shared rule's other option: an operator job owns
the folder instead. A file per run with no reaper is a disk that fills, and on a machine running 24/7 the
"eventually" is a date.

**Nothing in a long run accumulates per leg.** `LiveTrace` retires a leg's recorder in the capture that
hands its trace over (`Close` covers the abandon path), and `GitCheckoutProvider`'s per-repository gates are
reference-counted — created on first use, disposed when the last caller leaves, including when the checkout
failed. Both were `GetOrAdd`-forever maps: harmless while only the CLI drives one leg at a time, and a leak
with a shape the moment a long-running worker wires them in. `bench telemetry ingest` streams its spools and
commits in chunks (`--chunk-size`, default 500) so memory and the store's parameter list are bounded by a
size this process chose rather than by how productive the emitter has been, and the run summary counts its
two integers in SQL instead of hydrating every prompt, answer and metric of the run to fold them here.

## The arbiter, and why it never re-runs a leg

`bench judge` reads a finished run's STORED answers and appends one metric row per leg. It re-scores; it
never re-measures. That is the property the port exists for: the expensive artefact is the subject's output,
so changing the arbiter — or adding a second one that disagrees — costs its own inference and nothing else.

- **Named per arbiter.** The metric is `Judge verdict · {modelId}`, so two arbiters over one run are two
  series that cannot collide, and the same arbiter re-run sees only what it never finished. Work is selected
  by NOT-EXISTS against that name, which makes idempotency and crash-resumability the same query.
- **Asked a binary, at temperature 0.** A judge asked for a score invents a scale and drifts along it
  between runs. YES/NO means the same thing in March and in August.
- **An unreadable verdict is a refusal, never a NO.** Defaulting to NO makes a broken arbiter look exactly
  like a wrong subject, on every leg it touched.
- **No reference answer is a gap in the SUITE**, recorded as *not judgeable* — not a failing leg.
- **Self-judging is marked, not refused.** Measured the day it shipped: the subject model passed 6 of 6 of
  its own answers that an independent arbiter and the mechanical scorer both failed
  ([MEASURED_LESSONS.md](MEASURED_LESSONS.md) §4d).
- **The wrong suite is refused whole.** A verdict issued against a reference from a different suite is the
  one wrong result this system could not detect later, because it would look like a normal one.

The judge sits BESIDE the mechanical score, never instead of it. Two arbiters need a third thing to be
checked against, and the deterministic metrics in the same result are it.

## Guards that shape the API

Each is here because something went wrong that it now prevents; the catalogue is
[MEASURED_LESSONS.md](MEASURED_LESSONS.md).

- **Nothing is captured-or-zero.** `Captured` / `CapturedCount` carry a flag beside every value. Unreported
  tokens make a cost *unknown*, never free.
- **Unset is a refusal.** An empty model id or base url is refused rather than defaulted — an empty
  reranker id once resolved to a paid cloud model inside an arm labelled "$0 local".
- **A budget records the runtime that accepted it**, and a runtime refuses ceilings it cannot enforce. A
  cost ceiling is enforced by the harness not starting the next leg, never by a completion endpoint.
- **Discrimination is a property of a comparison**, not of a question, and nothing is deleted for being
  easy. Difficulty is a measured label per subject tier.
- **A suite version is frozen and hashed**; ground truth is commit-scoped and re-targeting is explicit.
- **Checkouts are read-only** — a bare clone per url, a worktree per commit, never a directory anyone works
  in.
- **A retrieval expectation in a lane with no retrieval is *not applicable*, not a miss.** Scoring it zero
  would make the no-tools baseline look worse than it is, and that baseline exists to be compared fairly.

## What does NOT exist yet

Stated because a description that quietly implies more than is built is the same defect as a stale diagram.

- **No tool-calling loop.** `IEngine` exposes tools and both engines implement them, but `LegRunner` asks the
  model exactly once. Retrieval now happens for a cell that names a variant — the harness performs it and
  puts the hits in the prompt (*The retrieval lane*, above) — so anchor recall is a real number there; what
  does not exist is the lane where the **subject** decides what to search and when. That is the other
  measurement, and the two are not interchangeable: the same four tools behind a different surface shape
  scored 4/63 against 37/63. The loop's per-leg wall budget already exists (`LegDeadline`) and is
  deliberately in place first: retrofitting it after the first long agentic campaign means discovering it
  from a multi-day gap in a log.
- **No cloud runtime.** Only the OpenAI-compatible local one.
- **No hardware sampler**, no UI, and the API route group is not hosted.
- **`IBenchStore` / `InMemoryBenchStore` are dead** — nothing calls them.
- **The authoring pipeline exists but has no THROUGHPUT number.** `author` and `vet` both run against the real
  Claude CLI, and what nobody knows yet is the only figure the founding plan says can be learned by running:
  how many accepted questions a week of this produces. Two facts bound it and neither is fixed —
  **`pr-diff` cannot be authored at all** while git history is unreadable inside the worktree, and **all three
  reviewer slots are one model**, so today's approvals are one opinion sampled three times. A number produced
  before those are addressed would be a number about this compromise rather than about the pipeline.
- **Two variant axes cannot be honoured yet, and they are the corpus ones.** `bench run --variants` plans one
  leg per variant and the engine serves everything a recipe names except **chunk size** and **embed model**,
  which it derives from its own configured recipe rather than from a request. So two variants differing only
  in chunk size are two variants it cannot distinguish, and what a run records instead is the collection that
  answered. They land with the index preparations below and with `dew_flow_rag_qln ·
  todo/PLAN_search_variant_axes.md` steps 3–4.
- **A pass cannot be TRIGGERED from here.** The echoed axes are now asserted and a mismatched cell is blocked
  (`EngineAxes.AssertAppliedIn` → `LegRunner.BlockAsync`), and `index_preparations` holds a preparation's owner,
  heartbeat and stranding sweep — but nothing starts an index pass over HTTP, so a corpus the engine has not
  built is a refusal the operator satisfies by hand. That is the tail of step 5 in
  `todo/PLAN_variant_matrix.md`.
- **The checked-out tree is verified but not yet READ.** `bench run` mirrors and checks out the target before
  it measures, which is what makes a commit real rather than recorded — but no lane reads the worktree yet:
  the retrieval lane reads the engine's index, and there is no tool loop to read files.
