# Exploratory pilot: task 1 on the v1 bare harness, 6 session(s) (2026-11-01)

> **Exploratory, not evidence.** Three pairs of the easiest task on a harness that differs from v2's. This note licenses sentences about THIS run only; it settles nothing about C# against TypeScript (`common.measurement` rule 3).

## What was run

- Date: 2026-11-01; series `x-20261101-13cc`.
- Subject: branch `feat/lang-explore-task1` at `6191377b05664b836d3e5fa8b6698f581b8473b9`; harness `spikes/lang-explore/explore.mjs` (imports `spikes/minigit-bench`), identity `dfbee11f37ed`, pilot tree `8698c03ffe8fa13411921a8fde294a72c8cb2cc5`.
- Agent: CLI 2.1.310 (sha256 `b73676430af1`), model `opus`, effort `high`; caps $50 per session, 75 min wall, 45 min stall.
- Stacks: .NET SDK 10.0.114, Node 26.12.1, TypeScript 6.0.3; locale `de_DE.UTF-8`; python3 3.14.4 (sha256 `52e0a13e60a9`).
- Task: spec 01 at `d622768` (sha256 `d890bb3fab5f`), FORMAT v2 at `1531de8` (sha256 `5b476946bf9f`).
- Limits: time limit 2000 ms, memory limit 536870912 bytes (calibrated on this host before pair 1).

## Prediction against observation (recorded before the run)

| measure | predicted | observed |
| --- | --- | --- |
| cost per session | $0.15 to $0.60, median about $0.25 | $0.13 to $0.23, median $0.17 |
| wall per session | 1 to 4 min | 0.5 min to 0.8 min, median 0.6 min |
| active - model_api | C# > TS in most sessions | C# > TS in 2 of 3 pair(s) |
| visible pass rate | 100% in every session | 6 of 6 session(s) at 100% |
| held-out | accepted in at least 5 of 6 sessions; trap hits 0 to 1 per stack | accepted in 6 of 6 scored session(s); trap hits C# 0 session(s) with a hit, TypeScript 0 session(s) with a hit |
| harness | 0 voids; T1-T5 hold; the partition sums to the wall; at most $50 in every session by two orders of magnitude | 0 void(s); T1-T5 pass in 6 of 6; the partition sums to the wall in 6 of 6; the most expensive session cost $0.2289 |

## Cost: the views and their cross-checks

| pair | stack | class | CLI total (E) | broker (B) | recompute | cross-checks | cache write split | tokens |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | C# | completed | $0.2012 | $0.2012 | $0.2012 | T1-T5 pass | 100% 1h | in 12, out 2801, cache read 68045, cache write 16438 |
| 1 | TypeScript | completed | $0.1733 | $0.1733 | $0.1733 | T1-T5 pass | 100% 1h | in 10, out 2114, cache read 50378, cache write 15112 |
| 2 | C# | completed | $0.1514 | $0.1514 | $0.1514 | T1-T5 pass | 100% 1h | in 12, out 2577, cache read 71837, cache write 10678 |
| 2 | TypeScript | completed | $0.1315 | $0.1315 | $0.1315 | T1-T5 pass | 100% 1h | in 10, out 2062, cache read 57118, cache write 9846 |
| 3 | C# | completed | $0.1545 | $0.1545 | $0.1545 | T1-T5 pass | 100% 1h | in 12, out 2765, cache read 73040, cache write 10563 |
| 3 | TypeScript | completed | $0.2289 | $0.2289 | $0.2289 | T1-T5 pass | 100% 1h | in 12, out 2445, cache read 101414, cache write 19962 |

## Time: wall, model_api, active - model_api, and the build/test time of the agent's own calls

| pair | stack | wall | model_api | active | active - model_api | build/test (own calls) | sums to the wall |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | C# | 41.8 s | 34.5 s | 41.2 s | 6.7 s | 6.4 s in 3 call(s) | yes |
| 1 | TypeScript | 27.5 s | 21.0 s | 26.8 s | 5.8 s | 5.8 s in 4 call(s) | yes |
| 2 | C# | 32.4 s | 24.3 s | 31.7 s | 7.4 s | 4.7 s in 2 call(s) | yes |
| 2 | TypeScript | 30.3 s | 27.0 s | 29.7 s | 2.7 s | 2.5 s in 3 call(s) | yes |
| 3 | C# | 33.9 s | 26.6 s | 33.3 s | 6.7 s | 6.4 s in 3 call(s) | yes |
| 3 | TypeScript | 46.7 s | 31.4 s | 46.0 s | 14.6 s | 14.4 s in 4 call(s) | yes |

## The time partition, every category

| pair | stack | build_test | model_api | other_tool | hooks | coai_gate | consultant | ci_wait | coderabbit_wait | sleep300 | unattributed |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | C# | 0.0 s | 34.5 s | 6.5 s | 0.2 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.7 s |
| 1 | TypeScript | 0.0 s | 21.0 s | 5.7 s | 0.1 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.6 s |
| 2 | C# | 0.0 s | 24.3 s | 7.2 s | 0.2 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.7 s |
| 2 | TypeScript | 0.0 s | 27.0 s | 2.5 s | 0.2 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.6 s |
| 3 | C# | 0.0 s | 26.6 s | 6.5 s | 0.2 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.6 s |
| 3 | TypeScript | 0.0 s | 31.4 s | 14.4 s | 0.2 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.6 s |

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
| 1 | 1.160936 | 1.521421 | 1.641236 | 1.14457 | 8-13 / 0-5 | 6 ms / 6 ms |
| 2 | 1.151416 | 1.070107 | 0.899156 | 2.767128 | 0-5 / 8-13 | 5 ms / 5 ms |
| 3 | 0.674713 | 0.726376 | 0.846279 | 0.45718 | 8-13 / 0-5 | 5 ms / 5 ms |

## Cost per correct solution

- C#, CPC (data attempts only, D24): 3 accepted of 3 session(s), spend $0.5070, cost per correct solution $0.1690
- C#, CPC_all (every session, voids included): 3 accepted of 3 session(s), spend $0.5070, cost per correct solution $0.1690
- TypeScript, CPC (data attempts only, D24): 3 accepted of 3 session(s), spend $0.5337, cost per correct solution $0.1779
- TypeScript, CPC_all (every session, voids included): 3 accepted of 3 session(s), spend $0.5337, cost per correct solution $0.1779

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

## What this does not settle

What it does not settle: anything about C# against TypeScript. Three pairs of the easiest task, on a harness that differs from v2's, license sentences about this run only.
