# Measured lessons — the evidence this benchmark is built on

> Status: **carried over 2026-08-14** from an earlier measurement programme run against a different
> codebase (the DewFlow / `claudeRag` monorepo, eval series 1–8, roughly 2026-07 … 2026-08). That
> repository is **out of scope for this project and is not touched**; this document exists so nothing
> here depends on having it checked out.
>
> Every number below was produced by a run, not by reasoning. They are reproduced because each one
> either **refutes something plausible** or **explains a guard** in
> [todo/PLAN_rag_bench_repo.md](../todo/PLAN_rag_bench_repo.md) and in the domain types. Read this
> before proposing a shortcut: most attractive shortcuts here have already been measured and lost.
>
> **What does not transfer.** The absolute quality numbers are about one engine, one corpus and one task
> set. Nothing here says "your retrieval should score 88/182". What transfers is the **shape of the
> failures** — and those turned out to be properties of measurement, not of that engine.

## 1. A sweep finds winners that are not there

Three separate configurations were chosen on convincing evidence and then reversed by a wider check.

| chosen on | reversed by |
|---|---|
| Reranker pool 20 beat the shipped 50 on a 50-task grid: **32 matched / ΣMRR 1.628** against **29 / 1.352** (pool 10 scored 29/1.612, reranker off 29/1.552) | The full set, 64 tasks / 182 expectations: **pool 50 = 88/182, pool 20 = 80/182**; the opaque-name slice 31 against 25, the architecture slice 8 against 5. A narrow pool sharpens a single-target answer and starves a multi-target one |
| Filtering the semantic channel to opaque-named members — "the first configuration that improves both sides instead of trading one for the other" — **60/107** against 59 (all) and 51 (off) | The same flag on the full set two days later: **64/182 against 88/182**. It gutted the very slice it existed to protect (opaque 31 → 14, coverage 62 % → 28 %), and was reverted within the hour |
| A 28-cell grid of signal combinations × query registers was expected to separate the registers | Predicted **flat before it ran**, and was. The prediction was recorded in advance precisely so flatness would read as a confirmation rather than a disappointment |

**What this buys the design.** Repeats defend against variance *within* one configuration; the
selection/held-out split defends against choosing *among many*. They are different guards and this
benchmark carries both. A configuration that won only on the half that selected it renders as
`Unproven` — not as "worse", and not as a result.

The second row also carries the sharper lesson: **a measurement is only valid against the corpus it
ran on.** The reversal was not a mistake in either run; 3 234 members had been re-described in
between, and the flag and the signal had come to select against each other. Everything measured before
that point reverted to being a hypothesis. This is why the target here is `(repoUrl, commitSha)`.

## 2. Ranking cannot fix what admission never let in

With the reranker off and the candidate pool read to its end, ten natural-language queries returned
**126–145 candidates out of 7 239, and the target was in none of them for 9 of 10** (the tenth sat at
rank 14).

> No reranker, no fusion reweighting and no rescoring can promote a member that never enters the
> candidate set — this closes a whole class of proposed fixes.

Two things follow. First, a probe that answers "recall or ranking?" is worth having **as a by-product
of every run**, which is what the white-box funnel is for; obtaining that answer once cost a
purpose-built expedition. Second, a first probe at `limit=100` proved nothing because the pipeline
silently capped the response at 20 through the reranker's return count — **read the raw fused list,
not the reranked response, when asking a recall question.**

Related, and measured on the same results: asked at *file* granularity instead of *member*
granularity, the same searches answer roughly twice as well in every category — the architecture slice
goes 3 % → 35 % inside a window of 20, and 19 % → 71 % read to full depth. That is a property of
granularity, not of conceptual questions.

## 3. Cost, ceilings and the ways a run lies about itself

| observation | consequence for this benchmark |
|---|---|
| A reranker row with an **empty model id** did not fall back to "the local model" — it resolved to the installation's system default, a paid cloud model, and an arm labelled "local, $0" was one invocation from billing ~100 requests | `ModelRef.Parse` refuses an unset id. Never a fallback |
| A context-compaction ceiling was configured, believed and reasoned from for a whole series; it was a knob of the *local tool loop* and reached **no CLI arm at all**, so a real degradation was attributed to a flooded window that never happened | `Budget` records the runtime that **accepted** it; unverified says so |
| A stale pinned port left the cross-encoder **dead for four arms** — `reranker = 0, sent = 0` — while the settings page still read `CrossEncoder`. Only a search response exposed it | Engine capability is declared and echoed, never assumed. A run records the engine and index fingerprint that actually served it |
| The eval command without an explicit run id wrote into *the project's newest run whatever its status*, and honoured `--label` only on a path it never took: **~14 evaluations overwrote each other in one session** | There is no implicit run selection. A command creates a run or names one |
| Repeat spread reached **4 points — the same magnitude as the effects being chased**; repeated control legs of one configuration diverged by up to **65 % on input tokens** | `n = 1` cannot rank. The report refuses to |
| Balancing leg order by the repeat index alone deals **2:1 at odd repeat counts**, identically for every question, so the bias never averages out | `Matrix` rotates on a global slot counter; a test reproduces the naive scheme |
| A refused tool call and an executed one were indistinguishable, because the ledger recorded a result's **length** rather than its outcome. A read-only guarantee was asserted on that basis for months and was false | `ToolCall.Refused` is a field, and "not captured" is a distinct state from "empty" |

## 4. What retrieval was and was not worth

Reported because it shapes what is worth *measuring*, not because it settles anything here.

- **Tools against no tools, same model, same questions, same turn cap**: a plain-filesystem tool set
  scored **36/63** against a full retrieval surface's **37/63**, while retrieval cost **52 % more
  wall-clock**. One question inverted it violently in the other direction (8/8 in 254 s against 0/8 in
  1 058 s), which is why per-task columns matter more than totals.
- **The same four tools behind a different surface shape** scored **4/63** — nine times worse — from
  the form of the surface alone. Surface shape is a variable, hence lanes.
- **Two engines compared across three models** came out indistinguishable; the cheap model on the
  cheap engine bought ~90 % of the expensive result for ~20 % of the money. Series cost: $164.16.
- **An LLM re-scorer reading full descriptions** lost to a cross-encoder **70/182 against 85** — and 15
  expectations stopped being *found* at all, not merely reordered.

**Read this section with its confound stated, because it is a large one.** Every number above came from
a **single small repository the same team wrote**, so "retrieval bought one point out of sixty-three" may
be a property of the *corpus* rather than of retrieval: on a small tree `grep` is cheap, and cheap `grep`
is precisely the condition under which retrieval cannot win. The direction on a large unfamiliar
codebase is **not predicted** — `grep` gets dearer with size, which helps retrieval, while retrieval also
gets *worse* with size, which does not: published work has measured embedders collapsing from 71.7/96.9
to **20.3/48.0** when moved into an agentic setting, with Recall@100 falling on dense repositories.

This is the strongest single argument for the design in [the plan](../todo/PLAN_rag_bench_repo.md): the
target is a **parameter**, and a finding that has only ever been reproduced on one corpus is a hypothesis
about that corpus until it is run against another.

**And there is no public set to borrow.** Of the repository-QA benchmarks surveyed, every one is Python;
the only C# set found is patch-and-test rather than question-answering. So the **methodology** can be
borrowed — a pull-request → gold-answer → discrete-facts pipeline, whose last step is exactly what an
`AnswerContains` expectation already is — but the data has to be authored. Two properties belong on every
authored question and both are cheap to forget: a seed change **newer than any plausible training
cutoff**, and a deliberate **memorisation trap** — a question whose obvious answer is the well-known one
and whose correct answer is not.

## 5. How agents actually search

A model working with no retrieval tools, five tasks, every call recorded: **not one of 37 searches was
in natural language.** Every opening move was an alternation of guessed CamelCase identifiers, often
followed by a structural glob, with documentation consulted second and searched by heading regex.

Replaying those wordings against a semantic pipeline beside the hand-authored concept sentences: the
same recall (3 of 9 targets each), differing only in rank (MRR 0.094 against 0.064).

**Consequences here.** The register a question is *authored* in is not the register it will be *asked*
in, so a lane's preamble is part of its identity. And a benchmark whose questions are all written in
polished prose is measuring a register its consumer does not use.

## 6. Where the time went

From one instrumented arm (10 questions, per-call latency recorded):

| channel | calls | median | total |
|---|---|---|---|
| semantic search | 39 | **7 996 ms** | **332.1 s** |
| doc search | 8 | 894 ms | 14.9 s |
| graph calls | 3 | 433 ms | 8.0 s |
| file read | 63 | 13 ms | 1.1 s |

The same model's no-retrieval arm spent **16.7 s in total** across all of its tools.

Component timings, warm, same query: embedding **0.38 s** on the integrated GPU against 0.23 s on the
discrete card; **cross-encoder rerank of 20 documents: 3.5–3.8 s** (2.2 s on the discrete card).
Measured components summed to ~4.3 s of an ~8 s call — **roughly half was never attributed**, and the
instrument that should have shown it printed the *sum of the stages it knew about* and called that the
total. A sum cannot show a missing part.

**Two retracted explanations, both plausible, both wrong**, recorded because the pattern is the point:
"search is slow because it runs on the integrated GPU" was wrong by an order of magnitude (the whole
card difference on the embed is 0.15 s), and "the literal search is slow because of a full tree walk"
was wrong too — the walk was 250 ms of an 11 220 ms call; the cost was reading 1.7 GB, of which 1.66 GB
was a gitignored build tree. Both were settled by measuring components before optimising.

**Consequence here.** Time is reported in three buckets — tools, thinking, infrastructure wait — and
the third exists because a busy accelerator otherwise reads as a slow model.

## 7. The synthetic asset can be quietly poisoned

An audit of 7 086 described members and 34 736 generated questions found the generator **copying the
prompt's own illustrative examples**: 43 % of members carried at least one verbatim template sentence,
26 % had nothing else, and **11 617 of 34 736 stored question rows were one of five sentences** (one of
them stored 2 536 times). Those vectors were live and serving searches.

**Consequence here.** Any generated asset gets a hygiene gate before it is measured, and a null result
from a poisoned asset is a verdict on the asset, not on the mechanism. The offline bake-off pattern —
score candidate prompts against stored material without writing to the index — cost $0 and is the
right shape for that check.

## 8. Provenance

These findings live in the DewFlow / `claudeRag` repository under `research/` (chiefly
`RESULTS_rag_eval_v3.md`, `PLAN_eval_v8/`, `PLAN_search_latency_where_the_eight_seconds_go.md`,
`RESULTS_native_toolset_arms.md`). That repository is **not a dependency of this one** and is not
modified by this project. Anything quoted here that later needs re-checking should be re-measured by
this benchmark against a pinned commit rather than looked up there — which is, after all, the point.
