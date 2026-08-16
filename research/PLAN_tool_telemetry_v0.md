# PLAN — server-side tool telemetry v0: schema, spool ingest, and the bench's own infrastructure

> Status: **IMPLEMENTED, 2026-08-15** — authored and shipped the same day, both halves. 124 tests green
> in this repository, 0 warnings; the emitter shipped alongside in `dew_flow_mcp` (54 tests green) and
> its design record is `dew_flow_mcp · research/PLAN_usage_telemetry.md`. Scope as built: domain record
> + codec, Postgres table + migration, `bench telemetry ingest`/`report`, and the AppHost with this
> benchmark's own Postgres container.
>
> **Deviations.**
> 1. **The fixture is the emitter's real output, not a hand-authored sample.**
>    `tests/Bench.Tests/Fixtures/mcp-spool-v0.jsonl` was produced by `dew_flow_mcp`'s own sink tests and
>    copied verbatim. A fixture written here would have proven only that this codec agrees with our idea
>    of the other repository — which is the one thing a cross-repository contract must rule out.
> 2. **`IngestReport` carries a documented equality trap.** It is a record struct holding a list, so
>    `==` compares the reasons by REFERENCE. Found by a test that asserted value equality and failed;
>    the type now says so in its remarks and callers compare counts.
> 3. **Ingest exit codes are sharper than planned.** A line this build cannot read exits `1` (a real
>    finding: an emitter writing a version we do not know), an unreadable spool exits `3`, and an empty
>    spool exits `0` — "nothing to ingest" is the normal state of an already-drained spool, not a failure.
> 4. **`bench telemetry report` over an empty store exits `5`, not `0`.** Nothing measured must not read
>    as "no problems found".
> 5. **A sub-verb is a WORD, not a flag** (`bench telemetry ingest`), so `CommandLine` grew positional
>    operands. `--action ingest` would have read as configuration when it is the command.
> 6. **The AppHost orchestrates a database and nothing else.** The CLI is a command an agent runs, not a
>    service an orchestrator supervises; it takes its connection string from `--connection` or
>    `ConnectionStrings__bench`. The API host joins when it exists.
> 7. **The logging rule was mirrored into this repository** (`src/Bench.ServiceDefaults`) because it
>    gained its first orchestrated host. The CLI deliberately does NOT take it: its stdout IS its
>    product (a report, or JSON), and a logging provider writing to that stream would corrupt the
>    contract the exit codes exist to keep.
>
> **Open tail — narrowed 2026-08-16.** The producer half is closed: the product host now registers the
> sink (`dew_flow_rag_qln · hosts/Daemon/Program.cs:120-121`, `AddTelemetrySpool(spoolDirectory, "daemon")`
> from the config key `Rag:Telemetry:SpoolDirectory`), so real product traffic can be metered — opt-in, a
> blank directory leaving `NullUsageSink` in place. What is still open is the *drain*: no spool from a real
> server run has been ingested through `bench telemetry ingest`, so the end-to-end path remains proven from
> the emitter's own output rather than from live traffic. That is the one claim this plan cannot close by
> reading code — it needs a run.
>
> Related: [PLAN_rag_bench_repo.md](../todo/PLAN_rag_bench_repo.md) §5.4 (the contract this implements) and
> §7 (the AppHost/Postgres this stands up); [research/MEASURED_LESSONS.md](../research/MEASURED_LESSONS.md)
> §3 (why "refused ≠ answered" and "not captured ≠ zero" are load-bearing).

## Goal, and the operator decisions that fix the shape

The founding plan's §5.4 defines server-side tool telemetry — every tool call the MCP surface serves,
benchmark and real sessions alike — and its open question 5 asked who owns the contract and where the
data lands. Both are now decided (operator, 2026-08-15):

1. **This repository owns both versioned contracts** — the white-box funnel (§5.2) and the tool
   telemetry (§5.4) — under one versioning scheme. Emitters live where the traffic is
   (`dew_flow_mcp`); the schema, the ingestion and the report live here.
2. **Everything lands in the benchmark's own Postgres.** No telemetry table in the product database,
   no new database for the MCP repo (it stays dependency-free and public).
3. **The transport is a local spool, not a live connection.** The emitter appends JSONL to a spool
   directory and never blocks, never fails a tool call, and never needs this repository's host to be
   up. `bench telemetry ingest` drains spool files into Postgres — idempotent and resumable, per the
   CLI contract (§6 of the founding plan). A live HTTP ingest can come later with the API host; the
   spool stays the durable path either way.
4. **Caller identity is recorded to the limit of what the transport knows.** The MCP `initialize`
   handshake carries the client name and version; the protocol does not carry the model, so for real
   sessions the model renders as *not captured* — never guessed, never defaulted. Benchmark legs
   self-declare their model, so benchmark traffic is fully attributed.

## The v0 record — one tool call, as JSON on the spool and one row in Postgres

As shipped, copied from a real spool line rather than transcribed
(`tests/Bench.Tests/Fixtures/mcp-spool-v0.jsonl`, reformatted here):

```jsonc
{
  "schema": "telemetry/v0",
  "at": "2026-08-15T09:30:01+00:00",          // UTC, emitter clock
  "emitter": { "app": "mcp-stdio", "pid": 1234, "machine": "JINX" },
  "caller": {
    "clientName":    { "captured": true,  "value": "claude-code", "reason": "" },
    "clientVersion": { "captured": true,  "value": "2.0.0",       "reason": "" },
    "model":         { "captured": false, "value": "",            "reason": "the MCP protocol carries no model identity for the caller" },
    "transport": "stdio"                      // stdio | http | in-process
  },
  "tool": "rt_read_local_file",
  "scope": "D:/work/repo",                    // what the call was scoped to
  "argumentsJson": "{\"path\":\"a.txt\"}",    // within the byte budget; truncation RECORDED
  "argumentsTruncatedBytes": 0,
  "outcome": "answered",                      // answered | refused | error — three states, not two
  "error": "",                                // the refusal/error text when outcome != answered
  "responseChars": 42,                        // ALWAYS exact, even when the body below was cut
  "responseBody": "lines 1-3 of 3",           // within its own byte budget
  "responseTruncatedBytes": 0,
  "tokens": { "captured": false, "value": 0, "reason": "this surface does not count tokens" },
  "serverMs": 13.4                            // server-side processing, never the caller's latency
}
```

**Three differences from this plan's first draft, and they are worth naming** because each was a place
where the drafted shape was more elaborate than the truth. `scope` is a **string**, not an object: the
emitting surface has exactly one thing to say about scope, and an object invites a consumer to expect
fields nobody fills. `emitter` carries no `session`: nothing in the pipeline has a session identity
that outlives a call, so the field would have been empty on every line. And every captured value ships
**both** `value` and `reason` even when unused — a consumer must never infer "unknown" from an absent
key.

The shape mirrors the domain's existing vocabulary deliberately: the captured/not-captured split is
`Captured` (`src/Bench.Domain/Trace/LegTrace.cs:9`), and the three-state outcome is the same lesson as
`ToolCall.Refused` (`src/Bench.Domain/Trace/LegTrace.cs:49`) — a refused call and an answered one are
otherwise identical from the outside. The emitter's own record of what it writes is
`dew_flow_mcp · research/telemetry_v0_wire.md`.

**Retention is decided here, before the first write** (§5.4's explicit requirement): the byte budgets
on arguments and response body are applied **at emit time** — the spool never contains more than the
budget (default 4 KB each, emitter-configurable), so no later clean-up job exists to forget. Rows are
kept as written; aggregates are computed by the report, not stored.

**Idempotent ingest**: every record's SHA-256 over its CANONICAL form — re-serialised from the domain
record, never hashed from the raw bytes, so two spellings of one call cannot become two identities — is
the dedupe key, behind a unique index. Re-ingesting a spool file, or killing ingest mid-file and
re-running, changes nothing. A spool file is renamed `*.ingested` only after every line of it is
committed.

*As built*, the adapter reads the batch's known fingerprints and inserts the rest, rather than the
drafted `ON CONFLICT DO NOTHING`. Two reasons, and the second is the load-bearing one: the report needs
to distinguish *ingested* from *duplicate* (a resumed ingest reporting "0 new" is the proof it resumed
rather than re-inserted), and `ON CONFLICT` would silently absorb that number. The batch is also
deduplicated **within itself** first — a spool may legitimately contain one line twice, and the unique
index would otherwise reject the whole `SaveChanges` rather than the repeat, losing the file. The index
remains the real guard: it is what makes two ingests racing over one spool a conflict rather than two
inserts.

**Aggregation keys on the report include the caller** — `(tool, clientName, model, transport)` — so a
mid-day switch of client or model cannot blend two populations into one row (the §5.4 lesson from the
upstream system that shipped daily aggregates without an engine column).

## Addendum, 2026-08-15 — five things this plan got wrong, found by re-reading the shipped code

Recorded here rather than quietly fixed, because three of the four were *defended in a comment* at the
time they were written, and a wrong justification outlives the code it was attached to.

**1. The report aggregated on the client and the comment called it cheap.** LINQ cannot express a
percentile, so `TotalsAsync` projected each group's durations with `g.Select(t => t.ServerMs).ToList()`
and sorted them in memory — reasoning that this was "cheap next to the scan that produced it". The scan
happens in the database; the transfer does not. Every `ServerMs` in the table this plan calls *the
largest in the system by an order of magnitude* crossed the wire to compute two numbers. It is now raw
SQL with `percentile_disc` — the **discrete** form deliberately, because it returns a duration some
call actually took, while `percentile_cont` interpolates between two calls and reports one nobody
experienced.

**2. The report had no time window at all**, while an index on `At` sat unused by any query. `--days N`
now bounds it, the window reaches the database rather than trimming a list already paid for, and the
footer states the span even when it is "all time" — a total whose span is unstated is a number two
readers scale differently.

**3. Refusing a line by name and then retiring the file that held it is a promise broken in the same
breath as it is made.** A spool with an unknown-version line was renamed `*.ingested` like any other,
so the build shipped to read those records would arrive to find a file the ingest no longer looks at.
The codec now returns a three-case `LineVerdict`, and the distinction is real rather than cosmetic:
`UnknownVersion` is **retryable** and keeps the file in place, `Unreadable` is **permanent** — a
half-written last line is the normal shape of a killed emitter, and keeping those files would make
every ordinary spool immortal.

**4. The AppHost's database password was regenerated while its data volume persisted.** Aspire's
default is a per-run generated password; postgres reads `POSTGRES_PASSWORD` only when it *initialises*
a cluster. So the first run works and the **second** hands out a password the existing cluster has
never heard of — "password authentication failed for user postgres" against a database that is running
perfectly, which reads as a broken connection string rather than as a rotated secret. Found by
restarting the AppHost, not by a test. The password is now a parameter resolved from user secrets, so
it survives a restart the way the data does.

**5. Payload retention was decided; FILE retention was not.** `*.ingested` files accumulated on the
emitting server forever. `bench telemetry prune --spool <dir> --older-than <days>` retires them, as a
separate action that is never a step of ingest and never automatic, with **no default age** — deleting
somebody's only copy of their data is not a thing to have a default for. It touches `*.ingested` only:
a spool still holding unread records is not a candidate at any age. It needs no database, so it does
not ask for a connection string; requiring one would make pruning impossible on the machine that emits.

## Infrastructure this stands up (founding plan §7, the unbuilt half)

- **`hosts/AppHost`** — Aspire, mirroring `dew_flow_rag_qln`'s precedent: its own Postgres container
  with a persistent volume, connection string flowed to the CLI/API via configuration. Nothing else
  joins it yet (engines are processes the CLI runs, not resources).
- The existing `BenchDbContext` (`src/Bench.Infrastructure/Persistence/BenchDbContext.cs`) gains a
  `ToolTelemetryRow` table + migration. `PostgresRunStore` is untouched.

## Build order

1. **Domain + contract**: `TelemetryRecord` (pure record in `Bench.Domain`, reusing `Captured`),
   `telemetry/v0` as its schema constant; JSON codec in `Bench.Application` with strict validation —
   an unknown schema version is a per-line refusal that names the version, never a crash and never a
   silent skip.
2. **Ports**: `ITelemetryStore` in `Bench.Application` (`AppendAsync` batch + `ReportAsync`
   aggregate), following the `IRunStore` split (`src/Bench.Application/RunStore.cs`).
3. **Postgres adapter**: `ToolTelemetryRow` + migration + `PostgresTelemetryStore`; unique index on
   the record hash.
4. **CLI verbs**: `bench telemetry ingest --spool <dir> [--json]` (drains `*.jsonl`, renames to
   `*.ingested`, reports counts: ingested / duplicate / refused lines, exit codes per the contract —
   refused lines are a `1`, an unreachable database a `3`); `bench telemetry report [--json]`
   (per-key counts, duration percentiles, outcome split; *not captured* renders as its own bucket,
   never as zero or as the popular value).
5. **AppHost + Postgres container**, wired so `bench` finds the connection string without hand-set
   environment variables.
6. Founding plan §5.4/§12.5 annotated: contract owned here, emitter plan referenced.

## Test plan

- Codec: v0 round-trip; unknown schema version refused per line with the version named; a
  `captured:false` field never yields a value downstream.
- Ingest idempotency: the same spool file twice → second pass all-duplicates, zero new rows
  (Testcontainers, mirroring `PostgresRunStoreTests`).
- Partial-file resume: kill after N lines (simulated by ingesting a truncated copy, then the full
  file) → exactly the missing lines land.
- Outcome triage: `answered`/`refused`/`error` arrive as three distinct values and the report never
  merges them.
- Report keys: two clients on one tool in one day produce two rows, never one blended row.
- Exit codes: refused lines → 1; DB unreachable → 3; empty spool → 0 with an explicit "nothing to
  ingest".

## Definition of Done

- [ ] `telemetry/v0` is a named, versioned schema; an unknown version is refused per line, not crashed on.
- [ ] Arguments and response bodies are byte-budgeted at emit; sizes are always exact; truncation is recorded.
- [ ] Outcome is three-state; *not captured* is a distinct state for model and tokens and renders as such.
- [ ] `bench telemetry ingest` is idempotent and resumable; the CLI exit-code contract holds.
- [ ] Report aggregates key on caller (client, model, transport) and tool.
- [ ] The AppHost runs its own Postgres container; no product database is touched.
- [ ] The emitter side ships from `dew_flow_mcp` per its plan; a spool produced there ingests here —
      proven by a fixture spool file committed in this repository's tests.

## Addendum, 2026-08-15 — correlation, added while v0 was still v0

The contract shipped without any way to say which leg a call belonged to. For the reading lane that is
survivable; for the **fix lane it is not** — a fix task budgets *investigate*, *fix* and *verify*
separately, and without correlation the server's own time and tokens can only be attributed to the whole
leg, which makes a phase that blew its ceiling indistinguishable from one that was cheap.

`TelemetryCorrelation(Leg, Phase)` is now on the record, and three properties make it safe:

- **It is what the CALLER said**, not what the server knows. An MCP server has no idea what a benchmark
  leg is, and giving it one would make the surface unshippable to anyone not running this harness. The
  harness puts its cell id in; a real session puts nothing, and reads as unattributed — the same honest
  absence as `Caller.Model`.
- **It is additive within v0.** A line written before the field existed carries no `correlation` object
  and still reads, as unattributed. The evidence is the fixture: it is a verbatim copy of what
  `dew_flow_mcp`'s emitter actually wrote before this existed, and the compatibility test reads it rather
  than a line authored to agree with the reader.
- **It reaches the fingerprint**, so two identical calls from different legs — or different phases of one
  leg — are different records and the idempotency guard cannot merge them. Proven by removing correlation
  from the wire: three tests went red on one shared hash.

`ByPhaseAsync(leg)` is the query it exists for. Unattributed traffic is excluded from it entirely rather
than folded in, and tokens are summed only over the calls that reported them — a file read has no tokens,
and its absence is not a zero.

**Still open:** nothing writes a correlation yet. The emitter half lives in the other repository, and the
harness will supply the cell id when the engine port lands.
