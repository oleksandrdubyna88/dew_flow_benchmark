# PLAN — chunking becomes an axis: structural against flat window, and the echo kept as evidence

> Status: **plan only, 2026-08-23. The first item is BLOCKED on the engine and that is the whole reason this
> plan exists separately.** Scope: `Bench.Domain/Variants` (one field), the honoured-recipe guard it extends,
> one migration, and the comparison rollup. Engine half:
> `dew_flow_rag_qln · todo/PLAN_tokenizer_contract_and_chunk_coverage.md` §3.2.
>
> Extracted from [../research/PLAN_corpus_axis_integrity.md](../research/PLAN_corpus_axis_integrity.md) when
> that plan was promoted: its guard shipped, its unit shipped, and these three items did not. A real feature
> left inside a document filed as documentation is a feature nobody finds.

## 1. What is already true, so nothing here is re-derived

The parent plan built the hard half. An engine echoes the corpus it will answer from, and `IndexReadiness`
ends the run naming both recipes **before a single cell exists** — text shape, chunk size, embed model,
tokenizer, vector width, indexed commit, dirty tree, failed pass and compute arm. A variant declares its
tokenizer and its width; both are three-state and both stay out of the canonical form until declared, so no
stored hash moves.

What is missing is a chunk STRATEGY, the evidence trail behind a matched recipe, and the measurement all of
it was for.

## 2. The blocker, stated first because it decides the build order

**`ChunkStrategy` must not be added until the engine can both SELECT and ECHO one.** Verified 2026-08-23:
`dew_flow_rag_qln/src` contains no strategy concept at all. If a variant could declare
`structural` against `window` today, two catalog rows would hash differently and resolve to **one corpus**,
with no echo able to catch it — which is precisely §1.1 of the parent plan, reintroduced by the plan meant to
fix it. A variant that can claim an axis the engine never applied is the defect, not the feature.

So this plan's first step is not in this repository. It is
`dew_flow_rag_qln · todo/PLAN_tokenizer_contract_and_chunk_coverage.md` §3.2: a chunk strategy selectable per
corpus and recorded in the recipe fingerprint, so two strategies coexist as collections and neither can answer
for the other.

## 3. Why the arm is worth running at all

The previous generation measured what happens when a structural unit meets a flat cap: across 113 markdown
files, chunk sizes ran `p50 972` to `max 12315` characters against one 256-token cap — **43.6 % of
documentation text invisible**, re-measured live at 40.4 % (`DewFlow · research/PLAN_doc_rag_256_subchunking.md`).
On the code side a single whole-file chunk spanned `AppHost.cs` lines 7–573 *"of which only the first ~60
would ever reach a vector"*. Both were fixed by adding a fixed-length layer beside the structural one — and
**which is better, for which question shape, was never measured.** That is exactly a matrix question.

**The prediction, recorded before the run so a flat result reads as a finding rather than a disappointment:**
a flat window helps entry-point and configuration questions, where the material structural parsing drops
entirely, and hurts precise member lookup.

## 4. The three pieces

### 4.1 `CorpusSpec.Strategy` — after the engine, never before

One field, following the shape the tokenizer and the width already established: optional, three-state,
appended to `Canonical` only when declared so no stored variant re-identifies. Compared in
`CorpusIdentity.Refuse` only when both sides name one. `bench variants add --strategy`, and the listing shows
it beside the rest.

The naming grammar matters here more than for the other axes, because this is the one a reader compares by
eye: a variant's display name should carry `structural · 256 · bge-m3` against `window · 256 · bge-m3` rather
than hiding the difference in a hash.

### 4.2 The echo, KEPT — the audit trail the guard does not leave

Today the readiness check reads the engine's recipe, compares it, prints it and **discards it**
(`RunCommand.ApproveCorpusAsync` — only the compute arm survives, onto `VariantChoice.Served`). So a published
database says a run's variant *claimed* 256 tokens of bge-m3 and cannot say what actually answered.

That the guard blocks a mismatch is not a substitute. The block proves the two agreed at plan time; it leaves
no record of WHAT they agreed on, and a reader re-checking a published result a year later has only the
recipe's own claim. Store the echoed `CorpusIdentity` on the run beside the planned one — one migration, one
write at the point the approval already happens.

**This is what makes the parent plan's `bench variants verify` meaningful again**, and it is worth saying why
the verb was dropped rather than built: the guard refuses a mismatched cell *before it exists*, so a stored
run can never contain one and a verb that scanned for them would be scanning for the impossible. What it can
usefully do is print the stored echo beside the plan — a reading verb, not a gate.

### 4.3 The first grid

**Strategy × chunk size × question group**, one embed model, one engine, repeats ≥ 2, with the §3 prediction
recorded beforehand. The question-group breakdown is the point: a single average across groups would hide
exactly the trade the prediction names.

## 5. Build order

1. *(other repository)* the engine selects and echoes a chunk strategy.
2. `CorpusSpec.Strategy` + canonical + the comparison, mirroring the tokenizer.
3. The echo stored on the run, and a reading verb that prints it beside the plan.
4. The grid, prediction first.

Steps 3 is independent of step 1 and can land first; steps 2 and 4 cannot.

## 6. Test plan

xUnit v3 executable, never `dotnet test`; `PostgresFixture` for anything stored.

- Two specs differing only in strategy hash differently; a spec with no strategy hashes **exactly** as it does
  today — the regression that cannot be undone, pinned on the literal the way the tokenizer's is.
- A cell whose echoed strategy differs is blocked naming both; a non-declaring engine measures and is flagged.
  Separate tests, because collapsing them is the defect.
- The stored echo survives a round trip and is readable for a run whose variant has since been retired.

## 7. Definition of Done

- [ ] A variant states how its corpus was CUT, and two variants differing only in that are two corpora.
- [ ] Every result carries the corpus recipe that actually served it beside the one it was planned for.
- [ ] The reading verb prints both, and says *not recorded* for runs measured before the echo was stored.
- [ ] The first strategy × chunk-size grid has run at repeats ≥ 2 with its prediction recorded beforehand.
- [ ] `todo/README.md` updated; `research/architecture.md` records the strategy axis and the stored echo.
