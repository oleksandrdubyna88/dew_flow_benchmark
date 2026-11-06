# Exploratory pilot: task 1 on the v1 bare harness, 6 session(s) (2026-11-05)

> **Exploratory, not evidence.** Three pairs of the easiest task on a harness that differs from v2's. This note licenses sentences about THIS run only; it settles nothing about C# against TypeScript (`common.measurement` rule 3).

## What was run

- Date: 2026-11-05; series `x-20261105-0121`.
- Subject: branch `feat/quick-run` at `4ad53935f9f0ef61bada833d90b462606badbc4d`; harness `spikes/lang-explore/explore.mjs` (imports `spikes/minigit-bench`), identity `eeb5e5220a75`, pilot tree `cbd991ff4a20de6904c7130aede0f855afd38798`.
- Agent: CLI 2.1.310 (sha256 `b73676430af1`), model `opus`, effort `high`; caps $50 per session, 75 min wall, 45 min stall.
- Stacks: .NET SDK 10.0.114, Node 26.12.1, TypeScript 6.0.3; locale `de_DE.UTF-8`; python3 3.14.4 (sha256 `52e0a13e60a9`).
- Task: spec 01 at `d622768` (sha256 `d890bb3fab5f`), FORMAT v2 at `1531de8` (sha256 `5b476946bf9f`).
- Limits: time limit 2000 ms, memory limit 536870912 bytes (calibrated on this host before pair 1).

## Prediction against observation (recorded before the run)

| measure | predicted | observed |
| --- | --- | --- |
| cost per session | $0.15 to $0.60, median about $0.25 | $0.12 to $0.23, median $0.14 |
| wall per session | 1 to 4 min | 0.5 min to 0.8 min, median 0.7 min |
| active - model_api | C# > TS in most sessions | C# > TS in 2 of 3 pair(s) |
| visible pass rate | 100% in every session | 6 of 6 session(s) at 100% |
| held-out | accepted in at least 5 of 6 sessions; trap hits 0 to 1 per stack | accepted in 6 of 6 scored session(s); trap hits C# 0 session(s) with a hit, TypeScript 0 session(s) with a hit |
| harness | 0 voids; T1-T5 hold; the partition sums to the wall; at most $50 in every session by two orders of magnitude | 0 void(s); T1-T5 pass in 6 of 6; the partition sums to the wall in 6 of 6; the most expensive session cost $0.2344 |

## Cost: the views and their cross-checks

| pair | stack | class | CLI total (E) | broker (B) | recompute | cross-checks | cache write split | tokens |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | C# | completed | $0.1500 | $0.1500 | $0.1500 | T1-T5 pass | 100% 1h | in 10, out 2718, cache read 57245, cache write 10525 |
| 1 | TypeScript | completed | $0.1237 | $0.1237 | $0.1237 | T1-T5 pass | 100% 1h | in 10, out 1902, cache read 55676, cache write 9316 |
| 2 | C# | completed | $0.1433 | $0.1433 | $0.1433 | T1-T5 pass | 100% 1h | in 12, out 2435, cache read 71444, cache write 10032 |
| 2 | TypeScript | completed | $0.1391 | $0.1391 | $0.1391 | T1-T5 pass | 100% 1h | in 10, out 2353, cache read 57101, cache write 10072 |
| 3 | C# | completed | $0.1448 | $0.1448 | $0.1448 | T1-T5 pass | 100% 1h | in 12, out 2707, cache read 68986, cache write 9600 |
| 3 | TypeScript | completed | $0.2344 | $0.2344 | $0.2344 | T1-T5 pass | 100% 1h | in 12, out 2608, cache read 102301, cache write 20213 |

## Time: wall, model_api, active - model_api, and the build/test time of the agent's own calls

| pair | stack | wall | model_api | active | active - model_api | build/test (own calls) | sums to the wall |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | C# | 37.0 s | 26.2 s | 36.1 s | 9.8 s | 9.5 s in 2 call(s) | yes |
| 1 | TypeScript | 29.0 s | 19.9 s | 28.1 s | 8.1 s | 7.8 s in 3 call(s) | yes |
| 2 | C# | 44.0 s | 24.3 s | 43.1 s | 18.8 s | 18.4 s in 3 call(s) | yes |
| 2 | TypeScript | 42.8 s | 25.7 s | 42.0 s | 16.3 s | 2.7 s in 2 call(s) | yes |
| 3 | C# | 39.3 s | 29.2 s | 38.2 s | 9.0 s | 8.7 s in 3 call(s) | yes |
| 3 | TypeScript | 45.8 s | 26.0 s | 44.7 s | 18.8 s | 3.4 s in 3 call(s) | yes |

## The time partition, every category

| pair | stack | build_test | model_api | other_tool | hooks | coai_gate | consultant | ci_wait | coderabbit_wait | sleep300 | unattributed |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | C# | 0.0 s | 26.2 s | 9.7 s | 0.2 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.9 s |
| 1 | TypeScript | 0.0 s | 19.9 s | 7.9 s | 0.2 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.9 s |
| 2 | C# | 0.0 s | 24.3 s | 18.5 s | 0.3 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.9 s |
| 2 | TypeScript | 0.0 s | 25.7 s | 16.1 s | 0.2 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.8 s |
| 3 | C# | 0.0 s | 29.2 s | 8.8 s | 0.3 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 1.1 s |
| 3 | TypeScript | 0.3 s | 26.0 s | 18.2 s | 0.2 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 1.0 s |

## Quality: pass rates and trap hits (counts only)

| pair | stack | visible pass rate | held-out pass rate | accepted | trap hit (a count; detail private) |
| --- | --- | --- | --- | --- | --- |
| 1 | C# | 100% | 100% | yes | 0 of 5 trap cases |
| 1 | TypeScript | 100% | 100% | yes | 0 of 5 trap cases |
| 2 | C# | 100% | 100% | yes | 0 of 5 trap cases |
| 2 | TypeScript | 100% | 100% | yes | 0 of 5 trap cases |
| 3 | C# | 100% | 100% | yes | 0 of 5 trap cases |
| 3 | TypeScript | 100% | 100% | yes | 0 of 5 trap cases |

0 of 108 passing run(s) took longer than the spec's 1 s floor and 0 used more than its 256 MB floor

## Pairs

| pair | C# / TS cost | C# / TS wall | C# / TS model_api | C# / TS active - model_api | masks | start skew |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | 1.212586 | 1.276223 | 1.313816 | 1.211868 | 0-5 / 8-13 | 6 ms / 6 ms |
| 2 | 1.030173 | 1.027226 | 0.947785 | 1.151465 | 8-13 / 0-5 | 6 ms / 6 ms |
| 3 | 0.617757 | 0.859347 | 1.124605 | 0.481207 | 0-5 / 8-13 | 8 ms / 8 ms |

## Cost per correct solution

- C#, CPC (data attempts only, D24): 3 accepted of 3 session(s), spend $0.4381, cost per correct solution $0.1460
- C#, CPC_all (every session, voids included): 3 accepted of 3 session(s), spend $0.4381, cost per correct solution $0.1460
- TypeScript, CPC (data attempts only, D24): 3 accepted of 3 session(s), spend $0.4972, cost per correct solution $0.1657
- TypeScript, CPC_all (every session, voids included): 3 accepted of 3 session(s), spend $0.4972, cost per correct solution $0.1657

## Voids, contamination and timeouts

- pair 1 C# attempt 1: completed; flags: started not quiet, foreign load, scored under load
- pair 1 TypeScript attempt 1: completed; flags: started not quiet, foreign load, scored under load
- pair 2 C# attempt 1: completed; flags: started not quiet, scored under load
- pair 2 TypeScript attempt 1: completed; flags: started not quiet, scored under load
- pair 3 C# attempt 1: completed; flags: started not quiet, foreign load, scored under load
- pair 3 TypeScript attempt 1: completed; flags: started not quiet, foreign load, scored under load

## Differences from v2's harness

- No execution layer (v2 D43) and no shared network namespace (not needed for an olympiad task).
- `runner/run_samples.py` is the pilot's olympiad-only judge, not v2's E1.S8a runner (D37).
- SDK 10.0.114, not 10.0.403; Node 26.12.1, not 26.13.0.
- The time and memory limits are 2 s and 512 MB, uncalibrated beyond the pilot's own calibration (v2 D14, E2.S3 not built).
- Build and test time is reported beside the time partition, not inside it (v1's classifier knows `test-v*.sh`, not `build.sh` or `run.sh`, and is not widened).
- No pre-registration, no seal and no A/A.
- CLI 2.1.310 is not re-pinned (v2 D8).
- v1's contamination rules run as they are (the rules that name minigit are idle; the path rules are active).

## What this does not settle

What it does not settle: anything about C# against TypeScript. Three pairs of the easiest task, on a harness that differs from v2's, license sentences about this run only.
