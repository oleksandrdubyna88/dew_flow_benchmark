# PLAN — E4 stories: the scoring gate, the reading sheet and the codex reader

> Status: **plan only, nothing implemented yet, 2026-10-14.** Epic E4 of [PLAN_subagent_policy_ab.md](PLAN_subagent_policy_ab.md).
>
> Scope: §8 units 6 and 7 of the parent — the mechanical scoring of a candidate per hold-out FILE (`SolutionSignals` widened,
> signals 3–4, verb `score`, the valid-alternative fairness check) and the codex requirement reading (verbs `sheet` and `read`,
> `PolicyRequirementReading`, `PolicyRequirementReader`). Pilot only: nothing of the parent's §12 (no pairwise, no defect hunt, no
> quality axes, no planted-variant calibration, no model-name deny-list beyond the pilot minimum of E4.S8).
>
> Related: [module_scouts.md](../research/module_scouts.md) (the reused pieces), [module_gate.md](../research/module_gate.md) (the
> blinded assessor), [PLAN_code_lane.md](PLAN_code_lane.md) §5.1 (signals 3–4 are specified there and built here, parent §7).
>
> **Cornerstone ledger.** This document marks ONE story: **E4.S3** (the hold-out gate). The parent's ledger names it:
> `Cornerstones: 2 of 3 — E2.S4, E4.S3`. Aligned 2026-10-14 with the parent's decisions D13 (Success never vacuous), D20 (docs
> per story), D21 (commands as exe + args), D22 (teeth = plain revert).

## Stories at a glance

| Story | Title | ≈ changed lines | Depends on | Model |
|---|---|---|---|---|
| E4.S1 | Per-test results, compile diagnostics and the docs strip, read purely | 300 | — (E1.S7 needs the junit reader too; whichever lands first builds it) | Opus brief · Sonnet code |
| E4.S2 | A scoring copy: the frozen tree, the candidate applied, a hold-out file from S | 350 | E4.S1 | Opus brief · Sonnet code |
| E4.S3 | **The hold-out gate: one file per copy, interface mismatch, Success** | 380 | E4.S1, E4.S2, E1.S10 (the classification record) | **Cornerstone — spec by Fable** · Sonnet code |
| E4.S4 | Teeth, neighbours and the candidate's own suite (signals 3 and 4) | 380 | E4.S1–S3, E1.S7/E1.S12 (baseline at P) | Opus brief · Sonnet code |
| E4.S5 | Verb `bench policy score` and the score record | 300 | E4.S3, E4.S4, E1.S10 (`stories.json`), E1.S11 (the dispatcher) | Opus brief · Sonnet code |
| E4.S6 | The fairness check, live: the reference and a hand-made valid alternative | 350 (TypeScript + record) | E4.S5, E1.S13 (committed pilot record) | Opus brief · Sonnet code · attended |
| E4.S7 | `PolicyRequirementReading`: prompt, schema, a reader that refuses, the reference gate | 380 | — | Opus brief · Sonnet code |
| E4.S8 | The reading sheet and the assessment tree — verb `sheet` | 380 | E4.S1, E4.S7, E1.S10 (R-items) | Opus brief · Sonnet code |
| E4.S9 | `PolicyRequirementReader` and verb `read` | 380 | E4.S7, E4.S8, E1.S10 (R-items) | Opus brief · Sonnet code · one live dry run |

Two parallel tracks: **scoring** S1 → S2 → S3 → S4 → S5 → S6, and **reading** S7 → S8 → S9 (S8 waits for S1's docs strip only).
Other epics' stories are named by id (linked by the main session, 2026-10-14).

## Rules every story here follows

- **Placement.** Pure readers and records in `src/Bench.Domain/Policy/` (Domain references nothing beyond the runtime —
  `tests/Bench.Tests/ArchitectureTests.cs:16`; `System.Xml.Linq` and `System.Text.Json` are runtime); anything that starts a
  process or touches disk in `src/Bench.Infrastructure/Policy/`; launches through a port in `src/Bench.Application/Policy/`
  (Application never reaches Infrastructure — `ArchitectureTests.cs:57`); verbs in `hosts/Cli/Policy*Command.cs`. Tests in
  `tests/Bench.Tests/Policy/`, fixtures in `tests/Bench.Tests/Fixtures/policy/`.
- **Tests.** RED first, watched failing for the real reason (not a missing registration); then green; then the whole suite.
  Build `node .claude/rules/shared/tools/low-priority.mjs dotnet build dew_flow_benchmark.slnx -c Release -m:4`; run
  `node .claude/rules/shared/tools/low-priority.mjs ./tests/Bench.Tests/bin/Release/net10.0/Bench.Tests.exe [--filter-class "*X"]`
  — **never `dotnet test`**. A break-it is a temporary edit of the PRODUCT code, watched red with the real symptom, then reverted;
  it never disables, skips or loosens a test or a tripwire.
- **Processes.** Exe + argv, never a shell string (`GateCommand`, `src/Bench.Infrastructure/Git/HarvestGates.cs:10`). `ProcessRunner`
  starts without a shell (`src/Bench.Infrastructure/Process/ProcessRunner.cs:106-127`), so on Windows a bare `npm` is *NotFound* —
  the restore is `npm.cmd ci`; build and test call `node` directly (`node node_modules/typescript/bin/tsc -p ./`, `node --test …`).
- **Attended only.** Every story that builds or runs a candidate builds model-authored code: it runs while an operator watches, one
  at a time, never beside another build or the test suite (parent §3.3, `PLAN_code_lane.md` §4.2). Nothing here is wired into a
  scheduler or `LegRunner`.
- **No arm, model, cost or time** enters a score record, the assessment tree, the reading sheet or a prompt; arms are joined to
  labels only in E5's records, after the reading.
- **Records committed under `research/policy-ab/`** are scrubbed (`ScoutScrubber.Leftovers` empty,
  `src/Bench.Domain/Scouts/ScoutScrubber.cs:50`); raw reports, copies and batch folders stay in the artefact root.
- **Docs per story**: rows in `research/module_tests.md` for its tests; its paragraph in `research/module_policy_ab.md` § Scoring or
  § Reading (the first policy story to land creates the file, parent D20; E5.S9 completes it). Commit messages per `git-workflow.md`.

---

### E4.S1 — Per-test results, compile diagnostics and the docs strip, read purely

**Goal.** Three pure readers every later step stands on: node:test's junit report → one outcome per test; tsc's diagnostic lines →
errors per file; a unified diff → the same diff without plan, research and docs hunks (parent §3.7, "Plan, research and docs hunks …
are stripped from every candidate AND the reference before any scoring or reading").

**Scope.** New `src/Bench.Domain/Policy/NodeTestReport.cs`, `TscDiagnostics.cs`, `PolicyDiffFilter.cs`. Read-only reuse:
`FixDiff.Parse` (`src/Bench.Domain/Authoring/FixDiff.cs:44`) for the file list a filtered diff must still parse to.

**Brief.**
- `NodeTestReport.Read(string junitXml, string treeRoot) → Outcome<NodeTestReport>`; `NodeTestReport(IReadOnlyList<NodeTestCase>
  Cases, IReadOnlyList<string> Duplicates)`; `NodeTestCase(string TestFile, string SuitePath, string Name, NodeTestOutcome
  Outcome, string Message)`; `NodeTestOutcome { Passed, Failed, Skipped, Todo, Cancelled }`. **Shape, measured on this machine
  (node v24.18.0, 2026-10-14):** a `describe` is a `<testsuite name=…>`; a top-level test is a `<testcase>` directly under
  `<testsuites>`; every `<testcase>` carries `name`, `time`, `classname="test"` and **`file` = the absolute path of the compiled
  `.js`**; a failure is a `<failure type=… message=…>` child; a skip is `<skipped type="skipped" …>`; the totals are XML comments;
  `<testsuite>` carries `hostname`. The coder records ONE real report from node here covering pass, fail, skip, todo, a cancelled
  test (a test whose timeout fires) and a nested `describe` — scrubbed of paths and host name before it is committed as the fixture
  — and writes the reader against it, never against this paragraph.
- Identity = (`TestFile` tree-relative with `/`, `SuitePath` = the enclosing suite names joined by ` > `, `Name`). A `file` outside
  `treeRoot` → Failure (a test file outside the copy is an instrument fault). Two cases with one identity are both kept, the second
  suffixed `#2`, and the identity listed in `Duplicates` — consumers treat a duplicate as not comparable, never pick one.
- `TestPathMap(string gateDir, string outDir, string rootDir)`: `src_vs_code/out/test/x.test.js` ↔ `src_vs_code/src/test/x.test.ts`
  (pilot `outDir` `out`, `rootDir` `src`, from its `tsconfig.json` at P); a path outside `outDir` → Failure.
- `TscDiagnostics.Read(string output, string gateDir) → Outcome<IReadOnlyList<TscDiagnostic>>`; `TscDiagnostic(string Path, int Line,
  int Column, int Code, string Message)`. tsc's non-pretty form `path(line,col): error TSnnnn: message` (what tsc writes when
  stdout is not a terminal — every launch here); indented continuation lines join the previous message; a file-less
  `error TSnnnn:` → `Path` empty; paths made tree-relative with the gate directory prefixed. Output carrying ANSI escapes →
  Failure ("pretty output — not parsed"), never a best-effort parse.
- `PolicyDiffFilter.WithoutDocs(string diff) → Outcome<FilteredDiff>`; `FilteredDiff(string Diff, IReadOnlyList<string> Dropped)`.
  Split on `diff --git a/… b/…` headers (the form `git diff --cached --binary` writes, parent §3.3 "After exit"); drop a whole file
  section whose path is under `todo/` or `research/`, ends `.md`, or is `CHANGELOG.md` at any depth. A rename/copy is judged by its
  NEW path, a deletion by its old path; a binary section is kept or dropped whole. An all-docs diff → empty diff, not a failure;
  an unparseable header → Failure. One splitter: E3.S3's `PolicyCandidate.Split` splits on the same headers — whichever lands
  first, the other reuses it. **`public static bool PolicyDiffFilter.IsDocPath(string path)`** is the one doc-path rule, shared
  with E1.S9 (whichever of E1.S9 / E4.S1 lands first creates it).
- **E1.S7 needs `NodeTestReport` too** — `prepare` runs the tests at P twice and lists flaky tests (parent §3.2 "Gates at P").
  Whichever of E1.S7 / E4.S1 lands first builds it in `Bench.Domain/Policy/`; the other reuses it. Never two readers.

**Tests** (`NodeTestReportTests`, `TscDiagnosticsTests`, `PolicyDiffFilterTests`; tables).
- RED: the three test classes against stubs that return empty results — red, each naming the missing case.
- junit fixture → every outcome read; nested suite path; tree root stripped; `file` outside the tree refused; not XML → Failure
  naming it; zero test cases → an empty report, not a failure; a duplicate identity → both kept, listed.
- tsc fixture (captured once from the pilot tree at P with one planted error, scrubbed) → grouped per file; a continuation line;
  a global error; ANSI → refused; empty output → no diagnostics.
- docs strip: `todo/x.md`, `research/y.md`, `src_vs_code/README.md`, `CHANGELOG.md`, `src_vs_code/src/a.ts`, a binary section,
  `notes.md → code.ts` (kept), `code.ts → notes.md` (dropped) → exactly the code sections kept, `Dropped` exact.
- **Break-it**: map `<skipped>` to `Passed` in the reader → the skip row goes red (a skipped neighbour must never read as green —
  E4.S4 rests on it); revert, green.

**Done when.** The three classes green; the scouts suite unchanged and green; both fixtures pass `ScoutScrubber.Leftovers` empty;
the `module_tests.md` rows written.

**Depends on.** Nothing.

**Model.** Scenarios + brief: Opus · code + tests: Sonnet · review: Opus · fixes: Sonnet.

---

### E4.S2 — A scoring copy: the frozen tree, the candidate applied, a hold-out file materialised from S

**Goal.** The scaffold every mechanical signal runs in, **widened from `SolutionSignals`, not copied** (parent §3.8, §3.11): a fresh
copy of the frozen story tree with its dependencies, the candidate's docs-stripped diff applied, and a hidden-test port that writes a
file's bytes from S — replacing `git checkout <fix> -- <tests>` (`src/Bench.Infrastructure/Git/SolutionSignals.cs:117-118`).

**Scope.** `src/Bench.Infrastructure/Git/SolutionSignals.cs:53-90` (`RunAsync`), `:92-154` (`SignalsAsync`), `:176-207`
(`ApplyAsync`, private → internal, shared); `src/Bench.Infrastructure/Git/ScratchWorktree.cs:21-33` (`AddAsync(…, underRoot)`),
`:42-43` (`RemoveAsync`), `:69-71` (`GateProcess.RunAsync`), `:54` (`GateDeadline`). New `src/Bench.Infrastructure/Git/IHiddenTestSource.cs`
and `src/Bench.Infrastructure/Policy/ScoringCopy.cs`, `DependencyTemplate.cs`.

**Brief.**
- **Port.** `IHiddenTestSource.WriteAsync(string treeDir, IReadOnlyList<string> paths, TimeSpan, CancellationToken) → Outcome<string>`.
  Two adapters: `CheckoutHiddenTests(CommitSha fix)` — today's `git checkout fix -- paths`, so the existing `RunAsync` keeps its
  behaviour exactly — and `MirrorShowHiddenTests(string mirrorDir, CommitSha s)` — `git -C <mirror> show S:<path>` read as BYTES
  (EOL and encoding preserved), parents created, written over whatever the candidate put at that path. A path absent at S →
  Failure naming it (the classification promised it exists); a path resolving outside `treeDir` → refused.
- **Prepared-tree overload.** `SolutionSignals.RunPreparedAsync(string treeDir, IHiddenTestSource hidden, IReadOnlyList<string>
  hiddenPaths, GateStep build, GateStep test, GateDeadline deadline, CancellationToken) → Outcome<PreparedRun>`;
  `PreparedRun(bool Built, string BuildOutput, bool TestsRan, bool TestsGreen, string TestOutput)`; `GateStep(string
  WorkingSubdirectory, GateCommand Command)` runs in `<tree>/<subdir>` (repository-relative, no `..`, not rooted — refused
  otherwise). `SignalsAsync` is refactored to apply, then call `RunPreparedAsync` with `CheckoutHiddenTests` — the old path becomes
  the new one's first caller, and `tests/Bench.Tests/Infrastructure/SolutionSignalsTests.cs` (unchanged) is the proof.
- **`ScoringCopy.CreateAsync(FrozenTree frozen, DependencyTemplate template, string candidateDiff, string underRoot, …) →
  Outcome<ScoringCopy>`**, `IAsyncDisposable`. (1) `ScratchWorktree.AddAsync(frozen.Dir, frozen.Commit, …, underRoot)` — the frozen
  tree is ONE commit holding every submodule's files (parent §2, `ScoutTrees`), so its worktree is complete, unlike a worktree of
  the original repository; (2) copy each dependency folder named by the story (pilot `src_vs_code/node_modules`) from the template;
  (3) delete each build-output folder named by the story (pilot `src_vs_code/out`) by its exact path if present — a stale
  `out/test/*.test.js` runs as a phantom test; (4) `PolicyDiffFilter.WithoutDocs` (E4.S1), then the shared `ApplyAsync`. A diff that
  does not apply → `Applied = false` with the reason: a verdict, never an instrument failure (`SolutionSignals.cs:47-49`). An empty
  diff → the frozen tree, `Applied = true`.
- **`DependencyTemplate`**: one per story — a worktree of the frozen tree plus the story's restore command (pilot `npm.cmd ci` /
  `npm ci` by platform) run ONCE, keyed by the SHA-256 of the lockfile; reused while that hash matches. A candidate diff that changes
  the lockfile gets the restore in its own copy, recorded `dependencies restored per copy`. The restore uses the network: attended,
  outside every cell. A junction to the template's `node_modules` was rejected — a model-authored test could write through it.
- **Location and removal.** `underRoot` must lie outside every git work tree (checked: `git -C <underRoot> rev-parse
  --is-inside-work-tree` must fail) — refused otherwise. Dispose = `ScratchWorktree.RemoveAsync`, then, if the directory survives
  (ignored folders), delete THAT directory by its exact path — never a pattern, never a parent. Runs on success, failure and
  cancellation.

**Tests** (`tests/Bench.Tests/Policy/ScoringCopyTests.cs`; real git through `DatedGitRepo`, `tests/Bench.Tests/Infrastructure/DatedGitRepo.cs:16`;
`git` standing in for build and test as `SolutionSignalsTests.cs:15-19` does).
- RED: a copy test against a `CreateAsync` that returns the bare worktree — red on the missing dependency folder.
- The existing `SolutionSignalsTests` untouched and green.
- A copy holds the frozen tree's files (a "submodule" folder included), the template's dependency folder, no `out/`, the candidate
  applied; a docs-and-code diff applies only the code.
- A hidden file from S overwrites the candidate's own file at that path, byte-identical to `git show S:path` (a CRLF fixture).
- A path missing at S → Failure naming it; a `../` path → refused; a `GateStep` subdir with `..` → refused.
- A diff that does not apply → `Applied = false`, the copy still removed.
- The copy directory is gone after dispose — on success, on a failed apply, on a cancelled token.
- A lockfile change → the restore runs in that copy (a fake restore command), recorded.
- **Break-it**: drop step (3) — a planted `out/test/phantom.test.js` in the template appears in the test run → red; restore.

**Done when.** The above green; `SolutionSignalsTests` unchanged and green; the scouts suite green.

**Depends on.** E4.S1 (the docs strip).

**Model.** Scenarios + brief: Opus · code + tests: Sonnet · review: Opus · fixes: Sonnet.

---

### E4.S3 — The hold-out gate: one file per copy, interface mismatch, Success

**Cornerstone — spec by Fable.** Every number of both arms passes through this story, and a wrong decision here rewards the
reference's shape silently: a valid alternative marked failed, or a candidate that delivered nothing marked successful, looks like
an ordinary score. **Detailed spec (scenarios + brief) to be written by Fable.** What follows is the goal, the acceptance criteria
and the break-its the spec must satisfy.

**Goal.** Score a candidate against the reference's hold-out tests per FILE, without imposing the reference's interface where the
frozen statement did not imply it (parent §3.2 "The hold-out set and its classification", §3.7 Success and Interface mismatch, §3.8
"Mechanical, per hold-out FILE"), on top of E4.S1's readers and E4.S2's copies.

**Acceptance criteria.**
1. **One file, one copy.** Each hold-out file named by `stories.json` is materialised ALONE into its own scoring copy, compiled and
   run there; no file's result touches another's. The never-materialise list — the pilot's `src_vs_code/src/test/typedFixtures.test.ts`
   and `src_vs_code/src/test/fixtures/typed/**` (T2's deliverable) — is never written into any copy.
2. **Pre-condition.** The candidate copy WITHOUT any hold-out file must typecheck and build; if not, Success is false ("does not
   build") and every hold-out file is *not run* — never *interface mismatch*.
3. **Interface mismatch** is decided from E4.S1's diagnostics: the copy fails to compile with errors ONLY inside the hold-out file.
   Its tests are then not run — tsc emits JavaScript despite errors, and a mismatched file's emitted code is never executed. Errors
   outside the hold-out file after adding it are an instrument anomaly, named, never a mismatch.
4. **What counts as interface.** The spec states, with its reason, which diagnostics are interface (a missing module, export,
   member or overload — names, signatures, module paths) and which are a candidate fault inside an interface it does provide (a
   wrong type through a spec-named function). Where the failing names are spec-implied, the record names the R-item that covers
   that interface (§3.7: "the R-item covering that interface carries the deviation").
5. **Success is never vacuous** (parent D13, decided 2026-10-14). P unchanged (the empty candidate) does NOT score Success, and
   neither does a candidate that implements nothing a hold-out file can compile against. The principle: Success needs AT LEAST ONE
   spec-implied test that compiled AND every compiled spec-implied test green; *interface mismatch* stays its own reported outcome.
   The spec settles the exact rule within that principle — for example whether a mismatch on a spec-implied interface also counts
   against Success — and states it with its reason.
6. **Classes, per test.** Spec-implied tests decide Success; beyond-spec tests feed *beyond-spec green ÷ compiled*; tests that exist
   unchanged at P inside a hold-out file are neighbours — recorded, counted in neither. A spec-implied test lost because a
   beyond-spec test's interface made its whole file mismatch is visible in the record (which class the failing names belong to).
7. **Nothing unmatched is dropped.** A test in a hold-out file that the classification does not list, or a listed test absent from
   the junit report, is an instrument failure naming it.
8. **Fixture proof (offline).** A fixture repository with: the reference (Success, zero mismatches); a valid alternative whose module
   layout differs (its first hold-out file → *interface mismatch*, the second still runs and counts, Success); the empty candidate
   (not Success); a candidate failing a spec-implied test (not Success). The fixture's compile step — real `tsc` if the spec finds a
   way to run it offline in the suite, otherwise a committed node script printing tsc-shaped diagnostics — is the spec's choice,
   stated with why; real tsc is exercised live in E4.S6 either way.
9. **Attended**, no scheduler wiring; copies removed by exact path on every path out (E4.S2).

**Break-its** (each red with the real symptom, then restored; no test loosened):
- materialise all hold-out files into ONE copy → the valid alternative's second file fails with it (parent §9);
- exclude mismatched files from Success with no further condition → the empty candidate scores Success;
- run a mismatched file's emitted JavaScript → its runtime failures reach the alternative's result;
- count a P-unchanged test inside a hold-out file as spec-implied → the classification fixture's numbers move.

**Done when.** Every acceptance criterion holds in the suite; the four break-its went red and back; the exact Success rule of
criterion 5 is written in this document's § Decisions taken here and in the parent's §3.7 if it narrows D13.

**Depends on.** E4.S1, E4.S2; E1.S10's classification record (`stories.json`: hold-out files, per-test class on the behaviour and
interface axes, the never-materialise list) — and this story's Fable spec is written BEFORE E1.S13 freezes the pilot (it sets the
classification rules) — this story's fixtures use the same record type.

**Model.** **Cornerstone — spec by Fable** (scenarios + brief: Fable; Fable refusing on a limit → Opus, said in the summary) ·
code + tests: Sonnet · review: Opus · fixes: Sonnet.

---

### E4.S4 — Teeth, neighbours and the candidate's own suite (signals 3 and 4)

**Goal.** Signal 3 (*the solver's own test exists and has teeth*) and signal 4 (*the neighbouring tests are intact*) as
`todo/PLAN_code_lane.md:165-166` specifies them — built here per parent §7 — plus the *Own suite* metric (§3.7).

**Scope.** New `src/Bench.Domain/Policy/Teeth.cs`, `NeighbourSuite.cs` (pure, over E4.S1's records) and
`src/Bench.Infrastructure/Policy/SignalRuns.cs` (the two runs, through E4.S2). The story's test-path rule: E1.S10's input field
`testGlobs` (pilot `src_vs_code/src/test/**`); an empty list is refused.

**Brief.**
- **One run of the candidate copy without any hold-out file** gives two readings from one junit report. *Own suite*: builds ∧
  every test green. *Neighbours*: the tests green in BOTH baseline runs at P (E1.S12's record), in files that exist at P, minus
  hold-out-classified tests, minus test infrastructure, minus tests red or flaky at P. Per neighbour: `green`, `red`, `missing` (the
  candidate deleted or renamed it; its file no longer loads) or `skipped`. A `skip`/`todo` the candidate introduced is `missing`,
  never green. **Signal 4 = no neighbour red**; `missing` is reported beside it with the names — neither red (a renamed test is not a
  broken one) nor silently green. A candidate that does not build → own suite false, neighbours *not run*.
- **Teeth.** A copy of the frozen tree with ONLY the candidate's hunks on test paths applied — product code stays at P, the plain
  revert of `PLAN_code_lane.md:165`. Build (errors expected and recorded, not fatal), then `node --test` over the compiled files of
  the candidate's added or changed test files only. Per test in those files: **new** (identity absent from P's baseline) or
  **existing**. A new test is `red` (failed or cancelled), `compile-red` (its own file had diagnostics and it did not pass), `green`
  (no teeth) or `absent` (not in the report: `compile-red` if its file had diagnostics, else an instrument note). **Signal 3 = at least
  one new test ∧ no new test green**; "the candidate added no test" is its own reason. An existing test that goes red counts as
  *changed with teeth* (reported); an existing test that stays green is *not assessed* — a name-level reader cannot tell an unchanged
  test from a toothless edit, and counting it would fail every correct candidate.
- **The T2 harness is a test file like any other** — no special case. The reference's harness checks the exact code and line per
  fixture (`typedFixtures.test.ts` at S), so the plain revert turns it red; a lax harness stays green and shows as no teeth — that is
  the measurement. Parent §3.7's "`type StoredSecret = string`-style reverts" is NOT built: a mutation names the reference's shape and
  cannot be aimed at an alternative's (recorded in § Decisions taken here).

**Tests** (`TeethTests`, `NeighbourSuiteTests` as tables; one integration class over E4.S3's fixture repository).
- RED: the tables against stubs returning "all green" — red on the toothless and the broken-neighbour rows.
- Teeth: new red → teeth; new green → no teeth, named; no new test → false with its reason; compile-red counted apart; existing red
  → changed with teeth; existing green → not assessed.
- Neighbours: green twice at P and red now → signal 4 red, named; flaky at P → excluded; deleted → missing, not red; a `skip` added
  → missing; a hold-out-classified test and test infrastructure → excluded.
- Own suite: one red own test → false.
- Integration (parent §9 *Mechanical scoring*): a toothless solution → signal 3 red; a solution breaking a neighbour → signal 4 red;
  the correct solution → both green.
- **Break-its**: read `Skipped` as green in the neighbour reading → the skip-added row passes signal 4 → red; run the teeth copy
  with the candidate's PRODUCT code instead of P's → the toothless fixture passes signal 3 → red. Each restored.

**Done when.** The above green; the scouts suite green; the deviation (plain revert) written in § Decisions taken here.

**Depends on.** E4.S1, E4.S2, E4.S3 (its fixture repository); E1.S12's baseline at P (two runs' per-test results, the flaky list).

**Model.** Scenarios + brief: Opus · code + tests: Sonnet · review: Opus · fixes: Sonnet.

---

### E4.S5 — Verb `bench policy score` and the score record

**Goal.** One verb scores one candidate diff — a cell's, the reference's or the alternative's — end to end and writes one
`PolicyScore` record, keyed by a label, never by an arm.

**Scope.** `hosts/Cli/Program.cs:113` (the verb switch, beside `"scouts"`); the `policy` dispatcher (`hosts/Cli/PolicyCommand.cs` —
E1.S11's, or E3.S3's if it lands first) with a new `hosts/Cli/PolicyScoreCommand.cs` shaped on
`hosts/Cli/ScoutsAssessCommand.cs:23-81`; `hosts/Cli/ExitCodes.cs:15-28`. New `src/Bench.Domain/Policy/PolicyScore.cs` and
`src/Bench.Infrastructure/Policy/PolicyScorer.cs` (Infrastructure, because `SolutionSignals` is).

**Brief.**
- `bench policy score --story <id> --stories <research/policy-ab/stories.json> --mirror <dir> --scoring-root <dir outside git>
  (--diff <file> --label <cell-id|alternative> | --reference) --out <file> [--gate-timeout-minutes 30]`. `--reference` computes the
  reference diff `git -C <mirror> diff P S` from the story record, label `reference`.
- Flow: E1.S10's `stories.json` reader → docs strip (S1) → copies (S2) → the hold-out gate (S3) → teeth, neighbours, own suite (S4) →
  `PolicyScore`. One `GateDeadline` for the whole verb.
- `PolicyScore`: label; story id; SHA-256 of the docs-stripped diff; dropped docs paths; applied; builds; Success; per hold-out file
  (ran / mismatch / not run, its tests with class and outcome); mismatch count; beyond-spec ratio (*not captured* when no beyond-spec
  test compiled — never 0); signal 3 with its counts; own suite; signal 4 with its red and missing lists; dependency restores; the gate
  commands; node and tsc versions; UTC start and end; wall. **No arm, model or cost field exists on the type** — E5 joins arms.
- Exit codes: 0 scored, whatever the verdict (a low score still exits 0); 3 the instrument could not run (worktree, restore, a hold-out
  path missing at S, an unmatched classification); 4 flags or configuration (unknown story, missing diff, a scoring root inside a
  git work tree, `--out` already exists — a committed score is never overwritten); 5 nothing scored.

**Tests** (`tests/Bench.Tests/Policy/PolicyScoreCommandTests.cs`, the CLI's existing in-process style).
- RED: the end-to-end test against a dispatcher without `score` — exit 4 "unknown verb", red.
- Each refusal with its exit code and reason.
- End to end over E4.S3's fixture repository as the mirror: `--reference` → Success, zero mismatches, the record round-trips JSON;
  the empty diff → not Success, exit 0.
- Structural: `PolicyScore`'s serialised properties contain no `arm`, `model`, `cost`, `usd` — asserted over the type's properties.
- **Break-it**: drop the scoring-root check → a root inside the test's repository is accepted → red; restore.

**Done when.** The above green; `bench help` lists the verb; the scouts suite green.

**Depends on.** E4.S3, E4.S4; E1.S10's `stories.json` reader and record types (the frozen tree's path and commit, P, S, gates, hold-out,
classification, baseline).

**Model.** Scenarios + brief: Opus · code + tests: Sonnet · review: Opus · fixes: Sonnet.

---

### E4.S6 — The fairness check, live: the reference and a hand-made valid alternative through the gate

**Goal.** Parent §3.8 "The gate's fairness check" and §4 step 5: the evidence, on the real pilot tree with real tsc and node, that
the gate scores the reference correctly and does not reward its shape.

**Scope.** `bench policy score` (E4.S5) over the pilot story; new `research/policy-ab/fairness/alternative.diff` and
`research/policy-ab/fairness.md`. No C#.

**Brief.**
- **First the reference**: `score --reference` → Success, zero mismatches, every spec-implied test green, signals 3 and 4 green. Any
  other result means the gate or the classification is wrong: stop, report, fix the cause — never loosen the gate.
- **Then the alternative**: one diff over P with the reference's behaviour and a different file and structure where the frozen
  statement leaves the shape open, chosen from E1.S13's committed classification rows: it changes at least one interface a hold-out file uses whose
  *interface* axis is NOT spec-implied (so the mismatch path runs live) and keeps every spec-implied interface. If the classification
  marks every hold-out interface spec-implied, the alternative varies internal structure only and `fairness.md` says the mismatch path
  was not exercised live (E4.S3's fixture is then its only proof).
- The Opus judge writes the alternative's brief (which shapes vary, citing the classification rows); a Sonnet coder writes it in a
  scratch tree of P outside every repository and runs that tree's own `npm test` green. Its T2 harness may be the reference's or its
  own — a deliverable, scored only through teeth (parent §3.2).
- `score --diff alternative.diff --label alternative` → must be Success, with mismatches only on files whose interface is not
  spec-implied. A spec-implied failure means the alternative is wrong (fix it) or the classification is (back to E1) — decided by the
  Opus judge and recorded, never by changing the gate.
- Attended, one run at a time, nothing else building. Copies and the scratch tree removed by exact path afterwards (parent §3.13).
- `fairness.md`: the shape choices and the classification rows they rest on, both `PolicyScore` records, the verdict, node and tsc
  versions. The diff is docs-free and scrubbed (`ScoutScrubber.Leftovers` empty); the repository is public.

**Tests.** No new suite tests (it is a live run of E4.S5); the RED observation is the gate's first run over the alternative, quoted in
`fairness.md` beside the final one.

**Done when.** Both records committed; the reference Success with zero mismatches; the alternative Success, mismatches only where
the interface is not spec-implied; copies removed; `fairness.md` committed. This is §4's "the valid-alternative variant scored Success"
evidence for `pilot.md`.

**Depends on.** E4.S5; E1.S13's committed `stories.json` for the pilot story.

**Model.** Brief: Opus · the alternative: Sonnet · review: Opus · fixes: Sonnet · the run: the main session, attended by the operator.

---

### E4.S7 — `PolicyRequirementReading`: prompt, schema, a reader that refuses, coverage and the reference gate

**Goal.** The pure half of the codex reading (parent §3.8, D7): the prompt, the output schema, a parser that REFUSES a malformed
answer naming its defect, coverage, the ≥ 90 % reference gate and the spot-check list.

**Scope.** New `src/Bench.Domain/Policy/PolicyRequirementReading.cs`. Reuse: `BlindedId` (`src/Bench.Domain/Gate/BlindExport.cs:11-45`);
the envelope parse of `ScoutAssessment.Rows` (`src/Bench.Domain/Scouts/ScoutAssessment.cs:154-174`, private) — **extracted** into one
internal Domain helper both call, scouts tests unchanged; the schema's construction style of `ScoutAssessment.cs:70-94`; `StableHash.Of`
for the rubric, prompt and schema hashes.

**Brief.**
- `RequirementVerdict { Met, Partial, NotMet, Unresolved }`, words `met`, `partial`, `not met`, `unresolved`.
  `PolicyRequirement(string Id, string Text, string HowToVerify)` (ids `R1…Rk`). `ReadingCandidate(BlindedId Id,
  IReadOnlyDictionary<string, int> Files)` — every path under `candidates/<id>/` → its line count. `ReadingBatch(IReadOnlyList<
  PolicyRequirement> Requirements, IReadOnlyList<ReadingCandidate> Candidates)`. `ReadingVerdict(BlindedId Id, string Requirement,
  RequirementVerdict Verdict, string Cite, string Note)`.
- `Prompt(string rubric, ReadingBatch batch, string retryNote = "")` — the rubric (from `prompts/policy-ab/read-requirements.md`,
  loaded by E4.S9), the R-items with their "how to verify", each id with its file list; nothing else. The inputs carry no label, arm,
  model, cost or time — the structure is the guarantee.
- `Schema`: `{"rows": [{"id", "requirement", "verdict" (enum of the four), "cite", "note"}]}`, `additionalProperties: false`, every
  property required (codex's enforced schema) — `cite` is `""` only for `unresolved`.
- `Read(string answer, ReadingBatch batch) → Outcome<IReadOnlyList<ReadingVerdict>>` REFUSES, listing every defect (capped at 20, so
  the re-ask can quote them): not JSON or no `rows`; an unknown id; an unknown R-item; a missing (id × R-item) pair; a duplicate pair; a
  verdict outside the four; `met`/`partial`/`not met` with an empty cite; a cite not `candidates/<id>/<path>:<line>` or
  `:<start>-<end>`; a cite into ANOTHER id's folder; a path not among that id's files; a line outside `1…lineCount`. A non-empty cite
  on `unresolved` is validated the same way. Paths: `\` → `/`, a leading `./` dropped, matched ordinal-ignore-case, stored in the
  batch's spelling. **Stricter than its model on purpose**: `ScoutAssessment.Read` (`ScoutAssessment.cs:121-140`) drops a bad row and
  returns the rest; this reader returns nothing from a malformed answer.
- `Coverage(verdicts, id) = (met + ½·partial) ÷ k`; `unresolved` counts 0 and is listed apart. `ReferenceSanity(verdicts,
  referenceId)` passes iff `met·10 ≥ 9·k` — integer arithmetic, `partial` is not `met`. `NotMet(verdicts)` → every `not met`, the
  reference's included, in id-then-R order — the spot-check list for `handcheck.md`.

**Tests** (`tests/Bench.Tests/Policy/PolicyRequirementReadingTests.cs`; every case of parent §9 *Codex reading* that is pure).
- RED: the refusal table against a `Read` that accepts everything — red on each malformed row.
- A well-formed reply → one verdict per pair, cites kept; each refusal shape above → Failure naming it; two defects → both named.
- Sanity: 8 of 10 met → fails; 9 of 10 → passes; 9 met + 1 partial → passes; 8 met + 2 partial → fails. Coverage with partials as ½;
  unresolved counted 0 and listed.
- The prompt over a fixture batch holds only the rubric, R-items, ids and paths (an exact-text assertion).
- **Break-its**: accept `met` without a file:line → the planted malformed reply passes (parent §9) → red; count `partial` as `met` in
  the sanity gate → 8 met + 2 partial passes → red. Each restored.

**Done when.** The above green; `ScoutAssessmentTests` unchanged and green after the `Rows` extraction.

**Depends on.** Nothing (pure; its R-items arrive through E4.S8/S9 from E1.S10's record).

**Model.** Scenarios + brief: Opus · code + tests: Sonnet · review: Opus · fixes: Sonnet.

---

### E4.S8 — The reading sheet and the assessment tree — verb `bench policy sheet`

**Goal.** Parent §3.8 "The reading sheet" and "The assessment tree": every candidate and the reference under a fresh blinded id,
docs-stripped and scrubbed, as post-image files codex can open, and a committed sheet a human can follow — with the label → id key
kept out of both.

**Scope.** New `src/Bench.Domain/Policy/ReadingSheet.cs` (pure: ids, bearing ranges, the markdown, the batch record),
`src/Bench.Infrastructure/Policy/AssessmentTree.cs`, `hosts/Cli/PolicySheetCommand.cs`. Reuse: `BlindedId.Mint`
(`src/Bench.Domain/Gate/BlindExport.cs:31`) and the shuffle of `ScoutAssessment.Blind` (`src/Bench.Domain/Scouts/ScoutAssessment.cs:104-110`);
`ScoutScrubber.Scrub` / `Leftovers` and `ScrubDenyList` (`src/Bench.Domain/Scouts/ScoutScrubber.cs:45`, `:50`, `:9-15`); E4.S1's docs
strip; E4.S7's `ReadingBatch`.

**Brief.**
- `bench policy sheet --story <id> --stories <file> --mirror <dir> --candidate <label>=<diff file> (repeated) --deny-list <local
  file> [--model-id <id> …] --artifact-root <dir>`; the reference is always added from `git -C <mirror> diff P S`.
- **Ids**: one fresh `BlindedId` per candidate and the reference, from a seeded `Random`; the order shuffled. The key (label → id,
  the seed) goes to `<artefact root>/policy/<story>/keys/<sheet id>.json` — outside the assessment tree and outside its parent folder
  — and into a batch folder only after the answer is read (E4.S9).
- **Assessment tree** `<artefact root>/policy/<story>/assess/<sheet id>/tree/`: `git archive` of the frozen tree's one commit,
  extracted (no `.git`, no `node_modules`); then per id `candidates/<id>/` with the post-image of every changed file (the docs-stripped
  diff applied to a scratch extract, the changed files copied) and `candidates/<id>/change.diff`. An empty candidate gets
  `candidates/<id>/NO_CHANGES.txt` (one line) so a `not met` has something to cite. Deleted files appear in `change.diff` only.
- **Scrubbing**: post-images, diffs and the sheet through `ScoutScrubber.Scrub` with ONE `ScrubDenyList` = the operator's local
  deny-list entries + `opus`, `sonnet`, `fable`, `haiku` + every `--model-id`. `ScoutScrubber` has no model-name rule of its own, and
  §12's model deny-list is series-only; this composed list is the pilot's minimum (see § Decisions taken here). A scrub must not change
  a file's line count (cites depend on it) — checked per file, a change refuses the sheet.
- **Bearing ranges** (the parent names them but defines no rule): per id, per changed file, the post-image line ranges of every hunk;
  an R-item that carries `anchors` (E1.S10's `requirements[].anchors`) lists only the ranges whose path or post-image text contains an anchor, otherwise all
  of that id's ranges, said so in the sheet. The sheet is the record and the reader's map; codex may open any file.
- **Outputs**: the tree; `<…>/<sheet id>/batch.json` (E4.S7's `ReadingBatch` with every file's line count, the tree path, the sheet
  id) for `read`; `research/policy-ab/reading-sheet.md` — story, the R-items with "how to verify", per R-item per id the bearing
  ranges, the assessment tree's hash; no label anywhere. Its header states the known residual: the reference's code comments name its
  review rounds (`typedFixtures.test.ts` at S: "E1 plan round, finding 0", "E1 code round, finding 5") — not edited, because editing the
  reference changes what is read; `pilot.md` lists it under partial blinding.

**Tests** (`tests/Bench.Tests/Policy/ReadingSheetTests.cs`, `AssessmentTreeTests.cs`; fixture diffs + a `DatedGitRepo` mirror).
- RED: a tree test against a builder that copies diffs unscrubbed — red on the planted `opus` comment.
- Ids: 8 lower-case hex, distinct; two seeds → two orders; no label (a cell GUID, `reference`, `alternative`) in any tree file or the
  sheet — searched as text.
- Docs hunks absent; a home path, an e-mail and `Opus` in a code comment → placeholders, line counts unchanged; an empty candidate →
  `NO_CHANGES.txt`; the key file outside the tree and its parent; `batch.json` round-trips into `ReadingBatch` with line counts equal to
  the files'; bearing ranges for a two-hunk file, with and without anchors.
- `ScoutScrubber.Leftovers` empty over the sheet and every tree file.
- **Break-it**: scrub the diffs only, not the post-images → the planted `opus` reaches `candidates/<id>/…` → red; restore.

**Done when.** The above green; the scouts suite green.

**Depends on.** E4.S1, E4.S7; E1.S10's record (R-items with "how to verify", the frozen tree, P, S). In the pilot, `--model-id`
takes the resolved ids from E3.S6's `pins.json`.

**Model.** Scenarios + brief: Opus · code + tests: Sonnet · review: Opus · fixes: Sonnet.

---

### E4.S9 — `PolicyRequirementReader` and verb `bench policy read`

**Goal.** Parent §3.8 "The launch", "The answer", "Sanity on the reference", "Spot-check": one batch → one codex launch, read-only,
the reference in the same batch, the ≥ 90 % gate before any candidate's coverage, the spot-check sheet for `handcheck.md`.

**Scope.** New `src/Bench.Application/Policy/PolicyRequirementReader.cs`, mirroring `ScoutClaimAssessor`
(`src/Bench.Application/Scouts/ScoutClaimAssessor.cs:25-48`) — no shared base for the ~10 lines they share (parent §3.8); the batch
folder through the existing `IScoutAssessmentFiles` (`ScoutClaimAssessor.cs:12-18`) with a new file adapter rooted at
`<artefact root>/policy/<story>/read/`, its containment check as `src/Bench.Infrastructure/Scouts/ScoutArtifacts.cs:215-240`.
`FindingAssessor.OptionsFor` (`src/Bench.Application/Gate/FindingAssessor.cs:85`; file names `:37-39`). `prompts/policy-ab/read-requirements.md`
read through `PromptCatalog`: `GateRubric` (`src/Bench.Application/PromptCatalog.cs:91-105`) already reads one whole file, LF-normalised
and hashed — extract that into one `PromptCatalog.Whole(root, role, name)` that `GateRubric` calls (if E3.S3 extracted it first for
`run.md`, use theirs). New `hosts/Cli/PolicyReadCommand.cs` shaped on `hosts/Cli/ScoutsAssessCommand.cs:23-38`, codex via
`ScoutsInputs.Executable` (`hosts/Cli/ScoutsInputs.cs:251`).

**Brief.**
- `PolicyRequirementReader.AskAsync(ReaderLaunch launch, ReadingBatch batch, string treeRoot, string rubric, string keyPath, …)`:
  fresh batch folder → `prompt.txt` + schema → `runtime.AskAsync(new AgentAsk(ModelRuntimeKind.CliCodex, exe, prompt, treeRoot, wall,
  model) { Options = FindingAssessor.OptionsFor(ModelRuntimeKind.CliCodex, directory) })` (`src/Bench.Application/CliAgent.cs:21`,
  `:289`) → `PolicyRequirementReading.Read`. Refused → ONE re-ask in a fresh batch folder, the defects quoted through `retryNote`;
  refused twice → *not captured*. Only after the answer is read (accepted or finally refused) is the key copied into the batch folder
  (`ScoutClaimAssessor.cs:24`, `:45`).
- `bench policy read --story <id> --sheet <sheet id> --reader-model <id> [--codex-exe <path>] [--read-timeout-minutes 20]
  [--reference-only] --artifact-root <dir>`. `--reader-model` is required — never the CLI's default model.
- Results: sanity first. Below 90 % → exit 1; `research/policy-ab/reading.json` holds `sanity: failed` and the reference's verdicts
  ONLY — no candidate coverage is computed or printed. Passed → `reading.json` with per (label × R-item) verdict, cite and note, the
  key joined after the read, coverage per label labelled *codex-read*, the reader model, rubric/prompt/schema hashes, attempts, batch
  ids, UTC times, codex's wall; codex spend is reported apart (a subscription, never inside an arm or the ceiling). And
  `research/policy-ab/handcheck.md` with every `not met` — the reference's included — as rows (id, label, R-item, cite, codex's note,
  an empty *agree / disagree + reason* column), headed **"a model checking a model — weaker than a human reading, the owner's choice
  (D7)"**. An Opus judge agent fills it in E5.S7, the main session verifying (parent D19); the verdicts are never overwritten.
- `--reference-only`: the dry run — a batch holding the reference alone; prints the parse and sanity result; writes nothing under
  `research/`.
- Exit codes: 0 read, sanity passed; 1 sanity failed (no-go: the checklist or the reader is broken); 3 codex failed, refused twice, or
  the tree is missing; 4 flags (no `--reader-model`, codex absent, timeout outside 1–120); 5 an empty batch.
- Residual, stated in `reading.json`: `-s read-only` lets codex READ any path on the machine; the key lives outside the tree and its
  parent until the read, which guards the reader's working set, not a determined search — the scouts' assessor carries the same.

**Tests** (`tests/Bench.Tests/Policy/PolicyRequirementReaderTests.cs`, `PolicyReadCommandTests.cs`; a scripted runtime like
`ScriptedAssessor`, `tests/Bench.Tests/Gate/Assessment/AssessRig.cs:176` — no live codex in the suite).
- RED: the launch test against a reader that never writes the key — red on "key present after the read".
- The launch: codex kind, read-only sandbox, schema and answer files in the batch folder, MCP off, cwd = the assessment tree, the
  batch folder outside the tree.
- At the moment `AskAsync` runs, the key is absent from the batch folder and the tree (the fake checks the disk); after, present.
- Refused once → re-asked with the defect quoted, accepted; refused twice → *not captured*, exit 3, nothing under `research/`.
- Sanity failure → exit 1, no candidate coverage in output or file; `handcheck.md` lists every `not met` with the reference's;
  `--reference-only` writes nothing under `research/`.
- **Break-it**: copy the key into the batch folder BEFORE the ask → the disk check sees it → red; restore.

**Live, attended (E4's Done-when):** one `read --reference-only` over the pilot reference — E4.S8's sheet built from the reference
alone — completes and parses, its sanity result printed.

**Done when.** The above green; the live dry run parsed; the scouts suite green.

**Depends on.** E4.S7, E4.S8; E1.S10's R-items; codex on this machine.

**Model.** Scenarios + brief: Opus · code + tests: Sonnet · review: Opus · fixes: Sonnet.

---

## Decisions taken here (the parent left them open — all accepted by the parent, 2026-10-14)

| # | Decision | Where | Why |
|---|---|---|---|
| 1 | *Bearing ranges* = the post-image hunk ranges per file, narrowed by an R-item's `anchors` when E1 records them | E4.S8 | the parent names them (§3.8) with no mechanical rule |
| 2 | Teeth is the plain revert of `PLAN_code_lane.md:165`; no `type StoredSecret = string` mutation | E4.S4 | a mutation aims at the reference's shape (§3.7 Teeth) |
| 3 | A neighbour the candidate removed, renamed or skipped is `missing` — reported, neither red nor green | E4.S4 | §3.7 is silent on it |
| 4 | Model names scrubbed by composing `opus`/`sonnet`/`fable`/`haiku` + resolved ids into the `ScrubDenyList` given to `ScoutScrubber` | E4.S8 | `ScoutScrubber` has no model rule; §12's deny-list is series-only |
| 5 | `PolicyRequirementReading.Read` refuses a whole malformed answer; it does not drop rows like `ScoutAssessment.Read` | E4.S7 | parent §3.8 "REFUSES a malformed answer" |
| 6 | Between `sheet` and `read` the key lives in `keys/`, outside the tree and its parent | E4.S8, S9 | two verbs need it on disk |
| 7 | Success's vacuous case — the principle is the parent's D13 (≥ 1 compiled spec-implied test, all compiled ones green); the exact rule is Fable's spec | E4.S3 | §3.7 "never Success = false" read literally made the empty candidate succeed |

## Definition of Done — E4

- [ ] E4.S1–S5, S7–S9 green from the test executable; every break-it named above went red with the real symptom and back; the scouts
      suite and `SolutionSignalsTests` unchanged and green.
- [ ] E4.S3's spec written by Fable (or Opus, said so) before its coder started; the ledger line in the parent names E4.S3.
- [ ] Parent §9 *Mechanical scoring* and *Codex reading* covered case by case.
- [ ] Live, attended: the reference scored Success with zero mismatches; the hand-made alternative scored Success with mismatches only
      where the interface is not spec-implied (`fairness.md`); one dry `read --reference-only` completed and parsed.
- [ ] The decisions above recorded in `research/module_policy_ab.md`; `research/module_tests.md` rows written.
- [ ] `node .claude/rules/shared/tools/plan-lifecycle.mjs` clean once the main session has added this file's `todo/README.md` row and
      the parent's link.
