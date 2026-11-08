# PLAN — language comparison v2, F2 stories: six validation packages and one freeze — the packages, the rulesets, the seal, the tag, the workspaces and the freeze record

> Status: **plan only, 2026-11-08: F2's stories document, revised to the parent's epoch-10 revision (branch `docs/lang-v2-epoch10-revision`, `a10972a3` and after, its PR #181). Nothing of F2 is built. F2 starts after the parent's build steps 2a (F1's tail, the bench pins of FORMAT's second amendment) and 2b (`leakcheck` over both repositories). F2.S1 is a cornerstone: its spec is Fable's and is not yet copied in.** *Before this:* 2026-11-07, written when F2 started, against epoch 9.
> Epic F2 of [PLAN_lang_comparison_v2.md](PLAN_lang_comparison_v2.md) (*Operative plan for epoch 9* with its epoch-10 lines,
> *Epics in force*, F2). A stories document keeps no ledger: its changes are entries in the parent
> (`common.plan-change-control`), and writing it when the epic starts is not a change. Read at `main` `57ccc379` (#179, epic F1)
> and the revision branch above. Scope: `spikes/lang-bench/`, `spikes/lang-explore/`, the module docs, the task repository
> `dew_flow_ai_language_comparison` (its specs' freeze rows, its tag, its twelve workspaces, its two rulesets), and the private
> area `~/rsd/lang-v2/` (the sealed archives; read only otherwise, but for F2.S1).
> The form follows F1's stories document, [PLAN_lang_comparison_v2_E7_F1.md](PLAN_lang_comparison_v2_E7_F1.md).

## 1. The epic, restated (parent, F2)

**Goal.** For each of the tasks 04, 05, 06, 07, 08 and 10, one package that makes a failure adjudicable; then one freeze over the
six specs and FORMAT, which publishes the specs and their workspaces (parent F2, D69, D72, D74, D75, change 9.2).

**Needs.** F1 (merged, #179, `57ccc379`) and its tail, build step 2a: the bench's pins of FORMAT's second amendment
(`chore/lang-v2-format-d72-pins`, `2b0947ef`), merged before F2.S1's first conformance run. Build step 2b: `leakcheck` over both
repositories, with the publication-commit scan and the planted-needle check, merged before any task-repository pull request
opens (parent merge conditions 3 and 5). Neither is F2's work; F2 waits for both.

**Done when (parent).** Six packages validated and sealed; the tag exists; `leakcheck` is clean; the private aid lists nothing
uncaught. Accepted also when (parent F2.S2, epoch 10): the workspaces come from the one engine and equal, by tree hash, what a
session is launched with and restored from; the two spec pin records are cross-checked; "sealed" is one encrypted archive per
task with its hash registered; FORMAT's protection binds from B's tag; D64's demonstrations on the real frozen files before the
tag; `leakcheck` over both repositories and the candidate publication commits clean, its planted-needle check failing as it must;
the owner's go of 2026-11-08 held.

**Publication (change 9.2, D76).** The six frozen specs and their twelve workspaces go public in the task repository at the
freeze, with the tag. The references and the trap coverage table stay private until the Stage 2 decision line.

**Not in this epic.** The series over six tasks, the pre-registration, the pilot and the report (F3). The launch half of the
spec-freeze guard (T-L20) and of the workspace-commit guard (T-L67), the per-pair removal of the clear held-out folder, and the
guard that refuses a series while the memory value is not registered are F3.S1's: F2.S2 builds the checks and shows them on real
files; F3.S1 calls them. Everything of Stage 2 (D76; merge condition 4): the process-only rulesets (`run-main`), CI and the
`strict` jobs of the task repository, its root `.gitignore`, the deploy key, run bases. The olympiad tasks 01, 02, 03 and 09 are
outside the claim: no A tag is made, and their pins do not move.

## 2. What exists — verified at `main` `57ccc379`, 2026-11-07, and updated 2026-11-08

- **FORMAT's second amendment is merged** on the task repository's `main`: its #4, merge commit `c204726` (2026-11-07). The
  bench's pins that follow it are F1's tail on the local branch `chore/lang-v2-format-d72-pins` (`2b0947ef`; the parent's
  re-review record for epoch 10: `specs.d/format.json`, `specs.json`, `hashes-lang.txt` and `PINNED_TASK_SHA` at `c204726`, the
  gate record, the six quick tasks' FORMAT pins in `explore.json`). On `main` the bench still pins the first amendment
  (`spikes/lang-bench/lib/task-repo.mjs:16`, `4ddb8f80…`).
- **The checker reads only a recorded FORMAT.** `spikes/lang-bench/lib/conformance-record.mjs` (`readRecord`,
  `recordedLabel`) refuses a FORMAT whose sha256 is not an entry of `format.json`. That is why build step 2a comes before F2.S1's
  first conformance run. The freeze-tag check is on `main` (`lib/conformance-check-structure.mjs:47`): the `Freeze tag` row is
  its placeholder or `` `spec-frozen-[AB]-<yyyymmdd>` `` with a real date, and stands only beside a gated verdict.
- **The memory value is chosen, not registered.** The amended F-LIMITS-D registers the one bound only once every daemon and web
  reference passes under it in both stacks. `spikes/lang-explore/inputs/memory-limit.json` (536870912 bytes) has a note that calls
  it registered; F2.S1 fixes that note and writes the registration field (parent D72, epoch 10). F2.S2 reads that field; it does
  not write it.
- **The task repository.** `main` holds `README.md`, `LICENSE`, `todo/tasks/FORMAT.md`, `todo/tasks/01-descent-to-one.md` and
  three `.gitkeep` files (`csharp/`, `node/`, `tasks/`). The six specs are drafts on LOCAL branches (`docs/spec-04`,
  `docs/spec-05`, `docs/specs-daemon`, `docs/specs-web`), not on the remote. No tag exists (`git ls-remote --tags` is empty,
  2026-11-07). No ruleset exists (`gh api repos/…/rulesets` returned `[]`, 2026-11-07). No `.github/`, so no CI and no workflow on
  tags. Its checkout `/home/jinx/git/dew_flow_ai_language_comparison` has the push check's `pre-push` hook.
- **The rulesets' red step.** Worktree `/home/jinx/git/wt/bench-e1-s7`, branch `feat/lang-v2-e1-s7-rulesets` at `c8595952`, no
  commit, five untracked files, 570 lines: `spikes/lang-bench/github-rulesets.mjs` (3 lines, a stub that returns 3),
  `lib/github-lang.mjs` (11 lines, stubs that say NOT BUILT YET), `test/github-lang.test.mjs` (307),
  `test/github-rulesets.test.mjs` (181), `test/support/github-fixtures.mjs` (68, a fake `gh`). The data the tests ask for is
  three GitHub rulesets: `template-frozen` (`main`: `pull_request` with thread resolution, `required_linear_history`,
  `non_fast_forward`, `deletion`, no bypass), `spec-frozen-tags` (`refs/tags/spec-frozen-*`: `update`, `deletion`,
  `non_fast_forward`, no bypass) and `spec-frozen-tags-create` (the same refs: `creation`, the admin role as the only bypass).
  The last two are the tag ruleset, split because one ruleset cannot restrict updates for everyone and let the admin create.
  The group words to remove: the header comments "E1.S7 group A slice" (4 files) and the test
  `Main_has_no_required_status_checks_until_group_C` with its message "the checks join with CI in group C (OP-L)". The branch
  was 8 commits behind `main` on 2026-11-07.
- **Two workspace builders.** lang-bench's `build-workspaces.mjs` (`lib/workspace-build.mjs` `buildWorkspace`, `--group <A|B>`)
  writes the template, `PACKAGES.md`, the runner, `runner/GRAMMAR.md`, a given `STATEMENT.md` and given samples. The one engine's
  `spikes/lang-explore/lib/workspace-quick.mjs` (`quickWorkspaceFiles`) builds what a session actually gets: the same parts plus
  the marked memory and heap sentences in `STATEMENT.md` and `samples/load.json` (F1.S2, `lib/memory-limit.mjs`). The engine
  builds the files in memory from the spec and FORMAT it reads at the commits pinned in `inputs/explore.json`; it reads no
  committed workspace.
- **`workspace-diff` does not know F1.S2's per-stack lines.** `spikes/lang-bench/lib/workspace-diff.mjs` masks only the lines after
  `<!-- stack-sentence -->` and `<!-- framework-sentence -->` (`MARKERS`), and its own files per stack are the pin files, the two
  scripts and `PACKAGES.md` (`OWN_FILES`). The heap sentence (after `<!-- heap-sentence -->`) and `samples/load.json` (its
  `heap` key) differ by stack, so `workspace-diff` over the engine's workspaces would report both (read from the code; not run).
- **No freeze code.** `lib/spec-freeze.mjs`, which E1.S2 named (`specBlobMatches`, `planFreeze`), does not exist; no writer
  makes `inputs/specs.d/spec-states.json` (it is a name in `WRITER_FILES`, `lib/specs-record.mjs:11`, only); no tool writes
  `inputs/hashes-lang.txt`. What exists: `push-check.mjs --tag <name> --repo <task repo>` (`lib/push-tag.mjs`: the tag's message
  gets the body rules, and its commit must already be on a remote ref), and `fetchTaskRepo` with the two rules of T-L1
  (`lib/task-repo.mjs`).
- **The held-out side.** The one engine reads `~/rsd/lang-v2/explore/heldout-NN/cases/` in clear:
  `spikes/lang-explore/lib/heldout-quick.mjs` `casesTreeHash` (names and bytes, links refused since F1.S1 (d)); `init` records it
  per series as `heldoutQuick.treeSha256` with the case and trap counts (`lib/init-quick.mjs:41`), and `heldoutMovedProblem`
  refuses a change between pairs. v1's sealing is `spikes/minigit-bench/heldout-tool.mjs seal --src <clear dir>` over
  `lib/heldout-seal.mjs` (`sealSuite`) and `lib/heldout-crypto.mjs` (AES-256-GCM, a key file); it knows v1's suite shape. Nothing
  seals a quick task's folder today.
- **`leakcheck` scans the bench only**, through `io.repoRoot ?? REPO` (`spikes/lang-explore/lib/leak.mjs:145`). Build step 2b
  widens it to both repositories; F2 uses what 2b ships.
- **The runner's chunk parsing.** `spikes/lang-bench/runner/rs_conn.py:171` `_chunked` parses a chunked body again from its start
  after each read (carry item 5 of `~/rsd/lang-v2/briefs/s3b-carry.md`): quadratic in the number of reads. The quick run's
  largest bodies were about 1 MiB and did no harm. The runner reports no reply sizes. The runner is 0.3.12 since #179.
- **The pins of the six specs live in two places.** lang-bench's `inputs/specs.json` (the parent names it) holds no spec yet;
  lang-explore's `inputs/explore.json` holds per task the spec's commit and sha256 (`draft: true`), and that is what the engine
  reads.
- **Free disk:** 643 GB on the volume of `~/rsd` (`df -h`, 2026-11-07).

## 3. Conventions for every story

F1's §3 holds here unchanged (roles, merge conditions, red-green-red, test names, D41, shape, `-m:4`, commits, the commands,
docs), with these additions:

- **The task repository's pull requests follow merge condition 5** (epoch 10). It has no CI in Stage 1, so a task-repository
  pull request merges when its document gate is at `proceed` with every finding resolved, and the bench-side checks that read
  it (the conformance check, `leakcheck` over both repositories, `workspace-diff` and the push check where workspaces change, the
  pin checks) ran against a checkout of its head before the merge, with their commands, exits and the head sha recorded in the
  pull request. The bench pins that follow it are on a bench branch of their own, merged by merge conditions 1 to 3, and that
  bench pull request runs the same checks again against the task repository's merge commit. **No task-repository pull request
  opens before build step 2b has merged.**
- **Two repositories, two caps.** At most three open pull requests per repository (`common.pull-requests` rule 7), counted apart.
- **Nothing reaches the task repository but through a pull request,** and once (b) is applied its `main` refuses anything else.
  Its pushes go through the `pre-push` hook; a tag is checked by `push-check.mjs --tag` and pushed alone.
- **Every new check has its pair (D64):** one permitted and one refused change, shown on synthetic fixtures by a test, and
  shown again on the real frozen files by the dry run of (e) before the tag. A freeze rule without its pair blocks the freeze.
- **The commands** add, for the task repository (`TASK` a checkout of it, never a checkout someone works in):
  `node spikes/lang-bench/workspace-diff.mjs --root "$TASK"`;
  `node spikes/lang-bench/push-check.mjs --repo "$TASK" --branch <b>`; `node spikes/lang-bench/push-check.mjs --tag <t> --repo "$TASK"`;
  `node spikes/lang-bench/conformance.mjs --repo "$TASK" --task <NN-slug> --format "$TASK/todo/tasks/FORMAT.md"`;
  `python3 -I ~/rsd/lang-v2/briefs/leakscan-lines.py "$TASK" history`; and build step 2b's `leakcheck` over both repositories,
  with its publication-commit scan and its planted-needle check, by the command 2b ships.

## F2.S1 — a package per task, six · cornerstone, `model: fable`

Cornerstone: the spec (scenarios and brief) is written by Fable, at `~/rsd/lang-v2/briefs/f2s1-spec-public.md` (its private half outside git), and copied in here by the team lead.

## F2.S2 — one freeze

**Model:** scenarios + brief Opus (this document) · code Sonnet · review Opus · fixes Sonnet; the ruleset apply and the tag push
are the team lead's own (§5). **Parent:** F2.S2 with its epoch-10 lines, D41, D61, D64, D67 (2) and (3), D72, D75, D76, change 9.2;
it replaces the per-group freezes, CP3, CP4 and CP4-B. **Accepted when (parent):** the tag exists; the rulesets read back;
`leakcheck` clean; and the epoch-10 items of §1.

| Part | What | State on 2026-11-08 | Pull request (§4) |
|---|---|---|---|
| (a) | the bench's pins of FORMAT's second amendment | **not F2's**: F1's tail, build step 2a, `chore/lang-v2-format-d72-pins` | a precondition |
| (b) | the rulesets: `main`'s protection and the tag ruleset, built from the red step; applied and read back under the owner's go of 2026-11-08 | open (red step only) | bench PR 2, then the apply |
| (c) | incremental chunk parsing in `rs_conn.py`, only if a frozen body needs it | open; decided by a measurement on task 05 | bench PR 3, or a dated line |
| (d) | the freeze tools: the freeze plan, the guards, the engine's workspace writer, `workspace-diff`, the hash list, the seal and its clear folder, the held-out record | open | bench PR 4 |
| (e) | the freeze in the task repository: the seal, the dry run, the freeze commit, the tag, the twelve workspaces, the workspace commit | open; after F2.S1's six packages | task PRs T1, T2 and the tag |
| (f) | the freeze record in the bench: the spec hashes, the archives' hashes, the trap tables' hashes, the workspace commit, the moved pins | open | bench PR 5 |

### (a) the d72 pins — F1's tail, not built here

The bench's pins of FORMAT's second amendment stay F1's (parent F1.S1 (e) and *Build order* 2a, epoch 10): branch
`chore/lang-v2-format-d72-pins` (`2b0947ef`), merged by merge conditions 1 to 3 before F2.S1's first conformance run. F2 builds
nothing of it. **Precondition for F2:** on `main`, `PINNED_TASK_SHA` is `c204726…`, `format.json` records the amended FORMAT, and
`specs-record.mjs --check`, `pin-runner.mjs --check` and `pin-exec-layer.mjs --check` exit 0. Every later step of F2.S2 reads the
FORMAT that 2a records; (e) checks that the frozen FORMAT is that one.

### (b) the rulesets: `main`'s protection and the tag ruleset

**The go.** The owner's go of 2026-11-05 covers exactly these on the task repository: `template-frozen` and the tag ruleset (its
two GitHub rulesets), applied from data with a read-back. **The owner's go of 2026-11-08 sets when:** the two approved rulesets
are applied, and the tag is pushed, only once all six packages pass and a dry run is clean. Neither covers anything else
(`run-main`, the deploy key, the PAT, a later update of a ruleset: each needs its own go).

**Start.** In `/home/jinx/git/wt/bench-e1-s7`: `git fetch origin` and `git merge origin/main` (a merge, never a rebase), then
take the five files as the red step: run them once, watch every test fail for "not built", record it in a reds file under
`~/rsd/lang-v2/briefs/`, and commit the red step as its own commit. Then build. A new branch name is allowed
(`feat/lang-v2-f2s2-rulesets`) if the old one is renamed, never pushed under two names.

**Scenarios.** The red step's tests, made green, with these changes only:

- The group wording goes. `Main_has_no_required_status_checks_until_group_C` becomes
  `Main_has_no_required_status_checks_in_stage_1`, its message "the checks join with CI in Stage 2"; the header comments of the
  four files say what the file is (F2.S2, the task repository's two rulesets) with no group. No other assertion changes.
- `The_data_is_three_frozen_rulesets_…` stays: three GitHub rulesets are the two the owner approved, the tag one in two parts
  (§2). The test's comment says so, so a reader does not count three approvals.
- Nothing else is added or removed: the red step was written against the data the owner approved, and its tests are the
  scenarios.

**Brief (Sonnet coder).** `lib/github-lang.mjs`: the data as the tests state it; `rulesetProblems` and `mergeSettingsProblems`
(pure); `readRulesets` (list, then each by id, through `runBounded` of `spikes/minigit-bench/lib/run.mjs` as exe and argv, a
timeout, no shell); `applyRulesets` creates only an absent ruleset, never updates or deletes one, refuses a present different one,
and reads back; `scrub` takes a token out of every reason. `github-rulesets.mjs`: `--check` (default, read-only) and `--apply`;
exit 0 equal, 1 different or absent, 2 usage, 3 `gh` failed. The owner's `gh` login does the write; no PAT file is read and no
token is in the child's environment (the tests hold both). Docs: `research/module_lang_bench.md`, `research/module_tests.md`.

**The apply (the team lead, not a coder), in (e) step 3:** after all six packages pass and the dry run is clean, and before the
freeze commit's pull request opens: `node spikes/lang-bench/github-rulesets.mjs --apply`, then `--check`. Both exit 0, and
`--check` says `equal` for each of the three. The lines go into a dated line of `spikes/lang-bench/inputs/checkpoints.md` in bench
PR 5, with the three ruleset ids. A `--check` that is not `equal` stops the freeze. The merge settings are reported and never
change the exit.

**Done when:** merged; applied at (e) step 3; read back `equal` three times; recorded.

### (c) incremental chunk parsing, only if a frozen body needs it

**The decision, measured, not guessed.** Two numbers, both written as one dated line in `spikes/lang-bench/inputs/checkpoints.md`
(counts and times only):

1. **S** = the largest reply body, in bytes, that any visible or held-out case of the six tasks expects, as F2.S1's package
   records count it (one number per task; the brief to F2.S1's writer asks for it, §7 item 2).
2. **P** = the time the current `_chunked` takes to parse a synthetic body of S bytes sent in 4 KiB chunks, read in 64 KiB pulls
   (the runner's `CHUNK`), median of five runs on the scoring CPUs `6-7`, by a small script beside `test/runner-conn.test.mjs`.

**Build it** when S is above 1 MiB (the largest the quick run met without harm) or P is above 100 ms. Otherwise the dated line
says "not needed" with the values of S and the value of P, and nothing is built.

**If it is built — scenarios** (`test/runner-conn.test.mjs`):

- `The_incremental_parse_gives_the_same_body_and_end_as_the_whole_parse_for_every_split_point`: fixture chunked bodies (one chunk,
  many, empty last chunk with trailers) split at every byte give the same `(body, end)` as today's function on the whole buffer.
- `A_malformed_chunk_is_refused_with_the_same_message_as_before` (bad size, bad end, a size line without its line feed).
- `The_parse_work_grows_linearly_with_the_number_of_reads` (counted, not timed: a counter seam counts bytes scanned; doubling the
  reads doubles the count, within one read's size). Its control is today's function, which the same test shows growing faster.

**If it is built — brief.** `rs_conn.py`: keep the parse state (offset, chunk being read, body so far) between reads, so each
read scans only new bytes; the caller stops copying the whole buffer. Nothing else changes. The runner's version moves past
0.3.12; then, as F1.S1 (f) did: `pin-runner.mjs`, `specs-record.mjs`, the three `--check` tools, a new dated
`inputs/exec-probe-<date>.json` (no old record changes), CP2's addendum in `inputs/checkpoints.md`. **A changed runner re-runs the
D40 gate of every task validated on the old one** (parent §6.1): F2.S1's 10 of 10 runs for those tasks are re-run on the new
runner before the freeze.

**When.** The decision needs S from task 05, the first task F2.S1 takes end to end (parent *Build order* 3, epoch 10). Decide
right after task 05's package, before the other five validate, so at most task 05 re-runs. A later package whose S breaks the rule reopens the decision before
its own validation. The runner is copied into every workspace, so (c) is settled before (e) writes any.

**Done when:** the dated line exists; if built, merged with its red recorded and the re-runs done.

### (d) the freeze tools

Built in parallel with F2.S1, on synthetic fixtures only (`ownedFor({ token: 'bench' })` folders, invented specs and cases;
never a real spec draft, case, reference or private file). Merged before the dry run of (e). `leakcheck` over both repositories
is not built here: it is build step 2b's, and (e) runs it.

**One CLI for the freeze:** `spikes/lang-bench/freeze.mjs`, with these verbs; each reads, prints and exits, and only `record`
writes, one file:

| Verb | What it does | Exit |
|---|---|---|
| `plan --repo <task> --ref <commit> --date <yyyymmdd>` | prints the freeze commit's edits: in each of the six specs, the `Freeze tag` row set to `` `spec-frozen-B-<date>` ``, and nothing else; refuses first | 0 a plan, 3 refused |
| `check-commit --repo <task> --before <commit> --after <commit>` | the freeze commit changes only those six rows | 0, 1 |
| `check-tag --repo <task> --tag <t>` | the tag is annotated, named `spec-frozen-B-<yyyymmdd>` with a real date equal to every spec's row, and its commit holds the six specs and FORMAT with the sha256 the freeze commit gave them | 0, 1 |
| `check-workspace --repo <task> --tag <t> --commit <w>` | T-L67: `<w>` descends from the tag, and every spec blob and FORMAT's blob at `<w>` equal the tag's; only `csharp/<NN-slug>/**` and `node/<NN-slug>/**` of the six tasks differ | 0, 1 |
| `hashes --root <checkout at w>` | prints `inputs/hashes-lang.txt` for that tree: every file under `csharp/`, `node/`, `runner/` and `tasks/`, plus FORMAT and the six specs, `sha256  path`, sorted | 0 |
| `record --repo <task> --tag <t> --commit <w>` | writes `inputs/specs.d/spec-states.json` (below) after every check above passes | 0, 1, 3 |

**What `plan` refuses** (each a sentence naming the spec): a `Gate verdict` row that is still its placeholder; a spec whose gate
record's first line names a FORMAT record other than the amended FORMAT's; a conformance check that is not all `ok` (it calls the
checker's `run` of `lib/conformance.mjs`, never a copy); FORMAT at `<ref>` that is not the FORMAT build step 2a records; a task
list that is not exactly the six (04, 05, 06, 07, 08, 10); a `<date>` that is not today's UTC date; FORMAT's class check
(`tools/format-classes.mjs`) not clean, since FORMAT's protection binds from this tag (below).

**FORMAT's protection binds from B's tag** (parent F2.S2, epoch 10). With no group A freeze, B's is the first real freeze, so
`FORMAT-MAINTENANCE.md` §3's "after the freeze of the first group" (`spikes/lang-bench/inputs/FORMAT-MAINTENANCE.md:133`) binds
from `spec-frozen-B-<yyyymmdd>`, and the class check reads B's frozen classification. `freeze.mjs record` writes the frozen
FORMAT's sha256 as the reference that the class check reads from then on (where `tools/format-classes.mjs` today names A's
record as `--frozen`, it takes the frozen B entry). Every later FORMAT change is then a dated amendment that the class check
judges against B's frozen classes.

**`inputs/specs.d/spec-states.json`**, E1's shape where it applies (E1 § 3): per spec `{ path, sha256, state: "frozen", gate,
rIds, commitSha, tagObjectSha }` for the six, and one `freeze` entry `{ tag, tagObjectSha, commitSha, workspaceCommit,
formatSha256, hashesLangSha256 }`. The frozen sha256 differs from the gated one only by the `Freeze tag` row; `record` checks that
too. `specs-record.mjs` assembles it into `inputs/specs.json`.

**The guards for F3.S1's launch half.** `lib/spec-freeze.mjs` exports `specBlobMatches({ repo, tag, path, sha256 })` (T-L20's guard
half: the blob of the spec at the tag has the pinned sha256) and `workspaceCommitProblems({ repo, tag, commit })` (the
`check-workspace` rule). F3.S1 calls them; nothing here launches.

**The workspaces come from the one engine (D70).** A new FREE verb, `node spikes/lang-explore/explore.mjs workspaces --tag <t>
--task-repo <task> --out <dir>`, writes for the six tasks and both stacks exactly what `quickWorkspaceFiles` gives a session, with
the spec and FORMAT read at the tag, into `<dir>/csharp/<NN-slug>/` and `<dir>/node/<NN-slug>/`. It writes through lang-bench's
`checkDestination` and `writeAtomically` (`lib/workspace-destination.mjs:107,122`, already exported), so `<dir>` is refused inside
any git worktree. A second verb, `workspaces-copy --from <dir> --into <task repository worktree>`, is the separate, reviewed copy
step (E1.S3's brief): it refuses a worktree that is not clean, whose `HEAD` is not the tag's commit, or that already holds a
workspace of these tasks, and copies only those folders. `--check --root <checkout at w>` builds the files in memory again and
compares them with the committed ones, per workspace by tree hash and then byte for byte to name the file; F3.S1's `init` calls
the same function, so what an agent is launched with and restored from is what the task repository publishes (parent F2.S2,
epoch 10). `build-workspaces.mjs` refuses a task of the six and names the engine's verb, so two builders cannot write them
(`common.reuse-first`, one road in).

**`workspace-diff` learns F1.S2's lines.** `MARKERS` gains `<!-- heap-sentence -->`; `samples/load.json` may differ between the
stacks only in its `heap` key (every other key equal, compared as JSON); nothing else changes.

**"Sealed" keeps its inherited meaning (parent §4.3, §6; F2.S2, epoch 10).** One encrypted archive per task, made by v1's
`spikes/minigit-bench/heldout-tool.mjs seal` (`sealSuite`, AES-256-GCM, the key a mode-600 file outside every git tree and box),
its hash registered; cases are in clear only in a scoring temporary folder. Hashes alone would weaken it. The tool is reused, never
copied: where its shape check refuses a quick task's folder (`cases/` with `cases.txt` and pairs, `traps.txt`, `mutants/`), it is
widened by an option or a seam for that shape (`common.reuse-first`, move 1), and v1's suites keep their bytes and their tests.
lang-explore gets three FREE verbs over it:

- `explore.mjs heldout-seal --task NN` seals the task's held-out folder into `heldout-NN.enc` beside it and prints the archive's
  sha256 and the counts; it refuses to seal over an existing archive of other bytes.
- `explore.mjs heldout-open --task NN` makes **the clear working folder** from the archive: mode 700, its tree hash checked
  against the hash recorded with the archive; it refuses when the folder already exists with other bytes.
- `explore.mjs heldout-close --task NN` removes the clear working folder; the archive and its identity stay.

**The clear folder's lifecycle** (parent F2.S2 and F3.S1, epoch 10): it is mode 700; it is checked against the archive's hash at
every pair; it is removed after each pair, and removed again at the start of any run after an interruption; a check that needs
the clear cases makes it again from the archive. F2.S2 builds the three verbs and the hash check; F3.S1 wires the per-pair and
start-of-run removal into the series. The readers of the clear folder today (`loadHeldoutQuick`, `leakcheck`'s material, the line
scan) read it through `heldout-open` once the archive exists (§7 item 3).

**The held-out record (lang-explore owns it; lang-bench imports nothing of lang-explore).** A new FREE verb, `explore.mjs
heldout-freeze --tasks 04,05,06,07,08,10`, writes `spikes/lang-explore/inputs/heldout-frozen.json`: per task the archive's
sha256, the clear tree's `treeSha256` (`casesTreeHash`), `caseCount`, `trapCount`, and the sha256 of the task's trap coverage
table and of its package record (both files as F2.S1's package names them). Counts and hashes only: no id, no name, no line. It
refuses a task without an archive. `init` of a quick series refuses a clear tree or an archive whose hash differs from this file.
The memory value's registration is not here: it is the field F2.S1 writes into `memory-limit.json` (§2).

**Scenarios** (each a red first; test files beside their modules):

- `spikes/lang-bench/test/spec-freeze.test.mjs` (new; synthetic task repositories made with `git init` in owned folders):
  `The_freeze_plan_changes_only_the_freeze_tag_row_of_each_spec` (permitted) and
  `A_freeze_commit_that_changes_any_other_byte_of_a_spec_is_refused_naming_the_line` (refused);
  `A_spec_whose_gate_verdict_is_still_its_placeholder_is_refused_before_any_edit`;
  `A_spec_gated_under_another_FORMAT_record_is_refused_for_the_six`;
  `A_session_spec_whose_blob_differs_from_the_tag_is_refused_and_one_that_differs_only_in_workspace_files_is_not` (T-L20's pair);
  `A_workspace_commit_that_is_not_a_descendant_of_the_tag_is_refused`;
  `A_workspace_commit_whose_spec_blob_differs_from_the_tag_is_refused_and_one_that_adds_only_workspace_files_passes` (T-L67's pair);
  `A_lightweight_tag_or_a_tag_whose_date_differs_from_the_spec_rows_is_refused`;
  `The_hash_list_of_a_tree_lists_every_file_under_the_workspace_roots_and_fetchTaskRepo_accepts_it` and its companion
  `A_file_added_after_the_list_was_written_is_refused_by_the_fetch` (T-L1, through the real `fetchTaskRepo`).
- `spikes/lang-bench/test/format-classes-frozen.test.mjs`: `After_B_is_frozen_a_change_to_a_common_section_is_refused_and_a_change_to_a_daemon_web_section_is_permitted`
  (the pair, on a synthetic FORMAT pair, reading B's frozen classification).
- `spikes/lang-bench/test/workspace-diff.test.mjs`: `The_heap_sentence_is_a_marked_sentence_and_may_differ_by_stack`;
  `A_load_json_that_differs_in_anything_but_heap_is_a_finding`; `A_byte_changed_in_the_memory_sentence_is_still_a_finding`
  (that sentence is the same in both stacks, so the new marker must not mask it).
- `spikes/lang-bench/test/templates-build.test.mjs`: `The_lang_bench_builder_refuses_the_six_tasks_and_names_the_engine_verb`.
- `spikes/lang-explore/test/quick-freeze-workspaces.test.mjs` (new, invented specs): `The_written_workspace_equals_what_a_session_gets_byte_for_byte`;
  `A_workspace_whose_grammar_is_not_the_frozen_FORMATs_is_refused` (T-L57's freeze half: a planted grammar of another FORMAT,
  refused; the frozen FORMAT's, permitted);
  `The_check_finds_one_changed_byte_in_a_committed_workspace_and_passes_an_equal_one` (the pair);
  `The_copy_refuses_a_dirty_worktree_a_worktree_at_another_commit_and_an_existing_workspace`;
  `The_writer_refuses_a_destination_inside_a_git_worktree` (through `checkDestination`).
- `spikes/lang-explore/test/quick-heldout-seal.test.mjs` (new; an invented key in an owned folder):
  `A_sealed_quick_folder_opens_to_the_same_tree_hash`; `The_clear_folder_is_mode_700`;
  `Close_removes_the_clear_folder_and_keeps_the_archive`; `A_clear_folder_whose_hash_differs_from_the_archive_is_refused` (the
  pair: an unchanged folder passes); `Sealing_over_an_existing_archive_of_other_bytes_is_refused`;
  `The_v1_suite_seals_to_the_same_bytes_as_before` (the widened tool keeps v1's shape).
- `spikes/lang-explore/test/quick-heldout-frozen.test.mjs` (new): `The_record_holds_counts_and_hashes_and_no_case_id_or_trap_name`
  (it scans its own output for the fixture's ids and names and finds none); `Init_refuses_a_held_out_tree_or_archive_that_differs_from_the_frozen_record`
  and `Init_accepts_the_same_tree_and_archive` (the pair); `A_task_without_an_archive_or_a_package_record_is_refused`.
- `spikes/lang-explore/test/quick-freeze-consumers.test.mjs` (new; D67 (2), below): `The_frozen_record_holds_every_field_the_pre_registration_guard_will_read`,
  read through a test stub of F3.S1's guard over a synthetic record: the spec hashes and states, the tag and its commit, the
  workspace commit, the hash list's sha256, the archives' and clear trees' hashes and counts, the trap tables' hashes, the memory
  value's registration field. A field with no source fails.

**Docs.** `research/module_lang_bench.md` (§ the task repository: the freeze CLI, the hash list writer, the builder's refusal,
FORMAT's protection from B's tag), `research/module_lang_explore.md` (the workspace verbs, the seal verbs and the clear folder's
lifecycle, the held-out record), `research/module_minigit_bench.md` (the seal tool's widening, if any), `research/module_tests.md`.

**Done when:** every scenario went red, green, red; merged; nothing in it read a real spec, case or private file.

### (e) the freeze in the task repository

**Preconditions, all on `main` of their repositories:** build steps 2a and 2b; (b) merged (not yet applied); (c) decided (and
merged if built); (d); F2.S1's six packages done, all twelve references passing, the memory value registered by F2.S1's field,
each spec gated (its `Gate verdict` row filled under the amended FORMAT) and merged with its samples (`tasks/<NN-slug>/samples/`)
by merge condition 5.

1. **The seal.** For each task, `explore.mjs heldout-seal`, then `heldout-open` and `explore.mjs heldout-freeze` over the six:
   the archives exist, each clear folder opens to the hash the archive records, and `heldout-frozen.json` is written on a bench
   branch (it goes into bench PR 5). The clear folders are then closed.
2. **The dry run on the real files (D64).** In a scratch clone of the task repository outside every worktree: `freeze.mjs plan`,
   apply the plan, commit, an annotated tag made locally and **never pushed**, `explore.mjs workspaces` and `workspaces-copy`, a
   second commit, then every check of steps 4 to 6, including each guard's refused case on a planted change in a copy of that
   clone (a one-word edit in a spec, a changed byte in a workspace, a changed held-out file in a clear folder opened from a copy
   of the archive, a grammar of another FORMAT in a workspace, a change to a `common` section of FORMAT under the class check),
   and build step 2b's `leakcheck` over both repositories with the scratch clone's two commits as the candidate publication
   commits, its planted-needle check failing as it must. An Opus judge reads the output against this list (it replaces CP4-B's
   reading); the result is one dated line in `spikes/lang-bench/inputs/checkpoints.md` (counts and verdicts, no content), in bench
   PR 5. A failure is fixed in the tools or a spec's package, and the dry run runs again, before anything is pushed. The scratch
   clone is removed afterwards. **A clean dry run with all six packages passing is the condition of the owner's go of
   2026-11-08.**
3. **The rulesets, under the owner's go of 2026-11-08** (the team lead): (b)'s apply and read-back, `equal` three times, recorded.
   From here on, `main` takes only pull requests and no `spec-frozen-*` tag can be moved or deleted.
4. **T1, the freeze commit.** Branch `freeze/spec-frozen-B-<date>` in a worktree of the task repository: `freeze.mjs plan` and its
   edits only. Merge condition 5: a document gate at `proceed` over the six changed rows; before the merge, against a checkout of
   the head: conformance of all six against the frozen FORMAT, all `ok`; `freeze.mjs check-commit`; `push-check --branch`
   (through the hook on push); 2b's `leakcheck` over both repositories with this branch's commit as the candidate publication
   commit; the line scan on the task repository; each command, exit and the head sha recorded in the pull request. Merged by
   squash: the merge commit is **C**.
5. **The tag, under the owner's go of 2026-11-08.** It cannot be undone. `git tag -a spec-frozen-B-<date> C` with a message that
   names the six tasks by number and nothing else; `push-check.mjs --tag`; the tag pushed **alone** with the owner's login (the
   creation ruleset's only bypass is the admin role); `git ls-remote --tags origin` shows it, and `freeze.mjs check-tag` exits 0.
   No workflow listens for tags there, so no run is awaited (`common.task-lifecycle` §3 checked: nothing to verify but the ref).
   FORMAT's protection binds from this tag.
6. **T2, the workspaces.** `explore.mjs workspaces --tag <t>` into an owned folder outside every worktree; `workspace-diff --root`
   over it; then `workspaces-copy --into` a worktree of the task repository at C, branch `workspaces/spec-frozen-B-<date>`. Merge
   condition 5: a document gate at `proceed` over the statements; before the merge, against a checkout of the head:
   `workspace-diff --root`; `explore.mjs workspaces --check --root`; `push-check --branch`; 2b's `leakcheck` over both repositories
   with this branch's commit as the candidate publication commit; the line scan; recorded in the pull request. Merged by squash:
   the merge commit is the **workspace commit W**. Then `freeze.mjs check-workspace --tag <t> --commit W` exits 0 on a fresh fetch.

**Done when:** the archives exist and their hashes are written; the dry run's line is recorded; the rulesets read back; the tag
is on the remote and checks; W is on the task repository's `main` and checks.

### (f) the freeze record in the bench

**Brief (Sonnet coder).** Branch `feat/lang-v2-f2s2-freeze-record`:

1. `freeze.mjs record --tag <t> --commit W` writes `inputs/specs.d/spec-states.json`; `specs-record.mjs` assembles `inputs/specs.json`.
2. `freeze.mjs hashes --root <checkout at W>` writes `inputs/hashes-lang.txt`; `PINNED_TASK_SHA` becomes W (D67 (3): the bare
   harness fetches at the workspace commit, never at the tag).
3. `spikes/lang-explore/inputs/heldout-frozen.json` from (e) step 1.
4. `spikes/lang-explore/inputs/explore.json`: the six tasks' `spec` pins move to C with their frozen sha256, `draft: false`, and
   `quick.reason` gets one dated sentence. A test holds them equal to `spec-states.json`:
   `The_engine_pins_of_the_six_specs_equal_the_freeze_record` (`spikes/lang-explore/test/quick-workspace.test.mjs`), because the
   spec pins otherwise live in two places (§2; parent F2.S2, epoch 10: derived from one source, or a test cross-checks them).
5. The class check's frozen reference is the frozen FORMAT (d).
6. `inputs/checkpoints.md`: the dated lines of (b)'s read-back, (c)'s decision if not already there, and (e)'s dry run.
7. Docs: `research/module_lang_bench.md` and `research/module_lang_explore.md` name the tag, C, W and the records; this document's
   status line; the parent's status line names the freeze.

This is the bench pins pull request of merge condition 5 for T1 and T2: it runs their checks again against C and W.

**Done when:** merged; `specs-record.mjs --check`, `pin-runner.mjs --check`, `pin-exec-layer.mjs --check` exit 0 on `main`;
`fetchTaskRepo` at W verifies; `explore.mjs workspaces --check --root` passes at W.

### The freeze rules and their pairs (D64)

| Rule | Refused | Permitted | Where shown |
|---|---|---|---|
| the freeze commit (D21) | any byte of a spec but its `Freeze tag` row | that row alone | (d) test; (e) dry run |
| the spec-freeze guard (T-L20, guard half) | a spec blob that differs from the tag's | a difference outside the spec blob | (d) test; (e) dry run |
| the workspace commit (T-L67) | not a descendant, or a spec or FORMAT blob that differs | only the six tasks' workspace folders added | (d) test; (e) dry run |
| the grammar per task (T-L57) | a workspace grammar of another FORMAT | the frozen FORMAT's grammar | (d) test; (e) dry run |
| the engine match (D67 (3)) | one changed byte in a committed workspace | equal bytes | (d) test; (e) dry run |
| the seal and the held-out record | a clear tree or an archive whose hash differs | the same tree and archive | (d) test; (e) dry run on a copy of the archive |
| FORMAT's protection from B's tag | a change to a `common` or `olympiad` section | a change to a `daemon-web` section | (d) test; (e) dry run |
| the fetch (T-L1) | a file off the hash list | the listed tree | (d) test; (f) at W |
| the leak check (merge condition 3) | an invented needle planted in a temporary task repository | the real candidate commits, clean | build step 2b's test; (e) dry run |
| the tag ruleset | an update or a deletion of a `spec-frozen-*` tag | the admin's creation | the read-back of (b); the creation in (e) step 5; the refused write: §7 item 1 |
| `main`'s protection | a push that is not a pull request | a pull request merged by squash | the read-back of (b); T1 and T2 |

### Every consumer of the frozen bytes (D67 (2))

| Frozen bytes | Consumer | Built or shown |
|---|---|---|
| the six specs at the tag | the engine's statement (`quickWorkspaceFiles`), through `explore.json`'s pins | built (F1); pins moved in (f) |
| the six specs at the tag | the checker (`conformance.mjs`) | built (E1.S8) |
| the six specs at the tag | F3.S1's guard (`specBlobMatches`) | built in (d); the launch half in F3.S1 |
| FORMAT at the tag | `runner/GRAMMAR.md` of each workspace; the checker's record; the class check | built (E1.S9, F1, 2a); the frozen reference in (d) and (f) |
| the workspaces at W | the engine's match check; `fetchTaskRepo` and `hashes-lang.txt`; `workspace-diff`; the push check | built in (d), existing |
| the archives and the clear trees | `heldout-open`; `init` and every pair (`heldoutMovedProblem`); F3.S1's guard and its per-pair removal | built in (d); the series half in F3.S1 |
| the trap tables' hashes | F3.S3's record (defects counted per trap class) | named; shown on a synthetic record in (d); built in F3.S3 |
| `spec-states.json`, `heldout-frozen.json`, `memory-limit.json`'s registration field | F3.S1's pre-registration guard | shown on a synthetic record in (d); built in F3.S1 |

The task repository's root `runner/` (§5 of the parent) has one consumer, CI, which is Stage 2's; F2.S2 does not write it, and
the hash list holds no entry for it.

## 4. The order of the pull requests

The five merge conditions are the parent's (*Merge conditions*, with epoch 10): **(1)** the gates on harness code: a coai plan
round at `proceed` before the code (for code already written, the round reads its brief as the scope), then a coai code round and
an Opus review on the committed branch, every finding resolved; **(2)** D68's whole runs of every suite touched, plain and with
`BENCH_REQUIRE_SANDBOX=1`, every test passed and nothing skipped, and CI's required checks present and passing at the head sha;
**(3)** D41: the line scan before every merge, and `leakcheck` when a spec, a sample, a package, a document under
`research/lang-v2/` or an agent-visible file is touched, over both repositories and the candidate publication commits, **and at
the freeze**; **(4)** no Stage 2 code; **(5)** a task-repository pull request: its document gate at `proceed`, the bench-side
checks run against a checkout of its head and recorded in it, the bench pins after it on a bench branch of their own.
`plan-lifecycle.mjs` clean on every bench pull request. Each bench pull request merges `main` before its last whole run, so the
run is about the head that merges.

| # | Repo | Items | Branch | What it runs before it merges | Which conditions bite |
|---|---|---|---|---|---|
| 1 | bench | this document; its `todo/README.md` row | `feat/lang-v2-f2` | line scan; `plan-lifecycle`; CI; an Opus reading by the team lead | (2) CI only, (3) line scan, (4) |
| 2a | bench | F1's tail: the d72 pins | `chore/lang-v2-format-d72-pins` | F1's; the parent's build step 2a | (1) to (3), as the parent says |
| 2b | bench | `leakcheck` over both repositories | the parent's build step 2b | the parent's | (1) to (4) |
| — | task | F2.S1's six spec pull requests, from task 05 | F2.S1's | F2.S1's, by merge condition 5 | (3), (5) |
| 2 | bench | F2.S2 (b), the rulesets | `feat/lang-v2-f2s2-rulesets` | the gates; lang-bench whole runs, both ways; line scan. The apply waits for (e) step 3 | (1), (2), (3) line scan only: nothing agent-visible; (4): `run-main` and CI's checks are not in the data |
| 3 | bench | F2.S2 (c), only if built | `fix/lang-v2-f2s2-chunks` | the gates; the runner re-pin; the probe re-run; lang-bench and lang-explore whole runs, both ways; the three `--check` tools; `push-check --self`; `leakcheck`; line scan | (1), (2), (3) with `leakcheck`: the runner is agent-visible; (4) |
| 4 | bench | F2.S2 (d), the freeze tools | `feat/lang-v2-f2s2-freeze-tools` | the gates; lang-bench, lang-explore and minigit-bench (if the seal tool is widened) whole runs, both ways; `push-check --self`; `leakcheck`; line scan | (1), (2), (3) with `leakcheck`: it changes how the held-out side is read; (4) |
| T1 | task | (e) step 4, the freeze commit | `freeze/spec-frozen-B-<date>` | merge condition 5: the document gate; conformance of the six; `check-commit`; `push-check --branch`; `leakcheck` over both repositories with the candidate commit; line scan; recorded in the pull request | (3) at the freeze, (5) |
| tag | task | (e) step 5 | — | the owner's go of 2026-11-08 (six packages pass, the dry run clean); `push-check --tag`; `check-tag` after the push | (3): the tag's message is public text |
| T2 | task | (e) step 6, the workspaces | `workspaces/spec-frozen-B-<date>` | merge condition 5: the document gate; `workspace-diff --root`; `workspaces --check`; `push-check --branch`; `leakcheck` over both repositories with the candidate commit; line scan; recorded; then `check-workspace` | (3) at the freeze, (5) |
| 5 | bench | F2.S2 (f), the freeze record (the bench pins of T1 and T2) | `feat/lang-v2-f2s2-freeze-record` | the gates (a code round over the record's tool runs); lang-bench and lang-explore whole runs, both ways, `LANG_V2_TASK_REPO` at W; the three `--check` tools; `fetchTaskRepo` at W; `leakcheck` over both repositories; line scan; `plan-lifecycle` | (1) to (5) |

**Order and why.** PR 1 first, so every brief cites a merged document. Then the parent's 2a (before F2.S1's first conformance run:
the checker reads only a recorded FORMAT) and 2b (before any task-repository pull request). F2.S1 takes task 05 end to end first;
(c)'s decision comes right after task 05's package, before the other five validate and before T2 (the runner is in every
workspace). PR 2 and PR 4 are built in parallel with F2.S1; PR 4 merges before the seal and the dry run. Then, strictly in this
order: the seal, the dry run, the rulesets applied (the owner's go needs all six packages and a clean dry run), T1, the tag, T2,
PR 5. The bench's cap of three open pull requests holds throughout: PRs 2, 3 and 4 can be open together only while none of
F2.S1's bench pull requests is open; otherwise land one first.

## 5. The model per role

| Role | Model | In F2 |
|---|---|---|
| Stories, scenarios and briefs | opus | this document, for F2.S2 |
| Cornerstone spec | fable | F2.S1's scenarios and brief (`~/rsd/lang-v2/briefs/f2s1-spec-public.md`, its private half outside git), copied in by the team lead; Opus stands in only if Fable refuses on a limit, and the summary says so |
| Coder | sonnet | the code and tests of PRs 2 to 5, and the fixes after review |
| Small edit | main session or sonnet | one file, at most 50 changed lines, bounded before the first edit: (c)'s dated "not needed" line qualifies; every pull request above spans more files and does not |
| Reviewer and judge | opus | the review of every pull request, beside the coai code round; the reading of (e)'s dry run; the verdict on whether a finding changes a story |
| Scout | sonnet | evidence only, such as S for (c) from the package records, or the remote state of the task repository |
| The team lead (main session) | — | the seal (it handles the key), the ruleset apply and read-back and the tag push under the owner's go of 2026-11-08; never a coder |

The cornerstone ledger does not change: the parent's line reads *Cornerstones: 2 of 2 — F1.S2, F2.S1*, and this document marks
only F2.S1.

## 6. Growth

- **The task repository:** twelve workspaces, each the template (2 KB for C#, up to 81 KB for Node with its lock), the runner
  (about 0.3 MB) and the statement and samples: about 5 MB in all, once. Kept for ever: it is the frozen input. A second freeze
  would be a new tag and a new workspace commit, never an edit.
- **The archives:** six, one per task, each about the size of its held-out cases, under `~/rsd/lang-v2/explore/`, outside git.
  Kept for ever, append only (D34); the key stays a mode-600 file outside every git tree and box. **The clear working folders:**
  at most six, mode 700, removed after each pair and at the start of any run after an interruption (F3.S1), made again from the
  archive when needed; a killed run leaves at most one set, which that rule removes.
- **The fetch cache:** `~/.cache/lang-v2/<sha>/`, one folder per pinned sha, a few MB each (3.7 MB on 2026-11-07); `c204726` and W
  add two. Kept, since each names a pin that a record cites.
- **The scratch folders:** the dry run's clone and the engine's write folder are owned folders (`ownedFor`), removed when the step
  ends; a killed run's folder is swept by the owner rules (`common.test-leftovers`).
- **The bench records:** `spec-states.json`, `heldout-frozen.json`, the hash list and a few dated lines: kilobytes, append only.
- F2.S1's growth (references, defect witnesses, package records) is its spec's.

## 7. Open points found while writing, for the team lead

1. **The tag ruleset's refused change has no safe live demonstration.** After (b), no one can move or delete a `spec-frozen-*`
   tag. D64 wants a refused change shown on the real thing; a real refused write would be a forced push or a deletion attempt
   against the public repository, which this family's tools refuse to send. This document shows the rule by the read-back and the
   creation. Whether the owner wants the refused write tried by hand is theirs.
2. **F2.S1's package record should carry two things for F2.S2:** the largest expected reply body per task (S, for (c)), and the
   name of its trap coverage table file (for `heldout-frozen.json`). F2.S1's spec is not copied in yet; this is for its writer.
3. **Who reads the clear held-out folder after the seal.** `leakcheck`'s material (2b), the line scan (`leakscan-lines.py`, outside
   git) and `loadHeldoutQuick` read `~/rsd/lang-v2/explore/heldout-NN/` in clear today. Once that folder lives only between
   `heldout-open` and `heldout-close`, each of them must open it first, or find nothing to read and report clean. 2b's tool and the
   line scan need that line; this document gives the verbs, not their callers' change.
4. **v1's seal tool may not take a quick task's folder as it is.** `heldout-tool.mjs seal` knows v1's suite shape (registry, runner
   protocol). (d) widens it rather than copying it; how much widening it needs is found by its first red test.

## 8. Definition of Done (F2)

- [ ] F2.S1: done as its spec says (the parent's acceptance: per task, the package record in the private area and a public line
      of counts only; all twelve references pass; the memory value registered by its field).
- [ ] F2.S2 (a): not F2's; build step 2a merged before F2.S1's first conformance run, checked.
- [ ] F2.S2 (b): merged; the two rulesets (three GitHub rulesets) applied under the owner's go of 2026-11-08 and read back
      `equal`, recorded.
- [ ] F2.S2 (c): the decision line exists with its numbers; if built, merged and the re-runs done.
- [ ] F2.S2 (d): the freeze tools merged, every scenario red, green, red.
- [ ] F2.S2 (e): six sealed archives; the dry run recorded and read by an Opus judge; `spec-frozen-B-<yyyymmdd>` on the remote,
      annotated, over the six specs and FORMAT; W on the task repository's `main`; `workspace-diff` and the push check clean;
      T1 and T2 merged by merge condition 5.
- [ ] F2.S2 (f): the spec hashes in `inputs/specs.json`; the archives', clear trees' and trap tables' hashes registered; the
      workspace commit named; `PINNED_TASK_SHA` at W; the engine's pins at the tag and cross-checked; FORMAT's class check reads
      B's frozen classification.
- [ ] `leakcheck` over both repositories and the candidate publication commits clean, its planted-needle check failing as it must;
      the line scan clean on both; the private aid lists nothing uncaught (F2.S1).
- [ ] The specs and workspaces are public at the freeze; the references and the trap coverage table are not (change 9.2).
- [ ] Every pull request met its row of §4; every failure on the way is a dated line in the leftovers plan's S5.
- [ ] No held-out content, case id or trap name in any public text.
- [ ] `plan-lifecycle.mjs` is clean; this document's status line says what is done; any change found on the way is an entry in
      the parent, never a ledger here.
