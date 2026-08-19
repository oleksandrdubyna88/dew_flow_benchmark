# dew_flow_benchmark

Measure **any code repository, at any commit, through any retrieval engine** — and get an answer that
survives being asked ten thousand times.

```bash
dotnet build dew_flow_benchmark.slnx -c Release
./tests/Bench.Tests/bin/Release/net10.0/Bench.Tests

./hosts/Cli/bin/Release/net10.0/bench plan \
  --repo https://github.com/App-vNext/Polly.git \
  --commit a603169f460df708206ecf907096848f584c9003 \
  --suite-file samples/polly-smoke-suite.json \
  --subjects qwen3-coder@local --lanes native --repeats 2 \
  --engine NoRetrieval --exclude "**/*.md"
```

```
target   https://github.com/App-vNext/Polly.git@a603169f…[**/*.md]
suite    polly-smoke@v1#1923a90239c4  (3 question(s))
engine   NoRetrieval|||
matrix   3 x 2 repeat(s) x 1 leg(s) = 6 cell(s)
first    qwen3-coder|t=0,s=1@native=6
split    retry-jitter-formula=Selection, circuit-open-condition=HeldOut, timeout-vs-caller-cancellation=HeldOut
warn     the engine reports no index fingerprint — results cannot later be attributed to the index that served them
```

That is a real suite against a real tree nobody here wrote, with every anchor verified at the pinned
commit — see [samples/README.md](samples/README.md) for the target, the verification and why each
question resists being answered from memory. It is three questions: enough to exercise every link,
far too few to rank anything.

## Why it is shaped this way

Most of what looks like ceremony here is a guard against something that has already gone wrong in a real
measurement programme, and each one cost a wrong number to learn. The catalogue is
[research/MEASURED_LESSONS.md](research/MEASURED_LESSONS.md); the four that shape the API surface:

- **A sweep manufactures winners.** Three configurations were chosen on convincing evidence and reversed
  by a wider check. So a suite splits into a selection half and a held-out half, and a configuration
  that won only where it was chosen renders as *unproven* — not as a result.
- **A measurement is only valid against the corpus it ran on.** An entire series reverted to hypothesis
  when the corpus was rebuilt underneath it. So the target is `(repoUrl, commitSha, exclusions)`, and
  ground truth is scoped to a commit: carrying an anchor forward is an explicit *re-target*, never a
  silent reuse.
- **Ranking cannot fix what admission never let in.** Nine of ten queries had their target absent from
  the entire candidate pool. So the trace port ships in two modes, and the white-box one carries the
  retrieval funnel as a by-product of every run.
- **An unset setting is not a default.** An empty model id once resolved to a paid cloud model inside an
  arm labelled "local, $0". So unset is a refusal, enforced by the type.

## Layout

| | |
|---|---|
| `src/Bench.Domain` | the measurement contract — depends on nothing, and a test enforces it |
| `src/Bench.Contracts` | wire shapes, shared by every surface |
| `src/Bench.Application` | use cases and ports |
| `src/Bench.Infrastructure` | adapters |
| `src/Bench.Api` | minimal-API group over the same use cases |
| `hosts/Cli` | `bench` — the first surface, and the one an agent drives |
| `hosts/Api` | `bench-api` — the read surface. It serves reports and starts nothing |
| `tests/Bench.Tests` | xUnit v3 on Microsoft Testing Platform, incl. the architecture guard |

The CLI's exit codes are a contract, because its first consumer is an agent: `0` pass · `1` a real
regression · `3` environment · `4` configuration · `5` no report. "The answer is bad" must never look
like "the harness could not start".

## Status

**A single-shot retrieval comparison runs end to end.** A cell claims, checks the target out read-only, asks
the engine for hits, assembles the prompt it will store, asks the model, scores the answer and the retrieval
against the question's anchors, and settles — with one wall budget per leg, a crash-recovery sweep, and
retention on the one table that grows. Beside it: an immutable variant catalog, a Postgres question bank whose
groups and reviewers are rows, a model registry that stores the NAME of an environment variable rather than a
key, a re-scoring arbiter that never re-runs a leg, telemetry ingest from a spool, and the layering guard.

**And the run can be read back as a comparison.** `bench report` and `bench-api` answer with the same object:
the metric along four axes, each arm on BOTH halves of the split, and a verdict — a configuration that won
only where it was chosen renders **UNPROVEN**, in that word, rather than as a smaller win
([research/PLAN_run_report.md](research/PLAN_run_report.md)).

Not yet built, and each has an owner in `todo/`:

| | |
|---|---|
| the tool-calling loop — the lane where the SUBJECT decides what to search | [todo/PLAN_tool_benchmark.md](todo/PLAN_tool_benchmark.md) |
| the corpus axes (chunk size, embed model), and triggering an index pass | [todo/PLAN_variant_matrix.md](todo/PLAN_variant_matrix.md) steps 5–6 |
| an engine that actually REPORTS which compute backend it served on — this side reads, compares and stores one, and no engine sends it yet | [todo/PLAN_compute_backend_axis.md](todo/PLAN_compute_backend_axis.md) |
| the hardware sampler, a cloud model runtime, and the UI | [todo/PLAN_rag_bench_repo.md](todo/PLAN_rag_bench_repo.md) |

And the bottleneck is not code: the bank holds questions in **two of its six groups**, so a comparison over it
today is a comparison about two groups ([todo/PLAN_question_bank_coverage.md](todo/PLAN_question_bank_coverage.md)).
Authoring is the one axis nothing else compensates for — running is cheap.

The founding plan and its build order are [todo/PLAN_rag_bench_repo.md](todo/PLAN_rag_bench_repo.md); what
exists in detail, including its own list of what does not, is
[research/architecture.md](research/architecture.md).

Conventions for contributors — including *never `dotnet test`* — are in [CLAUDE.md](CLAUDE.md).
