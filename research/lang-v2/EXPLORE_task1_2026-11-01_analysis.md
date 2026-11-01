# Exploratory pilot, task 1: analysis (2026-11-01)

> **Exploratory, not evidence.** Six sessions of the easiest task on a harness that differs from v2's. This page says
> whether the harness behaved and whether the run matched what we expected before it. It says nothing about C# against
> TypeScript (`common.measurement` rule 3).

The numbers are in [EXPLORE_task1_2026-11-01.md](EXPLORE_task1_2026-11-01.md), generated from the records. This page is
the team lead's reading of them and of the raw files behind them. Plan: [PLAN_lang_explore_task1.md](../PLAN_lang_explore_task1.md).

## 1. Short answer

- **The harness behaved.** 6 of 6 sessions completed, 0 voids, T1–T5 pass in 6 of 6, the time partition sums to the wall
  in 6 of 6, the readback matched the planned prompt and the locale in 6 of 6, and no host was allowed out of the box.
- **The cost is right.** Recomputed by hand from the tokens and the price table of 2026-10-29: equal to the CLI total and
  to the broker's figure in 6 of 6, to the last digit. Each token class is billed once, at its own rate (no double count).
- **The run matched the prediction, except for time.** Sessions were faster than predicted (0.5–0.8 min, not 1–4 min).
  Cost sat at the low edge of the predicted range. Every other row held.
- **Spend:** $1.04 for six sessions; the most expensive session cost $0.23 against the $50 cap.
- **Seven things to carry into v2 E1** (§4; F7b is already fixed). None of them changes a number in the note.

## 2. What was checked by hand

| check | how | result |
|---|---|---|
| cost | tokens × the 2026-10-29 Opus 5.5 rates (input $4, output $20, cache read $0.20, 1 h cache write $8 per MTok) | 6 of 6 equal to the CLI total and the broker, exactly |
| no double count | input, cache read and cache write are separate fields in the result envelope; each is priced once | holds |
| cache write split | the envelope's `cache_creation` split | 100 % one-hour writes in 6 of 6; the record prices them at the 1 h rate |
| time partition | sum of every category against the wall | equal in 6 of 6 (e.g. pair 1 C#: 34484 + 6485 + 197 + 667 = 41833 ms) |
| verdicts | pair 1: every case output compared byte for byte with the expected file | 36 of 36 equal (6 visible, 30 held-out) |
| the programs | all six read in full | all six correct; details private (D41) |
| isolation | egress logs, broker logs, the box's config folder | model traffic only through the broker; 19–20 refused CLI side calls to `api.anthropic.com` per session, the same as v1; the box's memory folder was empty |
| quiet gate | the records and the pair logs | it held the launch of each pair until the machine was quiet: 12.5 min, 1 min and 4.8 min (other work: coai reviews, another session's test suite) |

## 3. Against the prediction

| measure | predicted | observed | verdict |
|---|---|---|---|
| cost per session | $0.15–0.60, median ≈ $0.25 | $0.13–0.23, median $0.17 | at the low edge; one session below the range |
| wall per session | 1–4 min | 0.5–0.8 min, median 0.6 min | **missed: faster** |
| `active − model_api` | C# > TS in most sessions | C# > TS in 2 of 3 pairs | held, weakly |
| visible pass rate | 100 % in every session | 6 of 6 | held |
| held-out | accepted in ≥ 5 of 6; trap hits 0–1 per stack | accepted 6 of 6; trap hits 0 for each stack | held |
| harness | 0 voids, T1–T5, partition sums, ≪ $50 | all four hold | held |

Why the time miss: the agents wrote one short program in a single pass, ran the samples, cross-checked against a small
reference of their own, and stopped (6–8 turns). The prediction leaned on v1's minigit, a larger spec.

## 4. Findings for v2

| # | finding | effect here | what v2 should do |
|---|---|---|---|
| F1 | The per-case wall has about 50 ms resolution: the judge times `Popen.wait(timeout=…)`, which polls in steps of up to 50 ms. Walls are 115 or 165 ms for both stacks; they also include `systemd-run` and `bwrap` start-up (the empty-program control is 65 ms). | none on pass/fail (limit 2 s) | the E1.S8a runner measures with a blocking wait and reports program time beside the control |
| F2 | Task 1 does not separate the stacks. Sessions take 27–47 s and cost $0.13–0.23; the C#/TS ratio per pair ranges from 0.67 to 1.16 for cost and 0.73 to 1.52 for wall, so the pair-to-pair spread is larger than any stack difference. The trap cases were not hit in any session. | none | keep task 1 as the floor task; set v2 §4.1's estimate for it to under 1 min; at spec 01's gate, ask whether its trap cases discriminate at all (the detail stays private, D41) |
| F3 | The prompt cache was warm after pair 1: the first turn read 0 cached tokens in pair 1 and 5,790 in pairs 2 and 3 (the CLI's shared prefix, one-hour cache). | both sessions of a pair had the same value, so ratios are fair; later sessions cost ≈ $0.045 less (5,790 × ($8 − $0.20) per MTok) | record `firstTurnCacheRead` (already done) and name the first pair of each series as cold in the analysis |
| F4 | v1's descriptive `build_test` is 0 in all six: v1's classifier knows `test-v*.sh` and `run-tests.sh`, not `build.sh`, `run.sh` or the runner. The pilot's own build/test field has the time (2.5–14.4 s). | none (the note uses the pilot's field) | v2's classifier names the v2 workspace scripts and the runner |
| F5 | Pair 2 was flagged `scored under load`: other work ran during scoring. | only case timings, which F1 already limits | the scorer re-runs a flagged session's scoring (it is free and repeatable), or v2 holds the machine during scoring |
| F6 | The record's package list is the shared box npm cache, so the C# records list npm packages. | none | v2 reads each program's own package use (its lock file or project assets) |
| F7 | The leak check's default range began before main's merges into this branch, so it scanned older public versions of the v2 plan and matched ten generic six-word phrases the private notes share with them (no case value, no trap). | a false failure; re-run over `origin/main..HEAD`: clean | default the range to the merge base with `origin/main` |
| F7b | The check treated only plans in `todo/` as public wording, so promoting this plan to `research/` turned its own text into hits. | a false failure, fixed here: plans in `research/` count as public too (a RED-GREEN-RED test); re-run clean over 3,262 files and the branch's commits | none left |

Also seen, not a harness issue: every agent wrote its own Python reference in the box and compared outputs. The one
session that compared 150 random inputs has twice the tool time and is the most expensive of the six. On a task this small,
`active − model_api` mostly measures how much the agent chose to test itself, not the build.

## 5. What this does not settle

Anything about C# against TypeScript. Six sessions of one easy task on a harness that differs from v2's (the note lists
the differences) license sentences about this run only.
