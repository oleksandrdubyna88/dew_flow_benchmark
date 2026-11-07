# F1.S2: what ten scoring passes cost, measured on kept trees (2026-11-07)

> Status: **measured once, 2026-11-07, on the owner's host, for the quick run's task trees. Exploratory, not evidence.** Counts, times and sizes only: no case id, no pass-by-pass case table and no private path. The numbers below are
> the ones in `spikes/lang-explore/inputs/growth.json`; the reading of what they bound is in [module_lang_explore.md](../module_lang_explore.md), section "Scoring a quick-run session".

## What was run

A free scoring (no model was called) of kept trees from one finished quick-run series, with the new pass driver, the real pass child under `taskset`, the real build box and the real execution layer, on the scoring CPUs
(the measuring command re-executes itself under `taskset -c 6,7`, and the passes alternate between the 2-CPU profile `6-7` and the 8-CPU profile `0-7`). Three subjects:

| subject | what it is |
|---|---|
| the known intermittent C# session | the session the first quick run showed passing or failing by chance on the scoring CPUs |
| an accepted C# session | a session of the same series that the first scoring accepted |
| an accepted Node session | the same, in the other stack |

The measurement ran under its own temporary guard: 60 GiB of free disk required before the first subject was copied (676 GB were free), and a write ceiling of 20 GiB on the measurement folder, checked after every attempt. The folder
held 3.0 MB at the end. Nothing in the series it read was written.

## The verdicts

| subject | verdict | passes failing | attempts used |
|---|---|---|---|
| the known intermittent C# session | `intermittently incorrect` | 5 of 10, all five on the 2-CPU profile, none on the 8-CPU profile | 10 |
| an accepted C# session | `accepted` | 0 of 10 | 10 |
| an accepted Node session | `accepted` | 0 of 10 | 10 |

So the defective tree reads `intermittently incorrect` on the scoring CPUs under the new rule, and two sessions that the first scoring accepted read `accepted` 10 of 10 under both profiles at the registered limit
(512 MiB). In this measurement the defective tree failed every 2-CPU pass and passed every 8-CPU pass: the profile reached the program, which is what alternating the profiles is for.

## The times

Ten passes, the build left out (the sum of the ten attempts' own walls):

| subject | ten passes | one pass, on average | the build |
|---|---|---|---|
| the known intermittent C# session | 306,320 ms | about 30.6 s | 3,398 ms |
| an accepted C# session | 310,155 ms | about 31.0 s | 3,409 ms |
| an accepted Node session | 298,578 ms | about 29.9 s | 4,365 ms |

About five minutes of scoring per session with no retry. The worst case (every pass tried three times, the first two attempts of each pass made environment attempts through the same code path) took 311,565 ms of attempt time on the first
subject: the made-up attempts cost nothing, so the worst case in time is bounded by the thirty attempts' own limit (each environment attempt is also bounded by the pass child's deadline, the layer's ceiling plus 120 s, which a real hung
attempt could use in full). The real ten-pass time is what a pair's scoring costs: two sessions, about ten minutes.

## The sizes

| figure | bytes |
|---|---|
| the largest built tree of a subject (the Node tree, with its packages) | 57,888,615 |
| the C# trees | 1,941,750 and 944,843 |
| the retained size of ten passes (`score/` without the built copy, the largest of the three subjects) | 800,952 |
| the retained size of the worst case (thirty attempts) | 809,061 |
| the worst case of a pair: 2 trees + 2 worst-case retained sets (`pairWorstCaseBytes`) | 117,395,352 |

The disk rule (halt a pair below twice the pair's worst case) therefore needs about 235 MB free before a pair, against 676 GB free on the host today. What a session leaves behind is small once the built copy is removed; the
built copy lives only while the scoring is open.

## What this does not say

- **The host was shared.** The quiet check refused for 32 minutes in a first try, and for nine minutes in a second, because other sessions kept the host busy (a mean foreign CPU of 1 to 9 against the registered 0.5); the next
  60-second window was quiet and the measurement started then. Another session may have used the host during the 25 minutes the scoring lasted; the times are one measurement, not a distribution. A repeat on a quiet host is cheap.
- **The references of that task were not measured.** The task has one reference as a Python script that its `run.sh` starts from a path outside the tree; it cannot run in the box, and the data path takes only the two stacks.
  F2.S1's per-stack references make that measurement possible; the pre-registration's projection for 66 sessions uses the three subjects above until then.
- **Ten passes are a budget, not a proof.** A race that shows often on the scoring CPUs is caught (the defective tree fails five of five 2-CPU passes); a rarer one can pass all ten.
