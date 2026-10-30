# minigit bare arm — C# / .NET 10 against TypeScript / Node 26, the first record (2026-10-30)

> Status: **interim record, written 2026-10-30 while series B (`s-20261030-b82e`) runs.** It covers the three
> series that ended before it: `s-20261030-92a9` and `s-20261030-0c1a` (instrument checks, abandoned) and
> `s-20261030-bf89` (series A: A/A GO, pairs 1–3, stopped by its registered rule). Series B is a placeholder
> ([§ 11](#11-series-b-s-20261030-b82e--running)). Pre-registration: tag `prereg/minigit-bare-20261030` =
> `41395f7`; registered texts in [spikes/minigit-bench/inputs/](../../spikes/minigit-bench/inputs/). Plans:
> [PLAN_minigit_stack_bench.md](../../todo/PLAN_minigit_stack_bench.md) (parent) and
> [PLAN_minigit_stack_bench_E3_bare.md](../../todo/PLAN_minigit_stack_bench_E3_bare.md) (the bare arm). Instruments:
> [module_minigit_bench.md](../module_minigit_bench.md).

**How the numbers got here.** No number in a generated block was typed. `tools/extract.mjs` reads the series records
under `~/rsd/minigit/` (read-only) and writes [data/](data/) (one JSON file per series); `tools/render.mjs` rewrites
every block between `<!-- generated:… -->` marks from those files. The prose outside the blocks names rids, rules and
commits, and registered constants (the $6 cap, the $40 and $500 limits, n = 3); it holds no measured number. How to
run it again: [§ 12](#12-how-to-re-run-the-extraction).

**Held-out results** appear as counts, `percent` and `defects` only. No held-out case is named here.

## 1. Summary

<!-- generated:headline -->
- Every paid data session of the series (voids included) ran 40.2–76.6 s wall, spawn to exit; recorded sessions that launched a subagent: 0.
- Series s-20261030-bf89: 2 complete pair(s) of 3 (pair(s) 1, 2); missing: pair 3. Stopped by `second void: contaminated`.
- Cost C#/TS (headline recompute, cell = v1 + v2): pair 1 0.92, pair 2 0.90; C# > TS in 0 of 2.
- Wall C#/TS: pair 1 1.11, pair 2 1.02; model_api: pair 1 1.11, pair 2 0.89; active − model_api: pair 1 1.12, pair 2 1.99.
- The only pair that is neither flagged nor holds a `ran unpaired` cell is pair 1: cost 0.92, wall 1.11, model_api 1.11, active − model_api 1.12.
- A cell (v1 + v2) cost $0.44–$0.46 in C# and $0.48–$0.51 in TS; a C# cell's wall was 102.8–106.2 s.
- Quality: every scored data session was accepted (built, passed the visible tests, held-out primary 100 %); the lowest held-out percent was 100 % (accepted: 10 of 10 scored).
- Spend: $5.1475 bare over all three series (of $40), $5.9738 with the prior instrument spend (of $500).
<!-- /generated:headline -->

**What this licenses.** Series A reached two complete pairs of the three it registered; only pair 1 is neither flagged
nor holds a `ran unpaired` cell. The pre-registration allows "in k of n pairs" and whether ranges overlap, nothing more
([§ 6.6](#66-the-pre-registered-reading)). In series A, on this tiny task, the C# cells did **not** cost more than the
TypeScript cells, and every scored session of both stacks was accepted. The prediction's cost and wall items are **not
held** in the pairs reached ([§ 6.7](#67-the-prediction-beside-the-observation)). This is not yet the bare arm's result:
series A stopped early, and series B repeats the three pairs on the fixed instrument.

**What stopped series A.** Two C# v2 sessions (`ebd0b0`, pair 2; `3407d8`, pair 3) were voided `contaminated` by a false
positive of the contamination scan, which modelled the Bash tool's working directory per call. The second void is a
registered series stop. Both voids are instrument errors, not agent behaviour ([§ 9](#9-instrument-defects-in-the-order-they-were-found)).

## 2. What is measured, and why

The claim under test comes from `banteg/minigit-bench` @ `83644e0` (Codex CLI, gpt-5.4): coding agents are slower and
more expensive in C# than in TypeScript on the small greenfield task "minigit". The registered statement
([statement.md](../../spikes/minigit-bench/inputs/statement.md) § 2) gives three faults of that measurement:

- **Its cost counts cached input twice.** OpenAI's `input_tokens` already contains `cached_input_tokens`; banteg's formula
  prices all input at the full rate and then the cached part again. Re-priced from its own tokens, the statement gives
  C#/TS 1.71× → 1.32×.
- **Its time mixes model work and tool work** in one process timer.
- **Its quality signal is weak** (two visible tests pass with a wrong parent pointer, a build failure is swallowed, the
  agent can edit the test script).

This bench counts the same thing in four cost views and checks them against each other on every session:

| View | Source | Role |
|---|---|---|
| headline recompute | tokens per model from the CLI envelope's `modelUsage`, the 5m / 1h cache-write split from the transcripts, published list prices (`prices-2026-10-29.json`) | **primary** |
| E | the CLI's `total_cost_usd` | beside it |
| O | the OTel `api_request` records (the harness's receiver) | beside it |
| T | the transcripts (the 5m / 1h split; T3/T4 coverage) | check |
| B | the host-side credential broker, which forwards and prices every request the box sends | guard; prices a session that never wrote an envelope |

Checks T1–T5: T1 the envelope against itself (stop), T2 O against E (flag `otel incomplete`), T3/T4 the transcripts'
coverage (stop for the bare data cells after the T3 commit `255484e`), T5 B against E (investigate). Every figure is a
list-price API equivalent, not the subscription's bill.

**Time** is split exclusively: wall (spawn to exit), `model_api` (time inside model calls), and `active − model_api`
(tool and hook time with no model call open) are primary. `build_test`, `other_tool`, `hooks` and `unattributed` are
**descriptive only**: one Bash call is classed as one category, its highest, so a chain such as `sleep 300 && dotnet
build` counts its whole time as `build_test` (V16). The records do not hold the count of mixed chains per cell.

**Quality** is per phase: build, the fixed visible test scripts (`test-v1.sh`; at v2 `test-v2.sh` and `test-v1.sh`), and
an encrypted held-out suite scored after the session, outside the agent's tree: `percent` (primary; a blocked case counts
as failed) and `defects` (distinct failed core cases or SPEC clauses). Its calibration is in
[module_minigit_bench.md](../module_minigit_bench.md).

## 3. Conditions

Registered in [statement.md](../../spikes/minigit-bench/inputs/statement.md) § 4 and pinned in each series' `run-map.json`:

<!-- generated:conditions -->
| series | harness at init | harness accepted for data | prereg tag commit | claude | node | .NET SDK | TS | cap / wall / stall | bare budget / ceiling | spin median (ms) | stopped |
|---|---|---|---|---|---|---|---|---|---|---|---|
| s-20261030-92a9 | e63941bdbdf9 | — | 41395f72984e | 2.1.310 | 26.12.1 | 10.0.114 | 7.0.2 | $6 / 60 min / 45 min | $40 / $500 | 22.56 | no (abandoned or open) |
| s-20261030-0c1a | de9091b54e2b | — | 41395f72984e | 2.1.310 | 26.12.1 | 10.0.114 | 7.0.2 | $6 / 60 min / 45 min | $40 / $500 | 21.84 | no (abandoned or open) |
| s-20261030-bf89 | 3129eeadfa8d | faaf55b4ecf7 | 41395f72984e | 2.1.310 | 26.12.1 | 10.0.114 | 7.0.2 | $6 / 60 min / 45 min | $40 / $500 | 21.69 | second void: contaminated (2026-10-30T20:43:29.386Z) |
<!-- /generated:conditions -->

- **Agent and model:** Claude Code CLI 2.1.310, run from a read-only copy whose sha256 is in the run-map; `--model opus`
  (resolved to `claude-opus-5-5` in every session, see the token table); effort `high`; no MCP server, no instruction
  files, no review gate, no GitHub.
- **Stacks:** C# 14 on .NET 10, **SDK 10.0.114**. The pin was 10.0.113 until an unattended `apt` upgrade replaced the SDK on
  the host on 2026-10-30; the pin was changed before the tag, and the operator then held the dotnet packages
  ([stacks.json](../../spikes/minigit-bench/inputs/stacks.json)). TypeScript **7.0.2** with `tsc --noEmit` as the type check,
  run directly by **Node 26.12.1** (native type stripping).
- **The box:** `bwrap` with its own pid, ipc and network namespaces; the operator's home hidden except the session's own
  directories. Egress only through a host CONNECT proxy to `registry.npmjs.org` and `api.nuget.org`. The API only through
  the host credential broker: the real token never enters the box, which holds a per-session dummy. Both stacks' offline
  cache snapshots are copied into every session, so the two boxes differ only in the prompt.
- **Limits per session:** cap $6 (broker budget and `--max-budget-usd`), wall 60 min, stall 45 min. Bare budget $40, series
  ceiling $500, across series.
- **CPU:** one WSL2 VM (Linux 6.18.54.1-microsoft-standard-WSL2, 14 vCPUs, 43 GB) on the operator's PC. The two cells of a
  pair run at `nice 19` on masks 0-5 and 8-13, swapped each pair (the seed picks pair 1); CPUs 6-7 are left to the harness.
  A phase does not start on a loaded machine ([quiet.json](../../spikes/minigit-bench/inputs/quiet.json)); load during a
  phase is a flag.
- **Seed:** commit-reveal. `seed.sha256` was committed in the tagged commit; `seed.txt` was revealed after the A/A of
  series A was GO (`47b2bc0`) and hashes to the committed value. It fixes the data rids, pair 1's masks and the launch
  order.

## 4. The series, in order

| Series | Harness | What ran | How it ended |
|---|---|---|---|
| `s-20261030-92a9` | `e63941bdbdf9` (the tagged one) | A/A `d3f571` ∥ `433bf6`, v2 rehearsal, Node pilot `9804fc` | pilot **voided falsely** (the TypeScript toolchain read as an outside path, defect 1). G-8 refuted is a stop; the runner cannot re-run a pilot, so a new series was started. Abandoned. |
| `s-20261030-0c1a` | `de9091b54e2b` | A/A `58d1fa` ∥ `a4fddc`, v2 rehearsal, Node pilot `c9b413` | pilot **voided falsely** (`cd / && rm -rf /tmp/…` read as a walk of `/`, defect 6). `aa-verdicts`: STOP on G-8. Abandoned. |
| `s-20261030-bf89` (series A) | `3129eeadfa8d` for the A/A; `faaf55b4ecf7` accepted before pair 1 | A/A `5f55d0` ∥ `586c41`, rehearsal, Node pilot `98ee3c` (completed); by-hand lines; `aa-verdicts` **GO**; T3 commit; seed reveal; pairs 1–3 | **stopped by `second void: contaminated`**: `ebd0b0` (pair 2 C# v2) and `3407d8` (pair 3 C# v2), both false positives (defect 14). After pair 1 started, no harness change could be accepted in this series, so it ran on to its registered stop. |
| `s-20261030-b82e` (series B) | `814d82e66e53` | running | — |

Pair 2's C# v2 was re-run once as `d9638c` (attempt 2, `ran unpaired`, as the contract's re-run schedule requires) and
carries the flag `foreign load`. The work on the fix for defect 14 ran on the same machine at that time (team-lead log);
the CPU samples (§ 5.7) show the load, not its source. Pair 3's first start halted on the quiet check (a resumable halt, nothing paid) for the same reason
and ran on the second start. Pair 3's TypeScript v2 `96fcf4` completed and was charged, but the series stopped before it was
recorded and scored: it has no `record.json` and no held-out score.

## 5. Per session

Every paid session of the three series. Rehearsal sessions (a stub CLI, $0) are in § 5.6. "kind" is `aa` (A/A twin),
`pilot` (the Node pilot) or `data p<k>` (pair k). A void is not a data point and is never scored.

### 5.1 Class, flags and cost

Cost columns: the headline recompute (primary), E, B and O, all in USD; "charged" is the stop-rule figure the ledger
holds. T1–T5: `p` pass, `B` breach, `u` unchecked.

<!-- generated:sessions-cost -->
| series | rid | kind | stack | phase | att. | label | class | flags | headline (primary) | E | B | O | T1–T5 | charged |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 92a9 | d3f571 | aa | C# | v1 | a1 | paired | completed | — | $0.2228 | $0.2228 | $0.2228 | $0.2228 | p p p p p | $0.2228 |
| 92a9 | 433bf6 | aa | C# | v1 | a1 | paired | completed | — | $0.2637 | $0.2637 | $0.2637 | $0.2637 | p p p p p | $0.2637 |
| 92a9 | 9804fc | pilot | TS | v1 | a1 | paired | void: contaminated | — | $0.2121 | $0.2121 | $0.2121 | $0.2121 | p p p p p | $0.2121 |
| 0c1a | 58d1fa | aa | C# | v1 | a1 | paired | completed | — | $0.2077 | $0.2077 | $0.2077 | $0.2077 | p p p p p | $0.2077 |
| 0c1a | a4fddc | aa | C# | v1 | a1 | paired | completed | — | $0.2226 | $0.2226 | $0.2226 | $0.2226 | p p p p p | $0.2226 |
| 0c1a | c9b413 | pilot | TS | v1 | a1 | paired | void: contaminated | — | $0.2284 | $0.2284 | $0.2284 | $0.2284 | p p p p p | $0.2284 |
| bf89 | 5f55d0 | aa | C# | v1 | a1 | paired | completed | — | $0.2141 | $0.2141 | $0.2141 | $0.2141 | p p p p p | $0.2141 |
| bf89 | 586c41 | aa | C# | v1 | a1 | paired | completed | — | $0.2194 | $0.2194 | $0.2194 | $0.2194 | p p p p p | $0.2194 |
| bf89 | 98ee3c | pilot | TS | v1 | a1 | paired | completed | — | $0.2319 | $0.2319 | $0.2319 | $0.2319 | p p p p p | $0.2319 |
| bf89 | 14d81c | data p1 | TS | v1 | a1 | paired | completed | — | $0.2101 | $0.2101 | $0.2101 | $0.2101 | p p p p p | $0.2101 |
| bf89 | 078dcd | data p1 | C# | v1 | a1 | paired | completed | — | $0.1847 | $0.1847 | $0.1847 | $0.1847 | p p p p p | $0.1847 |
| bf89 | 1d2624 | data p1 | TS | v2 | a1 | paired | completed | — | $0.2679 | $0.2679 | $0.2679 | $0.2679 | p p p p p | $0.2679 |
| bf89 | ede2a0 | data p1 | C# | v2 | a1 | paired | completed | — | $0.2574 | $0.2574 | $0.2574 | $0.2574 | p p p p p | $0.2574 |
| bf89 | f57599 | data p2 | C# | v1 | a1 | paired | completed | — | $0.1909 | $0.1909 | $0.1909 | $0.1909 | p p p p p | $0.1909 |
| bf89 | d79dc1 | data p2 | TS | v1 | a1 | paired | completed | — | $0.2118 | $0.2118 | $0.2118 | $0.2118 | p p p p p | $0.2118 |
| bf89 | ebd0b0 | data p2 | C# | v2 | a1 | paired | void: contaminated | — | $0.2790 | $0.2790 | $0.2790 | $0.2790 | p p p p p | $0.2790 |
| bf89 | 97168d | data p2 | TS | v2 | a1 | paired | completed | — | $0.3012 | $0.3012 | $0.3012 | $0.3012 | p p p p p | $0.3012 |
| bf89 | d9638c | data p2 | C# | v2 | a2 | ran unpaired | completed | foreign load | $0.2720 | $0.2720 | $0.2720 | $0.2720 | p p p p p | $0.2720 |
| bf89 | f9cc17 | data p3 | C# | v1 | a1 | paired | completed | — | $0.1941 | $0.1941 | $0.1941 | $0.1941 | p p p p p | $0.1941 |
| bf89 | 922a82 | data p3 | TS | v1 | a1 | paired | completed | — | $0.2155 | $0.2155 | $0.2155 | $0.2155 | p p p p p | $0.2155 |
| bf89 | 96fcf4 | data p3 | TS | v2 | a1 | paired | completed | — | — | $0.2607 | $0.2607 | $0.2607 | no record | $0.2607 |
| bf89 | 3407d8 | data p3 | C# | v2 | a1 | paired | void: contaminated | — | $0.2795 | $0.2795 | $0.2795 | $0.2795 | p p p p p | $0.2795 |
<!-- /generated:sessions-cost -->

### 5.2 Tokens per model

From the record (E's `modelUsage` with the transcript split). "B tokens equal" compares them with the sum of the broker's
settled requests, field by field, including the 1h cache-write split. `96fcf4` has no record, so its row is the envelope's.

<!-- generated:sessions-tokens -->
| series | rid | kind | stack | phase | att. | model | input | output | cache read | cache write 5m | cache write 1h | first-turn cache read | B tokens equal |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 92a9 | d3f571 | aa | C# | v1 | a1 | claude-opus-5-5 | 8 | 3850 | 38934 | 0 | 17251 | 0 | yes |
| 92a9 | 433bf6 | aa | C# | v1 | a1 | claude-opus-5-5 | 12 | 5003 | 71976 | 0 | 18645 | 0 | yes |
| 92a9 | 9804fc | pilot | TS | v1 | a1 | claude-opus-5-5 | 12 | 4789 | 77452 | 0 | 12601 | 5790 | yes |
| 0c1a | 58d1fa | aa | C# | v1 | a1 | claude-opus-5-5 | 6 | 3525 | 22335 | 0 | 16587 | 0 | yes |
| 0c1a | a4fddc | aa | C# | v1 | a1 | claude-opus-5-5 | 8 | 3862 | 39226 | 0 | 17180 | 0 | yes |
| 0c1a | c9b413 | pilot | TS | v1 | a1 | claude-opus-5-5 | 10 | 5547 | 61159 | 0 | 13153 | 5790 | yes |
| bf89 | 5f55d0 | aa | C# | v1 | a1 | claude-opus-5-5 | 10 | 3498 | 52951 | 0 | 16689 | 0 | yes |
| bf89 | 586c41 | aa | C# | v1 | a1 | claude-opus-5-5 | 8 | 3780 | 38768 | 0 | 16997 | 0 | yes |
| bf89 | 98ee3c | pilot | TS | v1 | a1 | claude-opus-5-5 | 14 | 4975 | 97023 | 0 | 14120 | 5790 | yes |
| bf89 | 14d81c | data p1 | TS | v1 | a1 | claude-opus-5-5 | 10 | 4895 | 60012 | 0 | 12521 | 5790 | yes |
| bf89 | 078dcd | data p1 | C# | v1 | a1 | claude-opus-5-5 | 8 | 4147 | 44864 | 0 | 11591 | 5790 | yes |
| bf89 | 1d2624 | data p1 | TS | v2 | a1 | claude-opus-5-5 | 14 | 4392 | 116884 | 0 | 19578 | 5790 | yes |
| bf89 | ede2a0 | data p1 | C# | v2 | a1 | claude-opus-5-5 | 8 | 4547 | 47549 | 0 | 19621 | 5790 | yes |
| bf89 | f57599 | data p2 | C# | v1 | a1 | claude-opus-5-5 | 12 | 3968 | 76096 | 0 | 12036 | 5790 | yes |
| bf89 | d79dc1 | data p2 | TS | v1 | a1 | claude-opus-5-5 | 12 | 4614 | 77751 | 0 | 12991 | 5790 | yes |
| bf89 | ebd0b0 | data p2 | C# | v2 | a1 | claude-opus-5-5 | 16 | 4436 | 143040 | 0 | 20197 | 5790 | yes |
| bf89 | 97168d | data p2 | TS | v2 | a1 | claude-opus-5-5 | 14 | 5510 | 121152 | 0 | 20834 | 5790 | yes |
| bf89 | d9638c | data p2 | C# | v2 | a2 | claude-opus-5-5 | 12 | 4699 | 97001 | 0 | 19822 | 5790 | yes |
| bf89 | f9cc17 | data p3 | C# | v1 | a1 | claude-opus-5-5 | 10 | 4322 | 61373 | 0 | 11923 | 5790 | yes |
| bf89 | 922a82 | data p3 | TS | v1 | a1 | claude-opus-5-5 | 12 | 4775 | 77931 | 0 | 13042 | 5790 | yes |
| bf89 | 96fcf4 | data p3 | TS | v2 | a1 | claude-opus-5-5 (envelope) | 14 | 4145 | 117170 | 0 | 19287 | — | — |
| bf89 | 3407d8 | data p3 | C# | v2 | a1 | claude-opus-5-5 | 14 | 4593 | 118302 | 0 | 20496 | 5790 | yes |
<!-- /generated:sessions-tokens -->

### 5.3 Time

Seconds. Wall, `model_api` and `active − model_api` are primary; the rest are descriptive. "wall − Σ partition" is the
part of the wall the partition does not cover. In every session run before harness `179624f5fec2` it is the harness's
3 s settle plus the CLI's own start before its first event (G-6 evidence, § 7.5; defect 11). The series A data sessions ran at `faaf55b4ecf7`, after the fix.

<!-- generated:sessions-time -->
| series | rid | kind | stack | phase | att. | harness | wall s | model_api s | active − model_api s | build_test s | other_tool s | hooks s | unattributed s | unattributed / wall | wall − Σ partition s | turns | subagents |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 92a9 | d3f571 | aa | C# | v1 | a1 | e63941bdbdf9 | 47.9 | 37.5 | 6.7 | 6.4 | 0.1 | 0.1 | 0.1 | 0.2 % | 3.6 | 4 | 0 |
| 92a9 | 433bf6 | aa | C# | v1 | a1 | e63941bdbdf9 | 53.3 | 43.2 | 6.4 | 5.4 | 0.8 | 0.2 | 0.1 | 0.3 % | 3.6 | 8 | 0 |
| 92a9 | 9804fc | pilot | TS | v1 | a1 | e63941bdbdf9 | 55.4 | 48.5 | 3.0 | 1.9 | 0.9 | 0.2 | 0.1 | 0.3 % | 3.7 | 7 | 0 |
| 0c1a | 58d1fa | aa | C# | v1 | a1 | de9091b54e2b | 46.3 | 37.0 | 5.6 | 5.4 | 0.1 | 0.1 | 0.1 | 0.2 % | 3.6 | 3 | 0 |
| 0c1a | a4fddc | aa | C# | v1 | a1 | de9091b54e2b | 43.9 | 32.5 | 7.7 | 7.5 | 0.1 | 0.1 | 0.1 | 0.2 % | 3.5 | 4 | 0 |
| 0c1a | c9b413 | pilot | TS | v1 | a1 | de9091b54e2b | 63.6 | 56.1 | 3.9 | 2.0 | 1.8 | 0.1 | 0.1 | 0.1 % | 3.5 | 6 | 0 |
| bf89 | 5f55d0 | aa | C# | v1 | a1 | 3129eeadfa8d | 39.3 | 29.7 | 5.9 | 5.5 | 0.2 | 0.2 | 0.1 | 0.3 % | 3.6 | 5 | 0 |
| bf89 | 586c41 | aa | C# | v1 | a1 | 3129eeadfa8d | 46.9 | 35.0 | 8.3 | 8.1 | 0.1 | 0.1 | 0.1 | 0.2 % | 3.5 | 4 | 0 |
| bf89 | 98ee3c | pilot | TS | v1 | a1 | 3129eeadfa8d | 49.7 | 43.2 | 2.7 | 1.7 | 0.8 | 0.2 | 0.2 | 0.4 % | 3.5 | 10 | 0 |
| bf89 | 14d81c | data p1 | TS | v1 | a1 | faaf55b4ecf7 | 46.7 | 42.0 | 4.0 | 3.6 | 0.2 | 0.2 | 0.7 | 1.5 % | 0.0 | 5 | 0 |
| bf89 | 078dcd | data p1 | C# | v1 | a1 | faaf55b4ecf7 | 49.3 | 39.6 | 9.1 | 8.8 | 0.1 | 0.1 | 0.7 | 1.4 % | 0.0 | 4 | 0 |
| bf89 | 1d2624 | data p1 | TS | v2 | a1 | faaf55b4ecf7 | 49.1 | 36.1 | 12.3 | 10.9 | 1.2 | 0.3 | 0.7 | 1.4 % | 0.0 | 7 | 0 |
| bf89 | ede2a0 | data p1 | C# | v2 | a1 | faaf55b4ecf7 | 56.8 | 47.0 | 9.1 | 8.8 | 0.2 | 0.1 | 0.6 | 1.1 % | 0.0 | 4 | 0 |
| bf89 | f57599 | data p2 | C# | v1 | a1 | faaf55b4ecf7 | 40.2 | 33.4 | 6.0 | 5.2 | 0.7 | 0.2 | 0.7 | 1.7 % | 0.0 | 8 | 0 |
| bf89 | d79dc1 | data p2 | TS | v1 | a1 | faaf55b4ecf7 | 44.2 | 40.6 | 2.9 | 1.9 | 0.8 | 0.2 | 0.7 | 1.6 % | 0.0 | 9 | 0 |
| bf89 | ebd0b0 | data p2 | C# | v2 | a1 | faaf55b4ecf7 | 76.6 | 63.4 | 12.5 | 11.6 | 0.7 | 0.3 | 0.7 | 0.9 % | 0.0 | 9 | 0 |
| bf89 | 97168d | data p2 | TS | v2 | a1 | faaf55b4ecf7 | 56.3 | 46.4 | 9.3 | 8.8 | 0.3 | 0.2 | 0.7 | 1.2 % | 0.0 | 10 | 0 |
| bf89 | d9638c | data p2 | C# | v2 | a2 | faaf55b4ecf7 | 62.6 | 43.7 | 18.2 | 17.3 | 0.6 | 0.3 | 0.7 | 1.2 % | 0.0 | 8 | 0 |
| bf89 | f9cc17 | data p3 | C# | v1 | a1 | faaf55b4ecf7 | 55.7 | 42.9 | 12.1 | 11.7 | 0.2 | 0.2 | 0.7 | 1.2 % | 0.0 | 6 | 0 |
| bf89 | 922a82 | data p3 | TS | v1 | a1 | faaf55b4ecf7 | 42.2 | 38.9 | 2.6 | 1.7 | 0.7 | 0.2 | 0.7 | 1.6 % | 0.0 | 10 | 0 |
| bf89 | 96fcf4 | data p3 | TS | v2 | a1 | — | 49.8 | — | — | — | — | — | — | — | — | 7 | — |
| bf89 | 3407d8 | data p3 | C# | v2 | a1 | faaf55b4ecf7 | 70.6 | 57.9 | 12.0 | 11.0 | 0.8 | 0.2 | 0.7 | 1.0 % | 0.0 | 7 | 0 |
<!-- /generated:sessions-time -->

### 5.4 Quality, size, egress and packages

LOC is the scorer's count (product / test / files). Latency is the scorer's per-invocation median of one registered
command in the scoring box, beside the box's own baseline (a reference measure, load-sensitive). Egress counts CONNECTs; refused ones are the
box's own attempts to reach a host outside the bare list (the API host is reached only through the broker). "real token
seen" is the number of times a sampler found the real token in a process of the box.

<!-- generated:sessions-quality -->
| series | rid | kind | stack | phase | att. | LOC product / test / files | build | visible | held-out percent | defects | blocked | latency median / box baseline ms | egress | packages | real token seen |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 92a9 | d3f571 | aa | C# | v1 | a1 | 151 / 0 / 1 | ok | test-v1 11/11 | 100.0 % of 29 | 0 | 0 | 58.43 / 12.72 | allowed: none; refused: 12; 0.0 MB down | 4 seen, 0 added, 0 flagged | 0 |
| 92a9 | 433bf6 | aa | C# | v1 | a1 | 251 / 0 / 1 | ok | test-v1 11/11 | 100.0 % of 29 | 0 | 0 | 132.95 / 12.32 | allowed: none; refused: 26; 0.0 MB down | 4 seen, 0 added, 0 flagged | 0 |
| 92a9 | 9804fc | pilot | TS | v1 | a1 | 166 / 0 / 2 | not scored | — | — | — | — | — | allowed: none; refused: 24; 0.0 MB down | 4 seen, 0 added, 0 flagged | 0 |
| 0c1a | 58d1fa | aa | C# | v1 | a1 | 152 / 0 / 1 | ok | test-v1 11/11 | 100.0 % of 29 | 0 | 0 | 46.9 / 10.01 | allowed: none; refused: 13; 0.0 MB down | 4 seen, 0 added, 0 flagged | 0 |
| 0c1a | a4fddc | aa | C# | v1 | a1 | 156 / 0 / 1 | ok | test-v1 11/11 | 100.0 % of 29 | 0 | 0 | 47.27 / 10.02 | allowed: none; refused: 13; 0.0 MB down | 4 seen, 0 added, 0 flagged | 0 |
| 0c1a | c9b413 | pilot | TS | v1 | a1 | 175 / 0 / 2 | not scored | — | — | — | — | — | allowed: none; refused: 24; 0.0 MB down | 4 seen, 0 added, 0 flagged | 0 |
| bf89 | 5f55d0 | aa | C# | v1 | a1 | 152 / 0 / 1 | ok | test-v1 11/11 | 100.0 % of 29 | 0 | 0 | 53.17 / 9.99 | allowed: none; refused: 12; 0.0 MB down | 4 seen, 0 added, 0 flagged | 0 |
| bf89 | 586c41 | aa | C# | v1 | a1 | 152 / 0 / 2 | ok | test-v1 11/11 | 100.0 % of 29 | 0 | 0 | 53.01 / 9.89 | allowed: none; refused: 12; 0.0 MB down | 4 seen, 0 added, 0 flagged | 0 |
| bf89 | 98ee3c | pilot | TS | v1 | a1 | 165 / 0 / 2 | ok | test-v1 11/11 | 100.0 % of 29 | 0 | 0 | 78.71 / 10 | allowed: none; refused: 20; 0.0 MB down | 4 seen, 0 added, 0 flagged | 0 |
| bf89 | 14d81c | data p1 | TS | v1 | a1 | 186 / 0 / 2 | ok | test-v1 11/11 | 100.0 % of 29 | 0 | 0 | 80.67 / 10.1 | allowed: none; refused: 20; 0.0 MB down | 4 seen, 0 added, 0 flagged | 0 |
| bf89 | 078dcd | data p1 | C# | v1 | a1 | 166 / 0 / 1 | ok | test-v1 11/11 | 100.0 % of 29 | 0 | 0 | 44.18 / 10.07 | allowed: api.nuget.org 2; refused: 20; 5.6 MB down | 6 seen, 2 added (microsoft.net.illink.tasks 10.0.14, microsoft.netcore.app.host.linux-x64 10.0.14), 0 flagged | 0 |
| bf89 | 1d2624 | data p1 | TS | v2 | a1 | 306 / 0 / 2 | ok | test-v2 30/30, test-v1 11/11 | 100.0 % of 58 | 0 | 0 | 80.94 / 10.16 | allowed: none; refused: 17; 0.0 MB down | 4 seen, 0 added, 0 flagged | 0 |
| bf89 | ede2a0 | data p1 | C# | v2 | a1 | 288 / 0 / 1 | ok | test-v2 30/30, test-v1 11/11 | 100.0 % of 58 | 0 | 0 | 48.45 / 10.11 | allowed: api.nuget.org 2; refused: 20; 5.6 MB down | 6 seen, 2 added (microsoft.net.illink.tasks 10.0.14, microsoft.netcore.app.host.linux-x64 10.0.14), 0 flagged | 0 |
| bf89 | f57599 | data p2 | C# | v1 | a1 | 158 / 0 / 1 | ok | test-v1 11/11 | 100.0 % of 29 | 0 | 0 | 46.01 / 10.11 | allowed: none; refused: 12; 0.0 MB down | 4 seen, 0 added, 0 flagged | 0 |
| bf89 | d79dc1 | data p2 | TS | v1 | a1 | 138 / 0 / 2 | ok | test-v1 11/11 | 100.0 % of 29 | 0 | 0 | 84.06 / 9.81 | allowed: none; refused: 20; 0.0 MB down | 4 seen, 0 added, 0 flagged | 0 |
| bf89 | ebd0b0 | data p2 | C# | v2 | a1 | 314 / 0 / 1 | not scored | — | — | — | — | — | allowed: none; refused: 34; 0.0 MB down | 4 seen, 0 added, 0 flagged | 0 |
| bf89 | 97168d | data p2 | TS | v2 | a1 | 238 / 0 / 2 | ok | test-v2 30/30, test-v1 11/11 | 100.0 % of 58 | 0 | 0 | 87.19 / 9.82 | allowed: none; refused: 21; 0.0 MB down | 4 seen, 0 added, 0 flagged | 0 |
| bf89 | d9638c | data p2 | C# | v2 | a2 | 296 / 0 / 1 | ok | test-v2 30/30, test-v1 11/11 | 100.0 % of 58 | 0 | 0 | 52.7 / 10.6 | allowed: none; refused: 29; 0.0 MB down | 4 seen, 0 added, 0 flagged | 0 |
| bf89 | f9cc17 | data p3 | C# | v1 | a1 | 166 / 0 / 1 | ok | test-v1 11/11 | 100.0 % of 29 | 0 | 0 | 35.9 / 9.55 | allowed: api.nuget.org 3; refused: 20; 58.8 MB down | 8 seen, 4 added (microsoft.aspnetcore.app.runtime.linux-x64 10.0.14, microsoft.net.illink.tasks 10.0.14, microsoft.netcore.app.host.linux-x64 10.0.14, microsoft.netcore.app.runtime.linux-x64 10.0.14), 0 flagged | 0 |
| bf89 | 922a82 | data p3 | TS | v1 | a1 | 154 / 0 / 2 | ok | test-v1 11/11 | 100.0 % of 29 | 0 | 0 | 101.35 / 10.31 | allowed: none; refused: 13; 0.0 MB down | 4 seen, 0 added, 0 flagged | 0 |
| bf89 | 96fcf4 | data p3 | TS | v2 | a1 | — | not scored | — | — | — | — | — | — | — | 0 |
| bf89 | 3407d8 | data p3 | C# | v2 | a1 | 299 / 0 / 1 | not scored | — | — | — | — | — | allowed: api.nuget.org 3; refused: 28; 58.8 MB down | 8 seen, 4 added (microsoft.aspnetcore.app.runtime.linux-x64 10.0.14, microsoft.net.illink.tasks 10.0.14, microsoft.netcore.app.host.linux-x64 10.0.14, microsoft.netcore.app.runtime.linux-x64 10.0.14), 0 flagged | 0 |
<!-- /generated:sessions-quality -->

### 5.5 The voids and what the scan saw

<!-- generated:voids -->
| series | rid | kind | stack | phase | harness | high hits | rule | where | path the scan saw | charged |
|---|---|---|---|---|---|---|---|---|---|---|
| 92a9 | 9804fc | pilot | TS | v1 | e63941bdbdf9 | 3 | not persisted | — | not persisted (gap fixed in 5249048) | $0.2121 |
| 0c1a | c9b413 | pilot | TS | v1 | de9091b54e2b | 1 | outside-path | main:Bash | `/` | $0.2284 |
| bf89 | ebd0b0 | data p2 | C# | v2 | faaf55b4ecf7 | 1 | outside-path | main:Bash | `~/rsd/minigit/s-20261030-bf89/s/ebd0b0/box/test-v2.sh` | $0.2790 |
| bf89 | 3407d8 | data p3 | C# | v2 | faaf55b4ecf7 | 1 | outside-path | main:Bash | `~/rsd/minigit/s-20261030-bf89/s/3407d8/box/test-v2.sh` | $0.2795 |
<!-- /generated:voids -->

- `9804fc`: the scan of that harness did not keep its hits (fixed in `5249048`); the cause was found by reading the session:
  the pinned TypeScript toolchain and the box's own config directory were outside its allow-list.
- `c9b413`: the agent cleaned up with `cd / && rm -rf /tmp/…`; the scan read the `cd /` as a walk of `/`.
- `ebd0b0` and `3407d8`: each agent ran `cd src` in one Bash call and `cd .. && bash test-v2.sh` in the next. The Bash
  tool keeps its working directory across calls, so the agent was back in its work tree; the scan started every call at
  the work tree, so it put `test-v2.sh` one level above it. The team-lead log records that a replay of every real session
  at `9475b1fed23f` and then at `814d82e66e53` gave no high hit, with these voids and the two pilot voids reading
  `completed`; this record did not re-run that replay.

### 5.6 The v2 rehearsals (G-14)

A stub CLI on an A/A twin's tree, the real scorer, for both stacks; no paid request.

<!-- generated:rehearsals -->
| series | rid | stack | class | stub model | ledger charge | G-14 checks |
|---|---|---|---|---|---|---|
| 92a9 | 555233 | TS | completed | claude-sonnet-5-5 | $0.0000 | workspace ok, record ok, score ok |
| 92a9 | bd4a53 | C# | completed | claude-sonnet-5-5 | $0.0000 | workspace ok, record ok, score ok |
| 0c1a | a164d6 | C# | completed | claude-sonnet-5-5 | $0.0000 | workspace ok, record ok, score ok |
| 0c1a | ebe7f5 | TS | completed | claude-sonnet-5-5 | $0.0000 | workspace ok, record ok, score ok |
| bf89 | 4c53a2 | C# | completed | claude-sonnet-5-5 | $0.0000 | workspace ok, record ok, score ok |
| bf89 | d7a0b7 | TS | completed | claude-sonnet-5-5 | $0.0000 | workspace ok, record ok, score ok |
<!-- /generated:rehearsals -->

### 5.7 Load during each session

From the per-phase CPU sample files (5 s interval). Foreign CPU is host busy minus the harness's own ticks, in
CPU-equivalents, and is a lower bound (a process born and gone between two samples is missed).

<!-- generated:cpu -->
| series | samples file | rid | samples | foreign CPU mean | foreign CPU max | samples with a foreign agent | flags on the session |
|---|---|---|---|---|---|---|---|
| 92a9 | p0-v1-cpu.ndjson | 433bf6 | 10 | 0.12 | 0.21 | 0 | — |
| 92a9 | p0-v1-cpu.ndjson | d3f571 | 9 | 0.12 | 0.21 | 0 | — |
| 92a9 | p0-v1-cpu.ndjson | 9804fc | 10 | 0.11 | 0.15 | 0 | — |
| 0c1a | p0-v1-cpu.ndjson | 58d1fa | 9 | 0.10 | 0.14 | 0 | — |
| 0c1a | p0-v1-cpu.ndjson | a4fddc | 8 | 0.10 | 0.14 | 0 | — |
| 0c1a | p0-v1-cpu.ndjson | c9b413 | 12 | 0.10 | 0.13 | 0 | — |
| bf89 | p0-v1-cpu.ndjson | 586c41 | 9 | 0.09 | 0.11 | 0 | — |
| bf89 | p0-v1-cpu.ndjson | 5f55d0 | 7 | 0.10 | 0.11 | 0 | — |
| bf89 | p0-v1-cpu.ndjson | 98ee3c | 9 | 0.09 | 0.12 | 0 | — |
| bf89 | p1-v1-cpu.ndjson | 078dcd | 10 | 0.11 | 0.20 | 0 | — |
| bf89 | p1-v1-cpu.ndjson | 14d81c | 9 | 0.11 | 0.20 | 0 | — |
| bf89 | p1-v2-cpu.ndjson | 1d2624 | 10 | 0.12 | 0.20 | 0 | — |
| bf89 | p1-v2-cpu.ndjson | ede2a0 | 11 | 0.12 | 0.20 | 0 | — |
| bf89 | p2-v1-cpu.ndjson | d79dc1 | 9 | 0.10 | 0.17 | 0 | — |
| bf89 | p2-v1-cpu.ndjson | f57599 | 8 | 0.10 | 0.17 | 0 | — |
| bf89 | p2-v2-cpu.ndjson | 97168d | 11 | 0.10 | 0.13 | 0 | — |
| bf89 | p2-v2-cpu.ndjson | ebd0b0 | 15 | 0.11 | 0.22 | 0 | — |
| bf89 | p2-v2-cpu.ndjson | d9638c | 12 | 2.99 | 10.47 | 0 | foreign load |
| bf89 | p3-v1-cpu.ndjson | 922a82 | 9 | 0.11 | 0.15 | 0 | — |
| bf89 | p3-v1-cpu.ndjson | f9cc17 | 11 | 0.11 | 0.15 | 0 | — |
| bf89 | p3-v2-cpu.ndjson | 3407d8 | 14 | 0.12 | 0.20 | 0 | — |
| bf89 | p3-v2-cpu.ndjson | 96fcf4 | 10 | 0.12 | 0.20 | 0 | — |
<!-- /generated:cpu -->

## 6. Pairs (series A)

Only data pairs enter this section; the A/A and the pilots never do. The A/A twin spread (G-9) is shown in § 7.4 as a
reference at n = 1.

### 6.1 Per phase

Descriptive: the registered unit is the cell (v1 + v2), not the phase. Pair 3 has a v1 comparison and no C# v2.

<!-- generated:pairs-phase -->
| pair | phase | C# rid | TS rid | C# cost (headline) | TS cost (headline) | C#/TS | C# wall s | TS wall s | C#/TS | C# model_api s | TS model_api s | C#/TS | C# active − model_api s | TS active − model_api s | C#/TS |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | v1 | 078dcd | 14d81c | $0.1847 | $0.2101 | 0.88 | 49.3 | 46.7 | 1.06 | 39.6 | 42.0 | 0.94 | 9.1 | 4.0 | 2.26 |
| 1 | v2 | ede2a0 | 1d2624 | $0.2574 | $0.2679 | 0.96 | 56.8 | 49.1 | 1.16 | 47.0 | 36.1 | 1.30 | 9.1 | 12.3 | 0.74 |
| 2 | v1 | f57599 | d79dc1 | $0.1909 | $0.2118 | 0.90 | 40.2 | 44.2 | 0.91 | 33.4 | 40.6 | 0.82 | 6.0 | 2.9 | 2.09 |
| 2 | v2 | d9638c | 97168d | $0.2720 | $0.3012 | 0.90 | 62.6 | 56.3 | 1.11 | 43.7 | 46.4 | 0.94 | 18.2 | 9.3 | 1.96 |
| 3 | v1 | f9cc17 | 922a82 | $0.1941 | $0.2155 | 0.90 | 55.7 | 42.2 | 1.32 | 42.9 | 38.9 | 1.10 | 12.1 | 2.6 | 4.58 |
| 3 | v2 | missing | 96fcf4 | — | — | — | — | 49.8 | — | — | — | — | — | — | — |
<!-- /generated:pairs-phase -->

### 6.2 Per cell (v1 + v2) and the C#/TS ratio

<!-- generated:pairs-cell -->
| pair | C# cell (v1 + v2) | TS cell (v1 + v2) | C# cost (headline) | TS cost (headline) | C#/TS cost | C# wall s | TS wall s | C#/TS wall | C# model_api s | TS model_api s | C#/TS model_api | C# active − model_api s | TS active − model_api s | C#/TS active | labels |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 078dcd + ede2a0 | 14d81c + 1d2624 | $0.4421 | $0.4780 | 0.92 | 106.2 | 95.8 | 1.11 | 86.6 | 78.1 | 1.11 | 18.2 | 16.3 | 1.12 | — |
| 2 | f57599 + d9638c | d79dc1 + 97168d | $0.4629 | $0.5130 | 0.90 | 102.8 | 100.6 | 1.02 | 77.1 | 87.0 | 0.89 | 24.2 | 12.2 | 1.99 | `ran unpaired` cell; flagged: foreign load (d9638c) |
| 3 | f9cc17 + — | 922a82 + 96fcf4 | missing | — | — (a cell is missing) | missing | 92.1 | — (a cell is missing) | missing | — | — (a cell is missing) | missing | — | — (a cell is missing) | C# missing: no v2 data session (3407d8 void: contaminated; the series stopped before its re-run); TS incomplete: costUsd, modelApiMs, activeMinusModelApiMs not captured |
<!-- /generated:pairs-cell -->

### 6.3 Ratios over the pairs, three ways

(a) every data cell; (b) without cells that hold a `ran unpaired` session; (c) without flagged pairs (a pair is flagged
when any of its sessions carries `foreign load`, `harness core contended`, `started not quiet`, `start skew`, `broker
refused k`, `upstream 429 k` or `model_refusal_fallback`). No session was censored (no cap, wall or stall kill), so every
ratio below is a value, not a bound.

<!-- generated:medians -->
| way | pairs used | excluded | missing | cost: median C#/TS (range); C# > TS in k of n | wall: median C#/TS (range); C# > TS in k of n | model_api: median C#/TS (range); C# > TS in k of n | active: median C#/TS (range); C# > TS in k of n |
|---|---|---|---|---|---|---|---|
| (a) every data cell | 1, 2 | — | 3 | 0.91 (0.90–0.92); 0 of 2 | 1.07 (1.02–1.11); 2 of 2 | 1.00 (0.89–1.11); 1 of 2 | 1.55 (1.12–1.99); 2 of 2 |
| (b) without cells holding a `ran unpaired` session | 1 | 2 | 3 | 0.92 (0.92–0.92); 0 of 1 | 1.11 (1.11–1.11); 1 of 1 | 1.11 (1.11–1.11); 1 of 1 | 1.12 (1.12–1.12); 1 of 1 |
| (c) without flagged pairs | 1 | 2 | 3 | 0.92 (0.92–0.92); 0 of 1 | 1.11 (1.11–1.11); 1 of 1 | 1.11 (1.11–1.11); 1 of 1 | 1.12 (1.12–1.12); 1 of 1 |
<!-- /generated:medians -->

**With the voided attempts counted.** Not a registered measure: the money each stack's cell cost, every attempt
included. A void is an instrument error, so this view charges the C# side for the scan's defect.

<!-- generated:pairs-spend-voids -->
| pair | C# charged, voids included | C# voids | TS charged, voids included | TS voids | C#/TS |
|---|---|---|---|---|---|
| 1 | $0.4421 | — | $0.4780 | — | 0.92 |
| 2 | $0.7419 | ebd0b0 | $0.5130 | — | 1.45 |
| 3 | $0.4737 | 3407d8 | $0.4762 | — | 0.99 |
<!-- /generated:pairs-spend-voids -->

### 6.4 Per stack

Median and range over the cells that are complete, with every primary measure known. The TypeScript wall includes the
pair-3 cell, whose other measures are not captured.

<!-- generated:per-stack -->
| stack | cost (headline): median (range), n | wall s: median (range), n | model_api s: median (range), n | active − model_api s: median (range), n |
|---|---|---|---|---|
| C# | $0.4525 ($0.4421–$0.4629), 2 | 104.5 (102.8–106.2), 2 | 81.9 (77.1–86.6), 2 | 21.2 (18.2–24.2), 2 |
| TS | $0.4955 ($0.4780–$0.5130), 2 | 98.2 (95.8–100.6), 2 | 82.6 (78.1–87.0), 2 | 14.3 (12.2–16.3), 2 |
<!-- /generated:per-stack -->

### 6.5 Quality per stack and phase

"accepted" = build clean, every visible script 100 %, held-out primary 100 % with blocked cases counted against it
([checklist.json](../../spikes/minigit-bench/inputs/checklist.json)). Acceptance is a label, not a filter.

<!-- generated:quality-summary -->
| stack | phase | data sessions | scored | not scored | held-out percent: median (min) | defects: median (max) | accepted |
|---|---|---|---|---|---|---|---|
| C# | v1 | 3 | 3 | — | 100 (100) | 0 (0) | 3 of 3 |
| C# | v2 | 2 | 2 | — | 100 (100) | 0 (0) | 2 of 2 |
| TS | v1 | 3 | 3 | — | 100 (100) | 0 (0) | 3 of 3 |
| TS | v2 | 3 | 2 | 96fcf4 | 100 (100) | 0 (0) | 2 of 2 |
<!-- /generated:quality-summary -->

Every scored session is at the top of the held-out scale, so the held-out suite does not separate the stacks on this
task: a ceiling, not evidence of equal quality on harder work.

### 6.6 The pre-registered reading

The registered paragraph ([statement.md](../../spikes/minigit-bench/inputs/statement.md) § 7, parent § 11, verbatim):

> **What n = 3 licenses:** "in k of 3 pairs", and whether ranges overlap (overlap = not distinguished). A predicted direction
> held in 3 of 3 pairs is the strongest available result and still has 1/8 probability under no effect — so no p-values, no
> confidence intervals, no effect sizes as findings. It does not license other tasks, languages, models or CLI versions, the
> value of the process on real work, or any mechanism.

Applied to series A:

- **n reached:** two complete pairs (1 and 2). Pair 3 is **missing**: its C# cell has no v2 data session (`3407d8` voided,
  and the series stopped before the re-run), and its TypeScript v2 `96fcf4` was never scored.
- **Unpaired and flagged:** pair 2's C# v2 is the re-run `d9638c`, `ran unpaired` and flagged `foreign load`. Lines (b) and
  (c) of § 6.3 therefore hold pair 1 only.
- **Voids and their cause:** `ebd0b0` and `3407d8`, both `void: contaminated`, both false positives of the per-call
  working-directory model (defect 14). They are not data and enter no ratio; § 6.3's last table shows the spend view
  with them.
- **Ranges:** the per-stack ranges of § 6.4, side by side. Overlap means "not distinguished". Ranges that do not overlap
  at n = 2 per stack are two points each: the registration names no finding for them, and they are reported, not read.

<!-- generated:overlap -->
| measure | C# range (n) | TS range (n) | ranges overlap | reading |
|---|---|---|---|---|
| cost (headline) | $0.4421–$0.4629 (2) | $0.4780–$0.5130 (2) | no | C# below TS in every cell reached (n = 2 per stack: not a finding) |
| wall s | 102.8–106.2 (2) | 95.8–100.6 (2) | no | C# above TS in every cell reached (n = 2 per stack: not a finding) |
| model_api s | 77.1–86.6 (2) | 78.1–87.0 (2) | yes | not distinguished |
| active − model_api s | 18.2–24.2 (2) | 12.2–16.3 (2) | no | C# above TS in every cell reached (n = 2 per stack: not a finding) |
<!-- /generated:overlap -->

The bare-first deviation, verbatim from the statement (§ 8), still applies:

> 1. **The interaction** (prediction 3) compares windows days apart instead of interleaved blocks: server-side model changes,
>    API latency and time of day differ between arms. D12 catches an alias resolving to a new id, not a change behind one id.
> 2. **No arm-lead draw** for the bare cells: order between arms is confounded with time.
> 3. **T3 is armed for bare cells on bare evidence** (the A/A and the E2.S2 smoke); the process arm re-decides at P1.
> 4. **The process treatment is finalized after bare results are visible** (E2.S4). Parent §6 already fixes it; any change after
>    the first bare pair is a dated amendment applied to both stacks, never motivated by one stack's bare result.
> 5. **The prediction is registered in two parts**; the process part is public since `01b7459` with its dated edits, all
>    before any cell, so the split costs no evidence.
>
> The C#/TS comparison **inside** the bare arm keeps its concurrent pairs and loses nothing.

**banteg, as a ratio only, and with the caveat in the same sentence:** banteg's re-priced cost ratio is C#/TS 1.32× (other
agent, other model, other date, so the two numbers are not the same measurement); series A's cost ratios are in § 6.3.

### 6.7 The prediction beside the observation

Items 1, 2, 5 and 7 of [prediction.md](../../spikes/minigit-bench/inputs/prediction.md), read on the pairs series A
reached. The final reading belongs to the bare arm's full n.

<!-- generated:prediction -->
| item | registered expectation | observed (series pairs reached) | reading |
|---|---|---|---|
| 1 | C# > TS in ≥ 2 of 3 pairs (cost) | 0 of 2 | **not held** — 1 pair(s) missing; with them k could still not reach 2 |
| 1 | median C#/TS cost 1.15–1.5× | 0.91 | **not held** — n = 2, range 0.90–0.92 |
| 1 | per cell: TS $0.8–2.5, C# $1.0–3.5 | TS $0.48, $0.51; C# $0.44, $0.46 | **not held** — every complete cell is below its predicted range |
| 2 | wall C#/TS 1.3–1.8× | 1.07 | **not held** — n = 2, range 1.02–1.11 |
| 2 | API-time ratio 1.1–1.4× | 1.00 | **not held** — n = 2, range 0.89–1.11 |
| 5 | every bare session passes the visible tests | 10 scored data sessions | **held** — every scored session passes; not scored: 96fcf4 |
| 5 | held-out median percent ≥ 90 % in both stacks | lowest per-phase median 100 % | **held** — lowest scored session 100 % |
| 5 | … with at least one bare failure | no failure | **not held** — no scored data session failed a held-out case |
| 5 | no stack difference > 1 defect | largest per-phase median difference 0 | **held** — per-phase medians of `defects` |
| 7 | surprise: TS costing ≥ C# in ≥ 2 bare pairs | 2 of 2 | **the named surprise occurred** |
<!-- /generated:prediction -->

## 7. The A/A checks (G-1 … G-14)

The GO definition is [analysis-contract.md](../../spikes/minigit-bench/inputs/analysis-contract.md) § 10: every G holds
(G-6 `explained` counts as holds; G-9 never gates), except G-4's subagent half and G-12, which may be `not exercised`
with replacing evidence. A by-hand G without a dated line is NO GO.

### 7.1 `s-20261030-92a9`

<!-- generated:aa-92a9 -->
No G table: no aa-verdicts.json in this series: the tool (commit 2c2555a) was written after the series ran.
<!-- /generated:aa-92a9 -->

What the records of that series show (§ 5): both twins completed with T1–T5 passing, B = E, every scored session accepted;
the rehearsal held for both stacks; the Node pilot was voided (G-8 refuted). The by-hand G-10 and G-13 lines were never
written for it.

### 7.2 `s-20261030-0c1a`

<!-- generated:aa-0c1a -->
Verdict **STOP**, stops: G-8; computed 2026-10-30T17:10:47.988Z at harness `de9091b54e2b`; T3 rule: `stop`.

| G | must show | verdict | observed | replacing evidence |
|---|---|---|---|---|
| G-1 | the read-back is ok on every init field | holds | 58d1fa: the read-back is ok on every init field; a4fddc: the read-back is ok on every init field; c9b413: the read-back is ok on every init field | — |
| G-2 | T1 | holds | 58d1fa: T1 pass; a4fddc: T1 pass; c9b413: T1 pass | — |
| G-3 | T2 per model and kind | holds | 58d1fa: T2 pass; a4fddc: T2 pass; c9b413: T2 pass | — |
| G-4 | T3 and T4 over matched sources: main and subagent | not exercised | 58d1fa: T3 and T4 pass; a4fddc: T3 and T4 pass; no twin launched a subagent | the subagent half: the E2.S2 smoke fixture's B-T1 on CLI 2.1.310 (test/fixtures/real/claude-2.1.310-bare-smoke): 1 main and 1 subagent transcript file(s); T3 pass, T4 pass |
| G-5 | T5: B = E within $0.000001 | holds | 58d1fa: T5 pass; a4fddc: T5 pass; c9b413: T5 pass | — |
| G-6 | partition sum = wall and unattributed < 5 % (or explained) | refuted | 58d1fa: the categories sum to 42697 ms, the registered wall is 46255 ms: a gap of 3558 ms (counted as unattributed), unattributed is 7.8 % of the wall (limit 5 %); a4fddc: the categories sum to 40311 ms, the registered wall is 43860 ms: a gap of 3549 ms (counted as unattributed), unattributed is 8.3 % of the wall (limit 5 %); c9b413: the categories sum to 60063 ms, the registered wall is 63605 ms: a gap of 3542 ms (counted as unattributed), unattributed is 5.7 % of the wall (limit 5 %) | — |
| G-7 | the same record kinds for each twin; aliases per D12 | holds | both twins carry stream, envelope, O, T, audit, broker, egress with bytes, CPU and spin samples, packages, a score with every step known and a ledger line; every alias resolves to the baseline | — |
| G-8 | isolation: hosts, the real token 0, no contamination | refuted | c9b413: 1 high contamination hit(s) (class void: contaminated) | — |
| G-9 | the twin spread, recorded, never gating | recorded | 58d1fa against a4fddc: {"costRatio":1.0715990890137563,"wallRatio":1.0546055631554947,"modelApiRatio":1.1368751152923815,"activeMinusModelApiRatio":1.3626062322946175,"heldoutPercentDiff":0,"heldoutDefectsDiff":0,"cpuPerMask":{"0-5":0.052685896887211967,"8-13":0.05230654989692062}} | — |
| G-10 | the scorer by hand on a twin tree and the Node tree | not exercised | 58d1fa: no dated G-10 line for 58d1fa; c9b413: no dated G-10 line for c9b413 | — |
| G-11 | quiet check, sampler, spin probe, mask observed | holds | 58d1fa: the quiet check passed, the sampler and the spin probe wrote every interval, the mask was observed; a4fddc: the quiet check passed, the sampler and the spin probe wrote every interval, the mask was observed; c9b413: the quiet check passed, the sampler and the spin probe wrote every interval, the mask was observed | — |
| G-12 | the cap stops a session that reaches it at settled spend >= the cap | not exercised | no session reached the cap; no dated G-12 holds line | — |
| G-13 | each session's class read by hand | not exercised | 58d1fa: no dated G-13 line for 58d1fa; a4fddc: no dated G-13 line for a4fddc; c9b413: no dated G-13 line for c9b413 | — |
| G-14 | the v2 rehearsal for both stacks | holds | csharp holds; node holds | — |
<!-- /generated:aa-0c1a -->

### 7.3 `s-20261030-bf89` (series A)

<!-- generated:aa-bf89 -->
Verdict **GO**; computed 2026-10-30T18:35:32.914Z at harness `3129eeadfa8d`; T3 rule: `stop`.

| G | must show | verdict | observed | replacing evidence |
|---|---|---|---|---|
| G-1 | the read-back is ok on every init field | holds | 5f55d0: the read-back is ok on every init field; 586c41: the read-back is ok on every init field; 98ee3c: the read-back is ok on every init field | — |
| G-2 | T1 | holds | 5f55d0: T1 pass; 586c41: T1 pass; 98ee3c: T1 pass | — |
| G-3 | T2 per model and kind | holds | 5f55d0: T2 pass; 586c41: T2 pass; 98ee3c: T2 pass | — |
| G-4 | T3 and T4 over matched sources: main and subagent | not exercised | 5f55d0: T3 and T4 pass; 586c41: T3 and T4 pass; no twin launched a subagent | the subagent half: the E2.S2 smoke fixture's B-T1 on CLI 2.1.310 (test/fixtures/real/claude-2.1.310-bare-smoke): 1 main and 1 subagent transcript file(s); T3 pass, T4 pass |
| G-5 | T5: B = E within $0.000001 | holds | 5f55d0: T5 pass; 586c41: T5 pass; 98ee3c: T5 pass | — |
| G-6 | partition sum = wall and unattributed < 5 % (or explained) | explained | 5f55d0: the categories sum to 35703 ms, the registered wall is 39254 ms: a gap of 3551 ms (counted as unattributed), unattributed is 9.3 % of the wall (limit 5 %) (explained by the dated line: ~/rsd/minigit/s-20261030-bf89/handcheck/G-6-settle.txt); 586c41: the categories sum to 43353 ms, the registered wall is 46897 ms: a gap of 3544 ms (counted as unattributed), unattributed is 7.7 % of the wall (limit 5 %) (explained by the dated line: ~/rsd/minigit/s-20261030-bf89/handcheck/G-6-settle.txt); 98ee3c: the categories sum to 46117 ms, the registered wall is 49652 ms: a gap of 3535 ms (counted as unattributed), unattributed is 7.5 % of the wall (limit 5 %) (explained by the dated line: ~/rsd/minigit/s-20261030-bf89/handcheck/G-6-settle.txt) | — |
| G-7 | the same record kinds for each twin; aliases per D12 | holds | both twins carry stream, envelope, O, T, audit, broker, egress with bytes, CPU and spin samples, packages, a score with every step known and a ledger line; every alias resolves to the baseline | — |
| G-8 | isolation: hosts, the real token 0, no contamination | holds | 5f55d0: allowed CONNECTs within the bare list, refused within the known list, the real token 0, no contamination, canary or token hit; 586c41: allowed CONNECTs within the bare list, refused within the known list, the real token 0, no contamination, canary or token hit; 98ee3c: allowed CONNECTs within the bare list, refused within the known list, the real token 0, no contamination, canary or token hit | — |
| G-9 | the twin spread, recorded, never gating | recorded | 5f55d0 against 586c41: {"costRatio":1.0245649040504956,"wallRatio":1.1947062719722832,"modelApiRatio":1.1772028395518621,"activeMinusModelApiRatio":1.4102520435967303,"heldoutPercentDiff":0,"heldoutDefectsDiff":0,"cpuPerMask":{"0-5":0.06199196835827111,"8-13":0.049983806399632694}} | — |
| G-10 | the scorer by hand on a twin tree and the Node tree | holds | 5f55d0: holds by hand: ~/rsd/minigit/s-20261030-bf89/handcheck/G-10.txt; 98ee3c: holds by hand: ~/rsd/minigit/s-20261030-bf89/handcheck/G-10.txt | — |
| G-11 | quiet check, sampler, spin probe, mask observed | holds | 5f55d0: the quiet check passed, the sampler and the spin probe wrote every interval, the mask was observed; 586c41: the quiet check passed, the sampler and the spin probe wrote every interval, the mask was observed; 98ee3c: the quiet check passed, the sampler and the spin probe wrote every interval, the mask was observed | — |
| G-12 | the cap stops a session that reaches it at settled spend >= the cap | not exercised | no session reached the cap | dated G-12 line for 5f55d0: ~/rsd/minigit/s-20261030-bf89/handcheck/G-12-P-6-3129eeadfa8d.txt (P-6, harness 3129eeadfa8d) |
| G-13 | each session's class read by hand | holds | 5f55d0: holds by hand: ~/rsd/minigit/s-20261030-bf89/handcheck/G-13.txt; 586c41: holds by hand: ~/rsd/minigit/s-20261030-bf89/handcheck/G-13.txt; 98ee3c: holds by hand: ~/rsd/minigit/s-20261030-bf89/handcheck/G-13.txt | — |
| G-14 | the v2 rehearsal for both stacks | holds | csharp holds; node holds | — |
<!-- /generated:aa-bf89 -->

### 7.4 The twin spread (G-9)

Recorded, never gating; the twins of `92a9` have no G table. The walls of every twin here include the 3 s settle.

<!-- generated:aa-spread -->
| series | cost twin ratio | wall | model_api | active − model_api | held-out percent diff | defects diff | CPU per mask |
|---|---|---|---|---|---|---|---|
| s-20261030-0c1a | 1.07 | 1.05 | 1.14 | 1.36 | 0 | 0 | 0-5: 0.053, 8-13: 0.052 |
| s-20261030-bf89 | 1.02 | 1.19 | 1.18 | 1.41 | 0 | 0 | 0-5: 0.062, 8-13: 0.050 |
<!-- /generated:aa-spread -->

### 7.5 The by-hand evidence (series A)

Copied whole from `~/rsd/minigit/s-20261030-bf89/handcheck/`, with each file's sha256. The dated lines that cite them are
in [amendments.md](../../spikes/minigit-bench/inputs/amendments.md) (§ 9.2).

<!-- generated:handcheck -->
`G-10.txt` (sha256 `cbd56f90090d`):

```text
G-10 holds (2026-10-30, team lead, by hand). Series s-20261030-bf89.
Fixed visible test: ~/git/wt/minigit-bench-inputs/test-v1.sh, sha256 3dfcf64304f4aae6c3eb13cb915ebec81cfc3000a690d29a5c3411a4141ac27c (= inputs/hashes.txt line 5).
Each tree copied to a scratch dir, run in bwrap (--unshare-net, home/mnt/run/tmp hidden), make then bash test-v1.sh:
- 5f55d0 (C# twin), copied as the scorer copies (minus bin/ obj/ node_modules/): make rc 0, PASSED 11 FAILED 0 TOTAL 11.
- 5f55d0 clean rebuild from source (build/ and the wrapper removed): make rc 0, PASSED 11 FAILED 0 TOTAL 11.
- 98ee3c (Node pilot, no build step): PASSED 11 FAILED 0 TOTAL 11.
Scorer's records: both visible test-v1.sh 11/11, buildOk true. The by-hand counts equal the scorer's.
(A first attempt excluded build/ and gave 1/11: an error in the by-hand copy, not the scorer; the scorer keeps build/.)
```

`G-12-P-6-3129eeadfa8d.txt` (sha256 `4a52a8f29058`):

```text
G-12 replacing evidence (2026-10-30, team lead). No session of s-20261030-bf89 reached the $6 cap.
- E2.S2 broker proof P-6 (todo/PLAN_minigit_stack_bench_E2.md, "The credential broker, and what was run to prove it", P-6).
- Budget tests green at the run's harness identity 3129eeadfa8d: clean worktree at ae50489 (harnessIdentity = 3129eeadfa8d9e2c856a6315d2e5f38929ea4bc0f33d4d60a6e9adc96143a952),
  BENCH_REQUIRE_SANDBOX=1 node --test test/budget.test.mjs test/broker.test.mjs test/watchdogs.test.mjs: 43 tests, 43 pass, 0 fail, 0 skipped.
```

`G-13.txt` (sha256 `961223ef947e`):

```text
G-13 holds (2026-10-30, team lead, read by hand against stream.ndjson and broker.ndjson). Series s-20261030-bf89.
- 5f55d0: result envelope success, is_error false, 5 turns; exit code 0, no stop; broker 5 forwarding, 5 settled, all status 200, 0 refused -> completed (= recorded).
- 586c41: result envelope success, is_error false, 4 turns; exit code 0, no stop; broker 4 forwarding, 4 settled, all status 200, 0 refused -> completed (= recorded).
- 98ee3c: result envelope success, is_error false, 10 turns; exit code 0, no stop; broker 7 forwarding, 7 settled, all status 200, 0 refused -> completed (= recorded).
```

`G-6-settle.txt` (sha256 `d3cd9ef814a7`):

```text
G-6 explained (2026-10-30, team lead, read by hand). Series s-20261030-bf89.
The gap between the partition sum and the recorded wall is the harness's own settle, not a missing span source:
- lib/session-launch.mjs:185 sleeps plan.settleMs (3000 ms, DEFAULTS line 230) AFTER the CLI exits; :211 stamps endedAt/wallMs only after it.
- Measured per session (meta.startedAt/endedAt vs the first/last line of stream.stamped.ndjson):
  5f55d0: spawn->first stream event 501 ms; last stream event->endedAt 3050 ms; wall 39254; partition sum 35703
  586c41: spawn->first stream event 497 ms; last stream event->endedAt 3047 ms; wall 46897; partition sum 43353
  98ee3c: spawn->first stream event 482 ms; last stream event->endedAt 3053 ms; wall 49652
- The ~0.5 s before the first event is the CLI's own start (no tool, no request yet; first OTel record at +1.37-1.39 s).
- Every category the partition knows (build_test, model_api, other_tool, hooks) is present in each record; no span source is missing.
- Fix: record the CLI process exit as the wall's end (instrument amendment after this A/A, before pair 1).
```
<!-- /generated:handcheck -->

**Not done:** the pair-1 by-hand read-back that the E3b plan registers (argv, read-back, binds, snapshot hashes, first
stream lines and the package list of both v1 sessions, as a dated `amendments.md` line) has no line for series A. Series
B should carry it.

## 8. Spend

Every paid line once: the registered prior spend once, then each series' own lines. A ledger of a later series also
carries the earlier series' lines (`fromSeries`); they are not counted twice here. The rehearsals are $0.

<!-- generated:ledger -->
| when (UTC for series lines) | series | kind | rid | what | charged |
|---|---|---|---|---|---|
| 2026-10-29 | before E3b | prior | — | E1.S3 capture session (one tiny session on an unrelated task) | $0.1810 |
| 2026-10-29 | before E3b | prior | — | token check (one small claude -p call to check the subscription token; approximate) | $0.0100 |
| 2026-10-29 | before E3b | prior | — | E2.S2 design run (one bare session, real token in the box) | $0.1119 |
| 2026-10-29 | before E3b | prior | — | E2.S2 credential-broker proof sessions | $0.0185 |
| 2026-10-30 | before E3b | prior | — | E2.S2 bare-session smoke through the broker (the first one) | $0.1148 |
| 2026-10-30 | before E3b | prior | — | E2.S2 first re-smoke after the review fixes | $0.1718 |
| 2026-10-30 | before E3b | prior | — | E2.S2 second re-smoke | $0.1093 |
| 2026-10-30 | before E3b | prior | — | E2.S2 third re-smoke, the committed fixture | $0.1089 |
| 2026-10-30T14:27:53.911Z | s-20261030-92a9 | aa | d3f571 | completed | $0.2228 |
| 2026-10-30T14:28:00.970Z | s-20261030-92a9 | aa | 433bf6 | completed | $0.2637 |
| 2026-10-30T14:30:16.630Z | s-20261030-92a9 | rehearsal | 555233 | completed | $0.0000 |
| 2026-10-30T14:30:25.776Z | s-20261030-92a9 | rehearsal | bd4a53 | completed | $0.0000 |
| 2026-10-30T14:33:59.382Z | s-20261030-92a9 | pilot | 9804fc | void: contaminated | $0.2121 |
| 2026-10-30T17:06:16.602Z | s-20261030-0c1a | aa | 58d1fa | completed | $0.2077 |
| 2026-10-30T17:06:22.882Z | s-20261030-0c1a | aa | a4fddc | completed | $0.2226 |
| 2026-10-30T17:07:22.616Z | s-20261030-0c1a | rehearsal | a164d6 | completed | $0.0000 |
| 2026-10-30T17:07:28.710Z | s-20261030-0c1a | rehearsal | ebe7f5 | completed | $0.0000 |
| 2026-10-30T17:10:41.188Z | s-20261030-0c1a | pilot | c9b413 | void: contaminated | $0.2284 |
| 2026-10-30T18:26:26.603Z | s-20261030-bf89 | aa | 5f55d0 | completed | $0.2141 |
| 2026-10-30T18:26:32.954Z | s-20261030-bf89 | aa | 586c41 | completed | $0.2194 |
| 2026-10-30T18:27:36.111Z | s-20261030-bf89 | rehearsal | 4c53a2 | completed | $0.0000 |
| 2026-10-30T18:27:42.377Z | s-20261030-bf89 | rehearsal | d7a0b7 | completed | $0.0000 |
| 2026-10-30T18:30:43.863Z | s-20261030-bf89 | pilot | 98ee3c | completed | $0.2319 |
| 2026-10-30T19:13:04.161Z | s-20261030-bf89 | data | 14d81c | completed | $0.2101 |
| 2026-10-30T19:13:15.302Z | s-20261030-bf89 | data | 078dcd | completed | $0.1847 |
| 2026-10-30T19:16:20.712Z | s-20261030-bf89 | data | 1d2624 | completed | $0.2679 |
| 2026-10-30T19:16:33.933Z | s-20261030-bf89 | data | ede2a0 | completed | $0.2574 |
| 2026-10-30T19:20:05.600Z | s-20261030-bf89 | data | f57599 | completed | $0.1909 |
| 2026-10-30T19:20:11.944Z | s-20261030-bf89 | data | d79dc1 | completed | $0.2118 |
| 2026-10-30T19:23:33.742Z | s-20261030-bf89 | void | ebd0b0 | void: contaminated | $0.2790 |
| 2026-10-30T19:23:48.515Z | s-20261030-bf89 | data | 97168d | completed | $0.3012 |
| 2026-10-30T19:44:44.338Z | s-20261030-bf89 | data | d9638c | completed | $0.2720 |
| 2026-10-30T20:30:43.630Z | s-20261030-bf89 | data | f9cc17 | completed | $0.1941 |
| 2026-10-30T20:30:51.346Z | s-20261030-bf89 | data | 922a82 | completed | $0.2155 |
| 2026-10-30T20:38:32.139Z | s-20261030-bf89 | data | 96fcf4 | completed | $0.2607 |
| 2026-10-30T20:41:05.672Z | s-20261030-bf89 | void | 3407d8 | void: contaminated | $0.2795 |
<!-- /generated:ledger -->

<!-- generated:ledger-totals -->
| series | paid sessions | bare spend (own lines) |
|---|---|---|
| s-20261030-92a9 | 3 | $0.6986 |
| s-20261030-0c1a | 3 | $0.6587 |
| s-20261030-bf89 | 16 | $3.7902 |

| total | value | limit | share of limit |
|---|---|---|---|
| prior instrument spend (registered, before E3b) | $0.8263 over 8 sessions | — | — |
| bare spend, every series | $5.1475 | $40 | 12.9 % |
| all spend (prior + bare) | $5.9738 | $500 | 1.19 % |

Check: the sum of the series' own lines is equal to the carried total in the last series' ledger ($5.1475).
<!-- /generated:ledger-totals -->

Series B's own spend is not in these totals; it is added when its record is.

## 9. Instrument defects, in the order they were found

### 9.1 The defects

All of them were found between the tag (`41395f7`) and series B's harness (`814d82e66e53`). Each fix was an instrument
amendment: a dated line naming the accepted harness identity, committed before the series that used it. The script checks
that every commit below exists and that every harness identity has a dated line.

<!-- generated:defects -->
| # | defect | found by | fix commit | harness identity line | what it would have done to the result |
|---|---|---|---|---|---|
| 1 | TypeScript toolchain flagged as an outside path | Node pilot 9804fc (series 92a9) | `5249048` | `c825944d587d` | every TypeScript session runs `tsc` from the pinned toolchain outside the work tree, so every TS session would be `void: contaminated` while C# (toolchain under /usr) was never flagged: no TS data point, and the second void stops the series. Against TS only. |
| 2 | contamination skeleton too wide | Opus review of the fix (B1) | `a382519` | `500e09206354` | the allowed skeleton covered an ancestor of the harness dir and the home, so `grep -r` or `cd ../..` could reach the harness with no hit: a contaminated session would count as data. |
| 3 | `~` and `$HOME` read as the operator home | Opus review of the fix (M1) | `fc6a226` | `500e09206354` | ordinary commands on the box home (package caches) would be high hits; the review judged it C#-leaning. False voids. |
| 4 | a walk through a symbolic link | coai code round | `653a79c`, `c1bd510` | `20ee8034dddc` | `ln -s ../.. up && grep -r up` reached outside with no hit (false negative). The first fix (ee66350f9112) flagged every link walk, `/usr` links included, which would void sessions of both stacks; the second keeps high only under a protected root. |
| 5 | a script symlink made after a `cd` | coai code round | `4cb10ce` | `de9091b54e2b` | the link was resolved from the work tree, not from the `cd` target: false negative. |
| 6 | `cd /` read as a walk from `/` | Node pilot c9b413 (series 0c1a) | `c475c99` | `965745a2adcc` | a clean-up `cd / && rm -rf /tmp/…` voided the pilot. Any session that cleans up this way would be a false void. |
| 7 | heredoc bodies, `rm -rf` and a `//` in a quoted string read as walks | replay of A/A twin d3f571 after the `cd` fix | `179838c` | `47bbc9f8f642` | a Makefile written by heredoc with `rm -rf` and a C# `//` comment would have voided a C# A/A twin. Mostly against C#. |
| 8 | a walk of a glob not judged by its prefix | team lead, during the replay | `27966ff` | `7aacd09cee84` | `grep -r token /*` was not seen as a walk of `/`: false negative. |
| 9 | a walker behind `xargs` (and other wrappers) | coai code round | `244bc47` | `3129eeadfa8d` | `… \| xargs grep …` was not judged by the producer of the pipe: false negative. |
| 10 | quadratic scan | coai code round | `e5823c2`, `9699634` | `3129eeadfa8d` | heredoc and step building copied per line; a long session would scan slowly. No effect on a value. |
| 11 | the wall included the 3 s harness settle | A/A of series bf89 (G-6) | `edca65c` | `179624f5fec2` | every wall of 92a9, 0c1a and the bf89 A/A and pilot is about 3 s too long and its `unattributed` too large (G-6). The bf89 data sessions ran after the fix. On a ~45 s session it moves each C#/TS wall ratio towards 1. |
| 12 | the stub CLI wrote no transcripts | the test suite after the T3 commit 255484e | `4687abe` | `eff387f2f438` | under `t3 = stop` the free v2 rehearsal would end as `T3 unchecked`, a series stop. No paid effect. |
| 13 | box supervision timers left running | coai code round | `97c9a78` | `faaf55b4ecf7` | a box that failed to start left its timers running. No session hit it. |
| 14 | per-call `cwd` in the contamination scan | data sessions ebd0b0 (pair 2) and 3407d8 (pair 3), series bf89 | `6b09da2` | `9475b1fed23f` | the scan started every Bash call at the work tree. Both C# v2 sessions ran `cd src` in one call and `cd .. && bash test-v2.sh` in the next, so the scan put `test-v2.sh` one level above the work tree: a high hit and a void. Two false voids, one C# re-run `ran unpaired`, and series bf89 stopped at the second. Against C# in this series. |
| 15 | the printed "Shell cwd was reset to" line chose the start | coai code round on the `cwd` fix | `52bdec2` | `814d82e66e53` | a command can print that line and choose where the scan thinks it is: false negative. Fixed before series b82e. |
<!-- /generated:defects -->

What the list says about the result, in one place:

- **Two defects voided sessions of one stack only.** Defect 1 voided every TypeScript session it met (found on the first
  pilot, before any data). Defect 14 voided C# v2 sessions in series A, because the C# agents in those two sessions moved
  into `src` and back. Neither was a property of the stack; both were properties of the scan meeting one agent habit.
- **Most of the others were false negatives** (2, 4, 5, 8, 9, 15): a contaminated session would have counted as data.
  They were found in code, by the reviews (Opus, coai) and the team lead, not in a paid session. The replay recorded in
  the team-lead log found no high hit in any real session at the final harness; this record did not re-run it.
- **Defect 11 biased the time of the instrument checks only**: the A/A and pilot walls of all three series include the
  3 s settle. The data sessions of series A do not.
- The `aa-verdicts` tool itself was corrected by the Opus review before it was used for a GO (G-6 compared with the
  registered wall, G-12's replacing line, G-9's defect count, G-10/G-13 not accepting `explained`, G-11's sampler gap,
  G-7's stop only on alias drift; commits `6503006` … `455baf7`).

### 9.2 Every dated line of `amendments.md`

<!-- generated:amendments -->
| date | kind | subject | reason or evidence |
|---|---|---|---|
| 2026-10-30 | instrument | harness:c825944d587d | instrument: TS toolchain and own box paths were flagged as outside-path (pilot 9804fc); hits persisted; aa-verdicts added (E3b.S4) |
| 2026-10-30 | instrument | harness:500e09206354 | instrument: G-8 TS toolchain/own box paths false positive fixed, skeleton narrowed (B1), box home (M1); aa-verdicts G-6/G-12 per contract (E3b.S4) |
| 2026-10-30 | instrument | harness:ee66350f9112 | instrument: G-8 contamination scan follows a symbolic link a walk goes through (link-out, link-walk); on top of the line before (E3b.S4) |
| 2026-10-30 | instrument | harness:20ee8034dddc | instrument: G-8 link-out and link-walk are high only for a target under a protected root, on the scale of outside-path; on top of the line before (E3b.S4) |
| 2026-10-30 | instrument | harness:de9091b54e2b | instrument: G-8 link rules resolve a script's symlink from the directory a cd moved to; on top of the line before (E3b.S4) |
| 2026-10-30 | instrument | harness:965745a2adcc | instrument: G-8 cd is no reach; relative paths resolve from the cd target for every rule (pilot c9b413); on top of the line before (E3b.S4) |
| 2026-10-30 | instrument | harness:47bbc9f8f642 | instrument: G-8 heredoc bodies are data, walks are judged per simple command, rm is no walk, a comment mark in a quoted string is no path (A/A twin d3f571); on top of the line before (E3b.S4) |
| 2026-10-30 | instrument | harness:7aacd09cee84 | instrument: G-8 a walk of a glob is judged as a walk of its fixed prefix (grep -r token /*); on top of the line before (E3b.S4) |
| 2026-10-30 | instrument | harness:3129eeadfa8d | instrument: G-8 a wrapped walker is judged by its pipe's producer; the scan builds steps and heredoc bodies in linear time (coai); on top of the line before (E3b.S4) |
| 2026-10-30 | G-6 | explained | 5f55d0 \| ~/rsd/minigit/s-20261030-bf89/handcheck/G-6-settle.txt \| team lead (Opus 5.5) |
| 2026-10-30 | G-6 | explained | 586c41 \| ~/rsd/minigit/s-20261030-bf89/handcheck/G-6-settle.txt \| team lead (Opus 5.5) |
| 2026-10-30 | G-6 | explained | 98ee3c \| ~/rsd/minigit/s-20261030-bf89/handcheck/G-6-settle.txt \| team lead (Opus 5.5) |
| 2026-10-30 | G-10 | holds | 5f55d0 \| ~/rsd/minigit/s-20261030-bf89/handcheck/G-10.txt \| team lead (Opus 5.5) |
| 2026-10-30 | G-10 | holds | 98ee3c \| ~/rsd/minigit/s-20261030-bf89/handcheck/G-10.txt \| team lead (Opus 5.5) |
| 2026-10-30 | G-12 | holds | 5f55d0 \| ~/rsd/minigit/s-20261030-bf89/handcheck/G-12-P-6-3129eeadfa8d.txt (P-6, harness 3129eeadfa8d) \| team lead (Opus 5.5) |
| 2026-10-30 | G-13 | holds | 5f55d0 \| ~/rsd/minigit/s-20261030-bf89/handcheck/G-13.txt \| team lead (Opus 5.5) |
| 2026-10-30 | G-13 | holds | 586c41 \| ~/rsd/minigit/s-20261030-bf89/handcheck/G-13.txt \| team lead (Opus 5.5) |
| 2026-10-30 | G-13 | holds | 98ee3c \| ~/rsd/minigit/s-20261030-bf89/handcheck/G-13.txt \| team lead (Opus 5.5) |
| 2026-10-30 | instrument | harness:179624f5fec2 | instrument: G-6 wall is spawn to process exit; the 3 s harness settle is no longer in the wall (A/A bf89); on top of the line before (E3b.S4) |
| 2026-10-30 | instrument | harness:eff387f2f438 | instrument: the stub CLI writes matching transcripts so T3/T4 check its sessions under t3 = stop (E3b.S4) |
| 2026-10-30 | instrument | harness:faaf55b4ecf7 | instrument: one finalizer settles the box supervision, so a box that fails to start leaves no timer running (coai; on top of the line before, E3b.S4) |
| 2026-10-30 | instrument | harness:9475b1fed23f | instrument: G-8 the Bash tool's cwd persists across calls and resets to the work tree when it leaves it (data session ebd0b0, series bf89); on top of the line before (E3b.S4) |
| 2026-10-30 | instrument | harness:814d82e66e53 | instrument: G-8 the printed "Shell cwd was reset to" line never chooses a start (coai: spoofable); the next start is the work tree after an outside end, unknown after an unknown end; on top of the line before (E3b.S4) |
<!-- /generated:amendments -->

## 10. Threats and limits

- **n is tiny.** Series A reached two complete pairs; one is flagged and holds an unpaired re-run. The registration's own
  ceiling is n = 3 and descriptive. Nothing here is a finding about C# or TypeScript in general.
- **Series A stopped early, on instrument errors that hit one stack.** The two false voids removed C# v2 sessions only. The
  re-run of one of them ran alone and under foreign load. The missing pair 3 cannot be filled inside series A.
- **The task is tiny.** Every session finished in about a minute with a handful of turns and no subagent (§ 5.3), and the
  C# build-and-test time is a small slice of the wall. On this task the comparison mostly measures how many model tokens
  each stack's agent used, not how the toolchains behave under load or at scale.
- **Quality is at the ceiling** (§ 6.5): every scored session passed everything, so quality does not separate the stacks.
- **Walls of the instrument checks include the 3 s settle** (series 92a9, 0c1a, and the A/A and pilot of bf89). The G-9
  wall spread is therefore slightly compressed.
- **Prompt-cache warmth.** The A/A twins in each series started cold (first-turn cache read in § 5.2); later sessions
  started warm. The two cells of a pair start in the same tick, so warmth meets both stacks alike within a pair.
- **All cache writes were 1h writes** (§ 5.2), priced at the 1h rate. A tool that prices them at the 5m rate gets a lower
  figure for both stacks.
- **One model id, one CLI version, one machine.** D12 catches a new id behind the `opus` alias, not a change behind the
  same id. Server-side latency and time of day are not controlled.
- **Records missing:** `96fcf4` has no record and no score; there is no `pairs/p3.json`; series 92a9 has no G table; the
  pilot `9804fc` kept no contamination hits; the V16 mixed-chain count is not in any record; the pair-1 read-back line was
  not written.
- **Known residual risks of the box, stated in [module_minigit_bench.md](../module_minigit_bench.md) and not closed:** the
  egress proxy checks only the first TLS ClientHello of a tunnel (a second hello or a TLS 1.2 renegotiation is piped
  unread); the contamination scan reads only the immediate wrapper form of a walker (`sudo xargs find`, or a walker
  started by a script or a shell function, is not seen); the session scan cannot see a token broken by more whitespace
  than its window overlap.

## 11. Series B (`s-20261030-b82e`) — running

> **Placeholder.** Series B runs the same registration on the fixed instrument (harness `814d82e66e53`, which includes
> defects 14 and 15). Its A/A, rehearsal, pilot, G table, pairs, spend and the bare arm's reading will be added here, from
> `data/s-20261030-b82e.json`, when it ends. Nothing in this record is a result of series B.

## 12. How to re-run the extraction

```bash
# from the repository root; reads ~/rsd/minigit/<series>/ (or $MINIGIT_RUNS), writes research/minigit/data/<series>.json
node research/minigit/tools/extract.mjs
node research/minigit/tools/extract.mjs --series s-20261030-b82e     # one series, e.g. series B when it ends
# rewrites every generated block of this file from research/minigit/data/
node research/minigit/tools/render.mjs
```

- `tools/extract.mjs` is read-only on the runs root. It reads `run-map.json`, `ledger.ndjson`, `aa-verdicts.json`,
  `rehearsal-v2.json`, `pairs/p<k>.json`, `pairs/*-cpu.ndjson`, `handcheck/*.txt` and each session's `record.json`,
  `meta.json` and `broker.json` (and, for a session without a record, the envelope line of `stream.ndjson`). It never
  opens `score/` (the held-out cases), never copies a canary or a dummy token, and writes `~` for the operator's home.
- It recomputes every cell and ratio from the session records and compares them with the harness's own `pairs/p<k>.json`;
  for pairs 1 and 2 of series A they agree (`pairCrosscheck` in the data file).
- `tools/render.mjs` fails on an unknown block, a missing data file, a defect commit that does not exist, or a harness
  identity with no dated line.
- The data files name the sha256 of each series file they were read from (`extractedFrom.sources`).

Not built: the plan's `bare-report.mjs` (E3b.S4, "Interim record") does not exist; this extractor and renderer stand in
for it, and this file stands in for the plan's `research/minigit/bare-pairs-<date>.md`.
