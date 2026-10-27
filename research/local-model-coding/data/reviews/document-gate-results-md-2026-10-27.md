# Document gate — results.md, round 2 (2026-10-27)

> coai `review_document` (`newReview: true` — round 1 had closed), repository `dew_flow_benchmark`, branch
> `docs/local-model-coding-research`, document `research/local-model-coding/results.md` as of commit `d918ef2` (parts
> 1–5 and the mutation checks). **Verdict: proceed** — 2 gating findings, threshold 5, **all 4 reviewers answered**
> (antigravity: document review and summary; codex: document review and summary). Tokens 132 165 in / 8 767 out. Every
> finding was checked against the tables, **accepted**, and fixed in the next commit; `resolve` recorded 4 decisions and
> closed the stage.

## Findings

| # | Severity | Reviewer | Finding | Decision |
|---|---|---|---|---|
| 0 | Major | antigravity | §2 items 1 and 10 read as if the whole D scheme lost quality, while §7 shows D1 and D3 at 7.5 and 8 | accepted — item 10's heading now says two of four D arms lost quality; D1/D3 named; the README narrowed the same way |
| 1 | Major | antigravity | §8 counts a renamed test in S4, §7 does not list S4 among the renamed | accepted — S4 and D1 lengthened S5.6.8's name: the scenario detector accepts a name that contains the spec's, the mutation check only the exact name; §7 now says so |
| 2 | Minor | antigravity | O/S's longer elapsed time is not split between the split step and the task batches | accepted — split 5–9 min, review 9–20, fixes 10–28, the batches the rest (23–69 min) |
| 3 | Minor | codex | §9's "not below the classic band on average" does not say whether the lower bound or the mean is meant | accepted — read against the lower bound ($6.53): the mean $6.71 is not below it, but is below the band's mean ($7.21) |

## The reviewers' notes (verbatim, unmerged)

**antigravity — DocumentReview.** results.md evaluates eight coding schemes across 30 experimental arms on a single .NET
test-heavy story pair (E5.5 + E5.6) to determine whether cheaper models (local Qwen, Haiku) or task decomposition can match
the quality of Sonnet while lowering cost. The evaluation incorporates cost tracking, blind LLM judging, post-hoc review fix
rates, and six targeted mutation checks. The findings establish that skeleton fills and parallel task splits with
multi-layered reviews are more expensive than classic single-agent Sonnet due to overhead (skeleton generation, split
generation, and review passes), while direct Haiku coding without a split drops noticeably in quality. The primary
candidate offering a cost reduction without immediate quality loss is Sonnet-based task splitting with Haiku coding and a
single final Opus review (scheme S), though it introduces higher elapsed wall-clock time and higher reviewer finding open
rates. A reader should take away that writing code is rarely the cost bottleneck—review, cache context, and coordination
dominate—and that while scheme S is promising, the results stem from a single, mostly test-based workload evaluated with
n=4 per scheme and narrow mutation testing.

**antigravity — DocumentSummary.** results.md reports an owner-requested evaluation comparing eight implementation schemes
across 30 arms implementing the coai story pair E5.5 and E5.6 (commit b4e2ea96), testing whether cheaper LLMs can replace a
classic Sonnet-writes/Opus-reviews setup. It exists to provide the dew_flow family owner and engineers with empirical data
on cost, time, and quality tradeoffs to select a production coding workflow and prioritize next benchmark iterations.

The document details 30 arms across five parts: Classic ($6.53–7.85, 44–62 min), vLLM/Qwen drafts ($5.95–7.00, 62–68 min),
Skeleton + Qwen ($8.47–9.94), Skeleton + Haiku ($7.38–9.33), Opus task split with Sonnet task review ($8.13–12.01), Opus
split without task review ($5.48–7.90), Sonnet split ($5.03–7.49), and direct Haiku coding ($4.02–5.44). In parts 1–4,
quality remained clustered near 8/10 on the anchored Opus rubric without resolving differences. Quality dropped only in
Part 5 for direct Haiku (D2 scored 6, D4 scored 5.5, leaving 53% of review findings open). A frozen 6-mutant suite killed
180 of 180 mutants overall, but exposed vacuous tests in H2 and T3 and planner bypasses in T1, T2, and D3.

The findings depend on a single task composed predominantly of test code (E5.5 tests only, E5.6 mostly tests) at small
sample sizes (n=1 for V/VT, n=4 elsewhere), evaluated by a single compressing judge model (Opus) and a 30-minute uniform
fix timeout. It explicitly excludes shipping production code from the measurement branches, harness internal mechanics
beyond described setups, and planned future evaluations of local models on task schemes.

The dew_flow owner and engineers are asked to evaluate the cost-quality tradeoffs to pick a coding workflow for upcoming
tasks, take note of the quality risks of unassisted direct Haiku coding versus structured Sonnet/Opus splits, and decide
whether to expand behavioral mutation testing and proceed with the planned Qwen 3.8 task split experiment.

**codex — DocumentReview.** The report compares 30 implementations of one story pair across eight coding schemes. It tracks
cost, time, model usage, review findings, judged quality, and mutation-check results, while distinguishing the repeated
classic runs from single-run vLLM arms and noting limits such as one task, one judge model, and small sample sizes.

Its takeaway is that skeleton approaches cost at least as much as classic on this task; task splits with Haiku can reduce
cost, with Sonnet splits cheaper here than Opus splits; and unsplit Haiku is cheapest but leaves serious defects. The
report recommends broader frozen mutation coverage for future comparisons and identifies a planned Qwen task-split
experiment. It gives readers useful evidence for choosing what to investigate next, but the results do not establish which
scheme will perform best on other tasks.

**codex — DocumentSummary.** This report presents an owner-requested experiment to help dew_flow engineers choose a coding
scheme for real work and decide what to measure next. It compares 30 implementations of the E5.5 + E5.6 story pair, from
commit b4e2ea96, across eight schemes. The measured branches never merge; the report does not propose shipping their code.

The four classic runs cost $6.53–7.85 each and establish a wide baseline spread. In parts 1–4, the report finds no quality
difference it can resolve among classic, vLLM drafts, Qwen or Haiku skeletons, and Opus task splits with per-task Sonnet
review; these alternatives did not beat the classic cost consistently. Part 5 finds Opus- or Sonnet-planned Haiku task
splits cost $5.03–7.90, with judged quality of 7–8, while direct Haiku coding costs $4.02–5.44 but has lower scores in D2
and D4 and leaves more review findings open. Mutation checks killed all 180 mutants across the arms, while five arms had a
scenario test that missed its intended mutant. The report says these results are conditional: four runs per scheme are not
a significance test, the judge is one Opus model whose scores cluster near 8, and fix outcomes are a post-hoc measure.

The evidence is one mostly test-focused task in one repository, with a filler that saw only its prompt, a 30-minute fix
limit, and several time and blinding caveats. The report says it does not settle results for other tasks, local models in
the task-split scheme, more runs of vLLM, cheaper skeleton steps, or a longer fix window. It points to a planned Qwen
task-scheme study as future work and leaves shipping the measured code out of scope.

The report is for the dew_flow family owner and engineers to use when choosing a coding scheme and deciding what to
measure next. It asks them to read the results with the stated limits; it does not name a specific scheme for approval or
assign a concrete next action.
