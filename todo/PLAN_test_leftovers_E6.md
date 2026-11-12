# PLAN — Test leftovers, E6: benchmark's tests name, own, remove and collect what they make

> Status: **plan only, nothing implemented yet, 2026-11-12.** Written new against the shared rules at `d23a2ea` (this
> repository's pin) and `dew_flow_mcp`'s merged C# helper; the 2026-10-16 draft of this file (`08a87430`) was read as
> material only. Stories of epic E6 of `dew_flow_conventions · todo/PLAN_test_leftovers_rollout.md` (§ *Epics*, E6;
> § *Epoch 8* item 3, rows E6-a to E6-h). Scope: `tests/` (Bench.Tests, FakeCli, FakeCoai), `spikes/policy-ab-quick/test/`,
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

## 1. The conditions, observed — rows E6-a to E6-h (the parent's item 3, carried and observed at the pin)

| # | Condition | Implementation (file) | Invocations | Executable check | Prepares / does not prove | Observed at `7d795024` |
|---|---|---|---|---|---|---|
| E6-a | the boundary on both sides | `todo/PLAN_bench_test_leftovers_followups.md:63` and § 9 here | — | the row is on `main` in both | — | **[M]** merged, #177 `562cb4bd`. Met |
| E6-b | a worktree a test makes is owned, and the guard's boundary is stated | the worktree lives INSIDE an owned folder with the repository it belongs to (rule §1, last item): the product seams of D4 take a root; a `worktree add` a TEST writes by hand stays in its owned folder and carries one inline `raw-worktree` exemption naming that folder | `Bench.Tests`; the guards | the C# guard and the Node guard on the head; one planted test-file `worktree add` without the marker is red | The guards read the spelling, never the folder | **[M]** a compliant `git worktree add --detach --lock --reason "$RECORD" testsToDelete-u1001-bench-x HEAD` is `raw-worktree` in a `.sh`, in a `.cs` under `tests/` (Node guard, `--profile node` only) and in a `.test.mjs`; the comment `testsToDelete-exempt: raw-worktree — <reason>` on the line above clears each. **Not read:** the same line in a plain `tools/*.mjs` (not a test file). **[M]** the template's C# guard matches `"worktree", "add"` and the words `worktree add` (mcp `LeftoversGuard.cs:91`), only outside `src/` (`:199-203`) |
| E6-c | a killed child's worktree is collected by a NAMED implementation | the copied C# sweep's folder pass, on the owned folder that holds the clone and its worktree (sweep case 15, § S2) | the next run's start | sweep case 15: a dead-claimed owned folder with `repo/` and a linked `scratch/<id>/` is removed whole | The template has no worktree pass (mcp `LeftoversSweep.cs:3`); a worktree whose clone is OUTSIDE the folder is not seen at all | **[M] by reading, not run:** `HasGitFile(dir)` (`LeftoversSweep.cs:470-480`) tests only the candidate's OWN `.git` as a regular file, so `:171-175` (`other clone`) does not fire for the owned folder; the folder is quarantined and removed by `Removal.Quietly` → `Directory.Delete(recursive)` (`Removal.cs:40-54`), with the clone's `.git/worktrees/<id>` inside it. **To observe** in S2: case 15 RED first, then green |
| E6-d | the fixture's container: three separate proofs | `tests/Bench.Tests/Infrastructure/PostgresFixture.cs` | `Bench.Tests` | (1) Ryuk on: a killed child's container is gone; (2) the owner label `testsToDelete.owner` on the builder, read by the container pass; (3) no `.WithReuse(` | Ryuk collects by its session, not by owner; one test cannot stand for the other two | **[M]** the builder (`:27-33`) has `WithDatabase`, `WithUsername`, `WithPassword`, `WithCommand` only: no `WithName`, `WithLabel`, `WithReuse`, `WithCleanUp`; nothing in the tree sets `TESTCONTAINERS_RYUK_DISABLED` or a `testcontainers.properties`; `Testcontainers.PostgreSql` 4.16.0 (`Directory.Packages.props:41`). **To observe** in S3: the three proofs on `ubuntu-latest` (no `docker` CLI on the author's machine) |
| E6-e | the copy and its limits | the adapted copy with a manifest (D2), F29 read and fixed by copy (D3), F31 stated by its true condition (D3) | — | the copy's diff check (`TheAdaptedCopyTests`, coai's shape) | — | **[M]** mcp's folder at `21553ce`: 51 files, 8566 lines; since `197cc76` (the commit both earlier copies pin) only 8 files changed, all the mount path. The template has no `SweepLock` (F29 open there); coai carries one as its own file |
| E6-f | the per-test databases are exempt under rule §6 (b), said beside the fixture | `PostgresFixture.cs`: `NewEmptyDatabaseAsync` (`:96`, `CREATE DATABASE` `:108`) makes them in the fixture's container; the `container-scoped` marker beside it | `Bench.Tests` | a test: after the fixture is disposed, its container (by captured id) is gone, so every per-test database is gone; the guard reads the marker | The databases are never dropped one by one; the proof is the container's end | **[M]** no `DROP DATABASE` in the tree; 29 call sites in 16 test files; no marker, no name, no label. **[M]** the Node guard knows the `container-scoped` class (`leftovers-check.mjs`, 8 lines): the old draft's defect P6 is gone |
| E6-g | the guards read `tests/` and `tools/` | the copied C# guard (`.cs` under `tests/` and `src/`) and the shipped Node guard in CI (`--profile node --token bench --identity "the measuring harness"`) | `Bench.Tests`; CI | both green on the head; a scan of nothing is red (exit 2; the C# `FilesScanned > 0`); one planted raw temp in `tools/` is red | A spelling check | **[M]** no guard today (`git grep -l 'LeftoversGuard\|leftovers-check' -- tests tools .github` → nothing). The Node guard: default profile 3 findings, `--profile node` 209. A planted `mktemp -d` in `tools/x.sh` is `raw-temp` (both profiles). **Not read** by either guard: `.cs` in `tools/model-arms/`, `hosts/`, `spikes/` (C# guard: only `tests/` and `src/`; Node: only `tests/` as text); a plain `tools/*.mjs`; `*.py` in the Node guard |
| E6-h | the held-out runner's age removal of `score-tmp/*` is retired before any producer | `spikes/minigit-bench/lib/heldout-run.mjs` | the held-out tool's `sweep` command (`heldout-tool.mjs:105-112`), by hand only | its test: an old unclaimed folder under `score-tmp` is listed, not removed by age | — | **[M]** still there: `sweepLeftovers` `:93-113` removes every un-prefixed `score-tmp/<name>` older than 24 h (`:102-105`), no owner check; the test pins it (`heldout-run.test.mjs:154`: `removed: ['old-1']`) |

Rows added by this document, observed the same way:

| # | Condition | Observed at `7d795024` |
|---|---|---|
| E6-i | no test helper makes a LEGACY name (rule §1) | **[M]** 20 `legacy-name` findings outside the other plan's spikes: 15 in `tests/Bench.Tests/ModelArms/` (`TempFolder.cs:10` `Directory.CreateTempSubdirectory("testsToDelete-modelarms-")`, 112 `new TempFolder()` in 26 files; tokens `modelarms`, `briefs`, `m2`, `m3`, `x` are not rows of the Tokens table), 5 in `spikes/policy-ab-quick/test/` |
| E6-j | the CI job that runs `Bench.Tests` can read the mount | **[M]** `build-test` (`ci.yml:13-43`) checks out no submodule; the copied C# guard reads the Tokens table and starts the shipped `sweeper-scan.mjs` from the mount. `contract` (`:68`) and `plans` (`:120`) do check it out |
| E6-k | a test that makes a temp path removes it | **[M] by reading:** 8 creation lines are never removed — `Diagnostics/DailyRunFileSinkTests.cs:92`, `Engines/FilesystemEngineTests.cs:304`, `Engines/QlnEngineTests.cs:287`, `Authoring/VettingPassTests.cs:326`, `:339`, `Trace/TracePortTests.cs:166`, `:183`, `:268` |

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
the owner runs the suite on a Windows machine (`.agents/PROJECT.md` § *Commands*), so the limit is real here.

**Done when:** rows E6-a to E6-k are each met by their check; both guards are green over the head in CI; a killed run's
folders, worktrees and container are collected by the next run; the boundary sites of § 9 are clean by E6's own guard.

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
- **Databases.** 29 per-test `CREATE DATABASE` call sites in 16 files, never dropped, inside the fixture's container (E6-f).
- **Not read by any guard** **[M]**: `spikes/lang-bench/lib/conformance.mjs:152`, `lib/toolchain-install.mjs:152`,
  `spikes/minigit-bench/capture/capture-files.mjs:71`, `smoke-session.mjs:61`, `lib/preflight.mjs:33` (plain `.mjs`);
  `spikes/lang-bench/test/py/*.py` (`tempfile.mkdtemp`, with `finally`); `spikes/EvalLibrarySpike/Program.cs:12` (a console
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
targets) are listed in the check's own array, as coai does.

**D3 — The limits copied with the template, stated, and F29 closed by copy.** F29 (two sweeps at once on Windows can
report a removal they did not do, or restore a folder without its claim): this repository is run on Windows, so the
sweep takes coai's per-root named mutex, `SweepLock`, as a benchmark-own file (copied from
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

**D7 — The spikes, by the boundary.** The followups plan's stories S1 and S2 fix the sites its F1 and F2 name
(`todo/PLAN_bench_test_leftovers_followups.md` § *F-items*); E6 does not build them again. Every other finding of E6's
guards in `spikes/` is E6's (S6). E6's guard reads all of `spikes/` (the boundary row says so), so **S7 merges only after
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
label=testsToDelete.owner`, then ONE `docker inspect` for all ids, judged by the two keys of rule §4 (the label's record,
the name's owner, the caller's token; the record `gone`), age selects (10 minutes), removal by the captured id (`docker
rm -f -v <id>`). Never `--filter name=`. No `docker` binary, a stopped daemon, a non-zero exit or a 30-second timeout is
one reported line and nothing removed; the run goes on. Ryuk is the first collector; this pass collects what Ryuk could
not (a daemon restart, a reaper killed with the run).

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
| S3 | E6-d, E6-f: the fixture's name, label, Ryuk, no reuse; the container pass (D9); the `container-scoped` marker | Opus · Sonnet · Opus | S2 |
| S4 | E6-i, E6-b (by hand): `ModelArms/` — `TempFolder` and the legacy literals; the hand-made worktrees | Opus · Sonnet · Opus | S2 |
| S5 | E6-k, E6-b (seam): the rest of `Bench.Tests`, the fakes, the product seams of D4 | Opus · Sonnet · Opus | S2 |
| S6 | `spikes/policy-ab-quick` and the spike sites D7 gives E6 (Node, the shipped helper) | Opus · Sonnet · Opus | S1 |
| S7 | E6-g: both guards on, the CI step, the research lines (D8), the docs, the machine listing | Opus · Sonnet · Opus | S3, S4, S5, S6; the followups plan's S1 and S2 (D7) |

S1 is the last sweeper-stage change of this repository (rule § *The barrier*): S2 and later make the owner form, so S1
merges first. S3 to S6 touch disjoint files and may be built in parallel, but at most three PRs are open here at once
(`common/pull-requests.md` rule 7). No cornerstone (the parent's ledger is full at 3 of 3, and E6's entry says no).

## 6. Stories

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
benchmark-own files: `SweepLock` (D3), the start-of-run hook that runs the folder sweep once per process before the first
test (coai's `StartOfRunSweep` shape), and a `TempDir`-style entry (`TempDir.For(what).Path`) that the later stories use.
Wire the end-of-run audit. Add `git submodule update --init --depth 1 .agents/conventions` to `build-test` (E6-j). The
repository guard test is copied but NOT switched on (D10): it lands in S7.
**Scenarios** (each RED first where it pins a defect): the template's own tests, copied, green; case 15 (E6-c): in the temp
root, an old dead-claimed `testsToDelete-<owner>-bench-gitrepo-<id>/` holding `repo/` (a real repository) and
`scratch/<id>/` (a real linked worktree of it) is removed whole, and it is not kept as `other clone`; a live-claimed one
with the same content is kept; a folder whose OWN `.git` is a file is kept as `other clone`; the manifest check is red when
one copied byte changes; the sweep on Windows takes the mutex (the test reads the lock's name; the race itself is F29's
evidence, not re-run here).
**Files.** `tests/Bench.Tests/Leftovers/**`, `tests/Bench.Tests/Bench.Tests.csproj` (if a file needs it), `.github/workflows/ci.yml`,
`research/module_tests.md` (a *Leftovers* section: the helper, the sweep, the audit, the limits of D3).
**Done when.** The copy check is green; case 15 is green and was red before the copy's sweep existed; the whole suite is
green on `ubuntu-latest`.

### S3 — The fixture's container (E6-d, E6-f) and the container pass (D9)

**What.** `PostgresFixture` names its container `testsToDelete-<owner>-bench-pg-<id>` from the helper (`.WithName`), adds
`.WithLabel("testsToDelete.owner", <record>)`, never sets reuse, and keeps Ryuk. The `container-scoped` exemption comment
goes on the line above `NewEmptyDatabaseAsync` (`:96`): `testsToDelete-exempt: container-scoped — the per-test databases
live and die with the PostgresFixture container`. The container pass of D9 runs at the start of the run.
**Scenarios.** (1) Ryuk: a child test process starts the fixture and is killed with SIGKILL; within the reaper's window
the container named by the child is gone (`docker inspect` by the captured id fails). (2) The label: the started
container's `testsToDelete.owner` label equals the run's record, and the container pass, given a fake `docker` that lists
a dead-owner container of this token older than 10 minutes, removes it by id; a live-owner one, another owner's, another
token's and a young one are kept, each with its reason. (3) No reuse: the C# guard is red on a planted `.WithReuse(true)`
(through its `docker-name` or a benchmark-own check; the brief reads which), and the fixture file has none. (4) E6-f: after
`DisposeAsync`, the container is gone by id, so its databases are; the guard reads the marker; a malformed marker is an
`exemption` finding. (5) No `docker` on `PATH`: the pass reports one line and the suite goes on to the fixture's own
loud failure (`PostgresFixture.cs:45-47`).
**Files.** `tests/Bench.Tests/Infrastructure/PostgresFixture.cs`, a benchmark-own `ContainerPass.cs` in
`tests/Bench.Tests/Leftovers/` (on the copy check's own list), its tests, `research/module_tests.md`.
**Done when.** (1) to (5) green on `ubuntu-latest`; the name stays unique per process on one daemon (two suites at once:
two names).

### S4 — `ModelArms/`: the owned folder and no legacy name (E6-i, E6-b by hand)

**What.** `TempFolder` makes its folder through the helper (`testsToDelete-<owner>-bench-modelarms-<id>`, claimed) and
keeps its `OpenForDeleting` and `finally`; its 112 callers do not change. Each of the 15 legacy literals moves to the
helper or, where the test is about the old name itself, is spelled with the `PlantedLines` `~` precedent (coai's
`LeftoversBaselineTests.cs:256-260`). `PinnedConsumer`'s kept scratch (`:126-137`) moves INSIDE the run's owned folder
under a plain name, so it is the run's and goes with it; the brief checks that the reader is never given a path outside
the owned folder. The 10 real `worktree add` lines take one inline `raw-worktree` exemption each that names the
`TempFolder` they live in (E6-b); the 10 assertion strings use the `~` precedent, not a marker.
**Scenarios.** RED first: the guard's 15 `legacy-name` and 21 `raw-worktree` findings in `ModelArms/` before; zero after.
A test makes a `TempFolder`, is killed (a child process), and the next run's sweep removes the folder (rule §4: dead
record). The ModelArms suite is green; its time does not grow by more than the claim's cost (measured and recorded).
**Files.** `tests/Bench.Tests/ModelArms/**`.
**Done when.** No `legacy-name`, `raw-temp` or unmarked `raw-worktree` finding under `ModelArms/`.

### S5 — The rest of `Bench.Tests`, the fakes and the product seams (E6-k, E6-b seam)

**What.** The private temp classes of § 3 become wrappers over the copied `OwnedDir` (D5); `DatedGitRepo` puts its
repository and its scratch root in one owned folder; the 8 never-removed sites (E6-k) move into an owned folder with
`Dispose`; the path-only lines read `TempDir.Root` (the same string as `Path.GetTempPath()`; coai's shape G); the fakes
follow D6; the product seams follow D4; `HarvestGatesTests` and `SolutionSignalsTests` pass `DatedGitRepo.ScratchRoot`.
The `.ndjson` fixture line (§ 3): the brief reads whether any test asserts on that line's text; if none does, the quoted
command in the data is changed so it no longer spells the two words together, and the test is re-run; otherwise it stops
and asks.
**Scenarios.** RED first for each of the 8 never-removed sites: a test lists the temp root before and after the class's
tests and finds the folder left; green after. D4: a product call with the old signature still makes
`<temp>/bench-scratch/<uuid7>` (pinned); a test's worktree is under its owned folder and `git worktree list` in the
repository shows it inside. A killed `DatedGitRepo` user is collected by the next run (case 15's real-world twin).
**Files.** `tests/Bench.Tests/**` outside `ModelArms/` and `Leftovers/`; `tests/FakeCli/Program.cs`, `tests/FakeCoai/Program.cs`;
`src/Bench.Infrastructure/Git/{ScratchWorktree,HarvestGates,SolutionSignals}.cs`; `research/module_tests.md`.
**Done when.** No `raw-temp` or `raw-worktree` finding in `tests/` outside `ModelArms/` except the inline-exempt lines the
brief lists by file and reason; the suite is green; the product's own worktree names are unchanged (the pin test).

### S6 — `spikes/policy-ab-quick` and E6's other spike sites (Node)

**What.** The 25 `raw-temp` and 5 `legacy-name` lines in `spikes/policy-ab-quick/test/` (12 test files; names
`testsToDelete-pab-…` and `pab-…`, removed in `finally` today) use the shipped helper, `ownedDir(t, what)` from
`ownedFor({ token: 'bench' })`, as `spikes/lang-bench` does. The other spike findings that D7 gives E6 are listed in the
brief by file and line, each with its class and the reason it is E6's and not the followups plan's.
**Scenarios.** RED first: the guard's findings for those files before; zero after. A `policy-ab-quick` test killed
mid-run leaves a claimed folder that the shipped `sweepLeftovers` collects at the next start (the suite gets a start
sweep if it has none).
**Done when.** The listed files have no finding; the spike's suite is green.

### S7 — Both guards on, the CI step, the docs (E6-g)

**What.** Switch on the copied repository guard test (`LeftoversRepositoryGuardTests`: `FilesScanned > 0`, no findings).
Add a CI step to `build-test`: `node .agents/conventions/tools/leftovers-check.mjs --profile node --token bench --identity
"the measuring harness"`. Settle D8's two lines. Rewrite `http/README.md:32-33` to the owner form with `--rm`. Record the
machine listing of D11. Close the docs: `research/module_tests.md` (the guards, what they do not read — § 1's E6-g row —
and the limits of D3); `todo/README.md`.
**Scenarios.** RED again for each guard: a planted `Path.GetTempPath()` in a `tests/` `.cs`, a planted `mktemp -d` in a
`tools/` `.sh`, a planted `worktree add` without a marker, a planted `.WithReuse(true)`, an empty scan (a wrong root) —
each red, then removed. Both guards green on the head.
**Done when.** E6's Definition of Done (§ 11) holds.

## 7. Test plan

- Build and run as `.agents/PROJECT.md` says: `node .agents/conventions/tools/low-priority.mjs dotnet build
  dew_flow_benchmark.slnx -c Release -m:4`, then the test executable (never `dotnet test`), through the same wrapper.
- Every defect: RED (watched failing for the right reason), GREEN, then RED again by putting the defect back once.
- Kill tests use a child process and SIGKILL; they run on Linux and macOS; on Windows they skip with a reason where the
  platform cannot deliver the signal the same way.
- The Docker-backed proofs (S3) run on `ubuntu-latest` in CI. They are "to observe" until that run exists, and the PR
  quotes the run.
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
| The C# helper | `dew_flow_mcp` (E3) | E6 copies it at one commit (D2); a finding about the template goes to the parent as a note |
| `SweepLock` (F29) | coai (E5) | E6 copies it as its own file (D3) |

Disjoint otherwise: E6 changes nothing in another repository. The followups plan's row (`:63`) states the same split
from its side.

## 10. Reuse (`common/reuse-first.md`)

The helper, sweep, audit and guard are copied, not written (D2). `SweepLock` is coai's (D3). The Node side uses the
shipped `owned.mjs` (S6). The `~` spelling precedent is coai's. The only new code: the container pass (D9; no shipped C#
or Node container pass exists for a repository's own tests: the janitor is host-wide and not a test sweep), the fixture's
three lines, the product overloads of D4, the wrappers of D5 and D6.

## 11. Definition of Done

- [ ] E6-a to E6-k: each check green, with its observed result recorded on the PR that met it.
- [ ] The adapted copy's check is green; the manifest names `21553ce`; F29 is closed by `SweepLock`; F31 is stated by its true condition.
- [ ] A killed run's owned folder, its worktree (case 15) and the fixture's container are collected by the next run (S2, S3).
- [ ] The fixture's container is named in the owner form, labelled, Ryuk on, never reused; the per-test databases carry the `container-scoped` marker.
- [ ] No `raw-temp`, `raw-worktree`, `legacy-name` or `owner-blind` finding in either guard except the inline exemptions the stories list by file and reason.
- [ ] CI's `build-test` runs both guards; each is red on a scan of nothing.
- [ ] `research/module_tests.md` describes the helper, the sweeps, the guards, what they do not read, and the limits.
- [ ] The machine listing of D11 is on the S7 PR; nothing was removed by hand without the owner's word.
- [ ] This document is promoted with the parent, with its deviations.

## 12. Questions and prerequisites

1. **The followups plan's S1 and S2 before E6's S7 (D7).** That plan is open and has no build date. If it is still open
   when S1 to S6 are merged, the owner decides: wait, or move its F1/F2 sites to E6 (a boundary change on both sides).
2. **The open-PR cap.** This repository has one open PR (#103, this document's). S3 to S6 may be built in parallel but
   are opened at most three at a time.
3. Not prerequisites: F25 to F28, F32; the private sibling's mirror (F26).

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
