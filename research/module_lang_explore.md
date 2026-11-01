# Module — lang-explore: the exploratory pilot of language-comparison task 1

> Status: **built and run, 2026-11-01.** `spikes/lang-explore/` exists with its suite (151 tests); series `x-20261101-13cc`
> ran three paid pairs (6 of 6 sessions accepted, $1.04). The design record is
> [PLAN_lang_explore_task1.md](PLAN_lang_explore_task1.md); the results are
> [lang-v2/EXPLORE_task1_2026-11-01.md](lang-v2/EXPLORE_task1_2026-11-01.md) (generated) and
> [lang-v2/EXPLORE_task1_2026-11-01_analysis.md](lang-v2/EXPLORE_task1_2026-11-01_analysis.md) (the reading).
> **Exploratory, not evidence**: nothing here is part of v2's pre-registered series (`todo/PLAN_lang_comparison_v2.md`).
> This file describes what is built; a sentence here about something that does not run is a bug in the file.

## Purpose

Run the easiest task of the v2 language comparison (spec 01 in `dew_flow_ai_language_comparison`) as bare C# and
TypeScript sessions on the **v1 harness**, before v2's own harness exists, to see v2's cost and time scale and to check
that the harness behaves on a task that is not minigit. It imports `spikes/minigit-bench/` (never copies it) through four
optional seams whose defaults are byte-identical to v1 (W1–W4, described in [module_minigit_bench.md](module_minigit_bench.md)).

## Commands (`node spikes/lang-explore/explore.mjs <command>`)

| command | money | what it does |
|---|---|---|
| `prepare` | free; the only network use | TypeScript 6.0.3 from the registry by integrity, the Node cache snapshot, the C# snapshot re-hashed, the spin calibration |
| `calibrate` | free | both private references through the judge (30 s ceiling), the time and memory limits, every mutant against its predicted failing cases |
| `rehearse` | free | one pair end to end with v1's stub CLI and stub upstream; series `r-<yyyymmdd>-<4 hex>` |
| `init` | free | a series `x-<yyyymmdd>-<4 hex>` with its CPU-mask draw |
| `quiet` | free | the machine-quiet check alone |
| `run --pair k --series <dir> [--rerun <stack>]` | **paid** | one pair: the quiet check, two sessions launched together on disjoint masks, collect, classify, score, record; a halted or failed pair resumes (no new paid session) |
| `readback`, `status` | free | what each session was launched with against the registered prompt and environment; the series' state and spend |
| `note [--out] [--force]` | free | the public note, generated from the records only |
| `leakcheck [--range A..B]` | free | v1's leak check with the pilot's held-out outputs, reference and mutant lines and the private notes' six-word shingles as needles; plans in `todo/` and `research/` and the pins are public by decision |
| `prune` | free | removes the seeded cache copies and scoring copies after the note; keeps the records |

## Flow of one pair

1. `quiet` (v1's `lib/quiet.mjs`): foreign CPU, foreign agents, memory and the spin probe; it waits (up to 30 min) instead
   of launching on a loaded machine.
2. `lib/workspace.mjs` fills each box with exactly the frozen files of the stack's template (`templates/csharp/`,
   `templates/node/`), the statement and the agent-visible `runner/`.
3. v1 launches the sessions (`lib/session-launch.mjs`): the bwrap box, the CONNECT proxy (no host allowed), the
   credential broker (the only path to the model), the locale `de_DE.UTF-8` through seam W2.
4. v1 classifies and records each session (`record.json`: every v1 metric — the four cost views with T1–T5, tokens with the
   cache split, the time partition). `lib/records.mjs` adds the explore section (pass rates, trap hits as a count, the
   agent's own build/test time, CPC and CPC_all) and the pair record (`pairs/p<k>.json`).
5. Frozen files the agent changed are reported and put back in the scoring copy (`assertInside` refuses any path that
   resolves out of it). `lib/score.mjs` runs `runner/run_samples.py` in v1's scoring box (seam W3): a fresh copy per case,
   each run in its own `systemd-run --user --scope` with `MemoryMax`, `MemorySwapMax=0` and `OOMPolicy=continue`; memory
   is the scope's `memory.peak`.

## Data

- Private, outside every git tree (mode 0700): `~/rsd/lang-v2/explore/` — `heldout/` (generator, cases, references,
  mutants, manifest), `calibration.json`, `spin.json`, and one directory per series: `run-map.json`, `ledger.ndjson`,
  `pairs/`, `s/<rid>/` per session (records, stream, transcripts, egress and broker logs), `private/` (the hand check and
  any trap detail, D41).
- Public: the note and the analysis under `research/lang-v2/`, counts and rates only.

## Known limits (from the run; carried to v2 E1)

The per-case wall has about 50 ms resolution and includes the scope and box start-up; v1's descriptive `build_test`
category does not know this task's scripts (the explore section's own field does); the package list is the shared box
cache; the leak check's default history range starts before main's merges (use `--range origin/main..HEAD`). Details:
the analysis, §4.
