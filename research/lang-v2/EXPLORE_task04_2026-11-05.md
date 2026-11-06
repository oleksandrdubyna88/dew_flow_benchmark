# Exploratory pilot: task 04 on the v1 bare harness, 6 session(s) (2026-11-06)

> **Exploratory, not evidence.** Bare C# and TypeScript sessions of web task 04 (Rule API, an ungated draft spec), the owner's quick run of 2026-11-05, on a harness that differs from v2's. This note licenses sentences about THIS run only; it settles nothing about C# against TypeScript (`common.measurement` rule 3).

## What was run

- Date: 2026-11-06; series `x-20261105-9a03`.
- Subject: branch `feat/q-fix` at `55fa09d18fa38b94039539498a5474fa15b19bd1`; harness `spikes/lang-explore/explore.mjs` (imports `spikes/minigit-bench`), identity `501901fe6f51`, pilot tree `68812f0758adfba25b6cf76e24a53b33174874b2`.
- Agent: CLI 2.1.310 (sha256 `b73676430af1`), model `opus`, effort `high`; caps $50 per session, 75 min wall, 45 min stall.
- Stacks: .NET SDK 10.0.403, Node 26.13.0, TypeScript 6.0.3; locale `de_DE.UTF-8`; python3 3.14.4 (sha256 `52e0a13e60a9`).
- Task: spec 04 at `010ed60` (sha256 `4d6b98e76665`), FORMAT v2.1 at `010ed60` (sha256 `4a4e86c48a70`).
- Limits: memory 536870912 bytes, build 600000 ms, each scenario's own deadlines (fixed; not calibrated).

## Prediction against observation (recorded before the run)

| measure | predicted | observed |
| --- | --- | --- |
| cost per session | no prediction recorded | $0.82 to $1.25, median $0.96 |
| wall per session | no prediction recorded | 3.3 min to 5.8 min, median 3.9 min |
| accepted (of the sessions per stack) | no prediction recorded | C# 3 of 3, TypeScript 3 of 3 |
| trap hits (per session; detail private) | no prediction recorded | C# 0 to 0, TypeScript 0 to 0 |
| active - model_api | no prediction recorded | C# > TS in 1 of 3 pair(s) |
| harness | 0 voids; T1-T5 hold; the partition sums to the wall; every session far under the $50 cap | 0 void(s); T1-T5 pass in 6 of 6; the partition sums to the wall in 6 of 6; the most expensive session cost $1.2547 |

## Cost: the views and their cross-checks

| pair | stack | class | CLI total (E) | broker (B) | recompute | cross-checks | cache write split | tokens |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | C# | completed | $1.2547 | $1.2547 | $1.2547 | T1-T5 pass | 100% 1h | in 50, out 27454, cache read 1075139, cache write 61303 |
| 1 | TypeScript | completed | $0.9551 | $0.9551 | $0.9551 | T1-T5 pass | 100% 1h | in 32, out 19821, cache read 579008, cache write 55342 |
| 2 | C# | completed | $1.1480 | $1.1480 | $1.1480 | T1-T5 pass | 100% 1h | in 36, out 28386, cache read 744309, cache write 53904 |
| 2 | TypeScript | completed | $0.9305 | $0.9305 | $0.9305 | T1-T5 pass | 100% 1h | in 32, out 21141, cache read 566729, cache write 49276 |
| 3 | C# | completed | $0.8181 | $0.8181 | $0.8181 | T1-T5 pass | 100% 1h | in 22, out 18711, cache read 316370, cache write 47567 |
| 3 | TypeScript | completed | $0.8690 | $0.8690 | $0.8690 | T1-T5 pass | 100% 1h | in 26, out 18738, cache read 436614, cache write 50848 |

## Time: wall, model_api, active - model_api, and the build/test time of the agent's own calls

| pair | stack | wall | model_api | active | active - model_api | build/test (own calls) | sums to the wall |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | C# | 343.7 s | 248.5 s | 338.3 s | 89.8 s | 26.9 s in 9 call(s) | yes |
| 1 | TypeScript | 231.0 s | 191.7 s | 222.7 s | 31.0 s | 24.8 s in 6 call(s) | yes |
| 2 | C# | 347.9 s | 282.5 s | 296.5 s | 14.1 s | 40.0 s in 10 call(s) | yes |
| 2 | TypeScript | 223.2 s | 192.1 s | 217.0 s | 24.9 s | 17.6 s in 4 call(s) | yes |
| 3 | C# | 214.0 s | 187.3 s | 198.5 s | 11.2 s | 24.8 s in 6 call(s) | yes |
| 3 | TypeScript | 195.6 s | 161.8 s | 192.0 s | 30.2 s | 23.7 s in 5 call(s) | yes |

## The time partition, every category

| pair | stack | build_test | model_api | other_tool | hooks | coai_gate | consultant | ci_wait | coderabbit_wait | sleep300 | unattributed |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | C# | 0.0 s | 248.5 s | 88.6 s | 1.2 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 5.3 s |
| 1 | TypeScript | 22.1 s | 191.7 s | 8.1 s | 0.8 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 8.4 s |
| 2 | C# | 0.0 s | 282.5 s | 12.8 s | 1.2 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 51.4 s |
| 2 | TypeScript | 21.8 s | 192.1 s | 2.4 s | 0.8 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 6.2 s |
| 3 | C# | 0.0 s | 187.3 s | 10.6 s | 0.6 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 15.5 s |
| 3 | TypeScript | 4.4 s | 161.8 s | 25.0 s | 0.7 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 3.6 s |

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
| 1 | 1.313737 | 1.487416 | 1.296383 | 2.897942 | 8-13 / 0-5 | 10 ms / 10 ms |
| 2 | 1.233698 | 1.558924 | 1.470658 | 0.564345 | 0-5 / 8-13 | 10 ms / 10 ms |
| 3 | 0.941479 | 1.094202 | 1.157527 | 0.372907 | 8-13 / 0-5 | 11 ms / 11 ms |

## Cost per correct solution

- C#, CPC (data attempts only, D24): 3 accepted of 3 session(s), spend $3.2208, cost per correct solution $1.0736
- C#, CPC_all (every session, voids included): 3 accepted of 3 session(s), spend $3.2208, cost per correct solution $1.0736
- TypeScript, CPC (data attempts only, D24): 3 accepted of 3 session(s), spend $2.7546, cost per correct solution $0.9182
- TypeScript, CPC_all (every session, voids included): 3 accepted of 3 session(s), spend $2.7546, cost per correct solution $0.9182

## Voids, contamination and timeouts

- pair 1 C# attempt 1: completed; flags: low outside-path: 3 relative walk(s) in an unknown directory (not contamination), started not quiet, foreign load
- pair 1 TypeScript attempt 1: completed; flags: low outside-path: 2 relative walk(s) in an unknown directory (not contamination), started not quiet, foreign load
- pair 2 C# attempt 1: completed; flags: low outside-path: 9 relative walk(s) in an unknown directory (not contamination), started not quiet, foreign load, harness core contended, scored under load
- pair 2 TypeScript attempt 1: completed; flags: low outside-path: 4 relative walk(s) in an unknown directory (not contamination), started not quiet, foreign load, harness core contended, scored under load
- pair 3 C# attempt 1: completed; flags: low outside-path: 4 relative walk(s) in an unknown directory (not contamination), started not quiet, foreign load, scored under load
- pair 3 TypeScript attempt 1: completed; flags: low outside-path: 5 relative walk(s) in an unknown directory (not contamination), started not quiet, foreign load, scored under load

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
