# Exploratory pilot: task 03 on the v1 bare harness, 6 session(s) (2026-11-05)

> **Exploratory, not evidence.** Three pairs of olympiad task 03 (Wrapping arithmetic, an ungated draft spec) on a harness that differs from v2's. This note licenses sentences about THIS run only; it settles nothing about C# against TypeScript (`common.measurement` rule 3).

## What was run

- Date: 2026-11-05; series `x-20261105-0435`.
- Subject: branch `feat/quick-run` at `4ad53935f9f0ef61bada833d90b462606badbc4d`; harness `spikes/lang-explore/explore.mjs` (imports `spikes/minigit-bench`), identity `eeb5e5220a75`, pilot tree `cbd991ff4a20de6904c7130aede0f855afd38798`.
- Agent: CLI 2.1.310 (sha256 `b73676430af1`), model `opus`, effort `high`; caps $50 per session, 75 min wall, 45 min stall.
- Stacks: .NET SDK 10.0.114, Node 26.12.1, TypeScript 6.0.3; locale `de_DE.UTF-8`; python3 3.14.4 (sha256 `52e0a13e60a9`).
- Task: spec 03 at `3f115d1` (sha256 `5d1aaab93439`), FORMAT v2.1 at `3f115d1` (sha256 `4a4e86c48a70`).
- Limits: time limit 2000 ms, memory limit 536870912 bytes (calibrated on this host before pair 1).

## Prediction against observation (recorded before the run)

| measure | predicted | observed |
| --- | --- | --- |
| cost per session | $0.50 to $3.00 | $0.25 to $0.40, median $0.33 |
| wall per session | 5 to 25 min | 1.3 min to 4.6 min, median 3.5 min |
| accepted (of the sessions per stack) | 1 to 3 per stack | C# 3 of 3, TypeScript 3 of 3 |
| trap hits (per session; detail private) | 1 to 3 per stack | C# 0 to 0, TypeScript 0 to 0 |
| active - model_api | no prediction | C# > TS in 0 of 3 pair(s) |
| harness | 0 voids; T1-T5 hold; the partition sums to the wall; every session far under the $50 cap | 0 void(s); T1-T5 pass in 6 of 6; the partition sums to the wall in 6 of 6; the most expensive session cost $0.4029 |

## Cost: the views and their cross-checks

| pair | stack | class | CLI total (E) | broker (B) | recompute | cross-checks | cache write split | tokens |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | C# | completed | $0.2937 | $0.2937 | $0.2937 | T1-T5 pass | 100% 1h | in 18, out 6981, cache read 141199, cache write 15723 |
| 1 | TypeScript | completed | $0.3317 | $0.3317 | $0.3317 | T1-T5 pass | 100% 1h | in 20, out 7641, cache read 165929, cache write 18196 |
| 2 | C# | completed | $0.3272 | $0.3272 | $0.3272 | T1-T5 pass | 100% 1h | in 20, out 7700, cache read 169863, cache write 17393 |
| 2 | TypeScript | completed | $0.4029 | $0.4029 | $0.4029 | T1-T5 pass | 100% 1h | in 24, out 9935, cache read 215378, cache write 20132 |
| 3 | C# | completed | $0.2465 | $0.2465 | $0.2465 | T1-T5 pass | 100% 1h | in 16, out 5223, cache read 121568, cache write 14708 |
| 3 | TypeScript | completed | $0.3497 | $0.3497 | $0.3497 | T1-T5 pass | 100% 1h | in 20, out 8454, cache read 169217, cache write 18332 |

## Time: wall, model_api, active - model_api, and the build/test time of the agent's own calls

| pair | stack | wall | model_api | active | active - model_api | build/test (own calls) | sums to the wall |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | C# | 107.5 s | 63.0 s | 106.0 s | 42.9 s | 9.6 s in 4 call(s) | yes |
| 1 | TypeScript | 273.8 s | 71.6 s | 272.5 s | 200.9 s | 95.3 s in 6 call(s) | yes |
| 2 | C# | 158.1 s | 93.6 s | 157.2 s | 63.7 s | 10.4 s in 4 call(s) | yes |
| 2 | TypeScript | 217.1 s | 93.4 s | 216.2 s | 122.8 s | 87.9 s in 8 call(s) | yes |
| 3 | C# | 75.5 s | 52.3 s | 74.5 s | 22.3 s | 21.7 s in 4 call(s) | yes |
| 3 | TypeScript | 209.1 s | 92.4 s | 208.2 s | 115.8 s | 115.4 s in 9 call(s) | yes |

## The time partition, every category

| pair | stack | build_test | model_api | other_tool | hooks | coai_gate | consultant | ci_wait | coderabbit_wait | sleep300 | unattributed |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | C# | 0.0 s | 63.0 s | 42.3 s | 0.6 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 1.5 s |
| 1 | TypeScript | 0.4 s | 71.6 s | 199.7 s | 0.8 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 1.3 s |
| 2 | C# | 0.0 s | 93.6 s | 63.2 s | 0.4 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.9 s |
| 2 | TypeScript | 0.2 s | 93.4 s | 122.0 s | 0.6 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.9 s |
| 3 | C# | 0.0 s | 52.3 s | 21.9 s | 0.3 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.9 s |
| 3 | TypeScript | 0.2 s | 92.4 s | 115.1 s | 0.4 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.9 s |

## Quality: pass rates and trap hits (counts only)

| pair | stack | visible pass rate | held-out pass rate | accepted | trap hit (a count; detail private) |
| --- | --- | --- | --- | --- | --- |
| 1 | C# | 100% | 100% | yes | 0 of 80 trap cases |
| 1 | TypeScript | 100% | 100% | yes | 0 of 80 trap cases |
| 2 | C# | 100% | 100% | yes | 0 of 80 trap cases |
| 2 | TypeScript | 100% | 100% | yes | 0 of 80 trap cases |
| 3 | C# | 100% | 100% | yes | 0 of 80 trap cases |
| 3 | TypeScript | 100% | 100% | yes | 0 of 80 trap cases |

0 of 522 passing run(s) took longer than the spec's 1 s floor and 0 used more than its 256 MB floor

## Pairs

| pair | C# / TS cost | C# / TS wall | C# / TS model_api | C# / TS active - model_api | masks | start skew |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | 0.88561 | 0.392561 | 0.88036 | 0.213611 | 8-13 / 0-5 | 5 ms / 5 ms |
| 2 | 0.812048 | 0.728312 | 1.001788 | 0.518515 | 0-5 / 8-13 | 8 ms / 8 ms |
| 3 | 0.704978 | 0.360953 | 0.566202 | 0.192134 | 8-13 / 0-5 | 9 ms / 9 ms |

## Cost per correct solution

- C#, CPC (data attempts only, D24): 3 accepted of 3 session(s), spend $0.8674, cost per correct solution $0.2891
- C#, CPC_all (every session, voids included): 3 accepted of 3 session(s), spend $0.8674, cost per correct solution $0.2891
- TypeScript, CPC (data attempts only, D24): 3 accepted of 3 session(s), spend $1.0842, cost per correct solution $0.3614
- TypeScript, CPC_all (every session, voids included): 3 accepted of 3 session(s), spend $1.0842, cost per correct solution $0.3614

## Voids, contamination and timeouts

- pair 1 C# attempt 1: completed; flags: started not quiet, foreign load, harness core contended, scored under load
- pair 1 TypeScript attempt 1: completed; flags: started not quiet, foreign load, harness core contended, scored under load
- pair 2 C# attempt 1: completed; flags: started not quiet, foreign load, harness core contended
- pair 2 TypeScript attempt 1: completed; flags: started not quiet, foreign load, harness core contended
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
- The spec of this task is an ungated draft (v2 freezes gated specs, E1.S2).
- The held-out suite is the pilot's own (v2's is E2.S2's, validated jointly with final references).

## What this does not settle

What it does not settle: anything about C# against TypeScript. Three pairs of one olympiad task, on a harness that differs from v2's and over an ungated draft spec, license sentences about this run only.
