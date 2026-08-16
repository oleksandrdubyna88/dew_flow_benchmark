# PLAN — the reliability tail the 24/7 audit left open

> Status: **IMPLEMENTED, 2026-08-16.** All seven items shipped: the circuit breaker and the
> ownership-checked sweep earlier in the day (items 1 and 7), then item 1's wall-budget tail and items
> 2–6 in one pass — a leg-wide deadline, both unbounded dictionaries, the summary's two counts, a
> chunked spool ingest, the `Win32Exception` guard, and a named `logs/` retention owner. 449 tests, 444
> passing, 5 skipped (live QLN), 0 failing, against a Release build whose timestamps were checked.
>
> Deviations, item by item:
> - **1's tail** shipped as a domain type — `src/Bench.Domain/Runs/LegDeadline.cs` — rather than as a
>   check inside the loop the plan expected to exist by now. The loop still does not exist, which is
>   exactly why the mechanism is a value with its own tests: `LegDeadlineTests` runs the 25-turn
>   arithmetic the plan quotes without a lane to run it in, and `LegRunner.AskAsync` is the single place
>   the future loop turns. Consequence: the per-leg budget cannot be retrofitted after the first agentic
>   campaign, which was the whole point of placing this item before the loop.
> - **1's tail, second half.** `bench run` no longer passes `[]`: it asks for a wall ceiling
>   (`--leg-wall-seconds`, default 600 — the runtime's own former fallback, now owned and printed) and
>   **confirms it with the runtime before any cell exists**. That confirmation step
>   (`Bench.Application/BudgetConfirmation.cs`) is new and was not in the plan; it turned out to be the
>   FIRST caller of `IModelRuntime.AcceptBudgetAsync`, a port that had shipped with its refusal texts and
>   its own tests and had never been asked anything by a live path — the same "written but never
>   triggered" shape as the sweep that motivated this document.
> - **Unanswered legs now split two ways.** A leg whose own wall ran out settles `CapExceeded(Wall)`; one
>   that failed inside its budget still settles `Crashed`. Both remain unscored. The plan asked only for
>   the first; the second is the fairness half, and `A_leg_that_failed_INSIDE_its_wall_budget_is_still_a_crash`
>   exists to stop the cap from swallowing real faults.
> - **Item 2, LiveTrace.** Eviction happens in `CaptureAsync` rather than through the `Close(tuple)` the
>   plan named, because capture IS the handover and a method the caller must remember is a method the
>   caller will forget — this repository has a document's worth of evidence for that. `Close` ships too,
>   for the abandon path. The failure text now names both ways a recorder can be missing ("never opened,
>   or already captured"), since eviction created the second one.
> - **Item 2, GitCheckoutProvider.** Reference-counted gates (`Rent`/`Return`) rather than an evicting
>   cache: the share is taken before the wait, so a gate cannot be disposed while anybody is queued on
>   it. `Gates` is public so the leak is assertable rather than reviewable.
> - **Item 3** landed as two `COUNT` round trips instead of one grouped query. A single
>   `COUNT(*) FILTER` over a correlated collection is at the provider's mercy, and a summary that
>   silently falls back to client evaluation is the defect being removed.
> - **Item 4** grew one guard the plan did not ask for, found by the RED experiment itself: the chunk
>   size is operator input, and `new List<T>(chunkSize)` with a huge value fails in the first line of the
>   method whose whole promise is a bounded read. Capacity is now capped independently of the chunk.
> - **Item 6** names this repository's retention owner as **the host, at startup** — the first of the two
>   options the shared rule allows — 14 days, `Serilog:RetentionDays`, zero meaning "an operator job owns
>   it". A folder whose name does not parse as a day is never deleted.
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

### 1. A dead model endpoint burns the wall clock for every remaining leg — HIGH · **LANDED 2026-08-16, tail included**

`src/Bench.Infrastructure/Models/OpenAiCompatibleRuntime.cs:150-153` defaults the per-leg wall budget
to **10 minutes** when the caller passes no budgets, and `bench run` passes none —
`hosts/Cli/RunCommand.cs:135` builds its plan through `LegPlan.Reading(...)`, whose budget list is
literally `[]` (`src/Bench.Application/LegRunner.cs:19-20`).
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

**And that tail stops being a tail the moment a leg loops.** The agentic lane now being designed
(`todo/PLAN_tool_benchmark.md`) runs a leg as many turns rather than one completion, and the wall
budget above is per COMPLETION: the default it falls back to is applied inside
`OpenAiCompatibleRuntime`, once per call. Under the 25-turn cap the measured doctrine arms used, one
leg against a hanging endpoint is 25 × 10 minutes = **4 h 10 m**, and the breaker — which counts
consecutive failures, correctly, and fires at 20 by default — needs twenty of those before it stops
anything. That is **~3.5 days** of wall clock to learn what the first hang already said, on a campaign
whose whole premise is running unattended for weeks.

So, stated here rather than in the loop's own plan, because this is the item that owns it:

- A looping lane requires a **per-LEG wall budget, enforced across the whole loop** — one deadline for
  the leg, checked between turns and passed down as the remaining time for each call. A per-turn wall
  is not a budget; it is a budget multiplied by a number nobody bounded.
- The leg that exhausts it settles as `CapExceeded(BudgetKind.Wall, …)` — a recorded outcome the
  campaign continues past, never a `Crashed` and never a silent truncation.
- **The loop must not land before this is closed.** The wall budget is a `LegPlan.Budgets` entry and a
  check between turns; retrofitting it after the first long agentic campaign means discovering it from
  a three-day gap in a log.

**Shipped, later the same day.** `src/Bench.Domain/Runs/LegDeadline.cs` is the leg-wide deadline: created
once in `LegRunner.AskAsync`, `ForCall(now)` hands each call the REMAINDER rather than the ceiling, and
`Exhausted(now)` is the between-turns check the loop will read. `bench run` asks for the ceiling
(`--leg-wall-seconds`, default 600) and `BudgetConfirmation.ConfirmAsync` puts it to the runtime before a
single cell is created — a refusal ends the preparation, naming the knob that does not exist. A leg whose
wall ran out settles `CapExceeded(Wall, …)` and stores no result; one that failed inside its budget still
settles `Crashed`. The 25-turn arithmetic above is asserted in `LegDeadlineTests` against a loop that does
not exist yet, which is the only way it could have been asserted before the loop is written.

### 2. Two dictionaries that only ever grow — MEDIUM · **LANDED 2026-08-16**

- `src/Bench.Infrastructure/Trace/LiveTrace.cs:23,29` — `_byLeg.GetOrAdd(...)` per leg, and no
  `TryRemove` exists in the repository. Each `LegRecorder` holds a `List<ToolCall>` plus captured
  prompt and response text.
- `src/Bench.Infrastructure/Git/GitCheckoutProvider.cs:35,50` — one `SemaphoreSlim` per distinct
  repository key, never removed, never disposed.

Both are **inert right now**: `LegRunner` takes no `IRunTrace`, and `bench run` does not call
`ICheckoutProvider.EnsureAsync` (`RunCommand.cs:127` warns the target was not checked out). They stop
being inert the moment the long-running worker of `PLAN_variant_matrix.md` wires them in — which is
the deployment shape this whole audit is about.

**Fix:** give the trace a `Close(tuple)` that evicts after capture, and the checkout provider a
bounded or evicting lock map. Do it **before** the worker lands, not after.

**Shipped.** `LiveTrace.CaptureAsync` now `TryRemove`s: capture IS the handover, and a `Close` the caller
has to remember is a `Close` the caller will forget. `Close(tuple)` ships beside it for the abandon path,
and `Recording` exposes the count so the leak is assertable — `A_captured_leg_is_evicted_from_the_trace_and_does_not_accumulate`
measured 50 before the change and 0 after. Eviction gave "no leg was recorded" a second meaning, so the
failure text now names both ("never opened, or its trace was already captured"). `GitCheckoutProvider`
gained reference-counted gates: the share is taken BEFORE the wait, so nothing can dispose a semaphore
another caller is queued on, and `Gates` reads 0 between calls — including after a checkout that failed,
which is the release path a happy-path-only fix would have missed.

**Named on both sides.** `PLAN_variant_matrix.md` §3.6 and its build-order step 9 now carry the
reciprocal gate: `BenchRunWorker` may not land until this item has. A boundary named from one side is
not a boundary (`.claude/rules/shared/common/planning-docs.md`), and this one is the difference between
two dictionaries that are a footnote and two that leak for the life of a three-week campaign.

### 3. Two counts cost a full hydration of the run — MEDIUM · **LANDED 2026-08-16**

`hosts/Cli/RunCommand.cs:195` calls `results.ForRunAsync(...)`, which at
`src/Bench.Infrastructure/Persistence/PostgresResultStore.cs:36-45` materializes every result row with
`.Include(r => r.Metrics)` — every prompt, every answer, every metric with its `MetadataJson`
deserialized — so that line 200 can compute `scored.Count` and `scored.Count(r => r.Passed)`.
At the "tens of thousands of cells" the schema comment itself targets
(`BenchDbContext.cs:148-151`), that is the whole run pulled into memory to print two integers.

**Fix:** a `COUNT` / `COUNT(...) FILTER (WHERE ...)` query. The repository already learned this exact
lesson once and fixed it: `PostgresTelemetryStore.TotalsAsync:69-133` replaced a client-side
percentile with `percentile_disc` in SQL, and its comment records the diagnosis. Apply it a second
time.

**Shipped** as `IResultStore.ScoreboardAsync` → `RunScoreboard(Scored, Passed)`, two `COUNT` round trips
rather than one grouped query: a `COUNT(*) FILTER` over a correlated collection depends on the provider
translating it, and a summary that silently falls back to client evaluation is the defect wearing a
different hat. The RED test captured the executed SQL and read
`SELECT r."Id", r."Answer", … m."MetadataJson" FROM results` — the whole run, for two integers. It now
asserts the query SHAPE, because timing cannot distinguish three rows from fifty thousand.

### 4. Telemetry ingest reads a whole spool file and builds one unbounded `IN (…)` — MEDIUM · **LANDED 2026-08-16**

`hosts/Cli/TelemetryCommand.cs:85-104` reads the file with `File.ReadAllTextAsync` (no size cap), and
`src/Bench.Infrastructure/Persistence/PostgresTelemetryStore.cs:28-31` then asks
`.Where(t => byFingerprint.Keys.Contains(t.Fingerprint))` for every fingerprint in that file at once.
A busy 24/7 emitter produces spool files where both halves of that sentence are a problem.

**Fix:** stream the file line by line and ingest in chunks of a fixed size, so memory and the
parameter list are both bounded by the chunk rather than by the emitter's productivity.

**Shipped** as `SpoolIngest.ReadChunksAsync(IAsyncEnumerable<string>, chunkSize, ct)` over
`File.ReadLinesAsync`, with `--chunk-size` (default 500) on `bench telemetry ingest`. Refusals keep their
FILE line numbers across chunks, so the retire-or-keep decision is still about the file as a whole; the
rename still happens only after the last chunk commits, so the read-store-rename resume guarantee is
unchanged. One guard the plan did not ask for came out of the RED experiment: the chunk size is operator
input, and `new List<T>(chunkSize)` with a huge value fails in the first line of the method whose promise
is a bounded read — the buffer's capacity is now capped independently of the chunk.

### 5. `Process.Kill` catches one exception type — LOW · **LANDED 2026-08-16**

`src/Bench.Infrastructure/Process/ProcessRunner.cs:105-116` guards the tree-kill with
`catch (InvalidOperationException)` only; a `Win32Exception` (access denied, or the process exited
between the check and the call) escapes. This launcher is otherwise the family's reference
implementation and is being copied into `dew_flow_rag_qln` in the same audit's other task — so the
gap would be copied with it.

**Fix:** catch `Win32Exception` beside it. One line, and it stops a good pattern from propagating a
sharp edge.

**Shipped** as a named rule rather than a longer catch list: `ProcessRunner.IsAlreadyGone(Exception)`,
used by a `when` filter. The two exceptions are not obviously the same fact — which is why only one of
them was caught — so the rule states it once, in a place a test can read. Everything else still
propagates: a guard that swallowed every exception would turn a real fault in a best-effort path into
silence.

### 6. `logs/` has no retention owner — LOW, and now a rule · **LANDED 2026-08-16**

`BenchLogging.RunFilePath` writes `logs/{yyyy-MM-dd}/{app}-{HH-mm-ss}-{pid}.log`, one file per run,
and nothing ever deletes a day folder. `bench telemetry prune` exists for spools; its reasoning was
never extended to the host's own logs. The shared rule now requires every repository to NAME its
retention owner — see `.claude/rules/shared/common/logging-serilog.md` § Retention.

**Fix:** the default option in the rule — prune day folders older than 14 days at host startup,
best-effort, logged at Information — or a named operator job recorded in the README. Pick one; the
rule forbids leaving it unnamed.

**Shipped** as the first option: `BenchLogging.PruneLogFolders` runs inside `CreateLogger`, so every host
that logs also retires, and the window is `Serilog:RetentionDays` (default 14, zero meaning "an operator
job owns it" for a deployment that chooses the other option). Two properties are asserted rather than
assumed: a folder whose name does not parse as `yyyy-MM-dd` is never deleted — this method removes
directory TREES — and a folder another host holds open is skipped, not fatal, because retention is not
worth failing a startup over.

### 7. The sweep decided on elapsed time alone — HIGH · **LANDED 2026-08-16**

Found the day after the sweep was wired in, by running the full suite in **Debug**: the previous change was
verified in Release only, where the cross-test interference below did not surface.

`PostgresRunStore.SweepAsync` selected purely on `ClaimedAt <= cutoff`. While the method was dead code that
was harmless. From the moment it ran at every `bench run` startup and gained a `bench sweep` verb, it was a
live defect: this architecture explicitly invites several workers ("a second process running the same
command is a second worker"), so worker B starting a campaign would requeue a cell worker A was
legitimately still measuring — two workers on one leg, and A's `SettleAsync` then refused for an owner
mismatch it did nothing to cause. The 30-minute window against the 10-minute leg wall is a **margin**, not
a guarantee, and this system is about to run unattended for weeks.

The owner was also unusable for the check even in principle: `hosts/Cli/RunCommand.cs` recorded
`cli-{ProcessId}` — a pid with no machine — so a sweep on host B would have tested that pid against **its
own** process table and reached a confident wrong answer.

Two defects, because the tests said `Requeued.Should().Be(1)`: the sweep has no run filter (it is a
database-wide repair, deliberately), so in the shared Postgres that count included the cells
`SweepRecoveryTests` had stranded, and `A_cell_stranded_by_a_dead_host_is_handed_back` failed with
`Expected report.Requeued to be 1, but found 2` in the full suite while passing alone. That is the
shared-store rule in `.claude/rules/shared/common/testing.md` exactly: a count of what a run created is
history, not the guarantee.

**Shipped:** `WorkerIdentity` (label + host + pid) in `src/Bench.Domain/Runs/WorkerIdentity.cs`, carried
through `IRunStore`, `LegRunner` and the CLI; `cells.OwnerHost`/`OwnerPid` via migration
`20260816104702_CellOwnerIdentity`; `SweepAsync` keeps the staleness predicate in SQL and decides ownership
in memory over that small candidate set (`WorkerLiveness.ProcessIsAlive`). Rules mirrored from
`dew_flow_rag_qln · src/Rag.Infrastructure/Indexing/IndexPassStore.cs:191`: an unrecorded owner is an
orphan by definition, **another machine's row is left alone**, a live pid here is not an orphan.
**Deviation:** an owner this machine refuses to answer about (`Win32Exception`) counts as ALIVE rather than
gone — being refused an answer is not being told the process ended, and a stale row costs one retry where a
requeued live one costs a duplicated measurement. Every sweep assertion in `PostgresRunStoreTests` now
states the guarantee about THIS run through `ProgressAsync(run.Id)`; the abandon-before-requeue ordering
and the `SweepReport` shape are unchanged.

## Build order — executed in this order, 2026-08-16

1. ~~**(1) circuit breaker**~~ — landed with the CRITICAL fixes.
1a. ~~**(7) ownership-checked sweep**~~ — landed the day after the sweep went live; it had to
   precede any second worker, which is the deployment shape items 2 and 4 assume.
1b. ~~**(1's wall-budget tail)**~~ — landed. It **unblocks the agentic loop** of
   [../todo/PLAN_tool_benchmark.md](../todo/PLAN_tool_benchmark.md): that plan's loop may now be written,
   provided it turns inside `LegRunner.AskAsync` and reads `LegDeadline`, rather than giving each turn a
   budget of its own.
2. ~~**(5) `Win32Exception`**~~ — landed before the launcher is copied into `dew_flow_rag_qln`.
3. ~~**(3) summary counts**~~ and ~~**(4) chunked ingest**~~ — landed.
4. ~~**(2) bounded dictionaries**~~ — landed, which **clears one of the three gates** on
   [../todo/PLAN_variant_matrix.md](../todo/PLAN_variant_matrix.md) step 9 (`BenchRunWorker`). The other
   two remain: that plan's own step 6a (the accelerator lease) and
   `dew_flow_rag_qln · research/PLAN_reliability_tail.md` item 6.
5. ~~**(6) log retention**~~ — landed.

## Test plan

Per `.claude/rules/shared/common/testing.md`, every item starts with a RED test named for the
guarantee, observed failing for the real symptom:

| item | test name |
|---|---|
| ~~1~~ | shipped as `A_systemically_broken_environment_ends_the_campaign_instead_of_grinding_through_every_cell` (`tests/Bench.Tests/Application/LegDrainTests.cs`) |
| ~~1~~ | shipped as `A_leg_that_merely_scored_badly_never_trips_the_breaker` |
| ~~7~~ | shipped as `A_cell_whose_owner_is_still_running_is_not_handed_back` and `A_cell_claimed_on_another_machine_is_left_for_that_machine_to_sweep` (`tests/Bench.Tests/Infrastructure/PostgresRunStoreTests.cs`), with the rule itself in `WorkerIdentityTests` |
| ~~1 (tail)~~ | shipped as `A_looping_leg_stops_at_its_wall_budget_rather_than_at_the_budget_times_its_turns` (`Runs/LegDeadlineTests.cs`), plus `A_leg_stopped_by_its_own_wall_budget_is_a_cap_rather_than_a_crash` and its fairness twin `A_leg_that_failed_INSIDE_its_wall_budget_is_still_a_crash` (`Infrastructure/LegRunnerTests.cs`) — the first of those was the RED one: *expected CapExceeded, found Crashed* |
| ~~2~~ | shipped as `A_captured_leg_is_evicted_from_the_trace_and_does_not_accumulate` (RED: *expected 0, found 50*) and `A_finished_checkout_leaves_no_lock_behind` (RED: *expected 0, found 1*, including after a FAILED checkout) |
| ~~3~~ | shipped as `The_run_summary_does_not_hydrate_the_run_to_count_it` — asserted against the captured SQL rather than by timing, as the plan required. RED showed `SELECT r."Id", r."Answer", … m."MetadataJson"` |
| ~~4~~ | shipped as `A_spool_larger_than_one_chunk_is_ingested_in_bounded_batches` (RED: batches `{25}` where `{10, 10, 5}` was required) and `A_spool_smaller_than_a_chunk_is_still_one_round_trip` — chunking must not turn the ordinary case into a call per line |
| ~~5~~ | shipped as `A_process_that_exits_between_the_check_and_the_kill_is_not_an_error` (RED: the `Win32Exception` case *expected True, found False*) and `A_kill_that_failed_for_any_other_reason_still_propagates` |
| ~~6~~ | shipped as `A_day_folder_older_than_the_window_is_pruned_at_startup` (RED with the retention default at zero: *expected {"2026-07-01"}, found empty*), `A_folder_whose_name_is_not_a_day_is_never_deleted`, `A_retention_window_of_zero_keeps_everything_for_the_operator_job_that_owns_it` |

Run the test project's executable — never `dotnet test`; the platform has no VSTest host.

**Every item above was watched failing before it was fixed.** Where the fix landed first — items 2, 4, 5
and 6 — the change was reverted with the editor, the test observed going red for the real symptom, and the
fix restored; the messages are quoted in the table so the observation survives the session that made it.
Final state: **449 tests, 444 passing, 5 skipped** (the live-QLN suite), **0 failing**, Release, artefact
timestamps checked against the build.

## Definition of Done

- [x] Every item above is either implemented, or explicitly declined here with the reason recorded.
      All seven are implemented; nothing was declined.
- [x] Each implemented item has a RED-then-GREEN test, and the summary quotes both observations —
      the red messages are in the test-plan table.
- [x] The circuit breaker's thresholds and the retention window are configuration, not constants:
      `--max-consecutive-failures`, `--leg-wall-seconds`, `--chunk-size`, `Serilog:RetentionDays`.
- [x] A leg carries a wall budget enforced across its WHOLE loop, not per turn — `LegDeadline`, created
      once per leg, handing each call the remainder.
- [x] `logs/` retention has a named owner, per the shared logging rule: **the host, at startup**, 14 days.
- [x] The whole suite passes against a freshly built binary — timestamps checked, per
      `.claude/rules/shared/common/development-workflow.md` § Verify the ARTEFACT.
- [x] When this plan finished it was promoted to `research/` with its deviations recorded, and
      `todo/README.md` updated in the same task.
