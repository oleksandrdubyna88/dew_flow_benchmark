# PLAN — the benchmark as its own product: engine-agnostic, commit-pinned, CLI first

> Status: **plan only, nothing implemented yet, 2026-08-14.** Scope: this repository, from empty. The
> product it measures is **not** modified — operator decision, 2026-08-14.
>
> Authored in the `claudeRag` repository and moved here the same day, replacing an earlier plan
> (`PLAN_rag_experiment_matrix.md`) whose build order targeted that repository and was closed unbuilt.
>
> **Every measurement quoted below is carried over into this repository** as
> [research/MEASURED_LESSONS.md](../research/MEASURED_LESSONS.md), section by section. The earlier
> programme ran against a different codebase (DewFlow / `claudeRag`); that repository is **out of scope
> and is not touched**, so nothing here requires it to be checked out. Citations point at the local
> document; its §8 records where the findings originally came from.

## 1. Goal

A benchmark that answers *"is configuration A better than configuration B"* about **any code repository at
any commit, measured by any retrieval engine**, and that can be trusted at the scale of thousands of tests.

Five operator decisions fix the shape:

1. **Greenfield.** Nothing is reused as code from the existing `v2.RagBench` / `v2.Eval` / `v2.Data`.
2. **The target is `(repository URL, commit sha)`.** The thing measured is pinned, not ambient.
3. **The engine is a parameter**, exactly like the repository. Ours is one value of it.
4. **CLI first, API alongside, UI last** — so an agent can drive it before a human can click it.
5. **Its own Postgres**, isolated from any product database.

Decision 2 is a win rather than a requirement. The most expensive lesson of the whole measurement history is
*"every configuration measured before 2026-08-04 was measured on a corpus that no longer exists… any of
those results is a hypothesis again, not a conclusion"* ([MEASURED_LESSONS §1](../research/MEASURED_LESSONS.md)).
A commit-pinned target makes that failure **structurally impossible** instead of policing it with a
fingerprint column.

## 2. What greenfield costs, and the only real mitigation

The risk is not the volume of code. It is that **lessons already paid for in measurement live in the old
code, not in prose**, and a fresh repository re-earns them at full price. Each row below cost a wrong number,
a lost session, or real money. They are carried as *specification*, not as files, and each becomes a test.

| lesson | what it cost | what this repository must do |
|---|---|---|
| Leg order must balance across the whole matrix | `repeatIndex % 2` alone is 2:1 at odd repeat counts (the old `BenchmarkOrderPlan`) | order planning is a domain concern with its own tests, not a loop index |
| A refused tool call ≠ an executed one | a read-only guarantee was asserted for months and was false; `is_error` carried the reason while the ledger read only the result's LENGTH | every tool observation records **outcome**, not just size |
| An engine can index its own answer keys | defect 9 of the v6 series; runtime path lists policed it | see §5.5 — the problem changes shape here, it does not disappear |
| `n = 1` is not a result | repeat spread reached 4 points — *the same size as the effect*; repeated control legs of one configuration diverged 65 % on input tokens | the report refuses to rank configurations whose repeats overlap |
| An unset model id is not "the local model" | an empty reranker `modelId` resolved to the SYSTEM DEFAULT `claude-opus-4-8` and would have sent ~100 reranks to a paid API inside a "$0 local" arm ([MEASURED_LESSONS §3](../research/MEASURED_LESSONS.md)) | unset is a **refusal**. Never a fallback, never a default |
| A budget knob that never arrives | `CompactAtTokens` is a local-tool-loop knob that reached no CLI arm, so a whole degradation was misattributed to a flooded context window | every budget records the runtime that **accepted** it; unverified ⇒ the run is marked, not scored |
| Sampling read back from settings is not evidence | Ollama's OpenAI-compatible route substitutes its own defaults over the Modelfile | temperature/seed are recorded **as sent**, from the request |
| A crash loses the request totally and invisibly | the rescore queue committed its whole pass in one `SaveChangesAsync` | persist-before-enqueue, claim/settle, startup sweep |
| "Newest run" is not a run selector | the old `RunStore.ResolveAsync` returned the newest run whatever its status and `--label` was silently dropped; ~14 evaluations overwrote each other in one session ([MEASURED_LESSONS §3](../research/MEASURED_LESSONS.md)) | there is **no implicit run selection**. A command either creates a run or names one |

## 3. The measurement contract

One result row is identified by, and can only be compared within, this tuple:

```
target   = (repoUrl, commitSha, exclusions[])
engine   = (kind, endpoint, engineVersion, indexFingerprint)
suite    = (suiteId, suiteVersion)          -- frozen, hashed
subject  = (modelConfigId, samplingAsSent)  -- 1..N per suite
lane     = (toolSurface, preamble)          -- 1..N per suite
repeat   = ordinal                          -- n >= 2 to rank anything
budgets  = (cost, wall, turns, context) x (per phase, per question)
```

**3.1 Ground truth is commit-scoped.** An expectation naming `Foo.cs:120` is true at one commit. Re-running
the same questions at a newer commit is the *point* — regression detection — but it is not free: a
**re-target** operation re-validates every expectation against the new tree and flags the ones whose anchor
moved or vanished. Silent reuse across commits is forbidden; it is the same class of error as the corpus
that no longer exists, one level down.

**3.2 A suite version is frozen and hashed.** The recorded failure: the measured v8 set exists only in
Postgres while its seed file on disk is still a version-4 ancestor. A version that can be edited in place is
not a version. Editing a frozen suite creates the next version; every result names the version hash that
produced it.

**3.3 Models, lanes and engines are sets, not fields.** A run is the materialised cross product
`question × repeat × subject × lane`. Adding a fifth lane or a third model is data, never a migration.

**3.4 A suite splits into a selection half and a held-out half, and the split is load-bearing.** At the
stated horizon — thousands of tests, sweeping configurations — the failure mode is not a wrong measurement
but a *right measurement of noise*, and it has already happened three times, each time convincingly:

| the sweep said | the check said |
|---|---|
| pool 20 is best — 32 matched, ΣMRR 1.628 against the shipped 50's 29 / 1.352  | the full set reversed it: **88/182 at pool 50 against 80/182 at pool 20**, `opq` 31 vs 25 |
| `SemanticAdmission=opaque` is "the first configuration that improves both sides", 60/107 | two days later on the re-described corpus: **64/182 against 88/182**, `opq` 31 → 14, reverted within the hour |
| the 28-cell signal × profile grid would separate the registers | predicted flat **before it ran**, and was |

Multiply the cells and the rate of false winners goes with them. The assignment is deterministic per question
(stable hash, never random, so a re-run assigns identically), every report carries both columns, and a
configuration that won only on the half that selected it renders as **unproven, not as a result**. This is a
different guard from `n ≥ 2`: repeats defend against variance within one configuration, the split defends
against choosing among many.

## 4. Engines, including the ones that are not ours

An engine is `(kind, endpoint, version)` behind one port. Four kinds exist from day one, and the fourth is
not a courtesy:

- **QLN** (`dew_flow_rag_qln`) — white-box capable.
- **Mindex** — black-box.
- **Any external HTTP retrieval service** — black-box.
- **No retrieval at all** — plain filesystem tools over the checkout. First-class, because the series'
  central comparison has always been *tools against no tools* and the measured answer stays uncomfortable:
  the native tool-set scored **36/63 against the full retrieval bridge's 37/63**, while retrieval cost 52 %
  more wall-clock ([MEASURED_LESSONS §4](../research/MEASURED_LESSONS.md)). If this is not an engine, the question
  stops being asked.

An engine **declares** its capabilities; the bench never assumes them. An engine claiming a trace-contract
version the bench does not know **degrades to black-box** rather than failing the run.

## 4b. Task kinds: reading is saturated, so the discriminator is FIXING

Measured 2026-08-15 against `dotnet/aspnetcore`, on a reading set built to be hard — chain depth,
precision-plus-rewrite, breadth across three implementations, and diagnosis from a symptom. Opus scored
**89–100 % on all four**, against a discrimination threshold of ≤80 %. The one miss was honest and small:
it named both DI files line by line and omitted only the first link of the chain.

**Reading, in any form including abduction, saturates.** A benchmark whose tasks a subject answers
perfectly measures nothing but its own ceiling, so the reading lane keeps its value as a regression guard
and stops being the interesting question. The discriminator is **fixing a real bug**.

### The fix lane

A task is a real open issue in the target repository, at a commit where the bug is **verified live**.

**Authoring is itself a model's job.** A stronger reader (Fable) investigates the bug and prepares
**hidden tests** — held outside the corpus so the solver cannot read them — and a reference fix. That
authoring pass is what proves the task is well-formed at all, and it is where the two traps below get
caught.

**Solving runs in three phases**, each with its own budget from §3: **investigate → fix → verify**. This
is the one place the adopted evaluation library does not reach — its unit is a single evaluation with no
notion of phases (see [SPIKE_dotnet_eval_library.md](../research/SPIKE_dotnet_eval_library.md), follow-up
1) — so the phase model is ours to build.

**An arbiter closes the leg**, over evidence that is mostly not its opinion.

### Scoring is deterministic, and that is the point

Unlike a reading answer, a fix is checkable by machine. Four signals, all mechanical:

1. the diff lands in the right file;
2. the **hidden** tests go green;
3. the solver's **own** test exists *and has teeth* — it goes red when the fix is reverted;
4. the neighbouring tests are intact.

Signal 3 is the one that separates a fix from a plausible edit, and it is the same teeth-proof discipline
this repository applies to its own tests. An arbiter is still worth having for the parts no assertion
covers — was the diagnosis right, or did it patch a symptom — but it grades a leg whose facts are
already established.

### Two traps, both hit during the pilot

**Rebuild to the buggy state before the solver starts.** The pilot restored the tree but the *binary* was
still the fixed one; a solver running `--no-build` would have seen a false red and "fixed" a bug that was
not there. The harness rebuilds and confirms the failure before handing the task over.

**A bug that is formally open may be half-fixed on HEAD already.** Issue #51132's intended fix turned out
to be implemented as a warning in commit `294cab2f9b`, six days before it was picked. The authoring pass
must verify the bug reproduces **at the pinned commit**, which is another argument for a commit-pinned
target over a model's weights: the issue tracker says one thing and the tree says another.

The cycle is cheap enough to iterate: rebuild ~7 s, tests ~90 ms.

### Pilot, already proven end to end

Issue #59854 — `NavigationManager.Refresh(forceReload)` silently discards its argument, `forceLoad: true`
hard-wired in the base implementation while all three overrides pass it through honestly. Hidden tests
red on the unpatched tree (2 of 4, `ForceLoad=true` where false was expected), a one-line reference fix,
4 of 4 green, 147 of 147 neighbours intact. That task is the pilot of the mechanism, not the bar; the next
charge is #52514, whose root is the ordering of two runtimes' document listeners across two codebases.

## 4c. Where ground truth comes from, and why nothing is ever deleted

Running is cheap and authoring is not: a machine gets through a thousand questions overnight, a person
writes one good one in half an hour. So authoring is the real bottleneck of the stated horizon, and the
answer is not one source but a **combination**, chosen per batch.

### Sources, combinable, chosen in the UI

| source | what it produces | why it is here |
|---|---|---|
| **Repository history** | a merged pull request: its description is the gold answer, its changed files are the anchors, decomposed into discrete facts | **the answer key is written by the project's own maintainers, not by us** — we cannot tune it to what our retrieval happens to be good at, and it scales with the repository's history rather than with our labour |
| **Bugs and tests** | a real issue plus the commit that fixed it | for a fix task this IS the ground truth: the correct answer is the actual fix |
| **Synthetic (self-retrieval)** | a question generated from a member's own description, checked to return that member | unlimited and free, but it measures the index against itself and cannot say whether a HUMAN's question would find it. A metric, not a benchmark — included because combining it with the others is cheap, never as a set on its own |
| **A person** | one authored question | the highest quality and the rarest case. Needs the same shape as the rest, with the human as the source |

They are **checkboxes, not a radio button**: a batch may draw from several at once. Which implies two
things the UI must handle rather than discover — **deduplication**, because two sources routinely produce
the same question about the same anchor, and a **review state**, because a source produces *candidates*
and a suite must contain nothing nobody vouched for. Candidate → accepted → frozen into a version.

### The authoring model is chosen, never assumed

For every source that needs a model, the UI offers **the available model list** — cloud and local alike —
and the choice is recorded with the batch. Two reasons it must be a choice and not a constant. An author
model only writes questions it can itself answer, so the set's ceiling becomes the author's ceiling; and
when author and subject share a family, the questions quietly inherit that family's blind spots.

Two properties are mandatory on every authored question, whatever the source:

- **the seed is newer than any plausible training cutoff** — otherwise the measurement is of memory, not of work;
- **a memorisation trap** — a question whose obvious answer is the memorised one and whose correct answer is not.

### Difficulty is LABELLED, never pruned

An earlier draft of this section proposed retiring questions that every subject answers — as carrying no
information. **That is wrong, and the reason matters more than the correction.**

Discrimination is not a property of a question. It is a property of a question *against a particular set
of subjects*. A question that Opus and Fable both answer is uninformative for separating Opus from Fable,
and may be the hardest item in the set for a local 7B. Pruning by what saturates the strongest models
deletes precisely the range in which cheaper and smaller models still differ — and that range is where
the commercially interesting question lives, given the measured finding that the cheap model on the cheap
engine bought ~90 % of the expensive result for ~20 % of the money
([MEASURED_LESSONS §4](../research/MEASURED_LESSONS.md)).

So nothing is deleted. Instead:

1. Each question accumulates a **measured pass rate per subject tier**, as runs happen.
2. From that comes a label — *"saturated at Opus and above"* — which is a statistic about the question,
   not a verdict on it.
3. **Discrimination is computed per comparison, not stored on the question.** When a report ranks two
   configurations it uses the questions that discriminate *within that comparison's own subject set*, and
   says how many those were. The same question is excluded from an Opus-vs-Fable comparison and central to
   a Haiku-vs-local one.

The set therefore only ever grows, and the labels are what make it usable at both ends of the range.

## 5. Metrics — two independent modules, one with two implementations

Both are **out-of-band**: sampled or received asynchronously, written in batches, breaker-guarded. Neither
may fail a run, and neither may sit in the measured path — a synchronous metrics write inside the search
path measures the instrument.

### 5.1 Hardware (`IHardwareSampler`)

A time series — GPU utilisation, VRAM, CPU, disk, wall — sampled at a fixed interval, stamped, joined to
`(run, question, phase)` by timestamp rather than by call boundaries.

**Runs serialize on the accelerator.** With N models and one card, concurrent runs make every hardware number
meaningless and every latency number a queue measurement. A run takes a lease; the wait is its own bucket
(§5.3), never thinking time.

### 5.2 Trace (`IRunTrace`) — two implementations behind one port

| mode | what it yields | works against |
|---|---|---|
| **black-box** | prompt sent · response returned · every tool call with arguments and outcome · per-call latency · token split (fresh / cache-read / cache-write) · cost | any engine, any runtime |
| **white-box** | the above **plus the retrieval funnel**: candidates per channel → fused → sent to reranker → returned → graph-enriched (and how) → assembled into context → what actually reached the model | engines implementing the versioned trace contract |

The funnel is the highest-value artefact here. Answering *"is this a recall failure or a ranking failure"*
once required a hand-built one-off probe, and the answer — the target absent from a **126–145 candidate pool
for 9 of 10 queries** ([MEASURED_LESSONS §2](../research/MEASURED_LESSONS.md)) — closed a whole class of proposed
fixes. Every run should produce it as a by-product.

Both implementations satisfy the same port, so a report renders identically and simply carries empty funnel
columns for a black-box engine. **"Not captured" is distinct from "empty"**: for some runtimes the raw
response text is unobtainable (a CLI stream keeps a result's size, not its text), and an unknown must never
render as a zero.

#### The contract ships before its first live emitter — under three conditions

The white-box contract is defined, ported and reported here **without waiting for an engine to implement
it** (see open question 3 for why no engine can yet).

**The condition this was originally justified by no longer holds, and saying so is the point.** The first
version of this section leaned on an existing emitter: the earlier engine's stage instrument already
produced `embed · dense · semantic · sparse · fuse · load`, plus rerank, graph, lease-wait, freshness and a
real wall-clock total, so the contract could be *derived from a captured payload* rather than imagined.
That engine is now out of scope and is not touched, which leaves the contract with **no live source to be
derived from**. It is therefore authored from the stage list in
[MEASURED_LESSONS §6](../research/MEASURED_LESSONS.md) and marked **provisional** until a real engine emits
it. That is a weaker footing than a captured payload and must not be quietly upgraded: the failure this
project has retracted more than once is *a claim about a system taken from a description of the system*.

Three conditions keep the interface honest, and they are not optional:

1. **The contract is versioned from v0 and its first version is explicitly provisional.** When the first
   engine emits a real funnel, the payload is compared against v0 and the difference is recorded as a
   deviation — not smoothed away. A provisional contract that silently becomes canonical is the same
   defect as an unfrozen suite version.

   > **This happened on 2026-08-15, and the condition paid for itself.** `dew_flow_rag_qln` emitted its
   > first funnel, and **five of the seven drafted stage names were wrong** — the draft was authored
   > from the stage list of an engine that had gone out of scope, which is a description of a system
   > rather than a payload from one. Where draft and emitter disagreed the **emitter won**: a contract
   > whose names no producer uses produces empty columns. Recorded in `TraceContract.Deviation` and
   > pinned by a test. The reconciliation also found three things the draft was missing outright —
   > per-stage time, an **independently measured total** (so the unattributed remainder is arithmetic
   > rather than trust — the draft structurally repeated the "sum of the stages we know about" failure
   > it was written to prevent), and `Absent[]` for stages an engine does not perform. One thing was
   > needed from the emitter in return: the funnel now stamps its `ContractVersion`, without which the
   > degrade-to-black-box rule has nothing to read.
2. **Two real implementations from day one.** Black-box (live) and white-box **replayed from a fixture**,
   the latter used by tests and by the report. An interface with one implementation proves nothing about
   its shape; two is the minimum tirage at which it is visible whether the abstraction has grown into one
   engine. Until a real payload exists the fixture is hand-authored and labelled as such.
3. **Keep the contract SPECIFIC.** The characteristic failure of an unimplemented interface is that it
   turns into a bag of key-value pairs "so any engine can fill it" — carrying no schema, no guarantees,
   and nothing a report can compute on. Named stages with typed counts, and an engine **declares** which
   stages it supports. Generality belongs in the capability declaration, never in the payload shape.

### 5.3 Time, in three buckets

Tool time · model thinking · infrastructure wait (accelerator lease, queue, cold start). Two buckets are not
enough: the existing stage instrument had to split `admit` — GPU-lease wait plus eviction — off from the rest
precisely because a busy card otherwise reads as a slow model.

### 5.4 Tool-call telemetry, from the SERVER side

The trace in §5.2 is what the harness sees, and the harness only ever sees its own runs. The MCP server
sees something different and, in one respect, more valuable: **every call, including the ones no
benchmark made.** Both vantage points are needed, and neither substitutes for the other.

| | bench-side trace (§5.2) | server-side tool telemetry |
|---|---|---|
| covers | benchmark legs only | **all traffic**, benchmark and real sessions alike |
| knows the prompt, the model's answer, its cost | yes | no |
| knows server-side processing time, the payload it actually returned, which project it was scoped to | only by inference | **yes, exactly** |
| survives the harness not being involved | no | yes |

Every tool invocation is recorded with:

- **which tool**, how many times, and against **which project / target** it was scoped;
- **who called it** — runtime family (a Claude CLI session, a cloud model over an API, a local model)
  **and which model specifically**. Where the transport does not carry that, it is recorded as *not
  captured*, never guessed and never defaulted to the popular answer;
- **what went in** — the arguments, in full where they fit a per-call byte budget and truncated with
  the truncation recorded where they do not;
- **what came out** — the payload's size always, its body within the budget, and the **outcome**
  (answered / refused / error), because a refused call and an answered one are otherwise identical from
  the outside, which is exactly how a read-only guarantee was once asserted for months and was false;
- **tokens**, where the surface knows them — for a tool that embeds or reranks it does, for a pure file
  read it does not, and the difference must be visible rather than rendered as zero;
- **server-side processing time**, which is not the caller's latency: the wait for an accelerator lease
  belongs to the third bucket of §5.3 and must not be folded into either.

**This crosses a repository boundary and is a contract, not a feature this repository can ship alone.**
The tool surface lives in `dew_flow_mcp` and the engine behind it in `dew_flow_rag_qln`; what belongs
here is the schema, the ingestion port and the report. It has the same shape as the white-box trace
question — define the contract, version it from v0, accept a black-box degradation when a server does
not implement it — and it should reuse that versioning rather than invent a second one.

> **SHIPPED 2026-08-15** as [research/PLAN_tool_telemetry_v0.md](../research/PLAN_tool_telemetry_v0.md)
> (this side) and `dew_flow_mcp · research/PLAN_usage_telemetry.md` (the emitter). The unstated
> assumption in the paragraph above turned out to be the load-bearing one: it reads as though the two
> halves must land together, and they did not have to. A **local spool** — the server appends JSONL and
> never blocks, `bench telemetry ingest` drains it — decoupled them entirely, and is also why telemetry
> cannot fail a tool call. Still open: the private product host does not register the sink, so the path
> is proven from the emitter's own output rather than from live production traffic.

Two things to get right at design time, both cheap now and expensive later. **Aggregate on a key that
includes the caller and the engine**, or a mid-day switch of model or engine silently blends two
populations into one row — an upstream system shipped daily aggregates without an engine column and
then could not attribute a latency change to the switch that caused it. And **decide retention before
the first write**: full arguments and payloads across all production traffic is the largest table in
the system by an order of magnitude, so the budget belongs in the schema, not in a later clean-up job.

### 5.5 Answer-key hygiene, in its new shape

A separate repository does **not** retire this problem; it moves it. The suites now live outside the measured
tree, which is a real structural win — but the **target repository may itself contain prior results**.
Benchmarking a repository that publishes its own findings puts those results in the tree, answers
and all. Therefore `target.exclusions[]` is a first-class input, validated before a run starts, and every run
records what it excluded.

## 6. Surfaces

**CLI first, and shaped for an agent.** The measured reason: the same four tools scored 4/63 over the MCP
wire against 37/63 in bridge shape — *from the form of the surface alone*. Concretely:

- **Exit codes mean something**, copied from the http-smoke suite of the earlier programme: `0` pass · `1` a real regression ·
  `3`/`4`/`5` environment / configuration / no report. Never conflate "the measurement failed" with "the
  harness could not run".
- **JSON output beside human output**, on every command.
- **Idempotent and resumable.** Thousands of tests run for hours; re-invoking must resume, not restart or
  duplicate.
- **No implicit selection.** A command creates a run or names one (§2, last row).

**API** alongside the CLI over the same domain — not a second implementation. **UI last**, and in a Razor
Class Library from birth, mirroring the WASM-ready RCL `dew_flow_mcp` already ships.

## 7. Infrastructure

Its own AppHost and Postgres container, following `dew_flow_rag_qln`'s precedent of owning its Qdrant and
Neo4j. Plus one component with no precedent to copy:

**A read-only checkout cache.** A bare clone per repository URL and a worktree per commit — never a checkout
in a directory anyone works in. The old `CodeRepoProvider.EnsureAsync` runs `git checkout` in place on the
configured `RepositoryPath`; a benchmark doing that would rewrite a developer's working tree to a commit they
did not ask for.

## 8. Architecture

Ports and adapters, with the domain as a pure leaf — the one property of the old `v2.Eval` worth reproducing
deliberately (22 files, no EF, no HTTP).

- **Domain**: target / suite / question / expectation / run / trace as pure types; scoring, matching, order
  planning, cap policy, the funnel model; judges, engines, runtimes and samplers as interfaces.
- **Ports**: `IBenchStore`, `IEngine`, `IModelRuntime`, `IRunTrace`, `IHardwareSampler`, `IJudge`,
  `ICheckoutProvider`.
- **Adapters**: EF/Postgres · engine clients (QLN, mindex, HTTP, filesystem) · model runtimes (cloud CLI,
  local) · the sampler · git.
- **Hosts**: CLI and API, both thin.
- **One append-only trace table**, keyed by `(run, question, repeat, subject, lane, phase)`. This is what
  keeps a new metric an INSERT instead of a migration — and its absence is what produced two parallel
  expectation schemas in the old system, the single reason this is a rewrite rather than an extension.

**The extensibility test, applied while designing and asserted in the suite:** adding a fifth lane, a sixth
metric, a third judge or a fourth engine must require **no schema migration**. Demonstrate it by doing one of
the four in a test.

Copy the architecture guard from `dew_flow_mcp` (8 passing tests over layering). The enforcement is the
point: nothing of the kind existed before, and that is how the old coupling accumulated.

## 9. Build order

1. **Repository skeleton + CI + architecture guard**, mirroring the three existing `dew_flow_*` repos.
2. **Domain types + the measurement contract** (§3) — suite freeze/hash and the commit-scoped expectation as
   the first tests.
3. ~~**Checkout cache** (§7)~~ — **DONE 2026-08-14.** `GitCheckoutProvider`: one bare mirror per url, one
   worktree per commit, directory names from a hash of the url (a url is operator input, and a url pasted
   into a path is how `../` gets a say), per-url lock, everything under one cache root. `ProcessRunner` is
   the single launcher — exe + argv, never a shell string, timeout as a VALUE rather than an exception.
   The read-only guarantee is proven rather than asserted: the guard test was watched failing after the
   historical defect (checkout in place on the source) was deliberately reintroduced, and it failed on the
   right line — the source's HEAD had moved.
4. **CLI over an in-memory store**: create suite → run → report, with the exit-code contract. First
   end-to-end value, no database yet.
5. ~~**Postgres adapter** + durability~~ — **DONE 2026-08-14.** `BenchRun` + `RunCell` with the
   claim/settle/sweep transitions as PURE functions in the domain, so the database is left with the one
   job only it can do: making a claim atomic. `PostgresRunStore` does every state change as a **guarded**
   update — the condition lives in the WHERE clause, never in an `if` above it. Schema ships as a
   versioned EF migration rather than `EnsureCreated`, and the durability suite applies that migration,
   so a migration that does not run is a red test. Teeth proven: replacing the guarded update with a
   check-then-act turned the race test red with the real symptom — **12 winners instead of 1**.
   `MaxAttempts = 3` makes the sweep terminate: a cell that kills its host is abandoned, not requeued
   forever. Not yet wired to a host — no worker exists to drain a queue until the engine port lands.
5b. **Adopt the evaluation library, and the phase model** — **DONE 2026-08-15.** Their metric model and
   report writers are in; their orchestration and their disk store are not. `MetricCodec` is the boundary
   and round-trips both ways. Results are stored with metrics as ROWS, joined to the cell and the run, so
   "average this metric per engine" is a group-by rather than a scan that parses a directory name — the
   query their store cannot answer, and the whole reason storage stayed ours. Ratings are stored as NAMES,
   not ordinals. Phases are ours (`TaskKind`, `LegPhase`, `PhasePlan`): a fix task runs
   investigate → fix → verify → judge, a phase cannot start before its predecessors end, and a ceiling or a
   crash stops the LEG rather than only the phase.

6. ~~**Model runtime + the leg runner**~~ — **DONE 2026-08-15.** `OpenAiCompatibleRuntime` asks a local
   model and REFUSES the ceilings it cannot impose (cost, turns), which is the `CompactAtTokens` lesson made
   structural. `LegRunner` is the assembly — claim, ask, score, store, settle — result-first and re-entrant
   across the crash window. Scoring is mechanical (`AnswerScoring`); a retrieval expectation in a lane with
   no retrieval reads *not applicable* rather than a miss, so the no-tools baseline is not biased against
   itself. **Still single-shot: no tool-calling loop, so every lane is currently a no-tools lane.**

7. **Engine port + the four kinds.** Black-box trace live, the white-box contract defined and implemented
   against a captured fixture in the same pass (§5.2) so the two never diverge — this step does **not** wait
   for an engine to emit the funnel.
7. **Hardware sampler** + accelerator lease.
8. **Judge port** — arbiter selectable per suite, re-score without re-running legs.
9. **API** over the same domain.
10. **RCL + UI.**

Steps 1–4 are the walking skeleton; a real measurement becomes possible at step 6.

## 10. Test plan

- Suite freeze: editing a frozen version creates a new version; a result always names a hash that resolves.
- Re-target: an expectation whose anchor moved between commits is flagged, never silently matched.
- Order plan: a matrix with an odd repeat count balances across the whole matrix, not per task — the measured
  2:1 defect, RED first.
- Caps: cost / wall / turns / context each produce their own terminal outcome, distinguishable from a crash
  and from a wrong answer, and excluded from paired deltas.
- An unset model id is refused: a test asserts no paid runtime is reachable from an unset field.
- Trace port: the same report renders from black-box and white-box; "not captured" never renders as 0.
- Sampler: a sampler failure neither fails nor delays a run (fault injection).
- Resume: killing the host mid-run resumes without duplicating or losing questions.
- Extensibility: add a lane in a test, with no migration.

## 11. Definition of Done

- [ ] The measured product was not modified to make this work.
- [ ] A run is identified by the §3 tuple; results outside one tuple cannot be compared by the report.
- [ ] Suite versions are frozen and hashed; expectations are commit-scoped; re-target is explicit.
- [ ] Models, lanes and engines are data; adding one needs no migration, demonstrated in a test.
- [ ] Both trace modes ship behind one port, with **two real implementations** — live black-box and fixture-replay white-box; an interface with a single implementation does not satisfy this item.
- [ ] The white-box contract is derived from a captured payload, is version-stamped, names its stages explicitly, and degrades to black-box on an unknown version; engines declare supported stages rather than the payload being generic.
- [ ] *(separate, and later)* A live engine emits the funnel and it is persisted per question — the only item here that depends on an external owner (open question 3).
- [ ] Hardware sampling is out-of-band, joined by timestamp, cannot fail a run; runs serialize on the accelerator.
- [ ] Server-side tool telemetry (§5.4) has a versioned schema, an ingestion port and a report: per tool — count, project/target, caller runtime AND model, arguments in, payload out with its outcome, tokens where the surface knows them, server-side time. Unknown caller or unknown tokens render as *not captured*, never as a default or a zero.
- [ ] Tool-telemetry aggregates key on caller and engine, so a mid-day switch cannot blend two populations into one row; retention is decided in the schema, not deferred.
- [ ] Time is reported in three buckets; wait is never counted as thinking.
- [ ] Caps exist per phase and per question; a cap hit is a terminal outcome.
- [ ] Sampling is recorded as sent; every budget records the runtime that accepted it.
- [ ] The report refuses to rank configurations whose repeats overlap (`n ≥ 2`), and refuses to crown a winner proved only on the half that selected it (§3.4).
- [ ] `target.exclusions[]` is validated before a run and recorded with it.
- [ ] Ground-truth sources are combinable checkboxes with deduplication and a candidate → accepted → frozen review state; the authoring model is chosen from the available list and recorded with the batch.
- [ ] No question is ever deleted for being easy. Difficulty is a measured label per subject tier, and discrimination is computed PER COMPARISON against that comparison's own subjects.
- [ ] CLI honours the exit-code contract, emits JSON, is resumable; no command selects a run implicitly.
- [ ] The §2 carried-lessons table exists as a checklist, each row naming the test that pins it.

## 12. Open questions

1. **Language scope, and the first target.** Ground-truth authoring and symbol identity are language-specific; C# first is the obvious call. The sharper half of this question is the TARGET: every finding carried over came from one small self-authored repository, which is a confound large enough to invert the headline result ([MEASURED_LESSONS §4](../research/MEASURED_LESSONS.md)). The first serious target should therefore be a large unfamiliar C# codebase, and the first measurement worth running is whether the carried-over findings survive it at all.
2. **Authoring throughput, now that the sources are decided.** §4c fixes WHERE ground truth comes from — repository history as the backbone, a chosen model for volume, a person for the hard cases, all combinable — and that nothing is ever pruned by difficulty. What stays open is the rate: how many accepted questions per week the review step can actually pass, and whether the memorisation-trap property can be checked mechanically or needs a human every time. Both are answerable only by authoring a first real batch.
3. **Who implements the white-box trace contract first, and when — no longer a blocker, but still open, and now on a weaker footing.** `dew_flow_rag_qln` is at skeleton stage, and the engine that already had a stage instrument is out of scope and is not touched — so there is no live emitter and, unlike the first draft of this plan, **no captured payload to derive the contract from either** (see §5.2). The contract, the port, a hand-authored fixture and the report columns still ship without an emitter, which keeps this off the critical path of build step 6. What is genuinely open: when QLN grows retrieval far enough to emit a funnel, and how much the provisional v0 contract has to change when it does. Until then no run carries a real funnel, and the DoD distinguishes the two states rather than blurring them.
4. **Where the three pending retrieval arms run.** Graph header out of the embed text, class/file context in the embed text, and gating the description pass while every member still gets questions — three experiments carried over from the earlier programme, each with recorded measured support and each a change to *an engine*. The engine they were written against is out of scope now, so they are homeless: they survive as experiments this bench can run, and they need a host engine named before any of them means anything.
5. ~~**Who owns the tool-telemetry contract, and when does it land.**~~ **CLOSED 2026-08-15** — operator decision: **this repository owns both versioned contracts** (the white-box funnel and the tool telemetry) under one scheme, and everything lands in this benchmark's own Postgres. Shipped as [research/PLAN_tool_telemetry_v0.md](../research/PLAN_tool_telemetry_v0.md), with the emitter in `dew_flow_mcp · research/PLAN_usage_telemetry.md`. The transport is a **local spool** rather than a live connection, which is what let the two halves ship independently: a server that had to reach this host to record a call would couple a product's tool surface to a benchmark being up. What remains is not ownership but coverage — the private product host does not register the sink yet, so only the standalone MCP host emits.
6. **Repository visibility.** `dew_flow_mcp` and `dew_flow_sidecar_rust` are public, `dew_flow_rag_qln` is not. A benchmark that measures other people's engines has a different publication calculus than either.
