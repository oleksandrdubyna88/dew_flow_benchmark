# PLAN — corpus axis integrity: what a chunk axis means, and proving the engine honoured it

> Status: **PARTLY IMPLEMENTED, 2026-08-23 — the GUARD and the UNIT ship; the STRATEGY half is blocked on
> the engine, so this stays open.** The status line above read *"plan only, nothing implemented yet"* until
> today, which had been false since 2026-08-17: §3.2's guard — the whole point of the plan — was built then.
>
> **Shipped.** §1.1 is closed: an engine echoes the corpus it will answer from and `IndexReadiness` ends the
> run naming both recipes before a single cell exists, which is more than the plan asked for (it also checks
> the commit, the arm, a dirty tree and a failed pass). §1.2 is closed today: `CorpusSpec.Tokenizer` gives
> `ChunkTokens` its unit, and a corpus counted in another model's tokens is refused *even though both sides
> say 512* — the one check here where the numbers already agree. Both new axes are turnable from the CLI
> (`--tokenizer`, `--dimensions`) and visible in `bench variants list`.
>
> **Two defects of the "built and never called" class were found while doing it.** `EmbedDimensions` — its
> three states, its refusal, its warning, its wire round-trip and its tests — had no surface that could
> declare a width, so the recipe side was always `NotDeclared` and the guard could never fire; one CLI flag
> closed it, and the red test observed exactly that. And the tokenizer the engine has reported since
> 2026-08-16 was read into `CorpusIdentity` and then discarded, so the plan's §4 contract item 3 had been
> satisfied by the other repository for a week with nothing on this side consuming it.
>
> **Deviation, §3.1: an unset tokenizer is NOT a refusal.** The plan asked for one. It cannot hold —
> `CorpusSpec.Parse` is also how STORED rows are read back, so refusing would make every variant written
> before this axis unreadable, and a catalog row is immutable by design. It takes the three-state shape
> `EmbedDimensions` had already established for the identical problem: declared · not declared, mismatched
> only when both sides speak, and out of the canonical form until declared so no stored hash moves.
>
> **Still open, and the first item is why this document stays in `todo/`:**
>
> - **`ChunkStrategy` (§1.3) is not expressible, deliberately.** The engine has no strategy concept at all
>   (verified 2026-08-23: zero occurrences in `dew_flow_rag_qln/src`), so it can neither select one nor echo
>   one. Adding it to `CorpusSpec` now would mint two hashes that resolve to ONE corpus with no echo able to
>   catch it — §1.1's defect, reintroduced by the plan meant to fix it. It waits on
>   `dew_flow_rag_qln · todo/PLAN_tokenizer_contract_and_chunk_coverage.md` §3.2, which is §4 contract item 2.
> - **The echoed recipe is checked and PRINTED, never stored** (§3.2's last bullet, DoD 3). A published
>   database cannot be re-checked by a reader who was not there. Deferred rather than dropped: it needs a
>   migration, and a concurrent session was writing migrations in this tree at the time.
> - `bench variants verify` (§3.4), the API grouping, the UI pages, and the first strategy × chunk-size grid.
>
> Scope: `Bench.Domain/Variants` (two fields on
> `CorpusSpec`), `Bench.Application` (the honoured-recipe guard), `Bench.Infrastructure/Engines`
> (`QlnEngine` axes echo), `Bench.Api`, `hosts/Cli`, and `src/Bench.Ui` for the pages.
>
> **Boundary with [PLAN_variant_matrix.md](PLAN_variant_matrix.md)** — that plan owns the variant catalog
> (step 1, implemented 2026-08-16), the question bank, the model registry and the console shell. This
> plan adds two axes to the catalog's corpus half and one guard, and mounts its pages in that console. It
> does not touch the catalog machinery.
>
> Engine halves: `dew_flow_rag_qln · todo/PLAN_tokenizer_contract_and_chunk_coverage.md` (the chunk
> strategy and the tokenizer contract) and its `todo/PLAN_search_variant_axes.md` §3.2–3.3 (the
> index-state read and `ChunkTokens`). Tokenizer wire:
> `dew_flow_sidecar_rust · todo/PLAN_tokenizer_registry.md`.
>
> Related: [../research/MEASURED_LESSONS.md](../research/MEASURED_LESSONS.md) §1 (a measurement is only
> valid against the corpus it ran on).

## 1. The goal, before any solution

The matrix is supposed to answer *"which corpus recipe wins, and on which kind of question"*. Two things
stop it from answering that honestly about chunking.

### 1.1 A variant can claim an axis the engine never applied

`CorpusSpec(TextShape, ChunkTokens, EmbedModel)` is validated, hashed and part of a variant's identity
(`src/Bench.Domain/Variants/VariantDefinition.cs:98-134`). A variant naming `chunkTokens: 256` and one
naming `512` are two rows with two hashes, and a test already pins that they differ
(`tests/Bench.Tests/Variants/VariantDefinitionTests.cs:87-88`).

The engine, today, derives its window **from the model** and cannot be told otherwise — *"a configured
window that disagrees with the model truncates in silence"*
(`dew_flow_rag_qln · src/Rag.Application/Indexing/FastStage.cs:187-198`). So both variants resolve to the
same recipe, the same collection, the same vectors. Two rows, one corpus, and a report that puts them
side by side as a comparison of chunk sizes when nothing about the chunk size differed.

The engine's own plan fixes its half: `ChunkTokens` becomes a recipe field threaded into the sidecar's
enforced cap (`dew_flow_rag_qln · todo/PLAN_search_variant_axes.md` §3.3). **The benchmark's half is not
fixed by that**, and it is the half that matters here: nothing on this side checks that what a cell
measured is what the variant asked for. The guard has to exist regardless of which engine is under test,
because the next engine will have its own reasons to ignore an axis.

This is the founding discipline applied one level down. An engine already *declares* its capabilities and
*echoes* the axes that actually served a query rather than being trusted
(`src/Bench.Infrastructure/Engines/QlnRetriever.cs:255,316` — the echo moved out of `QlnEngine` when the retriever was split out). The corpus a query is served *from* has had no
such echo, and it is the more expensive half — a wrong query axis costs one query, a wrong corpus costs
the whole collection and everything measured against it.

### 1.2 `ChunkTokens` has no unit, and there is no tokenizer anywhere

`ChunkTokens` is an `int` refused only when below 1 (`VariantDefinition.cs:120-126`). Tokens **as counted
by whom** is undefined, and the word *tokenizer* does not appear anywhere in this repository.

That is survivable while one embedding model exists and unsurvivable the moment two do. `256` under
bge-m3's tokenizer and `256` under a second model's are different amounts of text; folding both into one
`Canonical` (`VariantDefinition.cs:133`) makes two different corpora hash as comparable configurations.
The matrix's whole premise is that a result is comparable only inside its tuple — an axis whose unit
depends on another axis breaks that quietly.

### 1.3 Chunking strategy is not expressible at all

`TextShape` is a free string, checked only for non-emptiness. Whether a corpus was cut **structurally**
(one chunk per member, over-long members split into balanced windows) or by a **flat window** over the
file is not something a variant can say, so the arm cannot be run.

It is worth running. The previous generation measured what happens when a structural unit meets a flat
cap: across 113 markdown files, chunk sizes ran `p50 972` to `max 12315` characters against one 256-token
cap — **43.6 % of documentation text invisible**, re-measured live at 40.4 %
(`DewFlow · research/PLAN_doc_rag_256_subchunking.md:44-53, 75-76`). On the code side a single whole-file
chunk spanned `AppHost.cs` lines 7–573 *"of which only the first ~60 would ever reach a vector"*
(`DewFlow · src/v2/v2.Agents/CodeRag/RoslynCodeParser.cs:40-47`). Both were fixed by adding a
fixed-length layer beside the structural one — but *which* is better, and for which question shape, was
never measured. It is exactly a matrix question.

## 2. What exists today, verified

| Fact | Where |
|---|---|
| `CorpusSpec(TextShape, ChunkTokens, EmbedModel)`, validated, hashed, part of variant identity | `src/Bench.Domain/Variants/VariantDefinition.cs:98-134` |
| `TextShape` is a free string, refused only when empty; `ChunkTokens` refused only below 1; `EmbedModel` unset is a refusal | `VariantDefinition.cs:115-131` |
| The corpus canonical is `corpus={TextShape}/{ChunkTokens}/{EmbedModel}` | `VariantDefinition.cs:133` |
| Two chunk sizes already produce two hashes — the axis is live in the catalog | `tests/Bench.Tests/Variants/VariantDefinitionTests.cs:87-88` |
| `RetrievalChannels { Dense, Sparse, Hybrid }` — dense-only and sparse-only are named as the comparison the matrix exists to run | `VariantDefinition.cs:9-14` |
| The wire codec refuses an unknown axis rather than dropping it | `src/Bench.Application/Variants/VariantJson.cs` (`CorpusWire` at `:138`) |
| An engine echoes the query axes that actually served it | `src/Bench.Infrastructure/Engines/QlnEngine.cs:222-238` |
| **No corpus-side echo, and no tokenizer concept** — zero occurrences of *tokeniz* in the repository | grep, whole repo |

## 3. The shape — decisions

### 3.1 `CorpusSpec` gains a strategy and a tokenizer

```csharp
public enum ChunkStrategy { Structural, FixedWindow }

public sealed record CorpusSpec
{
    public string TextShape { get; }
    public ChunkStrategy Strategy { get; }   // NEW — how the text was cut
    public int ChunkTokens { get; }
    public string Tokenizer { get; }         // NEW — whose tokens ChunkTokens counts
    public string EmbedModel { get; }

    public string Canonical =>
        $"corpus={TextShape}/{Strategy}/{ChunkTokens}@{Tokenizer}/{EmbedModel}";
}
```

- **`Tokenizer` unset is a refusal**, on the same reasoning `EmbedModel` already carries: a token count
  with no named counter is a number whose unit nobody can recover. The refusal message says so.
- `ChunkTokens` keeps its `int`, and its documentation gains the sentence that gives it meaning: *tokens
  as counted by `Tokenizer`, which is the tokenizer the serving model actually uses.*
- Both join `Canonical`, so a corpus cut differently or counted differently is a different corpus. This is
  the one-line change that stops §1.2's silent collapse.
- **A migration is not needed** for the catalog — a variant is never edited, only retired, so existing
  rows keep their definitions and their hashes. New rows carry the new fields. Old rows render with the
  strategy and tokenizer shown as *not recorded*, which is the truth about them.

### 3.2 An engine echoes the corpus it served, and a mismatch blocks the cell

The rule: **a cell may not be measured against a corpus whose recipe differs from the variant it was
planned for.** Not a warning, not a footnote in the report — the cell is blocked with a named reason, the
same move the matrix plan already makes for an index whose commit does not match
([PLAN_variant_matrix.md](PLAN_variant_matrix.md) §3.4).

- `IEngine` grows a corpus-description read alongside its existing `Describe`/`TraceContractVersion`:
  what text shape, strategy, chunk tokens, tokenizer, embed model and indexed commit the collection it
  will answer from was actually built with. `QlnEngine` fills it from the engine's index-state endpoint
  (`dew_flow_rag_qln · todo/PLAN_search_variant_axes.md` §3.2, extended with strategy and tokenizer by
  its sibling plan).
- An engine that cannot answer reports **not declared** — a distinct state from a mismatch. A cell over a
  non-declaring engine is measured and its result carries the flag, because refusing every black-box
  engine would remove the comparison this benchmark exists for. What must never happen is a *silent*
  assumption that the recipe matched.
- The echoed recipe is stored on the run beside the variant it was planned for, so a published database
  can be re-checked by a reader who was not there.

**Why blocking rather than reporting.** A blocked cell is visible, cheap and fixable. A measured cell
whose corpus was wrong is a number that enters an average, gets compared, and is indistinguishable from a
real result forever after — the exact failure `MEASURED_LESSONS.md` §1 records three times over.

### 3.3 The matrix crosses chunking with everything else

No new machinery: `Strategy` and `Tokenizer` are fields of a catalog row, so a test crossing them is the
existing expansion (`ExpandAsync` over active variants). What this plan adds is the *naming grammar* so a
matrix page is readable — a variant's display name carries its strategy (`structural · 256 · bge-m3`
against `window · 256 · bge-m3`) rather than hiding it in a hash.

The first grid worth running, stated so it is not re-derived later: **strategy × chunk size × question
group**, one embed model, one engine, repeats ≥ 2. The question-group breakdown is the point — the
prediction on record is that a flat window helps entry-point and configuration questions (the material
structural parsing drops entirely) and hurts precise member lookup. Writing the prediction down before
the run is what lets a flat result read as a finding rather than a disappointment.

### 3.4 CLI, then API, then UI — the axis has to be turnable

- **CLI**: `bench variants add` gains `--strategy` and `--tokenizer`; `bench variants list` shows them;
  a new `bench variants verify --run <id>` reports, per cell, planned recipe against echoed recipe and
  exits non-zero on any mismatch. That verb is usable before any page exists and is what CI would run.
- **API**: the variant and run reads carry both recipes; a comparison endpoint groups results by
  `(Strategy, ChunkTokens, question group)`.
- **UI** (in `Bench.Ui`, mounted in the console [PLAN_variant_matrix.md](PLAN_variant_matrix.md) §3.6
  builds, and only over endpoints that already exist):
  1. **Variants** — strategy and tokenizer as columns, not buried in the definition JSON.
  2. **Matrix** — a cell blocked by a recipe mismatch renders its reason inline, naming both recipes;
     this is the page where the guard becomes visible rather than a log line.
  3. **Corpus comparison** — strategy × chunk size, rolled up per question group, with the repeat spread
     printed beside every mean and any gap inside it labelled unproven.
  4. **Leg detail** — the echoed corpus recipe beside the planned one, always both, even when they match.

## 4. Cross-repository contract

What this plan needs, named identically on the other side:

1. **The index-state read reports the full recipe** — text shape, strategy, chunk tokens, tokenizer,
   embed model, indexed commit, point count (`dew_flow_rag_qln · PLAN_search_variant_axes.md` §3.2 plus
   `PLAN_tokenizer_contract_and_chunk_coverage.md` §4).
2. **A chunk strategy is selectable and recorded in the recipe fingerprint**, so two strategies coexist
   as collections and neither can answer for the other
   (`dew_flow_rag_qln · PLAN_tokenizer_contract_and_chunk_coverage.md` §3.2).
3. **A tokenizer is named per corpus** and reported back, so `ChunkTokens` has a unit (same plan, §3.1).

## 5. Build order

1. ~~**`CorpusSpec` fields + canonical + refusals**, wire codec, catalog CLI flags.~~ **HALF, 2026-08-23:**
   the TOKENIZER shipped with its canonical, its wire round-trip and `--tokenizer`; `--dimensions` shipped
   with it, closing an axis that had been declarable by nothing. `Strategy` did not — see the status line.
   **Deviation:** optional and three-state rather than refused when unset.
2. ~~**The corpus echo on `IEngine`**, `QlnEngine` filling it from index-state~~ **IMPLEMENTED 2026-08-17**
   (`IRetriever.InspectAsync` → `IndexState`), and it reads more than this plan asked: overlap, commit,
   dirty tree, pass status, backend, width. **Not done:** the three-state result *stored on the result* —
   it is checked before the run and printed, never persisted.
3. ~~**The block**, with its reason~~ **IMPLEMENTED 2026-08-17** (`IndexReadiness`, refusing before a cell
   exists and naming both values). `bench variants verify` is **not** built.
4. **API reads + the comparison grouping.**
5. **UI pages**, over the step-4 endpoints only.
6. **The first grid** — strategy × chunk size × question group, with the prediction recorded before it runs.

## 6. Test plan

xUnit v3 executable, never `dotnet test`; `PostgresFixture` for anything stored.

- **Identity**: two specs differing only in strategy hash differently; only in tokenizer, differently;
  an unset tokenizer is refused naming why. The 256/512 test keeps passing unchanged.
- **The guard (the point of the plan)**: a cell whose echoed recipe differs from its variant is blocked,
  and the reason names both recipes; a matching echo measures normally; a non-declaring engine measures
  and flags. All three are separate tests, because collapsing them is the defect.
- **Regression**: an existing variant with no strategy/tokenizer still loads, still hashes as it did, and
  renders as *not recorded* — proven against a stored row, since the catalog is immutable and old rows
  are permanent.
- **UI**: bUnit over the matrix cell — a blocked cell renders its reason and does not render as a zero
  score; the comparison page renders the unproven label when a gap is inside the repeat spread.
- Every defect found while building gets its RED test first.

## 7. Definition of Done

- [~] A variant states **whose tokens** sized it (2026-08-23) — but not how it was CUT, and both are
      optional rather than refused when unset. The deviation is argued in the status line.
- [ ] Two variants differing only in chunk strategy are two hashes and two corpora, never one.
- [ ] Every result carries the corpus recipe that actually served it beside the one it was planned for —
      the check runs and PRINTS; nothing persists it.
- [x] A recipe mismatch blocks the cell with both recipes named; a non-declaring engine is flagged, not
      silently trusted. (2026-08-17 for shape/size/model, 2026-08-23 for the tokenizer and the width.)
- [ ] `bench variants verify` exits non-zero on any mismatch in a run.
- [ ] Chunk strategy is visible as a column and as a comparison rollup per question group, in the CLI, the
      API and a UI page — in that order of arrival.
- [ ] The first strategy × chunk-size grid has run with its prediction recorded beforehand, at repeats ≥ 2.
- [x] `todo/README.md` updated; `research/architecture.md` records the corpus echo — including the tokenizer
      as the third non-verbatim comparison and the three-state rule the optional axes follow.

## 8. Open questions

1. **Whether the tokenizer belongs in `CorpusSpec` or beside `EmbedModel` as its property.** They travel
   together in every case anyone has named — but the counting-only Qwen tokenizer on the sidecar is a
   live example of a tokenizer with no model behind it, which is why this plan keeps them separate fields.
   Revisit once the engine's registry exists and shows whether the pairing is ever many-to-one.
2. **Whether a flat-window corpus deserves its own question group.** If it wins only on material
   structural parsing drops entirely, the honest comparison may be "coverage" rather than "ranking", and
   that is a different metric than the ones the matrix reports today.
3. **What to do with variants already measured before this plan.** They keep their hashes and their
   results; whether a report may place them beside post-plan variants is a comparison-scope question, and
   the conservative answer — *not recorded* is its own bucket, never folded in — is what §3.1 assumes.
