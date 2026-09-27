# Module — Gate: the coai gate-model benchmark

> Status: **the domain and the contracts (E1) and the store and the privacy guard (E2) exist, 2026-09-27; no
> product session is driven yet.** The driver (E3), the assessment (E4), the import (E5), the report surfaces and
> the page (E6) and the first campaign (E7) are open in [todo/PLAN_coai_gate_model_benchmark.md](../todo/PLAN_coai_gate_model_benchmark.md).
> This file describes what is built; a sentence here about something that does not run is a bug in the file.

## Purpose

Which reviewer MODEL, on each of the product's three review gates (plan · code/diff · feature), finds the
planted defects — at what cost, how repeatably. The product is `coai-mcp` (ConnectOtherAIs), driven the way a
person's editor drives it, never a side harness. The measurement model is the retrieval benchmark's, applied
to a different leg: the subject is a **reviewer plus its calibrated transport**, the task is a **frozen seeded
case** that serves all three gates, a run produces findings, a blinded strict assessment says which were
supported and which seeds were hit, and every number is a comparison inside one scope.

Three things this module exists to make impossible, each already paid for elsewhere in this repository:

- **a mean over two populations** — two products, two settings hashes, two rubric kinds in one column;
- **a gap rendered as a fact** — an unassessed finding as `0 %`, an unmetered reviewer as free, two repeats as a
  variance;
- **private text on a public page** — the suite, the checkouts, every prompt and answer and every finding's
  text stay outside git and outside the database; the database holds ids, hashes, enum names and numbers.

## The measurement tuple

```
gate      = plan | code | feature                          GateKind
task      = (taskId, language, hostedGates, calibration,   GateTask → TaskSummary is what the store holds
             seeds[] as (id, crossEpic))
suite     = GateSuite.Stamp = id#hash12                    hash of the tasks' LENGTH-PREFIXED canonical forms;
                                                           the clone location and the private names are NOT
                                                           inputs
reviewer  = (reviewerId, ReviewerDefinition.Hash)          runtime WORD (api · codex · gemini · claude ·
                                                           antigravity · local · remote) · model · endpoint ·
                                                           key NAME · transport (dialect, effort, maxTokens,
                                                           timeout, followUps, cap, thinking) · prices · gates
product   = ProductPin (binarySha256, versionText,         stored per CELL at claim time; a moved product
             gitSha, dirtyFiles as CapturedCount,          is refused naming both shas
             checkedTree)
settings  = settingsHash                                   every COAI_* as sent, minus secrets (E3)
repeat    = 1..n, repeats OUTERMOST                        GateMatrix.Plan
```

`GateScope = (suiteStamp, gate, product.versionText, product.binarySha256, settingsHash)` is what must match
before two runs may be put beside each other; everything else is an axis compared along. `GateReport.Scopes`
partitions a run set by it — two pins in one campaign are two partitions, and so are two builds that print
the same `--version` text from different bytes (a dirty rebuild at one HEAD), never a mean.

Inside a scope, `GatePopulation` decides WHICH runs and verdicts every figure is over, once: one run per cell
(campaign, task, reviewer, repeat) — its latest attempt; two campaigns in one scope are two runs of a repeat,
and an imported run carries its import's campaign id — with every attempt counted in the attempts columns; the
verdicts of ONE rubric (id, kind and hash), one per finding, a real reading preferred over an
`AssessmentFailure` and an independent assessor over one of the reviewer's own family; and a valid run that
found nothing counted as assessed with zero hits.

## Diagram

```mermaid
flowchart TB
    subgraph dom["Bench.Domain.Gate — pure, depends on nothing"]
        suite["GateSuite.Freeze<br/>GateTask · GateCase · SeedSpec · HostedGates"]
        reviewer["GateReviewer row<br/>ReviewerDefinition.Hash · ReviewerEndpoint · ReviewerTransport · ReviewerPrices"]
        vendor["CoaiVendorsSetting.From — the ONE producer of the vendors string<br/>ApplyTo(env) · CoaiVendorRow: vocabulary + typed reader"]
        pin["ProductPin.Continue"]
        matrix["GateMatrix.Plan — repeats outermost<br/>SlotRotation (shared with Matrix)"]
        cell["GateCell + GateCellLifecycle<br/>Claimable (shared with RunCell) · pin at claim · caller-minted id"]
        facts["GateRunFacts.From — the valid rule<br/>FailureCauses · GateReply · LedgerTurn · HttpCallFacts"]
        finding["GateFinding — hashes only<br/>FileHash = HMAC(FileHashKey, FindingPath.Normalise)"]
        verdict["GateVerdict.Under(RubricCatalog)<br/>Verdict: Strict · Lenient · AssessmentFailure"]
        population["GatePopulation.Of(scope, rubric)<br/>latest attempt per cell · one verdict per finding"]
        report["GateReport.PerModel / PerTask / Variance / Scopes<br/>Figure · Quantile.Q · PythonRound · SeedEvidence.Classify"]
    end
    subgraph store["the store (E2) — ports in Bench.Application.Gate, adapters in Bench.Infrastructure"]
        pgstore["PostgresGateStore : IGateStore<br/>six gate_* tables · guarded claim · one-statement hand-back<br/>the sweep never reaches a Finished or Failed run"]
        artstore["FileSystemGateArtifactStore : IGateArtifactStore<br/>artefact root OUTSIDE git · CellPaths · links resolved<br/>stage → flush → hash → rename · file-hash.key owner-only"]
        completion["GateCellCompletion<br/>artefacts, run.json last → refs → settle"]
        pub["PublicationGuard + FailureRedaction<br/>PostgresGatePublicationSource: every gate_* row, from the EF model<br/>bench gate export --public · bench gate prune"]
    end
    contracts["Bench.Contracts — Gate*Dto<br/>no free text reachable (type-graph walk, Type.Property allow-list)"]
    guard["ArchitectureTests<br/>deciders in Bench.Domain · Ui → Contracts only · one setting producer · name private"]

    suite --> matrix
    reviewer --> vendor
    reviewer --> matrix
    matrix --> cell
    pin --> cell
    facts --> population
    finding --> verdict
    verdict --> population
    population --> report
    report -. "mapped by the Application layer (E6)" .-> contracts
    cell --> pgstore
    finding --> pgstore
    completion --> artstore
    completion --> pgstore
    pgstore --> pub
    guard -. asserts .-> dom
    guard -. asserts .-> contracts
```

## Core entities, and the rule each one carries

| entity | file | the rule |
|---|---|---|
| `GateSuite` | `src/Bench.Domain/Gate/GateSuite.cs` | frozen over SNAPSHOTS (the task list is copied, every task holds its own copy of its seeds — a caller editing its lists afterwards changes nothing), hashed; the stamp is `id#hash12` over the tasks' canonical forms. A **moved clone keeps its stamp** (`CloneLocation` is not an input); private names are a guard input, not a hash input. `Task(id, gate)` refuses a task that cannot host the gate; `SeedsOf` refuses a task with no seeds — no zero-of-zero recall |
| `GateTask` · `GateCase` · `SeedSpec` | `GateTask.cs`, `SeedSpec.cs`, `CanonicalFields.cs` | the trial's shape field for field; a seed without trigger, mechanism AND consequence is refused, because the strict rubric judges all three. Every canonical form is **length-prefixed** (`CanonicalFields`, `length:value` per field, the seed count a field of its own) — a separator inside free text used to forge a boundary: two different cases, and one seed spelling two, stamped alike. The plan path is **repository-relative** (`RepositoryRelative`): rooted (`/x`, `\x`, `C:\x`, `C:x`), a url, or a whole `..` segment is refused. `TaskSummary` / `SeedRef` are what the database holds — no text, no path |
| `GateReviewer` · `ReviewerDefinition` | `GateReviewer.cs`, `ReviewerDefinition.cs` | the variant-catalog row, mirrored: added and retired, never edited, hashed; two rows with one hash are reported as one configuration (`GateReviewerCatalog.SameConfiguration`). The **transport is part of the subject**, so `effort` changes the hash. The definition's and the transport's canonical forms are length-prefixed (`CanonicalFields`) — a `|`-joined form let a model and a url trade text and keep one hash. Key names and refs are NAMES (`ModelConfig.IsReference`) |
| `ReviewerEndpoint` · `ReviewerRuntime` | `ReviewerEndpoint.cs` | a public vendor url is a VALUE; a loopback, private-range, link-local, `localhost`, `.local` or bare-name address is refused as a value and stored as a REFERENCE — `ModelConfig`'s rule, inverted for addresses. An IPv4-mapped IPv6 address (`[::ffff:10.0.0.7]`) is normalised to the IPv4 it carries before the range checks. The runtime is one member per product WORD — `Api`, `Codex`, `Gemini`, `Claude`, `Antigravity`, `Local`, `Remote`; there is no `cli`, because the product runs a word it does not know on Codex |
| `CoaiVendorsSetting` · `CoaiVendorRow` | `CoaiVendorsSetting.cs`, `CoaiVendorRow.cs` | `CoaiVendorsSetting.From` is the one producer of the vendors string and lives INSIDE the type: private constructor, private variable name, `ApplyTo(env)` the only way into an environment (it removes any inherited spelling of the variable). An architecture test reflects over every production assembly for any other member that returns a setting and any constant holding the name, each with a planted negative. **Only the gate under measurement is ticked**; the runtime is the product's word; an effort of `none` (the module default) is not written. `CoaiVendorRow` is the vocabulary — `KnownFields`, each with the JSON type the product reads — and the reader: an unknown field, or a known one of the wrong type (`"plan":"false"`), is refused by name; absent or `null` is the product's default |
| `ProductPin` | `ProductPin.cs` | `Continue(campaign, current)` refuses a moved product naming both shas; an imported pin (no binary hashed) never matches; a binary outside a checkout has an empty git sha and a dirty count that is *not captured*, never zero |
| `Claimable` (shared) | `src/Bench.Domain/Runs/Claimable.cs` | the four claim fields and the claim/settle/reclaim/stale transitions, composed by `RunCell` and `GateCell`; `MaxAttempts = 3` lives here once |
| `GateCell` · `GateCellLifecycle` | `GateCell.cs` | `Pending(id, runId, cell)` — the CALLER mints the id, the factory reads no clock; claimed UNDER a pin, refused without one; abandonment is `Claimable`'s rule |
| `SlotRotation` (shared) | `src/Bench.Domain/Runs/SlotRotation.cs` | the global slot rotation, called by `Matrix.Plan` and `GateMatrix.Plan` |
| `GateMatrix` | `GateMatrix.cs` | task × reviewer × repeat, **repeats outermost** (the three repeats of one task are never adjacent), reviewers rotated, repeats numbered from one |
| `GateRunFacts` · `FailureCauses` | `GateRunFacts.cs` | a port of the other harness's `summarise` / `failure_cause`: **valid** = verdict ∈ {proceed, revise} ∧ ≥ 1 ledger turn ∧ every turn `ok` ∧ a findings LIST. Tokens sum what was captured or are *not captured*; cost is `CapturedUsd` — unknown, never free; review and per-turn seconds round through `PythonRound`, as `summarise` does. The failure's first reason is its `FailureKind`; every reason is in the text |
| `GateFinding` · `FileHashKey` · `FindingPath` | `GateFinding.cs`, `FileHashKey.cs` | ordinal, severity, category, gating, line, `TextHash`, `FileHash` — the only text the type graph can carry is the two hashes (a walk test); the one constructor for a NEW finding takes the text and keeps the hash (`Stored` only reads back a row, refusing any hash that is not 64 lower-case hex). `FileHash` is **HMAC-SHA256** under a `FileHashKey` (at least 32 bytes, never printed) of the path in its one normal form (`\` → `/`, empty and `.` segments dropped) — a plain SHA-256 of a guessable path is confirmed by hashing candidates. The key lives ONLY in the artefact root, never in git or the database; the domain takes it as a value; the artefact root creates, reads and refuses it (`GateFileHashKeys`, E2) |
| `Rubric` · `RubricCatalog` · `Verdict` · `GateVerdict` | `Rubric.cs`, `Verdict.cs` | a verdict is issued under a rubric the catalog holds, of that rubric's kind; `AssessmentFailure(cause)` is a verdict case that never counts in a rate; the cluster key travels as a hash |
| `GatePopulation` | `GatePopulation.cs` | which runs and verdicts a figure is over: the latest attempt per cell (campaign, task, reviewer, repeat), every attempt kept beside; one rubric (id + kind + hash); one verdict per (run, finding) — real reading over `AssessmentFailure`, independent assessor over family-matched, then assessor and batch id; `IsAssessed` = has verdicts, or valid with zero findings |
| `GateReport` | `GateReport.cs`, `GateReportRows.cs`, `ReviewerAggregate.cs`, `Figure.cs`, `PythonRound.cs` | `PerModel(scope, rubric, input)` — the operator's columns, the `Rubric` required; **calibration tasks in `ModelTable.Calibration`, never in `Rows`**; `Runs` one per cell plus `Attempts` / `AttemptsFailed`; `AssessorFamilyMatched` counted apart; variance as two spreads over the cells, each with a state — seeds need 3 ASSESSED readings, findings 3 cells; `Unassessed` (`—`) where nobody looked; `Unknown` where nothing was metered; a failed run in every denominator; `AssessmentFailed` its own column; `TaskRowOf` / `VarianceOf` take their ids from the caller, so an empty group is an empty state; `Quantile.Q` = `report.py: q`; every rounding the Python report does (`q`, `pct`, the means, the costs) goes through `PythonRound` (the exact binary value, half to even), each pinned on vectors printed by the Python |
| `SeedEvidence` | `SeedEvidence.cs` | where a seed's evidence sat — pack / pack by file / on request / withheld / unknown — off the product's turn-1 prompt |
| `Gate*Dto` | `src/Bench.Contracts/GateContracts.cs` | figures travel as `GateFigureDto(known, value, state)`; `GateContractsGuardTests` walks every `Gate*Dto` into the nested types and collection elements it reaches and holds every text-bearing property — `string`, collections and dictionaries of strings, `object`, `JsonElement`, `JsonNode` — to an allow-list keyed by `Type.Property`, with `GateRunSummaryDto.FailureText` the one named exception; planted negatives (a `FailureText` elsewhere, a nested `FindingNote(string Title)`, `List<string>`) prove it bites |
| `GateRun` · `GateRunStatus` · `DataDirMode` · `GateSettlement` | `GateRun.cs` | a `bench gate run` invocation — the CAMPAIGN its cells belong to and the id every artefact path starts with. Forward-only status (Planned → Running → Finished or Failed); a terminal run's cells are never swept and never claimed; the data-directory mode is stored on the run so a resume cannot flip it. A product SESSION is one cell's settled attempt (only one attempt of a cell ever settles), so `GateRunRecord.RunId` is the cell's id. `GateSettlement` is `Completed(facts, findings, settingsHash, promptHash)` or `Failed(cause)`; `GateRunFacts.NotProduced(cause)` gives a failed session invalid facts with nothing captured — it stays in every denominator |
| `CellPaths` · `ArtifactPath` · `ArtifactScope` | `CellPaths.cs` | the ONE path function. `ArtifactPath` is relative to the artefact root: `/`-separated segments of `[A-Za-z0-9._-]`, never empty, `.` or `..`; rooted, drive, url and backslash forms are refused at parse time. `DataDirFor(run, cell, attempt)` is `runs/<run>/cells/<cell>/attempt-<n>/data` (isolated) or `runs/<run>/data-shared` (shared). `Allows(scope, path)` decides by SEGMENT (so `attempt-10` is not under `attempt-1`): an isolated cell cannot reach `data-shared`, a shared run cannot reach a cell's private `data`, no cell reaches another cell or another attempt |
| `ArtifactRef` · `ArtifactClass` · `ArtifactFootprint` | `ArtifactRef.cs` | what the database knows of a file: relative path, attempt, class, SHA-256, length — never the bytes. `Of` refuses a path outside the writing attempt; `Stored` re-parses a row's path, so a hand-edited `..` is refused on read. A footprint that could not be measured is `unknown`, never zero |
| `PublicationGuard` · `PrivateNames` · `FailureRedaction` | `PublicationGuard.cs` | the string guard: refuses `://`, a drive path, `/home/`, `\Users\` (and `/Users/`) and any private name (case-insensitive), naming table, column, row id and the RULE — never the text, and the row id itself is redacted, because a reviewer id can be the private name. The one column checked by a stricter rule than `://` is `gate_reviewers.EndpointUrl`: any non-empty value there passes only when `ReviewerEndpoint.Parse` reads a public vendor url — a schemeless `llm.corp.internal:8000` is refused as well (it spells no `://`). `FailureRedaction` replaces urls, machine paths and private names in the failure sentence before it is stored |
| `HashText` | `HashText.cs` | the one twelve-character short form of a hash every stamp uses — never a `[..12]` that throws on a shorter value |

## Entry points

- `bench gate export --public --db <conn> --suite-file <suite.json> --out <file.json>` — every `gate_*` row (read
  through the EF model, so a new column is exported and guarded without anyone listing it) through
  `PublicationGuard`, written as one JSON document (`kind: bench-gate-public-export`). **Built from database rows
  ONLY; it never opens the artefact root**, so nothing inside a request, a reply, a prompt, an answer or a findings
  file can reach it. Without `--public` → 4; without the suite's private names (`--suite-file` or
  `BENCH_GATE_SUITE`) → 4, because an export that does not know which names are private cannot prove it carries
  none; one violation → 5 and nothing written. The file is staged, flushed and renamed over the target; a target that cannot be written → 3, with the partial file removed.
- `bench gate prune --artifact-root <dir> [--tap-retention-days 30] [--dry-run] [--json]` — releases tap
  request/response bodies past the window from attempts that committed a `run.json`; lists unfinished and
  interrupted attempts without touching them, and never follows a link planted at an attempt's `tap/` (a prune
  deletes); prints every run's footprint (`unknown (why)` when it could not be
  measured). A root inside a git checkout → 4.
- The ports, for the driver (E3): `IGateStore` (plan · claim under a pin · settle with facts and findings · sweep
  · advance · cells · facts · artefact refs) and `IGateArtifactStore` (begin an attempt · write · read-and-verify ·
  attempts on disk · footprint · the file-hash key · prune), plus `GateCellCompletion`, the commit protocol.
  `GateFileHashKeys.ResolveAsync` is the one place the key is read, created or refused.
- `bench gate run | resume | status | sweep | probe | reviewers | suite verify` (E3), `bench gate assess` (E4),
  `bench gate import` (E5), `bench gate report` + `/api/bench/gate/*` + the Gate tab (E6) are open in the plan.

## The store — six tables, one migration, no existing table touched

| table | one row per | what it holds |
|---|---|---|
| `gate_runs` | `bench gate run` invocation | gate, suite stamp, data-dir mode, status, source (`native` or the harness an import came from) |
| `gate_cells` | task × reviewer × repeat | the claim (state, attempts, owner label/host/pid, claimed-at), the pin taken at claim, and once settled the session's facts — every count beside a *captured* flag, the vendor's finish WORDS, the verdict word, the failure KIND and the ONE free-text column, `FailureText` (redacted) |
| `gate_findings` | finding of a settled session | ordinal, severity, category, gating, line, `TextHash`, `FileHash`; `(cell, attempt, ordinal)` unique |
| `gate_verdicts` | verdict on a finding under a rubric | rubric id/kind/hash, the verdict case, the strict fields as enum names, cluster hash, seed id, assessor id, batch id, prompt hash, family match (written by E4) |
| `gate_reviewers` | reviewer catalog row | the definition flattened: runtime, model, the endpoint as a public url OR a reference name, key/creds/executable NAMES, the transport, prices, the gates ticked, added/retired (written by E3's `reviewers add`) |
| `gate_artifacts` | committed file | run, cell, attempt, class, RELATIVE path (unique), SHA-256, length |

**The claim** is one UPDATE guarded on `State == Pending` that sets the owner, the claim time, the pin and
`Attempts + 1` in the same statement — so the attempt number a cell is claimed at is its attempt directory — and a
run that ended is never claimed from. **The hand-back is ONE statement per stranded cell**, guarded on every fact
the sweep decided on — still claimed, the same owner (label, host, pid), the same claim time, the same attempt
count, a run that has not ended — and choosing requeue or abandonment (`Claimable.MaxAttempts`,
`FailureKind.Interrupted`) inside it. A hand-back never moves the count; the next claim moves it once. Candidates
are chosen the run store's way (stale in SQL, `WorkerIdentity.IsProvablyGoneOn` in memory). Measured by revert:
with the claim's `State` guard removed, 5 of 16 simultaneous claimers won one cell; with the hand-back's guard
reduced to the id, 6–8 simultaneous sweepers each counted the same cell. **The settle** is the guarded state change
plus the facts and the findings, in one transaction.

## The artefact root — layout and the commit protocol

```
<artifact-root>/                        refused if it is inside ANY git checkout (a .git folder or file above it)
  file-hash.key                         32 random bytes; created once (CreateNew), owner-only from creation
  runs/<runId>/
    data-shared/                        COAI_DATA_DIR of a SHARED run
    cells/<cellId>/attempt-<n>/         one cell attempt — created once, never reused
      data/                             COAI_DATA_DIR of an ISOLATED run
      reply.json, stderr.txt, ...       artefacts, each committed once
      tap/call-NN.request.json          tap bodies (released by prune)
      tap/call-NN.response.json
      tap/call-NN.json                  tap facts (kept forever)
      tap/pruned.jsonl                  what prune released: file, SHA-256, length, when
      run.json                          the LAST artefact an attempt commits
      interrupted.json                  written into an earlier attempt when a later one begins
```

**Containment** is decided twice. By segment, in the domain (`CellPaths.Allows`). On the real filesystem, by
`ArtifactContainment`: `Path.GetFullPath`, then every EXISTING component asked whether it is a symbolic link or a
junction (dangling ones included) and replaced by its resolved target, then a separator-aware starts-with
(case-insensitive on Windows) against the root AND against the attempt's own writable roots — and a writable root
that is itself reached through a link does not count as one, so `cells/<a>` made a link to `cells/<b>` cannot
carry cell a's writes into cell b's folder. The residual race — a link created between the check and the write —
needs a hostile process already running as the operator.

**The commit**: stage (`<name>.staging-<guid>`, `CreateNew`, write-through) → flush to disk → SHA-256 and length →
rename into place (refusing an existing target) → the `ArtifactRef`. `GateCellCompletion` then persists every ref
in one transaction and only then settles the cell — after first checking, before a byte is written, that the owner holds the cell and the scope's attempt IS the claim's attempt. Killed at each step in the tests: before the rename nothing
exists under the real name (the staging file does, and nothing reads it); after it the file is whole; after the
refs the cell is still claimed. In every case the sweep hands the cell back, the next claim is attempt 2, the next
`BeginAttemptAsync` creates `attempt-2` and marks `attempt-1` interrupted (kept, never continued), and the cell
settles — measured by revert: settling before the refs left a cell SETTLED over refs that were never written.
Directory entries are not flushed after a rename (.NET has no portable directory fsync); a power loss can lose a
rename, which the next attempt treats like any other interrupted one.

**The file-hash key** is `file-hash.key`, 32 bytes from the OS generator — never derived from a path, never in
the database, never printed (`FileHashKey.ToString` is redacted). Owner-only FROM creation: `UnixCreateMode` 0600
on POSIX, and on Windows a PROTECTED access list (inheritance cut) with one rule, the current user. Every READ re-checks that: a key whose permissions were loosened after creation is `Unusable`. A root that
has lost its key while `gate_findings` holds rows is REFUSED rather than re-keyed (`GateFileHashKeys`); two
workers on a fresh root race on `CreateNew`, and the loser reads the winner's key.

## The publication guard

Structural first: `GateEntitiesGuardTests` walks every entity the EF model maps to a `gate_*` table (from the
model, so a seventh table is walked the moment it is mapped) with the same `TextSurface` walk as the DTO guard,
and holds every text-bearing property to an allow-list keyed by `Type.Property`; `GateCellRow.FailureText` is the
one free-text exception. Then the string guard over REAL rows: `GatePublicationTests` re-reads every row of every
`gate_*` table — `gate_artifacts.RelativePath` included — on a database of its own, with the sample suite's private
names (`samples/gate-suite.sample.json`, always) and the operator's (`BENCH_GATE_SUITE`, when set), and a fixture
with one dirty row per rule, each named by table, column and row id. `bench gate export --public` calls the same
`GatePublication.Check`. The export never includes an artefact's contents: a private name planted inside a
findings file on disk is tested not to reach it.

## External dependencies

None. Everything in `Bench.Domain.Gate` is pure; `System.Text.Json.Nodes` (runtime) builds and reads the
vendor row, `System.Net.IPAddress` (runtime) classifies an endpoint's host, `System.Security.Cryptography`
(runtime) keys the file hash and `System.Numerics.BigInteger` (runtime) does `PythonRound`'s exact arithmetic.
The product's runtime words are pinned by a COPIED fixture, `tests/Bench.Tests/Fixtures/coai-runtime-names.json`
(`coai · src_mcp/runners/Reviewers/ReviewerRuntime.cs`, `RuntimeNames`, commit `9cb01a2b`), replaced from the
product's source when it grows.

The store (E2) adds no package: `Bench.Infrastructure` already carries EF Core and Npgsql; the key file's Windows
access list uses `System.Security.AccessControl` and `System.Security.Principal` (runtime, Windows-only calls
behind `OperatingSystem.IsWindows`).

## Growth surfaces

The tables and the artefact root exist since E2; the sizes are still the plan's projection (§4) until E7's first
campaign measures them.

| surface | projected at one full campaign (3 gates × 8 tasks × 4 reviewers × 3 repeats = 288 runs) | retires it | interrupted |
|---|---|---|---|
| `gate_runs`, `gate_cells` | one `gate_runs` row per campaign; 288 `gate_cells` rows, < 1 MB | kept forever (the measurement) | a cell `Claimed` by a dead worker is handed back at the next sweep by ONE guarded statement, ownership-checked (`WorkerIdentity.IsProvablyGoneOn`); three hand-backs → `Abandoned`; a cell of a Finished or Failed run is never swept |
| `gate_findings`, `gate_verdicts` | ~5 findings/run → ~1 500 rows; verdicts ≤ findings × assessors | kept forever | findings land in the settle's transaction or not at all; an assessment batch that dies leaves its ids unassessed (E4) |
| `gate_artifacts` | ~10 refs per attempt → ~3 000 rows | kept forever, beside the files they describe | refs are persisted in one transaction after every file is committed and before the settle — a crash between leaves files without refs, under an attempt the next attempt marks interrupted |
| the artefact root, `runs/<id>/` | measured 2026-09-27: 160 MB for 71 feature runs ≈ 2.3 MB/run → ~650 MB per campaign, mostly tap bodies | `bench gate prune` releases tap bodies past 30 days (`--tap-retention-days`), each call's facts made durable and its release logged to `tap/pruned.jsonl` before a body goes; request / reply / stderr / ledger / answers are kept forever | an attempt without `run.json` never finished — prune lists it and touches nothing; an interrupted attempt is kept whole; a prune killed half-way leaves the facts and some bodies, never neither, and the next prune finishes |
| staging files (`*.staging-<guid>`) | none in a healthy run; one per crash mid-write | nothing yet — counted in the footprint, read by nothing | a crash before the rename leaves one, never a half file under the real name |
| `file-hash.key` | 32 bytes, once per root | never — losing it refuses the root while findings exist | — |
| checkouts at the variant heads | 8 worktrees + bare mirrors, the size of the repositories | the checkout root's existing owner; `bench gate suite verify --prune` (E3) | — |
| `gate_reviewers` | tens of rows | never deleted, retired | — |

## Operator decisions assumed on 2026-09-27, pending the operator

Recorded in the plan's §9 and applied here where a type already carries them: an isolated data directory
by default (D4); a checkout build is "the product", pinned by sha (`ProductPin`); codex as the primary
assessor and the Claude CLI for an agreement figure (E4); the lenient 09-05/09-06 verdicts in their own
labelled column (`RubricKind.LenientWorth` is a separate population everywhere); the seeded 8-defect plan and
coai's own plans may go in `samples/`; the suite file lives in the local artefact root outside git; CLI
reviewers show *cost unknown*, never zero (`CapturedUsd`, `ReviewerPrices.Unknown`, `Figure.Unknown`); the
coordinator bumps the qln pin after E6.

## What does NOT exist yet

Everything that drives the PRODUCT: `ProcessSession` / `McpStdioClient` / `CoaiEnvironment` / the tap /
`ProductPin.Read` and the `bench gate run | resume | status | sweep` verbs that call the store (E3); the reviewer
catalog's store and verbs (E3 — `gate_reviewers` exists and is guarded, nothing writes it yet); the blinded export
and the assessor launch that write `gate_verdicts` (E4); the import of the 71 Python runs and the coai-bench
records (E5); the report verb, the API routes, the Gate tab and the mapping from `ModelTable` to
`GateModelTableDto` (E6). The per-model TABLES in the public export wait for E6's report; today the export is the
guarded rows.
