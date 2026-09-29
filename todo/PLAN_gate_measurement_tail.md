# PLAN — the gate benchmark's measurement tail: the hand-check and the lane stop

> Status: **open, 2026-09-29 — T1 and T5 remain; T2–T4 closed without re-runs (the operator's decision, 2026-09-29).**
> Extracted from [PLAN_coai_gate_model_benchmark.md](../research/PLAN_coai_gate_model_benchmark.md) when it was promoted
> (E1–E7 built and run).
> - T1 is reading, not building.
> - T5 is the bench half of D4 of
>   [PLAN_gate_reviewer_row_fidelity.md](../research/PLAN_gate_reviewer_row_fidelity.md). D4 is merged in coai as #622,
>   but no coai release carries it yet: mcp-v0.40.3 is the latest, and it predates #622.
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
RESULTS_gate_s73.md records them. What remains is the hand-check over what exists, and the lane stop, so that a future
campaign which meets a spend limit stops instead of burning its cells.

## 2. The items

| # | what | how | waits on |
|---|---|---|---|
| T1 | the hand-check of the S7.3 plan and code runs | per run, plan `01a0ec70-6e06-7194-9d73-bf893477ffec` then code `01a0ecad-dfa7-76ce-adc0-73e6682786ed`: `bench gate hand-check sample --run <run> --assessor codex-gpt-6-astra-exe --artifact-root <root> --db <bench>`; a person sets `agree` on each drawn row; then `bench gate hand-check record --file <the sample> --run <run> --assessor codex-gpt-6-astra-exe --artifact-root <root> --db <bench>` | a person's time; nothing in code |
| T2 | **closed, not run (2026-09-29).** Fable's code gate: one full campaign of 21 cells | `bench gate run --gate code --suite-file suite-s73b.json --reviewers claude-fable-5-1-cli2284 --repeats 3 --coai-exe <coai-mcp at 3f351c05> --artifact-root <root> --db <bench> --parallel 2 --per-endpoint 2 --creds-key-from-coai-settings --prediction "…"`, with `BENCH_CLAUDE_2284` naming the Claude CLI 2.1.284; assess with `bench gate assess --run <it> --assessor codex-gpt-6-astra-exe …`; read with `bench gate report --gate code --scope gate-seeded#0d0da7662eab --rubric strict-v1 --run <it>`. The 6 lost plan cells need the same with `--gate plan` (21 cells; the matrix cannot run six) | Fable's monthly limit resetting |
| T3 | **decided (2026-09-29): (a), accept.** The seven account-out code cells of `01a0ecad` (grok php1/tsx2/ts2 r3; Astra php1/py3/tsx2/ts2 r3) | a campaign runs whole reviewers × tasks × repeats, never single cells, so the choice is one of two, recorded in RESULTS_gate_s73.md. (a) Accept: 13 and 11 of 15 measured cells stand, as the record already states. (b) Re-run both reviewers' full code matrix (`--reviewers grok-4-7-think,codex-gpt-6-astra-gates --repeats 3`, otherwise as T2, 42 cells) and report the new campaign alone with `--run` | nothing — closed; (b) would have waited on the xAI balance and Astra's Codex limit |
| T4 | **closed, not run (2026-09-29).** grok re-runs, if any are wanted (for example to fill T2's comparison) | as S7.3 | the xAI balance: $50 spent to $51.63 by 2026-09-29 |
| T5 | the lane stop — the bench half of D4: a cell whose reviewer is refused for a spend or usage limit is an environment failure, and a run of them stops the lane instead of settling every remaining cell unmeasured (44 Fable cells, 2026-09-29) | RED first over the product's reply once it carries the CLI's reason; the lane's breaker counts them | a coai release carrying D4 ([PLAN_gate_reviewer_row_fidelity.md](../research/PLAN_gate_reviewer_row_fidelity.md)), built into the harness's product |

## 3. Build order

T1 and T5 are independent, and either can go first. Owners:

| item | owner | what the owner carries |
|---|---|---|
| T1 | **the operator** — the hand-check needs a person's judgement, which is why E4 deferred it | draw the samples, set `agree`, record them, and add the strict rates to RESULTS_gate_s73.md (an agent may run the commands and write the record once the rows are set) |
| T5 | **the next agent session on this repository** | cut the coai release that carries #622 (coai's own release process), rebuild the harness's product on it, then the RED test and the breaker |

- **T1** runs now over the two S7.3 campaigns as they stand; no re-run will add cells to it.
- **T5** starts once a coai release carries #622 and the harness's product is rebuilt on it: RED over that reply first,
  then the breaker.

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
- [ ] T5: a limit-refused reviewer stops its lane; the RED test watched failing first.
- [ ] This plan is promoted, or its remaining items are said and dated.
