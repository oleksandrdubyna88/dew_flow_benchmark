# Exploratory pilot: task 09 on the v1 bare harness, 6 session(s) (2026-11-01)

> **Exploratory, not evidence.** Three pairs of olympiad task 09 (Switched routes, an ungated draft spec) on a harness that differs from v2's. This note licenses sentences about THIS run only; it settles nothing about C# against TypeScript (`common.measurement` rule 3).

## What was run

- Date: 2026-11-01; series `x-20261101-52e3`.
- Subject: branch `feat/lang-explore-olympiad` at `0f9357559f14f3b92735d2d7afc5806a6478bbe3`; harness `spikes/lang-explore/explore.mjs` (imports `spikes/minigit-bench`), identity `dfbee11f37ed`, pilot tree `2028be69c8712790f48e12a37d58aeea77b9c541`.
- Agent: CLI 2.1.310 (sha256 `b73676430af1`), model `opus`, effort `high`; caps $50 per session, 75 min wall, 45 min stall.
- Stacks: .NET SDK 10.0.114, Node 26.12.1, TypeScript 6.0.3; locale `de_DE.UTF-8`; python3 3.14.4 (sha256 `52e0a13e60a9`).
- Task: spec 09 at `3f115d1` (sha256 `afa52d53e201`), FORMAT v2.1 at `3f115d1` (sha256 `4a4e86c48a70`).
- Limits: time limit 2004 ms, memory limit 536870912 bytes (calibrated on this host before pair 1).

## Prediction against observation (recorded before the run)

| measure | predicted | observed |
| --- | --- | --- |
| cost per session | $1.00 to $6.00 | $0.30 to $0.58, median $0.37 |
| wall per session | 10 to 45 min | 1.8 min to 4.1 min, median 2.1 min |
| accepted (of the sessions per stack) | 0 to 3 per stack | C# 3 of 3, TypeScript 3 of 3 |
| trap hits (per session; detail private) | 1 to 3 per stack | C# 0 to 0, TypeScript 0 to 0 |
| active - model_api | no prediction | C# > TS in 1 of 3 pair(s) |
| harness | 0 voids; T1-T5 hold; the partition sums to the wall; every session far under the $50 cap | 0 void(s); T1-T5 pass in 6 of 6; the partition sums to the wall in 6 of 6; the most expensive session cost $0.5755 |

## Cost: the views and their cross-checks

| pair | stack | class | CLI total (E) | broker (B) | recompute | cross-checks | cache write split | tokens |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | C# | completed | $0.4028 | $0.4028 | $0.4028 | T1-T5 pass | 100% 1h | in 16, out 9203, cache read 128396, cache write 24125 |
| 1 | TypeScript | completed | $0.3668 | $0.3668 | $0.3668 | T1-T5 pass | 100% 1h | in 18, out 7684, cache read 146226, cache write 22976 |
| 2 | C# | completed | $0.5755 | $0.5755 | $0.5755 | T1-T5 pass | 100% 1h | in 28, out 15439, cache read 298200, cache write 25867 |
| 2 | TypeScript | completed | $0.3040 | $0.3040 | $0.3040 | T1-T5 pass | 100% 1h | in 14, out 7485, cache read 109119, cache write 16558 |
| 3 | C# | completed | $0.3273 | $0.3273 | $0.3273 | T1-T5 pass | 100% 1h | in 16, out 8102, cache read 130139, cache write 17390 |
| 3 | TypeScript | completed | $0.3258 | $0.3258 | $0.3258 | T1-T5 pass | 100% 1h | in 14, out 8254, cache read 110009, cache write 17331 |

## Time: wall, model_api, active - model_api, and the build/test time of the agent's own calls

| pair | stack | wall | model_api | active | active - model_api | build/test (own calls) | sums to the wall |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | C# | 125.3 s | 84.7 s | 124.5 s | 39.7 s | 39.1 s in 5 call(s) | yes |
| 1 | TypeScript | 130.3 s | 86.1 s | 129.4 s | 43.4 s | 43.0 s in 7 call(s) | yes |
| 2 | C# | 248.9 s | 152.0 s | 240.1 s | 88.1 s | 95.1 s in 11 call(s) | yes |
| 2 | TypeScript | 112.4 s | 64.4 s | 111.6 s | 47.2 s | 46.9 s in 5 call(s) | yes |
| 3 | C# | 105.9 s | 69.6 s | 105.1 s | 35.5 s | 35.1 s in 6 call(s) | yes |
| 3 | TypeScript | 123.5 s | 71.6 s | 122.7 s | 51.1 s | 6.3 s in 4 call(s) | yes |

## The time partition, every category

| pair | stack | build_test | model_api | other_tool | hooks | coai_gate | consultant | ci_wait | coderabbit_wait | sleep300 | unattributed |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | C# | 0.0 s | 84.7 s | 39.4 s | 0.4 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.9 s |
| 1 | TypeScript | 1.5 s | 86.1 s | 41.5 s | 0.3 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.9 s |
| 2 | C# | 0.0 s | 152.0 s | 87.4 s | 0.6 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 8.7 s |
| 2 | TypeScript | 0.2 s | 64.4 s | 46.7 s | 0.3 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.9 s |
| 3 | C# | 0.0 s | 69.6 s | 35.2 s | 0.3 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.9 s |
| 3 | TypeScript | 0.0 s | 71.6 s | 50.8 s | 0.3 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.8 s |

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
| 1 | 1.098139 | 0.961785 | 0.984812 | 0.915286 | 0-5 / 8-13 | 7 ms / 7 ms |
| 2 | 1.892713 | 2.213518 | 2.360392 | 1.867652 | 8-13 / 0-5 | 9 ms / 9 ms |
| 3 | 1.0045 | 0.857962 | 0.972075 | 0.694728 | 0-5 / 8-13 | 7 ms / 7 ms |

## Cost per correct solution

- C#, CPC (data attempts only, D24): 3 accepted of 3 session(s), spend $1.3055, cost per correct solution $0.4352
- C#, CPC_all (every session, voids included): 3 accepted of 3 session(s), spend $1.3055, cost per correct solution $0.4352
- TypeScript, CPC (data attempts only, D24): 3 accepted of 3 session(s), spend $0.9966, cost per correct solution $0.3322
- TypeScript, CPC_all (every session, voids included): 3 accepted of 3 session(s), spend $0.9966, cost per correct solution $0.3322

## Voids, contamination and timeouts

- pair 1 C# attempt 1: completed; flags: scored under load
- pair 1 TypeScript attempt 1: completed; flags: scored under load

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
