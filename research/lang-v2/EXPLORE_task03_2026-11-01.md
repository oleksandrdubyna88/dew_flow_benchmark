# Exploratory pilot: task 03 on the v1 bare harness, 6 session(s) (2026-11-01)

> **Exploratory, not evidence.** Three pairs of olympiad task 03 (Wrapping arithmetic, an ungated draft spec) on a harness that differs from v2's. This note licenses sentences about THIS run only; it settles nothing about C# against TypeScript (`common.measurement` rule 3).

## What was run

- Date: 2026-11-01; series `x-20261101-43c4`.
- Subject: branch `feat/lang-explore-olympiad` at `d003ac82038bceff4fad84ebcd9fef302199076e`; harness `spikes/lang-explore/explore.mjs` (imports `spikes/minigit-bench`), identity `dfbee11f37ed`, pilot tree `0c68deccc1c3ae50b57db83a8e85f35ed2a59b72`.
- Agent: CLI 2.1.310 (sha256 `b73676430af1`), model `opus`, effort `high`; caps $50 per session, 75 min wall, 45 min stall.
- Stacks: .NET SDK 10.0.114, Node 26.12.1, TypeScript 6.0.3; locale `de_DE.UTF-8`; python3 3.14.4 (sha256 `52e0a13e60a9`).
- Task: spec 03 at `3f115d1` (sha256 `5d1aaab93439`), FORMAT v2.1 at `3f115d1` (sha256 `4a4e86c48a70`).
- Limits: time limit 2000 ms, memory limit 536870912 bytes (calibrated on this host before pair 1).

## Prediction against observation (recorded before the run)

| measure | predicted | observed |
| --- | --- | --- |
| cost per session | $0.50 to $3.00 | $0.30 to $0.42, median $0.35 |
| wall per session | 5 to 25 min | 1.6 min to 4.4 min, median 3.4 min |
| accepted (of the sessions per stack) | 1 to 3 per stack | C# 3 of 3, TypeScript 3 of 3 |
| trap hits (per session; detail private) | 1 to 3 per stack | C# 0 to 0, TypeScript 0 to 0 |
| active - model_api | no prediction | C# > TS in 0 of 3 pair(s) |
| harness | 0 voids; T1-T5 hold; the partition sums to the wall; every session far under the $50 cap | 0 void(s); T1-T5 pass in 6 of 6; the partition sums to the wall in 6 of 6; the most expensive session cost $0.4162 |

## Cost: the views and their cross-checks

| pair | stack | class | CLI total (E) | broker (B) | recompute | cross-checks | cache write split | tokens |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | C# | completed | $0.3405 | $0.3405 | $0.3405 | T1-T5 pass | 100% 1h | in 20, out 6594, cache read 158136, cache write 22109 |
| 1 | TypeScript | completed | $0.4034 | $0.4034 | $0.4034 | T1-T5 pass | 100% 1h | in 20, out 8365, cache read 165251, cache write 25375 |
| 2 | C# | completed | $0.3002 | $0.3002 | $0.3002 | T1-T5 pass | 100% 1h | in 18, out 6737, cache read 146760, cache write 17002 |
| 2 | TypeScript | completed | $0.3356 | $0.3356 | $0.3356 | T1-T5 pass | 100% 1h | in 18, out 7975, cache read 154184, cache write 18150 |
| 3 | C# | completed | $0.3468 | $0.3468 | $0.3468 | T1-T5 pass | 100% 1h | in 20, out 8323, cache read 172138, cache write 18228 |
| 3 | TypeScript | completed | $0.4162 | $0.4162 | $0.4162 | T1-T5 pass | 100% 1h | in 20, out 10929, cache read 169765, cache write 20454 |

## Time: wall, model_api, active - model_api, and the build/test time of the agent's own calls

| pair | stack | wall | model_api | active | active - model_api | build/test (own calls) | sums to the wall |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | C# | 98.1 s | 70.8 s | 97.3 s | 26.5 s | 7.1 s in 4 call(s) | yes |
| 1 | TypeScript | 263.7 s | 147.7 s | 262.8 s | 115.0 s | 114.5 s in 8 call(s) | yes |
| 2 | C# | 101.1 s | 73.2 s | 100.3 s | 27.1 s | 5.7 s in 4 call(s) | yes |
| 2 | TypeScript | 164.6 s | 73.6 s | 163.9 s | 90.3 s | 9.2 s in 6 call(s) | yes |
| 3 | C# | 202.5 s | 88.0 s | 201.7 s | 113.7 s | 113.2 s in 7 call(s) | yes |
| 3 | TypeScript | 254.9 s | 114.8 s | 254.2 s | 139.4 s | 98.4 s in 8 call(s) | yes |

## The time partition, every category

| pair | stack | build_test | model_api | other_tool | hooks | coai_gate | consultant | ci_wait | coderabbit_wait | sleep300 | unattributed |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | C# | 0.0 s | 70.8 s | 26.0 s | 0.4 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.9 s |
| 1 | TypeScript | 0.0 s | 147.7 s | 114.5 s | 0.6 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.9 s |
| 2 | C# | 0.0 s | 73.2 s | 26.8 s | 0.3 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.8 s |
| 2 | TypeScript | 0.2 s | 73.6 s | 89.8 s | 0.3 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.8 s |
| 3 | C# | 0.0 s | 88.0 s | 113.4 s | 0.4 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.8 s |
| 3 | TypeScript | 0.2 s | 114.8 s | 138.7 s | 0.4 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.8 s |

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
| 1 | 0.843911 | 0.372158 | 0.479216 | 0.229963 | 8-13 / 0-5 | 5 ms / 5 ms |
| 2 | 0.894434 | 0.614138 | 0.994347 | 0.300506 | 0-5 / 8-13 | 3 ms / 3 ms |
| 3 | 0.833144 | 0.794402 | 0.766674 | 0.816008 | 8-13 / 0-5 | 5 ms / 5 ms |

## Cost per correct solution

- C#, CPC (data attempts only, D24): 3 accepted of 3 session(s), spend $0.9874, cost per correct solution $0.3291
- C#, CPC_all (every session, voids included): 3 accepted of 3 session(s), spend $0.9874, cost per correct solution $0.3291
- TypeScript, CPC (data attempts only, D24): 3 accepted of 3 session(s), spend $1.1553, cost per correct solution $0.3851
- TypeScript, CPC_all (every session, voids included): 3 accepted of 3 session(s), spend $1.1553, cost per correct solution $0.3851

## Voids, contamination and timeouts

- pair 2 C# attempt 1: completed; flags: scored under load
- pair 2 TypeScript attempt 1: completed; flags: scored under load

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
