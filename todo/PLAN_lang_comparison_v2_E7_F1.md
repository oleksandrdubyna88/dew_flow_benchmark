# PLAN — language comparison v2, F1 stories: the harness and the verdict — the carry-ins, repeated scoring with the one memory value, the submission contract and the offered set

> Status: **plan only, 2026-11-07: F1's stories document, written when F1 starts (the parent's epoch 9 is open). F1.S1 (a) and (f) are recorded as done here; F1.S1 (b) to (e) and (g), F1.S2 and F1.S3 are open. F1.S2 is a cornerstone: its spec is Fable's and is not yet copied in.**
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
is `intermittently incorrect` under F1.S2 on the scoring CPUs, and both references of task 06 pass 10 of 10 under both
profiles at the registered limit; `build_test` is not zero for a C# session of a retained stream; the memory figure has one
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
| (f) | no plan id in an agent-visible runner file | **done for plan ids**: #173 `00d88233` | none; recorded here (PR 1) |
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
  bytes; an output file that exists already is refused, never overwritten);
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
   It writes one new file per series, `<series>/partition-recomputed-<yyyy-mm-dd>.json`: per rid the stack, the old categories
   (read from `record.json`), the new categories, and whether the new partition sums to the wall. It writes the file under a
   temporary name and renames it, so an interrupted run leaves no partial file, and it writes nothing else. Run
   it over the six series of `~/rsd/lang-v2/briefs/quick-series.txt`; the files stay in the private area, outside git.
4. **Docs.** `research/lang-v2/EXPLORE_quick_2026-11-06_analysis.md` gets one dated line under its `build_test` point (`:50`)
   and one under "Fix the classifier" (`:58`): done, with the per-stack count of sessions and the summed `build_test` seconds
   from the new files (counts and times only). `research/module_lang_explore.md` names the verb.

**Done when:** the three classifier tests and the recompute tests went red, green, red; the six files exist; at least one C#
session of a retained stream has `build_test` above zero in them (the parent's done-when); every recomputed partition sums to
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

### (f) no plan id in an agent-visible runner file — done for plan ids

**Proof.** #173 (`00d88233`) removed the plan ids from the agent-visible runner files and added
`spikes/lang-explore/test/quick-agent-visible.test.mjs`:
`No_file_an_agent_is_given_names_a_plan_item_a_decision_a_story_or_a_review_finding` and its companion
`The_pattern_finds_the_ids_it_is_meant_to_find_and_leaves_ordinary_words_and_codes_alone`, over `lang-bench/runner`,
`lang-bench/templates` and `lang-explore/runner`; 2 of 2 pass on `main` (2026-11-07). **What it does not show** of T-L64's
text half: no stack name in the runner's comments, the scan inside the push check's workspace check, and the `ast` proof (§2:
`rs_abort.py` still names the stacks in its comments). Whether those clauses still hold in Stage 1 is the team lead's call
(§10, item 2); this document records the plan-id half as done and nothing more.

### (g) the wall-clock C# test goes to the leftovers plan

**Brief.** One dated line in [PLAN_bench_test_leftovers_followups.md](PLAN_bench_test_leftovers_followups.md) S5, in that
section's dated form: `The_wait_for_the_script_says_so_first_and_ends_at_the_callers_cancellation_with_the_child_stopped`
(`tests/Bench.Tests/ModelArms/WslHostAwakeTests.cs:349`) waits in `Eventually` (`:357`) on a 15 s wall clock (`Patience`,
`:24`); it failed once on a CI runner at 15.018 s on PR #173, which changes no C# file; a timing dependency, margin or race not
decided. The line says that it widens S5 past that plan's stated scope (`spikes/` only). Nothing is built here.
**Done when:** the line is on `main`.

## F1.S2 — repeated scoring and the one memory value · cornerstone, `model: fable`

Cornerstone: the spec (scenarios and brief) is written by Fable, at `~/rsd/lang-v2/briefs/f1s2-spec.md`, and copied in here by the team lead.

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
- `The_unsupported_sentence_of_PACKAGES_md_is_derived_from_the_matrix_and_a_typed_sentence_fails`, and
  `PACKAGES_md_is_byte_equal_across_the_kinds_of_a_stack`.
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
   `lib/packages-md.mjs` reads the matrix for its sentence on unsupported combinations; nothing in it is typed by hand.
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
PRs 6 and 7.

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
| 1 | this document, with (a) and (f) recorded; its `todo/README.md` row | `docs/lang-v2-f1-stories` | line scan; `plan-lifecycle`; CI; an Opus reading by the team lead | (2) CI only, (3) line scan, (4) |
| 2 | F1.S1 (e), FORMAT's pins merged forward | `feat/lang-v2-format-amend-pins` | the merge of `main` and step 2's re-read; the gates; lang-bench and lang-explore whole runs, both ways, with `LANG_V2_TASK_REPO` at `4ddb8f80`; the three `--check` tools; `push-check --self`; `leakcheck`; line scan | (1), (2), (3) with `leakcheck`: it moves FORMAT's pins, from which every workspace's `runner/GRAMMAR.md` is cut, and changes the synthetic spec fixtures; (4) |
| 3 | F1.S1 (c) with carry items 2 to 4, and (g) | `docs/lang-v2-f1s1-case-blind` | the gates (a test and two comment lines in harness files); lang-bench whole runs, both ways; line scan | (1), (2), (3) line scan only: no agent-visible file moves (the `build.sh` line waits for PR 7); (4) |
| 4 | F1.S1 (b), the classifier and the recompute | `fix/lang-v2-f1s1-build-test` | the gates; minigit-bench and lang-explore whole runs, both ways; `repartition` over the six series; `leakcheck`; line scan | (1), (2), (3) with `leakcheck`: it adds lines to `research/lang-v2/`; (4) |
| 5 | F1.S1 (d), the held-out reader and the leak check's link | `fix/lang-v2-f1s1-heldout-links` | the gates; lang-explore whole runs, both ways; `leakcheck` on the real private side; line scan | (1), (2), (3): the condition asks only for the line scan, and `leakcheck` runs too because the pull request changes what it reads; (4) |
| — | F1.S2, as its spec says | its own | as its spec says; at least (1), (2), and (3) with `leakcheck`, since the statement's memory sentence is agent-visible | all four |
| 6 | F1.S3 (a), the offered set | `feat/lang-v2-f1s3-offered-set` | the gates; the snapshot built under the gate lock; the matrix written; lang-bench whole runs, both ways; lang-explore whole runs (its `PACKAGES.md` is rendered from these inputs); the three `--check` tools; `leakcheck`; line scan | (1), (2), (3) with `leakcheck`: `PACKAGES.md` changes; (4) |
| 7 | F1.S3 (b), the contract | `feat/lang-v2-f1s3-contract` | the gates; lang-bench and lang-explore whole runs, both ways; `leakcheck`; line scan | (1), (2), (3) with `leakcheck`: the templates change; (4) |

**Order and why.** PR 1 first, so every brief cites a merged document. PR 2 next, because F1.S2's FORMAT amendment moves the
same pins once more and must start from them (parent F1.S1 (e)). PRs 3, 4 and 5 are disjoint in code and are built in
parallel, then merged in that order (3 is the smallest). F1.S2 starts after PR 2 and before or beside PRs 3 to 5; its first
measurement wants PR 7's template-drift rule, or it records that the tree it measures predates the template edit. PR 6 before
PR 7. F1 is done when its done-when (§1) holds on `main`.

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

- **The recomputed partitions** (F1.S1 (b)): one file per series, a few kilobytes each, six files under `~/rsd/lang-v2/explore/`.
  Kept for ever, append only (D34); a later recompute writes a new dated file. An interrupted run leaves no partial file: it
  writes to a temporary name and renames.
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

## 11. Definition of Done (F1)

- [ ] F1.S1: (a) and (f) recorded here; (b), (c), (d), (e) and (g) merged, each with its red recorded where it has one; the six
      recomputed partition files exist; `pin-runner.mjs --check` and `specs-record.mjs --check` exit 0 on `main`.
- [ ] F1.S2: merged as its spec says (the parent's acceptance for it).
- [ ] F1.S3: the frozen list has one source; the Node `.npmrc` holds `ignore-scripts=true` and `save-exact=true`; every
      `record.json` carries the contract's identity and `deviations` as count and paths beside `accepted`; `Mvc.Testing` is in
      the registered C# snapshot; `PACKAGES.md` is derived from the matrix; T-L62 and T-L63 green.
- [ ] The parent's F1 done-when (§1) holds on `main`, and `research/module_lang_explore.md` describes the one engine.
- [ ] Every pull request met its row of §7; every failure on the way is a dated line in the leftovers plan's S5.
- [ ] No held-out content, case id or trap name in any public text: `leakcheck` and the line scan clean where §7 asks.
- [ ] `plan-lifecycle.mjs` is clean; this document's status line says what is done, and any change found on the way is an entry
      in the parent, never a ledger here.
