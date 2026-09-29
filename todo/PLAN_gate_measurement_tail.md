# PLAN — the gate benchmark's measurement tail: the hand-check and Fable's code gate

> Status: **plan only, 2026-09-29 — nothing done yet.** Extracted from
> [PLAN_coai_gate_model_benchmark.md](../research/PLAN_coai_gate_model_benchmark.md) when it was promoted (E1–E7 built and
> run). What is left is running and reading, not building. Nothing here is blocked by code. D4 of
> [PLAN_gate_reviewer_row_fidelity.md](PLAN_gate_reviewer_row_fidelity.md) (coai) is a preference: with it a
> reviewer's spend limit stops a lane at once instead of settling every remaining cell unmeasured.
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
  3 October. Their re-run waits on the accounts, as T2 and T4 do.

## 2. The items

| # | what | how | waits on |
|---|---|---|---|
| T1 | the hand-check of the S7.3 plan and code runs | per run, plan `01a0ec70-6e06-7194-9d73-bf893477ffec` then code `01a0ecad-dfa7-76ce-adc0-73e6682786ed`: `bench gate hand-check sample --run <run> --assessor codex-gpt-6-astra-exe --artifact-root <root> --db <bench>`; a person sets `agree` on each drawn row; then `bench gate hand-check record --file <the sample> --run <run> --assessor codex-gpt-6-astra-exe --artifact-root <root> --db <bench>` | a person's time; nothing in code |
| T2 | Fable's code gate: one full campaign of 21 cells | `bench gate run --gate code --suite-file suite-s73b.json --reviewers claude-fable-5-1-cli2284 --repeats 3 --coai-exe <coai-mcp at 3f351c05> --artifact-root <root> --db <bench> --parallel 2 --per-endpoint 2 --creds-key-from-coai-settings --prediction "…"`, with `BENCH_CLAUDE_2284` naming the Claude CLI 2.1.284; assess with `bench gate assess --run <it> --assessor codex-gpt-6-astra-exe …`; read with `bench gate report --gate code --scope gate-seeded#0d0da7662eab --rubric strict-v1 --run <it>`. The 6 lost plan cells need the same with `--gate plan` (21 cells; the matrix cannot run six) | Fable's monthly limit resetting |
| T3 | the seven account-out code cells of `01a0ecad` (grok php1/tsx2/ts2 r3; Astra php1/py3/tsx2/ts2 r3) | a campaign runs whole reviewers × tasks × repeats, never single cells, so the choice is one of two, recorded in RESULTS_gate_s73.md. (a) Accept: 13 and 11 of 15 measured cells stand, as the record already states. (b) Re-run both reviewers' full code matrix (`--reviewers grok-4-7-think,codex-gpt-6-astra-gates --repeats 3`, otherwise as T2, 42 cells) and report the new campaign alone with `--run` | the xAI balance (for b); Astra's Codex limit, 3 October |
| T4 | grok re-runs, if any are wanted (for example to fill T2's comparison) | as S7.3 | the xAI balance: $50 spent to $51.63 by 2026-09-29 |
| T5 | the lane stop — the bench half of D4: a cell whose reviewer is refused for a spend or usage limit is an environment failure, and a run of them stops the lane instead of settling every remaining cell unmeasured (44 Fable cells, 2026-09-29) | RED first over the product's reply once it carries the CLI's reason; the lane's breaker counts them | a coai release carrying D4 ([PLAN_gate_reviewer_row_fidelity.md](PLAN_gate_reviewer_row_fidelity.md)), built into the harness's product |

## 3. Build order

T2 and T3 each start as soon as their account is back, in either order (run, assess, report `--run`). D4, if it has
landed, makes them cheaper to fail and is not waited for. T1 last, so the hand-check covers their cells too; T1 can also
run at once over what exists. T4 only if asked.

## 4. Test plan

No code is built here. Every campaign is recorded with a `--prediction` before it runs, and read with `bench gate report
--run` so a voided campaign never enters a table. Every result goes into the results record in `research/`, next to the
figures it changes.

## 5. Definition of Done

- [ ] T1: the S7.3 scopes' strict rates are printed (hand-checked), and RESULTS_gate_s73.md states them.
- [ ] T2: Fable has 21 code cells on the S7.3 suite, and they are assessed and recorded.
- [ ] T3: choice (a) or (b) made and recorded in RESULTS_gate_s73.md, with the date.
- [ ] T5: a limit-refused reviewer stops its lane; the RED test watched failing first.
- [ ] This plan is promoted, or its remaining items are said and dated.
