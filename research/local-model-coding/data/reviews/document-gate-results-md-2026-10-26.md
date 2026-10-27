# Document gate — results.md, round 1 (2026-10-26)

> coai `review_document`, repository `dew_flow_benchmark`, branch `docs/local-model-coding-research`, document
> `research/local-model-coding/results.md` (as of commit `8efca6b`, parts 1–4). **Verdict: proceed** — 2 gating findings,
> threshold 5, **all 4 reviewers answered** (antigravity: document review and summary; codex: document review and summary).
> Tokens 124 957 in / 10 686 out. Every finding was checked against the tables, **accepted**, and fixed in `ddc0857`;
> `resolve` recorded 4 decisions and closed the stage.

## Findings

| # | Severity | Reviewer | Finding | Decision |
|---|---|---|---|---|
| 0 | Major | antigravity | §3 reports VT at $5.95, contradicting §2's "inside or at the edge of the classic band" ($6.53–7.85) | accepted — VT is below the band in cost, above it in time; fixed |
| 1 | Major | antigravity | §7 says H's cost "roughly holds" against Q's ($8–9) while §5 shows H2 and H4 inside the classic band | accepted — the row now says two of four H arms reached the band, which no Q arm did |
| 2 | Minor | antigravity | T's totals ($8.13 / $9.39 / $9.40 / $12.01) do not visibly add up from split + Haiku + final review ($4.97 / $6.36 / $6.27 / $9.12) | accepted — the task reviews and final fixes that make the difference are now named |
| 3 | Minor | codex | §2 says both vLLM arms are inside the classic time band, but VT took 68 min (band 44–62) | accepted — fixed with finding 0 |

## The reviewers' notes (verbatim, unmerged)

**antigravity — DocumentReview.** The document `results.md` reports an empirical comparison across 18 arms representing
five multi-agent coding schemes tested on one real C# story pair (E5.5 + E5.6, 12 test/scenario specifications) in the
ConnectOtherAIs project: Classic Sonnet, local vLLM drafting with Qwen, Sonnet skeleton filled by local Qwen, Sonnet skeleton
filled by Claude Haiku, and an Opus task-splitting scheme where Haiku writes and Sonnet reviews per task. Its key findings
are that no scheme reduced total costs or outmatched Classic Sonnet. The skeleton and Opus-split schemes ended up more
expensive than the classic baseline ($8.47–$9.94 for Qwen skeleton and $8.13–$12.01 for Opus/Haiku, versus $6.53–$7.85 for
Classic) because moving code writing to cheaper models failed to reduce the dominant cost drivers: context cache reads during
iterative agent sessions and the final Opus review pass. Local Qwen drafts and fills produced high compile-error rates and
required extensive Sonnet repair, whereas Haiku filled reliably for pennies but did not lower net arm costs. A decision-maker
reading the document learns that offloading raw code generation to cheaper models does not yield net savings under
full-context agent workflows on mostly-test tasks. However, before acting on or extending these results, a reader must
navigate several internal contradictions: Section 3 claims VT cost $5.95 while Section 2 and Section 7 set the classic floor
at $6.53 and state that VT's cost held inside the classic band; Section 7 asserts that Q's cost was above the classic range
while simultaneously stating that H roughly matched Q's $8–9 despite H2 and H4 falling within classic range; and the document
does not define how to resolve the known blinding leaks and test validation failures before kicking off Part 5.

**antigravity — DocumentSummary.** This document reports the empirical benchmark results of 18 experimental arms across four
completed parts to evaluate whether cheaper LLMs can replace Claude Sonnet as code writers on a 12-scenario story pair (E5.5
and E5.6) in ConnectOtherAIs. It exists to guide decisions on which multi-agent coding configuration to use for real
engineering tasks and to establish what the dew_flow project should measure next. Across all arms, no scheme demonstrated
quality or cost improvements over the classic baseline (Sonnet writes, Opus reviews, Sonnet fixes), which cost $6.53–$7.85
across 44–62 minutes. Local vLLM drafting offered no token savings and produced brittle drafts; Skeleton + Qwen cost more
($8.47–$9.94) because every fill broke the build and required a heavy Sonnet polish pass; Skeleton + Haiku filled cleanly
($0.05–$0.07 fill cost, 69–95% line survival) but ended at $7.38–$9.33 due to the high baseline cost of the skeleton pass and
Opus review; and task splitting via Opus with Haiku execution was the most expensive scheme tested ($8.13–$12.01, mean
$9.73). Across all 18 arms, the primary financial costs remained prompt cache reads (20–39M tokens per arm) and final Opus
reviews ($2.0–$3.8 per arm), while blind judge quality scores remained clustered at 7–8 out of 10. These conclusions depend on
data collected from commit b4e2ea96 using high-effort model settings under the pre-registered harness protocol, supported by
granular trace logs in tables.md and data/. The document explicitly excludes shipping the benchmarked branch code,
generalizing findings to non-test tasks or other repositories, evaluating the internal harness mechanics or system crash
incidents, and analyzing the 12 additional arms and mutation testing slated for part 5. The owner of the dew_flow family and
its engineers are asked to review these comparative run metrics, reject the assumption that cheaper writers reduce net
pipeline costs, and establish frozen behavioral validation gates—such as mutation checks—prior to authorizing subsequent
benchmark phases.

**codex — DocumentReview.** The document reports 18 runs of five coding schemes on one mostly-test task, with shared Opus
review and Sonnet fixes. It compares time, Claude cost, token use, code survival, and judge scores, and checks recorded
predictions against the results. Its overall takeaway is that cheaper coding steps did not reduce total cost in these runs;
the skeleton schemes added work, while Opus task splitting was the most expensive. The runs did not resolve a quality
difference. It also describes limits that narrow those conclusions: one task and repository, few runs for some schemes, CPU
contention, weak or potentially lenient review signals, and an Opus judge whose scores cluster near 8. It points readers to
the tables, harness, incidents, and protocol for supporting detail. A reader deciding what to measure next would take away
that behavioral checks, including mutation checks on controls, are still needed.

**codex — DocumentSummary.** Results.md reports 18 implementations of the E5.5 and E5.6 story pair from commit b4e2ea96, a
12-scenario, Opus-specified task in the coai project. It compares the project's classic Sonnet coding workflow with local Qwen
drafts, Sonnet skeletons filled by Qwen or Haiku, and Opus task breakdowns coded by Haiku and reviewed per task by Sonnet. The
report asks the dew_flow owner and engineers to use the measured time, Claude cost, token, and quality results to choose a
scheme and decide what to measure next. All 18 arms built and passed both suites; 17 delivered the scenarios under their exact
names. The report finds no quality difference resolved by its Opus judge, whose scores cluster around 8/10, and warns that the
result applies only to this task and these conditions. Classic runs took 44–62 minutes and cost $6.53–7.85. Qwen drafts did
not reduce Sonnet output or cost. Qwen-filled skeletons cost $8.47–9.94, while Haiku-filled skeletons cost $7.38–9.33; both
added a skeleton-writing step, and Qwen also required substantial rewriting. The Opus-to-Haiku task scheme cost $8.13–12.01,
averaging $9.73, above the classic band. Across schemes, cache reads and the final Opus review were major cost components.
The report also records which pre-run predictions held, failed, or could not be tested. The findings are limited to one
mostly-test task in one repository: E5.5 is tests only and E5.6 is mostly tests. V and VT each ran once; the other schemes ran
four times. The report flags crashes, concurrent CPU load, judge limitations and possible blinding leaks, and says the task
reviewers may have been lenient. It does not settle results for other tasks or repositories, different filler context or
local models, cheaper skeleton writing, or repeated vLLM runs. Part 5, including mutation checks across arms, is described
as still running and is not included. The report is for the dew_flow family owner and engineers to use when selecting a
coding scheme for real work and choosing the next measurements. It provides evidence and qualifications for that decision;
it does not ask them to ship the measured E5.5/E5.6 code, since the arms are isolated measurement branches that never merge.
