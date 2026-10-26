# Results — can a cheaper model write the code a Sonnet coder would? (2026-10-25/26)

> Status: **complete — 18 arms in four parts, all checked and judged (part 4 ended 2026-10-26 19:10 UTC).**
> Every number below is in [tables.md](tables.md), generated from [data/](data/). How it was measured:
> [harness.md](harness.md). What went wrong and what was corrected: [incidents.md](incidents.md). The pre-registered
> protocol and its change log: [protocol.md](protocol.md).

## 1. The question and the arms

One real story pair of the coai project — **E5.5 + E5.6** of `dew_flow_connect_other_ais ·
todo/PLAN_vibe_and_kimi_runtimes_E5.md` (12 scenarios, Opus-written spec), from commit `b4e2ea96` — implemented 18 times
by five schemes, every arm ending in the same Opus review and Sonnet fixes:

| Scheme | Arms | What writes the code |
|---|---|---|
| **Classic** (the project's rule) | C, C2, C3, C4 | Sonnet writes it all |
| **vLLM drafts** | V (thinking off), VT (thinking on) | the local model (Qwen3.6-35B-A3B on vLLM) drafts each method; Sonnet integrates and fixes |
| **Skeleton + Qwen** (the owner's scheme) | Q1–Q4 | Sonnet writes signatures, types, data and per-region instructions + context packs; a script has Qwen fill every body ≥ 4 lines between markers; Sonnet polishes |
| **Skeleton + Haiku** | H1–H4 | the same, with Haiku filling |
| **Opus tasks → Haiku → Sonnet per task** | T1–T4 | Opus splits the stories into 4–6 tasks with exact contracts; Haiku implements each task as a full agent; Sonnet reviews and fixes each task; tasks run in parallel where their files do not overlap |

The four classic arms are ONE configuration run four times: their spread is the **noise** every other arm is read
against. All models at effort `high`, named by alias; the hardware and timing are in [harness.md](harness.md).

## 2. What the run shows (with its conditions)

1. **Quality was the same everywhere.** All 18 arms build and pass both suites; 17 deliver 12/12 scenarios by exact name
   (T4 delivered S5.6.6 under another test name). The absolute blind judge (one Opus session per diff, anchored rubric, 2
   runs) scored 13 arms 8/10, H2, T3 and T4 8 and 7, and C and Q2 7/10.
   The two runs never differed by more than one point on any criterion; the classic arms themselves span 7–8.
2. **The classic arms vary a lot**: 44–62 min of work, **$6.53–7.85**, 79–93k Sonnet output tokens. A difference
   smaller than this band is not a result of the scheme.
3. **vLLM drafts (V, VT): no gain.** Inside or at the edge of the classic band on time, cost and quality. The local model
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
   per arm). But the Opus split cost $2.1–5.9 — Opus wrote 50–100k tokens of task documents, much of the code inside them
   — and the final Opus review still found 4–9 issues per arm (0–3 major), some of which the task reviews had passed (§6).
8. **Where the money goes in every scheme:** Sonnet reading the repository (cache reads, 20–39 M tokens per arm) and the
   Opus review ($2.1–3.8 per arm, 25–47 % of an arm). Moving code WRITING to a cheaper model cannot move either.
9. **A defect the skeleton scheme adds:** the skeleton step writes its deviation notes into the plan, and they go stale
   after the fill and the polish; the judge found such false notes in Q2 and H3 (checked by hand: both true findings).

**This run does not settle:** other tasks (this one is mostly tests: E5.5 is only tests, E5.6 mostly), smaller or more
self-contained pieces, a filler given the repository (here it saw only its prompt), other local models, a skeleton step
made cheaper (for example by a cheaper model), or V/VT at more than one run each.

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
  verbatim). Its cost: $2.13–5.94 (T4: 101k Opus output tokens) — with the final review, the largest item of a T arm.
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

## 7. Predictions against observations

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
| H: total Claude cost ≈ Q's ($8–9) | $7.38–9.33 (Q: $8.47–9.94) | roughly holds (a little lower) |
| H: quality inside the classic band | 7–8 | holds |
| T: cost above the classic band, $10–14 per arm | $8.13–12.01 (mean $9.73) — above the band, lower than predicted | partly holds |
| (interim, from T1 alone: "probably near the band, $7–8") | the four average $9.73 | **wrong** — corrected |
| T: Haiku $0.5–2 per arm | $0.18–0.63 | **wrong** (cheaper) |
| T: Haiku's code survives better than in H (> 80 %) | 90–98 % | holds |
| T: the Sonnet task reviews change < 30 % of Haiku's lines | 3–36 of 487–570 lines (< 7 %) | holds |
| T: work time 60–90 min | elapsed 80–100 min | roughly holds (a little above, under parallel load) |
| T: quality inside the classic band | 7.5–8 | holds |

## 8. Limits

- **n = 1 per arm** for V and VT; n = 4 for classic, Q, H and T. One task, mostly tests, in one repository.
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
  to be free of that knowledge.
- **Survival** counts verbatim (whitespace-free) lines: an edited line counts as dropped, so it measures code kept as
  written, not ideas kept.
