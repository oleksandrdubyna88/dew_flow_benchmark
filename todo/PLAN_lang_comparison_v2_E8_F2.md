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

Cornerstone. The scenarios and the brief below are the Fable writer's spec of 2026-11-07, amended on 2026-11-08 to the plan's epoch-10 revision, copied in by the team lead without change. Its private half (per task: the defects, the traps, the witnesses and the artefact map) stays outside git at `~/rsd/lang-v2/briefs/f2s1-spec-private.md`.
> Written 2026-11-07 by the Fable spec writer for `todo/PLAN_lang_comparison_v2.md` (epoch 9, *Epics in force*, F2.S1;
> D69–D76, *Merge conditions*, *Growth*, *Test plan*), read at `main` `57ccc379` with F1 merged. **Amended 2026-11-08 to
> the epoch-10 revision** (branch `docs/lang-v2-epoch10-revision`, `a10972a3` and after; the plan gate `proceed`, the
> owner's approval of item 12): defect witnesses in place of the mutant rule, the bias question's leak rule (D25), the
> registration of the memory value as a field F2.S1's tool writes (D72), the measured F-LOAD rule, the sealed archive per
> task with the clear folder's lifecycle, task 05 first with its witness cost recorded, merge condition 5 and build steps
> 2a and 2b. The team lead copies this text into `todo/PLAN_lang_comparison_v2_E8_F2.md` after checking it. It holds no
> held-out case, trap name, case id or reference text (D41); what names those is the private half,
> `~/rsd/lang-v2/briefs/f2s1-spec-private.md`, which the coders read on the host and never quote. Models
> (`common.subagent-models`): this spec is Fable's; Sonnet coders build the tools, the spec fixes, the references and the
> witnesses; an Opus judge reviews every pull request; Sonnet fixes.

**What the story is.** Six draft specs exist on the task repository's local branches, each with four visible samples
(five of them; one has none in git yet), a private held-out set of 18 to 32 cases written quickly for the owner's quick
run, and one Python reference per task that runs outside the box. Nothing has been validated against a reference in
either stack, no witness has been run, no spec has been gated, and every draft carries the fixed sentences of an older
FORMAT. After this story each of the six tasks has one **package**: a corrected and gated spec, its samples, two
references that pass every case ten of ten under F1's scoring, a held-out suite whose every case cites a requirement and
whose every trap is covered, one defect witness per trap per stack that the registered cases catch exactly, an answered
bias question, the two leak checks green, a sealed archive, and an identity F2.S2 can freeze. When all six are green the
memory value is registered. A failure in the series is then adjudicable: a case that fails names the requirement it
asserts, and a reference exists in the failing stack that passes it.

**Before the first package** (build order 2a, 2b): the bench's FORMAT pins for the second amendment
(`chore/lang-v2-format-d72-pins`, `2b0947ef`; the task repository's #4 is merged at `c204726`) are merged; `leakcheck` over
both repositories with the publication-commit scan and the planted-needle check is built, gated and merged. **No
task-repository pull request opens before 2b has merged** (merge condition 5).

---

### 1. What a package is (one task), with done-when per item

| # | Item | Done when |
|---|---|---|
| P1 | **The corrected spec**, gated by one document gate | The spec on its branch passes `node spikes/lang-bench/conformance.mjs --repo <task repo> --task NN-slug --format <FORMAT at c204726> --needles <private needles file>` with exit 0 (today every draft fails the same eight checks against the amended FORMAT: the `Freeze rule` row, F-STDERR, F-LIMITS-D, F-MEMORY, F-PROCS, F-FROZEN, F-ENV, F-OFFLINE; task 04 also lacks its sample files and has two placeholder names the checker reads as plan ids; F-LOAD's duration is set by the measured rule of P3). One `mcp__coai__review_document` round reached `proceed`, `good_enough` or `continue_anyway`, every finding resolved; the header's `Gate verdict` row carries the verdict and date; the gate record is `spikes/lang-bench/inputs/gates/spec-NN.md`, holding **only the verdict, the reviewer count and the findings in words that name no case** — no case text, case id or trap name (D25's leak rule). The spec's pull request on the task repository merges by merge condition 5: the document gate at `proceed`, the bench-side checks (the conformance check, `leakcheck` over both repositories, the pin checks) run against a checkout of its head with their commands, exits and the head sha recorded in the pull request; the bench pins that follow it are a bench branch of their own, merged by conditions 1 to 3, and run the same checks again against the merge commit. The spec's sha256 is then pinned in `spikes/lang-explore/inputs/explore.json` (`tasks.NN.spec`). |
| P2 | **Visible samples** | `tasks/NN-slug/samples/s01..s04.{in,out}` exist in git and equal section 6 byte for byte (the checker's `samples` check); the runner parses them; the generated workspace of each stack carries them unchanged. |
| P3 | **Two references**, one C# and one Node, built from the stack's own template | Each lives under `~/rsd/lang-v2/explore/refs-NN/<stack>/` as only the files an agent would write (sources; a project file only if a package outside the template's set is needed), never a frozen file; the package tool lays them on `spikes/lang-bench/templates/<stack>/<kind>/`, builds with the template's `build.sh` under D66's policy, and scores them with F1.S2's ten-pass driver at the one memory value (536,870,912 bytes, **chosen**; registered by P9): both read `accepted` (every visible and held-out case passed in all ten passes, five per CPU profile), with the enforced limit read back as that number in every case. The record keeps, per reference and per pass, the largest peak, the slowest case wall, the ten passes' wall and the retained size with the most retries (they feed *Growth*). **F-LOAD's duration is measured, not assumed:** the slowest whole-scenario wall of each load case (start, readiness, the load, the stop), over every pass of both references under both profiles, is at most 600 s minus 30 s and at most 600 s minus twice the measured non-load part (the wall of the same scenario with its load steps removed, run once per reference); a duration that leaves less is shortened in the spec before the gate; 540 stands only if measured so; the measurement is in the package record. |
| P4 | **The held-out suite** | `heldout-NN/cases/` has `hNNN.in`/`.out` pairs and `cases.txt`; every line's comparison word is one FORMAT 3.1 allows and every R-id it cites exists in the corrected spec (read from the pinned text, since `specs.json` has no entry before F2.S2); `manifest.json`'s per-case hashes equal the files; `traps.txt` lists exactly the case ids the trap table names; **every trap-and-stack row of the table names at least one existing case and exactly one witness**; the held-out reader (F1.S1 (d)) accepts the folder. |
| P5 | **Defect witnesses**, one isolated witness per trap per stack the table lists | `heldout-NN/mutants/<id>/` holds the files a witness replaces or adds over its stack's reference (an overlay, never a whole tree, never a frozen file); `manifest.json` lists each with its trap, its stack and its kind, `deterministic` or `race`. The package tool applies each over the reference, builds it, and runs it: a witness must build (one that does not is not caught), its stack's reference must pass, and the cases registered for its trap must reject it. **A deterministic witness** gets one pass on the 2-CPU profile and is caught when it fails every case registered for its trap and no other case. **A race witness** gets ten passes, five per profile, like a session, and is judged only on its failing passes: each failing pass fails at least one registered case and no case outside them; a timeout or a hang of a registered case counts as that case failing; at least one of the ten passes fails; its clean passes are allowed and kept. **An extra failure is a mapping to fix and record, never allowed silently**: the witness reads not caught until the trap table registers the case or the witness is corrected, and the record keeps the actual set. Every witness of the manifest is caught. **The cost** (walls and sizes of every witness run) is recorded, and task 05's cost line is written before the other five start. |
| P6 | **One bias question** (D25) | The spec and its suite, visible and held-out, go to the external vendors' review — the coai document gate and a consultant — with D25's question, **from a scratch folder outside every repository**, never from git; the reviewers answered per stack. The gate record (`inputs/gates/spec-NN.md`) holds the verdict, the reviewer count and the findings in words that name no case: no case text, case id or trap name; a scan holds it (scenario 29). |
| P7 | **The two leak checks green** and the link refusal | `node spikes/lang-explore/explore.mjs leakcheck` exits 0 over both repositories, reading the new reference folders and witness overlays as needles; `python3 -I ~/rsd/lang-v2/briefs/leakscan-lines.py . history` exits 0 on the branch; `loadHeldoutQuick` refuses a link anywhere in the package (F1.S1 (d), merged) and the conformance check is green (P1). |
| P8 | **The identity, registered** | `spikes/lang-explore/inputs/packages.json` gains the task's entry: the spec's sha256 and the FORMAT's, the held-out tree hash, the sealed archive's hash (P10), the trap and witness counts, the references' tree hashes per stack, the witness tree hash, the gate verdict and date, the memory value both references passed at, the F-LOAD measurement's three numbers, and one package identity (a sha256 over all of these) — counts, hashes and durations only, never a case id, trap id or path. F2.S2 reads it for the freeze. The public line of counts in the stories document is this entry's counts. |
| P9 | **The memory value registered** (D72; once, after all six) | `spikes/lang-explore/inputs/memory-limit.json` gains the field `registration` — the date and the twelve references' identities (task, stack, tree hash) — written by F2.S1's tool and by nothing else, only when all twelve references pass every case under the value in both stacks; the same write fixes the file's note, which calls the value "registered" before that held ("chosen" until the field exists). F3.S1's guard refuses a series without the field. If one reference cannot pass, the value moves for all six (D72) and every package re-runs. |
| P10 | **The package sealed** | One encrypted archive per task (`heldout-tool.mjs seal`'s shape: AES-256-GCM, the key a mode-600 file outside every git tree and box), its hash in `packages.json`; the cases are in clear only in a scoring temp dir or in one clear working folder beside the archive, **mode 700, checked against the archive's hash at every use, removed after each pair and removed again at the start of any run after an interruption**, while the archive and its identity stay; a check that needs the clear cases makes the folder again from the archive. |

**Done when (the story):** P1–P8 and P10 green for task 05 first, then for the other five; P9 written once; the witness
cost of 05 recorded before the other five start; `research/module_lang_explore.md` describes the package tool and
`research/module_tests.md` lists its scenarios; every pull request met the parent's merge conditions 1 to 5.

---

### 2. The scenarios: the checks of a package, as tests

Test names are sentences with underscores, in the style of the files they join. **Fakes** = `buildFn`/`runFn` seams, a
fake child, a temp folder under `ownedFor({ token: 'bench' })`, invented specs and cases; **real** = bwrap, the user bus,
the pinned toolchains and the certifying host, behind `needs` (skipped with the reason, a failure under
`BENCH_REQUIRE_SANDBOX=1`), on invented programs only. No test reads a private file or a quick-run tree.

#### 2.1 The reference trees (`test/quick-package-refs.test.mjs`, fakes)

1. `A_reference_workspace_is_the_templates_files_plus_the_reference_files_and_nothing_else` — given a fake template folder
   and a reference folder of two source files; observed: the assembled tree's file list equals the union, bytes equal, the
   template's frozen set is the template's.
2. `A_reference_folder_that_holds_a_frozen_file_is_refused_naming_the_path` — given `build.sh` or `samples/x.in` inside the
   reference folder; observed: `fail` with the relative path, nothing assembled.
3. `A_reference_folder_that_holds_a_link_is_refused_and_never_read_through` — a symbolic link to a file outside; observed:
   `fail` naming the link; the target's bytes appear nowhere.
4. `Both_references_are_scored_by_the_ten_pass_driver_and_green_means_accepted_in_both_stacks` — fake seams make
   `scorePasses` return `accepted` for both stacks; observed: P3 is green, the record names ten passes per stack and the
   profile of each pass; with one stack `intermittently incorrect` the verdict is red naming the stack and the pass counts,
   never a case id.
5. `A_reference_pass_enforced_under_another_memory_limit_is_an_environment_halt_not_a_verdict` — a fake pass record whose
   `memoryMaxBytes` differs from `inputs/memory-limit.json`; observed: the pass is an environment attempt (F1.S2's rule
   carried into the package), the package stops with the two numbers.
6. `The_record_keeps_each_references_largest_peak_slowest_wall_ten_pass_wall_and_retained_size` — fake passes with known
   numbers; observed: the private record's per-stack rows carry the maximum peak and wall of each pass and of all ten, the
   ten passes' wall and the retained bytes with the most retries (the numbers *Growth* reads).

#### 2.2 The witnesses (`test/quick-package-witness.test.mjs`, fakes)

7. `A_witness_is_the_reference_with_its_overlay_applied_and_an_overlay_onto_a_frozen_file_is_refused` — an overlay that
   replaces `src/main.ts` is applied; one that adds `run.sh` is refused naming the path; a whole-tree folder (one that
   carries the template's files) is refused as not an overlay.
8. `A_witness_identical_to_its_reference_is_refused` — overlay bytes equal to the reference's; observed: `fail` saying the
   witness changes nothing.
9. `A_deterministic_witness_is_caught_when_in_its_one_pass_every_registered_case_fails_and_no_other_case_does` — fake run:
   exactly the registered cases fail → caught; one registered case passes → not caught, the sentence gives counts
   (registered, failed, missing).
10. `An_unregistered_failure_is_a_mapping_to_fix_and_the_witness_is_not_caught_until_the_table_says_so` — a failing case
    outside the registered set, deterministic or race; observed: not caught, the record keeps the actual failing set and
    the sentence says "one case beyond the table" with counts; after the table registers the case (or the overlay changes)
    the same run reads caught. Nothing is allowed silently.
11. `A_race_witness_gets_ten_passes_and_is_judged_on_its_failing_passes_only` — fake passes: three failing passes, each
    failing registered cases only, seven clean → caught, the seven clean passes kept in the record; one failing pass with a
    case outside the registered set → not caught; ten clean passes → not caught, the sentence says "a trap with no catching
    case"; the passes alternate the two profiles like a session's.
12. `A_witness_whose_build_fails_is_not_caught_and_that_is_the_point` — `buildFn` returns a failed build; observed: not
    caught, reason `does not build`; a guard against "every case failed, so it is caught".
13. `A_timeout_or_a_hang_of_a_registered_case_counts_as_that_case_failing` — a pass whose registered case ends in the test
    bound, a step deadline or a stop within the grace period; observed: it counts as a failure of that case in both
    witness kinds; the same on an unregistered case counts as a failure outside the set.
14. `A_witness_pass_is_the_pass_child_with_the_affinity_read_back_on_its_profile` — the fake child records the mask it was
    given: the 2-CPU profile for a deterministic witness, the alternating profiles for a race one; another mask is a halt
    `affinity-not-applied`, never a verdict.
15. `Every_trap_and_stack_row_of_the_table_names_an_existing_case_and_exactly_one_witness` — a manifest with a row for
    both stacks and one witness → red naming the row and the missing stack; a row whose case is not in `cases/` → red; two
    witnesses on one row → red; a witness whose trap row names no case → red; a complete manifest → green.

#### 2.3 The suite, the R-ids, the manifest and the load rule (`test/quick-package-suite.test.mjs`, fakes)

16. `Every_case_cites_at_least_one_R_id_that_exists_in_the_corrected_spec` — an invented spec with `**R1.**`…`**R7.**`;
    a `cases.txt` line citing `R9` → red naming the case id and `R9` (the tool's own output stays on the host; nothing of
    it is written to a public file); a line with no R-id → red; a comparison word outside FORMAT 3.1's list → red.
17. `The_manifest_the_folder_and_cases_txt_agree_byte_for_byte` — a case file changed after `gen.py` ran (its sha256 in
    `manifest.json` differs) → red naming the case; an id in `cases.txt` with no pair, or a pair not listed → red (the
    held-out reader's own findings are reused, not rewritten).
18. `traps_txt_is_exactly_the_union_of_the_trap_tables_cases` — a case in `traps.txt` no row names, or a row's case missing
    from `traps.txt` → red with the counts.
19. `A_spec_whose_sha256_differs_from_the_pinned_one_is_refused` — the package reads the spec text from the task
    repository at the pin in `inputs/explore.json`; a text with another hash → refused before any scoring (the package
    validates the text that will freeze).
20. `The_F_LOAD_duration_rule_is_measured_over_every_pass_of_both_references_and_540_is_not_assumed` — fake pass records
    with whole-scenario walls of the load cases under both profiles and a non-load twin's wall: slowest wall ≤ 600 − 30 and
    ≤ 600 − 2 × non-load → green with the three numbers in the record; a slowest wall over either bound → red naming the
    bound missed and the duration to write into the spec; a package with no twin measurement → red, never green by the
    spec's number.

#### 2.4 Conformance, the gate, the registry and the registration (`test/quick-package-registry.test.mjs`, fakes plus the real checker)

21. `The_package_is_red_while_the_conformance_check_fails_or_the_verdict_row_is_still_its_placeholder` — the real
    `conformance.mjs` over an invented spec in a temp repository with the FORMAT stand-in of
    `lang-bench/test/fixtures/conformance/`: exit 1 → red with the checker's first line; exit 0 and a `Gate verdict` row
    still at its placeholder → red; exit 0 and a verdict with a date → green.
22. `The_package_identity_moves_with_any_byte_of_the_material_and_with_nothing_else` — same bytes twice → the same
    identity; one byte of a witness overlay, of a case, of a reference file, of `manifest.json` or of the spec → a
    different identity; a changed private record (a re-run) → the same identity.
23. `The_registry_entry_holds_counts_hashes_and_durations_only` — the written `packages.json` entry: a scan finds no
    `h\d{3}`, no witness or trap id, no absolute path, no text longer than a hash; its counts equal the manifest's; the
    three F-LOAD numbers are integers of milliseconds.
24. `register_refuses_while_any_item_is_red_or_the_record_is_older_than_the_material` — a record whose hashes differ
    from the material now → refused; a red item → refused; everything green and current → the entry is written once,
    and a second write with the same identity is a no-op.
25. `The_registration_field_is_written_only_when_all_twelve_references_pass_and_nothing_else_writes_it` — a fake
    registry with eleven passing references → refused naming the missing task and stack; twelve → `memory-limit.json`
    gains `registration: { date, references: [12 × { task, stack, treeSha256 }] }`, the note reads "chosen" until then and
    names the field after, `memoryLimit()` still loads the file, `bytes` is unchanged; a scan over `spikes/` finds no other
    writer of the field; a second write is a no-op; a reference identity that is not one of the twelve registered ones is
    named by F3.S1's guard (F3.S1's test, listed here as the consumer).

#### 2.5 The leak checks and the gate records (`test/quick-package-leak.test.mjs`; makes temporary git repositories, as `quick-leak-scan` does)

26. `leakcheck_reads_the_stack_reference_folders_and_the_witness_overlays_of_a_quick_task` — an invented line of 30+
    characters from `refs-NN/csharp/src/X.cs` planted in a public file → a finding naming the file, never the text; the
    same from `heldout-NN/mutants/<id>/src/main.ts` → a finding; the manifest's witness ids as words in a
    `research/lang-v2/*.md` fixture → a finding (the id tier extended to a quick task's manifest).
27. `A_line_equal_to_a_template_files_line_is_a_collision_not_a_needle` — a reference folder holding a line that also
    stands in `spikes/lang-bench/templates/**` at HEAD; observed: that line is dropped from the needles and counted as a
    collision, so the public templates are no finding; the same line with one character changed is a needle (the
    collision rule of the committed runner, extended to the templates, read through git).
28. `The_substring_needle_cap_holds_with_two_stack_references_per_task` — six invented tasks with reference folders of
    the expected size; observed: the needle count stays under `SUBSTRING_NEEDLES_MAX`, or the check fails closed with
    the count (never a slow run).
29. `A_gate_record_holds_only_the_verdict_the_reviewer_count_and_findings_that_name_no_case` — over
    `spikes/lang-bench/inputs/gates/spec-*.md` fixtures: a case id (`h\d{3}`), a witness id, a trap id of the manifest, a
    line equal to a case line or a six-word window of the private notes → red naming the file and the line number, never
    the text; a record of the verdict, the count and findings in words → green (`leakcheck`'s shingle tier already covers
    the notes; this scan adds the ids and the case lines against the gate folder).

#### 2.6 The real box (`test/quick-package-real.test.mjs`, real; invented programs only)

30. `A_toy_reference_pair_on_the_real_templates_goes_through_the_package_check_end_to_end` — the echo daemons of
    `quick-real-programs.mjs` (C# built by the template's `build.sh` in the real build box; Node started by `run.sh`)
    with an invented held-out folder of three cases and a manifest with one trap per stack and one deterministic witness
    each (the daemon answering wrongly on one word, registered to the one case that sends it); observed: both references
    `accepted` ten of ten, both witnesses caught, the record written, the identity stable across two runs.
31. `A_witness_that_only_the_memory_bound_catches_is_caught_through_the_certified_box` — the toy's word that allocates
    without end (both stacks: the typed-buffer path and the object path of F1.S2's enforcement check) as a deterministic
    witness registered to the one case that sends that word; observed: the case reads `memory limit` with its cause, the
    witness is caught, the reference passes the same case; it halts on a host that cannot certify memory (never a pass).
32. `A_race_toy_witness_is_caught_on_its_failing_passes_and_its_clean_passes_are_kept` — the toy's program that answers
    wrongly only under the 2-CPU mask (`WRONG_ON_TWO`, the F1.S2 fixture) as a race witness registered to its case;
    observed: the five 2-CPU passes fail that case only, the five 8-CPU passes are clean and kept, the witness is caught;
    with the same program registered to another case it is not caught (an unregistered failure).
33. `The_toy_references_are_scored_at_the_chosen_limit_read_back_from_the_cgroup` — every pass record's `memoryMaxBytes`
    equals `inputs/memory-limit.json`'s bytes.

#### 2.7 The command, the seal and the clear folder (`test/commands.test.mjs`, `quick-package-command.test.mjs`, `quick-package-seal.test.mjs`, fakes)

34. `package_is_a_free_command_that_runs_on_the_scoring_CPUs_under_the_series_lock` — `package` is in `REEXEC`; it takes
    the series lock of the task's explore root (so no session or scoring runs beside it) and releases it; a held lock is
    a resumable halt (exit 3) with the holder named.
35. `A_halted_package_check_resumes_from_its_records_and_runs_only_what_is_missing` — an environment halt after the C#
    reference's sixth pass and two witnesses; a second run re-reads `passes/` and the witness records and runs the rest;
    every record is written once (`wx`), nothing rewritten.
36. `seal_makes_one_encrypted_archive_per_task_whose_unsealed_tree_hash_is_the_held_out_tree_hash` — the minigit seal
    (`sealSuite`, `unseal`, a test key file) over an invented `cases/`: the archive decrypts to the same `casesTreeHash`;
    the archive's sha256 goes into the registry entry; a changed archive byte fails to unseal or gives another hash.
37. `The_clear_folder_is_mode_700_checked_against_the_archive_and_removed_after_each_pair_and_at_a_restart` — a clear
    folder with mode 755 → refused naming the mode; one whose tree hash differs from the archive's → refused; after a
    (fake) pair ends → removed; a run that starts with a stale clear folder → removed and remade from the archive, the
    archive untouched; the check that needs the clear cases finds them again.

Thirty-seven scenarios. Real-box ones: 30 to 33. Each is written red first and watched failing (`common.testing`); the
reds file is `~/rsd/lang-v2/briefs/f2s1-reds.txt`.

---

### 3. The brief: what to build or extend, file by file

All under `spikes/lang-explore/` unless said. Plain ESM on Node 26, no new dependency; functions under 50 lines,
complexity at most 4 (`code-shape.test.mjs` holds it); expected failures are `ok`/`fail` values; an unknown is
`{ known: false, why }`. Reuse before writing: every item names the code to call.

| File | What | Limits |
|---|---|---|
| `lib/package-quick.mjs` (new) | `loadPackage(home, task)`: the held-out side (`loadHeldoutQuick`), the manifest's `traps` and `witnesses` blocks with their shape checks (trap → stack, cases; witness → trap, stack, kind `deterministic`/`race`, overlay folder), `refs-NN/<stack>/` listings by `lstat` (a link or a frozen file refused), the pinned spec text (`world.sources.spec`, hash-checked against `inputs/explore.json`). `assembleReference({ template, refFiles })` → a tree in an owned temp folder (the shape of `layDaemonTree` in `test/quick-real-programs.mjs`, which moves into `lib/` as a generic helper). `packageIdentity(material)` → the sha256 of section 1 P8. | Pure but for reading; no box here. |
| `lib/package-refs.mjs` (new) | `validateReference({ world, stack, tree, heldout })`: `world.passSeams({ stack, source: tree, limits, sets: [{ name: 'heldout', dir }], traps })` into `scorePasses` (the exact call `measure-growth.mjs` makes), then `verdictOf`; green only on `accepted`; the per-pass peak, wall, ten-pass wall and retained size rows for the record (`dirBytes` from `lib/measure.mjs`); the memory read-back compared with `memoryLimit().bytes`. `loadRule({ passes, twinWall })`: the slowest whole-scenario wall of each load case against 600 s − 30 s and 600 s − 2 × the non-load twin's wall, the three numbers out. | Nothing of the pass driver changes; `passes.mjs` is called, not copied. The twin scenario (the load case with its load steps removed) is written by the suite author beside the case, outside `cases/`, under `heldout-NN/twins/`, and run once per reference through the same seams. |
| `lib/package-witness.mjs` (new) | `applyWitness({ referenceTree, overlayDir })` (reuse `overlay` from `lib/heldout.mjs`; refuse a whole tree, a frozen file, an empty diff), the build through `buildAndRestore` (`copy-quick.mjs`), the runs: a deterministic witness through `runPassChild` once on `PROFILES[0]`; a race witness through `scorePasses` with ten passes exactly as a session (its `verdict.json` is not the witness verdict: the witness verdict is computed from the per-pass case sets). `witnessVerdict({ kind, registered, passes })`: the two rules of section 1 P5 as small predicates — deterministic: failing set equals the registered set; race: every failing pass's set is non-empty and a subset of the registered set, and at least one pass fails; a case whose verdict is the test bound, a deadline or a stop-timeout counts as failing. One record file per witness, `wx`, under the package run folder, with every pass's set and the walls (the cost). | A witness run is never shortened to `--only`: the "no other case" half needs the whole suite. The cost of 05 is written into its record and quoted in the stories document (counts and durations) before the other five start. |
| `lib/package-suite.mjs` (new) | The R-id check (`**R<n>.**` ids parsed from the spec text, the way `conformance-spec.mjs` does — import it from lang-bench, do not re-parse), the comparison-word check (FORMAT 3.1's list, from `conformance-facts.mjs`), the manifest/folder/`cases.txt` agreement (reusing the held-out reader's problems), the trap table checks (every trap-and-stack row: ≥ 1 existing case, exactly one witness), `traps.txt` as the union. | Text in, findings out; no I/O beyond the folder reads. |
| `lib/package-record.mjs` (new) | The private run folder `~/rsd/lang-v2/explore/package-NN/<yyyymmddTHHMMSSZ>-<gen>/` (the name shape of `repartition`), `record.json` written once at the end (items P1–P8 and P10 with `ok`, the counts, the hashes, the F-LOAD numbers, the witness cost, the trace lines the operator adds for a failure), and `registerPackage()` → `inputs/packages.json` (counts, hashes, durations only; the whole file rewritten with sorted keys; refused while any item is red or the material's hashes moved). | Append-only folder; nothing in it is ever edited. |
| `lib/memory-limit.mjs`, `inputs/memory-limit.json` | `registerMemoryValue({ packages })`: writes the `registration` field (date; twelve `{ task, stack, treeSha256 }`) only when `packages.json` holds all six tasks with both references `accepted` at the file's `bytes`, and rewrites the note ("chosen" → the field); `memoryLimit()` returns `registration` (or `null`) beside `bytes`. The loader's checks stay; a file with the field whose twelve identities are not twelve distinct task-stack pairs is refused. A scan test holds that no other module writes the field (the shape of the one-source scan of the figure). | One writer. F3.S1's guard (`series.mjs`) reads the field; that half is F3.S1's. |
| `lib/package-seal.mjs` (new) | `sealPackage({ casesDir, keyFile, outDir })` and `unsealTo({ archive, keyFile, clearDir })` over minigit's `sealSuite`/`unseal` (`spikes/minigit-bench/lib/heldout-seal.mjs`, `heldout-crypto.mjs`: AES-256-GCM, the key a mode-600 file outside every git tree and box); `clearFolderProblem(dir, archiveHash)`: mode not 700, or the tree hash not the archive's → a sentence; `removeClear(dir)` after each pair and at the start of a run after an interruption (`removeUnder` of `policy-ab-quick/lib/safe-rm.mjs`). The archive lives at `~/rsd/lang-v2/explore/heldout-NN/suite.enc`, the clear working folder is `heldout-NN/cases/` itself (made again from the archive when absent). | The seal is written once per package state; a re-seal after a case change is a new archive hash and a new identity. F2.S2 registers the hash; F3.S1's series uses `removeClear` after each pair. |
| `lib/commands.mjs`, `explore.mjs` | The command `node spikes/lang-explore/explore.mjs package --task NN [--only refs\|witnesses\|suite\|load\|all] [--register] [--seal] [--accept-noisy <why>]`, FREE, in `REEXEC` (re-executed under `taskset -c 6,7`), the series lock taken as `rescore` takes it, the quiet check as `measure-growth.mjs` runs it (a package validation is a measurement of the references' peaks and walls). A separate verb, `register-memory`, reads `packages.json` and writes the field (P9). Exit 0 green, 1 an item red (the sentence names the item and counts), 3 a resumable halt, 4 usage or a refusal, 5 a failure. | One command; the sub-steps are the modules above. The conformance check is run by the operator, not by this command (it lives in lang-bench and needs the task repository): the command reads the header row and the gate record's existence and says so. |
| `lib/leak-material.mjs`, `lib/leak.mjs` | `codeFiles(root)` already reads `mutants/` under the held-out root and `quickRefFiles(refsDir)` reads `refs-NN/**` recursively: confirm with scenario 26, change nothing unless red. New: the collision rule extended to every file under `spikes/lang-bench/templates/**` at HEAD (read through git like the runner's lines; `committedRunnerLines` is the model), so a template line inside a reference folder is a collision. `idsOfHeldout` reads a quick task's `manifest.witnesses` keys beside `traps.txt`. The gate-record scan of scenario 29 (`leakGates`) over `spikes/lang-bench/inputs/gates/`: case ids, witness and trap ids of every manifest, exact case lines. The both-repositories scope and the publication-commit scan are build step 2b's, merged before this story's first spec pull request. | A code line that is a template's line is dropped from the needles only; case lines and note shingles never are. |
| `lib/heldout-quick.mjs` | `loadHeldoutQuick` also returns the manifest when present (`manifest.json` read with its per-case hashes checked against the files); a manifest with an unknown top-level key is refused naming the key; an absent `cases/` beside a present `suite.enc` is "sealed, not unsealed", not "absent". | The tree hash stays what it was (F2.S2 registers it). |
| `inputs/explore.json`, `inputs/packages.json` (new) | Per task the `spec` pin moves to the corrected text's sha and sha256 on the task repository's `main` (after its pull request merges by merge condition 5); the six `format` pins and `spikes/lang-bench/inputs/specs.d/format.json` are the pins branch's (`c204726`, sha256 `6c3546bb…`), merged first; `privateNotes.sha256` moves with the test-design files. `packages.json`: `{ "NN": { specSha256, formatSha256, heldoutTreeSha256, archiveSha256, refsTreeSha256: { csharp, node }, witnessesTreeSha256, counts: { cases, traps, witnesses }, memoryBytes, load: { slowestWallMs, twinWallMs, durationS }, gate: { verdict, date, reviewers }, identity, validatedOn } }`. | Hashes, counts and durations only; a scan test (scenario 23) holds it. |
| `spikes/lang-bench/inputs/gates/spec-NN.md` (new, six) | The gate record in `FORMAT-v2.md`'s shape, reduced to D25's leak rule: the checker's `ok format sha256 …` line, each round's verdict, the reviewer count, the findings in words that name no case, the per-stack bias answer in words, what was declined and why. | No case text, case id or trap name; scenario 29's scan and `leakcheck`'s shingles hold it. |
| The task repository (six pull requests, one per spec, none before build step 2b has merged) | The corrected spec and its samples; the header row; the eight fixed sentences; F-LOAD's measured duration; task 04's sample files and placeholder names; nothing else of substance. Each pull request carries the conformance line, the gate verdict, and the bench-side checks' commands, exits and head sha (merge condition 5); `leakcheck` over both repositories and the line scan run before each merge; the bench pins pull request runs them again against the merge commit. | A spec change after its gate is a dated amendment in section 8, applied to both stacks (FORMAT 1.3). |
| `research/module_lang_explore.md`, `research/module_tests.md`, `research/module_lang_bench.md` | The package tool's section (what a package is, the command, the records, the registry, the two witness rules, the registration field, the seal and the clear folder's lifecycle), the scenario table with observed numbers and load, the gate records' row. | Doc lines only. |

**Where each private artefact lives, so both leak checks read it** (merge condition 3; the line scan reads `heldout-NN`
and `refs-NN` recursively, `leakcheck` reads the case lines, `refs-NN/**`, `mutants/**`, `gen.py`, the oracle files and
the test designs' shingles):

- `~/rsd/lang-v2/explore/heldout-NN/cases/` — the pairs and `cases.txt` (the clear working folder, mode 700, remade from
  the archive); `heldout-NN/suite.enc` — the sealed archive; `heldout-NN/traps.txt`; `heldout-NN/manifest.json` (cases,
  traps, witnesses); `heldout-NN/mutants/<id>/…` (the witness overlays; the folder keeps the plan's name); `heldout-NN/twins/`
  (the load cases without their load steps, for the F-LOAD rule); `heldout-NN/gen.py`.
- `~/rsd/lang-v2/explore/refs-NN/csharp/`, `refs-NN/node/` — the two references (agent-written files only); `refs-NN/ref.py`
  stays as the generator's oracle.
- `~/rsd/lang-v2/explore/package-NN/<run>/` — the records (they hold case ids; the leak checks do not read this folder
  and nothing publishes from it).
- `~/rsd/lang-v2/test-design/NN.md` — the designs, updated with the real case ids (pinned by sha in `explore.json`);
  `test-design/needles-NN.txt` — the conformance needles, one per line, for `--needles`.
- The scratch folder of the bias review — outside every repository and every private folder the harness reads (a temp
  folder under the operator's scratch area), made for the round and removed after it.
- The seal key — one mode-600 file outside every git tree and box, as `heldout-tool.mjs keygen` makes it.

**Growth.** Per task: the records of two ten-pass validations (about 0.8 MB each, F1.S2's measurement), one record per
deterministic witness (a few KB) and ten passes' records per race witness (about 0.8 MB), kept for ever under
`~/rsd/lang-v2/explore/package-NN/`, append only; one archive per task beside the cases; the built trees live only during a
run and are removed with it (the Node tree with its packages is about 58 MB). Six tasks: well under 1 GB in all.
Interrupted: a run resumes from its records (scenario 35), a dead run's temporary files are removed by the next
(`publish-once.mjs`), and a stale clear folder is removed and remade from the archive (scenario 37).

**Commands, one suite at a time, lowered** (`LOW=.agents/conventions/tools/low-priority.mjs`): the three suites of the F1
stories document's §3 with `BENCH_REQUIRE_SANDBOX=1` and plain; the three `--check`s; `leakcheck` over both repositories and
the line scan; `plan-lifecycle`. Real C# builds keep `-m:4`; documents are written with Write or Edit, never a heredoc
that quotes a build line.

---

### 4. The order across the six tasks

**Task 05 first, end to end** (spec fix, gate, samples, two references, the load rule, suite checks, witnesses, leak
checks, seal, registry), **its witness cost recorded** (walls and sizes per witness kind, in the private record and as
counts and durations in the stories document), then **04, 06, 07, 08 and 10 in parallel**. Parallel means the
authoring: the spec fixes, the references and the witnesses are written at once by separate coders; the validation runs
serialise on the scoring CPUs under the series lock, one task's run after another's, because scoring never shares the
CPUs with anything (the plan's *The scoring CPUs*). The memory value is registered once, after the sixth package.

Why 05 and not 04 (the plan now fixes 05; the reasons stand):

1. 05 is the task whose spec makes the memory bound itself a tested behaviour (its R24, on releasing the memory of what
   no longer exists; its R26, on serving every other connection while one reads slowly). A witness there can be caught
   only by the memory bound in the certified box, a package path that no other kind of trap needs and that the first task
   must prove or disprove before five tasks run in parallel. Task 04 has no such path.
2. 05's suite is the slowest per pass (expiry margins, a slow-reader wait, a churn load), so its ten-pass cost — and the
   cost of a race witness's ten passes — bounds the budget for the other five honestly; 04's would flatter it.
3. 05 is the known task with needles (D18), so the `--needles` half of the conformance check and the needle half of the
   push scan are exercised on the first package.
4. 05 is a daemon over TCP with byte-exact replies, the shape 10 shares; 04's HTTP and JSON path is then exercised by
   three tasks at once (04, 06, 08) on a `runCases` path F1 already ran over the quick run's 36 sessions of both kinds.

Against 05 first: 04 is the web pilot and has one more defect (no sample files in git). Both are small.

---

### 5. What could go wrong, and the scenario that catches it

| Where a package could let a wrong verdict through later | Caught by |
|---|---|
| A reference passes nine passes of ten and is called green, so a flaky suite case ships and rejects honest sessions | 4 (green only on `accepted`, ten of ten, both stacks) |
| A reference is validated under another memory limit than the series enforces (a stale `memory-limit.json`, a changed cgroup) | 5, 33 |
| The memory value is called registered while a reference has not passed under it, so the series runs on an unconfirmed bound | 25; F3.S1's guard reads the field |
| A load scenario fits 540 s on paper and not on the scoring CPUs, so an honest program fails the test bound | 20 (measured over every pass of both references under both profiles, with the non-load twin) |
| A witness "is caught" because its build failed, so a trap looks covered that no case catches | 12 |
| A witness equals its reference, so a trap is "covered" by a program that is the reference | 8 |
| A witness is caught by a case outside its trap, and its own trap's cases never fired; or a wide defect fails cases the table never registered, so the table lies about what each case catches | 9, 10, 11 ("no other case"; an extra failure is a mapping to fix) |
| A race witness passes on the 8-CPU profile and is called caught, or fails once by a hang that is read as "no verdict" | 11, 13, 14, 32 |
| A race witness is judged by a majority of clean passes | 11 (judged on its failing passes only) |
| A trap-and-stack row has no witness, or two, so "one per trap per stack" is not what the manifest says | 15 |
| A case cites a requirement the corrected spec no longer has, or none, so a failure cannot be adjudicated | 16 |
| A case file was edited after the generator ran, so the manifest's hash and the file disagree | 17 |
| `traps.txt` and the trap table drift, so the record's trap-hit count means another thing than the table | 18 |
| The package validates one spec text and another freezes | 19, P1's pin |
| A spec is called gated while the checker fails or the header says otherwise | 21 |
| The registry names a case id, a trap id or a path | 23 |
| The registry is written from a stale run | 24, 22 |
| A reference line or a witness line enters git unseen | 26; the line scan by hand before each merge; 2b's both-repositories scope |
| The leak check refuses to run because two stack references push the needle count over the cap, and someone disables it | 28 (fails closed with the count; the fix is fewer needles, never a weaker check) |
| The template's own lines inside a reference folder flood the check with false findings, and the findings are then waved through | 27 |
| A gate record carries a case id, a trap name or a case line in a finding | 29 |
| The suite goes to the vendors from a repository checkout, so a scratch copy of it is left where git or a box can see it | P6 (a scratch folder outside every repository); the operator's record of the folder's removal |
| A reference folder carries a frozen file, so the package scores a different `build.sh` than the series will | 2 |
| A package run overlaps a session or another scoring and the measured peaks and walls are wrong | 34 |
| A halted run is restarted from scratch and the earlier records are overwritten | 35 |
| The clear cases stay on disk between pairs, world-readable, or drift from the archive | 37, 36 |
| The load cases are too small to let a program that keeps what the spec says it must release cross the limit inside the test bound, so a trap the spec names is never catchable | 31 for the tool's half; the sizing itself is a private matter of the first daemon package and is recorded in its trace lines |

---

### 6. Fairness: two references of comparable care

Neither stack's reference may fail for a reason of its own, because a failing reference moves the spec or the suite for
both stacks (D72, the joint validation). How the package keeps them comparable:

1. **Same brief, same template, same effort.** One Sonnet coder per stack per task, from the same corrected spec and the
   same private notes, on the stack's own template with the offered packages only (`PACKAGES.md`), under D66's build
   policy; no flag, no file and no setting an agent could not use. The two coders do not see each other's code.
2. **One judge reads both before validation.** An Opus judge reads the two references side by side and writes one line
   per task: of comparable care, or what one has that the other lacks (error handling per connection, a bounded write
   queue, a sweep, a lock). A gap is closed before the ten passes run, so the validation measures the suite and the spec,
   not one coder's afternoon.
3. **The same ten passes, at the same value, on both profiles** (section 1 P3), and the record keeps each reference's
   largest peak and slowest wall per pass beside the other's. A reference near the limit or near a deadline is seen; the
   plan's rule applies (a reference that cannot pass is fixed, or the value moves for all six — never for one, D72) and
   the deadlines of the suite, the load durations included (P3's measured rule), are set so both references meet them with
   margin (F-DEADLINES), never so one does.
4. **Nothing in a reference is tuned to the scoring CPUs**: a reference that passes only on one profile is a defect of the
   reference (or of the case's margin), traced and fixed, never a reason to drop a pass.
5. **A reference is not a model answer.** It never enters a workspace, a prompt, a run base or git; agents never see it
   (D40, D41, item 11: the references and the trap table stay private past the freeze). Its only job is to show that the
   suite is passable in that stack by a program of ordinary care.
6. **The trap table is reported per stack** (T2): where one stack has more trap rows or heavier ones, the table says so
   and the witnesses per stack stand beside it; `defects` in the series counts distinct trap classes, so one wide trap does
   not outweigh several narrow ones. The witness rule does not differ by stack: the same two kinds, the same passes,
   the same "no other case".

---

### Ambiguities in the plan found while writing (for the team lead; also in the hand-back report)

Settled by the epoch-10 revision (kept here as the record): the bias question's document and its leak rule (D25); the
registration of the memory value as a field (D72); FORMAT PR #4 merged, the bench pins as build step 2a; the first task.
Still open or new:

1. "Every case cites an R-id" is read as "an R-id that exists in the corrected spec", checked against the pinned text
   (there is no `specs.json` entry for the six before F2.S2).
2. The analysis cites "known defects" of the draft specs; no written list exists. The conformance run of 2026-11-07
   against the amended FORMAT (section 1, P1) is the list now; one task also lacks its sample files in git.
3. The test-design files change with the package (the table gets real case ids), so `explore.json`'s private-notes
   hashes move in the same pull request.
4. **The F-LOAD rule's "non-load part"** is not defined by the plan. This spec reads it as the wall of the same scenario
   with its load steps removed (a twin scenario, run once per reference through the same seams), because the runner
   records a case's wall and not a step's. If the team lead prefers a per-step measure, the runner's record must grow by
   per-step timing first (a runner pin move).
5. **"One per trap per stack" and the "no other case" rule together** make the trap table name every case a wide defect
   fails (a defect that fails every scenario is registered to every case). That is what the rule asks; it is said here so
   the table's size is not read as a mistake.
6. **Race witnesses cost ten passes each.** The plan asks for 05's cost to be measured first; if the cost is too high for
   the other five, the decision is the team lead's (fewer race rows, or more scoring time), never a shortened run.

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
start-of-run removal into the series.

**Every reader of a task's clear held-out folder fails closed** (the team lead's decision, 2026-11-08). When the folder is absent
and a sealed archive is registered for the task in `heldout-frozen.json`, a reader refuses with a named reason, `sealed: open it
first` (with the task), and never reports clean, empty or "absent". A reader that needs the cases opens the archive itself
through `heldout-open` and closes it after through `heldout-close`, in a `finally`, so a failure in between does not leave the
folder behind. One function decides it, `sealedAbsentProblem({ home, task })` in `lib/heldout-quick.mjs`, and every reader below
calls it before it reads (`common.reuse-first`, one road in). A task with no archive registered keeps today's behaviour. The
readers, found by a search for `heldoutDir`, `loadHeldoutQuick`, `casesTreeHash` and `heldout-` over `spikes/` and the private
scripts on 2026-11-08:

| Reader | Today, with the folder absent | After |
|---|---|---|
| `loadHeldoutQuick` (`lib/heldout-quick.mjs:86`), and through it `loadHeldout` (`lib/heldout.mjs:43`) | `fail(…, { absent: true })`, which callers may read as "nothing here" | the `sealed` refusal, with no `absent` flag, so no caller can take it for an absent task |
| `leakcheck`'s task loading and material (`lib/leak.mjs:83-89`, `:112`; build step 2b widens it) | **fails open:** `scanProblem` skips a task whose folder is `absent` when any other task's folder is present, and the clean line lists it as absent | the `sealed` refusal stops the scan (exit 4, never 0); to scan a sealed task, `leakcheck` opens it, scans and closes it |
| `leakcheck`'s link check (`lib/leak-material.mjs:44-51`, `privateLinks` over `heldout.dir`) | runs only over the tasks that loaded | covered by the row above: a sealed task loads only after `heldout-open` |
| `init` of a quick series (`lib/init-quick.mjs:38`) | refuses on `absent` | the `sealed` refusal, naming `heldout-open` |
| the scoring's copy of the held-out pairs (`lib/score-quick.mjs:53-66`, `copyHeld` from `heldoutDir`) | reads the pairs a loaded held-out object names | it scores only from an opened folder whose tree hash equals the archive's, and refuses on `sealed` |
| the line scan (`~/rsd/lang-v2/briefs/leakscan-lines.py`, outside git, `:30`) | **fails open:** `glob` over a missing `heldout-NN` gives no files, so fewer needles and an exit 0 | exits 2 ("the private sets could not be read") naming `sealed: open it first` when `heldout-frozen.json` in the bench checkout lists the task with an archive and its folder is absent; a script change outside git, recorded by the team lead as a dated line in `inputs/checkpoints.md` |
| `heldout-freeze` and `heldout-open` themselves (d) | — | they read the archive, never a stale clear folder |

`lib/material.mjs` (`materialOf`) and `lib/oracle.mjs` read the olympiad tasks' folders only; those are not sealed in Stage 1, so
they do not change, and the scenario below shows they are not reached for a quick task.

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
- **Fail closed, one scenario per reader** (`spikes/lang-explore/test/quick-heldout-sealed-readers.test.mjs`, new; each fixture is a
  sealed invented task registered in a fixture `heldout-frozen.json`, with no clear folder; each test asserts a refusal naming
  `sealed: open it first` and the task, never a pass, a clean line, an empty result or an `absent` flag; each has a control, the
  same task opened through `heldout-open`, which reads, so the refusal comes from the rule and not from a broken fixture):
  `loadHeldoutQuick_refuses_a_sealed_task_with_no_clear_folder`;
  `loadHeldout_refuses_a_sealed_quick_task_with_no_clear_folder`;
  `The_leak_check_refuses_a_sealed_task_with_no_clear_folder_even_when_another_tasks_folder_is_present` (exit 4, never 0: the
  fail-open of `scanProblem` today; its red is watched on today's code);
  `The_leak_check_link_scan_is_not_reached_for_a_sealed_task_with_no_clear_folder`;
  `Init_refuses_a_sealed_task_with_no_clear_folder_and_names_heldout_open`;
  `Scoring_refuses_a_sealed_task_with_no_clear_folder_and_copies_no_pair`;
  `A_reader_that_opens_the_archive_closes_the_folder_after_it_also_when_it_fails` (the `finally`: a fault planted after the open
  leaves no clear folder);
  `The_olympiad_readers_are_not_reached_for_a_quick_task` (`materialOf`, the oracle).
  The line scan, outside git: the team lead runs it once on a sealed fixture task with no clear folder (it must exit 2 naming
  `sealed: open it first`) and once opened (exit 0), records both exits in the dated line, and watches the first exit 0 on
  today's script first, as its red.
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
3. **Settled 2026-11-08 by the team lead:** every reader of a task's clear held-out folder fails closed; (d) holds the rule, the
   readers and one scenario per reader. Two of today's readers fail open (`leakcheck`'s `scanProblem` and the line scan). Build
   step 2b's `leakcheck` must carry the rule too, since it widens the same code.
4. **Settled 2026-11-08 by the team lead:** v1's seal tool is widened, never copied; how much widening it needs is found by its
   first red test.

## 8. Definition of Done (F2)

- [ ] F2.S1: done as its spec says (the parent's acceptance: per task, the package record in the private area and a public line
      of counts only; all twelve references pass; the memory value registered by its field).
- [ ] F2.S2 (a): not F2's; build step 2a merged before F2.S1's first conformance run, checked.
- [ ] F2.S2 (b): merged; the two rulesets (three GitHub rulesets) applied under the owner's go of 2026-11-08 and read back
      `equal`, recorded.
- [ ] F2.S2 (c): the decision line exists with its numbers; if built, merged and the re-runs done.
- [ ] F2.S2 (d): the freeze tools merged, every scenario red, green, red.
- [ ] Every reader of a task's clear held-out folder fails closed: each of (d)'s readers refuses a sealed task with no clear
      folder (`sealed: open it first`), one scenario each red, green, red; the line scan's two exits recorded.
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
