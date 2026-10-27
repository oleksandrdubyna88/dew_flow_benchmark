# Results — can a cheaper model write the code a Sonnet coder would? (2026-10-25/26)

> Status: **parts 1–5 complete — 30 arms, all checked, judged and mutation-checked (part 5 ended 2026-10-27 05:30
> UTC). Part 5b (SR, DR ×4) is complete (§7b, ended 2026-10-27 20:03 UTC).**
> Every number below is in [tables.md](tables.md), generated from [data/](data/). How it was measured:
> [harness.md](harness.md). What went wrong and what was corrected: [incidents.md](incidents.md). The pre-registered
> protocol and its change log: [protocol.md](protocol.md).

## 1. The question and the arms

One real story pair of the coai project — **E5.5 + E5.6** of `dew_flow_connect_other_ais ·
todo/PLAN_vibe_and_kimi_runtimes_E5.md` (12 scenarios, Opus-written spec), from commit `b4e2ea96` — implemented 30 times
by eight schemes (plus 8 arms of two more schemes in part 5b, §7b), every arm ending in the same Opus review and a fix step (Sonnet fixes in parts 1–4, Haiku in part 5):

| Scheme | Arms | What writes the code |
|---|---|---|
| **Classic** (the project's rule) | C, C2, C3, C4 | Sonnet writes it all |
| **vLLM drafts** | V (thinking off), VT (thinking on) | the local model (Qwen3.6-35B-A3B on vLLM) drafts each method; Sonnet integrates and fixes |
| **Skeleton + Qwen** (the owner's scheme) | Q1–Q4 | Sonnet writes signatures, types, data and per-region instructions + context packs; a script has Qwen fill every body ≥ 4 lines between markers; Sonnet polishes |
| **Skeleton + Haiku** | H1–H4 | the same, with Haiku filling |
| **Opus tasks → Haiku → Sonnet per task** | T1–T4 | Opus splits the stories into 4–6 tasks with exact contracts; Haiku implements each task as a full agent; Sonnet reviews and fixes each task; tasks run in parallel where their files do not overlap |
| **Opus tasks → Haiku, one Opus check, Haiku fixes** (part 5) | O1–O4 | as T without the Sonnet task reviews: one Opus review of the whole stories, and Haiku applies its findings |
| **Sonnet tasks → Haiku, one Opus check, Haiku fixes** (part 5) | S1–S4 | as O, with Sonnet writing the task split |
| **Haiku codes the stories directly** (part 5) | D1–D4 | no split: Haiku gets the classic coder brief; Opus review; Haiku fixes |
| **S + a fresh Haiku review** (part 5b) | SR1–SR4 | as S; before the Opus review, a fresh Haiku session reviews the whole stories and fixes what it found |
| **D + a fresh Haiku review** (part 5b) | DR1–DR4 | as D, with the same fresh Haiku review-and-fix before Opus |

The four classic arms are ONE configuration run four times: their spread shows how much one configuration varies on this
task. Four runs are not a statistical threshold (and C had an accidental extra attempt), so every other arm is read
against those individual observations, not as a significance test. All models at effort `high`, named by alias; the
hardware and timing are in [harness.md](harness.md). Haiku has no role in the project's model rules: every arm where it
writes code (H, T, O, S, D) is a deviation the owner asked for, recorded in [protocol.md](protocol.md).

## 2. What the run shows (with its conditions)

1. **In parts 1–4, no quality difference was resolved** (narrowed after review, 2026-10-26 — it first said "quality was
   the same everywhere"). All 18 arms build and pass both suites; 17 deliver 12/12 scenarios by exact name (T4 delivered
   S5.6.6 under another test name). The absolute blind judge (one Opus session per diff, anchored rubric, 2 runs) scored
   13 arms 8/10, H2, T3 and T4 8 and 7, and C and Q2 7/10; the two runs never differed by more than one point. But this
   judge is one model and compresses scores near 8: T3, whose junction-first test was confirmed to be unable to fail,
   still scored 7 and 8 — and so did H2, whose test has the same defect (found by the mutation checks, item 12). It
   separates "clean" from "has a major finding", and cannot show finer differences. **Part 5 is the first to show a
   difference** — in two of the four D arms (item 10).
2. **The classic arms vary a lot**: 44–62 min of work, **$6.53–7.85**, 79–93k Sonnet output tokens. A difference inside
   this range cannot be attributed to the scheme from these runs — which is not proof that there is none.
3. **vLLM drafts (V, VT): no gain from the drafts.** V sits inside the classic band ($7.00, 62 min — the band's upper
   edge in time). VT is $5.95, BELOW the band, and 68 min, ABOVE it; its lower cost comes from a cheaper review and fixes
   (only minor review findings), not from its drafts (§3), and it is one run. The local model
   did little of the work (6 requests each, all early); its drafts guessed existing API names and were fixed heavily or
   rewritten. Sonnet's output did not fall (93–94k, the top of the band). Thinking on (VT) made step 1 slower (43 min; 22
   min of GPU for 6 requests) without better drafts.
4. **Skeleton + Qwen (Q1–Q4): more expensive, same quality.** **$8.47–9.94**, above every classic arm, with 132–155k
   Sonnet output (the band: 79–93k). Qwen filled every region in 2–4 minutes for $0, but the build failed after every
   fill (2–11 compile errors, a lower bound), so the polish was a full second writing pass (47–67k Sonnet output tokens,
   278–455 changed lines). About half of Qwen's test lines survived (50–56 %).
5. **Skeleton + Haiku (H1–H4): a much better filler, still no saving.** Haiku's fill cost **$0.05–0.07 per arm**, left
   1–4 compile errors, and **69–95 % of its test lines survived** (Qwen: 50–56 %); the polish was a third of Q's (63–171
   changed lines, 20–36k Sonnet output, $1.0–2.0 against $2.5–3.4). But the arms still cost **$7.38–9.33** — two inside
   the classic band, two above — because the skeleton step itself costs $2.1–3.0 and the review $2.4–3.8.
6. **Why the skeleton schemes cost more — a correction of a first guess.** The context packs (8–60k tokens' worth per arm)
   were the first suspect; per step they do not explain it: the skeleton step wrote 53–85k output tokens whether its
   packs were small or large — about what a whole classic coding step writes (51–67k). The skeleton is not a cheap
   step, and the scheme adds a polish on top of it.
7. **Opus tasks → Haiku → Sonnet per task (T1–T4): the most expensive scheme, same quality.** **$8.13–12.01** (mean
   $9.73). Haiku did nearly all the writing for **$0.18–0.63 per arm**, and 90–98 % of its code is in the final code; the
   per-task Sonnet reviews were cheap ($0.17–0.71 each: they read only the task's files) and changed little (3–36 lines
   per arm). But the Opus split cost $2.1–5.9 — its session produced 36–101k output tokens, of which the task documents
   themselves are only 3.9–10.1k words (35–90 KB; corrected 2026-10-26 — it first said "50–100k tokens of task
   documents"); the rest is reasoning and tool use, not attributed further — and the final Opus review still found 4–9
   issues per arm (0–3 major), some of which the task reviews had passed (§6). Split + Haiku + final review alone come to
   $4.97 / $6.36 / $6.27 / $9.12; the rest of each total is the Sonnet task reviews ($1.48 / $1.46 / $1.79 / $2.16) and
   the final Sonnet fixes ($1.68 / $1.56 / $1.33 / $0.73) — [tables.md](tables.md) §4b.
8. **Where the money goes in every scheme:** cache reads (the context re-sent on every turn of a session: 20–39 M tokens
   per arm — a measure of session length × context, NOT of how much unique repository was read) and the Opus review
   ($2.0–3.8 per arm, 25–47 % of an arm). In parts 1–4, moving code WRITING to a cheaper model moved neither, because a
   Sonnet session still wrote, polished or reviewed the code in every arm. Part 5 has no Sonnet session in O and D (in S
   only the split), and the cost fell; its Opus review ($2.29–3.88) is then 34–68 % of an arm.
9. **A defect the skeleton scheme adds:** the skeleton step writes its deviation notes into the plan, and they go stale
   after the fill and the polish; the judge found such false notes in Q2 and H3 (checked by hand: both true findings).
10. **Haiku without a split (D1–D4) is the cheapest scheme, and two of its four arms lost quality.** **$4.02–5.44** (mean
    $4.55, 37 % below the classic mean of $7.21), 47–64 min. But D2 scored 6 and 6 and D4 6 and 5 — the only scores
    below 7 in 30 arms — with 2–4 major findings each, checked in the code (D4's S5.6.7 is one `[Fact]` where the spec
    asks for a three-arm Theory; D2's S5.5.2 has no exit-70 arm; both plans' status lines say tests are missing that the
    diff adds). The Opus review found 7–12 issues per D arm, and the Haiku fixer left **53 % of them open** (18 of 34; 6
    of 9 majors) — D4's fixer wrote that time ran out. D1 and D3 scored 7.5 and 8, inside the classic range: D's
    result is split, not uniform, and four runs cannot say how often a D arm fails.
11. **Removing the Sonnet task reviews brings the task split into the classic band, and a Sonnet split under it.**
    O (Opus splits): **$5.48–7.90** (mean $6.71; T was $8.13–12.01); S (Sonnet splits): **$5.03–7.49** (mean $6.22;
    three of four below every classic arm). Quality 7–8 in both, like the classic arms. Haiku did nearly all the
    writing again (474–542 meaningful lines, 98–100 % kept), and the Haiku fixer left 17 % (O) and 32 % (S) of the
    review's findings open (Sonnet fixers in parts 1–4: 13 %, 12 of 89). The price is time: 62–107 min elapsed (classic 44–62),
    because the batches run one after another, each with its builds: the split takes 5–9 min, the review 9–20 and the
    fixes 10–28; the task batches take the rest, 23–69 min. The split is cheaper from Sonnet ($1.95–2.31) than
    from Opus ($2.09–3.44). Two O arms lost a point the same way: the review asked to correct the shared "unconfined"
    caveat for Kimi, and the Haiku fixer rewrote it for every runtime (O2, O4) — a finding applied wider than the
    stories allow.
12. **The mutation checks, run on all 30 arms, found a defect the judge did not.** Every one of the 180 mutants (6 per
    arm) was killed by some test, so no arm lets a broken spec decision through unseen. But per intended scenario test,
    five arms have one that cannot catch its mutant: **H2 and T3** — the junction-first test of S5.6.7 can never fail
    (the recorder is set in `InitializeAsync`, and a value set in an async method does not flow back to the test; checked
    in the code) — and **T1, T2 and D3**, whose S5.6.5 builds the plan by hand instead of through the planner the spec's
    Given names, so it cannot see a missing planner arm (S5.6.4 still catches it). H2's defect passed the Opus review,
    the fixer and the judge (8 and 7). No classic, O or S arm has such a miss. The checks do not separate the D arms:
    their defects are missing scenario arms and false notes, which the six frozen mutants do not cover.

**This run does not settle:** other tasks (this one is mostly tests: E5.5 is only tests, E5.6 mostly), smaller or more
self-contained pieces, a filler given the repository (here it saw only its prompt), other local models, a skeleton step
made cheaper (for example by a cheaper model), V/VT at more than one run each, whether S's saving holds on other tasks
or with a time limit that lets Haiku's fixer finish, or a local model in the task scheme (planned:
`todo/PLAN_local_model_coding_qwen38.md`).

## 3. Part 1 — vLLM drafts against classic (V, VT, C, C2; then C3, C4)

- **Time and cost**: V 62 min / $7.00, VT 68 min / $5.95, against the classic band 44–62 min / $6.53–7.85
  ([tables.md](tables.md) §1). VT's lower cost is its cheaper review and fixes — its step-2 review found only minor issues
  (5 minor, no major), so its fixes cost $0.75 — not its coding step ($3.05, the most expensive step 1 of part 1).
- **What vLLM did** ([tables.md](tables.md) §5): V — 6 requests, 13k tokens, 3 min of GPU, all `--no-think`; VT — 6 requests,
  51k tokens (mostly thinking), 22 min of GPU. Per V's and VT's own notes (`data/arms/V/V-code.md`,
  `data/arms/VT/VT-code.md`): only two production pieces were used nearly unchanged (V's planner arm and refusal; VT's
  planner arm); test drafts were fixed heavily or rewritten; one VT production draft was discarded, and its method
  written first by Sonnet and re-prompted afterwards — a rule deviation the coder recorded.
- **Why the drafts were weak**: the prompts carried 0.3–2.5k tokens of context, while the Sonnet coder read 20+ M tokens
  of the repository. The model guessed existing names (`ProcessResult.Success`, `--home=` as one argument, an init-only
  property assigned).
- **The group judge exaggerated small differences.** One Opus session ranking V, C, C2, VT together put them C2 > V > C > VT
  in both runs (9/8, 8/7, 7/6, 6/5 after the correction) — while the absolute judge, scoring each alone, gave 8, 8, 7, 8.
  A ranking forces an order on near-equal items; read the absolute scores ([tables.md](tables.md) §6–7).

## 4. Part 2 — skeleton + Qwen (Q1–Q4)

- **Time** 59–63 min, inside the band; **cost $8.47–9.94**, above it. Per step ([tables.md](tables.md) §2): skeleton
  11–12 min, $2.0–2.9; the two fills 3–5 min, $0; polish 19–26 min, $2.5–3.4; review $2.1–3.0; fixes $0.7–1.6.
- **The fill** ([tables.md](tables.md) §4–5): 13–19 test regions and 0–2 production regions per arm, all filled, none
  rejected; 13–21 requests per arm, 103–121k prompt tokens but only 3–5k completion tokens (whole files in, one body out),
  2–3 min of GPU. The build failed after every fill: in Q1 one region declared a class inside a method body (the rail
  cannot see that), in the others ordinary compile errors.
- **Survival**: 50–56 % of Qwen's test lines in the final code. Hand-checked: a low value can be a real rewrite (Q1
  `s552`, 4/17 kept: Sonnet inlined and restructured) or a move (Q2's helper class moved unchanged to another file — the
  reason the measure looks in every changed file). Production regions: too few lines to read (0–5 per arm).
- **Quality**: 8/10 for Q1, Q3, Q4 and 7/10 for Q2 (two major findings, one of them a stale skeleton note).

## 5. Part 3 — skeleton + Haiku (H1–H4)

- Run **in parallel** with the end of Q3/Q4 (up to 6 arms at once, the owner's request), so their **work times (70–91
  min) include heavy CPU contention** (load average ~22 on 14 cores) and are not comparable with the band. Cost and
  tokens are not affected by load.
- **Cost $7.38–9.33** (H2 and H4 inside the classic band, H1 and H3 above). Per step: skeleton $2.1–3.0; Haiku fill
  $0.05–0.07 (14–20 requests, 160–236k tokens in all, ~1 min); polish $1.0–2.0; review $2.4–3.8; fixes $0.8–1.2.
- **The fill**: 13–18 test and 0–2 production regions per arm, all filled, none rejected; the build still failed after
  every fill, with 1–4 errors (Q: 2–11).
- **Survival**: **69–95 %** of Haiku's test lines in the final code (H1 90, H2 95, H3 94, H4 69), production 83–100 % of
  3–6 lines. Hand-checked on H1 and H4: a kept line is in the final file; a dropped call (3 times after the fill, 0 at the
  end) was really replaced.
- **The polish** changed 63–171 lines (Q: 278–455) and wrote 20–36k Sonnet output tokens (Q: 47–67k).
- **Quality**: 8/10 for H1, H3, H4; H2 8 and 7. The step-2 review found majors in H2 (3), H3 (1) and H4 (1); the fixes
  dealt with them.
- **Reading**: Haiku is a much better filler than the local Qwen for this kind of code (fewer errors, most of its code
  kept, a small polish), at a negligible price — but the scheme around it (a full skeleton step, a polish, the review)
  keeps the total at or above the classic arms.

## 6. Part 4 — Opus tasks, Haiku coder, Sonnet per task (T1–T4)

- **The split** ([tables.md](tables.md) §4b): every Opus task list passed the checker at once (5–6 tasks, 3–4 batches, up
  to 3 tasks in parallel); every task document carried exact contracts (full signatures with file and line, JSON rows
  verbatim). Its cost: $2.13–5.94 (T4: 101k Opus output tokens for 10.1k words of documents) — with the final review,
  the largest item of a T arm.
- **Haiku**: up to 19 minutes of agent work per task, **$0.02–0.30 per task**, 487–570 meaningful lines per arm, and
  **90–98 % of them in the final code**. Every parallel task's commits came back to the arm branch without a conflict.
- **Sonnet per task**: $0.17–0.71 per task, about 0.6 M cache-read tokens per review (a classic coder reads 10–30 M). It
  changed 0 lines in most tasks (3–36 per arm in all) — either Haiku was right, or the review was lenient. The final Opus
  review then found 4–9 issues per arm, 0–3 major. Opened in the code: T1's new test folders break the project's
  test-leftovers naming; T3's junction-first test can never fail (`GitScratch.AfterUnlink` is per async flow and is set in
  `InitializeAsync` after an `await`, so it never reaches the test method — passed by the task review AND the final review,
  found by the judge); T4 renamed S5.6.6's test and widened its table row without recording it.
- **Totals**: $8.13 / $9.39 / $9.40 / $12.01; elapsed 80–100 min (two arms at a time, each with parallel worktree builds).
- **Quality**: 8 / 8 / 7.5 / 7.5 — inside the classic band. T was judged with a WIDER redaction list than Q and H (the
  scheme's own words removed), so it was judged blinder.
- **Reading**: exactly specified tasks make a cheap coder work — Haiku wrote almost everything, and almost all of it stayed.
  But the split is itself an Opus writing job, the cheap per-task reviews do not replace the final review, and the total
  lands above every other scheme.

## 7. Part 5 — no task review: Opus or Sonnet splits, or no split; Haiku codes and fixes (O, S, D ×4)

- **Design** (the owner's, after part 4 and the consultation): O = T without the Sonnet task reviews; S = O with Sonnet
  splitting; D = no split at all — the consultant's "largest untested assumption" (does Haiku need an exact task plan?).
  One fresh Opus review of the whole stories per arm, never per task; Haiku fixes. Four rounds of three (O_n, D_n, S_n
  together), so the schemes shared time of day and machine load. Predictions recorded before the run (§9).
- **Cost and time** ([tables.md](tables.md) §1, §2, §4b): O $5.48 / $6.69 / $6.78 / $7.90, 78–99 min elapsed; S $6.30 /
  $6.04 / $5.03 / $7.49, 62–107 min; D $4.02 / $4.61 / $4.13 / $5.44, 47–64 min. The classic band: $6.53–7.85, 44–62 min.
  Every step's cost is the host's own number (no attempt was interrupted).
- **Per role**: split $2.09–3.44 (Opus) or $1.95–2.31 (Sonnet); Haiku's coding $0.30–0.99 per arm in O and S, $0.94–2.36
  in D (one long session over the whole stories); Opus review $2.29–3.88; Haiku fixes $0.14–0.61, except S4's $2.11 (a
  long session: 68 M cache-read tokens in that arm).
- **Haiku's code**: 474–542 meaningful lines per O/S arm, 98–100 % in the final code (T: 90–98 %) — but nobody reviewed it
  before the final review, so "kept" here says less than in T.
- **The final review and the fixes** ([tables.md](tables.md) §9 — a measure added AFTER the results were seen, not
  pre-registered; the prediction named the mutation checks and the judge as its test): findings per arm O 2–7, S 4–6,
  D 7–12. Left open by the
  Haiku fixer (partly or not fixed, by the fixer's own note): O 3 of 18, S 6 of 19, D 18 of 34 (6 of 9 majors). The
  Sonnet fixers of parts 1–4 left 12 of 89 (13 %). This is confounded: D's coder was also Haiku, so its review had more
  to find, and the fix step had the same time limit in every arm.
- **Quality** ([tables.md](tables.md) §6): O 8 / 7 / 7 / 7.5, S 8 / 7.5 / 8 / 8, D 7.5 / 6 / 8 / 5.5 (means of two
  runs). The majors in O2 and O4 are one pattern: the review asked to correct the shared "unconfined" caveat for Kimi,
  and the Haiku fixer rewrote it for every runtime, in C# and TypeScript; O3's is a false deviation note. D2's and D4's
  are scenario arms that are not written and status lines that are false (checked in the code, §2 item 10).
- **Scenarios by exact name**: 8 of 12 arms 12/12; O2, O3 (S5.6.6), D1 (S5.6.7) and D2 (S5.6.4, S5.6.8) renamed tests.
  S4 and D1 also lengthened S5.6.8's name (`AdmissionFlagsBothRows_And…`): the scenario detector accepts a name that
  CONTAINS the spec's, the mutation check only the exact name — so these count as present in [tables.md](tables.md) §3
  and as renamed in §8. Each renamed test exists and catches its mutant (§8), so all 12 arms deliver every scenario
  under some name.
- **Reading**: on this task, an exact split lets Haiku do the writing, and one Opus review is enough to keep the quality
  at the classic level; a Sonnet split is cheaper than an Opus one and no worse here. Without a split, Haiku's code is
  incomplete in ways the review finds but the Haiku fixer does not finish. The saving of O and S comes from removing
  the Sonnet task reviews AND from a fix step that costs very little; it costs time.

## 7b. Part 5b — S and D with a fresh Haiku review before Opus (SR, DR ×4)

> Complete: all 8 arms checked, judged, mutation-checked and hand-checked (2026-10-27, the chain ended 20:03 UTC).
> Every number is in [tables.md](tables.md) (§1–§4b, §6, §8; the fix-outcome table §9 was not extended to part 5b).

- **Design** (the owner's, after part 5; recorded before the run in [protocol.md](protocol.md) "Part 5b"): the S and D
  pipelines unchanged, plus ONE step after Haiku's coding — a fresh Haiku session reviews the whole stories with the
  Opus review's checklist and fixes what it found (`hr-review-fix`, 30 min). The Opus reviewer is not told that a Haiku
  review ran. Four rounds of two (SR_n with DR_n), 2026-10-27 10:50–17:43 UTC.
- **Cost** (the host's own numbers, [tables.md](tables.md) §1, §4b): SR $6.69 / $6.28 / $7.47 / $6.23
  (mean $6.67; S: $6.22); DR $5.18 / $6.68 / $5.54 / $4.80 (mean $5.55; D: $4.55). The classic band: $6.53–7.85.
  The Haiku review step: SR $0.66 / $0.72 / $0.54 / $0.61, DR $0.68 / **$2.10** / $0.44 / $0.48 (DR2's session read
  34 M cache tokens). DR2 is in the classic band; the other three DR arms are below it.
- **Time** (real elapsed, first step start to last step end): SR 100 / 99 / 110 / 66 min (S: 62–107); DR 84 / 66 / 74 /
  67 min (D: 47–64). The Haiku review step: SR 24 / 14 / 12 / 12 min, DR 16 / 16 / 15 / 13 min. DR1's Opus review
  stopped at its hard deadline (30 min, exit 124) after it had written its review; that review counts as complete.
- **What the Haiku reviewer found** (its own notes `data/arms/<arm>/<arm>-hreview.md`, counted by hand): SR 14 / 10 / 8 / 10 findings,
  6 / 7 / 5 / 6 fixed, 3 / 4 / 4 / 2 left open; DR 21 / 23 / 24 / 22 findings, 9 / 12 / 13 / 14 fixed, 5 / 4 / 7 / 8 left
  open (the rest: partly fixed, or wrong on a second look). Without a split, Haiku's code leaves about twice as much
  for the reviewer.
- **The Opus review after it** (`<arm>-review.json`): blocking + major findings per arm SR 0 / 0 / 0 / 0 (S: 0 / 0 / 0 /
  1), DR 1 / 3 / 2 / 2 (D: 2 / 3 / 0 / 4). All findings: SR 4 / 5 / 6 / 5 (S: 4 / 6 / 5 / 4), DR 9 / 6 / 7 / 9 (D: 7 / 8
  / 7 / 12). SR2 was the only arm of the 16 with the verdict "ready".
- **The Haiku review introduced defects the Opus review then flagged** (prediction 5; each checked against the step's
  commits): DR4 R3 — the Haiku review's guard against leading arguments (`0549de03`) raised `Build`'s cyclomatic
  complexity to 5; DR1 R6 — the Haiku review created `KimiDeleterHookCollection.cs` with a comment that calls two
  async-local hooks "process-wide statics". Two more are stale records the Haiku review left behind: SR4 R1 (deviation
  50 still names a Theory the Haiku review had split into two Facts) and DR2 R4 (the deviations not updated after the
  Haiku review's commits). All four are minor.
- **Objective checks**: all 8 arms build, pass the format and generator checks and have 0 new test failures (MCP and
  TypeScript). Scenarios by the detector: SR 12 / 11 / 12 / **9** of 12, DR 11 / 12 / 11 / 12. Haiku's code in SR:
  560 / 511 / 602 / 511 meaningful lines, 98 / 99 / 100 / 91 % in the final code.
- **Quality** (absolute judge, two runs each): SR 8 / 8 / 8 / 8, DR 7.5 / 7.5 / 7.5 / 7 (S 8 / 7.5 / 8 / 8, D 7.5 / 6 / 8 /
  5.5). Majors named by the judge, both runs added: SR 0 / 0 / 2 / 0, DR 0 / 1 / 1 / 3 (S: 1 in all, D: 12 in all).
  Hand-checked, three of them, all true: DR2's kimi guard refuses a plan with `Flags` but not with `Leading` (the same
  gap DR4's Haiku review closed); SR3's Haiku FIXER (after the Opus review's R1, commit `5dff3a23`) rewrote the shared
  "unconfined" caveat for every runtime in C# and TypeScript — the same pattern as O2 and O4 in part 5; DR4's plan says
  `VibeHomes.EmptiedUnderItsLock` "does not exist in the source", but it is at `VibeHomes.cs:220`. Not checked: the
  judge's DR3/DR4 point that S5.6.7 compares a snapshot of the shared TEMP folder while other classes run in parallel.
- **Mutation checks** (all 8 arms): 48 of 48 mutants killed. Per intended test: **SR1's
  `AKimiQuestionLaunch_IsTheShimRequest_InItsFolderOrItsCopy_OnTheRowsHome` is present and does NOT catch M2** (no
  planner arm for kimi; other tests catch it). Opened in the code: the test builds `Confinement.Planned` by hand
  (`PlanOf`) and calls the consultant's `Build`, never the planner — the same miss as T1, T2 and D3 in part 5. It is the
  only intended test of the 8 arms that is present and misses. Missing by exact name — each one renamed or split
  into two or three tests, and each of those catches its mutant (checked in `killedBy`; [tables.md](tables.md) §8 marks them `r`):
  `AKimiDiskRow_ReadsItsCopies_AndLeavesNothingBehind` in DR1–DR4, SR3, SR4; `AdmissionFlagsBothRows` in SR2, DR3,
  SR4; `APlanWithFlags_IsRefusedForKimi` in DR3; `AfterTheCheck_TheHomeIsGone_JunctionFirst` and
  `ThePlannerArm_IsACopyForDisk_AnEmptyFolderForNone_AndRefusesWeb` in SR4 (SR4 has 9 of 12 scenarios by name).
- **Independence — a leak found in the transcripts** ([incidents.md](incidents.md) §2e): the notes folder is shared by
  all arms, and the Haiku reviewers of DR3 and DR4 listed it and read the PREVIOUS DR arm's review notes (DR3 read
  DR2's, DR4 read DR3's; DR4's notes say "still open from DR3 H4"). DR3 and DR4 are therefore not independent of DR2
  and DR3. Part 5's fixers also opened other arms' files, but only tool logs (format, suite, generator output) and, in S2,
  D2's and O2's fix notes searched for the word "format" — no other arm's findings.
- **Reading**: the fresh Haiku review costs about $0.5–0.7 and 12–24 min per arm (one outlier, $2.10). It
  did not reduce what the Opus review finds — the DR arms still had majors for Opus in 4 of 4 arms — but the final
  code after the Opus review and the Haiku fix was judged better than D's: no DR arm below 7 (D: 2 of 4), and the
  judge's majors fell from 12 to 5. S was already at 8 and stays there. The Haiku fixer's over-wide fix (SR3, as O2 and O4 in part 5) is still
  there, and the extra review added small defects of its own (prediction 5). n = 4 per scheme, one task, and DR3/DR4 are
  partly dependent (above), so this is a direction, not a measured size.

| Prediction (recorded before the run) | Observed | |
|---|---|---|
| 1. The Haiku step costs $0.3–1.0 and 10–30 min; SR $5.5–8.5, DR $4.5–6.5; DR the cheapest scheme and below the classic band; SR's mean inside or below the band | step $0.44–0.72 except DR2 $2.10; 12–24 min. SR $6.23–7.47 (mean $6.67, inside the band). DR $4.80–6.68: DR2 above $6.5 and inside the band; DR's mean $5.55 below the band, but D ($4.55) stays cheaper | partly holds |
| 2. The Opus review finds fewer blocking+major than in S and D, by at least 1 per arm, most clearly in DR | SR 0 vs S 0.25; DR 2.0 vs D 2.25 per arm | **wrong** |
| 3. DR at most 1 of 4 arms below 7; SR stays at 7.5–8 | DR 7–7.5 (none below 7); SR 8 | holds |
| 4. The mutation checks still show at least one of the 8 arms with an intended test that misses its mutant | SR1's S5.6.5 test misses M2 (it never calls the planner; checked in the code) | holds |
| 5. In at least 1 arm the Haiku fix changes something the Opus review then flags | DR4 R3 (complexity), DR1 R6 (a false comment); plus two stale records (SR4 R1, DR2 R4); all minor | holds |
| 6. Wall time DR about 60–95 min, SR about 75–135 min | DR 66–84; SR 99–110, but SR4 66 | holds for DR; SR 3 of 4 |

## 8. Behaviour — the mutation checks on all 30 arms

- **The instrument** ([harness/mutate.mjs](harness/mutate.mjs), [tables.md](tables.md) §8): six mutants frozen before any
  part 5 result, each breaking one decision of the spec in an arm's final code (M1 one-shot homes not disposed
  junction-first; M6 the same on the rootless check's path; M2 no planner arm for kimi; M3 kimi × disk "confined"; M4
  kimi × web "confined"; M5 a plan with flags accepted); the arm's scenario classes re-run against each. Validated
  before use: on T3 it reports the known vacuous test, on C2 every intended test catches its mutant.
- **Result**: 180 of 180 mutants killed. Per intended scenario test (10 per arm, 300 in all): **five misses in five
  arms** — H2 and T3 (S5.6.7 cannot fail on M1: the `GitScratch.AfterUnlink` recorder is set in
  `InitializeAsync`, and an async-local value set inside an async method does not flow back to the caller, so the test
  body never sees it; H2 confirmed in the code, T3 in part 4) and T1, T2, D3 (S5.6.5 builds `Confinement.Planned` by
  hand, so it tests the consultant's `Build` but not the planner the spec's Given names). In ten cases a renamed
  test caught its mutant (T4, O2, O3, S4 one each, D1 two, D2 four) — checked in each mutant's `killedBy`.
- **Reading**: the mutants confirm the judge's blind spot (H2 scored 8 and 7 with a test that cannot fail) and show that
  every arm's test suite, as a whole, catches each broken decision. They cover six decisions only, so they cannot see a
  scenario arm that was never written — which is where D failed.

## 9. Predictions against observations

| Recorded before the run | Observed | |
|---|---|---|
| V's step 1 takes 1.2–1.6× C's | V 32 min, C2 32, C 16 (+14) — confounded by the crash | not testable |
| V costs 0.8–1.1× C | 0.90× C, 1.07× C2; inside the 4-arm band | holds |
| V's Claude output 20–40 % lower | 120k vs C 116k, C2 102k | **wrong** |
| V gets more findings; the judge prefers C or rates them equal | absolute: equal (8 vs 7–8) | "equal" holds |
| VT's step 1 takes 1.3–2× V's | 43 vs 32 min (1.3×) | holds |
| VT sends fewer requests than V | 6 and 6 | **wrong** |
| More of VT's drafts compile without a fix | no | **wrong** |
| VT's quality equal to V's or better | absolute: equal (8) | holds (the group judge said worse) |
| VT's cost within ±20 % of V's | −15 % | holds |
| Q: Sonnet output 30–50 % BELOW classic | 132–155k, 40–80 % ABOVE | **wrong** |
| Q: total cost inside the classic range | $8.47–9.94, above it | **wrong** |
| Q: survival > 50 % for production, < 50 % for tests | tests 50–56 %; production too few lines | not supported / not testable |
| Q: compile errors after the fill > 0 in every arm | 2–11 in every arm | holds |
| Q: quality inside the classic range | 7–8 | holds |
| H: fewer compile errors after the fill than Q | 1–4 vs 2–11 | holds |
| H: higher survival than Q, both kinds | tests 69–95 % vs 50–56 %; production 83–100 % (few lines) | holds |
| H: a smaller polish diff than Q | 63–171 vs 278–455 lines | holds |
| H: Haiku fill < $0.50 per arm | $0.05–0.07 | holds |
| H: total Claude cost ≈ Q's ($8–9) | $7.38–9.33 (mean $8.37; Q: $8.47–9.94, all above the band) — but H2 and H4 ($7.52, $7.38) are INSIDE the classic band, which no Q arm reached | partly holds — H is cheaper than Q |
| H: quality inside the classic band | 7–8 | holds |
| T: cost above the classic band, $10–14 per arm | $8.13–12.01 (mean $9.73) — above the band, lower than predicted | partly holds |
| (interim, from T1 alone: "probably near the band, $7–8") | the four average $9.73 | **wrong** — corrected |
| T: Haiku $0.5–2 per arm | $0.18–0.63 | **wrong** (cheaper) |
| T: Haiku's code survives better than in H (> 80 %) | 90–98 % | holds |
| T: the Sonnet task reviews change < 30 % of Haiku's lines | 3–36 of 487–570 lines (< 7 %) | holds |
| T: work time 60–90 min | elapsed 80–100 min | roughly holds (a little above, under parallel load) |
| T: quality inside the classic band | 7.5–8 | holds |
| O: $6–9 per arm, not below the classic band on average | $5.48–7.90; the mean $6.71 is not below the band's lower bound ($6.53), but below its mean ($7.21); O1 is below $6 | the average part holds (read against the lower bound); the range partly (O1) |
| S: $4–7 per arm | $5.03–7.49; S4 above $7 | holds for 3 of 4 |
| D: $3.5–6, the only scheme likely BELOW the classic band | $4.02–5.44, all four below the band; but O1 ($5.48) and S1–S3 are below it too | partly holds — D is the only scheme with every arm below |
| Haiku's fixes leave more findings open than Sonnet's | open: O 17 %, S 32 %, D 53 %; Sonnet fixers 13 % | holds (confounded in D: Haiku also wrote the code) |
| Quality: O and S inside the classic band, D the most likely to fall below | O 7–8, S 7.5–8; D2 6, D4 5.5 | holds |
| Mutation checks show at least one surviving mutant in the earlier 18 | no mutant survived a whole arm; per intended test, misses in H2, T1, T2, T3 (T3 the known case) | **wrong** at arm level; holds per scenario test |

## 10. Limits

> Reviewed by another vendor's model (coai consultation `52ceb2d9`, codex `gpt-6-astra`, 2026-10-26), given this
> folder. Its checkable claims were verified against the data before anything changed; it narrowed three statements
> above (§1 the noise band, §2 items 1, 2, 7 and 8) and its other points are in the limits below.

- **n = 1 per arm** for V and VT; n = 4 for classic, Q, H, T, O, S and D. One task, mostly tests, in one repository.
- **Part 5 ran a day after the classic arms**, three arms at a time; cost and tokens do not depend on load, time does.
  The classic arms were not re-run beside it, so O, S and D are compared with controls from another day.
- **The fix outcomes** (§7, [tables.md](tables.md) §9) come from the fixers' own notes, classified by one Opus session
  and hand-checked on two arms; a note can claim a fix the code does not have. The judge's scores are the check on that.
- **The fix step had the same time limit everywhere** (30 min); D4's Haiku fixer stopped at it with findings open. A
  longer limit would cost more and might close them; this run does not show how much.
- **The T arms' task reviews may be lenient by design**: a 15-minute Sonnet session reading only the task's files. Their
  few changes do not prove the code right — the final review and the judge found defects they had passed.
- **Three Windows crashes** interrupted step 1 of V, C and C2; V and C2 re-ran their test suite after resuming; C got an
  extra attempt through a harness defect ([incidents.md](incidents.md)).
- **Load**: up to 6 arms ran at once in parts 2–3; H's work times include heavy CPU contention. VT ran later, alone on the GPU.
- **The fillers saw only their prompt** — by design, and the main reason the local model guessed names.
- **The judges are Opus**, one model; the absolute rubric compresses scores near 8, so it separates "clean" from "has a
  major finding", not finer shades. Three of its major findings were checked in the code and were true.
- **Blinding leak**: the Q and H diffs contain the word "skeleton" (2–5 times, in their plan notes); the redaction did
  not cover it, so the judge could tell a skeleton arm. Its scores are close to the classic arms', but this is not proven
  to be free of that knowledge. Part 5's diffs name their coder too (D4's plan: "the Haiku coder"); the wider list used
  since T removed 0–9 such words per arm (S4: 9), but a diff's style can still tell a scheme.
- **Quality needs behavioural checks the judge cannot replace** — done in part 5 for all 30 arms (§8), and they found
  a test the judge passed. But six mutants cover six decisions of the spec; an unwritten scenario arm or a false note
  is outside them. A wider frozen set (one mutant per scenario) is the next step for any comparison like this one.
- **Token totals are session-wide** ([harness/metrics.mjs](harness/metrics.mjs)): they do not say which part of a session
  (documents, reasoning, exploration) the tokens went to; that would need transcript attribution.
- **Concurrency was capped per arm, not per machine**: two T arms with up to three tasks each meant up to six parallel
  builds; the disk's causal role in the crashes stays unproven (one crash came with no experiment load).
- **Survival** counts verbatim (whitespace-free) lines: an edited line counts as dropped, so it measures code kept as
  written, not ideas kept.
