# PLAN — E2 stories: read-back and the audits of the policy A/B pilot

> Status: **plan only, nothing implemented yet, 2026-10-14.** Epic E2 of [PLAN_subagent_policy_ab.md](PLAN_subagent_policy_ab.md).
>
> Scope: `src/Bench.Domain/Probes` (the stream reader, widened additively), `src/Bench.Domain/Scouts/PathAudit.cs` (widened),
> a new `src/Bench.Domain/Policy/` (pure deciders only), `tests/Bench.Tests/Policy/`, one new fixture folder
> `tests/Bench.Tests/Fixtures/policy-ab/`. Nothing here launches claude, codex or a build; every story is offline and pure.
> Cornerstone area of this epic (the plan's `## Epics`): **the leak boundary** — marked on E2.S4; the parent's ledger reads
> `Cornerstones: 2 of 3 — E2.S4, E4.S3`. Aligned 2026-10-14 with the parent's decisions D9 (rule not loaded = stop), D10
> (unobserved counted), D11 (cost check after filtering never-ran launches), D12 (tampering = stop), D15 (effort-pin denials
> counted), D20 (docs per story).

## 1. Goal, before any solution

A policy cell leaves three records: the `stream-json` stream, the audit hook's log (`.git/policy-audit.ndjson`, written by E1.S5's
hook script) and the candidate diff. The pilot is an INSTRUMENT check (plan §4, D5), so every decision taken over those records —
*did the reference leak*, *did a pin move*, *did the arm's rule load*, *was every model at effort `high`*, *what did the policy
actually do* — must be a pure function, table-tested before anything runs live. Today the reader cannot even see what the MAIN
session did (a main call is `(Id, Name)` only), the path audit reads children and four read-only tools only, and nothing reads
shell command text at all. A coding cell executes shell and writes scripts; a hole in this epic returns the reference silently and
nothing downstream can tell (plan §3.5, §3.6).

## 2. What exists — verified at `6cd59ad`, 2026-10-14

| Fact | Where |
|---|---|
| The reader: `ClaudeSubagents.Read(stdout)` groups the stream by `parent_tool_use_id`; main calls built at `:50` as `new MainToolCall(id, name)` | `src/Bench.Domain/Probes/ClaudeSubagents.cs:29`, `:43-64`, `:50` |
| A child call already carries input, fate and result text (`ChildCall`), and the tool-result text helper exists | `src/Bench.Domain/Probes/ClaudeSubagents.cs:110-123`, `ContentText` `:214-224`; `ChildToolCall` `src/Bench.Domain/Probes/SubagentTranscript.cs:38`; `ChildToolFate` `:28-34` |
| **Gap:** `MainToolCall(string Id, string Name)` — no input, fate or result. Constructed positionally in two tests | `src/Bench.Domain/Probes/SubagentTranscript.cs:77`; `tests/Bench.Tests/Scouts/ScoutCliCostBasisTests.cs:80`, `tests/Bench.Tests/Scouts/ScoutCostTests.cs:104` |
| The envelope reader reads `duration_ms`, `total_cost_usd`, `usage`, `modelUsage`, `subagent_stats` — **not** `duration_api_ms`/`num_turns` (both present in the committed fixtures' envelopes); `ResultEnvelope` is constructed positionally in two tests | `ClaudeSubagents.cs:152-163`; `SubagentTranscript.cs:92-104`; `ScoutCliCostBasisTests.cs:85`, `ScoutCostTests.cs:110` |
| The `system/init` event is read for `model`, `claude_code_version`, `cwd` only; it also carries `tools`, `agents`, `skills`, `plugins` (objects `{name, path, source}`, built-ins `path: "builtin"`), `per_turn_effort_active` | `ClaudeSubagents.cs:45`, `:54-56`; `tests/Bench.Tests/Fixtures/scouts/claude-2.1.289-confine-sources-none.ndjson` line 1 |
| The additive-property precedent: `Fallbacks { get; init; } = []` on the positional record | `SubagentTranscript.cs:128` |
| **Gap:** the path audit reads children only and only `Read`/`Glob`/`Grep`/`LS`; an unknown tool falls into the Grep branch (`_ => GrepTarget`) | `src/Bench.Domain/Scouts/PathAudit.cs:49`, `:54-61`, `:112-125` (default arm `:123`) |
| `AuditedTools` is read by two scout callers that must not change | `src/Bench.Domain/Scouts/ScoutReadBack.cs:37`, `:41`; `src/Bench.Domain/Scouts/ScoutAccessCheck.cs:145` |
| Path arithmetic that works the same on Windows and Linux CI: `IsAbsolute`, `Resolve` (folds `..`, empty above the root), `IsInside` (drive paths ignore case); the link port | `src/Bench.Domain/Scouts/TreePaths.cs:12`, `:17`, `:39`; `LinkTargets` `PathAudit.cs:32-36` |
| The statement leak check and its whole-word matcher | `LeakCheck.Find` `src/Bench.Domain/Scouts/LeakCheck.cs:30`; `ContainsWord` `:50`; `MinimumToken = 3` `:28` |
| The scouts' stop words and families (range-based), the cell decider's precedence, the backgrounded and fallback rules, the cost check | `src/Bench.Domain/Scouts/ScoutReason.cs:7`, `IsStop` `:86`; `ScoutCellReading` `src/Bench.Domain/Scouts/ScoutCellReading.cs:30`, `Decide` `:50-58`, `Backgrounded` `:73`, `CostOf` `:78-90`; `ScoutPinDrift` `:227-249` |
| **Trap for the cost check:** `ScoutTokens.Attribute` refuses a stream with ANY Agent call that answered with no model ("cannot be priced"), and `CostOf` turns that refusal into `Disagrees` — a guard-denied launch (arm B) has no child and no resolved model | `src/Bench.Domain/Scouts/ScoutCost.cs:83-98`, `ModelOf` `:150-151`; `ScoutCellReading.cs:84-89` |
| Pure deciders are pinned to `Bench.Domain` by an architecture test listing them | `tests/Bench.Tests/ArchitectureTests.cs:109` (`deciders` array from `:111`, assertion `:198`) |
| Synthetic stream builder (main `mainTools` carry empty input and no result) and the fixture loader | `tests/Bench.Tests/Scouts/ScoutStreams.cs:34`, `Cell` `:53-78`, `ToolSpec` `:10` |
| Fixture hygiene scan over committed folders | `tests/Bench.Tests/Scouts/ScoutScrubberTests.cs:18-22` (`CommittedFolders`), `:75-86`; scrub recipe `tests/Bench.Tests/Fixtures/scouts/README.md` |
| Fixtures are copied to the test output by one glob | `tests/Bench.Tests/Bench.Tests.csproj:20` |

## 3. Conventions every story follows

- **Pure in `Bench.Domain`.** New deciders live in `src/Bench.Domain/Policy/` (namespace `Bench.Domain.Policy`) and are added to the
  `deciders` array of `ArchitectureTests.cs:111` in the story that creates them. No IO, no clock, no process.
- **Additive widenings.** Every widened scout type keeps its existing constructors and behaviour; new data arrives as `init`
  properties with a not-captured default (the `Fallbacks` precedent). The scouts' suite is the proof and is not edited, except where a
  story says so explicitly (the hygiene folder list, the stream builder's new optional parameter).
- **Missing is a value, never 0 and never an exception** (`CapturedCount.Unavailable(reason)`, `NotCaptured` statics).
- **Reasons are allow-listed words** (`PolicyReason`, created in E2.S2) — no free sentence is stored; machine paths stay in memory.
- **Tests**: `tests/Bench.Tests/Policy/<Unit>Tests.cs`, xUnit v3 + FluentAssertions, named after the guarantee. Build and run only as
  `CLAUDE.md` says — `node .claude/rules/shared/tools/low-priority.mjs dotnet build dew_flow_benchmark.slnx -c Release -m:4`, then
  `node .claude/rules/shared/tools/low-priority.mjs ./tests/Bench.Tests/bin/Release/net10.0/Bench.Tests.exe --filter-class "*<Unit>Tests"`,
  then the whole suite. **Never `dotnet test`.** RED first: tests are written against a stub that returns the empty/not-captured
  answer and fail on the ASSERTION, not on a compile error.
- **Break-its** (where a story lists one): make the named change in production code, run the named test, see it red with the real
  symptom, restore, see it green; record both runs in the story's summary. A break-it never disables a tripwire, never edits a test.
- Fixtures are scrubbed before commit per `tests/Bench.Tests/Fixtures/scouts/README.md` and held to the hygiene scan; files are LF.
- **Docs per story** (parent D20): each story adds its paragraph to `research/module_policy_ab.md` (created by the first code story
  of any epic to land) and its rows to `research/module_tests.md`, in the same task.

## 4. Shared contracts (fixed here so stories can run in parallel)

**C1 — owned tool calls in stream order** (created by E2.S1, consumed by S3, S4, S6). `StreamPosition(int Event, int Block)`
(comparable: event index among the stream's JSON events, then the block's index inside its message);
`OwnedToolCall(StreamPosition At, string Owner, ChildToolCall Call)` where `Owner` is `""` for the main session and the Agent call's
`tool_use_id` for a child; `SubagentTranscript.ToolCalls` = every main and child tool call, ordered by `At`.

**C2 — the audit-log line** (the reader is E2.S2; the writer is E1.S5's `prompts/policy-ab/policy-audit.mjs`). One JSON object per line:

```json
{"event":"PreToolUse","agent_id":"<absent for the main session>","agent_type":"<absent for the main session>","tool_name":"Bash","effort":{"level":"high"}}
{"event":"InstructionsLoaded","file_path":"<as the payload gave it>","memory_type":"Project","load_reason":"session_start","sha256":"<64 lowercase hex of the file's bytes>"}
```

`effort` is the hook payload's `effort` object copied verbatim (measured shape `{"level":"high"}`, plan §2). Unknown keys are
ignored. **If E1.S5's script has landed when S2 starts, its key names win** and S2 conforms; if not, E1 conforms to C2 — the main session
reconciles either way; never two schemas.

## 5. Stories

### E2.S1 — The reader sees the main session: inputs, fates, results, the envelope's turn counts, the init lists

- **Goal.** `ClaudeSubagents.Read` returns, for every MAIN-session tool call, what it was asked (input), how it ended (fate) and what
  came back (result text); the envelope's `duration_api_ms` and `num_turns` as captured-or-not; the `system/init` skills, agents and
  plugins lists; and C1's ordered list of every tool call. The scouts read exactly what they read today.
- **Prerequisite (operator, attended, once, < $1) — the RED fixture.** After E1.S5's audit-hook script exists: a throwaway one-commit
  tree (`git init`, one small `a.js`), `.claude/settings.json` registering E1's `policy-audit.mjs` for `PreToolUse` (matcher `*`) and
  `InstructionsLoaded`; launched with plan §3.3's argv (`--max-budget-usd 1`, the effort-`high` pinned settings if E1 has produced
  them, `CLAUDE_CODE_DISABLE_BACKGROUND_TASKS=1`) and a prompt that makes the main session, in order: run `git status` with Bash;
  run `Get-Location` with PowerShell; Read `a.js`; Edit one line of `a.js`; Write a new `b.js`; run `node -e "process.exit(3)"` with
  Bash (a failing command); launch ONE `general-purpose` Agent with `model: "sonnet"` told to run `git status` with Bash and Read
  `a.js`, then reply OK; reply DONE. Keep stdout and `.git/policy-audit.ndjson`; scrub both (tree → `C:\work\tree`, every other
  machine path → the README's placeholders); commit as
  `tests/Bench.Tests/Fixtures/policy-ab/claude-<cli version>-coding-run.ndjson` and `…-coding-run.audit.ndjson` with a
  `README.md` in the scouts' fixture-README shape (argv, prompt, what each file shows, the scrub table).
- **Scope.**
  - `src/Bench.Domain/Probes/SubagentTranscript.cs:77` — `MainToolCall` gains `Input` (raw JSON, `""` default), `Fate`
    (`ChildToolFate`, default `NotCaptured`), `ResultText` (`""`), `At` (`StreamPosition`, default `(-1, -1)`) as `init` properties.
    `ChildToolCall` (`:38`) gains `At` the same way. Add `StreamPosition`, `OwnedToolCall` (C1) here.
  - `ResultEnvelope` (`:92`) gains `DurationApiMs` and `NumTurns` (`CapturedCount`, default `Unavailable("not read")`; the
    `NotCaptured` static says `"no result envelope"`).
  - New `InitLists(bool Captured, IReadOnlyList<string> Skills, IReadOnlyList<string> Agents, IReadOnlyList<InitPlugin> Plugins)` with
    `InitPlugin(string Name, string Source)` (the `path` is NOT kept — machine-specific) and a pure
    `IReadOnlyList<string> DifferencesFrom(InitLists other)` naming each list that differs (`"skills"`, `"agents"`, `"plugins"`) —
    compared as ordinal-sorted multisets; two not-captured lists differ (`"not captured"`), never "equal".
    `SubagentTranscript` gains `Init { get; init; } = InitLists.NotCaptured` and `ToolCalls { get; init; } = []`.
  - `SubagentCall` gains a computed `Ran` = the child left at least one message, or the result carries a `resolvedModel` (a
    guard-denied or "type not found" launch did not run).
  - `src/Bench.Domain/Probes/ClaudeSubagents.cs:43-64` — enumerate events WITH their index; build main calls exactly as `ChildCall`
    (`:110-123`) builds child calls (same fate precedence: denied → not captured → errored → returned; same `ContentText`); set `At`
    on both; read `duration_api_ms`/`num_turns` in `Envelope` (`:152-163`) like `Duration` (`:165`); read the init lists from `init`.
  - `tests/Bench.Tests/Scouts/ScoutStreams.cs:53` — `Cell` gains an optional `IReadOnlyList<ToolSpec>? mainCalls` that emits main
    `tool_use` events WITH input and, per `ToolSpec.Fate`, the result / `permission_denied` exactly as `Child` (`:89-124`) does but
    with no `parent_tool_use_id`. The existing `mainTools` parameter is untouched.
  - `tests/Bench.Tests/Scouts/ScoutScrubberTests.cs:18` — append `tests/Bench.Tests/Fixtures/policy-ab` to `CommittedFolders`.
- **Edge cases.** A main call with no result → `NotCaptured`, `ResultText` empty. A main call that is an `Agent` call is in
  `MainCalls` too (its `ResultText` is the tool_result's text — E2.S6 reads the guard's refusal there). Non-JSON lines are skipped
  and do NOT consume an event index. `num_turns` absent → `Unavailable`, never 0. An init with no `plugins` key → an empty list but
  `Captured = true`; no init event → `NotCaptured`.
- **Test scenarios** (`tests/Bench.Tests/Policy/PolicyReaderTests.cs`; RED: every assertion fails against the unwidened reader):
  - `A_main_bash_call_is_read_with_its_command_its_fate_and_its_result` — the coding fixture: the `git status` Bash call's input holds
    `"command":"git status"`, fate `Returned`, result text non-empty; the failing `node -e` call reads `Errored`.
  - `Main_edit_and_write_calls_carry_their_file_path_and_content` — Edit's `old_string`/`new_string`, Write's `content` in `Input`.
  - `A_powershell_call_is_read_like_a_bash_call`.
  - `Every_tool_call_of_main_and_child_comes_back_once_in_stream_order` — `ToolCalls` count = main + child calls; positions strictly
    increasing; the child's calls carry the Agent call's id as `Owner`.
  - `Duration_api_ms_and_num_turns_are_read_and_their_absence_reads_not_captured_never_zero` (fixture, then an edited envelope).
  - `The_init_lists_are_read_and_a_plugin_is_compared_by_name_and_source_not_by_path` — two inits differing only in a plugin `path`
    → no difference; an extra agent → `"agents"`.
  - `A_launch_that_never_ran_reads_Ran_false` — the scouts' `confine-restricted` fixture (string result, no child).
  - `The_scouts_reader_tests_are_unchanged_and_green` — run `--filter-class "*ClaudeSubagentsTests"` and `*PathAuditTests`, untouched.
- **Done when.** The scenarios above green; the whole suite green with no scout test edited (only the two scope-listed test-helper
  lines); the fixture and its README committed and clean under the hygiene scan. **Open by design:** the guard-denial shape of an
  arm-B Agent call is asserted in E2.S6 against E3.S9's rules-probe fixture.
- **Depends on.** E1.S5 (the audit-hook script, for the fixture's audit log); nothing else.
- **Size.** ≈ 280 changed lines (≈ 110 code, ≈ 170 tests/helper), plus the fixture files.
- **Coder: sonnet** · scenarios and brief: this document (Opus) · review: Opus.

### E2.S2 — The audit log is read, the policy's words exist, and "the arm's rule reached the session" is decided

- **Goal.** One pure reader turns `.git/policy-audit.ndjson` into typed records (C2), and `PolicyRuleReached` decides from those
  records alone whether the session loaded THIS arm's `subagent-models.md` at session start, byte for byte (plan §3.4 *Rule reached*).
  The closed vocabulary of every policy decision (`PolicyReason`) is created here, complete, so later stories never race to add words.
- **Scope.**
  - `src/Bench.Domain/Policy/PolicyAuditLog.cs` — `PolicyAuditLog.Read(string text)` →
    `PolicyAuditLog(IReadOnlyList<ToolRecord> Tools, IReadOnlyList<InstructionRecord> Instructions, int Unreadable)`;
    `ToolRecord(int Line, string AgentId, string AgentType, string ToolName, string EffortLevel)` (`EffortLevel` `""` when the line
    carries no `effort.level`); `InstructionRecord(int Line, string FilePath, string MemoryType, string LoadReason, string Sha256)`.
    Lines go through `ProbeJson` (`src/Bench.Domain/Probes/ProbeJson.cs:15`, `:38`); a non-JSON line or an unknown `event` adds to
    `Unreadable` and is never thrown on. `IsMain` = `AgentId.Length == 0`.
  - `src/Bench.Domain/Policy/PolicyReason.cs` — the enum and its families, as explicit sets (not ranges):
    **stops** `RuleNotLoaded`, `RuleHashMismatch`, `EffortNotHigh`, `MainEffortMissing`, `PinMoved`, `InitListsDiffer`,
    `ModelRefusalFallback`, `AgentBackgrounded`, `CostMismatch`, `OutsideReadReturnedContent`, `OutsideGlobListedPath`,
    `OutsideResultNotCaptured`, `CommandNetwork`, `CommandOutsidePath`, `CommandUnauditable`, `AuditLogTouched`, `OverlayTouched`,
    `FlaggedFileRun`; **flag** `ContentFlagged`; **void** `StrongReferenceHit`; plus `None`. `PolicyReasons.IsStop/IsFlag/IsVoid`.
    `RuleNotLoaded` is a STOP and a no-go (parent D9); `OverlayTouched` is the tampering stop of parent D12.
  - `src/Bench.Domain/Policy/PolicyRuleReached.cs` — `PolicyRuleReached.Check(PolicyAuditLog log, string treeRoot, string mount,
    string bytePin, LinkTargets links)` → `RuleReading(PolicyReason Reason, IReadOnlyList<InstructionRecord> Matching)`:
    expected path = `TreePaths.Resolve(treeRoot, $"{mount}/common/subagent-models.md")`; a record matches when its `FilePath`, resolved
    against `treeRoot` and through `links`, is that path (drive paths ignore case, as `TreePaths.IsInside` does) AND
    `LoadReason == "session_start"`. Reached ⇔ some matching record's `Sha256` equals `bytePin` (lowercase hex, ordinal).
    No matching record → `RuleNotLoaded`; matching records but none with the pin → `RuleHashMismatch`. `MemoryType` is reported, not
    required. Also `PolicyRuleReached.Loaded(log)` → the full ordered list of loaded files (plan §3.4: the session's starting context,
    reported per cell).
- **Edge cases.** A record whose path is the OTHER overlay file (`planning-docs.md`) never satisfies the check. A matching record with
  `load_reason` other than `session_start` (a nested-path load later) does not count. Hashes compare ordinally, NOT ignoring
  case — the hook writes lowercase (C2), so an uppercase pin or record is a contract break and reads `RuleHashMismatch`, which a test
  pins. Paths with backslashes and forward slashes both resolve.
- **Test scenarios** (`PolicyAuditLogTests.cs`, `PolicyRuleReachedTests.cs`; RED against stubs returning empty / `None`):
  - `Every_line_of_the_coding_runs_audit_log_is_read_main_and_child_apart` — E2.S1's fixture: main records have no agent id, the
    Sonnet child's records carry one and `agent_type`; every `PreToolUse` carries `high`.
  - `A_broken_line_or_an_unknown_event_is_counted_unreadable_and_never_thrown_on`.
  - `A_tool_line_without_an_effort_reads_an_empty_level_not_high`.
  - `The_arms_own_rule_loaded_at_session_start_with_its_byte_pin_is_reached`.
  - `The_other_arms_hash_on_the_right_path_reads_hash_mismatch` · `No_record_for_the_rule_reads_not_loaded` ·
    `The_rule_loaded_later_than_session_start_does_not_count` · `Planning_docs_with_the_right_hash_does_not_stand_in_for_the_rule` ·
    `A_windows_backslash_path_and_a_forward_slash_path_name_the_same_file`.
  - `Every_policy_reason_but_None_belongs_to_exactly_one_family`.
  - **Break-it**: `PolicyRuleReached` comparing the path only (hash ignored) → `The_other_arms_hash_on_the_right_path_reads_hash_mismatch`
    goes red naming `None`; restore.
- **Done when.** Scenarios and the break-it as above; §9 *Rule reached* of the plan green; `PolicyAuditLog`, `PolicyRuleReached`,
  `PolicyReasons` in the `deciders` list.
- **Depends on.** E2.S1's fixture (one test; the rest uses literal lines); C2 agreed with E1.S5's hook script. The pin VALUES come from
  E1.S6's `arms.json` at run time (E3.S4) — tests use literal hashes.
- **Size.** ≈ 300 changed lines (≈ 130 code, ≈ 170 tests).
- **Coder: sonnet** · scenarios and brief: this document (Opus) · review: Opus.

### E2.S3 — The path audit covers the main session and the write tools

- **Goal.** The fail-stop path audit (`PathAudit`) runs over main-session calls and over `Edit`/`Write`, as parameters, with the scouts'
  call unchanged: an outside read or write that came back is a stop wherever it was made (plan §3.5 row 2, §3.11).
- **Scope.**
  - `src/Bench.Domain/Scouts/PathAudit.cs` — new `PathAuditScope(IReadOnlySet<string> Tools, bool IncludeMainSession)` with
    `Scouts` = (`AuditedTools`, false) and `Policy` = ({Read, Glob, Grep, LS, Edit, Write}, true); overload
    `Audit(SubagentTranscript, string treeRoot, LinkTargets links, PathAuditScope scope)` reading `transcript.ToolCalls` (C1) filtered
    by owner and tool; the existing three-argument `Audit` (`:54`) delegates with `PathAuditScope.Scouts` and returns the identical
    list. `AuditedTools` (`:49`) keeps its name and its four members (two callers, §2).
  - `Target` (`:112-125`): add `"Edit"` and `"Write"` → `Resolved(cwd, file_path)`; make `"Grep"` an explicit case; the default arm
    becomes `(string.Empty, cwd)` — an unknown tool in scope is UNRESOLVABLE, hence outside (fail-closed), never silently a Grep of
    the working directory. Scouts are unaffected: their four tools all have explicit cases.
  - `AuditedCall` (`:28`) gains `Owner` (`init`, `""` default) so a finding says main or which child.
- **Edge cases.** An outside `Write` that returned "File created successfully …" → `OutsideContent` (stop); denied → `OutsideDenied`
  (recorded). An `Edit` with a relative `file_path` resolves against `transcript.WorkingDirectory`. A main-session `Glob` listing an
  outside path → `ListedOutside` exactly as for a child.
- **Test scenarios** (`tests/Bench.Tests/Policy/PolicyPathAuditTests.cs`, synthetic streams via `ScoutStreams.Cell(…, mainCalls:)`):
  - `A_main_session_read_outside_the_tree_that_returned_content_stops` · `A_main_session_read_outside_that_was_denied_is_recorded_not_stopped`
  - `An_outside_write_that_succeeded_stops_wherever_it_was_made` (main and child rows).
  - `An_edit_inside_the_tree_is_fine` · `An_edit_through_a_junction_that_leaves_the_tree_stops` (a fake `LinkTargets`).
  - `A_tool_the_scope_names_but_the_audit_cannot_resolve_is_outside` (scope with a made-up tool name).
  - `The_scout_scope_audits_children_and_the_four_read_tools_only` — a stream with an outside main Read and an outside child Edit
    gives an EMPTY scout audit and two stops under `Policy`.
  - **Break-it**: the policy overload ignoring `IncludeMainSession` → `A_main_session_read_outside_the_tree_that_returned_content_stops`
    red; restore. **Break-it**: restore the old `_ => GrepTarget` default → `A_tool_the_scope_names_…` red; restore.
- **Done when.** Scenarios and both break-its; `PathAuditTests`, `ScoutReadBackTests`, `ScoutAccessCheckTests` untouched and green.
- **Depends on.** E2.S1 (C1, `MainToolCall` input/fate/result).
- **Size.** ≈ 180 changed lines (≈ 50 code, ≈ 130 tests).
- **Coder: sonnet** · scenarios and brief: this document (Opus) · review: Opus.

### E2.S4 — `CommandAudit`: what counts as a leak in shell text and in written files, and when it stops (CORNERSTONE)

**Cornerstone — spec by Fable.** Area: the leak boundary (plan `## Epics`, E2). Detailed spec to be written by Fable; this
document fixes only the goal, the acceptance criteria and the break-its. The coder starts only after Fable's spec is committed into
this section (and the ledger line of the parent plan names this story).

- **Goal.** Every route to the reference a coding cell can open through shell text or through a file it writes is decided by one
  pure function over the cell's tool calls, fail-closed: a network attempt, a read or a working directory outside the cell tree, a touch
  of the audit log, or the run of a flagged script STOPS the campaign; a written file that could open the network or read outside the
  tree FLAGS the cell. Shell reads are expected NOT to be denied on native Windows (plan §3.5), so this detection is the boundary for
  the pilot — a miss here returns the reference silently and nothing downstream can tell.
- **Acceptance criteria.**
  1. Pure (`Bench.Domain.Policy`, in the `deciders` list): input = C1's ordered tool calls (main AND every child), the cell tree root,
     an explicit toolchain allow-list, `LinkTargets`; output = findings that each name the call id, the owner (main / Agent call id),
     the rule that fired and its kind (stop or flag) — machine paths in memory only.
  2. **Network → stop**, on Bash AND PowerShell command text, fate-agnostic (a denied attempt is still the finding): at least every
     case of plan §9 *CommandAudit* — `gh pr view`, `git clone https://…`, `curl`, `iwr`, `Invoke-RestMethod`, `npm view` — plus
     `Invoke-WebRequest`, `wget`, git verbs that reach a remote, a dependency restore mid-run (`npm ci`/`npm install`; plan §3.5 last
     row), and any URL host in command text. The spec states the complete verb and host list and why each other common verb
     (`npx`, `pip`, `ssh`, `scp`, `Start-BitsTransfer`, .NET web classes) is in or out.
  3. **Outside path → stop**: an absolute path outside the tree (`D:\rsd\…`), `~` and `~/.claude/projects/…`, a `..` chain that climbs
     out (`..\..\`), `%USERPROFILE%`/`%VAR%`, `$env:HOME`/`$env:USERPROFILE`/`${env:…}`, `$HOME`, a working-directory change or a
     `git -C`/`--prefix`-style option pointing outside, and an inside path that resolves outside through a junction. Paths inside the
     tree and under an allow-listed toolchain directory are fine. The spec decides how a `cd` inside one command changes resolution.
  4. **The canary routes E3 exercises are each detected** from their command text: `cat`/`type`/`Get-Content`/`gc` of a file under
     `~/.claude/projects/D--rsd-dew-flow-creds-for-devs/`, its `memory/` and `~/.claude/file-history/` (every spelling: `~`,
     `%USERPROFILE%`, `$env:USERPROFILE`, absolute), a sibling cell tree, the mirror root, a clone stand-in, `git -C <outside> log`,
     a shell fetch of `https://github.com`.
  5. **Compound commands**: one bad half stops the whole call — `;`, `&&`, `||`, `|`, newlines, `$( … )`, backticks, PowerShell script
     blocks.
  6. **Un-auditable is never fine**: an input that is not JSON, a command the audit cannot read (PowerShell `-EncodedCommand`/`-enc`,
     `Invoke-Expression`/`iex` of a non-literal, `node -e`/`python -c` whose argument is assembled at run time) — the spec says
     stop (`CommandUnauditable`) or why not.
  7. **Tampering → stop** (parent D12): ANY tool input of ANY tool (Read, Grep, Glob, Edit, Write, Bash, PowerShell; main or child)
     naming the audit log (`policy-audit.ndjson`, `.git/policy-audit`) → `AuditLogTouched`; an `Edit`/`Write` of an OVERLAY path
     (`.claude/settings.json`, `.claude/hooks/**`, `<mount>/common/subagent-models.md`, `<mount>/common/planning-docs.md` —
     E1.S4's `PolicyOverlay.Targets`), or a shell command that writes, moves, deletes or redirects into one → `OverlayTouched`. A
     shell command whose effect on such a path the audit cannot tell is a stop (fail-closed); the spec states which read-only shell
     mentions it lets pass, and why. Decided because an edit that removes the audit hook would turn every later child *unobserved*
     instead of stopped (D10).
  8. **Content scan → flag** over every `Write` `content` and `Edit` `new_string` (main and children): `fetch(`, the `http`/`https`/
     `net`/`dgram` modules (`require`, `import`, `node:` prefix), `child_process` with `git clone`/`curl`/`gh`,
     `Invoke-WebRequest`/`Invoke-RestMethod`, an absolute path outside the tree, `os.homedir()`/`~`/`.claude`, a `package.json`
     `scripts` entry calling a network verb. A test file importing `child_process` to run a local `node` inside the tree is NOT flagged.
  9. **A flagged file that is later run → stop**: by stream order (C1), a command that executes the flagged file (`node <file>`,
     `npx`/`tsx <file>`, `pwsh`/`bash <file>`, `npm run <flagged script>`, `npm test` when the flagged `package.json` script is the test
     script). A file written by SHELL redirection is covered by the command rules on the writing command itself.
  10. **No false stop on the pilot's own work**: the commands the run prompt and `stories.json` prescribe — `npm run typecheck`,
     `npm run compile`, `npm test`, `node --test --test-reporter=… --test-reporter-destination=<inside the tree> "out/test/*.test.js"`,
     `git status`/`diff`/`add`, `tsc -p ./ --noEmit` — are fine, or the pilot cannot run.
  11. **The allow-list cannot open a route**: an allow-list entry equal to, containing or contained in the trees root, the mirror root,
     `~/.claude` or the original clones' root is refused when the audit is constructed.
  12. The plan's §9 *CommandAudit* and *Content scan* tables pass row by row, as table tests.
- **Break-its** (plan §9; each red with the real symptom, then restored, no tripwire disabled): `CommandAudit` without the `gh` verb
  lets the planted `gh pr view` through; the content scan without `fetch(` lets the planted `.mjs` through; **and** (this document's
  addition) the tampering rule removed lets a `Read` of `.git/policy-audit.ndjson` through — and an `Edit` of
  `.claude/hooks/policy-audit.mjs` through.
- **Depends on.** E2.S1 (C1: main inputs, stream order). Independent of S2, S3, S5, S6.
- **Size.** ≈ 400–450 changed lines, most of it table tests; Fable's spec sets the final cut.
- **Cornerstone — spec by Fable** · **Coder: sonnet** · review: Opus.

### E2.S5 — The effort audit: any level but `high` stops the campaign; unobserved children are counted

- **Goal.** A pure function over the audit log and the stream decides plan §3.6's effort stop — every logged level, main session and
  every child, must be `high`, and a main session with no level-bearing record at all is a stop — and counts children that left no
  effort record (*unobserved*), which are reported, not stopped (plan §3.6, D4).
- **Scope.** `src/Bench.Domain/Policy/PolicyEffortAudit.cs` —
  `PolicyEffortAudit.Check(PolicyAuditLog log, SubagentTranscript transcript)` →
  `EffortAudit(IReadOnlyList<EffortSighting> Levels, bool MainObserved, int Unobserved, int ExtraAgentIds, int MissingLevel,
  IReadOnlyList<PolicyReason> Stops)` with `EffortSighting(string Who /* "main" or the agent type */, string AgentId, string Level,
  int Count)`. Rules:
  - every `ToolRecord` with a non-empty `EffortLevel` other than `"high"` (ordinal) → `EffortNotHigh` (once; `Levels` names which);
  - no main `ToolRecord` with a non-empty level → `MainEffortMissing`;
  - records with an empty level → `MissingLevel` count (reported; a main session with ONLY such records is still `MainEffortMissing`);
  - `Unobserved = max(0, Agents.Count(a => a.Ran) − distinct non-empty AgentIds having ≥ 1 level-bearing record)`; more distinct
    ids than calls that ran → `ExtraAgentIds` (reported). No matching of log lines to Agent calls (plan §3.6).
- **Test scenarios** (`PolicyEffortAuditTests.cs`, a table over literal audit-log lines plus a synthetic stream; RED against a stub
  with no stops):
  - `All_high_for_the_main_session_and_every_child_passes` (also on E2.S1's fixture pair).
  - `One_child_at_xhigh_stops_and_names_the_level` — the 2026-10-14 overlay's record shape (`{"level":"xhigh"}`, a Sonnet child).
  - `The_main_session_at_xhigh_stops` · `No_main_record_stops` · `A_main_session_whose_records_carry_no_level_stops`.
  - `A_child_that_made_no_tool_call_is_unobserved_and_counted_not_stopped`.
  - `A_denied_launch_that_never_ran_is_not_counted_unobserved` (`Ran = false`).
  - **Break-it** (plan §9): the audit ignoring child records lets the `xhigh` Sonnet fixture through →
    `One_child_at_xhigh_stops_and_names_the_level` red; restore.
- **Done when.** Plan §9 *Effort audit* green row by row; the break-it red then green; `PolicyEffortAudit` in the `deciders` list.
- **Depends on.** E2.S1 (`SubagentCall.Ran`), E2.S2 (`PolicyAuditLog`, `PolicyReason`).
- **Size.** ≈ 220 changed lines (≈ 70 code, ≈ 150 tests).
- **Coder: sonnet** · scenarios and brief: this document (Opus) · review: Opus.

### E2.S6 — What the policy actually did: per-call rows, edits and delegation, compliance per arm, the contrast

- **Goal.** Plan §3.6's read-back as REPORTED facts (never voids): per Agent call and per cell, what was launched on which model, who
  wrote how much, how far each arm followed its own rule, and whether the A/B contrast is present at all. Compliance is reported, the
  hand-read of every Agent prompt stays the main session's (plan §3.6).
- **Scope.** `src/Bench.Domain/Policy/PolicyAudit.cs`:
  - `PolicyCallRow` per Agent call: `SubagentType`, `InputModel` (or `""`), `ResolvedModel`, `MessageModels`, `TotalDurationMs`,
    `Ran`, `GuardDenied`, tool counts by kind — **write** (`Edit`, `Write`, `NotebookEdit`), **read** (`Read`, `Glob`, `Grep`, `LS`),
    **shell** (`Bash`, `PowerShell`), **other** — files changed and lines changed (below), `StreamUsage`, fallbacks whose
    `ParentToolUseId` is the call's id, `PromptSha256` and the prompt (for the hand-read list; never committed by this unit).
  - **Lines from tool inputs** (labelled so): `Edit` → removed = lines of `old_string`, added = lines of `new_string` (a replaced line
    counts twice, as the rule's own bound does); `Write` → added = lines of `content`; files = distinct `file_path`. Writes made by a
    shell command are invisible here and the row says *"shell writes not counted"*.
  - `PolicyCellAudit.Of(SubagentTranscript t, PolicyArm arm)`: Agent calls by (type × resolved model); the main session's own edits
    (files, added + removed); `MainWithinBound` = ≤ 1 file and ≤ 50 lines (B's edit bound, from inputs); delegation share = child
    lines ÷ all lines (`CapturedCount`-style: not captured when nothing was written); guard denials (B); internal model lines =
    `modelUsage` entries no call or main session answered with (listed apart).
  - **Guard denial** = an Agent main call (`MainCalls` entry, E2.S1) with fate `Errored` or `Denied` whose `ResultText` contains
    `subagent-models.md:` (the guard's `DENY_MARKER`). The exact carrier is CONFIRMED against E3.L1's rules-probe arm-B stream (committed by E3.S9); until that
    fixture lands, the rule is tested on a synthetic stream and the fixture test is a `[Fact(Skip = "closes with E3.S9's rules-probe fixture")]`
    whose skip reason names the E3 story.
  - **Effort-pin denials** (parent D15) = an Agent main call with fate `Errored` or `Denied` whose `ResultText` contains
    `policy-effort-pin:` (the marker of E1.S5's `effort-pin.mjs`) — counted per cell in both arms, never a stop; such a call did not
    run. Confirmed on E3.L1's probe streams (the probe's step 3) like the guard denial.
  - **Digest lines** (plan §3.4: how many the guard injected) → `CapturedCount.Unavailable("digest carrier not yet measured")` until
    E3's fixture shows where the additional context lands; then a follow-up of ≤ 20 lines reads it.
  - **Compliance A**: ran children on the pinned Opus id → compliant; on Fable → listed for the hand check WITH their prompts (A's
    carve-out: security, cryptography, authn/authz, new architecture); any other model → deviation. **Compliance B**: launches that
    named an alias (`sonnet`/`opus`/`fable`) vs not (denied included); Sonnet children that used a write tool (coders); read-only
    children per model; Fable launches with prompts; `MainWithinBound`.
  - `PolicyContrast.Of(PolicyCellAudit a, PolicyCellAudit b)` → `(bool ALaunchedAChild, bool BRanASonnetCoder, bool Present)`;
    "a child" = a call that `Ran`. E5 calls it for (A1, B) and (A2, B).
- **Edge cases.** An Agent call with no `model` in A resolves to the session's model — it is compliant iff that is the Opus id. A
  `Write` over an existing file counts only its content as added (labelled from inputs). `replace_all` does not multiply lines. A
  child of the wrong arm-kind (e.g. a Sonnet coder in A) is a deviation, never a void.
- **Test scenarios** (`PolicyAuditTests.cs`, `PolicyContrastTests.cs`; synthetic streams; RED against stubs returning empty rows):
  - `A_sonnet_child_that_edited_a_file_is_a_coder_with_its_files_and_lines` · `A_child_that_only_read_is_counted_as_gathering`.
  - `The_main_sessions_own_edits_are_counted_and_its_one_file_fifty_line_bound_is_read` (26 replaced lines → 52 → outside the bound).
  - `Delegation_share_is_child_lines_over_all_lines_and_not_captured_when_nothing_was_written`.
  - `A_launch_the_guard_refused_is_counted_denied_and_did_not_run` (synthetic).
  - `A_launch_the_effort_pin_refused_is_counted_in_either_arm_and_did_not_run` (synthetic).
  - `In_arm_A_a_fable_child_is_listed_for_the_hand_check_and_a_sonnet_child_is_a_deviation`.
  - `In_arm_B_a_launch_without_an_alias_is_counted` · `Contrast_is_absent_when_A_launched_no_child_or_B_ran_no_sonnet_coder`.
  - `An_internal_haiku_line_of_model_usage_is_listed_apart`.
  - `The_guard_denial_reads_the_same_on_the_live_arm_B_probe_stream` — skipped until E3.S9's fixture (see above).
- **Done when.** Scenarios green (the one skip named); the types in the `deciders` list.
- **Depends on.** E2.S1; E1's `PolicyArm` (E1.S3/E1.S4 — the one `PolicyOverlay.Apply(projectSettingsJson, arm)` takes — reuse it, never a second arm
  type); E3.S9's rules-probe fixture closes the skipped test.
- **Size.** ≈ 360 changed lines (≈ 160 code, ≈ 200 tests).
- **Coder: sonnet** · scenarios and brief: this document (Opus) · review: Opus.

### E2.S7 — The cell verdict: every stop of plan §3.6 in one pure decider

- **Goal.** One function takes a finished cell's records and returns its stops (campaign stops), its flags (unscored until the
  operator clears), and the audit facts — so E3.S4's `run` and E5's pilot settle a cell by calling one thing. Outcomes (wall, cap, a
  failed build, red tests, an incomplete stream) are never stops (plan §3.6 last bullet).
- **Scope.** `src/Bench.Domain/Policy/PolicyCellVerdict.cs`:
  - `PolicyCellPins(string CliVersion, string MainModelId, IReadOnlyDictionary<string, string> ResolvedByAlias /* opus, sonnet,
    fable */, InitLists Init)` — the values frozen by E3.S6's probe (`pins.json`); `PolicyCellExpectation(PolicyArm Arm, string TreeRoot, string Mount,
    string RuleBytePin, PolicyCellPins Pins, PriceTable Prices, IReadOnlyList<string> ToolchainAllowList)`.
  - `PolicyCellVerdict.Read(SubagentTranscript t, PolicyAuditLog log, PolicyCellExpectation e, LinkTargets links)` →
    `PolicyVerdict(IReadOnlyList<PolicyReason> Stops, IReadOnlyList<PolicyReason> Flags, RuleReading Rule, EffortAudit Effort,
    IReadOnlyList<AuditedCall> Paths, <S4's findings type>, PolicyCellAudit Audit, CostCheck Cost, Outcome<CellCost> PerModel)`
    with `Outcome` = `Stopped` if any stop, else `Flagged` if any flag, else `Scored`.
  - **Stops, in this fixed order** (the first is the stored reason): leak — path audit under `PathAuditScope.Policy` (E2.S3) mapped
    as `PathAudit.StopReason` maps it, then E2.S4's stop findings (each S4 kind mapped to exactly one of `CommandNetwork`,
    `CommandOutsidePath`, `CommandUnauditable`, `AuditLogTouched`, `OverlayTouched`, `FlaggedFileRun`); `RuleNotLoaded`/`RuleHashMismatch` (E2.S2);
    `EffortNotHigh`/`MainEffortMissing` (E2.S5); `ModelRefusalFallback` (`ScoutPinDrift.FellBack`, `ScoutCellReading.cs:236`);
    `AgentBackgrounded` (`ScoutCellReading.Backgrounded`, `:73`); `PinMoved` — `init.claude_code_version` ≠ `CliVersion`,
    `MainModel` ≠ `MainModelId`, or a call that ran answered (`resolvedModel` or any `message.model`) with an id that is not its
    alias's pinned id (no alias → the main model id); `InitListsDiffer` (`InitLists.DifferencesFrom`, E2.S1); `CostMismatch`.
  - **The cost check** (parent D11) reuses `ScoutCellReading.CostOf` (`:78`) on `t with { Agents = [.. t.Agents.Where(a => a.Ran)] }`
    — a call that never ran (a guard denial, an effort-pin denial) has no model and would make `ScoutTokens.Attribute` refuse the
    whole stream (§2), turning every denial into a false `CostMismatch`. A stream with no envelope → `Unchecked`, not a stop (the cap × 1.2 charge is E3.S2's). The per-model cost lines
    (`ScoutCost.Of`) are returned for `cells.jsonl`; the price table must hold a Fable row (E1.S13's `prices.json`) — a Fable child without
    one reads as the cost check's own failure, which IS a stop (an unpriceable cell is a broken instrument).
  - **Flags**: S4's `ContentFlagged`. **Not here**: the post-hoc reference check (E2.S8 runs after scoring), the pre-launch pin re-check
    of files (settings, overlay, audit-hook and tree hashes — E3.S1).
- **Edge cases.** A truncated stream (a wall kill) with all-`high` effort and no leak → `Scored`-shaped, `Cost = Unchecked` — an
  outcome, not a stop. Several stops → all listed, distinct, in the fixed order. A Fable child in arm A is never a pin move.
- **Test scenarios** (`PolicyCellVerdictTests.cs`, synthetic streams + literal audit lines; RED against a stub that returns `Scored`):
  - one test per stop word above, each naming its reason; `Several_stops_are_all_listed_and_the_first_in_fixed_order_is_stored`.
  - `A_guard_denied_launch_does_not_fail_the_cost_check` — the trap of §2, red against a naive `CostOf(t, …)`.
  - `A_wall_killed_stream_is_an_outcome_not_a_stop` · `A_content_flag_alone_reads_flagged_not_stopped`.
  - `Every_command_audit_stop_kind_maps_to_exactly_one_policy_reason` (enumerates S4's kinds — a new kind is red until mapped).
  - `A_clean_coding_run_reads_scored` — E2.S1's fixture pair with pins taken from it.
- **Done when.** Scenarios green; `PolicyCellVerdict` in the `deciders` list; plan §9's per-cell rows reachable through this one call.
- **Depends on.** E2.S1–E2.S6 (S4's spec fixes its finding kinds); E1's `PolicyArm` (E1.S3/E1.S4).
- **Size.** ≈ 320 changed lines (≈ 120 code, ≈ 200 tests).
- **Coder: sonnet** · scenarios and brief: this document (Opus) · review: Opus.

### E2.S8 — The post-hoc reference check over every candidate

- **Goal.** After a cell, its candidate diff is checked for tokens it could only have taken from the reference (plan §3.5 *Post-hoc*):
  hits are reported for the operator's review; a strong hit voids the cell.
- **Scope.** `src/Bench.Domain/Policy/PolicyReferenceCheck.cs` — `PolicyReferenceCheck.Find(string candidateDiff,
  IReadOnlyList<string> referenceOnlyIdentifiers, IReadOnlyList<string> referenceOnlyPhrases)` →
  `ReferenceHits(IReadOnlyList<string> Identifiers, IReadOnlyList<string> Phrases, PolicyReason Verdict)`:
  - identifiers through `LeakCheck.Find(candidateDiff, [], referenceOnlyIdentifiers)` (`LeakCheck.cs:30`, whole word, ≥ 3 chars) —
    reused, never re-implemented; only `AddedIdentifier` findings apply;
  - phrases (E1.S9 keeps those of ≥ 6 words) matched after collapsing runs of whitespace and stripping comment leaders
    (`//`, `#`, `*`, `--`) on both sides, ignoring case;
  - `Verdict` = `StrongReferenceHit` when ≥ 1 phrase OR ≥ 3 DISTINCT identifiers hit (the plan's "not motivated by the frozen
    statement" is already enforced upstream: E1.S9's reference-only list excludes every token in P's tree or the statement); 1–2
    identifiers → `None` with the hits reported for review.
- **Edge cases.** Identifiers named by the frozen statement never reach this list (E1), so a candidate using them is not a hit by
  construction — pinned by a test anyway. Diff headers are part of the text (a new file named after a reference-only identifier is a
  hit). An empty diff → no hits.
- **Test scenarios** (`PolicyReferenceCheckTests.cs`, plan §9 *Post-hoc leak check*; RED against a stub with no hits):
  `A_reference_only_comment_phrase_is_a_strong_hit` (also re-wrapped across lines and under `//`) ·
  `Two_reference_only_identifiers_are_reported_not_strong` · `Three_distinct_identifiers_are_strong` ·
  `An_identifier_the_frozen_statement_names_is_no_hit` · `An_identifier_inside_a_longer_word_is_no_hit`.
  **Break-it**: the phrase match without whitespace collapsing lets the re-wrapped phrase through → red; restore.
- **Done when.** Scenarios and the break-it; `PolicyReferenceCheck` in the `deciders` list.
- **Depends on.** Nothing to build; E1.S11's `prepare` supplies the two lists at run time (E5.S6 calls this after `score`).
- **Size.** ≈ 180 changed lines (≈ 50 code, ≈ 130 tests).
- **Coder: sonnet** · scenarios and brief: this document (Opus) · review: Opus.

## 6. Build order

```
E1.S5 audit hook ──────► S1 (operator fixture, reader) ──┬─► S3 path audit ─────────┐
                                                          ├─► S4 CommandAudit (Fable spec first) ─┤
                         S2 audit log + rule reached ◄────┘ (fixture for one test)               │
                                  │                                                              ├─► S7 cell verdict
                                  └─► S5 effort audit ──────────────────────────────────────────┤
                         S1 + E1.S3 arm ────► S6 policy audit ──────────────────────────────────┘
S8 post-hoc check — independent, any time
```

S2's code (not its fixture test) can start beside S1; S3, S4 (after its Fable spec), S5 and S6 run in parallel once S1 is in; S7 is
last. E3 consumes S2 (probe rule check), S5 (probe effort check and the live `xhigh` break-it), S4 (the access-check's detection) and
S7 (`run`'s settlement); E3.S9's rules-probe arm-B stream closes S6's skipped guard-denial test; E5 calls S7 per cell and S8 per
candidate.

## 7. Test plan (maps to the parent plan's §9)

| Plan §9 block | Story | Kind |
|---|---|---|
| *Reader* (main inputs/results, `duration_api_ms` not captured ≠ 0, init lists; scouts' reader unchanged) | E2.S1 | live fixture + synthetic |
| *Reader* — the guard denial classified | E2.S6 (skipped until E3.S9's fixture) | live fixture |
| *Rule reached* | E2.S2 | table |
| *Effort audit* | E2.S5 | table |
| *CommandAudit*, *Content scan* | E2.S4 | table (Fable's spec) |
| *Post-hoc leak check* | E2.S8 | table |
| Path audit widened (plan §3.5 row 2) | E2.S3 | synthetic |
| Every §3.6 stop through one call | E2.S7 | synthetic + live fixture |
| Break-its: `gh` verb, `fetch(`, child records ignored (plan §9) | S4, S4, S5 | red → restore |
| Break-its added here: rule hash ignored, main session ignored, Grep default, tampering rule, phrase whitespace | S2, S3, S3, S4, S8 | red → restore |

All offline; no live claude or codex in the suite. The whole suite runs after every story, from the executable.

## 8. Growth budget

| Surface | Projected size | Retired by |
|---|---|---|
| `tests/Bench.Tests/Fixtures/policy-ab/` — one coding-run stream + its audit log + README | < 300 KB | kept with the tests; replaced only by a re-recording |
| New source and tests | ≈ 2 200 changed lines over eight stories | — |

Nothing here writes at run time.

## 9. Definition of Done — epic E2

- [ ] E2.S1–E2.S8 landed, each with its scenarios green and its break-its red-then-green, recorded in the story's summary.
- [ ] The scouts' suite unchanged and green (only the two test-helper lines S1 names were touched).
- [ ] Every new decider in `ArchitectureTests`' `deciders` list; `PolicyReason`'s families total.
- [ ] The coding-run fixture committed, scrubbed, clean under `ScoutScrubberTests`.
- [ ] E2.S4's spec written by Fable, committed into its section before coding, and the parent plan's ledger line naming E2.S4.
- [ ] Plan §9 *Reader*, *Effort audit*, *Rule reached*, *CommandAudit* + *Content scan*, *Post-hoc leak check* green; the one
      skipped guard-denial test named and handed to E3.
- [ ] `node .claude/rules/shared/tools/plan-lifecycle.mjs` clean; this document promoted with the parent plan, not before.

## 10. Points this document raised — settled by the parent (2026-10-14)

- **Rule not loaded: stop or void?** → a STOP and a no-go (parent D9); the parent's §3.4, §3.6 and §9 now say so.
- **The post-hoc strong-hit rule is its own Sonnet story (E2.S8), not inside the cornerstone** → accepted; the parent's E2
  cornerstone paragraph now names E2.S4 alone and fixes the strong-hit rule mechanically (§3.5: one phrase, or three distinct
  identifiers).
- **Overlay-path edits mid-run** → a STOP (parent D12); E2.S4's criterion 7 states the rule, Fable's spec separates read-only shell
  mentions with its reason.
- **A tool line without an effort level** → counted (`MissingLevel`), a stop only when it leaves the main session with no level at
  all (parent D10).
- **The cost-check trap of §2** → never-ran launches filtered first (parent D11).
