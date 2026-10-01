# PLAN — the gate benchmark's measurement tail: the hand-check and the lane stop

> Status: **open, 2026-10-01 — T1 and T6 remain** (T6: the feature runs of 2026-09-30/10-01 await Astra, back 2026-10-06). T5 was built and checked on a real product on 2026-09-30; T2–T4 were
> closed without re-runs (the operator's decision, 2026-09-29).**
> Extracted from [PLAN_coai_gate_model_benchmark.md](../research/PLAN_coai_gate_model_benchmark.md) when it was promoted
> (E1–E7 built and run).
> - T1 is reading, not building.
> - T5 is the bench half of D4 of
>   [PLAN_gate_reviewer_row_fidelity.md](../research/PLAN_gate_reviewer_row_fidelity.md). It was built against the
>   reply shape #622 produces (§6, PR #59) and checked on coai mcp 0.40.4, released 2026-09-30: run `01a0f1b7`, see
>   [RESULTS_gate_mistral.md](../research/RESULTS_gate_mistral.md).
>
> Related docs: [RESULTS_gate_s73.md](../research/RESULTS_gate_s73.md), [RESULTS_gate_aa_cs2.md](../research/RESULTS_gate_aa_cs2.md),
> [module_gate.md](../research/module_gate.md).

## 1. The goal

The S7.3 record states counts, validity, cost, time and agreement. It states **no supported-rate**, because the strict
rates are withheld until a hand-check covers them (E4). Two of its five reviewers are incomplete:

- Fable 5.1 reached the model in 15 of the plan campaign's 21 cells before its monthly spend limit. The code gate needs
  one campaign of 21 cells (7 tasks × 3 repeats). Two were started: `01a0edf2` reached the model in none of its 21, and
  the re-run `01a0ee1f` in 4 of its 21.
- Seven code cells (grok 3, Astra 4) never reached `review_code`. That is answered, not open: the reviewers' accounts
  ran out. grok's key was refused with HTTP 403 once its balance was spent; Astra hit its Codex usage limit, until
  3 October.

**Neither gap is re-run (the operator, 2026-09-29).** Fable's cells and the seven account-out cells stay as
RESULTS_gate_s73.md records them. What remains is the hand-check over what exists. The lane stop, so that a future campaign
which meets a spend limit stops instead of burning its cells, was built on 2026-09-30 (T5).

## 2. The items

| # | what | how | waits on |
|---|---|---|---|
| T1 | the hand-check of the S7.3 plan and code runs | per run, plan `01a0ec70-6e06-7194-9d73-bf893477ffec` then code `01a0ecad-dfa7-76ce-adc0-73e6682786ed`: `bench gate hand-check sample --run <run> --assessor codex-gpt-6-astra-exe --artifact-root <root> --db <bench>`; a person sets `agree` on each drawn row; then `bench gate hand-check record --file <the sample> --run <run> --assessor codex-gpt-6-astra-exe --artifact-root <root> --db <bench>` | a person's time; nothing in code |
| T2 | **closed, not run (2026-09-29).** Fable's code gate: one full campaign of 21 cells | `bench gate run --gate code --suite-file suite-s73b.json --reviewers claude-fable-5-1-cli2284 --repeats 3 --coai-exe <coai-mcp at 3f351c05> --artifact-root <root> --db <bench> --parallel 2 --per-endpoint 2 --creds-key-from-coai-settings --prediction "…"`, with `BENCH_CLAUDE_2284` naming the Claude CLI 2.1.284; assess with `bench gate assess --run <it> --assessor codex-gpt-6-astra-exe …`; read with `bench gate report --gate code --scope gate-seeded#0d0da7662eab --rubric strict-v1 --run <it>`. The 6 lost plan cells need the same with `--gate plan` (21 cells; the matrix cannot run six) | Fable's monthly limit resetting |
| T3 | **decided (2026-09-29): (a), accept.** The seven account-out code cells of `01a0ecad` (grok php1/tsx2/ts2 r3; Astra php1/py3/tsx2/ts2 r3) | a campaign runs whole reviewers × tasks × repeats, never single cells, so the choice is one of two, recorded in RESULTS_gate_s73.md. (a) Accept: 13 and 11 of 15 measured cells stand, as the record already states. (b) Re-run both reviewers' full code matrix (`--reviewers grok-4-7-think,codex-gpt-6-astra-gates --repeats 3`, otherwise as T2, 42 cells) and report the new campaign alone with `--run` | nothing — closed; (b) would have waited on the xAI balance and Astra's Codex limit |
| T4 | **closed, not run (2026-09-29).** grok re-runs, if any are wanted (for example to fill T2's comparison) | as S7.3 | the xAI balance: $50 spent to $51.63 by 2026-09-29 |
| T5 | the lane stop — the bench half of D4: a cell whose reviewer is refused for a spend or usage limit is an environment failure, and a run of them stops the lane instead of settling every remaining cell unmeasured (44 Fable cells, 2026-09-29) | RED first over the product's reply once it carries the CLI's reason; the lane's breaker counts them | a coai release carrying D4 ([PLAN_gate_reviewer_row_fidelity.md](../research/PLAN_gate_reviewer_row_fidelity.md)), built into the harness's product |
| T6 | **the feature gate on the S7.3 suite, assessment** — Fable, Opus, Astra, devstral-2512 and codestral-2508 ran 2026-09-30/10-01 ([RESULTS_gate_feature_s73.md](../research/RESULTS_gate_feature_s73.md)); seeds, high-value and overstatement need the assessor | `bench gate resume --run 01a0f2a7…` for Astra's 13 pending reviewer cells, with `BENCH_CLAUDE_2284` set to the old VS Code path STRING for that resume only (the Claude rows' references must match their settled cells; see the record), then `bench gate assess --assessor codex-gpt-6-astra-exe` over runs `01a0f2a7`, `01a0f2a8`, `01a0f2ea`, `01a0f67a`, `01a0f74d`, `01a0f778` (and `01a0f28e`, Mistral Medium's); then the record and RESULTS_gate_model_choice.md | Astra's Codex usage limit, until 2026-10-06 |

## 3. Build order

T1 and T5 are independent, and either can go first. Owners:

| item | owner | what the owner carries |
|---|---|---|
| T1 | **the operator** — the hand-check needs a person's judgement, which is why E4 deferred it | draw the samples, set `agree`, record them, and add the strict rates to RESULTS_gate_s73.md (an agent may run the commands and write the record once the rows are set) |
| T5 | **the next agent session on this repository** | **done 2026-09-30:** built (§6, #59) and checked on coai mcp 0.40.4 (run `01a0f1b7`) |

- **T1** runs now over the two S7.3 campaigns as they stand; no re-run will add cells to it.
- **T5** is built against the reply shape #622 produces. What is left is the check on a released product.

T2–T4 are closed (§1).

## 4. Test plan

T1 builds no code. T5 starts with a RED test over the product's limit reply, watched failing before the breaker
counts it; the whole suite runs after it. Every campaign is recorded with a `--prediction` before it runs, and read with `bench gate report
--run` so a voided campaign never enters a table. Every result goes into the results record in `research/`, next to the
figures it changes.

## 5. Definition of Done

- [ ] T1: the S7.3 scopes' strict rates are printed (hand-checked), and RESULTS_gate_s73.md states them.
- [x] T2: closed without a run (the operator, 2026-09-29); RESULTS_gate_s73.md records Fable's code gate as four cells.
- [x] T3: choice (a) made and recorded in RESULTS_gate_s73.md, 2026-09-29.
- [x] T4: closed without a run (the operator, 2026-09-29).
- [x] T5: a limit-refused reviewer is benched, its cells stay pending, and the campaign ends `AccountOut`, exit 3. Every
  test was watched RED first. The order test and the CLI test were proved by reverting the fix. 2026-09-30.
- [x] T5's check on a real product, 2026-09-30. Run `01a0f1b7`, on coai mcp 0.40.4 with `--allow-product-change`.
  **Deviation:** Fable's limit had reset by then, so the account used was grok's spent xAI key. The reply was
  `the API refused the key … (HTTP 403)`; the first cell was refused as account-out and grok was benched. The campaign
  ended `AccountOut` with exit 3: 0 settled, 7 pending.
- [ ] T6: the feature runs of 2026-09-30/10-01 are assessed by Astra and recorded, and RESULTS_gate_model_choice.md gains their rows.
- [ ] This plan is promoted, or its remaining items are said and dated.

## 6. T5 — the design (2026-09-30)

### 6.1 The symptom, from the stored cells

A cell whose reviewer's account has run out is recorded today as a **measurement**. For example, cell `01a0ee1f…a6b988`
settled `Completed`, verdict `Unknown`, `VerdictNotPassing`: *"the plan loop never passed in 2 round(s)"*. Its every
stage reply carried the real cause in the product's `reviewers` line. The three shapes seen on 2026-09-29, one per
account:

| reviewer | the `reviewers` line (after `0 of 1 reviewers answered; failed: <id>/<role>: `) |
|---|---|
| Astra (Codex CLI) | `rate limited (after 1 attempt): {"type":"error","message":"You've hit your usage limit. … try again at Oct 3rd …` |
| grok (api) | `exit 77: [coai-mcp] the API refused the key for vendor 'grok-4-7-think' (HTTP 403) - check the vault entry …` |
| Fable (Claude CLI), after coai #622 | `exit 1: You've hit your monthly spend limit. Switch to another model to continue. (HTTP 429)` |

Before #622, Fable's line read `exit 1 (the CLI said nothing on stderr)`. That carries no cause, and T5 does not guess
one from it.

A completed cell resets the lane breaker. `LegDrain`'s tally sets `Consecutive = 0` on a scored leg
(`src/Bench.Application/LegDrain.cs:208`), and `GateCampaign.LegAsync` turns every settled cell into a scored leg
(`src/Bench.Application/Gate/GateCampaign.cs:116`). So the breaker never saw the 44 Fable cells.

### 6.2 What changes

1. **Recognise it (Domain, pure).** A new `ReviewerAccountOut.Reason(replyJson)` in `src/Bench.Domain/Gate/` reads a
   reply's `reviewers` string. It returns the failure text when **no reviewer answered** and the failure names an
   account running out; otherwise it returns empty (no null).
   - **The markers**, case-insensitive: `spend limit`, `usage limit`, `credit balance`, `insufficient credit`,
     `insufficient_quota`, `(HTTP 402)`, and `refused the key`. `billing` alone is not a marker, because too many
     unrelated errors mention it (plan round, 2026-09-30).
   - **What does not count:** a plain `rate limited` with none of those words. That is transient, and the product
     already retries it.
   - **Why a refused key counts, spent or wrong alike:** either way it is an environment failure, not a reviewer's
     result, and neither fixes itself mid-campaign. grok's spent balance arrived exactly as one. A bare
     `connection refused` is not `refused the key`.
   - **The helper:** `GateReplyParser`'s JSON helper (`src/Bench.Domain/Gate/GateReplies.cs:94`) is reused, not
     copied.
2. **Do not settle it.** In `GateCellRunner.DriveAsync` (`src/Bench.Application/Gate/GateCellRunner.cs:219`), the
   runner checks each stage reply of the scrubbed run before anything else. If any stage reply is account-out, the
   runner **does not settle and does not requeue**. It returns the refusal with the cell still claimed by its lane.
   `GateCampaign.LegAsync` then benches the reviewer under the pool's lock, and only after that requeues the cell.
   **Why in that order (consultation, 2026-09-30):** a requeue committed before the bench leaves a window in which
   another lane with endpoint room re-claims the fresh `Pending` cell, and one campaign could burn attempt after
   attempt. After the requeue, the cell is **requeued unmeasured**:
   - Its state goes back to `Pending` and its attempt stays counted, so the next claim gets a fresh
     `attempt-N+1` directory. `BeginAttemptAsync` refuses an existing one
     (`src/Bench.Infrastructure/Gate/FileSystemGateArtifactStore.cs:53`).
   - It is never abandoned: an empty account is not the cell's fault.
   - Its cause is recorded on the cell, and the attempt's files stay on disk. The stage replies are not written,
     because completion is skipped. The product's own `stderr.txt` in that attempt directory keeps the full
     `reviewers … failed: …` line (checked on cell `01a0ee1f…a6b988`, lines 8–10), so the diagnosis survives a later
     settle overwriting the cell's cause.
   - **The lane breaker still counts the refusal.** The leg produced nothing, and `LegDrain` counts that. Benching
     bounds it: each out reviewer contributes at most `--per-endpoint` refusals per campaign, against a budget of 20
     by default, so the reviewers still answering keep running.
   - The leg is returned as a refusal whose text is the fixed marker `ReviewerAccountOut.Marker`, then the failure
     text: `<marker>: <reason>`. This follows how `ClaimRefusal.NoPendingCell` is read (`GateCampaign.cs:103`), and
     it is how the failure text reaches the campaign's report.
   - **Each resume against a still-empty account** costs at most `--per-endpoint` attempts per such reviewer before
     it is benched again. Each attempt keeps its own small attempt directory, as a swept attempt does. Resuming is the
     operator's decision, so no ceiling is added.
   - **The store method:** `IGateStore.RequeueUnmeasuredAsync(cellId, owner, attempt, cause, ct)`, beside
     `HandBackUnmeasuredAsync` (`src/Bench.Application/Gate/GateStore.cs:48`). Both share one private guarded UPDATE
     in `PostgresGateStore` (`src/Bench.Infrastructure/Persistence/PostgresGateStore.cs:234`) that differs only in
     whether the attempt is given back. The test decorator `ForwardingGateStore`
     (`tests/Bench.Tests/Infrastructure/GateCellCompletionTests.cs:153`) forwards it.
3. **Stop claiming that reviewer.** When `LegAsync` sees the marker, it benches the reviewer in the campaign's
   `EndpointPool`. The pool's free set (`GateCampaign.cs:251`) skips benched reviewers, so no lane claims their cells
   again. **Other reviewers keep running.**
   - A lane's claim returns "no pending cell" in exactly two cases:
     - every reviewer of the campaign is benched;
     - the unbenched reviewers have nothing pending and no cell is in flight.

     Otherwise it waits for a release as today, including when an unbenched reviewer's endpoint is merely full.
   - A benched reviewer's in-flight cells still settle or requeue on their own.
4. **Say so, and leave the run resumable.** `GateCampaign.Report` (`GateCampaign.cs:202`) reports a new
   `CampaignStop.AccountOut` whenever a reviewer was benched. The reason names each benched reviewer and its failure
   text. How many of its cells are still pending is printed by `FinishAsync`, which already reads the run's cells.
   - `GateRunCommand.FinishAsync` (`hosts/Cli/GateRunCommand.cs:247`) does not mark the run Finished, since cells are
     pending. It exits with `ExitCodes.Environment` and a sentence that names
     `bench gate resume --run <id>` for when the account is back.
   - `FailureKind` (`src/Bench.Domain/Gate/GateRunFacts.cs:84`) is **not** extended. Nothing is settled, so no kind is
     needed.

### 6.3 Tests (RED first, each watched failing)

- **Domain.** `ReviewerAccountOut` over the three real lines above: each returns its reason. It returns empty for:
  - `1 of 1 reviewers answered`;
  - a plain `rate limited (after 3 attempts): Too many requests`;
  - Fable's pre-#622 `exit 1 (the CLI said nothing on stderr)`;
  - `exit 1: connection refused`;
  - a failure that only mentions billing;
  - a failure while another reviewer answered;
  - non-JSON text.
- **Campaign**, against the fake product and the Postgres fixture. The fake gains an `accountOut` switch: vendor id →
  failure sentence. A listed reviewer's `review_*` answers with that sentence as `0 of 1 reviewers answered; failed: …`
  (`tests/FakeCoai/Program.cs:193`).
  - **RED:** two reviewers, one out of account, several repeats each. Today every cell settles and the report says
    Drained.
  - **GREEN:** the out reviewer has at most `--per-endpoint` requeued attempts, and every other cell of it is still
    `Pending`. The other reviewer's cells all settle. The report is `AccountOut` and names the out reviewer. No cell of
    the out reviewer is `Settled`.
  - **No out cell reaches attempt 2 within one campaign.** Two lanes and room on the endpoint, which is the window
    the bench-before-requeue order closes.
  - **A resume after the account is back** (the switch removed) settles the requeued cells at attempt 2, in fresh
    directories.
- **CLI.** `FinishAsync` maps `AccountOut` to exit 3 (`ExitCodes.Environment`, `hosts/Cli/ExitCodes.cs:21`) and
  leaves the run `Running`.
- **Whole suite.** Run it after the change.

### 6.4 Build order

The fake's `accountOut` switch and the RED campaign test come FIRST, watched failing, then:

1. the domain recogniser (its tests first);
2. the store method (Postgres test first);
3. the runner's refusal;
4. the pool bench, then the requeue, then the campaign stop;
5. the CLI exit;
6. the docs.

The docs are `research/module_gate.md`, with the requeue and the new stop, and this plan's §5 tick.

The real-world check, a Fable-limit cell on a product carrying #622, waits on the mcp release. It is recorded as open
if the release is not cut.

### 6.5 As built (2026-09-30)

Built as designed, with the consultation's order: the runner leaves the cell claimed, and the campaign benches, then
requeues.

**The gate:**
- **Plan round:** `proceed`, 6 findings. Three were accepted: the failure text now travels in the refusal, the
  resume's cost is stated, and `billing` was dropped as a marker. Three were rejected, each with the code.
- **Cadence consultation:** solved.
- **Code round:** `proceed`, 11 findings from 8 reviewers. **One was accepted:** the bench is keyed by
  `GateReviewerId`, not a string. **Ten were rejected**, each with the code that answers it:
  - lanes that would hang: the existing `busy` wait covers it;
  - a report that would mask the account-out: the `pending` line prints for every stop;
  - stale pending counts: `FinishAsync` re-reads the cells after the campaign;
  - a failed requeue: every run/resume sweeps first;
  - the string marker: the same idiom as `NoPendingCell`, with a round-trip test;
  - five others.

**Proof by mutation, after GREEN:**
- With the requeue moved ahead of the bench, and the window held open for 3 s, the order test fails: a cell reaches
  attempt 2.
- Without the CLI's `AccountOut` mapping, the CLI test fails: exit 5, not 3.

The first version of the order test ran one reviewer on three lanes. It did **not** catch that mutant, because all
three lanes sat in their own sessions during the window, so it was rebuilt around a second reviewer whose lane is free
when the window opens.

**Found, not changed:** `EndpointPool.ClaimAsync` was already above the complexity limit of 4 before this change, and
it now has one more branch (the every-reviewer-benched exit). Recorded for the operator; it was not refactored
uninvited.
