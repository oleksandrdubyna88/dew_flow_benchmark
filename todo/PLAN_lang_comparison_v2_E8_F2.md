# PLAN — language comparison v2, F2 stories: six validation packages and one freeze — the packages, the pins, the rulesets, the tag, the workspaces and the freeze record

> Status: **plan only, 2026-11-07: F2's stories document, written when F2 starts (the parent's epoch 9 is open, F1 merged as #179). Nothing of F2 is built. F2.S1 is a cornerstone: its spec is Fable's and is not yet copied in. F2.S2 (a) waits for the task repository's PR #4 (FORMAT's second amendment), which is open.**
> Epic F2 of [PLAN_lang_comparison_v2.md](PLAN_lang_comparison_v2.md) (*Operative plan for epoch 9*, *Epics in force*, F2). A
> stories document keeps no ledger: its changes are entries in the parent (`common.plan-change-control`), and writing it when
> the epic starts is not a change. Read at `main` `57ccc379` (#179, epic F1). Scope: `spikes/lang-bench/`,
> `spikes/lang-explore/`, the module docs, the task repository `dew_flow_ai_language_comparison` (its specs' freeze rows, its
> tag, its twelve workspaces, its two rulesets), and the private area `~/rsd/lang-v2/` (read only, but for F2.S1).
> The form follows F1's stories document, [PLAN_lang_comparison_v2_E7_F1.md](PLAN_lang_comparison_v2_E7_F1.md).

## 1. The epic, restated (parent, F2)

**Goal.** For each of the tasks 04, 05, 06, 07, 08 and 10, one package that makes a failure adjudicable; then one freeze over the
six specs and FORMAT (parent F2, D69, D72, D74, D75).

**Needs.** F1 (merged, #179, `57ccc379`). One piece of F1 is not on `main`: FORMAT's second amendment and the pins that follow
it (F1.S2 (b)). It is built here as F2.S2 (a), first (§2, §7 item 1).

**Done when (parent).** Six packages validated and sealed; the tag exists; `leakcheck` is clean; the private aid lists nothing
uncaught.

**Not in this epic.** The series over six tasks, the pre-registration, the pilot and the report (F3). The launch half of the
spec-freeze guard (T-L20) and of the workspace-commit guard (T-L67) are F3.S1's: F2.S2 builds the checks and shows them on
real files; F3.S1 calls them before a launch. Everything of Stage 2 (D76; merge condition 4): the process-only rulesets
(`run-main`), CI and the `strict` jobs of the task repository, its root `.gitignore`, the deploy key, run bases. The olympiad
tasks 01, 02, 03 and 09 are outside the claim: no A tag is made, and their pins do not move.

## 2. What exists — verified 2026-11-07 at `main` `57ccc379`

- **FORMAT's second amendment is not merged.** The task repository's PR #4 (`docs/format-amend-d72`, head `68c181e`, 3
  commits, one file `todo/tasks/FORMAT.md`, 3 lines added and 2 removed) is `OPEN` (`gh pr view 4`, 2026-11-07). Its `main` is
  `4ddb8f8` (PR #3). The bench still pins the first amendment: `PINNED_TASK_SHA` `4ddb8f80…` (`spikes/lang-bench/lib/task-repo.mjs:16`),
  FORMAT's line in `spikes/lang-bench/inputs/hashes-lang.txt:4` (`06c36608…`), the one entry `A` of
  `spikes/lang-bench/inputs/specs.d/format.json` (tasks 01, 02, 03, 09), and the six quick tasks' `format` pins in
  `spikes/lang-explore/inputs/explore.json` (all `4ddb8f8`, label `v2, first amendment`). F1.S2 (b) owned this move (F1's
  stories document, *FORMAT's second amendment (D72, part (b))*); #179 merged F1 without it.
- **The checker reads only a recorded FORMAT.** `spikes/lang-bench/lib/conformance-record.mjs` (`readRecord`,
  `recordedLabel`) refuses a FORMAT whose sha256 is not an entry of `format.json`. So no spec can be checked against the
  amended FORMAT until (a) records it. That is why (a) comes first in F2.
- **The amended F-LIMITS-D** (PR #4's diff) says the one bound is registered before the freeze, "only once the reference
  solutions of every daemon and web task pass every test under it in both stacks". The value itself is already written:
  `spikes/lang-explore/inputs/memory-limit.json` (536870912 bytes, `decidedOn` 2026-11-07, D72).
- **The task repository.** `main` holds `README.md`, `LICENSE`, `todo/tasks/FORMAT.md`, `todo/tasks/01-descent-to-one.md` and
  three `.gitkeep` files (`csharp/`, `node/`, `tasks/`). The six specs are drafts on LOCAL branches (`docs/spec-04`,
  `docs/spec-05`, `docs/specs-daemon`, `docs/specs-web`), not on the remote. No tag exists (`git ls-remote --tags` is empty).
  No ruleset exists (`gh api repos/…/rulesets` returns `[]`). No `.github/`, so no CI and no workflow on tags. Its checkout
  `/home/jinx/git/dew_flow_ai_language_comparison` has the push check's `pre-push` hook.
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
  is `c8595952`, 8 commits behind `main`.
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
  `inputs/hashes-lang.txt`. What exists: the checker's freeze-row rules (`lib/conformance-check-structure.mjs`: the `Freeze tag`
  row is its placeholder or `` `spec-frozen-[AB]-<yyyymmdd>` `` with a real date, and stands only beside a gated verdict),
  `push-check.mjs --tag <name> --repo <task repo>` (`lib/push-tag.mjs`: the tag's message gets the body rules, and its commit must
  already be on a remote ref), and `fetchTaskRepo` with the two rules of T-L1 (`lib/task-repo.mjs`).
- **The held-out side's identity.** `spikes/lang-explore/lib/heldout-quick.mjs` `casesTreeHash` (names and bytes of `cases/`, links
  refused since F1.S1 (d)); `init` records it per series as `heldoutQuick.treeSha256`, with the case and trap counts
  (`lib/init-quick.mjs:41`), and `heldoutMovedProblem` refuses a change between pairs. Nothing registers it for all series.
- **`leakcheck` scans the bench only.** `leakcheckCommand` (`spikes/lang-explore/lib/leak.mjs:145`) scans `io.repoRoot ?? REPO`,
  the bench repository; the frozen specs, samples and workspaces will live in the task repository, which it does not read. The
  seam `io.repoRoot` exists; the runner and grammar lines that exempt a line are read from the same root
  (`committedRunnerLines`, `publicLines`).
- **The runner's chunk parsing.** `spikes/lang-bench/runner/rs_conn.py:171` `_chunked` parses a chunked body again from its start
  after each read (carry item 5 of `~/rsd/lang-v2/briefs/s3b-carry.md`): quadratic in the number of reads. The quick run's
  largest bodies were about 1 MiB and did no harm. The runner reports no reply sizes.
- **The pins of the six specs live in two places.** lang-bench's `inputs/specs.json` (the parent names it) holds no spec yet;
  lang-explore's `inputs/explore.json` holds per task the spec's commit and sha256 (`draft: true`), and that is what the engine
  reads.
- **Free disk:** 643 GB on the volume of `~/rsd` (`df -h`, 2026-11-07).

## 3. Conventions for every story

F1's §3 holds here unchanged (roles, merge conditions, red-green-red, test names, D41, shape, `-m:4`, commits, the commands,
docs), with these additions:

- **The task repository has no CI.** For its pull requests, "checks present and passing" means the checks this document lists
  for that pull request, run by hand at its head sha and pasted into its description as observed output. Its pushes go through
  the `pre-push` hook (`push-check.mjs --install-hook`); a tag is checked by `push-check.mjs --tag` and pushed alone.
- **Two repositories, two caps.** At most three open pull requests per repository (`common.pull-requests` rule 7), counted apart.
- **Nothing reaches the task repository but through a pull request,** and after F2.S2 (b) its `main` refuses anything else.
- **Every new check has its pair (D64):** one permitted and one refused change, shown on synthetic fixtures by a test, and
  shown again on the real files by the dry run of (e) before the real freeze. A freeze rule without its pair blocks the freeze.
- **The commands** add, for the task repository (`TASK` a checkout of it, never a checkout someone works in):
  `node spikes/lang-bench/workspace-diff.mjs --root "$TASK"`;
  `node spikes/lang-bench/push-check.mjs --repo "$TASK" --branch <b>`; `node spikes/lang-bench/push-check.mjs --tag <t> --repo "$TASK"`;
  `node spikes/lang-bench/conformance.mjs --repo "$TASK" --task <NN-slug> --format "$TASK/todo/tasks/FORMAT.md"`;
  `python3 -I ~/rsd/lang-v2/briefs/leakscan-lines.py "$TASK" history`; and, from (d) on,
  `node spikes/lang-explore/explore.mjs leakcheck --repo "$TASK"`.

## F2.S1 — a package per task, six · cornerstone, `model: fable`

Cornerstone: the spec (scenarios and brief) is written by Fable, at `~/rsd/lang-v2/briefs/f2s1-spec-public.md` (its private half outside git), and copied in here by the team lead.

## F2.S2 — one freeze

**Model:** scenarios + brief Opus (this document) · code Sonnet · review Opus · fixes Sonnet; the ruleset apply and the tag push
are the team lead's own (§5). **Parent:** F2.S2, D41, D61, D64, D67 (2) and (3), D72, D75; it replaces the per-group freezes,
CP3, CP4 and CP4-B. **Accepted when (parent):** the tag exists; the rulesets read back; `leakcheck` clean.

| Part | What | State on 2026-11-07 | Pull request (§4) |
|---|---|---|---|
| (a) | the bench's pins follow FORMAT's second amendment; `format.json` records it as `B` for the six | open; waits for task PR #4 | bench PR 2 |
| (b) | the rulesets: `main`'s protection and the tag ruleset, built from the red step, then applied and read back | open (red step only) | bench PR 3, then the apply |
| (c) | incremental chunk parsing in `rs_conn.py`, only if a frozen body needs it | open; decided by a measurement | bench PR 4, or a dated line |
| (d) | the freeze tools: the freeze plan, the guards, the engine's workspace writer, `workspace-diff`, the hash list, the held-out record, `leakcheck --repo` | open | bench PR 5 |
| (e) | the freeze in the task repository: the freeze commit, the tag, the twelve workspaces, the workspace commit | open; after F2.S1's six packages | task PRs T1, T2 and the tag |
| (f) | the freeze record in the bench: the spec hashes, the held-out hashes, the trap tables' hashes, the workspace commit, the moved pins | open | bench PR 6 |

### (a) the pins follow FORMAT's second amendment

This is F1.S2 (b)'s remaining half: the parent's text gives it to F1.S2, and #179 merged without it (§2). It is built here, as
F2's first code pull request, because F2.S1 cannot run its conformance checks against the amended FORMAT before it lands. F1's
done-when holds only once it has merged (§7, item 1).

**Precondition.** The task repository's PR #4 is merged on its `main` (merge commit `M`). Check before starting:
`git -C "$TASK" fetch origin && git -C "$TASK" log --oneline -3 origin/main`. If it is not merged, (a) waits; nothing here is
built against an unmerged FORMAT.

**Scenarios.**

- `The_format_record_names_group_B_for_the_six_daemon_and_web_tasks_and_keeps_A_for_the_four` (`test/specs-record.test.mjs`):
  `format.json` holds `A` unchanged (the first amendment, tasks 01, 02, 03, 09) and `B` (the amended FORMAT's sha256, its grammar's
  sha256, its gate file, tasks 04, 05, 06, 07, 08, 10); `specs.json` is their assembly. B was these six tasks' group under D44,
  and the checker and FORMAT allow only A and B (parent F2.S2), so the letter stays.
- `A_daemon_spec_checked_against_the_amended_FORMAT_prints_record_B` (`test/conformance-batch3b.test.mjs`, with
  `LANG_V2_TASK_REPO` at `M`): the checker's first line names `record B`; the same synthetic spec against a FORMAT recorded nowhere
  is exit 4, as today.
- The real-FORMAT cases of T-L56 and T-L58 run with `LANG_V2_TASK_REPO` at `M` and pass. T-L56 compares classes against A's
  FORMAT: see §7, item 6, before reading a finding there as a refusal.
- `The_engine_pins_the_six_tasks_to_the_FORMAT_that_format_json_records_as_B` (`spikes/lang-explore/test/quick-workspace.test.mjs`):
  each of the six `format` entries of `explore.json` has `B`'s sha256, so the one engine and the checker read one FORMAT.

**Brief (Sonnet coder).**

1. `spikes/lang-bench/lib/task-repo.mjs`: `PINNED_TASK_SHA` becomes `M`; its comment says "FORMAT v2 with its second amendment,
   gated". `inputs/hashes-lang.txt`: FORMAT's line gets the new sha256 (the three `.gitkeep` lines stay).
2. `inputs/specs.d/format.json` gains `B` beside `A`, in `A`'s shape (`version`, `sha256`, `gate`, `grammarSha256`, `tasks`).
   The grammar sha256 is the extractor's output over the amended FORMAT (`tools/grammar-extract.mjs`), never typed. Then
   `node spikes/lang-bench/specs-record.mjs` writes `inputs/specs.json`; never by hand.
3. `inputs/gates/FORMAT-v2.md` appends PR #4's document-gate rounds (verdicts, reviewer counts, findings taken and declined), in the
   form of its earlier lines; nothing above them changes.
4. `spikes/lang-explore/inputs/explore.json`: the six quick tasks' `format` entries move to `M` with `B`'s sha256 and the label
   `v2, second amendment`; `quick.reason` gets one dated sentence, as F1.S1 (e) wrote its own. The specs stay at their drafts here;
   (f) moves them to the tag.
5. Sweep for the old pin over `spikes/` (`4ddb8f80`, `06c36608`): only append-only records and the docs may still name it.
6. Docs: `research/module_lang_bench.md` (the pins and the B entry), `research/module_lang_explore.md` (the quick tasks' FORMAT
   pins, beside F1.S1 (e)'s paragraph), `research/module_tests.md`.

**Done when:** merged; `specs-record.mjs --check`, `pin-runner.mjs --check` and `pin-exec-layer.mjs --check` exit 0 on `main`;
the checker prints `record B` for a daemon or web spec. Only then does F2.S1 check a spec against the amended FORMAT.

### (b) the rulesets: `main`'s protection and the tag ruleset

The owner's go of 2026-11-05 covers exactly these on the task repository: `template-frozen` and the tag ruleset (its two GitHub
rulesets), applied from data with a read-back once the code merges. It covers nothing else (`run-main`, the deploy key, the PAT,
a later update of a ruleset: each needs its own go).

**Start.** In `/home/jinx/git/wt/bench-e1-s7`: `git fetch origin` and `git merge origin/main` (a merge, never a rebase), then
take the five files as the red step: run them once, watch every test fail for "not built", record it in a reads file under
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

**After the merge (the team lead, not a coder).** `node spikes/lang-bench/github-rulesets.mjs --apply`, then `--check`. Both
exit 0, and `--check` says `equal` for each of the three. The lines go into a dated line of `spikes/lang-bench/inputs/checkpoints.md`
in bench PR 6, with the three ruleset ids. A `--check` that is not `equal` stops the freeze. The merge settings are reported and
never change the exit.

**Done when:** merged; applied; read back `equal` three times; recorded.

### (c) incremental chunk parsing, only if a frozen body needs it

**The decision, measured, not guessed.** Two numbers, both written as one dated line in `spikes/lang-bench/inputs/checkpoints.md`
(counts and times only):

1. **S** = the largest reply body, in bytes, that any visible or held-out case of the six tasks expects, as F2.S1's package
   records count it (one number per task; the brief to F2.S1's writer asks for it, §7 item 4).
2. **P** = the time the current `_chunked` takes to parse a synthetic body of S bytes sent in 4 KiB chunks, read in 64 KiB pulls
   (the runner's `CHUNK`), median of five runs on the scoring CPUs `6-7`, by a small script beside `test/runner-conn.test.mjs`.

**Build it** when S is above 1 MiB (the largest the quick run met without harm) or P is above 100 ms. Otherwise the dated line
says "not needed" with the six values of S and the value of P, and nothing is built.

**If it is built — scenarios** (`test/runner-conn.test.mjs`):

- `The_incremental_parse_gives_the_same_body_and_end_as_the_whole_parse_for_every_split_point`: fixture chunked bodies (one chunk,
  many, empty last chunk with trailers) split at every byte give the same `(body, end)` as today's function on the whole buffer.
- `A_malformed_chunk_is_refused_with_the_same_message_as_before` (bad size, bad end, a size line without its line feed).
- `The_parse_work_grows_linearly_with_the_number_of_reads` (counted, not timed: a counter seam counts bytes scanned; doubling the
  reads doubles the count, within one read's size). Its control is today's function, which the same test shows growing faster.

**If it is built — brief.** `rs_conn.py`: keep the parse state (offset, chunk being read, body so far) between reads, so each
read scans only new bytes; the caller stops copying the whole buffer. Nothing else changes. The runner's version moves; then, as
F1.S1 (f) did: `pin-runner.mjs`, `specs-record.mjs`, the three `--check` tools, a new dated `inputs/exec-probe-<date>.json` (no old
record changes), CP2's addendum in `inputs/checkpoints.md`. **A changed runner re-runs the D40 gate of every task validated on
the old one** (parent §6.1): F2.S1's 10 of 10 runs for those tasks are re-run on the new runner before the freeze.

**When.** The decision needs S, which comes from the first package (04 or 05, end to end first in F2.S1). Decide right after it,
before the other five validate, so at most one task re-runs. The runner is copied into every workspace, so (c) is settled before
(e) writes any.

**Done when:** the dated line exists; if built, merged with its red recorded and the re-runs done.

### (d) the freeze tools

Built in parallel with F2.S1, on synthetic fixtures only (`ownedFor({ token: 'bench' })` folders, invented specs and cases;
never a real spec draft, case, reference or private file). Merged before the dry run of (e).

**One CLI for the freeze:** `spikes/lang-bench/freeze.mjs`, with these verbs; each reads, prints and exits, and only `record`
writes, one file:

| Verb | What it does | Exit |
|---|---|---|
| `plan --repo <task> --ref <commit> --date <yyyymmdd>` | prints the freeze commit's edits: in each of the six specs, the `Freeze tag` row set to `` `spec-frozen-B-<date>` ``, and nothing else; refuses first | 0 a plan, 3 refused |
| `check-commit --repo <task> --before <commit> --after <commit>` | the freeze commit changes only those six rows | 0, 1 |
| `check-tag --repo <task> --tag <t>` | the tag is annotated, named `spec-frozen-B-<yyyymmdd>` with a real date equal to every spec's row, and its commit holds the six specs and FORMAT with the recorded sha256 | 0, 1 |
| `check-workspace --repo <task> --tag <t> --commit <w>` | T-L67: `<w>` descends from the tag, and every spec blob and FORMAT's blob at `<w>` equal the tag's; only `csharp/<NN-slug>/**` and `node/<NN-slug>/**` of the six tasks differ | 0, 1 |
| `hashes --root <checkout at w>` | prints `inputs/hashes-lang.txt` for that tree: every file under `csharp/`, `node/`, `runner/` and `tasks/`, plus FORMAT and the six specs, `sha256  path`, sorted | 0 |
| `record --repo <task> --tag <t> --commit <w>` | writes `inputs/specs.d/spec-states.json` (below) after every check above passes | 0, 1, 3 |

**What `plan` refuses** (each a sentence naming the spec): a `Gate verdict` row that is still its placeholder; a spec whose gate
record's first line names a FORMAT record other than `B`; a conformance check that is not all `ok` (it calls the checker's `run`
of `lib/conformance.mjs`, never a copy); FORMAT at `<ref>` that is not `B`'s sha256; a task list that is not exactly the six
(04, 05, 06, 07, 08, 10); a `<date>` that is not today's UTC date.

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
compares them with the committed ones byte for byte; F3.S1's `init` calls the same function, so what an agent gets is what the
task repository publishes. `build-workspaces.mjs` refuses a task of the six and names the engine's verb, so two builders cannot
write them (`common.reuse-first`, one road in).

**`workspace-diff` learns F1.S2's lines.** `MARKERS` gains `<!-- heap-sentence -->`; `samples/load.json` may differ between the
stacks only in its `heap` key (every other key equal, compared as JSON); nothing else changes.

**The held-out record (lang-explore owns it; lang-bench imports nothing of lang-explore).** A new FREE verb, `explore.mjs
heldout-freeze --tasks 04,05,06,07,08,10`, writes `spikes/lang-explore/inputs/heldout-frozen.json`: per task `treeSha256`
(`casesTreeHash`), `caseCount`, `trapCount`, the sha256 of the task's trap coverage table and of its package record (both files
as F2.S1's package names them), and the frozen value of the memory limit with the six package records that show both
references passing at it (the amended F-LIMITS-D's condition, §2). Counts and hashes only: no id, no name, no line. `init` of a
quick series refuses a held-out tree whose hash differs from this file for a task it lists.

**`leakcheck` over the task repository.** `explore.mjs leakcheck --repo <dir>` scans `<dir>` instead of the bench, through the
`io.repoRoot` seam; the lines that exempt a runner or grammar line are still read from the bench's `origin/main`, so the scan root
and the exemption root are two arguments, not one.

**Scenarios** (each a red first; test files beside their modules):

- `spikes/lang-bench/test/spec-freeze.test.mjs` (new; synthetic task repositories made with `git init` in owned folders):
  `The_freeze_plan_changes_only_the_freeze_tag_row_of_each_spec` (permitted) and
  `A_freeze_commit_that_changes_any_other_byte_of_a_spec_is_refused_naming_the_line` (refused);
  `A_spec_whose_gate_verdict_is_still_its_placeholder_is_refused_before_any_edit`;
  `A_spec_gated_under_FORMAT_record_A_is_refused_for_the_six`;
  `A_session_spec_whose_blob_differs_from_the_tag_is_refused_and_one_that_differs_only_in_workspace_files_is_not` (T-L20's pair);
  `A_workspace_commit_that_is_not_a_descendant_of_the_tag_is_refused`;
  `A_workspace_commit_whose_spec_blob_differs_from_the_tag_is_refused_and_one_that_adds_only_workspace_files_passes` (T-L67's pair);
  `A_lightweight_tag_or_a_tag_whose_date_differs_from_the_spec_rows_is_refused`;
  `The_hash_list_of_a_tree_lists_every_file_under_the_workspace_roots_and_fetchTaskRepo_accepts_it` and its companion
  `A_file_added_after_the_list_was_written_is_refused_by_the_fetch` (T-L1, through the real `fetchTaskRepo`).
- `spikes/lang-bench/test/workspace-diff.test.mjs`: `The_heap_sentence_is_a_marked_sentence_and_may_differ_by_stack`;
  `A_load_json_that_differs_in_anything_but_heap_is_a_finding`; `A_byte_changed_in_the_memory_sentence_is_still_a_finding`
  (that sentence is the same in both stacks, so the new marker must not mask it).
- `spikes/lang-bench/test/templates-build.test.mjs`: `The_lang_bench_builder_refuses_the_six_tasks_and_names_the_engine_verb`.
- `spikes/lang-explore/test/quick-freeze-workspaces.test.mjs` (new, invented specs): `The_written_workspace_equals_what_a_session_gets_byte_for_byte`;
  `A_workspace_whose_grammar_is_not_group_Bs_is_refused` (T-L57's freeze half: A's grammar planted, refused; B's, permitted);
  `The_check_finds_one_changed_byte_in_a_committed_workspace_and_passes_an_equal_one` (the pair);
  `The_copy_refuses_a_dirty_worktree_a_worktree_at_another_commit_and_an_existing_workspace`;
  `The_writer_refuses_a_destination_inside_a_git_worktree` (through `checkDestination`).
- `spikes/lang-explore/test/quick-heldout-frozen.test.mjs` (new): `The_record_holds_counts_and_hashes_and_no_case_id_or_trap_name`
  (it scans its own output for the fixture's ids and names and finds none); `Init_refuses_a_held_out_tree_that_differs_from_the_frozen_record`
  and `Init_accepts_the_same_tree` (the pair); `A_task_without_a_package_record_is_refused`.
- `spikes/lang-explore/test/quick-leak.test.mjs`: `The_leak_check_over_the_task_repository_finds_a_planted_held_out_line_in_a_workspace_statement`;
  `A_runner_line_in_a_task_workspace_is_exempt_as_it_is_in_the_bench`; `An_unreadable_task_repository_is_exit_4_never_clean`.
- `spikes/lang-explore/test/quick-freeze-consumers.test.mjs` (new; D67 (2), below): `The_frozen_record_holds_every_field_the_pre_registration_guard_will_read`,
  read through a test stub of F3.S1's guard over a synthetic record: the spec hashes and states, the tag and its commit, the
  workspace commit, the hash list's sha256, the held-out hashes and counts, the trap tables' hashes, the memory value. A field
  with no source fails.

**Docs.** `research/module_lang_bench.md` (§ the task repository: the freeze CLI, the hash list writer, the builder's refusal),
`research/module_lang_explore.md` (the two verbs, the held-out record, `leakcheck --repo`), `research/module_tests.md`.

**Done when:** every scenario went red, green, red; merged; nothing in it read a real spec, case or private file.

### (e) the freeze in the task repository

**Preconditions, all on `main` of their repositories:** (a), (b) applied and read back `equal`, (c) decided (and merged if
built), (d); F2.S1's six packages done, each spec gated (its `Gate verdict` row filled under record `B`) and merged with its
samples (`tasks/<NN-slug>/samples/`). §7 item 2 is settled.

**First, a dry run on the real files (D64).** In a scratch clone of the task repository outside every worktree: `freeze.mjs plan`,
apply the plan, commit, an annotated tag made locally and **never pushed**, `explore.mjs workspaces` and `workspaces-copy`, a second
commit, then every check of the steps below, including each guard's refused case on a planted change in a copy of that clone
(a one-word edit in a spec, a changed byte in a workspace, a changed held-out file in a copy of the private folder, A's grammar in
a workspace). An Opus judge reads the output against this list (it replaces CP4-B's reading); the result is one dated line in
`spikes/lang-bench/inputs/checkpoints.md` (counts and verdicts, no content), in bench PR 6. A failure is fixed in the tools or a
spec's package before anything is pushed. The scratch clone is removed afterwards.

**Then the real freeze, in this order.**

1. **T1, the freeze commit.** Branch `freeze/spec-frozen-B-<date>` in a worktree of the task repository: `freeze.mjs plan` and its
   edits only. Before the pull request: conformance of all six against the frozen FORMAT, all `ok`; `freeze.mjs check-commit`;
   `push-check --branch` (through the hook on push); `leakcheck --repo` on the branch's tree; the line scan on the task repository.
   Merged by squash after its checks: the merge commit is **C**.
2. **The tag.** It is the one step that cannot be undone: once the tag ruleset is on, nobody can move or delete a `spec-frozen-*`
   tag. So the team lead puts it to the owner in one line first, with the dry run's line (§7 item 3). Then: `git tag -a
   spec-frozen-B-<date> C` with a message that names the six tasks by number and nothing else; `push-check.mjs --tag`; the tag
   pushed **alone** with the owner's login (the creation ruleset's only bypass is the admin role); `git ls-remote --tags origin`
   shows it, and `freeze.mjs check-tag` exits 0. No workflow listens for tags there, so no run is awaited (`common.task-lifecycle`
   §3 checked: nothing to verify but the ref).
3. **T2, the workspaces.** `explore.mjs workspaces --tag <t>` into an owned folder outside every worktree; `workspace-diff --root`
   over it; then `workspaces-copy --into` a worktree of the task repository at C, branch `workspaces/spec-frozen-B-<date>`.
   Before the pull request: `workspace-diff --root` over the worktree; `explore.mjs workspaces --check --root`; `push-check --branch`;
   `leakcheck --repo`; the line scan. Merged by squash: the merge commit is the **workspace commit W**. Then `freeze.mjs
   check-workspace --tag <t> --commit W` exits 0 on a fresh fetch.

**Done when:** the tag is on the remote and checks; W is on the task repository's `main` and checks; the dry run's line is
recorded.

### (f) the freeze record in the bench

**Brief (Sonnet coder).** Branch `feat/lang-v2-f2s2-freeze-record`:

1. `freeze.mjs record --tag <t> --commit W` writes `inputs/specs.d/spec-states.json`; `specs-record.mjs` assembles `inputs/specs.json`.
2. `freeze.mjs hashes --root <checkout at W>` writes `inputs/hashes-lang.txt`; `PINNED_TASK_SHA` becomes W (D67 (3): the bare
   harness fetches at the workspace commit, never at the tag).
3. `explore.mjs heldout-freeze` writes `spikes/lang-explore/inputs/heldout-frozen.json`.
4. `spikes/lang-explore/inputs/explore.json`: the six tasks' `spec` pins move to C with their frozen sha256, `draft: false`, and
   `quick.reason` gets one dated sentence. A test holds them equal to `spec-states.json`:
   `The_engine_pins_of_the_six_specs_equal_the_freeze_record` (`spikes/lang-explore/test/quick-workspace.test.mjs`), because the
   spec pins otherwise live in two places (§2).
5. `inputs/checkpoints.md`: the dated lines of (b)'s read-back, (c)'s decision if not already there, and (e)'s dry run.
6. Docs: `research/module_lang_bench.md` and `research/module_lang_explore.md` name the tag, C, W and the records; this document's
   status line; the parent's status line names the freeze.

**Done when:** merged; `specs-record.mjs --check`, `pin-runner.mjs --check`, `pin-exec-layer.mjs --check` exit 0 on `main`;
`fetchTaskRepo` at W verifies; `explore.mjs workspaces --check --root` passes at W.

### The freeze rules and their pairs (D64)

| Rule | Refused | Permitted | Where shown |
|---|---|---|---|
| the freeze commit (D21) | any byte of a spec but its `Freeze tag` row | that row alone | (d) test; (e) dry run |
| the spec-freeze guard (T-L20, guard half) | a spec blob that differs from the tag's | a difference outside the spec blob | (d) test; (e) dry run |
| the workspace commit (T-L67) | not a descendant, or a spec or FORMAT blob that differs | only the six tasks' workspace folders added | (d) test; (e) dry run |
| the grammar per task (T-L57) | a workspace grammar that is not B's | B's grammar | (d) test; (e) dry run |
| the engine match (D67 (3)) | one changed byte in a committed workspace | equal bytes | (d) test; (e) dry run |
| the held-out record | a changed case tree | the same tree | (d) test; (e) dry run on a copy of the private folder |
| the fetch (T-L1) | a file off the hash list | the listed tree | (d) test; (f) at W |
| the tag ruleset | an update or a deletion of a `spec-frozen-*` tag | the admin's creation | the read-back of (b); the creation in (e) step 2; the refused write: §7 item 3 |
| `main`'s protection | a push that is not a pull request | a pull request merged by squash | the read-back of (b); T1 and T2 |

### Every consumer of the frozen bytes (D67 (2))

| Frozen bytes | Consumer | Built or shown |
|---|---|---|
| the six specs at the tag | the engine's statement (`quickWorkspaceFiles`), through `explore.json`'s pins | built (F1); pins moved in (f) |
| the six specs at the tag | the checker (`conformance.mjs`) | built (E1.S8) |
| the six specs at the tag | F3.S1's guard (`specBlobMatches`) | built in (d); the launch half in F3.S1 |
| FORMAT at the tag | `runner/GRAMMAR.md` of each workspace; the checker's record | built (E1.S9, F1); B recorded in (a) |
| the workspaces at W | the engine's match check; `fetchTaskRepo` and `hashes-lang.txt`; `workspace-diff`; the push check | built in (d), existing |
| the held-out trees | `init` and every pair (`heldoutMovedProblem`); F3.S1's guard | built in (d); the guard in F3.S1 |
| the trap tables' hashes | F3.S3's record (defects counted per trap class) | named; shown on a synthetic record in (d); built in F3.S3 |
| `spec-states.json`, `heldout-frozen.json` | F3.S1's pre-registration guard | shown on a synthetic record in (d); built in F3.S1 |

The task repository's root `runner/` (§5 of the parent) has one consumer, CI, which is Stage 2's; F2.S2 does not write it, and
the hash list holds no entry for it.

## 4. The order of the pull requests

The four merge conditions are the parent's (*Merge conditions*): **(1)** the gates on harness code: a coai plan round at `proceed`
before the code (for code already written, the round reads its brief as the scope), then a coai code round and an Opus review on
the committed branch, every finding resolved; **(2)** D68's whole runs of every suite touched, plain and with
`BENCH_REQUIRE_SANDBOX=1`, every test passed and nothing skipped, and CI's required checks present and passing at the head sha;
**(3)** D41: the line scan before every merge, and `leakcheck` when a spec, a sample, a package, a document under
`research/lang-v2/` or an agent-visible file is touched, **and at the freeze**; **(4)** no Stage 2 code. `plan-lifecycle.mjs`
clean on every bench pull request. Each pull request merges `main` before its last whole run, so the run is about the head that
merges.

| # | Repo | Items | Branch | What it runs before it merges | Which conditions bite |
|---|---|---|---|---|---|
| 1 | bench | this document; its `todo/README.md` row | `feat/lang-v2-f2` | line scan; `plan-lifecycle`; CI; an Opus reading by the team lead | (2) CI only, (3) line scan, (4) |
| — | task | PR #4, FORMAT's second amendment (not F2's; its own document gate) | `docs/format-amend-d72` | its gate and the push check | — |
| 2 | bench | F2.S2 (a), the pins | `feat/lang-v2-f2s2-format-pins` | the gates; lang-bench and lang-explore whole runs, both ways, `LANG_V2_TASK_REPO` at M; the three `--check` tools; `push-check --self`; `leakcheck`; line scan | (1), (2), (3) with `leakcheck`: every workspace's `runner/GRAMMAR.md` is cut from this FORMAT; (4) |
| 3 | bench | F2.S2 (b), the rulesets | `feat/lang-v2-f2s2-rulesets` | the gates; lang-bench whole runs, both ways; line scan. After the merge: apply, read back | (1), (2), (3) line scan only: nothing agent-visible; (4): `run-main` and CI's checks are not in the data |
| — | task | F2.S1's six spec pull requests | F2.S1's | F2.S1's | F2.S1's |
| 4 | bench | F2.S2 (c), only if built | `fix/lang-v2-f2s2-chunks` | the gates; the runner re-pin; the probe re-run; lang-bench and lang-explore whole runs, both ways; the three `--check` tools; `push-check --self`; `leakcheck`; line scan | (1), (2), (3) with `leakcheck`: the runner is agent-visible; (4) |
| 5 | bench | F2.S2 (d), the freeze tools | `feat/lang-v2-f2s2-freeze-tools` | the gates; lang-bench and lang-explore whole runs, both ways; `push-check --self`; `leakcheck`; line scan | (1), (2), (3) with `leakcheck`: it changes what `leakcheck` reads; (4) |
| T1 | task | (e) step 1, the freeze commit | `freeze/spec-frozen-B-<date>` | conformance of the six; `check-commit`; `push-check --branch`; `leakcheck --repo`; line scan on the task repository | (3) with `leakcheck`, at the freeze; no CI there (§3) |
| tag | task | (e) step 2 | — | the owner's word; `push-check --tag`; `check-tag` after the push | (3): the tag's message is public text |
| T2 | task | (e) step 3, the workspaces | `workspaces/spec-frozen-B-<date>` | `workspace-diff --root`; `workspaces --check`; `push-check --branch`; `leakcheck --repo`; line scan; then `check-workspace` | (3) with `leakcheck`: the workspaces are agent-visible |
| 6 | bench | F2.S2 (f), the freeze record | `feat/lang-v2-f2s2-freeze-record` | the gates (a code round over the record's tool runs); lang-bench and lang-explore whole runs, both ways, `LANG_V2_TASK_REPO` at W; the three `--check` tools; `fetchTaskRepo` at W; `leakcheck` and `leakcheck --repo` at W; line scan; `plan-lifecycle` | all four |

**Order and why.** PR 1 first, so every brief cites a merged document. Task PR #4 before PR 2, and PR 2 before F2.S1 checks any
spec against the amended FORMAT (§2: the checker reads only a recorded FORMAT). PR 3 any time after PR 1, in parallel with F2.S1;
it is applied before T1, because the tag ruleset must exist and read back before the tag is made (consult C7b). PR 4's decision
comes right after F2.S1's first package, before the other five validate, and before T2 (the runner is in every workspace). PR 5 is
built in parallel with F2.S1 and merges before the dry run. Then the dry run, T1, the tag, T2, PR 6, strictly in this order. The
bench's cap of three open pull requests holds throughout: PRs 2, 3 and 5 can be open together only while none of F2.S1's bench
pull requests is open; otherwise land one first.

## 5. The model per role

| Role | Model | In F2 |
|---|---|---|
| Stories, scenarios and briefs | opus | this document, for F2.S2 |
| Cornerstone spec | fable | F2.S1's scenarios and brief (`~/rsd/lang-v2/briefs/f2s1-spec-public.md`, its private half outside git), copied in by the team lead; Opus stands in only if Fable refuses on a limit, and the summary says so |
| Coder | sonnet | the code and tests of PRs 2 to 6, and the fixes after review |
| Small edit | main session or sonnet | one file, at most 50 changed lines, bounded before the first edit: (c)'s dated "not needed" line qualifies; every pull request above spans more files and does not |
| Reviewer and judge | opus | the review of every pull request, beside the coai code round; the reading of (e)'s dry run; the verdict on whether a finding changes a story |
| Scout | sonnet | evidence only, such as S for (c) from the package records, or the remote state of the task repository |
| The team lead (main session) | — | the ruleset apply and read-back (the owner's go of 2026-11-05), the question to the owner before the tag, the tag push; never a coder |

The cornerstone ledger does not change: the parent's line reads *Cornerstones: 2 of 2 — F1.S2, F2.S1*, and this document marks
only F2.S1.

## 6. Growth

- **The task repository:** twelve workspaces, each the template (2 KB for C#, up to 81 KB for Node with its lock), the runner
  (about 0.3 MB) and the statement and samples: about 5 MB in all, once. Kept for ever: it is the frozen input. A second freeze
  would be a new tag and a new workspace commit, never an edit.
- **The fetch cache:** `~/.cache/lang-v2/<sha>/`, one folder per pinned sha, a few MB each (3.7 MB today); M and W add two.
  Kept, since each names a pin that a record cites.
- **The scratch folders:** the dry run's clone and the engine's write folder are owned folders (`ownedFor`), removed when the step
  ends; a killed run's folder is swept by the owner rules (`common.test-leftovers`).
- **The bench records:** `spec-states.json`, `heldout-frozen.json`, the hash list and a few dated lines: kilobytes, append only.
- F2.S1's growth (references, mutants, package records) is its spec's.

## 7. Open points found while writing, for the team lead

1. **FORMAT's second amendment moved from F1 to F2.** The parent gives it to F1.S2 (b), and F1's stories document says the same;
   #179 merged F1 without it (the pins are at `4ddb8f80`, PR #4 is open). This document builds it as F2.S2 (a), first, because
   F2.S1 needs it. Moving a story's part to another epic may count as a change in the parent's ledger (*in doubt, it is a
   change*), and epoch 9 already has 9.1, so a second change would stop the plan. A reading that avoids it: the work stays
   F1.S2 (b)'s by the parent's text, F1 is not done until it merges, and this document only sequences it. The team lead decides,
   before PR 2 opens.
2. **Are the frozen specs published at the freeze?** *What got smaller* item 11 says the specs publish at the Stage 2 decision
   line and stay private until then. D2, D21, D31 and the parent's §5 (kept in epoch 9) put the gated, frozen specs and the workspaces in the
   public task repository, and the Definition of Done says "the specs' **final state**". (e) pushes the six specs and twelve
   workspaces to the public repository. This document reads item 11 as the specs' final state after the series (with any
   adjudication), not the frozen text an agent gets anyway; if that reading is wrong, (e) cannot run as written and the plan
   changes.
3. **The tag cannot be undone, and its refused-change pair has no safe demonstration.** After (b), no one can move or delete a
   `spec-frozen-*` tag. D64 wants a refused change shown on the real thing; a real refused write would be a forced push or a
   deletion attempt against the public repository, which this family's tools refuse to send. This document shows the rule by the
   read-back and the creation, and asks the owner one question before the push. Whether the owner wants the refused write tried
   by hand is theirs.
4. **F2.S1's package record should carry two counts for F2.S2:** the largest expected reply body per task (S, for (c)), and the
   name of its trap coverage table file (for `heldout-frozen.json`). F2.S1's spec is not written yet; this is for its writer.
5. **"Sealed" has no Stage 1 meaning yet.** The parent's F2 done-when says "validated and sealed". v1 sealed held-out sets into
   an encrypted archive; the one engine reads `~/rsd/lang-v2/explore/heldout-NN/` in clear. This document reads "sealed" as: the
   tree hash and counts registered in `heldout-frozen.json`, and every `init` refuses a change. F2.S1's spec may define more.
6. **FORMAT's protected classes bind nothing in Stage 1.** `tools/format-classes.mjs` and T-L56 protect `common` and `olympiad`
   sections "after the freeze of the first group" (`lib/format-class-table.mjs`, `PROTECTED`). The second amendment changes a
   bullet of section 1.5 (`common`). Stage 1 makes no A tag (parent F2.S2), so there is no first-group freeze; a T-L56 finding on
   that bullet would then be recorded, not a refusal. The team lead confirms this reading before PR 2 merges.
7. **The memory value was written before its condition.** The amended F-LIMITS-D registers the one bound only once every
   reference passes under it; `memory-limit.json` says `decidedOn` 2026-11-07. This document puts the evidence (the six package
   records) into `heldout-frozen.json` at the freeze and leaves `memory-limit.json` alone, so no identity moves.
8. **Two places pin the six specs** (lang-bench's `specs.json`, lang-explore's `explore.json`). (f) moves both and a test holds them
   equal. Making the engine read `specs.json` would remove the second place; it is a larger change and is not done here.
9. **F1's own records lag #179.** The status line of [PLAN_lang_comparison_v2_E7_F1.md](PLAN_lang_comparison_v2_E7_F1.md) and its
   `todo/README.md` row still say "plan only … the rest open", though F1 merged. Not changed here.

## 8. Definition of Done (F2)

- [ ] F2.S1: done as its spec says (the parent's acceptance: per task, the package record in the private area and a public line
      of counts only).
- [ ] F2.S2 (a): the pins follow FORMAT's second amendment; `format.json` records `B` for the six; the three `--check` tools exit 0.
- [ ] F2.S2 (b): the two rulesets (three GitHub rulesets) applied on the task repository and read back `equal`, recorded.
- [ ] F2.S2 (c): the decision line exists with its numbers; if built, merged and the re-runs done.
- [ ] F2.S2 (d): the freeze tools merged, every scenario red, green, red.
- [ ] F2.S2 (e): the dry run recorded and read by an Opus judge; `spec-frozen-B-<yyyymmdd>` on the remote, annotated, over the six
      specs and FORMAT; W on the task repository's `main`; `workspace-diff` and the push check clean.
- [ ] F2.S2 (f): the spec hashes in `inputs/specs.json`; the held-out tree hashes and the trap tables' hashes registered; the
      workspace commit named; `PINNED_TASK_SHA` at W; the engine's pins at the tag.
- [ ] `leakcheck` clean on the bench and on the task repository at W; the line scan clean on both; the private aid lists nothing
      uncaught (F2.S1).
- [ ] Every pull request met its row of §4; every failure on the way is a dated line in the leftovers plan's S5.
- [ ] No held-out content, case id or trap name in any public text.
- [ ] `plan-lifecycle.mjs` is clean; this document's status line says what is done; any change found on the way is an entry in
      the parent, never a ledger here.
