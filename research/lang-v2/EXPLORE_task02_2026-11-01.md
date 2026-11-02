# Exploratory pilot: task 02 on the v1 bare harness, 6 session(s) (2026-11-01)

> **Exploratory, not evidence.** Three pairs of olympiad task 02 (Open areas, an ungated draft spec) on a harness that differs from v2's. This note licenses sentences about THIS run only; it settles nothing about C# against TypeScript (`common.measurement` rule 3).

## What was run

- Date: 2026-11-01; series `x-20261101-01d1`.
- Subject: branch `feat/lang-explore-olympiad` at `ea66bbce44bfd9aa62b01f2d5942b7b1ea3f9202`; harness `spikes/lang-explore/explore.mjs` (imports `spikes/minigit-bench`), identity `dfbee11f37ed`, pilot tree `9b0dab37e5aba0d79fd92500b54f4651a3e0ec75`.
- Agent: CLI 2.1.310 (sha256 `b73676430af1`), model `opus`, effort `high`; caps $50 per session, 75 min wall, 45 min stall.
- Stacks: .NET SDK 10.0.114, Node 26.12.1, TypeScript 6.0.3; locale `de_DE.UTF-8`; python3 3.14.4 (sha256 `52e0a13e60a9`).
- Task: spec 02 at `3f115d1` (sha256 `8705219dc617`), FORMAT v2.1 at `3f115d1` (sha256 `4a4e86c48a70`).
- Limits: time limit 2000 ms, memory limit 536870912 bytes (calibrated on this host before pair 1).

## Prediction against observation (recorded before the run)

| measure | predicted | observed |
| --- | --- | --- |
| cost per session | $0.20 to $0.80 | $0.17 to $0.22, median $0.20 |
| wall per session | 1 to 5 min | 1.0 min to 1.3 min, median 1.1 min |
| accepted (of the sessions per stack) | 3 per stack | C# 3 of 3, TypeScript 3 of 3 |
| trap hits (per session; detail private) | 0 to 1 per stack | C# 0 to 0, TypeScript 0 to 0 |
| active - model_api | C# > TS in most pairs (the build) | C# > TS in 0 of 3 pair(s) |
| harness | 0 voids; T1-T5 hold; the partition sums to the wall; every session far under the $50 cap | 0 void(s); T1-T5 pass in 6 of 6; the partition sums to the wall in 6 of 6; the most expensive session cost $0.2227 |

## Cost: the views and their cross-checks

| pair | stack | class | CLI total (E) | broker (B) | recompute | cross-checks | cache write split | tokens |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | C# | completed | $0.2053 | $0.2053 | $0.2053 | T1-T5 pass | 100% 1h | in 12, out 3234, cache read 64715, cache write 15950 |
| 1 | TypeScript | completed | $0.2227 | $0.2227 | $0.2227 | T1-T5 pass | 100% 1h | in 12, out 3469, cache read 69579, cache write 17421 |
| 2 | C# | completed | $0.1694 | $0.1694 | $0.1694 | T1-T5 pass | 100% 1h | in 12, out 3567, cache read 70662, cache write 10482 |
| 2 | TypeScript | completed | $0.1750 | $0.1750 | $0.1750 | T1-T5 pass | 100% 1h | in 10, out 3492, cache read 58895, cache write 11662 |
| 3 | C# | completed | $0.1951 | $0.1951 | $0.1951 | T1-T5 pass | 100% 1h | in 14, out 3874, cache read 93683, cache write 12356 |
| 3 | TypeScript | completed | $0.1747 | $0.1747 | $0.1747 | T1-T5 pass | 100% 1h | in 12, out 3363, cache read 74638, cache write 11559 |

## Time: wall, model_api, active - model_api, and the build/test time of the agent's own calls

| pair | stack | wall | model_api | active | active - model_api | build/test (own calls) | sums to the wall |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | C# | 57.3 s | 38.6 s | 56.6 s | 18.1 s | 17.7 s in 3 call(s) | yes |
| 1 | TypeScript | 60.7 s | 37.6 s | 60.0 s | 22.5 s | 1.9 s in 3 call(s) | yes |
| 2 | C# | 66.7 s | 47.4 s | 66.1 s | 18.7 s | 18.3 s in 3 call(s) | yes |
| 2 | TypeScript | 59.8 s | 31.3 s | 59.2 s | 27.9 s | 27.7 s in 3 call(s) | yes |
| 3 | C# | 63.1 s | 34.7 s | 62.4 s | 27.7 s | 27.4 s in 4 call(s) | yes |
| 3 | TypeScript | 76.2 s | 46.6 s | 75.5 s | 29.0 s | 28.6 s in 3 call(s) | yes |

## The time partition, every category

| pair | stack | build_test | model_api | other_tool | hooks | coai_gate | consultant | ci_wait | coderabbit_wait | sleep300 | unattributed |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | C# | 0.0 s | 38.6 s | 17.9 s | 0.2 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.7 s |
| 1 | TypeScript | 0.0 s | 37.6 s | 22.3 s | 0.2 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.7 s |
| 2 | C# | 0.0 s | 47.4 s | 18.5 s | 0.2 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.7 s |
| 2 | TypeScript | 0.0 s | 31.3 s | 27.7 s | 0.1 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.6 s |
| 3 | C# | 0.0 s | 34.7 s | 27.5 s | 0.2 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.7 s |
| 3 | TypeScript | 0.0 s | 46.6 s | 28.8 s | 0.2 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.7 s |

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
| 1 | 0.921688 | 0.944214 | 1.026842 | 0.803709 | 8-13 / 0-5 | 10 ms / 10 ms |
| 2 | 0.968112 | 1.116296 | 1.515572 | 0.669704 | 0-5 / 8-13 | 3 ms / 3 ms |
| 3 | 1.116841 | 0.827827 | 0.744909 | 0.95628 | 8-13 / 0-5 | 6 ms / 6 ms |

## Cost per correct solution

- C#, CPC (data attempts only, D24): 3 accepted of 3 session(s), spend $0.5698, cost per correct solution $0.1899
- C#, CPC_all (every session, voids included): 3 accepted of 3 session(s), spend $0.5698, cost per correct solution $0.1899
- TypeScript, CPC (data attempts only, D24): 3 accepted of 3 session(s), spend $0.5724, cost per correct solution $0.1908
- TypeScript, CPC_all (every session, voids included): 3 accepted of 3 session(s), spend $0.5724, cost per correct solution $0.1908

## Voids, contamination and timeouts

No void, no kill, no timeout and no flagged session.

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
