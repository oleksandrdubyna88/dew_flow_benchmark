# Exploratory pilot: task 07 on the v1 bare harness, 6 session(s) (2026-11-06)

> **Exploratory, not evidence.** Bare C# and TypeScript sessions of daemon task 07 (Stream processor with sliding windows, an ungated draft spec), the owner's quick run of 2026-11-05, on a harness that differs from v2's. This note licenses sentences about THIS run only; it settles nothing about C# against TypeScript (`common.measurement` rule 3).

## What was run

- Date: 2026-11-06; series `x-20261105-887a`.
- Subject: branch `feat/q-fix` at `55fa09d18fa38b94039539498a5474fa15b19bd1`; harness `spikes/lang-explore/explore.mjs` (imports `spikes/minigit-bench`), identity `501901fe6f51`, pilot tree `68812f0758adfba25b6cf76e24a53b33174874b2`.
- Agent: CLI 2.1.310 (sha256 `b73676430af1`), model `opus`, effort `high`; caps $50 per session, 75 min wall, 45 min stall.
- Stacks: .NET SDK 10.0.403, Node 26.13.0, TypeScript 6.0.3; locale `de_DE.UTF-8`; python3 3.14.4 (sha256 `52e0a13e60a9`).
- Task: spec 07 at `7c474a4` (sha256 `2087f5db2795`), FORMAT v2.1 at `7c474a4` (sha256 `4a4e86c48a70`).
- Limits: memory 536870912 bytes, build 600000 ms, each scenario's own deadlines (fixed; not calibrated).

## Prediction against observation (recorded before the run)

| measure | predicted | observed |
| --- | --- | --- |
| cost per session | no prediction recorded | $0.69 to $6.24, median $3.02 |
| wall per session | no prediction recorded | 3.4 min to 52.6 min, median 22.1 min |
| accepted (of the sessions per stack) | no prediction recorded | C# 3 of 3, TypeScript 3 of 3 |
| trap hits (per session; detail private) | no prediction recorded | C# 0 to 0, TypeScript 0 to 0 |
| active - model_api | no prediction recorded | C# > TS in 0 of 3 pair(s) |
| harness | 0 voids; T1-T5 hold; the partition sums to the wall; every session far under the $50 cap | 0 void(s); T1-T5 pass in 6 of 6; the partition sums to the wall in 6 of 6; the most expensive session cost $6.2357 |

## Cost: the views and their cross-checks

| pair | stack | class | CLI total (E) | broker (B) | recompute | cross-checks | cache write split | tokens |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | C# | completed | $0.6863 | $0.6863 | $0.6863 | T1-T5 pass | 100% 1h | in 26, out 15343, cache read 400515, cache write 37410 |
| 1 | TypeScript | completed | $3.0187 | $3.0187 | $3.0187 | T1-T5 pass | 100% 1h | in 92, out 72767, cache read 3295752, cache write 112983 |
| 2 | C# | completed | $0.8545 | $0.8545 | $0.8545 | T1-T5 pass | 100% 1h | in 34, out 19310, cache read 582597, cache write 43953 |
| 2 | TypeScript | completed | $5.5803 | $5.5803 | $5.5803 | T1-T5 pass | 100% 1h | in 152, out 119822, cache read 8562956, cache write 183832 |
| 3 | C# | completed | $1.3918 | $1.3918 | $1.3918 | T1-T5 pass | 100% 1h | in 62, out 32010, cache read 1392945, cache write 59093 |
| 3 | TypeScript | completed | $6.2357 | $6.2357 | $6.2357 | T1-T5 pass | 100% 1h | in 190, out 122952, cache read 11105442, cache write 194349 |

## Time: wall, model_api, active - model_api, and the build/test time of the agent's own calls

| pair | stack | wall | model_api | active | active - model_api | build/test (own calls) | sums to the wall |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | C# | 204.1 s | 139.6 s | 200.6 s | 61.0 s | 61.5 s in 8 call(s) | yes |
| 1 | TypeScript | 1326.2 s | 689.0 s | 1276.6 s | 587.6 s | 127.4 s in 13 call(s) | yes |
| 2 | C# | 308.9 s | 186.7 s | 307.5 s | 120.8 s | 74.1 s in 5 call(s) | yes |
| 2 | TypeScript | 3157.4 s | 1141.9 s | 3020.1 s | 1878.2 s | 1296.9 s in 28 call(s) | yes |
| 3 | C# | 586.7 s | 359.2 s | 585.5 s | 226.2 s | 197.1 s in 16 call(s) | yes |
| 3 | TypeScript | 2326.3 s | 1151.5 s | 2294.4 s | 1142.9 s | 286.9 s in 20 call(s) | yes |

## The time partition, every category

| pair | stack | build_test | model_api | other_tool | hooks | coai_gate | consultant | ci_wait | coderabbit_wait | sleep300 | unattributed |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | C# | 0.0 s | 139.6 s | 60.4 s | 0.6 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 3.5 s |
| 1 | TypeScript | 64.1 s | 689.0 s | 521.5 s | 2.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 49.5 s |
| 2 | C# | 0.0 s | 186.7 s | 119.9 s | 0.9 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 1.4 s |
| 2 | TypeScript | 308.5 s | 1141.9 s | 1564.2 s | 5.5 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 137.3 s |
| 3 | C# | 0.0 s | 359.2 s | 225.1 s | 1.1 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 1.2 s |
| 3 | TypeScript | 182.4 s | 1151.5 s | 956.7 s | 3.8 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 0.0 s | 31.9 s |

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
| 1 | 0.227363 | 0.153913 | 0.202595 | 0.103856 | 0-5 / 8-13 | 11 ms |
| 2 | 0.153124 | 0.097846 | 0.163541 | 0.064318 | 8-13 / 0-5 | 13 ms / 13 ms |
| 3 | 0.223196 | 0.252202 | 0.311971 | 0.19795 | 0-5 / 8-13 | 7 ms / 7 ms |

## Cost per correct solution

- C#, CPC (data attempts only, D24): 3 accepted of 3 session(s), spend $2.9326, cost per correct solution $0.9775
- C#, CPC_all (every session, voids included): 3 accepted of 3 session(s), spend $2.9326, cost per correct solution $0.9775
- TypeScript, CPC (data attempts only, D24): 3 accepted of 3 session(s), spend $14.8347, cost per correct solution $4.9449
- TypeScript, CPC_all (every session, voids included): 3 accepted of 3 session(s), spend $14.8347, cost per correct solution $4.9449

## Voids, contamination and timeouts

- pair 1 C# attempt 1: completed; flags: low outside-path: 5 relative walk(s) in an unknown directory (not contamination), 1 large file(s) outside the evidence paths not scanned (480000060 bytes), started not quiet, foreign load, scored under load
- pair 1 TypeScript attempt 1: completed; flags: low outside-path: 32 relative walk(s) in an unknown directory (not contamination), 10 large file(s) outside the evidence paths not scanned (9856921871 bytes), scored under load
- pair 2 C# attempt 1: completed; flags: low outside-path: 8 relative walk(s) in an unknown directory (not contamination), started not quiet, foreign load, harness core contended, scored under load
- pair 2 TypeScript attempt 1: completed; flags: low outside-path: 60 relative walk(s) in an unknown directory (not contamination), 14 large file(s) outside the evidence paths not scanned (13817489393 bytes), started not quiet, foreign load, harness core contended, scored under load
- pair 3 C# attempt 1: completed; flags: low outside-path: 17 relative walk(s) in an unknown directory (not contamination), 7 large file(s) outside the evidence paths not scanned (5750196256 bytes), started not quiet, foreign load
- pair 3 TypeScript attempt 1: completed; flags: low outside-path: 62 relative walk(s) in an unknown directory (not contamination), 4 large file(s) outside the evidence paths not scanned (1889112417 bytes), started not quiet, foreign load

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
