# PLAN — the code lane: tasks a model must SOLVE, and how a solution is scored

> Status: **plan only, nothing implemented yet, 2026-08-16.** Scope: `Bench.Domain` (task kinds,
> scoring), `Bench.Application` (phases, the authoring pass, the delivered-work protocol),
> `Bench.Infrastructure` (the sandbox executor, diff metrics), `Bench.Api` + `Bench.Ui` (the code
> group's pages), `hosts/Cli`.
>
> Sits beside [PLAN_variant_matrix.md](PLAN_variant_matrix.md) — the matrix supplies the axes
> (variant × subject × lane × repeat) and the console; this plan supplies **group 6** of the question
> bank and everything a code task needs that a reading question does not.
>
> Builds on [PLAN_rag_bench_repo.md](PLAN_rag_bench_repo.md) §4b (the fix lane, already specified) and
> [../research/MEASURED_LESSONS.md](../research/MEASURED_LESSONS.md).

## 1. The goal, before any solution

Reading saturates — measured against `dotnet/aspnetcore`, Opus scored 89–100 % on four hard reading
sets against a discrimination threshold of ≤80 % (§4b). The discriminating task is **producing code**:

- **fix a real bug** — an open issue at a commit where the bug is verified live;
- **implement a stated TODO / small feature** — no pre-existing bug, a requirement to satisfy.

For each we must: collect what the model produced, prove it **compiles**, prove it **does what was
asked**, and judge **how well** — then compare that across models, lanes (no tools · retrieval ·
agent+MCP) and retrieval variants, so a claim like *"with our MCP the quality is better"* is a measured
number rather than an impression.

## 2. What already exists, verified

| Capability | State | Where |
|---|---|---|
| `TaskKind.Fix`, phases `Investigate → Fix → Verify → Judge` | **built, unused** — `LegRunner` never starts a phase | `src/Bench.Domain/Runs/LegPhase.cs` (`PhasePlan.For`), `src/Bench.Application/LegRunner.cs:44-137` |
| Phase discipline: no phase starts before the previous is `Done`; a cap or crash stops the **leg** | built | `LegPhase.cs` |
| Budgets per phase (`CostUsd`, `Wall`, `Turns`, `Context`), `CapExceeded` as a distinct outcome | built | `src/Bench.Domain/Runs/Budgets.cs` |
| Read-only checkout: bare mirror + worktree per commit | built, unwired | `src/Bench.Infrastructure/Git/GitCheckoutProvider.cs:37-135` |
| Process launching as exe + argv with a timeout | built | `src/Bench.Infrastructure/Process/ProcessRunner.cs` |
| Authoring domain: candidate → review → promote, source/author/seed provenance | built, unwired | `src/Bench.Domain/Authoring/QuestionCandidate.cs` |
| Metric-as-row storage, judge series per arbiter | built | `metrics` table, `src/Application/JudgeRunner.cs` |
| The four mechanical signals, specified | plan only | `PLAN_rag_bench_repo.md:164-177` |

**Nothing here needs new invention at the domain level.** The missing pieces are execution (a sandbox
that builds and tests), authoring (hidden tests written before the run), and the quality measure.

## 3. The task, and how it is authored

### 3.1 A code task is a bank question with `TaskKind = Code`

`bank_questions` (PLAN_variant_matrix §3.3) gains `TaskKind (Reading | Code)` and, for code tasks, a
`CodeTaskJson` payload:

```json
{
  "kind": "fix | implement",
  "statement": "what the solver is told — an issue body, or the TODO and its acceptance criteria",
  "baseCommit": "40-hex, the commit where the bug reproduces / the TODO is open",
  "scope": { "projects": ["src/Foo/Foo.csproj"], "touchHint": ["src/Foo/Bar.cs"] },
  "build":  { "command": "dotnet build …", "timeoutSeconds": 900 },
  "tests":  { "command": "…Tests.exe", "neighbourFilter": "…", "timeoutSeconds": 900 },
  "hiddenTests": { "files": [{ "path": "…", "contentRef": "blob-sha" }], "filter": "…" },
  "referenceFix": { "diffRef": "blob-sha" }
}
```

`hiddenTests` and `referenceFix` are **held outside the corpus** — never in the worktree the solver
sees, never in the retrieval index. A solver that can read the tests is measuring nothing.

### 3.2 Authoring is a model's job, and the reviewers write the tests

Per §4b and the operator's requirement ("before the run the judges must write the tests"):

1. A **strong authoring model** (chosen from the model registry, PLAN_variant_matrix §3.7) investigates
   the task at `baseCommit` and produces: the statement, the **hidden tests**, and a reference fix.
2. The authoring pass **proves the task is well-formed** by running the two traps §4b names, as gates:
   - **the bug must reproduce** — hidden tests are red at `baseCommit` (a bug secretly already fixed on
     HEAD is refused, with the reason);
   - **the reference fix must work** — hidden tests green with it applied, neighbours intact;
   - the checkout is **rebuilt to the buggy state** before any solving (the second measured trap).
   A task failing any gate never enters the bank — it is recorded as `Rejected` with the failure.
3. The **reviewers** (`claude`, `codex`, `gemini`, …) review the candidate exactly as for a reading
   question — the checkmark row is the same mechanism — and a reviewer may attach **additional hidden
   tests**, which is what "the judges write the tests" means concretely. Tests from different reviewers
   are kept separately attributed, so "whose test caught it" is answerable.
4. **The authoring model may not be a subject** on that task without being marked, the same way
   self-judging is marked today.

## 4. Solving: phases, and the sandbox

### 4.1 Phases

`PhasePlan.For(TaskKind.Code)` = **Investigate → Implement → Verify → Judge** (the built `Fix` plan
renamed to cover both kinds). Each carries its own budget; a cap or a crash stops the leg, not just the
phase, and settles as `CapExceeded` / `Crashed` — never as a wrong answer.

Lanes differ in what the solver may use, and that is the comparison the operator wants:

| lane | the solver gets |
|---|---|
| `no-tools` | the statement and nothing else |
| `rag-context` | the statement plus retrieved context from the variant's engine, single-shot |
| `agent-mcp` | a CLI agent with its native tools **plus our MCP server** over the worktree |

### 4.2 The sandbox executor (new infrastructure, and a security surface)

- **One worktree per leg**, created from the read-only bare mirror at `baseCommit`, deleted after —
  never the operator's working tree, never shared between legs (two legs of one matrix run concurrently
  by design).
- Build and test run as **exe + argv with a timeout**, per the family security rule; no shell string.
- **Network is denied to the build and test steps** where the platform allows it, and the decision is
  recorded per leg either way: a task whose tests reach the internet is not reproducible.
- Everything the executor observed is persisted: exit codes, stdout/stderr (size-capped with the cap
  recorded), durations, and the **byte size** of each — the operator's "how many megabytes of logs"
  is a measured field here, not an estimate.
- Timeouts are **phase-specific budgets**, never one shared token (the family timeout-hygiene rule).

## 5. Scoring — two independent instruments, never one

### 5.1 Mechanical signals (deterministic, primary)

Per §4b, plus the compile gate the operator asked for:

| # | signal | how |
|---|---|---|
| 0 | **it compiles** | the build command exits 0 at the produced diff |
| 1 | the diff lands in the right file(s) | anchors from `scope.touchHint` |
| 2 | the **hidden** tests go green | run after the solve, from outside the corpus |
| 3 | the solver's **own** test exists *and has teeth* | revert the solver's non-test changes → its test must go red |
| 4 | the neighbouring tests are intact | the filtered neighbour suite still green |

Signal 3 is what separates a fix from a plausible edit. Signal 0 is a gate, not a score: a solution that
does not compile scores zero on 1–4 and is recorded as `BuildFailed` with the compiler's first errors —
distinct from "wrong", because the two mean different things about a model.

### 5.2 Delivered work (LLM-judged, secondary, inflation-resistant)

Mechanical signals answer *did it work*, not *how much of what was needed landed, and at what cost in
code*. For that we port the protocol proven in the operator's own `scoreMeter` V2 — **the mechanism,
not the repository**, as one independent leaf module (`Bench.Delivered`): the full engineering plan is
[PLAN_scoremeter_port.md](PLAN_scoremeter_port.md), and the summary is:

- **Cleaned LOC.** Port `DiffCleaner` + `PathCategories` + `LocMetrics` (line-normalized churn over
  counted files) — small, pure, dependency-free. It is the denominator of every efficiency number:
  points per cleaned line, tokens per cleaned line, minutes per cleaned line.
- **Anchored step weighting with a ZERO band.** The diff is decomposed into steps; each is weighted
  0–10 against a published anchor scale; a coverage gate reports how much of the diff was accounted
  for. **The zero band is the load-bearing part**: measured over 9 arms (3 real PRs × ×1/×3.5/×10
  behaviour-neutral padding, $36.75 on pinned Opus), ten times the lines bought **×0.88** — an
  inflation exponent of **−0.06**, against 0.615 where the extra lines were real work. The band landed
  on **160 of 160 padded steps** and on 4 of 245 real ones, and all four were whitespace, an import
  reorder, a dead comment and a cosmetic inlining. Source: `scoreMeter · tools/diff-only-inflation/REPORT_ZERO.md`.
- **Deterministic corrections in code, after the model has spoken** — the `ScoringPolicy` shape: a
  near-duplicate cap and a rescue allowance, applied to the model's raw scores, recomputable over
  stored runs without paying for a call. Raw and applied scores are both persisted with the rule that
  changed them; the raw score is never overwritten.

**What is NOT ported, and why:**

- **Grain (`Σ grain^0.75`) is not taken as a quality measure.** The same report states it plainly:
  *"Σ grain still cannot tell padding from work."* Its α was fitted on that product's own pool. If it
  appears here at all it is a descriptive secondary number, never a headline.
- **The constants are not inherited as truth.** `RescueAllowancePerPrPoint = 2`, `NearDuplicateCap = 2`,
  α, and the anchor scale itself were fitted on a PHP/JavaScript production corpus over 223 runs. On
  C# tasks they are somebody else's fit. They are ported **with their provenance recorded** and marked
  *inherited, unverified* until re-measured here — and re-measuring them is a task in §7, not an
  assumption.
- **The repository is not copied.** Its Application layer is ~101 files of which the scoring core is
  ~8; the rest is ticket ingestion, billing, a key pool and a web product. A copy would be a fifth
  mirror to maintain. What lands here is bench-owned code citing its source as a path
  (`scoreMeter · src/ScoreMeter.Application/Pipeline/ScoringPolicy.cs`), per the family's
  cross-repository citation rule.

### 5.3 The instrument's own noise sets the repeat count

The honest arms in that study moved 51.7 → 48.6 across a protocol change — *"inside the instrument's
own 13.2 % sampling spread"*. **A judged score with ~13 % spread cannot resolve a 10 % difference
between two models in one run.** Consequences, stated before any measurement here:

- code-lane cells run `repeats ≥ 2` by default, and a headline comparison quotes the spread beside the
  difference;
- a difference inside the spread renders as **unproven**, exactly as the held-out split rule already
  renders a configuration that won only on its selecting half;
- the weigher runs at temperature 0 with a recorded seed, like the arbiter.

### 5.4 The arbiter still closes the leg

Over evidence mostly not its own opinion: was the diagnosis right, or was a symptom patched? Multiple
arbiters as ordered rows (PLAN_variant_matrix §3.7); self-judging marked, never silently excluded.

## 6. Comparing — what the operator asked to see

Every number above is stored per leg, so the comparisons are queries, not new pipelines:

- **model vs model** on one variant and lane;
- **lane vs lane** for one model — the *"with our MCP, better or worse?"* question, answered with the
  mechanical pass rate, the delivered-work score, cleaned LOC, wall time, tokens, and the tool-call
  counts telemetry already attributes to the leg;
- **variant vs variant** for one model — does better retrieval produce better code, or only faster;
- **per-group rollups** — code tasks report beside the five reading groups, never folded into them:
  a pass rate and a mean weighted score are different scales and are shown as such.

## 7. Build order

1. **Task kind + payload** — `TaskKind` on bank questions, `CodeTaskJson`, the `Code` phase plan, and
   `LegRunner` finally starting phases. No execution yet: phases record, budgets enforce.
2. **The sandbox executor** — worktree per leg, build + test as exe+argv with per-phase timeouts,
   captured output with sizes, cleanup on every path including failure.
3. **Mechanical signals 0–4** — including the teeth-proof (revert the solver's non-test changes, expect
   red), each stored as its own metric row.
4. **The authoring pass** — a model authors statement + hidden tests + reference fix; the three gates
   (reproduces / reference works / rebuilt to buggy state) refuse a malformed task by name; reviewers
   attach their own tests and their marks.
5. **The scoreMeter port, module + deterministic half** — the `Bench.Delivered` leaf: line family and
   policy ([PLAN_scoremeter_port.md](PLAN_scoremeter_port.md) §6 steps 1–2).
6. **The scoreMeter port, the weigher** — zero-band prompts, gate, `stage_payloads`, `bench rescore`
   (same plan, §6 steps 3–4).
7. **Re-calibration** — the inflation arm on this corpus
   ([PLAN_scoremeter_port.md](PLAN_scoremeter_port.md) §6 step 5). Until it lands, every delivered-work
   number renders the *inherited calibration* badge.
8. **Console** — the code group's pages: task detail (statement, phases, diff, build/test output),
   per-signal columns, the comparison views of §6.

## 8. Test plan

- Domain: phase transitions (no start before previous done; cap stops the leg), signal computation from
  fixture outputs, the teeth-proof logic, `ScoringPolicy` corrections against hand-built inputs.
- Infrastructure: sandbox executor over a temp git repo — build failure, test failure, timeout, cleanup
  after each; a leg may never write outside its worktree (asserted).
- Authoring gates: a task whose hidden tests are green at `baseCommit` is refused with that reason.
- Parity: ported cleaned-LOC figures reproduce the source product's published values for the same diffs
  (its own `test_churn.py` gate is the precedent — port the gate, not just the code).
- Every bug gets its RED test first.

## 9. Definition of Done

- [ ] A code task runs end to end: worktree at the pinned commit, phases with budgets, a produced diff,
      a build verdict, hidden tests, neighbour tests, the teeth-proof.
- [ ] A solution that does not compile is `BuildFailed` with compiler output — never scored as "wrong".
- [ ] Hidden tests and the reference fix are provably outside anything the solver can read.
- [ ] Reviewers' marks and their attached tests are attributed per reviewer.
- [ ] Delivered-work scores carry a protocol version, raw **and** applied values, and the rule that
      changed them; an inherited-calibration badge until §7.7 lands.
- [ ] A ×10 behaviour-neutral padding arm, run on this corpus, buys no more score than ×1 — the
      property re-verified here rather than assumed from the source product.
- [ ] Code-group results render beside the five reading groups without being averaged into them.
- [ ] The lane comparison answers *"with MCP: better or worse"* with the spread quoted beside the
      difference.

## 10. Open questions

1. **Target repository for code tasks.** `dotnet/aspnetcore` is indexed and has real issues, but its
   build and test cycle is long; a smaller C# repository may be the practical first corpus. Decided
   with a measured build+test wall time, not by preference.
2. **Network denial mechanism** on Windows for the test step — container, job object, or accepted and
   recorded as "not denied". Decided at step 2 against what the platform actually offers.
3. **Who authors at scale.** The authoring pass is a model job with gates, but throughput is unmeasured
   (the founding plan's open question 2). One well-formed code task may cost more than ten reading
   questions; the bank's code group will be smaller than 100 for a while, and that is fine.
4. **Anchor scale ownership.** The ported anchors are that product's published scale. Once re-measured
   here they become this repository's own, versioned like every other contract — and the two may then
   legitimately diverge.
