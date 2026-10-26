# Cheaper code writers under a Sonnet coder — local model, Haiku (2026-10-25/26)

> Status: **complete — 18 arms in four parts, all checked and judged (2026-10-26 19:10 UTC).** Owner-requested experiment, run in `dew_flow_connect_other_ais` (coai) from a session in
> `dew_flow_conventions`; recorded here because this repository is where the family keeps its measurements.

## The question

On one real coding task of the coai project, can a cheaper model do part of a Sonnet coder's writing — and does that
change the **time**, the **cost** (in total and per agent), the **quality** or the **tokens**? Three ways were tried,
each against the project's own scheme (Sonnet writes, Opus reviews, Sonnet fixes), which was itself run four times to
measure its noise:

| Scheme | Arms | Who writes the code |
|---|---|---|
| Classic | C, C2, C3, C4 | Sonnet |
| vLLM drafts | V (thinking off), VT (thinking on) | a local Qwen3.6-35B-A3B (vLLM, AMD R9700) drafts each method; Sonnet integrates |
| Skeleton + Qwen | Q1–Q4 | Sonnet writes signatures, types, data and instructions; Qwen fills every body between markers; Sonnet polishes |
| Skeleton + Haiku | H1–H4 | the same, with Haiku filling |
| Opus tasks → Haiku → Sonnet per task | T1–T4 | Opus splits into 4–6 tasks with exact contracts; Haiku codes each as a full agent; Sonnet reviews each; parallel where files do not overlap |

Task: stories E5.5 + E5.6 of `dew_flow_connect_other_ais · todo/PLAN_vibe_and_kimi_runtimes_E5.md`, 12 scenarios, from
commit `b4e2ea96`. Every arm ends with the same Opus review and Sonnet fixes, and is a branch `exp/kimi-e56-*` there.

## The answer (details and conditions in [results.md](results.md))

- **No quality difference was resolved**: every arm builds and passes the suites, 17 of 18 deliver 12/12 scenarios by
  exact name, and all score 7–8 of 10 with an absolute blind Opus judge — a judge that compresses scores (a test proven
  unable to fail still scored 7–8), so this is not proof of equal quality — the same range as the four classic runs.
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
- **The money is in reading and reviewing**, not writing: Sonnet's cache reads (tens of millions of tokens per arm) and the
  Opus review (25–47 % of an arm). A cheaper writer cannot move those.

## Files

| File | What it holds |
|---|---|
| [results.md](results.md) | The findings, per part; predictions against observations; limits |
| [tables.md](tables.md) | Every number — generated from `data/` by `harness/make-tables.mjs`; never edited by hand |
| [protocol.md](protocol.md) | The protocol as kept during the run: design and predictions written before each part, every change appended with its time |
| [incidents.md](incidents.md) | Three Windows crashes and a disk logging bad blocks; every harness defect that touched a number, and its correction |
| [harness.md](harness.md) | How it ran: isolation, the pipelines, resume after a crash, the fillers, how each metric is computed, the judges, how to re-run |
| [data/](data/) | Per arm: `timing.jsonl` (every step attempt), `objective.json` (+ the uncorrected `objective.raw.json`), `survival.json`, `fill-*.json`, `fill-state.json` (the filler's exact code per region), `filler-requests.jsonl`, the coder's and fixer's notes and the step-2 review; `metrics.json` (time/cost/tokens per arm, step, attempt, model); `judges/` (both group judges and the absolute judge: keys, every verdict); `fillers/` (V's and VT's prompts and drafts) |
| [harness/](harness/) | The scripts and briefs exactly as used |

Not kept here: the session streams and transcripts (large, and they hold the whole coai checkout as read by the agents),
the vLLM prompts of the skeleton arms (re-creatable: `fill-state.json` holds every answer), the judges' diff copies (the
branches hold the code).
