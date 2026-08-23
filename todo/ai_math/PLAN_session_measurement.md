# PLAN — session measurement: deterministic tool-call traces from every runtime, hooks before proxies

> Status: **steps 1–4 and 7 implemented 2026-08-23; the Claude Code path RUNS end to end against a
> real session. Steps 5–6 (the OpenAI-compatible proxy, the Gemini OTLP mapping, Codex) open.**
> Method agreed with the operator 2026-08-23: hooks-first for Claude Code, nothing we build stands
> in a credential path, `git status --porcelain` as the mutation ground truth, persistence straight
> to Postgres, the UI is a **VS Code** extension.
>
> What shipped, and where it deviates from this plan, is recorded in §10. What the step-0 spikes
> actually measured is §0 — written before the code that depended on them, and twice contradicting
> what this plan assumed.
>
> Scope as built: `Bench.Domain/Sessions` (the pure classifier and detectors), `Bench.Application/Sessions`
> (ports, codec, ingest), `Bench.Infrastructure/Persistence` (two tables, one migration),
> `Bench.Api/SessionApi.cs`, `Bench.Ui` (the Math tab), `hosts/Collector`, `hosts/Hook`,
> `hosts/Cli/Sessions*.cs`, `tools/vscode-bench-sessions`.

## 0. What the step-0 spikes measured

Written here because a spike without a recorded finding did not happen. Two of the five contradicted
what this plan assumed, and both changed the design.

| Spike | Finding |
|---|---|
| (a) Does `PostToolUse` carry the tool RESPONSE? | **Yes.** The payload carries `tool_response`, shape varying by tool (object for most, string for some). It is read verbatim as raw JSON rather than deserialized, because its shape is undocumented and a strict reader would refuse the whole payload the first time an agent upgrade added a field. `ResponseChars` is therefore real, and it is the input to the window-waste question |
| (b) Cost of the porcelain digest | **~130 ms** on this repository. Affordable per shell call, NOT affordable per read — which is what the taxonomy gate is for |
| (c) Hook process cost | **~98 ms** floor (.NET start + parse + spool write), **~175 ms** with the POST. ReadyToRun publish moved it to ~166 ms — a 6 % gain, not worth requiring a publish step. So a tool call costs **~350 ms** of instrument, and that is the honest price of this method |
| (d) `SessionStart` / `SessionEnd` | Fire, and are worth their cost for one reason each: the first creates the session row before the first tool call, so a dashboard shows a session the moment it opens; the second stamps a last-seen time that is the agent's own rather than a timeout's |
| (e) The workspace-trust gate | Bit again, exactly as `PLAN_question_authoring` records: a headless run in an untrusted workspace prints *"Ignoring N permissions.allow entries"* and falls back to default permissions. It did not stop the recording — every tool call was still traced — but a run that needs specific tools must pre-trust the checkout |

### 0.1 Two findings that changed the design

**The duration column was measuring us.** First real session: a `Read` of one file recorded **~220 ms**,
of which the file read is a handful. Both timestamps sat on the far side of two process launches and a git
call, so the column was almost entirely instrument. Fixed by a rule rather than a subtraction: **each hook
stamps the instant closest to the tool boundary it observes** — a pre-event as late as it can, a post-event
the instant it was entered. The gap between those two is the tool.

**The ground truth was being skipped exactly where it was needed.** The first gate ran the porcelain check
only for calls the allowlist did NOT call read-only — which quietly removed the only case the check exists
for. An allowlist entry that is *wrong* can only be caught by checking the calls it claims are safe.
Corrected: a **shell** call is always checked, whatever the allowlist says; `Read`/`Grep`/`Glob` stay
unchecked because they cannot write by construction rather than by a table's opinion.
>
> Sibling: [PLAN_math_over_ai.md](PLAN_math_over_ai.md) — the WHY, the detectors, and the
> replacement map this capture feeds.

## 1. The goal, before any solution

Capture, per session and per tool call: which tool was called, with which arguments, against which
target, what came back, when, and in which phase — deterministically, for every runtime we measure:

1. **Claude Code CLI** — the reference adapter.
2. **The rest** — Codex CLI, Gemini CLI, Kimi (K2/K3), and — mandatorily — local models
   (Qwen, DeepSeek, Gemma, …) behind Ollama/vLLM-class servers.

Without modifying any CLI, without touching any credential, and without an LLM anywhere in the
pipeline. Today the harness's entire visibility into a CLI subject is the final JSON envelope
(`src/Bench.Infrastructure/Models/CliSubjectRuntime.cs`, `ClaudeEnvelope.cs`): tokens, cost, and the
answer — nothing about the loop that produced them. This plan is the third vantage point
[../PLAN_tool_benchmark.md](../PLAN_tool_benchmark.md) §3.5 says does not exist.

## 2. Decided constraints (operator, 2026-08-23)

| Question | Decision |
|---|---|
| Primary capture for Claude Code | **Hooks**, not a Messages-API proxy |
| Credentials | Nothing we build ever sees or forwards a token — the deciding argument for hooks |
| Bash ambiguity | Hard ground truth: a `git status --porcelain` digest before/after the call |
| Persistence | **Straight to Postgres** (the bench database), not JSONL-as-the-store |
| UI | **VS Code extension** (not Visual Studio), thin, phase 2 |
| Runtime order | Claude Code first; then the universal proxy lane; Gemini via its native OTEL |

## 3. Common architecture

### 3.1 The collector

One .NET 10 host (working name `hosts/Collector`; final name at build time) registered in the
existing AppHost beside `bench-api`. It exposes, on localhost only:

- `POST /ingest` — the single door every adapter posts events through.
- `GET /sessions`, `GET /sessions/{id}` — the read surface the CLI verbs and the VS Code extension
  poll. Read-only, like `bench-api`.
- an OTLP receiver (step 6) for the Gemini adapter.

Ingestion never blocks a caller: a bounded channel between the endpoint and the Postgres writer, and
when the database is unreachable a JSONL fallback spool with idempotent re-ingest — the discipline
already shipped in [../../research/PLAN_tool_telemetry_v0.md](../../research/PLAN_tool_telemetry_v0.md)
and `dew_flow_mcp · src/Mcp.Telemetry/SpoolUsageSink.cs` (never block a tool call, byte-budget
payloads at emit, one file per run, UTC day folders). The host logs per the family logging rule
(Serilog, coloured console + `logs/{yyyy-MM-dd}/{app}-{HH-mm-ss}-{pid}.log`, UTC).

### 3.2 The schema — `session_*` tables in the bench Postgres

- `session_runs` — id, operator task id + human name, runtime, model (as reported, never as
  requested), target repo + branch + cwd, started/ended (UTC), **`source`**:
  `hook | proxy | otel | inprocess`.
- `session_tool_calls` — session id, ordinal, tool name (verbatim, runtime's own casing), args
  digest + capped raw JSON, normalized target (file path or query), started + duration, outcome,
  phase label, mutation evidence (§3.3).
- `session_phase_spans` — derived spans (phase, start, end, tokens if the source carries them).

These are **not** the existing `tool_calls` rows (benchmark legs, observed in-process): different
provenance, and the tool-benchmark rule that observed and reconstructed calls are never averaged
together applies — `source` is a first-class column, and a session that *is* a bench leg joins via
the same correlation idea telemetry v0 uses, it does not merge.

"Not captured" is a state, never a zero — the `Captured` rule from the founding plan applies to
every count here.

### 3.3 The classifier — pure, shared, tested

One pure function set over normalized events, shared by every adapter; no adapter classifies.

**Tool taxonomy.** Claude Code's real tool names (verified against the tools reference,
code.claude.com/docs/en/tools-reference.md, 2026-08-23): `Read`, `Grep`, `Glob`, `WebFetch`,
`WebSearch` → read-class; `Edit`, `Write`, `NotebookEdit` → write-class; `Bash`, `PowerShell` →
ambiguous (below); `Agent`/`Task`, `Skill`, `TodoWrite`, `AskUserQuestion` → neutral. MCP tools
arrive namespaced `mcp__<server>__<tool>`; our own server's catalog maps `rag_*`/`graf_*`/`rt_*` →
read-class and `rt_apply_edit_plan` → write-class. Per-runtime tables are data, not code — adding a
runtime must not touch the classifier.

**Phases — three, not two.** `Research` (read-class), `Execution` (write-class), and
`Verification` (build/test-shaped commands: `dotnet build`, `dotnet test`, test-runner executables,
`cargo build/test`, `npm test`, …). Verification is first-class because (a) compile failures are
counted there, and (b) a read after an edit is healthy verification — collapsing it into Research
is what made the draft's "3+ reads after execution = loop" rule wrong.

**Bash/PowerShell — allowlist plus ground truth.** An allowlist classifies the command text
(read-only prefixes: `git status/log/diff/show`, `ls`, `cat`, …). On top of it, the hard check the
operator chose: the hook client computes a `git status --porcelain` digest **before and after**
each Bash/PowerShell call; a changed digest is mutation, whatever the command text looked like.
Disagreement between the allowlist and the digest is stored, not resolved silently — a command the
allowlist called read-only that dirtied the tree is itself a finding. Known limits, recorded per
call: writes outside the repository and writes matching `.gitignore` are invisible to porcelain
(the allowlist still stands), and the digest costs a git scan — step 0 measures that cost on an
aspnetcore-sized tree, and if it is too slow the mitigation ladder is `core.fsmonitor`, then
untracked-cache, then sampling only ambiguous commands.

**Re-research loop.** Repeated read-class calls against the **same normalized target** with no
intervening write-class call (≥3) — never "3+ reads of anything". Detector details live in the
sibling plan §4.

**Compile failures.** Verification-class calls that failed: exit code when the source carries it,
plus output shapes (`error CS\d`, `error\[E`, `FAILED`, `Build FAILED`). A per-session counter the
dashboard shows next to the phase.

## 4. Runtime adapters

### 4.1 Claude Code — hooks (the reference adapter)

`PreToolUse`/`PostToolUse` hooks in the *target* repo's `.claude/settings.json` (or injected via
`--settings`), each invoking a tiny single-file client (`bench-hook`) that forwards the hook's stdin
JSON plus its environment tags to `POST /ingest` and exits. Hooks are synchronous — the client's
budget is fire-and-forget with a ~200 ms cap, and when the collector is down it appends to a local
spool and still exits 0: **instrumentation must never block or fail the agent.**

Session identity: the hook payload's own `session_id` + `cwd`, plus `BENCH_TASK_ID`/`BENCH_TASK_NAME`
environment variables injected by whatever opened the terminal (the VS Code extension in phase 2, a
script until then). Hooks inherit the session's environment, so attribution needs no ports and no
headers — and nothing here ever sees an API key or OAuth token, which is what decided the method.

Verified against the docs (2026-08-23, code.claude.com/docs/en/hooks.md, hooks-guide.md,
env-vars.md): the event set includes `PreToolUse`, `PostToolUse`, `SessionStart`, `SessionEnd`,
`Stop`, `SubagentStart/Stop`; `PreToolUse` receives `session_id`, `cwd`, `tool_name`, `tool_input`
on stdin; hooks fire for MCP tools under `mcp__<server>__<tool>`; per-project settings are
supported.

Step-0 experiments (each ≤ half a day, findings recorded in this file):

- (a) whether `PostToolUse` carries the tool **response** (the docs show `tool_name`/`tool_input`
  and do not commit on `tool_response`) — decides whether outcomes come from hooks or stay
  *not captured* at first;
- (b) porcelain digest latency on an aspnetcore-sized tree (§3.3);
- (c) the token/cost side-channel: `CLAUDE_CODE_ENABLE_TELEMETRY=1` + `OTEL_LOG_TOOL_DETAILS` into
  the collector's OTLP receiver — what its events actually carry;
- (d) `SessionStart`/`SessionEnd` payloads for run boundaries.

**Rejected as the primary source: the `ANTHROPIC_BASE_URL` proxy.** Five reasons, each verified:
tool inputs arrive as SSE `input_json_delta` fragments needing stateful reassembly; a tool's
*result* is only visible in the **next** request's user message, so the last call's outcome can be
lost; OAuth sessions require forwarding the `anthropic-beta` capability; the fast-mode check and
WebFetch preflight bypass the base URL entirely; and it stands directly in the credential path,
which the operator ruled out. It may return later as an optional token/cost layer — if (c) does not
already cover that more cheaply. Prior art if it does return: the tool_use→tool_result pairing
state machine already written in `DewFlow · src/v2/v2.Shared/Benchmark/ClaudeStreamParser.cs`
(ported as mechanism, per the scoremeter rule — never as a submodule of a frozen repo).

### 4.2 The universal OpenAI-compatible proxy — one build, many runtimes

Every runtime that speaks `/v1/chat/completions` goes through **one** intercepting reverse proxy:
Codex CLI (provider base-url override), Kimi K2/K3 endpoints, and local servers (Ollama's `/v1`,
vLLM, LM Studio) when a third-party CLI drives them. The proxy records: model as sent, sampling as
sent, tokens from `usage`, `tool_calls` from responses (SSE `delta.tool_calls` reassembled), tool
results from the *next* request's messages — the same next-request caveat as 4.1, recorded as such.
Credentials pass through untouched (for local servers there are none). Session attribution: a
tagging header where the CLI allows custom headers, else one port per session — the port is an
implementation detail, never the identity (§5).

Recording sampling-as-sent is load-bearing for local models: Ollama's OpenAI route substitutes its
own sampling defaults over the Modelfile's, so a measurement that does not log the request's
`temperature`/`seed` is unreproducible — the proxy turns that trap into a stored fact.

### 4.3 Gemini CLI — native OpenTelemetry

Gemini CLI ships built-in OTEL export (settings-driven, OTLP). First choice: point it at the
collector's OTLP receiver and map its events into `session_*`. Step-0 spike (e): what its tool
events actually carry (names? arguments? results?) — the adapter's completeness is decided by that
finding, and anything missing is *not captured*, never zero.

### 4.4 Local models driven by this harness — already fully observed

When the bench itself drives a local model, `src/Bench.Application/ToolLoopRunner.cs` already sees
every turn, every requested tool call, every result — in-process, complete **by construction**. The
adapter is a ledger writer into `session_*` with `source = inprocess`, no interception at all. This
lane doubles as the calibration source: the same task traced in-process and through a hook/proxy
vantage point measures exactly what fraction each external vantage point misses.

### 4.5 Codex CLI — step-0 spike, then choose

Candidates, in preference order: `codex exec --json` event stream; the notify hook; the provider
base-url override into 4.2; session rollout files on disk. A 30-minute spike per candidate decides;
findings land in this file before any code.

## 5. Parallel sessions

Up to ~5 concurrent sessions is the working load. Identity is `(source, sessionId)` — the runtime's
own session id where one exists (Claude hooks), the injected `BENCH_TASK_ID` everywhere, a header
tag or per-port lane for proxied runtimes. The collector keys everything by that pair; ports never
appear in the schema.

## 6. The VS Code extension — thin, phase 2

- Starts a task terminal with `window.createTerminal({ name, cwd, env })` — the env carries
  `BENCH_TASK_ID`/`BENCH_TASK_NAME` (and, for proxied runtimes, the base-url variables). No PTY
  tricks; this is the API's happy path.
- A panel polling `GET /sessions`: task name, runtime, model, current phase, counters (research /
  edit / verification calls, re-research warnings, compile failures).
- **Thin-client rule:** every rendered number comes from the collector; the extension computes
  nothing and holds no state worth losing.
- Explicitly phase 2: the first twenty measured sessions need only a terminal, the collector and
  `bench sessions` verbs.

## 7. Build order

0. The step-0 spikes: 4.1 (a)–(d), 4.3 (e), 4.5, and the custom-header format for 4.2 — findings
   recorded in this file; a spike without a written finding did not happen.
1. Schema + collector + the Claude Code hook client, end-to-end on a real session against a real
   repo. This is the walking skeleton and the reference adapter.
2. The classifier with the porcelain ground truth, as pure code with fixtures.
3. `bench sessions` CLI verbs (`list · show · export`) — the read surface.
4. The in-process adapter (ToolLoopRunner ledger → `session_*`), and the first calibration
   comparison.
5. The universal OpenAI-compatible proxy — Ollama-driven local models and Kimi first.
6. The Gemini OTLP mapping; Codex per its spike's finding.
7. The VS Code extension.

## 8. Test plan

- **Classifier**: per-runtime taxonomy fixtures; the healthy-verify fixture (edit → read → build)
  must NOT flag; an allowlist-vs-porcelain disagreement is stored, not swallowed; build-failure
  shapes match across `dotnet`/`cargo`/`npm` fixture outputs.
- **Collector**: ingest idempotency (the same event twice is one row); DB-down → spool → re-ingest
  preserves order and loses nothing; the bounded channel never blocks the endpoint (property test).
- **Proxy**: SSE reassembly against recorded fixtures, including `delta.tool_calls` fragments split
  across chunk boundaries.
- **Hook client**: an end-to-end smoke with a real `claude -p` session against a scratch repo; the
  ~200 ms budget asserted; collector-down still exits 0.

## 10. What shipped, and where it differs from this plan

Built 2026-08-23. The Claude Code path runs end to end: hooks → collector → Postgres → CLI and console.

| Piece | Where |
|---|---|
| The pure classifier and detectors | `src/Bench.Domain/Sessions/` — `ToolTaxonomy`, `CommandClassifier`, `ToolTarget`, `PhaseClassifier`, `SessionAnalysis` |
| Ports, wire codec, ingest | `src/Bench.Application/Sessions/` |
| Two tables, one migration | `SessionEntities.cs`, `PostgresSessionStore.cs`, `20260823103215_SessionTraces` |
| Routes | `src/Bench.Api/SessionApi.cs` — `MapSessionIngest` (collector only) and `MapSessionReads` (both hosts) |
| The console | `Bench.Ui` — the **Math** tab and one session's own page |
| The collector | `hosts/Collector` — `bench-collector`, loopback-only on 5177, an AppHost resource |
| The hook client | `hosts/Hook` — `bench-hook` |
| The terminal verbs | `bench sessions install \| list \| show \| ingest` |
| The editor | `tools/vscode-bench-sessions` |

### Deviations worth recording

1. **No server-side spool.** The plan gave the collector a bounded channel and a fallback file. One spool
   on the EMITTER's side covers strictly more: a hook that could not post has the event in hand and a disk
   under it, while a collector that could not store one has to have received it first. One mechanism, one
   drain (`bench sessions ingest`).
2. **The collector refuses to start against a schema that is behind** rather than merely not migrating.
   `bench-api` may serve an unmigrated database and answer "no runs"; a WRITE surface that accepts events
   and drops them loses data that cannot be re-read.
3. **Phases and findings are derived on the way OUT, not stored.** The cheap per-session counters are SQL;
   the sequence detectors run when a session's page is opened. A list endpoint that ran them on every poll
   would load every call of every session to draw a summary.
4. **`SessionSummaryDto` carries `Disagreements`, not a findings count.** A findings column on the list
   would have read `0` for every row — a claim the list never computed. The one detector that IS SQL —
   a call classified harmless that moved the tree — earns the place instead.
5. **The plan's `src/Bench.Sessions` leaf module was not created.** The work divides cleanly along the
   layering that already exists, and a module cutting across it would have had to reference three layers
   to say anything.

### The instrument found a defect in itself, on its first real run

The first session ever traced was pointed at this repository and asked to find a bug. It found one **in
this system's own store**: a close event with no matching open was adopted as a finished call, and the
adoption seeded its before-digest from the CLOSE event — so the comparison ran a reading against itself and
answered `Unchanged` every time. One reading taken, and a confident claim that nothing changed. Its visible
consequence was a false `AllowlistCandidate` announcing that a command "changed nothing".

Fixed with the red test watched failing on exactly that symptom (`PostgresSessionStoreTests`). Recorded here
because it is the plan's own thesis arriving early: the defect was a *fabricated measurement*, the kind a
formula catches and a reading of the code did not.

## 9. Definition of Done

- [ ] A real Claude Code session produces a complete `session_*` trace with phases labeled — and
      nothing in any credential path was touched to get it.
- [ ] The porcelain ground truth runs inside its measured budget; disagreements with the allowlist
      are stored as rows.
- [ ] In-process ToolLoopRunner sessions land in the same tables with `source = inprocess`, and the
      first hook-vs-inprocess calibration comparison is recorded.
- [ ] At least one non-Claude runtime is traced end-to-end.
- [ ] Every step-0 spike has its finding written into this file.
- [ ] Build 0-warnings, tests green, the `todo/README.md` table row current.
