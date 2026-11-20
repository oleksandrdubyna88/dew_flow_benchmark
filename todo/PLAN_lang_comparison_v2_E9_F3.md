# PLAN — language comparison v2, F3 stories: the series and the report — the series over six tasks, one pre-registration, the pilot, five paid rounds, the read-back, the record, blind adjudication and the Stage 2 decision line

> Status: **2026-11-20: PRs 2 to 4 of F3.S1 (a) are merged (#236, #237, #238); PR 5 is built on `feat/lang-v2-f3s1-prereg-tools` and not merged; nothing paid has run, and O1, O2 and O3 still wait for the owner. The parent stopped for re-review at its entry 10.2 (PR 3's narrowed `init` refusal) and reopened as epoch 11 on 2026-11-20, approved by reviewers; its epoch-11 revision adds dated lines here, each marked "(2026-11-20, epoch 11)": the reading of `init` in PR 3's brief item 3 (change 10.2); in PR 5's brief item 2, candidate (b) reverted to the brief (`stage-sync --accept-harness` comes back, the freeze at the first data plan kept), (c) the widened harness identity with the `.agents/conventions` submodule's commit, and (d) the policy's free fields `t3`, `t5`, `decidedOn` and `reason`; the same in §3 and in the pilot's step 6. Next, once epoch 11 opens: PR 5 (restore (b), the whole suites, CI, the merge), PR 6, step T.** *Before this:* **plan only, 2026-11-19: F3's stories document, written when F3 starts. F2 is done (the freeze `spec-frozen-B-20261118` is accepted: C `502b65ad153887464c0c45ebc13e9bbef545c415`, W `f3d256e5fe1f774e189a9850056559b39b995a27`). Nothing of F3 is built. Three steps of it need the owner's explicit go before they run: the push of the pre-registration tag, the pilot's paid sessions, and the paid rounds (§4). Fourteen open points are in §11, each marked for the owner or the consultants; nine must be answered before the pre-registration files are written and one, the owner's, before PR 3. Revised 2026-11-19 for F3's plan round 1 (`proceed`, 2 of 2 reviewers, five findings accepted) and again the same day for the team lead's decision on the build order (consults `c823a3ae` and `757cdb08`) and an Opus review (all findings accepted): the parent's build order is kept; the harness (PRs 2 to 5) is frozen by the tag and the series runs from a branch that takes registered files only; the pilot's GO table, the read-back's print and the blind adjudication are tools under `research/lang-v2/tools/`, outside the harness identity, whose every consequential rule the tagged contract fixes; no merge condition is lowered; seven open points are closed from the parent's text.**
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
  `void` (`spikes/minigit-bench/lib/spend-gate.mjs:10`). There is no line for a re-run.
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
  `lib/series.mjs:160`) with `run-map.json`, `ledger.ndjson`, `pairs/`, `s/<rid>/` and `private/`. The parent's *Growth*
  counts 632 GB free on 2026-11-07 (`:900`).

## 3. Conventions for every story

F1's §3 and F2's §3 hold here unchanged (roles, the merge conditions, red-green-red, test names as sentences, D41, shape,
`-m:4`, commits by path, the commands, docs), with these additions:

- **Three steps wait for the owner's explicit go, and no other step does** (§4). A go is the owner's own words with a date. It
  is written as a dated line into the registered `amendments.md` before the step, and the paid command refuses a launch whose
  scope has no such line (F3.S1 (a), PR 3). An agent's message, a consultant's answer or an earlier go for another step is not
  a go. Whether that refusal stands in code is the owner's to say before PR 3 (open point 19).
- **The harness is fixed by the tag** (plan round 1, finding 1). Every pull request that changes a folder of the Stage 1
  harness identity (F3.S1 (b)) is one of PRs 2 to 5 and merges before the pre-registration files are generated. The manifest
  holds that identity, the dry run of step T runs on it, and the tag freezes it, so the instrument the pilot and the rounds
  use is the one the tag names. After the tag no pull request of F3 changes a harness folder. A harness fix that cannot be
  avoided is an instrument amendment with a dated line, possible only before the first data session; after it, it is
  refused (v1's rule, `spikes/minigit-bench/lib/run-map.mjs:15`, `hasDataSession`).
- **The series runs from its own branch, which takes registered files only** (the Opus review, M1). Other plans merge into
  `main` every day, and some touch folders of the harness identity (`spikes/minigit-bench/lib`, `spikes/policy-ab-quick`).
  Freezing those folders on `main` from the tag to the last slot would stop other plans for the weeks the series lasts and
  would need a rule every session of this repository obeys; so the series does not follow `main`. It runs from one worktree
  on a local branch cut at the tag's commit. That branch moves only by `stage-sync` (PR 5), which takes from `origin/main`
  the amendable registered files and nothing else: `amendments.md`, `seed.txt`, `crosscheck-policy.json`, the two cap
  fields of `explore.json`, `quiet.json`, `package-flags.json`. The guard refuses a paid launch when the branch differs from
  the tag in any other path, or when one of those files is not `origin/main`'s blob. So a harness change that reaches `main`
  after the tag neither reaches the series nor stops it, and every dated line the series acts on is public first.
  *(2026-11-20, epoch 11, candidate (c):)* the Stage 1 harness identity also holds `spikes/lang-explore/runner`,
  `spikes/lang-explore/templates`, `spikes/lang-bench/tools` and the `.agents/conventions` submodule's commit (its
  gitlink), because the harness imports or runs them. So a rules pin on `main` moves the identity too: one merged before
  PR 6 is in the manifest PR 6 generates, and one merged after the tag does not reach the series' branch. A paid launch
  is refused while the submodule is off its recorded commit or has local changes. The first bullet's "every pull request
  that changes a folder of the Stage 1 harness identity is one of PRs 2 to 5" binds F3's own pull requests; another
  plan's change to a harness folder before PR 6 (#232, #234 and the rules pins so far) is read by PR 6's manifest.
- **Three tools stand outside the harness identity** (the team lead's decision of 2026-11-19, below).
- **No task-repository pull request.** Merge condition 5 does not bite in F3. Merge conditions 1 to 4 do, and §8 says which for
  each pull request.
- **Whole runs, never fewer than the parent asks** (the Opus review, B1; merge condition 2, `:524`–`:527`). Every pull
  request that touches a file under `spikes/lang-explore/`, `spikes/lang-bench/` or `spikes/minigit-bench/` runs that
  suite whole, plain and with `BENCH_REQUIRE_SANDBOX=1`, at the head that merges. That holds for a pull request of one dated
  line too: `amendments.md`, `seed.txt`, `crosscheck-policy.json`, `explore.json` and the pre-registration files are inputs
  of the lang-explore suite. A whole run is never made beside a running slot. A pull request that touches
  `spikes/minigit-bench/` says in its description that v1's `harnessIdentity` moved and that no v1 series is open (the check
  of F1.S1 (b)).
- **Long commands run in a `systemd-run --user` unit** with `PATH` set: a round, the pilot's pairs, `stage-archive`. A
  background job and a `setsid` child die with the session that started them. This is for the series' commands, not for
  test suites.
- **At most three pull requests are open at a time** in this repository. Branches are built locally side by side; a
  fourth pull request is opened only when one of the three has merged.
- **D41 in F3's own outputs.** The pre-registration files, the data file, the report, the dated lines and the tag's message are
  public text: counts, hashes, costs, times and classes only. No case id, no trap name, no witness id, no case text, no
  requirement breakdown per case. The adjudication packets hold case text and stay in the series' private folder.
- **Nothing typed.** Every pre-registration file but its own stated prose, the data file and the report are written by a tool
  from the code's constants, the pinned inputs or the records. A test regenerates each and compares bytes.
- **The commands** add: `node spikes/minigit-bench/run-tests.mjs` (both ways) where v1 is touched; the pre-registration tests
  `node $LOW node --test spikes/lang-explore/test/stage-*.test.mjs`; for the tools of `research/lang-v2/tools/`
  `node --test research/lang-v2/tools/test/`; and where a v1 record tool is touched its own self-checks,
  `node research/minigit/tools/replay.mjs --self-check` and `node research/minigit/tools/manifest.mjs --self-check`.

### The tools outside the harness identity: the decision on the build order

Plan round 1 found that a tool merged after the tag under `spikes/lang-explore/lib` would make the live harness differ from
the tagged manifest. The first revision answered by building the pilot's, the read-back's and the adjudication's tools
before the tag, which moved them ahead of the parent's build order (`:815`–`:817`). **The team lead decided on 2026-11-19
(consults `c823a3ae` and `757cdb08`, both consultants for the design; the owner was told): the parent's build order is
kept.** The pilot's GO table, the read-back's print and the blind adjudication are tools under `research/lang-v2/tools/`,
beside the record tools, as v1's `research/minigit/tools/pairs.mjs` computes v1's registered analysis outside v1's
harness. They merge after the tag in the parent's order: the GO table and the read-back before the pilot runs (PR 7), the
adjudication when no launch remains (PR 12). Plan round 1's finding stays met: no harness folder changes after the tag.
This is also the reading of the parent's §6.2, row 9 (`:1510`): the pre-registration freezes what its readers read, and a
reader built later reads only what was frozen. Five conditions hold for every such tool, and each has its test (scenarios
92c to 92h):

1. **The tagged contract resolves every consequential choice a tool implements**, so the code has no room to interpret:
   which rows of the GO table apply to which sessions and what missing evidence means; which fields the read-back
   compares; which cases get a packet across the ten passes and which pass's evidence is shown; every mask; when a case
   is excluded as ambiguous and what that does to a verdict; what a `harness` class does. These rules are registered as
   data, `pilot-rules.json` and `adjudication-rules.json` (F3.S1 (b)), and a tool reads them from the tag's commit. A
   choice the contract leaves open is a refusal of the tool, never a default.
2. **Launch, scoring, scheduling, the barrier and every enforcement stay in the frozen harness, and the harness never
   imports a tool.** A gate of the schedule reads a dated line of the registered amendments, never a tool's file. A tool
   only reads stored session and score files and writes separate derived artefacts, under `<series>/derived/` and the two
   adjudication folders; it changes no record, no run-map entry, no ledger line. Every artefact names the tool's own
   version (the blob ids of its files) and the fingerprints of everything it read.
3. **v1's `spikes/minigit-bench/lib/adjudicate.mjs` and every other harness file stay untouched.** A tool orchestrates
   over the exports that exist. Each tool declares the harness files it imports, a test holds that list equal to its real
   transitive imports, and at run time the tool refuses when one of them is not the tag's blob. No pull request of a tool
   changes a path under a folder of `STAGE_HARNESS_DIRS` or a registered file.
4. **Each tool is tested on synthetic fixtures against the registered rules before it is used on a real session**, and
   once over the free rehearsal series.
5. **The record says it plainly:** the adjudication's code is written after the data collection ends; its complete rules
   were registered before the first paid session. The contract and the report both carry that sentence.

## 4. The steps that need the owner's word

| # | Step | Why | What the go must name | What runs without it |
|---|---|---|---|---|
| O1 | The push of the tag `prereg/lang-v2-<yyyymmdd>` to this repository's remote (F3.S1 (b), step T) | Public and not to be undone: the analysis is fixed in public history; from then on a registered snapshot cannot be superseded (`cache-store.mjs`) and a registered file changes only by a dated amendment | the tag's name and the commit it is put on | everything before it: PRs 2 to 6, and the dry run in a scratch clone with the scratch clone's own tag. No tag of that name is made in the real clone before the go: a local `prereg/lang-v2-*` tag already locks the snapshots, because `cache-store.mjs:238` reads the local tags too |
| O2 | The pilot's paid sessions: the A/A pair and the two pilot pairs, six sessions, and one re-run of each of them that ends as a void (F3.S2 (a), step P) | Money: void spend, each session capped at $50: $300 for the six, and $600 for twelve if every one were re-run once; the quick run's mean was $1.86 a session (information, not a bound the plan relies on) | the pilot; whether it covers the one re-run of a void session (this document asks that it does, or a void stops the pilot until a second go); and how many repeat pairs after a NO GO it covers (none, unless it says so). Each repeat is two more sessions at the same cap, and the disk reserve is checked again before it | `stage-init`, the dry run of the pilot, the free rehearsal |
| O3 | The paid rounds: 60 data sessions in five rounds, and at most one re-run of each void (F3.S2 (b), step R) | Money: no total ceiling exists (D7), so the only bound is 120 sessions (60 and one re-run each) times $50; at the quick run's mean the 60 sessions are about $110 (information, not a bound) | the rounds it covers (all five, or fewer); a go that names fewer is asked again before the next round | the GO table, the dated lines, the seed reveal, every dry run |

Nothing else in F3 needs the owner's go to run: not a merge, not the seed reveal, not an amendment for the wall and the
stall, not the report's pull request. Two open points are the owner's to answer and are no step: who decides the dated
decision line (open point 12) and the go line's refusal in code (open point 19). A second A/A or pilot pair after a NO GO
is paid for only under O2's words or a new go. If the series stops (a `stop:` class, a T1 breach, a second contaminated
void), a new series is a new O2 and O3. Outside F3, for the record: if the decision line says *go*, Stage 2's first step,
the engagement pilot, is a paid session and needs its own go from the owner.

## 5. F3.S1 — the series over six tasks and one pre-registration

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
| (b) | the pre-registration files | PR 6 |
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
  attempts) are not all distinct is refused when the seed is drawn, before its hash is committed, and another is drawn.
  **`seedProblem` is pure in PR 2 and no command calls it yet. Its wiring into the real draw-and-commit path is PR 5's seed
  verb and a prerequisite of it:** `explore.mjs seed-commit` asks `seedProblem` of each drawn seed and draws another before
  it writes the seed file or the hash (PR 5's scenario 65a). Until PR 5 merges no seed is committed. The A/A's and the
  pilot's sessions keep `pilotRid` (they run before the seed is revealed).
- **Which stack is launched and scored first** in a slot is a seeded bit, as v1's `firstStack` (`bare-plan.mjs:35`), with the
  task and the round in its text.
- **The barrier and the round rule.** A slot is offered only when every earlier slot is finished (`pairComplete`: both stacks
  scored, or void after the one re-run). A void is re-run once, alone, as `ran unpaired`, before the next slot (the barrier as
  `lib/series.mjs` has it; the parent's §4.4 words it differently and the operative part holds: §11, closed point 1). Round r + 1 therefore never starts while a
  pair of round r is unscored; the module also states that as its own check, so a later change to the barrier cannot lose it.
- **What `nextSlot` says: the slot and exactly what is still to run** (plan round 2, finding 2). `ok(...)` is never the slot
  alone: it carries `action` (`plan`, `resume` or `rerun`) and `todo`, the sessions still to run, each with its `stack`,
  `attempt`, `rid` and `step`, so a caller can neither repeat a scored attempt nor launch both stacks when one is done. The
  decision per session state, the same for a slot and for a pilot pair:

  | The stack's last session | `todo` |
  |---|---|
  | none (absent) in both stacks | both stacks, attempt 1, step `plan`, the leading stack first |
  | one stack holds exactly one session, only `planned` (a crash between planning the two), the other none | action `resume`: **first** the missing stack, step `plan`, **then** the lone session, step `launch` (`todo[0]` is the plan), so a second interruption leaves one planned or both planned, never one launched without its partner (code rounds 2 and 3) |
  | none in one stack and any other session in the other (launching or later, or two sessions) | a refusal naming the unit (`holds sessions of one stack only`): the two stacks are planned together, so it cannot say what is still to run |
  | `planned`, never started | that session, step `launch` |
  | `launching` (a crash or a halt cut the launch short) | that session, step `settle` (v1's reconcile settles it; it is neither launched again nor charged twice) |
  | ended with data, not scored (a killed or crashed session is a data attempt) | that session, step `score` |
  | scored | nothing: a scored attempt is never repeated |
  | void at attempt 1 | that stack alone, attempt 2, its own rid, step `plan` (`rerun`) |
  | void at attempt 2 | nothing: the cell ends `missing` |
  | a stop in one stack, the partner `launching` or ended with data and unscored | that partner session only, step `settle` or `score`, never `plan` or `launch` (v1's precedent, `bare-settle.mjs` `recordWaitingStop`: a stop is recorded once no session is unsettled); the result also carries `stop`, the sentence |
  | a stop, nothing underway (the partner scored, void, planned never started, or absent) | a refusal: the series' launches end for good; a partner that never started is not launched, a void partner is not re-run |
  | the run-map's own `stopped` flag (`markStopped`) | a refusal for every slot and every pilot step: the backstop |

  Anything open (`launch`, `settle`, `score`) is finished before a void is re-run. So after a restart with the first stack
  scored and the second not started only the second is offered, and after a void only that stack's re-run.
- **`pilotStep`'s own table** (the pilot is the A/A and two pilot pairs, in that order, each finished before the next). A pilot
  pair follows the slot table above (`unitStep`): absent, `launch`, `settle`, `score`, a void re-run alone at attempt 2, a void at
  attempt 2 ends the pair, a stop or a half-planned pair is a refusal, open sessions first. **The A/A** is two twins of one task
  and one stack (twin `a` on mask A, `b` on B); a run is both twins at one attempt:

  | The twins | `pilotStep` |
  |---|---|
  | both absent | both twins, attempt 1, step `plan` |
  | one twin of a run, only `planned`, the other absent (a crash between the two plannings) | action `resume`: **first** the other twin `plan`, **then** that twin `launch`, at that run's attempt |
  | one twin of a run without the other and that twin got further than `planned` (any attempt) | a refusal (`the A/A's run n holds twins a, not a and b`): the twins are planned together |
  | a twin planned, launching, or ended with data and unscored | finish that twin only (`launch`, `settle`, `score`), before anything else, a void twin included |
  | both scored | the A/A is finished: the daemon pair is next |
  | one scored and the other void (attempt 1), or both void | **both twins** run again, attempt 2, step `plan` (action `again`), never the void one alone: one twin measures no spread |
  | the repeat run planned, launching or unscored | finish those twins only |
  | the repeat run scored | the A/A is finished, whatever attempt 1 was |
  | a void twin at attempt 2 | a refusal, `the A/A has a void twin after its one repeat`: the pilot cannot go on, an instrument fix and a new go (O2) come first; the A/A is never run a third time |
  | a stop in one twin | as for a slot: the other twin, if `launching` or ended with data, is finished (`settle`, `score`); a twin never started is not launched; else a refusal |
  | twins that are not exactly `a` and `b` in a run (one twin, or two `a`) | a refusal naming the run and the twins it holds |

  The A/A looks only at the sessions of its own task AND stack (kind `aa`). So the pilot is at most twelve sessions (A/A four, each pair four).
- **`slotProblem` has one road in, `nextSlot`** (Opus review M1). `slotProblem({ map, slots, slot, pilotGo, seedRevealed,
  readbackLines })` → a sentence or `null`, and it is `null` for EXACTLY the slot `nextSlot` offers, whatever the state: it is built
  from `nextSlot`, so a stopped or half-planned slot, a gate, a conflicting or missing read-back line are refused by both, and
  a refusal of `nextSlot` is `slotProblem`'s sentence for every number. Otherwise: a number that is no slot of 1 to 30 (a text,
  `0`, `31`, `1.5`) is `Stage 1 has slots 1 to 30`; a slot before the offered one is `a finished slot is never run again`; a slot
  after it is the round rule's sentence (`round r + 1 never starts while a pair of round r is unscored`) or, inside a round, the
  barrier's sentence naming the offered slot with its two stack states. It never throws and reads no file. 14a holds this for every
  state (a stop, one stack only, a half slot, the read-back gate, round 3, a finished series) and every n in 1 to 30.
- **The round rule is its own check** (Opus review M2). `roundRuleProblem({ map, slots, target })` finds a slot of any round BELOW
  the target's that is not finished, without the barrier's "earlier slot" test, and `nextSlot` asks it of the slot it offers (the
  barrier makes it unreachable, so a test hands `nextSlot` slots in a deliberately weakened order) and `slotProblem` of the slot it
  is asked about.
- **Inputs are never a throw.** `maskOfSlot(input)` returns `ok(mask)` or `fail(sentence)` naming the input and the legal values
  (`round 6: the rounds are 1 to 5`, `task 99: the tasks of Stage 1 are ...`, `stack rust: the stacks are csharp, node`);
  `nextSlot`, `slotProblem` and `pilotStep` answer `fail` for a missing or stopped run-map and for missing arguments, and take
  `readbackLines` of `null`, `undefined` or with entries that are no objects as no lines.
- **What is never re-run.** A session whose scoring is decided (`accepted`, `rejected`, `intermittently incorrect`) is a data
  attempt and is finished. Only a void is offered a re-run: an `unscored: environment` session (`VOID_UNSCORED`) and v1's void
  classes. A killed or crashed session is a data attempt (§9).
- **The gates on a data slot.** No data slot is offered before a dated `pilot | GO` line of the registered amendments exists
  and the seed is revealed (F3.S2 (a)). The harness reads the line; it never reads the file the GO table's tool writes
  (§3, condition 2). The module takes both facts as arguments.
- **The read-back pairs.** One slot of round 1 and one of round 2, one of a daemon task and one of a web task, named from the
  seed, so they are not picked after a result is seen.
- **Read-back lines are keyed by the attempt's rid** (plan round 2, findings 1, 4 and 6). Rids differ per attempt by
  construction (`...:a<attempt>`), so a line about attempt 1 is never a line about attempt 2. (1) **Reconciliation:** a
  dated `G-1 | refuted` line whose rid is a scored session of a read-back slot makes that attempt `void: harness` for the
  schedule, even when the run-map still says scored (the harness applies the same line when it rescores, PR 3); `nextSlot`
  then offers that stack's re-run, attempt 2, under its rid. A line whose rid is no session of a read-back slot, a line
  of another row (`G-1` only) and a verdict word other than `holds` and `refuted` reconcile nothing and satisfy nothing.
  (2) **Who counts:** only a scored, non-void attempt of a read-back slot counts and needs its own `holds` line; a void
  attempt needs none (its re-run is the next step and needs its own line), and the line of the original attempt, `holds` or
  `refuted`, neither releases nor fails the re-run. (3) **The first dated line of a rid is final** (plan round 2, second
  pass): a later line for the same rid that says the other word is refused, never applied: `nextSlot` and `slotProblem`
  fail with a sentence naming the rid and both lines' dates (`the first dated line is final`), so a refutation after a
  `holds` can never void an attempt once the gate opened and a `holds` after a `refuted` never cancels the re-run. A repeated
  identical line is harmless; a conflict about a session of no read-back slot is nothing to the gate. **A line counts only if it
  carries a real calendar date** (`YYYY-MM-DD` that exists: `2026-02-30` and `2026-99-99` are no dates; v1's `parseAmendments` gives every line a date): an undated line, or one with no real date, is
  ignored, it neither opens the gate, nor voids, nor conflicts. "First" is the earliest date, ties in the order of the file, and
  the conflict sentence names the lines in that order.
- **The read-back gate: nothing runs past an unread read-back slot** (plan round 1, finding 2). When a read-back slot is
  scored, no later slot is offered until each of its two sessions that counts has its dated read-back line on `main`
  (`holds` or `refuted`). A `refuted` line makes that session `void: harness`; its one re-run is then the schedule's next
  step, as for any void, and the re-run is read back too before any later slot. So a read-back can never void a session
  after later slots ran, the re-run stays next to its partner in time, and the schedule needs no way back to a finished
  slot. **Why this and not a later invalidation:** a read-back that voids a session rounds later would need `nextSlot` to
  reopen a finished slot, a re-run far from its partner, and a rule for round 3 while that re-run is unsettled; each is a
  new state in the part of the harness that spends money. The price is two waits of hours each, not minutes: the judge's
  reading, then a pull request of the lines to `main` with D68's whole runs both ways and CI's checks (§8), then
  `stage-sync`. The parent's rule, the lines before round 3, follows from this gate, and the module still states it as its
  own check.
- **The pilot's plan.** The A/A is two sessions of one task and one stack, one on each mask, kind `aa`, twins `a` and `b`.
  The two pilot pairs are kind `pilot`; in the daemon pair C# is on mask A, in the web pair on mask B. A pilot session that
  ends as a void is re-run once, alone; a void twin makes the A/A run again once, both twins, because one twin measures no
  spread. So the pilot is at most twelve sessions. The trees must cover the tasks 04, 05 and 10 (G-16); which of 05 and 10
  is the daemon pair's and which the A/A's, and the A/A's stack, are data the pre-registration names (open point 2, which
  stays open: PR 2 only validates, the registered values come from PR 6). **The argument of `pilotPlan` and `pilotStep`** is
  `{ aa: { task, stack }, daemon, web }`, every field required; `aa.task` and `daemon` are two distinct tasks of 05 and 10
  in either order, `web` is 04, `aa.stack` is one of the two stacks, and the three trees therefore cover 04, 05 and 10. Anything
  else is a refusal whose sentence starts with the field's path (`pilot.aa.stack: ...`).

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
    - 10a. `The_round_rule_is_its_own_check_and_a_weakened_barrier_cannot_offer_a_slot_of_the_next_round`
11. `A_void_is_re_run_once_alone_before_the_next_slot_and_a_second_void_ends_the_cell`
12. `A_session_with_a_functional_failure_in_a_completed_pass_is_never_offered_a_re_run` — `rejected` and
    `intermittently incorrect` entries; the terminal rule of D71 at the schedule's level.
13. `An_unscored_environment_session_is_a_void_and_is_offered_its_one_re_run`
    - 9a. `Omitting_the_kind_leaves_the_quick_runs_pairs_as_they_were_and_kind_data_is_seen_only_when_asked` — the `series.mjs` kind
      parameter and the two new exports.
    - 9b. `A_function_never_throws_on_bad_input_and_says_what_was_wrong_and_what_is_legal`
    - 9c. `The_hash_helpers_and_the_void_test_are_v1s_and_the_quick_runs_one_each`
    - 13b. `A_stop_in_one_stack_does_not_block_finishing_the_partners_open_session_and_never_launches_one`
    - 13a. `The_offer_names_the_stack_the_attempt_the_rid_and_the_step_still_to_run_and_never_the_slot_alone` — the table of
      the decision per session state: absent, planned, launching, killed with data, scored, void, re-run planned, one stack
      only; a restart after the first stack was scored and before the second started offers only the second, a restart after a
      void only that stack's re-run.
14. `No_data_slot_is_offered_before_the_pilot_is_GO_and_the_seed_is_revealed`
    - 14a. `slotProblem_refuses_what_nextSlot_would_not_offer_and_accepts_what_it_offers` — for twelve states (a stop, one stack
      only, a half slot, the read-back gate, round 3, a finished series among them) and every n in 1 to 30, `slotProblem(n)` is null
      exactly when n is the offered slot; one refusal per sentence above.
    - 14b. `The_series_stopped_flag_of_the_run_map_refuses_every_slot_as_a_backstop`
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
    - 15f. `A_scored_attempt_with_a_refuted_line_becomes_its_re_run_through_nextSlot_by_the_attempts_rid` — the same map
      with and without the line: the next slot, then only that stack's re-run, attempt 2, under its rid.
    - 15g. `A_line_of_the_original_attempt_never_releases_the_re_run_and_each_attempt_has_its_own_rid`
    - 15h. `A_void_attempt_needs_no_line_and_its_re_run_does`
    - 15j. `A_read_back_line_without_a_date_is_ignored_it_neither_opens_the_gate_nor_voids_nor_conflicts`
    - 15k. `The_conflict_sentence_names_the_earlier_dated_line_first_whatever_the_order_of_the_file`
    - 15i. `The_first_read_back_line_of_a_rid_is_final_and_a_later_line_that_conflicts_is_refused_never_applied` — `holds` then
      `refuted` after later slots ran, `refuted` then `holds`, an identical repeat, a conflict about a plain slot, another attempt's rid.
16. `The_read_back_pairs_are_named_by_the_seed_one_of_each_kind_in_rounds_1_and_2`
17. `The_pilot_plan_holds_the_A_A_twins_on_the_two_masks_and_one_pair_per_kind_and_no_data_rid`
    - 17a. `A_void_pilot_session_is_re_run_once_alone_and_a_void_twin_runs_the_A_A_again_once_with_both_twins`
    - 17c. `The_A_A_decision_table_per_state_of_the_twins` — a test row for each line of the A/A table above.
    - 17d. `The_pilot_pairs_follow_the_slot_decision_table`
    - 17f. `A_crash_between_planning_the_two_sessions_of_a_pilot_pair_or_the_A_A_offers_the_missing_one` and, for a slot,
      13c. `A_crash_between_planning_the_two_sessions_of_a_slot_offers_the_missing_stack_and_a_launched_lone_session_is_still_refused`
    - 15l. `A_read_back_date_must_be_a_real_calendar_date`; 17g. `pilotPlan_and_pilotStep_with_no_argument_fail_with_a_sentence_and_never_throw`
    - 17e. `The_A_A_looks_only_at_the_twins_of_its_own_task_and_stack` — and two twins that are both `a` are refused.
    - 17b. `The_pilot_plans_argument_is_validated_field_by_field_and_each_refusal_names_its_field` — the control, 05 and 10 in
      either order, and a refusal for each required field, each stack, each wrong task and the duplicate.

**Brief (Sonnet coder).** Branch `feat/lang-v2-f3s1-schedule`. One new file and its test; `lib/series.mjs` is widened as listed
below, and v1's `bare-plan.mjs` exports its two hash helpers (`hex6`, `bitOf`, one word each: reuse, not a second copy; the rid
and the first stack of a slot are computed with them). **That touches `spikes/minigit-bench/`: the pull request says in its
description that v1's `harnessIdentity` moved and that no v1 series is open, and runs the v1 suite whole (§8's row for PR 2).** Functions under
50 lines, complexity at most 4; the mask table is computed, never a literal list. The functions: `slotsOf({ seed })`,
`maskOfSlot({ round, task, stack })` → `ok(mask)` | `fail(sentence)`, `ridOfSlot(...)`, `firstStackOfSlot(...)`, `seedProblem(seed)`,
`nextSlot({ map, slots, pilotGo, seedRevealed, readbackLines })` → `ok(slot + { action, todo })` | `fail(sentence)` | `ok(null)` when the series is
done, `slotProblem(...)` (one road in with `nextSlot`), `roundRuleProblem(...)`, `readbackSlots({ seed })`, `pilotPlan({ pilot } = {})`, `pilotStep({ map, pilot } = {})` (a missing argument is `fail('pilot: required')`, never a TypeError).
No command changes in this pull request. **`series.mjs` changes, all of them** (widened, never copied): `sessionsOf(map, pair, stack, kind)`,
`openSessions(map, pair, kind)`, `stackState(map, pair, stack, kind)` and `pairComplete(map, pair, kind)` take an optional last
argument, the run-map `kind` of the sessions to look at: `pilot` (the default, the quick run's pairs: every existing caller is
unchanged) or `data` (a Stage 1 slot; the schedule passes `DATA_KIND`); any other value looks at sessions of that kind and finds
none of a slot. The pilot's `aa` and `pilot` sessions are told apart by `task`, not through these functions. Two exports are
new: `stateOfSessions(list)` (the state of a stack from its sessions, extracted from `stackState`) and `isOpenSession(s)`
(extracted from `openSessions`); `isVoid(s)` is exported and `stateLine(map, pair, kind)` made kind-aware and exported (the schedule's
sentences use them, so there is one void test and one state line). **Tests:** `Omitting_the_kind_leaves_the_quick_runs_pairs_as_they_were_and_kind_data_is_seen_only_when_asked`
(scenario 9a) and `A_data_session_is_seen_through_the_kind_...`, beside the untouched `test/barrier.test.mjs`.

**Note for PR 3:** a data rid (six hex digits of a sha256) and a `pilotRid` (the A/A's and the pilot's, drawn at random) may
collide, about one in 10^4 over the whole series; `planSession` refuses a rid that is already in the run-map, so a collision
fails closed (the launch is refused, nothing is planned twice), and PR 3's `stage-run` says so in its sentence.

**Tests are run through the wrapper** (the project rule, `common.local-cpu-budget`): `node .agents/conventions/tools/low-priority.mjs taskset -c 0-5 node --test spikes/lang-explore/test/stage-schedule.test.mjs spikes/lang-explore/test/code-shape.test.mjs spikes/lang-explore/test/barrier.test.mjs`, and the whole suites as `module_tests.md` says.

**Done when:** the seventeen scenarios, 9a to 9c, 10a, 13a to 13c, 14a and 14b, 15a to 15l and 17a to 17g went red, green, red; §8's row for PR 2.

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
2. **`stage-init` (new verb, FREE).** It makes the series. It refuses without the pre-registration tag on the remote, without
   a clean tree under `spikes/`, and while any held-out side is not the registered one. It calls
   `checkWorkspaces` (`lib/workspaces-check.mjs:111`) on a checkout of the task repository at W and records the commit, so what
   a session is launched with and restored from is what the task repository publishes (parent F2.S2). It pins the series'
   harness identity (PR 5's list) and refuses one that is not the manifest's, unless a dated instrument line accepted it. With
   `--rehearsal` it makes an `r-` series for the free stub round and needs no tag. **The tag check is PR 5's guard; in this
   pull request it is an argument whose default refuses every series that is not a rehearsal**, so `stage-init` cannot
   make a paid series before PR 5 merges.
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
   *(2026-11-20, epoch 11, change 10.2 as the baseline:)* `init` refuses only while an OPEN paid Stage 1 series exists
   under the explore root, naming `stage-init` and `stage-run`; a rehearsal, a finished and a stopped series do not
   count, and a series directory whose state cannot be read refuses, naming it. `run` and `prune` refuse a Stage 1 series
   by its `stage` field, and `stage-run` refuses any other series. Scenario 29's "init refuses a stage series" reads so.
   Built in #237 (coai plan round, PR 3, finding 5; *What the code settled* below, point (4)).
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
7. **The dated lines of Stage 1, and the owner's go.** `lib/stage-amendments.mjs` (new) reads the registered
   `amendments.md`. It parses four kinds of its own and hands every other line to v1's `parseAmendments`, which is not
   changed: `YYYY-MM-DD | owner-go | pilot | <the owner's words>`,
   `YYYY-MM-DD | owner-go | rounds:<a>-<b> | <the owner's words>`,
   `YYYY-MM-DD | caps | wallMs:<n> | stallMs:<n> | <reason, with G-18>` and
   `YYYY-MM-DD | pilot | GO | verdicts:<12 hex of the sha256 of the GO table's file> | <who>` (F3.S2 (a)). `stage-run`
   refuses a paid launch whose scope no `owner-go` line covers, before anything is planned, and writes the line it used
   into the pair record and as an event line of the ledger (an event, never a charge). A malformed line of these kinds is
   reported, never counted. An evidence path in any dated line is written with the home as `~`. The refusal for a missing
   go line is open point 19: the owner says before this pull request whether it stands in code.
8. **The record.** `lib/records.mjs` `exploreSection` gains `arm`, `model` (the id the alias record resolved for the main
   model), `task`, `round` and `kind`, each known or saying why; `REQUIRED_PATHS` asks for them in a Stage 1 record only, so
   the older records stay complete as they are (D34, D76, T-L11). The pair record gains the slot's task, round and masks.
   Every v1 metric stays.
9. **`stage-status`** prints the rounds, the slots with their state, and the spend on five lines: pilot, A/A, data, void and
   re-run. The re-run line is derived, not a new ledger kind: it is the charges of sessions with `attempt` 2; `data` and
   `void` are those of `attempt` 1 (open point 14: the split must be confirmed before it is registered).
10. **`prune` refuses a Stage 1 series** (the parent's *Growth*: nothing is pruned).
11. **What a session stores for the readers that come after the tag.** The tools of §3 can only read what the frozen
    harness wrote, so this pull request makes sure it is there, per session and per pass, in files of their own that no
    later step rewrites: the argv and the binds of the agent box; the cache snapshots' hashes and the package list; the
    environment keys of the agent box and, for each completed pass, of the scoring box with the two heap keys and their
    values; `LANG` and `LC_ALL` as both boxes had them (the parent's Definition of Done, `:471`; T-L32); the sha256 of the
    scored tree's `run.sh` and whether the restore had to put it back, so that "the frozen `run.sh`, which ends in `exec`,
    started the program" is a stored fact of every session (`:472`; T-L31); the affinity read-backs of the launch and of
    every pass; the sha256 of the workspace's `STATEMENT.md`. A field that cannot be taken is `{ known: false, why }`.
12. **Docs.** `research/module_lang_explore.md` gains a section on the Stage 1 series (the directory, the verbs, the kinds,
    the seam in v1, the dated lines, what a session stores); `research/module_minigit_bench.md` names the seam;
    `research/module_tests.md` lists the scenarios with the observed numbers.

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
    - 41a. `Every_field_the_read_back_the_GO_table_and_the_record_tools_read_is_in_the_sessions_stored_files` — a list of
      the fields in the test, held against a fixture session; removing one from what the harness writes fails it.
    - 41b. `A_session_stores_the_culture_of_both_boxes_and_the_hash_of_the_run_sh_that_started_its_program`
    - 41c. `stage_init_with_the_default_guard_makes_a_rehearsal_series_only`
    - 41d. `A_GO_line_that_names_another_verdicts_hash_than_the_one_it_is_checked_against_is_reported`

**Done when:** the scenarios went red, green, red; a free rehearsal series runs round 1's six slots end to end with the stub
CLI (recorded with its walls in `research/module_tests.md`); v1's suite is green and its baseline fixture unchanged; §8's row
for PR 3.

**What the code settled (PR 3 built, 2026-11-20; the brief above is unchanged).** (1) *The contracts PR 4 and PR 5 fill in.*
`stage-init` takes the pre-registration's answer from `io.stage.initGuardFn({ rehearsal })` → `ok({ preregTag, preregCommit,
seedSha256, manifestSha256, pilot, harnessIdentity?, policy? })`; `stage-run` takes its guard from `io.stage.guardFn({ paid,
requireSeed })` → `ok(null)` | `fail(reason)`. Both defaults refuse everything but a rehearsal series, so no paid series can be made
or run before they exist. The dated lines are read from `spikes/lang-explore/inputs/prereg/amendments.md` and the revealed seed from
`inputs/prereg/seed.txt` (it must hash to the series' `seedSha256`); both absent is no line and no seed. (2) *The A/A and the
pilot's sessions carry `pair: 0`* (v1's own number for a fixed list), and tell their units apart by `kind`, `task`, `stack`, `twin`;
they have no pair record (the schedule never read `pair` for them). A slot's `pair` is its number, as the schedule says. (3) *The
order of the checks of `stage-run`*: series, lock, dated lines and seed, unit, owner's go, guard, task pins, pinned price file and
policy, plan; each refusal leaves the run-map as it was. A rehearsal series needs no go line and no GO, has a seed of its own, and
its read-back lines say `holds`. (4) *"`init` refuses a Stage 1 series"* is read as: `init` refuses only while an open paid Stage 1 series exists
under the explore root (not a rehearsal, not finished, not stopped), naming `stage-init` and `stage-run`; `run` and `prune` refuse a Stage 1 series by its `stage` field (coai plan round, PR 3, finding 5).
(5) *What a session stores*: the agent box's culture is read from `plan.json` (the locale `agentEnv` is built from) with both keys
confirmed in `argv.json`'s environment keys, and the scoring box's environment per pass comes from the layer's own `boxEnv` with
the same inputs; both say their source. `STORED_FIELDS` (12 fields) is the list the later tools read. (6) *`stage-run` on a data
rid that collides with a pilot rid* is refused for good (the data rid comes from the seed); a pilot rid collision is retried by
running the command again. (7) *The code rounds of PR 3*: the facts of a session are written only once it has ended, not
while it is `launching` (coai code round 1, PR 3); the owner-go event is written only when the unit goes ahead past every refusal check (the `onReady`
seam of the shared body; coai code round 1, PR 3); `init` fails closed on a series directory whose state cannot be read, naming it (coai code round 1,
PR 3); the read-back stop and the schedule's gate name the full command `node research/lang-v2/tools/readback.mjs --series <dir> --pair <k>` (coai code
round 1, PR 3); a failed schedule after a slot ends `--until round` with its reason and exit, not "round finished" (coai code round 1, PR 3);
the amendments test uses the owned `tmpDir` and `spendLines` builds its totals without mutation (coai code round 1, PR 3); a session with `data === false`
is a void for the schedule whatever slot a dated `refuted` line names, so its re-run is the next step (Opus review, PR 3); `stage-run` finishes the records an
ended unit still owes before it offers a unit (Opus review, PR 3); `--until round` does not stop a rehearsal at a read-back slot (Opus review, PR 3);
`stage-status` puts the data charges of a session the run-map holds as a void on the `void` line (Opus review, PR 3); a package list never recorded and an
affinity read-back that never ran are stored as unknown (Opus review, PR 3); `stage-run` and `stage-status` print every malformed Stage 1 line and every
home path as a report line, never a refusal (Opus review, PR 3); a missing pinned price or policy file refuses `stage-init` and `stage-run` with its path
instead of throwing (Opus review, PR 3).
(8) *(coai code round 2, PR 3)*: a unit owes its records when ANY of its ended sessions lacks its `stage-facts.json` OR its `record.json` lacks a Stage 1
field of its `explore` section (`STAGE_PATHS`, found with `records.mjs` `lacksPaths`, the check `missingPaths(record, scored, true)` uses), or a data slot lacks
its pair record, because the finalizer writes the facts BEFORE the explore section; the finalizer is safe to run twice (the facts file is published once, the
rest is rebuilt to the same bytes), and `next` finishes the records before it offers a unit; `stage-status` reads the run-map through `stage-step.mjs`
`mapAsRead`, the function `stage-run` reads it through, so a dated `refuted` line shows the stack as void with its re-run owed and its charge on the `void`
line; a moved harness refuses a Stage 1 unit (exit 3) without a `harness-moved` ledger event, so every refusal of Stage 1 leaves the run-map and the ledger as
they were, and the one-task `run` keeps its event; `slicesOf` builds its slices without mutation.
(9) *(coai code round 3, PR 3)*: the pair record `pairs/p<N>.json` is written whole or not at all (`run.mjs` `pairRecordFile` through `replaceJson`, a temporary file
beside it and a rename; the file is rewritten on purpose, so `publishOnce` does not fit), and a data slot whose pair record cannot be read or parsed owes its records
(`stage-owed.mjs` `lacksPair`), so `next` rewrites it before it offers a unit; the owner-go event is idempotent for one launch of a unit (`stage-step.mjs` `sayGo`
looks in the ledger for an event of the same pair, `what` and `rids` and appends none when it is there), so a process that died between the event and the run-map
leaves one event for the unit after its retry; the event gains a `rids` field, and a halted slot that is resumed (the same rids) adds no second event.

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
     policy's sha256, the caps, the locale and the base environment's allow-list, the series defaults T and G with every
     task's override (D14), the private trap aid's sha256 (D41; a hash only, `:1579`), the registered rule files of the tools,
     and the certifying host (host name, kernel, cgroup version). The manifest is PR 5's file; this module owns the list. For the harness identity alone a difference
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
   box. The rule takes both, which is the parent's own reading (`:898`, `:904`–`:905`; §11, closed point 13):
   `growth.json`'s figures plus a session folder's size. **That size is computed live and written nowhere** (the Opus
   review, M7): `growth.json` is a registered file and is not amended. At every check the module takes the larger of the
   floor the manifest registered (the largest kept session folder of the quick run's series, measured when the files were
   generated) and the largest ended session folder of this series as it is on disk, a freed session counted at the size its
   `freed.json` lists. So the pilot's real sizes enter the rule the moment they exist, with no file to rewrite.
5. **`lib/stage-archive.mjs`: the recovery path keeps the data, and says exactly what may go** (plan round 1, finding 3).
   Three FREE verbs; none launches or scores, and each takes the series lock.
   - **`stage-archive --series <dir> --to <backup dir>`** copies the whole series folder to the private backup, writes the
     sha256 list of the source (one line per file, `sha256  path`, links recorded and never followed, the walker of
     `lib/workspaces-tree.mjs` widened to stream a large tree), verifies the copy against that list file by file, and appends
     the archive's identity to `<series>/archive.json`: the list's sha256, the bytes, the place, the date and the slots
     finished at that time. It deletes nothing.
   - **`stage-free --series <dir>`** is the only thing that frees local space, and it frees one closed list:
     `FREEABLE = ['box/nuget', 'box/npm', 'box/home/.dotnet']`, the cache copies inside a session's box, and only of sessions
     that are final (scored, or a settled void) in a finished slot. A read-back slot is no finished slot for this verb
     until its dated lines exist: the judge's reading comes first. It first verifies again that the newest archive is at its
     place and that every file it is about to remove is in that archive's list with the same sha256; a file that is not is
     kept and named. In each session it leaves `freed.json` with the archive's identity and the removed folders' own list.
     **Never freed:** the workspace tree a session left (`work/`), `score/` with its build record, attempts, events and
     score, `record.json`, `meta.json`, `plan.json`, `argv.json`, the streams, the transcripts, the OTel and broker and
     egress logs, the read-back files, and everything at the series' root (the run-map, the ledger, `pairs/`, `private/`,
     the adjudication folders, `derived/`, `archive.json`). An unfinished slot's sessions are never touched.
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
    - 54g. `stage_free_leaves_a_read_back_slot_alone_until_its_dated_lines_exist`
    - 54h. `The_session_size_the_disk_rule_reads_is_the_larger_of_the_registered_floor_and_this_series_own_largest_and_no_file_is_written`
55. `The_clear_folder_of_a_slots_task_is_made_for_the_slot_and_removed_after_it_whether_the_slot_ends_or_throws`
56. `A_run_after_an_interruption_removes_the_stale_clear_folder_first`
57. `A_slot_is_refused_before_its_launch_when_the_keys_backup_cannot_be_verified`
58. `Only_the_slots_own_task_is_ever_in_clear`
59. (real) `The_certification_probe_passes_on_this_host_and_reads_the_peak_of_an_empty_program`

**Done when:** the scenarios went red, green, red; the real probe passes on the series host; §8's row for PR 4.

**What the code settled (PR 4 built, 2026-11-20; the brief above is unchanged).** (1) *Where each check sits in `stage-run`.* PR 3's order stands (series, lock, dated lines and seed, unit, owner's go, guard, task pins, pinned price file and policy) and PR 4's three checks follow it, before the plan: the agent box's environment (`envStep`), memory certification (`certifyStep`), the disk gate (`diskStep`), then the plan; the clear held-out folder wraps the run of a PAID unit (`underHeldout`, around the prerequisites, the loop and the scoring, so a unit the folder refuses is refused before the plan reaches the run-map). All of it is `lib/stage-launch.mjs` and one list in `stage-step.mjs`; v1 and the one-task `run` are unchanged. A failed step carries its own `exit` (3 for a halt the same command resumes, 4 for a refusal); every refusal leaves the run-map and the ledger byte for byte as they were, and the disk halt alone adds its own event line (`disk-reserve` / `disk-rule`, an event, never a charge). (2) *The default `io.stage.guardFn` of a paid series is `stageGuard`* over this checkout and the task repository the series recorded at `stage-init` (`inputs.json` `taskRepo.root`; `LANG_V2_TASK_REPO` is not consulted when the series names one, so the guard judges the checkout `stage-init` proved); `io.stage.guardArgs` replaces `repo`, `taskRepo` and `deps`. With no `deps.preregGuard` a paid series is refused, now with every problem of the guard named as well; a rehearsal's guard passes by itself and reads no record. `stage-init`'s default `initGuardFn` is unchanged (PR 5 supplies it). (3) *`stage-init` calls the one check `stageGuard`'s init half uses* (`checkedWorkspaces`, which `workspacesProblems` wraps), with the record's W for a paid series (`io.stage.recordFn` replaces the record read): a checkout at another commit than W, a changed workspace byte, and a record with no W each refuse and write no series; a rehearsal reads no record and needs no W. (4) *Memory certification runs once per launch, for every kind.* A rehearsal runs the probe too (kind `rehearsal`): a host that launches the empty program but cannot read the peak goes on with the flag `memory bound not certified` (said, and in the result's `flags`); a layer that cannot launch the empty program halts `layer-launch-failed` even for a rehearsal (the rehearsal needs the layer as well). The default probe is `hostProbe` (the layer over an empty tree, the pinned toolchain folders and the registered memory value, in owned temporary folders). (5) *The disk gate is for paid launches only* (a rehearsal is free and holds no reserve). Its sizes: `growth.json` (registered, read as it is), the registered floor of a session folder, free bytes of the series folder's volume. **The floor's source is `sessionFloorBytes` of `inputs/prereg/manifest.json` (or `io.stage.floorBytes`); no manifest exists before PR 6, so PR 5's tool and PR 6's file must carry that field** (an absent or zero size is a refusal, never a gate that passes). `extraCells` is `unit.extraCells ?? 0`: no unit of PR 3 is a pilot repeat pair, so PR 7 sets it when it adds one. (6) *The agent box's environment is checked for every unit*, a rehearsal's included, from the pins (`agentEnv` over the pins' `envExtra` and culture, as a session's box is built); a clash, an absent base key or a planted heap key is exit 3. (7) *A unit that ran and could not close its clear folder keeps its own result* (`withSlotHeldout` now returns `result` with that failure) and the series halts: exit 3 when the unit ended well, its own exit otherwise, `heldout-close` named; the next unit would be refused by the stale-folder rule anyway. (8) *The three verbs* (`stage-archive`, `stage-free`, `stage-restore`; `--to` is a new option) check that `--series` is a Stage 1 series under the explore root, take the series lock through the modules, and are not re-executed under the scoring CPUs. `stage-archive` is the LAST archive (it refuses a freed session, naming `stage-restore`) when the series has ended (stopped or finished); `stage-free` hands `unreadReadbackSlots` of the dated lines to `freeSeries`, so a read-back slot waits for its lines. (9) *Choices the brief did not fix, taken as the safer refusal.* The identity list has 41 names; the composite identities (`heldout.NN`, `memoryValue`, `policy`, `locale`, `seriesDefaults`, `toolchains`) are the sha256 of a stated text, so a difference shows as two short hashes whatever it was in; the `caps` identity is `capUsd` only (the wall and the stall are amendable by a dated `caps` line); the `host` identity is a hash and is never printed; the disk bound of an unfinished cell is two sessions less its voids, a pair's worst case is `growth.json`'s figure plus two session folders; `stage-free` covers data slots only, keeps and names a file whose archived copy has another hash, and refuses only when the archive entry, its list or its copy folder is missing or the list's hash is not the recorded one; a manifest that holds an identity the guard never reads is named, and so is an identity the manifest lacks. (10) *What PR 5 must plug in, and PR 13 and PR 7 must call.* PR 5: `deps.preregGuard({ repo, series, requireSeed })` -> `[sentence]` (the tag, the registered blobs, the clean tree, the harness identity, the seed for a data slot), `deps.readers.harness` (the `harness` identity, from its `STAGE_HARNESS_DIRS`; with none the identity is "cannot be read" and the guard names it), `deps.harnessAccepted` (the 12 hex of the accepted `instrument | harness:` lines while no data session has left `planned`), `POLICY_FREE_FIELDS` (`t3`, `t5`: PR 5's list of the policy's free fields must equal it), the manifest's `identities` (the 41 names) and `sessionFloorBytes`, and `io.stage.initGuardFn`. PR 13 (the record's final sha256 list) calls `freedSessionProblem(series, rid)`; PR 7 sets `unit.extraCells` for a pilot repeat. The guard's own `deps` (`manifest`, `live`, `readers`, `env`, `home`, `pinnedTaskSha`) are the seams of the tests. (11) *(coai code round 1, PR 4)* The archive's place is checked on real paths: the copy's folder (`<to>/<series name>`) may not be the series, inside it or around it, so `--to` as the series' parent, a link to it, a folder in the series and a link into it are refused (finding A). `stage-free` does not free a session that already holds a `freed.json` and names it in `alreadyFreed`, so one `freed.json` names the one archive that holds its files (B). `freeSeries` is refused without its unread slots, now through a `reading` that runs under the series lock (C1, then the Opus review's O3). `stage-archive` and `stage-restore` say their progress per phase through an optional callback wired to `io.say` (C2). One `diskGate` call sizes each ended session once (C3). `DATA_SLOTS` is exported once from `stage-schedule.mjs` and `FREED_FILE` is imported (C4); `walkEntries` is a recursive generator and `readRecord` builds new values (C5). *(Opus review, PR 4)* A paid unit's prerequisites are handed the clear folder their slot opened, so the reader does not ask for the task lock the slot holds, while any other reader or process is still refused (O1). A cache root that is a link or lies under one is kept and named, never chmodded, walked or removed (O2). What may be freed is judged under the series lock from the run-map as `stage-run` reads it, so a read-back slot with a refuted session keeps its caches (O3). An identity whose value is missing is unreadable, never the text "undefined" (O4). The D15 environment is built through `quickExplore`, as the agent box is (O5). A size that cannot be read is a refusal, exit 4 with no ledger line; only a real shortage halts with exit 3 and its event (O6), and a `freed.json` with a missing or damaged size is such a refusal (O7). A clear folder still open after the slot is said on every way out, a throw included: the error is rethrown with the sentence joined and the original as `cause` (O8). A missing `hashes-lang.txt` is one named problem of the guard, not a throw (O9). (12) *(coai code round 2, PR 4)* A copy is never written through a link: the archive checks every folder from its copy root to the listed files and refuses the whole archive, naming the path, when one is a link (a backup that already exists cannot send a file outside itself); a link at a target or at its temporary name is replaced, never followed (R1). The last archive's freed-session look is taken under the series lock (R2). `restoreAll` is recursive, without mutation (R3). An `archive.json` with no `archives` list is refused with a sentence, never a TypeError (R4). `stage-free` says its progress per session while it verifies and while it removes (R5, R6), and the archive's listing says `listing: started` before its walk and counts entries while it walks (R7). (13) *(coai code round 3, PR 4)* The link class is closed by one helper, `lib/safe-place.mjs` (an `lstat` of every folder on the way from a root; an exclusive temporary file and a rename): the archive copy, the hash list in the backup, `archive.json`, `freed.json` and the restore use it, so a link planted at a parent, at a predictable name or at a temporary name is refused or replaced and never followed; the audit of every write site is a table in `research/module_tests.md`. The disk gate's halt line is not appended through a link at the ledger. The identity readers give `ok` / `fail` as values and `liveIdentities` neither throws nor catches. The disk check says how many ended sessions it sizes and a line every N sessions, through `io.say`.

### (b) one pre-registration — PR 5 (the tools) and PR 6 (the files)

**What is registered** (parent F3.S1 (b), §9, D26, E4.S1 to S3). New folder `spikes/lang-explore/inputs/prereg/`:

| File | Content | Written by |
|---|---|---|
| `statement.md` | what is run: the goal narrowed, the six tasks, known (05, 06) and original (04, 07, 08, 10), the arm, the model alias and effort, the tool list, the certifying host, D73's claim and its limit, T28's sentence, the date it was written | its own prose, with a generated identity table |
| `analysis-contract.md` | above a marker, quotes of the parent read at a commit of `origin/main`: *The goal, narrowed*, D24, D28, D29, D71, D72, D73, §9's primary metric and its table of undefined cases, the outcome classes; *The claim and its stated limit* and *Outside the claim* (`:438`–`:446`), which are Stage 1's reading of what the analysis licenses (§9's older paragraph, `:1658`–`:1663`, speaks of ten tasks and three pairs and is not quoted; the operative part holds, `:367`–`:369`); and the report sentences of the **threat** rows T3, T5, T6 and T9, T23 and T25 to T28 of *Threats this change touches* (`:837`–`:849`). Those are threats to validity, not the cost cross-checks T1 to T5 of §8, which the contract names "the cost checks" wherever it means them. Below the marker, the pre-registration's own operationalisations (the list after this table) | `prereg/extract-contract.mjs`; the part below the marker is prose |
| `prediction.md` | the prediction for six tasks, bare, and when it was written (T28) | its source is open point 7 |
| `prompts-lang.json` | the bare template and its sha256; per task and stack the prompt text, the stack sentence, the framework sentence of a web task, and the sha256 of the workspace's `STATEMENT.md` as published at W | `prereg/write-prompts.mjs` |
| `schedule.json` | the tasks, the rounds, the mask of all 30 (round, task) cells, the two scoring profiles, the pass and attempt counts, the caps, the pilot's tasks, the A/A's task and stack, the read-back rule, the re-run rule | `prereg/write-schedule.mjs` (it refuses without the answers to open point 2) |
| `manifest.json` | every identity of PR 4's list, the Stage 1 harness identity among them, read when PRs 2 to 5 are all on `main`; the registered floor of a session folder's size; the disk projection for the 66 sessions, and beside it what one re-run and one repeat pair of the pilot add | `prereg/write-manifest.mjs` from `liveIdentities()`; it runs last but for the seed, after `pin-cli.mjs` |
| `prices-<yyyy-mm-dd>.json` | the published prices read that day, in v1's shape | `prereg/read-prices.mjs` (the one step that uses the network) |
| `crosscheck-policy.json` | T1 to T5's policy, starting from v1's registered values | copied once, then its own file |
| `pilot-rules.json` | the pilot's GO table and the read-back, as data (§3, condition 1): per row its id, the sessions it is asked of, the stored file and field it reads, computed or by hand, its threshold, what missing evidence gives, whether `not exercised` may pass and with which replacing evidence, whether a refutation stops; GO's definition; the read-back's fields and what each is compared with | the pre-registration's writer, as data; a test holds its row ids equal to the contract's table |
| `adjudication-rules.json` | the blind adjudication, as data (§3, condition 1): which sessions and cases get a packet and which pass's evidence is shown; the ordered masks (v1's list by the blob id of its file at the tag, then the added rules); the verdict words of both steps and when they freeze; the exclusion of an ambiguous case and the rule of the revised verdict; what a `harness` class does | the pre-registration's writer, as data |
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
- the overall reading, **for every measure and not for CPC alone** (§9, `:1646`; the measures are CPC, `CPC_all`, the
  success rate, tokens, wall, `model_api` and `active − model_api`): the median of the per-task ratios over the defined
  ones, the geometric mean, and the count of tasks where C# is higher, each with its denominator ("median over k of 6
  tasks; m undefined") and the line of undefined outcomes;
- **per kind, the same figures with their own denominators** (§9, `:1650`–`:1651`; D34, metrics are only added): for the
  three daemon tasks and for the three web tasks, "median over k of 3", the geometric mean and "C# higher in j of k";
  known (05, 06) against original (04, 07, 08, 10) is shown apart as a listing and not as a summary (D29, `:442`);
- the outcome classes: `rejected` and `intermittently incorrect` are data attempts that add their cost to CPC's numerator and
  nothing to its denominator; `unscored: environment` is a void, its cost on the void line, in neither part of the primary
  CPC, re-run once; a killed or crashed session is a data attempt, censored at the cap where the cap ended it;
- the two scoring profiles and the two session masks, the mask rule, that no scoring runs while a session runs, and that
  every mask is read back;
- the void grounds (v1's void classes and `unscored: environment`) and the re-run rule (once, alone, before the next slot);
  the stop grounds (v1's stop classes, a T1 breach, a second contaminated void, alias drift);
- what may be amended after the A/A, by a dated line, for both stacks alike: the instrument (the harness identity, the quiet
  thresholds, the package flags), the policy of the cost checks T3 and T5 (`:1562`, `:1611`; §11, closed point 9), and once
  the wall and the stall; never the cap, a prompt, a spec, a
  limit or the treatment of one stack; nothing of the instrument after the first data session;
- the spend lines: pilot, A/A, data, void and re-run, with the split of PR 3's item 9;
- **everything the three tools implement** (§3, condition 1), in words here and as data in the two rule files, so that a
  tool written after the tag has nothing to decide:
  - the pilot's GO table, row by row: v1's G-1 to G-14 with its definition of GO, and G-16, G-17 and G-18 as the parent's
    §11 reads them for Stage 1; for each row the sessions it is asked of (the twins, or all six), where its evidence is
    stored, its threshold, and what missing evidence means; G-12 at the registered $50; G-14's reading (open point 4);
    G-16 without its RSS clause, which lapsed with D74 (§11, closed point 3); G-18's figure (open point 5); the stops
    (G-2, G-8 and G-12 refuted, alias drift);
  - the read-back: the pairs' rule, the fields read and what each is compared with, that a difference is `refuted`, and
    that no later slot runs before a read-back slot's lines are written;
  - the packets: the sessions (the last data attempt of each cell of the 30 slots, never a pilot session), the cases
    (every visible or held-out case that failed in at least one completed pass; an environment attempt is no pass), the
    evidence shown (the first failing completed pass by its number), the pattern shown (failing passes per profile), that
    a failed build gets no packet;
  - the masks, as an ordered list, and what a memory cause shows before and after the reveal;
  - the two steps' words (`spec-ambiguity` or `not-ambiguous`; `product` or `harness`), who answers, and that step 1 is
    frozen by the reveal;
  - the exclusion: a case judged ambiguous in any session is dropped from every session of its task; the revised verdict
    is the verdict rule over the remaining cases; a failed build keeps its primary verdict; the revised reading stands
    beside the primary and never replaces it;
  - what a `harness` class does. This one is not answered yet (open point 15), and the tag waits for it;
- the sentence of §3's condition 5: the adjudication's code is written after the data collection ends, and its complete
  rules are these;
- the disk reserve, recomputed before every launch, and the closed list of what a recovery may free;
- the memory causes counted per task and stack (kernel kill, each runtime's abort) and the flags `abort-below-evidence` and
  `child-abort-line`, with the 50 % evidence threshold (D56);
- `deviations` (frozen files the restore had to put back, count and paths) beside `accepted`, never deciding it;
- every line shown with and without pairs that hold a `ran unpaired` session or a `foreign load` flag;
- whether a sign test over the six task medians is shown as a secondary line: **not shown** (the parent's list of what the
  report carries has none, `:461`–`:464`, and §9 calls no inferential figure a finding, `:1661`; open point 8 stays only
  for someone who wants the line added before the tag).

**Brief for PR 5 (Sonnet coder).** Branch `feat/lang-v2-f3s1-prereg-tools`.

1. **v1's guard takes its lists as arguments** (`spikes/minigit-bench/lib/prereg-guard.mjs`). `preregGuard` and
   `harnessIdentity` gain a configuration: the registered files as paths from the repository's root, the amendable inputs, the
   policy file and its free fields, the seed files, the harness folders and the consistency check. v1's own call passes v1's
   lists and answers exactly as before (its tests stay green, unchanged). This is the widening §6.2's row 9 asks for; no second
   guard is written.
2. **`lib/stage-prereg.mjs` (new): Stage 1's lists.** `STAGE_PREREG_FILES` (the table and the paths above),
   `STAGE_HARNESS_DIRS` (v1's six folders and `spikes/lang-explore/lib`, `spikes/lang-explore/prereg`,
   `spikes/lang-explore/rehearsal`, `spikes/lang-bench/lib`, `spikes/lang-bench/runner`, `spikes/lang-bench/templates`, with
   the top-level `*.mjs` of the three spikes), the policy's free fields (`t3`, `t5`: the parent's, `:1562`, `:1611`, `:1510`; v1's own list frees `t2` and `t3` and is
   v1's; §11, closed point 9), and the two structured
   exceptions: `explore.json` may differ from the tag only in `caps.wallMs` and `caps.stallMs`, once, when a dated `caps` line
   names the new values; `quiet.json` and `package-flags.json` as v1 allows them. A harness identity other than the series' is
   accepted only by a dated `instrument | harness:<12 hex>` line and never once a data session has left `planned`.
   **The series' branch** (§3; the Opus review, M1): `AMENDABLE_PATHS` is the closed list of §3. The guard adds two checks
   for a paid launch: `git diff --name-only <the tag's commit> HEAD` lists only paths of that list, and each listed file's
   blob at HEAD is `origin/main`'s blob after a fetch. `explore.mjs stage-sync --series <dir>` (FREE) is the one way the
   branch moves: it fetches, takes those files from `origin/main`, commits them on the series' local branch and prints what
   changed; it takes nothing else and never pushes. A dated instrument line that accepts another harness identity names the
   commit of `main` it is; `stage-sync --accept-harness <12 hex>` then takes the harness folders of exactly that commit,
   and is refused once a data session has left `planned`.
   *(2026-11-20, epoch 11, the re-review's decisions on PR 5's three candidates:)*
   - **(b) reverted to this brief.** A harness acceptance needs the dated line
     `YYYY-MM-DD | instrument | harness:<12 hex> | <reason>`. Its `<12 hex>` is the accepted HARNESS IDENTITY, the 12 hex
     of the hash over the live tree ids of the harness folders, and never a commit sha; the line's reason begins with the token `commit:<40 hex>`, the commit of `main` that holds the fix, and `stage-sync` reads that token only (a line without it, or with a second one, is refused) (the re-review gate,
     2026-11-20). `stage-sync --accept-harness <12 hex>` repeats that identity: it
     takes the harness folders of the commit that the line names, refuses a commit that is not reachable from `origin/main` after a fetch, checks that their identity is the 12 hex, refuses when
     it is not, and is refused once a data session has left `planned`. The acceptance is frozen into the run-map at the first data plan. A public hash identifies bytes but does
     not supply the reviewed source, so the fix must be on `main` first, and the series takes it from there. PR 5, which
     removed the flag (an operator's commit on the series' branch under the dated line), restores it with the freeze
     kept (`harnessAtFirstData`); scenario 79e stands as written.
   - **(c) adopted.** `STAGE_HARNESS_DIRS` also holds `spikes/lang-explore/runner`, `spikes/lang-explore/templates` and
     `spikes/lang-bench/tools`, and the identity holds the `.agents/conventions` submodule's commit, which counts as its
     gitlink. A paid launch is refused when the submodule is off its recorded commit or has local changes. Code the
     harness imports or runs belongs inside the frozen instrument (§3: the instrument the pilot and the rounds use is the
     one the tag names): `lib/workspace-quick.mjs` imports `spikes/lang-bench/tools`, and `lib/pass-seams.mjs` and
     `lib/score-quick.mjs` import the submodule's `owned.mjs`. Scenario 76 reads "a byte of lang-explore, lang-bench or v1,
     or the submodule's commit".
   - **(d) adopted.** The policy's free fields are `t3`, `t5` and their record fields `decidedOn` and `reason`; every other
     field stays frozen. A policy change needs its dated line `input:crosscheck-policy.json:<12 hex>`. The record fields
     describe a permitted T3 or T5 decision, and freezing them would leave misleading metadata. Scenario 71 reads these
     four fields.
3. **The generators**, in a new folder `spikes/lang-explore/prereg/`, each taking `--parent-commit <sha on origin/main>` where it
   quotes, and reading the parent through v1's `readAtCommit` (`spikes/minigit-bench/prereg/parent.mjs:27`, which takes the
   file): `extract-contract.mjs`, `write-prompts.mjs`, `write-schedule.mjs`, `write-manifest.mjs`, `read-prices.mjs`. Each
   writes its file whole and atomically, and `--check` exits 1 when the committed bytes are not what it would write.
4. **The seed.** `explore.mjs seed-commit` and `seed-reveal` over v1's `commitSeed` and `revealSeed`
   (`spikes/minigit-bench/lib/seed.mjs:29`, `:54`), which gain the prefix (`lv2-`), the list of files that must be in place
   first, and a check of the drawn seed (`seedProblem` of PR 2). The seed lives in `~/rsd/lang-v2/seeds/stage1.seed`, mode 600,
   outside git; only its hash is in the tree until the reveal. **`seed-commit` asks PR 2's `seedProblem` of each drawn seed and
   draws another when it refuses, before it writes the seed file or the hash** (a prerequisite carried from PR 2, where the
   function is pure and no command calls it); no colliding seed is ever committed.
5. **The CLI pin.** `prereg/pin-cli.mjs` copies the then-current CLI release into `~/rsd/lang-v2/toolchains/claude-<version>`
   (mode 0555) with v1's copy step (`bare-init.mjs`, extracted if it is private there), and writes the version, the path and
   the sha256 into `inputs/explore.json` `cli` (D8's re-pin, taken once: D47). The olympiad series are closed, so nothing else
   reads that entry for a paid run.
6. **The engine reads the registered prompt.** For a Stage 1 series the prompt of a task and stack is the registered text of
   `prompts-lang.json`, checked equal to what `quickPrompts` computes; a difference refuses `stage-init`. The template's
   final wording is F3.S1 (b)'s own to fix (`:1299`, `:886`); open point 10 is the one sentence of it that is not settled.
7. **Docs:** the module doc's section on the pre-registration (the files, the generators, the guard, what may be amended).

**Scenarios** (`test/stage-prereg.test.mjs`, `test/stage-prereg-guard.test.mjs` with temporary repositories in the shape of
v1's `test/prereg-fixtures.mjs`; v1's `prereg-guard.test.mjs` and `seed.test.mjs` gain the configuration cases):

60. `Every_quoted_block_of_the_contract_is_the_parents_text_at_a_commit_of_origin_main_byte_for_byte`
61. `A_quote_whose_block_is_missing_at_that_commit_fails_the_extraction_and_names_it`
62. `The_manifest_the_prompts_and_the_schedule_are_written_again_to_the_same_bytes_and_a_typed_value_fails_the_check`
63. `The_registered_prompt_of_a_task_and_stack_is_the_text_the_engine_sends`
64. `The_registered_statement_hashes_are_the_published_workspaces`
65. `The_seed_is_in_the_tree_as_a_hash_only_until_it_is_revealed_and_a_seed_at_the_tag_is_refused`
    - 65a. `The_real_draw_path_refuses_a_colliding_seed_and_draws_another_before_anything_is_committed` — the draw is given a
      random source whose first seed collides (PR 2's fixture seed): the file and the hash written are the second seed's, and
      nothing of the first is ever written.
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
    - 79a. `A_series_branch_that_differs_from_the_tag_in_a_harness_file_or_any_path_off_the_list_refuses_the_launch`
    - 79b. `A_registered_line_on_the_series_branch_that_is_not_origin_mains_blob_refuses_the_launch`
    - 79c. `A_harness_change_merged_to_main_after_the_tag_neither_reaches_the_series_nor_stops_it` — a temporary repository
      where `main` gains a commit under a harness folder and a dated line: after `stage-sync` the branch holds the line and
      not the harness change, and the guard passes.
    - 79d. `stage_sync_takes_the_listed_files_from_origin_main_and_nothing_else_and_never_pushes`
    - 79e. `An_accepted_harness_takes_the_folders_of_its_named_commit_before_the_first_data_session_and_is_refused_after`
    - 79f. `The_two_rule_files_hold_every_row_and_rule_the_contract_names_and_no_other` — the ids of the GO table and the
      rules of the adjudication in the contract's own part against `pilot-rules.json` and `adjudication-rules.json`.

**PR 6, the files** (branch `docs/lang-v2-prereg`; the team lead with an Opus writer for the prose). It is written only when
PRs 2 to 5 are all on `main`, because the manifest reads the harness identity (plan round 1, finding 1). The generators run
at a named commit of `origin/main` that holds them; `statement.md`, the contract's own part, `prediction.md` and the two
rule files are written; `seed-commit` runs last. Before it merges: every `--check` exits 0; the document gate
(`mcp__coai__review_document`) reads `statement.md`, `analysis-contract.md`, `prediction.md` and the two rule files, with
the question of §3's condition 1 put to it in words (is any choice of a tool left open?), and every finding is resolved;
D68's whole runs of lang-explore, both ways, at its head, because every file it adds or changes is an input of that suite
(`explore.json`'s `cli` entry among them); `leakcheck` and the line scan are clean. **PR 6 is
the last merge before the tag**: the tag goes on its merge commit. §11 marks the open points that must be answered before
these files are written.

**Done when (PR 5 and PR 6):** the scenarios went red, green, red; the files are on `main`, each generated one equal to its
generator's output, the manifest's harness identity equal to the live one on PR 6's merge commit; the document gate at `proceed`; §8's rows.

**What the code settled (PR 5 built, 2026-11-20; the brief above is unchanged).** (1) *What is wired where.* PR 5 plugs into PR 3's and PR 4's seams with ONE new module, `lib/stage-prereg-guard.mjs`: the default `deps.preregGuard` of `stageGuard` (`stageGuardOf`, `lib/stage-gates.mjs`) is `stagePreregGuard`, which runs v1's `preregGuard` over `STAGE_PREREG_CONFIG` (the tag on the remote, the registered blobs, the clean tree under `spikes/`, the harness identity, the series' own branch and, for a data slot, the seed); the default `deps.readers.harness` is `stageHarnessReader` (the tree ids of `STAGE_HARNESS_DIRS` and the top-level `*.mjs` of the three spikes, from the one `STAGE_HARNESS_LISTS` of `lib/stage-lists.mjs`, which `stage-init`'s own identity step and the series' branch check use too); the default `deps.harnessAccepted` is `acceptedHarness`, the 12 hex of every dated `instrument | harness:<12 hex>` line, read through PR 3's `parseStageAmendments`, and **none once any data session has left `planned`** (`hasDataSession`); the default `io.stage.initGuardFn` of `stage-init` is `stageInitGuard` through `defaultInitGuard`. `io.stage.guardArgs.deps` still replaces each default by name, and a key set to `undefined` removes it (the seams of the tests). A rehearsal is as before (its guard passes by itself and its `stage-init` answer is the rehearsal's). Before PR 6 there is no manifest and no tag, so a paid series is refused with PR 5's own named problems (`origin holds 0 tags named prereg/lang-v2-*`), not with PR 4's "not wired" sentence, which stays only for a guard whose `preregGuard` was removed. (2) *`stageInitGuard`* finds the ONE tag `prereg/lang-v2-*` on the remote (none, or two, is a refusal naming them), asks that its commit is in the history of the checkout, runs the same guard, and then asks that `prompts-lang.json` equals what `stagePrompts(quickExplore(explore))` computes (brief item 6: a difference refuses `stage-init`), that `schedule.json` holds the pilot (`aa`, `daemon`, `web`) and that the folder holds exactly one price file. It returns `ok({ preregTag, preregCommit, seedSha256, manifestSha256, pilot, harnessIdentity, policy })`: the seed's hash is the content of `seed.sha256`, `manifestSha256` the sha256 of the manifest's committed bytes, `harnessIdentity` the manifest's `identities.harness` and `policy` the repository-relative price file and policy (`registeredInputs`), which the series pins. `stage-init`'s identity step reads the Stage 1 harness identity (it read v1's before). (3) *The team lead's answers to the paused questions.* **Q1:** `capsLinesOf` is deleted; the one parser of dated lines is PR 3's (`parseStageAmendments`, `capsOf`), and the caps exception of `explore.json` reads its one line through it. **Q2:** `liveIdentities` is PR 4's (`lib/stage-identity.mjs`, over the readers of `lib/stage-identity-readers.mjs`); `write-manifest.mjs` imports it statically (no lazy load, so no `importer` seam is needed, and the paused note's item 2 became moot). The manifest holds exactly the 41 names of `IDENTITY_NAMES`, each as the text its reader gave (an extra name, a missing one and an identity that cannot be read are each refused naming them; the guard names an extra or a missing one the same way), and `sessionFloorBytes`: the largest kept session folder of the quick run's series, MEASURED when the files are generated (PR 4's point (5)). The `--floor-bytes` flag of the first draft is removed, since a typed floor is a typed value. **Q4:** the plan's list of harness folders stays; PR 4's `runnerManifest` reads only the PIN of `spikes/lang-bench/runner` (`runner.json`, itself a registered file) and its `templates` identity reads the live bytes of `spikes/lang-bench/templates`, and both folders are in `STAGE_HARNESS_DIRS`; two folders no reader of PR 4 reads, `spikes/lang-explore/runner` (the olympiad judge) and `spikes/lang-explore/templates`, were not in it and ARE now, so that a byte of either moves the harness identity. **Q5:** the contract's own part names each GO row with a line `<!-- go-row: G-<n> -->` and each adjudication rule with `<!-- adjudication-rule: <id> -->` (an id is lower-case words joined by `-`), and the two rule files must hold exactly those ids (scenario 79f); a row of `pilot-rules.json` has the keys `id`, `askedOf`, `evidence`, `mode`, `threshold`, `whenMissing`, `notExercised`, `stopsWhenRefuted` and a rule of `adjudication-rules.json` the keys `id`, `text`, each filled (`false` is an answer, `null` and an empty text are not): **PR 6 must follow these markers and keys**. **Q6:** nobody has seen the real pricing page, so `read-prices.mjs` never guesses: it finds the table by its EXACT header row (`Model | Base Input Tokens | 5m Cache Writes | 1h Cache Writes | Cache Hits & Refreshes | Output Tokens`), reads a price only as `$<number> / MTok`, and refuses unless it finds that header and a price for every model of the base file, naming the headers it did find, the cell that is not a price or the rows it found; a name given twice with different content is refused. **The first real read happens when PR 6 generates the files, and the operator compares the values with the page then.** **Q8:** the bench repository's two tag rulesets (`prereg-tags`, `prereg-tags-create` on `refs/tags/prereg/*`) are data in `lang-bench/lib/github-lang.mjs`, and `github-rulesets.mjs --bench --apply --owner-go 2026-11-19` is the one apply, gated by that date: the owner's go dated 2026-11-19 covers ONE creation, applied and read back before step T's tag, and a later change of them needs its own go. Nothing in this pull request applies a ruleset: every test runs over the fake `gh`. **Q9:** `stage-sync --accept-harness` is removed. In PR 4 a harness acceptance comes ONLY from a dated `instrument | harness:<12 hex>` line of the registered `amendments.md`, which `stage-sync` takes from `origin/main` like any listed file; `stage-sync` never writes an acceptance and never takes a harness folder (a harness fix is a commit of the operator's on the series' branch that the guard accepts under that line, before the first data session only). The flag is still parsed so that it is REFUSED with a sentence that names the dated line to add on main. The guard's sentence for an identity nothing accepted says the same (`harnessAcceptance` of the configuration; v1's keeps its `(--accept-harness)`). *(2026-11-20, epoch 11: Q9 is replaced. The re-review sent candidate (b) back to the brief, so `stage-sync --accept-harness <12 hex>` is RESTORED, with the freeze at the first data plan kept (`harnessAtFirstData`): the fix is on `main` first; its dated line `YYYY-MM-DD | instrument | harness:<12 hex> | commit:<40 hex> <reason>` carries the harness identity of the fixed folders as the 12 hex and begins its reason with the one token `commit:<40 hex>`; the flag finds that line in the amendments of `origin/main` (PR 3's `parseStageAmendments`), refuses a commit not reachable from `origin/main` after a fetch, takes the harness folders of that commit onto the series' branch, checks that their identity is the 12 hex (and restores the tree when it is not), commits with a pathspec, and is refused once a data session has left `planned`; a bare flag is refused with a sentence that names the value. The text above stays as it was built; `research/module_lang_explore.md` and `research/module_tests.md` "PR 5 epoch 11: (b) restored" hold the code's side.)* **Q10:** v1's `prereg-guard.mjs` (the configuration), `seed.mjs` (prefix, shape, files, check) and `bare-init.mjs` (the `installCli` export) changed; v1's own call answers as before, its suites are green, and the pull request says that **v1's `harnessIdentity` moved and that no v1 series is open**. (4) *Choices the brief and the answers did not fix, taken as the safer refusal.* `--series` of `stage-sync` is optional and, when given, must be a Stage 1 series (a typo is a refusal); the harness folders are never taken by `stage-sync`; a generator's `--check` exits 1 whenever the committed file is absent, unreadable or not what it would write; `read-prices.mjs` takes the models of the base file as "the models the series uses"; the open-point-10 sentence of `lib/stage-prompts.mjs` ("the time and memory limits STATEMENT.md states") is kept as the paused draft had it, for the team lead to confirm. (5) *What PR 6 must carry.* `manifest.json`: `identities` (the 41 names, each a text), `sessionFloorBytes` (measured), the `growth` figure and the projection; the files `schedule.json` (with its `pilot`), `prompts-lang.json` (`template`, `templateSha256`, `tasks.<NN>.<stack>.{ text, sha256 }`), exactly one `prices-<yyyy-mm-dd>.json`, `crosscheck-policy.json` and `seed.sha256`; the contract's markers and the two rule files' keys above; the tag name `prereg/lang-v2-<yyyymmdd>`, pushed once by step T, with the ruleset applied first. (6) *(coai plan round, PR 5; seven findings, each RED-GREEN-RED, `research/module_tests.md` "PR 5 coai plan round")* **S, the seed:** `seed.sha256` and `seed.txt` are written whole (temp file and rename); a seed file with no hash file is refused (never reused, never moved aside; the sentence names the way out: the operator removes it), a hash file with no seed file is a lost seed and refused; a second reveal of the same content says `already revealed` and writes nothing. **P, the price read:** `AbortSignal.timeout(30 s)` named as a refusal; the four named scenarios are tests of their own. **C, the CLI pin:** a copy through a temp file and a rename (v1's `installCli` is not atomic and is wrapped, unchanged); the same file there is `already pinned`, another is refused naming both hashes. **H, a harness acceptance is frozen, never forgotten:** the plan that first plans a data session writes `harnessAtFirstData` into the run-map in PR 3's one plan write, and from then the guard accepts exactly that identity or the manifest's; a line dated after the boundary accepts nothing new. **T, the tag:** `stage-init --prereg-tag <name>` names the tag when older series' tags are on the remote; with no flag and several tags it refuses naming them and the flag. **R, the price page:** the fetched page is saved as `prices-<day>.source.md` with its URL, time and sha256 in the price file's `provenance`; `--check` regenerates from it and never fetches. **F, a failed fetch:** a non-zero exit and a timeout (`FETCH_MS` 120 s; v1's `git` names a timeout) are named refusals in the guard and in `stage-sync`. (7) *(Opus review, PR 5; eleven findings, `research/module_tests.md` "PR 5 Opus review")* **V1:** a paid series' slices hold the REGISTERED texts of `prompts-lang.json` and a rehearsal's slice holds `stagePrompts` (the same Stage 1 wording, so a rehearsal rehearses the real wording); a launch refuses a slice with another text. **V2:** the instrument inputs freeze with the first data plan like the harness (`inputsAtFirstData`). **V3:** one `POLICY_FREE_FIELDS` list (t3, t5, decidedOn, reason); a policy change needs its dated `input:crosscheck-policy.json:<12 hex>` line; a missing policy is a refusal, a damaged one never throws. **V4:** `stage-sync` takes only `caps.wallMs` and `caps.stallMs` of `explore.json`, in the tag's own bytes. **V5:** `stage-sync` refuses a staged or changed file anywhere and commits with an explicit pathspec. **V6:** `spikes/lang-bench/tools` and the `.agents/conventions` submodule's recorded commit are in the Stage 1 harness identity; a paid launch refuses a submodule off its recorded commit or with local changes; a Stage 1 session's context records the Stage 1 identity. **V7:** `stage-sync` refuses while a Stage 1 series holds its series lock (read, never taken). **V8:** the manifest's floor is measured over the quick run's one-task series only and names them; `PILOT_SESSIONS` and `REPEAT_PAIR` come from `pilotPlan`. **V9:** `read-prices` takes the CLI version from `explore.json` and the day from the fetch; `--day` is gone. **V10:** the schedule file's read-back rounds are the schedule module's `READBACK_ROUNDS` and the numbers of its prose are the code's. **V11:** no reassigned variable, a missing hashes file and an `explore.json` with no `cli` block are refusal values. **What PR 6 must know:** `manifest.json` holds `sessionFloorSeries`; the price file holds `provenance` and its page snapshot `prices-<day>.source.md` is committed beside it; `inputs.json` of a paid series holds the registered prompt texts; `stage-init` needs `--prereg-tag` once a second tag exists. (8) *(coai code round 1, PR 5; six findings, each RED-GREEN-RED, `research/module_tests.md` "PR 5 coai code round 1")* **D1:** the module doc's entry of `read-prices.mjs` is the current interface (no `--day`; the CLI version from `explore.json`, `--cli-version` only repeats it; `--check` offline from the snapshot), and the paragraph on `stage-sync`'s tree refusal and the brief's removed flag in the PR 7 text are corrected. **D2:** a rehearsal series is made with `STAGE_PILOT`, the one the schedule generator uses (`REHEARSAL_PILOT` is removed). **D3:** `stage-sync` TAKES the series lock of every Stage 1 series under the explore root (PR 3's `acquireSeriesLock`) for the whole of its fetch, take and commit and releases every lock on every way out; one that cannot be taken releases the others and refuses naming the holder, so `stage-run` cannot start a unit during a sync and a sync cannot start during a unit (point (7) V7 said the lock was only read; that is replaced). **D4:** `stage-sync` reads every blob from `origin/main` and computes the new `explore.json` bytes in memory before it writes; a failed write or commit restores exactly the taken paths to HEAD (`git checkout HEAD --`, `git reset -q --`; a path HEAD does not hold is unstaged and removed), so the next sync is not refused for a dirty tree. **D5:** `stage-sync: fetching origin ...` is said before the fetch. **D6:** `--accept-harness`, bare or with a value, reaches the refusal that names the dated line. (9) *(coai code round 2, PR 5; three findings, each RED-GREEN-RED, `research/module_tests.md` "PR 5 coai code round 2")* **E1:** the policy's free fields are four, `t3`, `t5`, `decidedOn` and `reason`; the docs and three comments that said two say four. **E2:** `stage-sync --accept-harness` no longer edits the shared index and the working tree before it commits. It builds the take in an index file of its own (`GIT_INDEX_FILE` in the git folder: `read-tree`, one `update-index --index-info`, `write-tree`, `commit-tree`), checks the harness identity on that tree, writes a journal `{ ref, old, new }`, moves the branch with `update-ref <ref> <new> <old>` (a branch that moved meanwhile is refused), and only then brings the working tree and the shared index to the new commit by steps that are each safe to run twice. A kill before the move leaves the old branch whole; a kill after it leaves a branch ahead of its tree, which the next `stage-sync` (either form) finishes from the journal before it looks at the tree, and which the paid launch refuses until then. A journal was chosen over recognising the state from the trees, because a kill in the middle of the tree update leaves a mix of old and new files and a person's own checkout would look the same. The brief's text ("commits with a pathspec", "restores the tree when it is not") says what the flag does for the operator and stays; the restore is gone because nothing is written before the check. **E3:** the rollback's second lookup of HEAD's paths, which turned a failed lookup into "every path is absent" and removed files HEAD holds, goes with the rollback; the one place that still removes a file reads two trees first and stops, naming the failure, when either read fails. (10) *(Opus review of the take, PR 5; ten findings, each RED-GREEN-RED, `research/module_tests.md` "PR 5 Opus review of the take")* These make the planned step behave as section 3 and the epoch-11 lines already say; none is a plan change. **1:** `stage-init` records the branch it ran on in the series state, and both forms of `stage-sync` refuse unless HEAD is that branch of a paid Stage 1 series whose tag commit is in its history (with no series there is nothing to sync for). **2:** `origin/main` is read by its full ref name `refs/remotes/origin/main` everywhere. **3:** a series whose state cannot be read refuses `stage-sync`; with `--series X` every Stage 1 series is freeze-checked and locked, and an X outside the explore root is refused. **4:** the test of a failed lookup now fails the call the code makes (`ls-tree -r -z`). **5:** every removal is a value naming the path, and a link on the way or a folder where a file is to be removed is refused before anything is written. **6:** the tree is checked again before the take writes, and the recovery refuses a working file that is neither the old nor the new blob. **7:** git runs in `.agents/conventions` only when it is its own checkout. **8:** the `commit:` token ends at a space or the end, and only a whole word counts as a token. **9:** the recovery will not drop a journal while the branch's lock file exists (a detached git child can outlive a kill), and a stale index lock is removed only when no journal exists. **10:** a path with a newline is kept, the recovery checks the identity the commit's message names, and `git()` treats an output cut at its size limit as a failure. (11) *(PR 5b, 2026-11-20, the first real read; `research/module_tests.md` "PR 5b: read-prices meets the real page")*: the first real read of the pricing page, on 2026-11-20, was a refusal. `PRICE_HEADER` is now the header the page really has, `| Model | Base input tokens | 5m cache writes | 1h cache writes | Cache hits and refreshes | Output tokens |` (after the page's column padding is collapsed); the match is still exact (no case folding, no "&" for "and"). A price cell is `$<number> / MTok`, optionally with exactly ONE footnote mark `<sup>N</sup>`, and nothing else; the number in the cell is the price and the mark only explains it. A single footnote mark is recorded with its sentence: the price file's `notes` hold one line per footnote (`footnote N (<model id> <rate>, ...): <sentence of the page>`), taken from the page's `*<sup>N ...</sup>*` lines; a mark whose sentence is not on the page is a refusal that names the model, the rate and the mark. The footnote line has one exact form, `*<sup>N sentence</sup>*` on ONE line (the number, one space, the sentence); a line that starts like it and is not in that form, or a number with two different sentences, is a refusal, and the unnumbered line `*<sup>All other models use the standard 0.1x multiplier.</sup>*` is not recorded (it would be a guess which rates it covers). A row with a note in the model's name (`(limited availability)`, `(retired ...)`, `(for prompts over ...)`) is left unread and never breaks the read of the models the base file uses; but a noted row that is a tier of a base model (its name before ` (` is that model's name) refuses the table, naming the model and the row, because the pricing is ambiguous (coai plan round on PR 5b, N1 and N2); a base model with no clean row is still a refusal. Reasoning, so that no one reads this as a change of the plan: Q6's header was written before the page was seen and was labelled as such, and the plan expected the first real read at PR 6. Correcting the header to the observed one keeps the match exact and never guesses, so this satisfies the plan as written: it is no plan change, and the brief text is not touched. The six tests of `stage-prereg-tools.test.mjs` that encoded the old header (or the old reading of a row with a note) were adjusted; the new tests are in `read-prices-real-page.test.mjs` with an excerpt of the real page as their fixture.

### (b) the dry run and the tag — step T · the owner's go (O1)

**Steps** (the team lead, never a coder).

0. **The harness is final.** PRs 2 to 5 are on `main` and PR 6's manifest was generated after the last of them: the dry run
   first runs `write-manifest.mjs --check` and `identityProblems` on PR 6's merge commit and stops at any difference. From
   here on the series reads the harness at the tag's commit, whatever `main` does (§3: the series' branch).
1. **The dry run**, in a scratch clone of this repository with a scratch bare remote; nothing reaches the real remote, and no
   tag of this name is made in the real clone yet (a local one already locks the snapshots). In the scratch clone an
   annotated tag `prereg/lang-v2-<yyyymmdd>` on PR 6's merge commit is pushed to the scratch remote; the guard passes; then the
   pairs of D64 on the real files: an appended dated amendment line is accepted, and one changed byte of
   `analysis-contract.md`, of the cap in `explore.json`, of one spec pin and of one file under `spikes/lang-explore/lib` (the
   harness identity) is each refused, naming the file or the identity.
2. **The free stub round.** `stage-init --rehearsal`, then round 1's six slots with the stub CLI, a fixture seed, the hello
   programs of `rehearsal/` and the fixture held-out set, in the real boxes with the one-pass scoring: the order, the masks
   read back for each slot, the launch, both ends, the scoring, the next slot. Before it, `stage-run --dry-run` prints all
   30 slots of that rehearsal series with their masks and ends with the count lines, 15 of each mask per stack and 3 and 2
   per task, so the parent's *Accepted when* is shown on a real series and not only by the pure tests. The lines are kept
   as its evidence beside scenarios 1 to 59. The rehearsal series is kept until the pilot: PR 7's tools run over it
   (§3, condition 4).
3. `leakcheck` and the line scan on PR 6's merge commit; `plan-lifecycle`; the three `--check` tools of lang-bench.
4. **The owner's go for the tag (O1)**, asked with the commit, the dry run's result and what the tag locks.
5. The tag is made on the real clone: annotated, on PR 6's merge commit, its message one generated sentence that names the
   commit; checked before the push (not on the remote; annotated; on that commit; the commit is on `origin/main`); pushed
   alone; read back from the remote, peeled, equal to the commit.
6. One dated line in `spikes/lang-bench/inputs/checkpoints.md`: the tag, its commit, the dry run, the go. It is no commit
   of its own: it rides in PR 8, the first pull request of a dated line after the tag.

**Done when:** the tag is on the remote and peels to PR 6's merge commit; the dry run and the stub round are recorded; no paid
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
| the series' branch | a path off the amendable list that differs from the tag; a listed file that is not `origin/main`'s blob | the listed files as `main` has them | scenarios 79a to 79e |
| the tools' rules | a row or a rule in the contract that the rule files lack, or the reverse | the two files equal to the contract | scenario 79f |
| the owner's go | a paid launch with no line for its scope | the launch under its line | scenarios 31, 32 |
| the disk | a launch below the reserve of what is still to run, a repeat pair included; a slot below the rule | the launch with the space there | scenarios 51 to 53 |
| the recovery | freeing anything outside the three cache folders, of an open slot, of a read-back slot not yet read, or not in the verified archive; the final hash list over a freed session | freeing the listed folders of final sessions after a verified archive; the list after `stage-restore` | scenarios 54 to 54h, 118a |
| the clear folder | a slot whose backup cannot be verified; a second task in clear | one task's folder for one slot | scenarios 55 to 58 |

## 6. F3.S2 — the pilot, the paid rounds and the read-back

**Model:** scenarios + brief: this document · code Sonnet · review Opus · the by-hand reads and the read-back: an Opus judge ·
the paid commands: the team lead. **Parent:** F3.S2 (E4.S4 reduced; E5.S1; E5.S2). **Accepted when (parent):** the pilot is
GO; every session is classed; no round k+1 starts with round k unscored; the read-back lines exist before round 3.

| Part | What | Pull request or step (§8) |
|---|---|---|
| (a), (c) | the pilot's GO table and the read-back's print, as tools outside the harness identity | PR 7, after the tag and before the pilot |
| (a) | the go line for the pilot, with the tag's dated record | PR 8 |
| (a) | `stage-init`, the A/A pair, the two pilot pairs | step P — **the owner's go (O2)** |
| (a) | the pilot's record: the by-hand lines, the GO line, the wall and the stall, the seed revealed | PR 9 |
| (b) | rounds 1 to 5 | step R — **the owner's go (O3)** |
| (c) | the read-back lines of round 1's read-back slot | PR 10, before the slot after it |
| (c) | the read-back lines of round 2's read-back slot | PR 11, before the slot after it |

### (a) and (c) the tools — PR 7

**Brief (Sonnet coder).** Branch `feat/lang-v2-f3s2-pilot-readback-tools`. New files under `research/lang-v2/tools/` and its
`test/` only. It merges after the tag, in the parent's order, and before the pilot runs. The five conditions of §3 hold,
and this pull request builds the shared part that keeps them (item 5), which PRs 12 and 13 reuse.

1. **`pilot-verdicts.mjs --series <dir> [--amendments <file>]`** (read only but for its one artefact,
   `<series>/derived/pilot-verdicts.json`; exit 0 GO, 2 NO GO or STOP, 4 a refusal). It interprets the registered
   `pilot-rules.json`, read at the tag's commit; a row id it does not know, or a row whose rule the file leaves open, is a
   refusal. Where one of v1's exported rule functions fits the facts as they are, it is imported and called
   (`spikes/minigit-bench/lib/aa-rules.mjs` `SESSION_ROWS`, `aa-rules-twins.mjs` `g4` to `g14` and `t3Rule`, the helpers of
   `aa-check.mjs`); where it does not (the sessions a row is asked of, the cap of $50, G-14, G-16 to G-18), the tool states
   the row from the rule file. No v1 file changes. The rows:
   - **G-1 to G-14** with v1's row words (`holds`, `refuted`, `explained`, `recorded`, `not exercised`) and v1's GO: every
     row holds; G-6 may be `explained`; G-9 (the twin spread) is recorded and never gates; a by-hand row (G-10, G-13) without
     its dated line is NO GO; G-2, G-8 or G-12 refuted, or alias drift, is a STOP. The twin rows are asked of the two A/A
     twins, the rows v1 asks of its pilot too are asked of all six sessions. **G-12** reads at the registered cap of $50,
     not v1's $6; when no pilot session reaches it, it is `not exercised` with v1's replacing evidence (the budget tests
     green at the series' harness identity, as a dated line). **G-14** is computed as the contract fixes it (open point 4).
   - **G-16**: three dated by-hand lines, one per driver (the TCP driver, the daemon checks, the HTTP driver), each on a real
     pilot tree of the task that exercises it; the row names a missing one. What the hand runs is the private aid's (D41).
     Its clause on the RSS curve lapsed with D74 (§11, closed point 3) and is no part of the row.
   - **G-17**, computed from the registry and the frozen record: twelve references registered at the one value
     (`memory-limit.json`, `packages.json`), every witness of every package caught, and the frozen specs equal to the gated
     ones but for the freeze row (the freeze's own check, read from the record).
   - **G-18**, computed: the cross-check figure of the pilot beside the fixed $50 (it binds nothing; its formula is open
     point 5); the stall larger than the longest quiet gap of any pilot session, read from the stamped streams; every pilot
     session that reached $50 is `killed: cap`.
2. **The GO line is the team lead's, and the harness reads only the line.** The tool ends by printing the dated line to
   write, `YYYY-MM-DD | pilot | GO | verdicts:<12 hex of the sha256 of the file it wrote> | <who>`, and writes no line
   itself. The by-hand lines it reads are not on `main` yet when it first runs, so it takes them from the pull request's
   branch (`--amendments`); its artefact names the sha256 of the text it read.
3. **The wall and the stall, once.** The tool prints the longest quiet gap and the longest wall of the pilot. The team lead
   writes one dated `caps` line (PR 3's kind) with the new `wallMs` and `stallMs` for both stacks, or a line that keeps the
   registered 75 and 45 minutes; the guard accepts that one change of `explore.json` and no second.
4. **`readback.mjs --series <dir> --pair <slot>`: the read-back of a pair** (parent F3.S2 (c)). It reads what the harness
   stored (PR 3, item 11) and prints, for both sessions, each field the rule file names beside the manifest's value, with a
   mark where they differ: the argv, the binds, the cache snapshots' hashes, the package list, the agent box's environment
   keys, the scoring box's keys of a completed pass with the two heap keys and their values, the effective CPU affinity
   (the launch's read-back, the mask observed in flight, each pass's read-back), and the spec blob (the workspace's
   `STATEMENT.md` against the hash list at W, and the frozen spec's sha256). It writes the same as
   `<series>/derived/readback-<slot>-<utc>.json`. It refuses a slot that is not scored yet, and for a read-back slot it
   ends by naming the dated lines the schedule's gate still waits for. An Opus judge reads the print and writes one dated
   by-hand line per session in v1's form with the read-back's row,
   `YYYY-MM-DD | G-1 | holds|refuted | <rid> | <evidence path> | <who>`. A `refuted` line makes the session `void: harness`
   through the harness's own `handVerdictsFor` (PR 3), and its re-run is the schedule's next step.
5. **The boundary, built once for every tool** (`research/lang-v2/tools/boundary.mjs`; §3, conditions 2 and 3).
   `HARNESS_IMPORTS` per tool, the declared list of harness files it imports; `importsProblem()`, which refuses when a
   listed file at the tool's checkout is not the blob of the tag's commit; `artefactHead()`, which every artefact starts
   with: the tool's name, the blob ids of its own files, the tag, the sha256 of the rule file and of every file it read;
   `writeDerived()`, the one writer, which refuses a path outside `<series>/derived/` and the two adjudication folders.
6. **Before it is used** (§3, condition 4): its fixtures' tests, then one run of both tools over the free rehearsal series
   of step T, recorded with its output's counts in `research/module_tests.md`.
7. **Docs:** `research/module_lang_explore.md`, a section on the tools outside the harness identity (what each reads and
   writes, the five conditions); `research/module_tests.md`.

**Scenarios** (`research/lang-v2/tools/test/pilot-verdicts.test.mjs`, `readback.test.mjs`, `boundary.test.mjs`; fixture
series built in the test; no private file):

80. `GO_needs_every_row_to_hold_and_a_by_hand_row_without_its_dated_line_is_NO_GO`
81. `A_refuted_T1_isolation_or_cap_row_and_alias_drift_are_a_STOP`
82. `The_rows_asked_of_every_session_are_asked_of_all_six_pilot_sessions`
83. `The_cap_row_reads_at_50_dollars_and_is_not_exercised_with_its_evidence_when_no_session_reached_it`
84. `G_16_needs_one_dated_line_per_driver_and_names_the_missing_driver`
85. `G_17_is_computed_from_the_registry_and_holds_only_with_twelve_registered_references_at_the_one_value`
86. `G_18_reports_the_cross_check_figure_beside_50_dollars_and_binds_nothing`
87. `The_stall_must_exceed_the_longest_quiet_gap_of_any_pilot_session`
88. `The_twin_spread_is_recorded_and_never_gates`
89. `The_tool_prints_the_GO_line_with_the_hash_of_the_file_it_wrote_and_writes_no_line_itself`
90. `The_read_back_of_a_pair_shows_argv_binds_cache_hashes_packages_both_boxes_keys_the_affinity_and_the_spec_blob`
91. `A_read_back_value_that_differs_from_the_manifest_is_marked_and_named`
92. `A_stored_field_the_read_back_needs_and_does_not_find_is_a_refusal_naming_it_and_never_a_blank`
    - 92a. `The_read_back_of_a_slot_that_is_not_scored_is_refused`
    - 92b. `After_a_read_back_slot_the_print_names_the_lines_still_awaited_and_none_when_both_are_there`
    - 92c. `A_row_or_a_rule_the_registered_file_leaves_open_is_a_refusal_and_never_a_default` (condition 1)
    - 92d. `No_file_under_spikes_imports_a_tool` — a scan of the three spikes for the tools' folder (condition 2).
    - 92e. `A_tool_writes_only_under_derived_and_the_adjudication_folders_and_changes_no_record_run_map_or_ledger_byte`
      (condition 2: the fixture series' other files are hashed before and after).
    - 92f. `Every_artefact_names_the_tools_version_the_tag_the_rule_file_and_the_fingerprints_of_what_it_read`
    - 92g. `A_tools_declared_harness_imports_are_its_real_transitive_imports` (condition 3)
    - 92h. `A_tool_refuses_when_a_harness_file_it_imports_is_not_the_tags_blob` (condition 3)

**The pull request's own check** (also PRs 12 and 13): `git diff --name-only origin/main...HEAD` lists no path under a folder
of `STAGE_HARNESS_DIRS` and no registered file, and the Stage 1 harness identity at its head is the manifest's.

**Done when:** the scenarios went red, green, red; both tools ran over the rehearsal series; §8's row for PR 7.

### (a) the pilot — step P · the owner's go (O2)

**Before any launch.** PRs 2 to 6 are merged and the tag is on the remote; PR 7 is merged and its tools have run over the
rehearsal series. The series runs from one worktree on its own branch, cut at the tag's commit and moved only by
`stage-sync` (§3), with a clean tree under `spikes/`. The share with the key's backup is mounted. The paid commands run in a
`systemd-run --user` unit (§3).

1. `stage-init` (FREE): the series is made; its harness identity is the manifest's; the CLI copy and the tag are pinned; the
   reserve is checked for the first time (it is checked again before every launch).
2. `stage-run --what aa --dry-run` and `--what pilot:<NN> --dry-run`: the rids, the masks and the cap, nothing claimed.
3. **The owner's go for the pilot (O2).** Its line is PR 8, which also carries the tag's dated record (step T, item 6). The
   pull request runs D68's whole runs before it merges (§8), so the pilot starts hours after the go, not minutes. Then
   `stage-sync`.
4. `stage-run --what aa`: the two twins start together, are collected, classified, charged (ledger kind `aa`) and scored by
   the ten passes. Then `stage-run --what pilot:<daemon task>` and `--what pilot:<web task>` (ledger kind `pilot`). A
   session that ends as a void is re-run once under the schedule's rule, if O2's words cover it. All are void spend and
   never data (D16).
5. **By hand, each a dated line** (an Opus judge; the team lead signs): G-10, the scorer by hand on one twin's tree and on a
   Node tree, through the same clean copy and frozen build the scorer uses (`buildAndRestore`, `lib/copy-quick.mjs`), reading
   the session's `deviations` too; G-13, each session's class against its stream and the broker's log; G-16's three lines.
   The trees, the hand's commands and its outputs stay in the series' `private/` folder; a line names counts and a path
   with the home as `~`, never a case.
6. `pilot-verdicts.mjs`: GO, NO GO or STOP. On NO GO the instrument is fixed (a dated `instrument` line; the harness
   accepted before any data session by a dated `instrument | harness:<12 hex>` line on `main`, which `stage-sync` carries; `--accept-harness` is not a flag, PR 5 point (3) Q9), and the pair that missed is run again: paid,
   so under O2's words if they cover a repeat, else under a new go. The repeat is two more sessions than the 66 the
   projection counts: the reserve is recomputed before it with those two and everything still to run, and it is refused
   when the rest of the series would no longer fit (PR 4). A second NO GO on the same row goes to the owner before a third
   pair is paid for (v1's rule). *(2026-11-20, epoch 11: as written, (b) reverted to the brief: the fix is on `main`
   first; its dated `instrument | harness:<12 hex>` line carries the harness identity of the fixed folders as the 12 hex
   and names that commit as the token `commit:<40 hex>` that begins its reason; `stage-sync --accept-harness <12 hex>`
   repeats the identity, refuses a commit not reachable from `origin/main` after a fetch, takes the harness
   folders of that commit and checks that their identity is the 12 hex.)*
7. The wall and the stall: the one dated `caps` line. The policy of the cost checks T3 and T5, if the A/A asks for another,
   as a commit of `crosscheck-policy.json` in its free fields. *(2026-11-20, epoch 11, (d): the free fields are `t3`, `t5`,
   `decidedOn` and `reason`, and the change needs its dated `input:crosscheck-policy.json:<12 hex>` line.)*
8. `seed-reveal`: `seed.txt` is written; the guard asks for it from the first data slot on.
9. PR 9 carries steps 5, 7 and 8, the GO line the tool printed, and the table's result as counts. It runs D68's whole runs
   before it merges. Then `stage-sync`. The pilot's spend is on its two ledger lines.

**Done when:** `derived/pilot-verdicts.json` says GO and its dated line is on `main`; the by-hand lines, the `caps` line and
`seed.txt` are on `main` and on the series' branch; no data session has been launched.

### (b) the paid rounds — step R · the owner's go (O3)

1. **The owner's go for the rounds (O3)**, asked with the pilot's settled costs and the GO table. Its line names the rounds
   it covers; it rides in PR 9 or in a pull request of its own.
2. Round by round: `stage-run --series <dir> --what next --until round`. Each slot: the go line, the guard, the certification
   probe, the reserve and the disk rule, the clear folder opened, the quiet check, the masks read back, both sessions
   launched together, collected, classified and charged, then scored by the ten passes, the records written, the clear
   folder removed. Then the next slot. In rounds 1 and 2 the loop ends after the read-back slot; (c) says what happens
   before it goes on.
3. **What the operator does at a halt** (exit 3, resumable; the same command goes on): not quiet → wait; `--accept-noisy` is
   not used on a data slot without the team lead's written reason, and the flag stays in the record; a mask not applied, the
   preflight, a scoring environment halt → fix the cause and run again; `harness-moved` → the series' branch was moved by
   something other than `stage-sync`: restore it; the backup not verified → mount the share; `void: rate limit` → later; a
   read-back awaited → (c). **The disk rule or the reserve** → the recovery of PR 4, in this order and no other:
   `stage-archive` to the private backup (copy, list, verify, identity recorded); `stage-free`, which removes the three
   cache folders of final sessions of finished slots and nothing else; the same `stage-run` again. Nothing a later slot,
   the scoring, the adjudication or the extraction reads is ever freed, so the series goes on without a restore;
   `stage-restore` runs once, after the last slot, before the final archive and the record's hash list, which both refuse a
   series with a freed session. A file `<series>/pause` stops the loop between slots.
4. **A stop** (exit 2: a `stop:` class, a T1 breach under its policy, a second contaminated void, alias drift) ends the
   series' launches for good, as v1 and the engine have it. The record then names the slots that did not run, and the
   parent's box "30 data pairs ran" stays open. Whether the rest runs in a new series under the same tag is open point 16;
   nothing is built for it.
5. Between rounds the team lead reads `stage-status`: every session classed, the round's flags, the spend lines. A go that
   covered fewer rounds is asked again here.
6. After the last slot: `stage-restore` if anything was freed, then the final `stage-archive`.

**Done when:** the 30 data pairs ran: 30 slots are finished (both stacks scored, or void after the one re-run); every data
session has its `record.json` with ten passes' verdict, its timestamps, `arm` and `model`; every pair has its pair record; no
round started with the round before it unscored; the spend lines are settled. A stop that ended the series leaves this
unmet, with the missing slots named in the record.

### (c) the read-back — PRs 10 and 11, each before the slot after its read-back slot

In round 1 and in round 2, the pair the schedule named from the seed (one daemon, one web) is read back by an Opus judge with
PR 7's tool. This is measurement rule 1 for Stage 1: the stacks of a pair differ only where the harness says they do.

**It is done at once, and nothing runs meanwhile** (plan round 1, finding 2; the gate is PR 2's, scenarios 15a to 15e).

1. The read-back slot is scored; `stage-run --until round` ends there and names the slot.
2. The judge is given the print of `readback.mjs --series <dir> --pair <slot>` and the manifest, and writes one dated line per
   session, `holds` or `refuted`.
3. The lines go to `main` by their own pull request (PR 10 for round 1's slot, PR 11 for round 2's), which runs D68's whole
   runs of lang-explore both ways and CI's checks like every pull request that touches a suite's input (§8). Then
   `stage-sync` takes the file onto the series' branch.
4. Both `holds`: `stage-run` goes on with the next slot.
5. One `refuted`: that session is `void: harness`. Its one re-run is the schedule's next step and runs alone, before any
   later slot. The re-run is read back the same way, its line by a follow-up pull request under the same conditions, and
   only then is the next slot offered. A second `refuted` ends the cell (`missing`), like any second void.
6. A `refuted` line whose cause is the harness itself, and not one launch, cannot be mended after the first data session,
   because the instrument is frozen: the team lead stops the series there, and what follows is open point 16.

So no session is ever voided after later slots ran, and the parent's rule (the lines before round 3) holds with room to
spare. **The cost is two waits of hours each, in which no slot runs:** the judge's reading, two whole runs of the
lang-explore suite, CI's checks and the merge. The first of them records how long it took, in `research/module_tests.md`,
so the second is planned with a measured number.

## 7. F3.S3 — the record, blind adjudication and the Stage 2 decision

**Model:** scenarios + brief: this document · code Sonnet · review Opus · blind step 1 and step 2: Opus judges · the decision
line: open point 12. **Parent:** F3.S3 (E6.S1, from `note.mjs` to a series rollup; E6.S2 with E2.S5's packets; new).

| Part | What | Pull request (§8) |
|---|---|---|
| (b) | the packets, the two steps and the revised verdicts, as a tool outside the harness identity | PR 12, when no launch remains |
| (b) | the adjudication's run | after PR 12, before PR 14 |
| (a) | the record tools | PR 13 |
| (a), (b) | the data, the bare report, the adjudication's counts, the module docs | PR 14 |
| (c) | the dated decision line | PR 15 |

### (b) blind adjudication — PR 12

**Brief (Sonnet coder).** Branch `feat/lang-v2-f3s3-adjudication-tool`. New files under `research/lang-v2/tools/` and its
`test/` only; it merges when no launch remains (every slot finished), where the parent's build order puts the blind
adjudication. **Said plainly, as §3's condition 5 asks: this code is written after the data collection has ended. Its
complete rules were registered before the first paid session, in the contract and in `adjudication-rules.json`, and the
tool has nothing to decide: a rule the file does not hold is a refusal.** PR 7's boundary module and its checks are reused.

1. **v1's exports are called as they are; no v1 file changes** (§3, condition 3).
   `spikes/minigit-bench/lib/adjudicate.mjs` `buildPackets(cells)` (`:67`) is pure and takes cells, so the tool's own
   reader builds the cells and calls it; `lib/adjudicate-mask.mjs` gives the masks (`maskText`, `maskStderr`, `maskExit`,
   `maskCaptured`). The step functions `classifyAmbiguity` (`:117`), `revealFor` (`:129`) and `classifyOrigin` (`:143`) work
   on the files of a series' adjudication folders and are called if they do so over this series unchanged; the tool's
   first red test says, and where one does not fit the tool has its own step, never an edit of v1. `readCells` (`:42`) and
   `revisedFor` (`:153`) read and write v1's cell layout and are not used. The revised verdict calls `verdictOf`
   (`spikes/lang-explore/lib/verdict.mjs:83`).
2. **`adjudicate-reader.mjs`: the cells of a ten-pass series,** as the registered rule says. For every data session of the
   series (the last attempt with data of each cell of the 30 slots; never a pilot session), from the completed attempts
   under `s/<rid>/score/passes/`: every case, visible or held-out, that failed in at least one completed pass gives one
   packet. A packet holds the blind id, the case, the sentences of the requirement ids the case cites (from the frozen
   spec's text), the evidence of the first failing completed pass as the runner recorded it, the pattern (in how many
   completed passes of each profile it failed, so a mixed case shows as mixed), and for a `memory limit` case its cause as
   `kernel kill` or `runtime abort` with the flags `abort-below-evidence` and `child-abort-line` (T-L54; the runtime's own
   name would give the stack away and is shown only after the reveal). A session whose build failed gives no packet; it is
   counted apart.
3. **The masks** are the registered ordered list: v1's `maskRules`, held to the blob id the rule file names, then the added
   rules of the file (the two runtimes' names and error shapes, file suffixes, the heap keys). The tool adds none of its
   own; a test plants each registered word.
4. **`adjudicate.mjs --series <dir> --step packets|classify|reveal|class|revised`** with v1's flags. The packets go to
   `<series>/adjudication-packets/`, the verdicts to `<series>/adjudication/`; both are refused inside any git worktree.
   `adjudication/summary.json` holds counts only (per task and stack: packets, each step-1 verdict, each step-2 class, mixed
   against always failing). It and `adjudication/revised.json` (item 6) are the two files the record tools read.
5. **The two steps.** Step 1: an Opus judge that is given the packets folder and nothing else answers `spec-ambiguity` or
   `not-ambiguous` with a reason for every packet; the reveal is refused until all are answered, and the answers are frozen
   by it. Step 2, after the reveal: `product` or `harness` with a reason.
6. **The revised reading, and what carries it to the record** (plan round 1, finding 4). A case judged ambiguous in any
   session is dropped from every session of its task; a session's revised verdict is the verdict rule over its passes
   without the dropped cases; a session whose build failed keeps its primary verdict. The revised CPC stands beside the
   primary one and never replaces it. What a `harness` class does is whatever the registered rule says; until open point 15
   is answered the rule file cannot be written and the tag waits.
   The step `revised` writes **`adjudication/revised.json`**, once (`wx`; a later run writes a new dated file beside it and
   never over it). It holds one row for **every** data session of the series, also one with no packet, so the record
   tools need no rule of their own: `{ rid, task, stack, round, attempt, primaryVerdict, revisedVerdict, primaryAccepted,
   revisedAccepted, packets, droppedCases, buildFailed }`, the last three as counts and a flag; per task the count of
   dropped cases; `complete: true`; the artefact's head (the tool's version, the tag, the rule file's sha256); and a
   fingerprint, the sha256 over the sessions' `score.json` hashes it was computed from. **What it must not hold (D41 and
   the blind rule):** no case id, no blind id, no reason text (a reason may quote a case), no packet content, no
   requirement id; every field it has is one the public record already carries for a session, or a count. It is written
   only by `revised`, which refuses until the reveal exists and every packet has its step-2 class, and it lives in
   `adjudication/`, never in the packets folder, which is the only folder the step-1 judge is given. The cost is not copied
   into it: CPC's cost has one source, the session's record.

**Scenarios** (`research/lang-v2/tools/test/adjudicate.test.mjs`, a synthetic series; v1's `adjudicate.test.mjs` is not
touched and stays green):

93. `Every_case_that_failed_in_a_completed_pass_gets_one_packet_and_a_case_clean_in_all_ten_gets_none`
94. `A_packet_shows_the_case_the_cited_sentences_the_evidence_and_the_pass_pattern_and_nothing_of_the_stack_or_the_arm`
95. `Every_registered_runtime_word_is_masked_in_a_packet_and_the_tool_has_no_mask_of_its_own`
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
     - 101g. `A_rule_the_registered_file_does_not_hold_is_a_refusal_and_the_packet_rule_the_mask_list_and_the_harness_class_come_from_it`
     - 101h. `A_pilot_session_and_an_environment_attempt_give_no_packet`
     - 101i. `The_tool_changes_no_file_outside_the_two_adjudication_folders_and_refuses_when_an_imported_harness_file_is_not_the_tags_blob`

**Running it** (after the last slot and PR 12's merge): `packets`; the step-1 judge; `reveal`; the step-2 judge; `revised`.
**Done when:** every failing or mixed case has a step-1 and a step-2 verdict; `summary.json` and `revised.json` exist,
`revised.json` complete and with a row for every data session.

### (a) the record tools — PR 13

**Brief (Sonnet coder).** Branch `feat/lang-v2-f3s3-record-tools`. More files in `research/lang-v2/tools/`, outside the harness
identity, built on v1's `research/minigit/tools/` by import (`format.mjs`, `pairs.mjs` `median`, `replay.mjs`, `manifest.mjs`),
never by copy, and on PR 7's boundary module. It may be built during the rounds.

1. **`extract.mjs --series <dir> [--out research/lang-v2/data/<series>.json]`.** Read only. It reads the run-map, the ledger,
   the pair records, every session's `record.json` and the files PR 3's item 11 stored, `derived/pilot-verdicts.json`, the
   registered amendments, `adjudication/summary.json` and `adjudication/revised.json` (PR 12). From the last it takes each
   session's revised verdict and `revisedAccepted`, by rid. It refuses, naming the count, when that file is absent or not
   `complete`, when its rows are not exactly the data sessions of the run-map, or when its fingerprint is not the one of
   the sessions' `score.json` files now (a re-score after the adjudication makes it stale). It never opens the packets or
   the verdict files. It writes the data file: per session the kind, task, round, stack, `arm`, `model`, class, flags, the
   four cost views with the cost checks T1 to T5, the tokens per model and kind with the cache split, the time partition
   with every category, the scoring time apart, the verdict of the ten passes, the counts of visible and held-out cases
   passed, the defects as counts, `deviations`, the memory causes and flags, **the culture of both boxes and the `run.sh`
   fact** (below); per pair the masks, the stamps and the flags; the identities. It never opens `score/` beyond what
   `record.json` holds, drops every case id, and writes the home as `~`.
2. **The culture and `run.sh`, per session** (the parent's Definition of Done, `:471`–`:472`; D35, D38; the Opus review,
   M5). From what the harness stored: `culture: { agentBox, scoringBox }`, each `LANG` and `LC_ALL` as that box had them,
   and `runSh: { sha256, frozen, restored }`. The report has one block for them: the count of sessions per stack whose two
   boxes both had `de_DE.UTF-8`, and the count whose scored `run.sh` was the frozen file, with every exception listed by
   rid. A session that lacks either stored fact fails the extraction, naming it; it is never counted as holding.
3. **CPC has one source, and every measure gets the same figures.** The per-task figures call `cpcBoth`
   (`spikes/lang-explore/lib/records.mjs:181`). The ratios, the median, the geometric mean and the count are new small pure
   functions (`summary.mjs`) over §9's five cases, **for every measure** (CPC, `CPC_all`, the success rate, tokens, wall,
   `model_api`, `active − model_api`; §9, `:1646`), each with its denominator and its line of undefined outcomes, **over the
   six tasks and again per kind**, "median over k of 3" for the daemon tasks and for the web tasks (§9, `:1650`–`:1651`).
   **The revised reading is the same functions over the same sessions and the same costs, with `accepted` taken from
   `revisedAccepted`**; nothing about a case is needed for it, and both readings stand side by side in every table that
   has a CPC.
4. **`render.mjs`: the whole report is generated.** `report-template.md` holds the prose with two kinds of placeholder: a
   block computed from the data file, and a quote read from the parent at the commit the pre-registration quoted. The
   template holds no digit outside a placeholder and a heading's number; a test fails on one. The output is
   `research/lang-v2/RESULTS_lang_v2_stage1_<yyyy-mm-dd>.md`; `render.mjs --check` writes nothing and exits 1 when the
   committed report is not what it would write.
5. **The quotes are Stage 1's** (the Opus review, M3). What the analysis licenses is quoted from *The claim and its stated
   limit* and *Outside the claim* (`:438`–`:446`). §9's older paragraph (`:1658`–`:1663`) speaks of ten tasks, three pairs
   and five known tasks; it is not quoted, and T-L17's clause "the licensing paragraph of §9 verbatim" is read with the
   operative part, which holds where an older section says something else (`:367`–`:369`). The threat sentences are the
   rows of *Threats this change touches* (`:837`–`:849`): T3 (the claim's limit, beside every ratio), T5, T6 and T9, T23,
   T25, T26, T27 and T28. The template names them "threat T3" and so on, and names the cost cross-checks of §8 "cost check
   T1" to "cost check T5", so the two families are never one word.
6. **The blocks** (parent Definition of Done, F3.S3 (a), §9): the identities (measurement rule 6: the task repository's sha
   and freeze tag, the pre-registration tag, the harness identity, the runner manifest, the layer, FORMAT's sha, the
   contract's identity, the memory value, the CLI version, the model id, the toolchain checksums, the cache hashes, the
   machine, the dates, and each tool's version); per task CPC or the undefined outcome, `CPC_all`, the success rate, tokens,
   wall, `model_api`, `active − model_api`, `deviations` beside `accepted`, the counts of `rejected`,
   `intermittently incorrect` and `unscored: environment` per stack; the overall and the per-kind figures of item 3, with
   and without flagged pairs; known against original as a listing and never a summary (D29); every v1 metric (D34); the
   memory causes; the culture and `run.sh` block; every attempt, voids and re-runs included; the quiet flags; the five
   spend lines; the pilot's table with the twin spread; the read-back lines and the amendments; the prediction beside the
   observation; the voids replayed with the current scan (`research/minigit/tools/replay.mjs:85`, its series pattern an
   argument) and which were the scan's; the adjudication's counts and the revised reading beside the primary, with the
   sentence of §3's condition 5; the olympiad notes and the quick run cited as exploratory controls outside the claim,
   never pooled.
7. **The raw records' hashes.** v1's `manifest.mjs` over the series writes its sha256 list beside the data file. It is
   written over the whole tree: it refuses a series in which any session still holds a `freed.json` and names
   `stage-restore` (PR 4). `extract` and `render` do not need the freed folders and give the same bytes with or without
   them.
8. **Docs:** `research/module_lang_explore.md` (the record tools and where the one engine ends), `research/module_tests.md`.

**Scenarios** (`research/lang-v2/tools/test/*.test.mjs`, fixture series built in the test; no private file):

102. `The_report_is_rendered_from_the_data_file_alone_and_a_second_render_gives_the_same_bytes` (T-L17)
103. `A_digit_in_the_template_outside_a_placeholder_fails`
104. `The_claim_its_limit_and_what_is_outside_it_are_the_operative_parts_text_byte_for_byte_and_the_older_paragraph_is_not_in_the_report`
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
     - 118e. `Every_measure_has_its_median_geometric_mean_and_count_with_its_own_denominator_and_not_CPC_alone`
     - 118f. `Each_kind_has_the_same_figures_over_its_three_tasks_and_a_kind_with_an_undefined_task_reads_k_of_3`
     - 118g. `The_culture_of_both_boxes_and_the_run_sh_fact_are_in_the_data_for_every_session_and_a_session_without_them_fails_the_extraction`
     - 118h. `A_session_whose_culture_or_run_sh_differs_is_listed_by_rid_and_never_counted_as_holding`
     - 118i. `The_template_names_threats_and_cost_checks_apart_and_a_bare_T_number_fails`

**Done when:** the scenarios went red, green, red; v1's `replay.mjs --self-check` and `manifest.mjs --self-check` pass after
the change to `replay.mjs`; §8's row for PR 13.

### (a), (b) the bare report — PR 14

Branch `docs/lang-v2-stage1-report` (the team lead; no code). After the adjudication's run and `stage-restore`: `extract`,
the replay, the manifest and `render` run over the finished series; the data files and the report are committed with the
rows of `research/README.md`, the updated `research/module_lang_explore.md` (it describes the one engine, the series, the
tools outside the harness and the record tools), the two language-comparison rows of `research/architecture.md` (one still
says "not a series yet", the other calls the engine exploratory), and `research/module_tests.md`. `render.mjs --check` exits
0; `leakcheck` and the line scan are clean (the report is a document under `research/lang-v2/`). An Opus judge reads the
report against the pre-registration: every pre-registered figure is there, the conclusion is no wider than the claim and
its limit, and no number stands outside a generated block.

**Done when:** the report is on `main`, generated from the data alone, with T26's sentence and the claim's limit.

### (c) the Stage 2 decision line — PR 15

The line stands in the parent, under *Stage 2: the dated decision*, dated the day the report was generated:

> Stage 2, the process arm on the same six tasks, is decided on `<date>`: go, defer to `<date>`, or cancel, with a reason that
> does not depend on which stack won Stage 1.

PR 15 (branch `docs/lang-v2-stage2-decision`) writes it with the status lines of the parent and of this document and their
`todo/README.md` rows. It is the plan as written being carried out, so it is no entry of the ledger. What follows is outside
F3: on *go*, the engagement pilot before any Stage 2 epic is split, a paid session that needs the owner's own go; on
*defer*, the references and the trap table stay private until the next dated line; on *cancel*, E6.S3 (the publication,
which is public and not to be undone and so waits for the owner's go like O1; the teardown; the promotion of the parent
with its stories documents and their deviations).

**Done when:** the line is on `main`, dated; `plan-lifecycle.mjs` is clean.

## 8. The order of the pull requests and the steps

The merge conditions are the parent's (*Merge conditions*), and none is lowered here: **(1)** the gates on the instrument's
code: a coai plan round at `proceed` before the code, then a coai code round and an Opus review on the committed branch,
every finding resolved; **(2)** D68's condition, unchanged (`:524`–`:527`): one whole completion run of every suite the pull
request touches (`spikes/lang-explore/`, `spikes/lang-bench/`, `spikes/minigit-bench/` as touched), plain and with
`BENCH_REQUIRE_SANDBOX=1`, every test passed and nothing skipped, and CI's required checks present and passing at the head
sha; the same test failing in two consecutive whole runs stops the story; **(3)** D41: the line scan before every merge, and
`leakcheck` when a document under `research/lang-v2/`, a registered file or an agent-visible file is touched; **(4)** no
Stage 2 code. `plan-lifecycle.mjs` clean on every one. Each pull request merges `main` before its last whole run. At most
three are open at a time; branches are built locally side by side.

**A pull request "touches a suite" when it changes any file under that spike's folder, an input as much as a module.** So
the pull requests of dated lines and of the pre-registration files run the lang-explore suite whole, both ways: their
files are under `spikes/lang-explore/inputs/`. A pull request that changes files under `research/` only touches no suite;
for it condition (2) is CI's checks, and the tools' own tests run whole where a tool changes.

| # | Story | Branch | What it is | What it runs before it merges | Conditions |
|---|---|---|---|---|---|
| 1 | — | `docs/lang-v2-f3-stories` | this document, its `todo/README.md` row, and one line under the parent's F3 epic that links here | line scan; `plan-lifecycle`; CI; an Opus reading by the team lead | (2) no suite's file is touched: CI's checks; (3) line scan; (4) |
| 2 | F3.S1 (a) | `feat/lang-v2-f3s1-schedule` | the schedule, with the read-back gate (and v1's two hash helpers exported) | the gates; lang-explore and minigit-bench whole runs, both ways; line scan | (1), (2), (3) line scan, (4) |
| 3 | F3.S1 (a) | `feat/lang-v2-f3s1-series` | the series over six tasks, the seam in v1, the dated lines, what a session stores | the gates; lang-explore and minigit-bench whole runs, both ways; the free rehearsal of round 1; line scan | (1), (2), (3) line scan, (4) |
| 4 | F3.S1 (a) | `feat/lang-v2-f3s1-guards` | the guards before a paid pair, the reserve before every launch, the archive, free and restore verbs | the gates; lang-explore and lang-bench whole runs, both ways, `LANG_V2_TASK_REPO` at W; the real probe; `leakcheck`; line scan | (1), (2), (3) with `leakcheck`: it changes how the held-out side is opened, (4) |
| 5 | F3.S1 (b) | `feat/lang-v2-f3s1-prereg-tools` | the pre-registration tools, the widened guard, the series' branch and `stage-sync`, the seed, the CLI pin | the gates; lang-explore and minigit-bench whole runs, both ways; line scan | (1), (2), (3) line scan, (4) |
| 6 | F3.S1 (b) | `docs/lang-v2-prereg` | the pre-registration files, the two rule files among them; the manifest read after PRs 2 to 5 | every generator's `--check`; `identityProblems` empty on its head; the document gate; lang-explore whole runs, both ways; the three `--check` tools of lang-bench; `leakcheck`; line scan; CI | (2) in full: its files are inputs of the lang-explore suite; (3) with `leakcheck`: public text over the private side; (4) |
| T | F3.S1 (b) | — | the dry run and the stub round on the final harness, **the tag — the owner's go (O1)** | step T's steps 0 to 6 | (3): the tag's message is public text |
| 7 | F3.S2 (a), (c) | `feat/lang-v2-f3s2-pilot-readback-tools` | the GO table's and the read-back's tools and the boundary module, under `research/lang-v2/tools/` | the gates; the tools' tests, whole; its own check (no harness path, no registered file, the harness identity is the manifest's); one run over the rehearsal series; line scan; CI | (1); (2) no suite's file is touched: CI's checks and the tools' tests; (3) line scan; (4) |
| 8 | F3.S2 (a) | `docs/lang-v2-go-pilot` | the owner's go line for the pilot; the tag's dated line in `checkpoints.md` | lang-explore and lang-bench whole runs, both ways (one file of each spike changes); line scan; CI | (2) in full; (3) line scan |
| P | F3.S2 (a) | — | `stage-init`; **the A/A and the two pilot pairs — the owner's go (O2)**; the by-hand reads; `pilot-verdicts.mjs` | step P | — |
| 9 | F3.S2 (a) | `docs/lang-v2-pilot-record` | the by-hand lines, the GO line, the `caps` line with `explore.json`'s two fields, the policy if it moved, `seed.txt`, the go line for the rounds | lang-explore whole runs, both ways; `leakcheck`; line scan; CI | (2) in full; (3) with `leakcheck` |
| R | F3.S2 (b) | — | **rounds 1 to 5 — the owner's go (O3)**; the loop ends after each read-back slot | step R | — |
| 10 | F3.S2 (c) | `docs/lang-v2-readback-r1` | the read-back lines of round 1's read-back slot, before the slot after it | lang-explore whole runs, both ways, while no slot runs; `leakcheck`; line scan; CI | (2) in full; (3) with `leakcheck` |
| 11 | F3.S2 (c) | `docs/lang-v2-readback-r2` | the read-back lines of round 2's read-back slot, before the slot after it | the same | (2) in full; (3) with `leakcheck` |
| 12 | F3.S3 (b) | `feat/lang-v2-f3s3-adjudication-tool` | the packets, the two steps and the per-session revised verdicts, under `research/lang-v2/tools/`; when no launch remains | the gates; the tools' tests, whole; its own check; `leakcheck`; line scan; CI | (1); (2) no suite's file is touched: CI's checks and the tools' tests; (3) with `leakcheck`; (4) |
| 13 | F3.S3 (a) | `feat/lang-v2-f3s3-record-tools` | the record tools | the gates; the tools' tests, whole; v1's `replay.mjs --self-check` and `manifest.mjs --self-check` (it changes `replay.mjs`); its own check; `leakcheck`; line scan; CI | (1); (2) no suite's file is touched: CI's checks, the tools' tests and v1's self-checks; (3) with `leakcheck`; (4) |
| 14 | F3.S3 (a), (b) | `docs/lang-v2-stage1-report` | the data, the bare report, the docs | the adjudication's run complete; `stage-restore` if anything was freed; `render.mjs --check`; the tools' tests; `leakcheck`; line scan; CI; an Opus reading against the pre-registration | (2) no suite's file is touched: CI's checks; (3) with `leakcheck` |
| 15 | F3.S3 (c) | `docs/lang-v2-stage2-decision` | the dated decision line | `plan-lifecycle`; line scan; CI | (2) no suite's file is touched: CI's checks; (3) line scan |

**Order and why.** This is the parent's build order, steps 5, 6 and 7 (`:815`–`:817`), unchanged. PR 1 first, so every brief
cites a merged document. **Step 5:** PR 2 is pure and comes first of the code; PR 3 needs it; PRs 4 and 5 are built beside
PR 3 and merge after it (they supply the guard it refuses to run without). PRs 2 to 5 are every change to a harness folder
that F3 makes, and all of them are on `main` before PR 6's manifest is generated (plan round 1, finding 1). PR 6 is the last
merge before the tag. Step T. **Step 6:** PR 7, the two tools outside the harness, then PR 8, step P, PR 9; step R: round 1
up to its read-back slot, PR 10, the rest of round 1 and round 2 up to its read-back slot, PR 11, then to the end of
round 5. **Step 7:** when no launch remains, PR 12 and the adjudication's run; PR 13, which may be built any time after
PR 7; `stage-restore` if anything was freed and the final archive; PR 14; and PR 15 the same day as PR 14's report.

**The waits this order costs, said once.** Each of PRs 8 to 11 is a pull request of a line or two and still runs D68's
whole runs and CI before it merges: hours, not minutes. PR 8 stands between the owner's go and the pilot; PR 9 between the
pilot and round 1; PRs 10 and 11 each stop the series in the middle of a round. No slot runs during a whole run. The first
of these pull requests records its wall in `research/module_tests.md`.

## 9. The model per role

| Role | Model | In F3 |
|---|---|---|
| Stories, scenarios and briefs | opus by the role table | this document was written by the Fable planner on the team lead's order of 2026-11-19, a deviation of role that is recorded here and is no change of the plan (§11, closed point 18); F3 has no cornerstone |
| Coder | sonnet | the code and tests of PRs 2 to 5, 7, 12 and 13, and the fixes after review |
| Small edit | main session or sonnet | the dated-line pull requests 8 to 11 and 15: one or two files each, a few lines, bounded before the first edit |
| Reviewer and judge | opus | the review of every code pull request beside the coai code round; the by-hand rows G-10, G-13 and G-16; the read-back of rounds 1 and 2, done at once after its slot; both adjudication steps (step 1 sees the packets only); the reading of the report against the pre-registration |
| Scout | sonnet | evidence only: a stream's quiet gaps, the remote's tags, a series' state |
| The team lead (main session) | — | asking the owner for O1, O2 and O3 and for open points 12, 19 and 20; the tag; `stage-init`; every paid command; `stage-sync`; `stage-archive`, `stage-free` and `stage-restore`; `seed-commit` and `seed-reveal`; the dated lines; the pre-registration's prose and the two rule files with an Opus writer; never a coder |

The cornerstone ledger does not change: the parent's line reads *Cornerstones: 2 of 2 — F1.S2, F2.S1*, and this document
marks none.

## 10. Growth

- **The Stage 1 series** (`~/rsd/lang-v2/explore/x-<date>-<hex>/`, outside git): 66 sessions and at most one re-run of each
  void, the pilot's included; a repeat pair of the pilot after a NO GO adds two sessions each. At the quick run's size,
  about 1.4 GB a session, that is about 92 GB, and about 185 GB if every one of the 66 were re-run; the ten passes' records
  add under 1 MB a session. The parent counts 632 GB free (`:900`). Kept for ever, append only (D34); `prune` refuses it.
  **The reserve is checked before every paid launch** over what is still to run, a re-run and a repeat pair included, and
  the disk rule before every slot; the session size they read is computed live and written nowhere. **What a disk halt may
  free** is one closed list, the three cache folders in the box of a final session of a finished slot (`box/nuget`,
  `box/npm`, `box/home/.dotnet`), and only after the archive holding them was verified; they are restored from it before
  the final archive and the record's hash list. Nothing else of the series is ever removed. Interrupted: a halted slot is
  resumed, a `launching` session is settled by v1's `reconcile`, an interrupted pass is an environment attempt and is run
  again (D71); a `stage-archive`, `stage-free` or `stage-restore` that was killed is run again and finishes from the lists
  (each removes or copies only what its list names, and verifies after).
- **Its verified copy** on the private backup: the same size, made after the series ended and at any disk halt before local
  space is freed; each archive is an entry of `archive.json` with its list's sha256.
- **The series' branch and worktree:** one local branch at the tag's commit plus a few commits of dated lines, never
  pushed; one worktree. Both are removed after PR 14 merged; the tag and `main` hold everything they held.
- **The tools' artefacts** (`<series>/derived/`): the GO table's file and one read-back file per read, kilobytes each, each
  written once under a dated name; kept with the series.
- **The clear held-out folder:** one task's at a time, for one slot, mode 700, removed after it and at the start of a run
  after an interruption.
- **The free rehearsal series** (`r-…`): six slots of hello programs, well under 1 GB. It is no data; it is kept until PR 7's
  tools have run over it, then removed by hand, and a killed one is swept by the owner rules.
- **The pre-registration files and the dated lines:** kilobytes, in git; `amendments.md` only grows.
- **The seed:** one file of a few bytes, outside git, mode 600, kept.
- **The CLI copy:** one folder under `~/rsd/lang-v2/toolchains/`, kept, because every record names it.
- **The adjudication's packets and verdicts:** a few megabytes in the series' folder, private, kept with it; `revised.json`
  is a few kilobytes of rows and counts, and a second run adds a dated file beside it.
- **The data files, the hash list and the report:** in git, written once per series. v1's are 28 to 112 KB a series and a
  2.4 MB hash list for 12 sessions; Stage 1's are a few megabytes in all. A second render replaces the report whole.
- **The dry run's scratch clone and remote:** owned folders, removed when step T ends.

## 11. Open points, and the points closed on 2026-11-19

Each was a gap or a conflict in the parent, or a reading this document had to take. Line numbers are of
`todo/PLAN_lang_comparison_v2.md` at `5740809f`. The numbers are those of the first version, so earlier reports stay
readable. Every open point says who settles it: **owner**, where a rule or the parent names the owner, or **consultants**,
who are asked first for everything else.

### Closed (the team lead's decision and the Opus review; each from the parent's own text)

1. **When a void is re-run: before the next slot.** The operative part holds where an older section says something else
   (`:367`–`:369`); it says the barrier and the one void re-run stay (`:764`), and the base it names is lang-explore's own
   series (D70, `:825`), whose barrier re-runs before the next pair. §4.4's "after the round's own pairs" (`:1283`) is the
   older text.
3. **G-16's RSS clause lapsed.** The per-case RSS curve became follow-up work that was never measured (D74, `:829`; item 9
   of *What got smaller*, `:413`; the table's row for E3.S2, `:880`), so the hand has no curve to compare. The contract
   says so, and the row keeps its other clauses.
6. **One pre-registration, one first session.** D69 removed the groups (`:824`). The Definition of Done asks for one alias
   baseline and one CLI copy for the series (`:467`), and F3.S1 (b) names the CLI pin among the one pre-registration's parts
   (`:770`). So D47's "group A's pre-registration" and "the first session of group A" (`:1048`) read as the one
   pre-registration and the series' first session, the A/A's, and the T-L rows worded per group read without the group.
9. **The A/A may set the policy of the cost checks T3 and T5.** The parent says so three times (`:1562`, `:1611`, `:1510`).
   v1's own list (`t2`, `t3`) is v1's; Stage 1's list is the parent's.
13. **The disk rule's number is the parent's reading.** *Growth* counts a kept session with its box, about 1.4 GB (`:898`),
    and makes the pair's worst case "two sessions' trees plus that size twice" (`:904`–`:905`). `growth.json` holds only the
    second part, so the rule adds a session folder's size, computed live (the Opus review, M7).
18. **Who wrote this document** is a deviation of role and no change of the plan. The parent has an epic's stories document
    written by Opus (`:562`–`:563`); the team lead ordered this one from the Fable planner. It is recorded in §9.
21. **The tools and the build order: the parent's order is kept** (the team lead, 2026-11-19; consults `c823a3ae` and
    `757cdb08`; the owner was told). The GO table's, the read-back's and the adjudication's tools stand outside the harness
    identity under §3's five conditions and merge after the tag. No entry of the parent's ledger is needed.

### Open

**Needed before the pre-registration files are written (PR 6):** 2, 4, 5, 7, 8, 10, 11, 14 and 15. **Needed before PR 3:**
19.

2. **The pilot's tasks and the A/A** — *consultants*. Narrowed: the pilot's trees must cover the tasks 04, 05 and 10,
   because G-16 checks each driver on a real pilot tree of its task (`:1743`), and F3.S2 (a) has one pair per kind
   (`:780`). So the web pair is task 04, and the daemon pair and the A/A take 05 and 10 between them. Still free: which of
   the two is the A/A's, and the A/A's stack (D16's A/A was C#, of an olympiad task that is outside Stage 1's claim,
   `:1017`, `:1725`).
    **Answered 2026-11-19 (consult `a4be62c7`, 2 of 2):** the A/A is task 05 on Node (both twins), and the daemon pilot pair
    is task 10 in both stacks; the web pair is 04. Task 05's Node sessions of the quick run spread most (937.8 s to 3343.3
    s, $1.37 to $4.33, `research/lang-v2/EXPLORE_task05_2026-11-05.md`), and task 10 carries G-16's checks of the memory
    peak, SIGTERM and the timer-driven final reports in both runtimes. Registered in PR 6 (`schedule.json`'s pilot as `{ aa:
    { task: "05", stack: "node" }, daemon: "10", web: "04" }`), not drawn by the seed; G-9 records the twins' spread and
    gates on nothing.
4. **G-14** — *consultants*. §11 keeps G-1 to G-14 (`:1717`), and G-14 is v1's rehearsal of its second phase
   (`todo/PLAN_minigit_stack_bench_E3_bare.md:526`), which a one-phase series does not have. A reading: the free rehearsal of
   a daemon and a web task passes in both stacks on the series' harness. The contract must fix one reading (§3,
   condition 1).
    **Answered 2026-11-19 (the team lead, the reading above):** G-14 is read as the free rehearsal of one daemon and one web
    task passing in both stacks on the series' own harness (step T's stub round); the contract (PR 6) writes that reading.
5. **G-18's cross-check figure** — *consultants*. Its formula (`:1771`–`:1780`) is bounded by `capMax(arm)`, which is defined
   from the superseded $1000 ceiling, and scales by time guesses for tasks that were not piloted. A reading: twice the
   larger settled cost of the pilot pair of the task's kind. It binds nothing either way (`:1745`), and the contract must
   fix it.
    **Answered 2026-11-19 (the team lead, the reading above):** G-18's cross-check figure is twice the larger settled cost
    of the pilot pair of the task's kind; it binds nothing (`:1745`), and the contract writes it.
7. **The prediction** — *consultants*. §9's draft is for ten tasks and two arms (`:1665`); nothing states it for six tasks,
   bare, so there is no text to quote verbatim, and T28 asks `prediction.md` to say when it was written (`:849`). Who writes
   the six-task lines, and whether they go into the parent first, is open.
    **Answered 2026-11-19 (the team lead):** the team lead writes the six-task bare lines of `prediction.md` in PR 6, dated
    the day they are written and before the tag (T28), from the quick run's exploratory data and labelled as such; the
    parent's ten-task draft (`:1665`) stays as history and is not edited, and nothing goes into the parent first.
8. **Q6, the sign test** — *consultants*. Narrowed: this document writes "not shown", and the parent supports it: the list
   of what the report carries has no test (`:461`–`:464`), and §9 calls no p-value, interval or effect size a finding and
   leaves the sign test to Q6 (`:1661`), which nobody answered (`:2233`). The point stays only for a wish to add the line; it
   would have to be registered before the tag.
    **Answered 2026-11-19 (the team lead):** "not shown" stands; no sign test is registered, so none is reported.
10. **The bare prompt's limits sentence** — *consultants*. Narrowed: the wording is F3.S1 (b)'s own to fix, because §4.5
    leaves the final wording to E4.S2 (`:1299`) and E4.S1 to S3 are F3.S1 (b) (`:886`). What is left is the one sentence
    "under registered time and memory limits, never below the floors STATEMENT.md guarantees" (`:1307`): since D72 the
    statement states the limit itself (`:827`). Keep it or reword it, checked against the frozen `STATEMENT.md` texts at W,
    at the document gate of PR 6.
    **Answered 2026-11-19 (the team lead):** the sentence is reworded to "under the time and memory limits STATEMENT.md
    states" (D72: the statement states the limit itself), checked against the twelve frozen `STATEMENT.md` texts at W at PR
    6's document gate.
11. **The day of the prices** — *consultants*. §8 has them re-read on the series' first day (`:1568`); §9 freezes them with
    the tag before the first launch (`:1578`). They agree only if the tag and the first launch are on one day, and PR 8's
    whole runs stand between them. This document reads the prices on the day the files are generated.
    **Answered 2026-11-19 (the team lead):** the prices are read on the day the pre-registration files are generated and
    frozen by the tag (§9, `:1578`); on the series' first day they are read again as a check only, and a change is a dated
    amendment line before the first paid launch, never a change of the registered figures.
12. **Who decides the Stage 2 line** — *owner*. The parent gives its words and its date (`:851`–`:861`) and says the reason
    must not depend on which stack won (`:831`); it does not say who decides. D76 is the owner's decision, and a *go* leads
    to paid sessions, so the question goes to the owner.
    **Answered 2026-11-19 by the owner:** the owner decides and dates the line; the team lead prepares the report and a draft
    of the line, nothing more. PR 15 carries the owner's words.
14. **The re-run spend line** — *consultants*. D7 and the Definition of Done name pilot, A/A, data, void and re-run lines
    (`:1008`, `:482`); the ledger has no re-run kind. This document derives the line from the attempt number; the
    pre-registration states the split, so it must be confirmed first.
    **Answered 2026-11-19 (the team lead):** the re-run line is derived from the attempt number (attempt 2 of a void is the
    re-run line), and the contract (PR 6) states the split of the five lines.
15. **What a `harness` verdict of step 2 does** — *consultants*. E6.S2 puts a revised score beside the primary (`:2795`);
    D71 makes a rejection permanent and never re-run (`:826`); G-16's remedy is "fix, re-score". Whether a failure the
    harness caused changes a session's revised verdict, or is only listed, is not said. Under §3's condition 1 the
    contract must say it, so the tag waits for this answer.
    **Answered 2026-11-19 (consult `55cf3841`, 2 of 2):** a step-2 `harness` case is listed, never removed: it changes
    neither the revised verdict nor CPC (only ambiguity exclusions recompute the revised reading, as v1's `revisedFor` reads
    step 1 only, `spikes/minigit-bench/lib/adjudicate.mjs:152`). The report lists each such case with its session and
    evidence and says whether it was pivotal (the session's only failing case) or not; a corrected score exists only after
    an instrument fix and a re-score, recorded as a separate dated amendment beside the primary and the revised results. The
    contract (PR 6) and `adjudication-rules.json` carry it.
16. **After a series stop** — *consultants* (a new series would need the owner's O2 and O3 again). §11 says a stop holds
    "until fixed and an A/A is GO" (`:1749`); the engine and v1 refuse a stopped series for good. Whether the slots left run
    in a new series under the same tag, and are read with the first, is not said; nothing is built for it, and the
    parent's box "30 data pairs ran" (`:458`) stays open after a stop.
    **Answered 2026-11-19 (the team lead; not needed before PR 6):** a stop ends this series for good; the slots left would
    run only in a new series under a dated amendment of the pre-registration, with the owner's O2 and O3 again, and the
    parent's box "30 data pairs ran" stays open until then. The owner may decide otherwise when a stop happens.
17. **The credentials** — *consultants*. The Definition of Done has them torn down after the series (`:501`); E6.S3, which
    does it, comes after the dated decision (`:890`), and a *go* needs them. F3 leaves that box open.
    **Answered 2026-11-19 (the team lead):** F3 tears nothing down; the parent's credentials box stays open until E6.S3,
    after the dated decision (`:890`).
19. **The go line in code** — *owner*, before PR 3. The refusal of a paid launch without its `owner-go` line is a guard this
    document adds for §4; the parent has none. It is no spend limit (D53 adds none). **Its consequence:** if it is counted
    as a change of the plan, it is an entry of the parent's ledger, and the parent's epoch 10 has one entry already
    (10.1), so a second would stop the plan for a whole re-review. The owner says either that it is a safeguard of the
    way F3 is run and no change, or that it is to be procedure only; in the second case PR 3 drops the refusal and keeps
    the lines as a record.
    **Answered 2026-11-19 by the owner:** the refusal stands in code (PR 3, with PR 5's guard). It is part of this stories
    document as written when the epic starts and changes nothing the parent fixes (no scope, merge condition, task, metric or
    Definition-of-Done item), so it is no ledger entry.
20. **No ruleset protects the pre-registration tag** — *owner*, only if a ruleset is wanted. The task repository's rulesets
    cover `spec-frozen-*`. The guard sees a moved tag, because the series pins the commit. A ruleset on this repository
    would need the owner's own go.
    **Answered 2026-11-19 by the owner: yes, a ruleset.** Before step T's tag push, a ruleset on this repository refuses the
    update and the deletion of `refs/tags/prereg/*` (creation by the admin only, as the task repository's `spec-frozen-tags`
    pair), written as data beside `spikes/lang-bench/lib/github-lang.mjs`'s, applied with `--apply` and read back equal; PR 5
    carries the data, its check and its tests, and step T applies it and records the ids. The owner's go of 2026-11-19
    covers creating this ruleset once; any later change of it needs its own go.

## 12. Definition of Done (F3)

- [ ] F3.S1 (a): PRs 2, 3 and 4 merged, every scenario red, green, red; the free rehearsal of round 1 recorded; the real
      certification probe passes on the series host.
- [ ] F3.S1 (b): PRs 5 and 6 merged, PR 6's manifest read after PRs 2 to 5 were all on `main`; every generated file equals its
      generator's output; the two rule files hold every rule the tools implement, with no choice left open (open points 4,
      5 and 15 answered); the document gate at `proceed`; the dry run on the final harness and its pairs recorded;
      **the tag `prereg/lang-v2-<yyyymmdd>` on the remote, under the owner's go (O1), before any launch**; `prediction.md` in
      it and no `seed.txt`.
- [ ] No pull request of F3 changed a harness folder after the tag, and the series ran from its own branch, which differed
      from the tag in amendable registered files only.
- [ ] F3.S2 (a): PR 7 merged after the tag and before the pilot, its tools run over the rehearsal series first; **the pilot
      run under the owner's go (O2)**, the reserve checked before every launch and before any repeat pair; the by-hand lines,
      the GO line, the one `caps` line and the revealed seed on `main`; `derived/pilot-verdicts.json` says GO; G-17 and G-18
      read; the pilot's spend on its own lines.
- [ ] F3.S2 (b): **the rounds run under the owner's go (O3)**; **the 30 data pairs ran** (five rounds of six tasks, the
      parent's `:458`), every data session scored by the ten passes with its `record.json`, `arm` and `model`; every attempt
      kept; no round started with the round before it unscored; the cost checks T1 to T5 held for every data session, or the
      breach is in the report with what was done. A stop that ended the series leaves this box open (open point 16).
- [ ] The declared culture `de_DE.UTF-8` held in both boxes for every session, and the frozen `run.sh` started the program in
      every scored tree (the parent's `:471`–`:472`): the report's block shows it per stack from what each session stored,
      with every exception listed.
- [ ] F3.S2 (c): the read-back lines of round 1's and round 2's read-back slot on `main`, each before the slot after it ran
      (so before round 3); a refuted session re-run and read back before any later slot.
- [ ] F3.S3 (b): PR 12 merged when no launch remained; every failing or mixed case has a step-1 and a step-2 verdict;
      `adjudication/revised.json` is complete, with a row for every data session and nothing of a case; the revised reading,
      computed from it by the record tools, stands beside the primary; the report says that the adjudication's code was
      written after the data collection and its rules registered before.
- [ ] The three tools met §3's five conditions: no harness file and no registered file changed by their pull requests; each
      artefact names its tool's version and what it read; scenarios 92c to 92h green.
- [ ] F3.S3 (a): the report on `main`, generated from the data alone, `render.mjs --check` at 0; it carries per task CPC or
      the undefined outcome, for every measure the six-task median, the geometric mean and the count, the same per kind
      over its three tasks, every v1 metric, `deviations` beside `accepted`, the counts of `intermittently incorrect` and
      `unscored: environment` per stack, the claim's limit quoted from the operative part, T26's sentence, every identity,
      every attempt and the five spend lines; the olympiad notes cited as controls outside the claim.
- [ ] F3.S3 (c): the dated decision line in the parent.
- [ ] The series' verified copy is on the private backup, its identity recorded; if a disk halt freed cache folders, only
      the three listed ones of final sessions were freed, and they were restored before the final archive and the hash list.
- [ ] `research/module_lang_explore.md`, `research/module_tests.md`, `research/architecture.md` and `research/README.md`
      describe what now exists; `research/module_tests.md` lists every scenario of this document as covered, or not covered
      with a reason, and says what the harness does not prove.
- [ ] Every pull request met its row of §8, D68's whole runs among them wherever a suite's file changed; every failure on
      the way is a dated line in the leftovers plan's S5.
- [ ] No held-out content, case id or trap name in any public text: `leakcheck` and the line scan clean where §8 asks.
- [ ] `plan-lifecycle.mjs` is clean; this document's status line says what is done; any change found on the way is an entry
      in the parent, never a ledger here.
- [ ] Left to the parent's own Definition of Done, after the decision: the publication of the references and the trap table,
      the credential teardown, v1's status line, and the promotion.
