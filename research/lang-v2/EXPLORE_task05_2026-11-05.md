# Exploratory pilot: task 05 on the v1 bare harness, 6 session(s) (2026-11-06)

> **Exploratory, not evidence.** Bare C# and TypeScript sessions of daemon task 05 (Key-value server with expiry, an ungated draft spec), the owner's quick run of 2026-11-05, on a harness that differs from v2's. This note licenses sentences about THIS run only; it settles nothing about C# against TypeScript (`common.measurement` rule 3).

## What was run

- Date: 2026-11-06; series `x-20261105-7b6f`.
- Subject: branch `feat/q-fix` at `55fa09d18fa38b94039539498a5474fa15b19bd1`; harness `spikes/lang-explore/explore.mjs` (imports `spikes/minigit-bench`), identity `501901fe6f51`, pilot tree `68812f0758adfba25b6cf76e24a53b33174874b2`.
- Agent: CLI 2.1.310 (sha256 `b73676430af1`), model `opus`, effort `high`; caps $50 per session, 75 min wall, 45 min stall.
- Stacks: .NET SDK 10.0.403, Node 26.13.0, TypeScript 6.0.3; locale `de_DE.UTF-8`; python3 3.14.4 (sha256 `52e0a13e60a9`).
- Task: spec 05 at `efc45b1` (sha256 `29f423088a30`), FORMAT v2.1 at `efc45b1` (sha256 `4a4e86c48a70`).
- Limits: memory 536870912 bytes, build 600000 ms, each scenario's own deadlines (fixed; not calibrated).

## Prediction against observation (recorded before the run)

| measure | predicted | observed |
| --- | --- | --- |
| cost per session | no prediction recorded | $1.37 to $4.33, median $1.73 |
| wall per session | no prediction recorded | 9.2 min to 55.7 min, median 15.6 min |
| accepted (of the sessions per stack) | no prediction recorded | C# 3 of 3, TypeScript 3 of 3 |
| trap hits (per session; detail private) | no prediction recorded | C# 0 to 0, TypeScript 0 to 0 |
| active - model_api | no prediction recorded | C# > TS in 0 of 3 pair(s) |
| harness | 0 voids; T1-T5 hold; the partition sums to the wall; every session far under the $50 cap | 0 void(s); T1-T5 pass in 6 of 6; the partition sums to the wall in 6 of 6; the most expensive session cost $4.3257 |

## Cost: the views and their cross-checks

| pair | stack | class | CLI total (E) | broker (B) | recompute | cross-checks | cache write split | tokens |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | C# | completed | $1.7292 | $1.7292 | $1.7292 | T1-T5 pass | 100% 1h | in 54, out 43733, cache read 1426521, cache write 71125 |
| 1 | TypeScript | completed | $1.3673 | $1.3673 | $1.3673 | T1-T5 pass | 100% 1h | in 48, out 33036, cache read 1119257, cache write 60315 |
| 2 | C# | completed | $1.4116 | $1.4116 | $1.4116 | T1-T5 pass | 100% 1h | in 42, out 37067, cache read 948436, cache write 60054 |
| 2 | TypeScript | completed | $4.3257 | $4.3257 | $4.3257 | T1-T5 pass | 100% 1h | in 158, out 87171, cache read 7038424, cache write 146750 |
| 3 | C# | completed | $1.5024 | $1.5024 | $1.5024 | T1-T5 pass | 100% 1h | in 50, out 37180, cache read 1197261, cache write 64895 |
| 3 | TypeScript | completed | $2.3526 | $2.3526 | $2.3526 | T1-T5 pass | 100% 1h | in 88, out 54224, cache read 2734218, cache write 90120 |

## Time: wall, model_api, active - model_api, and the build/test time of the agent's own calls

| pair | stack | wall | model_api | active | active - model_api | build/test (own calls) | sums to the wall |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | C# | 628.2 s | 414.5 s | 474.2 s | 59.7 s | 74.2 s in 10 call(s) | yes |
| 1 | TypeScript | 937.8 s | 376.1 s | 870.2 s | 494.1 s | 25.7 s in 3 call(s) | yes |
| 2 | C# | 550.4 s | 369.7 s | 535.9 s | 166.2 s | 49.8 s in 6 call(s) | yes |
| 2 | TypeScript | 3343.3 s | 860.1 s | 3026.2 s | 2166.1 s | 104.3 s in 13 call(s) | yes |
| 3 | C# | 927.1 s | 399.4 s | 925.8 s | 526.4 s | 361.8 s in 8 call(s) | yes |
| 3 | TypeScript | 1887.8 s | 729.0 s | 1701.3 s | 972.3 s | 56.8 s in 6 call(s) | yes |

## The time partition, every category

| pair | stack | build_test | model_api | other_tool | hooks | coai_gate | consultant | ci_wait | coderabbit_wait | sleep300 | unattributed |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | C# | 0.0 s | 414.5 s | 58.2 s | 1.5 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 154.0 s |
| 1 | TypeScript | 0.0 s | 376.1 s | 493.1 s | 1.1 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 67.6 s |
| 2 | C# | 0.0 s | 369.7 s | 164.5 s | 1.6 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 14.5 s |
| 2 | TypeScript | 60.8 s | 860.1 s | 2100.7 s | 4.6 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 317.1 s |
| 3 | C# | 0.0 s | 399.4 s | 525.4 s | 1.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 1.3 s |
| 3 | TypeScript | 47.5 s | 729.0 s | 922.6 s | 2.1 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 186.6 s |

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
| 1 | 1.264683 | 0.66981 | 1.102074 | 0.120877 | 8-13 / 0-5 | 9 ms / 9 ms |
| 2 | 0.326332 | 0.164632 | 0.429855 | 0.076711 | 0-5 / 8-13 | 20 ms / 20 ms |
| 3 | 0.638608 | 0.491089 | 0.547928 | 0.541412 | 8-13 / 0-5 | 9 ms / 9 ms |

## Cost per correct solution

- C#, CPC (data attempts only, D24): 3 accepted of 3 session(s), spend $4.6432, cost per correct solution $1.5477
- C#, CPC_all (every session, voids included): 3 accepted of 3 session(s), spend $4.6432, cost per correct solution $1.5477
- TypeScript, CPC (data attempts only, D24): 3 accepted of 3 session(s), spend $8.0457, cost per correct solution $2.6819
- TypeScript, CPC_all (every session, voids included): 3 accepted of 3 session(s), spend $8.0457, cost per correct solution $2.6819

## Voids, contamination and timeouts

- pair 1 C# attempt 1: completed; flags: low outside-path: 8 relative walk(s) in an unknown directory (not contamination), foreign load, harness core contended, scored under load
- pair 1 TypeScript attempt 1: completed; flags: low outside-path: 15 relative walk(s) in an unknown directory (not contamination), foreign load, harness core contended, scored under load
- pair 2 C# attempt 1: completed; flags: low outside-path: 12 relative walk(s) in an unknown directory (not contamination), started not quiet, foreign load, harness core contended
- pair 2 TypeScript attempt 1: completed; flags: low outside-path: 51 relative walk(s) in an unknown directory (not contamination), started not quiet, foreign load, harness core contended
- pair 3 C# attempt 1: completed; flags: low outside-path: 2 relative walk(s) in an unknown directory (not contamination), started not quiet, foreign load, harness core contended, scored under load
- pair 3 TypeScript attempt 1: completed; flags: low outside-path: 31 relative walk(s) in an unknown directory (not contamination), started not quiet, foreign load, harness core contended

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
