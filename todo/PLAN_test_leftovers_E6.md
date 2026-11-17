# PLAN — Test leftovers, E6: benchmark's tests name, own, remove and collect what they make

> Status: **amended 2026-11-17 for the parent's epoch 11** (its § *Epoch 11*, the re-review after entry 10.2: S4's sites
> take a disposition by lifetime, and the M2 and M3 pilot scaffolds write what must outlive the run into the folder the
> required `BENCH_PILOT_OUT` names, unprefixed, never the temp root (row E6-l, new); row E6-i reads both guards with
> counts; S4's *Files* name the model-arms docs; D6 for the probe runner's path; S5's shape G bounded; S6 reads both
> guards, with named *Files* and a start sweep; S7's *Files*; the plan records on every story's line; claims C6, C8 and
> C10 observed in part; the `/tmp/.git` todo in § 12; after the parent's re-review consult, the feasibility claims
> observed before epoch 11 opens: D6 revised to a three-way home for the fake CLI, row E6-l's C# half, S5's shape-G
> classification; S6's Python run blocked until its brief names a sandbox; the Definition of Done's documented exemption
> for the two scaffolds listed in § 11). S4 not started (its brief is local). **Earlier: progress 2026-11-17: S3 built and green on hosted CI (PR #219; brief `research/PLAN_test_leftovers_E6_S3_brief.md`); F42-c merged (#218). Earlier: in progress, 2026-11-16: amended for the parent's epoch 10** (§ *Epoch 10* item 8.3: § 6 *Before S3*
> restated by row F42-c and item 4.1's named paths; S3's Files name `TheAdaptedCopyTests.cs`; claims C1–C10 each with a
> state and the gate it is observed before (§ 1, *The claims*); C4's `GetTempPath()` plants replaced by plants the
> `Source` kind reads, that spelling in `src/` and `tools/` `.cs` written as a limit of both guards; E6-f1's Node half
> observed). F42-c built on `feat/e6-copy-f42` (the copy re-synced to mcp `53f65130`). **Earlier, 2026-11-16: revised for the parent's epoch 9** (§ *Epoch 9* item 8.3: rows E6-d and E6-g
> restated, E6-f split into E6-f1 and E6-f2, a state on every row; S3 after F41-b and F42-c; S7's reuse plant replaced
> by E6-f2's fixture test). **Earlier, 2026-11-12: S1 merged (#205; E6-h: the held-out sweep lists `score-tmp` and removes nothing by
> age); S2 built (#207: the adapted copy, the start-of-run sweep, case 15, the audit; brief `research/PLAN_test_leftovers_E6_S2_brief.md`), merged as `84d4953e`.** Found by S1, not fixed (outside this document's text): `spikes/minigit-bench/lib/score-steps.mjs:57-73`
> `stepSweep` removes the scorer's own marked folders by marker age. Written new against the shared rules at `d23a2ea` (this
> repository's pin) and `dew_flow_mcp`'s merged C# helper; the 2026-10-16 draft of this file (`08a87430`) was read as
> material only. Stories of epic E6 of `dew_flow_conventions · todo/PLAN_test_leftovers_rollout.md` (§ *Epics*, E6;
> § *Epoch 8* item 3, rows E6-a to E6-h; § *Epoch 9* item 3 and § *Epoch 10* item 3, the rows they restate). Scope: `tests/` (Bench.Tests, FakeCli, FakeCoai), `spikes/policy-ab-quick/test/`,
> `spikes/minigit-bench/lib/heldout-run.mjs`, two product seams (`HarvestGates`, `SolutionSignals`), one CI job, the docs.

**Cited as paths, never links** (the shared `common/planning-docs.md` § *Across repositories*): the parent
`dew_flow_conventions · todo/PLAN_test_leftovers_rollout.md`; the rule `.agents/conventions/common/test-leftovers.md` and its
detail, janitor and baseline companions (mounted here); the C# template `dew_flow_mcp · tests/Mcp.Tests/Leftovers/`; the
two adapted copies `dew_flow_connect_other_ais · src_mcp/tests_leftovers/` and
`dew_flow_creds_for_devs · src_service_defaults/tests/Support/Leftovers/`. *Rule §n* is a section of the rule's detail.

**How to read the evidence.** **[M]** is measured for this document, read-only, at benchmark `7d795024` (rules `d23a2ea`,
mcp `21553ce`), with the command in § *How to repeat the measurements*. **[D]** is taken from a named document. A
statement with neither mark is a decision of this document. Facts were collected by two Sonnet scouts and opened again
by the Opus author where a decision rests on them.

**The epoch-9 revision (2026-11-16).** The parent stopped at its entry 8.2: the rule's own `container-scoped` marker is
refused by both guards, and no guard reads `.WithReuse(` (the Node half **[M]** below; the C# half read from code, no
reuse spelling in the template's guard files at mcp `53f65130`, **[R]**). Its re-review (§ *Epoch 9*) is the baseline epoch 9 is approved
on, and this revision follows it (item 8.3; the judge's cut row lists the lines of #103 it replaces). It is not a change
of the parent and not an entry anywhere: this document keeps no ledger (`common/plan-change-control.md`). Rows the parent
does not restate keep their checks and their evidence. A restated row keeps every check its earlier text had and adds to
it; the one check the parent supersedes, E6-d's proof (3) "no `.WithReuse(`", which named a guard check that does not
exist, is replaced by E6-f2 (3). Measurements added by this revision are marked **[M]** with `46e27108` beside them: benchmark `46e27108`, rules pin
`4c9a684`, read-only, commands in § *How to repeat the measurements*.

Revised 2026-11-16; plan gate `215fc3a2` proceed (one finding accepted: D9's place; three rejected with reasons, two
carried to S3's brief: the effective Ryuk check at fixture start, and a run that settles whether Testcontainers 4.16.0
has any configuration path to reuse); an independent Opus review's eight points applied.

**The epoch-10 amendment (2026-11-16).** The parent stopped again at its entry 9.2; its entry 9.1 had found that F42-c
cannot keep this document's § 6 boundary "No benchmark-own file changes". Its re-review (§ *Epoch 10*) is the baseline
epoch 10 is approved on, and this amendment does what its item 8.3 asks for this document; the re-review record's cut
row for #215 lists the lines it corrects (C1, C2, C4, and C3, C5–C10 written without a state). It is not a change of the
parent and not an entry anywhere: this document keeps no ledger. Every check written here before stays; a restated row or
story keeps every check its earlier text had and adds to it. New mark, as the parent's § *Epoch 10*: **[R]** is a
statement read from code and NOT run; a claim resting on **[R]** is never **observed**, and it is written open-ended
("red or not, the plant says which"), so its observation fills in the result and never contradicts the text (the
parent's item 8.1). Measurements added by this amendment are marked **[M]** with `83f166d6` beside them: benchmark
`83f166d6` (rules pin `b1746499`), mcp `21553ce` and `53f65130`, read-only, commands in § *How to repeat the
measurements*. No .NET build and no Docker ran for it, so every statement about the C# copy's verdict is **[R]** or
**[D]**.

**The epoch-11 amendment (2026-11-17).** The parent stopped at its entry 10.2: S4's two pilot scaffolds make output that
must outlive the test, and S4's two dispositions for its legacy literals (the helper, or `~`) do not cover it. Its
re-review (§ *Epoch 11*) is the baseline epoch 11 is approved on, and this amendment does what its item 8.3 asks for this
document: S4 as its item 4.2, rows E6-i (restated) and E6-l (new), and its items E11-3 to E11-11 where they fall here.
It is not a change of the parent and not an entry anywhere: this document keeps no ledger. Every check written here
before stays; a restated row or story keeps every check its earlier text had and adds to it, and the replaced text
stays beside it as *Was (epoch 10):*. Measurements added by this amendment are marked **[M]** with `b348bb33` beside
them: benchmark `b348bb33` (the mount at `b1746499`, the copy at mcp `53f65130`), read-only. ONE C# run: the copied
repository guard test (`--explicit only --filter-class "*LeftoversRepositoryGuardTests"`) from an existing Release build
of a tree equal to `b348bb33`, 173 findings, class for class F42-c's O10; no build and no Docker ran for it. Every
other statement about the C# copy's verdict on a NEW line stays **[R]**. **The lifetime rule** (the parent's § *Epoch 11*
item 8.1): every site a story disposes of is classified by its lifetime before that story's plan gate — the run (the
helper); inside an owned folder (a plain name inside); a name never made or a path only read; must OUTLIVE the run (an
operator-named folder, outside the testsToDelete family, never the temp root); a test about the old name (`~`). A site
that fits none stops before the gate.

**Each row has ONE state** (the parent's § *Epoch 9* item 8.1). **observed**: the row's own check ran on a candidate that
must pass AND on a counterexample that must fail, at this repository's pins. **proposed**: the check is written, not yet
run; it is never approved as measured. **blocked**: the check needs something this document does not have; the row names
what. Where a row's check is a test or a guard run, a reading of today's tree is the starting point and is written as
such, never as "observed".

## 1. The conditions — rows E6-a to E6-l (E6-l added by the epoch-11 amendment), each with its state (the parent's § *Epoch 8* item 3, as § *Epoch 9* item 3 restates it)

| # | Condition | Implementation (file) | Invocations | Executable check | Prepares / does not prove | Read or measured at `7d795024` (the starting point) | State (epoch 10) |
|---|---|---|---|---|---|---|---|
| E6-a | the boundary on both sides | `todo/PLAN_bench_test_leftovers_followups.md:63` and § 9 here | — | the row is on `main` in both | — | **[M]** merged, #177 `562cb4bd`. Met | **observed**: the check "the row is on `main` in both" ran on a candidate and a counterexample, **[M]** 2026-11-16. Candidate, `origin/main`: `git show origin/main:todo/PLAN_bench_test_leftovers_followups.md \| grep -c "E6 does not build these fixes a second time"` → 1, and this document's § 9 row "The fixes of F1 and F2 in" on `origin/main` → 1. Counterexample, the commit before #177 (`git log --format=%h -1 562cb4bd^` → `9336a774`): the same count of the phrase → 0 (at `562cb4bd` → 1) |
| E6-b | a worktree a test makes is owned, and the guard's boundary is stated | the worktree lives INSIDE an owned folder with the repository it belongs to (rule §1, last item): the product seams of D4 take a root; a `worktree add` a TEST writes by hand stays in its owned folder and carries one inline `raw-worktree` exemption naming that folder | `Bench.Tests`; the guards | the C# guard and the Node guard on the head; one planted test-file `worktree add` without the marker is red | The guards read the spelling, never the folder | **[M]** a compliant `git worktree add --detach --lock --reason "$RECORD" testsToDelete-u1001-bench-x HEAD` is `raw-worktree` in a `.sh`, in a `.cs` under `tests/` (Node guard, `--profile node` only) and in a `.test.mjs`; the comment `testsToDelete-exempt: raw-worktree — <reason>` on the line above clears each. **Not read:** the same line in a plain `tools/*.mjs` (not a test file). **[R]** (read from code, not run; this cell said **[M]** before the epoch-10 amendment) the template's C# guard matches `"worktree", "add"` and the words `worktree add` (mcp `LeftoversGuard.cs:91` at `21553ce` and at `53f65130`), only outside `src/` (`:199-203` at `21553ce`, `:204-209` at `53f65130`): claim C8 | **proposed**: the Node guard's half (marker and no marker) was run at `d23a2ea`; the row's check, both guards on the head with a planted line red, runs in S4, S5 and S7. Claim C8 (the C# half, "only outside `src/`"), **proposed**: observed by S4's and S5's plants through the copy at `53f65130` (after F42-c), before S4's plan gate; red or not, each plant says which |
| E6-c | a killed child's worktree is collected by a NAMED implementation | the copied C# sweep's folder pass, on the owned folder that holds the clone and its worktree (sweep case 15, § S2) | the next run's start | sweep case 15: a dead-claimed owned folder with `repo/` and a linked `scratch/<id>/` is removed whole | The template has no worktree pass (mcp `LeftoversSweep.cs:3`); a worktree whose clone is OUTSIDE the folder is not seen at all. Windows: N1 (read-only git objects), which does not arise here while benchmark has no Windows leg | **[M] by reading, not run:** `HasGitFile(dir)` (`LeftoversSweep.cs:470-480`) tests only the candidate's OWN `.git` as a regular file, so `:171-175` (`other clone`) does not fire for the owned folder; the folder is quarantined and removed by `Removal.Quietly` → `Directory.Delete(recursive)` (`Removal.cs:40-54`), with the clone's `.git/worktrees/<id>` inside it. **To observe** in S2: case 15 RED first, then green | **observed** by S2 on Linux (#207, `LeftoversSweepCase15Tests.cs`: RED by hand, then green, **[D]** its commit message; not re-run by this revision). Windows: not applicable while benchmark has no Windows leg (`git grep -i windows -- .github/workflows` → nothing, **[M]** `46e27108`); the case 15 tests skip on Windows; N1 is F42's, for the template. Since F42-c, the copied `Removal` clears read-only files below a folder before the delete on Windows (N1, fixed in mcp F42 and observed there on `windows-latest`, run `43928795620`; not observed here, no Windows leg); the *Limits* cell's N1 sentence describes the copy before F42-c |
| E6-d | the fixture's container: three separate proofs (restated by the parent's § *Epoch 9* item 3) | `tests/Bench.Tests/Infrastructure/PostgresFixture.cs` | `Bench.Tests` on `ubuntu-latest` | (1) Ryuk on: a child test process starts the fixture; the container exists by its captured id BEFORE the child is killed, and the Docker query that says so succeeded; after the kill, within the reaper's window, the same query succeeds and finds nothing; (2) the owner label `testsToDelete.owner` on the builder, read by the container pass; (3) = E6-f2 (3): reuse proved off at run time. And no `TESTCONTAINERS_RYUK_DISABLED` in any spelling: a benchmark test over every `.cs` of the repository, red on a planted `Environment.SetEnvironmentVariable("TESTCONTAINERS_RYUK_DISABLED", "true")`, because the Node guard does not read that form (`[]`, **[M]** `46e27108`) and the C# copy is expected not to (**[R]**; claim C7), the parent's F43 | Ryuk collects by its session, not by owner; one test cannot stand for the other two | **[M]** the builder (`:27-33`) has `WithDatabase`, `WithUsername`, `WithPassword`, `WithCommand` only: no `WithName`, `WithLabel`, `WithReuse`, `WithCleanUp`; nothing in the tree sets `TESTCONTAINERS_RYUK_DISABLED` or a `testcontainers.properties`; `Testcontainers.PostgreSql` 4.16.0 (`Directory.Packages.props:41`). **To observe** in S3: the three proofs on `ubuntu-latest` (no `docker` CLI on the author's machine). **Corrected 2026-11-16:** proof (3) used to read "no `.WithReuse(`", a guard check that does not exist: **[M]** `46e27108`, the Node guard's `scanText` of `.WithReuse(true)` in a `.cs` is `[]`, and of `Environment.SetEnvironmentVariable("TESTCONTAINERS_RYUK_DISABLED", "true")` is `[]`; the C# copy's `RyukOff` wants `:` or `=` after the name (`tests/Bench.Tests/Leftovers/LeftoversGuard.cs:93`, read, not run; the same line at mcp `53f65130`, **[R]**) | **proposed**; hosted only: Docker runs in `build-test` on `ubuntu-latest`, and the fixture fails loudly without it (`PostgresFixture.cs:45-47`). Claim C7 (the C# half of "neither guard reads" the `SetEnvironmentVariable` form), **proposed**: expected not read, read or not to be observed through the copy at `53f65130` before S3's plan gate, so S3's own test (7) is known to be the only reader, or the second |
| E6-e | the copy and its limits | the adapted copy with a manifest (D2), F29 read and fixed by copy (D3), F31 stated by its true condition (D3) | — | the copy's diff check (`TheAdaptedCopyTests`, coai's shape) | — | **[M]** mcp's folder at `21553ce`: 51 files, 8566 lines; since `197cc76` (the commit both earlier copies pin) only 8 files changed, all the mount path. The template has no `SweepLock` (F29 open there); coai carries one as its own file | **observed** by S2 (#207: the check was committed red, 52 targets missing, then green with the copy, **[D]** its commit messages). It is run again by F42-c (`feat/e6-copy-f42`, § 5) on its final head; results on its PR |
| E6-f1 | the per-test databases carry the rule §6 (b) marker, and both guards accept it at benchmark's pins (E6-f, split by the parent's § *Epoch 9* item 3) | `PostgresFixture.cs`: `NewEmptyDatabaseAsync` (`:96`, `CREATE DATABASE` `:108`) makes them in the fixture's container; the `container-scoped` marker beside it, in the form of the parent's item 4.2 (`lives and dies with` or `live and die with`, then the owner) | `Bench.Tests` (the copied C# guard's scan of that file); the Node guard (`scanText` of that file; the repository count) | FILE-scoped, because benchmark's tree is not clean before S7 (the Node guard: 209 findings at `46e27108`; the copied C# repository test is `Explicit` until S7): no finding in `PostgresFixture.cs` from either guard, and the Node guard's count over the repository unchanged against the base. Counterexample: the same file with the reason's `live and die with` removed gives one `exemption` finding in each guard (the C# half expected, **[R]**, F42's case-for-case port of F41; claim C9). "Both guards clean on the head" stays S7's | the lifetime: E6-f2 | **[M]** no `DROP DATABASE` in the tree; 29 call sites in 16 test files; no marker, no name, no label. **Corrected 2026-11-16:** this cell used to say "**[M]** the Node guard knows the `container-scoped` class (`leftovers-check.mjs`, 8 lines)". Those lines are in the guard's fixtures and its rule test (`tools/fixtures/…`, `tools/test-leftovers-rule.test.mjs`), not in the guard. **[M]** `46e27108`: the marker in either spelling, and with the phrase missing, is one `exemption` finding ("an exemption names an unknown class `container-scoped`"); the C# copy's `Exemptible` lacks the class (`LeftoversGuardExemptions.cs:11`, read, not run) | **proposed** (S3), by the parent's § *Epoch 10* item 3: no longer blocked once F42-c merges, since F41-b is done (#216 `83f166d6`, the rules pin `b1746499`). The Node half is **observed** **[M]** `83f166d6`: `scanText('tests/Bench.Tests/Infrastructure/PostgresFixture.cs', …)` of the marker over `NewEmptyDatabaseAsync` → `[]` (candidate); the same marker without `live and die with` → one `exemption` (counterexample). The C# half (claim C9) waits for F42-c's copy and is observed through it before S3's plan gate. Until F42-c, no story writes the marker. *Was (epoch 9):* **blocked** on F41-b (the rules pin at F41's release) and F42-c (the copy re-synced to F42's commit) |
| E6-f2 | the fixture's container ends with the fixture, and reuse is never set (E6-f, split by the parent's § *Epoch 9* item 3) | benchmark's fixture tests (S3) | `Bench.Tests` on `ubuntu-latest` | (1) the container exists, by its captured id, BEFORE disposal, and the Docker query that says so succeeded; (2) after disposal the same query succeeds and finds nothing: a failed query is never "absent"; so every per-test database is gone with it; (3) reuse, proved at RUN time by (2): a reused container is not stopped by disposal, so (2) is red whenever reuse is in effect, whatever turned it on. RED, each: reuse enabled through the builder the fixture uses, and through configuration alone (`TESTCONTAINERS_REUSE_ENABLE=true`, or `testcontainers.reuse.enable=true` in the properties file the pinned version reads), with the fixture's source unchanged. A read of the built container's reuse setting (if the pinned API exposes it) or a source test may be ADDED, never instead of (2) | the databases are never dropped one by one, the proof is the container's end; other consumers' fixtures; a shared `testcontainers-reuse` class is a follow-up only when a second consumer needs one (the parent's item 4.2) | **[M] `46e27108`, by reading the package, not run:** the UTF-16 strings of `Testcontainers.dll` 4.16.0 (every target framework, from the local NuGet cache) hold `TESTCONTAINERS_RYUK_DISABLED` and `ryuk.disabled` but no reuse key (no `REUSE_ENABLE`, no `reuse.enable`); reuse appears only as the builder's `WithReuse` (its metadata) and in its error messages. So the pinned version may read no reuse configuration at all. S3's brief settles it by a run; if configuration cannot turn reuse on, that half's run is recorded as such and the parent is told (its decision, not this document's) | **proposed**; hosted only, as E6-d |
| E6-g | the guards read every directory E6 promises (restated by the parent's § *Epoch 9* item 3) | the copied C# guard (copied in S2; its repository test `Explicit` until S7) and the shipped Node guard as a CI step (`--profile node --token bench --identity "the measuring harness"`; S7, S2 added none) | `Bench.Tests`; CI | both green on the head (S7); a scan of nothing is red (exit 2; the C# `FilesScanned > 0`); one planted raw temp in `tools/` is red; and per directory of § 1's table *E6-g by directory*, a planted break-it (a raw temp, or where the row says so, another class such as `owner-blind`) in a file of a kind that directory's guard READS is red. *Was (epoch 9):* "a planted raw temp" per directory, which the `src/` row's second plant (`owner-blind`) is not. A promised directory whose files no guard reads is named there as a limit, with its file kinds | A spelling check; which plain `.mjs` files make temp paths: no guard reads them | **[M]** no guard today (`git grep -l 'LeftoversGuard\|leftovers-check' -- tests tools .github` → nothing). The Node guard: default profile 3 findings, `--profile node` 209. A planted `mktemp -d` in `tools/x.sh` is `raw-temp` (both profiles). **Not read** by either guard: `.cs` in `tools/model-arms/`, `hosts/`, `spikes/` (C# guard: only `tests/` and `src/`, **[R]** by reading `GuardFiles`, claim C3; Node: only `tests/` as text); a plain `tools/*.mjs`; `*.py` in the Node guard. **[M]** `46e27108`: the table below | **proposed**: S2 planted no directory for this row; the plants are S7's. Each C# cell of the table below is claim C3, observed before S7's plan gate |
| E6-h | the held-out runner's age removal of `score-tmp/*` is retired before any producer | `spikes/minigit-bench/lib/heldout-run.mjs` | the held-out tool's `sweep` command (`heldout-tool.mjs:105-112`), by hand only | its test: an old unclaimed folder under `score-tmp` is listed, not removed by age | — | **[M]** still there: `sweepLeftovers` `:93-113` removes every un-prefixed `score-tmp/<name>` older than 24 h (`:102-105`), no owner check; the test pins it (`heldout-run.test.mjs:154`: `removed: ['old-1']`) | **observed** by S1 (#205: the test changed to `removed: []`, red on the old code, then green, **[D]** its PR) |

Rows added by this document, read the same way:

| # | Condition | Read or measured at `7d795024` (the starting point) | State (epoch 10) |
|---|---|---|---|
| E6-i | no test helper makes a LEGACY name (rule §1) | **[M]** 20 `legacy-name` findings outside the other plan's spikes: 15 in `tests/Bench.Tests/ModelArms/` (`TempFolder.cs:10` `Directory.CreateTempSubdirectory("testsToDelete-modelarms-")`, 112 `new TempFolder()` in 26 files; tokens `modelarms`, `briefs`, `m2`, `m3`, `x` are not rows of the Tokens table), 5 in `spikes/policy-ab-quick/test/`. **Epoch 11, [M] `b348bb33`:** the 112 are sites, not spellings: 108 `new TempFolder()` in 25 files and 4 `TempFolder _temp = new()` fields (S4's brief N2). Read by BOTH guards (the parent's row E6-i, restated): under `ModelArms/` the Node guard (`--profile node`) has 49 findings (15 `legacy-name`, 14 `raw-temp`, 20 `raw-worktree`) of its 209, and the copied C# guard 82 (15 `legacy-name`, 16 `owner-blind` on the same lines and `M2PilotScaffold.cs:78`, 31 `raw-temp` of which 17 are `"/tmp"` literals, 20 `raw-worktree`) of its 173 | **proposed** (S4, S6). S4's check, against the base `b348bb33`: under `ModelArms/` no `legacy-name`, `raw-temp` or `owner-blind` finding and no `raw-worktree` but the 10 marked lines, in each guard: the Node guard 209 → **160** (default profile 3 → 3), the C# guard 173 → **91**; no finding added elsewhere; on a later base, that base's count less these. S6's: row E6-i's spike lines in both guards (S6 *Done when*) |
| E6-j | the CI job that runs `Bench.Tests` can read the mount | **[M]** `build-test` (`ci.yml:13-43`) checks out no submodule; the copied C# guard reads the Tokens table and starts the shipped `sweeper-scan.mjs` from the mount (read from the template, **[R]**; #207's CI ran the copy green with the mount, **[D]**, below). `contract` (`:68`) and `plans` (`:120`) do check it out | **proposed**: S2 added the line (`ci.yml:23` at `46e27108`) and #207's CI ran the copy green with it (**[D]**); the counterexample, the job without the line failing, was not recorded |
| E6-k | a test that makes a temp path removes it | **[M] by reading:** 8 creation lines are never removed — `Diagnostics/DailyRunFileSinkTests.cs:92`, `Engines/FilesystemEngineTests.cs:304`, `Engines/QlnEngineTests.cs:287`, `Authoring/VettingPassTests.cs:326`, `:339`, `Trace/TracePortTests.cs:166`, `:183`, `:268` | **proposed** (S5) |
| E6-l | output a test makes ON PURPOSE to outlive the run goes to an operator-named folder outside the testsToDelete family, never to the temp root (added 2026-11-17 by the parent's § *Epoch 11*, from its entry 10.2) | **[M]** `b348bb33`: two such tests, both `Explicit = true` (the suite never runs them): `M2PilotScaffold.cs:39-40` (`testsToDelete-m2-target-<id>`, `-root-<id>` under `Path.GetTempPath()`, "meant to outlive the test — the owner removes it after the pilot", `:11-12`) and `M3PilotScaffold.cs:34`, `:37`, `:105` (ten `testsToDelete-m3-<what>-<id>` paths and a claimed manifest, removed by the pilot's step 15). M2's validation probe (`:72-78`, also called by M3 at `:54`) is removed in `finally`: test lifetime, not this row's | **proposed** (S4). The check: (1) each scaffold, run once with `--explicit only` and `BENCH_PILOT_OUT` unset, fails before it makes anything with a message naming `BENCH_PILOT_OUT` (RED first: today each writes under the temp root); the same for a value that is empty, relative, not an existing folder or inside a git checkout. (2) Run once each with it set to a scratch folder outside every git checkout: every printed path is under it, named `bench-m2-<what>-<id>`, `bench-m3-<what>-<id>`, `bench-m3-manifest-<id>.json`; the temp root holds no entry of theirs after the run; the outputs then removed by exact path (M3 by its manifest, T4–T5). (3) Both guards on both files: no `raw-temp`, `legacy-name` or `owner-blind`, no exemption marker. The Node half of (3) **observed [M] `b348bb33`**: the planned lines `[]`; controls: a rename alone under `Path.GetTempPath()` → `raw-temp`, today's line → `raw-temp` and `legacy-name`. The C# half **observed** 2026-11-17 (the parent's § *Epoch 11* item 3.3, O-b: `LeftoversGuard.Scan` over private trees at `tests/Bench.Tests/ModelArms/M2PilotScaffold.cs`, consumer `b348bb33`, upstream mcp `53f65130`): each planned line `[]`; controls: under `GetTempPath()` → `raw-temp`, today's line → `legacy-name`, `owner-blind`, `raw-temp`, under `TempDir.Root` → `[]` (the blind spot this row forbids). *Was:* **[R]**, expected clean, observed through the copy before S4's plan gate |

**E6-g by directory** — which guard READS each directory E6 promises, by file kind. **[M]** `46e27108`: the Node guard by
`isScanned(path, 'node')` of the shipped guard at the pin `4c9a684`; the C# copy by reading `GuardFiles`
(`tests/Bench.Tests/Leftovers/LeftoversGuardFiles.cs:27-70`), not run: every C# cell below is **[R]**, claim C3,
**proposed**, each observed by a plant through the copy at `53f65130` (after F42-c) before S7's plan gate; red or not,
the plant says which, and a plant red in no guard becomes a limit of this table, never a break-it (the parent's item
8.1). The Node guard under `--profile node` reads
`*.test.*` and `*.spec.*` JavaScript and TypeScript files anywhere, every `.sh` and `.ps1`, the workflows, and every
non-binary file under the root folders `tests/`, `test/` and `scripts/`. It does NOT read a plain `.mjs` elsewhere
(`spikes/minigit-bench/lib/heldout-run.mjs`, `tools/x.mjs`, `spikes/policy-ab-quick/test/x.mjs`: false). The C# copy reads
`.cs` under `tests/` (as tests) and `src/` (as product code), every `.sh`, `.ps1` and `.py`, the workflows, and `.mjs`,
`.js`, `.cjs` only under `.github/scripts/` and `http/`; it never enters `.agents`, `.claude`, `bin`, `obj`,
`node_modules`.

| Directory (why E6 promises it) | File kinds there | Read by | Plant (S7), red in each guard that reads it | Limit: read by no guard |
|---|---|---|---|---|
| `tests/Bench.Tests`, `tests/FakeCli`, `tests/FakeCoai` (S2–S5) | `.cs` | both: the C# copy as tests; the Node guard as text | `Path.GetTempPath()` in a `.cs` | — |
| `src/` (the seams of D4, S5) | `.cs` | the C# copy, as product code (the `Source` kind) | a plant the `Source` kind reads (claim C4): a reference to `Owned.TempRoot` outside the helper's folder, expected one `raw-temp` ("… outside the helper's folder"), and the bare prefix word with no owner segment, expected one `owner-blind` (**[R]**, mcp `53f65130` `LeftoversGuard.cs:216-219`, `:297-301`); red or not, the plant says which. *Was:* "a `GetTempPath()` in the forms the product kind judges (the brief records which)", expected never red (the next column) | not read by the Node guard (`isScanned('src/X.cs', 'node')` → false, **[M]** `83f166d6`). `GetTempPath()` and the other raw temp spellings in a `src/` `.cs`: a limit of BOTH guards (C4): the `Source` kind reports no raw temp spelling (`HitsOutsideSource` returns nothing for it, mcp `53f65130` `LeftoversGuard.cs:204-207`, **[R]**; expected never red, observed with C4's plants before S7's plan gate) |
| `tools/model-arms/` (S7) | `.cs`, `.ps1`, `.csproj`, `.json`, `.md` | `.ps1`: both. `.cs`: neither today; the C# copy after S7's manifest operation, as the `Source` kind (claim C10) | `mktemp -d` in a `tools/` `.sh`; after S7, in a `tools/model-arms/` `.cs`, the plant the `Source` kind reads, as in `src/` (C4, C10). *Was:* "`GetTempPath()` in a `tools/model-arms/` `.cs` after S7", expected never red | `.cs` until S7 (`isScanned('tools/model-arms/X.cs', 'node')` → false, **[M]** `83f166d6`); after S7, `GetTempPath()` and the other raw temp spellings in a `tools/` `.cs`: a limit of both guards, as in `src/` (C4); `.csproj`, `.json`, `.md` |
| `spikes/policy-ab-quick/test/` (S6) | 14 `*.test.mjs`, one plain `fixtures/ctrlc-parent.mjs`, data (`.ndjson`, `.xml`) | `*.test.mjs`: the Node guard | `mkdtemp(` in a `*.test.mjs` | `fixtures/ctrlc-parent.mjs` (it makes no temp path today: no `tmpdir`, `mkdtemp`, `TMP`) |
| `spikes/minigit-bench/lib/heldout-run.mjs` (E6-h, S1) | a plain `.mjs` | neither; its test `test/heldout-run.test.mjs` by the Node guard | — | the file itself: S1's own test is its check |
| the rest of `spikes/` (D7, S6) | `*.test.mjs`, plain `.mjs`, `.sh`, `.py`, `.cs`, plain `.ts` | `*.test.*`: the Node guard. `.sh`: both. `.py`: the C# copy | `mktemp -d` in a `spikes/` `.sh` | plain `.mjs` (`lib/`, `capture/`; § 3's list); `.cs` (`spikes/EvalLibrarySpike/Program.cs`); the 9 plain `.ts` files (all `main.ts` fixtures under `spikes/lang-bench/test/` and `spikes/lang-explore/rehearsal/`; `git ls-files spikes \| grep -E '\.ts$' \| grep -v -E '\.test\.ts$\|\.spec\.ts$'` → 9; `isScanned` false for them, **[M]** 2026-11-16) |
| `research/local-model-coding/harness/` (D8, S7) | `.sh` (31), `.ps1` (3), plain `.mjs` (17), one `.js`, `.json`, `.md`, `.env` | `.sh`, `.ps1`: both | `d=$(mktemp -d)` in a `.sh` there: `scanText('research/…/harness/x.sh', 'd=$(mktemp -d)')` → `raw-temp` (**[M]** 2026-11-16); D8 marks its two lines | the 17 `.mjs` and 1 `.js` files there (`git ls-files research/local-model-coding/harness \| grep -cE '\.mjs$'` → 17, `'\.js$'` → 1; `isScanned` false; the C# copy reads `.mjs` and `.js` only under `.github/scripts/` and `http/`) |
| `.github/workflows/` (the CI step, S2, S7) | `.yml` | the C# copy (it reads workflows, above). The Node guard opens the files but reports no `mktemp -d` in a `run:` | `mktemp -d` in a workflow `run:`: the C# copy's reading is to be run by S7, red or not (the plant says which). Expected red, **[R]**: `.yml` is in `RawTempByExtension` (`LeftoversGuard.cs:84`, `:89`); claim C5, **proposed**, observed before S7's plan gate | `mktemp -d` in a workflow `run:`: the Node guard gives NO finding (**[M]** 2026-11-16: `scanText('.github/workflows/ci.yml', …)` of a `run: \|` block with `d=$(mktemp -d)` → `[]`; a `worktree add` there is reported). The step's own run is the check of the CI step itself |
| `http/README.md` (S7 rewrites `:32-33`) | `.md` | neither | — | the file: S7's rewrite is checked by its review, not a guard |

**[M]** `46e27108`, a correction to § 3: the C# copy (copied in S2) reads `.py` as a script (**[R]**, `GuardFiles`,
claim C3), and nine lines of `spikes/lang-bench/test/py/*.py` call `tempfile.mkdtemp(` (for example
`test_rs_kill.py:152`). Whether the copy's `mkdtemp(` pattern reports them there was not run: expected reported,
**[R]** (`\bmkdtemp(?:Sync)?\s*\(`, mcp `53f65130` `LeftoversGuard.cs:78`); red or not, the observation says which
(claim C6, **proposed**, observed before S7's plan gate; D7 says whose they are). S7 switches the repository test on and
meets them, under D7's boundary.

**The claims — every place this document states a copied tool's reach, a discovery boundary or a plant** (the parent's
§ *Epoch 10* item 3.2, C1–C10; added by the epoch-10 amendment). Each is observed before the plan gate named, which is
the gate of the story or brief that RELIES on it, and again when one of its inputs changes (a pin bump included). An
observation names six things (the parent's item 8.1): the consumer SHA (benchmark at that PR's base), the upstream pin
(mcp `53f65130`, the commit F42-c copies), the exact invocation, the fixture path, the expected diagnostic and the
OBSERVED result. The "without" half (the copy as copied, before the story's operation or site) runs in a scratch test
against the copy at `53f65130`, built as this repository builds it, `nice -n 19`, `-m:4`,
`-p:AppHostRuntimeIdentifier=linux-x64`; the "with" half is the story's own RED → GREEN. For the C# copy the invocation
is `LeftoversGuard.Scan(<private tree>)` over a throw-away tree holding the plant at the fixture path (never this
worktree); for the Node guard, `scanText(<path>, <text>)` of the mounted `leftovers-check.mjs`. A plant must be able to
fail: each names the configuration it is red in, and one red in no configuration is a NOT-read statement, never a
break-it.

| # | The claim, where in this document | Expected, open-ended | Evidence | State, and before which gate it is observed |
|---|---|---|---|---|
| C1 | § 6 *Before S3*, its old "No benchmark-own file changes" and Files "manifest targets and `template-manifest.json` only" | — | false, the parent's entry 9.1: `TheAdaptedCopyTests.cs:79` pins `21553ce`; `build-test` installs nothing in the mount; case 15's reason names the old removal (`LeftoversSweepCase15Tests.cs:19-21`) **[M]** `83f166d6` | replaced by row F42-c and item 4.1's named paths (§ 6 *Before S3*, restated): **proposed**, before F42-c's plan gate; completeness proved by row F42-c's checks (1)–(4b) |
| C2 | S3's Files: `ContainerPass.cs` "on the copy check's own list" | the own list is `BenchOwn` in `TheAdaptedCopyTests.cs` (`:32-46` **[M]** `83f166d6`), which S3 edits; the check refuses a `.cs` of the folder on neither list | read, the check's own comment (`:28-30`) | S3's Files name `TheAdaptedCopyTests.cs`: **proposed**, observed by S3's copy check red without the `BenchOwn` entry, before S3's plan gate |
| C3 | § 1 row E6-g's "not read" cell, *E6-g by directory*'s C# cells (the C# copy reads `.cs` under `tests/` and `src/`, `.sh`, `.ps1`, `.py`, the workflows, `.mjs`/`.js`/`.cjs` only under `.github/scripts/` and `http/`), § 3's *Not read by any guard*, D7's "by file kind" | each C# cell as written; a cell the plant contradicts is rewritten as the observation says | **[R]** (`GuardFiles`, mcp `53f65130` `LeftoversGuardFiles.cs:28-31`, `:63-69`); the Node cells **[M]** `46e27108` | **proposed**: each C# cell observed by a plant through the copy at `53f65130` (after F42-c), before S7's plan gate |
| C4 | *E6-g by directory*, rows `src/` and `tools/model-arms/`; S7's scenarios | `GetTempPath()` in a `Source`-kind `.cs` expected never red in either guard: a stated limit of both guards. S7 plants instead what the `Source` kind reads: an `Owned.TempRoot` reference outside the helper's folder (expected `raw-temp`) and the bare prefix word (expected `owner-blind`); red or not, the plant says which | **[R]**: `HitsOutsideSource` returns nothing for `SourceKind.Source` (mcp `53f65130` `LeftoversGuard.cs:204-207`); the `Source` kind reads only `owner-blind` and, as `raw-temp`, an `Owned.TempRoot` reference outside the helper's folder (`:216-219`, `:297-301`). The Node guard reads neither path: `isScanned('src/X.cs', 'node')`, `isScanned('tools/model-arms/X.cs', 'node')` → false **[M]** `83f166d6` | **proposed**: the limit and the replacement plants observed before S7's plan gate (the parent's items 4.3 and 8.3: S7 keeps a plant the `Source` kind does read, so its check count stays) |
| C5 | *E6-g by directory*, row `.github/workflows/`: `mktemp -d` in a workflow `run:` in the C# copy | expected red; red or not, to be observed | **[R]**: `.yml` is in `RawTempByExtension` (`LeftoversGuard.cs:84`, `:89`). Node `[]` **[M]** `46e27108` | **proposed**, before S7's plan gate |
| C6 | § 1's correction to § 3: nine `.py` `tempfile.mkdtemp(` lines in the C# copy | expected reported; red or not, to be observed | **[R]**: `\bmkdtemp(?:Sync)?\s*\(` (`LeftoversGuard.cs:78`) | **observed** 2026-11-17 (the parent's § *Epoch 11*, E11-6), **[M]** `b348bb33`: consumer `b348bb33`, upstream mcp `53f65130`, invocation `Bench.Tests --explicit only --filter-class "*LeftoversRepositoryGuardTests"`, fixture the repository's own lines, expected reported, OBSERVED reported: all nine as `raw-temp` (`mkdtemp(`), at `spikes/lang-bench/runner/run_samples.py:35`, `:306`, `test/py/test_rs_kill.py:152`, `test_rs_launch.py:478`, `test_run_samples.py:174`, `:192`, `:241`, `:434`, `:449`. D7 makes them S6's (none is on a line F1 or F2 names). *Was (epoch 10):* **proposed**, before S7's plan gate (D7 says whose they are) |
| C7 | row E6-d and S3 (7): the C# half of "neither guard reads" `SetEnvironmentVariable("TESTCONTAINERS_RYUK_DISABLED", "true")` | expected not read; read or not, to be observed | Node `[]` **[M]** `46e27108`; C# **[R]**: `RyukOff` wants `[:=]` after the name (`LeftoversGuard.cs:93`) | **proposed**: the C# half through the copy at `53f65130`, before S3's plan gate, so S3's own test is known to be the only reader, or the second |
| C8 | row E6-b: the C# copy reads `worktree add` "only outside `src/`" | expected so; red or not, each plant says which | **[R]** (`LeftoversGuard.cs:91`, `:204-209` at `53f65130`) | **proposed**, observed by S4's and S5's plants through the copy, before S4's plan gate. *Epoch 11:* S4's half **observed** by S4's brief (§ 11 O1, **[D]**, the brief local at `4dc04820`): a `worktree add` line is one `raw-worktree` in each guard in a `tests/` `.cs`; in a `src/` `.cs` the copy scans the file and reports nothing, and the Node guard does not scan it. S5's half stays **proposed**, before S5's plan gate |
| C9 | row E6-f1 and S3 (5): the marker clean and, without `live and die with`, one `exemption` "in each" guard | the C# half expected as the Node half; to be observed | Node **observed** **[M]** `83f166d6` (row E6-f1); C# after F42-c | **proposed** (the C# half), before S3's plan gate |
| C10 | S7's *What*: one manifest operation on `LeftoversGuardFiles.cs` makes `.cs` under `tools/` a `Source` file | a discovery boundary changed by an operation: without it, a plant in `tools/model-arms/X.cs` is not read; with it, read as `Source` | not run | **proposed**: S7's brief names the operation and observes it, without and with, with C4's replacement plants, before S7's plan gate. *Epoch 11:* the "without" half **observed** by S4's brief (§ 11 O1 (d), **[D]**): a `.cs` under `tools/model-arms/` is not discovered by the copy (scanned 0) and `isScanned(…, 'node')` is false; the "with" half stays **proposed**, before S7's plan gate |

## 2. Goal

A run of benchmark's tests leaves nothing behind that it did not mean to keep. Every folder, file, worktree and container
a test makes outside its process is named `testsToDelete-<owner>-bench-<what>-<id>` by one helper per language, claimed
first, removed by the test on every path, and collected at the start of the next run when the run that made it was
killed — only when its owner is provably gone. The Testcontainers Postgres carries the name and the owner label, keeps its
reaper, and its per-test databases are exempt because they live and die with it. Two guards keep it so. The AppHost's
persistent Postgres, the product's checkout cache and `bench-scratch/` of a real benchmark run are the product's and stay
as they are.

**The goal's Windows exception, by its true condition** (parent § *Epoch 8* item 1 and 8.3): what a run leaves on Windows
in a process whose NEW files and folders are owned by `BUILTIN\Administrators` (measured: pwsh on `windows-latest`) is
not removed by the sweep and not named by the audit (F31); it waits for a person. An elevated Git Bash makes the CALLER's
files (measured), and the exception does not apply there. A pin of this limit in this suite reads who owns a new file, as
coai #962's test does, and skips with its reason where the caller owns it. Benchmark's CI has no Windows leg **[M]**;
`.agents/PROJECT.md` § *Commands* is written for a Windows machine, so the limit can be real here (whether that machine
still builds or tests this repository is § 12's question 3).

**Done when:** rows E6-a to E6-l (E6-f as E6-f1 and E6-f2; E6-l added 2026-11-17) are each met by their check; both guards are green over the head in CI; a killed run's
folders and worktrees are collected by the next run, and its container by the next run that starts the fixture; the boundary sites of § 9 are clean by E6's own guard.

## 3. The invocation inventory (§ *Epoch 3* item 4 of the parent)

**[M]** the Node guard, `--profile node --token bench --identity "the measuring harness"`, at `7d795024`: 209 findings.

| Folder | raw-temp | raw-worktree | legacy-name | owner-blind | Fixed by |
|---|---|---|---|---|---|
| `tests/Bench.Tests` | 84 | 21 | 15 | 0 | S4 (ModelArms), S5 (the rest) |
| `tests/FakeCli`, `tests/FakeCoai` | 2 | 0 | 0 | 0 | S5 |
| `spikes/policy-ab-quick` | 25 | 0 | 5 | 0 | S6 |
| `spikes/lang-bench` | 3 | 0 | 5 | 0 | the followups plan where its F1/F2 name the site, else S6 (D7) |
| `spikes/minigit-bench` | 18 | 0 | 21 | 2 | the same |
| `spikes/lang-explore` | 0 | 0 | 0 | 6 | the same |
| `research/local-model-coding/harness` (`t-lib.sh:20`, `part6/t-lib.sh:12`) | 0 | 2 | 0 | 0 | S7 (D8) |

The default profile finds 3 of these (the two `t-lib.sh` lines and `spikes/lang-bench/test/hello/prove.sh:7`).

**What each kind is today** **[M]**:

- **Temp folders in C# tests.** A private `IDisposable` per file (`TempSuite`, `TempLogRoot`, `TempSpool`, `TempCatalog`,
  `TempBank`, `TempRoot`, `FakeCli`, `FakeCoai`, `ScoutFake`, `DatedGitRepo`, `TempGitRepo`) names `bench-<x>-<guid>` in the
  temp root: no owner, no claim. `ModelArms/TempFolder` names `testsToDelete-modelarms-…` (legacy) and removes in `finally`
  (`TempFolder.cs:54-64`). About 40 sites remove by `Dispose`/`finally`; 8 never remove (E6-k); about 36 lines only build
  a path (a `cwd`, an `--artifact-root`). `ModelArms/PinnedConsumer.cs:126-137` moves a timed-out reader's scratch to
  `testsToDelete-modelarms-reader-<name>-<id>` on purpose, "removed by hand" (`:109-110`).
- **Child-process defaults.** `tests/FakeCli/Program.cs:664-669` writes under `<temp>/bench-fake-cli/<model>` and
  `tests/FakeCoai/Program.cs:417` under `<temp>/fake-coai-data` when their variable is not set; the parent tests make and
  remove their own folders.
- **Worktrees and clones.** Product: `ScratchWorktree.AddAsync` (`src/Bench.Infrastructure/Git/ScratchWorktree.cs:13-28`)
  makes `<root>/<uuid7>` by `worktree add --detach --force`, the default root `<temp>/bench-scratch` (`:16`); callers
  `HarvestGates.cs:67` and `SolutionSignals.cs:72` (default root), `CliSubjectRuntime.cs:88` (`WorktreeRoot`). Tests reach
  the default root through `HarvestGatesTests` and `SolutionSignalsTests` over a `DatedGitRepo` (worktrees outside any owned
  folder). Tests by hand: 10 real `worktree add` lines, all inside a `TempFolder`
  (`ModelArms/ArmGitTaskQuestionsTests.cs:228,234,242,304,305,307,421,438`, `TaskWorktreesPlantTests.cs:47`,
  `TaskWorktreesReviewTests.cs:27`); 10 lines that only spell `worktree add` in an assertion
  (`TaskWorktreesReviewTests.cs:40,73,91`, `TaskWorktreesRoadTests.cs:79`, `TaskWorktreesTests.cs:70,190,265,455,522,570`);
  1 line of captured data (`Fixtures/scouts/claude-2.1.289-two-children-models.ndjson:2`, a transcript that quotes a
  `worktree add` command).
- **Containers.** One: `PostgresFixture.cs:27-33`, the one collection fixture `"postgres"` (`:120-121`), so one container
  per test process, used by 68 test classes. Not E6's: the AppHost's persistent Postgres (`hosts/AppHost/AppHost.cs:55-61`),
  the `contract` job's `services:` container (`ci.yml:52-61`, job-scoped), `scripts/bank-thirds.sh:71-72` and
  `scripts/telemetry-ingest.ps1:39-40` (`docker start` of that existing container), and `http/README.md:32-33` (a person's
  recipe, `docker run -d --name bench-suite-pg …` without `--rm`; S7 rewrites it).
- **Databases.** 29 per-test `CREATE DATABASE` call sites in 16 files, never dropped, inside the fixture's container (E6-f1, E6-f2).
- **Not read by any guard** **[M]** (the Node half; the C# half **[R]**, by reading `GuardFiles`, claim C3): `spikes/lang-bench/lib/conformance.mjs:152`, `lib/toolchain-install.mjs:152`,
  `spikes/minigit-bench/capture/capture-files.mjs:71`, `smoke-session.mjs:61`, `lib/preflight.mjs:33` (plain `.mjs`);
  `spikes/lang-bench/test/py/*.py` (`tempfile.mkdtemp`, with `finally`; since S2 the C# copy reads `.py`, **[R]**, see § 1's
  *E6-g by directory* and claims C3 and C6); `spikes/EvalLibrarySpike/Program.cs:12` (a console
  spike). Product temp sites in `src/` and `hosts/` (`SolutionSignals.cs:179`, `ScoutTrees.cs:178`,
  `FileSystemGateAssessmentFiles.cs:33`, `GateToolsCommand.cs:136`) remove in `finally`/`Dispose`; they are the product's.

## 4. Decisions

**D1 — Owner form, token `bench`.** Names are `testsToDelete-<owner>-bench-<what>-<id>` (rule §1); `bench` is a registered
row (`the measuring harness`). The Node side already uses it: `spikes/lang-bench` binds `ownedFor({ token: 'bench' })`
(`test/toolchain-fixtures.mjs:10-11`).

**D2 — The C# helper is an adapted copy of `dew_flow_mcp · tests/Mcp.Tests/Leftovers/` at `21553ce`**, by file, with a
committed `template-manifest.json` in coai's format (keys `about`, `repository`, `commit`, `entries`; per file the upstream
path, the target, the upstream blob SHA and ordered exact-replace operations with their counts) and a diff check
`TheAdaptedCopyTests` that undoes them on the raw bytes (parent § *Epoch 4* item 4). Target folder
`tests/Bench.Tests/Leftovers/`, namespace `Bench.Tests.Leftovers`. **Why `21553ce` and not `197cc76`** (the earlier
copies' commit): the 8 files changed between them carry only the move of the mount to `.agents/conventions`, which this
repository has, so the copy needs fewer operations. Permitted operations: the namespace; `OwnedToken.cs` (`RepoToken =
"bench"`, `RepoRow = "the measuring harness"`); the helper's paths (`tests/Mcp.Tests/…` → `tests/Bench.Tests/…`); the
edits a story lists. The ownership, liveness and deletion algorithms are not changed. Benchmark-own files (not manifest
targets) are listed in the check's own array, as coai does. *Epoch 9 (2026-11-16):* the copy is re-synced to F42's
commit before S3 (the parent's row F42-c, § 5's re-sync PR): the template then takes the `container-scoped` kind and
carries the fixes of N1–N5 that reproduced. The manifest's `commit` names F42's commit from then on, and the same
operations apply; `21553ce` stays the commit S2 copied.

**D3 — The limits copied with the template, stated, and F29 closed by copy.** F29 (two sweeps at once on Windows can
report a removal they did not do, or restore a folder without its claim): this repository's commands are written for
Windows, so the sweep takes coai's per-root named mutex, `SweepLock`, as a benchmark-own file (copied from
`dew_flow_connect_other_ais · src_mcp/tests_leftovers/SweepLock.cs`, cited in its header; reuse-first, no second design).
F31: stated in the manifest's `about` and in `research/module_tests.md` by its true condition (§ 2), never as "an elevated
process".

**D4 — The worktree seam is a ROOT** (the old draft's D4, kept). `HarvestGates.RunAsync` (`:50`) and
`SolutionSignals.RunAsync` (`:53`) each gain an overload with a trailing `string scratchRoot`, passed to the existing
`ScratchWorktree.AddAsync(…, underRoot)` (`:21-22`); the existing signatures delegate with a new
`ScratchWorktree.DefaultRoot` that holds exactly today's `Path.Combine(Path.GetTempPath(), "bench-scratch")`. Product
callers (`HarvestCommand.cs:242`, `SolveCommand.cs:184`) do not change, so a benchmark run names its worktrees as today,
and a test pins that. Tests pass a root inside the owned folder that also holds the repository: the worktree is then
INSIDE what the run owns (rule §1, last item), needs no prefix and no lock, and a kill is collected with the folder
(E6-c). *Rejected:* a leaf-name seam (`testsToDelete-…-wt-<id>` in the temp root, locked): it needs the worktree pass the
C# template does not have, and the `other clone` rule would then keep every such leaf.

**D5 — Everything a test makes lives inside ONE owned folder** (the old draft's D3, kept). A helper never puts a second
top-level folder in the temp root: the old `bench-<x>-<guid>` classes become thin wrappers over the copied `OwnedDir`, and
their siblings move inside. Why: an unclaimed prefixed folder is collectable once ten minutes old, so a prefixed sibling
outside its claim could be swept from under a long run; inside, one claim covers everything.

**D6 — The fakes' homes are owned folders the child can find** (the old draft's D5, kept in shape): the parent passes
the owned folder through the fake's own variable; with the variable unset, the fake refuses to start instead of writing
to `<temp>/bench-fake-cli` or `<temp>/fake-coai-data`.
*Epoch 11 (the parent's § *Epoch 11*, E11-3):* that holds on every launch path that passes the variable (`FAKE_CLI_HOME`,
`COAI_DATA_DIR`). The probe runner launches the fake CLI under a minimal REPLACED environment in which only `TEMP` passes,
"so a test cannot hand the fake a variable at all" (`tests/FakeCli/Program.cs:658-661`, **[R]**;
`research/PLAN_question_consultant_probes.md:317-318`, **[D]**); its tests name the home
`<temp>/bench-fake-cli/<model>` (`tests/Bench.Tests/Probes/Driver/FakeCli.cs:15`, **[M]** `b348bb33`). On that path the
per-model home lies INSIDE an owned folder the test made, reached through the model id the test mints (the id names
that folder, so `FakeHome.For` resolves inside it); an id that names no existing folder makes the fake refuse, and
`<temp>/bench-fake-cli` is never made. No product file changes for it. S5's brief observes, before its plan gate, that
the probe path accepts such a model id (red or not, the run says which); if it does not, the brief stops and reports:
a product file is outside S5's *Files*.
*Observed and revised during the parent's re-review (its § *Epoch 11* item 3.3, O-a, 2026-11-17):* through the probe
runner (`CliProbeRunner`, the child's minimal environment) in a scratch clone at `b348bb33`, a model id that is the leaf of
an owned folder (`testsToDelete-u1000-bench-fakecli-<6>`, 40 characters) parses as a model id and a subject's model, the
attempt is answered and the fake's call log lands inside that folder; an owner-form id naming no folder settles `Failed`,
exit 3, and nothing is made: **observed**. A refusal of EVERY id without a folder would break the scouts' out-of-process
test, which launches the fake through the production services with no variable and a test-given `TEMP`
(`tests/Bench.Tests/Cli/ScoutsOutOfProcessTests.cs:19-20`, `:31-32`). So the fake's home is three-way: `FAKE_CLI_HOME` when
passed; an owner-form id → `<TEMP>/<id>`, which must exist, else exit 3; any other id → `<TEMP>/bench-fake-cli/<model>` as
today. Each test launch puts that home inside an owned folder (the probe driver by the owner-form id; the scouts'
out-of-process test by its test-given `TEMP`; the model-arms launches by `FAKE_CLI_HOME`). The refusal on an unset
variable stays for the fake coai (`COAI_DATA_DIR`), which the product's gate driver always sets
(`src/Bench.Domain/Gate/CellPaths.cs:83-84`, **[R]**).

**D7 — The spikes, by the boundary.** The followups plan's stories S1 and S2 fix the sites its F1 and F2 name
(`todo/PLAN_bench_test_leftovers_followups.md` § *F-items*); E6 does not build them again. Every other finding of E6's
guards in `spikes/` is E6's (S6). E6's guards read `spikes/` (the boundary row says so) by file kind, not all of it:
its test files and its `.sh`, `.ps1` and `.py` scripts, never a plain `.mjs` or a `.cs` there (§ 1's *E6-g by directory*, corrected
2026-11-16; this sentence used to say "all of `spikes/`"; the C# half is **[R]**, claim C3, observed before S7's plan gate). So **S7 merges only after
the followups plan's S1 and S2 are merged**, or after the owner moves those sites to E6. This is the boundary as both
documents already write it; it is not a change.

**D8 — The two research harness lines.** `research/local-model-coding/harness/t-lib.sh:20` and `part6/t-lib.sh:12` make
worktrees for a measurement, which the rule covers. S7 reads each one. If its experiment still runs, the worktree is
made inside the experiment's own root folder, and the line takes one inline `raw-worktree` exemption that names that
folder. If the experiment is archived and not run, the line takes one inline exemption that says so. No experiment is
run by this epic.

**D9 — The container pass is benchmark's own, by label** (rule §4: a sweep per kind the repository makes; rule § *The
janitor*: the janitor sweeps no stores and is not re-implemented, and a repository keeps its own pass for what it
creates). At the start of the run, before the fixture starts: ONE `docker ps -a -q --no-trunc --filter
label=testsToDelete.owner`, then, only when it listed any, `docker inspect` for the ids in batches of at most 100, judged by the two keys of rule §4 (the label's record,
the name's owner, the caller's token; the record `gone`), age selects (10 minutes), removal by the captured id (`docker
rm -f -v <id>`). Never `--filter name=`. Each `docker` call is an executable with an argument array, never a
shell string, with a 30-second timeout; on the timeout the whole process tree is killed (`.agents/PROJECT.md` § 11 and
`common/security.md` § *External processes*; the brief uses the repository's existing process runner where it already
does this). No `docker` binary, a stopped daemon, a non-zero exit or a 30-second timeout is
one reported line and nothing removed; the run goes on. Ryuk is the first collector; this pass collects what Ryuk could
not (a daemon restart, a reaper killed with the run). **Where it runs** (the plan gate of this revision, session
`215fc3a2`): inside `PostgresFixture.InitializeAsync`, before the container starts, behind a once-per-process gate —
not in `StartOfRunSweep`, because xUnit initializes a collection fixture before any `BeforeAfterTestAttribute.Before`,
so a pass there would run after the fixture's container already exists.

**D10 — No `raw-temp` baseline.** Every site moves in its story; the repository guard test lands in S7 with the last site
(the old draft's reason: two merges would leave `main` red between them).

**D11 — The old names are listed, never swept.** `bench-*`, `fake-coai-data`, `testsToDelete-modelarms-*` and the other
legacy names carry no owner segment, so no new sweep matches them (rule §1); the janitor lists legacy ones. S7 lists what
is on the WSL machine before and after, and nothing is removed without the owner's word. `bench-scratch/` is the
product's and is never removed by hand while a benchmark may run.

## 5. Order

| Story | What | Model (brief · code · review) | Depends on |
|---|---|---|---|
| S1 | E6-h: the held-out runner lists `score-tmp`, never removes by age | Opus · Sonnet (or main session, small edit) · Opus | — (lands before any producer) |
| S2 | E6-e, E6-c, E6-j: the adapted copy, the start-of-run folder sweep with `SweepLock`, the audit; the mount in `build-test` | Opus · Sonnet · Opus | S1 |
| — (F42-c) | the copy re-synced to F42 (F42-c): a prerequisite PR of S3, not a story of this document (§ 6, *Before S3*) | Opus · Sonnet · Opus | S2; F42 merged in mcp |
| S3 | E6-d, E6-f1, E6-f2: the fixture's name, label, Ryuk, no reuse; the container pass (D9); the `container-scoped` marker | Opus · Sonnet · Opus | S2; F41-b (this repository's rules pin at F41's release); F42-c (the re-sync PR above) |
| S4 | E6-i, E6-b (by hand): `ModelArms/` — `TempFolder` and the legacy literals; the hand-made worktrees | Opus · Sonnet · Opus | S3 (so F41-b and F42-c) |
| S5 | E6-k, E6-b (seam): the rest of `Bench.Tests`, the fakes, the product seams of D4 | Opus · Sonnet · Opus | S3 |
| S6 | `spikes/policy-ab-quick` and the spike sites D7 gives E6 (Node, the shipped helper) | Opus · Sonnet · Opus | S1, S3 |
| S7 | E6-g: both guards on, the CI step, the research lines (D8), the docs, the machine listing | Opus · Sonnet · Opus | S3, S4, S5, S6; the followups plan's S1 and S2 (D7) |

S1 is the last sweeper-stage change of this repository (rule § *The barrier*): S2 and later make the owner form, so S1
merges first. S4 to S6 touch disjoint files and may be built in parallel once S3 has merged, but at most three PRs are open here at once
(`common/pull-requests.md` rule 7). No cornerstone (the parent's ledger is full at 3 of 3, and E6's entry says no).

**Epoch 9 (2026-11-16), the parent's § *Epoch 9* item 5.** S3 waits for two things outside this document, so that
E6-f1's marker is accepted by both guards when S3 writes it. **F41-b** is this repository's pin bump to the conventions
release that carries F41 (the `container-scoped` kind in the rule and the Node guard); it is one of the nine pin bump
PRs the parent plans with that release, not a story here. **F42-c** is the re-sync of the C# copy to the mcp commit of
F42, in this repository, its own PR before S3. It is a prerequisite PR, not a story of this document: it changes no
condition of E6, it re-runs E6-e's check at a new commit (D2). As in the parent's build order, S4 to S7 follow S3: all
the remaining stories come after F42-c and S3, so they wait for both prerequisites too.

**Epoch 10 (2026-11-16), the parent's § *Epoch 10* item 5.** F41-b is done (#216, `83f166d6`). This amendment merges
before F42-c's plan gate. Then F42-c, its brief revised by new commits on `feat/e6-copy-f42` (never a rewrite) to item
4.1's named paths and row F42-c's checks (1)–(4b) → E6.S3, its local brief rewritten to rows E6-f1, E6-f2, E6-d and claim
C7 → S4 … S7 as above. Each claim of § 1 (*The claims*) is observed before the plan gate it names.

**Epoch 11 (2026-11-17), the parent's § *Epoch 11* item 5.** F42-c (#218) and S3 (#219) are merged. This amendment
merges before S4's plan gate. Then S4's brief, revised by new commits on `feat/e6-s4-modelarms` (never a rewrite): K5 as
row E6-l, its § 0.1 as S4's *Files* below, its § 9 as S4's *Done when*, its § 14 answered by the parent's item 4.2, the
C# half of row E6-l (3) observed → S4's plan gate → S4. S5 and S6 briefs, each with its items observed before its gate;
S7 after S4–S6 and the followups plan's S1 and S2 (D7).

## 6. Stories

**Every story's PR carries its plan records** (added by the epoch-11 amendment): its brief, its `todo/README.md` row,
the brief's promotion with its `research/README.md` row, and this document's progress line. They count as on that
story's *Files* line. **Every story's inline exemptions** are listed by its brief (file, line, class, reason) before its
plan gate, and the PR that adds them copies the list into § 11 below; no other exemption is added; `owner-blind` and
`ryuk-off` take none.

### S1 — The held-out runner lists, never removes by age (E6-h)

**What.** `sweepLeftovers` (`heldout-run.mjs:93-113`) stops removing un-prefixed `score-tmp/<name>` by age: every entry
under `score-tmp` is LISTED with its age, and nothing is removed. The `sweep` command (`heldout-tool.mjs:105-112`) prints
each listed entry and the command a person runs after checking it. `withClearSuite` (`:64-83`) still removes its own
folder in `finally`.
**Scenarios.** (1) RED first: change the test at `heldout-run.test.mjs:140-160` to expect `removed: []` and `old-1` listed;
watch it fail on today's code; then green. (2) A young entry is listed too, never removed. (3) A failing `lstat` is one
reported entry, not a throw.
**Files.** `spikes/minigit-bench/lib/heldout-run.mjs`, `heldout-tool.mjs`, `test/heldout-run.test.mjs`;
`research/module_tests.md` (the minigit section, one line).
**Done when.** No code path in the repository removes by age or by bare prefix (`node .agents/conventions/tools/leftovers-check.mjs`
shows no `owner-blind` outside § 9's sites); the minigit suite is green.

### S2 — The adapted copy, the folder sweep and the audit (E6-e, E6-c, E6-j)

**What.** Copy the 51 files of mcp's `tests/Mcp.Tests/Leftovers/` at `21553ce` (plus `DirectoryLinks.cs` and
`RepositoryRoot.cs`) to `tests/Bench.Tests/Leftovers/` under D2, with the manifest and `TheAdaptedCopyTests`. Add the
benchmark-own files: `SweepLock` (D3); the start-of-run hook, copied from coai's `StartOfRunOrderAttribute` (an
assembly-level xUnit v3 `BeforeAfterTestAttribute`) and `StartOfRunSweep`, which runs the folder sweep ONCE per process,
behind a once-only gate that every thread awaits: no test body on any thread starts before the sweep has finished; and a `TempDir`-style entry (`TempDir.For(what).Path`) that the later stories use.
Wire the end-of-run audit. Add `git submodule update --init --depth 1 .agents/conventions` to `build-test` (E6-j). The
repository guard test is copied but NOT switched on (D10): it lands in S7.
**Scenarios** (each RED first where it pins a defect): the template's own tests, copied, green; case 15 (E6-c): in the temp
root, an old dead-claimed `testsToDelete-<owner>-bench-gitrepo-<id>/` holding `repo/` (a real repository) and
`scratch/<id>/` (a real linked worktree of it) is removed whole, and it is not kept as `other clone`; a live-claimed one
with the same content is kept; a folder whose OWN `.git` is a file is kept as `other clone`; the manifest check is red when
one copied byte changes; the start-of-run hook runs the sweep exactly once when many test classes start at once on 8 threads (a
counter the test reads); two sweeps at once on Windows: coai's two-sweeps race test, copied as a benchmark-own file,
asserts neither F29 outcome (a removal reported that did not happen, a folder restored without its claim). It runs
where the suite runs on Windows (the owner's machine) and skips elsewhere with its reason; benchmark has no Windows CI
leg, and this epic adds none.
**Files.** `tests/Bench.Tests/Leftovers/**`, `tests/Bench.Tests/Bench.Tests.csproj` (if a file needs it), `.github/workflows/ci.yml`,
`research/module_tests.md` (a *Leftovers* section: the helper, the sweep, the audit, the limits of D3).
**Done when.** The copy check is green; case 15 is green and was red before the copy's sweep existed; the whole suite is
green on `ubuntu-latest`.

### Before S3 — the copy re-synced to F42 (the parent's row F42-c; a prerequisite PR, not a story)

**What.** When mcp has merged F42 (the `container-scoped` kind in the C# template, case for case with F41, and the
notes N1–N5 reproduced, each one that applies fixed), the adapted copy is brought to that commit: the manifest's
`commit` and each entry's upstream blob SHA re-pinned, the copied files rewritten from the template under the same
operations (D2), and any operation whose count changed corrected. No copied file is edited by hand.

*Restated 2026-11-16 by the parent's § Epoch 10 (row F42-c, item 4.1), from its entry 9.1.* This section used to say
"No benchmark-own file changes" and list as Files "manifest targets and `template-manifest.json` only" (claim C1). F42-c's
brief found that the re-sync needs benchmark-own touches (the parent's entry 9.1: `TheAdaptedCopyTests.cs:79` pins
`21553ce`; `build-test` installs nothing in the mount, which the differential test needs; case 15's Windows reason names
the old removal, **[M]** `83f166d6`). The scope is now a list of NAMED PATHS, each with the dependency that needs it,
proved complete by a run; never a count and never a bare "no other file":

- **The manifest and its targets** under `tests/Bench.Tests/Leftovers/`: `commit` → `53f651309a271e7388c703e1ec73799ff6f1c76e`;
  55 entries (53 today, **[M]** `83f166d6`); nine changed blobs (`LeftoversAudit.cs` `f1a93747` → `3f32fc52`,
  `LeftoversAuditException.cs` `6d703ecf` → `dd5860bb`, `LeftoversAuditTests.cs` `fcc8484b` → `2e258e84`,
  `LeftoversGuard.cs` `8a92a79b` → `529646e3`, `LeftoversGuardExemptions.cs` `156fe549` → `a240a05a`,
  `LeftoversGuardTests.cs` `f6a8e035` → `951b43af`, `Native.cs` `dd6c8680` → `f9a277f6`, `OwnedTests.cs` `20a0a213` →
  `b97e3eff`, `Removal.cs` `05e58fb5` → `d6ec896f`); two new entries (`LeftoversGuardScenarios.cs` `8da3abb6`,
  `LeftoversGuardDifferentialTests.cs` `2d308cd3`); the other 44 entries unchanged (**[M]** `83f166d6`: `git rev-parse`
  of each path at mcp `21553ce` and `53f65130`, and the manifest's blobs today). The manifest's `about` field names
  `21553ce` today (**[M]** `template-manifest.json:2`, `83f166d6`); it may change as F42-c's brief writes it (the parent's
  row F42-c (4b): "the two new entries (its `about` as the brief writes it)"), and in no other way.
- **The permitted transformations:** D2's operations as the manifest holds them today, applied unchanged; ONE count
  corrected (`mcp tests:` 6 → 7 in `LeftoversAuditTests.cs`, **[M]** 6 lines at `21553ce`, 7 at `53f65130`); the
  namespace operation only on the two new entries; no new kind of operation.
- **`TheAdaptedCopyTests.cs:79`** (it pins the manifest's commit) and nothing else of that file (S3 edits its `BenchOwn`,
  `:32-46`; claim C2).
- **`.gitattributes`:** one `-text` line per new copied file (S2's decision: one line per copied file).
- **`.github/workflows/ci.yml`, job `build-test` only:** Node 20 or later on `PATH` with its version printed, then
  `npm ci --ignore-scripts` in `.agents/conventions`, both before the *Test* step (the differential test runs the mounted
  Node guard, which reads workflow rows through the mount's `yaml`). `build-test` is the only job that runs `Bench.Tests`
  (**[M]** `83f166d6`, `ci.yml` read).
- **`LeftoversSweepCase15Tests.cs`:** the Windows reason's text only, wherever it stands: the reason string (`:19-21`) and
  the XML doc that carries the same reason (`:14-15`); the skip stays.
- **The docs:** `research/module_tests.md`, `research/README.md`, `todo/README.md`, this document (progress lines only:
  its § 6 boundary is amended by the stories amendment, not by F42-c), F42-c's brief and S2's brief (their promotion:
  `todo/PLAN_test_leftovers_E6_F42c_brief.md` → `research/…`, `todo/PLAN_test_leftovers_E6_S2_brief.md` → `research/…`).

A file the clean-checkout run needs outside these paths is found BEFORE the PR, by that run; adding it is then a change
of the parent's epoch 10 and counts there.

**Check.** `TheAdaptedCopyTests` green at F42's commit; one byte changed in a copied file → red (the parent's row F42-c).
The copied guard's own tests, copied with it, green; case 15 still green on Linux. And row F42-c's checks, as the parent's
§ *Epoch 10* item 3 writes them: (1) from a CLEAN checkout of the PR head (a fresh clone; `git submodule update --init`,
which is the mount only; then `npm ci --ignore-scripts` in `.agents/conventions`): `TheAdaptedCopyTests` green with 55
entries; one copied byte changed → its row red; the `mcp tests:` count set back to 6 → red at that operation.
(2) `LeftoversGuardDifferentialTests` 3 of 3 green, its marker rows RUN, not skipped; and red with the mount's
`node_modules` absent (`YamlMissingError`). (3) CI `build-test` green on the head, where Node 20 or later (its version
printed) and the mount's `npm ci` come before the *Test* step. (4) `git diff --name-only <base>..HEAD` lies within the
named paths above. (4b) `git diff -U0 <base>..HEAD` per path stays within that path's bound: the manifest changes only its
`commit`, the nine blob SHAs, the `mcp tests:` count 6 → 7 and the two new entries (its `about` as F42-c's brief writes
it); `TheAdaptedCopyTests.cs` only `:79`;
case 15 only the reason's text (`:19-21` and `:14-15`); `ci.yml` only `build-test`, one step; no new kind of operation; this document only its
progress lines. Does not prove: Windows (benchmark has no Windows leg).
**Files.** The named paths above. *Was:* `tests/Bench.Tests/Leftovers/**` (manifest targets and `template-manifest.json`
only).
**Done when.** The check is green on `ubuntu-latest`, and its red run is quoted on the PR; checks (1)–(4b) ran and their
results are quoted on the PR. State: **proposed** (row F42-c), before F42-c's plan gate; built on `feat/e6-copy-f42` (brief `research/PLAN_test_leftovers_E6_F42c_brief.md`); checks (1)–(4b) run on its final head, results on its PR.

### S3 — The fixture's container (E6-d, E6-f1, E6-f2) and the container pass (D9)

**Starts after** S2, F41-b and F42-c (§ 5): only then does each guard accept the marker this story writes.
**What.** `PostgresFixture` names its container `testsToDelete-<owner>-bench-pg-<id>` from the helper (`.WithName`), adds
`.WithLabel("testsToDelete.owner", <record>)`, never sets reuse, and keeps Ryuk. The `container-scoped` exemption comment
goes on the line above `NewEmptyDatabaseAsync` (`:96`): `testsToDelete-exempt: container-scoped — the per-test databases
live and die with the PostgresFixture container`. Under F41 the marker is a declaration: it clears nothing on that line,
since the fixture drops no database (the parent's item 4.2). The container pass of D9 runs in the first process that starts the fixture, before its container (D9).
**Scenarios.** Every Docker query below is the same kind of call: it must SUCCEED to count, and a failed query or a
timeout fails the test, never passes it as "absent" (the brief names the query, for example `docker ps -a -q --no-trunc
--filter id=<id>`, exit 0, empty when nothing is found). (1) Ryuk (E6-d (1)): a child test process starts the fixture;
the container exists by the captured id before the child is killed with SIGKILL; within the reaper's window the same
query succeeds and finds nothing. This replaces "`docker inspect` by the captured id fails", which read a failed query as
absent. (2) The label (E6-d (2)): the started container's `testsToDelete.owner` label equals the run's record, and the
container pass, given a fake `docker` that lists a dead-owner container of this token older than 10 minutes, removes it
by id; a live-owner one, another owner's, another token's and a young one are kept, each with its reason. (3) No reuse
(E6-f2 (3), which is E6-d (3)): no guard reads reuse (the Node guard: `.WithReuse(true)` → `[]`, **[M]** `46e27108`; the C#
copy: no reuse spelling in its guard files, **[R]**), so it is proved at run time by (4)'s disposal check. RED, each:
reuse enabled through the builder the fixture uses, and through configuration alone (`TESTCONTAINERS_REUSE_ENABLE=true`,
or `testcontainers.reuse.enable=true` in the properties file the pinned version reads), with the fixture's source
unchanged; the brief first reads which configuration the pinned 4.16.0 reads (§ 1, E6-f2: none was found in its
binaries), and records the configuration half's result either way. A source test that the fixture file has no
`.WithReuse(`, and a read of the built container's reuse setting if the API exposes one, may be added, never instead.
(4) E6-f2 (1) and (2): the container exists by the captured id before `DisposeAsync`, the query having succeeded; after
it, the same query succeeds and finds nothing, so its databases are gone. (5) E6-f1: no finding in `PostgresFixture.cs`
from the copied C# guard or the Node guard, and the Node guard's count over the repository equals the base's; a
malformed marker (the same file with the reason's `live and die with` removed) is one `exemption` finding in each (the
Node half observed, **[M]** `83f166d6`; the C# half expected, **[R]**, claim C9, observed through F42-c's copy before
S3's plan gate). (6) No
`docker` on `PATH`: the pass reports one line and the suite goes on to the fixture's own loud failure
(`PostgresFixture.cs:45-47`). (7) Ryuk is never turned off in code (E6-d, the parent's F43): a benchmark test over every
`.cs` of the repository finds no `TESTCONTAINERS_RYUK_DISABLED` in any spelling, and is red on a planted
`Environment.SetEnvironmentVariable("TESTCONTAINERS_RYUK_DISABLED", "true")`, which the Node guard does not read (`[]`,
**[M]** `46e27108`) and the C# copy is expected not to read (**[R]**, `RyukOff` wants `:` or `=` after the name,
`LeftoversGuard.cs:93`); read or not, claim C7 observes it through the copy at `53f65130` before S3's plan gate, so this
test is known to be the only reader, or the second.
**Files.** `tests/Bench.Tests/Infrastructure/PostgresFixture.cs`, a benchmark-own `ContainerPass.cs` in
`tests/Bench.Tests/Leftovers/` (on the copy check's own list), its tests, `research/module_tests.md`; and
`tests/Bench.Tests/Leftovers/TheAdaptedCopyTests.cs`, whose `BenchOwn` (`:32-46`) is that own list: `ContainerPass.cs`
and each new test file of the folder are added there, since the check refuses a `.cs` of the folder that is on neither
list (claim C2, **proposed**: observed by S3's copy check red without the entry, before S3's plan gate). F42-c touches
only `:79` of the same file and merges first.
**Done when.** (1) to (7) green on `ubuntu-latest`, and each counterexample of (3), (5) and (7) seen red once, quoted on
the PR; the name stays unique per process on one daemon (two suites at once: two names).

### S4 — `ModelArms/`: the owned folder, no legacy name, and the pilots' output outside the family (E6-i, E6-l, E6-b by hand)

**What** (restated by the epoch-11 amendment, the parent's § *Epoch 11* item 4.2). `TempFolder` makes its folder through
the helper (`testsToDelete-<owner>-bench-modelarms-<id>`, claimed) and keeps its `OpenForDeleting` and `finally`; its
112 construction sites do not change. Each site S4 disposes of is classified by its LIFETIME, and each class has ONE
disposition:

| Lifetime | Disposition | S4's sites (at `b348bb33`) |
|---|---|---|
| the run | the copied helper (`OwnedDir`, `TempFolder`, `TempDir.For`): owner form, claimed, removed by exact path | `TempFolder.cs:10`; `BriefFillerTests.cs:156`, `:184`, `:231`; `ModelArmsProgramTests.cs:83`; `PosixFolderIsolationTests.cs:321` (`OwnedDir.Create("drvfs", scratch)` on the operator's 9p mount; its limit in its summary: a killed run leaves the claimed folder there, which no start-of-run sweep reads); `KernelFactsTests.cs:135`; M2's validation probe `M2PilotScaffold.cs:72-78` (also called by M3, `M3PilotScaffold.cs:54`), a `TempFolder` |
| inside an owned folder | a plain name inside, no prefix (rule §1, last item) | `PinnedConsumer.cs:126-137`: the kept scratch moves INSIDE the run's owned folder under a plain name, so it is the run's and goes with it; the brief checks that the reader is never given a path outside the owned folder; the failure message is the record |
| a name never made, a path only read, sample text | a name from the helper (`Owned.NameFor`) where the name matters; otherwise a neutral literal; `TempDir.Root` (shape G) only for a path nothing creates or keeps | `InitVerbTests.cs:138`; `TimingLineTests.cs:311-312`; the 17 `"/tmp"` literals the C# copy reads (rewritten, never exempted); `KernelFactsTests.cs:27`; `ValidateVerbTests.cs:752`, `:755` |
| must OUTLIVE the run (the owner's attended pilot; row E6-l) | the folder the REQUIRED variable `BENCH_PILOT_OUT` names, outside the testsToDelete family: unprefixed children `bench-m2-<what>-<id>`, `bench-m3-<what>-<id>`, `bench-m3-manifest-<id>.json` (claimed exclusively as today); refused, before anything is made, with a message naming `BENCH_PILOT_OUT`, when it is unset, empty, relative, not an existing folder or inside a git checkout (`init` refuses a root there, `ModelArms.Core/Experiment/ExperimentFile.cs:70`); never `Path.GetTempPath()` or `TempDir.Root` for this output, no exemption marker; retired by the pilot's own teardown (M2: the two printed paths; M3: its manifest, T1–T5, T4 reading `bench-m3-*-<id>`). The precedent: `BENCH_ARTIFACT_ROOT` (`hosts/Cli/ProbesInputs.cs:75-78`, refused with a sentence naming it, `:97`) | `M2PilotScaffold.cs:39`, `:40`; `M3PilotScaffold.cs:34`, `:37`, `:105` |
| a test about the old name itself | the `~` spelling, in the form clean in BOTH guards: `"tests~To~Delete-…".Replace("~", …)` (`"tests~ToDelete-…"` leaves the piece `ToDelete`, `owner-blind` in the C# copy: S4's brief O5) | none in S4 |

The 10 real `worktree add` lines take one inline `raw-worktree` exemption each that names the `TempFolder` they live in
(E6-b), on the line itself or the line directly above; the 10 assertion strings use the `~` precedent
(`"work~tree add"`), not a marker. *Was (epoch 10):* "Each of the 15 legacy literals moves to the helper or, where the
test is about the old name itself, is spelled with the `PlantedLines` `~` precedent (coai's
`LeftoversBaselineTests.cs:256-260`)", which no disposition of the pilots' output fits (the parent's entry 10.2).
**Scenarios.** RED first: the guards' findings in `ModelArms/` before (row E6-i's counts); zero after but the 10 marked
lines. A test makes a `TempFolder`, is killed (a child process), and the next run's sweep removes the folder (rule §4:
dead record). Each pilot scaffold, RED first: with `BENCH_PILOT_OUT` unset it writes under the temp root today; after,
it refuses naming the variable; with it set, every path it prints is under it (row E6-l). The ModelArms suite is green;
its time does not grow by more than the claim's cost (measured and recorded beside the base's 2 m 10.7 s). *Was (epoch
10):* "RED first: the guard's 15 `legacy-name` and 21 `raw-worktree` findings in `ModelArms/` before; zero after" (the
Node guard reads 20 `raw-worktree` there, **[M]** `b348bb33`; the 21st of § 3 is the `.ndjson` line, S5's).
**Files** (named paths; the brief's § 0.1 gives each `ModelArms/` file its sites).
- `tests/Bench.Tests/ModelArms/**`;
- `research/module_model_arms.md`: `:1462` (`TempFolder`'s folder form), `:2114` (a timed-out reader's scratch, now kept
  inside the owned folder), `:2250-2256` (M2's procedure: step 1's printed paths and `BENCH_PILOT_OUT`, the log's removal
  sentence); the run logs (`:2262`, `:2322`, `:2370`) keep their observed paths as history;
- `research/PLAN_model_arms_E6.md` (IMPLEMENTED): a dated note beside `:338-341` (the names), `:381` (T4's name check),
  `:746`, `:819` and `:821` (the kept reader's name; the growth rows), the old text kept;
- `research/PLAN_model_arms_E3.md` (IMPLEMENTED): a dated note beside `:810-811`, `:876`, `:885`, `:1146`, the old text kept;
- the plan records (§ 6's first paragraph).
Read and not on the line (**[M]** `b348bb33`, `git grep` of the old pilot, kept-reader and `TempFolder` spellings over
`*.md`): `research/module_tests.md` and `research/PLAN_model_arms.md` print none of them; `research/module_model_arms.md:1308`
and the other IMPLEMENTED model-arms plans that name `testsToDelete-modelarms-…` record what was built then and stay.
*Was (epoch 10):* `tests/Bench.Tests/ModelArms/**`.
**Done when.** Rows E6-i (S4's half) and E6-l met by their checks; the brief's scenarios each RED first and RED again
quoted; the ModelArms namespace green with its time recorded; the whole suite's FAILED list equal to the base's; no
growth of `/tmp/testsToDelete-*modelarms*` across a namespace run; `TheAdaptedCopyTests` green (no manifest target
touched); `git diff --name-only <base>..HEAD` within the *Files* above; `plan-lifecycle.mjs` clean. *Was (epoch 10):* No
`legacy-name`, `raw-temp` or unmarked `raw-worktree` finding under `ModelArms/` (kept: row E6-i's check holds it, in both
guards and with `owner-blind`).

### S5 — The rest of `Bench.Tests`, the fakes and the product seams (E6-k, E6-b seam)

**What.** The private temp classes of § 3 become wrappers over the copied `OwnedDir` (D5); `DatedGitRepo` puts its
repository and its scratch root in one owned folder; the 8 never-removed sites (E6-k) move into an owned folder with
`Dispose`; the path-only lines read `TempDir.Root` (the same string as `Path.GetTempPath()`; coai's shape G); the fakes
follow D6; the product seams follow D4; `HarvestGatesTests` and `SolutionSignalsTests` pass `DatedGitRepo.ScratchRoot`.
The `.ndjson` fixture line (§ 3): the brief reads whether any test asserts on that line's text; if none does, the quoted
command in the data is changed so it no longer spells the two words together, and the test is re-run; otherwise it stops
and asks.
*Epoch 11 (the parent's § *Epoch 11*, E11-3 to E11-5):* (a) **Shape G, bounded.** `TempDir.Root` is clean in both
guards only because neither traces what a path is used for: a blind spot, not a proof. A "path-only" line reads it only
when NOTHING creates or keeps that path, neither the test nor the product it calls (an `--artifact-root` the product
writes into is created); the brief reads each such line's callee, and a path that is created goes into an owned folder
(`TempDir.For`, `OwnedDir`); never for retained output (row E6-l). (b) **The fake CLI on the probe runner's path**
follows D6 as the epoch-11 amendment restates it (the home reached through the model id; no product file). (c) **The
copied C# guard's lines here** (**[M]** `b348bb33`): it reads 75 findings under `tests/` outside `ModelArms/` and
`Leftovers/` (72 `raw-temp` and 1 `owner-blind` in `Bench.Tests`, 2 `raw-temp` in the fakes), the Node guard 73 (70
`raw-temp` and 1 `raw-worktree`, the `.ndjson` line, in `Bench.Tests`; 2 in the fakes). C#-only:
`Gate/Driver/GateReplyParserTests.cs:125`, `:132` (`"/tmp"` in sample text: rewritten as S4's literals, never exempted)
and `Probes/ProbeContractsGuardTests.cs:19` (`owner-blind`: the literal `ProbeCellReportDto.LatestState` holds `testSt`,
a piece of the prefix word, case-insensitive; the line's literal is rewritten so no literal spells the piece, for example
through `nameof`, and the product's member names do not change; `owner-blind` takes no exemption). Node-only: the
`.ndjson` line (the copy does not read `.ndjson`). (d) **Shape G, classified** (the parent's § *Epoch 11* item 3.3, O-c, by
reading the callee of each of the copy's 75 lines here, **[R]**): 29 create nothing and may take `TempDir.Root` —
`Authoring/AuthoringPassTests.cs:280`, `:292`; `Cli/GateAaCommandTests.cs:214`; `Cli/GateAssessCommandTests.cs:48`;
`Cli/GateDriverCommandTests.cs:32`; `Engines/FilesystemEngineTests.cs:184`; `Engines/QlnEngineTests.cs:301`;
`Engines/QlnEngineLiveTests.cs:174`; `Gate/Driver/ProductPinReaderTests.cs:72`;
`Infrastructure/FileSystemGateArtifactStoreTests.cs:237`, `:238`, `:246`; `Infrastructure/GitCheckoutLocalSourceTests.cs:93`;
`Infrastructure/ProcessRunnerCancellationTests.cs:28`; `Infrastructure/ProcessRunnerStderrSinkTests.cs:22`;
`Infrastructure/ProcessRunnerTests.cs:15`, `:26`, `:39`, `:56`, `:75`, `:92`, `:107`, `:138`; `Models/AssessorArgvTests.cs:78`;
`Scouts/ScoutLaunchProcessTests.cs:100`, `:107`; `Suites/AnchorCheckTests.cs:103`, `:110`; `Trace/TracePortTests.cs:257`.
2 are not provable here and take an owned folder: `Cli/GateDriverCommandTests.cs:33` (`--artifact-root` = the temp root)
and `Models/CliAgentLiveTests.cs:83` (a real CLI's working directory). The other 41 create a folder or a file and take an
owned folder; 3 are not paths (above).
**Scenarios.** RED first for each of the 8 never-removed sites: a test lists the temp root before and after the class's
tests and finds the folder left; green after. D4: a product call with the old signature still makes
`<temp>/bench-scratch/<uuid7>` (pinned); a test's worktree is under its owned folder and `git worktree list` in the
repository shows it inside. A killed `DatedGitRepo` user is collected by the next run (case 15's real-world twin).
**Files.** `tests/Bench.Tests/**` outside `ModelArms/` and `Leftovers/`; `tests/FakeCli/Program.cs`, `tests/FakeCoai/Program.cs`;
`src/Bench.Infrastructure/Git/{ScratchWorktree,HarvestGates,SolutionSignals}.cs`; `research/module_tests.md`; the plan
records (§ 6's first paragraph).
**Done when.** No `raw-temp` or `raw-worktree` finding in `tests/` outside `ModelArms/` except the inline-exempt lines the
brief lists by file and reason; the suite is green; the product's own worktree names are unchanged (the pin test).
*Epoch 11:* read in BOTH guards, against the base `b348bb33`: under `tests/` outside `ModelArms/` and `Leftovers/` the
Node guard 73 → 0 and the C# guard 75 → 0 (every class, `owner-blind` included), except the exemptions the brief lists
(§ 6's first paragraph); on a later base, that base's count there. And a whole-suite run leaves no `bench-fake-cli` in the
system temp root (today `/tmp/bench-fake-cli` exists, empty: the probe driver removes each model's home, never the parent;
**[M]** 2026-11-17), listed before and after on the PR.

### S6 — `spikes/policy-ab-quick` and E6's other spike sites (Node, and the scripts the C# copy reads)

**What.** The 25 `raw-temp` and 5 `legacy-name` lines in `spikes/policy-ab-quick/test/` (12 test files; names
`testsToDelete-pab-…` and `pab-…`, removed in `finally` today) use the shipped helper, `ownedDir(t, what)` from
`ownedFor({ token: 'bench' })`, as `spikes/lang-bench` does. The other spike findings that D7 gives E6 are listed in the
brief by file and line, each with its class and the reason it is E6's and not the followups plan's.
*Epoch 11 (the parent's § *Epoch 11*, E11-6 to E11-8):* S6 reads BOTH guards. Its scope is every finding of either
guard in `spikes/` at its base that is not on a line the followups plan's F1 or F2 names (F1:
`spikes/lang-bench/lib/exec-probe/lines.mjs:38`, `probe.mjs:51`, `test/exec-layer-isolation.test.mjs:58`; F2:
`spikes/minigit-bench/test/helpers.mjs:30-37`, `fake-home.mjs:26`, `lib/cache-snapshot.mjs:42-45`,
`spikes/lang-explore/test/commands.test.mjs:64-65`; none of the findings below is on one of them). **[M]** `b348bb33`:
the Node guard, 85: `policy-ab-quick` 30 (25 `raw-temp`, 5 `legacy-name`), `lang-bench` 8 (5 `legacy-name`, 3
`raw-temp`), `lang-explore` 6 (`owner-blind`), `minigit-bench` 41 (21 `legacy-name`, 18 `raw-temp`, 2 `owner-blind`). The
copied C# guard, 14, all in `spikes/lang-bench`: 10 `raw-temp` (the nine `.py` `mkdtemp(` lines of claim C6 and
`test/hello/prove.sh:7`, which the Node guard also reports), 3 `legacy-name` (`test/py/box_support.py:187`, `:192`, an
owner segment built at run time from `os.getuid()`; `test/py/test_rs_abort.py:98`, a name never made), 1 `owner-blind`
(`test/py/test_burner.py:39`). A name built with a run-time owner segment (`testsToDelete-u${uid}`,
`f"testsToDelete-u{…}"`) is red in both guards: it goes through the shipped helper, or takes a `legacy-name` exemption
the brief lists that names the run-time owner. Each site is classified by lifetime first (the epoch-11 amendment's
lifetime rule). The `.py` units are started by Node test files under `python3 -I -m unittest`
(`spikes/lang-bench/run-tests.mjs:7-8`); the brief names how its `.py` changes are run within this machine's rule for
Python (a sandbox). *Blocked until then* (the parent's § *Epoch 11* item 3.3, O-e): the `.py` units were not run during
the re-review; S6 STOPS before its plan gate unless its brief names the sandbox (a `wslc` image or an owned venv) its
`.py` RED → GREEN runs in, and the owner is asked if none is available.
**Scenarios.** RED first: the guard's findings for those files before; zero after. A `policy-ab-quick` test killed
mid-run leaves a claimed folder that the shipped `sweepLeftovers` collects at the next start (the suite gets a start
sweep if it has none). *Epoch 11:* `spikes/minigit-bench/run-tests.mjs`, the runner the `policy-ab-quick` tests run
through (`research/module_tests.md:183-185`), has none (**[M]** `b348bb33`: `git grep -n sweepLeftovers -- spikes` finds
only `lang-bench`), so S6 adds it there in lang-bench's shape (`spikes/lang-bench/run-tests.mjs:19-26`), reuse-first.
**Files** (added by the epoch-11 amendment). `spikes/policy-ab-quick/test/**`; the files of the findings above
(`spikes/lang-bench/runner/run_samples.py`; `spikes/lang-bench/test/{exec-layer-cleanup-stop,exec-layer-parent,exec-probe-stop,preflight-lang,sweep}.test.mjs`,
`test/hello/prove.sh`, `test/py/{box_support,test_burner,test_rs_abort,test_rs_kill,test_rs_launch,test_run_samples}.py`;
`spikes/lang-explore/test/quick-heldout-{key,lock,round2,seal}.test.mjs`;
`spikes/minigit-bench/test/{agent-box-fs,agent-box,bare-collect-allow,bwrap,capture-session,fake-home,heldout-run,ledger,preflight,reuse-scan,secrets-opt-in,secrets}.test.mjs`);
`spikes/minigit-bench/run-tests.mjs` (the start sweep); `research/module_tests.md`; the plan records. And, for a test
helper a listed test imports, the test folders they live in: `spikes/lang-bench/test/**`, `spikes/lang-explore/test/**`,
`spikes/minigit-bench/test/**`, never the files F1 and F2 name (`helpers.mjs`, `fake-home.mjs`: the followups plan's S2);
the brief names each such helper with the line that needs it.
**Done when.** The listed files have no finding; the spike's suite is green. *Epoch 11:* in BOTH guards, against the
base `b348bb33`: in `spikes/`, the Node guard 85 → 0 and the C# guard 14 → 0, except the exemptions the brief lists
(none for `owner-blind`); the suites of `policy-ab-quick`, `minigit-bench`, `lang-bench` and `lang-explore` green as
`research/module_tests.md` runs them.

### S7 — Both guards on, the CI step, the docs (E6-g)

**What.** Switch on the copied repository guard test (`LeftoversRepositoryGuardTests`: `FilesScanned > 0`, no findings).
The copied C# guard also reads `.cs` under `tools/` as product code (the `Source` kind, as `src/`), by one manifest
operation on `LeftoversGuardFiles.cs`, so a temp or prefix spelling added under `tools/model-arms/` is judged like
product code; that folder's tests live in `tests/Bench.Tests/ModelArms/`, which the guards read as tests. That operation
changes a discovery boundary (claim C10, **proposed**): S7's brief names it and observes it, without (a plant in
`tools/model-arms/X.cs` not read) and with (read as `Source`), with C4's replacement plants, before S7's plan gate. Judged
like product code means the `Source` kind's classes only: a raw temp spelling such as `GetTempPath()` there is expected
never red (**[R]**, claim C4), a limit of both guards that `research/module_tests.md` states.
Add a CI step to `build-test`: `node .agents/conventions/tools/leftovers-check.mjs --profile node --token bench --identity
"the measuring harness"`. Settle D8's two lines. Rewrite `http/README.md:32-33` to the owner form with `--rm`. Record the
machine listing of D11. Close the docs: `research/module_tests.md` (the guards, what they do not read — § 1's E6-g row
and its table *E6-g by directory*, with the limits by file kind — and the limits of D3); `todo/README.md`.
**Scenarios.** RED again for each guard: a planted `Path.GetTempPath()` in a `tests/` `.cs`, a planted `mktemp -d` in a
`tools/` `.sh`, a planted `worktree add` without a marker, an empty scan (a wrong root) —
and, in a `tools/model-arms/` `.cs` after the operation, a plant the `Source` kind reads (claim C4: an `Owned.TempRoot`
reference outside the helper's folder, expected `raw-temp` in the C# copy; the bare prefix word, expected `owner-blind`;
**[R]**, red or not, the brief records which, and a plant red in no configuration becomes a NOT-read statement, never a
break-it) — each red, then removed; and the plant of each row of
*E6-g by directory*, red in each guard that reads that kind (the C# cells observed before S7's plan gate, claim C3). A
`GetTempPath()` in a `src/` or `tools/` `.cs` is not a break-it: it is expected never red in either guard (claim C4), and
the brief records that observation as the limit. *Was:* "a planted `GetTempPath()` in a `tools/model-arms/` `.cs` (read
as product code: red only in the forms the `Source` kind judges, the brief records which)", a plant expected never red;
the plant count stays (the parent's § *Epoch 10* item 4.3). Reuse is not a guard plant: no guard reads it
(`.WithReuse(true)` in a `.cs` is `[]` in the Node guard, **[M]** `46e27108`, § 1 E6-d; the C# copy has no reuse
spelling, **[R]**). In its place, E6-f2's fixture test runs RED again with reuse enabled through the builder,
and is green with the fixture as it is. Both guards green on the head.
**Files** (added by the epoch-11 amendment, the parent's § *Epoch 11* E11-9). `tests/Bench.Tests/Leftovers/template-manifest.json`
(S7 deletes the `Explicit` operation, whose reason says "S7 deletes this operation", and adds one on
`LeftoversGuardFiles.cs`, claim C10) and those two targets, `tests/Bench.Tests/Leftovers/LeftoversRepositoryGuardTests.cs`
and `LeftoversGuardFiles.cs`; `.github/workflows/ci.yml` (`build-test`: the Node guard step; Node and the mount's `npm ci`
are there since F42-c); `research/local-model-coding/harness/t-lib.sh` and `part6/t-lib.sh` (D8); `http/README.md`;
`research/module_tests.md`; the plan records (§ 6's first paragraph).
**Done when.** E6's Definition of Done (§ 11) holds. *Epoch 11, the counts it starts from* (**[M]** `b348bb33`, less S4's,
S5's and S6's): the Node guard 2 (`--profile node`) and 2 (default profile), the C# guard 2: the two D8 lines, `raw-worktree`,
in each; after S7, 0 in each, with `FilesScanned > 0`.

## 7. Test plan

- Build and run as `.agents/PROJECT.md` says: `node .agents/conventions/tools/low-priority.mjs dotnet build
  dew_flow_benchmark.slnx -c Release -m:4`, then the test executable (never `dotnet test`), through the same wrapper.
- Every defect: RED (watched failing for the right reason), GREEN, then RED again by putting the defect back once.
- Kill tests use a child process and SIGKILL; they run on Linux and macOS; on Windows they skip with a reason where the
  platform cannot deliver the signal the same way.
- The Docker-backed proofs (S3) run on `ubuntu-latest` in CI. They are "to observe" (state **proposed**) until that run
  exists, and the PR quotes the run.
- A row is **observed** only when its own check ran on a candidate that passes AND a counterexample that fails, at this
  repository's pins (§ 1). A marker or an example spelling this document writes is run through BOTH guards, the shipped
  Node guard and the C# copy (the parent's § *Epoch 9* item 8.1).
- Every claim of § 1 (*The claims*, C1–C10) is observed before the plan gate it names, with the six things the parent's
  § *Epoch 10* item 8.1 asks for, and again when one of its inputs changes; until then it is **proposed**, a reading of
  code says **[R]**, and the text stays open-ended. A plant names the configuration it is red in; an exclusion or a
  boundary is proved from both sides (without and with); a file-scope line is a list of named paths, proved complete by
  the PR's own checks from a clean checkout (§ 6 *Before S3*).
- A Docker query that fails or times out fails its test; it is never read as "absent" (E6-d, E6-f2).
- The spike suites (S1, S6) run by hand, as today: `node spikes/minigit-bench/run-tests.mjs`, and the `policy-ab-quick`
  tests through that runner with their paths (`research/module_tests.md:185`).

## 8. Growth

| Surface | Projected size | Who retires it | When interrupted |
|---|---|---|---|
| Owned test folders in the temp root | at most the suite's open scopes at one time (one per running test class, about 8 threads); each removed by its `Dispose` | the test; after a kill, the next run's sweep (rule §4) | the next run's start-of-run sweep; the janitor |
| The fixture's container | one per test process, `postgres:17-alpine` with up to 29 per-test databases; size **to observe** in S3 (`docker system df -v` before and after one suite run, quoted on the PR) | `DisposeAsync`; Ryuk after a kill; the container pass (D9) | Ryuk within its window; else D9 at the next start |
| Worktrees in tests | inside their owned folder (D4) | with the folder | with the folder (E6-c) |
| Claims (`<folder>.owner`) | one small file per owned folder | with the folder | the sweep judges an orphan claim like any claim |

Nothing here is kept forever. The product's `bench-scratch/`, checkout cache and AppHost volume are not created by tests
and are out of this table.

## 9. Boundaries (`common/planning-docs.md` § *A boundary between two plans*)

| Item | Built by | The other's part |
|---|---|---|
| The fixes of F1 and F2 in `spikes/lang-bench/`, `spikes/minigit-bench/test/`, `spikes/lang-explore/test/` | `todo/PLAN_bench_test_leftovers_followups.md`, S1 and S2 | E6 lists them (§ 3), its guard reads them, its S7 waits for them (D7) |
| The other spike findings of E6's guards; `spikes/minigit-bench/lib/heldout-run.mjs` (E6-h) | E6 (S6, S1) | the followups plan does not touch them |
| The rule, the Tokens table, the janitor, the Node guard and helper | `dew_flow_conventions` (E1) | E6 reads them through the mount and never edits them here |
| The `container-scoped` exemption kind in the rule and the Node guard (added 2026-11-16) | `dew_flow_conventions`, the parent's follow-up F41 | E6 writes the marker only after its rules pin reads F41's release (F41-b, § 5) |
| The same kind in the C# template, and N1–N5 (added 2026-11-16) | `dew_flow_mcp`, the parent's follow-up F42 | E6 re-syncs its copy to F42's commit (F42-c, § 6 *Before S3*), never edits the copied files by hand |
| A shared reading of Ryuk turned off through `SetEnvironmentVariable`, and of reuse (added 2026-11-16) | nobody yet: the parent's F43 is a pointer | E6 proves Ryuk on and reuse off by its own tests (E6-d, E6-f2) |
| The C# helper | `dew_flow_mcp` (E3) | E6 copies it at one commit (D2), re-synced once to F42's commit (F42-c); a finding about the template goes to the parent as a note |
| `SweepLock` (F29) | coai (E5) | E6 copies it as its own file (D3) |
| rag_qln's collections: the test corpora, and the `bench` namespace benchmark reads | rag_qln (E7, its S2 for test corpora) | benchmark's tests make and delete no collection in a qln store (**[M]** `git grep -n -i "CollectionPrefix\|DropCollection\|/collections/" -- tests src hosts` → nothing); the product's `bench` namespace is the product's, out of E6. E7's stories carry the same row |

Disjoint otherwise: E6 changes nothing in another repository. The followups plan's row (`:63`) states the same split
from its side.

## 10. Reuse (`common/reuse-first.md`)

The helper, sweep, audit and guard are copied, not written (D2). `SweepLock` is coai's (D3). The Node side uses the
shipped `owned.mjs` (S6). The `~` spelling precedent is coai's. The only new code: the container pass (D9; no shipped C#
or Node container pass exists for a repository's own tests: the janitor is host-wide and not a test sweep), the fixture's
three lines, the product overloads of D4, the wrappers of D5 and D6.

## 11. Definition of Done

- [ ] E6-a to E6-l (E6-f as E6-f1 and E6-f2; E6-l added by the epoch-11 amendment): each check green, with its observed result recorded on the PR that met it; every row's state is **observed**.
- [ ] The adapted copy's check is green; the manifest names mcp's commit `53f65130` (F42; `21553ce` until F42-c); F29 is closed by `SweepLock`; F31 is stated by its true condition.
- [ ] F42-c stayed within § 6 *Before S3*'s named paths, by row F42-c's checks (1)–(4b) from a clean checkout.
- [ ] Every claim C1–C10 of § 1 was observed before the gate it names, and its result is written where the claim stands; the limits it found (C4's `GetTempPath()` in a `Source`-kind `.cs` among them) are in `research/module_tests.md`.
- [ ] A killed run's owned folder, its worktree (case 15) and the fixture's container are collected by the next run that starts the fixture (the container) or the next run (folders and worktrees) (S2, S3).
- [ ] The fixture's container is named in the owner form, labelled, Ryuk on, never reused (proved at run time, E6-f2); the per-test databases carry the `container-scoped` marker, and both guards accept it (E6-f1).
- [ ] No `raw-temp`, `raw-worktree`, `legacy-name` or `owner-blind` finding in either guard except the inline exemptions the stories list by file and reason.
      *Epoch 11:* each story's brief lists its exemptions (file, line, class, reason) before its plan gate, and the PR that
      adds them copies the list here, under this line; `owner-blind` and `ryuk-off` take none (§ 6's first paragraph).
- [ ] Output that must outlive the run is in the folder `BENCH_PILOT_OUT` names, never in the temp root and never prefixed (E6-l).
      *The documented exemption of the parent's Definition of Done, item 1, read for its epoch 11* (its § *Epoch 11* item
      4.3; the owner approves it with epoch 11): exactly two tests, `tests/Bench.Tests/ModelArms/M2PilotScaffold.cs` and
      `tests/Bench.Tests/ModelArms/M3PilotScaffold.cs`, each `[Fact(Explicit = true)]`; their output, the unprefixed
      `bench-m2-<what>-<id>`, `bench-m3-<what>-<id>` and `bench-m3-manifest-<id>.json`, made only under `BENCH_PILOT_OUT`,
      never in the temp root, collected by no sweep, removed only by the pilot's named teardown (M2: the two printed paths;
      M3: its manifest, T1–T5); verified by this list and by row E6-l's test that each scaffold refuses, naming
      `BENCH_PILOT_OUT`, before it makes anything when the variable is unset. No other test, script or CI step uses it.
- [ ] CI's `build-test` runs both guards; each is red on a scan of nothing.
- [ ] `research/module_tests.md` describes the helper, the sweeps, the guards, what they do not read, and the limits.
- [ ] The machine listing of D11 is on the S7 PR; nothing was removed by hand without the owner's word.
- [ ] This document is promoted with the parent, with its deviations.

## 12. Questions and prerequisites

1. **The followups plan's S1 and S2 before E6's S7 (D7).** That plan is open and has no build date. If it is still open
   when S1 to S6 are merged, the owner decides: wait, or move its F1/F2 sites to E6 (a boundary change on both sides).
2. **The open-PR cap.** This repository has one open PR (#103, this document's). S4 to S6 may be built in parallel once S3
   has merged, and are opened at most three at a time.
3. **The owner's Windows machine (merge condition (c)).** Its barrier is closed: it was never scanned, and the owner's
   2026-11-04 statement covers only E2's and E5's repositories (`dew_flow_conventions · research/security/machines.md`,
   § *The owner's Windows machine*). Before S2, the first producer, merges, the owner either states that the machine does
   not build or test this repository, or the machine gets the scan and the checklist there. The parent's E6 entry names
   the same condition ("P1 only if the owner's Windows machine builds or tests benchmark").
   *Answered (read 2026-11-16):* the owner stated that the machine builds and tests neither this repository nor rag_qln
   (`dew_flow_conventions` #166 `113e9c2`; the parent's § *Epoch 9* item 1, merge condition (c)).
4. Not prerequisites: F25 to F28, F32, F43; the private sibling's mirror (F26).
   *Epoch 11, a todo of this repository, not built by E6's stories (the parent's § *Epoch 11* item 4.5):* the `/tmp/.git`
   finding. `RootFacts.GitTopOf` (`tools/model-arms/ModelArms/Adapters/RootFacts.cs:19-23`) takes the nearest
   ancestor-or-self holding a `.git` file or folder, so ONE `/tmp/.git` makes every experiment and scout root under `/tmp`
   refuse as "inside the git checkout /tmp", as F42-c's two whole-suite runs met
   (`research/PLAN_test_leftovers_E6_F42c_brief.md:749-750`). Checked by S4's brief § 12 at `b348bb33`: every `git init`
   argv (40 sites), every `.git` written by hand (15), the temp root as a working directory, the spikes' Node tests: no
   cause in this repository. Not checked: other repositories' tests and other processes on the shared machine; when the
   entry appeared. S4 does not change the exposure (its owned folders are in the same `/tmp`). To do: name a
   `<temp root>/.git` loudly at the start-of-run sweep instead of letting each test refuse, or run the suite under a
   run-scoped temp root outside `/tmp`; and find the creator on the machine.
5. **Prerequisites of S3, added by the parent's epoch 9:** F41-b (the rules pin at F41's release) and F42-c (the copy
   re-synced to F42's commit), § 5. Neither is a story of this document: F41 is the conventions repository's and F42
   mcp's; here, F41-b is a pin bump PR of F41's release and F42-c a re-sync PR (§ 6, *Before S3*).

## Plan gate (coai, 2026-11-12)

Round 1 on `6428c931`: **proceed**, 2 of 2 reviewers answered, 5 findings. Accepted 3: D9's `docker` calls are an
executable with an argument array, a 30-second timeout and a whole-tree kill; S2 names the start-of-run hook (coai's
assembly-level attribute, once per process) and pins it with a test; S2 copies coai's two-sweeps race test for Windows.
Rejected 2: a check that the fixture never points at an external Postgres (it has no such path:
`PostgresFixture.cs:35` is always the started container's string); a prefix check of the fakes' folder variable inside
the fakes (the parent sets it; the token stays written once, rule §2).

Code round 1 on `1eaf3799` (the document is this PR's whole diff): **proceed**, 8 of 8 reviewers answered, 10 findings.
Accepted 4: the C# guard reads `tools/` `.cs` as product code (S7); every thread waits for the start-of-run sweep (S2);
`docker inspect` only when `docker ps` listed ids, in batches of at most 100 (D9). Rejected 6: the `<repo> · <path>`
citation form (it is the rule's own example); the status line (line 3, the lifecycle check is clean); the goal after
the rows (the parent orders the rows first); the root seam (`ScratchWorktree` already has it); the assembly hook
(rule §4 and coai's reviewed shape); the docker CLI against the Testcontainers client (both read `DOCKER_HOST`).

## How to repeat the measurements

From the worktree root, at `7d795024` with the mount at `d23a2ea` (`npm ci --ignore-scripts --prefix .agents/conventions` first):

```bash
node .agents/conventions/tools/leftovers-check.mjs --repo . --token bench --identity "the measuring harness"          # 3 findings
node .agents/conventions/tools/leftovers-check.mjs --repo . --profile node --token bench --identity "the measuring harness"  # 209
git grep -n "WithName\|WithLabel\|WithReuse\|Ryuk\|TESTCONTAINERS_RYUK_DISABLED" -- tests                          # nothing in the fixture
git grep -n "DROP DATABASE" -- tests                                                                                  # nothing
git -C <dew_flow_mcp> show 21553ce:tests/Mcp.Tests/Leftovers/LeftoversSweep.cs | sed -n '165,180p;468,482p'           # E6-c
```

The counterexamples of E6-b and E6-g were planted in a throw-away copy (`git archive HEAD`, the mount copied in,
`git init`), never in this worktree.

The epoch-9 revision's measurements (2026-11-16), at `46e27108` with the mount at its pin `4c9a684`, read-only; the
`scanText` and `isScanned` calls touch no file:

```bash
node .agents/conventions/tools/leftovers-check.mjs --repo . --profile node --token bench --identity "the measuring harness"  # 209; none in PostgresFixture.cs
node --input-type=module -e "const g = await import('./.agents/conventions/tools/leftovers-check.mjs');
  const f = 'tests/Bench.Tests/Infrastructure/PostgresFixture.cs';
  for (const t of ['// testsToDelete-exempt: container-scoped — the per-test databases live and die with the PostgresFixture container',
                   '// testsToDelete-exempt: container-scoped — the database lives and dies with the PostgresFixture container',
                   'var c = new PostgreSqlBuilder(\"postgres:17\").WithReuse(true).Build();',
                   'Environment.SetEnvironmentVariable(\"TESTCONTAINERS_RYUK_DISABLED\", \"true\");'])
    console.log(JSON.stringify(g.scanText(f, t + '\nvar x = 1;')));                  // one exemption, one exemption, [], []
  for (const p of ['spikes/minigit-bench/lib/heldout-run.mjs', 'tools/x.mjs', 'spikes/policy-ab-quick/test/x.mjs',
                   'spikes/policy-ab-quick/test/x.test.mjs', 'tools/x.sh', 'tests/FakeCli/Program.cs', 'tools/model-arms/x.cs'])
    console.log(p, g.isScanned(p, 'node'));"                                          # false, false, false, true, true, true, false
strings -el ~/.nuget/packages/testcontainers/4.16.0/lib/net10.0/Testcontainers.dll | grep -i -E 'reuse|^TESTCONTAINERS_'  # reuse messages, no reuse key
git grep -n -i windows -- .github/workflows                                                                            # nothing
git grep -n "mkdtemp" -- '*.py'                                                                                        # 9 lines
git grep -n "container-scoped" 4c9a684 -- tools/   # in dew_flow_conventions: fixtures and the rule test only, not the guard
```

The epoch-10 amendment's measurements (2026-11-16), at `83f166d6` with the mount at its pin `b1746499`
(`npm ci --ignore-scripts` in `.agents/conventions` first), read-only:

```bash
node --input-type=module -e "const g = await import('./.agents/conventions/tools/leftovers-check.mjs');
  const f = 'tests/Bench.Tests/Infrastructure/PostgresFixture.cs';
  for (const t of ['// testsToDelete-exempt: container-scoped — the per-test databases live and die with the PostgresFixture container',
                   '// testsToDelete-exempt: container-scoped — the per-test databases are kept by the PostgresFixture container'])
    console.log(JSON.stringify(g.scanText(f, t + '\n    public async Task<string> NewEmptyDatabaseAsync(string name)')));  // [], one exemption
  for (const p of ['src/X.cs', 'tools/model-arms/X.cs']) console.log(p, g.isScanned(p, 'node'));"                       # false, false
node -e "const m = require('./tests/Bench.Tests/Leftovers/template-manifest.json'); console.log(m.commit, m.entries.length)"  # 21553ce5…, 53
sed -n '32,46p;79p' tests/Bench.Tests/Leftovers/TheAdaptedCopyTests.cs                                                  # BenchOwn; the pinned commit
grep -n -E '^  [a-z-]+:|Bench.Tests|npm ci' .github/workflows/ci.yml                                                     # only build-test runs Bench.Tests; no npm ci there
# in dew_flow_mcp, per path P of the nine changed and two new files:
git rev-parse 21553ce:tests/Mcp.Tests/Leftovers/P 53f65130:tests/Mcp.Tests/Leftovers/P
git show 21553ce:tests/Mcp.Tests/Leftovers/LeftoversAuditTests.cs | grep -c 'mcp tests:'                                 # 6 (7 at 53f65130)
git show 53f65130:tests/Mcp.Tests/Leftovers/LeftoversGuard.cs | sed -n '78p;84p;89p;91p;93p;204,219p;297,301p'           # C4–C8, read
```

The epoch-11 amendment's measurements (2026-11-17), at `b348bb33` with the mount at `b1746499` (`npm ci --ignore-scripts`
in `.agents/conventions` first), read-only, from a worktree whose tree equals `b348bb33` and holds a Release build:

```bash
node .agents/conventions/tools/leftovers-check.mjs --repo . --profile node --token bench --identity "the measuring harness"  # 209
nice -n 19 ./tests/Bench.Tests/bin/Release/net10.0/Bench.Tests --explicit only --filter-class "*LeftoversRepositoryGuardTests" --no-ansi
                                         # fails listing 173: 115 raw-temp, 22 raw-worktree, 18 legacy-name, 18 owner-blind
# per folder and class, from each list: ModelArms/ Node 49, C# 82; tests/ outside ModelArms/ Node 73, C# 75; spikes/ Node 85, C# 14
git grep -n -E "testsToDelete-(m2|m3)|modelarms-reader|testsToDelete-modelarms" -- '*.md'     # the docs of S4's Files
git grep -n -i sweepLeftovers -- spikes                                                         # lang-bench only
node --input-type=module -e "const g = await import('./.agents/conventions/tools/leftovers-check.mjs');
  const f = 'tests/Bench.Tests/ModelArms/M2PilotScaffold.cs';
  for (const l of ['var output = System.Environment.GetEnvironmentVariable(\"BENCH_PILOT_OUT\");',
                   'var target = Path.Combine(output, $\"bench-m2-target-{id}\");',
                   'var path = Path.Combine(output, $\"bench-m3-manifest-{id}.json\");',
                   'var target = Path.Combine(Path.GetTempPath(), $\"bench-m2-target-{id}\");'])
    console.log(JSON.stringify(g.scanText(f, l + '\nvar x = 1;').map(x => x.cls)));"     # [], [], [], [raw-temp]
```
