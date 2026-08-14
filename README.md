# dew_flow_benchmark

Measure **any code repository, at any commit, through any retrieval engine** — and get an answer that
survives being asked ten thousand times.

```bash
dotnet build dew_flow_benchmark.slnx -c Release
./tests/Bench.Tests/bin/Release/net10.0/Bench.Tests

./hosts/Cli/bin/Release/net10.0/bench plan \
  --repo https://github.com/dotnet/aspnetcore.git \
  --commit 3f1acb59718cadf111a0a796681e3d3509bb3381 \
  --suite-file samples/demo-suite.json \
  --subjects qwen3-coder@local,claude-opus-5@cloud \
  --lanes native,retrieval --repeats 3 --exclude "research/**"
```

```
target   https://github.com/dotnet/aspnetcore.git@3f1acb59…[research/**]
suite    demo@v1#99f955867676  (3 question(s))
matrix   3 x 3 repeat(s) x 4 leg(s) = 36 cell(s)
first    qwen@native=3, qwen@retrieval=2, opus@native=2, opus@retrieval=2
split    order-total=Selection, cache-invalidation=HeldOut, startup-sweep=Selection
warn     claude-opus-5 is a billable cloud model — set per-phase and per-question cost ceilings
```

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
| `tests/Bench.Tests` | xUnit v3 on Microsoft Testing Platform, incl. the architecture guard |

The CLI's exit codes are a contract, because its first consumer is an agent: `0` pass · `1` a real
regression · `3` environment · `4` configuration · `5` no report. "The answer is bad" must never look
like "the harness could not start".

## Status

Early. The walking skeleton is in: the measurement contract, the order plan, the split, the CLI, and the
guards. Not yet built: the read-only checkout cache, the Postgres adapter, the engine clients, the
hardware sampler, the judge, the API host and the UI. The plan and its build order are
[todo/PLAN_rag_bench_repo.md](todo/PLAN_rag_bench_repo.md).

Conventions for contributors — including *never `dotnet test`* — are in [CLAUDE.md](CLAUDE.md).
