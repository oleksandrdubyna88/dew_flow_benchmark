# RESULTS — E7's A/A: the C# driver against the Python calibration on cs2

> Status: **measured, 2026-09-28.** This was the first time the C# gate driver (`bench gate run`, E3) re-ran a task the
> Python calibration had measured. It was compared by the turn-1 prompt's normalised shape (`bench gate aa`, E7 S7.2a),
> with seeds hit read blind under `strict-v1` (`bench gate assess`, E4). **The A/A passes, but only after a fix.** The
> first comparison found the driver handing the product a different tree. PR #49 fixed it, and the re-run matches the
> calibration byte for byte after normalisation.
>
> Related: [PLAN_coai_gate_model_benchmark.md](PLAN_coai_gate_model_benchmark.md) (E7, S7.1–S7.2a),
> [module_gate.md](module_gate.md) (the driver's checkout, `bench gate aa`),
> [RESULTS_gate_feature_first_real_report.md](RESULTS_gate_feature_first_real_report.md) (the imported calibration this
> is compared with), [PLAN_gate_reviewer_row_fidelity.md](../todo/PLAN_gate_reviewer_row_fidelity.md) (the row defects
> the first campaign found).

## What was held fixed

| variable | value |
|---|---|
| task | `cs2` (feature gate, 2 seeds `cs2-S1`, `cs2-S2`), a one-task A/A suite stamped `gate-seeded-aa-cs2#12738048eb15` |
| product | coai `5d73ead7`, 0 dirty files in `src_mcp/src`, binary `0be8eabb435f`. This is the commit the calibration's cs2 runs used |
| reference | the calibration's `p2-grok-4.7-cs2-r1`, imported as cell `06bfbe00`, turn-1 shape `451c8c505317` |
| harness | `bench` at `4dc98df` (E7 merged), then `44dc722` (PR #49) for campaigns 3 and 4 |
| transport | the calibration's rows: effort, max tokens 8192, 20-minute turn timeout, 3 follow-ups, 20-minute review cap, isolated data directories. Thinking is **on**, which is what the vendor default is for both families (see campaign 1) |
| assessor | `codex-gpt-6-astra-exe`: gpt-6-astra through the codex CLI, `strict-v1`. It is a separate row only because the bare `codex` word does not launch on this machine (D5 of the fidelity plan) |

## The self-check first

Over the imported calibration alone, `bench gate aa --run <import> --against 06bfbe00` compared 4 cells, all at shape
`451c8c505317`. The reference was listed apart and the other 87 cells were another task or commit. It exited 0 in 2.3 s.
So all of the Python harness's own cs2 runs at `5d73ead7` share one shape, and that shape is the yardstick.

## Four campaigns

| run | reviewers × repeats | what happened |
|---|---|---|
| `01a0e98d` | grok, glm, Fable 5.1, Opus 5.5, Astra × 2 | **only Astra's 2 cells valid.** grok and glm were skipped by the product in 2–4 s: the driver wrote `"thinking": false` and the product refuses *off* for xai and glm (D1, D2). Fable failed its turn with HTTP 429, the account's Fable limit. Opus failed with `unrecognized_model`: the `claude` on PATH is 2.1.258, which predates the model |
| `01a0e993` | grok-think, glm-think, Opus (CLI 2.1.284) × 2 | 6 of 6 valid. **The A/A failed:** 4 of 4 API cells at shape `9afba0db90c6`, differing from the reference at 3 431 lines |
| `01a0e9a8` | grok-think, glm-think × 2, **fixed driver** | 4 of 4 valid, **4 of 4 at shape `451c8c505317`**, `bench gate aa` exit 0 |
| `01a0e9b1` | Opus (CLI 2.1.284), Astra × 2, **fixed driver** | 4 of 4 valid |
| `01a0ec20` | Fable 5.1 (CLI 2.1.284) × 2, fixed driver, 2026-09-29 once the account's limit had reset | 2 of 2 valid |

### Why campaign 2 differed

Both causes were in the gate's own clone (`GateCloneCheckouts`), and neither was a model:

1. **Line endings.** `git clone --shared` inherited the machine's global `core.autocrlf=true`, so the plan (committed LF)
   reached the product CRLF. The calibration's checkout had it LF.
2. **An empty submodule.** cs2's repository pins its shared rules as a submodule, and the clone never initialised it.
   The product's rules section read *"NONE of the 12 rules this stage is judged against are here"*, where the
   calibration's read *"8 of the 12 rules … are below"*.

The fence structure was identical (the same eight section fences at the same lines), which is what pointed at the
tree rather than at the prompt builder. The shape comparison found the second cause, which a raw-hash comparison could not
have told apart from session noise. That is the reason for comparing shapes.

The missing rules changed what the reviewers were asked. The same reviewer, the same repeat count and the same product,
before and after the fix:

| reviewer | tokens in / run, before → after | findings / run, before → after |
|---|---|---|
| glm-5.3 | 70 k → 91 k | 6, 6 → 6, 5 |
| grok-4.7 | 83–123 k → 101–104 k | 3, 2 → 2, 3 |
| Opus 5.5 | 447–449 k → 709 k | 6, 8 → 10, 11 |
| Astra | 104–105 k → 125–127 k | 3, 3 → 4, 3 |

So every cell measured before the fix was measured on a different task, and none of its numbers are used below.

## Observed against predicted (the fixed driver)

The prediction, recorded with campaign 3: *every grok-4.7 and glm-5.3 cell has the Python runs' turn-1 prompt shape
`451c8c505317`.* **Held, 4 of 4.**

The prediction's second half was carried over from campaign 2: *grok's and glm's seeds hit on cs2 fall inside the Python
range.* **Held.** The comparison is per cell on cs2 only; n is 2 for C# and 3–4 for Python:

| reviewer | harness | valid | seeds hit | findings | cost / run | s / run |
|---|---|---|---|---|---|---|
| grok-4.7 | Python | 3 of 4 | S1 + S2, every valid run | 3–4 | $0.36–0.37 | 420–624 |
| grok-4.7 | C# | 2 of 2 | S1 + S2, both | 2–3 | $0.34–0.35 | 483–516 |
| glm-5.3 | Python | 3 of 3 | S1 + S2, every run | 3–5 | $0.15–0.22 | 224–355 |
| glm-5.3 | C# | 2 of 2 | S1 + S2, both | 5–6 | $0.20–0.21 | 366–377 |
| Opus 5.5 | C# only | 2 of 2 | S1 + S2, both | 10–11 | $3.91–3.99 * | 410–447 |
| Astra | C# only | 2 of 2 | S1 + S2, both | 3–4 | — (subscription) | 48–50 |
| Fable 5.1 | C# only | 2 of 2 | S1 + S2, both | 10–12 | $6.69–13.89 * | 614–1 360 |

\* Opus's and Fable's costs are the CLI's own reported figures at list price, not an invoice. Fable's second repeat read
twice the tokens of its first (750 k against 356 k), which is where its cost and time spread comes from.

The strict readings are **not an A/A of the assessor**. The Python cells were read by the calibration's own assessor
prompt; these were read by `strict-v1`. So the difference below is recorded, not concluded:

| population | supported | partial | refuted | other |
|---|---|---|---|---|
| Python grok + glm, cs2 | 18 | 3 | 1 | — |
| C# grok + glm, cs2 | 8 | 5 | 3 | — |
| C# Opus 5.5 | 13 | 5 | 2 | 1 unresolved |
| C# Fable 5.1 | 6 | 9 | 3 | 4 unresolved |
| C# Astra | 7 | — | — | read by its own family, counted apart |

The strict percentages stay **not hand-checked** (E4). The hand-check was deferred by the operator on 2026-09-28.

## What this does and does not show

- **Shown:** on cs2 at `5d73ead7`, the C# driver hands grok-4.7 and glm-5.3 the same turn-1 prompt the Python harness
  did, once the product's per-run session id and data directory are normalised away. Under those conditions its cells
  are the Python harness's cells.
- **Not shown:**
  - The other six tasks. The A/A is scoped to one task at one commit on purpose.
  - The CLI reviewers' prompts. A CLI reviewer leaves no turn-1 prompt by design, so Opus, Astra and Fable have no A/A.
    Their cells are simply the first C# measurements of them. (Fable was blocked by the account's limit on 2026-09-28
    and measured on 2026-09-29, `01a0ec20`.)
- **Found on the way** (in the fidelity plan):
  - D1 and D2: thinking is two-state in the bench and three-state in the product, and the import labels the calibration's
    rows thinking-off.
  - D3: `reviewers add` has no price tier.
  - D4: coai's Claude adapter drops the CLI's own failure reason.
  - D5: the assessor's bare `codex` does not launch on Windows, and the failure was recorded finding by finding rather
    than refused up front.

## Reproduce

```
bench gate run --gate feature --suite-file <aa-cs2.suite.json> --reviewers grok-4-7-think,glm-5-3-think \
  --coai-exe <coai-mcp at 5d73ead7> --artifact-root <root> --db <bench> --repeats 2 --parallel 4 --per-endpoint 2 \
  --creds-key-from-coai-settings --prediction "…"
bench gate aa --run <run> --against 06bfbe00-6183-89e6-8412-4d105f03249d \
  --suite-file <suite.json>,<aa-cs2.suite.json> --artifact-root <root> --db <bench>
bench gate assess --run <run> --assessor codex-gpt-6-astra-exe --suite-file <aa-cs2.suite.json> --artifact-root <root> --db <bench>
```
