# Exploratory pilot: task 09 on the v1 bare harness, 6 session(s) (2026-11-05)

> **Exploratory, not evidence.** Three pairs of olympiad task 09 (Switched routes, an ungated draft spec) on a harness that differs from v2's. This note licenses sentences about THIS run only; it settles nothing about C# against TypeScript (`common.measurement` rule 3).

## What was run

- Date: 2026-11-05; series `x-20261105-4f51`.
- Subject: branch `feat/quick-run` at `4ad53935f9f0ef61bada833d90b462606badbc4d`; harness `spikes/lang-explore/explore.mjs` (imports `spikes/minigit-bench`), identity `eeb5e5220a75`, pilot tree `cbd991ff4a20de6904c7130aede0f855afd38798`.
- Agent: CLI 2.1.310 (sha256 `b73676430af1`), model `opus`, effort `high`; caps $50 per session, 75 min wall, 45 min stall.
- Stacks: .NET SDK 10.0.114, Node 26.12.1, TypeScript 6.0.3; locale `de_DE.UTF-8`; python3 3.14.4 (sha256 `52e0a13e60a9`).
- Task: spec 09 at `3f115d1` (sha256 `afa52d53e201`), FORMAT v2.1 at `3f115d1` (sha256 `4a4e86c48a70`).
- Limits: time limit 2004 ms, memory limit 536870912 bytes (calibrated on this host before pair 1).

## Prediction against observation (recorded before the run)

| measure | predicted | observed |
| --- | --- | --- |
| cost per session | $1.00 to $6.00 | $0.30 to $0.39, median $0.33 |
| wall per session | 10 to 45 min | 1.7 min to 2.9 min, median 2.1 min |
| accepted (of the sessions per stack) | 0 to 3 per stack | C# 3 of 3, TypeScript 3 of 3 |
| trap hits (per session; detail private) | 1 to 3 per stack | C# 0 to 0, TypeScript 0 to 0 |
| active - model_api | no prediction | C# > TS in 0 of 3 pair(s) |
| harness | 0 voids; T1-T5 hold; the partition sums to the wall; every session far under the $50 cap | 0 void(s); T1-T5 pass in 6 of 6; the partition sums to the wall in 6 of 6; the most expensive session cost $0.3945 |

## Cost: the views and their cross-checks

| pair | stack | class | CLI total (E) | broker (B) | recompute | cross-checks | cache write split | tokens |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | C# | completed | $0.2995 | $0.2995 | $0.2995 | T1-T5 pass | 100% 1h | in 14, out 7449, cache read 105893, cache write 16166 |
| 1 | TypeScript | completed | $0.3945 | $0.3945 | $0.3945 | T1-T5 pass | 100% 1h | in 16, out 9618, cache read 148930, cache write 21542 |
| 2 | C# | completed | $0.3233 | $0.3233 | $0.3233 | T1-T5 pass | 100% 1h | in 16, out 7784, cache read 132104, cache write 17646 |
| 2 | TypeScript | completed | $0.3407 | $0.3407 | $0.3407 | T1-T5 pass | 100% 1h | in 16, out 8500, cache read 134937, cache write 17957 |
| 3 | C# | completed | $0.3341 | $0.3341 | $0.3341 | T1-T5 pass | 100% 1h | in 14, out 8496, cache read 110335, cache write 17756 |
| 3 | TypeScript | completed | $0.3304 | $0.3304 | $0.3304 | T1-T5 pass | 100% 1h | in 14, out 7998, cache read 113676, cache write 18452 |

## Time: wall, model_api, active - model_api, and the build/test time of the agent's own calls

| pair | stack | wall | model_api | active | active - model_api | build/test (own calls) | sums to the wall |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | C# | 100.6 s | 64.9 s | 99.7 s | 34.8 s | 34.5 s in 5 call(s) | yes |
| 1 | TypeScript | 171.8 s | 90.1 s | 170.9 s | 80.8 s | 80.5 s in 7 call(s) | yes |
| 2 | C# | 114.8 s | 78.3 s | 114.0 s | 35.8 s | 35.3 s in 5 call(s) | yes |
| 2 | TypeScript | 124.7 s | 73.9 s | 123.9 s | 50.1 s | 49.7 s in 6 call(s) | yes |
| 3 | C# | 104.3 s | 72.0 s | 103.5 s | 31.6 s | 7.8 s in 3 call(s) | yes |
| 3 | TypeScript | 125.6 s | 77.0 s | 124.7 s | 47.7 s | 2.2 s in 3 call(s) | yes |

## The time partition, every category

| pair | stack | build_test | model_api | other_tool | hooks | coai_gate | consultant | ci_wait | coderabbit_wait | sleep300 | unattributed |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | C# | 0.0 s | 64.9 s | 34.5 s | 0.3 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.9 s |
| 1 | TypeScript | 0.0 s | 90.1 s | 80.4 s | 0.4 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.9 s |
| 2 | C# | 0.0 s | 78.3 s | 35.4 s | 0.3 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.8 s |
| 2 | TypeScript | 0.0 s | 73.9 s | 49.7 s | 0.3 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.8 s |
| 3 | C# | 0.0 s | 72.0 s | 31.3 s | 0.3 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.8 s |
| 3 | TypeScript | 0.3 s | 77.0 s | 47.2 s | 0.3 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.8 s |

## Quality: pass rates and trap hits (counts only)

| pair | stack | visible pass rate | held-out pass rate | accepted | trap hit (a count; detail private) |
| --- | --- | --- | --- | --- | --- |
| 1 | C# | 100% | 100% | yes | 0 of 15 trap cases |
| 1 | TypeScript | 100% | 100% | yes | 0 of 15 trap cases |
| 2 | C# | 100% | 100% | yes | 0 of 15 trap cases |
| 2 | TypeScript | 100% | 100% | yes | 0 of 15 trap cases |
| 3 | C# | 100% | 100% | yes | 0 of 15 trap cases |
| 3 | TypeScript | 100% | 100% | yes | 0 of 15 trap cases |

0 of 246 passing run(s) took longer than the spec's 1 s floor and 0 used more than its 256 MB floor

## Pairs

| pair | C# / TS cost | C# / TS wall | C# / TS model_api | C# / TS active - model_api | masks | start skew |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | 0.759209 | 0.585726 | 0.720153 | 0.430901 | 8-13 / 0-5 | 6 ms / 6 ms |
| 2 | 0.949006 | 0.92077 | 1.059728 | 0.714271 | 0-5 / 8-13 | 6 ms / 6 ms |
| 3 | 1.011272 | 0.831101 | 0.934624 | 0.66164 | 8-13 / 0-5 | 9 ms / 9 ms |

## Cost per correct solution

- C#, CPC (data attempts only, D24): 3 accepted of 3 session(s), spend $0.9570, cost per correct solution $0.3190
- C#, CPC_all (every session, voids included): 3 accepted of 3 session(s), spend $0.9570, cost per correct solution $0.3190
- TypeScript, CPC (data attempts only, D24): 3 accepted of 3 session(s), spend $1.0656, cost per correct solution $0.3552
- TypeScript, CPC_all (every session, voids included): 3 accepted of 3 session(s), spend $1.0656, cost per correct solution $0.3552

## Voids, contamination and timeouts

- pair 1 C# attempt 1: completed; flags: started not quiet, foreign load, scored under load
- pair 1 TypeScript attempt 1: completed; flags: started not quiet, foreign load, scored under load
- pair 2 C# attempt 1: completed; flags: started not quiet
- pair 2 TypeScript attempt 1: completed; flags: started not quiet
- pair 3 C# attempt 1: completed; flags: started not quiet
- pair 3 TypeScript attempt 1: completed; flags: started not quiet

## Differences from v2's harness

- No execution layer (v2 D43) and no shared network namespace (not needed for an olympiad task).
- `runner/run_samples.py` is the pilot's olympiad-only judge, not v2's E1.S8a runner (D37).
- SDK 10.0.114, not 10.0.403; Node 26.12.1, not 26.13.0.
- The time and memory limits are 2 s and 512 MB, uncalibrated beyond the pilot's own calibration (v2 D14, E2.S3 not built).
- Build and test time is reported beside the time partition, not inside it (v1's classifier knows `test-v*.sh`, not `build.sh` or `run.sh`, and is not widened).
- No pre-registration, no seal and no A/A.
- CLI 2.1.310 is not re-pinned (v2 D8).
- v1's contamination rules run as they are (the rules that name minigit are idle; the path rules are active).
- The spec of this task is an ungated draft (v2 freezes gated specs, E1.S2).
- The held-out suite is the pilot's own (v2's is E2.S2's, validated jointly with final references).

## What this does not settle

What it does not settle: anything about C# against TypeScript. Three pairs of one olympiad task, on a harness that differs from v2's and over an ungated draft spec, license sentences about this run only.
