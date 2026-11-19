# PLAN — language comparison v2, F3 stories: the series and the report — the series over six tasks, one pre-registration, the pilot, five paid rounds, the read-back, the record, blind adjudication and the Stage 2 decision line

> Status: **plan only, 2026-11-19: F3's stories document, written when F3 starts. F2 is done (the freeze `spec-frozen-B-20261118` is accepted: C `502b65ad153887464c0c45ebc13e9bbef545c415`, W `f3d256e5fe1f774e189a9850056559b39b995a27`). Nothing of F3 is built. Three steps of it need the owner's explicit go before they run: the push of the pre-registration tag, the pilot's paid sessions, and the paid rounds (§4). Twenty-one open points for the team lead are in §11; thirteen of them must be answered before the pre-registration files are written. Revised the same day for F3's plan round 1 (`proceed`, 2 of 2 reviewers, five findings, all accepted): every harness change merges before the pre-registration's manifest and the tag; a read-back is done and on `main` before the next slot is offered; the disk recovery frees named cache folders only and restores them before the record is hashed; the adjudication writes per-session revised verdicts that the record tools read; the disk reserve is recomputed before every launch, a pilot repeat included.**
> Epic F3 of [PLAN_lang_comparison_v2.md](PLAN_lang_comparison_v2.md) (*Operative plan for epoch 9* with its epoch-10 lines,
> *Epics in force*, F3; build order steps 5 to 7; D69–D76). A stories document keeps no ledger: its changes are entries in the
> parent (`common.plan-change-control`), and writing it when the epic starts is not a change. Read at `main` `5740809f` (#230, the
> freeze record). Scope: `spikes/lang-explore/`, a few seams of `spikes/minigit-bench/` (imported, never copied), new record tools
> under `research/lang-v2/tools/`, the module docs, the pre-registration files and one tag in this repository, and the private
> area `~/rsd/lang-v2/` (the series, the seed, the adjudication packets). The task repository is not changed by F3.
> The form follows F1's and F2's stories documents, [PLAN_lang_comparison_v2_E7_F1.md](PLAN_lang_comparison_v2_E7_F1.md) and
> [PLAN_lang_comparison_v2_E8_F2.md](PLAN_lang_comparison_v2_E8_F2.md).

## 1. The epic, restated (parent, F3)

**Goal.** One pre-registered bare series over six tasks (04, 05, 06, 07, 08, 10), isolated, accounted, balanced, with frozen
identities, run and scored; then a generated record on six tasks with the claim's limit stated, and Stage 2 decided on a date.

**Needs.** F2's tag. It exists and is accepted (the status line above; the record is
`spikes/lang-bench/inputs/specs.d/spec-states.json`).

**The three stories (parent).** F3.S1: (a) the series over six tasks, (b) one pre-registration. F3.S2: (a) one pilot, (b) the
paid rounds, (c) the read-back. F3.S3: (a) the record tools, (b) blind adjudication, (c) the Stage 2 decision line. This
document splits them into pull requests and operator steps (§8). It adds no story and no epic.

**Done when (parent).** The tag preceded the first launch; the pilot is GO; every data session is scored by F1.S2 with its
`record.json`; no round k+1 ran with round k unscored; the record renders from data alone, types no number, and carries the
sentence that the quick run's task 07 is confounded (T26); the decision line is written and dated.

**The numbers the parent fixes, repeated here so no brief restates them differently.** Five rounds, each round every task once,
30 pairs, 60 data sessions (D73). The pilot: one daemon pair, one web pair and one bare A/A pair, six sessions, all void spend
(D16). Ten scoring passes per session, five on CPUs `6-7` and five on CPUs `0-7` (D71). The session masks are A = `0-5` and
B = `8-13`. One memory value, 536,870,912 bytes (D72). A cap of $50 per session and no total ceiling (D7). One tag,
`prereg/lang-v2-<yyyymmdd>`, before the first launch, the pilot included (D26, §9).

**Not in this epic.** Anything of Stage 2 (D76; merge condition 4): the process arm, its pilots, the anchor round, E0, the
task repository's CI. The publication of the references and the trap coverage table, the credential teardown and the promotion
of the parent (E6.S3): they come after the dated decision, and the parent's Definition of Done keeps them. D65's lexical
deviation classes and the per-case CPU time and RSS curve (follow-up work, D74). The olympiad tasks: their notes are cited as
exploratory controls and nothing of them is run.

## 2. What exists — verified at `main` `5740809f`, 2026-11-19

Read by three Sonnet scouts and checked in the files by the writer. Paths are under `spikes/lang-explore/` unless they name
another folder.

- **A pair runner for ONE task exists; a series over six does not.** `lib/series.mjs` is a series of one task: `createSeries`
  (`:128`) writes `explore-series.json` with one `task` and `pairs` (3, from `inputs/explore.json`); the masks come from a random
  draw at `init` and swap every pair (`drawMask` `:24`, `maskOf` `:27`; `MASKS` `:14` are `low: '0-5'` and `high: '8-13'`, the
  parent's A and B); `barrierProblem` (`:65`) refuses pair k+1 until both sessions of pair k are scored or void after their one
  re-run (`MAX_ATTEMPTS` 2, `:16`); `planPair` (`:82`) plans a pair, a void's re-run (`ran unpaired`) or a resume. There is no
  seed, no round, no task order and no mask rule over a schedule. `lib/run.mjs` `runPair` (`:79`) runs one pair under the series
  lock through v1's `runLoop` (`spikes/minigit-bench/lib/bare-series.mjs:250`) and writes the pair record (`finalizePair`,
  `:60`). The verbs are `init --task NN` and `run --task NN --pair k --series <dir>` (`lib/commands.mjs:82`, `:124`).
- **Every session of the engine has the run-map kind `pilot`.** `lib/run-ctx.mjs` `contextOf` (`:53`) sets `kind`, `seed: null`,
  `preregTag: null` (`:60`), `handVerdictsFor: () => []` (`:62`) and a spend total no session reaches (`NO_TOTAL_USD`); `runDeps`
  gives the loop a guard that always passes (`guard: async () => ok(null)`, `:152`). v1's loop treats the kind `data` as v1's own
  two-phase schedule (`nextStep`, `ridOf`, `maskFor`; `bare-series.mjs:234`), and any other kind as a fixed list
  (`fixedStep`, `:37`). v1 writes a ledger line of kind `data` or `void` only for a `data` session
  (`spikes/minigit-bench/lib/bare-settle.mjs:13`, `ledgerKind`); the kinds are `prior`, `rehearsal`, `aa`, `pilot`, `data`,
  `void` (`lib/spend-gate.mjs:10`). There is no line for a re-run.
- **The quick run's pause point covers the same six task ids.** `lib/run.mjs:174` (`pauseOf`) applies `lib/pause.mjs`
  `pauseVerdict` (`:124`) to every task of `quick.planTasks`, with `quick.pausePoint.usd` 150 in `inputs/explore.json`, summed
  over every series of those tasks under the explore root. The quick run spent $67.01. So a Stage 1 pair run through today's
  `run` would stop at about $150 in all, against D7 and D53 (no total, no imported pause point).
- **The cap is already $50 and is enforced by v1.** `inputs/explore.json` `caps`: `capUsd` 50, `wallMs` 4,500,000 (75 min),
  `stallMs` 2,700,000 (45 min). v1 puts the cap into the CLI's `--max-budget-usd` (`spikes/minigit-bench/lib/launch.mjs:50`),
  the broker's admission (`lib/budget.mjs`) and the watchdog; a session that reaches it is `killed: cap`, a data attempt.
- **The verdict, the passes and the memory value are built (F1).** `lib/passes.mjs` `scorePasses`, `lib/verdict.mjs`
  (`PROFILES` `:18`, `verdictOf` `:83`), `VOID_UNSCORED` (`lib/series.mjs:18`), `lib/memory-limit.mjs`, the mask read-back before
  a paid launch (`lib/mask-preflight.mjs:22`) and before every pass (`lib/affinity.mjs`). `run` scores a pair only after both
  sessions ended, inside the same command, so the next pair cannot start while a scoring runs.
- **The disk rule is a function nobody calls.** `lib/growth.mjs:32` `diskRuleVerdict` and `inputs/growth.json`
  (`pairWorstCaseBytes` 117,395,352, measured 2026-11-07 in F1 on kept quick-run trees) exist; no command calls the rule before
  a pair. No reserve check exists. F2.S1's measurements on the twelve references are in the private package records only;
  `growth.json` was not written again.
- **The clear held-out folder is swept at entry and never closed after a pair.** `lib/heldout-reader.mjs:23` `readerHeldout`
  is the one road in for `init`, `run`, `rescore` and `replay` and calls `sweepClearFolder` (`lib/heldout-lifecycle.mjs:196`);
  `closeClearFolder` (`:289`) is called by the verb `heldout-close` only. It removes nothing unless the key's backup copy is
  verified on the share's mount, so the mount must be there for every pair of the series.
- **The frozen record's guard is a table with no caller.** `lib/freeze-consumers.mjs` (`FREEZE_READS` `:26`, `freezeProblems`
  `:52`) lists what a guard reads of `spec-states.json`, `inputs/heldout-frozen.json` and `inputs/memory-limit.json`; only tests
  call it. `spikes/lang-bench/lib/spec-freeze.mjs` exports the launch half's two guards, `specBlobMatches` (`:76`) and
  `workspaceCommitProblems` (`:124`), and `lib/workspaces-check.mjs:111` exports `checkWorkspaces`; their comments name F3.S1 as
  the caller, and nothing calls them outside the freeze tools. `spikes/lang-bench/lib/task-repo.mjs:17` pins W.
- **Memory certification runs at scoring, not before the launch.** `spikes/lang-bench/lib/exec-layer/certify.mjs`
  (`certifyMemory` `:24`, the halt `memory-not-certifiable` `:13`) runs when the layer opens a data run, which is after both
  sessions were paid. T-L29 asks for the refusal before any process starts.
- **v1 has the whole pre-registration machinery, for v1's files.** `spikes/minigit-bench/lib/prereg-guard.mjs`: `PREREG_FILES`
  (`:19`, seventeen files of `spikes/minigit-bench/inputs/`), `HARNESS_DIRS` (`:31`, v1's and `policy-ab-quick`'s folders only),
  `harnessIdentity` (`:47`), `preregGuard` (`:157`: the tag on origin peels to the run-map's commit, every registered blob at
  HEAD equals its blob at the tag, `amendments.md` only grows, `crosscheck-policy.json` may change in `t2` and `t3` only, the
  seed is absent at the tag and hashes to `seed.sha256` after it, the tree under `spikes/` is clean and the harness identity is
  the series' or an accepted one). `lib/seed.mjs` (`commitSeed` `:29`, `revealSeed` `:54`, the shape `mg-` and 16 hex).
  `prereg/*.mjs` write the registered files from the parent plan at a commit of `origin/main`
  (`prereg/parent.mjs:27` `readAtCommit`, which takes the file as an argument). `lib/amendments.mjs` (`parseAmendments` `:60`,
  `handVerdictsFor` `:79`) reads the dated lines. `lib/aa-verdicts.mjs:39` `aaVerdicts` computes the table G-1 to G-14 from an
  A/A's records; the table's text is `todo/PLAN_minigit_stack_bench_E3_bare.md:513-526`. lang-explore imports of all this only
  `harnessIdentity` and `aliasStore`.
- **The pre-registration tag's pattern is known to one reader.** `spikes/lang-bench/lib/cache-store.mjs:205`
  (`PREREG_TAG_PATTERN = 'prereg/lang-v2-*'`): once such a tag exists, locally or on the remote, a registered snapshot can no
  longer be superseded and a superseded one may be pruned. Nothing creates the tag. The only tag in this repository is v1's,
  `prereg/minigit-bare-20261030`. No ruleset protects a tag of this repository.
- **The prices and the crosscheck policy a session is priced with are v1's files.** `lib/run-ctx.mjs` reads them through
  `crosscheckPolicyFrom(V1, {})`: `spikes/minigit-bench/inputs/prices-2026-10-29.json`
  (`spikes/minigit-bench/capture/policy-input.mjs:8`) and `crosscheck-policy.json` (`t1: stop`, `t2: otel-incomplete`,
  `t3: stop`, `t5: investigate`).
- **The CLI copy is v1's.** `inputs/explore.json` `cli`: version 2.1.310 at `~/rsd/minigit/toolchains/claude-2.1.310`, model
  `opus`, effort `high`. v1's `bare-init.mjs` (`initPlan` `:173`, `applyInit` `:223`) is the code that copies and pins a CLI.
- **The bare prompt is the parent's §4.5 text, byte for byte.** `lib/plan.mjs:12` `PROMPT_TEMPLATE`; `inputs/prompts.json` holds
  the olympiad stack sentences and `lib/init-quick.mjs` `quickPrompts` computes the quick ones at `init`.
- **The read-back exists for the prompt and the environment keys.** `lib/readback.mjs:57` `readbackOf` compares a session's
  `plan.json` with the registered prompt, the transcript's first user message and the keys of `argv.json`; the verb is
  `readback` (`lib/commands.mjs:287`). It does not read the binds, the cache hashes, the package list, the scoring box's heap
  keys, the affinity or the spec blob.
- **A generated note exists per task; a series record does not.** `lib/note.mjs:234` `renderNote` writes
  `research/lang-v2/EXPLORE_task<NN>_<date>.md` from the records; `lib/records.mjs` computes CPC over the data attempts and
  `CPC_all` with the voids (`cpcOf` `:168`, `cpcBoth` `:181`) and lists the required metrics of a record (`REQUIRED_PATHS`
  `:88`). v1's record tools are `research/minigit/tools/` (`extract.mjs`, `session.mjs`, `pairs.mjs`, `format.mjs`,
  `render.mjs:115` with generated blocks between `<!-- generated:<name> -->` markers, `replay.mjs:85`, `manifest.mjs`); no test
  holds v1's prose free of typed numbers. `research/lang-v2/` has no `tools/` and no `data/`.
- **Blind adjudication exists for v1's layout only.** `spikes/minigit-bench/adjudicate.mjs` (`packets`, `classify`, `reveal`,
  `class`, `revised`) over `lib/adjudicate.mjs` and `lib/adjudicate-mask.mjs`; `readCells` (`lib/adjudicate.mjs:42`) reads
  `<series>/<cell>/score/heldout-<phase>.json`, which a ten-pass scoring does not write.
- **`prune` removes reproducible folders of any series** (`lib/commands.mjs:296`, `:307`), while the parent's *Growth* says
  nothing of Stage 1 is pruned.
- **CI runs no spike test.** `.github/workflows/ci.yml` has `build-test`, `contract`, `plans` and `workflows · actionlint`;
  the suites of `spikes/` are run by hand and recorded in `research/module_tests.md`. lang-explore has 101 test files.
- **The record's private side.** A series is `~/rsd/lang-v2/explore/x-<yyyymmdd>-<4 hex>/` (`CANONICAL_SERIES`,
  `lib/series.mjs:160`) with `run-map.json`, `ledger.ndjson`, `pairs/`, `s/<rid>/` and `private/`. Free disk was 643 GB on
  2026-11-07.

## 3. Conventions for every story

F1's §3 and F2's §3 hold here unchanged (roles, the merge conditions, red-green-red, test names as sentences, D41, shape,
`-m:4`, commits by path, the commands, docs), with these additions:

- **Three steps wait for the owner's explicit go, and no other step does** (§4). A go is the owner's own words with a date. It
  is written as a dated line into the registered `amendments.md` before the step, and the paid command refuses a launch whose
  scope has no such line (F3.S1 (a), PR 3). An agent's message, a consultant's answer or an earlier go for another step is not
  a go.
- **The harness is fixed by the tag** (plan round 1, finding 1). Every pull request that changes a folder of the Stage 1
  harness identity (F3.S1 (b)) merges before the pre-registration files are generated: PRs 2 to 7, the pilot's, the
  read-back's and the adjudication's tools among them. The manifest holds that identity, the dry run of step T runs on it,
  and the tag freezes it, so the instrument the pilot and the rounds use is the one the tag names. `stage-init` refuses any
  other harness. After the tag a harness change is an instrument amendment with a dated line, possible only before the first
  data session; after it, it is refused (v1's rule, `spikes/minigit-bench/lib/run-map.mjs:15`, `hasDataSession`). The record
  tools live under `research/lang-v2/tools/`, outside that identity, as v1's do.
- **No task-repository pull request.** Merge condition 5 does not bite in F3. Merge conditions 1 to 4 do, and §8 says which for
  each pull request.
- **Whole runs.** A pull request that touches `spikes/minigit-bench/` runs that suite too, both ways, and says in its
  description that v1's `harnessIdentity` moved and that no v1 series is open (the check of F1.S1 (b)).
- **D41 in F3's own outputs.** The pre-registration files, the data file, the report, the dated lines and the tag's message are
  public text: counts, hashes, costs, times and classes only. No case id, no trap name, no witness id, no case text, no
  requirement breakdown per case. The adjudication packets hold case text and stay in the series' private folder.
- **Nothing typed.** Every pre-registration file but its own stated prose, the data file and the report are written by a tool
  from the code's constants, the pinned inputs or the records. A test regenerates each and compares bytes.
- **The commands** add: `node spikes/minigit-bench/run-tests.mjs` (both ways) where v1 is touched; the pre-registration tests
  `node $LOW node --test spikes/lang-explore/test/stage-*.test.mjs`; and for the record tools
  `node --test research/lang-v2/tools/test/`.

## 4. The steps that need the owner's word

| # | Step | Why | What the go must name | What runs without it |
|---|---|---|---|---|
| O1 | The push of the tag `prereg/lang-v2-<yyyymmdd>` to this repository's remote (F3.S1 (b), step T) | Public and not to be undone: the analysis is fixed in public history; from then on a registered snapshot cannot be superseded (`cache-store.mjs`) and a registered file changes only by a dated amendment | the tag's name and the commit it is put on | everything before it: the tools, the files' pull request, the dry run, the local tag and its check |
| O2 | The pilot's paid sessions: the A/A pair and the two pilot pairs, six sessions (F3.S2 (a), step P) | Money: void spend, each session capped at $50, so at most $300; the quick run's mean was $1.86 a session (information, not a bound the plan relies on) | the pilot, and how many repeat pairs after a NO GO it also covers (none, unless it says so); each repeat is two more sessions at the same cap, and the disk reserve is checked again before it | `stage-init`, the dry run of the pilot, the free rehearsal |
| O3 | The paid rounds: 60 data sessions in five rounds, and at most one re-run of each void (F3.S2 (b), step R) | Money: no total ceiling exists (D7), so the only bound is 120 sessions (60 and one re-run each) times $50; at the quick run's mean the 60 sessions are about $110 (information, not a bound) | the rounds it covers (all five, or fewer); a go that names fewer is asked again before the next round | the GO table, the dated lines, the seed reveal, every dry run |

Nothing else in F3 needs the owner: not a merge, not the seed reveal, not an amendment for the wall and the stall, not the
report's pull request, not the dated decision line's wording (who decides its content is open point 12). A second A/A or
pilot pair after a NO GO is paid for only under O2's words or a new go. If the series stops (a `stop:` class, a T1 breach, a
second contaminated void), a new series is a new O2 and O3.

## F3.S1 — the series over six tasks and one pre-registration

**Model:** scenarios + brief: this document · code Sonnet · review Opus · fixes Sonnet. **Parent:** F3.S1 (E3.S1 thin, E3.S5
reduced; E4.S1, S2 and S3). **Accepted when (parent):** a stub series over six tasks shows the order, the mask rule (each
pair's masks as the rule says; 15 of each mask per stack; 3 and 2 per task), no scoring while a session runs, the disk rule
halting below its threshold, and the guard refusing a changed identity; the tag precedes the first launch.

| Part | What | Pull request or step (§8) |
|---|---|---|
| (a) | the schedule: order, masks, rids, the barrier, the round rule | PR 2 |
| (a) | the series over six tasks: `stage-init`, `stage-run`, the task slice, the kinds, the record's `arm` and `model` | PR 3 |
| (a) | the guards before a paid pair: the frozen record, the identities, memory certification, the disk reserve and rule, the clear held-out folder | PR 4 |
| (b) | the pre-registration tools: the generators, the widened guard, the seed, the CLI pin, the price read | PR 5 |
| (b) | the pre-registration files | PR 8 |
| (b) | the dry run and the tag | step T — **the owner's go (O1)** |

### (a) the schedule — PR 2

One new pure module, `lib/stage-schedule.mjs`. Nothing in it reads a file or the clock.

**What it decides.**

- **The tasks and the rounds.** The six tasks are `FREEZE_TASKS` (`lib/workspaces-sources.mjs`), imported, so the list has one
  source. Five rounds. A **slot** is one (round, task); there are 30, numbered 1 to 30 in the order they run, and the slot's
  number is the run-map's `pair`, so v1's pair files (`pairs/p<k>.json`) and every reader of `pair` work unchanged.
- **The order inside a round** comes from the seed: the tasks of round r sorted by the sha256 of
  `<seed>:lang-v2:r<r>:t<task>:order`. Each round holds every task once.
- **The mask rule** is the parent's, and never reads the seed: with k = 0 for 04, 1 for 05, 2 for 06, 3 for 07, 4 for 08 and
  5 for 10, in round r the C# session of task k runs on mask A (`MASKS.low`, `0-5`) when r + k is odd and on mask B
  (`MASKS.high`, `8-13`) when it is even; the Node session takes the other mask. `maskOf` (the draw and the swap per pair) is
  not used for a Stage 1 series.
- **The rids** of data sessions are the parent's §4.4 form without a group: the first six hex digits of the sha256 of
  `<seed>:bare:t<task>:<stack>:r<round>:a<attempt>`. A seed whose 120 possible rids (six tasks, two stacks, five rounds, two
  attempts) are not all distinct is refused when the seed is drawn, before its hash is committed, and another is drawn. The
  A/A's and the pilot's sessions keep `pilotRid` (they run before the seed is revealed).
- **Which stack is launched and scored first** in a slot is a seeded bit, as v1's `firstStack` (`bare-plan.mjs:35`), with the
  task and the round in its text.
- **The barrier and the round rule.** A slot is offered only when every earlier slot is finished (`pairComplete`: both stacks
  scored, or void after the one re-run). A void is re-run once, alone, as `ran unpaired`, before the next slot (the barrier as
  `lib/series.mjs` has it; the parent's §4.4 words it differently, open point 1). Round r + 1 therefore never starts while a
  pair of round r is unscored; the module also states that as its own check, so a later change to the barrier cannot lose it.
- **What is never re-run.** A session whose scoring is decided (`accepted`, `rejected`, `intermittently incorrect`) is a data
  attempt and is finished. Only a void is offered a re-run: an `unscored: environment` session (`VOID_UNSCORED`) and v1's void
  classes. A killed or crashed session is a data attempt (§9).
- **The gates on a data slot.** No data slot is offered before the pilot's table says GO and the seed is revealed (F3.S2
  (a)). The module takes both facts as arguments.
- **The read-back pairs.** One slot of round 1 and one of round 2, one of a daemon task and one of a web task, named from the
  seed, so they are not picked after a result is seen.
- **The read-back gate: nothing runs past an unread read-back slot** (plan round 1, finding 2). When a read-back slot is
  scored, no later slot is offered until each of its two sessions that counts has its dated read-back line on `main`
  (`holds` or `refuted`). A `refuted` line makes that session `void: harness`; its one re-run is then the schedule's next
  step, as for any void, and the re-run is read back too before any later slot. So a read-back can never void a session
  after later slots ran, the re-run stays next to its partner in time, and the schedule needs no way back to a finished
  slot. **Why this and not a later invalidation:** a read-back that voids a session rounds later would need `nextSlot` to
  reopen a finished slot, a re-run far from its partner, and a rule for round 3 while that re-run is unsettled; each is a
  new state in the part of the harness that spends money, and the price of avoiding them is two short waits. The parent's
  rule, the lines before round 3, follows from this gate, and the module still states it as its own check.
- **The pilot's plan.** The A/A is two sessions of one task and one stack, one on each mask, kind `aa`, twins `a` and `b`.
  The two pilot pairs are kind `pilot`; in the daemon pair C# is on mask A, in the web pair on mask B. Which tasks, and the
  A/A's task and stack, are data the pre-registration names (open point 2).

**Scenarios** (`test/stage-schedule.test.mjs`, pure):

1. `Each_round_holds_every_task_once_and_the_order_inside_a_round_comes_from_the_seed`
2. `The_same_seed_gives_the_same_30_slots_and_another_seed_gives_another_order`
3. `In_round_r_task_k_runs_C_sharp_on_mask_A_when_r_plus_k_is_odd_and_Node_takes_the_other_mask` — all 30 cells against a table
   written out in the test.
4. `The_mask_of_a_slot_never_depends_on_the_seed` — a hundred seeds, one mask table.
5. `Over_the_30_pairs_each_stack_holds_each_mask_exactly_15_times`
6. `Each_task_gives_a_stack_one_mask_3_times_and_the_other_2_times`
7. `A_swap_per_pair_over_a_seeded_order_can_give_a_task_one_mask_five_times_and_that_is_the_point` — the refuted approach,
   reproduced: `maskOf` over the slots in their seeded order, for a seed the test finds, gives one task the same mask for one
   stack in every round.
8. `The_rids_of_a_seed_are_distinct_over_task_stack_round_and_attempt_and_a_colliding_seed_is_refused`
9. `A_slot_is_refused_while_an_earlier_slot_is_not_finished_and_the_sentence_names_it`
10. `No_slot_of_the_next_round_is_offered_while_a_pair_of_this_round_is_unscored`
11. `A_void_is_re_run_once_alone_before_the_next_slot_and_a_second_void_ends_the_cell`
12. `A_session_with_a_functional_failure_in_a_completed_pass_is_never_offered_a_re_run` — `rejected` and
    `intermittently incorrect` entries; the terminal rule of D71 at the schedule's level.
13. `An_unscored_environment_session_is_a_void_and_is_offered_its_one_re_run`
14. `No_data_slot_is_offered_before_the_pilot_is_GO_and_the_seed_is_revealed`
15. `Round_3_is_refused_until_the_read_back_lines_of_rounds_1_and_2_exist` — kept as the parent's own rule, with a map built
    by hand in which the gate of 15a was skipped.
    - 15a. `No_slot_after_a_read_back_slot_is_offered_until_both_its_sessions_have_their_dated_line` — one line of two is
      not enough; the sentence names the slot and the rid still unread.
    - 15b. `A_refuted_read_back_makes_the_session_a_void_and_its_re_run_is_the_next_step_before_any_later_slot`
    - 15c. `The_re_run_of_a_refuted_session_is_read_back_too_before_the_next_slot_is_offered`
    - 15d. `A_second_refuted_read_back_ends_the_cell_and_the_next_slot_is_offered_only_then` — the cell is `missing`, as after
      any second void; nothing loops.
    - 15e. `A_read_back_line_for_a_slot_that_is_no_read_back_slot_opens_nothing_and_closes_nothing` — a line about another
      slot's rid neither satisfies the gate nor reopens a finished slot.
16. `The_read_back_pairs_are_named_by_the_seed_one_of_each_kind_in_rounds_1_and_2`
17. `The_pilot_plan_holds_the_A_A_twins_on_the_two_masks_and_one_pair_per_kind_and_no_data_rid`

**Brief (Sonnet coder).** Branch `feat/lang-v2-f3s1-schedule`. One new file and its test; `lib/series.mjs` gains nothing but
an export of what the module needs (`pairComplete`, `stackState`, `MASKS`, `STACK_ORDER` are exported already). Functions under
50 lines, complexity at most 4; the mask table is computed, never a literal list. The functions: `slotsOf({ seed })`,
`maskOfSlot({ round, task, stack })`, `ridOfSlot(...)`, `firstStackOfSlot(...)`, `seedProblem(seed)`,
`nextSlot({ map, slots, pilotGo, seedRevealed, readbackLines })` → `ok(slot)` | `fail(sentence)` | `ok(null)` when the series is
done, `readbackSlots({ seed })`, `pilotPlan({ pilot })`. No command changes in this pull request.

**Done when:** the seventeen scenarios and 15a to 15e went red, green, red; §8's row for PR 2.

### (a) the series over six tasks — PR 3

**What changes.** One series directory holds the whole of Stage 1: the A/A, the pilot and the 30 slots, so there is one
ledger, one run-map and one alias baseline (D47: the first session of the series, the A/A's, sets it). The pair of a slot runs
through the code a quick-run pair runs through today; what is new is which task a pair belongs to, and the kinds.

**Brief (Sonnet coder).** Branch `feat/lang-v2-f3s1-series`.

1. **`lib/stage-series.mjs` (new).** `createStageSeries` writes the directory `x-<yyyymmdd>-<4 hex>` (the canonical name, so
   `CANONICAL_SERIES` and `seriesRootProblem` hold) through `createSeries`'s own steps, widened by an optional `stage` argument,
   never copied. `explore-series.json` holds `{ seriesId, createdAt, stage: 1, arm: 'bare', tasks, rounds, preregTag,
   preregCommit, seedSha256, pilot }`. `inputs.json` holds the series-wide parts once (the subject, the CLI pin, the caps, the
   locale, the base environment, the runner pin, the templates' identity and hashes, the pre-registration manifest's sha256)
   and, under `tasks.<NN>`, exactly what `quickInitExtra` (`lib/init-quick.mjs`) records for one task today (the task's pins,
   `heldoutQuick`, `generatedSha256`, `packagesMdSha256`, the prompts) with that task's limits.
   `inputsOfTask(inputs, taskId)` returns the one-task shape, and every existing check takes it unchanged:
   `frozenPinsProblem`, `heldoutMovedProblem`, `harnessMovedForStacks`, `seriesLimits`, `seriesPrompts`. That is the widening;
   none of those checks is rewritten.
2. **`stage-init` (new verb, FREE).** It makes the series. It refuses without the pre-registration tag on the remote (PR 5's
   guard), without a clean tree under `spikes/`, and while any held-out side is not the registered one. It calls
   `checkWorkspaces` (`lib/workspaces-check.mjs:111`) on a checkout of the task repository at W and records the commit, so what
   a session is launched with and restored from is what the task repository publishes (parent F2.S2). It pins the series'
   harness identity (PR 5's list) and refuses one that is not the manifest's, unless a dated instrument line accepted it. With
   `--rehearsal` it makes an `r-` series for the free stub round and needs no tag.
3. **`stage-run --series <dir> --what aa | pilot:<NN> | next` (new verb; SPENDS MONEY unless `--dry-run`).** `next` is the
   schedule's next slot; no flag names another data pair. A void's one re-run is the schedule's next step, so `next` runs
   it, alone, before any later slot; no flag skips it. `--until round` repeats `next` until the round's last slot is
   finished, a halt, a stop, or a file `<series>/pause` exists (checked between slots only; a running pair is never
   interrupted by it). It also ends, with exit 3 and a sentence naming the slot and the command that prints its read-back,
   when the slot it just scored is a read-back slot: nothing later runs until the lines are on `main` (the read-back gate of
   PR 2). `--accept-noisy <why>` is as today. The body is `runPair`'s: the
   part of `lib/run.mjs` after the plan (`readyOrRefused`, `loopAndFinalize`, `finishedIfInterrupted`, `finalizePair`) is called
   with the slot's task and the task's slice; it is extracted into one exported function both verbs call, not copied.
   `run` and `init` refuse a Stage 1 series and name the new verbs; `stage-run` refuses any other series.
4. **The kinds.** Run-map entries carry `kind` `aa`, `pilot` or `data`, and `task`, `round` and `arm: 'bare'`. A `data`
   session must be charged as `data` or `void` (`ledgerKind`), freeze the instrument (`hasDataSession`) and ask the gate for
   the revealed seed (`bare-gates.mjs:19`), which v1 does for the kind `data` only, and v1's loop gives that kind its own
   two-phase schedule. So v1 gets **one seam**, in `spikes/minigit-bench/lib/bare-series.mjs`: when the context carries
   `stepOf`, `loopPass` (`:234`) asks it for the next step, and `planItems` (`:48`) takes the sessions the caller planned in the
   map, as it does for the other kinds; `earlierPairNotDone` is not asked, because the caller's barrier already answered.
   Without `stepOf` v1 behaves byte for byte as before (`test/fixtures/explore-seams-baseline.json`). lang-explore passes
   `stepOf` as the step of ONE slot: launch what is planned of this slot, then score what ended with data.
5. **The context of a Stage 1 pair** (`lib/run-ctx.mjs` `contextOf`): `seed` is the revealed seed for a data slot and null
   before; `preregTag` and the series' pins come from the series; `handVerdictsFor` reads the registered `amendments.md`
   through v1's `handVerdictsFor` (`amendments.mjs:79`), so a dated `refuted` line makes a session `void: harness` (E5.S2); the
   guard is the Stage 1 guard (PR 4 and PR 5). **A Stage 1 series refuses to run with the guard that always passes**: until
   PR 4 and PR 5 are merged, only a rehearsal series runs. The prices and the crosscheck policy are read from the files the
   series pinned (`crosscheckPolicyFrom` takes both paths), not from v1's defaults.
6. **No pause point.** `pauseOf` (`lib/run.mjs:174`) is not asked for a Stage 1 series (D7, D53). `lib/pause.mjs` classifies a
   Stage 1 series, by its `stage` field, as no series of the olympiad or quick plan: it is not summed and it does not make that
   check fail closed.
7. **The owner's go.** `lib/stage-amendments.mjs` (new) reads the registered `amendments.md`. It parses three kinds of its
   own and hands every other line to v1's `parseAmendments`, which is not changed:
   `YYYY-MM-DD | owner-go | pilot | <the owner's words>`, `YYYY-MM-DD | owner-go | rounds:<a>-<b> | <the owner's words>`, and
   `YYYY-MM-DD | caps | wallMs:<n> | stallMs:<n> | <reason, with G-18>` (F3.S2 (a)). `stage-run` refuses a paid launch whose
   scope no `owner-go` line covers, before anything is planned, and writes the line it used into the pair record and as an
   event line of the ledger (an event, never a charge). A malformed line of these kinds is reported, never counted.
8. **The record.** `lib/records.mjs` `exploreSection` gains `arm`, `model` (the id the alias record resolved for the main
   model), `task`, `round` and `kind`, each known or saying why; `REQUIRED_PATHS` asks for them in a Stage 1 record only, so
   the older records stay complete as they are (D34, D76, T-L11). The pair record gains the slot's task, round and masks.
   Every v1 metric stays.
9. **`stage-status`** prints the rounds, the slots with their state, and the spend on five lines: pilot, A/A, data, void and
   re-run. The re-run line is derived, not a new ledger kind: it is the charges of sessions with `attempt` 2; `data` and
   `void` are those of `attempt` 1 (open point 14 asks the team lead to confirm that split).
10. **`prune` refuses a Stage 1 series** (the parent's *Growth*: nothing is pruned).
11. **Docs.** `research/module_lang_explore.md` gains a section on the Stage 1 series (the directory, the verbs, the kinds,
    the seam in v1, the go line); `research/module_minigit_bench.md` names the seam; `research/module_tests.md` lists the
    scenarios with the observed numbers.

**Scenarios** (`test/stage-series.test.mjs`, `test/stage-run.test.mjs`, `test/stage-amendments.test.mjs`, fakes: the seams
`io.runSeams.loopFn` and `prerequisitesFn`, the stub CLI, a fixture seed; `spikes/minigit-bench/test/explore-seams.test.mjs`):

18. `stage_init_makes_one_series_over_the_six_tasks_with_each_tasks_frozen_slice`
19. `A_task_slice_passes_every_check_a_one_task_series_passes` — the five existing checks over `inputsOfTask`, with the bytes
    of a one-task `init` as the control.
20. `A_changed_pin_of_one_task_refuses_that_tasks_slot_and_names_the_task`
21. `next_runs_the_schedules_slot_and_no_flag_names_another_data_pair`
22. `Both_sessions_of_a_slot_start_together_and_the_scoring_starts_only_after_both_ended`
23. `No_session_is_launched_while_a_scoring_of_the_series_runs` — the series lock is held from the launch to the end of the
    scoring; a second `stage-run` is refused naming the holder.
24. `A_data_session_is_charged_as_data_or_void_and_a_pilot_or_A_A_session_under_its_own_kind`
25. `The_record_of_a_stage_session_carries_arm_model_task_round_and_kind_each_known_or_saying_why`
26. `The_first_session_of_the_series_sets_the_alias_baseline_and_a_later_one_resolving_to_another_id_stops_the_series` (T-L40
    without groups)
27. `A_slot_already_planned_is_never_planned_twice` (T-L41 without groups)
28. `A_stage_series_is_not_under_the_quick_runs_pause_point_and_that_check_neither_counts_it_nor_stops_on_it`
29. `run_and_init_refuse_a_stage_series_and_stage_run_refuses_an_exploratory_one`
30. `A_stage_series_never_runs_with_the_guard_that_always_passes`
31. `A_paid_launch_without_its_owner_go_line_is_refused_before_anything_is_planned`
32. `An_owner_go_for_rounds_1_to_2_does_not_cover_round_3_and_one_for_the_pilot_covers_no_round`
33. `A_malformed_go_line_is_reported_and_never_counts`
34. `until_round_stops_at_the_rounds_last_slot_at_a_halt_and_at_the_pause_file`
    - 34a. `until_round_ends_after_a_read_back_slot_and_says_what_is_awaited_and_goes_on_once_the_lines_are_there`
35. `A_halted_slot_is_resumed_and_plans_no_new_session`
36. `A_session_that_reaches_the_cap_is_killed_cap_and_stays_a_data_attempt_and_five_rounds_stay_planned_whatever_was_spent`
    (T-L10 for Stage 1: no ceiling line)
37. `A_dated_refuted_line_makes_the_session_a_void_harness_and_offers_its_re_run`
38. `The_dry_run_prints_the_five_rounds_with_their_masks_and_the_cap_and_claims_nothing` — before the seed is revealed it
    prints the mask table and says that the order is not known yet.
39. `prune_refuses_a_stage_series`
40. `The_spend_lines_are_pilot_A_A_data_void_and_re_run_and_they_sum_to_the_ledger`
41. (v1) `The_loop_asks_the_callers_step_when_one_is_given_and_plans_v1s_own_pairs_as_before`

**Done when:** the scenarios went red, green, red; a free rehearsal series runs round 1's six slots end to end with the stub
CLI (recorded with its walls in `research/module_tests.md`); v1's suite is green and its baseline fixture unchanged; §8's row
for PR 3.

### (a) the guards before a paid pair — PR 4

**Brief (Sonnet coder).** Branch `feat/lang-v2-f3s1-guards`. Four new modules; each check is a sentence per problem, and a
refusal happens before anything is planned or paid.

1. **`lib/stage-guard.mjs`.** `stageGuard({ repo, series, taskRepo, paid, requireSeed })` → `ok(null)` | `fail(reason,
   { problems })`, every problem named. In this order:
   - **the frozen record:** `freezeProblems({ specStates, heldoutFrozen, memoryLimit })` (`lib/freeze-consumers.mjs:52`) is
     empty. That refuses a series while the freeze is not `accepted` and while `memory-limit.json` lacks its registration
     (parent D72, epoch 10). The twelve registered references are the ones `inputs/packages.json` names (`refsTreeSha256`); one
     that is not is named (the consumer half of F2's scenario 25);
   - **the task repository at the record** (a checkout given by `--task-repo` or `LANG_V2_TASK_REPO`): `specBlobMatches`
     (`spikes/lang-bench/lib/spec-freeze.mjs:76`) for each of the six specs, T-L20's launch half; `workspaceCommitProblems`
     (`:124`), T-L67's launch half; `PINNED_TASK_SHA` is the record's `freeze.workspaceCommit`; the sha256 of
     `inputs/hashes-lang.txt` is `freeze.hashesLangSha256`;
   - **the identities:** `liveIdentities()` reads every identity the pre-registration freezes, each from the place that owns
     it, and `identityProblems(manifest, live)` names each one that differs with both short hashes. The list (parent F3.S1 (a),
     §9, E4.S3): the Stage 1 harness identity (PR 5's list of folders; plan round 1, finding 1: the tag freezes the
     instrument that is used), the runner manifest (`inputs/specs.d/runner.json`), the execution layer's tree hash and the
     memory policy
     (`exec-layer.json`, `memory-policy.json`), `pidsMax` 512 read from `lib/exec-layer/scope-entry.sh`, the CLI copy's sha256,
     the toolchain checksums, the two snapshots' tree hashes, FORMAT's sha256, the six specs' sha256, the six archives', clear
     trees' and trap tables' hashes with their counts, the memory value and its registration, the contract's identity
     (`contractIdentity()`), the templates' identity, the registered prompts' and statements' sha256, the price file's and the
     policy's sha256, the caps, the locale and the base environment's allow-list, and the certifying host (host name, kernel,
     cgroup version). The manifest is PR 5's file; this module owns the list. For the harness identity alone a difference
     is accepted when a dated `instrument | harness:<12 hex>` line names the live one and no data session has left `planned`;
     for every other identity there is no acceptance;
   - **paid only:** the checks of PR 5's guard (the tag, the registered blobs, the clean tree, the harness identity, the
     seed for a data slot).
2. **Memory certification before the launch.** Before a data slot or a pilot or A/A launch is planned, the layer's own probe
   (`certifyMemory`, `spikes/lang-bench/lib/exec-layer/certify.mjs:24`) runs once on this host. A host without a readable
   `memory.peak` refuses with the named halt `memory-not-certifiable`, exit 3, resumable on a certifying host, and nothing is
   planned (T-L29, D36). A rehearsal proceeds and carries the flag `memory bound not certified`. The check at scoring stays
   as the second line.
3. **D15's environment (T-L14's half).** Before a launch, the agent box's environment is the registered base and holds
   neither per-case heap key; a clash or a planted heap key refuses the launch. The record lists the keys of both boxes.
4. **`lib/stage-disk.mjs`.** **The reserve is recomputed before every paid launch** (plan round 1, finding 5), not only
   before the first: free disk under `~/rsd/lang-v2/` is at least the bound of everything still to run, which is the sessions
   of this launch and every session the schedule still holds, times the measured worst case of one session with every retry
   and its one void re-run. Before the A/A that is the parent's whole-series bound, the 66 sessions. A repeat pair after a
   NO GO is no part of the 66: it adds its own two sessions to the launch that runs it, so a repeat that would leave less
   than the remaining series needs is refused before it is paid, and every later launch counts what is really left. The
   sessions that already ran are counted by what they hold on disk, not by the estimate. `diskRuleVerdict`
   (`lib/growth.mjs:32`) before every slot: below twice the pair's
   measured worst case the series halts, exit 3, with a `disk-rule` event line in the ledger (an event, never a charge) and
   both numbers in the sentence; it goes on when the space is there. A missing or zero size is a refusal, never a rule that
   passes. **Which number is "the measured worst case":** `inputs/growth.json` measures the scoring's files (the built copy
   and the retained records, 117 MB for a pair), while the parent's *Growth* counts a kept session at about 1.4 GB with its
   box. The rule here takes both: the largest kept session folder (measured by the tool over the quick run's series before
   the pilot, and again over the pilot's sessions, the larger standing) plus `growth.json`'s figures. Open point 13 asks the
   team lead to confirm this reading.
5. **`lib/stage-archive.mjs`: the recovery path keeps the data, and says exactly what may go** (plan round 1, finding 3).
   Three FREE verbs; none launches or scores, and each takes the series lock.
   - **`stage-archive --series <dir> --to <backup dir>`** copies the whole series folder to the private backup, writes the
     sha256 list of the source (one line per file, `sha256  path`, links recorded and never followed, the walker of
     `lib/workspaces-tree.mjs` widened to stream a large tree), verifies the copy against that list file by file, and appends
     the archive's identity to `<series>/archive.json`: the list's sha256, the bytes, the place, the date and the slots
     finished at that time. It deletes nothing.
   - **`stage-free --series <dir>`** is the only thing that frees local space, and it frees one closed list:
     `FREEABLE = ['box/nuget', 'box/npm', 'box/home/.dotnet']`, the cache copies inside a session's box, and only of sessions
     that are final (scored, or a settled void) in a finished slot. It first verifies again that the newest archive is at its
     place and that every file it is about to remove is in that archive's list with the same sha256; a file that is not is
     kept and named. In each session it leaves `freed.json` with the archive's identity and the removed folders' own list.
     **Never freed:** the workspace tree a session left (`work/`), `score/` with its build record, attempts, events and
     score, `record.json`, `meta.json`, `plan.json`, `argv.json`, the streams, the transcripts, the OTel and broker and
     egress logs, the read-back files, and everything at the series' root (the run-map, the ledger, `pairs/`, `private/`,
     the adjudication folders, `archive.json`). An unfinished slot's sessions are never touched.
   - **`stage-restore --series <dir> [--rid <rid>]`** copies a freed session's folders back from the archive, verifies them
     against `freed.json`'s list and removes `freed.json`.
   - **Who needs a freed folder, and when it comes back.** Nothing between the halt and the report reads `FREEABLE`: the
     resume of the open slot and every later slot work in their own sessions; the scoring builds from `work/` over the
     registered snapshot, never the session's cache (D65); `rescore` and `replay` the same; the adjudication reads
     `score/passes/` and the frozen spec; `extract` reads the records; the replay of the contamination scan reads the streams
     and `argv.json`. Two things do need the whole tree, and each refuses a series that still holds a `freed.json`, naming
     `stage-restore`: the final sha256 list of the record (PR 13) and the last `stage-archive` after the series ended. A
     command that rebuilds a finished session's record from its folder refuses a freed session the same way. An absent cache
     is therefore never read as an empty one.
   - `prune` stays refused for a Stage 1 series: it would remove `score/`.
6. **`lib/stage-heldout.mjs`: the clear held-out folder per slot.** A slot runs inside `withOpenedHeldout`
   (`lib/heldout-lifecycle.mjs:324`) for its own task: the folder is made from the archive, checked against the registered
   tree hash, and removed when the slot ends, whether it returns or throws. A run after an interruption removes a stale
   folder first (`sweepClearFolder`, `:196`, which `readerHeldout` already calls). Because the removal needs the key's backup
   verified on the share's mount (`closeClearFolder`, `:289`), a slot is refused before its launch when that cannot be
   verified. Only the slot's own task is ever in clear.
7. **Docs** as in PR 3, and the runbook's first half in `research/module_lang_explore.md`: what halts a slot and what the
   operator does for each halt.

**Scenarios** (`test/stage-guard.test.mjs`, `test/stage-disk.test.mjs`, `test/stage-heldout.test.mjs`, fakes and temporary git
repositories; `test/stage-guard-real.test.mjs`, real, behind `needs`):

42. `The_guard_refuses_a_series_while_the_frozen_record_lacks_a_field_and_names_the_field`
43. `The_guard_refuses_while_the_memory_value_is_not_registered`
44. `A_reference_identity_that_is_not_one_of_the_twelve_registered_ones_is_named`
45. `A_spec_blob_that_differs_from_the_tags_refuses_the_launch_and_a_difference_outside_the_spec_does_not` (T-L20's launch
    half, with its permitted change)
46. `A_workspace_commit_that_is_not_the_records_refuses_the_launch` (T-L67's launch half)
47. `The_workspaces_a_session_is_given_equal_the_published_ones_by_tree_hash_at_stage_init_and_one_changed_byte_is_named`
48. `Each_live_identity_that_differs_from_the_manifest_is_named_with_both_short_hashes` — table-driven over the whole list; a
    companion case removes one identity from the list and fails, so the list cannot shrink in silence.
49. `A_host_that_cannot_certify_memory_refuses_every_paid_launch_before_anything_is_planned_and_a_rehearsal_goes_on_with_its_flag`
50. `A_heap_key_in_the_agent_boxs_environment_refuses_the_launch`
51. `The_reserve_refuses_the_first_launch_below_the_series_bound_and_names_both_numbers`
    - 51a. `The_reserve_is_recomputed_before_every_launch_over_the_sessions_still_to_run`
    - 51b. `A_pilot_repeat_counts_its_own_two_sessions_and_is_refused_when_the_rest_of_the_series_would_not_fit`
    - 51c. `A_launch_after_a_repeat_counts_what_is_really_left_and_what_the_ended_sessions_really_hold`
52. `The_disk_rule_halts_a_slot_below_twice_its_worst_case_resumably_and_records_why_and_goes_on_when_the_space_is_there`
53. `A_missing_or_zero_size_is_a_refusal_and_never_a_rule_that_passes`
54. `A_copy_that_differs_from_the_source_in_one_byte_fails_the_archives_verification`
    - 54a. `stage_free_removes_only_the_three_cache_folders_of_final_sessions_in_finished_slots` — a fixture series with one
      finished and one open slot: after it, every other file of the series has its bytes, and the open slot's sessions are
      whole.
    - 54b. `stage_free_removes_nothing_that_is_not_in_the_verified_archive_with_the_same_hash_and_names_what_it_kept`
    - 54c. `stage_free_refuses_when_the_archive_is_not_at_its_place_or_no_longer_verifies`
    - 54d. `stage_restore_brings_a_freed_session_back_to_its_listed_bytes_and_a_copy_that_differs_is_refused`
    - 54e. `A_series_that_halted_on_the_disk_rule_was_archived_freed_and_resumed_ends_with_the_records_of_one_that_never_halted`
      — the fixture series runs twice with fakes for the sessions, once straight and once with a halt before slot 3,
      `stage-archive`, `stage-free`, the resume to the end, the adjudication's reader and `stage-restore`; every
      `record.json`, pair record, the run-map and the ledger are equal but for their time stamps and the event lines of the
      halt. The report's half of this path is scenario 118a.
    - 54f. `The_final_hash_list_and_the_last_archive_refuse_a_series_that_still_holds_a_freed_session`
55. `The_clear_folder_of_a_slots_task_is_made_for_the_slot_and_removed_after_it_whether_the_slot_ends_or_throws`
56. `A_run_after_an_interruption_removes_the_stale_clear_folder_first`
57. `A_slot_is_refused_before_its_launch_when_the_keys_backup_cannot_be_verified`
58. `Only_the_slots_own_task_is_ever_in_clear`
59. (real) `The_certification_probe_passes_on_this_host_and_reads_the_peak_of_an_empty_program`

**Done when:** the scenarios went red, green, red; the real probe passes on the series host; §8's row for PR 4.

### (b) one pre-registration — PR 5 (the tools) and PR 8 (the files)

**What is registered** (parent F3.S1 (b), §9, D26, E4.S1 to S3). New folder `spikes/lang-explore/inputs/prereg/`:

| File | Content | Written by |
|---|---|---|
| `statement.md` | what is run: the goal narrowed, the six tasks, known (05, 06) and original (04, 07, 08, 10), the arm, the model alias and effort, the tool list, the certifying host, D73's claim and its limit, T28's sentence, the date it was written | its own prose, with a generated identity table |
| `analysis-contract.md` | above a marker, quotes of the parent read at a commit of `origin/main`: *The goal, narrowed*, D24, D28, D29, D71, D72, D73, §9's primary metric and its table of undefined cases, the outcome classes, the paragraph on what the analysis licenses, the sentences of T3, T5, T6 and T9, T23 and T25 to T28. Below it, the pre-registration's own operationalisations (the list after this table) | `prereg/extract-contract.mjs`; the part below the marker is prose |
| `prediction.md` | the prediction for six tasks, bare, and when it was written (T28) | its source is open point 7 |
| `prompts-lang.json` | the bare template and its sha256; per task and stack the prompt text, the stack sentence, the framework sentence of a web task, and the sha256 of the workspace's `STATEMENT.md` as published at W | `prereg/write-prompts.mjs` |
| `schedule.json` | the tasks, the rounds, the mask of all 30 (round, task) cells, the two scoring profiles, the pass and attempt counts, the caps, the pilot's tasks, the A/A's task and stack, the read-back rule, the re-run rule | `prereg/write-schedule.mjs` (it refuses without the answers to open point 2) |
| `manifest.json` | every identity of PR 4's list, the Stage 1 harness identity among them, read when PRs 2 to 7 are all on `main`; the disk projection for the 66 sessions, and beside it what one repeat pair of the pilot adds | `prereg/write-manifest.mjs` from `liveIdentities()`; it runs last but for the seed, after `pin-cli.mjs` |
| `prices-<yyyy-mm-dd>.json` | the published prices read that day, in v1's shape | `prereg/read-prices.mjs` (the one step that uses the network) |
| `crosscheck-policy.json` | T1 to T5's policy, starting from v1's registered values | copied once, then its own file |
| `amendments.md` | the line formats and no line | prose |
| `seed.sha256` | the seed's hash; `seed.txt` is absent at the tag | `explore.mjs seed-commit`, last |

The registered files outside that folder are pinned by path: `spikes/lang-explore/inputs/explore.json`, `memory-limit.json`,
`packages.json`, `heldout-frozen.json`, `growth.json` and `package-flags.json`; `spikes/lang-bench/inputs/specs.json`,
`specs.d/*.json`, `hashes-lang.txt`, `cache-snapshots-lang.json`, `cache-recipe-lang.json` and `package-matrix.json`; and
`spikes/minigit-bench/inputs/quiet.json`, which the quiet check reads.

**The operationalisations the contract's own part states** (each a sentence the gate reads; none changes what the parent
fixes):

- per task CPC over the data attempts, with "no correct solution" and its spend when none is accepted, and the ratio C# over
  TypeScript defined only when both stacks have an accepted attempt (§9's five cases); `CPC_all` as the second line;
- the overall reading: the median of the per-task ratios over the defined ones, the geometric mean, and the count of tasks
  where C# is higher, each with its denominator and the line of undefined outcomes;
- the outcome classes: `rejected` and `intermittently incorrect` are data attempts that add their cost to CPC's numerator and
  nothing to its denominator; `unscored: environment` is a void, its cost on the void line, in neither part of the primary
  CPC, re-run once; a killed or crashed session is a data attempt, censored at the cap where the cap ended it;
- the two scoring profiles and the two session masks, the mask rule, that no scoring runs while a session runs, and that
  every mask is read back;
- the void grounds (v1's void classes and `unscored: environment`) and the re-run rule (once, alone, before the next slot);
  the stop grounds (v1's stop classes, a T1 breach, a second contaminated void, alias drift);
- what may be amended after the A/A, by a dated line, for both stacks alike: the instrument (the harness identity, the quiet
  thresholds, the package flags), the T3 and T5 policy, and once the wall and the stall; never the cap, a prompt, a spec, a
  limit or the treatment of one stack; nothing of the instrument after the first data session;
- the spend lines: pilot, A/A, data, void and re-run, with the split of PR 3's item 9;
- the pilot's GO table: v1's G-1 to G-14 with its definition of GO, and G-16, G-17 and G-18 as the parent's §11 reads them
  for Stage 1 (open points 3 to 5), with the stops (G-2, G-8 and G-12 refuted, alias drift);
- the read-back pairs' rule, and that no later slot runs before a read-back slot's lines are written; the blind
  adjudication's two steps and what the revised reading is (F3.S3 (b); open point 15);
- the disk reserve, recomputed before every launch, and the closed list of what a recovery may free;
- the memory causes counted per task and stack (kernel kill, each runtime's abort) and the flags `abort-below-evidence` and
  `child-abort-line`, with the 50 % evidence threshold (D56);
- `deviations` (frozen files the restore had to put back, count and paths) beside `accepted`, never deciding it;
- every line shown with and without pairs that hold a `ran unpaired` session or a `foreign load` flag;
- whether a sign test over the six task medians is shown as a secondary line: **not shown**, unless open point 8 says
  otherwise before the tag.

**Brief for PR 5 (Sonnet coder).** Branch `feat/lang-v2-f3s1-prereg-tools`.

1. **v1's guard takes its lists as arguments** (`spikes/minigit-bench/lib/prereg-guard.mjs`). `preregGuard` and
   `harnessIdentity` gain a configuration: the registered files as paths from the repository's root, the amendable inputs, the
   policy file and its free fields, the seed files, the harness folders and the consistency check. v1's own call passes v1's
   lists and answers exactly as before (its tests stay green, unchanged). This is the widening §6.2's row 9 asks for; no second
   guard is written.
2. **`lib/stage-prereg.mjs` (new): Stage 1's lists.** `STAGE_PREREG_FILES` (the table and the paths above),
   `STAGE_HARNESS_DIRS` (v1's six folders and `spikes/lang-explore/lib`, `spikes/lang-explore/prereg`,
   `spikes/lang-explore/rehearsal`, `spikes/lang-bench/lib`, `spikes/lang-bench/runner`, `spikes/lang-bench/templates`, with
   the top-level `*.mjs` of the three spikes), the policy's free fields (`t3`, `t5`; open point 9), and the two structured
   exceptions: `explore.json` may differ from the tag only in `caps.wallMs` and `caps.stallMs`, once, when a dated `caps` line
   names the new values; `quiet.json` and `package-flags.json` as v1 allows them. A harness identity other than the series' is
   accepted only by a dated `instrument | harness:<12 hex>` line and never once a data session has left `planned`.
3. **The generators**, in a new folder `spikes/lang-explore/prereg/`, each taking `--parent-commit <sha on origin/main>` where it
   quotes, and reading the parent through v1's `readAtCommit` (`spikes/minigit-bench/prereg/parent.mjs:27`, which takes the
   file): `extract-contract.mjs`, `write-prompts.mjs`, `write-schedule.mjs`, `write-manifest.mjs`, `read-prices.mjs`. Each
   writes its file whole and atomically, and `--check` exits 1 when the committed bytes are not what it would write.
4. **The seed.** `explore.mjs seed-commit` and `seed-reveal` over v1's `commitSeed` and `revealSeed`
   (`spikes/minigit-bench/lib/seed.mjs:29`, `:54`), which gain the prefix (`lv2-`), the list of files that must be in place
   first, and a check of the drawn seed (`seedProblem` of PR 2). The seed lives in `~/rsd/lang-v2/seeds/stage1.seed`, mode 600,
   outside git; only its hash is in the tree until the reveal.
5. **The CLI pin.** `prereg/pin-cli.mjs` copies the then-current CLI release into `~/rsd/lang-v2/toolchains/claude-<version>`
   (mode 0555) with v1's copy step (`bare-init.mjs`, extracted if it is private there), and writes the version, the path and
   the sha256 into `inputs/explore.json` `cli` (D8's re-pin, taken once: D47). The olympiad series are closed, so nothing else
   reads that entry for a paid run.
6. **The engine reads the registered prompt.** For a Stage 1 series the prompt of a task and stack is the registered text of
   `prompts-lang.json`, checked equal to what `quickPrompts` computes; a difference refuses `stage-init`. The template's final wording
   is the parent's §4.5 text unless open point 10 changes it before the files are written.
7. **Docs:** the module doc's section on the pre-registration (the files, the generators, the guard, what may be amended).

**Scenarios** (`test/stage-prereg.test.mjs`, `test/stage-prereg-guard.test.mjs` with temporary repositories in the shape of
v1's `test/prereg-fixtures.mjs`; v1's `prereg-guard.test.mjs` and `seed.test.mjs` gain the configuration cases):

60. `Every_quoted_block_of_the_contract_is_the_parents_text_at_a_commit_of_origin_main_byte_for_byte`
61. `A_quote_whose_block_is_missing_at_that_commit_fails_the_extraction_and_names_it`
62. `The_manifest_the_prompts_and_the_schedule_are_written_again_to_the_same_bytes_and_a_typed_value_fails_the_check`
63. `The_registered_prompt_of_a_task_and_stack_is_the_text_the_engine_sends`
64. `The_registered_statement_hashes_are_the_published_workspaces`
65. `The_seed_is_in_the_tree_as_a_hash_only_until_it_is_revealed_and_a_seed_at_the_tag_is_refused`
66. `No_registered_file_holds_a_case_id_a_trap_name_or_a_case_line` — the leak check's tiers over the folder, on fixtures.
67. `The_contract_states_the_claims_limit_the_outcome_classes_and_the_void_line_in_the_parents_words`
68. `The_schedule_file_is_refused_without_the_pilots_tasks_and_the_A_As_task_and_stack`
69. `The_guard_refuses_a_tag_that_is_not_on_the_remote_or_peels_to_another_commit`
70. `A_registered_file_that_differs_from_the_tag_refuses_the_launch_and_an_appended_amendment_line_does_not` (the pair of D64)
71. `The_policy_may_change_in_its_free_fields_only`
72. `The_wall_and_the_stall_may_change_once_by_their_dated_line_and_the_cap_in_dollars_never`
73. `A_data_slot_needs_the_revealed_seed_and_the_pilot_does_not`
74. `A_dirty_tree_under_spikes_refuses_a_paid_launch`
75. `A_harness_identity_that_is_not_the_series_refuses_a_paid_launch_until_a_dated_line_accepts_it_and_never_after_the_first_data_session`
76. `The_stage_harness_identity_moves_with_a_byte_of_lang_explore_lang_bench_or_v1_and_with_no_other_file`
77. `v1s_guard_over_v1s_lists_answers_as_before`
78. `The_engine_prices_a_stage_session_from_the_registered_price_file`
79. `A_stage_session_runs_the_pinned_CLI_copy_by_its_sha256`

**PR 8, the files** (branch `docs/lang-v2-prereg`; the team lead with an Opus writer for the prose). It is written only when
PRs 2 to 7 are all on `main`, because the manifest reads the harness identity (plan round 1, finding 1). The generators run
at a named commit of `origin/main` that holds them; `statement.md`, the contract's own part and `prediction.md` are written; `seed-commit` runs
last. Before it merges: every `--check` exits 0; the document gate (`mcp__coai__review_document`) reads `statement.md`,
`analysis-contract.md` and `prediction.md` and every finding is resolved; `leakcheck` and the line scan are clean. **PR 8 is
the last merge before the tag**: the tag goes on its merge commit. §11 marks the open points that must be answered before
these files are written.

**Done when (PR 5 and PR 8):** the scenarios went red, green, red; the files are on `main`, each generated one equal to its
generator's output, the manifest's harness identity equal to the live one on PR 8's merge commit; the document gate at `proceed`; §8's rows.

### (b) the dry run and the tag — step T · the owner's go (O1)

**Steps** (the team lead, never a coder).

0. **The harness is final.** PRs 2 to 7 are on `main` and PR 8's manifest was generated after the last of them: the dry run
   first runs `write-manifest.mjs --check` and `identityProblems` on PR 8's merge commit and stops at any difference. Nothing
   that changes a harness folder merges between this step and the pilot.
1. **The dry run**, in a scratch clone of this repository with a scratch bare remote; nothing reaches the real remote. A local
   annotated tag `prereg/lang-v2-<yyyymmdd>` on PR 8's merge commit is pushed to the scratch remote; the guard passes; then the
   pairs of D64 on the real files: an appended dated amendment line is accepted, and one changed byte of
   `analysis-contract.md`, of the cap in `explore.json`, of one spec pin and of one file under `spikes/lang-explore/lib` (the
   harness identity) is each refused, naming the file or the identity.
2. **The free stub round.** `stage-init --rehearsal`, then round 1's six slots with the stub CLI, a fixture seed, the hello
   programs of `rehearsal/` and the fixture held-out set, in the real boxes with the one-pass scoring: the order, the masks
   read back for each slot, the launch, both ends, the scoring, the next slot; then `stage-verdicts`, the read-back's print
   and the adjudication's `packets` step over that rehearsal series, so every tool of PRs 2 to 7 has run once on the
   harness the tag will name. Its lines are kept as the evidence of the parent's *Accepted when* beside scenarios 1 to 59.
3. `leakcheck` and the line scan on PR 8's merge commit; `plan-lifecycle`; the three `--check` tools of lang-bench.
4. **The owner's go for the tag (O1)**, asked with the commit, the dry run's result and what the tag locks.
5. The tag is made on the real clone: annotated, on PR 8's merge commit, its message one generated sentence that names the
   commit; checked before the push (not on the remote; annotated; on that commit; the commit is on `origin/main`); pushed
   alone; read back from the remote, peeled, equal to the commit.
6. One dated line in `spikes/lang-bench/inputs/checkpoints.md`: the tag, its commit, the dry run, the go.

**Done when:** the tag is on the remote and peels to PR 8's merge commit; the dry run and the stub round are recorded; no paid
session has been launched.

### The rules of F3.S1 and their pairs (D64)

| Rule | Refused | Permitted | Where shown |
|---|---|---|---|
| the mask rule | a cell whose mask is not the table's | the 30 cells of the table | scenario 3; the stub round's read-backs |
| the barrier and the round rule | a slot while an earlier one is open; round 3 without the read-back lines | the schedule's next slot | scenarios 9, 10, 15 |
| the read-back gate | any slot after a read-back slot whose two lines are not on `main`; a later slot before a refuted session's re-run | the next slot once both lines hold | scenarios 15a to 15e, 34a |
| a registered file | one changed byte | an appended dated amendment line | scenario 70; the dry run |
| the caps | a changed cap in dollars; a second change of the wall or the stall | one dated change of the wall and the stall | scenario 72 |
| an identity | a live identity that differs from the manifest, the harness identity among them | the manifest's identities | scenario 48; the dry run |
| the frozen record | a missing field, a spec blob or a workspace commit that differs | the accepted record | scenarios 42 to 47 |
| the seed | a seed at the tag; a data slot before the reveal | the pilot before the reveal | scenarios 65, 73 |
| the harness | another identity after the first data session | a dated acceptance before it | scenario 75 |
| the owner's go | a paid launch with no line for its scope | the launch under its line | scenarios 31, 32 |
| the disk | a launch below the reserve of what is still to run, a repeat pair included; a slot below the rule | the launch with the space there | scenarios 51 to 53 |
| the recovery | freeing anything outside the three cache folders, of an open slot, or not in the verified archive; the final hash list over a freed session | freeing the listed folders of final sessions after a verified archive; the list after `stage-restore` | scenarios 54 to 54f, 118a |
| the clear folder | a slot whose backup cannot be verified; a second task in clear | one task's folder for one slot | scenarios 55 to 58 |

## F3.S2 — the pilot, the paid rounds and the read-back

**Model:** scenarios + brief: this document · code Sonnet · review Opus · the by-hand reads and the read-back: an Opus judge ·
the paid commands: the team lead. **Parent:** F3.S2 (E4.S4 reduced; E5.S1; E5.S2). **Accepted when (parent):** the pilot is
GO; every session is classed; no round k+1 starts with round k unscored; the read-back lines exist before round 3.

| Part | What | Pull request or step (§8) |
|---|---|---|
| (a), (c) | the pilot's table and the read-back's reading, as tools | PR 6, before the tag |
| (a) | the go line for the pilot | PR 9 |
| (a) | `stage-init`, the A/A pair, the two pilot pairs | step P — **the owner's go (O2)** |
| (a) | the pilot's record: the by-hand lines, the wall and the stall, the seed revealed | PR 10 |
| (b) | rounds 1 to 5 | step R — **the owner's go (O3)** |
| (c) | the read-back lines of round 1's read-back slot | PR 11, before the slot after it |
| (c) | the read-back lines of round 2's read-back slot | PR 12, before the slot after it |

### (a) and (c) the tools — PR 6

**Brief (Sonnet coder).** Branch `feat/lang-v2-f3s2-pilot-readback`. It adds files under `spikes/lang-explore/lib`, a folder
of the harness identity, so it merges **before the pre-registration files are generated and before the tag** (§3; plan
round 1, finding 1): merged after the tag, it would make the live harness differ from the tagged manifest, the guard would
refuse the pilot, and the tag would not name the instrument the pilot uses. It may be built beside PRs 4 and 5. The pilot
and the read-back themselves keep the parent's place, after the tag (open point 21).

1. **`lib/stage-pilot.mjs` (new) and the verb `stage-verdicts --series <dir>`** (FREE, read only but for
   `<series>/pilot-verdicts.json`; exit 0 GO, 2 NO GO or STOP, 4 a refusal). The table is computed, never typed:
   - **G-1 to G-14** by v1's rules (`spikes/minigit-bench/lib/aa-rules.mjs`, `aa-rules-twins.mjs`, `aa-verdicts.mjs:39`), which
     gain the subjects as an argument: the two A/A twins for the twin rows, and all six pilot sessions for the rows v1 marks
     as required of its pilot too. v1's row words stay (`holds`, `refuted`, `explained`, `recorded`, `not exercised`) and so
     does v1's GO: every row holds; G-6 may be `explained`; G-9 (the twin spread) is recorded and never gates; a by-hand row
     (G-10, G-13) without its dated line is NO GO; G-2, G-8 or G-12 refuted, or alias drift, is a STOP. **G-12** reads at the
     registered cap of $50, not v1's $6; when no pilot session reaches it, it is `not exercised` with v1's replacing evidence
     (the budget tests green at the series' harness identity, as a dated line). **G-14** has no meaning in a one-phase series
     as v1 words it; the row is computed as open point 4 is answered.
   - **G-16**: three dated by-hand lines, one per driver (the TCP driver, the daemon checks, the HTTP driver), each on a real
     pilot tree of the task that exercises it; the row names a missing one. What the hand runs is the private aid's (D41).
     The clause on the RSS curve is open point 3.
   - **G-17**, computed from the registry and the frozen record: twelve references registered at the one value
     (`memory-limit.json`, `packages.json`), every witness of every package caught, and the frozen specs equal to the gated
     ones but for the freeze row (the freeze's own check, read from the record).
   - **G-18**, computed: the cross-check figure of the pilot beside the fixed $50 (it binds nothing; its formula is open
     point 5); the stall larger than the longest quiet gap of any pilot session, read from the stamped streams; every pilot
     session that reached $50 is `killed: cap`.
2. **The wall and the stall, once.** `stage-verdicts` prints the longest quiet gap and the longest wall of the pilot. The
   team lead writes one dated `caps` line (PR 3's kind) with the new `wallMs` and `stallMs` for both stacks, or a line that
   keeps the registered 75 and 45 minutes; the guard accepts that one change of `explore.json` and no second.
3. **`lib/stage-readback.mjs` (new): the read-back of a pair** (parent F3.S2 (c)). `readback --series <dir> --pair <slot>` on
   a Stage 1 series prints, for both sessions, what an Opus judge reads against the manifest: the argv (`argv.json`), the
   binds, the cache snapshots' hashes, the package list, the agent box's environment keys, the scoring box's keys of one
   completed pass with the two heap keys and their values, the effective CPU affinity (the launch's read-back file, the mask
   observed in flight, each pass's read-back), and the spec blob (the workspace's `STATEMENT.md` against the hash list at W,
   and the frozen spec's sha256). `readbackOf` (`lib/readback.mjs:57`) stays what it is and is one part of the output.
   The judge writes one dated by-hand line per session in v1's form with the read-back's row,
   `YYYY-MM-DD | G-1 | holds|refuted | <rid> | <evidence path> | <who>`. A `refuted` line makes the session `void: harness`
   through `handVerdictsFor` (PR 3), and its re-run is the schedule's next step. The verb refuses a slot that is not
   scored yet, and for a read-back slot it prints at its end which lines the gate of PR 2 still waits for.
4. **Docs:** the module doc's sections on the pilot's table and the read-back; `research/module_tests.md`.

**Scenarios** (`test/stage-pilot.test.mjs`, `test/stage-readback.test.mjs`, fixture series; v1's `aa-verdicts.test.mjs` and
`aa-verdicts-rules.test.mjs` stay green and gain the subjects cases):

80. `GO_needs_every_row_to_hold_and_a_by_hand_row_without_its_dated_line_is_NO_GO`
81. `A_refuted_T1_isolation_or_cap_row_and_alias_drift_are_a_STOP`
82. `The_rows_asked_of_every_session_are_asked_of_the_four_pilot_sessions_too`
83. `The_cap_row_reads_at_50_dollars_and_is_not_exercised_with_its_evidence_when_no_session_reached_it`
84. `G_16_needs_one_dated_line_per_driver_and_names_the_missing_driver`
85. `G_17_is_computed_from_the_registry_and_holds_only_with_twelve_registered_references_at_the_one_value`
86. `G_18_reports_the_cross_check_figure_beside_50_dollars_and_binds_nothing` — a fixture where the figure is $12 still leaves
    every later session's cap at $50.
87. `The_stall_must_exceed_the_longest_quiet_gap_of_any_pilot_session`
88. `The_twin_spread_is_recorded_and_never_gates`
89. `No_data_slot_is_offered_while_the_table_is_not_GO`
90. `The_read_back_of_a_pair_shows_argv_binds_cache_hashes_packages_both_boxes_keys_the_affinity_and_the_spec_blob`
91. `A_read_back_value_that_differs_from_the_manifest_is_marked_and_named`
92. `A_refuted_read_back_line_makes_the_session_a_void_harness_and_offers_its_re_run`
    - 92a. `The_read_back_of_a_slot_that_is_not_scored_is_refused`
    - 92b. `After_a_read_back_slot_the_print_names_the_lines_still_awaited_and_none_when_both_are_there`

**Done when:** the scenarios went red, green, red; §8's row for PR 6.

### (a) the pilot — step P · the owner's go (O2)

**Before any launch.** PRs 2 to 8 are merged; the tag is on the remote, and no harness folder changed since PR 8's manifest.
The series runs from one worktree of its own at `origin/main`, moved forward only by the dated-line pull requests (9 to
12), with a clean tree under `spikes/`. The share with the key's backup is mounted. Long commands are started detached from
the operator's session, so a session that ends does not end a pair.

1. `stage-init` (FREE): the series is made; its harness identity is the manifest's; the CLI copy and the tag are pinned; the
   reserve is checked for the first time (it is checked again before every launch).
2. `stage-run --what aa --dry-run` and `--what pilot:<NN> --dry-run`: the rids, the masks and the cap, nothing claimed.
3. **The owner's go for the pilot (O2).** Its line is PR 9.
4. `stage-run --what aa`: the two twins start together, are collected, classified, charged (ledger kind `aa`) and scored by
   the ten passes. Then `stage-run --what pilot:<daemon task>` and `--what pilot:<web task>` (ledger kind `pilot`). All six
   are void spend and never data (D16).
5. **By hand, each a dated line** (an Opus judge; the team lead signs): G-10, the scorer by hand on one twin's tree and on a
   Node tree, through the same clean copy and frozen build the scorer uses (`buildAndRestore`, `lib/copy-quick.mjs`), reading
   the session's `deviations` too; G-13, each session's class against its stream and the broker's log; G-16's three lines.
   The trees, the hand's commands and its outputs stay in the series' `private/` folder; a line names counts and a path, never
   a case.
6. `stage-verdicts`: GO, NO GO or STOP. On NO GO the instrument is fixed (a dated `instrument` line; the harness accepted
   before any data session), and the pair that missed is run again: paid, so under O2's words if they cover a repeat, else
   under a new go. The repeat is two more sessions than the 66 the projection counts: the reserve is recomputed before it
   with those two and everything still to run, and it is refused when the rest of the series would no longer fit (PR 4).
   A second NO GO on the same row goes to the owner before a third pair is paid for (v1's rule).
7. The wall and the stall: the one dated `caps` line. The T3 and T5 policy, if the A/A asks for another, as a commit of
   `crosscheck-policy.json` in its free fields.
8. `seed-reveal`: `seed.txt` is written and committed; the guard asks for it from the first data slot on.
9. PR 10 carries steps 5, 7 and 8 and the table's result (counts only). The pilot's spend is on its two ledger lines.

**Done when:** `pilot-verdicts.json` says GO; the dated lines, the `caps` line and `seed.txt` are on `main`; no data session
has been launched.

### (b) the paid rounds — step R · the owner's go (O3)

1. **The owner's go for the rounds (O3)**, asked with the pilot's settled costs and the GO table. Its line names the rounds
   it covers; it rides in PR 10 or in a pull request of its own.
2. Round by round: `stage-run --series <dir> --what next --until round`. Each slot: the go line, the guard, the certification
   probe, the reserve and the disk rule, the clear folder opened, the quiet check, the masks read back, both sessions
   launched together, collected, classified and charged, then scored by the ten passes, the records written, the clear
   folder removed. Then the next slot. In rounds 1 and 2 the loop ends after the read-back slot; (c) says what happens
   before it goes on.
3. **What the operator does at a halt** (exit 3, resumable; the same command goes on): not quiet → wait; `--accept-noisy` is
   not used on a data slot without the team lead's written reason, and the flag stays in the record; a mask not applied, the
   preflight, a scoring environment halt → fix the cause and run again; `harness-moved` → restore the harness the series was
   made with; the backup not verified → mount the share; `void: rate limit` → later; a read-back awaited → (c). **The disk
   rule or the reserve** → the recovery of PR 4, in this order and no other: `stage-archive` to the private backup (copy,
   list, verify, identity recorded); `stage-free`, which removes the three cache folders of final sessions of finished
   slots and nothing else; the same `stage-run` again. Nothing a later slot, the scoring, the adjudication or the extraction
   reads is ever freed, so the series goes on without a restore; `stage-restore` runs once, after the last slot, before the
   final archive and the record's hash list, which both refuse a series with a freed session. A file `<series>/pause` stops
   the loop between slots.
4. **A stop** (exit 2: a `stop:` class, a T1 breach under its policy, a second contaminated void, alias drift) ends the
   series' launches for good, as v1 and the engine have it. The record then names the slots that did not run. Whether the
   rest runs in a new series under the same tag is open point 16; nothing is built for it.
5. Between rounds the team lead reads `stage-status`: every session classed, the round's flags, the spend lines. A go that
   covered fewer rounds is asked again here.
6. After the last slot: `stage-restore` if anything was freed, then the final `stage-archive`.

**Done when:** 30 slots are finished (both stacks scored, or void after the one re-run), or a stop ended the series and the
record names the missing slots; every data session has its `record.json` with ten passes' verdict, its timestamps, `arm` and
`model`; every pair has its pair record; no round started with the round before it unscored; the spend lines are settled.

### (c) the read-back — PRs 11 and 12, each before the slot after its read-back slot

In round 1 and in round 2, the pair the schedule named from the seed (one daemon, one web) is read back by an Opus judge with
PR 6's tool. This is measurement rule 1 for Stage 1: the stacks of a pair differ only where the harness says they do.

**It is done at once, and nothing runs meanwhile** (plan round 1, finding 2; the gate is PR 2's, scenarios 15a to 15e).

1. The read-back slot is scored; `stage-run --until round` ends there and names the slot.
2. The judge is given the print of `readback --series <dir> --pair <slot>` and the manifest, and writes one dated line per
   session, `holds` or `refuted`.
3. The lines go to `main` by their own small pull request (PR 11 for round 1's slot, PR 12 for round 2's); the series'
   worktree is moved forward to it.
4. Both `holds`: `stage-run` goes on with the next slot.
5. One `refuted`: that session is `void: harness`. Its one re-run is the schedule's next step and runs alone, before any
   later slot. The re-run is read back the same way, its line riding in the same pull request's follow-up, and only then is
   the next slot offered. A second `refuted` ends the cell (`missing`), like any second void.
6. A `refuted` line whose cause is the harness itself, and not one launch, cannot be mended after the first data session,
   because the instrument is frozen: the team lead stops the series there, and what follows is open point 16.

So no session is ever voided after later slots ran, the parent's rule (the lines before round 3) holds with room to spare,
and the cost is two waits of a judge's reading and a small pull request each.

## F3.S3 — the record, blind adjudication and the Stage 2 decision

**Model:** scenarios + brief: this document · code Sonnet · review Opus · blind step 1 and step 2: Opus judges · the decision
line: open point 12. **Parent:** F3.S3 (E6.S1, from `note.mjs` to a series rollup; E6.S2 with E2.S5's packets; new).

| Part | What | Pull request (§8) |
|---|---|---|
| (b) | the packets and the two steps for a ten-pass series, as a tool | PR 7, before the tag |
| (b) | the adjudication's run | after the last slot, before PR 14 |
| (a) | the record tools | PR 13 |
| (a), (b) | the data, the bare report, the adjudication's counts, the module docs | PR 14 |
| (c) | the dated decision line | PR 15 |

### (b) blind adjudication — PR 7

**Brief (Sonnet coder).** Branch `feat/lang-v2-f3s3-adjudication`. It changes `spikes/lang-explore/lib` and v1's
`lib/adjudicate.mjs`, both in the harness identity, so it merges **before the pre-registration files are generated and
before the tag**, like every harness change (§3; plan round 1, finding 1). That also fixes the masks and the revised
reading's rule before any result is seen. The adjudication itself runs where the parent puts it, after the rounds (open
point 21).

1. **v1's five steps are reused, with another reader.** `spikes/minigit-bench/lib/adjudicate.mjs`: `buildPackets`,
   `classifyAmbiguity`, `revealFor`, `classifyOrigin` and `revisedFor` take their cells from `readCells` (`:42`), which reads
   v1's layout. `readCells` gains a reader argument; v1's own reader stays the default.
2. **`lib/adjudicate-quick.mjs` (new): the reader for a ten-pass series.** For every data session of the series (the last
   attempt with data of each cell), from the completed attempts under `s/<rid>/score/passes/`: **every case, visible or
   held-out, that failed in at least one completed pass** gives one packet. A packet holds the blind id, the case, the
   sentences of the requirement ids the case cites (from the frozen spec's text), the evidence of the first failing pass as
   the runner recorded it, the pattern (in how many completed passes of each profile it failed, so a mixed case shows as
   mixed), and for a `memory limit` case its cause as `kernel kill` or `runtime abort` with the flags `abort-below-evidence`
   and `child-abort-line` (T-L54; the runtime's own name would give the stack away and is shown only after the reveal).
   A session whose build failed gives no packet; it is counted apart.
3. **The masks.** v1's `maskRules` (`lib/adjudicate-mask.mjs`) with the session's literals, widened by the words a v2 program
   and its runtime print (the two runtimes' names and error shapes, file suffixes, the heap keys); a test plants each.
4. **The verbs.** `explore.mjs adjudicate --series <dir> --step packets|classify|reveal|class|revised` with v1's flags. The
   packets go to `<series>/adjudication-packets/`, the verdicts to `<series>/adjudication/`; both are refused inside any git
   worktree. `adjudication/summary.json` holds counts only (per task and stack: packets, each step-1 verdict, each step-2
   class, mixed against always failing). It and `adjudication/revised.json` (item 6) are the two files the record tools
   read.
5. **The two steps.** Step 1: an Opus judge that is given the packets folder and nothing else answers `spec-ambiguity` or
   `not-ambiguous` with a reason for every packet; the reveal is refused until all are answered, and the answers are frozen
   by it. Step 2, after the reveal: `product` or `harness` with a reason.
6. **The revised reading, and what carries it to the record** (plan round 1, finding 4). A case judged ambiguous in any
   session is dropped from every session of its task; a session's revised verdict is the verdict rule (`verdictOf`) over its
   passes without the dropped cases; a session whose build failed keeps its primary verdict. The revised CPC stands beside
   the primary one and never replaces it. What a `harness` class does is open point 15.
   The step `revised` writes **`adjudication/revised.json`**, once (`wx`; a later run writes a new dated file beside it and
   never over it). It holds one row for **every** data session of the series, also one with no packet, so the record
   tools need no rule of their own: `{ rid, task, stack, round, attempt, primaryVerdict, revisedVerdict, primaryAccepted,
   revisedAccepted, packets, droppedCases, buildFailed }`, the last three as counts and a flag; per task the count of
   dropped cases; `complete: true`; and a fingerprint, the sha256 over the sessions' `score.json` hashes it was computed
   from. **What it must not hold (D41 and the blind rule):** no case id, no blind id, no reason text (a reason may quote a
   case), no packet content, no requirement id; every field it has is one the public record already carries for a session,
   or a count. It is written only by `revised`, which refuses until the reveal exists and every packet has its step-2
   class, and it lives in `adjudication/`, never in the packets folder, which is the only folder the step-1 judge is given.
   The cost is not copied into it: CPC's cost has one source, the session's record.

**Scenarios** (`test/stage-adjudicate.test.mjs`, a synthetic series; v1's `adjudicate.test.mjs` stays green):

93. `Every_case_that_failed_in_a_completed_pass_gets_one_packet_and_a_case_clean_in_all_ten_gets_none`
94. `A_packet_shows_the_case_the_cited_sentences_the_evidence_and_the_pass_pattern_and_nothing_of_the_stack_or_the_arm`
95. `Every_planted_runtime_word_is_masked_in_a_packet`
96. `A_memory_limit_packet_shows_kernel_kill_or_runtime_abort_with_its_two_flags_and_never_the_runtimes_name`
97. `The_reveal_is_refused_until_every_packet_has_its_first_verdict_and_the_first_verdicts_are_frozen_after_it`
98. `The_revised_verdict_drops_an_ambiguous_case_from_every_session_and_never_rewrites_the_primary`
99. `A_failed_build_gets_no_packet_and_is_counted_apart`
100. `The_packets_and_the_verdicts_are_refused_inside_any_git_worktree`
101. `The_summary_holds_counts_only`
     - 101a. `The_revised_file_has_one_row_for_every_data_session_and_a_session_with_no_packet_keeps_its_primary_verdict`
     - 101b. `The_revised_file_holds_no_case_id_no_blind_id_no_reason_and_no_requirement_id` — a scan over the written file
       against the fixture's case ids, blind ids, reasons and requirement ids, and against the list of its allowed keys.
     - 101c. `The_revised_file_is_refused_until_the_reveal_exists_and_every_packet_has_its_second_class`
     - 101d. `The_packets_folder_holds_packets_and_the_mask_list_and_nothing_that_names_a_stack_a_verdict_or_a_session`
     - 101e. `A_second_revised_run_writes_a_new_dated_file_and_never_changes_the_first`
     - 101f. `A_case_dropped_in_one_task_changes_no_session_of_another_task`

**Running it** (after the last slot; the tool has been on `main` since before the tag): `packets`; the step-1 judge;
`reveal`; the step-2 judge; `revised`. **Done when:** every failing or mixed case has a step-1 and a step-2 verdict;
`summary.json` and `revised.json` exist, `revised.json` complete and with a row for every data session.

### (a) the record tools — PR 13

**Brief (Sonnet coder).** Branch `feat/lang-v2-f3s3-record-tools`. A new folder `research/lang-v2/tools/`, outside the harness
identity, built on v1's `research/minigit/tools/` by import (`format.mjs`, `pairs.mjs` `median`, `replay.mjs`, `manifest.mjs`),
never by copy. It may be built during the rounds.

1. **`extract.mjs --series <dir> [--out research/lang-v2/data/<series>.json]`.** Read only. It reads the run-map, the ledger,
   the pair records, every session's `record.json`, `pilot-verdicts.json`, the registered amendments,
   `adjudication/summary.json` and `adjudication/revised.json` (PR 7). From the last it takes each session's revised verdict
   and `revisedAccepted`, by rid. It refuses, naming the count, when that file is absent or not `complete`, when its rows
   are not exactly the data sessions of the run-map, or when its fingerprint is not the one of the sessions' `score.json`
   files now (a re-score after the adjudication makes it stale). It never opens the packets or the verdict files. It writes
   the data file: per session the kind, task, round, stack, `arm`, `model`, class,
   flags, the four cost views with T1 to T5, the tokens per model and kind with the cache split, the time partition with every
   category, the scoring time apart, the verdict of the ten passes, the counts of visible and held-out cases passed, the
   defects as counts, `deviations`, the memory causes and flags; per pair the masks, the stamps and the flags; the identities.
   It never opens `score/` beyond what `record.json` holds, drops every case id, and writes the home as `~`.
2. **CPC has one source.** The per-task figures call `cpcBoth` (`spikes/lang-explore/lib/records.mjs:181`); the ratios, the
   median, the geometric mean and the count are new small pure functions (`summary.mjs`) over §9's five cases. **The revised
   reading is the same functions over the same sessions and the same costs, with `accepted` taken from `revisedAccepted`**;
   nothing about a case is needed for it, and both readings stand side by side in every table that has a CPC.
3. **`render.mjs`: the whole report is generated.** `report-template.md` holds the prose with two kinds of placeholder: a
   block computed from the data file, and a quote read from the parent at the commit the pre-registration quoted. The
   template holds no digit outside a placeholder and a heading's number; a test fails on one. The output is
   `research/lang-v2/RESULTS_lang_v2_stage1_<yyyy-mm-dd>.md`; `render.mjs --check` writes nothing and exits 1 when the
   committed report is not what it would write.
4. **The blocks** (parent Definition of Done, F3.S3 (a), §9): the identities (measurement rule 6: the task repository's sha
   and freeze tag, the pre-registration tag, the harness identity, the runner manifest, the layer, FORMAT's sha, the
   contract's identity, the memory value, the CLI version, the model id, the toolchain checksums, the cache hashes, the
   machine, the dates); per task CPC or the undefined outcome, `CPC_all`, the success rate, tokens, wall, `model_api`,
   `active − model_api`, `deviations` beside `accepted`, the counts of `rejected`, `intermittently incorrect` and
   `unscored: environment` per stack; the overall median of the defined ratios, the geometric mean and the count, each with
   its denominator and the line of undefined outcomes, with and without flagged pairs; known against original, and daemon
   against web, each as a listing and never a summary (D29); every v1 metric (D34); the memory causes; every attempt, voids
   and re-runs included; the quiet flags; the five spend lines; the pilot's table with the twin spread; the read-back lines
   and the amendments; the prediction beside the observation; the voids replayed with the current scan
   (`research/minigit/tools/replay.mjs:85`, its series pattern an argument) and which were the scan's; the adjudication's
   counts and the revised reading beside the primary; the claim's limit beside every ratio; the sentences of T5, T6 and T9,
   T23 and T25 to T28; the paragraph on what the analysis licenses; the olympiad notes and the quick run cited as exploratory
   controls outside the claim, never pooled.
5. **The raw records' hashes.** v1's `manifest.mjs` over the series writes its sha256 list beside the data file. It is
   written over the whole tree: it refuses a series in which any session still holds a `freed.json` and names
   `stage-restore` (PR 4). `extract` and `render` do not need the freed folders and give the same bytes with or without
   them.
6. **Docs:** `research/module_lang_explore.md` (the record tools and where the one engine ends), `research/module_tests.md`.

**Scenarios** (`research/lang-v2/tools/test/*.test.mjs`, fixture series built in the test; no private file):

102. `The_report_is_rendered_from_the_data_file_alone_and_a_second_render_gives_the_same_bytes` (T-L17)
103. `A_digit_in_the_template_outside_a_placeholder_fails`
104. `The_paragraph_on_what_the_analysis_licenses_is_the_parents_text_byte_for_byte`
105. `Known_and_original_tasks_are_separate_listings_with_no_summary_line`
106. `Three_attempts_costing_2_3_and_4_with_one_accepted_give_a_CPC_of_9_and_none_accepted_gives_no_correct_solution_with_its_spend`
     (T-L18)
107. `A_void_is_outside_CPC_and_inside_CPC_all_and_a_session_killed_at_the_cap_counts_its_censored_cost`
108. `A_rejected_and_an_intermittently_incorrect_session_add_cost_to_the_numerator_and_nothing_to_the_denominator`
109. `An_unscored_environment_session_is_on_the_void_line_and_in_neither_part_of_the_primary_CPC`
110. `A_task_with_no_accepted_attempt_in_one_stack_reads_undefined_with_the_stack_its_attempts_and_its_spend_and_never_a_bound`
     (T-L27: the three cases, and a slot that did not run reads `not run`, never a zero cost)
111. `The_median_the_geometric_mean_and_the_count_are_over_the_defined_ratios_only_and_each_states_its_denominator`
112. `Every_v1_metric_has_its_column_and_removing_one_from_the_list_fails` (T-L26)
113. `The_memory_causes_and_the_two_flags_are_counted_per_task_and_stack_from_the_data_alone` (T-L54)
114. `Every_attempt_is_listed_voids_and_re_runs_included_and_the_five_spend_lines_sum_to_the_ledger`
115. `The_claims_limit_stands_beside_every_ratio_and_the_sentence_on_the_quick_runs_task_07_is_there`
116. `Every_line_is_given_with_and_without_the_pairs_that_hold_an_unpaired_or_a_loaded_session`
117. `The_data_file_holds_no_case_id_no_home_path_and_no_token`
118. `The_schema_holds_arm_and_model_for_every_session`
     - 118a. `A_series_that_was_halted_archived_freed_resumed_and_restored_gives_the_data_file_and_the_report_of_one_that_never_halted`
       — the report's half of scenario 54e: the two fixture series of that scenario, extracted and rendered, give equal
       bytes; the same with the three cache folders still freed; and the hash list is refused until `stage-restore` ran,
       then equals the straight series' list.
     - 118b. `The_revised_CPC_is_computed_from_the_per_session_revised_verdicts_and_stands_beside_the_primary` — a fixture
       where one ambiguous case turns a rejected session into an accepted one: the primary CPC is unchanged, the revised
       one counts that session in its denominator.
     - 118c. `Extraction_refuses_an_absent_incomplete_or_stale_revised_file_and_one_whose_rows_are_not_the_data_sessions`
     - 118d. `The_data_file_carries_the_revised_verdict_per_session_and_nothing_else_of_the_adjudication_but_counts`

**Done when:** the scenarios went red, green, red; §8's row for PR 13.

### (a), (b) the bare report — PR 14

Branch `docs/lang-v2-stage1-report` (the team lead; no code). After the adjudication's run and `stage-restore`: `extract`,
the replay, the manifest and `render` run over the finished series; the data files and the report are committed with the rows of `research/README.md`, the updated
`research/module_lang_explore.md` (it describes the one engine, the series and the record tools), the two
language-comparison rows of `research/architecture.md` (one still says "not a series yet", the other calls the engine
exploratory), and `research/module_tests.md`. `render.mjs --check` exits 0;
`leakcheck` and the line scan are clean (the report is a document under `research/lang-v2/`). An Opus judge reads the report
against the pre-registration: every pre-registered figure is there, the conclusion is no wider than the claim and its limit,
and no number stands outside a generated block.

**Done when:** the report is on `main`, generated from the data alone, with T26's sentence and the claim's limit.

### (c) the Stage 2 decision line — PR 15

The line stands in the parent, under *Stage 2: the dated decision*, dated the day the report was generated:

> Stage 2, the process arm on the same six tasks, is decided on `<date>`: go, defer to `<date>`, or cancel, with a reason that
> does not depend on which stack won Stage 1.

PR 15 (branch `docs/lang-v2-stage2-decision`) writes it with the status lines of the parent and of this document and their
`todo/README.md` rows. It is the plan as written being carried out, so it is no entry of the ledger. What follows is outside
F3: on *go*, the engagement pilot before any Stage 2 epic is split; on *defer*, the references and the trap table stay
private until the next dated line; on *cancel*, E6.S3 (the publication, which is public and not to be undone and so waits
for the owner's go like O1; the teardown; the promotion of the parent with its stories documents and their deviations).

**Done when:** the line is on `main`, dated; `plan-lifecycle.mjs` is clean.

## 8. The order of the pull requests and the steps

The merge conditions are the parent's (*Merge conditions*): **(1)** the gates on harness code: a coai plan round at `proceed`
before the code, then a coai code round and an Opus review on the committed branch, every finding resolved; **(2)** D68's
whole runs of every suite touched, plain and with `BENCH_REQUIRE_SANDBOX=1`, every test passed and nothing skipped, and CI's
required checks present and passing at the head sha; **(3)** D41: the line scan before every merge, and `leakcheck` when a
document under `research/lang-v2/`, a registered file or an agent-visible file is touched; **(4)** no Stage 2 code.
`plan-lifecycle.mjs` clean on every one. Each pull request merges `main` before its last whole run. At most three are open
at a time in this repository.

| # | Story | Branch | What it is | What it runs before it merges | Conditions |
|---|---|---|---|---|---|
| 1 | — | `docs/lang-v2-f3-stories` | this document and its `todo/README.md` row | line scan; `plan-lifecycle`; CI; an Opus reading by the team lead | (2) CI only, (3) line scan, (4) |
| 2 | F3.S1 (a) | `feat/lang-v2-f3s1-schedule` | the schedule, with the read-back gate | the gates; lang-explore whole runs, both ways; line scan | (1), (2), (3) line scan, (4) |
| 3 | F3.S1 (a) | `feat/lang-v2-f3s1-series` | the series over six tasks, the seam in v1, the go line | the gates; lang-explore and minigit-bench whole runs, both ways; the free rehearsal of round 1; line scan | (1), (2), (3) line scan, (4) |
| 4 | F3.S1 (a) | `feat/lang-v2-f3s1-guards` | the guards before a paid pair, the reserve before every launch, the archive, free and restore verbs | the gates; lang-explore and lang-bench whole runs, both ways, `LANG_V2_TASK_REPO` at W; the real probe; `leakcheck`; line scan | (1), (2), (3) with `leakcheck`: it changes how the held-out side is opened, (4) |
| 5 | F3.S1 (b) | `feat/lang-v2-f3s1-prereg-tools` | the pre-registration tools, the widened guard, the seed, the CLI pin | the gates; lang-explore and minigit-bench whole runs, both ways; line scan | (1), (2), (3) line scan, (4) |
| 6 | F3.S2 (a), (c) | `feat/lang-v2-f3s2-pilot-readback` | the pilot's table and the read-back's reading, as tools | the gates; lang-explore and minigit-bench whole runs, both ways; line scan | (1), (2), (3) line scan, (4) |
| 7 | F3.S3 (b) | `feat/lang-v2-f3s3-adjudication` | the packets, the two steps and the per-session revised verdicts, as a tool | the gates; lang-explore and minigit-bench whole runs, both ways; `leakcheck`; line scan | (1), (2), (3) with `leakcheck`, (4) |
| 8 | F3.S1 (b) | `docs/lang-v2-prereg` | the pre-registration files; the manifest read after PRs 2 to 7 | every generator's `--check`; `identityProblems` empty on its head; the document gate; the three `--check` tools of lang-bench; `leakcheck`; line scan; CI | (2) CI only, (3) with `leakcheck`: public text over the private side, (4) |
| T | F3.S1 (b) | — | the dry run and the stub round on the final harness, **the tag — the owner's go (O1)** | step T's steps 0 to 6 | (3): the tag's message is public text |
| 9 | F3.S2 (a) | `docs/lang-v2-go-pilot` | the owner's go line for the pilot | line scan; CI | (2) CI only, (3) line scan |
| P | F3.S2 (a) | — | `stage-init`; **the A/A and the two pilot pairs — the owner's go (O2)**; the by-hand reads; `stage-verdicts` | step P | — |
| 10 | F3.S2 (a) | `docs/lang-v2-pilot-record` | the by-hand lines, the `caps` line, the policy if it moved, `seed.txt`, the go line for the rounds | `leakcheck`; line scan; CI | (2) CI only, (3) with `leakcheck` |
| R | F3.S2 (b) | — | **rounds 1 to 5 — the owner's go (O3)**; the loop ends after each read-back slot | step R | — |
| 11 | F3.S2 (c) | `docs/lang-v2-readback-r1` | the read-back lines of round 1's read-back slot, before the slot after it | `leakcheck`; line scan; CI | (2) CI only, (3) with `leakcheck` |
| 12 | F3.S2 (c) | `docs/lang-v2-readback-r2` | the read-back lines of round 2's read-back slot, before the slot after it | `leakcheck`; line scan; CI | (2) CI only, (3) with `leakcheck` |
| 13 | F3.S3 (a) | `feat/lang-v2-f3s3-record-tools` | the record tools | the gates; the tools' tests; lang-explore whole runs (it imports `records.mjs`); line scan | (1), (2), (3) line scan, (4) |
| 14 | F3.S3 (a), (b) | `docs/lang-v2-stage1-report` | the data, the bare report, the docs | the adjudication's run complete; `stage-restore` if anything was freed; `render.mjs --check`; `leakcheck`; line scan; CI; an Opus reading against the pre-registration | (2) CI only, (3) with `leakcheck` |
| 15 | F3.S3 (c) | `docs/lang-v2-stage2-decision` | the dated decision line | `plan-lifecycle`; line scan; CI | (2) CI only, (3) line scan |

**Order and why.** PR 1 first, so every brief cites a merged document. PR 2 is pure and comes first of the code; PR 3 needs
it. PRs 4 and 5 are built beside PR 3 and merge after it (they supply the guard it refuses to run without). PRs 6 and 7 are
built beside them. **Every pull request that changes a harness folder, PRs 2 to 7, is on `main` before PR 8's manifest is
generated** (plan round 1, finding 1), so the manifest names the harness the pilot and the rounds will run, the dry run of
step T runs on that harness, and nothing of it moves after the tag. PR 8 is the last merge before the tag. Step T. PR 9,
step P, PR 10. Step R: round 1 up to its read-back slot, PR 11, the rest of round 1 and round 2 up to its read-back slot,
PR 12, then to the end of round 5. PR 13 is outside the harness identity and may be built any time after PR 3. After the
last slot: the adjudication's run, `stage-restore` if anything was freed, the final archive, PR 14, and PR 15 the same day
as PR 14's report.

**Against the parent's build order.** Steps 5, 6 and 7 keep their order as activities: the series code and the tag, then
the pilot, the rounds and the read-back, then the adjudication, the record and the decision line. What moved is where two
tools are built: the pilot's and the read-back's (PR 6) and the adjudication's (PR 7) merge before the tag, although they
belong to F3.S2 and F3.S3. Whether that is an entry of the parent's ledger is the team lead's (open point 21).

## 9. The model per role

| Role | Model | In F3 |
|---|---|---|
| Stories, scenarios and briefs | opus by the role table | this document was written by the Fable planner on the team lead's order of 2026-11-19 (open point 18); F3 has no cornerstone |
| Coder | sonnet | the code and tests of PRs 2 to 7 and 13, and the fixes after review |
| Small edit | main session or sonnet | the dated-line pull requests 9 to 12 and 15: one file each, a few lines, bounded before the first edit |
| Reviewer and judge | opus | the review of every code pull request beside the coai code round; the by-hand rows G-10, G-13 and G-16; the read-back of rounds 1 and 2, done at once after its slot; both adjudication steps (step 1 sees the packets only); the reading of the report against the pre-registration |
| Scout | sonnet | evidence only: a stream's quiet gaps, the remote's tags, a series' state |
| The team lead (main session) | — | asking the owner for O1, O2 and O3; the tag; `stage-init`; every paid command; `stage-archive`, `stage-free` and `stage-restore`; `seed-commit` and `seed-reveal`; the pre-registration's prose with an Opus writer; never a coder |

The cornerstone ledger does not change: the parent's line reads *Cornerstones: 2 of 2 — F1.S2, F2.S1*, and this document
marks none.

## 10. Growth

- **The Stage 1 series** (`~/rsd/lang-v2/explore/x-<date>-<hex>/`, outside git): 66 sessions and at most one re-run of each
  void; a repeat pair of the pilot after a NO GO adds two sessions each. At the quick run's size, about 1.4 GB a session,
  that is about 92 GB, and about 176 GB if every data session were re-run; the ten passes' records add under 1 MB a
  session. Kept for ever, append only (D34); `prune` refuses it. **The reserve is checked before every paid launch** over
  what is still to run, a repeat pair included, and the disk rule before every slot. **What a disk halt may free** is one
  closed list, the three cache folders in the box of a final session of a finished slot (`box/nuget`, `box/npm`,
  `box/home/.dotnet`), and only after the archive holding them was verified; they are restored from it before the final
  archive and the record's hash list. Nothing else of the series is ever removed. Interrupted: a halted slot is resumed, a
  `launching` session is settled by v1's `reconcile`, an interrupted pass is an environment attempt and is run again
  (D71); a `stage-archive`, `stage-free` or `stage-restore` that was killed is run again and finishes from the lists
  (each removes or copies only what its list names, and verifies after).
- **Its verified copy** on the private backup: the same size, made after the series ended and at any disk halt before local
  space is freed; each archive is an entry of `archive.json` with its list's sha256.
- **The clear held-out folder:** one task's at a time, for one slot, mode 700, removed after it and at the start of a run
  after an interruption.
- **The free rehearsal series** (`r-…`): six slots of hello programs, well under 1 GB. It is no data; it is removed by hand
  once step T's dated line records its result, and a killed one is swept by the owner rules.
- **The pre-registration files and the dated lines:** kilobytes, in git; `amendments.md` only grows.
- **The seed:** one file of a few bytes, outside git, mode 600, kept.
- **The CLI copy:** one folder under `~/rsd/lang-v2/toolchains/`, kept, because every record names it.
- **The adjudication's packets and verdicts:** a few megabytes in the series' folder, private, kept with it; `revised.json`
  is a few kilobytes of rows and counts, and a second run adds a dated file beside it.
- **The data files, the hash list and the report:** in git, written once per series. v1's are 28 to 112 KB a series and a
  2.4 MB hash list for 12 sessions; Stage 1's are a few megabytes in all. A second render replaces the report whole.
- **The dry run's scratch clone and remote:** owned folders, removed when step T ends.

## 11. Open points found while writing, for the team lead

Each is a gap or a conflict in the parent, or a reading this document had to take. None is decided here. Line numbers are of
`todo/PLAN_lang_comparison_v2.md` at `5740809f`. **Needed before the pre-registration files are written (PR 8):** 1 to 5, 7 to
11 and 13 to 15. Point 21 came from the plan round of 2026-11-19.

1. **When a void is re-run.** §4.4 says after the round's own pairs and before the next round (`:1283`). The operative part
   says "the barrier and the one void re-run stay" (`:764`), and the barrier as built refuses the next pair until the void is
   re-run (`spikes/lang-explore/lib/series.mjs:65`). This document follows the operative part: the re-run comes before the
   next slot. The pre-registration must state one of them.
2. **The pilot's tasks and the A/A.** F3.S2 (a) has one pair per kind (`:780`), so one daemon task; G-16 wants the drivers
   checked on real pilot trees of three tasks, 05, 10 and 04 (`:1743`). The A/A is "two identical bare sessions" with no task
   or stack named; D16's A/A is of an olympiad task, which is outside Stage 1's claim (`:1017`, `:1725`). A reading that
   gives all three trees: the web pair on 04, the daemon pair on one of 05 and 10, the A/A on the other. The task, and the
   A/A's stack, need the team lead's word.
3. **G-16's RSS curve.** G-16 asks the hand's RSS curve to agree with the descriptive curve within 10 % (`:1743`); D74 made
   the curve follow-up work that was never measured (`:829`). The clause cannot be checked; it needs a dated line saying so.
4. **G-14.** §11 keeps G-1 to G-14 (`:1717`), and G-14 is v1's rehearsal of its second phase
   (`todo/PLAN_minigit_stack_bench_E3_bare.md:526`), which a one-phase series does not have. A reading: the free rehearsal of
   a daemon and a web task passes in both stacks on the series' harness.
5. **G-18's cross-check figure.** Its formula (`:1771`–`:1780`) is bounded by `capMax(arm)`, which is defined from the
   superseded $1000 ceiling, and scales by time guesses for tasks that were not piloted. A reading: twice the larger settled
   cost of the pilot pair of the task's kind. It binds nothing either way (`:1745`).
6. **D47 and the T-L rows still speak of groups.** The CLI is re-pinned "at group A's pre-registration" and the baseline is
   set by "the first session of group A" (`:1048`); T-L9, T-L10 and T-L40 to T-L42 are worded per group (`:2022`, `:2053`).
   D69 removed the groups. This document reads them as the one pre-registration and the first session of the series.
7. **The prediction.** §9's draft is for ten tasks and two arms (`:1665`); nothing states it for six tasks, bare, so there is
   no text to quote verbatim, and T28 asks `prediction.md` to say when it was written (`:849`). Who writes the six-task
   lines, and whether they go into the parent first, is open.
8. **Q6, the sign test.** Still unanswered (`:2233`), while the epoch-9 line says Stage 1 opens no question (`:2185`). The
   contract must say whether the line is shown; this document writes "not shown".
9. **Which policy fields the A/A may set.** The parent says T3 and T5 (`:1562`, `:1611`, `:1751`) and that v1's guard allows
   it (`:1510`); v1's guard frees `t2` and `t3` (`spikes/minigit-bench/lib/prereg-guard.mjs:30`). This document takes the
   parent's T3 and T5.
10. **The bare prompt's limits sentence.** §4.5 says the hidden tests run "under registered time and memory limits, never
    below the floors STATEMENT.md guarantees" (`:1307`), and that the final wording is fixed in E4.S2 (`:1299`), which is
    F3.S1 (b). Since D72 the statement states the limit itself (`:827`). Keep the sentence, or change it before it is
    registered: it is every session's prompt.
11. **The day of the prices.** §8 has them re-read on the series' first day (`:1568`); §9 freezes them with the tag before the
    first launch (`:1578`). They agree only if the tag and the first launch are on one day. This document reads them on the
    day the files are generated.
12. **Who decides the Stage 2 line.** The parent gives its words and its date (`:851`–`:861`) and says the reason must not
    depend on which stack won (`:831`); it does not say who decides. The line spends nothing itself, so §4 does not list it.
13. **The number the disk rule reads.** *Growth* counts a kept session at about 1.4 GB (`:897`–`:899`) and calls the pair's
    worst case "two sessions' trees plus that size twice" (`:904`). `inputs/growth.json` measures the built copy and the
    retained scoring records, 117 MB for a pair, and F2.S1's reference measurements are in the private package records only.
    With that number alone the reserve for 66 sessions is a few gigabytes against about 92 GB of real growth. This document
    adds the measured session folder to the rule; that makes the rule stricter, never smaller. It also checks the reserve
    before every launch and not only before the first pair (`:757`–`:759`), so that a repeat pair of the pilot is counted;
    that adds a check and removes none.
14. **The re-run spend line.** D7 and the Definition of Done name pilot, A/A, data, void and re-run lines (`:1008`, `:482`);
    the ledger has no re-run kind. This document derives the line from the attempt number and asks for the split to be
    confirmed, because the pre-registration states it.
15. **What a `harness` verdict of step 2 does.** E6.S2 puts a revised score beside the primary (`:2795`); D71 makes a
    rejection permanent and never re-run (`:826`); G-16's remedy is "fix, re-score". Whether a failure the harness caused
    changes a session's class or its CPC is not said. This document reports it and changes nothing.
16. **After a series stop.** §11 says a stop holds "until fixed and an A/A is GO" (`:1749`); the engine and v1 refuse a
    stopped series for good. Whether the slots left run in a new series under the same tag, and are read with the first, is
    not said; nothing is built for it.
17. **The credentials.** The Definition of Done has them torn down after the series (`:501`); E6.S3, which does it, comes
    after the dated decision (`:890`), and a *go* needs them. F3 leaves that box open.
18. **Who wrote this document.** The role table gives a stories document to Opus; the team lead ordered this one from the
    Fable planner. No cornerstone is marked, so the ledger is untouched.
19. **The go line in code.** The refusal of a paid launch without its `owner-go` line (PR 3) is a guard this document adds
    for §4. It is no spend limit (D53 adds none); if the team lead reads it as a change, it becomes procedure only.
20. **No ruleset protects the pre-registration tag** in this repository; the task repository's cover `spec-frozen-*` only.
    The guard sees a moved tag, because the series pins the commit. A ruleset would need the owner's own go.
21. **Two tools are built before the tag** (plan round 1, finding 1, accepted). The parent's build order names F3.S2 after
    the tag and F3.S3 after the rounds (`:815`–`:817`). The pilot's and the read-back's tools (PR 6) and the adjudication's
    (PR 7) change harness folders, so they merge before the manifest and the tag; the pilot, the rounds, the read-back, the
    adjudication's run and the report keep the parent's order. Whether moving the two tools is a build-order entry of the
    parent's ledger, or only the plan as written being met (the tag must freeze the instrument that is used), is the team
    lead's. The same round's findings 2 to 5 add checks and steps inside F3's own briefs and change nothing the parent fixes:
    the read-back gate is stricter than "before round 3" (`:788`), the reserve is checked more often (`:757`), and the
    recovery frees less than "what the verified archive holds" allows (`:761`–`:763`).

## 12. Definition of Done (F3)

- [ ] F3.S1 (a): PRs 2, 3 and 4 merged, every scenario red, green, red; the free rehearsal of round 1 recorded; the real
      certification probe passes on the series host.
- [ ] F3.S1 (b): PRs 5 and 8 merged, PR 8's manifest read after PRs 2 to 7 were all on `main`; every generated file equals its
      generator's output; the document gate at `proceed`; the dry run on the final harness and its pairs recorded; **the tag `prereg/lang-v2-<yyyymmdd>` on the remote, under the owner's go (O1), before any
      launch**; `prediction.md` in it and no `seed.txt`.
- [ ] F3.S2 (a): PR 6 merged before PR 8 and the tag; no harness folder changed between the tag and the pilot; **the pilot
      run under the owner's go (O2)**, the reserve checked before every launch and before any repeat pair; the by-hand lines, the one
      `caps` line and the revealed seed on `main`; `pilot-verdicts.json` says GO; G-17 and G-18 read; the pilot's spend on its
      own lines.
- [ ] F3.S2 (b): **the rounds run under the owner's go (O3)**; 30 slots finished, or a stop with the missing slots named;
      every data session scored by the ten passes with its `record.json`, `arm` and `model`; every attempt kept; no round
      started with the round before it unscored; T1 to T5 held for every data session, or the breach is in the report.
- [ ] F3.S2 (c): the read-back lines of round 1's and round 2's read-back slot on `main`, each before the slot after it ran
      (so before round 3); a refuted session re-run and read back before any later slot.
- [ ] F3.S3 (b): PR 7 merged before PR 8 and the tag; every failing or mixed case has a step-1 and a step-2 verdict;
      `adjudication/revised.json` is complete, with a row for every data session and nothing of a case; the revised reading,
      computed from it by the record tools, stands beside the primary.
- [ ] F3.S3 (a): the report on `main`, generated from the data alone, `render.mjs --check` at 0; it carries per task CPC or
      the undefined outcome, the six-task median, the geometric mean and the count, every v1 metric, `deviations` beside
      `accepted`, the counts of `intermittently incorrect` and `unscored: environment` per stack, the claim's limit, T26's
      sentence, every identity, every attempt and the five spend lines; the olympiad notes cited as controls outside the claim.
- [ ] F3.S3 (c): the dated decision line in the parent.
- [ ] The series' verified copy is on the private backup, its identity recorded; if a disk halt freed cache folders, only
      the three listed ones of final sessions were freed, and they were restored before the final archive and the hash list.
- [ ] `research/module_lang_explore.md`, `research/module_tests.md`, `research/architecture.md` and `research/README.md`
      describe what now exists; `research/module_tests.md` lists every scenario of this document as covered, or not covered
      with a reason, and says what the harness does not prove.
- [ ] Every pull request met its row of §8; every failure on the way is a dated line in the leftovers plan's S5.
- [ ] No held-out content, case id or trap name in any public text: `leakcheck` and the line scan clean where §8 asks.
- [ ] `plan-lifecycle.mjs` is clean; this document's status line says what is done; any change found on the way is an entry
      in the parent, never a ledger here.
- [ ] Left to the parent's own Definition of Done, after the decision: the publication of the references and the trap table,
      the credential teardown, v1's status line, and the promotion.
