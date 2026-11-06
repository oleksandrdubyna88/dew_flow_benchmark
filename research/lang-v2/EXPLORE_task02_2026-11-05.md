# Exploratory pilot: task 02 on the v1 bare harness, 6 session(s) (2026-11-05)

> **Exploratory, not evidence.** Three pairs of olympiad task 02 (Open areas, an ungated draft spec) on a harness that differs from v2's. This note licenses sentences about THIS run only; it settles nothing about C# against TypeScript (`common.measurement` rule 3).

## What was run

- Date: 2026-11-05; series `x-20261105-ab00`.
- Subject: branch `feat/quick-run` at `4ad53935f9f0ef61bada833d90b462606badbc4d`; harness `spikes/lang-explore/explore.mjs` (imports `spikes/minigit-bench`), identity `eeb5e5220a75`, pilot tree `cbd991ff4a20de6904c7130aede0f855afd38798`.
- Agent: CLI 2.1.310 (sha256 `b73676430af1`), model `opus`, effort `high`; caps $50 per session, 75 min wall, 45 min stall.
- Stacks: .NET SDK 10.0.114, Node 26.12.1, TypeScript 6.0.3; locale `de_DE.UTF-8`; python3 3.14.4 (sha256 `52e0a13e60a9`).
- Task: spec 02 at `3f115d1` (sha256 `8705219dc617`), FORMAT v2.1 at `3f115d1` (sha256 `4a4e86c48a70`).
- Limits: time limit 2000 ms, memory limit 536870912 bytes (calibrated on this host before pair 1).

## Prediction against observation (recorded before the run)

| measure | predicted | observed |
| --- | --- | --- |
| cost per session | $0.20 to $0.80 | $0.16 to $0.25, median $0.20 |
| wall per session | 1 to 5 min | 1.0 min to 1.5 min, median 1.3 min |
| accepted (of the sessions per stack) | 3 per stack | C# 3 of 3, TypeScript 3 of 3 |
| trap hits (per session; detail private) | 0 to 1 per stack | C# 0 to 0, TypeScript 0 to 0 |
| active - model_api | C# > TS in most pairs (the build) | C# > TS in 1 of 3 pair(s) |
| harness | 0 voids; T1-T5 hold; the partition sums to the wall; every session far under the $50 cap | 0 void(s); T1-T5 pass in 6 of 6; the partition sums to the wall in 6 of 6; the most expensive session cost $0.2464 |

## Cost: the views and their cross-checks

| pair | stack | class | CLI total (E) | broker (B) | recompute | cross-checks | cache write split | tokens |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | C# | completed | $0.2279 | $0.2279 | $0.2279 | T1-T5 pass | 100% 1h | in 12, out 3824, cache read 68796, cache write 17203 |
| 1 | TypeScript | completed | $0.2464 | $0.2464 | $0.2464 | T1-T5 pass | 100% 1h | in 12, out 4213, cache read 71898, cache write 18459 |
| 2 | C# | completed | $0.1966 | $0.1966 | $0.1966 | T1-T5 pass | 100% 1h | in 12, out 3798, cache read 78139, cache write 13115 |
| 2 | TypeScript | completed | $0.1642 | $0.1642 | $0.1642 | T1-T5 pass | 100% 1h | in 10, out 3249, cache read 57595, cache write 10954 |
| 3 | C# | completed | $0.1663 | $0.1663 | $0.1663 | T1-T5 pass | 100% 1h | in 10, out 3252, cache read 57909, cache write 11200 |
| 3 | TypeScript | completed | $0.1897 | $0.1897 | $0.1897 | T1-T5 pass | 100% 1h | in 14, out 3926, cache read 91202, cache write 11611 |

## Time: wall, model_api, active - model_api, and the build/test time of the agent's own calls

| pair | stack | wall | model_api | active | active - model_api | build/test (own calls) | sums to the wall |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | C# | 60.5 s | 34.4 s | 59.2 s | 24.8 s | 24.5 s in 4 call(s) | yes |
| 1 | TypeScript | 78.8 s | 43.7 s | 77.5 s | 33.8 s | 33.5 s in 4 call(s) | yes |
| 2 | C# | 63.7 s | 33.8 s | 62.8 s | 29.0 s | 28.8 s in 4 call(s) | yes |
| 2 | TypeScript | 75.2 s | 31.5 s | 74.3 s | 42.8 s | 42.5 s in 3 call(s) | yes |
| 3 | C# | 83.7 s | 29.7 s | 82.6 s | 52.9 s | 51.9 s in 3 call(s) | yes |
| 3 | TypeScript | 88.6 s | 36.3 s | 87.6 s | 51.2 s | 50.4 s in 5 call(s) | yes |

## The time partition, every category

| pair | stack | build_test | model_api | other_tool | hooks | coai_gate | consultant | ci_wait | coderabbit_wait | sleep300 | unattributed |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | C# | 0.0 s | 34.4 s | 24.5 s | 0.3 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 1.4 s |
| 1 | TypeScript | 30.8 s | 43.7 s | 2.7 s | 0.3 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 1.3 s |
| 2 | C# | 0.0 s | 33.8 s | 28.7 s | 0.3 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.9 s |
| 2 | TypeScript | 0.0 s | 31.5 s | 42.6 s | 0.3 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.9 s |
| 3 | C# | 0.0 s | 29.7 s | 52.4 s | 0.4 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 1.1 s |
| 3 | TypeScript | 0.2 s | 36.3 s | 50.2 s | 0.8 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 1.0 s |

## Quality: pass rates and trap hits (counts only)

| pair | stack | visible pass rate | held-out pass rate | accepted | trap hit (a count; detail private) |
| --- | --- | --- | --- | --- | --- |
| 1 | C# | 100% | 100% | yes | 0 of 19 trap cases |
| 1 | TypeScript | 100% | 100% | yes | 0 of 19 trap cases |
| 2 | C# | 100% | 100% | yes | 0 of 19 trap cases |
| 2 | TypeScript | 100% | 100% | yes | 0 of 19 trap cases |
| 3 | C# | 100% | 100% | yes | 0 of 19 trap cases |
| 3 | TypeScript | 100% | 100% | yes | 0 of 19 trap cases |

0 of 198 passing run(s) took longer than the spec's 1 s floor and 0 used more than its 256 MB floor

## Pairs

| pair | C# / TS cost | C# / TS wall | C# / TS model_api | C# / TS active - model_api | masks | start skew |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | 0.925114 | 0.767878 | 0.785807 | 0.734089 | 8-13 / 0-5 | 7 ms / 7 ms |
| 2 | 1.197264 | 0.848025 | 1.072479 | 0.678033 | 0-5 / 8-13 | 8 ms / 8 ms |
| 3 | 0.876429 | 0.944466 | 0.818632 | 1.031701 | 8-13 / 0-5 | 4 ms / 4 ms |

## Cost per correct solution

- C#, CPC (data attempts only, D24): 3 accepted of 3 session(s), spend $0.5907, cost per correct solution $0.1969
- C#, CPC_all (every session, voids included): 3 accepted of 3 session(s), spend $0.5907, cost per correct solution $0.1969
- TypeScript, CPC (data attempts only, D24): 3 accepted of 3 session(s), spend $0.6002, cost per correct solution $0.2001
- TypeScript, CPC_all (every session, voids included): 3 accepted of 3 session(s), spend $0.6002, cost per correct solution $0.2001

## Voids, contamination and timeouts

- pair 1 C# attempt 1: completed; flags: started not quiet, foreign load, scored under load
- pair 1 TypeScript attempt 1: completed; flags: started not quiet, foreign load, scored under load
- pair 2 C# attempt 1: completed; flags: started not quiet, foreign load, scored under load
- pair 2 TypeScript attempt 1: completed; flags: started not quiet, foreign load, scored under load
- pair 3 C# attempt 1: completed; flags: started not quiet, harness core contended, foreign load, scored under load
- pair 3 TypeScript attempt 1: completed; flags: started not quiet, harness core contended, foreign load, scored under load

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
