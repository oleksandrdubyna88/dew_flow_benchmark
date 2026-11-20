# Pre-registration statement — language comparison v2, Stage 1

Written on 2026-11-20, before the pre-registration tag `prereg/lang-v2-<yyyymmdd>` and before any paid session of the
series, the pilot included. This file is registered by that tag. It states what is run. How the results are read is
`analysis-contract.md` beside it; the prediction is `prediction.md`.

## What is run

- **The question** is the parent plan's *The goal, narrowed*, quoted word for word at the top of `analysis-contract.md`:
  on six tasks of two kinds, with Claude Code, how do C# on .NET and TypeScript strict on Node with Fastify compare in
  cost per correct solution, and beside it in success rate, tokens and decomposed time, for a bare agent with the task
  prompt only.
- **The six tasks** are the daemons 05, 07 and 10 and the web APIs 04, 06 and 08. Two are **known** (05, 06) and four are
  **original** (04, 07, 08, 10). The specs are the frozen texts of the freeze `spec-frozen-B-20261118`, and the workspaces
  are those of the task repository at its workspace commit W, both as `spikes/lang-bench/inputs/specs.d/spec-states.json`
  records them.
- **The arm** is **bare**: one Claude Code session per task and stack, with the registered prompt of `prompts-lang.json`
  and nothing else. No gate, no plan and no review run inside the session. The process arm is Stage 2 and is not run here.
- **The model** is the alias and the effort that `explore.json`'s `cli` entry pins (`opus`, effort `high`), run by the one
  CLI copy that entry pins by its version, its path and its sha256.
- **The tools** the agent has are v1's bare tool list (`BARE_TOOLS` of `spikes/minigit-bench/lib/launch.mjs`): Agent,
  Bash, Read, Edit, Write, Glob, Grep and TodoWrite. The bare arm lists no MCP server. The box reaches the model only
  through the credential broker, and other hosts only through the CONNECT proxy, within v1's bare host list
  (`EGRESS_HOSTS.bare` of `spikes/minigit-bench/lib/egress-hosts.mjs`); the isolation row G-8 checks it.
- **The schedule** is `schedule.json`: five rounds, each round every task once, 30 pairs and 60 data sessions; the masks
  of all 30 cells; ten scoring passes per session on two CPU profiles; the cap of $50 per session; the pilot and the A/A;
  the read-back and the re-run rules. The order of the slots and the read-back slots are drawn from the seed, whose hash
  is `seed.sha256` and which is revealed after the A/A.
- **The pilot** is one A/A pair (two identical bare sessions of task 05 on Node) and two pilot pairs, task 10 and task 04,
  each in both stacks. All six sessions are void spend and never data.
- **The certifying host** is the one machine whose identity the manifest records as `host` (its name, its kernel and its
  cgroup version, as a hash). Every data session runs on it, each program in its own systemd user scope, with
  `memory.peak` and `memory.events` kept. One memory value holds for all six tasks (D72).
- **The prices** are `prices-<yyyy-mm-dd>.json` of this folder, read from the public pricing page on the day these files
  were generated, with the page's snapshot beside it and its sha256 in the file's `provenance`. Its `cliRates` list is
  empty for the pinned CLI version, **by design**: `read-prices.mjs` keeps a CLI rate only where it was observed on the
  pinned CLI version, and no rate has been observed on this version yet. On the series' first day the prices are read
  again as a check only; a change is a dated amendment line before the first paid launch, never a change of the
  registered figures.
- **The cost cross-checks** T1 to T5 run with `crosscheck-policy.json`, which starts from v1's registered values. Its free
  fields (`t3`, `t5`, `decidedOn`, `reason`) may be set after the A/A by a dated line.

## The identities

The identity table of this pre-registration is `manifest.json`, written by `prereg/write-manifest.mjs` from the live
identities after every other file of this folder but the seed's hash was committed. It is not repeated here: a table
typed into this file would be a typed value, and this file's own hash is one of the identities the manifest holds.

## The claim and its stated limit

Five pairs per task, 30 pairs in all (D73). Five pairs separate per-task medians only at about 1.5 times, and the 30 pairs
read the pooled direction. **Neither reading can show a difference of 20 %.** The report repeats this beside every ratio.
The result holds for these six daemon and web tasks only. The olympiad tasks are exploratory controls outside the claim,
cited and never pooled, and nothing here is a claim about the languages in general.

## When this was written

**The pre-registration was written after an exploratory run of the same six tasks; that run is not data.** (Threat T28.)
The quick run of 2026-11-05 (`research/lang-v2/EXPLORE_quick_2026-11-06_analysis.md`) was seen before the specs' fixes,
before this pre-registration and before `prediction.md`, which says when it was written. Its task 07 cost gap is
confounded by a memory statement that differed from the enforced limit and is never read as a language result (threat
T26).
