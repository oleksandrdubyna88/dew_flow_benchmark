# PLAN — language comparison v2, F1 stories: the harness and the verdict — the carry-ins, repeated scoring with the one memory value, the submission contract and the offered set

> Status: **plan only, 2026-11-07: F1's stories document, written when F1 starts (the parent's epoch 9 is open). F1.S1 (a) is recorded as done here; F1.S1 (b) to (g), F1.S2 and F1.S3 are open ((f) reopened 2026-11-07: only its plan-id half is done). F1.S2 is a cornerstone: its spec is Fable's and is not yet copied in.**
> Epic F1 of [PLAN_lang_comparison_v2.md](PLAN_lang_comparison_v2.md) (*Operative plan for epoch 9*, *Epics in force*, F1). A
> stories document keeps no ledger: its changes are entries in the parent (`common.plan-change-control`), and writing it when
> the epic starts is not a change. Read at `main` `0214a3eb` (#178, the epoch 9 revision). Scope: `spikes/lang-explore/`,
> `spikes/lang-bench/`, one function of `spikes/minigit-bench/lib/classify.mjs`, the module docs, one dated line in
> [PLAN_bench_test_leftovers_followups.md](PLAN_bench_test_leftovers_followups.md), and the private area `~/rsd/lang-v2/`
> (read, and one new file per quick-run series written there).

## 1. The epic, restated (parent, F1)

**Goal.** `spikes/lang-explore/` is the base; nothing is built twice; a session's verdict comes from ten scoring passes under
two CPU profiles with one stated rule; the memory limit is one registered value, enforced and stated alike (parent D70–D72).

**Needs.** The stop lifted: the parent's epoch 9 line (2026-11-07, by the owner).

**Done when (parent).** lang-explore's tests are green, plain and with `BENCH_REQUIRE_SANDBOX=1`; the defective quick-run tree
is `intermittently incorrect` under F1.S2 on the scoring CPUs, and accepted quick-run sessions of both stacks read
`accepted` 10 of 10 under both profiles at the registered limit (the references' check is F2.S1's; change 9.1); `build_test` is not zero for a C# session of a retained stream; the memory figure has one
source and the enforcement check passes in both stacks; `research/module_lang_explore.md` describes the one engine.

**Not in this epic.** The validation packages, the spec fixes, the freeze and the rulesets (F2); the series over six tasks,
the pre-registration, the pilot and the report (F3); D65's lexical deviation taxonomy and the per-case `cpuMs` and RSS curve
(follow-up work, D74); anything of Stage 2 (D76; merge condition 4). The olympiad controls' own templates
(`spikes/lang-explore/templates/`) are outside the claim and do not change.

## 2. What exists — verified 2026-11-07 at `main` `0214a3eb`

- **#164 and #167 are merged**, on `main`'s first-parent history: `8dcbdee2` (E1.S8, the conformance checker) and `c8595952`
  (E1.S3 phase A, the templates and the builder). #167 was squash-merged, so its doc commit `714176db` is not an ancestor of
  `main`, but its content is there: `research/module_lang_bench.md:901` and `:926` carry the "superseded by parent D65" marks.
- **The classifier.** `spikes/minigit-bench/lib/classify.mjs:107-116` (`isBuildTest`) with `isHarnessScript` at `:91`
  (`test-v*.sh`, `run-tests.sh` only). Probed 2026-11-07: `classifyCommand('bash build.sh')`, `'./build.sh'`, `'bash run.sh'`
  and `'python3 -I runner/run_samples.py samples/'` all return `other_tool`; `npx tsc --noEmit` returns `build_test`. The
  quick run's C# records read `build_test: 0` (one checked: a task 06 C# session's `record.json`). lang-explore's own
  `isBuildTestCommand` (`spikes/lang-explore/lib/records.mjs:12,16`, `explore.buildTest`) already counts all four. The note
  text `spikes/lang-explore/lib/note-tasks.mjs:55` says the classifier "is not widened". The partition is built by v1's
  `smokeViews` (`spikes/minigit-bench/lib/smoke-verdicts.mjs:58`) from `spansFrom` and `partition`
  (`spikes/minigit-bench/lib/timeline.mjs:68`). Each quick-run session keeps its evidence under `<series>/s/<rid>/`
  (`stream.ndjson`, `stream.stamped.ndjson`, `otel/`, `meta.json`, `record.json`); the six series are listed in
  `~/rsd/lang-v2/briefs/quick-series.txt`.
- **The C# compile and the copy.** The template's `Directory.Build.props` excludes `**/bin/**;**/obj/**;**/node_modules/**`
  (quoted at `research/module_lang_bench.md:901`); the clean copy drops only the exact names (`copy-quick.mjs:12,24` with
  `excludedDirs`, `spikes/minigit-bench/lib/score-deps.mjs:10`). The module's sentence today reads "so a build in place and a
  build on the copy agree" (`:901`; the carry file's `:825` has drifted). No test holds `Bin/`, and neither the module doc nor
  any `spikes/*/test/` says "letter case" (searched 2026-11-07).
- **The held-out reader.** `spikes/lang-explore/lib/heldout-quick.mjs:14-22` (`casesTreeHash`) reads every entry of `cases/`
  with `readFileSync`, so it follows a link and throws `EISDIR` on a folder; `isDir` (`:53`) uses `statSync`, which follows a
  link. Its callers: `loadHeldoutQuick` (`:59-68`) and `spikes/lang-explore/lib/rehearsal-quick.mjs:37`. The same class
  elsewhere: `spikes/lang-explore/lib/leak-material.mjs:27` keeps only `isFile()` entries, so a linked file under a private
  folder is skipped in silence by `leakcheck`; `spikes/lang-explore/lib/heldout.mjs:12` (the olympiad reader) reads through
  links too.
- **FORMAT's pins branch.** `feat/lang-v2-format-amend-pins` (worktree `/home/jinx/git/wt/bench-format-pins`), local, not on
  the remote (`git ls-remote` finds no such branch), 5 commits on `c8595952` (`92e5dca8` … `b7bba0d3`, 19 files, 90 lines added
  and 52 removed), ahead 5 and behind 7 of `main`. `git merge-tree --write-tree main feat/lang-v2-format-amend-pins` is clean
  (exit 0) at `0214a3eb`. `main` still pins the old FORMAT, all under `spikes/lang-bench/`: `inputs/specs.d/format.json:4,6`,
  `inputs/specs.json:4-5`, `inputs/hashes-lang.txt:4`, `lib/task-repo.mjs:16` (`40e678fe`). The task repository's `main` is at `4ddb8f8` (PR #3); the
  checkout `/home/jinx/git/dew_flow_ai_language_comparison` is there, while `/home/jinx/git/wt/task-pinned` is still at
  `40e678f`. Its RED, GREEN, RED record is `~/rsd/lang-v2/briefs/format-pins-reds.txt`; its brief, `format-pins.md`, beside it.
- **The quick world's own FORMAT pins.** `spikes/lang-explore/inputs/explore.json` pins each quick task's spec and FORMAT at
  the spec's draft branch (FORMAT labelled `v2.1`, sha256 `4a4e86c4…`, for all six); `lib/world.mjs:96` reads them, and
  `workspace-quick.mjs:75` extracts `runner/GRAMMAR.md` from that FORMAT. lang-bench's FORMAT pin is not read there.
- **The agent-visible plan-id test.** `spikes/lang-explore/test/quick-agent-visible.test.mjs` (#173, `00d88233`) reads
  `lang-bench/runner`, `lang-bench/templates` and `lang-explore/runner`; run 2026-11-07 on `main`: 2 of 2 pass. Its pattern
  catches plan ids, F ids included. It does not look for stack names; `spikes/lang-bench/runner/rs_abort.py` names `.NET`,
  `dotnet` and `v8` in comments and docstrings (`:2`, `:6`, `:43`, `:58`, `:79`), and two of those words are also return values.
- **The wall-clock C# test.** `tests/Bench.Tests/ModelArms/WslHostAwakeTests.cs:349`, `Patience` 15 s at `:24`, its
  `Eventually` at `:357`. The leftovers plan's S5 does not name it (searched 2026-11-07).
- **The submission contract today.** `copy-quick.mjs` (44 lines: `quickPristineCopy`, `splitFrozenChanges`) and
  `build-quick.mjs` (52 lines: the marked build, `buildOutcome`, `boxBuild` over `lang-bench/lib/workspace-box.mjs`). The
  frozen list lives in `workspace-quick.mjs:20-23` (`PIN_FILES`) and `:98-101` (`frozenOf`, with the scripts, `STATEMENT.md`,
  `PACKAGES.md`, `samples/` and `runner/`); a second reading of it is `spikes/lang-bench/test/templates.test.mjs:92-108`
  (`AGENT_EDITABLE`, `DERIVED_OR_SCRIPT`); the build scripts name the pin files in a comment line. `templates/stack-files.json`
  is a different list (the push check's file patterns) and so is `lib/push-visible.mjs:9`. The records carry
  `explore.frozenChanges` (`records.mjs:60`) and `run.harnessIdentity`, v1's tree identity (`prereg-guard.mjs:31,47`), but no
  contract identity and no `deviations`. Runner drift is told apart from the agent's changes (`harness-pin.mjs:16`,
  `copy-quick.mjs:41`); template drift is not.
- **The Node `.npmrc`.** The three `spikes/lang-bench/templates/node/*/.npmrc` hold `offline=true` and
  `update-notifier=false`, which is `NPMRC_WORKSPACE` (`spikes/lang-bench/lib/workspace-build.mjs:71`), checked at
  `test/templates.test.mjs:155`. Neither `ignore-scripts` nor `save-exact` is there.
- **The offered set** (paths under `spikes/lang-bench/`). `inputs/cache-recipe-lang.json` lists five C# packages (Hosting, JwtBearer, xunit.v3, its runner, the
  test SDK) and ten Node packages; `Microsoft.AspNetCore.Mvc.Testing` is not among them. `inputs/package-matrix.json` does not
  exist. `lib/packages-md.mjs:78` (`renderPackagesMd`) renders `PACKAGES.md` from the record and the recipe, and the quick
  workspace uses it (`workspace-quick.mjs:77`). The registered snapshots: C# 174 MB, Node 178 MB, under `~/rsd/lang-v2/caches/`.
  `make-snapshots.mjs` supersedes only before the pre-registration tag and prunes only after it, and
  `lib/cache-store.mjs:205` reads that tag as `prereg/lang-v2-A-*`, which Stage 1's tag `prereg/lang-v2-<yyyymmdd>` (parent
  F3.S1 (b)) does not match.

## 3. Conventions for every story

- **Roles (`common.subagent-models`).** This document's scenarios and briefs are Opus's. A Sonnet coder writes the code and
  the tests from them; an Opus judge reviews; a Sonnet coder writes the fixes. F1.S2's scenarios and brief are Fable's. Every
  launch names its model as `sonnet`, `opus` or `fable`. §8 has the table.
- **The merge conditions are the parent's** (*Merge conditions*, 1 to 4) and §7 says which of them bites for each pull
  request. Never weaker: a whole completion run at the head sha that merges, plain and with `BENCH_REQUIRE_SANDBOX=1`, every
  test passed and nothing skipped, CI's required checks present and passing for that sha; the same test failing in two
  consecutive whole runs stops the story until it is diagnosed; every failure on the way goes to the leftovers plan's S5 by a
  dated line, with `/proc/loadavg`.
- **Red, green, red (`common.testing`).** A test is written first and watched failing for the real symptom, then green, then
  the production line it depends on is removed and the test watched failing again before the line is restored. Both
  observations go in the story's summary and in a reds file under `~/rsd/lang-v2/briefs/`. A pin of behaviour that already
  holds (F1.S1 (c)) has a control case that fails, so the pin can fail.
- **Test names** state the guarantee as a sentence with underscores, in the style of the file they join
  (`The_held_out_reader_…`, `A_…_and_that_is_the_point` for a guard against a plausible wrong approach). Never a story id.
- **No held-out content, case id or trap name (D41)** in a test, a fixture, a comment, a commit message or a pull request.
  Tests use invented fixtures in owned folders (`ownedFor({ token: 'bench' })`), never a quick-run tree, a reference or a
  private file. A check that needs the private side (`leakcheck`, the line scan) reads it on the host.
- **Shape.** Plain ESM on Node 26, no new dependency; functions under 50 lines, complexity at most 4, files under 400 lines
  typical; expected failures are values (`ok` / `fail`), never a throw for control flow; an unknown is `{ known: false, why }`,
  never 0. Reuse before writing (`common.reuse-first`): every brief names the code to call.
- **Real C# builds keep `-m:4`.** A Bash heredoc that quotes a build line trips a hook, so documents and briefs that quote one
  are written with Write or Edit.
- **Commits** by path, never `git add -A`; `git diff --staged --stat` read before each commit; no force-push and no history
  rewrite of a pushed branch.
- **The commands, one suite at a time, lowered** (`LOW=.agents/conventions/tools/low-priority.mjs`; `TASK` is a task
  repository checkout at `4ddb8f80`, the one at `/home/jinx/git/dew_flow_ai_language_comparison`):
  - lang-bench: `BENCH_REQUIRE_SANDBOX=1 LANG_V2_TASK_REPO="$TASK" node $LOW node spikes/lang-bench/run-tests.mjs`, then plain
    `node $LOW node spikes/lang-bench/run-tests.mjs`;
  - minigit-bench: `BENCH_REQUIRE_SANDBOX=1 node $LOW node spikes/minigit-bench/run-tests.mjs`, then plain;
  - lang-explore: `BENCH_REQUIRE_SANDBOX=1 node $LOW node --test --test-concurrency=8 spikes/lang-explore/test/`, then plain;
  - the pins: `node spikes/lang-bench/pin-runner.mjs --check`, `node spikes/lang-bench/specs-record.mjs --check`,
    `node spikes/lang-bench/pin-exec-layer.mjs --check`;
  - D41: `node spikes/lang-explore/explore.mjs leakcheck` and `python3 -I ~/rsd/lang-v2/briefs/leakscan-lines.py . history`;
  - plans: `node .agents/conventions/tools/plan-lifecycle.mjs`.
- **Docs.** Each story updates `research/module_lang_explore.md` or `research/module_lang_bench.md` for what it changes, and
  `research/module_tests.md` with the observed numbers and the load of its whole runs.

## F1.S1 — the carry-in merges

**Model:** scenarios + brief Opus (this document) · code Sonnet · review Opus · fixes Sonnet. **Parent:** F1.S1, D75; it
replaces §14 A1's steps 0 to 2. **Accepted when (parent):** (b) and (d) each have a test watched failing first, and (c)'s pin
test passes on today's code; the recomputed partitions file exists; `pin-runner.mjs --check` and `specs-record.mjs --check`
exit 0 on the moved pins.

| Item | What | State on 2026-11-07 | Pull request (§7) |
|---|---|---|---|
| (a) | #164 and #167 merged | **done**: `8dcbdee2`, `c8595952` | none; recorded here (PR 1) |
| (b) | `build_test` counts the build, run and sample commands; partitions recomputed | open | PR 4 |
| (c) | the C# compile's letter case recorded, never changed; carry items 2 to 4 | open | PR 3 |
| (d) | the held-out reader refuses anything but regular files | open | PR 5 |
| (e) | the bench's side of FORMAT's first amendment merged forward | open (branch built, unmerged) | PR 2 |
| (f) | T-L64's text half: no plan id and no stack name in an agent-visible runner comment, the scan in the push check, the `ast` proof | **open**: the plan-id half is done by #173 `00d88233`; the rest is not | PR 6 |
| (g) | the wall-clock C# test goes to the leftovers plan | open | PR 3 |

### (a) #164 and #167 — done

**Proof.** Both are on `main`'s first-parent history, merged on 2026-11-05: `8dcbdee2` (#164, E1.S8) and `c8595952` (#167,
E1.S3 phase A). #167's doc commit `714176db` arrived inside the squash: the "superseded by parent D65" marks stand at
`research/module_lang_bench.md:901` (the rule for E3's scorer) and `:926` (the input for E3's plan round). Nothing is merged
again and nothing is built. **Done when:** this document records it, which it does.

### (b) the `build_test` classifier, and the partitions recomputed from the retained streams

**Scenarios** (`spikes/minigit-bench/test/classify.test.mjs`, beside `Build_and_test_commands_are_work`):

- `A_shell_running_build_sh_or_run_sh_is_build_test`: `bash build.sh`, `sh build.sh`, `./build.sh`, `bash ./run.sh`,
  `cd ws && bash build.sh`, `bash -c 'bash build.sh'`, `timeout 600 bash build.sh` each give `build_test`. **RED today:**
  each gives `other_tool` (probed 2026-11-07).
- `The_visible_samples_runner_is_build_test`: `python3 runner/run_samples.py samples/`,
  `python3 -I runner/run_samples.py samples/` and `/usr/bin/python3 -I runner/run_samples.py samples/` give `build_test`.
  **RED today.**
- `Reading_or_editing_the_scripts_is_not_build_test`: `cat build.sh`, `sed -n 1,5p run.sh`, `chmod +x build.sh`,
  `grep exec run.sh` and `python3 -c "print(1)"` stay `other_tool` (it guards against widening by a substring).
- `The_v1_classifier_counts_every_command_explore_buildTest_counts` (`spikes/lang-explore/test/records.test.mjs`): over one
  fixture list of build, run and sample commands, every command for which `isBuildTestCommand` is true is `build_test` in
  `classifyCommand`, so the two cannot drift apart again; `explore.buildTest` stays beside the partition (parent T11).
- The recompute (`spikes/lang-explore/test/quick-repartition.test.mjs`, a fixture session made of invented stream lines):
  `The_recomputed_partition_moves_a_build_sh_call_from_other_tool_to_build_test_and_still_sums_to_the_wall`;
  `The_recompute_writes_a_new_dated_file_and_never_changes_a_record_or_an_earlier_file` (the session's `record.json` keeps its
  bytes; an earlier result keeps its bytes and its name);
  `Two_runs_in_the_same_second_with_changed_inputs_write_two_results_with_two_names` (a fixed clock seam; the generation ids
  differ, no file is replaced);
  `A_run_over_unchanged_inputs_reports_the_existing_complete_result_and_writes_nothing` (its path is printed, the folder's
  listing is the same before and after);
  `A_temporary_file_left_by_a_killed_run_is_never_read_as_a_result_and_a_retry_completes` (a fixture temporary file with a dead
  owner and half a JSON body: the retry writes a complete result under a new name and removes the stale file; one with a live
  owner is left and named);
  `A_session_whose_stamped_stream_is_missing_is_listed_with_its_reason_and_never_as_zero`.

**Brief (Sonnet coder).**

1. `spikes/minigit-bench/lib/classify.mjs`: `isHarnessScript` (`:91`) also accepts `build.sh` and `run.sh` by the same
   path-ending rule; `isBuildTest` (`:107-116`) gains one case for `python3` or `python` whose script argument (options such
   as `-I` and `-u` skipped) ends in `run_samples.py`, written as a small helper so the function stays at complexity 4.
   Nothing else in the classifier changes.
2. **Two consequences, both said in the pull request.** v1's `harnessIdentity` moves, because `spikes/minigit-bench/lib` is
   one of its folders (`spikes/minigit-bench/lib/prereg-guard.mjs:31`), so every lang-explore record from this merge on carries
   a new `run.harnessIdentity`; before the merge, check that no open v1 series still runs paid sessions under the old identity
   (its run-maps), and say what was checked. The note sentence at `spikes/lang-explore/lib/note-tasks.mjs:55` ("…is not
   widened") changes for new notes to say what the classifier now counts and from which date; the notes already written under
   `research/lang-v2/` stay as they are.
3. **The recompute** is a FREE verb, `node spikes/lang-explore/explore.mjs repartition --series <dir>`, beside `rescore` in
   `explore.mjs` and `lib/commands.mjs`. It reuses v1's path, never a second partition: for each session folder `s/<rid>/` of
   the series it builds the spans and the partition the way `smokeViews` does (`spikes/minigit-bench/lib/smoke-verdicts.mjs:58`,
   with `spansFrom` and `partition` from `timeline.mjs`), from the retained `stream.stamped.ndjson`, `otel/` and `meta.json`.
   **The result and its name.** One result file per run, `<series>/partition-recomputed/<yyyymmddTHHMMSSZ>-<gen>.json`: a UTC
   timestamp and a short generation id (8 hex digits from a random source), so a second run the same day, or the same second,
   never takes an existing name. A result is immutable once written: never edited, never renamed again, never removed. It holds
   per rid the stack, the old categories (read from `record.json`), the new categories, and whether the new partition sums to
   the wall; and at its head `complete: true` and a fingerprint of its inputs (the classifier's identity, the sha256 of
   `classify.mjs` and of each session's evidence files read). **Before it writes**, the verb looks in that folder for a complete
   result with the same fingerprint; if there is one, it reports that file's path and exits 0 without writing anything. A
   result whose fingerprint differs (the classifier or the evidence moved) is kept, and the new run writes a new name beside it.
   **Interrupted runs.** The verb writes to a temporary file in the same folder, named with the owned-name helper
   (`ownedFor({ token: 'bench' })`, so its owner can be read) and with a `.tmp` suffix, fsyncs it and renames it to the final
   name in one step. A temporary file is never read as a result. A retry first looks for a complete result, as above; it never
   reuses or appends to an earlier temporary file, and it removes a stale one only when its owner is gone, by the owner rules of
   `common.test-leftovers`; one whose owner still runs is left alone and named in the output. Run it over the six series of
   `~/rsd/lang-v2/briefs/quick-series.txt`; the files stay in the private area, outside git.
4. **Docs.** `research/lang-v2/EXPLORE_quick_2026-11-06_analysis.md` gets one dated line under its `build_test` point (`:50`)
   and one under "Fix the classifier" (`:58`): done, with the per-stack count of sessions and the summed `build_test` seconds
   from the new files (counts and times only). `research/module_lang_explore.md` names the verb.

**Done when:** the three classifier tests and the recompute tests went red, green, red; a complete result exists for each of the six
series; at least one C# session of a retained stream has `build_test` above zero in them (the parent's done-when); every recomputed partition sums to
the wall or says why not; §7's checks for PR 4.

### (c) the C# compile's letter case, recorded and never changed; carry items 2 to 4

**Not done** (§2: no line and no test on 2026-11-07). No code changes; the copy stays exactly D65's.

**Scenarios** (`spikes/lang-bench/test/workspace-exec-kinds.test.mjs`, beside its real C# builds in the build box; they skip
with their reason without the box, and the sandbox run fails on that skip):

- `A_folder_named_Bin_in_another_letter_case_is_left_out_of_the_csharp_compile_in_place_and_on_the_clean_copy`: the C#
  daemon template with a working `Program.cs` and `Bin/Extra.cs` holding text that does not compile: `bash build.sh` exits 0
  in place, and exits 0 on the clean copy made by `quickPristineCopy`, which keeps `Bin/` because its folder match is exact.
  The same for `OBJ/Extra.cs` and `Node_Modules/Extra.cs`. It passes on today's code.
- `A_folder_of_another_name_holding_the_same_text_fails_the_build_and_that_is_the_point`: `Bim/Extra.cs` with the same text
  makes `bash build.sh` exit non-zero. This control is the red of the pin: the first test, with the file moved to `Bim/`, is
  watched failing once.

**Brief.**

1. `research/module_lang_bench.md:901`, the sentence about the folders that are not compiled, becomes: a folder named `bin`,
   `obj` or `node_modules` at any depth and **in any letter case** is not compiled (MSBuild's globs match without regard to
   case on Linux), while the clean copy drops only the exact names and `tests/**` stays case-sensitive; so a build in place and a
   build on the copy agree **for the default compile items**; a hand-written `<Compile Include>` naming a file under an excluded
   folder builds in place and fails on the copy (CS2001), and Stage 1 does not count it (D74, T23). Carry items 1 and 3.
2. `research/module_lang_bench.md:918`: a bare repository's `HEAD` may also be a link (the code already says so,
   `lib/workspace-destination.mjs:35`), and the list of paths that "cannot be judged" names a dangling link too; the comment at
   `lib/workspace-destination.mjs:94` says the same (a comment only). Carry item 2.
3. `spikes/lang-bench/test/workspace-exec-kinds.test.mjs:248-249`: the comment's "The rule for E3's scorer" is marked superseded
   by parent D65, its text kept (a comment only). Carry item 4.
4. The `build.sh` comment line about the letter case is not written here: it rides with F1.S3 (b), so the templates' identity
   moves once (parent F1.S1 (c)). The carry file's "dated line under D65" is not written either: the parent's (c) names only the
   module line and the pin test.

**Done when:** both tests pass in the sandbox run with nothing skipped; the red of the control is recorded; the three doc lines
and the two comment lines are on the branch; §7's checks for PR 3.

### (d) the held-out reader refuses anything but regular files

**Scenarios** (`spikes/lang-explore/test/quick-units.test.mjs`, beside
`The_held_out_reader_refuses_a_missing_folder_a_missing_manifest_a_stray_file_and_an_unknown_trap`; invented pairs in an owned
folder):

- `The_held_out_reader_refuses_a_case_file_that_is_a_link_and_names_it_without_reading_through_it`: one pair file replaced by
  a link to a file outside the folder with the same bytes is refused, naming the entry; the target is never read. **RED
  today:** the link is followed and the hash computed.
- `The_held_out_reader_refuses_a_cases_folder_that_is_a_link` and `The_held_out_reader_refuses_a_traps_file_that_is_a_link`.
- `The_held_out_reader_refuses_a_folder_a_fifo_and_a_socket_in_the_cases_folder_and_never_throws`: each is refused as a value
  naming the entry. **RED today:** a folder named like a pair file throws `EISDIR` from `casesTreeHash`.
- `The_tree_hash_of_a_folder_of_regular_files_is_the_same_value_as_before_the_check`: the hash of a valid folder is computed
  exactly as now, so a series made before this change still matches at its next pair.
- From the sweep for the same class (`common.security`, *sweep for the class*): `A_linked_file_under_a_private_folder_is_named_by_the_leak_check_and_never_skipped`:
  a link under a task's `refs-NN/` or `mutants/` makes `leakcheck` report the path and exit non-zero. **RED today:**
  `lib/leak-material.mjs:27` skips it in silence, so the check D41 relies on would not read that file.

**Brief.** In `spikes/lang-explore/lib/heldout-quick.mjs`, put the check on the one road in: a `caseEntriesProblem(casesDir)`
that `lstat`s every entry of `cases/` and refuses anything but a regular file, and refuses `cases/` itself or `traps.txt` when
either is a link (by `lstat`, so `isDir` at `:53` no longer follows a link); `casesTreeHash` returns `ok(hex)` or `fail` and
calls it first, and its two callers (`loadHeldoutQuick`, `lib/rehearsal-quick.mjs:37`) take the value. For a valid folder the
hashed bytes do not change. In `spikes/lang-explore/lib/leak-material.mjs`, `filesUnder` reports a link it meets as a finding
with its path (a value, not a throw), and `leakcheck` exits non-zero on it. The olympiad reader
`spikes/lang-explore/lib/heldout.mjs:12` has the same shape; it is
outside the claim, so it is named in the pull request and not changed.

**Done when:** each test went red for the real symptom (followed, thrown, skipped), green, and red again with the check
removed; `node spikes/lang-explore/explore.mjs leakcheck` still exits 0 on the real private side; §7's checks for PR 5.

### (e) the bench's side of FORMAT's first amendment, merged forward

FORMAT's first amendment is on the task repository's `main` (its PR #3, `4ddb8f8`). The bench's side is built on
`feat/lang-v2-format-amend-pins` and is not merged. **Scenarios:** the branch's own, already watched red and green
(`~/rsd/lang-v2/briefs/format-pins-reds.txt`): `a_freeze_tag_without_a_group_letter_is_found_by_the_header_check`,
`the_plan_id_v2_is_found_by_the_no-needle_check` and `the_plan_id_v12_is_found_by_the_no-needle_check`
(`test/conformance-batch3b.test.mjs`); plus the real-FORMAT cases of T-L56 and T-L58, which run only with `LANG_V2_TASK_REPO`.

**What merging it forward needs, exactly:**

1. In its worktree, `git fetch origin` and `git merge origin/main` (a merge, never a rebase: the branch's history is kept as it
   is). Today the merge is clean (§2).
2. After the merge, re-read what tooling parses (`common.git-workflow` rule 11): `inputs/specs.json` is generated, so
   `node spikes/lang-bench/specs-record.mjs --check` must exit 0 on the merged tree; if it does not, re-run the tool and never
   edit the file by hand. Read `b7bba0d3`'s dated lines in the parent (D65's row and one §14 line) and the E1 document on the
   merged tree: they are dated records in the older sections, which the operative part overrides, and change no scope; check
   that `plan-lifecycle.mjs` stays clean.
3. Sweep for the old pins over `spikes/` (`d293bafc`, `95f3c26e`, `40e678fe`): only the append-only history in
   `inputs/gates/FORMAT-v2.md` and the docs may still name them. `spikes/lang-explore/inputs/explore.json` pins drafts, not this
   FORMAT, and does not move here (§10, item 3).
4. Point `LANG_V2_TASK_REPO` at a task repository checkout at `4ddb8f80`; `/home/jinx/git/wt/task-pinned` is at `40e678f`, so
   use the main checkout or move that worktree first, or the real-FORMAT cases fail on the hash check, not on the code.
5. Run §7's checks for PR 2: the gates, the whole lang-bench runs, the lang-explore runs (its quick world reads lang-bench's
   inputs), the three `--check` tools, `node spikes/lang-bench/push-check.mjs --self --branch <branch>`, `leakcheck` and the
   line scan.
6. Push the branch for the first time, open the pull request, wait for CI's checks at the head sha, merge. Record in it that
   draft 04's `<v0>` and `<v1>` at its line 174 are now flagged by the checker, as expected; F2.S1 rewrites that line.

**Done when:** merged; the three `--check` tools exit 0 on `main`; F1.S2's memory amendment then moves the pins once more
(parent F1.S1 (e)).

### (f) T-L64's text half: no plan id and no stack name in an agent-visible runner comment — open

**Done part.** #173 (`00d88233`) removed the plan ids from the agent-visible runner files and added
`spikes/lang-explore/test/quick-agent-visible.test.mjs`:
`No_file_an_agent_is_given_names_a_plan_item_a_decision_a_story_or_a_review_finding` and its companion
`The_pattern_finds_the_ids_it_is_meant_to_find_and_leaves_ordinary_words_and_codes_alone`, over `lang-bench/runner`,
`lang-bench/templates` and `lang-explore/runner`; 2 of 2 pass on `main` (2026-11-07). They stay.

**Open part** (the team lead's resolution (2) in §10: it satisfies the plan as written, so it is not a change). The runner is
copied whole into the workspace of both stacks, so a stack name in its comments tells each agent what the other side is.
`spikes/lang-bench/runner/rs_abort.py` names the stacks in comments and docstrings at `:2`, `:6`, `:43`, `:58` and `:79`, and
the push check leaves the pinned runner out of its scan on purpose (`research/module_lang_bench.md:922`: the plan ids it held
then are gone since #173).

**Scenarios** (`spikes/lang-bench/test/runner-text.test.mjs`, new, with the Python reading started from it under
`python3 -I`):

- `No_comment_or_docstring_of_a_runner_file_names_a_stack`: every comment (by Python's `tokenize`) and every docstring (by
  `ast`) of `runner/*.py` is matched against the stack words of `inputs/stack-words-lang.json`, by that file's own rule
  (case-insensitive on word boundaries but for its case-sensitive words). **RED today:** the five lines of `rs_abort.py` above.
- `The_detection_strings_are_kept_as_exact_approved_entries_and_nothing_else_is`: the three detection strings of
  `rs_abort.py` (`:18`, `:20`, `:21`) and the two runtime labels it returns are behaviour, so they stay, each as one exact
  entry of an approved list; a stack word in any other string literal of a runner file is a finding, and a stale approved entry
  that no longer occurs is a finding too, so the list cannot grow quietly.
- `The_push_check_scans_the_pinned_runner_and_refuses_a_stack_name_in_its_comments`: the push check's agent-visible scan
  (`lib/push-visible.mjs`, `findVisibleRules`) runs over `runner/*.py` and over each workspace's `runner/` copy, with the plan-id
  rules it already has and the stack-word rule for comments and docstrings; a planted comment naming a stack is reported with
  its path and line, and the real runner, after the brief, gives none. Its companion: the same scan finds the planted line in a
  workspace copy, so it can match there.
- `The_runner_change_is_comments_and_docstrings_only` (the `ast` proof): for every module of `runner/`, `ast.dump` of the tree
  with docstrings removed at the branch head equals the same at the base (`git show <base>:<path>`), but for the version
  constant; watched red once by changing one literal on a scratch copy.

**Brief (Sonnet coder).**

1. Rewrite the five comment and docstring lines of `rs_abort.py` in stack-neutral words ("the first runtime", "the second
   runtime", or by the detection string's own text); change no code line. Sweep every other `runner/*.py` with the first test
   before believing it is the only file.
2. The approved list holds exact strings only, with one reason each, in `inputs/` beside the stack words; the scan reads it.
3. Find where the push check leaves the pinned runner out (the module doc's sentence at `:922` names it; enumerate with a
   search for the runner's paths in `lib/push-*.mjs` and `lib/run-base-tree.mjs`, not from memory) and take that exclusion out,
   so the scan covers `runner/` in the task repository and in every built workspace.
4. The `ast` proof is a small tool beside the test, run in the pull request against its merge base and recorded in it and in
   `research/module_tests.md`; it is not a permanent test, because its base moves.
5. **The identities move.** The runner's version moves past 0.3.9 and its manifest changes: `node spikes/lang-bench/pin-runner.mjs`
   writes `inputs/specs.d/runner.json` and `inputs/runner-versions.json`, `specs-record.mjs` assembles `inputs/specs.json`, and the
   three `--check` tools exit 0. The execution probe is re-run as a new dated `inputs/exec-probe-<date>.json` (T-L53; no old
   record is changed), and `inputs/checkpoints.md` gets CP2's addendum naming the old and the new manifest. A quick-run tree
   rescored later sees the runner as moved, which `harness-pin.mjs:16` already reports as harness drift, not as the agent's change.
6. Docs: `research/module_lang_bench.md:922`, the sentence that the runner is not scanned, is replaced by what is scanned now;
   `research/module_tests.md`.

**Done when:** each new test went red, green, red; the `ast` proof is recorded and was shown to fail on a code change; the push
check's self run and the three `--check` tools exit 0 on the new manifest; the new probe record exists; §7's checks for PR 6.

### (g) the wall-clock C# test goes to the leftovers plan

**Brief.** One dated line in [PLAN_bench_test_leftovers_followups.md](PLAN_bench_test_leftovers_followups.md) S5, in that
section's dated form: `The_wait_for_the_script_says_so_first_and_ends_at_the_callers_cancellation_with_the_child_stopped`
(`tests/Bench.Tests/ModelArms/WslHostAwakeTests.cs:349`) waits in `Eventually` (`:357`) on a 15 s wall clock (`Patience`,
`:24`); it failed once on a CI runner at 15.018 s on PR #173, which changes no C# file; a timing dependency, margin or race not
decided. The line says that it widens S5 past that plan's stated scope (`spikes/` only). Nothing is built here.
**Done when:** the line is on `main`.

## F1.S2 — repeated scoring and the one memory value · cornerstone, `model: fable`

Cornerstone. The scenarios and the brief below are the Fable writer's spec of 2026-11-07, amended the same day for the plan round's six accepted findings, copied in by the team lead without change, except that its section 4 (the measurement before any freeze, now a bounded bootstrap) stays outside git at `~/rsd/lang-v2/briefs/f1s2-spec.md`, because it names the private series, session and folders it measures. Only counts, times and sizes from that measurement reach git.

What the story is: today a quick-run session is scored once (`spikes/lang-explore/lib/score-quick.mjs`: one clean
copy, one build in the build box, one run of the runner through the execution layer). The quick run's analysis
(`research/lang-v2/EXPLORE_quick_2026-11-06_analysis.md`) showed one session that passed or failed by chance on the
scoring CPUs, so one pass is a sample, not a verdict. After this story a session is built once and run ten times under
two CPU profiles, and one stated rule turns the ten passes into one of four states. The memory limit, which the quick
run's task 07 agents read wrong, becomes one registered number that the statement, the visible load test's
configuration and the scorer all read from one file, and a check on the certifying host shows it is enforced.

### 1. The verdict rule, in one place

This is D71 as a coder must read it. Where the plan's words and these words differ, say so in the review; they are
meant to be the same rule.

**The units.**

- A **session** is one agent run of one stack on one task. It is scored after it ended, never while any session runs.
- A **pass** is one run of the runner over every visible and every held-out case of the task, on the built copy of
  the session's tree, with a fresh program for every case, the same requests and the same deadlines every time.
- Every session gets **ten passes**: passes 1, 3, 5, 7, 9 on the **2-CPU profile** (CPUs `6-7`) and passes 2, 4, 6,
  8, 10 on the **8-CPU profile** (CPUs `0-7`). The profiles alternate so a change of the host halfway cannot be told
  from a profile. The same ten for both stacks.
- An **attempt** is one try at a pass. A pass that ends in an **environment failure** (the layer or the runner failed:
  a box that cannot start, a holder that fails, a missing or wrong snapshot, a halt of the layer, a record that does
  not judge every case, a missing memory reading, a host fault, an interrupted run) is tried again **whole**, at most
  **twice**. So a pass has at most three attempts. Every attempt is kept.
- A pass is **completed** when one of its attempts ended with the runner judging every case, and only when the
  attempt's record passes the **complete validator**: the record parses, carries the runner's completion marker (the
  top-level `format`, `runner`, `kind`, `build`, `launch` and `cases` the pinned runner writes, with `format` and
  `runner` equal to the pinned values), holds exactly one result for every expected case (no duplicate, no unknown id,
  none missing), and every result has its required fields (`id`, `status`, `reason`, `wallMs`,
  `lifecycle.memory.peakBytes`, `lifecycle.memory.maxBytes`, `lifecycle.leftover`). A record that is malformed,
  truncated or partial is an **environment attempt, never a functional failure, even when the results it does hold
  carry a non-pass verdict**: a half-written record is no evidence of anything. A completed pass is **clean** when
  every case of it is `pass`, and **failing** when any case of it has another verdict.
- A **functional failure** is any case verdict other than `pass` in a completed pass: `wrong answer`, `memory limit`,
  a leftover process, the test bound reached, standard error overflow, a scenario that ended early, and `build failed`.
  An environment failure is not a case verdict at all; it is the absence of evidence.
- The **build** happens once per session, before the passes, at an **atomic commit point**: the clean copy is made and
  built in a temporary owned folder (`score/build-<token>/`, the token from `ownedFor`), the result is validated (the
  marker was seen, the exit is known, the tree is a directory, every frozen file is restored and the frozen changes
  are recorded), the folder is renamed into place as `score/tree/`, and only then a durable completion record
  `score/build.json` is written (`wx`) naming the tree's hash (`treeSha256` over every file of `score/tree/`, names
  and bytes in name order, the same shape as `casesTreeHash`), the build's exit, wall, `timedOut` and tail, and the
  frozen changes. A pass runs only against a tree whose completion record exists and whose hash matches the tree now:
  a record is validated against the tree it names, every time the driver starts. On resume, any `score/build-*`
  folder and any `score/tree/` without a completion record is uncommitted build state: it is removed and the build
  happens again. A build that really ran and exited non-zero is committed too (its record says `ok: false`): every
  case is `build failed` and no pass runs. A build that did not begin (no marker) is an environment failure of the
  session's build step, nothing is committed, and it is tried again like a pass, at most twice.

**The four states.** Exactly one of them, from the completed passes and the exhausted ones:

| state | when |
|---|---|
| `accepted` | the build passed, all ten passes completed, and every case passed in all ten |
| `rejected` | the build failed; or at least one completed pass is failing and no completed pass is clean |
| `intermittently incorrect` | at least one completed pass is failing and at least one completed pass is clean |
| `unscored: environment` | no completed pass is failing, and some pass has no completed attempt after its three attempts |

While no completed pass is failing and some pass still has an attempt left, the rule answers `pending`; that is the
driver's working state, never a recorded verdict.

**The one terminal rule (the sentence a test checks):** *if any completed pass of a session holds a genuine
functional failure, the session is `rejected` (or `intermittently incorrect` when some pass is clean), permanently,
whatever any other pass did, with or without retries; it never enters the void or re-run schedule, and its cost counts
in CPC's numerator like any rejected session.*

What that means for the driver: a functional failure in pass 3 with pass 7 still environment after three attempts is
`rejected` or `intermittently incorrect` according to the clean count, never `unscored: environment`. Every pass still
runs its attempts to completion or exhaustion (the budget is ten passes, at most thirty attempts; it never stops early),
because the label `intermittently incorrect` needs to know whether any pass was clean. A majority of clean passes never
accepts: nine clean and one failing is `intermittently incorrect`.

**What each state does in the series.** `accepted`, `rejected` and `intermittently incorrect` are **data**: the
session is `scored`, final, never re-run, and its cost is in CPC's numerator (and, for `accepted`, one in the
denominator). `unscored: environment` is a **void**: the session stays `ran` with `data: false` and a class that
starts with `void:`, so `series.mjs`'s existing schedule offers its one re-run (`run --pair k --rerun <stack>`); its
cost goes on D24's void line and is counted per stack in the note and the report.

**One meaning of terminal.** Two different things end, and the schema keeps them apart:
- **Scoring exhaustion** is the scorer's: `verdict.json.scoringState` is `'open'` (an attempt is still possible),
  `'decided'` (`accepted`, `rejected` or `intermittently incorrect`: no attempt can change it) or `'exhausted'`
  (`unscored: environment`: every pass is completed or has used its three attempts, and no completed pass is
  failing). The verdict file never says whether the session is final.
- **Session finality** is the series': it is read from the run-map entry and nowhere else. The exact transitions:
  a `decided` scoring makes v1's `scoreOne` apply `transition(map, rid, 'scored')` (`ran → scored`, the only move
  `NEXT` allows from `ran`), and a `scored` session is final. An `exhausted` scoring makes `scoreOne` patch the
  entry **without a transition**: it stays `ran`, with `data: false`, `class: VOID_UNSCORED` and its history appended
  to `reclassifiedFrom`. That entry is not final: `series.mjs`'s `stackState` reads it as `void-rerunnable` while the
  stack has fewer than `MAX_ATTEMPTS` (2) sessions in the pair, so `planRerun` plans the one re-run (a new session, a
  new rid, `label: 'ran unpaired'`); it becomes `void-final` when the re-run's session exists, and the cell's final
  session is `finalOf()`'s: the latest attempt with data, else the latest attempt. The void re-run is the only road
  from `exhausted` to a final cell.
- Every consumer reads the right field: the record's `explore.verdict` and `explore.scoringState` come from the
  verdict file; the record's `class` and `data`, the note's finality counts, `cellOfSession`, `finalOf` and CPC come
  from the run-map; nothing reads finality from `verdict.json`, and nothing reads a verdict from the run-map's class.

**What stays as it is.** D66's one acceptance policy for both stacks (the frozen build is the acceptance build; what
was scored is what was submitted). `replay --times` is a diagnostic reading of one case and never a verdict.
`rescore` keeps every refusal and every write it has today. The references are validated under both profiles before
the freeze (F2.S1 runs that; this story makes it possible). Ten passes are a budget, not a proof (T25).

---

### 2. The scenarios

One test per behaviour, named as this repository names a test: a sentence stating the guarantee, in snake case, under
`node:test`, in `spikes/lang-explore/test/`. **Fakes** means the test runs with `buildFn`, `runFn`, a fake `spawn`,
a fake `procApi` and a temp home, and starts no box; the fixtures are invented (a toy program that fails on purpose,
invented case ids `s01`, `h001`, `h002` as the existing fixtures use; never a quick-run tree or a reference).
**Real box** means bwrap, the user bus and the pinned toolchains, behind `sandboxGate`/`needs` (skipped with the reason,
a failure under `BENCH_REQUIRE_SANDBOX=1`). **Certifying host** means a real box whose user scope can read
`memory.peak`, the host the pre-registration names.

A pass in the fakes is `{ profile, cpus, attempts: [{ attempt, kind: 'completed' | 'environment', cases, reason }] }`.
Every fixture case is built with `rec()` from `test/quick-score-fixture.mjs`, so it passes `casesOfRecord`.

#### 2.1 The verdict rule (`test/quick-verdict.test.mjs`, pure, fakes)

| # | test name | given | observed |
|---|---|---|---|
| V1 | `Ten_completed_passes_with_every_case_passing_is_accepted` | build ok; ten completed passes, every case `pass` | `accepted`, `scoringState: 'decided'`, counts clean 10 |
| V2 | `A_failed_build_is_rejected_with_no_pass_run_and_every_case_build_failed` | build ran and exited 1 | `rejected`; the rollup's cases all `build failed`; the driver ran zero passes (the pass seam counts 0 calls) |
| V3 | `One_completed_pass_with_a_functional_failure_and_no_clean_pass_is_rejected` | ten completed passes, each with one `wrong answer` | `rejected`, `scoringState: 'decided'` |
| V4 | `A_functional_failure_beside_clean_passes_is_intermittently_incorrect_and_never_a_majority_pass` | nine clean and one failing; also one clean and nine failing | both `intermittently incorrect`; the nine-clean case is not `accepted` |
| V5 | `A_functional_failure_in_one_pass_beside_an_environment_failure_in_another_rejects` | pass 1 failing; pass 2 one environment attempt, attempts left | `rejected` (not `pending`, not `unscored: environment`) |
| V6 | `A_functional_failure_stands_when_another_pass_stays_environment_after_its_two_re_runs_the_session_is_rejected_never_unscored_and_never_re_run` | pass 3 completed with a `memory limit`; pass 7 three environment attempts; the rest clean | `intermittently incorrect` (clean passes exist), `scoringState: 'decided'`; with no clean pass, `rejected`; neither answer carries any finality field |
| V7 | `An_environment_attempt_re_run_whole_and_then_clean_leaves_the_pass_clean_and_the_session_accepted` | pass 4: attempt 1 environment, attempt 2 completed clean; the rest clean | `accepted`; pass 4 counted clean, its two attempts both present |
| V8 | `An_environment_failure_that_stays_after_two_re_runs_with_no_functional_failure_anywhere_is_unscored_environment` | pass 6 three environment attempts; the other nine clean | `unscored: environment`, `scoringState: 'exhausted'` |
| V9 | `The_verdict_is_pending_while_a_pass_without_a_functional_failure_still_has_an_attempt_left` | passes 1–9 clean, pass 10 one environment attempt | `pending`; after a second environment attempt still `pending`; after the third, V8's answer |
| V10 | `Every_non_pass_verdict_of_a_completed_pass_is_a_functional_failure_and_an_environment_attempt_holds_no_case_verdict` | for each verdict word the runner's record can carry (derived from the exported list, not retyped): one completed pass with one case of it | each gives `rejected`; an attempt of kind `environment` with a `cases` list present is refused by the shape check (a fixture the code rejects must fail loudly, never pass as empty) |
| V11 | `A_pass_is_completed_only_when_the_runner_judged_every_expected_case_a_record_missing_a_case_is_an_environment_attempt` | a runner record with `h002` missing | the validator (2.9) fails; the attempt is kind `environment` with the reason naming the missing id; no verdict |
| V12 | `The_verdict_is_the_same_whatever_order_the_passes_and_attempts_are_read_in` | V6's passes shuffled | the same answer, byte for byte |

#### 2.2 The pass driver (`test/quick-passes.test.mjs`, fakes)

| # | test name | given | observed |
|---|---|---|---|
| P1 | `A_session_is_built_once_and_then_run_ten_times_five_on_the_2_CPU_profile_and_five_on_the_8_CPU_profile` | a fake build and a fake pass child that record their calls | `buildFn` called once; ten pass calls; the `cpus` of calls 1,3,5,7,9 is exactly `'6-7'` and of 2,4,6,8,10 exactly `'0-7'`; each pass file names its profile |
| P2 | `Every_pass_runs_every_case_on_the_same_built_copy_with_the_same_ids_and_deadlines` | the same | all ten calls carry the same `tree`, the same sorted `caseIds` (visible and held-out), the same `caseWallMs`, `holdSeconds` and `limits` |
| P3 | `An_environment_attempt_is_re_run_whole_at_most_twice_and_each_attempt_is_kept_as_its_own_file` | a pass child that answers environment, environment, then completed for pass 2; always environment for pass 5 | `passes/p02-8cpu-a1.json`, `-a2.json`, `-a3.json` exist; pass 5 has exactly three files and no fourth call; the verdict is `unscored: environment` |
| P4 | `No_attempt_file_is_ever_overwritten_or_removed_and_a_second_scoring_of_the_same_session_appends_nothing` | run the driver twice on a session scored ten of ten | the directory listing and every file's bytes are the same after the second run; the pass child was called zero times the second time; files are opened with `wx` |
| P5 | `A_resume_after_an_interruption_reuses_the_completed_passes_and_runs_only_what_is_missing` | six pass files on disk (passes 1–6 completed), then the driver starts | the pass child is called for passes 7–10 only; the verdict reads all ten |
| P6 | `The_verdict_file_names_every_pass_its_profile_its_attempts_the_scoring_time_and_keeps_it_apart_from_the_agents_wall` | a fake clock | `verdict.json` has `verdict`, `scoringState`, `build` (the completion record's hash, exit, wall), `passes[10]` each `{ n, profile, cpus, attempts: [{ attempt, kind, file, startedAt, ms }] }`, `events` (every event file, in order), `scoringMs` (the sum), `buildMs`; it has no finality field; the session record's `time.wallMs` is unchanged by scoring |
| P7 | `A_pass_refuses_to_start_while_a_session_of_the_series_is_launching_and_records_the_halt_without_an_attempt` | a run-map with another session `launching` | the driver answers a halt (kind `environment`, reason names the rid); no pass file is written; one event file `score/events/<utc>-halt.json` is written (`wx`) and the rebuilt `verdict.json.events` lists it |
| P8 | `The_rollup_score_keeps_the_records_meaning_each_case_carries_its_worst_verdict_and_largest_peak_over_the_completed_passes_and_accepted_equals_the_verdict` | passes where `h001` fails once with evidence and passes nine times at a smaller peak | `score.json` has `h001` with the failing verdict, its reason and `firstMismatch.evidence` from the failing pass, `peakBytes` the largest of the ten; `explore.verdict` is `intermittently incorrect`; `explore.accepted` is false; `explore.passes` is 10; the v1 fields `quality.heldout.percent`, `defects`, `primary100` are computed from that rollup as before |
| P9 | `A_stop_signal_ends_the_scoring_between_two_passes_keeps_what_was_written_and_the_next_run_resumes_it` | abort after pass 3's child returns | the driver exits with the stop's code; three pass files; no partial fourth; a second driver run continues at pass 4 |
| P10 | `A_session_whose_verdict_is_unscored_environment_becomes_a_void_with_its_history_appended_and_the_series_offers_its_one_re_run` | the loop over one stubbed session whose passes exhaust | the run-map entry stays `ran`, `data: false`, `class: 'void: unscored environment'`, `reclassifiedFrom` has one line (the old class, `data: true`, the reason, `at`); the ledger gained an `event` line and no charge; `stackState()` is `void-rerunnable`; the pair record counts it per stack |
| P11 | `A_rejected_or_intermittently_incorrect_session_is_scored_and_final_and_the_series_offers_no_re_run` | a session whose passes give V4's answer | the entry is `scored`, `data: true`; `stackState()` is `done`; `rerunProblem` refuses `--rerun`; `cellOfSession` holds its cost |
| P12 | `Rescore_scores_through_the_same_ten_passes_and_its_refusals_and_writes_are_as_they_were` | the rescore fixture of `quick-rescore-run.test.mjs` | the scoring seam is called through the pass driver (ten pass calls); every existing rescore test stays green unchanged; a rescore whose passes give `unscored: environment` writes nothing and halts, as a harness failure does today |
| P13 | `Replay_stays_a_single_pass_diagnostic_on_one_case_writes_only_its_own_file_and_feeds_no_verdict` | the replay fixture | `scoreFn` called with `only` and once per `--times`; nothing under `score/` is written or read; `DIAGNOSTIC_NOTE` unchanged; every existing replay test stays green unchanged |
| P14 | `The_note_counts_accepted_rejected_intermittently_incorrect_and_unscored_environment_per_stack_from_the_records_alone` | records of all four states in a series fixture | the note's table has the four counts per stack and no number typed by hand |

#### 2.3 The affinity read-back (`test/quick-affinity.test.mjs`)

| # | test name | given | observed | runs with |
|---|---|---|---|---|
| A1 | `The_read_back_accepts_a_Cpus_allowed_list_that_is_exactly_the_profile_in_any_spelling_and_refuses_any_other` | `'0-7'` against `'0,1,2,3,4,5,6,7'`, `'0-3,4-7'`; `'6-7'` against `'6,7'`; then `'0-6'`, `'0-8'`, `'6'`, `''`, null | the first group gives null (no problem); each of the second gives a sentence naming both masks | fakes |
| A2 | `A_pass_whose_mask_cannot_be_applied_is_refused_before_the_build_recorded_as_a_halt_named_affinity_not_applied_and_never_scored` | a fake `procApi` whose `status` of the child shows `'6'` for a wanted `'6-7'` | the driver kills the child, writes no attempt file, answers kind `environment` with reason starting `affinity-not-applied`; one event file records wanted and observed and the rebuilt snapshot lists it; exit 3 at the command | fakes |
| A3 | `The_pass_child_reads_its_own_affinity_at_start_and_another_mask_is_an_environment_answer_naming_both_masks` | the child entry with a fake `/proc/self/status` text | the child writes an attempt of kind `environment` whose reason starts `affinity-not-applied` and runs no case | fakes |
| A4 | `On_this_host_a_pass_child_started_under_each_profile_reads_back_exactly_that_profile_even_from_a_parent_pinned_to_CPUs_6_and_7` | a real `taskset -c 6,7 node` parent spawning `taskset -c 0-7 …` and `taskset -c 6-7 …` probe children | each child's `Cpus_allowed_list` normalises to its profile (a child may widen past its parent's mask) | real box (Linux with `taskset`; skipped elsewhere with the reason) |
| A5 | `Before_a_paid_launch_the_preflight_probes_each_mask_of_the_pair_and_refuses_the_launch_resumably_when_a_probe_reads_back_another_mask` | the pair's preflight with a fake probe answering `'0-4'` for mask `'0-5'` | the preflight fails with the sentence; the loop halts (exit 3) before any session is launched; the run-map is unchanged; `readback.json` is not written | fakes |

#### 2.4 The one memory value (`test/quick-memory-value.test.mjs`)

| # | test name | given | observed | runs with |
|---|---|---|---|---|
| M1 | `The_statement_the_load_test_configuration_and_the_scorers_limit_read_one_number_from_the_one_file_and_it_is_536870912` | the real inputs | `memoryLimit().bytes === 536870912`; the generated `STATEMENT.md` of both stacks contains D72's sentence with that number and `512 MiB`; `samples/load.json` of both stacks has `memoryLimitBytes` equal to it; `calibrationFor(...)` for a quick task gives `limits.memoryBytes` equal to it; the three are read back from the generators, never retyped in the test except the one literal above | fakes |
| M2a | `The_memory_figure_has_exactly_one_source_in_the_tree` | a scan of `spikes/lang-explore/lib/**`, `explore.mjs`, `inputs/explore.json`'s `quick` block, `spikes/lang-bench/lib/**`, `templates/**` for `536870912`, `536_870_912`, `0x20000000`, `512 MiB`, `512MiB` | no match outside `inputs/memory-limit.json` | fakes |
| M2b | `The_scan_still_finds_the_literal_in_the_registered_file_so_a_pass_is_not_an_empty_scan` | the same scanner on `inputs/memory-limit.json` | one match | fakes |
| M3 | `Each_stacks_statement_discloses_its_heap_setting_derived_from_the_value_by_the_layers_own_function` | both statements | the C# heap sentence names `DOTNET_GCHeapHardLimit` = the value `heapKeys('csharp', bytes)` gives (expected `0x1ccccccc`, 483,183,820 bytes, 90 %); the Node sentence names `--max-old-space-size=` the value `heapKeys('node', bytes)` gives (expected 460); the words "Build resources are separate." are present | fakes |
| M4 | `Both_stacks_get_the_same_memory_sentence_and_the_same_load_configuration_and_differ_only_in_the_stack_heap_and_framework_sentences` | the workspace of each quick task (extends the existing quick-workspace test) | the statements agree byte for byte up to the marked sentences; `samples/load.json` is byte-identical across stacks except the per-stack `heap` block | fakes |
| M5 | `A_registered_value_that_is_not_a_positive_whole_number_of_MiB_is_refused_when_the_inputs_load_and_names_the_file` | `memory-limit.json` with 536870913, 0, a string | `loadExplore`/`memoryLimit` fails naming the file and the rule; no workspace is built | fakes |
| M6 | `The_score_records_the_enforced_limit_of_every_case_read_back_from_the_cgroup_and_a_case_whose_read_back_differs_from_the_value_is_an_environment_failure_not_a_verdict` | a runner record whose `lifecycle.memory.maxBytes` is the value for every case; then one case at 268435456 | each rollup case has `memoryMaxBytes === 536870912`; the second gives kind `environment` with a reason naming the case and both numbers; no verdict | fakes |
| M7 | `On_the_certifying_host_a_case_scope_reads_memory_max_536870912_and_an_invented_workload_over_the_limit_is_stopped_and_classified_memory_limit_with_its_cause_in_both_stacks` | the probe's over-limit programs of both stacks (`lang-bench/lib/exec-probe/programs.mjs`, modes `overlimit` and `overlimit-objects`) run as one pass through the lang-explore pass path with the limit from the one file | for each stack and mode: the case's `memoryMaxBytes` is 536870912 (the runner read it from `prog/memory.max`); the verdict is `memory limit`; the cause is recorded (`oomKill >= 1`, or `runtimeAbort` `dotnet`/`v8` with `peakBytes * 2 >= maxBytes`); `memoryCertified` is true; the run's `cleanup.clean` is true | certifying host |

#### 2.5 Growth and the disk rule (`test/quick-growth.test.mjs`, pure, fakes)

| # | test name | given | observed |
|---|---|---|---|
| G1 | `The_pairs_worst_case_is_two_sessions_trees_plus_twice_the_measured_retained_size_of_ten_passes_with_every_re_run` | `growth.json` fields `sessionTreeBytes`, `retainedTenPassesWorstBytes` | `pairWorstCaseBytes()` is `2 * sessionTreeBytes + 2 * retainedTenPassesWorstBytes`; a missing field is a refusal naming it, never a default of 0 |
| G2 | `The_disk_rule_halts_below_twice_the_pairs_measured_worst_case_and_goes_at_or_above_it` | free bytes one under, equal to, one over `2 * pairWorstCaseBytes` | `halt` with the sentence naming both numbers; `go`; `go`. (The series-level halt before a pair, resumable, is F3.S1's test over this function.) |

#### 2.6 Through the real box (`test/quick-passes-real.test.mjs`, real box)

| # | test name | given | observed |
|---|---|---|---|
| R1 | `A_toy_program_that_fails_only_when_its_Cpus_allowed_list_is_6_7_reads_intermittently_incorrect_with_exactly_five_failing_passes_so_the_profile_reaches_the_program` | an invented Node daemon on the Node daemon template that reads `/proc/self/status` and answers wrongly when its list normalises to `6-7`; one invented visible scenario and two invented held-out scenarios in a temp home | the verdict is `intermittently incorrect`; the failing passes are exactly 1, 3, 5, 7, 9 (the 2-CPU ones); the clean passes exactly 2, 4, 6, 8, 10; thirty attempts never happened (ten files); every attempt file names the pass's profile and observed affinity |
| R2 | `A_toy_program_that_passes_everywhere_reads_accepted_10_of_10_under_both_profiles_at_the_registered_limit_in_both_stacks` | the same toy without the failing branch, once per stack (the C# one on the C# daemon template) | `accepted`; `explore.passes` 10; every case's `memoryMaxBytes` is the one value; the scoring time and retained bytes are printed as diagnostics (not asserted) |

#### 2.7 The atomic build commit point (`test/quick-build-commit.test.mjs`, fakes)

Each of these fails on a build done in place, or on a marker that is not checked against the tree.

| # | test name | given | observed |
|---|---|---|---|
| B1 | `A_build_happens_in_a_temporary_owned_folder_and_is_renamed_into_place_only_after_it_was_validated_and_its_completion_record_names_the_trees_hash` | a fake build that records the folder it built in | the build ran in `score/build-<token>/`, never in `score/tree/`; after it `score/tree/` exists, `score/build-*` does not, and `score/build.json` names `treeSha256` equal to the hash of `score/tree/` computed by the test with the same function, plus exit, wall, `timedOut`, tail and the frozen changes; the record was written after the rename (the fake records the order of the two writes) |
| B2 | `A_resume_that_finds_a_built_tree_without_its_completion_record_discards_it_and_builds_again` | `score/tree/` on disk (a stray file inside), no `build.json`; also a leftover `score/build-abc/` | both are removed; `buildFn` is called once more; the new tree has no stray file; `build.json` is then written |
| B3 | `A_completion_record_whose_hash_does_not_match_the_tree_it_names_is_a_halt_and_no_pass_runs` | `build.json` and `score/tree/` present, one byte of the tree changed after the commit | the driver answers kind `environment` with reason starting `build-record-mismatch` naming both hashes; no pass child is called; an event file records it; the attempt files already there are untouched |
| B4 | `A_build_that_ran_and_failed_is_committed_with_ok_false_and_a_build_that_never_began_commits_nothing` | a marked exit 1; then a markerless exit 1 | the first: `build.json` with `ok: false` and the tail, every case `build failed`, no pass; the second: no `build.json`, no `score/tree/`, an environment attempt of the build step, and a third markerless answer exhausts it (`unscored: environment`) |

#### 2.8 One scorer per session (`test/quick-passes-lock.test.mjs`, fakes)

| # | test name | given | observed |
|---|---|---|---|
| L1 | `scorePasses_refuses_to_start_unless_this_process_holds_the_series_lock_that_run_and_rescore_take` | a series dir whose `series.lock` is absent; then one held by another pid; then one this process took with `acquireSeriesLock` | the first two refuse at once with a sentence naming `series.lock` and the holder, write nothing under `score/`, and call no build; the third runs |
| L2 | `Two_concurrent_starts_on_the_same_session_cannot_run_the_same_pass` | two drivers started together on one session under one held lock (the second from a second `acquireSeriesLock` attempt, which fails; and a forced second start inside the same process) | the second start refuses with `scorer-busy` naming the first's pid and the lock file `score/scorer.lock` (`wx`, pid and start time, taken over only when its owner is dead); after both return, every pass has exactly one attempt file for each attempt and the fake pass child saw each `(pass, attempt)` once |
| L3 | `A_scorer_lock_whose_owner_is_dead_is_taken_over_and_one_whose_owner_lives_is_not` | `score/scorer.lock` naming a dead pid; then a live one | the first start takes it over and records a `takeover` event; the second refuses |

#### 2.9 The complete validator for attempt records (`test/quick-attempt-record.test.mjs`, pure, fakes)

Each case gives an environment attempt whose reason names the defect; none gives a verdict.

| # | test name | given | observed |
|---|---|---|---|
| C1 | `A_truncated_or_unparseable_record_is_an_environment_attempt_never_a_functional_failure` | the first half of a real-shaped record's bytes; `{}`; empty; not JSON | kind `environment`, reason `record-invalid: …`; `cases` absent |
| C2 | `A_record_without_the_runners_completion_marker_is_an_environment_attempt_even_when_every_case_it_holds_is_present` | a record missing `format`, or `runner` not the pinned version, or `build`/`launch` absent | environment, reason naming the missing or wrong key; the pinned values are read from the runner's pin, not retyped |
| C3 | `A_partial_record_holding_a_non_pass_verdict_is_an_environment_attempt_and_the_verdict_in_it_is_not_counted` | expected `s01, h001, h002`; a record with `s01` FAIL `wrong answer` and `h002` PASS, `h001` missing | environment, reason names `h001`; `verdictOf` over a session with nine clean passes and this attempt is `pending`, never `rejected` |
| C4 | `A_duplicate_id_or_an_unknown_id_is_an_environment_attempt` | `h001` twice; an id `h777` not expected (the invented id the existing quick-score test uses for a case that is not there) | environment, reason names the id and says duplicate or unexpected |
| C5 | `A_result_missing_a_required_field_is_an_environment_attempt_and_the_required_list_is_one_exported_constant` | each required field removed in turn from one result, iterating the exported `REQUIRED_CASE_FIELDS` (never a retyped list) | environment, naming the id and the field; a result with every field passes the validator (the positive case, so the file is not all refusals) |

#### 2.10 `verdict.json` as a derived snapshot (`test/quick-verdict-snapshot.test.mjs`, fakes)

| # | test name | given | observed |
|---|---|---|---|
| D1 | `verdict_json_is_rebuilt_from_the_attempt_and_event_files_alone_and_a_deleted_or_corrupted_one_comes_back_identical` | a scored session; delete `verdict.json`; then write garbage into it; then write a plausible snapshot with an extra invented event | after each, one driver start rebuilds it byte for byte equal to the original; the invented event is gone (it had no event file); the rebuild function never opens `verdict.json` for reading (a fake `fs` records every open) |
| D2 | `An_event_is_a_file_written_once_and_a_resume_that_rebuilds_the_snapshot_loses_none` | three halts over three starts (P7's, A2's, B3's) | three event files with distinct names (`<utc>-<kind>.json`, `wx`); the snapshot's `events` has three entries in file order after any number of restarts; a driver that merged from the previous snapshot would be caught by D1 |

#### 2.11 The disk guard before the first measurement (`test/quick-growth-bootstrap.test.mjs`, fakes)

| # | test name | given | observed |
|---|---|---|---|
| E1 | `The_measurement_refuses_to_start_below_the_free_disk_floor_and_stops_resumably_when_its_folder_passes_the_write_ceiling` | a fake `statfs` giving one byte under `MEASURE_FREE_FLOOR_BYTES`; then enough, and a fake `du` of the measurement folder that passes `MEASURE_WRITE_CEILING_BYTES` after the second attempt | the first refuses before any subject is copied, naming both numbers and the reason; the second stops after the attempt that crossed the ceiling, records a `ceiling` event, leaves every attempt file, and exits 3 |
| E2 | `An_ordinary_scoring_run_refuses_to_start_without_a_valid_growth_json_and_only_the_bootstrap_flag_goes_without_it` | no `growth.json`; an invalid one; then the driver started with `bootstrap: true` | the first two refuse naming the file and the validator's sentence, build nothing; the third runs and writes nothing to `growth.json` itself |
| E3 | `growth_json_is_validated_on_write_every_size_a_positive_whole_number_the_attempt_count_at_most_thirty_and_pairWorstCaseBytes_equal_to_the_formula` | the measurement's writer given a result with a size 0, a missing wall, `mostAttempts` 31, a worst case off by one | each refused with the field named; nothing written; a good result writes the file and `growth.mjs` reads it back equal |

#### 2.12 Scoring exhaustion apart from session finality (`test/quick-finality.test.mjs`, fakes)

| # | test name | given | observed |
|---|---|---|---|
| F1 | `The_verdict_file_carries_a_scoring_state_and_no_finality_and_every_consumer_reads_finality_from_the_run_map_alone` | records of all four states in a series fixture | `verdict.json` has `scoringState` and no key named `terminal`, `final` or `reRun`; `records.mjs`, `note.mjs` and the pair record read `class`/`data` from the run-map entry for finality and `explore.verdict`/`explore.scoringState` for the scoring; a scan of `lib/` finds no read of a finality key on a verdict object (and finds the one sanctioned read of `scoringState`, so the scan is alive) |
| F2 | `An_unscored_session_is_scoring_exhausted_and_not_final_and_its_finality_comes_from_the_series_after_the_one_void_re_run` | the loop over a session whose passes exhaust; then the re-run session scored `accepted`; alternatively the re-run exhausting too | after the first: entry `ran`, `data: false`, `class: VOID_UNSCORED`, `stackState` `void-rerunnable`; after the re-run scored: the cell's `finalOf` is the re-run, `stackState` `done`; after a second exhaustion: `void-final`, the cell has no data session and the pair record says so per stack |

That is **62 scenarios**: 12 + 14 + 5 + 8 + 2 + 2 + 4 + 3 + 5 + 2 + 3 + 2 (M2 is two tests, which the
structural-scan rule requires). The existing tests of `quick-score`, `quick-rescore`, `quick-rescore-run`,
`quick-replay`, `quick-records`, `quick-workspace` and `code-shape` stay green as they are; a new `lib/*.mjs` is
measured by `code-shape` without being listed.

---

### 3. The brief: what to change, file by file

Read first: `spikes/lang-explore/lib/score-quick.mjs`, `run-ctx.mjs`, `records.mjs`, `series.mjs`, `rescore.mjs`,
`replay.mjs`, `workspace-quick.mjs`, `quick.mjs`, `limits.mjs`, `inputs.mjs`, `explore.mjs`;
`spikes/lang-bench/lib/exec-layer/run.mjs`, `prefix.mjs`, `env.mjs` (`heapKeys`), `scope.mjs`;
`spikes/minigit-bench/lib/bare-series.mjs` (`scoreOne`), `run-map.mjs` (`transition`, `NEXT`), `score-run.mjs`
(the pattern of a scorer in a child process), `cpu-sampler.mjs` (`maskObserved`, `allowedList`).

**Limits on the code.** Every function under 50 lines and cyclomatic complexity at most 4, no exemption
(`test/code-shape.test.mjs` measures every `lib/*.mjs`). Data is immutable: `Object.freeze`, spread, never a mutation
of an argument. Failures are values (`ok`/`fail` of `minigit-bench/lib/result.mjs`); an environment failure is
`fail(reason, { kind: 'environment' })`. Files written with `{ flag: 'wx' }` where append-only is the rule. No
held-out content, case id or trap name reaches a public file; `leakcheck` and the line scan run before the merge.

#### 3.1 New modules under `spikes/lang-explore/lib/`

**`verdict.mjs`** (pure; no I/O).
- `PROFILES = Object.freeze([{ name: '2cpu', cpus: '6-7' }, { name: '8cpu', cpus: '0-7' }])`, `PASS_COUNT = 10`,
  `ATTEMPTS_PER_PASS = 3`.
- `profileOfPass(n)` → the odd passes' profile is `'2cpu'`, the even passes' is `'8cpu'`.
- `VERDICTS = Object.freeze(['accepted', 'rejected', 'intermittently incorrect', 'unscored: environment'])`.
- `isClean(cases)` → every case `verdict === 'pass'`.
- `passState(pass)` → `'clean' | 'failing' | 'open' | 'exhausted'` (open: no completed attempt and attempts left).
- `verdictOf({ build, passes })` → `Object.freeze({ verdict, scoringState, counts: { clean, failing, open, exhausted }, reason })`
  where `verdict` is one of `VERDICTS` or `'pending'` and `scoringState` is `'open'` (verdict `pending`),
  `'decided'` (`accepted`, `rejected`, `intermittently incorrect`) or `'exhausted'` (`unscored: environment`). No
  key says whether the session is final: that is the run-map's (section 1, *One meaning of terminal*). A pass whose
  shape is wrong (an environment attempt carrying `cases`, an attempt without a kind) throws, so a wrong fixture is loud.
- `rollupOf({ build, passes, limits })` → the one score in `scoreShape`'s result shape: per case the worst verdict over
  the completed passes (the first failing pass's `verdict`, `reason`, `firstMismatch`, `rIds`, `comparison`),
  `peakBytes` and `wallMs` the largest over the completed passes, `memoryMaxBytes` the common read-back, plus
  `passes: <completed count>`.

**`attempt-record.mjs`** (pure; the complete validator of 2.9).
- `REQUIRED_CASE_FIELDS = Object.freeze(['id', 'status', 'reason', 'wallMs', 'lifecycle.memory.peakBytes', 'lifecycle.memory.maxBytes', 'lifecycle.leftover'])`
  and `COMPLETION_KEYS = Object.freeze(['format', 'runner', 'kind', 'build', 'launch', 'cases'])`.
- `attemptRecordProblem({ text, expected, pinnedRunner })` → null, or one sentence starting `record-invalid:`
  naming the first defect: not JSON, a completion key missing, `format`/`runner` not the pinned values (read from
  `lang-bench/inputs/specs.d/runner.json` through `harness-pin.mjs`, never retyped), a missing, duplicate or unknown
  id, a missing required field. The driver turns any sentence into an environment attempt and never reads the cases
  of a record that has one (C1–C5). `casesOfRecord` of `score-quick.mjs` calls it first and keeps its own mapping.

**`build-commit.mjs`** (the atomic commit point of section 1 and 2.7).
- `treeHash(dir)` → sha256 over every file's name and bytes in name order (the shape of `casesTreeHash`).
- `commitBuild({ work, owned, prepare, build })` → builds in `score/build-<token>/`, validates, renames to
  `score/tree/`, writes `score/build.json` (`wx`) → `ok(record)` | environment `fail`. The record:
  `{ treeSha256, ok, exit, timedOut, wallMs, tail, frozenChanges, harnessDrift, nonRegularLeftOut, committedAt }`.
- `committedTree(work)` → `ok(record)` when `build.json` exists and `treeHash('score/tree')` equals its
  `treeSha256`; `fail('build-record-mismatch: …')` when it differs (B3); `ok(null)` when there is no record.
- `discardUncommitted(work)` → removes every `score/build-*` and a `score/tree/` without a record (B2); returns the
  names removed, which go into an event file.

**`passes.mjs`** (the driver; reads and writes `s/<rid>/score/`).
- Layout, all under `score/`: `build-<token>/` (temporary, never scored), `tree/` (the committed built copy, removed
  when `scoringState` leaves `open` or the void is settled), `build.json` (the completion record, `wx`),
  `passes/pNN-<profile>-aK.json` (one attempt, `wx`), `events/<utc>-<kind>.json` (one halt, takeover, discard or
  ceiling event, `wx`), `scorer.lock` (one scorer per session), `verdict.json` (the derived snapshot: rebuilt from
  `build.json`, `passes/` and `events/` on every write, written whole to a temp name and renamed, **never read as an
  input and never merged from itself**), `score.json` (the rollup the record reads, written once when the state
  leaves `open`).
- `attemptFile({ work, n, profile, attempt })`, `eventFile({ work, now, kind })` → the paths.
  `readAttempts(work)` → the passes as `verdictOf` reads them, each attempt through `attemptRecordProblem`.
  `rebuildVerdict(work)` → reads the immutable files and writes the snapshot; it takes no previous snapshot (D1, D2).
- `lockHeldByMe({ seriesDir, pid, startTime })` → true when `<series>/series.lock` names this process (pid and start
  time, the pair `series-lock.mjs` stamps); `scorePasses` refuses at once when it is false (L1). The series lock is
  the one `run` and `rescore` already take; the driver never takes it itself.
- `takeScorerLock({ work, pid, startTime, isAlive })` → `score/scorer.lock` created `wx` with the same stamp; a live
  owner refuses `scorer-busy`; a dead owner is taken over with a `takeover` event (L2, L3). Released in `finally`.
- `scorePasses({ work, seriesDir, prepare, build, runPass, affinity, seriesState, growth, bootstrap = false, now, signal, say })` →
  `{ kind: 'scored', score, verdict } | { kind: 'void', class: VOID_UNSCORED, reason, verdict } | { kind: 'environment', reason } | { kind: 'stopped', reason }`.
  Steps, in this order, each its own function: (1) `lockHeldByMe` else refuse (L1); (2) `takeScorerLock` (L2, L3);
  (3) unless `bootstrap`, `growth.loaded()` must be a valid `growth.json` else refuse naming it (E2); (4) refuse while
  a session of the series is `launching` (P7); (5) `discardUncommitted`, then `committedTree` (B2, B3), then
  `commitBuild` when there is no record (B1, B4, up to three tries of a markerless build); (6) for each pass with an
  attempt left and no completed attempt: the affinity probe (A2), the child under `taskset -c <cpus>` (P1), the
  attempt file, `rebuildVerdict`; (7) at the end `rollupOf`, `score.json`, `rebuildVerdict`, and the tree removed.
  `signal` ends it between two attempts (P9). Every refusal or halt before an attempt is an event file and an answer of
  kind `environment`, so the loop halts resumably (exit 3) and nothing is scored.
- `runPassChild({ cpus, args, spawn })` → spawns `taskset -c <cpus> <node> lib/pass-child.mjs <args file>` and reads
  back the child's `Cpus_allowed_list` right after the spawn with `maskObserved` from `minigit-bench/lib/cpu-sampler.mjs`;
  a mismatch kills the child and answers the `affinity-not-applied` halt.

**`pass-child.mjs`** (an entry, run as a process; the shape of `minigit-bench/score-session.mjs`).
- Reads its own `/proc/self/status`, compares with the wanted `cpus` (A3), then runs one pass: `runPass` of
  `score-quick.mjs` over `score/tree/` and the case folder; writes the attempt JSON to the path it was given, exit 0
  (completed), 3 (environment), 4 (usage). It inherits the affinity, and so do the scope, the namespaces, the box and
  every program: the execution layer is not changed and keeps its pinned identity.

**`affinity.mjs`** (pure but for one read).
- `normalizeCpuList(text)` → `'0-7'` from `'0,1,2-7'`, null from an empty or malformed text.
- `affinityProblem({ wanted, observed })` → null or the sentence `affinity-not-applied: wanted <wanted>, observed <observed>`.
- `probeMask({ cpus, spawn })` → spawns `taskset -c <cpus> cat /proc/self/status`, answers `ok(observed)` or `fail`.
  Used by the pass driver before each pass and by the pair preflight for both session masks (A5).

**`memory-limit.mjs`**.
- `memoryLimit({ file = inputs/memory-limit.json })` → `ok(Object.freeze({ bytes, mib, decidedOn, by }))` or a `fail`
  naming the file when `bytes` is not a positive whole number of MiB (M5).
- `memorySentence(limit)` → D72's sentence, exactly: *"The program and its descendants run under a 512 MiB
  (536,870,912-byte) cgroup memory limit, including runtime overhead. Build resources are separate."* with both
  numbers formatted from `bytes`.
- `heapSentence(stack, limit)` → the stack's sentence from `heapKeys(stack, bytes)` of `lang-bench/lib/exec-layer/env.mjs`
  (never a typed hexadecimal or a typed 460).
- `loadTestConfig(stack, limit)` → the object written to `samples/load.json`: `{ memoryLimitBytes, memoryLimitMiB, heap: heapKeys(stack, bytes), note }`.

**`growth.mjs`** (pure but for the one file).
- `pairWorstCaseBytes({ sessionTreeBytes, retainedTenPassesWorstBytes })`, `diskRuleVerdict({ freeBytes, pairWorstCaseBytes })`
  → `{ go: true } | { go: false, reason }`; both refuse a missing number (G1, G2).
- `MEASURE_FREE_FLOOR_BYTES`, `MEASURE_WRITE_CEILING_BYTES` (section 4, the bootstrap's temporary guard; E1) and
  `measureGuard({ freeBytes, folderBytes })` → `{ go } | { go: false, reason }`.
- `growthProblem(record)` → null or a sentence (every size a positive whole number, every wall present,
  `mostAttempts` at most 30, `pairWorstCaseBytes` equal to the formula; E3); `writeGrowth({ file, record })` refuses
  on a problem, else writes to a temp name and renames; `loadGrowth({ file })` → `ok(record)` after the same check,
  `fail` naming the file when missing or invalid (E2).

#### 3.2 Changed modules

**`spikes/lang-explore/inputs/memory-limit.json`** (new): `{ "bytes": 536870912, "mib": 512, "decidedOn": "2026-11-07", "by": "D72, the team lead, from consult 0a205591", "note": "one registered value for all six quick tasks; the statement, samples/load.json and the scorer read it here" }`.
`inputs/explore.json`: the `quick.limits.memoryBytes` key goes; `quickCalibration` (`quick.mjs`) reads the memory file
and `inputs.mjs`'s `quickLimitsOk` no longer asks for it. The olympiad `limits.memoryFloorBytes` is another thing (the
olympiad calibration floor) and is left alone; M2a's scan names the `quick` block only.

**`inputs/growth.json`** (new, after the measurement): counts, times and sizes only (section 4).

**`score-quick.mjs`**: split `scoreIn` into two exported steps and keep `scoreQuick` as their composition for
`replay` (one copy, one build, one run, `only`):
- `prepareAndBuild(args, area)` → `ok({ copy, frozen, drift, left, cases, build })` | `fail`; a failed build is
  returned with `build.ok false` so the driver writes the `build failed` rollup and runs no pass.
- `runPass({ a, copy, cases, owned, dirs })` → today's `runStep` plus: carry `maxBytes` of each case's
  `lifecycle.memory` into the case as `memoryMaxBytes` (`caseOf`), and refuse with `environment` a case whose
  `memoryMaxBytes` is not `a.limits.memoryBytes` (M6). `enforcedBounds` is unchanged.

**`run-ctx.mjs`**: `scoreSessionDep` for a quick world passes `seriesDir` (for `lockHeldByMe`) and returns
`scorePasses(...)`'s answer mapped to the loop's kinds: `scored` (scoring `decided`) → `{ kind: 'scored', score }`;
`void` (scoring `exhausted`) → `{ kind: 'void', class, reason }`; `environment` and `stopped` → as today
(`environment` halts the loop resumably, exit 3). The olympiad path is unchanged. `runDeps.preflight` gains the two
mask probes of the pair (A5). `rescore` reaches the same dep under the series lock it already holds, so L1 holds for
it without a change to `rescore.mjs`.

**`spikes/minigit-bench/lib/bare-series.mjs`** `scoreOne`: the two transitions of section 1, *One meaning of
terminal*: `r.kind === 'scored'` → `transition(map, rid, 'scored')` as today (`ran → scored`, the session final);
one new branch for `r.kind === 'void'` → the entry is patched on its `ran` status, no transition (`NEXT` allows only
`ran → scored`, and the void stays `ran` so the series' void schedule sees it):
`{ class: r.class, data: false, reclassifiedFrom: [...(entry.reclassifiedFrom ?? []), { class, data, reason, at }] }`;
the ledger gets an `event` line, the session record is written with `score: null`, and the loop goes on. Every
existing kind behaves byte for byte as today; tests in `spikes/minigit-bench/test/bare-series.test.mjs` pin the new
branch, pin that `'scored'` still transitions, and pin that a `void` entry is `void-rerunnable` to `stackState` and
`void-final` once its re-run exists (F2). This is the one change to v1 and it is small; say its line count in the
summary.

**`series.mjs`**: nothing in the schedule changes (`isVoid` already reads a class starting with `void:`). Export
`VOID_UNSCORED = 'void: unscored environment'` here so the class string has one source.

**`records.mjs`** `exploreSection`: `accepted: known(verdict === 'accepted')` read from `score.explore.verdict`
(`acceptedOf` stays for the olympiad path and for the rollup's own consistency check); add `verdict`,
`scoringState`, `passes`, `scoringMs`, `buildTreeSha256` (known); `SCORED_PATHS` gains `explore.verdict`,
`explore.scoringState` and `explore.passes`. Finality (`class`, `data`) stays v1's, from the run-map entry, as it is
today; `exploreSection` never writes a finality key (F1). `missingPaths` of an existing olympiad record is unchanged
(a test).

**`note.mjs`**: the four verdict counts per stack from `explore.verdict` (P14) and the finality counts (data,
void-rerunnable, void-final) from the run-map classes, derived, no number typed (F1).

**`workspace-quick.mjs`**: `quickStatement` gains two marked lines after the stack sentence:
`<!-- memory-sentence -->` + `memorySentence(limit)` and `<!-- heap-sentence -->` + `heapSentence(stack, limit)`;
`filesFrom` adds `samples/load.json` from `loadTestConfig(stack, limit)` (frozen, since `frozenOf` freezes `samples/`;
`writeVisible` only matches `sNN.in/out`, so the scorer never reads it as a case). `quickWorkspaceFiles` takes the
limit from `memoryLimit()` (a seam for tests). The templates' identity moves once with F1.S3's template edit, not here.

**`explore.mjs`**: unchanged in its re-exec (`run`, `rehearse`, `rescore`, `replay` stay under `taskset -c 6,7`;
the pass children widen to `0-7` themselves, which the kernel allows without a cpuset, and A4 proves on this host).

**`rescore.mjs`, `replay.mjs`**: no edit. Rescore scores through `scoreSessionDep`, so it gets the ten passes (P12);
replay calls `world.scoreFn` with `only`, which stays the one-pass `scoreQuick` (P13).

**FORMAT's second amendment (D72, part (b)).** In the task repository, F-LIMITS-D's sentence "is given to the test
runner, not to the program's author" becomes: the bound is the one the statement states, the same for every task,
registered before the freeze and enforced as F-MEMORY says. One document gate (`review_document`) on that pull request,
a changelog line, `Format version: 2` kept. Then the bench's pins move: `spikes/lang-bench/inputs/specs.d/format.json`,
FORMAT's line in `inputs/hashes-lang.txt`, `PINNED_TASK_SHA`, `inputs/gates/FORMAT-v2.md` appends the round; the
checks `specs-record.mjs --check`, `pin-runner.mjs --check`, `pin-exec-layer.mjs --check` exit 0. Order: the pins
branch of F1.S1 (e) merges first; this amendment moves them once more.

**Documentation**: `research/module_lang_explore.md` describes the one engine with the ten passes, the four states
and the verdict file (E3.S10's part); `research/module_tests.md` gains the 62 flows as covered with their files, and
under *what it does not prove*: ten passes catch a race that shows often on the scoring CPUs and a rarer one can pass
all ten (T25); the certifying-host check runs on the owner's host before the freeze and at each release of the harness,
not in CI.

#### 3.3 What must not change

- The meaning of every existing record field: `quality.*`, `explore.cases[*]` (set, id, verdict, wallMs, peakBytes,
  oomKills, exit), `explore.passRate`, `explore.verdicts`, `explore.limits`, `explore.buildMs`, `trapHits`,
  `frozenChanges`, `buildTest`. New fields are added beside them (D34: metrics are only added). An olympiad record is
  byte for byte what it was.
- D66's one acceptance policy: the frozen build is the acceptance build; `copy-quick.mjs` and `build-quick.mjs` are
  not edited here (F1.S3 owns the contract).
- `replay`'s diagnostic status: its note, its one output file, its refusals, its lock.
- The execution layer (`spikes/lang-bench/lib/exec-layer/`) and the runner: no edit, so `exec-layer.json` and
  `runner.json` keep their identity. The affinity is applied to the pass child, outside the layer.
- The run-map's transition table.
- No attempt, event or completion-record file is ever rewritten or removed. `verdict.json` is the one derived file:
  rebuilt whole from those files on every write, written to a temp name and renamed, never opened for reading by the
  driver and never merged from itself, so a resume cannot lose an entry of its lists (D1, D2).
- The series lock's contract (`series-lock.mjs`): the driver proves it is held and never takes it.

#### 3.4 Commands the coder runs

```
node --test spikes/lang-explore/test/                                   # plain
BENCH_REQUIRE_SANDBOX=1 node --test spikes/lang-explore/test/            # the completion run, nothing skipped
BENCH_REQUIRE_SANDBOX=1 node spikes/minigit-bench/run-tests.mjs           # the one v1 change
BENCH_REQUIRE_SANDBOX=1 LANG_V2_TASK_REPO=<task repository checkout> node spikes/lang-bench/run-tests.mjs
node spikes/lang-explore/explore.mjs leakcheck
python3 -I ~/rsd/lang-v2/briefs/leakscan-lines.py . history
```

Every scenario of 2.1 to 2.5 and 2.7 to 2.12 is written RED first and watched failing for its own reason (the message names the
missing behaviour, not a setup error); the summary carries the red message and the green run of each. R1, R2 and M7
run on the owner's host; their runs are recorded with their numbers.

---

### 4. The measurement before any freeze

Private: `~/rsd/lang-v2/briefs/f1s2-spec.md`, section 4. A bounded bootstrap step, guarded by a conservative free-disk floor and a write ceiling stated there with their reasons. It measures the real wall time and the retained size of ten passes, and of the worst case of retries and the void re-run, on the known intermittent C# session and on accepted sessions of both stacks. It writes and validates `growth.json` last, and ordinary scoring refuses to start without it. Its inputs and outputs stay under `~/rsd/lang-v2/`; only `growth.json`'s counts, times and sizes and one prose line reach git.

### 5. What could go wrong

Each place a wrong verdict could come from, and the scenario that catches it.

| the risk | how it would show | caught by |
|---|---|---|
| The verdict reads a majority or an average: nine clean passes accept | a racy program accepted | V4 |
| An environment failure counted as a functional one, or the reverse | a harness fault rejects a session; a real failure voided | V5, V6, V8, V10, V11 |
| The terminal rule weakened: a functional failure beside an exhausted environment pass becomes `unscored: environment` and is re-run as a void | a rejected session re-run for free, biasing CPC | V6, P11 |
| A pass re-run more than twice, or an attempt dropped on the floor | the budget grows; evidence lost | P3, P4 |
| A resume re-runs completed passes or overwrites attempt files | append-only broken; scoring time doubled | P4, P5, P9 |
| The profile never reaches the program (the child inherits the parent's `6,7`, or `taskset` fails silently) | all ten passes on two CPUs; the 8-CPU half is fiction | A2, A3, A4, R1 |
| The read-back compares spellings (`0,1,2,3,4,5,6,7` vs `0-7`) and refuses a good mask, or accepts a superset | needless halts, or a wrong mask accepted | A1 |
| A pass starts while a session runs (the 8-CPU profile overlaps mask A) | the session's time and the pass's result both disturbed | P7 (and F3.S1's order) |
| The statement says one number and the scope enforces another (the quick run's task 07 confound, T26) | agents aim at a wrong limit | M1, M2a, M6, M7 |
| A typed hexadecimal in the heap sentence drifts from the layer's `heapKeys` | the statement lies about the heap | M3 |
| The limit literal reappears somewhere (a test fixture copied into `lib/`, a template) | two sources, one changes | M2a, with M2b proving the scan is alive |
| The runner's `maxBytes` is not carried into the score, so the enforced limit is never read back | M6 passes vacuously | M6 (a fixture the code rejects must fail), M7 |
| The rollup changes the meaning of a record field (a mean peak, a first-pass verdict) | the report's memory claim or `defects` wrong | P8, the unchanged olympiad record test in `quick-records` |
| `accepted` in the record still reads `acceptedOf(score)` and ignores the pass count | a session accepted on fewer than ten passes | P8, V1 |
| `unscored: environment` recorded as `scored` with data, so it enters CPC | a void in the denominator | P10 |
| A void re-run of a rejected session | a session paid twice and the best kept | P11 |
| `rescore` or `replay` quietly becomes a second verdict path | two engines | P12, P13 |
| The note or the report types a count | a number not from data | P14 |
| The disk rule reads a guessed number or defaults a missing one to zero | a halt that never fires, a disk filled mid-pair | G1, G2, section 4 |
| Scoring time added to the agent's wall | time partition wrong | P6 |
| The build box's non-zero exit from a host fault during a marked build reads as a submission | a false `build failed` | known limit of `build-quick.mjs`; the build's tail is kept; the blind adjudication reads it (F3.S3) |
| A build interrupted or killed halfway is scored as if whole (a half-built tree in place, a marker from an earlier build) | passes judge a tree the agent never submitted as built | B1, B2, B3 |
| A completion record believed without checking the tree it names (a tree changed after the commit) | ten passes over the wrong bytes | B3 |
| Two scorers on one session (a `rescore --wait` racing a resumed `run`, or two resumes) interleave attempts, double-write a pass or score a half-committed build | duplicate or torn attempt files; a verdict from mixed states | L1, L2, L3 |
| A truncated or partial runner record read as a verdict (the runner writes its JSON in place, not atomically; a kill mid-write leaves half a file) | a functional failure invented from a half record, or a pass counted clean with cases missing | C1, C3, C4, V11 |
| A record without the runner's completion keys accepted because its `cases` look fine | the same | C2 |
| `verdict.json` merged from its previous self loses an event or a halt on a crash between two writes, or keeps an entry whose file never existed | the audit trail wrong, both ways | D1, D2 |
| The first measurement fills the disk or runs unbounded because no number exists yet | the host full mid-bootstrap, other sessions' trees at risk | E1, section 4 |
| An ordinary run starts before `growth.json` exists or on an invalid one | the disk rule reads nothing | E2, E3 |
| `terminal` means two things: a consumer reads the verdict file for session finality, or the run-map for a verdict | an exhausted session reported final, or a void counted as scored | F1, F2 |
| A rarer race passes all ten | accepted wrongly | not caught; T25 says so in the report |

---

### 6. Open points for the judge (found while writing; none changes the rule above)

1. **"Both references of task 06."** The plan (F1.S2, F2.S1, D40) speaks of a reference per stack. On disk on
   2026-11-07 each `refs-NN/` holds one `ref.py`, `samples/` and one `ws/` (`build.sh`, `run.sh`); there is no
   `csharp/` and `node/` pair. The data path refuses a stack other than `csharp` or `node` (`HALT_STACK`), so the
   reference as it stands cannot be scored as a data run; it can be scored with `data: false`, which leaves memory
   uncertified. The measurement in section 4 therefore runs on the references that exist when it runs: if F2.S1 has
   made the per-stack references of task 06 by then, both; otherwise the one in `ws/` as a non-data run, named as such
   in `growth.json`. The judge decides whether F1.S2's *Accepted when* ("both references read `accepted`") waits for
   F2.S1's task 06 references or is read against what exists.
2. **The class string of the void.** `series.mjs` plans a re-run only for a class starting with `void:`; the plan's
   label is `unscored: environment`. This spec records the label in `verdict.json` and the record (`explore.verdict`)
   and uses `void: unscored environment` as the run-map class. If the judge prefers another string, it has one source
   (`VOID_UNSCORED`).
3. **"The visible load test's configuration."** No file or module in the tree defines it today (the visible samples are
   the spec's section 6 table; F-LOAD's load scenarios are hidden). This spec makes it a generated frozen file
   `samples/load.json` read by the agent, so the value the agent tests against is the enforced one. F2.S1 may name it
   differently when it generates the load test; the one source stays `memory-limit.json`.
4. **The session-launch read-back.** *The scoring CPUs* asks for a read-back before each session launch too. Today
   `bare-phase.mjs` observes the mask 15 s after the launch and records it; it does not refuse. A5 adds a probe in
   lang-explore's pair preflight that refuses before any paid launch. The in-flight observation stays as it is.
5. **The olympiad floor.** `inputs/explore.json`'s `limits.memoryFloorBytes` is also 536870912 but is the olympiad
   calibration's floor, not D72's value. M2a's scan leaves it alone and says so. If the judge wants it to read the
   same file, that is one line in `limits.mjs`.
6. **One change to v1.** The `void` result kind in `bare-series.mjs`'s `scoreOne` is the one edit to
   `spikes/minigit-bench/`; every existing kind keeps its behaviour. The alternative (settling the void in
   lang-explore after the loop) needs a `scored → ran` move the run-map forbids, so the small v1 seam is the honest
   shape.

## F1.S3 — the submission contract and the offered set

**Model:** scenarios + brief Opus (this document) · code Sonnet · review Opus · fixes Sonnet. **Parent:** F1.S3 (E1.S3b reduced
to what exists, E1.S4b), D65, D66, D70, D74, change 7.1, T-L62, T-L63. Two parts and two pull requests: **(a) the offered set
first**, because the contract's identity hashes the registered snapshot, and **(b) the contract** after it, so the identity
is computed once, last. Beside F1.S2; whichever of them merges second merges `main` first.

**Accepted when (parent):** a test that the frozen list has one source; the identity in the record; the D65 script case exits
non-zero with the frozen file unchanged; T-L63. The Stage 1 Definition of Done also asks for T-L62's `.npmrc` keys.

### (a) the offered set: `Mvc.Testing` in the C# snapshot, the matrix, `PACKAGES.md` from the matrix

**Scenarios** (`spikes/lang-bench/test/package-matrix.test.mjs`, new; the builds need the box and skip with their reason
without it):

- `Every_offered_package_in_each_applicable_template_has_a_row_of_the_matrix`: each package of the recipe, in each kind it
  applies to, has a row of `inputs/package-matrix.json`, `works` or `unsupported` with the code; a missing row fails.
- `JwtBearer_in_the_olympiad_kind_has_its_row_with_its_code` (measured on #167's branch: `NU1603`, through
  `Microsoft.Extensions.Logging.Abstractions` 8.0.0, whose dependency is in the cache only at 10.0.14). The recipe is changed so
  that it works, or the row stays `unsupported` and `PACKAGES.md` says it is for the web kind.
- `An_unsupported_row_takes_the_package_out_of_that_kinds_offered_set_in_the_recipe_PACKAGES_md_and_the_box`: for every row
  `unsupported` (package, template), three things hold, each asserted: the recipe does not offer the package for that kind (its
  entry's `kinds` leaves it out); that kind's `PACKAGES.md` does not list it; and adding it in that template and running
  `bash build.sh` in the build box with no registry, over the registered snapshot, fails with the row's recorded code, so the
  workspace cannot install it. A fixture row is also flipped to `unsupported` for a package that works, and the first two
  assertions then fail until the recipe and `PACKAGES.md` follow, which shows the test reads the matrix and not a copy of it.
- `PACKAGES_md_of_a_kind_lists_exactly_the_packages_whose_row_for_that_kind_is_works`, and
  `A_typed_package_line_in_PACKAGES_md_that_the_matrix_does_not_hold_fails`. `PACKAGES.md` may now differ between the kinds of
  a stack, exactly where the matrix differs (this replaces E1.S4b's byte-equal rule).
- `A_csharp_web_test_project_with_WebApplicationFactory_passes_offline_and_build_sh_still_ignores_tests`: in the web kind, a
  `tests/Tests.csproj` with `Microsoft.AspNetCore.Mvc.Testing` passes under `dotnet test --project tests/Tests.csproj` in the
  box with no registry, and `bash build.sh` still exits 0 with it present.
- `A_node_test_with_inject_passes_in_the_web_kind_with_no_added_package`: Fastify's `inject()` under `node --test`.
- `The_csharp_snapshot_is_registered_again_the_old_record_is_superseded_and_the_node_record_is_unchanged`.
- `A_supersede_after_the_stage_1_pre_registration_tag_is_refused` and `The_prune_sees_the_stage_1_pre_registration_tag`
  (`spikes/lang-bench/test/cache-lang.test.mjs`, with a fake tag reader). **RED today:** `lib/cache-store.mjs:205` reads only
  `prereg/lang-v2-A-*`, so after F3.S1's tag `prereg/lang-v2-<yyyymmdd>` a supersede would still be allowed and the prune would
  never run.

**Brief.**

1. `inputs/cache-recipe-lang.json` gains `Microsoft.AspNetCore.Mvc.Testing` with its `licence` and `maintenance`
   (`csharp.nuget-packages`: a Microsoft package under MIT, no private author), on the patch line of the shared framework the
   recipe already uses for JwtBearer; the exact version is the one the snapshot build resolves and records. The snapshot's test
   project `inputs/cache-projects/csharp/tests/SnapTests/SnapTests.csproj` references it, so the restore brings it and its
   dependencies into the cache.
2. Under the gate lock: `node spikes/lang-bench/make-snapshots.mjs --supersede csharp --reason "<the reason, change 7.1>"`,
   then `--stack csharp`. Node's snapshot is not remade. The old C# folder is pruned only after the pre-registration tag
   (`make-snapshots.mjs:7`), so it stays until F3.S1.
3. `PREREG_TAG_PATTERN` (`lib/cache-store.mjs:205`) and its sentence at `:206` read Stage 1's tag form
   `prereg/lang-v2-*`, which also matches the old group form; red first.
4. A writer, `spikes/lang-bench/package-matrix.mjs` with `--check`, builds each offered package in each applicable template,
   one at a time, in the build box with no registry, under the gate lock, and writes `inputs/package-matrix.json`.
   **An `unsupported` row excludes the package from that template's offered set:** each recipe entry gains `kinds`, the kinds it
   is offered in, which the writer's `--check` holds equal to the matrix's `works` rows; `renderPackagesMd`
   (`lib/packages-md.mjs:78`) takes the kind as well as the stack and lists only what the matrix says works there, so its caller
   `spikes/lang-explore/lib/workspace-quick.mjs:77` passes `task.kind`; nothing in it is typed by hand. The snapshot stays one
   per stack (a package a web task needs is in it), so the third assertion of the test is a real offline build that fails, not a
   missing file.
5. Docs: `research/module_lang_bench.md` § Package sets and snapshots, with the known limit on JwtBearer and the new rows;
   `research/module_tests.md`.

### (b) the contract: one frozen list, the `.npmrc` keys, the identity, `deviations`

**Scenarios.**

- `The_frozen_list_and_the_excluded_folders_have_one_source` (`spikes/lang-explore/test/quick-contract.test.mjs`, new): the
  copy, the restore, the workspace builder and lang-bench's template test read one exported list; the scripts' comment lines
  name exactly its pin files per stack; the module doc's frozen-list sentence names exactly its entries. Structural, with its
  companion: a scan of `spikes/lang-explore/lib/` and `spikes/lang-bench/lib/` for a second literal list of the pin-file names
  finds none, and the same scan finds the one source, so it can match.
- `The_node_npmrc_of_every_template_holds_ignore_scripts_and_save_exact` (`spikes/lang-bench/test/templates.test.mjs`, which
  follows `NPMRC_WORKSPACE`).
- T-L62, on the real Node templates in the build box (`spikes/lang-bench/test/scripts-stopped.test.mjs`, new):
  `A_lifecycle_script_that_rewrites_a_frozen_file_leaves_it_unchanged_and_the_type_error_fails_the_build`, for `preinstall`,
  `install`, `postinstall` and `prepare` (a `sed` that turns `strict` off in `tsconfig.json`, beside a source with a type
  error): the build exits non-zero with TS2322 and the frozen file is byte-for-byte unchanged; the control
  `The_control_without_ignore_scripts_still_exits_0_and_that_is_the_point`, with the frozen file in place at the start;
  `No_lock_entry_of_the_templates_or_the_offered_set_has_an_install_script`;
  `A_plain_npm_install_of_a_listed_package_writes_an_exact_version`; all three kinds still pass `npm ci --offline`, `tsc`,
  ESLint and `npx --offline`.
- `Every_record_json_carries_the_contract_identity_of_the_scoring_that_made_it`, a record written by `finish.mjs` from kept
  evidence included; `The_contract_identity_moves_with_a_template_a_snapshot_or_a_contract_module_and_with_nothing_else` (a
  change to `score-quick.mjs` or to the statement does not move it).
- `A_frozen_file_the_agent_changed_is_one_deviation_with_its_path_beside_accepted`;
  `An_untouched_tree_has_zero_deviations_known_never_unknown`;
  `Deviations_never_change_accepted_and_that_is_the_point` (a tree with a deviation and every case passed stays `accepted`).
- `Under_a_moved_template_a_changed_template_file_is_harness_drift_and_not_a_deviation`: a session made before this pull
  request and rescored after it does not charge the template's new `.npmrc` or `build.sh` bytes to the agent.
- `The_node_locks_do_not_move` (they were generated with `--ignore-scripts`).

**Brief (Sonnet coder).**

1. **One source.** A new `spikes/lang-explore/lib/frozen-set.mjs` holds what `workspace-quick.mjs:20-23` and `:98-101` hold
   today (the scripts, the pin files per stack, `STATEMENT.md`, `PACKAGES.md`, `samples/`, `runner/`) and re-exports
   `excludedDirs` from `spikes/minigit-bench/lib/score-deps.mjs:10` (one copy of the folder list, not two). `workspace-quick.mjs`,
   `copy-quick.mjs` and `spikes/lang-bench/test/templates.test.mjs:92-108` import it. It is its own file so the contract's
   identity does not move when the statement's sentences change. `templates/stack-files.json` and `lib/push-visible.mjs:9`
   are other lists and stay.
2. **The `.npmrc` keys.** `NPMRC_WORKSPACE` (`spikes/lang-bench/lib/workspace-build.mjs:71`) gains `ignore-scripts=true` and
   `save-exact=true`; the three `templates/node/*/.npmrc` follow; `test/templates.test.mjs:155` follows the constant.
3. **The `build.sh` comments**, in the same edit, so the templates' identity moves once: C#, "any folder named bin, obj or
   node_modules, in any letter case" (F1.S1 (c)); Node, the copy drops any folder named `node_modules` and `npm ci` remakes the
   root one from the lock. A comment line only; the build lines do not change.
4. **The identity.** `contractIdentity()`: a sha256 over sorted `path<TAB>sha256` lines of `copy-quick.mjs`, `build-quick.mjs`,
   `frozen-set.mjs`, `spikes/lang-bench/lib/workspace-box.mjs`, `spikes/lang-bench/lib/cache-offline.mjs` (it holds
   `NPM_OFFLINE_ENV`), every file under `spikes/lang-bench/templates/`, and the tree hashes of both registered snapshots
   (`inputs/cache-snapshots-lang.json`). The scoring puts it into `score.json`; `record.json` carries it as
   `explore.contractIdentity`, read from the score, so a record finished later still names the identity it was scored under;
   `REQUIRED_PATHS` and `SCORED_PATHS` (`records.mjs:69-77`) gain it. F3.S1's guard reads it later.
5. **`deviations`.** `explore.deviations = known({ count, paths })`, from the agent half of `splitFrozenChanges`
   (`copy-quick.mjs:41`), beside `explore.accepted`; `explore.frozenChanges` stays (D34). The note's table shows it beside
   `accepted`. Nothing reads it into `acceptedOf`. D65's lexical taxonomy is not built (D74).
6. **Template drift.** The series records the templates' identity at `init`, as it records the runner's pin; when the two
   differ, `splitFrozenChanges` puts a change to a template file in `harness`, as it does for `runner/` today
   (`harness-pin.mjs:16`). This matters for F1.S2's measurement on a quick-run tree.
7. **Docs.** `research/module_lang_explore.md`: the contract and the one engine (the parent's done-when for F1);
   `research/module_lang_bench.md`: the frozen list and the build line cite the one module; `research/module_tests.md`.

**Done when:** the parent's acceptance above, T-L62 and T-L63 green in the sandbox runs with nothing skipped; §7's checks for
PRs 7 and 8.

## 7. The order of the pull requests

One small pull request per item or group of items, built in parallel where the files are disjoint, **merged one at a time**.
Every pull request updates `research/module_tests.md`, so each merges `main` before its last whole run: D68's run is about the
head sha that merges, and a merge of `main` after it makes a new head.

The four merge conditions (parent *Merge conditions*): **(1)** the gates on harness code: a coai plan round at `proceed` before
the code (for code already written, the round reads its brief as the scope), then a coai code round and an Opus review on the
committed branch, every finding resolved; **(2)** D68's whole runs of every suite touched, plain and with
`BENCH_REQUIRE_SANDBOX=1`, every test passed and nothing skipped, and CI's required checks present and passing at the head sha;
**(3)** D41: the line scan before every merge, and `leakcheck` when a spec, a sample, a package, a document under
`research/lang-v2/` or an agent-visible file (the runner, the templates, `STATEMENT.md`, `PACKAGES.md`) is touched;
**(4)** no Stage 2 code. `plan-lifecycle.mjs` clean on every one.

| # | Items | Branch | What it runs before it merges | Which conditions bite |
|---|---|---|---|---|
| 1 | this document, with (a) recorded; its `todo/README.md` row | `docs/lang-v2-f1-stories` | line scan; `plan-lifecycle`; CI; an Opus reading by the team lead | (2) CI only, (3) line scan, (4) |
| 2 | F1.S1 (e), FORMAT's pins merged forward | `feat/lang-v2-format-amend-pins` | the merge of `main` and step 2's re-read; the gates; lang-bench and lang-explore whole runs, both ways, with `LANG_V2_TASK_REPO` at `4ddb8f80`; the three `--check` tools; `push-check --self`; `leakcheck`; line scan | (1), (2), (3) with `leakcheck`: it moves FORMAT's pins, from which every workspace's `runner/GRAMMAR.md` is cut, and changes the synthetic spec fixtures; (4) |
| 3 | F1.S1 (c) with carry items 2 to 4, and (g) | `docs/lang-v2-f1s1-case-blind` | the gates (a test and two comment lines in harness files); lang-bench whole runs, both ways; line scan | (1), (2), (3) line scan only: no agent-visible file moves (the `build.sh` line waits for PR 8); (4) |
| 4 | F1.S1 (b), the classifier and the recompute | `fix/lang-v2-f1s1-build-test` | the gates; minigit-bench and lang-explore whole runs, both ways; `repartition` over the six series; `leakcheck`; line scan | (1), (2), (3) with `leakcheck`: it adds lines to `research/lang-v2/`; (4) |
| 5 | F1.S1 (d), the held-out reader and the leak check's link | `fix/lang-v2-f1s1-heldout-links` | the gates; lang-explore whole runs, both ways; `leakcheck` on the real private side; line scan | (1), (2), (3): the condition asks only for the line scan, and `leakcheck` runs too because the pull request changes what it reads; (4) |
| 6 | F1.S1 (f), the rest of T-L64: the runner's comments, the push check's scan, the `ast` proof, the re-pin | `fix/lang-v2-f1s1-runner-text` | the gates; lang-bench whole runs, both ways, with `LANG_V2_TASK_REPO` at `4ddb8f80`; lang-explore whole runs (its workspaces copy the runner); the `ast` proof against the merge base; the probe re-run; the three `--check` tools; `push-check --self`; `leakcheck`; line scan | (1), (2), (3) with `leakcheck`: the runner is agent-visible; (4) |
| — | F1.S2, as its spec says | its own | as its spec says; at least (1), (2), and (3) with `leakcheck`, since the statement's memory sentence is agent-visible | all four |
| 7 | F1.S3 (a), the offered set | `feat/lang-v2-f1s3-offered-set` | the gates; the snapshot built under the gate lock; the matrix written; lang-bench whole runs, both ways; lang-explore whole runs (its `PACKAGES.md` is rendered from these inputs); the three `--check` tools; `leakcheck`; line scan | (1), (2), (3) with `leakcheck`: `PACKAGES.md` changes; (4) |
| 8 | F1.S3 (b), the contract | `feat/lang-v2-f1s3-contract` | the gates; lang-bench and lang-explore whole runs, both ways; `leakcheck`; line scan | (1), (2), (3) with `leakcheck`: the templates change; (4) |

**Order and why.** PR 1 first, so every brief cites a merged document. PR 2 next, because F1.S2's FORMAT amendment moves the
same pins once more and must start from them (parent F1.S1 (e)). PRs 3, 4 and 5 are disjoint in code and are built in
parallel, then merged in that order (3 is the smallest). PR 6 follows PR 2 (both move `inputs/specs.json`, through its tool) and
closes F1.S1: F1.S1 is done only when PR 6 has merged. F1.S2 starts after PR 2 and before or beside PRs 3 to 6; it rebases
nothing on the runner, but if its own pull request is ready before PR 6, PR 6 merges first, so the runner's identity moves before
F1.S2's records name it. Its first measurement wants PR 8's template-drift rule, or it records that the tree it measures
predates the template edit. PR 7 before PR 8. F1 is done when its done-when (§1) holds on `main`.

## 8. The model per role

| Role | Model | In F1 |
|---|---|---|
| Stories, scenarios and briefs | opus | this document, for F1.S1 and F1.S3 |
| Cornerstone spec | fable | F1.S2's scenarios and brief (`~/rsd/lang-v2/briefs/f1s2-spec.md`), copied in by the team lead; Opus stands in only if Fable refuses on a limit, and the summary says so |
| Coder | sonnet | every pull request's code and tests, and the fixes after review |
| Small edit | main session or sonnet | one file, at most 50 changed lines, bounded before the first edit: the dated line of (g) qualifies; (c) spans five files and does not; a Sonnet coder is always correct |
| Reviewer and judge | opus | the review of every pull request, beside the coai code round; the verdict on whether a finding changes a story |
| Scout | sonnet | evidence only, such as the v1 run-maps for (b) or a stream read for the recompute |

The cornerstone ledger does not change: the parent's line reads *Cornerstones: 2 of 2 — F1.S2, F2.S1*, and this document marks
only F1.S2.

## 9. Growth

- **The recomputed partitions** (F1.S1 (b)): one result file per run whose inputs changed, a few kilobytes each, in
  `<series>/partition-recomputed/` of the six quick-run series; a run over unchanged inputs writes nothing. Kept for ever,
  append only (D34); each result has its own timestamped, generation-named file and is never edited. An interrupted run leaves
  only an owned temporary file, which no reader takes for a result and the next run removes once its owner is gone.
- **The C# snapshot** (F1.S3 (a)): one new folder beside the old one under `~/rsd/lang-v2/caches/`, about the old one's 174 MB
  plus the test host's packages. The old folder is pruned by `--prune-superseded` only after F3.S1's tag, so about 0.4 GB is held
  until then (632 GB free on 2026-11-07). A killed build is swept by the next one (`make-snapshots.mjs`: the owned work folders,
  then the staging folders).
- **The matrix builds** (F1.S3 (a)): one owned work folder per build, removed when the build ends; a killed run's folders are
  swept by the owner rules.
- F1.S2's growth (ten passes, retries and re-runs) is its spec's.

## 10. Open points found while writing, for the team lead

1. **F1.S1 (c) quotes a sentence that is not there.** The parent names "the S3 section's 'agree for the default compile items'
   sentence"; the module's sentence reads "so a build in place and a build on the copy agree" (`research/module_lang_bench.md:901`),
   and "agree for the default compile items" is the carry file's proposed wording. The brief above writes it.
2. **T-L64's text half is wider than #173's test.** F1.S1 (f) and T-L64's dated line call the comment-and-docstring half done;
   #173's test proves no plan id, but T-L64 also asks for no stack name in the runner's comments, the scan in the push check's
   workspace check, and the `ast` proof, and `rs_abort.py` still names the stacks in its comments. Either those clauses are
   recorded as not needed in Stage 1, with a reason, or a small item builds them. In doubt this is a change for the parent's
   ledger, so it is not decided here.
3. **No story moves the quick world's FORMAT pins.** Stage 1 scores with lang-explore's engine (D70), whose workspaces take
   `runner/GRAMMAR.md` from the FORMAT pinned per task in `spikes/lang-explore/inputs/explore.json` (the drafts' `v2.1`), not from
   lang-bench's pin that F1.S1 (e) moves. F2.S1 or F2.S2 presumably moves them with the fixed specs; the parent names neither.
4. **The pre-registration tag pattern.** `lib/cache-store.mjs:205` reads `prereg/lang-v2-A-*`, which Stage 1's tag does not
   match: after F3.S1 a supersede would pass its guard and the old snapshot would never be pruned. F1.S3 (a) fixes it, as a fix
   that only makes the code meet the plan as written.
5. **(b) moves v1's harness identity**, because the classifier lives in a folder of v1's `harnessIdentity`. The plan does not
   say so; the brief makes the pull request check v1's open series.
6. **(d)'s wording.** The parent says the reader refuses "any entry that is not a regular file or a directory"; `cases/` is flat,
   so a folder in it is refused too (today it throws). The sweep also found that `leakcheck` skips a linked file in silence; the
   brief puts that into (d) as the same class, so D41's check cannot be blinded by a link.
7. **(g) widens the leftovers plan's S5** past its stated scope (`spikes/` only) to a C# test; that plan has no ledger, and the
   team lead decides whether its owner counts this as a change there.
8. **`todo/README.md`'s row for the parent** still reads "still stopped for re-review", though the epoch 9 line is written.

**The team lead's resolutions, 2026-11-07.** (1) The quoted wording of (c) is the carry file's; the doc line (c) writes may use it. (2) T-L64's rest is built in (f) of the parent, now worded so: no stack name in an agent-visible runner comment, the scan inside the push check, its `ast` proof. It satisfies the plan as written, so it is not a change. (3) lang-explore's own FORMAT pins in `inputs/explore.json` move to the amended FORMAT in (e) with the bench's pin, and once more with F1.S2's memory amendment; the one engine must not read a draft FORMAT. (4) `cache-store.mjs`'s tag pattern is fixed in F1.S3 (a), as written above. (5) (b) moves v1's `harnessIdentity`, because `classify.mjs` is hashed: (b)'s PR says so and re-pins what reads it. (6) (d) refuses a folder too, and `leakcheck` names a linked file it skips instead of skipping it silently. (7) (g) stays as written: one dated line in the leftovers plan; its owner decides. (8) `todo/README.md`'s row is updated in this commit. The Fable spec's open point 1, the references of task 06, is change 9.1 in the parent; its points 2 to 7 are the spec's own choices and stand.

## 11. Definition of Done (F1)

- [ ] F1.S1: (a) recorded here; (b) to (g) merged, (f) last of them (PR 6), each with its red recorded where it has one; a
      complete recomputed partition result exists for each of the six series; `pin-runner.mjs --check` and `specs-record.mjs --check` exit 0 on `main`.
- [ ] F1.S2: merged as its spec says (the parent's acceptance for it).
- [ ] F1.S3: the frozen list has one source; the Node `.npmrc` holds `ignore-scripts=true` and `save-exact=true`; every
      `record.json` carries the contract's identity and `deviations` as count and paths beside `accepted`; `Mvc.Testing` is in
      the registered C# snapshot; `PACKAGES.md` is derived from the matrix; T-L62 and T-L63 green.
- [ ] The parent's F1 done-when (§1) holds on `main`, and `research/module_lang_explore.md` describes the one engine.
- [ ] Every pull request met its row of §7; every failure on the way is a dated line in the leftovers plan's S5.
- [ ] No held-out content, case id or trap name in any public text: `leakcheck` and the line scan clean where §7 asks.
- [ ] `plan-lifecycle.mjs` is clean; this document's status line says what is done, and any change found on the way is an entry
      in the parent, never a ledger here.
