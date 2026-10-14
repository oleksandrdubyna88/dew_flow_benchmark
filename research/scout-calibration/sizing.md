# Scout sizing — how many tasks would a decision run need

> **reported, not a gate** (plan D7, §3.8). Run `01a13812-0a7d-7720-ac97-a66df19042db` (pilot). The pre-registered decision, simulated 1000 times per task count and per true Δ: seed 20261013, 3 runs per task, 1 task per repository, each simulated decision's clustered bootstrap at its pre-registered seed with 1000 resamples. Target: ≥ 80.0 % passes at Δ = 0 and ≤ 5.0 % false passes at Δrecall = -0.03. A rate near the 5.0 % limit carries a Monte Carlo error of about ±0.7 points at 1000 simulations.

## The pilot it is drawn from

| workload | paired tasks | repositories | cells A | cells B | observed Δrecall (B − A) | wrong claims |
|---|---|---|---|---|---|---|
| W1 | 2 | 2 | 2 | 2 | -0.083 | simulated |
| W2 | 2 | 2 | 2 | 2 | -0.071 | simulated |
| W3 | 2 | 2 | 2 | 2 | +0.167 | simulated |

## Per task count

| tasks | pass at Δ = 0 (W1 · W2 · W3) | pass at Δrecall = -0.03 (W1 · W2 · W3) | the rule passes at 0 | worst false pass | meets both |
|---|---|---|---|---|---|
| 8 | 38.0 % · 34.7 % · 13.3 % | 4.0 % · 3.1 % · 3.9 % | 1.8 % | 4.0 % | no |
| 9 | 46.6 % · 44.3 % · 24.1 % | 10.2 % · 7.9 % · 8.5 % | 5.0 % | 10.2 % | no |
| 10 | 23.9 % · 44.8 % · 12.5 % | 6.0 % · 5.3 % · 5.3 % | 1.3 % | 6.0 % | no |
| 11 | 27.1 % · 29.2 % · 10.6 % | 7.5 % · 8.4 % · 6.4 % | 0.8 % | 8.4 % | no |
| 12 | 39.6 % · 40.2 % · 19.8 % | 7.2 % · 7.2 % · 6.1 % | 3.2 % | 7.2 % | no |
| 13 | 29.6 % · 47.4 % · 14.3 % | 4.3 % · 4.3 % · 4.6 % | 2.0 % | 4.6 % | no |
| 14 | 41.4 % · 39.3 % · 22.0 % | 10.2 % · 8.1 % · 8.8 % | 3.6 % | 10.2 % | no |
| 15 | 49.3 % · 48.2 % · 14.5 % | 7.2 % · 5.5 % · 5.6 % | 3.4 % | 7.2 % | no |
| 16 | 41.4 % · 61.8 % · 22.3 % | 11.3 % · 13.2 % · 10.5 % | 5.7 % | 13.2 % | no |
| 17 | 49.7 % · 67.0 % · 29.2 % | 7.6 % · 7.4 % · 6.8 % | 9.7 % | 7.6 % | no |
| 18 | 53.6 % · 53.2 % · 23.0 % | 12.0 % · 10.0 % · 11.6 % | 6.6 % | 12.0 % | no |
| 19 | 40.2 % · 55.6 % · 25.4 % | 7.4 % · 5.4 % · 8.0 % | 5.7 % | 8.0 % | no |
| 20 | 43.1 % · 61.3 % · 15.5 % | 9.1 % · 8.2 % · 7.8 % | 4.1 % | 9.1 % | no |
| 21 | 47.1 % · 51.5 % · 21.0 % | 5.0 % · 5.5 % · 4.9 % | 5.1 % | 5.5 % | no |
| 22 | 42.2 % · 58.5 % · 15.2 % | 7.1 % · 6.7 % · 7.7 % | 3.8 % | 7.7 % | no |
| 23 | 48.0 % · 70.5 % · 21.6 % | 3.7 % · 5.1 % · 4.3 % | 7.3 % | 5.1 % | no |
| 24 | 59.3 % · 74.4 % · 28.7 % | 8.0 % · 8.1 % · 8.5 % | 12.7 % | 8.5 % | no |
| 25 | 64.1 % · 65.0 % · 20.3 % | 5.8 % · 5.5 % · 5.8 % | 8.5 % | 5.8 % | no |
| 26 | 60.9 % · 72.5 % · 26.2 % | 8.3 % · 8.8 % · 7.8 % | 11.6 % | 8.8 % | no |
| 27 | 64.7 % · 76.9 % · 19.8 % | 6.7 % · 6.5 % · 4.5 % | 9.9 % | 6.7 % | no |
| 28 | 70.2 % · 69.2 % · 26.5 % | 9.3 % · 9.4 % · 9.1 % | 12.9 % | 9.4 % | no |
| 29 | 63.6 % · 77.9 % · 34.6 % | 7.1 % · 6.6 % · 6.8 % | 17.1 % | 7.1 % | no |
| 30 | 65.7 % · 79.5 % · 24.8 % | 8.2 % · 7.5 % · 7.9 % | 13.0 % | 8.2 % | no |

## The answer

no task count from 8 to 30 meets both targets — precision is bought with tasks, never with a wider margin (§3.8).
