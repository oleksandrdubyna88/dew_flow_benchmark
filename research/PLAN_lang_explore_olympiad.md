# PLAN — exploratory pilot, part 2: three more olympiad specs of the language comparison (02, 03, 09), on the v1 bare harness

> Status: **IMPLEMENTED, 2026-11-01.** Run 2026-11-01: tasks 02, 03 and 09, three pairs each, 18 of 18 sessions accepted, $5.58; every part-2 verdict hand-checked (966 of 966); notes and the analysis [EXPLORE_olympiad_2026-11-01_analysis.md](lang-v2/EXPLORE_olympiad_2026-11-01_analysis.md) committed through the coai document gate. Open: `prune`, after this branch merges. Deviations: § Deviations at the end. One unit, no epics. Owner-authorised on 2026-11-01 ("prepare
> tasks 02, 03 and 09 and run exploratory C#/TS pairs on them tonight": yes). **Exploratory, not evidence**, exactly like its
> parent [PLAN_lang_explore_task1.md](PLAN_lang_explore_task1.md) (implemented and run 2026-11-01, series
> `x-20261101-13cc`): it is not part of `todo/PLAN_lang_comparison_v2.md`'s pre-registered series and replaces none of its
> steps. This plan reuses every decision of the task-1 pilot (its D-X1 … D-X10, its scoring in § 4, its tests T-X1 … T-X14)
> and states only what changes. Budget: no total set by the owner; **$50 per session** as before; this plan adds a stop
> rule (D-O7).

## Plan changes

> Epoch 1 · approved 2026-11-01 · by reviewers · state: open

- 1.1 · 2026-11-01 · agent · contract — some mutants are advisory: a mutant whose failing set depends on time, stack depth or pipe chunking carries `"strict": false`; calibration records its actual set and requires only that it fails at least one case
  (One cause: found while the three tasks' material was written — a mutant that fails by a time or memory limit, by a
  recursion-depth threshold or by how the runner feeds the pipe has no exact failing set that is a property of its logic,
  so D-O3/§5's "every mutant caught as predicted" would stop calibration on noise. Strict mutants (every logic defect)
  still must fail exactly their predicted set. The advisory ones, with their reasons, are in each manifest's notes:
  task 02 three of fourteen, task 03 two of fourteen, task 09 seven of sixteen. The ledger heading was missing since the
  approval commit (`ce79a62`); written here with that date. The first change of epoch 1; no stop.)

## 1. The goal

Run three harder olympiad tasks — 02 *Open areas*, 03 *Wrapping arithmetic*, 09 *Switched routes* — as bare C# ∥ TypeScript
pairs on the same harness as task 1, to see how cost, time and correctness scale with difficulty before v2's own harness
exists. Task 1 took 0.5–0.8 min and $0.13–0.23 per session, its traps were never hit, and it separated nothing (its
analysis, F2). These three tasks are the plan's 5–10, 20–30 and 45–60 minute guesses.

## 2. What exists — verified 2026-11-01

| Piece | State |
|---|---|
| The pilot harness `spikes/lang-explore/` (task 1 only) | on `main`; 151 tests; `inputs/explore.json` pins ONE spec (`specs.spec01`), ONE private-notes file and ONE held-out folder `~/rsd/lang-v2/explore/heldout/`; the note path is `EXPLORE_task1_<date>.md` |
| Spec drafts 02, 03, 09 | task repository branch `docs/specs-olympiad` at `3f115d1` (not gated, not frozen); sha256 `8705219d…86710` (02), `5d1aaab9…d3505` (03), `afa52d53…907d4` (09); FORMAT v2.1 on that branch sha256 `4a4e86c4…c1be`. Each spec's § 6 holds its three visible samples inline (exact bytes); no sample files exist yet |
| Limits in the specs | floors **1 s** and **256 MB**; registration: at least 3 × the slower reference's wall, at least 2 × the larger reference's peak, memory by cgroup `memory.peak` |
| Private test-design notes | `~/rsd/lang-v2/test-design/` (one note per task) (mode 0600, outside git; sha256 `9f982b2f…3038`, `51d3a077…ec08`, `bd8ce97e…15ee`) |
| Stacks of the pilot | host SDK 10.0.114, Node 26.12.1, TypeScript 6.0.3 offline (the pilot's D-X3/D-X4); E1.S5's pinned toolchains exist but are not merged and are not used here (§ 6) |

## 3. Decisions

| Id | Decision |
|---|---|
| D-O1 | **Scope:** tasks 02, 03, 09; bare arm only; **3 pairs per task** (18 sessions), C# ∥ TS started in the same tick on disjoint masks, as D-X1. Order: **02, then 03, then 09**; pairs one at a time, and the team lead reads each pair's records before paying for the next (D-X1's barrier). Task 1's series, records and note are untouched (append-only). |
| D-O2 | **`--task NN` on every command** (`init`, `calibrate`, `rehearse`, `run`, `readback`, `status`, `note`, `prune`, `leakcheck`). `inputs/explore.json` gains a `tasks` map — per task: the spec path and sha256, the private-notes path and sha256, the held-out folder — and keeps every task-1 field exactly as recorded (task 01 is the map's first entry, built from them). A series records its task; a command given a task that differs from its series' task refuses (exit 4). |
| D-O3 | **Per-task private material** under `~/rsd/lang-v2/explore/heldout-NN/` (mode 0700, outside every git tree, never bound into an agent box): `gen.py` (a Python reference that computes every expected output), **at least 15 held-out cases** including the largest the spec allows and every trap case the task's private notes name, one C# and one TS reference in workspace shape, **at least 6 mutants** each with its predicted failing set, and `manifest.json` with every file's sha256. Written by Sonnet coders from the spec and the task's private notes; the references are written apart (one coder per stack, neither sees the other's code). **An independent oracle**, because two references and `gen.py` can share one misreading of an ungated draft: (a) a third coder writes `brute.py`, a deliberately naive solver by a different method (exhaustive search, direct simulation), run on every case small enough for it, and its outputs must equal `gen.py`'s; (b) the team lead derives by hand, from the spec text alone, the expected output of at least five small cases per task, including every boundary and trap case small enough to work by hand, and records them in `handcheck.md` beside the manifest. Any disagreement stops calibration until the spec or the material is corrected (a spec correction is a dated note in the draft, named in the task's note). No case value, mutant or trap name ever enters git (D41). |
| D-O4 | **Workspace and samples:** as D-X6, per task. `samples/` is generated from the spec's § 6 table, byte for byte; the generator refuses a table it cannot parse. |
| D-O5 | **Limits per task:** calibrated as D-X8 with the spec's own rule, and the harness floors OVERRIDE the specs' lower floors (1 s / 256 MB): time limit = **max(2 s, 3 × the slower reference's slowest case wall)**, memory limit = **max(512 MiB, 2 × the larger reference's peak)** — unconditionally (the 2 s / 512 MiB floors absorb the harness's ~115 ms start-up, task-1 analysis F1). Recorded in `calibration-NN.json`. |
| D-O6 | **Stacks and agent:** unchanged from the task-1 pilot (D-X2 … D-X5, D-X7), so the four tasks are comparable with each other. The difference from v2's pins is named in every note, as before. |
| D-O7 | **Caps and a stop rule:** $50 per session, 75 min wall, 45 min stall (D-X2) — the hard runaway guard, unchanged (the owner's cap). New, a **pause point at $60**: the plan's spend is the sum of the broker-charged USD in the `ledger.ndjson` of every series this plan creates (the same ledger the pilot already writes). `run --task NN --pair k` **refuses before launching** (exit 2, a stop, naming the spend) when `spent + projected > $60`, where `projected` = 2 × max($5, 3 × the most expensive session of this plan so far) — a pair's plausible worst case, since both sessions run at once and cannot be stopped between pairs mid-flight. Only `--allow-over "<the owner's words and date>"` passes it, and that text goes into the pair record. If the owner declines, the plan ends there: what ran is recorded and noted, and § 9 counts a task whose pairs stopped at the pause point as complete with the number of pairs it has. |
| D-O8 | **Outputs:** one generated note per task, `research/lang-v2/EXPLORE_task<NN>_<series date>.md` (the task-1 note generator with the task in its title and path), and ONE written analysis `research/lang-v2/EXPLORE_olympiad_<date>_analysis.md` across tasks 01, 02, 03, 09, against § 4's predictions. Counts and rates only (D41). |

## 4. Prediction — recorded before the run

| Measure | 02 Open areas | 03 Wrapping arithmetic | 09 Switched routes |
|---|---|---|---|
| cost per session | $0.20–0.80 | $0.50–3.00 | $1.00–6.00 |
| wall per session | 1–5 min | 5–25 min | 10–45 min |
| accepted (of 3 per stack) | 3 per stack | 1–3 per stack | 0–3 per stack |
| trap hits | 0–1 per stack | 1–3 per stack | 1–3 per stack |
| `active − model_api` | C# > TS in most pairs (the build) | no prediction | no prediction |
| harness | 0 voids; T1–T5 hold; the partition sums to the wall; every session ≪ $50 | same | same |

Total predicted spend: **$10–40**. Why: task 1 cost $0.13–0.23 for a one-pass program; these specs are 1.3–1.9 × longer,
need an algorithm (02: flood fill on up to 10⁶ cells; 03: a full expression grammar with wrap-around; 09: the IOI-hard
query problem), so more turns and more self-testing are expected.

## 5. Build order

1. **Harness (Sonnet coder):** D-O2, D-O4, D-O5, D-O8 in `spikes/lang-explore/`; tests T-O1 … T-O6 below; RED-GREEN-RED;
   the task-1 records still read (`status`, `readback`, `note --out <tmp>` over `x-20261101-13cc` give the same output as
   before).
2. **Private material, per task (three Sonnet coders in parallel, private folders only):** D-O3. Each runs its references
   through the pilot's judge (`runner/run_samples.py`) on the visible samples and its own cases until both pass, and runs
   each mutant to confirm its predicted failing set.
3. **Review:** an Opus review of the harness diff and of each task's manifest (counts, the mutants' coverage of the
   private notes' trap list — by count and id, never quoted); the coai code gate on the harness diff.
4. **Per task, in order 02 → 03 → 09:** `calibrate --task NN` (both references 100 %, every mutant caught as predicted,
   limits recorded) → `rehearse --task NN` ($0) → `init --task NN` → pairs 1–3, the first pair's verdicts read by hand
   (all visible cases and at least 20 held-out verdicts across the pair) → `note --task NN` → `leakcheck`. **A void** is re-run once; if the re-run voids too, the cell stays `void` (unpaired), the pair is recorded as incomplete, and the run moves on to the next pair — no second re-run.
5. **The analysis** (D-O8), committed through the coai document gate; a PR; merge when green. **Only then** `prune --task NN` for each task: it removes the reproducible copies (the seeded caches, the scoring copies) and KEEPS every record, transcript, ledger, `calibration-NN.json` and the whole `heldout-NN/` folder (the evidence a later correction needs), kept until v2's research record is written, then the owner decides, as the task-1 pilot's growth table says.

## 6. Differences from v2 (named in every note)

The task-1 pilot's list (no execution layer, the pilot's olympiad-only judge, host SDK 10.0.114 and Node 26.12.1 instead of
E1.S5's pins, no pre-registration, seal or A/A, CLI 2.1.310), plus: **the three specs are ungated drafts** (v2 freezes
gated specs, E1.S2), and **the held-out suites are the pilot's** (v2's are E2.S2's, validated jointly with final
references).

## 7. Risks

| Risk | Guard |
|---|---|
| A wrong reference makes a correct agent fail (09 is hard) | two references written apart must agree on every case and on `gen.py`'s outputs; any disagreement stops calibration |
| A held-out case leaks into the public note | counts only; `leakcheck` with each task's expected outputs and its private notes' shingles before the commit (as the task-1 pilot) |
| A runaway session | the $50 / 75 min / 45 min caps; D-O7's $60 stop rule |
| The machine is loaded by other sessions' work | the quiet gate waits (task-1 analysis: it held launches 1–12 min); scoring under load is flagged |
| Task-1 records disturbed | T-O2 |

## 8. Test plan

| Id | Test |
|---|---|
| T-O1 | `--task` routes every path (held-out folder, calibration file, series, note path); a missing or unknown task is exit 4; a command whose task differs from its series' task is exit 4 |
| T-O2 | Task 1 is unchanged: `explore.json`'s task-1 values are byte-equal to the recorded ones; `status`/`readback`/`note --out <tmp>` over a fixture copy of `x-20261101-13cc` give the pre-change output |
| T-O3 | `samples/` from a spec's § 6 table equals the table's bytes (`⏎` → LF), for 02, 03 and 09; a malformed table is refused |
| T-O4 | Calibration per task: the limits follow D-O5's rule and floors; a reference that disagrees with `gen.py` stops it |
| T-O5 | The leak needles of task NN come from `heldout-NN/` only; `leakcheck` reads every task's material |
| T-O6 | The $0 rehearsal per task end to end |
| T-O7 | The pause point: a ledger summing to $58 with a most expensive session of $1 refuses pair k (`58 + 2 × 5 > 60`), exit 2 naming the spend; `--allow-over` passes and is recorded; the sum reads every series of the plan, never another plan's |
| T-O8 | The oracle: a `brute.py` that disagrees with `gen.py` on one case stops calibration naming the case id only |

## 9. Definition of Done

- [x] The harness change with its tests; the pilot suite green under `BENCH_REQUIRE_SANDBOX=1` (302 of 303; the one skip is the leak scan's perf test, run only with `LEAK_PERF=1` — a deviation, see below), and v1's suite green (1232 of 1232, 2026-11-01). Merged with this branch's PR.
- [x] Per task: private material complete with `brute.py` and `handcheck.md` agreeing; calibration clean; rehearsal PASS; three pairs run (a void re-run once; a twice-void cell recorded void), or fewer when the $60 pause point stopped the plan and the owner declined; every session record holds every v1 metric and the explore fields.
- [x] Three generated notes and one analysis committed; `leakcheck` clean. - [ ] Then `prune` run (records and `heldout-NN/` kept).
- [x] This plan promoted to `research/` with its deviations (2026-11-01).

END OF PLAN

## Deviations (recorded 2026-11-01, after the run)

- **The leak check was redesigned** (`spikes/lang-explore/lib/leak-*.mjs`; v1's matcher untouched). With task 09's ~5.5 M
  case lines, v1's needles × bytes matcher ran 88 minutes and did not finish. The new check uses tiers: substrings for
  small sets; exact lines; 3-line windows; 64-character chunks, each verified. It adds a pre-material baseline rule (a
  `baselineCommit` per task, checked to predate the held-out folder) and the committed-runner collision rule. It passed the
  coai code gates and runs in about 50 s over all four tasks. Its perf test is skipped unless `LEAK_PERF=1`, so the suite
  has one skip.
- **One advisory mutant of task 09 was retired before calibration.** It failed none of its predicted cases at the limit the
  spec's rule sets, and § 5 requires at least one. It was decided with the coai consultants: retire it, and neither tune the
  limit nor enlarge the cases. The mutant, its record and the earlier manifest are kept privately.
- **The hand-check of task 09 re-ran the programs.** The harness keeps only the first 64 KiB of each output, so each
  session's program was re-run on every held-out input in an isolated box, and its full output was compared (analysis F2).
- **Task 03's first pair launched only at its third attempt.** The quiet gate refused it twice, each time after 30 minutes
  (a coder's test suites; then a stuck leak check). No spend.

