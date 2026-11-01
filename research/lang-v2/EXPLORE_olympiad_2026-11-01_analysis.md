# Exploratory pilot, olympiad tasks 01, 02, 03 and 09: analysis (2026-11-01)

> **Exploratory, not evidence.** Four olympiad tasks, three bare C# ∥ TypeScript pairs each, on the task-1 pilot's harness
> (`spikes/lang-explore/`), with ungated draft specs. This analysis licenses sentences about these runs only. It settles
> nothing about C# against TypeScript (`common.measurement` rule 3); v2's series does that, with its own gates.
> Plan: [research/PLAN_lang_explore_olympiad.md](../PLAN_lang_explore_olympiad.md) (D-O8). Notes per task:
> [01](EXPLORE_task1_2026-11-01.md), [02](EXPLORE_task02_2026-11-01.md), [03](EXPLORE_task03_2026-11-01.md),
> [09](EXPLORE_task09_2026-11-01.md). Task 1's own analysis: [EXPLORE_task1_2026-11-01_analysis.md](EXPLORE_task1_2026-11-01_analysis.md).
> Counts and rates only: no held-out case, trap or mutant detail is public here (D41).
>
> **Scope words.** All four tasks are olympiad-style (stdin to stdout). "The pilot" means all four. "Part 2" means tasks 02, 03 and 09: this plan added them, and its § 4 recorded their predictions. Task 01 ran first, under the task-1 plan, and its own analysis compares it with that plan's predictions.

## 1. Short answer

- **Every one of the 24 sessions was accepted**: 12 per stack, each with all its visible and held-out cases passing.
- **No trap hits.** The plan predicted 1–3 per stack for tasks 03 and 09.
- **The total spend was $7.59**: task 01 $1.04, task 02 $1.14, task 03 $2.14, task 09 $2.30. Part 2 came to $5.58, against a predicted $10–40.
- **Cost and time missed every prediction in the same direction: they were far lower.** Task 09, an IOI-hard query problem, took a median of 2.1 minutes and $0.37 per session, where the plan predicted 10–45 minutes and $1–6.
- **No stack difference shows that is larger than the spread between pairs.** The C#-to-TypeScript ratio of median cost per task is 0.89 (task 01), 1.11 (02), 0.84 (03) and 1.24 (09). The sign flips between tasks.
- **The harness worked:**
  - no voids;
  - T1–T5 held in 24 of 24 records;
  - the time partition summed to the wall in 24 of 24;
  - the most expensive session cost $0.58, against a $50 per-session cap.
- **What it shows:** these four tasks, as drafted, do not separate the stacks and are not hard for this model (Opus, effort `high`). Section 4 lists what v2 should take from this.

## 2. Per task and stack (medians of 3 sessions)

| task | stack | cost (median; sum) | wall | active − model_api | accepted | cases per session |
|---|---|---|---|---|---|---|
| 01 | C# | $0.154; $0.51 | 34 s | 7 s | 3 of 3 | all pass |
| 01 | TypeScript | $0.173; $0.53 | 30 s | 6 s | 3 of 3 | all pass |
| 02 | C# | $0.195; $0.57 | 63 s | 19 s | 3 of 3 | 33 of 33 |
| 02 | TypeScript | $0.175; $0.57 | 61 s | 28 s | 3 of 3 | 33 of 33 |
| 03 | C# | $0.340; $0.99 | 101 s | 27 s | 3 of 3 | 87 of 87 |
| 03 | TypeScript | $0.403; $1.16 | 255 s | 115 s | 3 of 3 | 87 of 87 |
| 09 | C# | $0.403; $1.31 | 125 s | 40 s | 3 of 3 | 41 of 41 |
| 09 | TypeScript | $0.326; $1.00 | 123 s | 47 s | 3 of 3 | 41 of 41 |

- Cost is the CLI's total. For every session, the broker's view and the recomputation from tokens give the same figure (T1–T5).
- `active − model_api` is the time the session spent outside model calls: its own builds, test runs and tool calls.
- The prediction that C# spends more here, because of its build, held in 1 of the 9 part-2 pairs. Task 03's TypeScript sessions spent the longest outside the model: a median of 115 s, against 27 s for C#. That is the agents' own self-testing, not a build.
- n = 3 per cell. No interval is given, and no difference here is a finding.

## 3. What was checked by hand

- **In part 2, every verdict of every pair was checked before the next pair ran, without the harness's matcher:**
  - task 02: 198 verdicts;
  - task 03: 522 verdicts;
  - task 09: 246 verdicts.
- **Task 01** followed its own plan, which asked for the first pair only: its 36 verdicts were compared byte for byte, and all six programs were read in full (its analysis, § 2). Its pairs 2 and 3 rest on the harness's matcher alone.
- **How:**
  - The visible cases were compared with the spec's bytes, and the agents' copies of the samples were confirmed unchanged.
  - The held-out outputs were compared with the generator's expected bytes and with a second, independent oracle:
    - task 02: a flood-fill oracle written for this check; it agrees with the naive brute oracle on the 10 inputs that oracle accepts;
    - task 03: the brute oracle on all 84 cases;
    - task 09: an independent reference on all 38 cases, plus the brute oracle on the 24 it accepts.
- **One limit was found this way** (F2 below). The harness keeps only the first 64 KiB of each case's output. Task 09 has 16 cases with longer outputs, so for those the kept files could not prove the verdict. Each session's own program was therefore run again on all 38 inputs, in an isolated box (no network; home, Windows drives and runtime sockets hidden; WSL interop masked; environment cleared). Every full output matched.
- **No verdict changed.** All 1,002 hand-checked verdicts (966 in part 2, 36 in task 01) agree with the harness.

## 4. Findings for v2

- **F1. Difficulty was mis-predicted, and known tasks may be known to the model.**
  - Tasks 02 and 09 are paraphrases of published problems; task 09's is an IOI 2018 task. Task 03 is original.
  - All four were solved in every session, in minutes. Task 09's sessions show no sign of the search a new IOI-hard problem would need.
  - Contamination cannot be told apart from ability with this design. v2's cost per correct solution compares stacks on the same tasks, so contamination does not bias the C#/TS ratio by itself. But tasks that every session solves cannot show a difference in correctness, and v2's difficulty and budget predictions should be revised down.
  - **What is decided and what is not.** v2's task mix (five known, five original) is a decision of the v2 plan that the owner approved; this pilot does not change it. Proposed to the owner, to be decided before v2's spec freeze (E1.S2):
    - (a) label each task in v2's analysis as known or original, with whether a public solution exists;
    - (b) report the known and the original tasks also as separate groups;
    - (c) revise v2's per-task difficulty and budget predictions with these observations.

    The team lead carries the proposal into the v2 plan as a change for the owner. Until the owner decides, the mix stays as approved.
- **F2. The runner must record evidence for the full output, not only a capped copy.**
  - The kept output is capped at 64 KiB for the report, while the verdict is streamed over the full output. So a later reader cannot re-check a verdict on a large case from the records.
  - v2's runner (E1.S8a) should record the sha256 and byte length of each case's full output beside the verdict. Then the hand-check and any later correction can verify it without re-running untrusted code.
- **F3. The leak check must scale with the held-out material.**
  - The v1 leak matcher costs needles × bytes. With task 09's ~5.5 million case lines it ran 88 minutes and did not finish.
  - It was replaced for this pilot by a tiered scan: exact substrings for small needle sets; then hashed whole lines, 3-line windows and 64-character chunks, each hit verified exactly. A pre-material baseline rule makes text that existed before the material was created a counted collision, not a leak. The full check now takes about 50 s over all four tasks.
  - v2's pre-push check reads held-out needles too. It needs the same shape before v2's olympiad material is generated. Owner: v2 story E1.S1 (the push check), as a fix forward before E2 generates the held-out material.
  - The scan's documented limits are in `research/module_lang_explore.md`: one or two lines of a short numeric case, lines out of order, and content inside compressed archives.
- **F4. A mutant that depends on time may not fire under a "3 × the slower reference" limit.**
  - In task 09, one advisory mutant (of 16) failed none of its predicted cases at the calibrated limit, where the plan requires an advisory mutant to fail at least one.
  - It was retired privately, with its prediction, observation and timings recorded. The decision was made with the coai consultants: retire it, and neither tune the limit nor enlarge the cases until it fires.
  - The other mutants all behaved as predicted: 14 of 14 (task 02), 14 of 14 (03), 15 of 15 (09).
  - For v2: expect a limit derived from the references to let constant-factor differences pass. A behaviour the task must reject needs a rule in the spec, not a time-based mutant. Owner: v2's calibration story in E2 (the mutants and limits per task).
- **F5. Paid runs need the machine to themselves.**
  - The quiet gate refused task 03's first pair twice, each time after 30 minutes. The first time, a coder's test suites kept all 14 CPUs busy. The second time, a leak check of mine that never finished kept one CPU at 100%.
  - Nothing was spent either time, and the gate was right both times.
  - For v2's rounds: development work and paid runs need separate windows, and the run scheduler should list the foreign processes it waits on. Owner: v2 epic E3 (the runs and their quiet gate).
- **Who carries these.** The team lead takes F1–F6 into the v2 plans: F2 into E1.S8a's runner (part ii-b or a fix forward), F3 into E1.S1, F4 into E2's calibration, F5 into E3, and F1 and F6 as proposed plan changes for the owner.
- **F6. The build cost did not show.** The predicted C# overhead outside model calls (F1 of task 1's analysis) did not appear in 8 of the 9 part-2 pairs. These sessions build a few times at most. Their self-testing dominates the time outside the model, and that varies more by task and pair than by stack.

## 5. Part 2 against the prediction (plan § 4, recorded before the run)

Task 01's predictions belong to the task-1 plan, and its analysis (§ 3) compares them.

| measure | 02 predicted / observed | 03 predicted / observed | 09 predicted / observed |
|---|---|---|---|
| cost per session | $0.20–0.80 / $0.17–0.22 | $0.50–3.00 / $0.30–0.42 | $1.00–6.00 / $0.30–0.58 |
| wall per session | 1–5 min / 1.0–1.3 min | 5–25 min / 1.6–4.4 min | 10–45 min / 1.8–4.1 min |
| accepted per stack | 3 / 3 and 3 | 1–3 / 3 and 3 | 0–3 / 3 and 3 |
| trap hits per stack | 0–1 / 0 and 0 | 1–3 / 0 and 0 | 1–3 / 0 and 0 |
| C# > TS outside the model | most pairs / 0 of 3 | no prediction / 0 of 3 | no prediction / 1 of 3 |
| harness | clean / clean | clean / clean | clean / clean |
| total spend | $10–40 for 02, 03 and 09 / $5.58 | | |

## 6. What this does not settle

- Whether C# or TypeScript is cheaper or faster for an agent. n = 3 per cell, the drafts are ungated, and the harness is not v2's. Every C#/TS ratio here is inside the spread between pairs.
- Whether these tasks are easy, or known to the model. F1 names both readings, and this design cannot separate them.
- Anything about the process arm (conventions, gates, PRs). Only bare sessions ran.
- Model and CLI versions other than the ones pinned in each note.
