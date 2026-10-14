# RESULTS — Opus scouts against Sonnet scouts (preliminary — not a verdict)

> Status: **the preliminary run ran to the end, 2026-10-14 — preliminary, not a verdict** (plan D7). §1–§4 were committed before cell
> one (`a69e3de`); §5–§7 are the results. **Short answer: at one effort, Sonnet teams found at least as many of the needed files as Opus
> teams, 1.6–1.8× faster and 34–56 % cheaper; they made more wrong claims (+0.15 to +0.98 per 10).**
> Plan: [PLAN_scout_model_calibration.md](../todo/PLAN_scout_model_calibration.md) · the pilot: [scout-calibration/pilot.md](scout-calibration/pilot.md) ·
> module: [module_scouts.md](module_scouts.md).

## 1. The question

The operator asked (2026-10-12): when Claude Code's investigating subagents ("scouts") move from Opus to Sonnet at ⌈1.5·N⌉ — one Opus
becomes two Sonnet, two become three, three become five — **do they still find what is needed, how much faster are they, and how much
cheaper?** Three workloads: W1 (1 → 2), W2 (2 → 3), W3 (3 → 5).

## 2. The operator's metric decision (2026-10-14, before cell one)

> "What matters to me is that what it returns holds what is needed — Opus judges afterwards and re-checks everything itself. For the
> statistics, count the wrong claims too."

So **recall — the files of the change the team's KEY FILES name — is the primary metric**, with time and cost (published prices) beside
it; **wrong claims per 10 are reported for the statistics** and are not what the reading turns on. The pre-registered margins of the plan
(Δrecall ≥ −0.03, Δwrong ≤ +0.5) are still computed and printed as they were registered.

## 3. The design of the preliminary run

- **Tasks:** the eight MAIN tasks of the D10 draw (`scout-calibration/tasks.json`, seed `scouts-preliminary-2026-10-12`) — six private
  repositories by label and two public ones; their reserves stand behind them.
- **Arms:** A1/B1, A2/B2, A3/B3 on every task — 48 cells, plus the plan's two spare cells for void re-runs (50).
- **One effort for every model: `--effort high`** — the pilot's §11 showed that projecting the operator's settings ran the Sonnet scouts
  at `xhigh` and the Opus scouts at `high`, which made the Sonnet teams slow and dear; this run compares MODELS at one explicit level.
- **Budgets:** `scout-calibration/schedule.json`, from the pilot (per arm the largest settled cell × 1.5, the slowest × 2).
- **Pinned:** claude 2.1.289 · `opus` → `claude-opus-5-5` · `sonnet` → `claude-sonnet-5-5` · feature-dev 217b1ff2dbbb.
- **Assessor:** codex `gpt-6.1-sol`, rubric `scout-assess-v1`. Cost check at the CLI's own rates (`cliRates`); every reported dollar at
  the published prices.

## 4. The prediction — §3.9 of the plan, verbatim, as written before the pilot

- Recall: B ≥ A in W2 and W3; W1 between −0.02 and +0.05. C below A by 0.03–0.08 (each Sonnet scout is a little less thorough; the extra
  scouts make up for it).
- Wrong claims: B ≈ A + 0.3 per 10.
- Team time: B ≈ 0.6–0.8 × A (smaller model, parallel scouts, team time = the slowest scout).
- Team cost: B ≈ 0.3–0.5 × A. Basis: the probe, where one trivial Sonnet scout cost $0.023 against $0.57 for one Opus scout — a single
  unmatched run, so a hint, not a measurement.

**Honesty note:** this prediction was written into the plan before the pilot, but this file is committed AFTER the pilot's §11 (26 cells
at `high` on the two pilot tasks) was seen — Sonnet faster 1.1–1.8×, cheaper 37–63 %, recall 91–100 % of Opus, wrong claims about
+0.6 per 10. The prediction above is not revised in its light; the result will be read against it as written.

## 5. Results

**Run `01a139f4`** (preliminary, seed 20261014, `--effort high`, settings pin `d7972dfc7004`): 48 cells planned — **47 scored, 1 void**.
Spent **$50.53** (CLI-reported; the assessor's codex calls ran on the subscription). 1 221 claims, every one assessed.

### 5.1 Per workload — the operator's question

Recall = the files of the change the team named, averaged over tasks; time = the team's wall clock (first Agent call to last result);
cost = the team's tokens at the published prices. Paired differences B − A, clustered by repository, one-sided 95 %.

| workload | Opus team | Sonnet team | recall A → B | Δrecall [95 %] | team time A → B | team cost A → B |
|---|---|---|---|---|---|---|
| **W1** | 1 Opus | 2 Sonnet | 0.79 → **0.82** | +0.033 [−0.034, +0.101] | 95.8 s → **60.3 s** (1.59× faster) | $0.59 → **$0.39** (−34 %) |
| **W2** | 2 Opus | 3 Sonnet | 0.78 → **0.83** | +0.045 [+0.010, +0.081] | 101.4 s → **55.9 s** (1.81× faster) | $1.12 → **$0.49** (−56 %) |
| **W3** | 3 Opus | 5 Sonnet | 0.78 → **0.85** | +0.020 [0, +0.061] *(7 tasks)* | 97.0 s → **62.1 s** (1.56× faster) | $1.54 → **$0.84** (−45 %) |

**Sonnet found at least as many of the needed files in every workload, and did so 1.6–1.8× faster at 34–56 % less money.** On the
pre-registered recall margin (lower bound ≥ −0.03) W2 and W3 pass and W1 misses by 0.004 — its interval straddles zero over eight
tasks; the point estimate favours Sonnet.

### 5.2 Wrong claims — for the statistics (§2)

| workload | Opus | Sonnet | Δwrong / 10 [95 %] | claims decided (A / B) |
|---|---|---|---|---|
| W1 | 1.75 | 1.90 | +0.15 [−0.79, +1.08] | 80 / 158 |
| W2 | 1.26 | 2.24 | +0.98 [+0.44, +1.50] | 159 / 238 |
| W3 | 1.38 | 1.89 | +0.75 [−0.02, +1.55] | 240 / 346 |

Sonnet teams make more wrong claims — clearly at W2, likely at W3, indistinguishable at W1; no workload stays under the registered
+0.5 upper bound. Under the operator's reading the claims are re-checked by the Opus judge anyway, so this is the price of the speed and
not the decision; the pilot's hand-read (pilot.md §5.3) found most of such excess to be line-citation precision rather than substance.
Seven claims were unparseable (A2 1, B1 2, B2 2, B3 2); none unresolved.

### 5.3 Per arm — time per scout and where the money goes

| arm | team time | slowest scout | mean scout | team cost | session cost (CLI) | $ per file found | caller load |
|---|---|---|---|---|---|---|---|
| A1 · 1 Opus | 95.8 s | 95.8 s | 95.8 s | $0.595 | $0.659 | $0.116 | 10.0 files |
| B1 · 2 Sonnet | 60.3 s | 58.0 s | 52.6 s | $0.391 | $0.553 | $0.073 | 11.3 files |
| A2 · 2 Opus | 101.4 s | 100.6 s | 92.2 s | $1.125 | $1.241 | $0.219 | 11.8 files |
| B2 · 3 Sonnet | 55.9 s | 52.3 s | 48.1 s | $0.491 | $0.699 | $0.089 | 12.4 files |
| A3 · 3 Opus | 97.0 s | 93.8 s | 84.2 s | $1.541 | $1.701 | $0.301 | 12.5 files |
| B3 · 5 Sonnet | 62.1 s | 54.3 s | 46.2 s | $0.842 | $1.183 | $0.164 | 15.0 files |

The team cost split by token kind (`report --prices`, the mean per cell; input tokens are negligible everywhere):

| arm | output | cache read | cache write 5 m | total |
|---|---|---|---|---|
| A1 | 8 548 tok · $0.171 | 704 k · $0.141 | 56.5 k · $0.283 | $0.595 |
| B1 | 12 667 tok · $0.127 | 518 k · $0.052 | 85.1 k · $0.213 | $0.391 |
| A2 | 16 799 tok · $0.336 | 1 224 k · $0.245 | 108.7 k · $0.543 | $1.125 |
| B2 | 18 216 tok · $0.182 | 546 k · $0.055 | 101.7 k · $0.254 | $0.491 |
| A3 | 23 804 tok · $0.476 | 1 526 k · $0.305 | 151.9 k · $0.759 | $1.541 |
| B3 | 29 023 tok · $0.290 | 981 k · $0.098 | 181.5 k · $0.454 | $0.842 |

At one effort a Sonnet team writes about 1.1–1.5× the output of the Opus team it replaces and loads a similar amount of code into its
context, at half the per-token price — hence the saving. The largest line on every team is the cache write (the code its scouts read
into context), then the output.

### 5.4 Per task

Files found / files of the change, team time, team cost (private tasks by label):

| task | language · kind · \|G\| | A1 | B1 | A2 | B2 | A3 | B3 |
|---|---|---|---|---|---|---|---|
| p0017 | JavaScript · what-must-change · 7 | 5 · 116 s · $0.54 | 5 · 69 s · $0.30 | 5 · 121 s · $1.19 | **6** · 53 s · $0.34 | 5 · 132 s · $1.54 | 5 · 72 s · $0.66 |
| p0260 | TypeScript · what-must-change · 13 | 10 · 81 s · $0.49 | 10 · 56 s · $0.45 | 10 · 99 s · $1.02 | 10 · 49 s · $0.53 | 10 · 97 s · $1.76 | 10 · 61 s · $0.89 |
| p0677 | JavaScript · where-decided · 7 | 5 · 91 s · $0.89 | 5 · 79 s · $0.67 | 5 · 112 s · $1.74 | **6** · 62 s · $0.72 | 5 · 91 s · $2.17 | 5 · 72 s · $1.50 |
| p0768 | YAML · where-decided · 3 | 3 · 50 s · $0.24 | 3 · 42 s · $0.18 | 3 · 57 s · $0.50 | 3 · 35 s · $0.23 | 3 · 56 s · $0.64 | 3 · 40 s · $0.31 |
| p0964 | JavaScript · how-end-to-end · 13 | 5 · 126 s · $0.68 | **7** · 62 s · $0.34 | 6 · 137 s · $1.43 | **7** · 61 s · $0.57 | 6 · 141 s · $2.05 | void |
| p1535 | TypeScript · where-decided · 7 | **7** · 115 s · $0.75 | 6 · 49 s · $0.35 | 6 · 106 s · $1.11 | 6 · 53 s · $0.55 | 6 · 86 s · $1.47 | **7** · 59 s · $0.79 |
| p1771 | TypeScript · how-end-to-end · 3 | 3 · 87 s · $0.48 | 3 · 63 s · $0.22 | 3 · 75 s · $0.66 | 3 · 53 s · $0.32 | 3 · 76 s · $0.93 | 3 · 59 s · $0.49 |
| connect-other-ais #454 | TypeScript · how-end-to-end · 4 | 3 · 102 s · $0.70 | **4** · 63 s · $0.63 | 3 · 105 s · $1.34 | 3 · 81 s · $0.67 | 3 · 96 s · $1.77 | 3 · 72 s · $1.26 |

Two things the table shows that the means hide: **adding Opus scouts barely found more files** — A1, A2 and A3 found the same count
on six of eight tasks — and **every Sonnet team was faster than its Opus counterpart on every task**.

### 5.5 Against the prediction (§4)

| predicted | measured | |
|---|---|---|
| Recall B ≥ A at W2 and W3; W1 between −0.02 and +0.05 | W2 +0.045, W3 +0.020, W1 +0.033 | **held** |
| Wrong claims B ≈ A + 0.3 per 10 | +0.15, +0.98, +0.75 | higher than predicted at W2/W3 |
| Team time B ≈ 0.6–0.8 × A | 0.63, 0.55, 0.64 | held — slightly faster at W2 |
| Team cost B ≈ 0.3–0.5 × A | 0.66, 0.44, 0.55 | **less saving than predicted** at W1 and W3 |

## 6. Deviations, voids and spend

- **A first launch was stopped** (run `01a139e8`, $11.53 — $6.25 settled and $5.28 charged as unknown for two interrupted attempts):
  the pilot-derived walls (slowest pilot cell × 2, e.g. 2.7 min for A2) cut a valid A2 cell on a larger main task. The run was relaunched
  with **every arm's wall at 15 min and cap at $5** — a wall guards against hangs and a cap against runaway spend; neither should censor a
  slow but valid cell, which on "who is faster" would have biased the result. The pilot-derived schedule stays on record.
- **Voids: three cells on one task, all for the same reason.** On `p0964` a Sonnet scout called `Bash` — B2 once, B3 twice (the
  original and its re-run). The session offers only Task/Read/Glob/Grep; every call came back "No such tool available: Bash … disabled for
  this session, in subagents as well as here", nothing ran, and the pre-registered rule voids a cell with any child call outside the
  read-only set. The two spare cells re-ran both: B2 scored, B3 voided again — so **W3 is read over seven tasks**. No Opus scout tried
  `Bash`. The refused call gains a scout nothing; a reading that does not void it would add B3's `p0964` cell back (not done here).
- Spend: this run $50.53; the stopped launch $11.53; the pilot and its follow-up before it, $40.67 (pilot.md).

## 7. What this means for the rule

At one effort Sonnet scouts at ⌈1.5·N⌉ are a good trade for the operator's purpose: they bring back the files that are needed —
as many as Opus, here slightly more — in roughly 60 % of the time and about half the money, and the extra wrong claims are what the
Opus judge re-checks. Two conditions make it hold: **the Sonnet scouts must run at `high`, not `xhigh`** (pilot §11 — at `xhigh` the same
team took twice as long and cost 2.5× more), and the sample is eight tasks in eight repositories, preliminary under D7.
