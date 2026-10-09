# RESULTS — the feature gate on the S7.3 suite: Fable, Opus, Astra, devstral, codestral, and the attempted mistral-large

> Status: **campaigns run 2026-09-30 to 2026-10-01; assessed by Astra 2026-10-09** (T6 of
> [PLAN_gate_measurement_tail.md](../todo/PLAN_gate_measurement_tail.md); § *The assessment*).
>
> - **What is in this record:** the campaigns, every failure with its cause, and Astra's strict reading of every valid
>   finding — seeds hit, high-value, overstatement.
> - **Astra as a reviewer stays at one cell.** Its 13 other cells are pending: the operator chose not to fill them
>   (2026-10-09).
> - **Not hand-checked:** strict percentages stay "not hand-checked" (E4).
> - **Added 2026-10-09:** NVIDIA Nemotron 3 Ultra and 3 Super through OpenRouter (§ *Nemotron*). By the operator's
>   choice they were read by hand, not by Astra.
>
> Related: [RESULTS_gate_mistral.md](RESULTS_gate_mistral.md) (Mistral Medium 3.5, including its feature gate),
> [RESULTS_gate_feature_first_real_report.md](RESULTS_gate_feature_first_real_report.md) (the four API models'
> feature-gate calibration), [RESULTS_gate_model_choice.md](RESULTS_gate_model_choice.md), [module_gate.md](module_gate.md).

## What was held fixed

| variable | value |
|---|---|
| suite | `gate-seeded#0d0da7662eab`, the S7.3 suite file `suite-s73b.json` |
| product | coai `3f351c05`, binary `8b1c538643ca`, the build of Mistral Medium 3.5's feature run; all runs share one report scope |
| harness | `bench` `699db4b` → `4feaacf` (#64 added `--tasks`) |
| Claude CLI | 2.1.284 throughout. From 2026-10-01 it is a copy pinned from npm (`@anthropic-ai/claude-code-win32-x64@2.1.284`), because VS Code had updated its own copy to 2.1.286 and deleted 2.1.284 |
| reviewers | `claude-fable-5-1-cli2284`, `claude-opus-5-5-cli2284`, `codex-gpt-6-astra-gates`, and through OpenRouter at the vendor's default `openrouter-devstral-2512`, `openrouter-codestral-2508` and `openrouter-mistral-large-2512` |
| assessor | `codex-gpt-6-astra-exe`, `strict-v1`, when it is back |

## The runs

| run | what | outcome |
|---|---|---|
| `01a0f2a7` | Fable, Opus, Astra, 7 × 2, 3 sessions at once | Fable 4 valid, Opus 4 valid + 1 `GoodEnough`, Astra 1 valid; **20 Claude cells `TurnFailed`** (see *Failures*). Astra benched after 1 cell, account out, **13 cells pending** |
| `01a0f2a8` | devstral, codestral, mistral-large, 7 × 1 | devstral 7/7 and codestral 7/7 valid; mistral-large 2/7 valid, 5 HTTP 429 |
| `01a0f2ba` | mistral-large alone, 7 × 1, one call at a time | 0/7: HTTP 429, and one answer cut at the 8,192-token output cap |
| `01a0f2ea` | Fable, Opus, 7 × 2, one session at a time | Fable 4, Opus 6 valid; **18 `TurnFailed`** |
| `01a0f678` | Fable, Opus, 7 × 1 | **0 valid**: the CLI could not start (VS Code had deleted 2.1.284). Stopped by PID; abandoned, 12 cells left pending on purpose |
| `01a0f67a` | Fable, Opus, 7 × 1, pinned CLI | 11 valid, 1 `TurnFailed` (Fable tsx2; stopped automatically at the first fast cell). Its 2 php1 cells were completed by `resume`: 13 valid (Fable 6, Opus 7) |
| `01a0f74d` | Fable, `--tasks tsx2`, × 2 | 2/2 valid (Fable) |
| `01a0f778` | Fable, Opus, `--tasks php1`, × 1 | 2/2 valid (Fable 1, Opus 1) |
| `01a0f7cf` | mistral-large, `--tasks cs2`, × 1, an availability check | 0/1: 12 minutes of HTTP 429 |

**Coverage of valid cells.** Every task has at least two valid cells for Fable and Opus. Runs `01a0f2a7`, `01a0f2ea`,
`01a0f67a`, `01a0f74d` and `01a0f778` together:
- Fable: 4 + 4 + 6 + 2 + 1 = 17;
- Opus: 4 + 6 + 7 + 0 + 1 = 18.

devstral and codestral are one valid cell per task, all from `01a0f2a8`.

| task | cs2 | rs3 | js3 | ts2 | py3 | tsx2 | php1 | valid |
|---|---|---|---|---|---|---|---|---|
| Fable 5.1 | 3 | 3 | 3 | 2 | 2 | 2 | 2 | 17 |
| Opus 5.5 | 3 | 3 | 3 | 2 | 3 | 2 | 2 | 18 |
| devstral-2512 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 7 |
| codestral-2508 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 7 |

## What was known before the assessment

Valid cells only, all seven tasks. **Every "per run" figure is per valid cell, which is one review by one reviewer of
one task, averaged over that reviewer's valid cells**; "s p50" is the median over them. The cost column is the product ledger's list-price estimate. For Fable and Opus it
is notional: the Claude CLI runs on a Claude Max subscription, which bills nothing per token.

| reviewer | valid cells | findings / run | tokens in / run | cost / run | s p50 |
|---|---|---|---|---|---|
| Fable 5.1 | 17 | 12.1 | 834 k | $13.72 (notional) | 1 044 |
| Opus 5.5 | 18 | 8.2 | 503 k | $2.85 (notional) | 261 |
| Astra | 1 | 3.0 | 126 k | subscription | 47 |
| devstral-2512 | 7 | 7.6 | 270 k | $0.124 | 114 |
| codestral-2508 | 7 | 1.1 | 79 k | $0.024 | 9 |
| mistral-large-2512 | 2 | 4.0 | 103 k | $0.055 | 281 |

- **codestral-2508 mostly answers "no issues".** On 5 of 7 tasks it read 34–90k tokens and returned 0 findings in one
  turn. It found something only on php1 (2) and tsx2 (6). These are valid cells and a real result, not a harness
  fault: a model built for code completion, asked to review.
- **Fable reads the most.** 834k tokens per review, at about 17 minutes p50.

## Failures, and what each was

- **The Claude Max usage window.** In three runs, after about 1–1.5 hours of Fable and Opus work, every call failed
  within 3–5 s with `exit 1 (the CLI said nothing on stderr)`, and later calls succeeded again. This product,
  `3f351c05`, predates coai #622, so the CLI's own reason was dropped.
  - **Why T5 did not catch it:** T5 deliberately does not read a cause into that empty sentence.
  - **What happened:** the cells settled as `TurnFailed`, 38 in `01a0f2a7` and `01a0f2ea`.
  - **What was done:**
    - run one session at a time;
    - stop the run automatically by PID at the first cell under 30 s, polling every 5 s;
    - re-run only the missing cells with `bench gate run --tasks` (#64).
  - **Why tsx2 and php1 were the gap:** every campaign runs the tasks in suite order, so they were always the cells the
    window cut off.
- **The CLI deleted under the runs.** VS Code auto-updated Claude Code to 2.1.286 and removed the 2.1.284 folder the
  rows pointed at, so `01a0f678` could not start the CLI. The fix was a pinned copy from npm. A `resume` refuses once
  that path changes (the reviewer's references resolve elsewhere), so the run was abandoned.
- **mistral-large-2512 is unreachable through OpenRouter.** Every attempt over two days returned HTTP 429:
  `mistralai/mistral-large-2512 is temporarily rate-limited upstream`, `limit_source: upstream_provider_shared_pool`.
  - **What it would need:** the operator's own Mistral key added to OpenRouter (BYOK).
  - **The decision:** the operator chose not to buy anything beyond OpenRouter, so the model is **dropped**
    (2026-10-01).
  - **Its replacement:** within Mistral, the coding model devstral-2512 is the candidate, decided by the assessment.
- **Astra benched for its own account.** Its Codex limit comes back on 2026-10-06. As a reviewer it was benched after one
  cell (T5 worked), leaving 13 cells pending in `01a0f2a7`. They are resumable then, with one detail. **On 2026-10-09 the operator chose
  not to resume them;** the assessment went ahead without them.
  - **Why the detail is needed:** `resume` compares each reviewer's resolved references with those its settled cells
    were measured under. The Claude rows in this run were measured with `BENCH_CLAUDE_2284` pointing at the VS Code copy,
    which is now deleted, so with the pinned path the resume is refused.
  - **What to do:** for this one resume, set `BENCH_CLAUDE_2284` back to that old path string,
    `C:\Users\<user>\.vscode\extensions\anthropic.claude-code-2.1.284-win32-x64\resources\native-binary\claude.exe`.
  - **Why it is safe:** the Claude reviewers have no pending cell in this run, so nothing launches their CLI and only
    Astra's cells run.

## The ledger against the bill

The benchmark's cost figures are the product ledger's list-price estimates. OpenRouter's dashboard for the same key, as
of 2026-10-01, shows the actual bill:

| model | OpenRouter bill (total) | ledger estimate (total, all its cells) |
|---|---|---|
| Mistral Medium 3.5 (all three gates) | $13.39 | $12.48 |
| devstral-2512 | $0.53 | $0.87 |
| mistral-large-2512 | $0.22 | about $0.22 |
| codestral-2508 | $0.14 | $0.17 |

- **Medium 3.5 bills about 7 % more than the ledger.** Likely cause: tokens the ledger does not count, such as
  reasoning.
- **devstral bills about 39 % less.** Likely cause: a prompt-cache discount the row does not model (cached input was
  set at the input price).

So read the cost columns as close estimates, not invoices.

## The assessment (T6, 2026-10-09)

Astra (`codex-gpt-6-astra-exe`, `strict-v1`) read every valid finding of runs `01a0f2a7`, `01a0f2a8`, `01a0f2ea`,
`01a0f67a`, `01a0f74d` and `01a0f778` in one pass:
- 437 findings assessed, 0 assessment failures;
- 1 finding left unassessed: twice Astra wrote two seeds into one `seed_hit` string, and the pass refused that answer;
- 3 findings were Astra's own as a reviewer, counted apart.

Read with `bench gate report --gate feature --scope a797c446cdb4 --rubric strict-v1 --run <the six>`.

**Measured tasks.** The seeds, high-value and overstated columns are over VALID cells. "Valid %" counts every settled
cell, including the `TurnFailed` cells that the Claude Max window ended.

| reviewer | settled | valid % | seeds hit / run (of 2) | high-value / run | overstated % | findings / run | cost / run | s p50 |
|---|---|---|---|---|---|---|---|---|
| **Fable 5.1** | 28 | 42.9 | **1.83** | **2.08** | 4.2 | 4.93 | $13.05 (notional) | 1 044 † |
| **Opus 5.5** | 26 | 50 | **1.54** | 1.77 | 8.8 | 4.04 | $3.11 (notional) | 261 † |
| Astra | 1 | 100 | 2 (one cell) | 0 | 0 | 3 | subscription | 47 |
| devstral-2512 | 5 | 100 | 0.60 | 0.80 | 71.4 | 7.0 | $0.13 | 96 |
| mistral-large-2512 | 5 | 40 | 0.50 (two cells) | 0.50 | 0 | 1.6 | $0.05 | 249 |
| codestral-2508 | 5 | 100 | 0.20 | 0 | 50 | 1.6 | $0.03 | 13 |

† Valid cells only; the report's own median also counts the 3–5 s `TurnFailed` cells.

- **Fable and Opus are the strongest reviewers measured on this gate.**
  - Seeds: 1.83 and 1.54 per run, against grok-4.7's 1.47 and glm-5.3's 1.07 in the 2026-09-27 calibration.
  - Precision: the most high-value findings, and the least overstatement (4–9 %, against 22 % for grok and glm).
  - Caveat: that calibration ran on another harness and suite stamp, so the comparison is across scopes.
  - Cost: list-price only, and notional; the CLI runs on a Claude Max subscription. Its usage window is what cut these
    campaigns, and it bounds how often they can review.
- **devstral-2512 does not pay its way.** 0.60 seeds per run at 71 % overstatement: cheap, and mostly noise.
- **codestral-2508 is not a reviewer.** 0.20 seeds per run, with "no issues" on most tasks.
- **mistral-large-2512 stays dropped.** Its two valid cells are not a measurement.

## Nemotron 3 Ultra and 3 Super (2026-10-09)

**What ran:** run `01a1217f`, feature gate, 7 tasks × 1, on the same product build (`8b1c538643ca`), `bench`
`c0ef2f7`. The two models went through OpenRouter's **paid** endpoints, `nvidia/nemotron-3-ultra-550b-a55b` and
`nvidia/nemotron-3-super-120b-a12b`, with the api rows' transport: the vendor's default thinking and 8,192 output tokens.

**Why the paid endpoints:**
- **The free one was overloaded.** An earlier attempt on the `:free` endpoints (run `01a12171`) was stopped after its
  first cell. NVIDIA's free pool answered `provider_overloaded`.
- **That answer exposed a product defect.** OpenRouter delivered the overload as **HTTP 200 with an `error` object**,
  which coai read as an empty answer: lost, not retried. Filed as coai issue #721.
- **The operator approved the paid endpoints** for that reason, at about $0.70 for both models.

**How it was read: by hand, at the operator's choice, not by Astra.** Each finding was compared, task by task, with
the two planted defects in the suite's seed specs. A finding counts when it names the defect's mechanism in the seeded
file. This is not the strict rubric, so treat the rate as an order of magnitude beside the Astra-read rows.

| reviewer | valid | findings / run | seeds hit (of 14) | seeds / run, measured tasks | cost (7 runs) | s p50 |
|---|---|---|---|---|---|---|
| Nemotron 3 Ultra | 7 / 7 | 4.1 | **2** (both in rs3) | **0.40** | $0.67 | 50 |
| Nemotron 3 Super | 0 / 7 | — | — | — | $0.07 | 500 |

- **Ultra found both of rs3's planted defects** and nothing planted anywhere else.
  - It rated `engine_cache_dir` ignoring the shape as Blocking, exactly right.
  - It found the poisoned-ledger `snapshot()` returning `None`, but rated it only Minor.
  - Elsewhere it came close twice: it looked at `isTransientResolverError` in ts2, and mentioned the too-thin status
    in py3. Both times it argued a different point.
- **The rest of its findings are mostly minor or nit-level** (logging, validation, hard-coded paths). Some contradict
  themselves: one says a constant is "not in scope", then that it "works"; another says a navigation "is not relevant
  here".
- **0.40 seeds per run is Mistral Medium 3.5's level.** The calibration's API reviewers are at 0.80–1.47 on this
  gate.
- **Super produced no review.** Six cells were cut at the 8,192-token output cap (`LengthCut`): its reasoning used
  the whole budget before any answer. The seventh ended `EmptyContent`, with `finish=error` after 3,220 reasoning
  tokens. A larger output cap would change the transport the other rows were measured under, so it was not tried.

## What this does and does not show

- **Shown:**
  - Fable and Opus produce full feature reviews at 8–12 findings per run;
  - devstral and codestral run cleanly and cheaply through OpenRouter, and codestral rarely finds anything;
  - mistral-large cannot currently be measured on this key.
- **Not shown yet:** whether any of these findings hit the planted defects, or are right. That is the assessment's
  (T6), and then the hand-check's (T1).
