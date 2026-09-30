# RESULTS — Mistral Medium 3.5 on the S7.3 plan and code gates

> Status: **measured, 2026-09-30.**
> - **What ran:** Mistral Medium 3.5 (`mistralai/mistral-medium-3-5`, through OpenRouter, at the vendor's default
>   thinking), over the same seven seeded tasks, gates, repeats, product commit, settings and assessor as
>   [RESULTS_gate_s73.md](RESULTS_gate_s73.md).
> - **The assessor:** every finding was read blind by Astra under `strict-v1`, the same assessor as S7.3.
> - **Not hand-checked:** strict percentages stay "not hand-checked" (E4).
> - **Scope:** Mistral's runs are on the same commit but a **separate report scope**; see *What was held fixed*.
>
> Related: [RESULTS_gate_s73.md](RESULTS_gate_s73.md), [module_gate.md](module_gate.md),
> [PLAN_gate_measurement_tail.md](../todo/PLAN_gate_measurement_tail.md).

## What was held fixed

| variable | value |
|---|---|
| suite | `gate-seeded#0d0da7662eab`, the S7.3 suite file `suite-s73b.json` unchanged |
| product | coai `3f351c05`, 0 dirty files, rebuilt 2026-09-30 in a clean worktree as the same Debug build |
| harness | `bench` `99f0cc5` |
| runs | plan `01a0f1b6`, code `01a0f1ba`: 7 tasks × 3 repeats = 21 cells each, isolated data directories, `--parallel 2 --per-endpoint 2` |
| reviewer | `openrouter-mistral-medium-3-5`: api, `https://openrouter.ai/api/v1`, dialect openai, thinking at the vendor's default (no `thinking` field), list price $1.50 / $7.50 per M tokens in / out, the other api rows' transport (8192 max tokens, 20 min, 3 follow-ups) |
| assessor | `codex-gpt-6-astra-exe`, `strict-v1`, as in S7.3 |
| reading | `bench gate report --gate … --scope <scope id> --rubric strict-v1 --run <campaign>` |

**The two populations.** Each gate has 21 cells: 7 tasks × 3 repeats.
- **Measured tasks:** 5 tasks, 15 runs. Every table below is over these, as in S7.3.
- **Calibration tasks:** js3 and ts2, 6 runs. The report puts them apart, and they are named where used.

Cell counts, "invalid" counts and the campaign cost totals are over **all 21 cells**, and say so.

**Why the scope differs.** The same commit rebuilt is not byte-identical: this build hashes `8b1c538643ca`, S7.3's
`44467c1c14a5`. The report refuses to mix two product binaries in one table. So Mistral's figures are reported from
their own scopes, `dabef85d5da4` (plan) and `6e8e3e1f1827` (code), and set beside S7.3's figures from its record.

Same product code and settings, different bytes. What that could change is not measured here.

## Plan gate (`01a0f1b6`)

Measured tasks, 15 runs per reviewer: every column below is over those 15. The S7.3 rows are from its record.

| reviewer | valid % | findings / run | high-value / run | overstated % | s p50 | tokens in / run | cost / run |
|---|---|---|---|---|---|---|---|
| **Mistral Medium 3.5** | 60 | **8.80** | **0** | **100** | **17.7** | 17 k | **$0.039** |
| grok-4.7 | 100 | 4.33 | 0.67 | 63.9 | 134 | 18 k | $0.085 |
| glm-5.3 | 100 | 5.53 | 0.67 | 42.5 | 176 | 16 k | $0.058 |
| Astra | 100 | 2.00 | 0.60 | 41.2 | 20 | 31 k | subscription |
| Opus 5.5 | 93.3 | 7.73 | 1.53 | 41.6 | 76 | 85 k | $0.47 |

- **Seeds are ~0 by construction** on this gate for every reviewer: the seeds are code defects, and the plan gate reads
  the plan text only.
- **The prediction was ill-posed on seeds.** "Fewer seeds than grok-4.7" was ill-posed here, as S7.3's was, and it is
  recorded as such. "Costs about as much as grok-4.7" was wrong: Mistral cost less than half as much.
- **The invalid cells are all `GoodEnough`:** 6 of the 15 measured runs (hence 60 % valid), and 11 of all 21 cells.
  The product's round budget ran out on Mistral's finding volume, the same pattern Opus showed.

## Code gate (`01a0f1ba`)

Measured tasks, 15 runs per reviewer: every column below is over those 15.

| reviewer | valid % | findings / run | **seeds hit / run** (of 2) | high-value / run | overstated % | s p50 | tokens in / run | cost / run | cost / seed |
|---|---|---|---|---|---|---|---|---|---|
| **Mistral Medium 3.5** | 46.7 | 24.87 | **0.13** | 0.53 | **71.4** | 99 | 253 k | $0.41 | $3.11 |
| grok-4.7 | 86.7 | 12.07 | 1.08 | 4.38 | 49.3 | 691 | 261 k | $1.06 | $0.98 |
| glm-5.3 | 100 | 15.67 | 0.93 | 3.87 | 39.4 | 406 | 236 k | $0.53 | $0.57 |
| Astra | 73.3 | 5.33 | 0.75 | 3.42 | 39.4 | 43 | 290 k | subscription | — |
| Opus 5.5 | 46.7 | 27.33 | 1.07 | 7.67 | 43.0 | 281 | 800 k | $4.50 | $4.22 |

**Mistral raises as much as Opus and finds almost none of what was planted.**
- **Seeds:** 0.13 of 2 seeds per run on the measured tasks, one seed per 7.5 runs, against 0.75–1.08 for the other
  four.
- **Cost per seed:** that low hit rate makes it $3.11 per seed despite the lowest cost per run among the API
  reviewers. That is five times glm-5.3's and three times grok-4.7's.
- **Assessment:** 71 % of its findings read as *overstated* and 0.53 per run as high-value. The other four are at
  39–49 % and 3.4–7.7.

The prediction, recorded with the run, was: "fewer seeds per run than grok-4.7 and Opus 5.5 (1.08 and 1.07 of 2); its
cost per run lies between glm-5.3's and grok-4.7's". Against S7.3's measured-task figures, $0.53 and $1.06, it held on
seeds and missed on cost: $0.41 is below glm-5.3's $0.53.

The plan prediction's cost clause was "about as much per run as grok-4.7". At $0.039 against grok's $0.085, that
missed too.

The invalid cells are again all `GoodEnough`: 8 of the 15 measured runs (hence 46.7 % valid), and 13 of all 21 cells.
On the calibration tasks, reported apart, it hit 0 seeds in 6 runs.

## Agreement

"The same issue" is Astra's cluster within a task. Astra carries its cluster keys from pass to pass, so Mistral's
clusters are matched against the S7.3 reviewers' issues.

The basis, the same as S7.3's agreement section:
- **A reviewer's issues** are the distinct (task, cluster) pairs from its VALID cells on all seven tasks.
- **"Alone"** means no one of S7.3's four reviewers raised that pair.
- **Jaccard** is the pairs both raised over the pairs either raised.

| | plan gate | code gate |
|---|---|---|
| Mistral's issues | 50 | 129 |
| raised by Mistral alone | **21 (42 %)** | **90 (70 %)** |
| shared with Opus / glm / grok / Astra (Jaccard) | 22 / 14 / 16 / 6 % | 15 / 11 / 9 / 5 % |

S7.3's four reviewers overlap each other at 28–36 % on code. Mistral sits further from all of them, and on code seven
of its issues in ten are its own. With the seed rate above, that reads as distance, not as unique finds.

## What it cost

| item | amount |
|---|---|
| plan campaign | $0.92, the product ledger's sum over all 21 cells |
| code campaign | $8.56, the product ledger's sum over all 21 cells |
| assessment | Astra, on the Codex subscription |

The per-run costs in the tables are over the 15 measured runs only. They do not multiply up to these totals, because
the totals also hold the 6 calibration runs, at $0.055 (plan) and $0.39 (code) per run.

The estimate before the run was $20–25, from glm-5.3's token use on the same cells.

## Harness notes from this run

- **The Claude assessor could not run at one turn.** Astra's Codex account was at its limit when the run finished, so
  the first assessment attempt used Opus 5.5 (Claude CLI 2.1.284). Every batch stopped at `Reached max turns (1)`: 33
  findings, recorded as `AssessmentFailure NoAnswer` under Opus's own verdict log.
  - The benchmark plan had recorded this in E4, and left raising the ceiling to the operator.
  - The operator raised it to 30, and PR #60 made it a flag, `bench gate assess --max-turns`.
  - Before it was needed, Astra came back, so no figure here depends on Opus.
- **T5's check on a real product passed** (run `01a0f1b7`, coai mcp 0.40.4, `--allow-product-change`). grok's spent
  xAI key came back as `the API refused the key … (HTTP 403)`. The first cell was refused as account-out, grok was
  benched, and the campaign ended `AccountOut` with exit 3: 0 cells settled, 7 pending. This is a check run, not a
  measurement, and it stays unfinished by design.

## What this does and does not show

- **Shown:**
  - on these seven tasks, Mistral Medium 3.5 through OpenRouter produces a high volume of findings, fast and cheaply;
  - by Astra's strict reading, it lands on the planted defects far less often than any reviewer S7.3 measured;
  - it marks most of its findings in a way the assessor reads as overstated.
- **Not shown:**
  - whether its findings are right more or less often in general: the strict rates wait for the hand-check (T1);
  - whether a different effort or thinking setting, or Mistral's own API instead of OpenRouter, would change it. One
    transport was measured, at the vendor's default thinking;
  - whether the separate product scope matters: same commit, different bytes.
