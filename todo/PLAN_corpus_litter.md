# PLAN — a benchmark run must not leave a corpus behind

> Status: **plan only — and the blocker it names has EXPIRED, while a larger precondition it did not know
> about has taken its place (audited 2026-08-23).** Scope: whatever in this repository creates or names a RAG
> corpus for an arm. Raised 2026-08-15 from a measurement taken in `dew_flow_rag_qln`.
>
> **The documented blocker is gone.** §"Build order" step 1 says the prefix *"is a per-request field on both
> sides … but no HTTP surface carries it, so a caller cannot choose one today"*. It does now:
> `dew_flow_rag_qln` carries `CollectionPrefix` on the index-pass, index-state and search endpoints, with
> `CollectionNamespace.RefusePrefix` validating it, and it even ships a listing that says whose each
> collection is (`03cc363`). Nothing on the engine side stops this plan any more.
>
> **What replaced it is bigger and changes the build order.** *Nothing in this repository creates a corpus.*
> `IndexPreparation` — the four-state machine, its owner, its heartbeat, its stranding rule, the
> `index_preparations` table and `PostgresPreparationStore` — appears **only in its own definition and its
> tests**: no host, no use case and no DI registration references `IPreparationStore`. Verified 2026-08-23 by
> grep across `src/`, `hosts/` and the container registrations. A run reads whatever the operator already
> indexed (`ApproveCorpusAsync` → `InspectAsync`) and builds nothing.
>
> So **this harness cannot leak a corpus today**, and step 1 has nothing to name: a `bench_` prefix is a claim
> about corpora this repository creates, and it creates none. The plan is PREVENTIVE, which is a fine thing to
> be — the 22 GB of rubbish it was raised from is real and the exposure at matrix scale is real — but its
> first step cannot land before the thing that would litter exists. That wiring is
> [PLAN_variant_matrix.md](PLAN_variant_matrix.md) step 5, which owns index preparation and shipped its
> SHAPES without a caller.
>
> **Revised order, therefore:** the prefix and delete-on-finish land *with* the preparation wiring, in the
> same change, so the namespace exists from the first corpus this benchmark ever builds rather than being
> retrofitted over a population that already leaked. Retrofitting is what the source incident had to do.
>
> Related: that repository's `todo/PLAN_runtime_panel.md` (the panel that lists unclaimed collections) and
> `research/module_indexing.md` (why a collection's name is its claim).

## The symptom, measured elsewhere and applicable here

In `dew_flow_rag_qln`, Qdrant held **24.38 GB of which 22 GB was rubbish**: 22 collections nobody wrote to.
Three came from one rename. **Nineteen came from test runs** — the live tests minted a fresh project id per
run, so every run created a collection and nothing ever removed it. Deleting them took the volume to 2.13 GB.

Those tests are fixed (fixed project ids, so a run reuses one collection and is incremental besides). This
plan exists because **a benchmark has the same exposure at a much larger scale and cannot rely on
discipline**: the operator's words were "у меня будут тысячи тестов".

An arm that builds its own corpus is the expensive case. On `dotnet/aspnetcore` one corpus is **75 218 points
and roughly 2 GB**; a matrix of 24 variants is ~1.2 million points. A sweep that leaves one behind per cell
fills a disk in an afternoon, and the failure arrives as "no space" during an unrelated run rather than as
anything pointing at the benchmark.

**And 24 was the number before the engine plans landed.** `dew_flow_rag_qln · todo/PLAN_search_variant_axes.md`
adds `ChunkTokens` (256 | 512, §3.3) and a second dense embedder (§3.4) to the recipe, and collection identity
already fingerprints model, shape, window and overlap field by field
(`dew_flow_rag_qln · src/Rag.Domain/Corpus/CorpusVariant.cs:92-93`). So the resident set is 24 × 2 × 2 =
**96 corpora per target repository** — roughly **190 GB of Qdrant for one repo**, against the 24.38 GB that
was already the crisis above. The budget and the eviction rule for that set are
[PLAN_variant_matrix.md](PLAN_variant_matrix.md) §3.4a; what this plan owns is which corpora may be deleted at
all, which is the paragraph below.

## What makes this tractable

A collection's NAME is its claim: `code_{projectId}_{branchHash}_{recipeHash}`. So ownership is decidable by
reading the catalog — no extra bookkeeping, and no risk of a registry drifting from reality.

## Decide, then build

**Which corpora does a run own?** Three shapes, and the answer decides everything:

| shape | who owns the corpus | clean-up |
|---|---|---|
| the arm reuses an existing project's index | the operator | **never** delete — it is someone's working index |
| the arm builds an AD-HOC corpus for the cell | the run | delete when the run ends, unless kept deliberately |
| the arm builds one to be compared across runs | the matrix | keep, and account for it |

The middle row is the one that leaks. The rule proposed: **an ad-hoc corpus a run created is deleted when
that run finishes**, and a run that wants to keep one says so — the opposite default from today, where
keeping is implicit and free until it isn't.

**Delete-on-finish applies to the middle row only, and this is worth stating because the sibling plan
assumes the opposite for its own corpora.** A variant-matrix corpus is the THIRD row, not the second: it is
built to be compared across runs by construction, and `ExpandAsync`
([PLAN_variant_matrix.md](PLAN_variant_matrix.md) §3.2) exists precisely so a settled test reopens weeks
later when a variant or a subject is added. Deleting its corpus when the run finishes would make that
expansion a full re-index of a tree that never changed — 24 minutes per corpus, on the exact operation the
matrix was designed around. So a corpus is tagged with its ownership row at creation, and only the second row
is swept on finish. A corpus with no recorded owner is treated as the second row, because the default that
leaks is the one that must not be the default; a matrix corpus that failed to be tagged is a bug the sweep
will announce by deleting something, which is better than one that silently accumulates.

**Retention when a corpus outlives its run.** A cell's corpus may be worth keeping while its results are being
read. Then: keep it for a stated window, and surface it rather than hide it — "4 corpora from runs older than
7 days, 8.1 GB, delete" — as a button, never a silent sweep. A benchmark that deletes evidence on a timer is a
benchmark whose old numbers cannot be re-checked.

**A prefix that says whose it is.** Corpora a benchmark creates should carry a namespace of their own
(`bench_…` rather than `code_…`), so that "sweep everything this tool made" is expressible without deciding
anything about a real project's index. This is the cheapest half of the whole plan and probably the first
thing to do.

## Build order

1. **Name benchmark corpora under their own prefix.** Nothing else can be swept safely until "ours" is
   decidable from the name alone. The engine half already has the seam and it is not exposed: the prefix is
   a per-request field on both sides (`dew_flow_rag_qln · src/Rag.Application/Indexing/FastLanePipeline.cs:20`
   and `src/Rag.Application/Search/SearchService.cs:21`, both defaulting to `"code"`), but no HTTP surface
   carries it, so a caller cannot choose one today. Widening those endpoints is the whole change — named on
   the engine side in `dew_flow_rag_qln · todo/PLAN_corpus_variants.md`.
2. **Delete-on-finish** for AD-HOC corpora a run created, including on a failed or cancelled run — the
   failure path is the one that leaks in practice. Matrix corpora are excluded by their ownership tag.
3. **A retention listing**: what exists, whose run made it, how old, how big, and a delete button.
4. **Report it in the run's own output**: a run that created and removed 24 corpora should say so, because a
   number nobody prints is a number nobody notices growing.

## Test plan

- A run that creates a corpus and fails still removes it.
- A corpus belonging to a real project is never selected by any sweep, even when it matches every other rule.
- The listing's size total matches what the store reports.

## Definition of Done

- [ ] A corpus created by a benchmark run is identifiable from its name alone.
- [ ] Every corpus carries which ownership row it belongs to; delete-on-finish selects the ad-hoc row only,
      and a matrix corpus survives the run that built it.
- [ ] A finished run leaves no AD-HOC corpus behind unless it was told to keep it; a crashed run leaves none
      either.
- [ ] What is deliberately kept is listed with its age and size, and removable in one action.
- [ ] Runs report how much they created and released.
