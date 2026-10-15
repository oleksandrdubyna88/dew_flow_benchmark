# The series on coai #751 — subagent-model policy A/B, 2026-10-15

> Status: **ran to the end, 2026-10-15; n = 3 per arm, one story, preliminary, not a verdict.** Six cells in three adjacent A/B blocks,
> from the committed seed. Attempts 1 and 2 were voided by the account's spend limit (no data); attempt 3 completed all six cells, the sixth
> after the owner raised the spend ceiling. Plan: [PLAN_subagent_policy_ab.md](../../todo/PLAN_subagent_policy_ab.md) §1 (the question);
> harness: `spikes/policy-ab-quick/` (the quick spike with a story config, not the plan's `bench policy` verbs); data:
> [policy-ab-coai-751-data.json](../bench/policy-ab-coai-751-data.json) and the codex reading
> [policy-ab-coai-751-reading-s2c.json](../bench/policy-ab-coai-751-reading-s2c.json) (commit `a685702`). Earlier record:
> [quick-pilot.md](quick-pilot.md).
>
> **One cell, c2, does not run the model the others run.** Ten events in, the CLI switched its whole session from Opus 5.5 to Opus 4.8
> after a safeguard refusal (§7). Every A-versus-B number below is given with c2 and, where it matters, without it.

## 1. The question and what was compared

The plan's §1, unchanged: for one already-merged story re-run from its pre-implementation spec, does the **new** subagent-model policy
deliver a change as good as the **old** one, and at what cost in time and money? Main session held fixed (`claude -p --model opus`),
**effort `high` for every model in every cell** (the audit log records no other level in any session, main or child), the same prompt, the
same cap ($30 per cell), the same plan file with its model words struck.

| | |
|---|---|
| Story | `dew_flow_connect_other_ais` PR #751, epic E2 of the paths-per-side plan: the registry of the sides a profile was used on (stories 2.1–2.4) |
| Parent P / reference S | `af7f2856` / `3ebf208c` |
| Arm A | the conventions' **old** rule: every subagent runs on Opus (blob `6d11121e`, the pin `bb022429` itself), no model guard |
| Arm B | the **new** role-table rule (blob `f4689729`: Sonnet scouts and coders, Opus judges, Fable cornerstones) + its model guard + the **harness's test-only main-session edit bound** |
| Edit bound | arm B only; one file and 50 lines per request, counted per tool call, a refusal after the fact. **Stricter than the shipped rule**, which makes the 50 lines an eligibility test decided before the first edit and says "No tool counts this". So B is the policy as complied with under enforcement |
| Concurrency | both prompts say independent work may be launched in one message; background tasks stay disabled |
| Cells | three adjacent A/B blocks, the lead of each block drawn from the committed seed `s2-66d73fd984bb0579` (commit `d24db9c`, before the draw): **c1 = B, c2 = A, c3 = A, c4 = B, c5 = A, c6 = B** |
| Pre-registered | `inputs/statement.md`, `checklist.json` (14 items), `holdout.json`, `prediction.md`, `seed.txt`, `treatment.md` — all committed before any cell |

What each arm receives is written down in `spikes/policy-ab-quick/stories/coai-751/inputs/treatment.md`. Both arms get one conventions base
(P's own pin), built into a one-commit repository; they differ in exactly three files (`common/subagent-models.md`,
`settings/hooks/subagent-model.mjs`, `settings/settings.json`), which `prepare-cell` asserts and a probe re-checks. The probe on the final
harness passed: both arms' trees fail the same 3 of 5805 of P's own tests, the mounts differ in those three files, the rule-load signal is
equal. The known limit of that design: B's new rule points at two things the base lacks
(`research/RESULTS_subagent_model_tiers.md`, and a section of `planning-docs.md`).

## 2. History of the run

| Attempt | Runs root | What happened | Spend |
|---|---|---|---|
| 1 | `D:/rsd/_s2` | c1 (B) ended with HTTP 429 after 2432 s, 82 turns: the account's spend limit | $6.96, **void** |
| 2 | `D:/rsd/_s2b` | c1 (B) ended with 429 after 1986 s, 73 turns; c2–c6 each ended with 429 in about 4 s on their first turn | $6.57 + $0, **void** |
| 3 | `D:/rsd/_s2c` | c1–c5 under the pre-registered $100 ceiling; c6 would have been refused by the rule (spent + cap > ceiling), so the owner raised the ceiling to $150 (`CEILING.md`, the per-cell cap stays $30) and c6 ran under `--resume --stop-after 150` | six cells, $87.30 |

**Void spend, reported apart: $13.53.** It is not in any number below. The harness learned from attempts 1 and 2: a cell that ends with the
CLI's own rate-limit markers is `void: rate limit`, stops the series, is never scored; `--resume` keeps every data point and re-runs void and
never-started cells. The resume run then re-scored the reference tree after the first run had shed it and recorded compile errors; that was a
harness defect, fixed (`bc33484`), and the reference re-scored correctly: both hold-outs pass (22/22, 85/85), own suite 5830 tests with the 3
known failures. No cell was re-run for it.

## 3. Results

Every cell ended `completed`; every cost is the CLI envelope's total. Hold-outs are `configTransfer` and `panelView`, the two
spec-implied test files; the own suite is each candidate's compiled suite judged against the reference's failing set by file and name.

| | c1 | c2 | c3 | c4 | c5 | c6 |
|---|---|---|---|---|---|---|
| Arm | B | A | A | B | A | B |
| Cost | $10.97 | **$23.51** | $12.15 | $10.94 | $16.36 | $13.37 |
| Wall (CLI duration) | 96.1 min | 54.4 | 56.1 | 70.7 | 50.0 | 80.4 |
| Turns (CLI count) | 66 | 176 | 155 | 64 | 143 | 81 |
| Children (max in one message / in time) | 6 Sonnet (3 / 3) | 0 | 1 Opus (1 / 1) | 4 Sonnet (2 / 2) | 1 Opus (1 / 1) | 5 Sonnet + 1 Opus (2 / 2) |
| Main-session edits: lines / shell-write calls | 2 / 10 | 834 / 13 | 1054 / 29 | 0 / 8 | 732 / 22 | 29 / 6 |
| Child edits: lines / shell-write calls | 870 / 55 | 0 / 0 | 0 / 0 | 1176 / 53 | 90 / 2 | 999 / 54 |
| `configTransfer` | pass 22/22 | pass | pass | pass | pass | pass |
| `panelView` | **fail 84/85** | **fail 84/85** | **fail 84/85** | **fail 84/85** | **fail 84/85** | **fail 84/85** |
| Own suite against the reference's failing set | 0 new failures | 0 | 0 | 0 | 0 | 0 |
| Own-suite tests run (reference 5830) | 5849 | 5818 | 5827 | 5852 | 5829 | 5853 |
| Codex reading: met / partial / not met (of 14) | 8 / 5 / 1 | 9 / 3 / 2 | 8 / 5 / 1 | 9 / 4 / 1 | 8 / 4 / 2 | 9 / 5 / 0 |
| R6 (the stamp is never awaited) | met | met | met | met | met | met |

Edit lines are the audit log's added plus removed lines of edit-tool calls; shell-write calls are counted, not measured in lines (the
children mostly write through the shell). Reference: both hold-outs pass, codex reading 11 met / 3 partial / 0 not met.

### 3.1 Per arm: median and range (n = 3 each)

| | A (c2, c3, c5) | B (c1, c4, c6) | B against A, medians |
|---|---|---|---|
| Cost | $16.36 (12.15–23.51) | $10.97 (10.94–13.37) | −33 % |
| Wall | 54.4 min (50.0–56.1) | 80.4 min (70.7–96.1) | +48 % |
| Turns | 155 (143–176) | 66 (64–81) | −57 % |
| Children | 1 (0–1) | 6 (4–6) | |
| Max children at once | 1 (0–1) | 2 (2–3) | |
| Lines written by the main session | 834 (732–1054) | 2 (0–29) | |
| Lines written by children | 0 (0–90) | 999 (870–1176) | |
| Codex: met | 8 (8–9) | 9 (8–9) | |
| Codex: partial | 4 (3–5) | 5 (4–5) | |
| Codex: not met | 2 (1–2) | 1 (0–1) | |

**Without c2** (A = c3, c5): A cost median $14.26 (12.15–16.36), so B is **−23 %**; A wall median 53.1 min (50.0–56.1), so B is **+52 %**;
A turns 149 (143–155). B's cost range ($10.94–13.37) lies below c5's $16.36 but overlaps c3's $12.15.

## 4. Predictions against observation

`inputs/prediction.md`, written before the seed, the draw, the probe and any cell.

| # | Predicted | Observed | Held? |
|---|---|---|---|
| 1a | Both hold-outs pass in every cell | `configTransfer` passes in all six; `panelView` fails one test (84/85) in **all six cells, both arms**, the same test every time — *"with the switch off the panel says the settings are shared, and does not tick the box"* — deterministic; the reference passes 85/85 | **refuted, for both arms alike** |
| 1b | Codex reads 10–13 of 14 met in every cell | 8–9 met in every cell; the reference itself scored 11 | **refuted** |
| 1c | No A–B quality difference detectable at n = 3 | identical hold-out results; 0 new own-suite failures in all six; codex met median A 8, B 9 (ranges 8–9 and 8–9) | **holds** (no difference detected) |
| 2a | A: main session writes most of the code, 0–2 Opus children | 0, 1, 1 Opus children; the main session wrote 732–1054 lines | holds |
| 2b | B: 2–4 Sonnet coders, at least one message launching two or more concurrently | 4–6 children (c6 also one Opus), the largest launch message held 3, 2, 2 children, max concurrency 3, 2, 2 | holds in spirit (more children than predicted) |
| 2c | B: at least one Opus review child | only c6 (*"Judge: review E2 diff"*); c1 and c4 had none | **partly** |
| 3 | B's median wall within ±25 % of A's | 80.4 against 54.4 min, **+48 %** (+52 % without c2) | **refuted** |
| 4 | B's median cost within ±15 % of A's | $10.97 against $16.36, **−33 %** (−23 % without c2) | **refuted** — and with c2 in, the pre-written *surprise* ("B cheaper than A by more than 25 %") |
| 5 | Every logged level is `high` | every logged level, main and child, in every cell | holds |
| 6 | No canary; no instrument path edited | 0 canary hits, 0 tamper findings in all six | holds |

The second surprise condition (B's quality below A's on a hold-out in two or more cells) did not occur: the hold-out results are identical.
The first one (cost) is crossed with c2 and not crossed without it (−23 % against the −25 % line).

## 5. The anatomy (observations, not proof)

Cost split by model from the envelope's `modelUsage`:

| | Main-session model | Children | Main-session cache read |
|---|---|---|---|
| c1 (B) | Opus 5.5 $5.57 | Sonnet $5.40 | 10.8 M |
| c4 (B) | Opus 5.5 $4.77 | Sonnet $6.17 | 9.2 M |
| c6 (B) | Opus 5.5 $7.91 | Sonnet $5.45 | 16.7 M |
| c3 (A) | Opus 5.5 $12.15 | one Opus child, inside that total | 31.5 M |
| c5 (A) | Opus 5.5 $16.36 | one Opus child, inside that total | 36.3 M |
| c2 (A) | **Opus 4.8** $23.41 (+ $0.09 on Opus 5.5) | none | 34.7 M |

In A the main session does the work itself: median 155 turns and about 834 lines written by it. In B the main session writes almost
nothing (median 2 lines) and the CLI counts about 66 turns, while Sonnet children write about 1000 lines; the orchestrator's own bill is $4.77–7.91
against A's $12.15–16.36, and the children add $5.40–6.17. Cheaper tokens fit the cost difference. In wall time the B cells run their children
partly one after another: the children's busy time (union of their spans) was 49, 31 and 44 minutes inside cells of 96, 71 and 80, and the
overlap of two or more children was 19, 6 and 9 minutes. Children that wait on one another, and the orchestrator's reading and review around
them, are the likely cost in time. That is a reading of one cell per slot, a hypothesis for a larger run.

## 6. Findings

- **The same hold-out failure in all six cells.** `panelView`'s "switch off" test fails in both arms. The reference passes it. It is not an
  arm effect; it is something about the statement and the checklist that no candidate satisfied. Not investigated further here.
- **Codex's not-met items** are R1 (the `stampSide` signature) in c1, c2, c3, c5 and R4 (a changed label bypasses the once-a-day limit) in
  c2, c4, c5. B's c6 had none.
- **Delegation shape in B:** scouts then coders in c1 (3 + 3), coders only in c4 (4), coders, an Opus judge and a review-fix coder in c6 (5 + 1).
- **No edit-bound denials and no guard denials** in any cell (in B the main session wrote 2, 0 and 29 lines).
- **Canary 0, tamper 0, effort not `high` 0** in every cell. Reference-only identifiers appear in every cell (19–44 hits), as in the pilot:
  names taken from the statement and from P's code.
- **Leak suspects** (37–134 per cell) are the cell's own session folders, its `%TEMP%` working folder, `npx` calls, regex noise, and reads of
  `~/.nuget/packages` in c3, c5, c6. No cell touched another cell, the reference, an earlier run's folder, the owner's other projects or any
  other clone. c2 read and wrote one rule dump under `/tmp`, outside the tree.
- **Session memory.** Cell sessions wrote per-cell project memory under `~/.claude/projects/D--rsd--s2c-c<n>-tree/memory/`: c2
  (`MEMORY.md`, `coai-extension-gate.md`, `paths-per-side-plan.md`), c3 (`MEMORY.md`, `running-the-suites-offline.md`), c6 (`MEMORY.md`,
  `offline-build-and-checks.md`). Only those per-cell folders; no later cell reads them.
- **The rule did not reach a session through its start.** The SessionStart output did not mention the rule in any cell, in either arm; no
  session opened the rule file directly; the sessions ran the resolver's `explain` / `read` 11, 10, 2, 13, 7 and 13 times (c1–c6).
- **Loaded context at the first turn:** cache written 14,058 tokens in c1 and 5,391–5,616 in the other five.

## 7. c2: the model changed under the cell

Ten events into c2 (arm A) the CLI logged a `model_refusal_fallback`: Opus 5.5's safeguards flagged the session (category `cyber`) and the
whole session, scope `session`, was answered by **Opus 4.8** from then on. The stream shows one Opus 5.5 message and 365 Opus 4.8 main-session
messages; no other cell has a fallback event. c2 is therefore a different main-session model from the other five. Its $23.51 (Opus 4.8 $23.41)
is the top of A's range, and its 176 turns the most of any cell. It is kept in every table above because the cell ran and completed, and the
medians are shown both ways; whether a refusal fallback is a property of the policy is not something this data can say.

## 8. How strong the evidence is

- The **hold-outs and the own suite** are mechanical and identical across the arms: they are the strong evidence, and they show no
  difference detected.
- The **codex reading** is weak evidence. Its calibration passed only under the pilot's rule scaled to this checklist (reference 11 of 14 met,
  the line being 75 % = 11); under the plan's 90 % line (13 of 14) the reference would not pass. The planted defect (the stamp awaited) was
  read `not_met` on R6, as required, and scored 10 met against the reference's 11. The reader also marks the same signature item R1 not met in
  four cells of both arms. Treat the R-item counts as a rough instrument.
- **Cost and time** are measured, not read; but they rest on three cells per arm, one of which (c2) ran another model.

## 9. Limits

- **n = 3 per arm**, one story, one prompt, one machine, one CLI version.
- **Arm B also carries the harness's edit bound**, not only the policy; and the bound is stricter than the shipped rule. Without it, the
  pilot's generation 2 showed the shipped text alone did not make the main session delegate.
- **The rule reached neither arm's session start;** a rule that has to be asked for by the session (resolver `explain` / `read`) is what both
  arms had.
- **Per-cell project memory** was written by sessions (c2, c3, c6), an unmeasured channel, isolated per cell.
- **The stream cost meter** (the watchdog) read about $12.75 for c2 against the $23.51 envelope; it killed nothing. The envelope is what this
  report uses.
- **Three A cells ran slightly fewer own-suite tests than the reference** (5818, 5827, 5829 against 5830); B's ran more (5849–5853). A note,
  not a failure: no new failure against the reference's failing set in any cell.
- **The reference's own suite fails 3 of 5830 tests** (executable-bit and changelog tests of the cell-shaped tree); the in-cell agents saw
  the same 3 failures in both arms.
- **Void spend of $13.53** is outside the six cells; the codex reading was not metered.

## 10. Against the quick pilot (creds #176)

| | Pilot, generation 3 (n = 1 per arm + an A/A cell) | This series (n = 3 per arm) |
|---|---|---|
| Story | about 400 lines, two tasks | about twice as large (epic E2, four stories) |
| Children | sequential only | allowed in parallel in both arms; B launched up to 3 at once |
| B against A, cost | +13 % | −33 % (−23 % without c2) |
| B against A, wall | +31 % | +48 % (+52 % without c2) |
| Quality | no difference detected | no difference detected |

Same direction on time, opposite on cost.

## 11. What this does and does not say

On this story, the role-table policy under the harness's edit bound cut the median cost by about a third (about a fifth to a quarter if c2,
which ran Opus 4.8, is set aside) at no detectable quality loss on the hold-outs and the own suite, and cost about half again in wall time. It
does **not** say that the shipped rule, without the harness's edit bound, behaves the same; it does not say anything about other stories or
sizes; and with three cells per arm it cannot separate a policy effect from cell-to-cell spread of the size seen inside arm A.

## 12. What the next measurement needs

1. A re-run, or more cells, so that a cell that falls back to another model (c2) does not decide an arm's median; record the fallback event
   as a per-cell flag in the harness.
2. The same story without the edit bound in B, to measure the shipped rule as written.
3. The `panelView` failure looked at once, by hand: a requirement every candidate misses and the reference meets may be a gap in the
   statement, not in the candidates.
4. A reader calibrated to the plan's 90 % line, or a hand reading of the not-met items, before R-item counts carry weight.
