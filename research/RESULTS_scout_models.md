# RESULTS — Opus scouts against Sonnet scouts (preliminary — not a verdict)

> Status: **in progress, 2026-10-14 — the preliminary run (plan D7) is being launched; this file holds what was fixed BEFORE cell one**
> (the prediction, the operator's metric decision, the design) and will hold every result. Preliminary — not a verdict.
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

*To be filled from the run — per workload and per arm: recall, wrong claims per 10, team time and per-scout time, team cost with its
split by token kind (input, output, cache read, cache write 5 m / 1 h), the paired intervals, the hand-read of the wrong verdicts, and
every void, stopped or aborted cell with its reason.*
