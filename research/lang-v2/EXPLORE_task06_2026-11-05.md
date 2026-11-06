# Exploratory pilot: task 06 on the v1 bare harness, 6 session(s) (2026-11-06)

> **Exploratory, not evidence.** Bare C# and TypeScript sessions of web task 06 (Blog API, an ungated draft spec), the owner's quick run of 2026-11-05, on a harness that differs from v2's. This note licenses sentences about THIS run only; it settles nothing about C# against TypeScript (`common.measurement` rule 3).

## What was run

- Date: 2026-11-06; series `x-20261105-33fe`.
- Subject: branch `feat/q-fix` at `55fa09d18fa38b94039539498a5474fa15b19bd1`; harness `spikes/lang-explore/explore.mjs` (imports `spikes/minigit-bench`), identity `501901fe6f51`, pilot tree `68812f0758adfba25b6cf76e24a53b33174874b2`.
- Agent: CLI 2.1.310 (sha256 `b73676430af1`), model `opus`, effort `high`; caps $50 per session, 75 min wall, 45 min stall.
- Stacks: .NET SDK 10.0.403, Node 26.13.0, TypeScript 6.0.3; locale `de_DE.UTF-8`; python3 3.14.4 (sha256 `52e0a13e60a9`).
- Task: spec 06 at `4f44c80` (sha256 `bc0d067152c1`), FORMAT v2.1 at `4f44c80` (sha256 `4a4e86c48a70`).
- Limits: memory 536870912 bytes, build 600000 ms, each scenario's own deadlines (fixed; not calibrated).

## Prediction against observation (recorded before the run)

| measure | predicted | observed |
| --- | --- | --- |
| cost per session | no prediction recorded | $0.94 to $3.15, median $2.12 |
| wall per session | no prediction recorded | 3.9 min to 26.3 min, median 10.5 min |
| accepted (of the sessions per stack) | no prediction recorded | C# 2 of 3, TypeScript 3 of 3 |
| trap hits (per session; detail private) | no prediction recorded | C# 0 to 0, TypeScript 0 to 0 |
| active - model_api | no prediction recorded | C# > TS in 3 of 3 pair(s) |
| harness | 0 voids; T1-T5 hold; the partition sums to the wall; every session far under the $50 cap | 0 void(s); T1-T5 pass in 6 of 6; the partition sums to the wall in 6 of 6; the most expensive session cost $3.1500 |

## Cost: the views and their cross-checks

| pair | stack | class | CLI total (E) | broker (B) | recompute | cross-checks | cache write split | tokens |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | C# | completed | $2.7150 | $2.7150 | $2.7150 | T1-T5 pass | 100% 1h | in 82, out 64370, cache read 2856682, cache write 106991 |
| 1 | TypeScript | completed | $2.1169 | $2.1169 | $2.1169 | T1-T5 pass | 100% 1h | in 64, out 48525, cache read 2021943, cache write 92714 |
| 2 | C# | completed | $3.1500 | $3.1500 | $3.1500 | T1-T5 pass | 100% 1h | in 94, out 71819, cache read 3822882, cache write 118579 |
| 2 | TypeScript | completed | $1.1834 | $1.1834 | $1.1834 | T1-T5 pass | 100% 1h | in 46, out 25416, cache read 1025824, cache write 58722 |
| 3 | TypeScript | completed | $0.9411 | $0.9411 | $0.9411 | T1-T5 pass | 100% 1h | in 32, out 20141, cache read 627593, cache write 51575 |
| 3 | C# | completed | $1.7104 | $1.7104 | $1.7104 | T1-T5 pass | 100% 1h | in 50, out 41731, cache read 1335338, cache write 76059 |

## Time: wall, model_api, active - model_api, and the build/test time of the agent's own calls

| pair | stack | wall | model_api | active | active - model_api | build/test (own calls) | sums to the wall |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | C# | 1072.4 s | 648.4 s | 1068.1 s | 419.7 s | 256.3 s in 16 call(s) | yes |
| 1 | TypeScript | 630.5 s | 494.8 s | 628.7 s | 133.9 s | 52.8 s in 9 call(s) | yes |
| 2 | C# | 1576.6 s | 731.2 s | 1559.0 s | 827.8 s | 688.8 s in 14 call(s) | yes |
| 2 | TypeScript | 545.7 s | 228.8 s | 544.6 s | 315.8 s | 61.3 s in 8 call(s) | yes |
| 3 | TypeScript | 236.9 s | 174.1 s | 230.0 s | 55.9 s | 57.2 s in 7 call(s) | yes |
| 3 | C# | 556.6 s | 366.6 s | 526.3 s | 159.7 s | 80.9 s in 7 call(s) | yes |

## The time partition, every category

| pair | stack | build_test | model_api | other_tool | hooks | coai_gate | consultant | ci_wait | coderabbit_wait | sleep300 | unattributed |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | C# | 0.0 s | 648.4 s | 417.8 s | 1.9 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 4.3 s |
| 1 | TypeScript | 41.1 s | 494.8 s | 91.4 s | 1.4 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 1.8 s |
| 2 | C# | 0.0 s | 731.2 s | 825.3 s | 2.5 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 17.6 s |
| 2 | TypeScript | 54.7 s | 228.8 s | 260.2 s | 0.9 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 1.1 s |
| 3 | TypeScript | 3.4 s | 174.1 s | 51.8 s | 0.7 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 6.9 s |
| 3 | C# | 0.0 s | 366.6 s | 158.6 s | 1.1 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 30.3 s |

## Quality: pass rates and trap hits (counts only)

| pair | stack | visible pass rate | held-out pass rate | accepted | trap hit (a count; detail private) |
| --- | --- | --- | --- | --- | --- |
| 1 | C# | 100% | 100% | yes | 0 of 0 trap cases |
| 1 | TypeScript | 100% | 100% | yes | 0 of 0 trap cases |
| 2 | C# | 100% | 96.9% | no | 0 of 0 trap cases |
| 2 | TypeScript | 100% | 100% | yes | 0 of 0 trap cases |
| 3 | TypeScript | 100% | 100% | yes | 0 of 0 trap cases |
| 3 | C# | 100% | 100% | yes | 0 of 0 trap cases |

The time floors of the olympiad tasks do not apply to daemon and web cases: a scenario's own deadlines judge time.

## Pairs

| pair | C# / TS cost | C# / TS wall | C# / TS model_api | C# / TS active - model_api | masks | start skew |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | 1.282558 | 1.700777 | 1.310448 | 3.133524 | 8-13 / 0-5 | 11 ms / 11 ms |
| 2 | 2.66169 | 2.889305 | 3.195911 | 2.621446 | 0-5 / 8-13 | 9 ms / 9 ms |
| 3 | 1.817469 | 2.349237 | 2.105117 | 2.859127 | 0-5 / 8-13 | 11 ms / 11 ms |

## Cost per correct solution

- C#, CPC (data attempts only, D24): 2 accepted of 3 session(s), spend $7.5753, cost per correct solution $3.7877
- C#, CPC_all (every session, voids included): 2 accepted of 3 session(s), spend $7.5753, cost per correct solution $3.7877
- TypeScript, CPC (data attempts only, D24): 3 accepted of 3 session(s), spend $4.2414, cost per correct solution $1.4138
- TypeScript, CPC_all (every session, voids included): 3 accepted of 3 session(s), spend $4.2414, cost per correct solution $1.4138

## Voids, contamination and timeouts

- pair 1 C# attempt 1: completed; flags: started not quiet, harness core contended, foreign load, scored under load
- pair 1 TypeScript attempt 1: completed; flags: low outside-path: 19 relative walk(s) in an unknown directory (not contamination), started not quiet, harness core contended, foreign load, reclassified by rescore
- pair 2 C# attempt 1: completed; flags: low outside-path: 27 relative walk(s) in an unknown directory (not contamination), foreign load, harness core contended, scored under load
- pair 2 TypeScript attempt 1: completed; flags: low outside-path: 11 relative walk(s) in an unknown directory (not contamination), foreign load, harness core contended, scored under load
- pair 3 TypeScript attempt 1: completed; flags: low outside-path: 6 relative walk(s) in an unknown directory (not contamination), started not quiet, foreign load
- pair 3 C# attempt 1: completed; flags: low outside-path: 17 relative walk(s) in an unknown directory (not contamination), started not quiet, foreign load

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
