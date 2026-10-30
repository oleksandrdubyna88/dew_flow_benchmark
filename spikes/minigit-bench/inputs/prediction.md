# Prediction, bare part

Quoted from `todo/PLAN_minigit_stack_bench.md` section 11 at `85c54b790a37479a32fc88b7b0b4353652fed601`, a commit of origin/main (the full prediction has been public since `01b7459`); `prereg/extract-prediction.mjs` wrote it. Process clauses are elided and marked; items 3, 4 and 6 are process items and stand in the parent at the named commits.

> *(The ranges below are descriptive expectations about medians and ratios, stated before the run — not effect sizes
> claimed as findings.)*

1. Bare cost: C# > TS in ≥2 of 3 pairs; median C#/TS 1.15–1.5×. Basis: banteg's own tokens re-billed give 1.32×, and C#
   adds a project file, a wrapper and build loops. Per cell (v1+v2): TS $0.8–2.5, C# $1.0–3.5.

2. Bare wall: C#/TS 1.3–1.8×; the API-time ratio smaller (1.1–1.4×) — the difference is dotnet tool time.

5. Quality: every bare session passes the visible tests; bare held-out median `percent` ≥ 90 % in both stacks with at least one bare failure; […process clause — registered with the process scope…]; no stack difference > 1 `defect`.

7. Surprises named in advance: TS costing ≥ C# in ≥2 bare pairs; […two process clauses — registered with the process scope…].

## What the bare-first deviation weakens

Extracted from the E3b plan (`todo/PLAN_minigit_stack_bench_E3_bare.md`):

1. **The interaction** (prediction 3) compares windows days apart instead of interleaved blocks: server-side model changes,
   API latency and time of day differ between arms. D12 catches an alias resolving to a new id, not a change behind one id.

2. **No arm-lead draw** for the bare cells: order between arms is confounded with time.

3. **T3 is armed for bare cells on bare evidence** (the A/A and the E2.S2 smoke); the process arm re-decides at P1.

4. **The process treatment is finalized after bare results are visible** (E2.S4). Parent §6 already fixes it; any change after
   the first bare pair is a dated amendment applied to both stacks, never motivated by one stack's bare result.

5. **The prediction is registered in two parts**; the process part is public since `01b7459` with its dated edits, all
   before any cell, so the split costs no evidence.

The C#/TS comparison **inside** the bare arm keeps its concurrent pairs and loses nothing.
