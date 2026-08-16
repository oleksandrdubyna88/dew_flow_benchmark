# PLAN — the scoreMeter port: cleaned LOC, the zero-band weighting protocol, and the harness method

> Status: **plan only, nothing implemented yet, 2026-08-16.** Scope: new code in `Bench.Domain` +
> `Bench.Application`, fixtures and tests; one new table. The source repository is read-only:
> `scoreMeter` at `\\wsl.localhost\Ubuntu\home\jinx\git\scoreMeter` — cited below as paths, per the
> cross-repository citation rule.
>
> Consumer: [PLAN_code_lane.md](PLAN_code_lane.md) §5.2 — its build-order steps 5–6 and the
> recalibration in step 7 ARE this plan. Axes and console: [PLAN_variant_matrix.md](PLAN_variant_matrix.md).

## 1. The goal, and why a port rather than a copy

The code lane needs a delivered-work score that volume cannot buy. That instrument exists and is
**measured**: scoreMeter V2's zero-band protocol took ×10 behaviour-neutral padding from a ×1.7 score
gain to **×0.88** (inflation exponent −0.06), landed zero on **160 of 160 padded steps** and on 4 of
245 real ones — all four genuinely worthless churn
(`scoreMeter · tools/diff-only-inflation/REPORT_ZERO.md`).

Copying the repository would be the family's fifth mirror: its Application layer is ~101 files, of
which the scoring core is ~8; the rest is ticket ingestion, billing, a subscription-key pool and a web
product. So: **three things are ported as bench-owned code citing its source; nothing else crosses.**

## 2. Source inventory, verified

| Source (`scoreMeter · src/ScoreMeter.Application/`) | What it is | Verdict |
|---|---|---|
| `Diffs/DiffParser.cs`, `Diffs/DiffCleaner.cs` (106 L) | split a unified diff by file; drop no-value files, **recording** what was dropped | **port** |
| `Metrics/PathCategories.cs` | path → `Counted \| Evidence \| Dropped`; **case-sensitive, first-match-wins** — both measurement-pinned (case-insensitive matching once read a 9.42-hour ticket as 0 lines) | **port, categories become data** (§3.1) |
| `Metrics/LineNormalizer.cs` | drop blanks/comments, strip inline comments with string masking, collapse whitespace, **join continuations into logical lines**, wrap at 100 chars — a size that does not depend on the author's line-break taste | **port, C#-adapted** (§3.2) |
| `Metrics/LocMetrics.cs` (202 L) | the metric family; the three fixed line figures: `Diff` (raw), `Cleaned` (churn in counted files), `Excluded` (evidence lines) | **port the line figures**; the layer-weighted members are their fit — not ported |
| `Pipeline/Prompts/DiffWeightingPrompts.cs` | the ten anchor lines (validated: models quote them back reproducibly over 2,088 production units + 452 steps), the **`ZeroAnchor`** kept separate, the rule keeping zero from eating the bottom of the scale, few-shot examples admitted only where history agrees; `PromptId` bumped on any string change | **port the text**, new protocol id carrying provenance (§4.3) |
| `Pipeline/CoverageGate.cs` (372 L) | `Accept \| Retry \| Fail` with six named statuses (`passed`, `capped-substantive`, `capped-borderline`, `too-thin-to-gate`, `hard-failure`, `under-threshold`); one re-ask naming the shortfall; a capped run never indistinguishable from a clean one | **port the semantics**; size bands inherited-marked. Known limitation stays known: *"the coverage gate still loosens with size"* (their report) — recorded, not silently fixed |
| `Pipeline/ScoringPolicy.cs` (194 L) | deterministic corrections in code AFTER the model: near-duplicate cap (declared mirrors on one anchor file), rescue allowance; raw and applied both persisted with the rule name; recomputable over stored runs with zero calls | **port**; constants inherited-marked (§4.4) |
| `Pipeline/Scoring.cs`, `Pipeline/JsonReplyReader.cs`, `StageParsers.cs` | weighted sum; strict reply parsing | **port the minimum** the stage needs |

**Explicitly NOT ported:**

- **Grain** (`GrainScoring`/`GrainCompute`, α = 0.75) — their own report: *"Σ grain still cannot tell
  padding from work."* Not a quality measure here; at most a later descriptive column.
- **Layer weights** (`Metrics/LayerWeights.cs`, `FileSaturationLayer`, `LayerSpreadLoc`) — fitted on
  their stack and pools.
- **The pipeline machinery** (`WeightedPipeline`, `StageRunner`, `ILlmTransport`, prompt caching, key
  pool) — the bench has `IModelRuntime`, budgets and its own persistence.
- **The ticket-based mode** entirely — the bench's tasks carry their own statement; there is no Jira.

## 3. The honest part — what must CHANGE in the port

### 3.1 Path categories are PHP/JS-tuned; the table becomes data

The source table names `**/*Test.php`, `**/behat/**`, `**/doctrine_migrations/**`, `**/Version20*.php`,
`**/*.g.php`, `**/*ApiClient.php` — and deliberately counts Symfony `*.yml` config as logic. Ported
as-is it would misprice a C# diff. So:

- The **mechanism** is ported exactly: case-sensitive matching, first-match-wins order, glob→regex
  translation, the three fates, "dropped is recorded, never silently gone".
- The **table** becomes a bench-owned category list (data, not code edits), seeded with the source
  rows plus a C# band: `**/obj/**`, `**/bin/**` → dropped; `**/*.Designer.cs`, `**/*.g.cs`,
  `**/*.generated.cs`, `**/Migrations/**` (EF) → evidence; `**/*.csproj`, `**/*.props`, `**/*.slnx`
  → evidence (build shape is proof, not priced logic); test trees already covered by the source's
  `**/Tests/**` rows.
- A property test pins the two measurement-pinned behaviours, and a fixture test proves the C#
  additions change **nothing** for the source fixtures' PHP/JS paths — additions only, no re-fates.

### 3.2 The line normalizer treats `#` as a comment; in C# that is the preprocessor

`LineNormalizer.IsDroppable` drops lines starting `#` (PHP/Python comments, keeping `#[` attributes).
In C#, `#` opens `#if`/`#region`/`#nullable`. The port makes comment syntax **per-language, keyed by
extension** (the source already does this for stylesheets in `Join`):

- `.cs`: `//`, `/* … */`, `///` are droppable; `#region`/`#endregion`/`#pragma`/`#nullable` droppable
  as non-logic; `#if`/`#else`/`#endif` **kept** — they alter what compiles, and dropping them merges
  branches into nonsense.
- `ConfiguringAnnotations` (Doctrine/Symfony docblock tags) become an empty set for C#: attributes are
  real `[…]` lines and survive on their own; `///` doc comments drop like any comment.
- String masking learns C# verbatim (`@"…"`) and raw (`"""…"""`) strings — the masker is what keeps a
  URL's `//` from reading as a comment, and C# literals break the source's single-quote/escape model.
- Continuation joining ports unchanged — the leading-`.` fluent-chain head the source built for PHP
  `->` chains already covers C# LINQ/builder chains.

Every adaptation above lands with its own RED-first tests on real C# diff fixtures.

### 3.3 The anchor scale ships verbatim, with inherited examples

The ten scale lines are reused word for word — their stability is measured (models quote the matching
line back and land on its score reproducibly), and rewriting them would discard exactly that evidence.
The few-shot examples are their history's; ours will replace them only as our own history accumulates
agreement, the same admission rule they used.

## 4. Where it lands

### 4.1 Layout — ONE independent module (operator decision 2026-08-16)

The port is a single new project, **`src/Bench.Delivered`** — a class library with **zero package
references and zero project references**, the same leaf discipline `Bench.Domain` and `Bench.Contracts`
already live under, asserted by the same `ArchitectureTests` (regex and JSON are BCL; nothing else is
needed):

- **Everything deterministic lives inside the module**: `PathFate`, `PathCategoryTable` (+ the seeded
  table as data), `LineNormalizer`, `DiffParser`, `DiffCleaner`, `LocFigures` (Diff/Cleaned/Excluded),
  `DeliveredWorkPolicy` (the `ScoringPolicy` port: caps, allowance,
  `Adjustment(Key, RawScore, AppliedScore, Rule)` trail), `CoverageDecision` (the pure
  accept/retry/fail arithmetic and the six statuses), the **prompt texts and protocol ids**, and the
  strict reply parsers. The module never calls a model, never touches a store, never reads a file —
  strings in, values out.
- **`Bench.Application/Delivered/DeliveredWorkStage`** is the only orchestration, and the only place
  that references the module from the pipeline side: it feeds the module's prompts to `IModelRuntime`
  (temperature 0, recorded seed, per the code lane's noise rule), hands replies back to the module's
  parsers, runs the one re-ask the gate asks for, persists `stage_payloads`, and stores the module's
  decisions. The stage owns IO; the module owns every rule.
- **No reference in either direction** between `Bench.Delivered` and `Bench.Domain` — a sibling leaf,
  not a layer. That is what makes it liftable whole (into a package, or another repository) without
  touching the bench, and what keeps the bench's own domain free of another product's vocabulary.

### 4.2 Storage: the recompute property is the point

The source's defining property — policy and figures recompute over historical runs **without one model
call** — requires persisting the raw stage payloads:

```
stage_payloads: Id, ResultId (FK), Stage (decompose | weigh | coverage), Ordinal,
                PayloadJson, PromptHash, Protocol, CreatedAt
```

Scores land as the existing metric rows (raw AND applied, with the rule that changed each); cleaned-LOC
figures as metric rows per leg. A `bench rescore` verb recomputes policy over stored payloads — proving
the property, not just claiming it.

#### What grows, and who owns it

`stage_payloads` is append-only and it is the largest row this plan writes: the decompose stage's payload
is a step-by-step account of a whole diff, and there are three stages per result. A code-lane leg at
`repeats ≥ 2` (§5.3) therefore stores the diff twice over in a form that is several times the diff's own
size — call it **tens of kilobytes per leg**, which at this repository's stated target of *"tens of
thousands of cells"* (`src/Bench.Infrastructure/Persistence/BenchDbContext.cs:165,171`) is **tens of
gigabytes** for this table alone. The shared rule is that an append-only table names its retention or
rollup before the first write (`.claude/rules/shared/common/reliability.md` § Everything that grows has an
owner), and the founding plan put the reason plainly: the budget belongs in the schema, not in a clean-up
job written after the disk fills.

**The owner here is: kept forever, deliberately, and it is the one table where that is the correct
answer.** The whole property this section exists to preserve is that policy and figures recompute over
historical runs without one model call — and a payload that has been rolled up or aged out is a run that
can no longer be rescored, which is the property gone. So:

- `stage_payloads` is **permanent**, and its projected size is a budget line the retention listing prints
  rather than a cleanup target. At the matrix sizes of `PLAN_variant_matrix.md` §3.4a this is the number
  to watch, so it is stated rather than discovered.
- What may be dropped without losing the property is the **prompt text** — reconstructible from
  `Protocol` + `PromptHash`, which is why both are columns. Kept raw for a configured window, dropped
  after, with the hash and the protocol version retained so a rescore still proves which prompt produced
  the payload. Reference shape: `dew_flow_rag_qln · src/Rag.Infrastructure/Runtime/SizeHistoryStore.cs:76,159-204`
  (7 days raw, rollup beyond).
- Nothing in a payload may carry an absolute local path or a machine-specific value; a diff is repository
  content and belongs in a publishable database, a path from the operator's disk does not
  (`PLAN_variant_matrix.md` §3.5).

### 4.3 Protocol and provenance

Every score carries a protocol string, source acknowledged inside it:
`delivered-work-v1 (anchors inherited: scoreMeter diff-weighting-v3 / diff-only-gated-zero-2026-08-13)`.
The console renders an **inherited calibration** badge on any score whose protocol names an inherited
component (PLAN_code_lane §5.2). The badge dies only through §6 step 5.

### 4.4 Constants arrive marked, not trusted

`NearDuplicateCap = 2`, `RescueAllowancePerPrPoint = 2`, the coverage bands, the anchor examples — all
fitted on a PHP/JS production corpus (223 runs / 1,614 units). Each is declared in one place with an
`InheritedFrom` note naming the source measurement, so "which numbers are ours" is answerable by
reading one file.

## 5. The third thing: the harness method

Ported as **practice**, encoded in this repo's shape rather than as Python:

- **Frozen arms, gates before spend** — their run order (`test_churn.py` parity gate → build arms →
  `verify_arms.py` 48 checks → only then the paid run) becomes the recalibration harness's order here.
- **Parity fixtures**: three real diffs from the source study plus their **published** cleaned-LOC
  values, copied in with provenance headers; a red parity test means the port drifted, exactly like
  their gate meant it for the Python→C# port.
- **The inflation re-run on OUR corpus** (§6 step 5): one real solved code-lane task, padded ×10
  behaviour-neutral (their `satellites.py` recipe: reachable, deterministic, changes nothing
  observable), two samples per arm, pinned model and seed, the exponent computed the same way. The
  property is **re-verified here, never assumed transferred** — until then the badge stays.
- **Fixed identities in shared stores** for every fixture and arm, per the family testing rule — the
  source's own 22-of-24-GB Qdrant lesson applies to arm worktrees and result rows alike.

## 6. Build order

1. **The module and the line family** — `src/Bench.Delivered` created as a leaf (zero references,
   `ArchitectureTests` extended to assert it, same as `Bench.Domain`); then `DiffParser` +
   `DiffCleaner` + `PathCategoryTable` + `LineNormalizer` + `LocFigures` inside it, with the
   §3.1/§3.2 adaptations behind their own tests and the parity fixtures green.
2. **The policy** — `DeliveredWorkPolicy` with the trail; behaviour pins ported from the source's
   measured cases (run #13862's 79 → 9 shape, the declared-mirror cap) as fixture tests.
3. **Prompts + gate + stage** — the zero-band weigher, strict parsing (an unreadable reply is a
   refusal, never a guess), one re-ask, `stage_payloads` persistence, the protocol string.
4. **`bench rescore`** — policy recompute over stored payloads, zero model calls, proven by a test
   that counts runtime invocations.
5. **Recalibration** — the inflation arm on our corpus; on a pass (exponent ≈ ≤ 0, padded steps
   zeroed, honest score inside the sampling spread) the inherited badge drops; on a fail the anchors
   get recalibrated HERE and the protocol version bumps.

Steps 1–2 are pure domain work and can start before the code lane's sandbox exists; steps 3–5 need a
model runtime and (step 5) one solved task.

## 7. Test plan

- xUnit v3 exe, RED first, per the family rules.
- Architecture: `Bench.Delivered` references **nothing** — the extended `ArchitectureTests` assertion,
  red build on the first convenience reference.
- Parity: the three source diffs reproduce their published cleaned-LOC numbers; the PHP/JS fixture
  paths keep their exact fates after the C# category additions.
- Properties: case-sensitive matching and first-match-wins order each have a test that fails if
  "tidied"; the C# `#if`-kept / `#region`-dropped split; verbatim/raw string masking.
- Policy: deterministic order (score desc, then key) elects the same survivor on equal input; dropped
  rescues appear in the trail at applied 0, never vanish.
- Gate: each of the six statuses reachable in a test; a capped run distinguishable from a clean one in
  what is persisted.
- Stage: an unparseable weigher reply refuses the leg's delivered-work score with a reason — the
  mechanical signals (PLAN_code_lane §5.1) are untouched by that refusal.

## 8. Definition of Done

- [ ] The port is one leaf module (`Bench.Delivered`) with zero references, proven by the
      architecture test; only `DeliveredWorkStage` orchestrates it.
- [ ] Parity fixtures equal the source's published numbers; the parity test is loud, not skippable.
- [ ] The C# adaptations (§3.1, §3.2) are tested, and provably change nothing for the source fixtures.
- [ ] Every delivered-work score persists raw + applied + rule + protocol string; `bench rescore`
      reproduces stored scores with zero model calls.
- [ ] The zero band is in the shipped prompt; an all-padding step scores 0 in a fixture test.
- [ ] All inherited constants are declared in one place with their source measurement named.
- [ ] Grain appears nowhere as a headline metric.
- [ ] The inflation property is re-verified on this corpus, or every affected score still renders the
      inherited-calibration badge.
- [ ] `todo/README.md` updated; [PLAN_code_lane.md](PLAN_code_lane.md) steps 5–7 point here.

## 9. Open questions

1. **`#if` blocks in cleaned LOC** — kept as logic (§3.2) is the starting rule; if real C# diffs show
   it distorting (config-heavy files), measure before changing.
2. **Coverage bands for C# step sizes** — inherited; their gate's loosens-with-size behaviour is a
   known open flank in the source too. Revisit with our first hundred scored steps.
3. **Whether the saturation family (`churn^0.75` per file) earns a place later** — not ported now;
   if a per-file size figure is ever wanted, it re-enters through a measurement, not through nostalgia.
