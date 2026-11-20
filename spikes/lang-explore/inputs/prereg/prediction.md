DRAFT — for the team lead's review

# Prediction — language comparison v2, Stage 1, six tasks, bare

> **Written on 2026-11-20** by the team lead (the draft by the PR 6 writer), before the pre-registration tag and before any
> paid session of the series (threat T28). **Exploratory, not evidence:** every line below is drawn from the quick run of
> 2026-11-05 (`research/lang-v2/EXPLORE_quick_2026-11-06_analysis.md`), which saw the same six tasks on draft specs, three
> pairs per task, on a harness written fast, scored once. That run is not data, is never pooled with this series, and
> three pairs per task can suggest a pattern but cannot prove one. The parent's ten-task draft prediction stays as
> history and is not this file.

## What the quick run showed (the analysis's own figures)

| Task | Kind | Accepted C# | Accepted Node | Median cost C# | Median cost Node | C# cheaper in |
|---|---|---|---|---|---|---|
| 04 | web | 3 of 3 | 3 of 3 | $1.15 | $0.93 | 1 of 3 |
| 05 | daemon | 3 of 3 | 3 of 3 | $1.50 | $2.35 | 2 of 3 |
| 06 | web | 2 of 3 | 3 of 3 | $2.71 | $1.18 | 0 of 3 |
| 07 | daemon | 3 of 3 | 3 of 3 | $0.85 | $5.58 | 3 of 3 |
| 08 | web | 3 of 3 | 3 of 3 | $0.77 | $0.87 | 3 of 3 |
| 10 | daemon | 3 of 3 | 3 of 3 | $1.74 | $2.91 | 3 of 3 |

Totals over the 18 sessions of each stack: cost $25.95 for C# and $41.06 for Node; without task 07, $23.02 and $26.22.
Task 07 is $12.34 of the $15.11 total difference, and its gap is confounded (threat T26): its Node agents worked against a
stated 256 MB floor while 512 MiB was enforced.

## The prediction

1. **Quality.** Both stacks reach an accepted solution on every task: no task ends "no correct solution" in either stack,
   so every per-task CPC ratio is defined. The quick run accepted 35 of 36 sessions on first scoring (17 of 18 C#, 18 of 18
   Node). Ten scoring passes are stricter than one, so the success rate may be lower than that in either stack, and most
   likely where a multi-threaded server meets a race (the quick run's one reject was such a race, a C# session of task
   06).
2. **Cost per correct solution, the daemon tasks (05, 07, 10).** C# is cheaper on each of the three: C# higher in 0 of 3,
   and the per-kind median ratio below 1. On task 07 the direction is predicted, not the size: with the memory statement
   now equal to the enforced limit (D72), the quick run's gap is not expected to repeat at its size.
3. **Cost per correct solution, the web tasks (04, 06, 08).** Mixed: no stack is cheaper on all three. C# higher on 04 and
   06, cheaper on 08, as the quick run's medians were.
4. **Overall.** The median of the six per-task C# over TypeScript CPC ratios is below 1, and C# is higher in 2 of 6 tasks.
   The direction rests on the daemon tasks; the size is not predicted, and the claim's limit holds (a 20 % difference is
   not distinguished).
5. **Time.** The wall follows the cost: C# shorter on the daemon tasks, mixed on the web tasks.

## How this file is read

The report shows each line beside its observation. A line that the series contradicts is reported as contradicted; no
line is changed after the tag.
