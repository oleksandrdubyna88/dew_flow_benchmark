# Pre-registration statement — language comparison v2, Stage 1

Written on 2026-11-20. This file is registered by the pre-registration tag `prereg/lang-v2-<yyyymmdd>`. It states what is
run; how the results are read is `analysis-contract.md` beside it, and the prediction is `prediction.md`. Every fact
below names the registered file and field that supports it. "The harness" means the folders whose identity is
`manifest.json`'s `harness`, which the tag freezes.

*Source of the date:* the date line above, and the tag's commit, which holds this file. A paid Stage 1 series is made
only under that tag (the harness's `stage-init` refuses a paid series without it), so the file precedes every paid
session, the pilot included.

## What is run

- **The question** is the parent plan's *The goal, narrowed*: on six tasks of two kinds, with Claude Code, how do C# on
  .NET and TypeScript strict on Node with Fastify compare in cost per correct solution, and beside it in success rate,
  tokens and decomposed time, for a bare agent with the task prompt only. *Source:* the first quoted block of
  `analysis-contract.md`.
- **The six tasks** are the daemons 05, 07 and 10 and the web APIs 04, 06 and 08. Two are **known** (05, 06) and four are
  **original** (04, 07, 08, 10). *Source:* `schedule.json` `tasks` and `kinds`; the quoted block *The claim and its stated
  limit* of `analysis-contract.md`. The specs are the frozen texts of the freeze `spec-frozen-B-20261118`, and the
  workspaces are those of the task repository at its workspace commit W. *Source:*
  `spikes/lang-bench/inputs/specs.d/spec-states.json`; `manifest.json` `spec.<NN>`.
- **The arm** is **bare**: one Claude Code session per task and stack, sent the registered prompt and nothing else.
  *Source:* `prompts-lang.json` `tasks.<NN>.<stack>.text` and its `sha256`; the manifest's `prompts`.
- **The model** is the alias `opus` at effort `high`, run by the one CLI copy that is pinned by its version, its path and
  its sha256. *Source:* `explore.json` `cli` (`model`, `effort`, `version`, `pathParts`, `sha256`); the manifest's
  `cliSha256`.
- **The tools** the agent has are v1's bare tool list: Agent, Bash, Read, Edit, Write, Glob, Grep and TodoWrite. The bare
  arm lists no MCP server. The box reaches the model only through the credential broker, and other hosts only through
  the CONNECT proxy within v1's bare host list; the GO row G-8 checks it in the pilot. *Source:* the harness:
  `BARE_TOOLS` and `mcpConfigFor` of `spikes/minigit-bench/lib/launch.mjs`, `EGRESS_HOSTS.bare` of
  `spikes/minigit-bench/lib/egress-hosts.mjs`; `pilot-rules.json` row G-8.
- **The schedule**: five rounds, each round every task once, 30 slots and 60 data sessions; the masks of all 30 cells;
  ten scoring passes per session on two CPU profiles; the attempts per cell (a void is re-run once); the cap of $50 per
  session; the read-back pairs, named from the seed. *Source:* `schedule.json` `rounds`, `slots`, `dataSessions`,
  `masks`, `scoring`, `attempts`, `caps` and `readback`. The order of the tasks within each round is drawn from the seed
  by the harness (`spikes/lang-explore/lib/stage-schedule.mjs`).
- **The pilot** is one A/A pair (two identical bare sessions of task 05 on Node) and two pilot pairs, task 10 and task 04,
  each in both stacks, run in that order, the A/A first. All six sessions are void spend and never data. *Source:*
  `schedule.json` `pilot`; the order is the harness's `pilotStep`; `pilot-rules.json` `pilot`.
- **The certifying host** is the one machine whose identity the manifest records as `host` (its name, its kernel and its
  cgroup version, as a hash, never printed). *Source:* `manifest.json` `identities.host`. Each program runs in its own
  systemd user scope under the registered memory policy and the one memory value. *Source:* the manifest's
  `execLayerTree`, `memoryPolicy` and `memoryValue`; `spikes/lang-explore/inputs/memory-limit.json`.
- **The prices** were read from the public pricing page on the day these files were generated. *Source:*
  `prices-2026-11-20.json` (`readOn`, `models`, and `provenance` with the URL, the time and the sha256 of the page's
  snapshot `prices-2026-11-20.source.md` beside it); the manifest's `prices`. Its `cliRates` list is **empty, by
  design**: a CLI rate is kept only where it was observed on the pinned CLI version. *Source:* the price file itself, its
  `cliRates` and its first `notes` entry.
- **The cost cross-checks** T1 to T5 run with `crosscheck-policy.json`, which starts from v1's registered values. *Source:*
  `crosscheck-policy.json`; the manifest's `policy`.

## The two amendment deadlines of the inputs

- **The prices.** On the series' first day the page is read again, as a check only. A changed price is recorded as a
  dated amendment line **before the first paid launch, which is the A/A's**. The registered figures of the price file
  never change. *Source:* `analysis-contract.md` section 5; `amendments.md`.
- **The policy of the cost checks T3 and T5.** Its free fields `t3`, `t5`, `decidedOn` and `reason` may be set **after the
  A/A**, each change by its dated `input:crosscheck-policy.json:<12 hex>` line. *Source:* `analysis-contract.md` section 5
  and pilot row G-4 (`pilot-rules.json`); the line form in `amendments.md`.

## The seed

The seed orders the tasks of each round and names the read-back slots. `seed.sha256` is written by `seed-commit` as **the
last file before the tag**, so it is in the tag's tree. The seed itself stays secret, outside git, until `seed-reveal`
writes `seed.txt` after the A/A. The registered hash is checked twice: `seed-reveal` refuses a seed that does not hash to
the committed `seed.sha256`, and the pre-registration guard refuses a `seed.txt` that does not hash to the tag's
`seed.sha256` (and any `seed.txt` at the tag). *Source:* `seed.sha256` in the tag's tree; the guard and the seed verbs of
the harness.

## The identities

The identity table of this pre-registration is `manifest.json`, written by `prereg/write-manifest.mjs` from the live
identities after every other file of this folder but the seed's hash was committed. It is not repeated here: a table
typed into this file would be a typed value, and this file's own hash is one of the identities the manifest holds.

## The claim and its stated limit

Five pairs per task, 30 pairs in all. Five pairs separate per-task medians only at about 1.5 times, and the 30 pairs read
the pooled direction. **Neither reading can show a difference of 20 %.** The report repeats this beside every ratio. The
result holds for these six daemon and web tasks only; the olympiad tasks are exploratory controls outside the claim, and
nothing here is a claim about the languages in general. *Source:* the quoted blocks *The claim and its stated limit*,
*Outside the claim*, D73 and the threat rows T3 and T27 of `analysis-contract.md`.

## When this was written

**The pre-registration was written after an exploratory run of the same six tasks; that run is not data.** That run is
the quick run, which was seen before the specs' fixes, before this pre-registration and before `prediction.md`; its task
07 cost gap is never read as a language result. *Source:* the threat rows T28 and T26 of `analysis-contract.md`;
`prediction.md`, which names that run and says when it was written.
