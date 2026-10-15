# The quick pilot — subagent-model policy A/B on one story, 2026-10-14

> Status: **ran to the end, 2026-10-14; n = 1 per arm (plus an A/A cell), preliminary, not a verdict.** Three generations:
> generation 1 VOID (instrument), generation 2 valid but the policy under test never engaged, generation 3 valid with a
> test-only edit bound on arm B. Plan: [PLAN_subagent_policy_ab.md](../../todo/PLAN_subagent_policy_ab.md) §1 (the question);
> harness: `spikes/policy-ab-quick/` (a spike, not the plan's `bench policy` verbs); data:
> [quick-pilot-data.json](../bench/quick-pilot-data.json) and the three codex readings `research/bench/quick-pilot-reading-gen{1,2,3}.json`.

## 1. The question

The plan's §1, unchanged: for one already-merged story re-run from its pre-implementation spec, does the **new** subagent-model
policy (conventions release `ea3252f2`: Sonnet scouts and coders, Opus judges, Fable cornerstones, plus a per-story process and a
main-session edit bound of 1 file / 50 lines) deliver a change **as good as** the **old** policy (conventions `3df745a7` =
`d88e293^`: Opus by default, Fable for security and new architecture) — and faster, cheaper? Main session held fixed (`claude -p
--model opus`, claude 2.1.289), every model at effort `high`, the same cap ($40) and wall (120 min) per cell. What differs between
the arms is the whole policy — model assignment AND process — never the model alone.

| | |
|---|---|
| Story | `dew_flow_creds_for_devs` PR #176, epic E1 (T1 + T2): the slot table gains a bundle-key column, a `StoredSecret` type, a compile-fail harness with three fixtures |
| Parent P / reference S | `9f62e4db` / `9369b4d3` (S touches 12 files under `src_vs_code`, +337 / −67) |
| Arms | A = conventions `3df745a7` (overlay `subagent-models.md` + `planning-docs.md`); B = conventions `ea3252f2` + its guard |
| Cells | three per generation — A1, A2, B — in the order drawn from the committed seed (A, A, B) |
| Models | `opus` → `claude-opus-5-5`, `sonnet` → `claude-sonnet-5-5`; a Haiku call of ≈ $0.001 per cell is the CLI's own |

## 2. What was pre-registered (all in `spikes/policy-ab-quick/inputs/`, committed before any cell)

| File | What it fixes | Commit |
|---|---|---|
| `statement.md` | The frozen E1 text at P with the then-policy's model lines **struck identically for both arms** (four strikes listed in the file: the `(Opus)` heading suffix, one sentence, two `**Model: Opus**` markers) | `edfe6d3` |
| `checklist.json` | R1–R12, the requirement checklist the codex reader scores, written from the statement before reading S's diff | `edfe6d3` |
| `holdout.json` | The hold-out classes: 3 spec-implied new tests in `slotTable.test.ts` and `syncPinRule.test.ts` (materialised over the candidate with their 13 pre-existing P tests), the harness and fixtures as deliverables scored only through R7–R10 | `edfe6d3` |
| `reference-only-tokens.json` | 15 identifiers and 10 phrases present in S and absent from P and the statement — the leak probe | `edfe6d3` |
| `commands.json` | Restore, typecheck, compile, lint, ratchet, test — executable + arguments, no shell | `edfe6d3` |
| `prediction.md` | Six predictions and two surprise conditions (§8) | `edfe6d3` |
| `seed.txt` | `q1-c8e41169…`; order = position of B among [A, A, B] = sha256(seed) mod 3 | `82390ae`, before the draw |

The codex reading's sanity gate was also fixed in advance: the anonymised reference must score ≥ 9 / 12 `met`, or the reading of
that generation is not used.

## 3. The instrument and its three generations

Three defects of the instrument were found on the way. Two were found before generation 1 by probes and landed in `c8e4116`
(before the seed commit); one voided generation 1; and generation 2 exposed a fourth problem that is a finding about the policy
rather than about the harness.

### 3.1 Before generation 1 — context bloat (fixed)

Measured on the first prepared tree: native rule discovery loaded **64 files / 466 KB**, about half of it the conventions
submodule's own `research/`, `todo/` and README files — about **340k tokens per main session and 170k per child** before any work.
Fix: the cell tree keeps only the submodule's rule directories, identical for both arms (`lib/trim.mjs`). After it every cell
loaded 26 instruction files at session start, the arm's `subagent-models.md` among them with the overlay's sha
(`instructions.shaMatchesOverlay: true` in every summary).

### 3.2 Before generation 1 — effort cannot be pinned from a hook (fixed by settings, audited per child)

Measured with probes: a subagent takes **its own model's** effort from settings (`modelSettings.<alias>`, else `effortLevel`), not
the parent's. The Agent tool's per-launch `effort` argument is **not forwarded to hooks**, and the child ran at the settings level
regardless (probe: the model sent `xhigh`, the child logged `high`). A `SessionStart` rewrite of settings reaches neither the
running session nor its later subagents. So the plan's effort-pin hook (D15) could not see what it was meant to deny and was
dropped; instead every cell runs with `effortLevel: high` and `modelSettings` `sonnet` / `opus` / `fable` each `high` (one pinned
file, sha `2b28ca6d…` in every `run.json`), and every child's logged level is audited. **Every logged level, main and child, in
every cell of every generation was `high`.**

### 3.3 Generation 1 — VOID: the child env dropped `CLAUDE_CODE_ENTRYPOINT`

All three cells ran to completion and passed the hold-outs, but **no cell could build or test**: every non-trivial Bash command
was refused (*"This command requires approval"*) and every PowerShell call failed (*"The command line is too long"*). Bisected
with $0.05 probes: the harness's minimal child environment had dropped `CLAUDE_CODE_ENTRYPOINT`, and without it the CLI's own
command checks fail on this machine. Fix (`a5cc6f5`): the child env sets `CLAUDE_CODE_ENTRYPOINT=claude-vscode`, fixed and
identical for both arms (the entrypoint the operator's own sessions run under). Generation 1 is kept as a record only
(`inputs/generations.md`): $5.75 + $6.63 + $9.61 = **$21.99**; each cell launched children whose job was to work around the broken
shell (A1: one Opus "Probe command execution"; B: one Sonnet "Probe running tsc/npm test" and one Opus "Review E1 diff as
compiler"), and its codex reading (A1 9, A2 8, B 8, reference 11) is dominated by "observations missing".

### 3.4 Generation 2 — valid instrument, but the policy never engaged

With all fixes in, **no cell launched a single subagent**. B's main session wrote **6 files / 317 lines** itself, against its own
rule's 1-file / 50-line bound; the rule was loaded (sha-matched) and simply not followed. Generation 2 is therefore a valid
measurement of "Opus main session alone, under rule A's text" against "Opus main session alone, under rule B's text" — not of the
two policies as designed.

### 3.5 Generation 3 — arm B gets a test-only edit bound

Owner decision, 2026-10-14: arm B's tree gets a **harness-only** `PreToolUse` hook (`hooks/edit-bound.mjs`, verdict in
`lib/edit-bound.mjs`, commit `aeb8482`) that denies a main-session `Edit`/`Write`/`MultiEdit`/`NotebookEdit` inside the tree once
the request would exceed 1 file or 50 lines, with the rule's own wording and *"Delegate the code to a coder subagent with model
\"sonnet\""*; it resets on `UserPromptSubmit` and never applies to a child. **The conventions rule is unchanged**; this enforces
what B's text already says. Arm A is unchanged. Same seed, so the same order.

## 4. Results — generation 2 (valid; no delegation in any cell)

| | A1 | A2 | B | reference |
|---|---|---|---|---|
| Hold-out (slotTable 5/5, syncPinRule 11/11) | pass | pass | pass | — |
| Own suite | pass 5204/5208 | pass 5209/5213 | pass 5207/5211 | — |
| Codex R-reading, met of 12 | **12** | 9 | 10 | 11 (gate ≥ 9: pass) |
| Children launched | 0 | 0 | 0 | — |
| Main-session edits | 9 files / ≈360 lines | 10 files / ≈537 lines | 6 files / ≈317 lines | — |
| Wall (CLI duration) | 20.6 min | 21.1 min | 21.7 min | — |
| Cost (all Opus) | $4.33 | $4.14 | **$5.15** | — |

B: +4 % wall and +21 % cost against the A mean ($4.24) — with no subagent in either arm, so nothing here is the policy's model
assignment; it is one more Opus session that read more (B cache read 11.4M against 9.0M / 7.7M).

## 5. Results — generation 3 (valid; B delegates under the test-only bound)

### 5.1 Quality

| | A1 | A2 | B | reference |
|---|---|---|---|---|
| Hold-out (slotTable 5/5, syncPinRule 11/11) | pass | pass | pass | pass |
| Own suite | pass 5204/5208 | pass 5210/5214 | **fail 5209/5214** (1) | pass 5203/5207 |
| Codex R-reading, met of 12 | 10 | 9 | 9 | 11 (gate ≥ 9: pass) |
| Not met / partial items | R3, R11 | R3, R11, R12 | R3, R11, R12 | R12 |

(1) *"copying the same secret again restarts its window instead of racing the first"* — a clipboard-timer test. It passed 5 / 5
on rerun; the disk was full during that suite run (the reference's `npm ci` failed with ENOSPC minutes later, `pilot-run-gen3.log`).
Read as **flaky under load**, not as a candidate defect.

**The shared defect.** All three gen-3 candidates moved `Date.now()` after the awaited slot reads in `snapshotForRevision`, so a
revision's timestamp is taken after the reads instead of before (P builds the object literal with `at: Date.now()` first,
`src_vs_code/src/revisionSnapshot.ts:39-44` at P; the reference keeps `const at = Date.now()` before the walk). Codex marks it on
R3 and R11. Verified for this record by opening B's tree and A1's / A2's candidate diffs. By the codex reading's R3 notes the same defect appears in
7 of the 9 candidates across all three generations, in both arms; the hold-out tests do not catch it (the guard test checks which getters are
read, not when the clock is read).

**B's extra R11 partial** — *"an existing sync assertion is replaced"*: the diff removes
`assert.equal(SEALABLE_MAPS.length, SECRET_SLOTS.length, …)` from `syncPinRule.test.ts`. The stream shows where it came from: the
Opus main session first tried that edit itself (the one the bound denied), then wrote the replacement into the T1 coder's brief
as a step it specified (*"no assertion edited (except the one in step 5 below, which I specify)"*). The orchestrator decided it;
the Sonnet coder carried it out.

**Quality reading.** A1 10, A2 9, B 9 against a reference at 11: the A1–A2 spread (1) is as large as the A–B difference (0–1). No
quality difference between the arms is visible at n = 1.

### 5.2 Delegation and the policy audit

| | A1 | A2 | B |
|---|---|---|---|
| Children | 0 | 0 | 3 Sonnet coders (`general-purpose`): T1 9.6 min, T2 6.6 min, rework *"Make harness verdict file-aware"* 2.4 min |
| Scouts / Opus review children | — | — | none |
| Main-session edits (reader's count) | 8 files / ≈355 lines | 9 files / ≈336 lines | 3 files / ≈50 lines, incl. the denied attempt |
| Edit-bound denials | n/a | n/a | 1 (`syncPinRule.test.ts`, *"this would be 2 files / 44 lines"*) |
| Model-guard denials | n/a | n/a | 0 |
| Effort logged | main `high` | main `high` | main + 3 children, all `high` |

What B's main session actually wrote: the T1 RED test in `slotTable.test.ts` (≈36 lines, before any child) and a checkbox in the
plan document after the coders (outside the scored diff). It wrote three briefs of 8.5k, 10.9k and 3.1k characters. Why the bound
let the second file (the plan document) through is not established. The summary's child edit count reads 0 although the coders
wrote the code: the reader does not attribute child edits, so the delegated line count is **not measured**. The children ran
**sequentially**: the child env sets `CLAUDE_CODE_DISABLE_BACKGROUND_TASKS=1` (`lib/env.mjs`; a backgrounded Agent call misreads cost and
time, [scout pilot](../scout-calibration/pilot.md) §3), so T1 and T2, which touch disjoint files per the statement, could not overlap.

### 5.3 Speed and cost

| | A1 | A2 | B |
|---|---|---|---|
| Wall (CLI duration) | 31.1 min | 26.9 min | **38.1 min** |
| Turns | 70 | 65 | 40 |
| Cost | $4.89 | $4.75 | **$5.43** = Opus $3.57 + Sonnet $1.86 |

Against the A mean ($4.82, 29.0 min): B **+13 % cost, +31 % wall** (+22 % against A1, +42 % against A2). A1–A2 spread: $0.14 (3 %)
and 4.2 min (15 %). Spend of generation 3: $15.07; of all three generations: $50.68 in cells, plus the codex readings (not metered).

## 6. Why B cost more and took longer — the anatomy (n = 1 per arm)

Token anatomy from the CLI envelope's `modelUsage`, generation 3:

| | A1 | A2 | B main (Opus) | B children (Sonnet, 3) | B total |
|---|---|---|---|---|---|
| Output tokens | 50.0k | 49.4k | 36.2k | 27.4k | 63.6k |
| Cache write | 226k | 211k | 204k | 315k (91–113k each) | 519k |
| Cache read | 10.39M | 10.33M | 6.07M | 3.99M | 10.06M |
| API messages (main) | ≈ 60 | ≈ 60 | 37 | — | — |

1. **The orchestrator still pays for understanding.** B's Opus session cost $3.57 — 74 % of an A cell — without writing the code:
   it read the story, wrote the RED test, wrote ≈ 22.5k characters of briefs precise enough to specify individual assertions, and
   verified the result. The Opus saving against the A mean is $1.25; the Sonnet bill is $1.86. Net **+$0.61**.
2. **Each child starts cold.** Every Sonnet coder cache-wrote 91–113k tokens: the instructions, the rules, and files the main
   session had already read. B's total cache write is 2.4× an A cell's; its total cache read (10.06M) is almost exactly an A cell's
   — the reading moved from Opus to Sonnet, it did not go away.
3. **The children were sequential.** 18.6 min of child time sits end to end inside B's 38.1 min; B's main session alone took
   ≈ 19.5 min, two-thirds of an A cell's whole wall.
4. **One rework round.** The third child (2.4 min) fixed the harness's verdict after the main session's review — a loop A's
   single-author sessions did not have.
5. **The story is small.** About 400 changed lines, two tasks: the delegation overhead (cold starts, briefs, review, rework) is of
   the same size as what Sonnet saves on the code. On this story it slightly exceeded it.

This is a mechanism read from one cell per arm; it is a hypothesis for the next run, not a law.

## 7. The leak and tamper checks

- **Canaries** — three files planted under `~/.claude` (`projects/<the creds project>/`, its `memory/`, `file-history/`): no hit
  in any stream or candidate, in any cell (`canary.count = 0`).
- **Reference-only tokens** — 13–16 hits per cell, in both arms, in both the stream and the diff (`SlotSource`, `Expectation`,
  `projectOptions` in every cell; `PACKAGE_ROOT`, `expectationOf`, `fromTable`, `fromSnapshot`, a phrase or two). Judged by the main
  session as natural naming from the statement and P's code (slot *source*, harness *expectation*, project *options*), not
  leakage; the canaries, which no natural naming can produce, stayed silent. A judgement, labelled as one.
- **Tamper** — no edit or shell call on the overlay, hooks or settings in any cell (`tamper.calls = []`).

## 8. Prediction against observation

`prediction.md`, written before the seed and any cell; observed values are generation 3 unless stated.

| # | Predicted | Observed | Held? |
|---|---|---|---|
| 1 | All cells pass the 3 spec-implied hold-outs and their own suite; R-coverage 10–12 / 12 everywhere; no visible A–B difference; A1–A2 spread as large as any A–B difference | Hold-outs: all pass. Own suite: B 1 flaky failure. R: 10, 9, 9 (gen 2: 12, 9, 10) — two cells below the predicted range. A1–A2 spread ≥ A–B: yes | partly |
| 2 | A writes most code itself or with 1–2 Opus children (a Fable launch plausible); B launches ≥ 2 Sonnet children and ≥ 1 Opus review | A: alone, 0 children, no Fable (gen 2 and 3). B gen 3: 3 Sonnet coders, **no** scout, **no** Opus review child (the main session reviewed). B gen 2: no children at all | A yes; B no |
| 3 | B's wall within ±30 % of A's | +31 % against the A mean (+22 % / +42 %); gen 2: +4 % | borderline miss |
| 4 | B 20–40 % cheaper than the A mean; every cell under $25 | B **13 % dearer** (gen 2: 21 % dearer); every cell ≤ $5.43 | direction wrong |
| 5 | Every logged level `high`; the effort-pin hook denies nothing | All `high`. The hook was dropped before generation 1 (§3.2); no Agent call carried an `effort` argument | yes / not testable |
| 6 | No canary token and no reference-only identifier anywhere | Canaries: none. Reference-only identifiers: in every cell, judged natural naming | partly |

Surprise conditions: *B slower than both A cells by > 50 %* — no (+22 % / +42 %); *B fails a hold-out both A cells pass* — no.

## 9. What this does NOT show

- **Any arm difference at all.** n = 1 per arm, one story; the A1–A2 spread is as large as the A–B quality difference, and one cell
  per arm cannot estimate B's variance.
- **The policy as deployed.** Generation 3's B ran under a harness-only edit bound. Under the shipped text alone (generation 2) the
  main session did not delegate at all; generation 3 measures the policy *as complied with*, not as followed by default.
- **The whole new process.** No scouts, no judge-written scenarios, no Opus review child, no Fable spec were launched — B's
  measured delegation is "Opus orchestrates, Sonnet codes".
- **Parallel delegation.** Background tasks were disabled, so independent stories ran one after the other.
- **A medium or large story.** ≈ 400 lines, two tasks; the overhead-to-saving balance in §6 depends on size.
- **Quality beyond one reader.** The R-reading is one codex model's (codex-cli 0.160.0, `gpt-6.1-sol`; the model field is recorded
  in generation 1's file, null in 2 and 3), sanity-gated on the reference; no item scored `not_met`, so the plan's spot-check of
  "not met" items had nothing to check. The two items this record leans on (R3's timestamp, B's replaced assertion) were verified
  by opening the files. No person read the candidates.
- **Effort other than `high`, other machines, other CLI versions** (claude 2.1.289 only).

## 10. Beside the pilot: the coai gate's own records (context, not evidence for either arm)

A read-only query of both coai stores (the Windows and the WSL data directory), comparing the two weeks before the rule switch
with the ≈ 1 day after:

| | code rounds before / after | findings per round | accepted share |
|---|---|---|---|
| Windows | 393 / 10 | 4.2 → 7.6 (one branch dominates the after) | 60 % → 70 % |
| WSL | 416 / 100 | 4.8 → 3.9 | 65 % → 49 % |

Not interpretable as a policy effect: one day of data; the reviewer set changed on 10-11 (codex only); and coai records **only the
main session's model**, so "Opus + Sonnet coders" is indistinguishable from "Opus + Opus coders". A coai plan to record the
subagent models and the diff size per round is in progress: `dew_flow_connect_other_ais · todo/PLAN_a_round_records_who_wrote_the_code_and_how_much.md`.

## 10a. Beside the pilot: one real working session, two days (context, anonymised)

The operator's own long-running session on a private project (VS Code, Opus main session, the same kind of work both days)
ran every subagent on Opus on day 1 and Sonnet coders (Opus reviewers/judges, a few Fable) on day 2. Read-only aggregates from
its transcripts (tokens deduplicated per `message.id`, list prices from `research/scout-calibration/prices.json`) and the
project's `git log` — no text, no names:

| | day 1 (all Opus) | day 2 (Sonnet coders) |
|---|---|---|
| active hours | 12.2 | 12.4 |
| subagents | 9, all Opus | 48: 23 Sonnet coders, 19 Opus (reviews, judges, planners), 4 Fable, 2 inheriting Opus |
| summed subagent wall time | 14.7 h | ≈ 80 h (parallel work) |
| commits / merged PRs | 96 / 20 | 156 / 28 |
| lines added | +15.3 k | +38.5 k |
| list-price cost | ≈ $333 | ≈ $522 (Opus $346 + Sonnet $176) |
| **$ per 1 000 lines added** | **≈ $21.7** | **≈ $13.6 (−37 %)** |
| $ per merged PR | ≈ $16.7 | ≈ $18.6 |

What it shows: in a long session with **parallel** coders the cheaper tokens pay — cost per hour rose (≈ $27 → $42) but output
per hour rose more, so cost per line fell by about a third. That is the regime the pilot's single small story, with sequential
children, could not reach (§6). What it does not show: quality (the project had no gate rounds those days; fix-commit share
≈ 50 % both days), or why the subscription quota lasted longer (the transcripts carry no quota metering). Confounders: different
tasks per day, the main session on Opus both days, and Sonnet subagents ran at effort `medium` on that machine (its settings were
keyed by full model ids; converged to `high` afterwards). Lines added is a crude unit — a reason for the coai plan above.

## 11. Recommendations for the next measurement

1. **A medium story** — e.g. coai #751 (≈ 900 lines), the plan's second candidate — where the code Sonnet writes is large against
   the fixed overhead of briefs and cold starts.
2. **Parallel independent stories** — a story with two or more tasks on disjoint files, with background agents allowed and their
   cost and time read correctly (the scout pilot's `async_launched` finding has to be solved in the reader, not by disabling it).
3. **Keep the test-only edit bound, or measure compliance without it** — and say which. Generation 2 shows that "the rule is
   loaded" does not mean "the rule is followed"; compliance is itself an outcome worth a row.
4. **≥ 3 runs per arm** (the plan's series: 3 stories × 2 arms × 3 runs), so the A/A spread can be set against the A–B difference.
5. **Attribute child edits and per-child time and tokens** in the reader, and record subagent models in coai through the plan
   above, so the product's own gate records can answer the same question at scale.
6. **Keep the codex R-reading and the timestamp lesson**: the hold-out passed every candidate, the reading found the one
   behavioural defect seven of nine candidates share. Add a hold-out for the snapshot's clock order to the series' story contract
   only if it is spec-implied; otherwise keep it as a reading item.
