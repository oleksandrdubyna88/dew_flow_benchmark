# Exploratory pilot: task 08 on the v1 bare harness, 6 session(s) (2026-11-06)

> **Exploratory, not evidence.** Bare C# and TypeScript sessions of web task 08 (Ledger API, an ungated draft spec), the owner's quick run of 2026-11-05, on a harness that differs from v2's. This note licenses sentences about THIS run only; it settles nothing about C# against TypeScript (`common.measurement` rule 3).

## What was run

- Date: 2026-11-06; series `x-20261105-fca1`.
- Subject: branch `feat/q-fix` at `55fa09d18fa38b94039539498a5474fa15b19bd1`; harness `spikes/lang-explore/explore.mjs` (imports `spikes/minigit-bench`), identity `501901fe6f51`, pilot tree `68812f0758adfba25b6cf76e24a53b33174874b2`.
- Agent: CLI 2.1.310 (sha256 `b73676430af1`), model `opus`, effort `high`; caps $50 per session, 75 min wall, 45 min stall.
- Stacks: .NET SDK 10.0.403, Node 26.13.0, TypeScript 6.0.3; locale `de_DE.UTF-8`; python3 3.14.4 (sha256 `52e0a13e60a9`).
- Task: spec 08 at `4f44c80` (sha256 `19f916b5169b`), FORMAT v2.1 at `4f44c80` (sha256 `4a4e86c48a70`).
- Limits: memory 536870912 bytes, build 600000 ms, each scenario's own deadlines (fixed; not calibrated).

## Prediction against observation (recorded before the run)

| measure | predicted | observed |
| --- | --- | --- |
| cost per session | no prediction recorded | $0.69 to $1.01, median $0.86 |
| wall per session | no prediction recorded | 2.5 min to 4.4 min, median 3.9 min |
| accepted (of the sessions per stack) | no prediction recorded | C# 3 of 3, TypeScript 3 of 3 |
| trap hits (per session; detail private) | no prediction recorded | C# 0 to 0, TypeScript 0 to 0 |
| active - model_api | no prediction recorded | C# > TS in 0 of 3 pair(s) |
| harness | 0 voids; T1-T5 hold; the partition sums to the wall; every session far under the $50 cap | 0 void(s); T1-T5 pass in 6 of 6; the partition sums to the wall in 6 of 6; the most expensive session cost $1.0088 |

## Cost: the views and their cross-checks

| pair | stack | class | CLI total (E) | broker (B) | recompute | cross-checks | cache write split | tokens |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | C# | completed | $0.6906 | $0.6906 | $0.6906 | T1-T5 pass | 100% 1h | in 16, out 15916, cache read 219178, cache write 41049 |
| 1 | TypeScript | completed | $0.7536 | $0.7536 | $0.7536 | T1-T5 pass | 100% 1h | in 28, out 15361, cache read 471089, cache write 44001 |
| 2 | C# | completed | $0.7722 | $0.7722 | $0.7722 | T1-T5 pass | 100% 1h | in 26, out 17142, cache read 418444, cache write 43198 |
| 2 | TypeScript | completed | $0.8683 | $0.8683 | $0.8683 | T1-T5 pass | 100% 1h | in 30, out 18911, cache read 522046, cache write 48196 |
| 3 | C# | completed | $0.8649 | $0.8649 | $0.8649 | T1-T5 pass | 100% 1h | in 28, out 18070, cache read 513818, cache write 50074 |
| 3 | TypeScript | completed | $1.0088 | $1.0088 | $1.0088 | T1-T5 pass | 100% 1h | in 32, out 21405, cache read 641887, cache write 56520 |

## Time: wall, model_api, active - model_api, and the build/test time of the agent's own calls

| pair | stack | wall | model_api | active | active - model_api | build/test (own calls) | sums to the wall |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | C# | 152.6 s | 138.6 s | 148.3 s | 9.7 s | 9.1 s in 4 call(s) | yes |
| 1 | TypeScript | 236.0 s | 175.0 s | 230.7 s | 55.7 s | 35.6 s in 6 call(s) | yes |
| 2 | C# | 216.8 s | 173.7 s | 199.7 s | 26.1 s | 38.5 s in 6 call(s) | yes |
| 2 | TypeScript | 262.4 s | 179.4 s | 240.3 s | 60.9 s | 71.9 s in 8 call(s) | yes |
| 3 | C# | 211.4 s | 174.7 s | 201.6 s | 26.9 s | 25.5 s in 6 call(s) | yes |
| 3 | TypeScript | 267.0 s | 213.3 s | 256.4 s | 43.1 s | 46.4 s in 9 call(s) | yes |

## The time partition, every category

| pair | stack | build_test | model_api | other_tool | hooks | coai_gate | consultant | ci_wait | coderabbit_wait | sleep300 | unattributed |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | C# | 0.0 s | 138.6 s | 9.3 s | 0.4 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 4.2 s |
| 1 | TypeScript | 16.5 s | 175.0 s | 38.4 s | 0.9 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 5.3 s |
| 2 | C# | 0.0 s | 173.7 s | 24.3 s | 1.8 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 17.1 s |
| 2 | TypeScript | 57.7 s | 179.4 s | 1.6 s | 1.7 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 22.1 s |
| 3 | C# | 0.0 s | 174.7 s | 26.1 s | 0.8 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 9.8 s |
| 3 | TypeScript | 4.4 s | 213.3 s | 37.6 s | 1.1 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 10.5 s |

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
| 1 | 0.916468 | 0.646503 | 0.792119 | 0.1745 | 8-13 / 0-5 | 11 ms / 11 ms |
| 2 | 0.889326 | 0.826239 | 0.968106 | 0.42784 | 0-5 / 8-13 | 13 ms / 13 ms |
| 3 | 0.857353 | 0.791839 | 0.818743 | 0.623817 | 8-13 / 0-5 | 12 ms / 12 ms |

## Cost per correct solution

- C#, CPC (data attempts only, D24): 3 accepted of 3 session(s), spend $2.3277, cost per correct solution $0.7759
- C#, CPC_all (every session, voids included): 3 accepted of 3 session(s), spend $2.3277, cost per correct solution $0.7759
- TypeScript, CPC (data attempts only, D24): 3 accepted of 3 session(s), spend $2.6306, cost per correct solution $0.8769
- TypeScript, CPC_all (every session, voids included): 3 accepted of 3 session(s), spend $2.6306, cost per correct solution $0.8769

## Voids, contamination and timeouts

- pair 1 C# attempt 1: completed; flags: low outside-path: 1 relative walk(s) in an unknown directory (not contamination), started not quiet, foreign load, harness core contended, scored under load
- pair 1 TypeScript attempt 1: completed; flags: low outside-path: 7 relative walk(s) in an unknown directory (not contamination), started not quiet, foreign load, harness core contended, scored under load
- pair 2 C# attempt 1: completed; flags: low outside-path: 3 relative walk(s) in an unknown directory (not contamination), started not quiet, foreign load, harness core contended, scored under load
- pair 2 TypeScript attempt 1: completed; flags: low outside-path: 6 relative walk(s) in an unknown directory (not contamination), started not quiet, foreign load, harness core contended, scored under load
- pair 3 C# attempt 1: completed; flags: low outside-path: 3 relative walk(s) in an unknown directory (not contamination), started not quiet, foreign load, harness core contended
- pair 3 TypeScript attempt 1: completed; flags: low outside-path: 6 relative walk(s) in an unknown directory (not contamination), started not quiet, foreign load, harness core contended

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
