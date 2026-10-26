# Results — can a cheaper model write the code a Sonnet coder would? (2026-10-25/26)

> Status: **parts 1 and 2 complete; part 3 (H1–H4, Haiku as the filler) running when this was written — its numbers are
> in [tables.md](tables.md) once it ends, and §5 is completed then.**
> Every number below is in [tables.md](tables.md), generated from [data/](data/). How it was measured:
> [harness.md](harness.md). What went wrong and what was corrected: [incidents.md](incidents.md). The pre-registered
> protocol and its change log: [protocol.md](protocol.md).

## 1. The question and the arms

One real story pair of the coai project — **E5.5 + E5.6** of `dew_flow_connect_other_ais ·
todo/PLAN_vibe_and_kimi_runtimes_E5.md` (12 scenarios, Opus-written spec), from commit `b4e2ea96` — implemented 14 times
by four schemes, every arm ending in the same Opus review and Sonnet fixes:

| Scheme | Arms | What writes the code |
|---|---|---|
| **Classic** (the project's rule) | C, C2, C3, C4 | Sonnet writes it all |
| **vLLM drafts** | V (thinking off), VT (thinking on) | the local model (Qwen3.6-35B-A3B on vLLM) drafts each method; Sonnet integrates and fixes |
| **Skeleton + Qwen** (the owner's scheme) | Q1–Q4 | Sonnet writes signatures, types, data and per-region instructions + context packs; a script has Qwen fill every body ≥ 4 lines between markers; Sonnet polishes |
| **Skeleton + Haiku** | H1–H4 | the same, with Haiku filling |

The four classic arms are ONE configuration run four times: their spread is the **noise** every other arm is read
against. All models at effort `high`, named by alias; the hardware and the runs' timing are in [harness.md](harness.md).

## 2. What the run shows (with its conditions)

1. **Quality was the same everywhere.** All 14 arms build, pass both suites and deliver 12/12 scenarios. The absolute
   blind judge (one Opus session per diff, anchored rubric, 2 runs) scored 8 of the 10 finished arms **8/10** and two
   **7/10** (C: an out-of-scope rewrite of a shared caveat; Q2: two major findings in run 1). The two runs never differed
   by more than one point on any criterion. The classic arms themselves span 7–8.
2. **The classic arms vary a lot**: 44–62 min of work, **$6.53–7.85**, 79–93k Sonnet output tokens. A difference
   smaller than this band is not a result of the scheme.
3. **vLLM drafts (V, VT): no gain.** Both landed inside or at the edge of the classic band on time, cost and quality.
   The local model did little of the work: 6 requests each, all early in step 1; its drafts used wrong names of existing
   APIs and were fixed heavily or rewritten. Sonnet's output did not fall (93–94k, the top of the band). Thinking on (VT)
   made step 1 slower (43 min; 22 min of GPU for 6 requests) without better drafts.
4. **Skeleton + Qwen (Q1–Q4): more expensive, same quality.** **$8.47–9.94**, above every classic arm, with 132–155k
   Sonnet output tokens (the band is 79–93k). Qwen filled every region in 2–4 minutes and the rail rejected nothing,
   but the build failed after every fill (2–11 compile errors, a lower bound), so Sonnet's polish was a full second
   writing pass (47–67k output tokens). About half of Qwen's test lines survived to the final code (50–56 %; strict
   43–55 %); there was almost no production code to fill (0–5 lines per arm).
5. **Why Q cost more — a correction of a first guess.** The first reading blamed the context packs (13–21 per arm,
   100–190 KB). Measured per step, the packs do not explain it: in the H arms with small packs (8–11k tokens) the skeleton
   step still wrote 63–70k output tokens, as much as with large ones. The skeleton step alone writes about what a classic
   coding step writes (53–85k against 51–67k: instructions, signatures, data, packs); the extra comes from the polish.
6. **Where the money goes in every scheme:** Sonnet reading the repository (cache reads, 12–39 M tokens per arm) and the
   Opus review ($2.1–3.6 per arm, 25–47 % of an arm). Moving code WRITING to a cheaper model cannot move either.

**This run does not settle:** other tasks (this one is mostly tests: E5.5 is only tests, E5.6 mostly), smaller or more
self-contained pieces, a local model given the repository (here it saw only its prompt), other local models, or the
schemes at more than one run per arm (except classic, n = 4, and Q, n = 4).

## 3. Part 1 — vLLM drafts against classic (V, VT, C, C2; then C3, C4)

- **Time and cost**: V 62 min / $7.00, VT 68 min / $5.95, against the classic band 44–62 min / $6.53–7.85 ([tables.md](tables.md) §1).
  VT's lower cost is its cheaper review and fixes — its step-2 review found only minor issues (5 minor, no major), so
  its fixes cost $0.75 — not its coding step ($3.05, the most expensive step 1 of part 1).
- **What vLLM did** ([tables.md](tables.md) §5): V — 6 requests, 13k tokens, 3 min of GPU, all `--no-think`; VT — 6 requests,
  51k tokens (mostly thinking), 22 min of GPU. Per V's and VT's own notes (in `data/arms/V/V-code.md`, `data/arms/VT/VT-code.md`):
  only two production pieces were used nearly unchanged (V's planner arm and refusal; VT's planner arm); test drafts
  were fixed heavily or rewritten; one VT production draft was discarded, and its method written first by Sonnet and
  re-prompted afterwards — a rule deviation the coder recorded.
- **Why the drafts were weak**: the prompts carried 0.3–2.5k tokens of context, while the Sonnet coder read 20+ M tokens
  of the repository. The model guessed existing names (`ProcessResult.Success`, `--home=` as one argument, an init-only
  property assigned).
- **The group judge exaggerated small differences.** One Opus session ranking V, C, C2, VT together put them C2 > V > C > VT
  in both runs (scores 9/8, 8/7, 7/6, 6/5 after the correction) — while the absolute judge, scoring each alone, gave 8, 8,
  7, 8. A ranking forces an order on near-equal items; read the absolute scores ([tables.md](tables.md) §6–7).

## 4. Part 2 — skeleton + Qwen (Q1–Q4)

- **Time** 59–63 min, inside the band; **cost $8.47–9.94**, above it. Per step ([tables.md](tables.md) §2): skeleton
  11–12 min, $2.0–2.9; the two fills 3–5 min, $0; polish 19–26 min, $2.5–3.4; review $2.1–3.0; fixes $0.7–1.6.
- **The fill** ([tables.md](tables.md) §4–5): 13–19 test regions and 0–2 production regions per arm, all filled, none
  rejected; 13–21 requests per arm, 103–121k prompt tokens but only 3–5k completion tokens (whole files in, one body
  out), 2–3 min of GPU. The build failed after every fill: in Q1 one region declared a class inside a method body (the
  rail cannot see that), in the others ordinary compile errors.
- **Survival**: 50–56 % of Qwen's test lines in the final code. Hand-checked: a low value can be a real rewrite (Q1
  `s552`, 4/17 kept: Sonnet inlined and restructured) or a move (Q2's helper class moved unchanged to another file — the
  reason the measure looks in every changed file).
- **Quality**: 8/10 for Q1, Q3, Q4 and 7/10 for Q2 — the classic band's range.

## 5. Part 3 — skeleton + Haiku (H1–H4)

*Running at the time of writing.* Known so far (the fills are complete): 13–18 test regions and 0–2 production regions per
arm, all filled, none rejected; the build still failed after every fill, with **1–4 compile errors** (Q: 2–11); Haiku
cost **$0.05–0.07 per arm** for the whole fill. The final time, cost, survival and quality are filled in here from
[tables.md](tables.md) when the arms, their checks and their judge sessions end.

## 6. Predictions against observations

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
| Q: survival > 50 % for production, < 50 % for tests | tests 50–56 %; production too few lines to read | not supported / not testable |
| Q: compile errors after the fill > 0 in every arm | 2–11 in every arm | holds |
| Q: quality inside the classic range | 7–8 | holds |
| H: fewer compile errors after the fill than Q | 1–4 vs 2–11 | holds (fills only, so far) |
| H: Haiku fill < $0.50 per arm | $0.05–0.07 | holds |
| H: survival higher, polish smaller than Q; total cost ≈ Q; quality in the band | — | §5, when the arms end |

## 7. Limits

- **n = 1 per arm** for V and VT; n = 4 for classic, Q and H. One task, mostly tests, in one repository.
- **Three Windows crashes** interrupted step 1 of V, C and C2; V and C2 re-ran their test suite after resuming; C got an
  extra attempt through a harness defect ([incidents.md](incidents.md)).
- **Load**: up to 6 arms ran at once in parts 2–3; work times there include CPU contention. VT ran later, alone on the GPU.
- **The local model saw only its prompt** — by design (the same for Haiku), and the main reason its drafts guessed names.
- **The judges are Opus**, one model; the absolute rubric compresses scores near 8, so it separates "clean" from
  "has a major finding", not finer shades.
- **Survival** counts verbatim (whitespace-free) lines: an edited line counts as dropped, so it measures code kept as
  written, not ideas kept.
