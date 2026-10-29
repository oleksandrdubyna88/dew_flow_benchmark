# Results — can a cheaper model write the code a Sonnet coder would? (2026-10-25/26)

> Status: **parts 1–5 complete — 30 arms, all checked, judged and mutation-checked (part 5 ended 2026-10-27 05:30
> UTC). Part 5b (SR, DR ×4) is complete (§7b, ended 2026-10-27 20:03 UTC). Part 5c (CR, CH ×4) is complete (§7c, ended
> 2026-10-28 17:27 UTC). Part 6 — a second task, 9 arms (C, CR, SRS × 3), each ending in the coai code gate — is complete
> (§7d, arms ended 2026-10-29 17:47 UTC, mutation checks 20:37 UTC).**
> Every number below is in [tables.md](tables.md), generated from [data/](data/). How it was measured:
> [harness.md](harness.md). What went wrong and what was corrected: [incidents.md](incidents.md). The pre-registered
> protocol and its change log: [protocol.md](protocol.md).

## 1. The question and the arms

One real story pair of the coai project — **E5.5 + E5.6** of `dew_flow_connect_other_ais ·
todo/PLAN_vibe_and_kimi_runtimes_E5.md` (12 scenarios, Opus-written spec), from commit `b4e2ea96` — implemented 30 times
by eight schemes (plus 8 arms of two more schemes in part 5b, §7b, and 8 in part 5c, §7c), every arm ending in the same Opus review and a fix step (Sonnet fixes in parts 1–4, Haiku in part 5). Part 6 (§7d) is a different task with its own arms and tables:

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
| **Classic + a fresh Haiku review** (part 5c) | CR1–CR4 | as C (Sonnet codes and fixes); before the Opus review, the same fresh Haiku review-and-fix |
| **Classic + a fresh Haiku review, Haiku fixes** (part 5c) | CH1–CH4 | as CR, but Haiku applies the Opus review |

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

13. **Part 6 — a second, larger task, with the coai code gate at the end of every arm (§7d).** Three schemes, three runs each. C: Sonnet
    codes, Opus reviews, Sonnet fixes. CR: C plus a fresh Haiku review-and-fix before Opus. SRS: Sonnet splits into 7 tasks, Haiku codes
    each, then the same Haiku review-and-fix, then as C.
    - **Objective checks.** In the objective checks (normal temp folder) all 9 arms build, pass both suites with 0 new failures, and deliver
      43 of 43 scenarios. Every one of 72 mutants was killed. Inside the arms, the long temp folder made one test fail in the arms' own
      whole-suite runs, in all nine arms (a Unix-socket path over 108 characters); the suite counts in the tables are those of the
      objective checks.
    - **Cost, Claude only.** The coai providers' cost was not measured. So the total cost of a scheme that ends in the gate cannot be
      compared from these figures. The measured means are **$16.63 (C), $16.55 (CR) and $17.16 (SRS)**. The $17.16 includes SRS3's two
      subagents ($15.90 if the Opus and Sonnet subagents are left out). The $16.63 includes C1's Opus review of $8.75, which a limit
      interruption split in two attempts (the other arms' reviews cost $3.98–$4.76). With that review at the mean of C2's and C3's, the C
      mean is $15.16. The C/CR order then reverses, so the cost difference between C and CR is not measured. The spread inside C
      ($14.88–$19.92) is larger than the spread between schemes.
    - **Quality.** The absolute judge gave C 7.67, CR 8.50 and SRS 8.00. CR has the highest observed mean in three runs, with no major
      finding in any CR arm. But its range overlaps the other schemes, and the gap to C comes from C1 alone (6.5, two real majors). No scheme
      effect is measured. The Opus review still found 1–2 majors in every arm.
    - **The coai gate.** It gave 146 code findings: 10 real, 26 partly real, 110 not real (judge's score 2–4 of 10 per arm). The gate agent
      accepted 9 and fixed them (8 real, 1 partly real). The mutation checks did not separate the schemes.
    - **Incidents.** The overnight spend limit stopped round 1 (repaired, [incidents.md](incidents.md) §2g). SRS3's Haiku review-and-fix
      started two subagents on other models, so that step is contaminated. n = 3 per scheme: a direction, not a measured size.

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

## 7c. Part 5c — the classic scheme with a fresh Haiku review before Opus (CR, CH ×4)

> Complete: all 8 arms checked, judged, mutation-checked and hand-checked (2026-10-28, the chain ended 17:27 UTC).
> Every number is in [tables.md](tables.md) (§1–§3b, §6, §8).

- **Design** (the owner's, 2026-10-28; recorded before the run in [protocol.md](protocol.md) "Part 5c"): the classic
  arm unchanged (Sonnet codes, brief `1-code`), plus the fresh Haiku review-and-fix of part 5b before the Opus review.
  **CR**: Sonnet fixes after Opus, as in C. **CH**: Haiku fixes after Opus (`h-fix`; it differs from `3-fix` only in
  the model's name). Four rounds of two (CR_n with CH_n), 11:01–15:59 UTC. Each arm had its own notes folder, and from
  11:31 UTC every other arm's data was locked ([incidents.md](incidents.md) §2f); no CR/CH arm read another arm's work.
- **Cost** ([tables.md](tables.md) §1, §2): CR $7.39 / $8.64 / $8.66 / $7.70 (mean **$8.10**, above the classic band
  $6.53–7.85 in 2 of 4 arms); CH $7.04 / $7.09 / $6.97 / $7.33 (mean **$7.11**, all inside the band; C's mean $7.21).
  The Haiku review step: CR $1.18 / $0.54 / $0.37 / $0.46, CH $0.97 / $0.39 / $0.70 / $0.41. The fix step: Sonnet
  $0.88–1.19, Haiku $0.07–0.40 — that is the whole difference between CR and CH. Step 1 (Sonnet codes) $2.93–4.35.
- **Time** (real elapsed): CR 78 / 66 / 70 / 60 min, CH 84 / 58 / 65 / 69 min (C's work time: 44–62). The Haiku review
  step 10–20 min; the Opus review 5–17 min.
- **What the Haiku reviewer found on Sonnet's code** (its notes, counted by hand): CR 5 / 4 / 4 / 5 findings, 2 / 2 / 1 /
  3 fixed; CH 4 / 5 / 5 / 3, 1 / 0 / 2 / 1 fixed (the rest left open or wrong on a second look; CH2 changed no code).
  On Haiku's own code it found 8–14 (SR) and 21–24 (DR): Sonnet's code leaves much less for it.
- **The Opus review after it**: blocking + major per arm CR 0 / 1 / 1 / 0, CH 0 / 1 / 1 / 1 — 0.5 and 0.75 (C: 1 / 2 /
  0 / 0, 0.75). All findings CR 6 / 5 / 5 / 6, CH 3 / 4 / 5 / 5 (C: 4 / 5 / 2 / 4). CR1 had the verdict "ready".
- **The Haiku review introduced defects the Opus review then flagged** (prediction 5; checked against the step's
  commits): CR4 R2 — the Haiku review added `[Collection("unlink-hooks")]` to four test classes with a comment that
  calls the async-local `GitScratch.AfterUnlink` and `KimiHome.AfterUnlink` "process-wide" (the same false comment as
  DR1); CH1 R2 — after the Haiku review's commit, the helper `TheRowsOneShotFolder` is never called. Both minor.
- **Objective checks**: all 8 arms build, pass the format and generator checks, have 0 new test failures and deliver
  12 / 12 scenarios by exact name. CR2's build passed on the second build: the first ended in a .NET runtime crash
  ([incidents.md](incidents.md) §2f).
- **Quality** (absolute judge, two runs each): **CR 8 / 8 / 8 / 8 with 0 majors** — of the 11 schemes run four times, it and SR
  are the only ones at 8 in every arm, and it is the only one with no major at all; CH 7.5 / 7 / 8 / 8, 4 majors (C: 7 / 8 / 8 / 8, 2 majors).
  Hand-checked, all true: **CH1 and CH2 (3 of the 4)** — after a minor Opus finding (the caveat "a shell is always there"
  is false for Kimi), the **Haiku fixer** rewrote the shared "unconfined" caveat for every runtime in C# and TypeScript
  and recorded no deviation (commits `58e06eee`, `ff1de017`). CR1's and CR4's **Sonnet fixers** made the same change for
  the same finding, but recorded it as plan deviations 60 and 61, and the judge did not count it as major. The same
  over-wide Haiku fix as O2, O4 and SR3. **CH4**: deviation 39, written by Sonnet in step 1, says the coder did not read
  the selected rules in full — not caused by the Haiku steps.
- **Mutation checks** (all 8 arms): 48 of 48 mutants killed, and every intended scenario test is present by name and
  catches its mutant — no weak test and no renamed one, as in the four classic arms.
- **Reading**: on Sonnet's code the fresh Haiku review is cheap ($0.4–1.2, 10–20 min) and finds little (3–5 per arm,
  1–3 fixed); the Opus review afterwards finds about what it found in C. CR's final code scored 8 in every arm (as
  SR) and is the only scheme with no major from the judge, at about $0.9 more than C. With Haiku as the fixer (CH), the cost falls to C's level
  ($7.11) and the quality to 7–8: the Haiku fixer repeats its known over-wide fix of the shared caveat, without a
  deviation. n = 4 per scheme, one task, and the judge compresses scores near 8: a direction, not a measured size.

| Prediction (recorded before the run) | Observed | |
|---|---|---|
| 1. The Haiku step $0.3–1.0 and 10–30 min; CR $7.5–9.0 (mean above the classic band); CH $6.0–8.0 (mean inside the band, about $1 below CR) | step $0.37–1.18 (CR1 above $1.0), 10–20 min. CR $7.39–8.66, mean $8.10 (CR1 below $7.5). CH $6.97–7.33, mean $7.11, $0.99 below CR | holds, but CR1 outside two ranges |
| 2. Wall time CR and CH 60–100 min | CR 60–78, CH 58–84 | holds (CH2 58) |
| 3. Opus finds at most 1 blocking+major per arm on average in CR and CH; no clear fall from C | CR 0.5, CH 0.75 (C 0.75) | holds |
| 4. CR 7.5–8 in all 4 arms; CH no arm below 7, its mean at most 0.5 below CR's | CR 8 / 8 / 8 / 8; CH 7.5 / 7 / 8 / 8, mean 7.6 (0.4 below) | holds |
| 5. In at least 1 of 8 arms the Haiku review-and-fix changes something the Opus review then flags | CR4 R2 (a false "process-wide" comment), CH1 R2 (a helper left unused); both minor | holds |
| 6. All 48 mutants killed; at most 1 of 8 arms with an intended test that misses its mutant | 48 / 48; none | holds |

## 7d. Part 6 — a second task: C, CR and SRS, each ending in the coai code gate (3 × 3 arms)

> Complete: all 9 arms checked, judged, mutation-checked and hand-checked. The arms ended 2026-10-29 17:47 UTC, the checks 19:27, the
> mutation checks 20:37. Every number is in [tables.md](tables.md) §10, generated from [data/part6/](data/part6/). This part is a NEW task.
> Its numbers are not mixed into the tables of parts 1–5c. Do not compare a number here with a number there (other task, a larger plan,
> a coai gate at the end of every arm).
>
> **Cost in this section is Claude-only cost.** The cost of the coai providers (other vendors) was not measured. So the total cost of a
> scheme that ends in the gate cannot be compared from these figures. The tokens that the coai server reported are in the gate bullet below.

- **Question.** Does a cheaper first step change the cost, the time or the quality on a larger, real task? Every arm ends in the project's
  own review gate. The gate is coai: models of other vendors review the code, and a Sonnet agent decides what to fix.
- **Task.** Issue #878 of `dew_flow_connect_other_ais`: a model whose quota is spent is still launched and runs into its timeout. And the
  quota part of issue #765: agy's structured quota error, the `/quota` reset times, a remembered cooldown, and the card's countdown.
- **Plan.** `todo/PLAN_a_spent_quota_cools_the_model_down.md`. Fable wrote it from a Sonnet code map. It passed two coai plan gates and a
  consultation before the run. It is frozen at commit `6786582e`. It has 43 scenarios (39 in C#, 4 in node), four stories and 12 decisions.
- **Size.** Each arm wrote 2,553–3,143 changed lines. The plan estimated 1,300–1,500. Half or more of the lines are tests. Before any change,
  the whole MCP suite (13,807 tests) and the extension tests passed on the host.
- **Arms.** Three runs of each scheme. The rounds of three ran at the same time: C_n, CR_n and SRS_n.

| Scheme | Arms | Steps (model, time limit) |
|---|---|---|
| **C** | C1–C3 | Sonnet codes (180 min) → Opus reviews, read-only (40) → Sonnet fixes (60) → coai gate (Sonnet, 150) |
| **CR** | CR1–CR3 | as C, with a fresh Haiku review-and-fix (60) before the Opus review |
| **SRS** | SRS1–SRS3 | Sonnet splits the plan into 4–7 tasks (40) → Haiku codes each task (45 each; in parallel worktrees where the files do not overlap) → fresh Haiku review-and-fix (60) → Opus reviews → Sonnet fixes → coai gate. All three splits had 7 tasks |

- **The gate step.** The Sonnet agent first runs a plan round on the frozen plan. That round only unlocks the code gate. Every plan finding
  is rejected as "frozen". Then the agent runs code rounds over the whole branch. It stops at the verdict `proceed` or `good_enough`.
  It accepts a finding and fixes it (red test first), or it rejects the finding with a reason. Two providers answered every round
  (antigravity and codex). A code round asks 8 reviewers. The gate steps ran one at a time (a lock), to spare the providers' quotas.
- **Isolation** (details in [harness.md](harness.md) §9). Each arm had a folder with a random name. Other work was closed (mode 000/300).
  Each arm had its own temp folder and its own coai data folder. No brief names the scheme. A scan of the nine arms' tool calls found no
  read of another arm, of `/proc` outside the arm, or of the real repository ([incidents.md](incidents.md) §2g).
- **Interruption.** The spend limit stopped round 1 (C1, CR1, SRS1) on the night of 28 October. 14 attempts ended in the limit error
  (C1 3, CR1 4, SRS1 7). The runner marked them done by mistake. They were repaired and resumed 11 hours later. Round 1's elapsed times
  include the wait. Round 1's costs include the interrupted attempts ([incidents.md](incidents.md) §2g).
- **Contamination.** SRS3's Haiku review-and-fix started two subagents, one on Opus and one on Sonnet. $3.79 of that step's $5.35 is not
  Haiku. The step is marked contaminated in every table.
- **Objective checks.** All 9 arms build and pass the format check. The MCP suite (13,878–13,916 tests) and the TypeScript suite
  (6,539–6,540 tests) have 0 new failures in all 9 arms. These checks ran with the normal temp folder.
  - **Qualification.** Inside the arms, the temp folder was long. One test then failed in the arms' own whole-suite runs:
    `RegularFilesTests.A_fifo_a_socket_and_a_device_…`. A Unix-socket path was longer than 108 characters. All nine arms show it: the gate
    notes of C1, C3, CR2, CR3, SRS1, SRS2 and SRS3 name it, and so do the fix notes of C2 and CR1 (1 failed in each own run). In the
    objective checks it passed. The suite counts in the tables are those of the objective checks, so they include it as passed.
    "All passed" in this section means: in the objective checks, under the normal temp folder.
  - **Scenarios.** All 43 are present in all arms. 8 arms show 43 of 43 passing by name. C1 shows 42. Its S14 test is named
    `AnUnknownModel_HasNoGroup_AndEveryKnownOneHasItsKey`. It is a theory with 8 cases, and all 8 passed. The check looks for the exact
    name as a whole word, and a longer name does not match. So this is a naming artefact, not a failure. The mutation check found the test
    and it caught its mutant. The quality judge was told that a longer name is a rename, not an absence.
- **Cost and time** ([tables.md](tables.md) §10.1 and §10.2). Cost is Claude-only.

| Arm | Claude-only $ | Active min | Elapsed min | Attempts |
|---|---|---|---|---|
| C1 | $19.92 | 125 | 796 † | 7 |
| C2 | $14.88 | 157 | 157 | 4 |
| C3 | $15.08 | 142 | 142 | 4 |
| **C mean** | **$16.63** | **141** | 142–157 (round 2 and 3 arms) | 5.0 |
| CR1 | $16.27 | 119 | 779 † | 9 |
| CR2 | $15.88 | 124 | 124 | 5 |
| CR3 | $17.49 | 166 | 169 | 5 |
| **CR mean** | **$16.55** | **136** | 124–169 (round 2 and 3 arms) | 6.3 |
| SRS1 | $14.85 | 235 | 862 † | 19 |
| SRS2 | $17.53 | 231 | 190 | 12 |
| SRS3 | $19.11 ‡ | 248 | 228 | 12 |
| **SRS mean** | **$17.16** ($15.90 if SRS3's Opus and Sonnet subagents are left out) | **238** | 190–228 (round 2 and 3 arms) | 14.3 |

  ‡ SRS3's cost includes $3.79 of subagent cost (Opus and Sonnet) in its Haiku review-and-fix step.
  † Round 1 waited out the overnight spend limit. Its elapsed time is not a work time. Active minutes leave the waits out. In SRS the Haiku
  tasks of one batch ran at the same time. So SRS active minutes are a sum of work, and the elapsed time is shorter.

  - **Which figures the comparison uses.** The measured means are the primary figures: **$16.63 (C), $16.55 (CR), $17.16 (SRS)**. They
    include the contamination and the interruption. Two adjusted figures sit beside them, only to size two effects.
  - **C is raised by C1's interrupted review.** C1's Opus review cost $8.75 over two attempts: $3.46 before the limit stopped it, and $5.30
    after the resume. The other arms' reviews, in one attempt, cost $3.98–$4.76. If C1's review costs the mean of C2's and C3's
    reviews ($4.35), C1 would cost $15.52 and **C's mean would be $15.16** (C2 and C3 alone: $14.98). The measured C mean ($16.63) is $0.08
    above CR ($16.55). The adjusted C mean ($15.16) is $1.39 below it. So **the order of C and CR reverses**, and **the cost difference
    between C and CR is not measured** by these runs.
  - **SRS has a second adjustment.** Without SRS3's two subagents, the SRS mean is **$15.90** (measured: **$17.16**). The comparison above
    uses the measured $17.16. With both adjustments the means are C $15.16, SRS $15.90 and CR $16.55.
  - **The spread inside one scheme is larger than the spread between schemes.** C alone ranges over $14.88–$19.92.
  - **Mean cost by role (C / CR / SRS), Claude only.**
    - Sonnet code or split: $8.35 / $9.10 / $3.36.
    - Haiku task coding: — / — / $3.57.
    - Haiku review-and-fix: — / $1.31 / $2.92 ($1.66 if SRS3's subagents are left out).
    - Opus review: $5.82 / $3.89 / $4.04.
    - Sonnet fix: $1.25 / $1.27 / $1.62.
    - coai gate (the Sonnet agent only): $1.20 / $0.98 / $1.64.
  - **Writing is the largest item in C and CR.** It is 42–60 % of an arm. In SRS the split and the Haiku tasks cost $6.93 on average. That is
    $1.42 less than Sonnet's coding step in C. The Haiku review, the fixes and the gate then take that saving back.
  - **Time.** C and CR needed 119–166 active minutes. SRS needed 231–248 (the sum of its sessions). In rounds 2 and 3, the elapsed time is
    124–169 min for C and CR, and 190–228 min for SRS. The gate step took 15–37 min of the agent's time. The wait for the lock is not counted.
  - **C2's Opus review** ran to its hard deadline (50 min, exit 124) after it had written its review. The review is used as written.
- **The Opus review** ([tables.md](tables.md) §10.4). Checked against each arm's `review.json` and the final result of its review stream.
  - Blocking + major findings per arm: C 1 / 1 / 2, CR 1 / 2 / 2, SRS 2 / 2 / 1. The means are 1.33, 1.67 and 1.67. No arm had a blocking finding.
  - All findings per arm: C 6 / 3 / 5, CR 3 / 6 / 5, SRS 7 / 7 / 6. Every verdict was "fix-then-ready".
  - The Haiku review-and-fix before it did not lower the number of majors that the Opus review found.
  - The Haiku reviewers' own notes show what they found: 5 / 8 / 8 findings in CR1–CR3 (1 / 3 / 2 major), and 7 / 8 / 12 in SRS1–SRS3
    (3 / 4 / 3 major or blocking, by their own severity).
  - One example: C1 shipped a real defect. DeepSeek's quota key was "codex", so a DeepSeek refusal parked every Codex row. The Haiku
    review-and-fix found and fixed that defect first in CR2 and SRS1. The Opus review found it in CR1.
- **Quality: the absolute blind judge** ([tables.md](tables.md) §10.5). One Opus session per diff, an anchored rubric, 2 runs, scheme words redacted.

| Arm | Judge overall (run 1 / run 2) | Correctness | Tests | Conventions | Simplicity | Opus review: blocking + major | Judge majors: named / after the hand check | coai findings | Real / partly / not real | coai judge score | Accepted by the gate agent (real) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| C1 | **6.5** (7 / 6) | 7.0 | 7.5 | 8.0 | 7.5 | 1 | 3 / 2 | 11 | 0 / 1 / 10 | 2 | 0 (0) |
| C2 | **8.5** (8 / 9) | 8.5 | 8.5 | 8.5 | 8.0 | 1 | 0 / 0 | 11 | 1 / 2 / 8 | 3 | 1 (1) |
| C3 | **8.0** (8 / 8) | 8.5 | 8.0 | 8.0 | 8.0 | 2 | 0 / 0 | 17 | 3 / 3 / 11 | 4 | 3 (3) |
| **C mean** | **7.67** (7.67 / 7.67) | 8.00 | 8.00 | 8.17 | 7.83 | 1.33 | 3 / 2 (total) | 39 | 4 / 6 / 29 | 3.00 | 4 (4) |
| CR1 | **8.5** (9 / 8) | 9.0 | 9.0 | 8.0 | 8.0 | 1 | 0 / 0 | 16 | 0 / 3 / 13 | 2 | 0 (0) |
| CR2 | **9.0** (9 / 9) | 9.0 | 8.5 | 8.0 | 8.0 | 2 | 0 / 0 | 16 | 1 / 3 / 12 | 3 | 1 (1) |
| CR3 | **8.0** (8 / 8) | 8.0 | 8.0 | 8.0 | 8.0 | 2 | 0 / 0 | 15 | 0 / 4 / 11 | 2 | 0 (0) |
| **CR mean** | **8.50** (8.67 / 8.33) | 8.67 | 8.50 | 8.00 | 8.00 | 1.67 | 0 / 0 (total) | 47 | 1 / 10 / 36 | 2.33 | 1 (1) |
| SRS1 | **8.0** (8 / 8) | 9.0 | 8.0 | 7.0 | 8.0 | 2 | 2 / 0 | 15 | 2 / 2 / 11 | 3 | 0 (0) |
| SRS2 | **8.0** (8 / 8) | 8.5 | 8.5 | 8.0 | 8.0 | 2 | 1 / 1 | 18 | 1 / 5 / 12 | 3 | 1 (1) |
| SRS3 | **8.0** (8 / 8) | 8.5 | 8.0 | 7.0 | 7.5 | 1 | 0 / 0 | 27 | 2 / 3 / 22 | 3 | 3 (2) |
| **SRS mean** | **8.00** (8.00 / 8.00) | 8.67 | 8.17 | 7.33 | 7.83 | 1.67 | 3 / 1 (total) | 60 | 5 / 10 / 45 | 3.00 | 4 (3) |

  - The two runs of one arm never differ by more than one point in the overall score. The criteria columns are the mean of the two runs.
  - **The hand check opened the judge's four majors in the arm code.**
    - **C1, DeepSeek's quota key.** `DeepseekRuntime` inherits `CodexRuntime.QuotaKeyOf = "codex"`. Real, major, named in both runs.
    - **C1, the time-zone test.** `AnAgyResetWithoutAnOffsetIsReadAsUtcTests` sets `TZ` and checks a +9 h offset with no OS guard. `ci.yml`
      has a Windows job that runs the suite, and .NET on Windows ignores `TZ`. Real, major, named in run 2.
    - **SRS1, a false plan note.** The plan says `QuotaCooldowns` is a partial class in two files. The code has one 409-line file. Real, but
      minor: a false record and 9 lines over the size rule, with no behaviour defect.
    - **SRS2, `FirstOf`.** It asks for the reset from the first refusal with `Spent == true`, else from `asking[0]`. So a hopeless refusal
      that follows a transient one is not read. Real, a major edge case, named in run 2.
    - After the check, the distinct majors are C 2 (both in C1), CR 0 and SRS 1.
  - **SRS1's false note has a cause in the gate step.** The coai plan round named the partial class. The gate agent saw a code defect and
    merged the two files (`c768433a`). Its brief said not to change the plan file, so the plan's deviation note stayed false. The judge saw
    the final code and the final note.
  - **CR has the highest observed mean (8.50), in three runs, and no scheme effect is measured.** The CR range (8.0–9.0) overlaps the C2
    and C3 arms (8.5 and 8.0) and every SRS arm (8.0). The gap to C (7.67) comes from C1 alone (6.5, two real majors). No CR arm had a
    major from the judge. In part 5c, the four CR arms also had 0 majors and scored 8 each, so none of the 7 CR arms on two tasks had a
    major. With three arms per scheme, this is a direction and not a measured size.
  - SRS has the lowest conventions mean (7.33).
- **The coai gate** ([tables.md](tables.md) §10.6). 146 code findings came in 11 code rounds. The 49 plan findings were all rejected as frozen.
  - Every first code round gave 6–18 findings, so at least 3 in every arm. C3 and SRS3 had a second round. Every code round ended
    `proceed`, except SRS1's, which ended `good_enough`.
  - The gate agent accepted 3 findings in C3 and in SRS3, 1 in each of C2, CR2 and SRS2, and none in C1, CR1, CR3 and SRS1.
  - A blind judge rated the findings. It used one Opus session per arm. It settled each finding in the code at its commit. It did not see
    the gate agent's decisions. Result: **10 real, 26 partly real, 110 not real**. By scheme: C 4 / 6 / 29, CR 1 / 10 / 36, SRS 5 / 10 / 45.
    The set score is 2–4 of 10 (mean 2.78).
  - The judge's summaries say the same for every arm. The "not real" findings ask for something the frozen plan decided against (for
    example a cache, a size cap, or a different key), or they misread the code. The judge also named 6 important defects that the review
    missed (CR1 1, SRS2 3, SRS3 2).
  - The gate agent **accepted 9 of the 146 findings: 8 were real and 1 was partly real; none was "not real"**. It accepted 8 of the 10 real
    findings. The two it rejected are SRS1's minor performance remarks.
  - The 10 real findings are mostly small. By the judge's severity, 2 are major: a `null` entry in the cooldown file escapes the fail-open
    read (C2), and an exception in `Task.Run(Apply)` escapes the round (SRS2). 8 are minor (a docs gap, a `partial` class, serial `/quota`
    probes, and similar).
  - The Opus review had not named any of the 10 as such. In C3 it had named the cancellation case of one of them (R4). SRS2's came from the
    fix of the Opus review's own R4.
  - **Gate cost, Claude only:** $0.84–$2.00 of Sonnet per arm, 15–37 min of the agent.
  - **Gate cost of the providers: not measured.** The server reports tokens, not dollars, and one pair for the whole round, not one per
    provider. The reported tokens in (and out) per arm: C1 731,856 (16,775), C2 815,085 (112,285), C3 1,449,751 (60,130); CR1 737,391
    (17,069), CR2 728,849 (17,250), CR3 730,147 (17,492); SRS1 752,881 (37,129), SRS2 741,698 (19,647), SRS3 1,352,678 (32,219). The total
    is 8,040,336 in and 329,996 out. A code round is about 606,000–718,000 tokens in. The arms with a second round (C3, SRS3) have about double.
    These are tokens of other vendors' models. They cannot be turned into a cost here.
- **Mutation checks** ([tables.md](tables.md) §10.7). Eight mutants were frozen on 2026-10-29, before any ran. **72 of 72 were killed**, and
  0 were not applicable. Each mutant breaks one decision of the plan in an arm's final code: the ladder reads the words not the verdict,
  agy `RESOURCE_EXHAUSTED` alone is "spent", the agy table keys Argon as Gemini, a torn file throws, an `Ok` does not lift, an `Ok` beats a
  spent refusal, the earlier reset wins, and a cooling row still runs. Three notes:
  - In **SRS2 and SRS3, the test with S34's name does not catch M3**. It checks the report by group, not the model-to-key table. Three other
    tests with other names killed M3. So those arms hold the guard under another name.
  - In **C1, the S14 test has a longer name** (the naming artefact above). It caught M3.
  - For M5, two tests killed the mutant in **CR1, CR2, CR3 and SRS1**: S25's test, and a source-scan test (`EveryBudgetInAClockedClass…`)
    that fails when the mutant adds a clock read in a clocked class. The second kill is not a behavioural one. S25's test also failed, so the
    kill stands.
  - The mutants did not separate the schemes.
- **What this shows.** All three schemes deliver the plan on this task. In the objective checks (normal temp folder) all 9 arms build, pass
  the format check and both suites, deliver 43 of 43 scenarios, and every one of 72 mutants was killed. The measured Claude-only means are
  within $0.6 of each other ($16.55–$17.16), and the spread inside one scheme is larger. The ordering of C and CR on cost reverses once C1's
  interrupted review is adjusted, so the cost difference between C and CR is not measured. SRS needs the most time (190–228 min elapsed
  against 124–169 for C and CR) and shows no quality gain. The Haiku review-and-fix did not lower the Opus review's major findings, but
  it found real defects (DeepSeek's key in CR2 and SRS1). The gate step costs about $1 of Sonnet per arm, and its reviewers' cost is not measured.
  It found a few small real defects (8 accepted and fixed) among many findings that are wrong or against the plan (110 of 146).
- **What it does not show.**
  - n = 3 per scheme. The ranges overlap, and C1 alone moves the C mean.
  - One task, one repository.
  - One judge model (Opus, 2 runs). It compresses scores near 8. It scored 8 or 9 for arms with 4–9 minor findings.
  - One judge of the coai findings. It worked in each arm's clone. Commit messages and plan notes were visible, and the scheme words were
    not redacted there as in the diffs of the quality judge.
  - The cost leaves out the coai providers. The total cost of a scheme with the gate is not known.
  - Round 1's costs include a limit interruption.
  - SRS3's Haiku review is contaminated. Its cost and its 12 findings are not those of a clean Haiku review.
  - The arms of a round ran at the same time on one host, so times carry load.

| Prediction (recorded before the run, [harness/part6/PROTOCOL.md](harness/part6/PROTOCOL.md)) | Observed | |
|---|---|---|
| P1. Cost per arm (Claude only): C $18–33, CR $20–35, SRS $16–30; CR's mean about $1–3 above C's | C $14.88–19.92 (only C1 in range), CR $15.88–17.49 (all below), SRS $14.85–19.11 (SRS2 and SRS3 in range). CR's mean $16.55 against C's $16.63: $0.08 below. With C1's interrupted review adjusted, C's mean is $15.16 and CR is $1.39 above | **wrong** for the cost level (all lower than predicted); the CR-above-C part is **not decided** (it depends on the adjustment) |
| P2. Elapsed per arm, gate included, 3–6 h; the gate step 30–120 min | Elapsed (rounds 2 and 3): C 142–157 min, CR 124–169, SRS 190–228: 2 of 6 arms in 3–6 h (SRS2, SRS3). Round 1 cannot be read (overnight wait; active time 119–235 min). Gate step 15–37 min: 1 of 9 arms at 30 min or more (C3) | **wrong** (shorter than predicted) |
| P3. Opus review's blocking + major per arm: C 1–3; CR at most C's; SRS at least C's | C 1 / 1 / 2 (in range), CR mean 1.67 against C's 1.33, SRS 1.67 | C holds, SRS holds, **CR wrong** (CR is above C) |
| P4. The coai gate's first code round gives ≥ 3 findings in every arm; the judge rates at least half of all coai findings real; in at least 2 of the 3 schemes coai finds a real defect the Opus review did not name | First rounds: 6–18 findings. Real: 10 of 146 (7 %); real or partly: 36 of 146 (25 %); the best arm (C3) 6 of 17. A real finding the Opus review did not name: in C (C2, C3), CR (CR2) and SRS (SRS1, SRS2, SRS3) | parts 1 and 3 hold, **part 2 wrong** |
| P5. Judge: C and CR 7–8 in every arm; SRS 6.5–8 | C 6.5 / 8.5 / 8.0, CR 8.5 / 9.0 / 8.0, SRS 8.0 / 8.0 / 8.0 | SRS holds; C and CR: 4 of 6 arms outside 7–8 (C1 below; C2, CR1, CR2 above) — **wrong** |
| P6. Scenarios by exact name: C and CR ≥ 40 of 43 in every arm; SRS ≥ 38 | 43 present in all 9; 43 passing in 8, 42 in C1 (a naming artefact) | holds |
| Mutation checks (written in `mutate6.mjs` before the run): M1, M2, M3, M7, M8 killed in all 9 arms; survivors most likely at M4–M6; 7–8 of 8 killed per arm; no scheme difference beyond one mutant; at most 2 not applicable | 8 of 8 killed in all 9 arms; no survivor; 0 not applicable; no scheme difference | the kills hold; **the expected survivors at M4–M6 did not appear** |

## 8. Behaviour — the mutation checks on all 30 arms

> Part 6 has its own frozen mutants (8, on another task): §7d, [tables.md](tables.md) §10.7.

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

> The predictions of parts 5b, 5c and 6 are in §7b, §7c and §7d, beside their results.

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
- **Part 6** (§7d): n = 3 per scheme, one task, one repository. The cost is Claude-only: it leaves out the coai providers (other vendors), whose cost was not measured (only tokens, §7d). Round 1 was
  interrupted by the overnight spend limit: its elapsed times contain the wait, and its costs contain the interrupted attempts. SRS3's Haiku
  review-and-fix started two subagents (Opus and Sonnet; $3.79 of its $5.35): it is not a Haiku review. C1's Opus review was split by the limit and cost $8.75 (the other arms' $3.98–$4.76), which raises the C mean to $16.63 ($15.16 adjusted). One judge model scored the code
  (2 runs, compressed near 8), and one judge model rated the coai findings from inside each arm's clone, where the scheme was not redacted.
  The three arms of a round ran at the same time on one host; the gate steps ran one at a time. The mutants cover 8 decisions of 12.
  The arms' own whole-suite runs showed one failing test in all nine arms (a Unix-socket path longer than 108 characters under the per-arm temp folder, an effect
  of the isolation). The objective checks ran with the normal temp folder, and that test passed there; the suite counts in the tables are from those checks.
- **Survival** counts verbatim (whitespace-free) lines: an edited line counts as dropped, so it measures code kept as
  written, not ideas kept.
