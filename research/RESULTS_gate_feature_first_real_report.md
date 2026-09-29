# RESULTS — the feature gate's first real report through the C# benchmark

> Status: **measured, 2026-09-28.** This is the first time the `Gate` context's own report (E6, `5ad6e7c`) was read
> over REAL data rather than a fixture: the 2026-09-27 calibration of four API reviewer models, brought in by E5's
> importer. The suite's task list was recorded with `bench gate suite record` over the operator's suite file (7 tasks)
> and the four coai-bench case suites (2 + 2 + 1 + 2 tasks).
>
> Related: [module_gate.md](module_gate.md) (the context, the import, the report),
> [PLAN_coai_gate_model_benchmark.md](PLAN_coai_gate_model_benchmark.md) (E7, the first re-run through the C# driver — IMPLEMENTED 2026-09-29, see RESULTS_gate_aa_cs2.md and RESULTS_gate_s73.md).
> The measurement itself, its protocol and its recommendation are published by the product:
> `research/RESULTS_feature_reviewer_models.md` in the ConnectOtherAIs repository.

## How it was read

```
bench gate report --gate feature --scope a9f0231334d8 --rubric strict-v1 --db <bench>
```

- **Scope** `a9f0231334d8`: feature gate · suite `gate-seeded#fb80578c897f` · settings `cf7c7afb4494`.
- **Product:** imported from calib-py phase 2. The binary is not hashed, because the harness that produced it did not record the hash.
- **Rubric:** `strict-v1` only. No figure below is over any other rubric.
- **Runs:** 84 settled runs from 92 attempts (8 superseded by their re-run), 4 models × 7 tasks × 3 repeats.

## Measured tasks (calibration tasks excluded)

| reviewer | runs | valid % | findings/run | seeds hit (range) | high-value/run | overstated % | s p50 | s p90 | tokens in/run | cache % | cost/run | cost/seed |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| grok-4.7 | 15 | 100 | 4.33 | **1.47** (1–2) | **1.8** | 22.2 | 548.7 | 868.3 | 214 191 | 49.1 | $0.539 | $0.368 |
| glm-5.3 | 15 | 100 | 3.8 | 1.07 (0–2) | 0.8 | 22.2 | 325.9 | 1 023 | 161 939 | 55.3 | **$0.271** | **$0.254** |
| deepseek-v4-pro | 15 | 93.3 | 1.8 | 0.86 (0–2) | 0.57 | 40.9 | 316.8 | 487 | 187 012 | 63.2 | $0.289 | $0.361 |
| qwen3.8-max | 15 | 100 | 5.2 | 0.8 (0–2) | 0.53 | 23.5 | 556.5 | 704.8 | 183 598 | 59.8 | $0.347 | $0.434 |

The strict percentages (supported %, supported+partial %) read **not hand-checked** for every model. The counts behind
them are stored, but the report withholds a strict rate until a person records a hand-check over a drawn sample (E4).
That hand-check is E7's work. **Assessment failed: 0** for every model.

## Calibration tasks, reported apart

Two of the seven tasks settled the transport during calibration, so they describe the tuning as much as the model.

| reviewer | runs | valid % | seeds hit | high-value/run | overstated % | s p50 | cost/run |
|---|---|---|---|---|---|---|---|
| grok-4.7 | 6 | 100 | 1.67 | 3.67 | 3.1 | 591.3 | $0.547 |
| glm-5.3 | 6 | 100 | 0.83 | 1.5 | 25 | 368.4 | $0.272 |
| deepseek-v4-pro | 6 | 100 | 0 | 0.17 | 100 | 356 | $0.276 |
| qwen3.8-max | 6 | 100 | 0.33 | 0.67 | 14.3 | 712.2 | $0.388 |

## All tasks, calibration included (the population the Python harness published)

| reviewer | runs | valid % | seeds hit | high-value/run | s p50 | tokens in/run | cost/run | cost/seed |
|---|---|---|---|---|---|---|---|---|
| grok-4.7 | 21 | 100 | 1.52 | 2.33 | 578.1 | 215 285 | $0.541 | $0.355 |
| glm-5.3 | 21 | 100 | 1.0 | 1.0 | 334 | 175 115 | $0.271 | $0.271 |
| deepseek-v4-pro | 21 | 95.2 | 0.6 | 0.45 | 327.9 | 185 212 | $0.285 | $0.499 |
| qwen3.8-max | 21 | 100 | 0.67 | 0.57 | 561.1 | 188 046 | $0.359 | $0.538 |

Against the Python harness's `results.json` for the same population, **134 of 144** numbers are identical (E5). The 10
that differ are:

- **8 strict percentages** are withheld as *not hand-checked*.
- **deepseek-v4-pro's** seeds hit (0.60 against 0.57) and high-value per run (0.45 against 0.43) differ. Its one invalid run
  found nothing. This context counts only a VALID empty run as an assessed zero; Python counted the invalid one too
  (12/20 against 12/21).

**Variance:** a seeds-hit spread is stated for 27 of 28 task × reviewer pairs. It is withheld for the one pair with
fewer than three assessed repeats.

## What this confirms

- **The ranking is the product's recommendation, reached independently.** Reading the report without the calibration
  tasks does not change the order. grok-4.7 finds the most seeded issues and the most high-value findings; glm-5.3 is the
  cheapest per seed and the fastest of the two with a 100 % valid rate. The product's measurement ticks exactly these two.
- **deepseek-v4-pro's overstatement is the outlier:** 40.9 % on the measured tasks, 100 % on the calibration tasks,
  against 22–24 % for the others.
- **The report reproduces the harness it replaces.** The only differences are the rules this context adds on purpose:
  the hand-check gate and valid-only zeros. That is the condition E7 needs before its re-run through the C# driver can be
  compared with this one.
