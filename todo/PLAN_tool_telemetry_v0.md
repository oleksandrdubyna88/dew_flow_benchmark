# PLAN — server-side tool telemetry v0: schema, spool ingest, and the bench's own infrastructure

> Status: **plan only, nothing implemented yet, 2026-08-15.** Scope: this repository (domain type,
> Postgres table, `bench telemetry ingest`/`report` verbs, AppHost + Postgres container) plus the
> emitter in `dew_flow_mcp` (its own plan: `dew_flow_mcp · todo/PLAN_usage_telemetry.md`).
>
> Related: [PLAN_rag_bench_repo.md](PLAN_rag_bench_repo.md) §5.4 (the contract this implements) and
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

```jsonc
{
  "schema": "telemetry/v0",
  "at": "2026-08-15T12:34:56.789Z",          // UTC, emitter clock
  "emitter": { "app": "mcp-host", "pid": 1234, "machine": "…", "session": "…" },
  "caller": {
    "clientName":    { "captured": true,  "value": "claude-code" },   // from MCP initialize
    "clientVersion": { "captured": true,  "value": "2.x" },
    "model":         { "captured": false, "reason": "not carried by the MCP protocol" },
    "transport": "stdio"                      // stdio | http
  },
  "tool": "rt_read_local_file",
  "scope": { "workspaceRoot": "…" },          // what the call was scoped to; project id when known
  "argumentsJson": "…",                       // within the byte budget; truncation RECORDED
  "argumentsTruncatedBytes": 0,
  "outcome": "answered",                      // answered | refused | error — three states, not two
  "error": "",                                // the refusal/error text when outcome != answered
  "responseChars": 8192,
  "responseBody": "…",                        // within its own byte budget; size is always exact
  "responseTruncatedBytes": 0,
  "tokens": { "captured": false, "reason": "surface does not count tokens" },
  "serverMs": 13.4                            // server-side processing, never the caller's latency
}
```

The shape mirrors the domain's existing vocabulary deliberately: the captured/not-captured split is
`Captured` (`src/Bench.Domain/Trace/LegTrace.cs:9`), and the three-state outcome is the same lesson as
`ToolCall.Refused` (`src/Bench.Domain/Trace/LegTrace.cs:38`) — a refused call and an answered one are
otherwise identical from the outside.

**Retention is decided here, before the first write** (§5.4's explicit requirement): the byte budgets
on arguments and response body are applied **at emit time** — the spool never contains more than the
budget (default 4 KB each, emitter-configurable), so no later clean-up job exists to forget. Rows are
kept as written; aggregates are computed by the report, not stored.

**Idempotent ingest**: every record's SHA-256 (over the canonical JSON line) is the dedupe key — a
unique index in Postgres, `ON CONFLICT DO NOTHING` on insert. Re-ingesting a spool file, or killing
ingest mid-file and re-running, changes nothing. A spool file is renamed `*.ingested` only after every
line of it is committed.

**Aggregation keys on the report include the caller** — `(tool, clientName, model, transport)` — so a
mid-day switch of client or model cannot blend two populations into one row (the §5.4 lesson from the
upstream system that shipped daily aggregates without an engine column).

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
