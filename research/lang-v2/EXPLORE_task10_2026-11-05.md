# Exploratory pilot: task 10 on the v1 bare harness, 6 session(s) (2026-11-06)

> **Exploratory, not evidence.** Bare C# and TypeScript sessions of daemon task 10 (DAG job scheduler, an ungated draft spec), the owner's quick run of 2026-11-05, on a harness that differs from v2's. This note licenses sentences about THIS run only; it settles nothing about C# against TypeScript (`common.measurement` rule 3).

## What was run

- Date: 2026-11-06; series `x-20261105-9f9e`.
- Subject: branch `feat/q-fix` at `55fa09d18fa38b94039539498a5474fa15b19bd1`; harness `spikes/lang-explore/explore.mjs` (imports `spikes/minigit-bench`), identity `501901fe6f51`, pilot tree `68812f0758adfba25b6cf76e24a53b33174874b2`.
- Agent: CLI 2.1.310 (sha256 `b73676430af1`), model `opus`, effort `high`; caps $50 per session, 75 min wall, 45 min stall.
- Stacks: .NET SDK 10.0.403, Node 26.13.0, TypeScript 6.0.3; locale `de_DE.UTF-8`; python3 3.14.4 (sha256 `52e0a13e60a9`).
- Task: spec 10 at `7c474a4` (sha256 `3ab78c29b9ce`), FORMAT v2.1 at `7c474a4` (sha256 `4a4e86c48a70`).
- Limits: memory 536870912 bytes, build 600000 ms, each scenario's own deadlines (fixed; not calibrated).

## Prediction against observation (recorded before the run)

| measure | predicted | observed |
| --- | --- | --- |
| cost per session | no prediction recorded | $1.53 to $3.22, median $2.43 |
| wall per session | no prediction recorded | 7.9 min to 35.4 min, median 18.3 min |
| accepted (of the sessions per stack) | no prediction recorded | C# 3 of 3, TypeScript 3 of 3 |
| trap hits (per session; detail private) | no prediction recorded | C# 0 to 0, TypeScript 0 to 0 |
| active - model_api | no prediction recorded | C# > TS in 0 of 3 pair(s) |
| harness | 0 voids; T1-T5 hold; the partition sums to the wall; every session far under the $50 cap | 0 void(s); T1-T5 pass in 6 of 6; the partition sums to the wall in 6 of 6; the most expensive session cost $3.2192 |

## Cost: the views and their cross-checks

| pair | stack | class | CLI total (E) | broker (B) | recompute | cross-checks | cache write split | tokens |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | C# | completed | $1.9772 | $1.9772 | $1.9772 | T1-T5 pass | 100% 1h | in 44, out 50157, cache read 1328375, cache write 88520 |
| 1 | TypeScript | completed | $2.9054 | $2.9054 | $2.9054 | T1-T5 pass | 100% 1h | in 74, out 69077, cache read 3003997, cache write 115344 |
| 2 | C# | completed | $1.7439 | $1.7439 | $1.7439 | T1-T5 pass | 100% 1h | in 38, out 45154, cache read 1029063, cache write 79357 |
| 2 | TypeScript | completed | $3.2192 | $3.2192 | $3.2192 | T1-T5 pass | 100% 1h | in 92, out 75989, cache read 3616843, cache write 121956 |
| 3 | C# | completed | $1.5334 | $1.5334 | $1.5334 | T1-T5 pass | 100% 1h | in 44, out 37839, cache read 1136560, cache write 68637 |
| 3 | TypeScript | completed | $2.4264 | $2.4264 | $2.4264 | T1-T5 pass | 100% 1h | in 70, out 58784, cache read 2315787, cache write 98405 |

## Time: wall, model_api, active - model_api, and the build/test time of the agent's own calls

| pair | stack | wall | model_api | active | active - model_api | build/test (own calls) | sums to the wall |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | C# | 915.8 s | 443.0 s | 914.5 s | 471.5 s | 246.2 s in 5 call(s) | yes |
| 1 | TypeScript | 2124.6 s | 625.0 s | 2075.8 s | 1450.8 s | 255.5 s in 10 call(s) | yes |
| 2 | C# | 767.1 s | 390.6 s | 765.2 s | 374.6 s | 126.1 s in 5 call(s) | yes |
| 2 | TypeScript | 1097.6 s | 694.1 s | 1069.3 s | 375.2 s | 184.8 s in 12 call(s) | yes |
| 3 | C# | 471.9 s | 372.4 s | 436.9 s | 64.5 s | 47.5 s in 8 call(s) | yes |
| 3 | TypeScript | 1631.7 s | 608.3 s | 1611.6 s | 1003.3 s | 151.0 s in 7 call(s) | yes |

## The time partition, every category

| pair | stack | build_test | model_api | other_tool | hooks | coai_gate | consultant | ci_wait | coderabbit_wait | sleep300 | unattributed |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | C# | 0.0 s | 443.0 s | 470.6 s | 0.9 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 1.3 s |
| 1 | TypeScript | 160.3 s | 625.0 s | 1288.7 s | 1.8 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 48.8 s |
| 2 | C# | 0.0 s | 390.6 s | 373.7 s | 0.9 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 1.9 s |
| 2 | TypeScript | 120.4 s | 694.1 s | 252.8 s | 2.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 28.3 s |
| 3 | C# | 0.0 s | 372.4 s | 63.5 s | 1.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 35.0 s |
| 3 | TypeScript | 108.7 s | 608.3 s | 893.1 s | 1.4 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 20.1 s |

## Quality: pass rates and trap hits (counts only)

| pair | stack | visible pass rate | held-out pass rate | accepted | trap hit (a count; detail private) |
| --- | --- | --- | --- | --- | --- |
| 1 | C# | 100% | 100% | yes | 0 of 0 trap cases |
| 1 | TypeScript | 100% | 100% | yes | 0 of 0 trap cases |
| 2 | C# | 100% | 100% | yes | 0 of 0 trap cases |
| 2 | TypeScript | 100% | 100% | yes | 0 of 0 trap cases |
| 3 | C# | 100% | 100% | yes | 0 of 0 trap cases |
| 3 | TypeScript | 100% | 100% | yes | 0 of 0 trap cases |

The time floors of the olympiad tasks do not apply to daemon and web cases: a scenario's own deadlines judge time.

## Pairs

| pair | C# / TS cost | C# / TS wall | C# / TS model_api | C# / TS active - model_api | masks | start skew |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | 0.680512 | 0.431045 | 0.708899 | 0.324954 | 8-13 / 0-5 | 12 ms / 12 ms |
| 2 | 0.541725 | 0.698891 | 0.562796 | 0.998369 | 0-5 / 8-13 | 16 ms / 16 ms |
| 3 | 0.631961 | 0.289236 | 0.61214 | 0.064335 | 8-13 / 0-5 | 9 ms / 9 ms |

## Cost per correct solution

- C#, CPC (data attempts only, D24): 3 accepted of 3 session(s), spend $5.2544, cost per correct solution $1.7515
- C#, CPC_all (every session, voids included): 3 accepted of 3 session(s), spend $5.2544, cost per correct solution $1.7515
- TypeScript, CPC (data attempts only, D24): 3 accepted of 3 session(s), spend $8.5509, cost per correct solution $2.8503
- TypeScript, CPC_all (every session, voids included): 3 accepted of 3 session(s), spend $8.5509, cost per correct solution $2.8503

## Voids, contamination and timeouts

- pair 1 C# attempt 1: completed; flags: low outside-path: 2 relative walk(s) in an unknown directory (not contamination), started not quiet, foreign load, harness core contended
- pair 1 TypeScript attempt 1: completed; flags: low outside-path: 21 relative walk(s) in an unknown directory (not contamination), started not quiet, foreign load, harness core contended
- pair 2 C# attempt 1: completed; flags: low outside-path: 1 relative walk(s) in an unknown directory (not contamination), started not quiet, foreign load, harness core contended, scored under load
- pair 2 TypeScript attempt 1: completed; flags: low outside-path: 27 relative walk(s) in an unknown directory (not contamination), started not quiet, foreign load, harness core contended, scored under load
- pair 3 C# attempt 1: completed; flags: low outside-path: 4 relative walk(s) in an unknown directory (not contamination), started not quiet, scored under load
- pair 3 TypeScript attempt 1: completed; flags: low outside-path: 21 relative walk(s) in an unknown directory (not contamination), started not quiet, scored under load

## Differences from v2's harness

- The sessions are scored by v2's execution layer and runner (E1.S8a, E1.S8b), over the visible samples and the held-out set, on the toolchains and caches of v2 (SDK 10.0.403, Node 26.13.0, TypeScript 6.0.3).
- The limits are fixed (memory 512 MiB, build 600 s, each scenario's own deadlines), not registered from reference solutions (v2 D14).
- Build and test time is reported beside the time partition, not inside it (v1's classifier knows `test-v*.sh`, not `build.sh` or `run.sh`, and is not widened).
- No pre-registration, no seal and no A/A.
- CLI 2.1.310 is not re-pinned (v2 D8).
- v1's contamination rules run as they are (the rules that name minigit are idle; the path rules are active).
- The spec of this task is an ungated draft (v2 freezes gated specs, E1.S2).
- The held-out set was written quickly beside the draft and was not validated against two references or mutants; no oracle, mutant or calibration run was made (the owner's order of 2026-11-05).

## What this does not settle

What it does not settle: anything about C# against TypeScript. The few sessions of one task, on a harness that differs from v2's, over an ungated draft spec and a held-out set written for speed, license sentences about this run only.
