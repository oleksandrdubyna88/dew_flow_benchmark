# RESULTS — which reviewer models to choose for coai's gates

> Status: **conclusions drawn 2026-09-30** from the three measurements below. It adds no new measurement.
>
> - **Feature gate:** [RESULTS_gate_feature_first_real_report.md](RESULTS_gate_feature_first_real_report.md). The
>   2026-09-27 calibration: four API models, 7 tasks × 3 repeats.
> - **Plan and code gates:** [RESULTS_gate_s73.md](RESULTS_gate_s73.md) (grok-4.7, glm-5.3, Astra and Opus 5.5, with
>   Fable after them) and [RESULTS_gate_mistral.md](RESULTS_gate_mistral.md) (Mistral Medium 3.5). Same 7 tasks × 3
>   repeats.
> - **Assessment:** every figure is over the measured tasks, 15 runs per reviewer, read blind by Astra under
>   `strict-v1`.
> - **Not hand-checked:** the strict supported-rates wait for the hand-check (T1 of
>   [PLAN_gate_measurement_tail.md](../todo/PLAN_gate_measurement_tail.md)). The ranking below rests on planted defects
>   found and on the assessor's reading of each finding, not on a person's verification.

## The evidence, per gate

**Feature gate.** The four API models are from the 2026-09-27 calibration, 15 runs each. Mistral was added on
2026-09-30 with **5 runs** (1 repeat) in a separate scope ([RESULTS_gate_mistral.md](RESULTS_gate_mistral.md)), so its
row is a smaller sample.

| reviewer | seeds hit / run (of 2) | high-value / run | overstated % | cost / run | cost / seed |
|---|---|---|---|---|---|
| grok-4.7 | **1.47** | **1.8** | 22 | $0.54 | $0.37 |
| glm-5.3 | 1.07 | 0.8 | 22 | **$0.27** | **$0.25** |
| deepseek-v4-pro | 0.86 | 0.57 | 41 | $0.29 | $0.36 |
| qwen3.8-max | 0.80 | 0.53 | 24 | $0.35 | $0.43 |
| Mistral Medium 3.5 (5 runs) | 0.40 | 0.20 | 33 | $0.43 | $1.08 |

**Code gate.**

| reviewer | valid % | seeds hit / run (of 2) | high-value / run | overstated % | cost / run | cost / seed | s p50 |
|---|---|---|---|---|---|---|---|
| grok-4.7 | 86.7 | **1.08** | 4.38 | 49 | $1.06 | $0.98 | 691 |
| Opus 5.5 | 46.7 | 1.07 | **7.67** | 43 | $4.50 | $4.22 | 281 |
| glm-5.3 | **100** | 0.93 | 3.87 | 39 | $0.53 | **$0.57** | 406 |
| Astra | 73.3 | 0.75 | 3.42 | 39 | subscription | — | **43** |
| Mistral Medium 3.5 | 46.7 | 0.13 | 0.53 | 71 | $0.41 | $3.11 | 99 |

**Plan gate.** Seeds cannot be hit here: they are code defects, and this gate reads only the plan text. So the
comparison is high-value findings and overstatement.

| reviewer | high-value / run | overstated % | cost / run |
|---|---|---|---|
| Opus 5.5 | **1.53** | 42 | $0.47 |
| Fable 5.1 | 1.36 (15 cells, before its spend limit) | — | about 4 × Opus |
| grok-4.7 | 0.67 | 64 | $0.085 |
| glm-5.3 | 0.67 | 43 | **$0.058** |
| Astra | 0.60 | 41 | subscription |
| Mistral Medium 3.5 | 0 | 100 | $0.039 |

**The reviewers complement each other.** On plans, 60 % of distinct issues come from one reviewer alone, and on code
46 %. The closest pairs share about a third of their issues: glm & grok 36 % on code, grok & Opus 34 % on plans. More
than one reviewer finds more, which is what the gate's multi-vendor design assumes.

## The choice

| gate | default reviewers | add when |
|---|---|---|
| **feature** | grok-4.7 + glm-5.3 | — |
| **code** | grok-4.7 + glm-5.3 | Opus 5.5 for an epic's final code round or a security-sensitive change, where about $4.50 a review is acceptable |
| **plan** | glm-5.3 + Opus 5.5 | grok-4.7 as a third reviewer only when cost is not a concern (see below) |

**1. Feature and code gates: grok-4.7 + glm-5.3.**
- They are the top two API reviewers on the two gates where seeds can be hit: feature (1.47 and 1.07) and code (1.08
  and 0.93).
- glm is the cheapest per seed on both gates, and 100 % valid on each.
- grok finds the most among the API models.
- They overlap only about a third, so the pair covers more than either alone.

This matches the product's own feature-gate recommendation, reached independently.

**2. Plan gate: glm-5.3 + Opus 5.5.**
- Opus finds more than twice the high-value findings of any API reviewer (1.53 against 0.67), at about $0.47 a
  review.
- glm is the cheapest of the rest ($0.058), at a 43 % overstatement.
- grok ties glm on high-value findings on plans, but 64 % of its plan findings read as overstated, the most of any
  reviewer but Mistral. It is not in the plan default.

**3. Opus 5.5 on the code gate: only in the cases the table names.**
- It ties grok on seeds (1.07) and has the most high-value findings (7.67 per run), at about $4.50 a review, four times
  grok.
- **Half its code cells hit the product's round budget** (`GoodEnough`), at the S7.3 settings (`f7ea3636f571`). A
  budget large enough for it to finish was not measured. Raise the role's round budget and measure before relying on
  it as a code reviewer.

**4. Astra: add it where review latency matters and a Codex seat is available. The evidence does not show it adding
coverage.**
- It answers in 20–45 s, the fastest reviewer, with the fewest findings at the lowest overstatement.
- Its 0.75 code seeds per run are below both default reviewers, so it is a speed choice, not a coverage one.
- Its Codex subscription ran dry during S7.3 and came back on 2026-09-30. It is a usage-limited seat: expect gaps.
- It is also the benchmark's assessor, so its own findings are counted apart and its figures are the least
  independent.

**5. Do not use:**
- **Mistral Medium 3.5:** the fewest seeds on both gates where seeds can be hit.
  - **Code:** 0.13 seeds per run, with 71 % of its findings read as overstated.
  - **Feature:** 0.40 per run over 5 runs, against 0.80–1.47 for the others.

  It is fast and cheap per run, and it misses what was planted. Its cost per seed is the highest of the API
  reviewers.
- **deepseek-v4-pro:** the overstatement outlier on the feature gate (41 %, and 100 % on the calibration tasks).
- **qwen3.8-max:** the fewest feature-gate seeds of the four API models, at a higher cost than glm.

**6. Undecided: Fable 5.1.** It sits beside Opus on the plan gate (1.36 high-value per run) at about four times the
cost. Its code gate reached only 4 cells before its spend limit, which is not a measurement.

## What would change this

- **The hand-check (T1).** If the strict supported-rates reorder the reviewers, correctness outranks the seed counts.
- **Other settings.** Each model was measured on one transport and one thinking and effort setting.
- **Cost and accounts.** grok's $50 xAI balance lasted one week of benchmarking (about $1 per code review). A gate
  that reviews every commit should budget for that, or lean on glm.
