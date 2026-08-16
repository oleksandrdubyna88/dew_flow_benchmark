# PLAN — a benchmark run must not leave a corpus behind

> Status: **plan only, nothing implemented yet.** Scope: whatever in this repository creates or names a RAG
> corpus for an arm. Raised 2026-08-15 from a measurement taken in `dew_flow_rag_qln`.
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

## What makes this tractable

A collection's NAME is its claim: `code_{projectId}_{branchHash}_{recipeHash}`. So ownership is decidable by
reading the catalog — no extra bookkeeping, and no risk of a registry drifting from reality.

## Decide, then build

**Which corpora does a run own?** Three shapes, and the answer decides everything:

| shape | who owns the corpus | clean-up |
|---|---|---|
| the arm reuses an existing project's index | the operator | **never** delete — it is someone's working index |
| the arm builds a corpus for the cell | the run | delete when the run ends, unless kept deliberately |
| the arm builds one to be compared across runs | the matrix | keep, and account for it |

The middle row is the one that leaks. The rule proposed: **a corpus a run created is deleted when that run
finishes**, and a run that wants to keep one says so — the opposite default from today, where keeping is
implicit and free until it isn't.

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
   decidable from the name alone.
2. **Delete-on-finish** for corpora a run created, including on a failed or cancelled run — the failure path
   is the one that leaks in practice.
3. **A retention listing**: what exists, whose run made it, how old, how big, and a delete button.
4. **Report it in the run's own output**: a run that created and removed 24 corpora should say so, because a
   number nobody prints is a number nobody notices growing.

## Test plan

- A run that creates a corpus and fails still removes it.
- A corpus belonging to a real project is never selected by any sweep, even when it matches every other rule.
- The listing's size total matches what the store reports.

## Definition of Done

- [ ] A corpus created by a benchmark run is identifiable from its name alone.
- [ ] A finished run leaves none behind unless it was told to keep them; a crashed run leaves none either.
- [ ] What is deliberately kept is listed with its age and size, and removable in one action.
- [ ] Runs report how much they created and released.
