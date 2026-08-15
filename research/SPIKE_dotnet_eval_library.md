# SPIKE — does `Microsoft.Extensions.AI.Evaluation` carry this benchmark's shape?

> Status: **RUN AND DECIDED, 2026-08-15.** Verdict: **adopt for scoring, reporting and result storage;
> keep our domain; implement `IEvaluationResultStore` over our own database rather than using the disk
> store.** The decision rule was written down before the spike started (below) and is met.
>
> Spike code: [spikes/EvalLibrarySpike/](../spikes/EvalLibrarySpike/) — deliberately outside
> `dew_flow_benchmark.slnx`, so CI neither builds it nor is slowed by it. No LLM was called: every
> criterion was exercised with deterministic evaluators, so the answer cost nothing and cannot vary
> between runs.

## Why the spike happened at all

Because it should have happened *first*. The reuse-first rule was applied inward — the repository copies
the shape of its `dew_flow_*` siblings — and never applied outward at the ecosystem. Roughly 60 % of what
the founding plan describes as work to be done already exists in published libraries, and finding that
out after writing a durable run store is a worse order than finding it out before. Recorded here rather
than smoothed over, because the cost was real and bounded: see *What this costs us* at the end.

The operator's constraint narrowed the field to one candidate: **no JavaScript, no Python.** That rules
out promptfoo, Ragas, DeepEval, Inspect and Langfuse's server, and leaves the `Microsoft.Extensions.AI`
family — published by Microsoft, stable at **10.9.0** (`.NLP` is preview-only and was not considered).

## The decision rule, written before the spike

> If criteria 1 and 4 pass, adopt it and delete the hand-rolled run store. If 4 fails — if their model
> cannot carry our measurement key — write our own, with the reason recorded.

## What was tested, and what happened

| # | criterion | verdict |
|---|---|---|
| 1 | Is "did the engine surface `src/Orders.cs#OrderService.Total`" expressible as a first-class evaluator? | **PASS** |
| 2 | Can a different judge re-score a stored answer without re-running the model? | **PASS** |
| 3 | Does the result store give resumption after a kill, and at what granularity? | **PASS**, per scenario+iteration |
| 4 | Can a scenario carry our dimensions — engine, lane, subject, target commit? | **PASS, with a caveat that decides the storage choice** |
| 5 | How much of the held-out split is there? | **NONE**, as expected — it is ours either way |

### 1 — anchor matching is a first-class evaluator

`IEvaluator.EvaluateAsync(messages, modelResponse, chatConfiguration, additionalContext, ct)` is built
around a chat exchange, and our ground truth is not a chat message — but `EvaluationContext` is designed
to be subclassed, and that is the seam. A `RetrievalContext` carrying hits and the expected anchor rides
in `additionalContext`; the evaluator emits a `NumericMetric` with an `EvaluationMetricInterpretation`
(`Failed = true` when the anchor is absent) and per-metric `Metadata`. Live output:

```
[1] metric name=Anchor recall value=1 failed=False
[4] metric metadata survived: {"anchor":"src/Cache.cs#ReadCache.Invalidate","hitCount":"1"}
```

Deterministic evaluators need **no `ChatConfiguration` at all** — `null` is accepted — so retrieval
scoring costs no tokens and sits in the same metric pipeline as the LLM-judged metrics. That is better
than the plan assumed: it had retrieval scoring and answer judging as separate machinery.

### 2 — re-scoring is structural, not a feature we must build

`ScenarioRunResult` **stores the subject's `ModelResponse`**. A second evaluator reads it and produces a
new metric with nothing re-inferred:

```
[2] the SUBJECT's answer is stored with the result: "the answer for cache-invalidation mentions tenant"
[2] re-scored stored answer with a different evaluator: Mentions tenant=True
```

The plan listed "re-judge stored answers without re-running the legs" as a requirement to implement.
It is a property of the storage model instead.

Note the caching in this library caches the **judge's** calls, not the subject's. That is the right way
round for us: the subject's answers are the expensive artefact and they are persisted as results.

### 3 — resumption is per scenario+iteration

`GetScenarioNamesAsync` / `GetIterationNamesAsync` list what has already been written, so resuming is
"skip what is present". Granularity is one leg. A leg interrupted mid-flight is lost and re-run, which is
the correct trade for legs measured in minutes.

### 4 — our key fits, but only as strings, and the disk store makes the fit worse

Their identity is three levels — **execution / scenario / iteration** — plus a flat `IList<string>` of
tags. Our key has six dimensions: target, engine, suite, subject, lane, repeat. Both encodings work:

```
[4] scenario names stored: cache-invalidation.qln.retrieval.qwen-local | order-total.noretrieval.native.opus-cloud | ...
[4] tags survived: engine:qln, lane:retrieval, subject:qwen@local, target:https://github.com/dotnet/aspnetcore.git@3f1acb59…
```

**The caveat, found by running it rather than by reading it.** The first attempt used `|` as the
separator and threw:

```
System.ArgumentException: The parameter 'ScenarioName' contains invalid path characters
  or directory traversal sequences. (Parameter 'ScenarioName')
   at …Storage.DiskBasedResultStore.WriteResultsAsync(…)
```

The scenario name is a **directory name**. So with the disk store the measurement key must be path-safe,
and "group by engine" means reading every result and parsing strings back out of a composite name — a
flattening that is lossy for querying and brittle for exactly the reason the domain refuses elsewhere:
parsing a display string back into structure works until someone changes the wording.

**This is what decides the architecture, and it decides it favourably:** `IEvaluationResultStore` is an
*interface*, and `DiskBasedResultStore` is one implementation of it. Implementing it over Postgres keeps
their evaluator/metric/report model **and** our six-dimensional typed key. The same is true of
`IEvaluationResponseCacheProvider`.

### Bonus — the report is free

`HtmlReportWriter` takes `IEnumerable<ScenarioRunResult>` and produces a self-contained report:

```
[bonus] HTML report written: 882 KB, no code of ours
```

That is most of build steps 9–10 of the plan, unwritten.

## What we adopt, and what stays ours

| adopt | keep ours |
|---|---|
| `IEvaluator` + metric model, incl. deterministic evaluators | target = `(repoUrl, commitSha, exclusions)` and the checkout cache |
| `EvaluationContext` as the carrier for retrieval hits and fix-task evidence | suite freeze/hash, commit-scoped anchors, re-target |
| `ScenarioRunResult` as the stored unit — it carries the subject's answer | engine port, and the white-box funnel contract |
| `HtmlReportWriter` / `JsonReportWriter` | the order plan, and **the selection/held-out split** |
| `IEvaluationResultStore` — the *interface*, implemented over our Postgres | hardware sampling and accelerator serialisation |

## What this costs us

The honest ledger, so the number is not vague:

- **`PostgresRunStore` and its claim/settle/sweep queue — about a day — is now largely redundant.** Their
  resumption model plus our decision to *serialise runs on the accelerator* means the real requirement
  was "continue after a crash", not "many workers racing on one queue". The domain lifecycle
  (`CellLifecycle`) survives as the state model; the multi-worker machinery does not need to.
- The EF entities and the migration stay useful — an `IEvaluationResultStore` over Postgres needs a
  schema anyway, and it will be close to this one.
- Everything else built so far — domain, checkout cache, CLI, order plan, split — is untouched by this
  decision.

## Follow-ups this spike opened

1. **`ScenarioRun` has no notion of phases.** The fix-task lane needs *investigate → fix → verify* with a
   budget each; their unit is one evaluation. Either three scenario runs sharing a prefix, or one run
   with our own phase records in context. Decide before building the fix lane.
2. **The scenario name is a path segment in the disk store.** Our Postgres implementation must not
   inherit that constraint by copying its key shape.
3. `.NLP` (BLEU/GLEU/F1) is preview-only, so it stays out under the no-previews policy. We do not need
   it: our expectations are anchor matching and, for fix tasks, test outcomes.
