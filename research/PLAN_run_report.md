# PLAN — the comparison comes out of the store, and the split finally decides who won

> Status: **IMPLEMENTED, 2026-08-19.** All five steps shipped the same day the plan was written: the store's
> generalised aggregate and its de-hydration, `RunReport`, the `bench report` verb, the wire contract with its
> three routes, and `hosts/Api` in the AppHost. The guard this repository was built around — the
> selection/held-out split — is consulted for the first time, and `Unproven` is now a word a report prints.
>
> Scope as built: `Bench.Application` (`RunReport`, `RunReportContract`, two port additions),
> `Bench.Infrastructure/Persistence` (one generalised query, one hydration fix, two reads),
> `Bench.Contracts` (the report DTOs), `Bench.Api` (three `GET`s), `hosts/Api` (new), `hosts/Cli`
> (`bench report`, `CommandLine.Double`), `Bench.Domain` (`Suite.IdOf`). **No migration**, as planned: every
> dimension groups by a column that already existed, and the split half is derived by a pure function.
>
> **Deviations, and the three defects the build found that the plan had not predicted.** Each is argued where
> it belongs in the text below; in summary:
>
> - **The aggregate was hydrating the whole campaign.** `AverageAsync` materialised full result rows — every
>   prompt, answer and `ResponseMetaJson` of a run — to average one number, which is verbatim the diagnosis
>   already written twice in the same port. Fixed to a projection, RED test first.
> - **The exit-code contract had a hole.** A missing `--metric` exited `3` (environment) rather than `4`
>   (configuration), collapsing the distinction the contract rests on. The surface validates its own
>   invocation now; the phrase is `RunReport.NoMetricNamed`, one constant for both sides.
> - **`Suite.IdOf` had to exist and the plan had not noticed why.** A run stores the suite STAMP while
>   `SeedSplit` assigns from the suite ID — splitting on the stamp would have reshuffled every half at every
>   freeze, the exact defect `StableHash` is there to prevent.
> - **§3.2's `SplitHalf?` became `QuestionScope`**, a closed pair carrying question ids: Postgres cannot
>   compute the hash, so the half travels as its members and the aggregate stays a group-by.
> - **§3.7's "not assertable through EF" caveat was wrong** and was withdrawn before the build — the
>   technique was already in the neighbouring test file.
> - **A one-sided-split warning** was added, which the plan did not list; a test found that the split is the
>   report's own reading of the run's questions.
> - **`--json` emits the contract, not the view**, which is stronger than the equivalence test the plan
>   proposed.
> - **One DoD line was withdrawn rather than ticked**: `Discrimination.Usable` stays uncalled, because
>   calling it would change what a ranking IS. Recorded in the DoD below and in `architecture.md`'s gap list.
>
> This is step 7 of [PLAN_variant_matrix.md](../todo/PLAN_variant_matrix.md) §5, brought forward out of its order
> because §1 below is a defect rather than a missing feature.
>
> Related docs: [architecture.md](architecture.md) (*The measurement contract*, *The
> arbiter*), [MEASURED_LESSONS.md](MEASURED_LESSONS.md) (§the sweep that manufactured
> winners — the reason `ProofState` exists at all).

---

## 1. The goal, before any solution

**This harness can produce evidence and cannot read a comparison out of it.** `bench run` fills `results`,
`metrics`, `funnels` and `retrieved_hits`; the only thing any surface prints afterwards is two integers from
`ScoreboardAsync` — "N of M passed". Everything needed to turn those rows into the answer the project exists
to give is written, tested, and **called by nothing**:

| Written, tested, no caller in `src/` or `hosts/` | Where |
|---|---|
| `MetricByDimension` (a mean per dimension, WITH its leg count) | `src/Bench.Application/ResultStore.cs:10` |
| `AverageByEngineAsync` / `AverageByLaneAsync` | `src/Bench.Application/ResultStore.cs:91,94` |
| `SeedSplit.Proof` → `ProofState` (Confirmed · Unproven · Suspicious) | `src/Bench.Domain/Splitting/SeedSplit.cs:60,70` |
| `QuestionSpread`, `SaturatedAt`, `SpreadAcross` | `src/Bench.Domain/Runs/Discrimination.cs:13` |
| `Discrimination.Over` / `Discrimination.Usable`, `DiscriminationReport` | `src/Bench.Domain/Runs/Discrimination.cs:126,141,109` |

Verified 2026-08-19 by grepping each name across `src/` and `hosts/`: every hit is either the declaration
itself or a test.

**This is the third instance of one defect class, and the repository has already named the other two.** The
crash-recovery sweep was fully implemented and called by nothing until `LegDrain` wired it
(*What the drain survives* — "the audit finding this whole section exists to prevent repeating");
`ICheckoutProvider` was tested from the first commits with no caller while every run printed that its commit
was "recorded but unverified" and measured anyway. Both were found by an audit rather than by use. The
report is the same shape, and it is the most consequential of the three, because what it leaves unbuilt is
not a guard — it is **the product**: `research/architecture.md` opens by saying the output is *"a comparison,
not a pass or a fail"*, and no code assembles one.

The concrete consequence today: `SeedSplit` splits every suite into a selection half and a held-out half,
`bench plan` prints the assignment, `bench run` measures both — and **nothing ever compares them.** The guard
against the failure mode that cost this programme three reversed conclusions is fully built and has never
once been consulted.

## 2. What exists today, verified

| Fact | Where |
|---|---|
| A private group-by already generalises over a dimension selector | `src/Bench.Infrastructure/Persistence/PostgresResultStore.cs:163` — `AverageAsync(runId, metricName, Func<ResultRow,string>, ct)` |
| Booleans aggregate as 1/0; a metric with no numeric reading is excluded, never counted zero | same method, `:157-161` and `:178` |
| The two public averages are two `Func` arguments to it | `:115-121` — `Run.EngineKind` and `Cell.LaneName` |
| `cells` already carries every dimension this report needs | `CellRow` in `src/Bench.Infrastructure/Persistence/BenchDbContext.cs:44` (`QuestionId`), `:53` (`SubjectModelId`), `:55` (`LaneName`), `:60` (`VariantId`, nullable — the control arm) — no schema change to group by any of them |
| The split half needs no column | `SeedSplit.Assign(suiteId, questionId)` is a `StableHash` bucket, deliberately derived from the suite **id** rather than its version |
| A run records which questions it froze, per group, as a snapshot | `run_questions` (`IRunQuestionStore.ForRunAsync`) |
| The renderer shape is settled | `hosts/Cli/PlanCommand.cs:55-79` — aligned `label   value`, `--json` short-circuit, `Fail(error, reason, code)` |
| The exit-code contract | `hosts/Cli/ExitCodes.cs` — `0` pass · `1` regression · `3` environment · `4` configuration · `5` no report |

## 3. The shape — decisions

### 3.1 The report is a use case; the CLI and the API render it

`RunReport` in `Bench.Application` returns a `RunReportView`, and both surfaces print it. This is the rule
`PlanCommand`/`PlanRequestHandler` already follow, and it is what makes the API a door onto the same answer
rather than a second implementation that agrees until somebody edits one.

The use case is the only place that decides anything: which dimensions are worth showing, what may be
ranked, what renders as *unproven*. A renderer that decides is a renderer whose decisions cannot be tested
without a process.

### 3.2 The dimensions become ONE method, not six

`IResultStore` gains

```csharp
Task<IReadOnlyList<MetricByDimension>> AverageByAsync(
    Guid runId, ReportDimension dimension, string metricName, SplitHalf? half, CancellationToken ct);
```

with `ReportDimension` = `Engine | Lane | Subject | Variant`, and `AverageByEngineAsync` /
`AverageByLaneAsync` are **removed** rather than kept as delegations. Six near-identical group-bys in one
port is the shape CLAUDE.md §5 refuses for `Outcome<T>`, for the same reason: the fifth one drifts.

The adapter already has the general form privately (`:163`), so this is exposing an existing seam, not a new
query. `PostgresResultStoreTests` has five call sites against the two removed methods; they move to the new
one and are the guard that the refactor changed no arithmetic. Deleting a tested public method is the part
that needs saying out loud, so: the behaviour is pinned by those five assertions before the signature moves,
and the same numbers are asserted after.

`half` is nullable because *"across the whole suite"* and *"on the selection half"* are both real questions,
and a magic third enum member meaning "don't filter" would put that distinction inside the dimension.

### 3.3 The split half is DERIVED, so this plan adds no migration

`AverageAsync` already materialises its rows before grouping, so the half is a predicate over
`Cell.QuestionId` evaluated where `SeedSplit.Assign` can be called. Storing the half would be a denormalised
copy of a pure function of two values already in the row — and the one thing that must never happen to it is
drifting from the function, because a split that re-assigns invisibly defeats itself (the reason
`SeedSplit` uses `StableHash` and not `GetHashCode`).

The suite id comes from the run, not from the caller: a half computed against the wrong suite id is a
silently different split.

### 3.4 A mean over two legs is not a ranking, and the report says which it has

`MetricByDimension.Legs` exists for exactly this, and its own doc comment states the requirement: *"a mean
over two legs and a mean over two hundred are different claims, and the report must be able to refuse to
rank the first."* Nothing currently reads it.

So the view carries, per dimension, either a ranking or a **refusal naming the count**. The floor is
`--min-legs` (default 2, matching *"n ≥ 2 to rank anything"* in the measurement tuple), and a dimension
below it renders its averages with the ranking withheld — never no output, because the numbers are real and
the operator is the one who decides whether to spend more repeats.

### 3.5 `ProofState` is the headline; the average is the supporting detail

For each dimension value other than the control arm, the report computes the metric on **both halves** and
asks `SeedSplit.Proof(wonOnSelection, wonOnHeldOut)`. What renders first is the verdict:

- `Confirmed` — won on the half that selected it and on the half that did not. A result.
- `Unproven` — won only where it was chosen. **Rendered as its own word, never as a smaller win**, because
  every false winner this programme produced landed here and read as a discovery.
- `Suspicious` — won only on the held-out half. Printed as odd, per the enum's own instruction: more likely
  a split artefact than a finding, and worth seeing rather than hiding.
- `NotAWinner` — omitted from the ranking, present in the table.

"Won" is decided against the run's control arm — the dimension value whose variant is `NotApplicable`, or,
when every cell names a variant, the baseline recipe. A run with no control arm gets a **comparison between
arms with no baseline stated**, and the report says so rather than nominating one silently.

A half with no legs at all is not a loss: it is `Unmeasured`, the same three-state discipline
`QuestionSpread.Unmeasured` already applies to a model that never attempted a question.

### 3.6 Discrimination is part of the report, not a separate verb

`Discrimination.Over(questions, models, minSpread)` needs a pass rate per question per subject, which is one
group-by the store does not have yet:

```csharp
Task<IReadOnlyList<QuestionPassRate>> PassRateByQuestionAndSubjectAsync(Guid runId, CancellationToken ct);
```

The report then prints `DiscriminationReport.Describe` and, when asked, the saturation label per tier.
Tiers arrive as data (`--tier <modelId>=<rank>`), never as an ordering in code — the constraint
`SaturatedAt` was written under.

**Nothing here retires a question.** `Discrimination`'s own doc comment is unusually emphatic that
discrimination is a property of a comparison rather than of a question, and that pruning what saturates the
strongest models deletes the range where cheaper models still differ. The report reports; it proposes no
deletions and offers no flag that would.

### 3.7 The aggregate stops hydrating the whole campaign — found while planning this

`AverageAsync` (`:170-175`) issues `.Include(r => r.Metrics…).Include(r => r.Cell!).ThenInclude(c => c.Run!)`
and then `.ToListAsync()`, materialising full `ResultRow` entities — **every `Prompt`, every `Answer`, every
`ThinkingText` and every `ResponseMetaJson` of the run**, in order to average one number.

This is the defect whose diagnosis is already written down twelve lines above the method that has it, on
`ScoreboardAsync` (`src/Bench.Application/ResultStore.cs:75-82`): *"every prompt, every answer and every
metric with its metadata crossed the wire and was deserialized so a finished campaign could print 'N of M
passed'. At the tens of thousands of cells this schema targets, that is the whole run pulled into memory to
render one line."* `TotalsAsync` carries the same diagnosis a third time. The lesson has been recorded twice
and the query that repeats it was never re-read.

It has not hurt yet because nothing calls it. Fixing it while wiring the first caller is the cheap moment;
after the first ten-thousand-cell campaign it is a report that OOMs, and the operator will read that as the
store being too small.

The fix is a projection: select the dimension key, the question id and the metric's raw value before
`ToListAsync`, so the rows that cross the wire are three small columns. The projection must keep the
`AsNumber()` exclusion rule — *not a number* stays excluded rather than becoming zero — which means the raw
metric value travels and the parse still happens here.

**Corrected 2026-08-19, before the build: it IS directly assertable, and this plan was wrong to say
otherwise.** The claim here was that a query's hydration cannot be checked through EF without reading
generated SQL. It can, and the technique was already in the file the fix lands beside:
`PostgresResultStoreTests.Recording(sql)` builds a context that keeps every statement it issues, and
`The_run_summary_does_not_hydrate_the_run_to_count_it` uses it to assert that no statement contains
`"Prompt"` — with a comment explaining that timing would prove nothing, because the defect is invisible at
three rows and fatal at fifty thousand. Reaching for a caveat before searching for the neighbour that
already solved it is the failure `reuse-first` is about.

So the fix ships with a real RED test,
`The_average_does_not_hydrate_every_prompt_of_the_run_to_compute_one_mean`, asserting the same thing about
`"Prompt"`, `"Answer"` and `"ResponseMetaJson"`.

### 3.8 The host, and what it must not become

`hosts/Api` — a minimal `WebApplication` that calls `AddBenchLogging`, maps `MapBenchApi`, and is registered
in the AppHost with the existing `bench` database reference. Three properties:

- **It is read-only.** `bench run` stays the only verb that reaches a model or spends money, and it stays a
  command an agent runs rather than a service an orchestrator supervises — the AppHost's own comment says
  why. The API host serves reports and the `plan` computation; it starts no run. When a start button exists
  it is step 9's `BenchRunWorker`, gated on the accelerator lease, and it is not this plan.
- **Its logging is the shared one.** `BenchLogging` per the family's Serilog rule — coloured console, one
  file per run under `logs/{yyyy-MM-dd}/`, retention owned at startup. No second copy of a logging decision.
- **It applies no migrations.** Two processes racing `Migrate()` against one database is a defect the CLI
  already owns; the API assumes a schema and fails by name if it is behind.

Routes, all `GET`, all under the existing `/api` group:

| Route | Answers |
|---|---|
| `/api/runs` | the runs this store holds, newest first — an operator cannot report on an id they cannot find |
| `/api/runs/{id}/report?metric=&minLegs=&minSpread=` | the `RunReportView`, the same object the CLI renders |
| `/api/runs/{id}/scoreboard` | the two integers, for a poll that must stay cheap |

`Bench.Contracts` gains the DTOs. It holds two files today and covers only `plan`; that is not an oversight
to preserve.

### 3.9 What this plan deliberately does not do

- **No UI.** Step 8 of the sibling plan, and it renders these endpoints — which is why they come first.
- **No cross-RUN comparison.** Every query here is scoped to one run id. Comparing two runs is only legal
  inside one `ComparisonScope` (target + suite), and enforcing that is its own piece of work with its own
  refusals; a report that quietly compared across scopes would be the exact failure the scope type exists
  to prevent.
- **No new metric.** The report reads what `AnswerScoring`, `RetrievalScoring` and the arbiters already
  wrote. A report that computes its own number is a second scorer.
- **No bar.** `bench report` exits `0` for a bad score, like `bench run`. There is still no agreed
  threshold, and an agent that reads "the subject answered badly" as "the harness is broken" keeps
  reporting the wrong news.

## 4. Build order

Each step ships alone with tests green before the next starts.

1. ~~**The store, generalised and projected.**~~ **IMPLEMENTED 2026-08-19.** `ReportDimension`,
   `QuestionScope`, `QuestionPassRate`, `AverageByAsync`, `PassRateByQuestionAndSubjectAsync`, the §3.7
   projection, the two removed methods and their five moved assertions. `PostgresResultStoreTests` 16/16,
   whole suite 777 of 778 (the one failure is another session's in-flight `ReviewRules` message, not this
   work). Deviations, all recorded above where they belong:
   - The half arrives as `QuestionScope`, a closed pair, rather than the `SplitHalf?` §3.2 proposed — the
     store stays a query and the split stays a decision in the application, and the ids make it a `WHERE`
     over a short list instead of a fold over every leg (§3.3).
   - All four dimension keys travel in the projection rather than one selected server-side: rendering an
     enum to text in SQL is at the mercy of the provider's translation, and a silent fall back to client
     evaluation is the defect `ScoreboardAsync`'s own comment refuses.
   - The variant key is `VariantSelectionCodec.Decode(...).Canonical`, reused rather than reimplemented, so
     the control arm reads `-` in a report exactly as it does in a leg identity.
   - §3.7's "not assertable" caveat was wrong and is corrected in place.
2. ~~**`RunReport` + `RunReportView`.**~~ **IMPLEMENTED 2026-08-19.** Dimensions, halves, `ProofState`, the
   min-legs refusal, the discrimination block, the control-arm rule and its "no baseline stated" case.
   `RunReportTests` 11/11 in 0.55 s against scripted stores, no container — the house `ScriptedLegs`
   pattern. Whole suite 795 of 796 (the one failure remains another session's in-flight `ReviewRules`
   message). Deviations:
   - **`Suite.IdOf` had to exist, and the plan had not noticed why.** A run stores the suite STAMP
     (`s@v3#abcdef012345`), while `SeedSplit` assigns a half from the suite **id** — deliberately, so a new
     frozen version cannot reshuffle the halves under a comparison spanning versions. Splitting on the
     stamp would therefore re-assign every question at every freeze, which is the exact defect
     `SeedSplit`'s use of `StableHash` exists to prevent. The decomposition lives beside `Suite.Stamp`, the
     one place that already knows the format, and `SuiteFreezeTests` pins that two versions of one suite
     yield one id.
   - **`IRunStore.QuestionIdsAsync` is new** — a `DISTINCT` over `cells.QuestionId`. Planned rather than
     measured questions, so a half whose legs all crashed reads as *measured nothing* instead of as a half
     that never existed; from the cells rather than the bank snapshot, because a file-frozen run writes no
     `run_questions` rows at all. Still no migration.
   - **`HalfReading` is a new type**, not `Captured`/`CapturedCount` reused: neither carries a mean with the
     leg count behind it. It repeats their discipline — a half nobody ran and a half scored zero are
     different states — rather than the type.
   - **A one-sided-split warning was added**, which this plan did not list. Found by writing the test: the
     split is the report's own reading of the run's questions, so a run whose questions all land on one side
     produces an empty half — and with no half that did not choose, *nothing can ever be confirmed*. A
     report that stayed silent there would read as a clean result.
3. ~~**`bench report`.**~~ **IMPLEMENTED 2026-08-19.** Renderer only, following `PlanCommand`; `--json`
   emits the view verbatim with enums as NAMES. `ReportCommandTests` 10/10, whole suite 805 of 806 (the
   one failure is still another session's in-flight `ReviewRules`). Deviations:
   - **The exit-code contract had a hole, and a test found it.** A missing `--metric` exited `3`
     (environment) because every `RunReport` refusal was mapped there — collapsing the one distinction the
     contract rests on: *you asked wrongly* against *what you asked about is not here*. The surface now
     validates its own invocation (`4`) and treats anything the use case refuses as `3`. The phrase is not
     duplicated: `RunReport.NoMetricNamed` is a public constant consumed by both, the shape
     `ClaimRefusal.NoPendingCell` already set for exactly this reason.
   - **`CommandLine.Double` is new**, parsed with `InvariantCulture` — `--min-spread 0.25` must mean a
     quarter on a machine whose decimal separator is a comma, or one command produces two comparisons
     depending on where it ran.
   - **This verb does not migrate**, alone among the database verbs. Migrating from a report would create
     an empty schema and then answer "no run", which reads as *your id is wrong* when the truth is *this is
     the wrong database*.
   - **The scripted stores were extracted** to `tests/Bench.Tests/Application/ScriptedStores.cs` and are
     shared by the use-case and CLI tests. A second pair would let the two surfaces be tested against two
     different ideas of what the store answers, when the whole point of `RunReport` is that both render one
     object.
4. ~~**Contracts + routes.**~~ **IMPLEMENTED 2026-08-19.** `RunReportDto` and its parts in
   `Bench.Contracts`, `RunReportContract` mapping in `Bench.Application`, and the three `GET`s.
   `BenchApiTests` 4/4, no host: the report route is a NAMED method rather than a lambda, so the rule in
   it is assertable while the route stays one line of wiring. Deviations:
   - **`--json` now emits the CONTRACT, not the view.** The plan expected a test that the two agree;
     making them one object is stronger, and it is what `RunPlanDto`'s own comment already demanded — *an
     agent reading the CLI and a browser reading the API never see different truths*. The typed view stays
     for the human rendering, where a `switch` over `ProofState` is a compiler error when a member is added
     and a `switch` over a string is not.
   - **`IRunStore.RecentAsync` is new**, for `/api/runs`. A listing, emphatically not a "current run": an
     operator who cannot find an id cannot report on it, and the port's own comment records what resolving
     an absent id to the newest row once cost.
   - `400` against `404` is the HTTP spelling of the CLI's `4` against `3`, asserted.
5. ~~**`hosts/Api` + AppHost registration.**~~ **IMPLEMENTED 2026-08-19.** `bench-api`, registered in the
   AppHost with the database referenced and waited for. Read-only, applies no migrations, refuses without
   a connection string. Smoked live: `/api/health` → `{"status":"ok"}`; a report with no `metric` → **400**
   carrying the shared phrase; `/api/runs` against an unreachable database → **500** in 3.3 s with the
   error logged; no connection string → exit **4**, the fatal reaching both the coloured console and
   `logs/2026-08-19/bench-api-…log`. Deviation: the no-database refusal moved INSIDE the `try`, because an
   early return past the `finally` skips `CloseAndFlush` — it survived only because this file sink happens
   to be synchronous.

Steps 1–3 are the value; 4–5 are what keeps step 8 from starting with a UI over nothing.

## 5. Test plan

xUnit v3 executable, never `dotnet test`. `PostgresFixture` for step 1; steps 2–4 need no container.

| What | How |
|---|---|
| The refactor changed no arithmetic | The five existing `PostgresResultStoreTests` assertions, moved to `AverageByAsync`, asserting the same numbers |
| A half is filtered by the SUITE's split | `An_average_on_the_selection_half_excludes_every_held_out_question` — seeded so the two halves have deliberately different means, and the wrong-suite-id variant produces a different set |
| **Unproven never renders as a win** | `A_variant_that_won_only_where_it_was_chosen_is_unproven_and_not_ranked` — the direct regression of the lesson `SeedSplit` exists for |
| Suspicious is visible | `A_variant_that_won_only_on_the_held_out_half_is_reported_as_suspicious_rather_than_hidden` |
| An unmeasured half is not a loss | `A_dimension_with_no_legs_on_one_half_is_unmeasured_rather_than_beaten` |
| A thin mean is not a ranking | `A_dimension_with_fewer_legs_than_the_floor_prints_its_average_and_withholds_the_ranking` |
| No baseline is stated, not invented | `A_run_with_no_control_arm_reports_arms_without_nominating_a_baseline` |
| *Not a number* stays excluded after the projection | `A_metric_with_no_numeric_reading_is_excluded_from_the_mean_rather_than_counted_as_zero` — carried through the §3.7 rewrite, since the projection is where it would be lost |
| Discrimination counts what it says | `A_question_every_subject_passes_is_trivial_here_and_never_unmeasured` |
| The report never proposes a deletion | `No_report_output_recommends_retiring_a_question` — a shape assertion, because the pressure to add that flag is exactly what `Discrimination`'s comment argues against |
| The API and the CLI agree | `The_json_the_CLI_prints_is_the_object_the_endpoint_returns` — one use case, asserted rather than assumed |
| Exit codes | `A_run_with_no_legs_exits_NoReport` · `A_low_score_exits_Pass` · `An_unknown_run_id_exits_Environment` |
| The layering guard | `ArchitectureTests` unchanged — `hosts/Api` may reference `Bench.Infrastructure`; `Bench.Api` may not |

Every defect found during the build gets its RED test first, watched failing for the real symptom.

## 6. Definition of Done

- [ ] `dotnet build dew_flow_benchmark.slnx -c Release` — 0 warnings.
- [ ] The test executable is green; every table above has a named test.
- [ ] `bench report --db … --run <id>` prints a comparison whose winners carry a `ProofState`, and
      `--json` emits the same object the endpoint returns.
- [ ] `AverageByEngineAsync` / `AverageByLaneAsync` are gone, and no group-by in the port is a near-copy of
      another.
- [ ] No aggregate query materialises `Prompt`, `Answer` or `ResponseMetaJson`.
- [x] `SeedSplit.Proof` and `Discrimination.Over` have callers in `src/`.
- [ ] ~~`Discrimination.Usable` has a caller.~~ **Corrected 2026-08-19: this line was over-reach and is
      withdrawn rather than ticked.** `Usable` returns the questions a report *may rank on* — and calling it
      would change what a ranking IS, from "the mean over this run's questions" to "the mean over the ones
      that separate these subjects". That is a real decision about the measurement, not a wiring gap, and
      §3.6 never proposed it: the design prints `DiscriminationReport.Describe` beside the ranking so the
      reader can see how many questions voted for nobody, and leaves the ranking over everything measured.
      Restricting it is worth doing deliberately, with its own argument, or not at all — so it stays
      uncalled and is named here instead of being wired in to satisfy a checklist.
- [ ] `hosts/Api` is registered in the AppHost, serves the three routes, applies no migrations, and starts
      no run.
- [ ] `research/architecture.md` records the report as existing, and its *What does NOT exist yet* list no
      longer omits it. **BLOCKED 2026-08-19: another session holds that file uncommitted**, and the same
      applies to CLAUDE.md's project table, which has no `hosts/Api` row. This is the one thing standing
      between this plan and `research/`. Do it, then promote.
- [ ] No migration was added by this plan.

## 7. Open questions

1. **Which metric is the default?** `--metric` has no obvious default: `Anchor recall` is the retrieval
   answer and means nothing for the control arm, and a per-lane default would be a decision hidden in a
   flag. Proposal: **no default — `--metric` is required**, and the error lists the metric names this run
   actually holds. An operator who cannot name the metric does not yet have a question.
2. **Does a `Confirmed` winner need a minimum margin?** `Discrimination.DefaultMinSpread` is 0.25 for a
   question's spread; nothing analogous exists for a dimension's win. Winning by 0.001 on both halves is
   `Confirmed` under §3.5 and is noise. Proposal: report the margin beside the verdict and add no threshold
   in this plan — a floor nobody has measured is a quality claim, and this repository's rule is that those
   are refused rather than guessed.
