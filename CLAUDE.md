# Claude Code — Project Rules for dew_flow_benchmark

These rules apply to **all code in this repository** and override Claude's defaults.

## Project Overview

`dew_flow_benchmark` measures **any code repository, at any commit, through any retrieval engine**, and
is built so its answers survive being asked at the scale of thousands of tests. It is a .NET 10 solution
in the `dew_flow_*` family, alongside `dew_flow_rag_qln` (the retrieval product), `dew_flow_mcp` (the
public tool surface) and `dew_flow_sidecar_rust`.

**Read first:** [todo/PLAN_rag_bench_repo.md](todo/PLAN_rag_bench_repo.md) — the founding plan and the
build order. **Then:** [research/MEASURED_LESSONS.md](research/MEASURED_LESSONS.md) — the evidence base.
Most of the domain's shape is a guard against something in that document, and a change that looks like a
simplification usually removes one.

**The repository this project measures is not this repository, and the earlier DewFlow / `claudeRag`
monorepo is out of scope entirely** — it is never modified, and nothing here depends on it being checked
out.

## Commands

```bash
# Build
dotnet build dew_flow_benchmark.slnx -c Release

# Run tests — ALWAYS via the test project's executable, NEVER `dotnet test`
# (xUnit v3 / Microsoft Testing Platform: there is no VSTest testhost, so `dotnet test` aborts)
./tests/Bench.Tests/bin/Release/net10.0/Bench.Tests
./tests/Bench.Tests/bin/Release/net10.0/Bench.Tests --filter-method "*SomeTestName*"
./tests/Bench.Tests/bin/Release/net10.0/Bench.Tests --filter-class "*MatrixOrderTests"

# The CLI
# The variant catalog — retrieval configurations as rows, added and retired, never edited
./hosts/Cli/bin/Release/net10.0/bench variants add --name hybrid-rrf-256 --db "$BENCH_DB" \
  --engine qln --channels hybrid --fusion rrf --k 60 \
  --text-shape src --chunk-tokens 256 --embed-model bge-m3 --rerank-pool 50 --limit 20
./hosts/Cli/bin/Release/net10.0/bench variants list --all --db "$BENCH_DB"

./hosts/Cli/bin/Release/net10.0/bench plan \
  --repo https://github.com/org/repo.git --commit <40-hex> --suite-file samples/demo-suite.json \
  --subjects qwen@local,opus@cloud --lanes native,retrieval --repeats 3 [--json]

# The MCP telemetry spool's scheduled consumer (2026-08-19): the producers never prune — this
# repository owns the files' lifecycle, and a Task Scheduler job (dew_flow-telemetry-ingest, daily
# 03:30) runs ingest-then-prune. scripts/telemetry-ingest.ps1 is the action;
# scripts/register-telemetry-ingest.ps1 (re)registers it.
```

## Project Structure

| Project | Role |
|---------|------|
| `src/Bench.Domain` | The measurement contract: targets, suites, anchors, budgets, the matrix, the split, the trace shapes. **Depends on nothing** — an architecture test enforces it |
| `src/Bench.Contracts` | Wire shapes shared by every surface. Also depends on nothing: a contract that can reference the domain leaks it |
| `src/Bench.Application` | Use cases and **ports**. Every interface an adapter implements is declared here and nowhere else |
| `src/Bench.Infrastructure` | Adapters — store, git, engine clients, model runtimes, sampler |
| `src/Bench.Api` | Minimal-API group over the same use cases the CLI drives |
| `hosts/Cli` | `bench` — the first surface, and the one an agent drives |
| `tests/Bench.Tests` | xUnit v3 on Microsoft Testing Platform, including the architecture guard |

The layering is guarded by `ArchitectureTests` from the first commit, deliberately: the system this
replaces had no such guard and its coupling accumulated exactly where nothing was watching.

---

## 1. Language & Runtime

.NET 10, latest C#, `TreatWarningsAsErrors`. Use the newest syntax: primary constructors for DI,
collection expressions (`[]`, `[.. a, .. b]`), `required`, `field`, `await cts.CancelAsync()`,
`PeriodicTimer` over `System.Threading.Timer`.

## 2. Records for data, classes for services

`record` for every data container; `class` only for stateful services and types with identity.
Positional records for immutable values; `record class` with `init` for mutable config shapes.

## 3. No primitive obsession

A business concept does not travel as a `string` or an `int`. `CommitSha`, `RepoUrl`, `ModelRef`,
`SuiteVersion` are types with parsing and validation, and every one of them exists because an untyped
version of it went wrong somewhere. Parsing lives in a static factory returning `Outcome<T>` — never a
throwing constructor, because a malformed input is an expected answer.

## 4. No null in business logic

Return `[]`, `string.Empty`, or a typed empty value. **"Not captured" and "empty" are different facts
and must be different states** — the `Captured` record exists for exactly that, and rendering an unknown
as a zero is how a gap in instrumentation becomes a claim about the subject.

## 5. Expected failures are values, not exceptions

`Outcome<T>` (`Ok` / `Fail`) is the one shared shape; a closed record hierarchy (private constructor,
nested `sealed record`s) when a failure has several meaningful cases — `RetargetVerdict`, `LegOutcome`.
Never `throw` for control flow. Unexpected infrastructure failures still throw; catch them with the
exception as the **first** log argument, and rethrow unless this layer genuinely recovers.

Do not invent a per-call-site `(bool ok, T value, string reason)` record. That is `Outcome<T>` wearing a
disguise, and three of them in one file is how the previous system ended up with two parallel schemas.

## 6. Pure functions, small methods, small files

`private static` wherever a method reads no state. Cyclomatic complexity **≤ 4** — extract, or use a
switch expression. Files 200–400 lines typical, **800 max**; split by extracting a named unit with its
own responsibility, never a `partial` to duck the limit.

## 7. Immutability

Never mutate; return a new value. Public contracts expose `IReadOnlyList<T> { get; init; }` defaulting
to `[]`, never `List<T> { get; set; }`.

## 8. Tests

**Every feature ships with tests in the same task.** Every bug fix starts with a RED test that
reproduces the defect, watched failing for the *real* symptom before the fix — and if the fix landed
first, revert it, watch the test go red, restore it. Report both observations; "tests pass" is not
evidence.

A test's name states the guarantee (`First_position_is_balanced_across_the_whole_matrix_at_an_odd_repeat_count`),
never a ticket number. Where a test exists to pin a specific historical defect, reproduce the refuted
approach in the test so the defect has a shape — see `MatrixOrderTests`.

Full rule: [.claude/rules/shared/common/testing.md](.claude/rules/shared/common/testing.md).

## 9. Plans and documentation

`todo/` holds work not yet done; `research/` documents what exists. A plan carries a `> Status:` line on
line 2–3, the goal before any solution, verified `file.cs:line` references, a build order, a test plan
and a Definition of Done. When a plan's work lands, promote it with its **deviations** recorded — what
shipped differently is the most valuable part. Full rule:
[.claude/rules/shared/common/planning-docs.md](.claude/rules/shared/common/planning-docs.md).

**Cross-repository citations are paths, not links.** A relative link that resolves on one machine is
worse than a citation naming its source.

## 10. Dependencies

Central package management (`Directory.Packages.props`), monthly bump to the latest **stable**, never a
preview. Before adding any package, establish its **licence** and its maintenance state; a package from
a private individual always needs the operator's approval first. Two pins are load-bearing and
documented in place: **FluentAssertions stays at 7.2.2** (8.x moved to a non-commercial licence), and
Aspire SDK + Hosting packages move as a matched pair. Full rule:
[.claude/rules/shared/csharp/nuget-packages.md](.claude/rules/shared/csharp/nuget-packages.md).

## 11. Security

No hardcoded secrets, tokens or connection strings — environment variables or user-secrets. Validate at
every boundary. Launch external processes as **exe + argv**, never a shell string, always with a timeout;
this matters more here than in most projects, because the benchmark clones and checks out arbitrary
repositories at operator-supplied urls.

**Checkouts are read-only and never in a directory anyone works in** — a bare clone per url, a worktree
per commit. The equivalent component in the previous system ran `git checkout` in place on a configured
repository path, which for a benchmark means rewriting a developer's working tree to a commit they never
asked for.

## 12. User-facing text is English

Every string a user reads — CLI output, API messages, UI labels, commit messages, documentation — is
English. No mixed-language chrome.

---

## Definition of Done

- [ ] `dotnet build dew_flow_benchmark.slnx -c Release` — **0 warnings** (warnings are errors here).
- [ ] The test executable runs green; new behaviour has tests; a fix has a test that was watched failing.
- [ ] The architecture guard still passes — no layer reached where it must not.
- [ ] Any plan the work finished was promoted with its deviations recorded.
- [ ] A guard removed or weakened is explained against [research/MEASURED_LESSONS.md](research/MEASURED_LESSONS.md); most of them cost a wrong number to learn.
