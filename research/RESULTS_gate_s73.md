# RESULTS — E7 S7.3: the plan and code gates over the seven tasks, four reviewers and Fable

> Status: **measured, 2026-09-29.** The first native C# campaign over all seven seeded tasks on the plan and code gates:
> grok-4.7, glm-5.3, Astra (gpt-6-astra via the Codex CLI) and Opus 5.5 (Claude CLI 2.1.284), 3 repeats each, on
> product coai `3f351c05` (mcp 0.40.2). Every finding was read blind by Astra under `strict-v1`. Fable 5.1 was run
> afterwards on the same inputs (§ Fable). **Strict percentages stay "not hand-checked"** — the hand-check is deferred by
> the operator (E4) — so the rates below are counts, validity, cost, time and agreement, not a supported-rate ranking.
>
> Mistral Medium 3.5 was run later on the same inputs: [RESULTS_gate_mistral.md](RESULTS_gate_mistral.md).
>
> Related: [PLAN_coai_gate_model_benchmark.md](PLAN_coai_gate_model_benchmark.md) (E7, *S7.3 as run so far*),
> [RESULTS_gate_aa_cs2.md](RESULTS_gate_aa_cs2.md) (the A/A that made these cells comparable with the calibration's),
> [module_gate.md](module_gate.md).

## What was held fixed

| variable | value |
|---|---|
| suite | `gate-seeded#0d0da7662eab` — the calibration's seven tasks, with ts2 declared without its rules submodule (its url no longer resolves; the calibration measured it that way, #51) and tsx2's and php1's synthetic plans carried in the suite (the calibration wrote them beside its checkout, #52) |
| product | coai `3f351c05`, binary `44467c1c14a5`, 0 dirty files, the same pin across every cell |
| harness | `bench` `15485a1` (plan run) and `cb6c09b` (code run — #53's row fix) |
| runs | plan `01a0ec70`, code `01a0ecad` — 7 tasks × 4 reviewers × 3 repeats = 84 cells each, isolated data directories |
| reviewers | `grok-4-7-think` (xai, medium, thinking on), `glm-5-3-think` (dashscope token plan, high, thinking on), `codex-gpt-6-astra-gates`, `claude-opus-5-5-cli2284` |
| assessor | `codex-gpt-6-astra-exe`, `strict-v1`; Astra's own findings, read by its own family, are counted apart (all 21 of its cells: 50 on plan, 117 on code) |
| reading | `bench gate report --gate … --scope gate-seeded#0d0da7662eab --rubric strict-v1 --run <campaign>` (#55) — the code scope also holds the voided run `01a0ec99`, left out |

Two tasks, js3 and ts2, are calibration tasks and are reported apart by the report; the tables below are the five
**measured** tasks (15 runs per reviewer).

## Plan gate (`01a0ec70`)

| reviewer | valid % | findings / run | high-value / run | overstated % | s p50 | tokens in / run | cost / run |
|---|---|---|---|---|---|---|---|
| grok-4.7 | 100 | 4.33 | 0.67 | 63.9 | 134 | 18 k | $0.085 |
| glm-5.3 | 100 | 5.53 | 0.67 | 42.5 | 176 | 16 k | **$0.058** |
| Astra | 100 | 2.00 | 0.60 | 41.2 | **20** | 31 k | subscription |
| Opus 5.5 | 93.3 | **7.73** | **1.53** | 41.6 | 76 | 85 k | $0.47 * |

**Seeds hit is ~0 for every reviewer, by construction:** the seeds are code defects planted in the variant diff, and the
plan gate reads only the plan text. The prediction written for this run ("grok hits more seeds than glm") was therefore
ill-posed for this gate — recorded as such, not as a result. The invalid cells (8 of 84) are all `GoodEnough`: the
product's round budget ran out and it stopped the loop itself. One is on a measured task, Opus py3/r2, which is the one
invalid cell behind Opus's 93.3 %. The other seven are on calibration tasks: Opus js3/r3 and ts2 r1–r3, and grok ts2 r1–r3.

## Code gate (`01a0ecad`)

| reviewer | valid % | findings / run | **seeds hit / run** (of 2) | high-value / run | overstated % | s p50 | tokens in / run | cost / run | cost / seed |
|---|---|---|---|---|---|---|---|---|---|
| grok-4.7 | 86.7 | 12.07 | **1.08** | 4.38 | 49.3 | 691 | 261 k | $1.06 | $0.98 |
| glm-5.3 | **100** | 15.67 | 0.93 | 3.87 | 39.4 | 406 | 236 k | **$0.53** | **$0.57** |
| Astra | 73.3 | 5.33 | 0.75 | 3.42 | 39.4 | **43** | 290 k | subscription | — |
| Opus 5.5 | 46.7 | **27.33** | 1.07 | **7.67** | 43.0 | 281 | 800 k | $4.50 * | $4.22 |

\* the Claude CLI's own reported figure at list price, not an invoice.

Why cells were invalid: 22 of 84. The valid % above counts the **measured** cells only (15 per reviewer), so each cell is
listed with its scope:

| reviewer | measured tasks (in the table) | calibration tasks (js3, ts2 — reported apart) |
|---|---|---|
| Opus 5.5 | 8 `GoodEnough`: cs2 r1–r3, py3 r3, rs3 r1 and r3, tsx2 r2–r3 | 3 `GoodEnough`: ts2 r1–r3 |
| Astra | 1 `GoodEnough` (cs2 r3); 3 **account out** (php1, py3, tsx2 — all r3) | 1 account out (ts2 r3), 1 usage limit on a later turn (js3 r3) |
| grok-4.7 | 2 **account out** (php1, tsx2 — both r3) | 1 `GoodEnough` (ts2 r1), 1 account out (ts2 r3) |
| glm-5.3 | — | 1 `GoodEnough` (ts2 r2) |

Opus's `GoodEnough` cells are its finding volume meeting the product's round budget: about 27 findings a review, and the
loop is stopped before it passes.

The **account out** cells are the reviewer's account running dry near the end of the run. All of them are repeat 3, the
last cells to run. Their plan round had no answer (`Unknown`), so `review_code` never ran:

- **grok:** *"the API refused the key … (HTTP 403)"*. The xAI balance was spent ($51.63 against the $50 on it).
- **Astra:** *"You've hit your usage limit … try again at Oct 3rd"*, the Codex subscription.

So none of them is a reading of the model's, and none is a harness defect. The product reports them as a round with no
answer, rather than as the reviewer being unavailable.

## Agreement — how often two reviewers raise the same issue

"The same issue" is the assessor's cluster within a task (it reads all of a task's findings, and carries its clusters from
batch to batch); valid cells only.

| | plan gate | code gate |
|---|---|---|
| distinct issues | 140 | 224 |
| raised by one reviewer only | 84 (60 %) | 103 (46 %) |
| by two / three / all four | 29 / 21 / 6 | 71 / 34 / 16 |

| reviewer | plan: issues (only it) | code: issues (only it) |
|---|---|---|
| glm-5.3 | 90 (44) | 134 (39) |
| Opus 5.5 | 67 (23) | 109 (24) |
| grok-4.7 | 51 (12) | 117 (30) |
| Astra | 21 (5) | 51 (10) |

Pairwise, shared issues over issues either raised: plan — grok & Opus 34 %, glm & Opus 29 %, glm & grok 28 %, Astra & any
12–16 %; code — glm & grok 36 %, glm & Opus 35 %, grok & Opus 31 %, Astra & any 19–23 %. **The reviewers mostly complement
each other**, more so on plans than on code: on a plan six issues in ten come from one reviewer alone.

## What it cost

grok's week (the xAI console, 23–29 September): **$51.63**, 20.0 M tokens, 1 121 requests — the operator's whole $50
balance. Per review in this campaign a code review costs grok ~$1.06 and a plan review ~$0.09; glm ~$0.53 and ~$0.06. The
CLI reviewers' figures are list-price estimates on subscriptions; Astra's Codex subscription hit its usage limit once
during the code run.

## Fable 5.1

Run after the four, on the same suite, product and harness (`claude-fable-5-1-cli2284`, the Claude CLI 2.1.284).
**Fable's month ran out mid-campaign.** From 16:12 UTC on 2026-09-29 every call exited in about 3 s, and the ledger read
*"exit 1 (the CLI said nothing on stderr)"*. A direct call of the same CLI later gave the real reason: *"You've hit your
monthly spend limit"* (HTTP 429). coai's Claude adapter dropped that reason (D4 of the fidelity plan, fixed since in coai #622), so each doomed
cell ran its plan loop to `Unknown` instead of stopping the campaign.

| gate | run | cells that reached the model | valid | findings / run | s p50 | cost / run * |
|---|---|---|---|---|---|---|
| plan | `01a0ed5f` | 15 of 21 (repeat 3 of six tasks lost to the limit) | 6 (the rest `GoodEnough`) | 7.6 (measured tasks) | 269 | $2.03 |
| code | `01a0edf2` | **0 of 21**: the limit, before any review | — | — | — | — |
| code | `01a0ee1f` (re-run) | 4 of 21, then the limit again | 1 (3 `GoodEnough`) | ~29 | 744–1 468 | $11.24–20.22 |

On the **plan gate**, where 15 cells ran, Fable sits beside Opus. Per run on the measured tasks: 7.6 findings (Opus 7.73)
and 1.36 high-value (Opus 1.53), at about four times Opus's list cost and three and a half times its time. Its issues
overlap Opus's most. Over the plan run with all five reviewers there are 150 distinct issues: 86 raised by one reviewer,
3 by all five. Fable raised 39 on its valid cells, 10 of them alone. Its shared share with each other reviewer is Opus 31 %,
grok 25 %, glm 15 % and Astra 13 %.

On the **code gate** four cells are not a measurement, and no rate is stated. For the record: cs2, js3, rs3 and ts2,
repeat 1, about 29 findings each, 6 of 8 seeds hit, and 53 of 117 findings read `supported` (not hand-checked). That is
the heaviest reviewer of the five by far: 0.5–1 M tokens in per review.

## Harness defects S7.3 found — each fixed before its numbers were used

| PR | defect | how it showed |
|---|---|---|
| #51 | a submodule whose url no longer resolves made every cell of its task refuse | ts2: 20 refusals in a row stopped the first campaigns |
| #52 | a plan the variant head does not commit could not be given | tsx2, php1: "the plan is not in the checkout"; S7.1's record had wrongly said `suite verify` proved every plan committed |
| #53 | a code-gate row was ticked for `code` only, while its protocol runs the plan loop first | all 84 code cells of `01a0ec99` refused before any model call; E3's fake product answered unticked stages, so no test saw it |
| #54 | a sweep ignored a dead owner's claim stamped in the future | a reboot stepped the clock back an hour mid-run; four claims stranded until #54 |
| #55 | a scope-wide report counted a voided campaign with the real one | the code table read "valid 23–50 %, s p50 0.1" until narrowed with `--run` |

## What this does and does not show

- **Shown:** on the code gate grok-4.7 and Opus 5.5 hit the most seeds per run (1.08, 1.07 of 2), glm-5.3 the cheapest seed
  ($0.57) at full validity, Astra the fastest (43 s) with the fewest findings; Opus raises by far the most findings and
  high-value findings per run, at ~4× grok's cost, and half its code cells hit the product's round budget. The four
  reviewers agree on a minority of issues.
- **Not shown:** which reviewer's findings are more often RIGHT — the strict rates are withheld until the hand-check; any
  ranking by quality waits for it. Nor the feature gate at this scale for the CLI reviewers.
- **Accounts ran out at the end.** grok's xAI balance, Astra's Codex subscription (until 3 October) and Fable's monthly
  spend limit all ran out on 2026-09-29.
- **No re-runs — the operator's decision, 2026-09-29.** The figures above are final as they stand:
  - The seven account-out code cells stay unmeasured, as the tables record them. This is choice (a) of the tail
    plan's T3.
  - Fable's code gate stays at the four cells above, and its plan gate at 15 of 21.
  - No grok re-runs.
- **Done since:** the fidelity plan
  ([PLAN_gate_reviewer_row_fidelity.md](PLAN_gate_reviewer_row_fidelity.md), IMPLEMENTED 2026-09-29). Its D4, coai
  #622, keeps the Claude CLI's reason, so a spend limit now reads as one.
- **Done since, 2026-09-30:**
  - the lane stop (T5, #59): a reviewer whose account ran out is benched, not measured;
  - Mistral Medium 3.5 on the same inputs ([RESULTS_gate_mistral.md](RESULTS_gate_mistral.md)).
- **Open**, in [PLAN_gate_measurement_tail.md](../todo/PLAN_gate_measurement_tail.md): the hand-check (T1).
