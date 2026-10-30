# Cheaper code writers under a Sonnet coder — local model, Haiku (2026-10-25/27)

> Status: **parts 1–5 complete — 30 arms, all checked, judged and mutation-checked (2026-10-27 05:30 UTC). Part 5b (8 more arms) complete at 20:03 UTC — [results.md](results.md) §7b. Part 5c (8 more arms) complete 2026-10-28 17:27 UTC — §7c. Part 6 (a second task, 9 arms, each ending in the coai code gate) complete 2026-10-29 17:47 UTC — §7d. Part 7 (CHH: two focused Haiku reviewers, 3 arms on the same task) complete 2026-10-30 04:28 UTC — §7e. Part 6's costs were corrected on 2026-10-30 ([incidents.md](incidents.md) §2h).** Owner-requested experiment, run in `dew_flow_connect_other_ais` (coai) from a session in
> `dew_flow_conventions`; recorded here because this repository is where the family keeps its measurements.

## The question

On one real coding task of the coai project, can a cheaper model do part of a Sonnet coder's writing — and does that
change the **time**, the **cost** (in total and per agent), the **quality** or the **tokens**? Seven ways were tried,
each against the project's own scheme (Sonnet writes, Opus reviews, Sonnet fixes), which was itself run four times to
measure its noise:

| Scheme | Arms | Who writes the code |
|---|---|---|
| Classic | C, C2, C3, C4 | Sonnet |
| vLLM drafts | V (thinking off), VT (thinking on) | a local Qwen3.6-35B-A3B (vLLM, AMD R9700) drafts each method; Sonnet integrates |
| Skeleton + Qwen | Q1–Q4 | Sonnet writes signatures, types, data and instructions; Qwen fills every body between markers; Sonnet polishes |
| Skeleton + Haiku | H1–H4 | the same, with Haiku filling |
| Opus tasks → Haiku → Sonnet per task | T1–T4 | Opus splits into 4–6 tasks with exact contracts; Haiku codes each as a full agent; Sonnet reviews each; parallel where files do not overlap |
| Opus tasks → Haiku, one Opus check | O1–O4 | as T, without the per-task reviews; Haiku also fixes |
| Sonnet tasks → Haiku, one Opus check | S1–S4 | as O, with Sonnet writing the split |
| Haiku directly | D1–D4 | no split: Haiku codes the stories from the classic brief and fixes |

Task: stories E5.5 + E5.6 of `dew_flow_connect_other_ais · todo/PLAN_vibe_and_kimi_runtimes_E5.md`, 12 scenarios, from
commit `b4e2ea96`. Every arm ends with the same Opus review and a fix step (Sonnet; Haiku in O, S, D), and is a branch
`exp/kimi-e56-*` there. Haiku has no role in the project's model rules; its use is the owner's recorded deviation.

**Part 6 is a different task and has its own arms.** The coai issues #878 and the quota part of #765, as a frozen plan with 43 scenarios
(`todo/PLAN_a_spent_quota_cools_the_model_down.md` at `6786582e`). Three schemes, three runs each, and every arm ends in the coai code gate
(models of other vendors review the code, a Sonnet agent decides what to fix):

| Scheme | Arms | Who does what |
|---|---|---|
| C | C1–C3 | Sonnet codes, Opus reviews, Sonnet fixes, then the coai gate |
| CR | CR1–CR3 | as C, with a fresh Haiku review-and-fix before the Opus review |
| SRS | SRS1–SRS3 | Sonnet splits the plan into 7 tasks, Haiku codes each task, the same fresh Haiku review-and-fix, then as C |
| CHH (Part 7, the same task) | CHH1–CHH3 | Sonnet codes, then two fresh read-only Haiku reviewers (A: each plan decision at every site; B: do the tests test what they claim, on every CI machine), Sonnet fixes both, then as C |

## The answer (details and conditions in [results.md](results.md))

- **In parts 1–4 no quality difference was resolved**: every arm builds and passes the suites, and all score 7–8 of 10
  with an absolute blind Opus judge — a judge that compresses scores (two tests proven unable to fail still scored
  7–8), so this is not proof of equal quality — the same range as the four classic runs.
- **The classic scheme's own noise is wide**: $6.53–7.85 and 44–62 min across four identical runs.
- **vLLM drafts gave nothing measurable**: inside the classic band; the drafts guessed existing API names and were mostly
  rewritten.
- **Skeleton + Qwen cost more** ($8.47–9.94): the fill took minutes and $0, but never compiled, so the polish was a second
  full writing pass. About half of Qwen's test lines survived.
- **Skeleton + Haiku: a far better filler, still no saving** ($7.38–9.33): Haiku's fill cost $0.05–0.07, 69–95 % of its
  test lines survived and the polish shrank to a third of Q's — but the skeleton step ($2.1–3.0) and the review remain.
- **Opus tasks → Haiku → Sonnet per task is the most expensive scheme** ($8.13–12.01, mean $9.73) at the same quality:
  Haiku wrote nearly everything for $0.18–0.63 per arm and 90–98 % of it stayed, but the Opus split ($2.1–5.9) and the
  final review remain, and the cheap per-task reviews passed defects the final review and the judge found.
- **Part 5 — the cheapest schemes.** Without the per-task reviews, the task split reaches the classic band: **O (Opus
  splits) $5.48–7.90, S (Sonnet splits) $5.03–7.49** — three S arms below every classic arm — at the classic quality
  (7–8), but slower (62–107 min). **Haiku with no split (D) is the cheapest, $4.02–5.44, and the only scheme with arms that
  lost quality**: two of four scored 6 and 5.5, with scenario arms not written (the other two 7.5 and 8), and its Haiku
  fixer left half the review's findings open.
- **Mutation checks on all 30 arms** (six frozen mutants each): every mutant is caught by some test, but five arms have
  a scenario test that cannot catch its mutant — one of them (H2) passed the review, the fixer and the judge.
- **Part 5b — S and D with a fresh Haiku review before Opus**: the step costs about $0.5–0.7 and 12–24 min
  per arm. It lifted D's quality (no DR arm below 7; the judge's majors 12 → 5) at $4.80–6.68 per arm, but the Opus
  review still found majors in every DR arm. SR stays at 8, $6.23–7.47. DR3 and DR4 are not independent: their Haiku
  reviewers read the previous arm's notes ([incidents.md](incidents.md) §2e).
- **Part 5c — the classic scheme with a fresh Haiku review before Opus**: with Sonnet fixing (CR) every arm scored 8
  with no major from the judge, at $7.39–8.66 (mean $8.10, about $0.9 above C); with Haiku fixing (CH) $6.97–7.33 (mean
  $7.11, C's level) and 7–8, its Haiku fixer again rewriting the shared caveat without a deviation. The Haiku review
  found 3–5 issues per arm on Sonnet's code. Other arms' data was locked during the run; an older leak (T2 read T1's
  fix notes) was found by the same scan ([incidents.md](incidents.md) §2f).
- **Part 6 — a second, larger task with the coai code gate** (9 arms; [results.md](results.md) §7d): in the objective checks (normal temp folder) all 9 arms build, pass both
  suites, and deliver 43 of 43 scenarios, and every one of 72 mutants is killed (inside the arms, one test failed in their own runs because of the long temp folder). The measured
  Claude-only means, **corrected on 2026-10-30**, are **$15.48 (C), $16.30 (CR) and $17.09 (SRS; $15.83 if SRS3's Opus and Sonnet subagents are left out)**. The first count added every attempt of a
  resumed step and over-counted (it gave $16.63, $16.55 and $17.16, and a wrong reading that C was dear because of the spend limit); a resumed attempt reports its session's running total
  ([incidents.md](incidents.md) §2h). C has the lowest mean, but with three arms each **no cost difference between the schemes is measured** (Welch's t 0.55–1.15), and the spread inside one scheme is as large.
  The coai providers' cost was not measured, so the total cost of a scheme that ends in the gate cannot be compared.
  The absolute judge gave C 7.67, CR 8.50 (the highest observed mean in three runs; no major finding in any CR arm, but the ranges overlap and no scheme effect is measured) and SRS 8.00; one C arm (6.5, two real majors) makes the gap to C. The Opus review still
  found 1–2 majors in every arm. The coai gate gave 146 code findings, of which a blind judge rated 10 real, 26 partly real and 110 not real; the
  gate agent accepted and fixed 9 (8 real). An overnight spend limit stopped round 1 (repaired, [incidents.md](incidents.md) §2g), and SRS3's Haiku
  review-and-fix started two subagents on other models, so that step is marked contaminated. n = 3 per scheme: a direction, not a measured size.
- **Part 7 — CHH, two focused Haiku reviewers, on the same task as Part 6** (3 arms; §7e). **The focuses were drawn from Part 6's misses on this same task, so the test favours CHH.**
  Haiku A named, in each arm, a defect that an arm of Part 6 had shipped (DeepSeek's quota key, a multi-line `AGY_ERROR`, a transient refusal that hides a hopeless one), and the fixer fixed it; Haiku B found 11 findings about tests
  (6 major, mostly tests without teeth) and none about a CI machine. The Opus review then found 1 / 1 / 0 blocking + major; the judge gave 8, 8 and 7 (the 7 rests on a load-dependent suite failure that the hand check did not confirm); all 24 mutants were killed; the gate
  agents accepted 0 of 46 findings. Claude-only cost $23.52 / $20.73 / $19.77, mean **$21.34**: $5.86 above C and $5.04 above CR (both larger than the spread of the runs) and $4.25 above SRS (not measured).
  CHH1 waited 3 h 16 min for the owner's spend limit (active minutes leave it out).
- **The money is in reading and reviewing**, not writing: Sonnet's cache reads (tens of millions of tokens per arm) and the
  Opus review (25–47 % of an arm in parts 1–4). A cheaper writer did not move those while a Sonnet session still wrote or
  polished the code; part 5 removed that session, and its cost fell — the Opus review is then 34–68 % of an arm.

## Files

| File | What it holds |
|---|---|
| [results.md](results.md) | The findings, per part; predictions against observations; limits |
| [tables.md](tables.md) | Every number — generated from `data/` by `harness/make-tables.mjs`; never edited by hand |
| [protocol.md](protocol.md) | The protocol as kept during the run: design and predictions written before each part, every change appended with its time |
| [incidents.md](incidents.md) | Three Windows crashes and a disk logging bad blocks; every harness defect that touched a number, and its correction |
| [harness.md](harness.md) | How it ran: isolation, the pipelines, resume after a crash, the fillers, how each metric is computed, the judges, how to re-run |
| [data/](data/) | Per arm: `timing.jsonl` (every step attempt), `objective.json` (+ the uncorrected `objective.raw.json`), `survival.json`, `fill-*.json`, `fill-state.json` (the filler's exact code per region), `filler-requests.jsonl`, the coder's and fixer's notes and the step-2 review; `mutation.json` (part 5's mutation checks, every arm); the task arms' task lists and per-task diffs; `metrics.json` (time/cost/tokens per arm, step, attempt, model); `fix-outcomes.json` (what each fixer did with each review finding); `judges/` (both group judges and the absolute judge: keys, every verdict); `fillers/` (V's and VT's prompts and drafts); **`part6/`** (Part 6, and the cost sheet of all 12 arms: `metrics6.json`, with `metrics6.v1-summed-attempts.json`, the first, wrong count; the absolute judge's scores, key, hand check and verdicts, the coai findings and the judge of them, and per arm the objective checks, the Opus review, the gate summary and the mutation checks, and the tokens the coai server reported per round); **`part7/`** (Part 7: per CHH arm the objective checks, the Opus review, the two Haiku reviews (`hA.json`, `hB.json`) with the fixer's notes, the gate summary and the mutation checks (CHH3's spoiled first run too); the judge's scores, key, hand check and verdicts; the coai findings, the judge of them and the tokens reported; `haiku-outcomes.json`) |
| [harness/](harness/) | The scripts and briefs exactly as used; [harness/part6/](harness/part6/) holds the scripts of Parts 6 and 7 (they ran in another folder) |
| [data/raw/](data/raw/README.md) | **Every raw record**, compressed (41 MB for parts 1–4, 23 MB for part 5, 25 MB for Part 6, 9 MB for Part 7): every session's full event stream and exact prompt, all session transcripts, the checks' full logs and test reports, every filler prompt and answer, the judges' complete sessions, all notes and logs — with checksums; the owner's email addresses masked, nothing else changed |
| [data/reviews/](data/reviews/) | The other-vendor reviews of this folder, verbatim: the consultation (codex gpt-6-astra) and the document gate's two rounds on `results.md` (findings, decisions, the reviewers' notes) |

Not kept here: the arms' clones (the branches `exp/kimi-e56-*` in `dew_flow_connect_other_ais` hold every arm's commits).
