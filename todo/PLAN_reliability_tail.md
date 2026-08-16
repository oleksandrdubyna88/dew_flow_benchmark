# PLAN — the reliability tail the 24/7 audit left open

> Status: **item 1 landed 2026-08-16; items 2–6 open.** Scope: `hosts/Cli`,
> `src/Bench.Infrastructure` (models, persistence, trace, git, process). The four CRITICAL/HIGH
> defects of the same audit — the unguarded drain loop, the uninvoked sweep, the missing signal
> handling and the null logger — were fixed in a separate task, and item 1 below came with them
> because the breaker and the per-leg guard are the same loop.
>
> Related: `.claude/rules/shared/common/reliability.md` (the doctrine this audit produced),
> `dew_flow_conventions · common/reliability.md:1` (its source of truth).

## Why this document exists

On 2026-08-16, the eve of the first long unattended runs, all four `dew_flow_*` repositories were
audited against one mission: **thousands of legs, 24/7, no leaks, no hangs, every failure legible in
the log afterwards.** The audit produced 49 findings. The ones that can END or INVALIDATE a campaign
were fixed the same day. What is written here is the rest — each item real, each cheap, none of them
able to kill a run, and all of them able to make a long run wasteful, unexplainable, or slowly fatal.

The reason they are a plan rather than a chat message: the audit's own most instructive finding was
`SweepAsync` — a crash-recovery path fully implemented, fully tested, and **called by nothing**.
Work that is written down but never triggered is the failure mode this family has already paid for.

## The symptom, per item

### 1. A dead model endpoint burns the wall clock for every remaining leg — HIGH · **LANDED 2026-08-16**

`src/Bench.Infrastructure/Models/OpenAiCompatibleRuntime.cs:150-153` defaults the per-leg wall budget
to **10 minutes** when the caller passes no budgets, and `hosts/Cli/RunCommand.cs:118` passes `[]`.
There is no consecutive-failure counter anywhere. So an endpoint that is simply *down* mid-campaign
does not fail the campaign — it costs up to 10 minutes × every remaining cell before anyone looks.
On a 10 000-cell run that is weeks of wall clock spent learning what the first failure already said.

**Fix:** a consecutive-failure circuit breaker around the leg loop. N legs failing for a transport
reason in a row ends the campaign with a reason naming the endpoint and the last error. The count and
the window are configuration, not constants. Distinguish a *transport* failure (breaker counts it)
from a *scored badly* leg (breaker ignores it) — the harness reports, it does not judge.

**Shipped** as `src/Bench.Application/LegDrain.cs` (`DrainLimits.ConsecutiveFailureBudget`, default 20,
`--max-consecutive-failures` on `bench run`), with a bounded backoff between consecutive failures so a
lost claim race can no longer spin at zero delay. **Deviation:** the breaker counts every leg that
produced no RESULT — a refusal or a fault — rather than classifying "transport" specifically, because
today a claim refusal is distinguishable only by its text and a typed `ClaimNextAsync` outcome is its
own change. The plan's real requirement is met exactly: a scored leg resets the run, so a subject
answering badly never trips it (`A_leg_that_merely_scored_badly_never_trips_the_breaker`). What is
still open here is the **wall-budget** half of the symptom — `bench run` still passes `[]` budgets, so
each of the N legs before the breaker fires can still cost the 10-minute default when the endpoint
hangs rather than refuses.

### 2. Two dictionaries that only ever grow — MEDIUM, latent today

- `src/Bench.Infrastructure/Trace/LiveTrace.cs:23,29` — `_byLeg.GetOrAdd(...)` per leg, and no
  `TryRemove` exists in the repository. Each `LegRecorder` holds a `List<ToolCall>` plus captured
  prompt and response text.
- `src/Bench.Infrastructure/Git/GitCheckoutProvider.cs:35,50` — one `SemaphoreSlim` per distinct
  repository key, never removed, never disposed.

Both are **inert right now**: `LegRunner` takes no `IRunTrace`, and `bench run` does not call
`ICheckoutProvider.EnsureAsync` (`RunCommand.cs:110` warns the target was not checked out). They stop
being inert the moment the long-running worker of `PLAN_variant_matrix.md` wires them in — which is
the deployment shape this whole audit is about.

**Fix:** give the trace a `Close(tuple)` that evicts after capture, and the checkout provider a
bounded or evicting lock map. Do it **before** the worker lands, not after.

### 3. Two counts cost a full hydration of the run — MEDIUM

`hosts/Cli/RunCommand.cs:167-179` calls `results.ForRunAsync(...)`, which at
`src/Bench.Infrastructure/Persistence/PostgresResultStore.cs:36-45` materializes every result row with
`.Include(r => r.Metrics)` — every prompt, every answer, every metric with its `MetadataJson`
deserialized — so that line 179 can compute `scored.Count` and `scored.Count(r => r.Passed)`.
At the "tens of thousands of cells" the schema comment itself targets
(`BenchDbContext.cs:148-151`), that is the whole run pulled into memory to print two integers.

**Fix:** a `COUNT` / `COUNT(...) FILTER (WHERE ...)` query. The repository already learned this exact
lesson once and fixed it: `PostgresTelemetryStore.TotalsAsync:69-133` replaced a client-side
percentile with `percentile_disc` in SQL, and its comment records the diagnosis. Apply it a second
time.

### 4. Telemetry ingest reads a whole spool file and builds one unbounded `IN (…)` — MEDIUM

`hosts/Cli/TelemetryCommand.cs:85-104` reads the file with `File.ReadAllTextAsync` (no size cap), and
`src/Bench.Infrastructure/Persistence/PostgresTelemetryStore.cs:28-31` then asks
`.Where(t => byFingerprint.Keys.Contains(t.Fingerprint))` for every fingerprint in that file at once.
A busy 24/7 emitter produces spool files where both halves of that sentence are a problem.

**Fix:** stream the file line by line and ingest in chunks of a fixed size, so memory and the
parameter list are both bounded by the chunk rather than by the emitter's productivity.

### 5. `Process.Kill` catches one exception type — LOW

`src/Bench.Infrastructure/Process/ProcessRunner.cs:105-116` guards the tree-kill with
`catch (InvalidOperationException)` only; a `Win32Exception` (access denied, or the process exited
between the check and the call) escapes. This launcher is otherwise the family's reference
implementation and is being copied into `dew_flow_rag_qln` in the same audit's other task — so the
gap would be copied with it.

**Fix:** catch `Win32Exception` beside it. One line, and it stops a good pattern from propagating a
sharp edge.

### 6. `logs/` has no retention owner — LOW, and now a rule

`BenchLogging.RunFilePath` writes `logs/{yyyy-MM-dd}/{app}-{HH-mm-ss}-{pid}.log`, one file per run,
and nothing ever deletes a day folder. `bench telemetry prune` exists for spools; its reasoning was
never extended to the host's own logs. The shared rule now requires every repository to NAME its
retention owner — see `.claude/rules/shared/common/logging-serilog.md` § Retention.

**Fix:** the default option in the rule — prune day folders older than 14 days at host startup,
best-effort, logged at Information — or a named operator job recorded in the README. Pick one; the
rule forbids leaving it unnamed.

## Build order

1. ~~**(1) circuit breaker**~~ — landed 2026-08-16 with the CRITICAL fixes; its wall-budget tail is
   still open (see the item).
2. **(5) `Win32Exception`** — one line, and it must land before the launcher is copied elsewhere.
3. **(3) summary counts** and **(4) chunked ingest** — independent, either order.
4. **(2) bounded dictionaries** — must precede the long-running worker; after it, they are live leaks.
5. **(6) log retention** — independent of everything above.

## Test plan

Per `.claude/rules/shared/common/testing.md`, every item starts with a RED test named for the
guarantee, observed failing for the real symptom:

| item | test name |
|---|---|
| ~~1~~ | shipped as `A_systemically_broken_environment_ends_the_campaign_instead_of_grinding_through_every_cell` (`tests/Bench.Tests/Application/LegDrainTests.cs`) |
| ~~1~~ | shipped as `A_leg_that_merely_scored_badly_never_trips_the_breaker` |
| 2 | `A_captured_leg_is_evicted_from_the_trace_and_does_not_accumulate` |
| 3 | `The_run_summary_does_not_hydrate_the_run_to_count_it` (assert via query count / no-tracking materialization, not timing) |
| 4 | `A_spool_larger_than_one_chunk_is_ingested_in_bounded_batches` |
| 5 | `A_process_that_exits_between_the_check_and_the_kill_is_not_an_error` |
| 6 | `A_day_folder_older_than_the_window_is_pruned_at_startup` |

Run the test project's executable — never `dotnet test`; the platform has no VSTest host.

## Definition of Done

- [ ] Every item above is either implemented, or explicitly declined here with the reason recorded.
- [ ] Each implemented item has a RED-then-GREEN test, and the summary quotes both observations.
- [ ] The circuit breaker's thresholds and the retention window are configuration, not constants.
- [ ] `logs/` retention has a named owner, per the shared logging rule.
- [ ] The whole suite passes against a freshly built binary — timestamps checked, per
      `.claude/rules/shared/common/development-workflow.md` § Verify the ARTEFACT.
- [ ] When this plan finishes, it is promoted to `research/` with its deviations recorded, and the
      *Currently open* table below is updated in the same task.
