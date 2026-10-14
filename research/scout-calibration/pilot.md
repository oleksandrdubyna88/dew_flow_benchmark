# The pilot — scout model calibration (E2 S8), 2026-10-13 to 2026-10-14

> Status: **the pilot ran to the end; preliminary, not a verdict** (plan D7). Two pilot tasks, every workload's pair, an A/A
> replication and one budget-exhausted cell; the hand-checks passed. Plan: [PLAN_scout_model_calibration.md](../../todo/PLAN_scout_model_calibration.md) ·
> module: [module_scouts.md](../module_scouts.md).
>
> **Read §11 first.** §4's Opus-against-Sonnet numbers were measured with the Sonnet scouts at effort `xhigh` and the Opus scouts at
> `high` (found after the pilot, from the child transcripts); §11 re-measures every pair at ONE effort, `high`, and reverses the reading.

The pilot's job is to prove the instrument before the preliminary run spends its money: that a cell launches confined, that its
stream is read right, that cost and time are captured exactly, that the matcher and the assessor agree with a person — and to set
the caps and walls the preliminary run launches under. It is NOT a measurement of Opus against Sonnet: two tasks cannot be one.
The numbers below are written down because they are what the pilot saw, and because the next run is planned from them.

## 1. What ran

| run | what | cells | spent (CLI-reported) |
|---|---|---|---|
| `01a13812` | the two pilot tasks × A1/B1, A2/B2, A3/B3 (one repeat) | 12 + 2 re-run generations | $17.02 |
| `01a13859` | A/A: A1 twice more on each pilot task | 4 | $2.12 |
| `01a1385e` | one budget-exhausted cell: B1 under a $0.05 cap | 1 settled (the run stops there by design) | $0.07 |
| — | three cheap probes of the background-agent finding (§3.1) | — | ≈ $0.55 |

**Total ≈ $19.76 of the $60 pilot ceiling.** Pinned throughout: claude 2.1.289 · `opus` → `claude-opus-5-5` · `sonnet` →
`claude-sonnet-5-5` · feature-dev plugin 217b1ff2dbbb · pinned settings 916c89ebba32. Cells ran one at a time, never alongside a
test suite, so the times are not contended.

The pilot tasks (D10's repositories 9 and 10, `pilot-tasks.json`): **`p1539`** — private, Python, *where is it decided*, \|G\| = 6;
**`t-openhands-de5a79b4ce`** — OpenHands/OpenHands at `2c5ce2fa2dca`, *where is it decided*, \|G\| = 7.

## 2. The selection that fed it

`bench scouts tasks` over the 2 740 screened candidates (seed `scouts-preliminary-2026-10-12`) drew the D10 set — 8 main tasks, their
8 reserves, 2 pilot tasks — exactly the order of the 2026-10-12 screen, and froze all 18 trees. Getting there found five instrument
defects, each fixed with a RED test before the next try (benchmark PRs):

| PR | found | fix |
|---|---|---|
| #83 | a private repository read from a local WSL clone: `clone --bare` brought no `refs/remotes/*`, so commits the clone holds were "not in" the mirror | a local source's remote-tracking refs are mirrored too |
| #84 → #85 | a private repository's P carries `.claude` settings granting a directory outside it; the tree guard stopped the whole selection. Excluding such a P at admission (#84) then excluded **431 candidates — three whole repositories** | #85: the tree DROPS the project's own Claude settings (recorded as a removal, kept out of G); the guard stays as the backstop |
| #85 | a path longer than Windows' 260 characters under the trees root: `git add` "Filename too long" | the tree's git runs with `core.longpaths` |
| #90 | a private repository's submodule url is a private GitHub repository this machine cannot reach | a local source's submodule is archived from its clone's `.git/modules/<path>`, or from a sibling clone whose `origin` is that same repository |
| #87 | (operator, 2026-10-14) reading all 2 740 candidates took about an hour | the LAZY read: in the draw's own hash order, a repository only to its fifth admissible candidate, ten repositories in all — **113 of 2 740 read**, the same draw (proven equal to the full read) |

Seven statements were refused by the freeze's leak check (each on one or more words an identifier the change added, or a stem of a
G file) and reworded without changing the question; one reserve statement was written fresh (the code's admission excluded the
2026-10-12 screen's reserve for that repository: two files of G existed at P).

## 3. What the pilot corrected in the instrument

| PR | the pilot's finding | the correction |
|---|---|---|
| #91 | **Cell 1 (A1) stopped on `CostMismatch`.** Under the scout launch claude 2.1.289 ran the Agent call in the BACKGROUND: `status: async_launched`, no `totalDurationMs`, no usage, the answer in a later `task_notification`, a second turn with a second `result` envelope. Cost, time and answer were all misread. Three probes: a plain launch is synchronous either way; the scout flags background it; `CLAUDE_CODE_DISABLE_BACKGROUND_TASKS=1` makes it synchronous again | every scout launch carries that variable; a backgrounded agent STOPS a cell as `AgentBackgrounded` (never scored) |
| #91 | **Cell 2 (B2, synchronous) stopped on `CostMismatch`.** The CLI bills a Sonnet 5.5 cache hit at **$0.20/M**; the pricing page (re-read 2026-10-14) says **$0.10/M** (0.05× input, footnote 2). Sonnet's $1.10243 was exactly its tokens at $0.20; the Opus main session matched the page to the cent | `prices.json` keeps the published prices — every reported dollar — and carries the CLI's observed rate beside them (`cliRates`), used only by the cost check |
| #90 | `ProbeCampaignTests` lost progress lines under two concurrent lanes (red on CI and locally, unrelated to the scouts) | a concurrent queue |

After #91 every remaining cell settled cleanly: the cost check agreed within 2 %, no stop, no void.

## 4. The numbers — PILOT, two tasks, one repeat

Arms (§3.3): **A** = Opus scouts, **B** = Sonnet scouts at ⌈1.5·N⌉, all `feature-dev:code-explorer`. Recall = files of G the team's
KEY FILES name. Wrong claims per 10 decided claims, by the assessor (codex `gpt-6.1-sol`, rubric `scout-assess-v1`). Time = the team's
wall clock (first Agent call to last Agent result). Cost = the team's tokens at the **published** prices.

| workload | arm | recall | wrong / 10 | team time | team cost | session cost (CLI) |
|---|---|---|---|---|---|---|
| W1 (1 → 2) | A1 · 1 Opus | 0.76 | 0.50 | 57.7 s | $0.39 | $0.45 |
| | B1 · 2 Sonnet | 0.68 | 2.00 | 77.4 s | $0.81 | $1.09 |
| W2 (2 → 3) | A2 · 2 Opus | 0.83 | 0.25 | 71.1 s | $0.95 | $1.06 |
| | B2 · 3 Sonnet | 0.76 | 1.83 | 90.3 s | $0.97 | $1.30 |
| W3 (3 → 5) | A3 · 3 Opus | 0.83 | 1.33 | 73.5 s | $1.28 | $1.43 |
| | B3 · 5 Sonnet | 1.00 | 2.00 | 113.6 s | $1.70 | $2.29 |

Paired differences (B − A), clustered by repository, one-sided 95 % — **over two tasks, so the intervals mean little**:

| workload | Δrecall | Δwrong / 10 | the pre-registered margins |
|---|---|---|---|
| W1 | −0.083 [−0.167, 0] | +1.50 [+1.00, +2.00] | Δrecall ≥ −0.03 and Δwrong ≤ +0.5 |
| W2 | −0.071 [−0.143, 0] | +1.58 [+1.17, +2.00] | |
| W3 | +0.167 [0, +0.333] | +0.67 [+0.53, +0.80] | |

What the pilot saw, read as no more than it is:

- **Faster: Opus**, in every workload — the Sonnet teams took 20 s to 40 s longer (more scouts, each reading more).
- **Cheaper: Opus at W1 and W3, even at W2.** Two Sonnet scouts cost twice one Opus scout ($0.81 against $0.39): each Sonnet scout read
  about 2.6× the cache an Opus scout read.
- **Recall: no clear winner** — Sonnet lower at W1/W2 by one file, higher at W3 (5 Sonnet scouts found every file on both tasks).
- **Wrong claims: Sonnet higher in every workload**, beyond the +0.5 margin. The hand review in §5.3 splits that excess: most of it is
  *citation* — a compound claim whose second half sits more than 10 lines from the cited line — and less of it *substance*.

## 5. The hand-checks (§3.7, D9 — done by this session)

`bench scouts score --run 01a13812… --handcheck 20 --seed 20261014` drew 20 found pairs, 14 not-found pairs (the pool held only 14)
and 20 claim verdicts. Every item was opened in its task's tree. The filled file is [handcheck.md](handcheck.md) (private items by
label and commitment; their plaintext stays local).

### 5.1 The matcher — passes

| | agree | threshold |
|---|---|---|
| found | **20 / 20** — each G file an exact entry of the team's KEY FILES | ≥ 19 / 20 |
| not found | **14 / 14** — no entry names the file, exactly or by path suffix; three lists name a *different* file of the same name in another directory, and both files exist in the tree | |

### 5.2 The assessor — passes

| | agree | threshold |
|---|---|---|
| claim verdicts | **20 / 20** (19 `right`, 1 `wrong`) | ≥ 18 / 20 |

### 5.3 Beyond the draw: every `wrong` verdict

The draw held a single `wrong` verdict, so the verdicts that carry the pilot's main finding were barely sampled. All 49 `wrong`
verdicts of run `01a13812` were unblinded locally (claim, cell, arm, the assessor's note) and read; eleven were checked against the
code line by line, and every one held under the rubric (two are close calls about ordering and an early-return condition). Then each
of the 49 was classed by this session:

| | claims decided | `wrong` | of which **citation** (the fact holds; a part sits > 10 lines from the cited line) | of which **substance** (the code contradicts the claim) |
|---|---|---|---|---|
| Opus (A1 + A2 + A3) | 120 | 10 | 3 (0.25 / 10) | 7 (0.58 / 10) |
| Sonnet (B1 + B2 + B3) | 200 | 39 | 22 (1.10 / 10) | 17 (0.85 / 10) |

Per workload, substance only, per 10: W1 A 0.50 · B 0.75 — W2 A 0 · B 1.00 — W3 A 1.00 · B 0.80. The rubric's line rule is
pre-registered and stays; this split is a reading aid for the preliminary run, not a second rule.

## 6. A/A — the noise of one arm against itself

A1, three runs per task (run `01a13812`'s generation 2 plus run `01a13859`'s two repeats):

| task | recall | team time | team cost |
|---|---|---|---|
| p1539 | 4/6 · 4/6 · 4/6 | 57.7 s · 72.1 s · 70.7 s | $0.52 · $0.56 · $0.57 |
| OpenHands | 6/7 · 6/7 · 7/7 | 57.6 s · 70.8 s · 72.6 s | $0.27 · $0.40 · $0.40 |

Recall moves by at most one file; time by about a fifth; cost by up to a third on the same task — the size of effect the pilot's
arm differences mostly sit inside, which is why §7's answer is what it is.

## 7. Sizing (§3.8) — reported, not a gate

`bench scouts sizing` simulated the pre-registered decision 1 000 times per task count from these cells
([sizing.md](sizing.md)): **no task count from 8 to 30 meets both targets** — the rule never passes often enough at Δ = 0 while
keeping false passes at the margin low. As measured while building it, the percentile bootstrap is too narrow with this few
repositories. The preliminary run (50 cells, D7) does not need it; a decision run does — the interval method is the operator's call.

## 8. The budget-exhausted cell

B1 on `p1539` under `--max-budget-usd 0.05`: the CLI stopped at $0.0662 (its cap is an estimate and overshoots), the cell settled
`aborted:spend` at the real cost, the run stopped and nothing was scored past it — as §3.10 specifies.

## 9. The preliminary run's budgets

`bench scouts schedule` from run `01a13812` — per arm the largest settled cell × 1.5 and the slowest × 2 ([schedule.json](schedule.json)):

| arm | A1 | B1 | A2 | B2 | A3 | B3 |
|---|---|---|---|---|---|---|
| cap | $0.87 | $2.16 | $1.94 | $2.06 | $2.46 | $3.74 |
| wall | 127 s | 179 s | 161 s | 217 s | 159 s | 284 s |

At the pilot's mean costs, eight main tasks × six arms come to about **$60** (plus the assessor's codex calls).

## 10. Open, for the operator

1. **The decision run's interval method** (§7) — the percentile bootstrap cannot certify a decision at any count up to 30.
2. **The wrong-claim margin** — Sonnet's excess is mostly line-citation precision; whether the +0.5 margin should read substance
   only is a decision the pilot cannot make.
3. **The preliminary run** (50 cells, ≈ $60) — ready to launch on the operator's word: tasks frozen, schedule written, instrument proven.

## 11. After the pilot: the effort was unequal — 26 cells at one effort (2026-10-14)

**What was wrong.** The operator's experience said Sonnet scouts are much faster and cheaper; the pilot said the opposite. A persisted
two-child session with the scout launch's flags read each child's effort from its transcript (`effort` / `perTurnEffort`): the Opus
child ran at **`high`**, the Sonnet child at **`xhigh`**. The pinned settings projected the operator's configuration — `effortLevel: xhigh`,
with `modelSettings` lowering only `opus` to `high` — and on claude 2.1.289 a child takes `modelSettings` by its own model, else the global
level (a consultant's prior that children inherit the parent's effort was refuted by this read). §4 therefore compared *configurations*,
not models. Benchmark #93 added `--effort <level>`: one explicit level for every model, in a settings file of its own (its own settings
pin, `d7972dfc7004` for `high`), and `report --prices`: each team's cost split by token kind. A second confound the consultant named — a
team of N takes the first N focus areas, so B1's two Sonnet scouts cover two areas against A1's one — is separated by arm **C3**: three
Sonnet scouts on exactly A3's three areas.

**What ran.** Four pilot runs on the same two tasks, one cell at a time: `01a13949` (A3 and C3, two repeats each, `--effort high`),
`01a13952` (A1, B1, A2, B2, B3, `--effort high`), `01a1395e` (B1, two repeats, the operator's configuration — Sonnet at `xhigh`, the
contemporaneous control), `01a13965` (A1 and B1 once more, `--effort high`). **26 cells, every one scored, $20.91**; 640 claims assessed.

### 11.1 One effort (`high`) for both models

| workload | arm | cells | recall | wrong / 10 | team time | team cost | output tokens | cache read | cache write |
|---|---|---|---|---|---|---|---|---|---|
| W1 | A1 · 1 Opus | 4 | 0.77 | 0.75 | 75.9 s | $0.48 | 6 830 | 471 k | 50 k |
| | B1 · 2 Sonnet | 4 | 0.73 | 1.50 | **41.4 s** | **$0.29** | 9 545 | 244 k | 68 k |
| W2 | A2 · 2 Opus | 2 | 0.85 | 1.25 | 69.5 s | $0.80 | 12 798 | 652 k | 83 k |
| | B2 · 3 Sonnet | 2 | 0.77 | 1.83 | **51.3 s** | **$0.44** | 15 074 | 435 k | 98 k |
| W3 | A3 · 3 Opus | 4 | 0.85 | 0.83 | 70.9 s | $1.23 | 18 326 | 1 049 k | 130 k |
| | B3 · 5 Sonnet | 2 | 0.85 | 1.40 | **63.1 s** | **$0.78** | 24 376 | 899 k | 179 k |
| | C3 · 3 Sonnet (A3's areas) | 4 | 0.85 | 1.58 | **45.8 s** | **$0.46** | 14 583 | 449 k | 108 k |

Recall is files of G found over files of G, pooled over the arm's cells; team cost at the published prices; tokens are the team's mean per
cell. Cells per arm are few (2–4 on two tasks), so every figure carries the A/A noise of §6 (recall ± one file, time ± a fifth, cost up to
a third).

### 11.2 The control: the same Sonnet team at the operator's `xhigh`

| B1 · 2 Sonnet | cells | recall | wrong / 10 | team time | team cost | output tokens | cache read |
|---|---|---|---|---|---|---|---|
| `high` (§11.1) | 4 | 0.73 | 1.50 | 41.4 s | $0.29 | 9 545 | 244 k |
| `xhigh`, run at the same time | 4 | 0.73 | 1.38 | 91.7 s | $0.73 | 20 441 | 1 365 k |
| `xhigh`, the pilot (§4) | 2 | 0.69 | 2.00 | 77.4 s | $0.81 | 20 773 | 1 552 k |

At `xhigh` a Sonnet scout reads about five times the cache and writes twice the output it does at `high`, for the same recall and no
fewer wrong claims: **the effort alone made §4's Sonnet teams slow and dear.** The Opus arms barely moved between §4 and §11.1 — they
were at `high` both times.

### 11.3 What this reads as — two tasks, preliminary

- **Faster: Sonnet**, in every workload at equal effort — 1.8× at W1, 1.35× at W2, 1.1× at W3 (B3) and 1.55× for the same three areas (C3).
- **Cheaper: Sonnet** — 40 % less at W1, 45 % at W2, 37 % at W3 (B3), 63 % for the same three areas (C3).
- **Recall: Sonnet at 95 % of Opus at W1, 91 % at W2, equal at W3** (and equal for C3) — near the operator's remembered "87 %".
- **Wrong claims: Sonnet still higher**, about +0.6 per 10 (Opus 0.75–1.25, Sonnet 1.40–1.83) — around the +0.5 margin, unchanged by
  the effort (the `xhigh` control is 1.38). §5.3's review found most of the pilot's excess was line-citation precision rather than
  substance; these runs' verdicts have not been hand-read that way yet.

**For the preliminary run:** launch it with `--effort high` (a model comparison), and keep the operator's configuration as a separately
labelled arm if the deployment question is wanted too. The operator's own setup runs every Sonnet subagent at `xhigh`; a
`modelSettings` entry for `sonnet` at `high` would give the scouts the speed and price measured here.
