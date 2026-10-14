# PLAN — Subagent-model policy A/B: does the role table deliver a story as well as "Opus by default", faster, cheaper?

> Status: **plan only, nothing implemented yet, 2026-10-14 — revised after an independent review the same day, then once more
> after the five stories documents were written (main-session decisions D9–D23).** Scope: a new **Policy** section of this
> benchmark (sibling context `Bench.*.Policy`, `bench policy` verbs) that re-runs an already-merged story from its
> pre-implementation spec under two subagent-model policies with the main session held fixed and every model at effort `high`.
> The **pilot** is built small (§8); everything a full series needs and the pilot does not is listed apart in §12 and built only
> after the owner's go. Records under `research/policy-ab/`.
>
> Related: [module_scouts.md](../research/module_scouts.md) (the pieces this composes),
> [PLAN_scout_model_calibration.md](PLAN_scout_model_calibration.md) (the scout half of the same rule),
> [PLAN_code_lane.md](PLAN_code_lane.md) (signals 3–4 and the attended-only rule), [module_gate.md](../research/module_gate.md)
> (the blinded assessor), [MEASURED_LESSONS.md](../research/MEASURED_LESSONS.md). The rule under test lives in
> `dew_flow_conventions · common/subagent-models.md`; its plan and results are `dew_flow_conventions ·
> research/PLAN_subagent_model_tiers.md` and `research/RESULTS_subagent_model_tiers.md`. Stories documents:
> [E1](PLAN_subagent_policy_ab_E1.md), [E2](PLAN_subagent_policy_ab_E2.md), [E3](PLAN_subagent_policy_ab_E3.md),
> [E4](PLAN_subagent_policy_ab_E4.md), [E5](PLAN_subagent_policy_ab_E5.md).

## 1. The question, before any solution

The operator switched the shared rule on 2026-10-13 without waiting for the scout calibration (that plan's status line). The
operator's question now: *"compare quality, speed and time of the new subagent-model policy against the old one — can we take an
already-finished story in a separate tree and re-run it under the new rules?"*

**The estimand.** For one story S, frozen at its parent commit P with its pre-implementation spec: the **quality of the delivered
change within an identical resource envelope** (same wall cap, same dollar cap), and the wall time and dollars actually spent,
**by subagent-model policy**, with the main session held fixed (Opus, the same tools, the same prompt, the same tree apart from
the policy files) and **every model — main session and every child — at effort `high`, pinned and audited per child** (D4).

**What differs between the arms is the WHOLE policy, not the model alone.** Arm A's rule (conventions `d88e293^`) is *Opus by
default, Fable for the decisions a later round cannot undo* — architecture of something new, security, cryptography,
authentication and authorisation, "a key, a token or a session" (`d88e293^ · common/subagent-models.md:8`, `:18-19`, DoD `:68`).
Arm B's rule (release `ea3252f`) assigns models by role — Sonnet scouts (⌈1.5·N⌉) and coders, Opus judges, Fable cornerstone
specs (`ea3252f · common/subagent-models.md:18-24`) — **and adds its own process steps**: per story, scenarios and a brief written
by a judge before any coder starts, Opus review, Sonnet fixes (`:39-40`, `:62-63`), and a main-session edit bound of one file and
≤ 50 lines (`:69-74`), enforced in part by its guard. A difference between the arms is therefore the effect of the policy as
deployed — model assignment AND process — and the report never attributes it to the model alone.

**Fable can appear in BOTH arms.** The pilot story (typed stored secrets) and story 3 (a consent gate) are security work, which A's
own rule sends to Fable; a Fable launch in A is compliance, not drift. Story 3 is also tagged Fable by its own plan.

**What a single-story re-run does not measure.** The rule's plan → epics split (Fable) and epic → stories split (Opus) — a re-run
starts from a story, not a plan (a stated limitation, §10); the rule's value across a whole plan; repositories outside the family;
the coai review gate, the consultants and every MCP tool (all off in both arms — §3.3); Workflow `agent()` calls (no hook sees
them); a later model behind an alias; any effort level but `high`; the run-to-run variance of either arm in the pilot (one run per
arm cannot estimate it).

### Owner decisions (2026-10-14, recorded so nobody re-asks them)

| # | Decision |
|---|---|
| D1 | **Pilot first**: 1 story × 2 arms × 1 run (+ the A/A cell of D6). The owner then decides on the full series, 3 stories × 2 arms × 3 runs. |
| D2 | Arm A's CHILD process may run with `SUBAGENT_MODEL_GUARD=off` — the fourth measuring harness with that opt-out. Never a working session, never arm B. |
| D3 | The main session chooses the stories. Chosen 2026-10-14 (§3.2): **creds #176** (pilot), **coai #751**, **creds `5158ac24`** (Fable-tagged). Story ids remain an INPUT to `prepare`; §3.2's rows are candidates until `stories.json` is committed. |
| D4 | **Effort `high` for EVERY model**, identical bytes in both arms, pinned and audited per child (owner order 2026-10-14) — an arm comparison at unequal effort measures effort, not the model (§2, §6). |
| D5 | **The pilot is built in hours, not epics** (review 2026-10-14): only what an instrument check needs (§8); the rest is series-only (§12). Pilot quality = the mechanical gates (§3.8) + a codex reading against the requirement checklist (D7). |
| D6 | **A/A cell: yes** (owner, 2026-10-14). The pilot is THREE cells — A, B and a second A on the same story — ceiling $207.60 (§3.9); the order is drawn from one committed seed (§3.3, D16). |
| D7 | **The requirement reading is done by ANOTHER VENDOR, not the operator** (owner, 2026-10-14): codex, read-only, reads each candidate and the anonymised reference against R1…Rk (§3.8). No pairwise and no planted-variant calibration in the pilot; the reference must score ≥ 90 % met, and every "not met" is spot-checked on the main session's side — by an Opus judge agent it dispatches (D19) — labelled a model checking a model. Coverage is reported as *codex-read*. |
| D8 | **Canaries in `~/.claude` approved** (owner, 2026-10-14): three small files — in `~/.claude/projects/D--rsd-dew-flow-creds-for-devs/`, its `memory/` and `~/.claude/file-history/` — removed afterwards by exact path (§3.5). |

### Main-session decisions (2026-10-14, after the stories split — about forty defects the five stories writers found, settled once)

The writers' own resolutions are accepted unless a row below overrides them; each row carries its reason.

| # | Decision | Reason |
|---|---|---|
| D9 | **The arm's rule not loaded (native layout) = STOP, a no-go** — never a void, never a covariate (§3.4, §3.6). | Native discovery is mechanical (measured, §2): a session without its rule is a broken instrument, and a plain re-run repeats it. |
| D10 | **An unobserved child is COUNTED and reported, never a stop** (§3.6). A tool line with no effort level is counted too; it stops only when it leaves the main session with no level at all. | A child that made no tool call cannot log effort; stopping on it would stop on silence, not on a wrong level. |
| D11 | **Guard-denied (never-ran) launches are filtered out BEFORE the cost check** (E2.S7). | `ScoutTokens.Attribute` refuses a stream holding an Agent call that answered with no model (`src/Bench.Domain/Scouts/ScoutCost.cs:83-98`) and `CostOf` turns that refusal into `Disagrees` (`src/Bench.Domain/Scouts/ScoutCellReading.cs:78-90`) — every arm-B guard denial would be a false `CostMismatch` stop. |
| D12 | **Editing an overlay, hook or settings path mid-run = STOP (tampering)** — an `Edit`/`Write`, or a shell command, on `.claude/settings.json`, `.claude/hooks/**`, the two overlay rule files or the audit log; decided by E2.S4's verdict (§3.5). | Removing the audit hook would turn every later child *unobserved* instead of stopped (D10); silence must never be the reward for tampering. |
| D13 | **An empty or non-compiling candidate is NEVER Success.** Success needs at least one spec-implied hold-out test that compiled AND every compiled spec-implied test green; *interface mismatch* stays its own reported outcome. E4.S3's Fable spec settles the exact rule within this principle. | Read literally, "a mismatch is never Success = false" made a candidate whose every hold-out file mismatches — P unchanged included — a Success. |
| D14 | **The frozen statement STRIKES the spec's model lines and tags** (`(Opus)` in a heading, `**Model: Opus**`), identically for both arms; the strike list is committed with the statement (E1.S9); the spec file in the tree keeps P's bytes. | They were the then-policy's role assignment, not story content; verbatim they tell B's session to put its stories on Opus, against its own rule. |
| D15 | **The Agent tool's per-launch `effort` argument is pinned by a hook in BOTH arms**: an identical `PreToolUse` `Agent\|Task` deny hook, `prompts/policy-ab/effort-pin.mjs` (hashed into the pins), denies any `effort` other than `high`; each denial is counted per cell; the child-level effort audit stays the backstop STOP. | The Agent tool takes an `effort` enum (seen 2026-10-14 in a persisted transcript's tool schema, E3 §8): a model-chosen level is not configuration and the pinned settings cannot prevent it. Conventions' guard v2 is getting the same rule, but B's overlay is from `ea3252f`, so the identical extra hook keeps the arms equal. |
| D16 | **The cell order is drawn by `SeededDraws`** (`src/Bench.Domain/SeededDraws.cs:10`, `Below` `:34`) from a seed committed BEFORE the draw; **a pilot prediction is committed before cell one**, together with the seed (E5.S3). | `new Random(seed)`'s sequence is not a documented contract (`SeededDraws.cs:5-8`); a seed committed with its order could be chosen after seeing the order; the measurement rule makes a prediction mandatory (`.claude/rules/shared/common/measurement.md:67`). |
| D17 | **A stop whose fix changes a pinned input invalidates every earlier cell**: all cells re-run only if recorded spend + their reservations stay within the $207.60 ceiling; otherwise the pilot is a no-go (§3.6, §3.9). | Cells under two pins are not one instrument; the ceiling funds one re-run generation, not three. |
| D18 | **The post-exit step** — candidate diff, the E2 verdict, the `cells.jsonl` rows — **is owned by E3.S3 and E3.S4**; E5.S2 closes as covered. | The plan named it in no unit; E3's document builds it. |
| D19 | **The main session's edit bound holds in E5**: `pilot.md`, `compliance.md` and the `handcheck.md` spot-check are written by an Opus judge agent (or a Sonnet transcriber of a judge's verdicts); the main session only verifies. Refines D7's "the main session spot-checks". | The rule in force (`subagent-models.md`: ≤ 1 file, ≤ 50 lines, bounded before starting) binds the session running the pilot too. |
| D20 | **`research/module_policy_ab.md` and the `research/module_tests.md` rows are created with the FIRST code story and updated by every story** (knowledge-base rule), never deferred to E5; `ModuleTestsCatalogTests` is widened to read the sub-verbs of `hosts/Cli/PolicyCommand.cs` (E1.S11). | `research/module_tests.md:156-158` wants a verb's row in the same task; the catalogue test reads sub-verbs from `ScoutsCommand.cs` only (`tests/Bench.Tests/Cli/ModuleTestsCatalogTests.cs:61-66`). |
| D21 | **Gate commands are recorded as executable + arguments** (`npm.cmd`, `node node_modules/typescript/bin/tsc`, `node --test …`), never shell strings. | The process runner starts without a shell (`src/Bench.Infrastructure/Process/ProcessRunner.cs:179`), so a bare `npm` is not found on Windows. |
| D22 | **Teeth = the plain revert of the candidate's non-test changes** (`todo/PLAN_code_lane.md:165`); no `type StoredSecret = string`-style mutation. | A mutation names the reference's shape and cannot be aimed at an alternative's. |
| D23 | **The access check runs each read route against EVERY target** (≈ 25 calls, inside the $5 cap); every launch — cells and probes — writes `kind`-tagged spend lines to `cells.jsonl`, and the probes count against the same ceiling. | "One probe per route" cannot decide §3.5's own rule, which needs shell reads of both the clone stand-in and `~/.claude`. |

## 2. What exists — verified 2026-10-14

| Fact | Where |
|---|---|
| History-free tree: `git archive P` from the bare mirror, submodules at their pinned sha, listed paths and the project's Claude settings removed, fresh `git init` + ONE commit (added with `-f`, so anything restored before the commit is committed), tree hash re-checked before every cell | `src/Bench.Infrastructure/Scouts/ScoutTrees.cs:35` (summary), `:48` (class), settings removal hard-coded at `:147` via `ScoutGroundTruth.ClaudeProjectSettings` (`src/Bench.Domain/Scouts/ScoutCandidates.cs:175`); grant guard `:132`, `:151`; `add -A -f` `:279` |
| The tree's spec record | `ScoutTreeSpec`, `src/Bench.Application/Scouts/ScoutPorts.cs:158` |
| P verified per merge style (squash → one parent, P = fix^1; rebase → `baseRefOid`; merge → first parent); a **local squash** — one parent, P is it, keyed by its sha — is exactly a direct commit's shape (series only, §12) | `ScoutParentCheck.Refusal`, `src/Bench.Domain/Scouts/ScoutParentCheck.cs:30`, `Linear` `:38`; `ScoutMergeStyle.LocalSquash`, `src/Bench.Domain/Scouts/ScoutCandidates.cs:21` |
| The one confined launch: `-p --model opus --output-format stream-json --verbose --tools … --strict-mcp-config --mcp-config <empty> --forward-subagent-text --max-budget-usd --no-session-persistence --setting-sources "" --settings <pinned> --plugin-dir <pinned>`; never `--restricted` | `ScoutLaunch.Options`, `src/Bench.Application/Scouts/ScoutLaunch.cs:44-56`; `--restricted` rationale `:15-17` |
| `AgentSettingSources.Only(["project"])` already exists | `src/Bench.Application/CliAgent.cs:195`; `AgentAskOptions` at `:100` |
| claude argv: `CliArgv.For` → `Claude(...)`; `--permission-mode plan` only for read-only; `--plugin-dir` last | `src/Bench.Infrastructure/Models/CliAgentRuntime.cs:68`, `:111-130` |
| Processes start without a shell — a bare `npm` is *not found* on Windows (D21) | `src/Bench.Infrastructure/Process/ProcessRunner.cs:179` (`UseShellExecute = false`) |
| Cell runner: mark launching → launch with a line sink to `stream.ndjson` → commit stderr/argv/prompt/seal → read → settle. Coupled to `ScoutRun`/`ScoutCell`/`IScoutStore`/`ScoutPrompts`; the pilot does NOT extract from it (D5) | `CliScoutRunner`, `src/Bench.Infrastructure/Scouts/CliScoutRunner.cs:40`; `Ask` `:139-153`; `MeasureAsync` `:183-200`; `CommitAsync` `:203-220` |
| Child environment: allow-listed names only (no `CLAUDECODE`, no `CLAUDE_CODE_*` session variables, no secret-named variable), plus `SUBAGENT_MODEL_GUARD=off` and `CLAUDE_CODE_DISABLE_BACKGROUND_TASKS=1` FORCED | `ProbeChildEnvironment`, `src/Bench.Domain/Probes/ProbeChildEnvironment.cs:25-30` (names), `:58-61` (`ForScouts`) |
| Stream reader grouped by `parent_tool_use_id`: per Agent call `subagent_type`/`model`/prompt, child tools with input, fate and result, `Fallbacks`, the envelope's `modelUsage` and `subagent_stats` | `ClaudeSubagents.Read`, `src/Bench.Domain/Probes/ClaudeSubagents.cs:29` |
| **Gap:** a MAIN-session tool call is `(Id, Name)` only — no input, no fate, no result | `MainToolCall`, `src/Bench.Domain/Probes/SubagentTranscript.cs:77` |
| **Gap:** the path audit reads children only, and only `Read`/`Glob`/`Grep`/`LS` | `PathAudit.AuditedTools` and `Audit`, `src/Bench.Domain/Scouts/PathAudit.cs:49`, `:54-62` |
| The statement leak check: basenames and stems of ground-truth files, identifiers the change added — whole-word, case-insensitive | `LeakCheck.Find(statement, groundTruth, addedIdentifiers)`, `src/Bench.Domain/Scouts/LeakCheck.cs:30` |
| Tokens per model (deduplicated by message id), cost, the 2 % cost check, the CLI's observed Sonnet cache-read rate | `ScoutTokens.Attribute` `src/Bench.Domain/Scouts/ScoutCost.cs:83`; `CliRate` `:26`; `PriceTable` `:30`; `ScoutCost.Check` `:208`; `research/scout-calibration/prices.json` |
| **Trap:** `ScoutTokens.Attribute` refuses a stream holding ANY Agent call that answered with no model, and `CostOf` reads that refusal as `Disagrees` — a guard-denied launch has no model (D11) | `src/Bench.Domain/Scouts/ScoutCost.cs:83-98`; `src/Bench.Domain/Scouts/ScoutCellReading.cs:78-90` |
| **Gap:** the price table has no Fable row — and Fable is legitimate in BOTH arms here (§1) | `research/scout-calibration/prices.json` (opus, sonnet, haiku only) |
| Spend ledger (reservation = cap × 1.2) and the pure recovery decider | `SpendLedger`, `src/Bench.Domain/Scouts/SpendLedger.cs:17`, `ReservationFor` `:33`; `ScoutAttemptLifecycle.Recover`, `src/Bench.Domain/Scouts/ScoutAttempt.cs:100` |
| **One explicit effort** (PR #93): `ScoutPinnedSettings.EffortLevels`, `FileNameFor(effort)` (a file per level, so the settings pin names the condition), `Explicit(effort)` → `{"effortLevel": level}` with NO `modelSettings`; `ScoutArtifacts.Effort` + `ExplicitEffort()` write that file once and refuse a file saying anything else; the pinned file may carry ONLY `effortLevel` and `modelSettings` | `src/Bench.Domain/Scouts/ScoutPinnedSettings.cs:16` (`AllowedKeys`), `:88`, `:92`, `:97`; `src/Bench.Infrastructure/Scouts/ScoutArtifacts.cs:52`, `:183`; `research/module_scouts.md:531` |
| **Effort is per the CHILD's own model, measured.** A child takes `modelSettings.<its alias>.effortLevel`, else the top-level `effortLevel` — never the parent's. The operator's settings (`effortLevel: xhigh`, only `opus` pinned `high`) ran the scouts' pilot Sonnet children at `xhigh`: 2 Sonnet scouts 92 s / $0.73 at `xhigh` against 41 s / $0.29 at `high`, 1 Opus scout $0.39 at `high`. PR #93 read it from persisted child transcripts' `effort` / `perTurnEffort`. A second route, probed 2026-10-14 in a scratch tree: a project `PreToolUse` hook logging the payload's `effort` with `agent_id` — all three models set `high` logged `{"level":"high"}` for the main Opus, a Sonnet child and a Fable child; a one-run `--settings` overlay `{"modelSettings":{"sonnet":{"effortLevel":"xhigh"}}}` made the Sonnet child log `xhigh` (teeth). The operator's user settings are now `high` for every model | `research/module_scouts.md:531`; `todo/PLAN_scout_model_calibration.md:495`; the hook probe is the main session's, scratch only (the pilot's rules-probe records it as a fixture) |
| **The Agent tool takes a per-launch `effort` argument** (an enum, `low`…`max`) — a session can set a child's level itself, past the pinned settings (D15) | seen 2026-10-14 in a persisted subagent transcript's tool-schema attachment on this machine ([E3 §8](PLAN_subagent_policy_ab_E3.md)) |
| **Rule delivery in the native layout, measured ($0.01 spike, scratch, 2026-10-14).** Under `--setting-sources project` with `--strict-mcp-config`, a project `InstructionsLoaded` hook fired with `load_reason: session_start`, `memory_type: Project` for BOTH `CLAUDE.md` and a nested `.claude/rules/shared/common/probe-rule.md`; payload keys `session_id`, `transcript_path`, `cwd`, `scratchpad_dir`, `hook_event_name`, `file_path`, `memory_type`, `load_reason`; the model quoted both files' codewords. `system/init` listed 22 bundled skills, agents `claude`, `claude-code-guide`, `Explore`, `general-purpose`, `Plan`, `statusline-setup`, and built-in plugins only. Backup if the hook ever stops firing: `--debug` output names the memory files loaded | the main session's spike; claude 2.1.289 |
| node:test's junit report, measured (node v24.18.0): a `describe` is a `<testsuite>`; a top-level test is a `<testcase>` directly under `<testsuites>`; every `<testcase>` carries `file` = the absolute path of the compiled `.js` and `<testsuite>` a `hostname` | [E4.S1](PLAN_subagent_policy_ab_E4.md) |
| Reproducible draws: SplitMix64, documented, the same on every runtime (D16) | `SeededDraws`, `src/Bench.Domain/SeededDraws.cs:10`, `Below` `:34`; rationale `:5-8` |
| Global slot rotation (series only) | `SlotRotation.Rotated`, `src/Bench.Domain/Runs/SlotRotation.cs:19` |
| Scoring at base: apply diff in a scratch worktree, build gate, hidden tests by `git checkout <fix> -- <tests>`, test gate; `Passed` includes `RightFiles` | `SolutionSignals.RunAsync`, `src/Bench.Infrastructure/Git/SolutionSignals.cs:53-90`; checkout `:117-118`; `TouchesCause` `:159`; `Passed` `:19` |
| Signals 3 (teeth) and 4 (neighbour suite) specified, not built | `todo/PLAN_code_lane.md:161-166` |
| Harvest gates: red at base with the fix's tests, green at the fix — ONE build and ONE test command per run, a whole-suite verdict (so per hold-out FILE means one run per file) | `HarvestGates.RunAsync`, `src/Bench.Infrastructure/Git/HarvestGates.cs:50`; `PhaseAsync` `:145` |
| Model-authored code is built **attended only** until the sandbox assertions pass | `todo/PLAN_code_lane.md:114-135` (§4.2) |
| Blinding and a codex read-only reader already exist: fresh ids by `BlindedId.Mint`, already used outside the gate; the codex options (`-s read-only`, the output schema enforced, the final message to a file, MCP off); the scouts' claim assessor — one batch → one codex launch with a history-free tree as its working directory, batch files in a fresh folder OUTSIDE that tree; its reply parser leaves an unnamed, doubled or illegal row unassessed rather than refusing the answer (the policy reader is stricter, §3.8); strict rubric and the hand-check record (series) | `ScoutAssessorLaunch`, `src/Bench.Application/Scouts/ScoutClaimAssessor.cs:9`; batch folder `:11`; `AskAsync` `:29-47`; `ScoutAssessment.Read`, `src/Bench.Domain/Scouts/ScoutAssessment.cs:121-123`, its schema `:70`; `BlindedId.Mint`, `src/Bench.Domain/Gate/BlindExport.cs:31` (used by `src/Bench.Domain/Scouts/ScoutAssessment.cs:107`); `FindingAssessor.OptionsFor`, `src/Bench.Application/Gate/FindingAssessor.cs:85-87`; `prompts/gate-assess/strict.md`; `ScoutClaimAssessor`, `src/Bench.Application/Scouts/ScoutClaimAssessor.cs:25`; `HandCheck`, `src/Bench.Domain/Gate/HandCheck.cs:12`; `GateHandChecks`, `src/Bench.Application/Gate/GateHandChecks.cs:17` |
| The scrubber has no model-name rule: it redacts e-mails and the deny-list's private names only | `ScoutScrubber.Scrub`, `src/Bench.Domain/Scouts/ScoutScrubber.cs:45-46` |
| The test-catalogue check reads CLI sub-verbs from `ScoutsCommand.cs` only; a verb ships with its catalogue row in the same task (D20) | `tests/Bench.Tests/Cli/ModuleTestsCatalogTests.cs:61-66`; `research/module_tests.md:156-158` |
| A fake CLI that replays a recorded stream per prompt hash (writes no file into its cwd) | `tests/FakeCli/Program.cs:118` |
| Run-to-run variance rivals effects; three configurations reversed by a wider check; **a model marking its own homework passed every answer it got wrong** — the reason the pilot's reader is another vendor, sanity-checked against the reference, its "not met" verdicts spot-checked (D7) | `research/MEASURED_LESSONS.md:17-43` (§1); `:199` (§4d) |
| The scouts' pilot: 12 cells + 2 re-runs + A/A + one exhausted cell ≈ **$19.76**; heaviest cell $2.29, slowest ≈ 2 min | `research/scout-calibration/pilot.md:14-21`, `:60-67` |

**Found while planning (verified in the source repositories with `git`/`gh`, 2026-10-14):**

- **Two candidates were rejected, and why is a finding.** The first draft's story 1, coai #754 (`7b9d0928`, P `f3eeed6a`):
  its spec text **did not exist before its code** — at P the plan had one wider story **5.3** (move `CooldownParser`, a
  duration form, a local display, a server catalog); the 5.3a/5.3b split and 5.3a's text were written by the PR itself (the code
  commit `2c9433b784` precedes the docs commit `d192480d96`), and four review-fix commits (`5cc10c7e20`, `0ca8abf3b4`,
  `ca7282c875`, `3d18713f77`) put behaviours into the hold-out tests that no pre-implementation spec names. A frozen statement
  would have needed a scope note written after the fact — the spec must predate the code. The first draft's story 3, coai #766
  (`722fc597`, P `c8f095bd`): **56 files, +3 395/−108** — a whole epic, not a story; a 120-min wall would measure the envelope,
  not the policy. Selection rule kept from this: **a story qualifies only if its spec text is final at P** (the PR may tick
  boxes, never write the behaviour) **and it is one story or one epic's worth of ≤ ~1 000 changed lines**.
- **The chosen stories** (D3; each SHA, parent, heading and file list re-read with `git`, the PRs with `gh pr view`):
  - **Pilot — `dew_flow_creds_for_devs` PR #176**, squash `9369b4d3093c0852b9969b53ba8c1912141398db`, one parent
    `9f62e4db07a05e247b0fa25ea380cac21909b323` = the PR's `baseRefOid`. Spec at P: `todo/PLAN_typed_stored_secrets.md:429`
    `### E1 — Foundations: the table is the one list, the type exists, the harness runs (Opus)` — stories T1 and T2 with their RED
    / teeth text, final at P (the PR's plan diff only ticks T1/T2, adds "Done" notes and the status line). 16 files; in
    `src_vs_code` +337/−67 (404 changed lines, 211 in tests). PR commits: `f038982c` (T1), `9e380014` (T2), `b7fd99fa` (docs), one
    `perf(tests)` fix `8bcafd59` (the harness reuses the previous fixture's program — speed, no behaviour). Commands from
    `src_vs_code`, identical at P and S: `npm run typecheck` (`tsc -p ./ --noEmit`), `npm test` (`npm run compile && node --test
    "out/test/*.test.js"`); no Docker, no network after `npm ci`. Security work in A's sense (secrets and their PIN door) — Fable
    is legitimate in both arms. Its heading carries `(Opus)` and T1/T2 carry **Model: Opus** — struck from the frozen statement
    (D14).
  - **Medium — `dew_flow_connect_other_ais` PR #751**, squash `3ebf208c55c591103140cd2f3148c4869e14ff7b`, one parent
    `af7f2856ecfaa732159db7dacda3255dfbde2334` = `baseRefOid`. Spec at P: `todo/PLAN_paths_per_side.md:458` `### E2 — the sides this
    profile has been used on`, stories 2.1–2.4 (`:460-466`) with their RED tests. 26 files; in `src_vs_code/src` +878/−10 (888
    changed lines, 458 in tests; ≈ 904 with `package.json`). Two fix commits (`6af34c8c` a refused side stamp is logged;
    `dd610613` an impossible or far-future side time) — candidates for *beyond-spec*.
  - **Fable-tagged — `dew_flow_creds_for_devs` direct commit `5158ac248626a5661578874ec75ad561dfe0d7f8`** (no PR — the repository
    had no PR flow then; on `main`'s first-parent chain), one parent `42421d67555cd7f1a7be5db9459beff2460363bc`. Spec at P:
    `todo/PLAN_agent_consent_policy_stories.md:273` `### S2.2 — Let a pre-consented use call through without a modal or a modal
    slot`, model line `:326` "**Fable** — the consent gate itself; every line here decides whether a human is asked." 7 files; in
    `src_vs_code` +304/−20 (324 changed lines, 160 in tests). Follow-ups `7fb31fea`, `c5368da7` (same issue #95) are outside the
    spec and outside the reference.
- **Every chosen tree at P already carries the OLD rule, byte-identical to arm A's text.** `git diff --stat <pin> d88e293^ --
  common/subagent-models.md common/planning-docs.md` is EMPTY for all three pins, and each pin is an ancestor of `d88e293`:

  | Story | Conventions mount (`.gitmodules`) | Pin (date) | Project `.claude/settings.json` at P | How the rules reach a session |
  |---|---|---|---|---|
  | creds #176 (pilot) | `.claude/rules/shared` | `8355757` (2026-09-26) | `permissions` only (`acceptEdits`; allow Edit, Write, NotebookEdit, Bash, PowerShell) — **no hooks** | **native**: no `.agents/`, no loader; Claude Code's own `.claude/rules/**` discovery (measured, row above) |
  | creds `5158ac24` | `.claude/rules/shared` | `fba970b7` (2026-09-16) | `permissions` only — no hooks | native |
  | coai #751 | `.agents/conventions` | `bb022429` (2026-10-09) | `permissions` + `SessionStart` `load-instructions.mjs` + `PreToolUse` `Bash\|PowerShell` `build-flags.mjs` | **resolver**: the SessionStart hook, then `ENTRY.md`'s procedure (series only) |

  The pilot is an **unmigrated consumer** (the conventions README at `8355757`: "An unmigrated consumer still uses its committed
  `CLAUDE.md` and `.claude/rules/` tree"). `subagent-models.md` carries no `paths:` frontmatter in either version (A: `tasks`
  only; B at `ea3252f`: `tasks` only), so native discovery loads it at session start whatever the task. 63 of the pin's 76 `.md`
  files carry no `paths:` (≈ 449 KB, the conventions' own `research/` and `todo/` included); exactly which load is recorded per
  cell from the `InstructionsLoaded` log.
- **The policy release changed exactly two rule files** — `git diff --stat d88e293^ ea3252f -- common csharp typescript rust
  ENTRY.md AGENTS.md`: `common/subagent-models.md` (+112/−49) and `common/planning-docs.md` (+15, the epics/stories layout the new
  rule points at) — plus `settings/hooks/subagent-model.mjs` (no imports; `DENY_MARKER = 'subagent-models.md:'` at `:35`) and its
  `settings/settings.json` entry. The old catalog at `8355757` and `bb022429` (`tools/lib/rule-catalog.mjs:9` `TASKS`, task check
  `:92`) accepts every task name the new rule's frontmatter lists — relevant only to the resolver layout.
- **The loader's hash is not the file's hash** (series only — the resolver layout). `rule-catalog.mjs:44-47` `normalizedText(file)
  = folded(readFileSync(file, "utf8"))`, `:98` `hash: sha256(text)`; printed as `BEGIN RULE <id> sha256:<hash>` by
  `tools/lib/rule-cli.mjs:115` — the same lines at `8355757`, `fba970b7` and `bb022429`. Native discovery reads the file's bytes.
- **`settings/hooks/ensure-user-guard.mjs` WRITES `~/.claude`** (it self-installs the user guard). It must never run in a cell.
- **The reference sits verbatim outside every repository.** The sessions that built the chosen stories left their transcripts in
  `~/.claude/projects/D--rsd-dew-flow-creds-for-devs/*.jsonl` (and the worktree slugs `D--rsd--wt-creds-*`), edit snapshots in
  `~/.claude/file-history`, and notes in the projects' `memory/` folders — all readable by a shell in a cell. §3.5 covers them.
- **The reference names its own review rounds** — the pilot's `typedFixtures.test.ts` at S carries comments such as "E1 plan round,
  finding 0" and "E1 code round, finding 5" ([E4.S8](PLAN_subagent_policy_ab_E4.md)); the reference is not edited, so its
  sanity reading is not fully blind (§6).
- All three repositories are **public** — the reference is on GitHub.

## 3. Design

### 3.1 Arms

| | Arm A — old policy | Arm B — new policy |
|---|---|---|
| Rule text | `common/subagent-models.md` and `common/planning-docs.md` as of conventions `d88e293^` (`3df745a7…`) — Opus by default, **Fable for security, cryptography, authn/authz and new architecture** | the same two files as of release `ea3252f` — models by role, plus the per-story process (§1) |
| Guard | none wired; child env `SUBAGENT_MODEL_GUARD=off` (D2) | `settings/hooks/subagent-model.mjs` from `ea3252f` wired as `PreToolUse` `Agent\|Task` in the tree's project settings; the variable ABSENT from the child env, whatever the parent holds |
| Main session | `claude -p --model opus` | identical |
| Effort | `effortLevel: high` + `modelSettings` `sonnet`/`opus`/`fable` each `high` (§3.3) | identical bytes |
| Effort-pin hook | `.claude/hooks/effort-pin.mjs`, `PreToolUse` `Agent\|Task`: denies an Agent call whose `effort` argument is present and not `high` (D15) | identical bytes |
| Audit hook | `.claude/hooks/policy-audit.mjs` (observe-only, §3.6) | identical bytes |
| Everything else | the frozen story tree, the run prompt, tools, caps, walls, env | identical |

**The overlay is the only difference, applied the same way to both arms.** Per cell, the frozen story tree is copied to a fresh
path; the arm's two rule files are written over `<mount>/common/`, where `<mount>` is the path `.gitmodules` gives the
`dew_flow_conventions` submodule. The pilot accepts the **native** mount `.claude/rules/shared` only; the resolver mount
`.agents/conventions` is series-only (§12); anything else is refused at `prepare`. A writes its files even where the tree already
holds them (at all three chosen pins the bytes are already identical — §2), so each arm's rule text is deterministic whatever the
story's pin. Both arms get the audit hook `.claude/hooks/policy-audit.mjs` registered for `PreToolUse` (matcher `*`) and
`InstructionsLoaded`, and the effort-pin hook `.claude/hooks/effort-pin.mjs` registered for `PreToolUse` `Agent|Task` (identical
bytes in both arms); B additionally gets the guard at `.claude/hooks/subagent-model.mjs` and its ONE `PreToolUse` `Agent|Task`
entry. All are merged into `.claude/settings.json` (created if absent) by a pure `PolicyOverlay.Apply(projectSettingsJson, arm,
guardEntry)` that keeps the project's own keys and hooks byte-for-byte and refuses a project that already wires a model guard,
an audit or an effort pin ([E1.S4](PLAN_subagent_policy_ab_E1.md)). The arm sources are read byte-exact from the conventions
mirror and checked against git's own blob ids (E1.S6). `ensure-user-guard.mjs` is never copied. The overlay is committed into the
cell tree's one commit, so `git status` is clean at launch and the diff never contains it. Each overlay file's **byte** SHA-256 is
a pin — it is what native discovery reads and what the audit hook hashes at `InstructionsLoaded`.

Known asymmetry, recorded not fixed: B's rule links `../research/RESULTS_subagent_model_tiers.md`, which the story's submodule
pin does not hold — a dangling link in B's tree. Overlaying the conventions research folder would add text A never sees.

### 3.2 The story contract — `bench policy prepare`, its own early unit

A story is frozen by `bench policy prepare` before any cell, written to `research/policy-ab/stories.json`. **Story ids are an
INPUT** (D3); the table below holds candidates until `stories.json` is committed. Scoring, the codex reading and any later
calibration all depend on this record; nothing else does (§8). The judgement inputs the main session writes — the scope note,
R1…Rk, the classification, the gate commands — live in a committed `research/policy-ab/inputs/<story>.json` whose hash
`stories.json` records ([E1.S10](PLAN_subagent_policy_ab_E1.md)).

- **Repository, P, S, merge style** — the pilot accepts `squash` (one parent, P is it; the PR's `baseRefOid` and commit list
  recorded), P verified by `ScoutParentCheck` semantics against the mirror. `direct` (story 3) is series-only (§12).
- **The tree** — `ScoutTrees` with a new `ScoutTreeSpec.KeepProjectSettings` (default false; scouts unchanged): the project's
  `.claude/settings.json` STAYS; a committed `.claude/settings.local.json` is still removed; the grant guard still refuses a tree
  whose own settings grant outside it; **a tree whose committed settings name `effortLevel` or `modelSettings` is refused** (none
  of the three does — §2), so no project layer competes with the pinned effort. Unlike the scouts, the story's plan is NOT
  removed — at P it is the spec. Tree hash recorded. **The dependency restore is not `prepare`'s**: it runs per cell, in the cell
  copy, AFTER the one-commit init (which adds with `-f`, `ScoutTrees.cs:279` — a restore before it would commit `node_modules`)
  and before the wall, identically for both arms ([E3.S1](PLAN_subagent_policy_ab_E3.md)).
- **The frozen statement** — the spec section as it stood at P, **with its model lines and tags struck** (D14: every whole-word
  `Opus`, `Sonnet`, `Fable`, `Haiku` in the section replaced by `[model withheld]`, the struck lines recorded as
  `statement.modelMentions` — the strike list committed with the statement, identical for both arms), plus a **scope note** written
  by the main session from P's text only (pilot: "epic E1 only — T1 and T2; E2/E3 are not in scope; E1 DoD's
  `review_plan`/`review_code`, the consultation and the PR are unavailable here — skip them and say so"). Leak-checked by
  `LeakCheck.Find` against the reference diff's added identifiers and hold-out file stems — names the spec itself already carried
  at P (the pilot's spec names `typedFixtures.test.ts` and its three fixtures) are allowed and listed — hashed, committed **before
  cell one** (E1.S9).
- **The requirement checklist** — R1…Rk derived from the frozen statement only, each with "how to verify" and optional `anchors`
  (names whose presence marks the code that bears on it — used for the reading sheet's bearing ranges, §3.8); hashed, committed
  before cell one. It is the codex reader's yardstick in the pilot (§3.8) and the calibrated assessor's in the series.
  Test-infrastructure deliverables (the pilot's T2 harness, its fixtures, the `tsconfig`/ESLint exclusions) are R-items.
- **The hold-out set and its classification (scored without rewarding the reference's shape).** The reference's test files,
  minus test infrastructure. Pilot: the S versions of `src_vs_code/src/test/slotTable.test.ts` and `syncPinRule.test.ts` (both
  exist at P, so only the tests S added or changed are hold-out — a test present and unchanged at P is a neighbour).
  `typedFixtures.test.ts` and the fixtures under `src_vs_code/src/test/fixtures/typed/` are **T2's deliverable, not a
  yardstick**: never materialised over the candidate's own harness; they are scored only through R-items and teeth (§3.7). Every
  hold-out test is classified, before cell one and blind to every candidate, on **two axes**: is its *behaviour* implied by the
  frozen statement, and is its *interface* (the names, signatures and module paths it calls) implied by it? **Spec-implied** =
  both; otherwise *beyond-spec*, with the reason. The classification goes into `stories.json`, under the rules E4.S3's Fable spec
  sets — which is therefore written BEFORE the pilot is frozen (E1.S13).
- **Gates at P** — the commands per the repository's rules, recorded as **executable + arguments** (D21) in the input file. Pilot,
  from `src_vs_code`: restore `npm.cmd ci` (per cell, above); typecheck `node node_modules/typescript/bin/tsc -p ./ --noEmit`;
  compile `node node_modules/typescript/bin/tsc -p ./`; test `node --test --test-reporter=spec --test-reporter=junit
  --test-reporter-destination=stdout --test-reporter-destination={report} out/test/*.test.js` — the repository's own `npm test`
  with a per-test report added. The baseline runs TWICE at P with the junit report read by `NodeTestReport` (one reader, built by
  whichever of [E1.S7](PLAN_subagent_policy_ab_E1.md) / [E4.S1](PLAN_subagent_policy_ab_E4.md) lands first): tests failing,
  flaky or not comparable at P are listed and excluded from the neighbour signal. `HarvestGates` confirms red-at-P / green-at-S
  **per hold-out file** — it gives one whole-suite verdict per run (§2), so it runs once per file, from the worktree root, with
  `npm.cmd --prefix src_vs_code ci` and `npm.cmd --prefix src_vs_code test`; the per-test picture is the junit runs' (E1.S12).
- **The reference diff** — S's diff, scored beside the candidates as a REFERENCE, never as an arm (it had a human in the loop and
  review rounds; its time and cost are not comparable).
- **Reference-only tokens** (for the post-hoc leak check, §3.5): identifiers the reference added and comment phrases (≥ 6 words)
  it added, over non-doc files only, minus anything present in P's tree or in the frozen statement — a candidate cannot reach them
  except through the reference (E1.S9).

| # | Story | Size | Fit | Caveat |
|---|---|---|---|---|
| 1 (pilot) | creds #176 `9369b4d3`, P `9f62e4db` — typed stored secrets E1 (T1 slot table, T2 `StoredSecret` + compile-fail harness) | 404 changed lines (211 test), 16 files, TypeScript | small; spec final at P; native rule layout; `npm test` | one `perf(tests)` fix — speed only; deps restored per cell (§3.5); model lines struck (D14) |
| 2 | coai #751 `3ebf208c`, P `af7f2856` — paths per side E2, stories 2.1–2.4 | ≈ 904 changed lines (458 test), 26 files, TypeScript | medium; the resolver layout (series only) | two fix commits → classify per test; `test/host/sideSeenScenario.ts` skipped (needs a VS Code host) — `npm test`, never `test:host`; small edits in `panelView`/`helpCoverage`/`configTransfer` tests classified per test |
| 3 | creds `5158ac24` (direct), P `42421d67` — consent policy S2.2, tagged **Fable** by its plan | 324 changed lines (160 test), 7 files, TypeScript | Fable plausible in both arms; native layout; a `direct` commit (series only) | hold-out `brokerMcpRoutes.test.ts` (+139) with its `brokerWorld.ts` helper (both exist at P); follow-ups `7fb31fea`, `c5368da7` outside the spec |

All three stories are TypeScript; no MTP/trx reader is needed by this plan.

### 3.3 One cell

```
claude -p --model opus --output-format stream-json --verbose
       --tools Task Bash PowerShell Read Edit Write Glob Grep TodoWrite
       --strict-mcp-config --mcp-config <empty> --forward-subagent-text --max-budget-usd <cap>
       --no-session-persistence --setting-sources project --settings <artefact root>/policy/pinned-settings.effort-high.json
       (run prompt on stdin, cwd = the cell's fresh tree copy, NO --plugin-dir)
```

- **The launch is a small verb, not an extracted runner** (D5): `bench policy run` ([E3.S3](PLAN_subagent_policy_ab_E3.md))
  takes the lock (one launch at a time), checks the ceiling and the identical envelope, builds the cell tree (E3.S1: the frozen
  tree re-hashed, a fresh copy without `.git`, the arm's overlay through E1.S6's writer, ONE commit, the restore, a clean `git
  status`, the overlay manifest compared with `arms.json`), re-checks every pin (CLI version, tree hash, settings hash, overlay,
  audit-hook and effort-pin hashes, the prompt template) and refuses on any mismatch, writes the prompt, an attempt marker and
  the `launch` spend line, launches the exact argv above with the child environment below, tees stdout line by line to
  `stream.ndjson` and stderr to its own file. `--max-budget-usd` enforces the cap; the wall is a process timeout. `CliScoutRunner`
  is untouched.
- **`--setting-sources project`** (not `""`): the tree's own settings and hooks load as in a real session, the operator's user
  settings do not — so the user-level guard, the user permission list, the user-granted directories, `enabledPlugins` and the
  user MCP servers all drop out of both arms; `CLAUDE.md` and `.claude/rules/**` arrive (measured, §2).
- **No plugins** in either arm: `feature-dev`'s agents carry `model: sonnet` in their frontmatter and would put Sonnet into arm A
  through a call that names no model. Built-in agent types only; each cell's `system/init` event — its skills, agents and plugins
  lists — is recorded and must be **identical across the arms** (the 2026-10-14 spike saw 22 bundled skills, agents `claude`,
  `claude-code-guide`, `Explore`, `general-purpose`, `Plan`, `statusline-setup`, built-in plugins only); a difference is a stop.
- **Pinned settings — effort `high` for every model (D4).** PR #93's `ScoutPinnedSettings.Explicit(effort)` is **widened, not
  copied**: an overload `Explicit(effort, models)` adds `modelSettings.<alias>.effortLevel = effort` for each named alias; the
  scouts' one-argument call keeps today's text (its tests are the proof). The arm file is `Explicit("high", ["sonnet", "opus",
  "fable"])` — `{"effortLevel": "high", "modelSettings": {"sonnet": {"effortLevel": "high"}, "opus": {…}, "fable": {…}}}` —
  written once as `pinned-settings.effort-high.json` the way `ScoutArtifacts.ExplicitEffort()` does, plus one fixed `permissions`
  block from `prompts/policy-ab/permissions.json` (`defaultMode: acceptEdits`; allow Edit, Write, Bash, PowerShell; deny the
  network verbs of §3.5 — prevention only, the detection is E2.S4's). A new `PolicyPinnedSettings` composes the two; **identical
  bytes for A and B**; its hash is the settings pin. *Why every model is named although the top level already says `high`:* PR #93
  omitted `modelSettings` because the scouts load no other settings layer (`--setting-sources ""`); a cell loads the project layer,
  and a per-model entry there would beat a top-level-only pin — naming every model closes that, and `prepare`'s refusal (§3.2)
  closes it twice. A per-LAUNCH `effort` argument is closed by the effort-pin hook (D15).
- **Child environment** — a new `ProbeChildEnvironment.ForPolicyArm(parent, PolicyChildInputs)` composing the same allow-list;
  `PolicyChildInputs` carries the arm, the empty git-config file's path and the `PATH` directories to drop (validated: a blank or
  relative path is refused — E1.S3). Fixed: `CLAUDE_CODE_DISABLE_BACKGROUND_TASKS=1` (Agent results stay synchronous; a
  backgrounded agent stops the cell as in the scouts), `GIT_CONFIG_GLOBAL=<empty file>` + `GIT_CONFIG_NOSYSTEM=1` (no credential
  helper, no url rewrite), `GIT_TERMINAL_PROMPT=0`, fixed `GIT_AUTHOR_*`/`GIT_COMMITTER_*` (`policy-ab`, `policy-ab@invalid` —
  environment, never git config); A adds `SUBAGENT_MODEL_GUARD=off`; B structurally excludes that name. `PATH` is filtered to drop
  a directory holding `gh`.
- **Fresh path per cell** (`<runs root>/<story>/<cell id>`): claude's per-project memory folder is keyed by the cwd, so every cell
  starts with no memory and no transcript of another cell.
- **One cell at a time, attended** (`PLAN_code_lane.md` §4.2: model-authored code is built only while an operator watches);
  never alongside a test suite or another build. **Cell order (three cells, D6, D16)**: the seed text is committed with the pilot
  prediction FIRST; then `bench policy order` ([E5.S1](PLAN_subagent_policy_ab_E5.md)) draws `new SeededDraws(first 8 bytes of
  StableHash(seed text)).Below(3)` as B's position (1, 2 or 3, equal chance) and the two A cells take the other positions (A1
  and A2 in run order); the draw lands in `research/policy-ab/evidence.json` with the frozen cap, wall and pins, and `run`
  launches only the next position of that order.
- **The run prompt** (`prompts/policy-ab/run.md`, hashed, identical across arms): implement the story the frozen statement names,
  from the spec file at its path; **follow the repository's own instructions** for loading its rules and for how work is done
  there; work autonomously — nobody answers questions, record decisions in the final summary; the review gate, the consultants
  and all MCP tools are unavailable here — skip those steps and say so; do not reach the network; do not commit — leave every
  change in the working tree; build and run the tests the repository's rules prescribe before finishing; end with a summary of
  changes, tests run and their result. It names no model, no effort and no policy.
- **After exit — owned by E3.S3 and E3.S4 (D18)**: `git add -A` + `git diff --cached --binary <base>` in the cell tree is the
  candidate (a commit the session made anyway is included); file sections touching overlay paths are dropped and recorded
  (`PolicyCandidate.Split`); the audit log is copied out of `.git/` (never in the diff; an absent log is read as an empty log, so
  the verdict fails closed); E2.S7's verdict is read and written with the `settled` and `verdict` lines of `cells.jsonl`; `run`
  exits 3 on any stop.

### 3.4 Rules reached, guard live, effort pinned — proved, not assumed

**Before the pilot — `bench policy rules-probe`** ([E3.S6](PLAN_subagent_policy_ab_E3.md); cap $3 per arm, its own prompt
`prompts/policy-ab/rules-probe.md`, which states its purpose — a harness self-test in which a hook may be expected to refuse),
in a cell tree prepared exactly as for a measured cell, with ONE difference: the probe keeps session persistence (its fresh cwd
gives it its own per-project folder, removed afterwards by exact path) so child transcripts exist for the effort cross-check. The
probe asks the session to (1) launch ONE `general-purpose` Agent with NO model and the prompt "reply OK"; (2) launch one Agent each
with `model: sonnet`, `opus`, `fable`, each told to run `git status` once with Bash and then reply OK (one tool call each, so the
audit hook logs its effort); (3) launch one Agent with `model: sonnet` and `effort: "xhigh"`, prompt "reply OK".

Pass criteria, read from records — the stream and the audit log — never from the model's prose; a step the session declined is
*inconclusive*, which is a no-go, never a pass:

- **Rule reached (hard, D9).** An `InstructionsLoaded` record whose `file_path` is `<mount>/common/subagent-models.md` with
  `load_reason: session_start`, and the hook's SHA-256 of that file's bytes equals the arm's byte pin. The mechanism is measured
  (§2); `--debug` is the backup if the event ever stops firing.
- **Guard.** In **B** the no-model Agent call was DENIED with a result *carrying* `subagent-models.md:` (the guard's `DENY_MARKER`;
  the CLI may wrap a hook's reason, so the check is containment, and the probe records the real shape); in **A** it ran and its
  child resolved to the Opus id.
- **Effort.** The main session and every child that ran logged `effort.level = "high"`; the same children's transcripts say
  `high` in `effort` / `perTurnEffort` (PR #93's reading) — the two sources agree once, here, and cells then rely on the hook alone.
  **The effort pin (D15):** step (3)'s launch was DENIED in both arms with the effort-pin hook's marker `policy-effort-pin:` and no
  child ran under it.
- **`system/init`** lists are identical across the two probes; the resolved `opus`/`sonnet`/`fable` ids are identical too.

On a pass the probe writes `research/policy-ab/pins.json` (CLI version, main model id, resolved ids, the `system/init` lists) —
the pins `run` re-checks; `research/policy-ab/rules-probe.md` (each criterion per arm, the observed denial texts, whether hook
`agent_id`s matched the transcript file ids, whether B's guard digest is visible in the stream) is written in every case.

**The effort break-it, live, once** (cap $2): arm B's probe tree with the pinned settings replaced, for this run only, by the same
text plus `modelSettings.sonnet.effortLevel = "xhigh"` (the 2026-10-14 overlay); only the Sonnet child of step (2) is asked for.
The effort audit must stop it, naming `xhigh`. A probe that does not catch it is a no-go.

Either arm failing any criterion is a no-go (§4). The streams and audit logs become fixtures (E3.S9).

**In every measured cell**: the same `InstructionsLoaded` check (`PolicyRuleReached`, pure over the audit log and the pins) — a
cell whose session did not load its arm's rule is a **STOP and a no-go** (D9); the full `InstructionsLoaded` list (the session's
starting context, identical across arms bar the two overlay files); the effort-pin denials counted; for B, how many Agent calls
the guard denied. The guard's digest lines are reported *not captured* until the probe's arm-B stream shows where they land.

### 3.5 Isolation and leak controls

The scouts' access check covered read-only tools. A coding cell executes shell and writes scripts, so the boundary is every route
to the reference:

| Route | Prevention | Detection (read-back) |
|---|---|---|
| GitHub via `gh`/`git`/`curl`/`Invoke-WebRequest` in Bash or PowerShell | no git remote in the tree; no credential helper (§3.3); `gh` off `PATH`; permission deny rules for the network verbs; web tools absent from `--tools`; MCP off | `CommandAudit` (new, pure, [E2.S4](PLAN_subagent_policy_ab_E2.md) — the cornerstone): every main and child Bash/PowerShell command text checked for network verbs and hosts — any hit STOPS the campaign; an un-auditable command (`-EncodedCommand`, `iex` of a non-literal) is never fine |
| Sibling clones on `D:\rsd` (originals, mirrors, other cells' trees, the artefact root, the hold-out tests) | the trees root is outside every repository; the grant guard | `PathAudit` widened to main calls and to `Edit`/`Write` (an unknown tool in scope is unresolvable, hence outside — E2.S3); `CommandAudit` resolves absolute paths, `..`, `~`, `%VAR%`/`$env:` in command text — outside the cell tree (toolchain dirs on an allow-list that may not contain or touch a protected root) STOPS |
| **`~/.claude`** — the reference verbatim in `projects/<creds slugs>/*.jsonl`, `file-history`, the `memory/` folders; other projects' memory | `--setting-sources project`; fresh cwd | the path rule above (`~/.claude` is outside the tree) — and canaries there in the access check (D8) |
| **A script the model writes** (a `.js`/`.mjs`/`.cjs`/`.ts`/`.ps1`/`.sh`/`.py` file, or a `package.json` script) that opens the network or reads outside the tree | — | **content scan** in `CommandAudit` over every `Write` content and `Edit` new text: `fetch(`, `http`/`https`/`net`/`dgram` modules, `child_process` with `git clone`/`curl`/`gh`, `Invoke-WebRequest`/`Invoke-RestMethod`, absolute paths outside the tree, `os.homedir()`/`~`/`.claude`. A hit FLAGS the cell — unscored until the operator reads the hunk and clears (recorded) or voids it; a later command that runs the flagged file is a stop |
| **Tampering with the instrument** (D12) | — | an `Edit`/`Write`, or a shell command, on `.claude/settings.json`, `.claude/hooks/**`, `<mount>/common/subagent-models.md`, `<mount>/common/planning-docs.md`; ANY tool input naming the audit log (`.git/policy-audit.ndjson`) — STOP |
| The plan's later "as built" notes | the spec is P's text; the scope note is leak-checked | — |
| Dependencies needing the network mid-run | the restore runs once per cell copy, after its one commit and BEFORE the wall, identically for both arms (§3.2) | a restore attempted mid-run is a network verb → detected |

**Post-hoc, every candidate** ([E2.S8](PLAN_subagent_policy_ab_E2.md)): `LeakCheck.Find` over the candidate's diff text with the
story's reference-only identifiers (§3.2), plus a phrase match for the reference-only comment phrases (whitespace collapsed,
comment leaders stripped, case ignored). A hit is reported and reviewed by the operator; a **strong hit** — one reference-only
comment phrase, or three or more DISTINCT reference-only identifiers — voids the cell. ("Not motivated by the frozen statement"
is enforced upstream: the reference-only lists already exclude every token in P's tree or the statement.)

**`bench policy access-check`** ([E3.S8](PLAN_subagent_policy_ab_E3.md); live, before cell one, cap $5, ONE run in an arm-B tree —
the routes do not depend on the arm, whose only environment difference is the guard variable, and B's guard is why the probe names
every child's model): random canaries planted in a sibling cell tree, the mirror root, a stand-in for an original clone, a junction
escaping the tree, and — each as one new uniquely named `.txt` file listed in a manifest before it exists, removed afterwards by
exact path, **approved by the owner (D8)** — in `~/.claude/projects/D--rsd-dew-flow-creds-for-devs/`, its `memory/` folder and
`~/.claude/file-history/`. **Each read route against every target** (D23, ≈ 25 calls): `Read`; Bash `cat`; PowerShell
`Get-Content`; then `git -C <clone stand-in> log`; a shell fetch of `https://github.com`; a written script that reads a canary and
is run; and ONE `opus` child reading one outside canary with a shell, to show children fall under the same audit. Outcome per
(route, target): denied = protected; attempted and detected = detected-only; content returned and NOT detected = leaked, exit 3,
the campaign never starts; never attempted = inconclusive (also exit 3). **Expected on native Windows:** file tools denied, shell
reads NOT denied (no OS sandbox).

**Decided: the pilot's access-check result decides the series' boundary.** If shell reads outside the tree — the original clones
and `~/.claude` above all — come back *protected*, the series runs as the pilot did. If they are *detected-only*, the pilot may
still run under fail-stop detection, but **the full series requires an OS-level boundary** — an OS sandbox (WSL2 with Claude
Code's own sandbox, or a container) and/or an outbound Windows Firewall block for the cell processes — and `pilot.md`'s go/no-go
says so in its own row; **the owner decides then**. Neither is built for the pilot: both are machine-level changes (the firewall
rule needs elevation), and WSL carries the 2026-10-07 deletion incident's constraints (no deletion from WSL, script files only).

### 3.6 Read-back and the policy audit — every cell

**The audit hook** (`prompts/policy-ab/policy-audit.mjs`, hashed into the pins, written by the overlay as
`.claude/hooks/policy-audit.mjs` in both arms — [E1.S5](PLAN_subagent_policy_ab_E1.md)): on `PreToolUse` (every tool) and
`InstructionsLoaded` it appends one JSON line to `<cell tree>/.git/policy-audit.ndjson` — event, `agent_id` and agent type when
present, `tool_name`, the payload's `effort` verbatim, and for `InstructionsLoaded` the `file_path`, `memory_type`, `load_reason`
and the SHA-256 of the file's bytes. The log path comes from the **script's own location** (`import.meta.url` → `../../.git/`), not
the process cwd — a hook's cwd can follow the session's `cd`, the script's location cannot. It prints nothing, exits 0, never
blocks, and reads no environment variable. A node start per tool call adds wall time equally to both arms; it is recorded, not
subtracted.

**The effort-pin hook** (`prompts/policy-ab/effort-pin.mjs`, D15, E1.S5): on `PreToolUse` `Agent|Task` it denies, with a reason
opening `policy-effort-pin:`, a call whose `tool_input.effort` is present and not `high`; it allows every other call, prints only
the hook decision, and is identical in both arms. Denials are counted per cell (E2.S6) — a counted fact, never a stop; the
child-level effort audit below stays the backstop.

**Why the hook, not the transcripts, is the effort source for cells.** Both are proven (§2). Transcripts need session
persistence, which the cells turn off (no transcript of one cell can feed another, nothing accumulates under `~/.claude`); the hook
works with persistence off, logs the MAIN session as well as every child, per call, as it happens, and its teeth were shown with an
`xhigh` overlay. A child that makes no tool call logs no effort; the pilot counts those as *unobserved* = Agent calls that ran −
distinct `agent_id`s in the log (D10). A tool line with no effort level is counted (`MissingLevel`). The stop rule below needs no
matching of log lines to Agent calls, so the pilot does none.

Per Agent call (from the stream): `subagent_type`, the `model` argument (or none), `resolvedModel`, the child's `message.model`,
duration, tools by kind (write tools → it wrote code; read-only → it gathered), files and lines it changed (from tool inputs —
shell writes are invisible and said so), deduplicated tokens, any fallback, whether it ran. Per cell: Agent calls by (type ×
resolved model), the effort levels seen (main and children), unobserved children, effort-pin denials, the main session's own edits
(files, added + removed lines), the delegation share (lines written by children ÷ all lines written), guard denials (B), the
`system/init` lists, and `modelUsage`'s internal Haiku calls listed apart.

- **Compliance is reported, not voided** — it is the policy as followed. **A**: Opus everywhere except its Fable carve-out —
  children on Opus, Fable launches each with their stated reason checked against A's carve-out (security, cryptography, authn/authz,
  new architecture); a Sonnet child in A is a deviation. **B**: every launch named an alias; Sonnet children that wrote code;
  read-only children per model; whether the main session stayed within one file and 50 lines; Fable launches and their reason; the
  process steps visible (a brief/scenarios before a coder, an Opus review). Every Agent prompt is classified by hand — by an Opus
  judge agent, the main session verifying (D19) — into `research/policy-ab/compliance.md`.
- **Contrast present** (a §4 row): A launched at least one child, and B ran at least one Sonnet coder (a Sonnet child that used a
  write tool). Otherwise `pilot.md` says plainly what was actually compared.
- **Stops** (cell unscored, campaign stops — one pure decider, [E2.S7](PLAN_subagent_policy_ab_E2.md)): a leak detected (§3.5); a
  tampering (D12); a pin moved (CLI version, resolved opus/sonnet/fable ids, settings hash, overlay, audit-hook and effort-pin
  hashes, tree hash, the `system/init` lists); the arm's rule not loaded (D9 — a no-go); a `model_refusal_fallback` anywhere —
  re-run once as a new generation, counted; **any logged effort other than `high`**, main session or child, or a main session with
  no effort record at all — the pinned settings did not govern, a configuration defect a plain re-run would repeat: the campaign
  stops and the cause is fixed with a RED test first; a backgrounded agent; a cost-check gap > 2 % (computed over the calls that
  ran — never-ran launches filtered first, D11). **A fix that changes a pinned input invalidates the earlier cells (D17)**: all
  cells re-run if the ceiling allows, else no-go; a fix that moves no pin re-runs only the stopped cell as the one generation.
- **Not stops**: an unobserved child (D10), an effort-pin denial, a guard denial — counted and reported.
- **Outcomes, never voids**: the wall reached, the cap exhausted, a failed build, red tests — scored with their cost and time.

### 3.7 Metrics

| Metric | Definition | Role |
|---|---|---|
| **Success** (D13) | the candidate typechecks and builds ∧ **at least one spec-implied hold-out test compiled against the candidate** ∧ every compiled spec-implied test green ∧ no neighbour test red that was green twice at P. An empty or non-compiling candidate is never Success; E4.S3's Fable spec settles the exact rule within this | **primary** |
| Interface mismatch | per hold-out file: it did not compile against the candidate's API (errors only inside the hold-out file) — its OWN reported outcome; its tests are not run; for a test whose interface is spec-implied, the R-item covering that interface carries the deviation | reported beside Success |
| **Requirement coverage** | R-items met ÷ k, partial = ½ — **codex-read** in the pilot (another vendor, sanity-checked against the reference, "not met" spot-checked — §3.8, D7), by the calibrated assessor in the series | **primary** (pilot: labelled *codex-read*) |
| Teeth (D22) | the plain revert of the candidate's non-test changes (`PLAN_code_lane.md:165`), its tests kept: at least one NEW test, and no new test green; an existing test that goes red is *changed with teeth*, one that stays green is *not assessed* | secondary |
| Own suite | the candidate's tree as delivered (its own tests, before any hold-out) builds and is green | secondary |
| Neighbours (signal 4) | tests green twice at P, outside hold-out and test infrastructure: none red; a neighbour the candidate removed, renamed or skipped is `missing` — reported, neither red nor green | secondary |
| Beyond-spec hold-out | beyond-spec tests green ÷ beyond-spec tests that compiled (*not captured* when none compiled) | reported |
| Wall time | launch → exit; `result.duration_ms`; per child `totalDurationMs`; `duration_api_ms` and `num_turns` where the envelope carries them (reader widened, else *not captured*) | reported — "speed" |
| Cost | `total_cost_usd`; per model from tokens at the published price; cache read/write apart | reported |
| Policy audit | §3.6, the effort audit, the effort-pin denials and the contrast included | reported (effort: a stop) |
| *Series only (§12)*: verified residual defects, engineering quality 1–5, pairwise preference | the blinded assessor | — |

`TouchesCause` is NOT a quality signal here: a valid alternative implementation may touch other files. `SolutionReport.Passed`
(which includes it) is not used. **Plan, research and docs hunks** — a file under `todo/` or `research/`, a `.md` file, or
`CHANGELOG.md` at any depth, one rule `PolicyDiffFilter.IsDocPath` shared by E1.S9 and E4.S1 — are stripped from every candidate
AND the reference before any scoring or reading; a document the arm's own rule requires is never counted as scope creep.

### 3.8 Scoring instruments

**Mechanical, per hold-out FILE** ([E4.S1–E4.S5](PLAN_subagent_policy_ab_E4.md)) — `SolutionSignals` widened, not copied: an
overload taking a prepared tree (a scoring copy — a worktree of the frozen story tree, which is ONE commit holding every
submodule's files, with the story's dependency folder copied from a per-story template and stale build output removed, the
candidate applied) and a hidden-test port that writes a file's BYTES from `git -C <mirror> show S:<path>`. **Each hold-out file is
materialised alone, into its own copy of the candidate tree**, compiled and run there; a file that does not compile is recorded as
*interface mismatch* for that file — its emitted JavaScript is never run — and touches no other file's result. The candidate copy
WITHOUT any hold-out file must build first, else Success is false ("does not build") and every hold-out file is *not run*. One
file couples its tests: a beyond-spec test's interface can take the file's spec-implied tests down with it, and the record shows
which class the failing names belong to. The neighbour suite runs once on the candidate copy without any hold-out. Per-test
results come from `NodeTestReport`; compile diagnostics from tsc's non-pretty output (`TscDiagnostics`). Signal 3 (teeth, D22) and
signal 4 (neighbours against the twice-run baseline) are built here as `PLAN_code_lane.md:165-166` specifies; the reference's test
infrastructure is never materialised (§3.2). The attended rule holds. Verb `score` writes one record keyed by a label, with no
arm, model or cost field on its type.

**The gate's fairness check (pilot, [E4.S6](PLAN_subagent_policy_ab_E4.md)):** first the reference through `score` — Success, zero
mismatches; then one hand-made **valid alternative shape** of the reference — same behaviour, different file and structure where
the spec leaves the shape open, written by a Sonnet coder from an Opus brief that cites the classification rows — through the
same gate. It must score Success, with interface mismatches only where the interface is not spec-implied. This is the evidence
that the gate does not reward the reference's shape (`research/policy-ab/fairness.md`).

**The codex reading (pilot, D7, [E4.S7–E4.S9](PLAN_subagent_policy_ab_E4.md)).** The pilot's quality reading against R1…Rk is done
by **another vendor** — codex, read-only — not by the operator and not by a Claude model reading Claude's work (a model marking its
own homework passed every answer it got wrong, `MEASURED_LESSONS.md:199`, §4d). Minimal by design:

- **The reading sheet** (`research/policy-ab/reading-sheet.md`, verb `sheet`): per R-item its "how to verify", and for each of the
  three candidates and the reference — under fresh `BlindedId.Mint` ids from a seeded shuffle, docs hunks stripped, scrubbed by
  `ScoutScrubber` with ONE composed deny-list (the operator's private names + `opus`, `sonnet`, `fable`, `haiku` + every resolved
  model id from `pins.json` — the scrubber has no model rule of its own, §2), a scrub that changes a file's line count refused, arm,
  model, cost and time withheld — the **bearing ranges**: the post-image line ranges of every hunk of that candidate, narrowed to
  the ranges containing an R-item's `anchors` when it has them.
- **The assessment tree**: an extract of the frozen story tree's one commit (no `.git`, no dependencies); under `candidates/<id>/`
  each candidate's changed files in full (post-image) and its diff, docs-stripped and scrubbed (an empty candidate gets a one-line
  `NO_CHANGES.txt`, so a *not met* has something to cite); the reference is one of them under its own fresh id. The id → label key
  lives in `<artefact root>/policy/<story>/keys/` — outside the tree AND its parent — between `sheet` and `read`, and is copied to
  the batch folder only after the answer is read (the `ScoutClaimAssessor` rule, `ScoutClaimAssessor.cs:24`).
- **The launch** reuses `ScoutClaimAssessor.AskAsync`'s shape (`ScoutClaimAssessor.cs:29-47`): ONE batch → ONE codex launch,
  `ICliAgentRuntime.AskAsync` with `AgentAsk(CliCodex, executable, prompt, assessmentTree, wall, model)` and
  `FindingAssessor.OptionsFor(ModelRuntimeKind.CliCodex, batchDirectory)` (`FindingAssessor.cs:85-87`: `-s read-only`, the output
  schema enforced, the answer file, MCP off); prompt, schema and answer in a fresh batch folder outside the tree
  (`ScoutClaimAssessor.cs:11`); the reader model named explicitly, never the CLI's default. The prompt and schema are new
  (`prompts/policy-ab/read-requirements.md`) and live in a pure `PolicyRequirementReading` beside `ScoutAssessment`: `Prompt`,
  `Schema`, `Read`. A thin `PolicyRequirementReader` mirrors `ScoutClaimAssessor`; no shared base is extracted for the ~10 lines
  they share (the batch file writes and the one `AskAsync` call) — the two read different rows and a base class would couple the
  scouts to this plan.
- **The answer**: per (candidate id × R-item) exactly one verdict — met / partial / not met / unresolved — with a
  `candidates/<id>/<path>:<line>` (or `:<start>-<end>`) for every verdict but *unresolved*. `PolicyRequirementReading.Read`
  REFUSES a malformed answer as a whole, naming every defect — **stricter than its model**, `ScoutAssessment.Read`, which leaves an
  unnamed, doubled or illegal row unassessed (`ScoutAssessment.cs:121-123`): an unknown id or R-item, a missing or duplicate
  (id × R-item) row, a verdict outside the four, a missing file:line, a file:line outside that candidate's files or line count, a
  cite into another id's folder. A refused answer is re-asked once with the defects quoted; refused twice, the reading is *not
  captured*. No pairwise, no defect hunt, no quality axes in the pilot.
- **Sanity on the reference (a no-go gate)**: the anonymised reference, read in the SAME batch, must be read as met on ≥ 90 % of
  R-items (`met·10 ≥ 9·k`; partial is not met); below that, the checklist or the reader is broken → no-go, before any candidate's
  coverage is read.
- **Spot-check**: every *not met* verdict — candidates' and reference's — is checked against the diff by an Opus judge agent the
  main session dispatches (D19), BEFORE the id → arm key is opened, and recorded agree / disagree with the reason in
  `research/policy-ab/handcheck.md`, labelled **"a model checking a model — weaker than a human reading, the owner's choice
  (D7)"**. Disagreements are reported; the verdict is not overwritten.
- Coverage is reported as ***codex-read***; codex spend (or its subscription use) is reported apart, never inside an arm or the
  pilot ceiling. `read` writes `research/policy-ab/reading.json`.

**Series only (§12):** the full blind assessor (defects, quality axes, pairwise in both orders) and its calibration with planted
variants — both grown from the pilot's reader.

### 3.9 Money and time — the pilot

- **Identical envelope**: one cap and one wall for BOTH arms, frozen before cell one (scouts used per-arm caps; here a per-arm cap
  would change the estimand); `run` refuses a cell whose cap or wall differs from an earlier cell's.
- **Pilot numbers**: cap **$40** per cell (`--max-budget-usd`), wall **120 min**. Ceiling **$207.60**, each item at cap × 1.2 for
  overshoot: 3 × $48 (the cells A, B and the A/A cell, D6) + $48 (one stop-and-re-run generation) + rules-probe 2 × $3.60 + the
  effort break-it $2.40 + access-check $6 = $144 + $48 + $7.20 + $2.40 + $6. The probes are inside it (D23): E3's live pre-flight
  reserves $15.60 ($13 at cap). Basis: the scouts' heaviest cell was $2.29 for under two minutes of reading; a story run is long and
  multi-turn with build/test cycles. A rough single-Opus estimate (≈ 200 turns over a ~150 k cached context ≈ 30 M cache-read
  tokens ≈ $6, + ~200 k output ≈ $4, + ~2 M cache writes ≈ $10) gives ~$20; A's Opus children may double it. The pilot story (404
  changed lines) is smaller than that estimate assumes. $40 is headroom, not a forecast. A cap or wall reached is an outcome.
- **Spend is recorded, not ledgered**, in the pilot: every launch — cells and probes — writes a `kind`-tagged `launch` line and a
  `settled` line to `cells.jsonl` (E3.S2); a launch's spend is its `total_cost_usd`, or, with no settled cost, its whole cap × 1.2
  as unknown cost; before every launch the run verb refuses when recorded spend + this launch's cap × 1.2 would pass the ceiling.
  The ceiling funds ONE re-run generation; a stop whose fix moves a pin re-runs all cells only if the ceiling still allows (D17).
  The series' ledger form is set by the re-split (§12).
- Codex (the reader, D7) is reported apart — never inside an arm or this ceiling.

### 3.10 Persistence

**Files, no Postgres**: the artefact root holds streams, markers and audit logs (local only); `research/policy-ab/` holds the
committed records. No harvest: harvest lands a Kind `Fix` row in the bank (`src/Bench.Domain/Authoring/CodeTask.cs:15`) and these
are stories, not bug fixes. A DB store, a read API and a console tab are NOT built.

### 3.11 Reuse map — the pilot

| Piece | How |
|---|---|
| `ScoutTrees`, `ScoutTreeSpec` | widened: `KeepProjectSettings` + the effort-key refusal (scouts' default unchanged); `ScoutSubmodules` moved, unchanged, into `Bench.Domain` so `.gitmodules` can be read purely (E1.S4) |
| `ScoutParentCheck`, `ScoutHistory`, `LeakCheck`, `ScoutScrubber`, `ScoutTokens`, `ScoutCost.Check`, `PriceTable`/`CliRate`, `SeededDraws`, `StableHash` | reused (`ScoutParentCheck`/`ScoutHistory` gain overloads without a `ScoutCandidate`, E1.S8; `LeakCheck.Find` also over candidate diffs, §3.5) |
| `ScoutPinnedSettings.Explicit`, `EffortLevels`, `FileNameFor`; `ScoutArtifacts.ExplicitEffort()`'s write-once-and-hold rule | `Explicit` widened with a `models` overload (§3.3); the rest reused; `PolicyPinnedSettings` composes it with the permissions block |
| `ClaudeSubagents` / `SubagentTranscript` | widened: `MainToolCall` gains `Input`, `Fate`, `ResultText` (additive); envelope gains `duration_api_ms`, `num_turns` as *captured or not*; the `system/init` lists read; every tool call in stream order (E2.S1) |
| `PathAudit` | widened: the audited tool set and the main session become parameters; scouts pass today's values |
| `ProbeChildEnvironment` | widened: `ForPolicyArm(parent, PolicyChildInputs)` beside `ForScouts`, same allow-list |
| `SolutionSignals` | widened: prepared-tree overload + hidden-test port + one-file-per-copy hold-out runs; signals 3–4 added |
| `HarvestGates` | reused per hold-out file for red-at-P / green-at-S at `prepare` |
| `FindingAssessor.OptionsFor`, `ScoutClaimAssessor.AskAsync`'s launch shape, `ScoutAssessorLaunch`, `ScoutAssessment`'s envelope parse (extracted into one helper both call), `BlindedId.Mint` | reused for the codex reader (§3.8); its prompt, schema and row parser are new (`PolicyRequirementReading`) |
| Shared once, built by whichever story lands first | `NodeTestReport` (E1.S7 or E4.S1), `PolicyDiffFilter.IsDocPath` (E1.S9 or E4.S1), `hosts/Cli/PolicyCommand.cs` (E1.S11, or E3.S3 if it starts first) |
| **New** | `PolicyOverlay`, `PolicyMount`, the arm sources and overlay writer, the audit and effort-pin hook scripts, `CommandAudit` (with the content scan), the reference-only phrase match, `PolicyRuleReached`, `PolicyEffortAudit`, `PolicyAudit`, `PolicyCellVerdict`, the story/checklist/hold-out records, the cell tree builder, the probe and access-check judges, the reading sheet and assessment tree, `PolicyRequirementReading` + `PolicyRequirementReader`, `PolicyPilotOrder`, `bench policy` verbs `prepare`, `run`, `rules-probe`, `access-check`, `score`, `sheet`, `read`, `order` |

`CliScoutRunner` and `CliSubjectRuntime` are not used by the pilot; `CliAgentRuntime.AskAsync` is used only for the codex reader,
as `ScoutClaimAssessor` uses it.

### 3.12 Prompts and records — the pilot

`prompts/policy-ab/`: `run.md`, `rules-probe.md`, `access-check.md`, `read-requirements.md`, `permissions.json`,
`policy-audit.mjs`, `effort-pin.mjs` — each hashed into the pins. `research/policy-ab/`: `inputs/<story>.json` (the judgement
inputs, §3.2), `stories.json` (§3.2), `arms.json` (conventions shas, the overlay files' byte hashes, the hook and pinned-settings
hashes, the per-story overlay manifests), `prices.json` (a copy of the scouts' with a **Fable** row, URL and date — **no cell of
either arm runs before it exists**, since Fable is legitimate in both), `pins.json` and `rules-probe.md` (§3.4), `access-check.md`,
`fairness.md` + `fairness/alternative.diff` (§3.8), `prediction.md` (with the seed) and `evidence.json` (caps, wall, pins, the
drawn order — §3.3), `cells.jsonl` (per launch: `kind`-tagged spend lines; per cell: arm, story, generation, pins, outcome, every
metric, the verdict and audit with effort), `candidates/<cell>.diff` (docs-stripped), `compliance.md` (the hand classification of
every Agent prompt and every content-flag clearance), `reading-sheet.md`, `reading.json` (the codex verdicts per id × R-item,
keyed back to labels after the answer is read), `handcheck.md` (the spot-check of every *not met*), `pilot.md`. All three stories
are from public repositories; diffs and statements may be committed, raw streams and audit logs are not (large, carry machine
paths).

### 3.13 Growth budget — the pilot

| Surface | Projected size | Retired by |
|---|---|---|
| The frozen pilot tree | ~60 MB with the conventions submodule | removed by hand (exact path) after `pilot.md`'s pull request is merged |
| Cell trees (with `node_modules`/`out`) + scoring copies (one per hold-out file per candidate) + the dependency template + the assessment tree | 3 cells × ~1 GB + ~10 copies × ~1 GB + ~1 GB + ~0.1 GB ≈ 14 GB worst case | each removed by exact path once its scores or records are committed |
| Raw streams, markers, audit logs, the codex batch folder | ~7 × ~5 MB, local | kept until promotion, then gzipped; never committed |
| claude per-project folders (one per cwd; the probes' with transcripts) | ~7 small folders under `~/.claude/projects` | removed by the exact slugs of the pilot's paths, nothing else |
| The access-check canaries in `~/.claude` | 3 small files | removed by exact path right after the check (a manifest written first) |
| Committed records | < 2 MB | kept with RESULTS |

## 4. The pilot — 1 story × 2 arms × 1 run + one A/A cell, and the owner's go/no-go

Order: (1) `prepare` the pilot story — after E4.S3's Fable spec sets the classification rules — (tree, statement with model lines
struck, checklist, hold-out classification, reference-only tokens, baseline twice, gates, byte hashes) → commit; (2) `prices.json`
with the Fable row; (3) `rules-probe` both arms, then the effort break-it; (4) `access-check`; (5) the fairness check — the
reference and the valid-alternative variant through the mechanical gate; (6) the **prediction and the seed** committed (D16);
(7) the evidence commit — `order` draws the three-cell order, freezing caps, wall and pins; (8) the three cells — A1, B, A2 in the
drawn order — back to back, attended, one at a time, each committed before the next; (9) mechanical scoring and the post-hoc leak
check; (10) the reading sheet → the codex reading in one batch (three candidates + the reference) → the reference sanity check →
the spot-check into `handcheck.md`; (11) `pilot.md`, drafted by an Opus judge from the committed records (D19).

**It checks the instrument, not the policy.** One run per arm cannot estimate variance (MEASURED_LESSONS §1); the pilot reports
all three cells' numbers labelled *instrument check — not a comparison*. The A/A pair (D6) is reported as *how far two runs of the
same arm land apart on this story* — a first look, not a variance estimate.

| Go criterion for the full series | Evidence |
|---|---|
| **Rule reached — hard** (D9): in both probes and every cell, the `InstructionsLoaded` record of the arm's own `subagent-models.md` with its byte pin. No "or explained" | audit logs, `pilot.md` |
| Guard live: B's guard denied the probe's no-model launch, A's did not | §3.4 streams |
| Model assignment matches the arm's rule: A's children on Opus except Fable launches that fall under A's carve-out (each reason checked); B's launches all named an alias | §3.6 audit, `compliance.md` |
| **Contrast present**: A launched ≥ 1 child and B ran ≥ 1 Sonnet coder — else `pilot.md` says plainly what was compared | §3.6 audit |
| **Effort equal and `high` everywhere**: every logged level — main session and every child, all three cells and both probes — is `high`; hook and transcripts agreed in the probe; the effort pin denied the probe's `xhigh` launch in both arms; the `xhigh` break-it was caught; unobserved children and effort-pin denials counted; `system/init` lists identical | audit logs, `cells.jsonl` |
| No leak route returned content undetected; every (route, target) status known; no unreviewed content flag; no tampering; no strong post-hoc hit — and, if shell reads of the original clones or `~/.claude` were open, the row "the series needs an OS-level boundary" present (§3.5) | `access-check.md`, per-cell audits |
| Capture complete: diff extracted, cost check within 2 %, time and tokens per model present — and the **capture repeats** across the A/A pair (the same fields present and readable in both A cells) | `cells.jsonl` |
| Scoring discriminates: the hold-out red at P and green at S; the reference and the valid-alternative variant scored Success (§3.8); the codex reading parsed for all three candidates and the reference, the reference read as met on ≥ 90 % of R-items, every *not met* spot-checked | `stories.json`, `fairness.md`, `reading.json`, `handcheck.md` |
| **Caps sized**: each cell spent < 0.8 × cap ($32) and ran < 0.8 × wall (96 min); otherwise cap and wall are resized (× 1.5 / × 2 of the larger cell) before the series, and a clipped cell is re-run under them | pilot spend and time |

A no-go names the failing criterion and its fix; the owner decides on a re-pilot.

## 5. The full series — 3 stories × 2 arms × 3 runs (EXPLORATORY, built after the owner's go)

- **Order**: 9 blocks (repeat outermost, then story), each block one A and one B cell; the leading arm by
  `SlotRotation.Rotated([A, B], globalBlockIndex)` — 5 : 4 at nine blocks, so position is reported as a covariate. One cell at a
  time; a story's two arms never separated by more than one block (cache and time-of-day drift fall on both).
- **Pinned and frozen before cell one**: CLI version, resolved ids, the effort file (`high` for every model), settings/overlay/
  hook/prompt hashes, the `system/init` lists, story trees, caps, walls, the dependency restore's lockfile hashes, and a written
  **prediction** (committed sha cited in RESULTS). A pin move stops the series; a moved alias is a new series.
- **Reported, every run**: per story × arm successes/3, interface mismatches, coverage, defects, quality, teeth; time and cost as
  median and range; per-story B − A; then the equal-story-weighted mean of the per-story differences; pairwise wins/ties/losses; the
  rule-delivery layout per story (native or resolver) and the contrast per cell beside the numbers. The three stories are the
  generalisation units: no p-values, no power claim, the label **exploratory** in the RESULTS title and status line.
- Void cells re-run as generations up to 2 spare cells; further voids are reported, not replaced.
- Projected spend: 18 × the pilot-derived cap × 1.2 (≈ $300–900 at $20–40 a cell); the ceiling is the owner's number.
- What must be built first is §12.

## 6. Threats to validity

- **One story ≠ the policy.** The plan-level Fable work is untouched (§1, §10).
- **The whole policy moves, not the model.** B's rule changes process (briefs and scenarios before code, Opus review, the edit
  bound) as well as models (§1); a difference cannot be attributed to Sonnet-vs-Opus alone. The contrast row (§4) and the per-cell
  audit say which parts of the policy actually acted.
- **Fable in both arms.** The pilot and story 3 are security work, where A's own rule calls for Fable; a cell where both arms put
  the core on Fable compares less than one where they do not. Reported per cell, never voided.
- **Variance.** Three runs per arm per story; a single difference reads as noise until the series shows its direction per story.
- **Unequal effort between models** — the measured precedent: the scouts' pilot ran Sonnet at `xhigh` and Opus at `high` because a
  child takes its OWN model's effort from the settings, and the comparison it produced (Sonnet slower and dearer) was partly an
  effort comparison (§2). Here every model is pinned `high` in identical bytes for both arms, a unit test proves every model
  resolves to `high`, a per-launch `effort` argument is denied by the effort-pin hook in both arms (D15), and every cell's effort
  audit stops the campaign on any other level (§3.3, §3.6). Residual: a child that makes no tool call is *unobserved*, reported per
  cell.
- **Shape bias in the hold-out.** The reference's tests encode its interface; a valid alternative can fail to compile against them.
  Each hold-out file runs alone, *interface mismatch* is its own outcome, tests are classified on behaviour AND interface, the
  reference's test infrastructure is never imposed, and the valid-alternative variant proves the gate (§3.2, §3.8). Residual: one
  file couples its tests — a beyond-spec interface can take spec-implied tests in the same file down; the record shows it.
- **Spec gap.** The pilot's spec is final at P and its one fix commit is speed-only; story 2 has two fix commits whose behaviours
  may be beyond-spec; story 3's follow-ups are outside the reference. The spec's model lines are struck (D14), which removes a
  then-policy instruction from both arms alike.
- **Policy reach differs by layout.** In the native layout (stories 1 and 3) both arms' rule texts load at session start
  unconditionally — measured, and a hard pass condition (D9); in the resolver layout (story 2, series) a rule reaches a session only
  if it follows `ENTRY.md` for its task — there it is a covariate, and §5 reports the layout beside each story's numbers.
- **The reference is on this machine.** Transcripts, file history and memory under `~/.claude` hold it verbatim (§2). Shell reads
  are detection-only on Windows; written scripts are content-scanned; tampering with the instrument stops the campaign; candidates
  are checked post hoc for reference-only tokens. A determined model could still evade a text audit — the series does not run
  without an OS-level boundary if shell reads were open (§3.5).
- **The hooks are visible.** The model can read `.claude/hooks/policy-audit.mjs`, `effort-pin.mjs` and their settings entries —
  identical in both arms; touching them or the log is a stop (D12).
- **Blinding is partial**: the codex reading uses fresh ids and scrubbed diffs, but style may reveal the arm; the reference's own
  comments name its review rounds (§2), so the reference's sanity reading is not fully blind; between `sheet` and `read` the id
  key lies outside the tree and its parent, but `-s read-only` lets codex read any path — the key's placement guards the reader's
  working set, not a determined search (the scouts' assessor carries the same residual).
- **The pilot's reader is a model, checked by a model** (D7, the owner's choice): codex reads, the reference must score ≥ 90 % met,
  and an Opus judge spot-checks every *not met* — weaker than a human reading; coverage is labelled *codex-read*, and the series
  grows the reader into a calibrated assessor before any series number is read (§12).
- **Prompt cache**: a later cell may hit an earlier cell's cached prefix; cache tokens are reported apart and order is balanced.
- **Environment differs from daily use**: no MCP, no gate, no plugins, no background tasks, no memory, effort `high` for every
  model. Identical across arms; the absolute numbers are not the operator's daily ones.

## 7. Boundaries with other plans

| Item | Built by | The other plan's part |
|---|---|---|
| Signals 3 (teeth) and 4 (neighbours) in `SolutionSignals` | **this plan** | `PLAN_code_lane.md` consumes them; its sandbox §4.2 stays its own |
| History-free trees, the confined launch, pins, the explicit-effort file | `PLAN_scout_model_calibration.md` (built; effort via PR #93) | this plan widens `ScoutTrees`, `PathAudit`, `ClaudeSubagents`, `ProbeChildEnvironment`, `ScoutPinnedSettings.Explicit` additively; the scouts' behaviour is unchanged and its suite is the proof |
| The scout half of the rule (⌈1.5·N⌉ Sonnet readers) | `PLAN_scout_model_calibration.md` | here scouts appear only as the policy uses them inside a story |
| The verdict on the shared rule | `dew_flow_conventions · research/PLAN_subagent_model_tiers.md` | this plan hands its RESULTS there as a PR, after the series |

Order: none blocks the other; this plan's widenings land behind the scouts' existing tests. Disjoint otherwise.

## 8. Build order — the pilot

Each unit names the stories that build it and what it depends on. Units without a dependency run in parallel. **Docs ride with
the code (D20)**: the first code story to land creates `research/module_policy_ab.md` and its `research/module_tests.md` rows;
every later story updates both in the same task.

1. **Reader** — [E2.S1](PLAN_subagent_policy_ab_E2.md): `MainToolCall` input/fate/result; envelope `duration_api_ms`/`num_turns`;
   the `system/init` lists; every tool call in stream order. RED fixture first: a **hand-recorded, scrubbed** `claude -p
   --output-format stream-json --verbose` coding run on a throwaway tree, with main-session Bash/Edit calls and its audit-hook log,
   recorded once by the operator. *Depends on* E1.S5 (the audit-hook script, for the fixture's log). Its guard-denial case needs
   the rules-probe's arm-B stream: it closes with E3.S9's fixture, not with unit 1.
2. **`prepare`** — [E1.S1, E1.S7–E1.S13](PLAN_subagent_policy_ab_E1.md): `ScoutTreeSpec.KeepProjectSettings` + the effort-key
   refusal; reference facts from the mirror; the statement with model lines struck + scope note (`LeakCheck`); checklist;
   hold-out set and two-axis classification; reference-only tokens; gates at P twice and per hold-out file at S; `stories.json`.
   *Depends on nothing* to build; the pilot's freeze (E1.S13) waits for E4.S3's Fable spec. `NodeTestReport` comes from whichever
   of E1.S7 / E4.S1 lands first; `PolicyDiffFilter.IsDocPath` from whichever of E1.S9 / E4.S1.
3. **The arms** — E1.S2–E1.S6: `Explicit(effort, models)` + `PolicyPinnedSettings` + `prompts/policy-ab/permissions.json` (the
   permissions block it composes, §3.3) + the every-model-resolves-to-`high` test; `ProbeChildEnvironment.ForPolicyArm`;
   `PolicyOverlay` + `PolicyMount`; the audit hook and effort-pin hook scripts; the arm sources and the overlay writer; `arms.json`;
   `prices.json` with the Fable row (E1.S13). *Depends on nothing.*
4. **The audits** — [E2.S2–E2.S8](PLAN_subagent_policy_ab_E2.md): the audit-log reader and `PolicyReason`; `PolicyRuleReached`
   (*its pin values from unit 3*); the widened `PathAudit`; `CommandAudit` with the content scan and the tampering rule (the
   cornerstone, E2.S4); the effort audit; `PolicyAudit` with compliance, the contrast and the effort-pin denials; `PolicyCellVerdict`
   (cost check after filtering never-ran launches); the post-hoc reference check (*its token lists from unit 2*). *Depends on* 1.
5. **The verbs and the pre-flight** — [E3.S1–E3.S9, E3.L1–E3.L3](PLAN_subagent_policy_ab_E3.md): the cell tree and every pin
   re-checked; the launch options and the spend record; `run` with the post-exit step and the verdict (D18); `rules-probe` with the
   effort pin's live check and the `xhigh` break-it; `access-check`; prompts `run.md`, `rules-probe.md`, `access-check.md`; the three
   live runs; their streams as fixtures. *Depends on* 3 and 4.
6. **Mechanical scoring** — [E4.S1–E4.S6](PLAN_subagent_policy_ab_E4.md): the junit, tsc and docs-strip readers; scoring copies and
   the hidden-test port; the hold-out gate (the cornerstone, E4.S3); teeth, neighbours, own suite; verb `score`; the live fairness
   check. *Depends on* 2.
7. **The reading sheet and the codex reader** — E4.S7–E4.S9: `PolicyRequirementReading` (pure: `Prompt`, `Schema`, `Read` with its
   refusals) + `PolicyRequirementReader` on `FindingAssessor.OptionsFor` + `prompts/policy-ab/read-requirements.md`; verbs `sheet`
   (fresh ids, scrubbed, docs stripped, R-items with the bearing ranges, the assessment tree) and `read` (one batch, the reference
   included, the ≥ 90 % sanity gate, the spot-check list for `handcheck.md`). *Depends on* 2. Its live `sheet` in the pilot takes
   the resolved model ids for the scrub from unit 5's `pins.json`.
8. **The pilot** — [E5.S1–E5.S10](PLAN_subagent_policy_ab_E5.md) (§4): the seeded order and the evidence manifest (`order`); the
   prediction and seed; three cells; scoring; the codex reading; `pilot.md` by an Opus judge (D19). *Depends on* 1–7, codex on this
   machine and an attending operator.
9. **Docs, completed** — E5.S9: `research/module_policy_ab.md` completed, the `architecture.md` section, `research/module_tests.md`
   re-checked; **the §7 boundary table written into `PLAN_code_lane.md` and `PLAN_scout_model_calibration.md`** (both sides,
   planning-docs); the owner's go/no-go recorded (E5.S10).

## 9. Test plan — the pilot

All from the test executable, never `dotnet test`. Offline by default; live streams and audit logs become fixtures (scrubbed).
Each stories document maps its stories onto these blocks.

- **Reader** (E2.S1): the hand-recorded coding stream — main Bash/Edit inputs and results read; `duration_api_ms` absent reads
  *not captured*, never 0; the `system/init` lists read. Scouts' reader tests unchanged and green. A guard denial classified —
  this case uses the rules-probe's arm-B stream, so it closes with E3.S9's fixture.
- **Codex reading** (`PolicyRequirementReading`, against fake codex replies — no live codex in the suite): a well-formed reply →
  one verdict per (id × R-item), file:lines kept; REFUSED, each naming its defect: an unknown id, an unknown R-item, a missing
  (id × R-item) row, a duplicate row, a verdict outside met/partial/not met/unresolved, a met/partial/not-met verdict without a
  file:line, a file:line outside that candidate's `candidates/<id>/` files, a reply that is not JSON; the reference at 8/10 met →
  the sanity gate fails, at 9/10 → passes; the id → arm key absent from the batch folder until the answer is read; no arm, model,
  cost or time string in the assessment tree or the prompt (planted strings).
- **Overlay**: A and B over a tree without project hooks (creds at `9f62e4db`: `permissions` only), over a settings fixture WITH
  hooks, and over a tree with no `.claude/settings.json`; the merge adds exactly the audit entries and the effort-pin entry (both
  arms) and one `Agent|Task` guard entry (B); existing keys and hooks kept byte-for-byte; a project already wiring a guard, an
  audit or an effort pin refused; `ensure-user-guard.mjs` never written; a mount other than `.claude/rules/shared` refused; the
  byte hashes stable across two writes; arm sources matching git's blob ids.
- **Hooks** (node scripts, run as hooks are): the audit hook logs the main and a child call with their effort, an
  `InstructionsLoaded` line with the file's byte hash, prints nothing, and writes into its OWN tree from another cwd; the effort-pin
  hook denies an Agent call with `effort: "xhigh"` (reason opening `policy-effort-pin:`), allows one with `effort: "high"` and one
  without `effort`, and ignores every other tool.
- **Effort pin**: the arm settings text is byte-identical for A and B; its top level and `modelSettings.sonnet`, `.opus`, `.fable`
  all say `high`; a resolver following the measured rule (the model's own `modelSettings` entry, else the top level) gives `high`
  for sonnet, opus, fable AND an unnamed model (haiku); `Explicit(effort)` without models still yields PR #93's text; `prepare`
  refuses a tree whose settings name `effortLevel` or `modelSettings`.
- **Effort audit** as a table over audit-log fixtures: all `high` → pass; one child `xhigh` (the 2026-10-14 overlay's record) → stop
  naming the level; the main session `xhigh` → stop; no main record → stop; a child with no tool call → *unobserved*, counted, not
  stopped; a line without a level → counted.
- **Rule reached**: an `InstructionsLoaded` fixture with the arm's byte hash → reached; the other arm's hash, or no record → STOP.
- **Cell verdict**: one test per stop; a guard-denied launch does not fail the cost check (D11); a tampering edit of
  `.claude/settings.json` stops (D12); a wall-killed stream is an outcome.
- **Environment**: B's child lacks `SUBAGENT_MODEL_GUARD` even when the parent sets it `off`; A's has `off`; neither carries
  `CLAUDECODE`, `CLAUDE_CODE_*` (except the forced background switch), a secret-named variable or a `gh` directory on `PATH`.
- **CommandAudit** as a table: `gh pr view`, `git clone https://…`, `curl`, `iwr`, `Invoke-RestMethod`, `npm view`, an absolute
  `D:\rsd\…` path, `~/.claude/projects/…`, `..\..\`, `%USERPROFILE%`, `$env:HOME`, a reference to `.git/policy-audit.ndjson`, an edit
  of `.claude/hooks/policy-audit.mjs`, a compound command with one bad half → stop; `npm run typecheck`, `node --test …`, `git diff`,
  a path inside the tree, an allow-listed toolchain dir → fine. **Content scan**: a written `.mjs` with `fetch("https://…")`, with
  `require("https")`, with `child_process` running `git clone`, with `os.homedir()` + `.claude`, a `package.json` script calling
  `curl` → flag; a test file importing `child_process` for a local `node` call inside the tree → no flag.
- **Post-hoc leak check**: a candidate diff carrying a reference-only comment phrase → strong hit; two reference-only identifiers
  → reported; three → strong; an identifier the frozen statement names → none.
- **Mechanical scoring**: a fixture TypeScript repository with the reference (Success); a valid alternative whose module layout
  differs (its hold-out file → *interface mismatch*, the other hold-out file still runs and counts, Success); the empty candidate
  (NOT Success, D13); a solution whose test has no teeth (signal 3 red); one that breaks a neighbour (signal 4 red); a removed
  neighbour (`missing`); a pre-existing flaky test excluded; per-test results parsed from node:test's junit report; docs hunks
  stripped.
- **Order**: a fixed seed text gives the same order in two processes; over many seed texts B lands in each position about 1/3;
  `order` refuses without a committed prediction; `run` refuses a cell out of the drawn order.
- **Break-it** (each red with the real symptom, then restored; no tripwire disabled): `CommandAudit` without the `gh` verb lets the
  planted `gh` command through; the content scan without `fetch(` lets the planted script through; the tampering rule removed lets a
  `Read` of the audit log through; B's env keeping the parent's `off`; the overlay merge dropping the project's own hooks; **the
  effort audit ignoring child records lets the `xhigh` Sonnet fixture through**; **`Explicit` dropping one model's entry fails the
  every-model test**; **materialising all hold-out files into one copy turns the valid alternative's second file into a failure**;
  **excluding mismatched files from Success with no further condition lets the empty candidate succeed**; **the reading parser
  accepting a verdict without a file:line lets the planted malformed reply through**; and live, once, the `xhigh` overlay in the
  rules-probe (§3.4).

## 10. Decisions taken, limitations

No owner question is open. **Answered 2026-10-14:**

- **O4 — an A/A cell in the pilot: YES (D6).** One more arm-A cell on the pilot story; cap $40 (≈ $20–40 expected), about two more
  attended hours; the pilot ceiling is $207.60. It buys a first look at how far two runs of the SAME arm land apart — whether a
  pilot A–B difference is even readable against A–A — and a check that the capture repeats; not a variance estimate.
- **Who reads the candidates: ANOTHER VENDOR (D7).** Codex, read-only, against the requirement checklist with the reading sheet;
  the reference must score ≥ 90 % met; every *not met* spot-checked by an Opus judge the main session dispatches (a model checking
  a model — the owner's choice, D19); coverage labelled *codex-read* (§3.8). The operator does not read.
- **Canaries in `~/.claude`: APPROVED (D8).**
- **The main session's fifteen decisions after the stories split** (D9–D23, §1) — each with its reason in that table.

**Resolved earlier, recorded so nobody re-asks:**

- **O1 (story 1's spec written with its code)** — resolved by D3's re-selection: coai #754 dropped for exactly that reason (§2);
  the pilot is creds #176, whose E1 text is final at P.
- **O2 (an OS-level sandbox; now also the outbound firewall block)** — decided in §3.5: the pilot's access-check decides; open shell
  reads make an OS-level boundary a requirement of the series, stated in `pilot.md`, and the owner decides then.
- **O3 (story 3 an epic)** — resolved by D3: coai #766 dropped (§2); story 3 is creds `5158ac24`, one Fable-tagged story of 324
  changed lines.
- **O5 (external multi-vendor review of candidates)** — resolved: no review runs inside a cell, and the review gate is not a scorer
  — its findings are not calibrated against a hand reading.

**Limitation (was O6):** a plan-level lane (a plan → epics → stories run) is the only way to measure the rule's Fable split; it is
out of scope here, and RESULTS must say so.

## 11. Definition of Done — the pilot

- [ ] The widenings landed with the scouts suite green and unchanged; every new pure piece has table tests; every break-it went
      red and back; `research/module_policy_ab.md` and the `research/module_tests.md` rows updated with every code story (D20).
- [ ] The pilot story frozen before cell one: tree hash, P verified, statement (model lines struck, D14) + scope note leak-checked
      and hashed, checklist, two-axis hold-out classification with reasons under E4.S3's rules, reference-only tokens, baseline run
      twice, gates per hold-out file at P and S, byte hashes; the judgement inputs committed in `inputs/<story>.json`.
- [ ] `prices.json` with the Fable row committed before any cell of either arm.
- [ ] `rules-probe` passed for both arms: the arm's rule loaded with its byte hash, B's guard denied, A's no-model child on Opus,
      the effort pin denied the `xhigh` launch, every logged effort `high` with hook and transcripts agreeing, `system/init` and the
      resolved ids identical; `pins.json` and `rules-probe.md` committed; the `xhigh` break-it caught.
- [ ] `access-check` ran each read route against every target (the `~/.claude` canaries included); no content returned undetected;
      every status recorded, with the OS-level-boundary consequence stated if shell reads were open; every canary removed by exact
      path.
- [ ] The reference and the valid-alternative variant scored Success through the mechanical gate (`fairness.md`).
- [ ] The prediction and the seed committed BEFORE the draw; `evidence.json` (caps, wall, pins, the drawn order) committed before
      the first cell (D16).
- [ ] All three pilot cells (A1, B, A2) recorded with `total_cost_usd` (or their unknown charge) within the $207.60 ceiling; the
      cost check within 2 % per cell; every cell's effort audit `high` for the main session and every observed child; no
      unreviewed content flag; a stop handled by §3.6, D17 applied.
- [ ] Every Agent prompt classified in `compliance.md` by an Opus judge, the main session verifying (D19).
- [ ] The codex reading parsed for the three candidates and the reference in one batch; the reference read as met on ≥ 90 % of
      R-items; every *not met* spot-checked in `handcheck.md` under its model-checks-model label before the key was opened; codex
      spend reported apart.
- [ ] `pilot.md` (drafted by an Opus judge from the committed records) reports all three cells labelled *instrument check*, the A/A
      spread, the audit per arm, the contrast, coverage as *codex-read*, the prediction against the observation, and every go
      criterion with its evidence.
- [ ] `research/module_policy_ab.md`, `architecture.md`, `research/module_tests.md`, `research/README.md`, `todo/README.md`
      current; the boundary table of §7 written into `PLAN_code_lane.md` and `PLAN_scout_model_calibration.md` too.
- [ ] Cell trees, scoring copies, the dependency template, the assessment tree and the cells' `~/.claude/projects` folders removed by
      exact path once their records are committed; the frozen pilot tree after `pilot.md`'s pull request is merged.
- [ ] Open tail: the owner's go/no-go on the series; if go, §12 built, the series run and `RESULTS_policy_ab.md` (exploratory)
      handed to `dew_flow_conventions · research/PLAN_subagent_model_tiers.md` as a PR there.

## 12. Series-only — built after the owner's go, never for the pilot

Everything below was in the first design and was cut from the pilot by D5. None of it is built before the owner's go on §4's
report; the re-split decides its order.

| Item | What it is | Why the series needs it |
|---|---|---|
| `StreamedClaudeLaunch` | the shared half of `CliScoutRunner` (`CliScoutRunner.cs:139-153`, `:183-220`) extracted: durable mark → launch → line sink → commit; `ScoutArtifacts` keyed by an attempt folder both runners pass | 18 unattended-between-cells launches deserve the scouts' crash-safe path, not a script |
| The spend ledger and `resume` | the pure `SpendLedger` + `ScoutAttemptLifecycle.Recover` over a file adapter; form set by the re-split | a crash mid-series must lose at most the cell in flight |
| `PolicyRunLimits`, `PolicySchedule` | pilot-derived cap/wall (the larger pilot cell × 1.5 / × 2, owner-approved) from `schedule.json`; 9 blocks via `SlotRotation.Rotated` | the series' order balancing and its money bounds |
| `prune` automation | removes only paths the run created, by exact path | 20 cells × ~1.5 GB |
| `tests/FakeCli` knobs | `writeFiles` and an audit-log knob for runner e2e tests | to test the extracted runner offline |
| The resolver layout (story 2) | the `.agents/conventions` mount; the loader-way hash `sha256(normalizedText(file))` computed by importing the tree's own `tools/lib/rule-catalog.mjs` (`:44-47`, `:98`; printed by `tools/lib/rule-cli.mjs:115`); the `BEGIN RULE` check in the main session's tool results, as a covariate | coai at `af7f2856` delivers rules through its loader |
| The `direct` merge style (story 3) | one parent, P is it, keyed by sha — `ScoutMergeStyle.LocalSquash`'s rule (`ScoutCandidates.cs:21`) — plus first-parent membership; later commits recorded as outside the reference | creds `5158ac24` has no PR |
| The full blind assessor | the pilot's codex reader (§3.8) widened: prompts `assess-candidate.md`, `assess-pairwise.md`; verified residual defects under `strict.md`; anchored quality axes; pairwise in both orders | the series reports defects, quality and preference, which the pilot's reader does not read |
| The assessor calibration | planted variants of the reference (a requirement removed; a defect the hold-out catches; one it misses; a valid alternative shape — which also goes through the mechanical gate) run BEFORE the series' first cell; pass ≥ 90 % R-agreement with a hand reading, every planted defect found and below the reference, the alternative not penalised; recorded with `HandCheck` (`HandCheck.cs:12`) / `GateHandChecks` (`GateHandChecks.cs:17`) | the pilot's reader is only sanity-checked (D7); no series number is read before the assessor is calibrated |
| The route × agent access matrix | every route tried by the main session AND one child of each built-in type | the pilot's per-route result decides whether it is needed |
| The OS-level boundary | an OS sandbox (WSL2 + Claude Code's sandbox, or a container) and/or an outbound Windows Firewall block for the cell processes (needs elevation) | required if the pilot found shell reads open (§3.5); the owner decides |

## Epics

Six epics — five for the pilot, one for the series; cornerstone quota Q = ⌈2·6/5⌉ = 3. Stories: one document per epic,
`todo/PLAN_subagent_policy_ab_E<n>.md`, written by Opus when that epic starts and never before (E6's only after the owner's go);
each links this plan, and this section links back. The pilot is an INSTRUMENT check (§4, D5): the split follows what a wrong
instrument would corrupt silently. The question consultant and the independent review (2026-10-14) ranked four areas — (1) the leak
boundary: the reference is on this machine verbatim (§3.5); (2) rule reached (§3.4); (3) the child-model and effort audit: any level
but `high` STOPS the campaign (D4, §3.6); (4) hold-out scoring that must not reward the reference's shape (§3.2, §3.8). (2) and (3)
are measured mechanisms, each with a live break-it and a hard go criterion — Opus-spec stories; (1) and (4) are judgements no later
round undoes — the two cornerstone areas. The third slot is unassigned; a mark outside those areas goes to the operator. **E1–E4
gate the cells (E5); `pilot.md` and the owner's go gate E6.** **Cross-epic order that matters:** E4.S3's Fable spec is written
before E1.S10's classification fields are final and before E1.S13 freezes the pilot; `NodeTestReport` and
`PolicyDiffFilter.IsDocPath` are built once, by whichever of E1.S7 / E1.S9 / E4.S1 lands first.

Cornerstones: 2 of 3 — E2.S4, E4.S3

### E1 — Freeze the story and the arms (pilot · M)

Stories: [PLAN_subagent_policy_ab_E1.md](PLAN_subagent_policy_ab_E1.md) — E1.S1–E1.S13; no cornerstone.

- **Goal.** The pilot story and both arms exist as committed bytes before any model runs — `stories.json`, `arms.json`,
  `prices.json` with the Fable row (§3.1, §3.2, §3.12). Nothing in this epic launches claude.
- **Scope.** §8 unit 2 — `prepare`: `ScoutTreeSpec.KeepProjectSettings` (`src/Bench.Application/Scouts/ScoutPorts.cs:158`,
  the removal at `src/Bench.Infrastructure/Scouts/ScoutTrees.cs:147`) + the effort-key refusal; reference facts from the mirror;
  statement (model lines struck, D14) + scope note through `LeakCheck.Find` (`src/Bench.Domain/Scouts/LeakCheck.cs:30`); R1…Rk;
  the hold-out set with its two-axis classification; reference-only tokens; the baseline twice at P and the harvest per hold-out
  file via `HarvestGates.RunAsync` (`src/Bench.Infrastructure/Git/HarvestGates.cs:50`); the committed `inputs/<story>.json`. §8
  unit 3: `PolicyOverlay.Apply` (pure) + `PolicyMount`; `prompts/policy-ab/policy-audit.mjs` and `effort-pin.mjs` (D15);
  `ProbeChildEnvironment.ForPolicyArm` beside `ForScouts` (`src/Bench.Domain/Probes/ProbeChildEnvironment.cs:58`); the
  `Explicit(effort, models)` overload of `src/Bench.Domain/Scouts/ScoutPinnedSettings.cs:97` + `PolicyPinnedSettings` +
  `prompts/policy-ab/permissions.json`; the arm sources and the overlay writer; byte hashes. The cell tree itself and the restore
  are E3.S1's.
- **Depends on.** Nothing to build; E1.S13 (the freeze) waits for E4.S3's Fable spec.
- **Done when.** §9 *Overlay*, *Hooks*, *Effort pin*, *Environment* green; the break-its "overlay merge dropping the project's own
  hooks", "B's env keeping the parent's `off`", "`Explicit` dropping one model's entry" red with the real symptom, then restored; the
  scouts suite unchanged; `stories.json` per §3.2 with red-at-P / green-at-S per hold-out file; the four records
  (`inputs/creds-176.json`, `stories.json`, `arms.json`, `prices.json`) committed; `ModuleTestsCatalogTests` reads the `policy`
  sub-verbs (D20).

### E2 — Read-back and the audits (pilot · M) — cornerstone area: the leak boundary

Stories: [PLAN_subagent_policy_ab_E2.md](PLAN_subagent_policy_ab_E2.md) — E2.S1–E2.S8; cornerstone **E2.S4** (`CommandAudit`, spec
by Fable).

- **Goal.** Every record a cell leaves can be read, and every stop of §3.6 — a leak, a tampering, a pin moved, the rule not loaded,
  an effort other than `high` — is a pure function over those records, table-tested before anything runs live; unobserved children
  and effort-pin denials are counted (D10).
- **Scope.** §8 unit 1, the reader (`src/Bench.Domain/Probes/ClaudeSubagents.cs:29`): `MainToolCall` input/fate/result
  (`src/Bench.Domain/Probes/SubagentTranscript.cs:77`), the envelope's `duration_api_ms`/`num_turns` as captured-or-not, the
  `system/init` lists; RED fixture: the operator's hand-recorded, scrubbed coding stream + audit log. §8 unit 4: the audit-log reader
  and `PolicyReason`; `PolicyRuleReached`; `PathAudit` with tool set and the main session as parameters
  (`src/Bench.Domain/Scouts/PathAudit.cs:49`, `:54`); `CommandAudit` (pure — network verbs and hosts, path resolution, the content
  scan over `Write`/`Edit`, the tampering rule of D12); `PolicyEffortAudit`; `PolicyAudit` (compliance per arm, the contrast,
  effort-pin denials); `PolicyCellVerdict` (the cost check after filtering never-ran launches, D11); the post-hoc check —
  `LeakCheck.Find` over diffs + phrases (E2.S8).
- **Depends on.** The reader on E1.S5 (the audit-hook script, for its fixture's log); `PolicyRuleReached` on E1's pins, the
  post-hoc check on E1.S9's tokens; the guard-denial case closes with E3.S9's fixture.
- **Done when.** §9 *Reader*, *Effort audit*, *Rule reached*, *Cell verdict*, *CommandAudit* with *Content scan*, *Post-hoc leak
  check* green; the break-its "`CommandAudit` without the `gh` verb", "content scan without `fetch(`", "the tampering rule removed",
  "effort audit ignoring child records lets the `xhigh` Sonnet fixture through" red then restored; the scouts' reader tests
  unchanged.
- **Cornerstone.** The leak boundary — `CommandAudit`, the content scan, the tampering rule and the routes E3's access check
  exercises — is ONE story, E2.S4, spec by Fable: a hole here returns the reference silently and nothing downstream can tell. The
  post-hoc strong-hit rule is fixed mechanically by this plan (§3.5) and is its own Sonnet story, E2.S8.

### E3 — The verbs and the pre-flight (pilot · M · live, $15.60 at cap × 1.2, $13 at cap)

Stories: [PLAN_subagent_policy_ab_E3.md](PLAN_subagent_policy_ab_E3.md) — E3.S1–E3.S9 and the live E3.L1–E3.L3; no cornerstone.

- **Goal.** The launcher exists and the instrument is proven live before any cell: both arms' rule reached by byte hash, B's guard
  denying and A's not, the effort pin denying a model-set level, effort `high` for the main session and every child, every leak
  route with a known status.
- **Scope.** §8 unit 5: the cell tree (fresh copy, overlay, ONE commit, then the restore, clean status, manifest against
  `arms.json`) and every pin re-checked (E3.S1); the launch options, the spend record and the identical envelope (E3.S2);
  `bench policy run` with the post-exit step — candidate, audit log, verdict, `cells.jsonl` lines (E3.S3, E3.S4; D18);
  `rules-probe` with the effort pin's live check and the `xhigh` break-it (§3.4); `access-check` (§3.5, each read route against
  every target, D23); prompts `run.md`, `rules-probe.md`, `access-check.md`. Live, once each: the probe in both arms ($3 cap per
  arm), the `xhigh` break-it ($2), the access-check in an arm-B tree ($5) — canaries in a sibling cell tree, the mirror root, a
  clone stand-in, a junction, `~/.claude/projects/D--rsd-dew-flow-creds-for-devs/`, its `memory/` and `~/.claude/file-history/`
  (D8), each removed afterwards by exact path; `research/policy-ab/pins.json`, `rules-probe.md`, `access-check.md`.
- **Depends on.** E1 (E1.S6's writer, E1.S10's records, E1.S13's committed pilot) and E2 (E2.S1–E2.S7).
- **Done when.** §3.4's criteria hold in BOTH probes, read from records: `InstructionsLoaded` of `subagent-models.md` with the arm's
  byte pin; B's no-model launch denied carrying `subagent-models.md:`, A's child on the Opus id; the effort pin's denial; every
  logged effort `high`, hook and transcripts agreeing; `system/init` and resolved ids identical. The live `xhigh` break-it caught,
  naming the level. Every (route, target) known, no content returned undetected (exit 3 otherwise), the OS-level-boundary row
  written if shell reads were open; probe streams and audit logs committed as scrubbed fixtures (E3.S9, which closes E2.S1's
  guard-denial case); the probes' `~/.claude/projects` slugs removed by exact path; `run` stops a cell that ran under anything but
  its records.

### E4 — The scoring gate, the reading sheet and the codex reader (pilot · M) — cornerstone area: the hold-out gate

Stories: [PLAN_subagent_policy_ab_E4.md](PLAN_subagent_policy_ab_E4.md) — E4.S1–E4.S9; cornerstone **E4.S3** (the hold-out gate,
spec by Fable).

- **Goal.** A candidate is scored per hold-out FILE without rewarding the reference's shape, and never scores Success empty (D13);
  another vendor (codex, read-only) reads every candidate and the reference blind against R1…Rk (D7).
- **Scope.** §8 unit 6: the junit, tsc and docs-strip readers (E4.S1); scoring copies and the hidden-test port
  (`src/Bench.Infrastructure/Git/SolutionSignals.cs:53`, replacing the checkout at `:117`; E4.S2); one copy per hold-out file,
  *interface mismatch* as its own outcome, Success (E4.S3); teeth as the plain revert (D22), neighbours with `missing`, own suite —
  signals 3 and 4 per `todo/PLAN_code_lane.md:165-166` (E4.S4); verb `score` (E4.S5); the live fairness check (E4.S6). §8 unit 7:
  `PolicyRequirementReading` (pure: prompt, schema, `Read` refusing malformed verdicts — stricter than
  `src/Bench.Domain/Scouts/ScoutAssessment.cs:121-123`) (E4.S7); verb `sheet` — `BlindedId.Mint`
  (`src/Bench.Domain/Gate/BlindExport.cs:31`), `ScoutScrubber` with the composed model-name deny-list, docs stripped,
  arm/model/cost/time withheld, R-items with their bearing ranges → `research/policy-ab/reading-sheet.md`; the assessment tree
  (`candidates/<id>/`) (E4.S8); `PolicyRequirementReader` launching codex as `src/Bench.Application/Scouts/ScoutClaimAssessor.cs:29-47`
  does, with `FindingAssessor.OptionsFor` (`src/Bench.Application/Gate/FindingAssessor.cs:85-87`) and the batch folder outside the
  tree (`ScoutClaimAssessor.cs:11`); `prompts/policy-ab/read-requirements.md`; verb `read` — one batch with the reference, the ≥ 90 %
  sanity gate, the spot-check list for `handcheck.md` (E4.S9). No pairwise, no planted-variant calibration.
- **Depends on.** E1 (E1.S10's record types; E1.S13's committed pilot for the live E4.S6 and the dry `read`). The code is
  independent of E2 and E3; the pilot's live `sheet` (E5.S7) takes its model-id scrub list from E3's `pins.json`.
- **Done when.** §9 *Mechanical scoring* green: the reference; the valid alternative → *interface mismatch* on one file while the
  other still runs and counts; the empty candidate NOT Success; no teeth → signal 3 red; a broken neighbour → signal 4 red; flaky
  test excluded; junit parsed; docs stripped. §9 *Codex reading* green over fake codex replies: a well-formed reply parsed; every
  malformed shape refused naming its defect; the sanity gate at 8/10 fails and at 9/10 passes; the key absent until the answer is
  read; no planted arm/model/cost/time string reaches the tree or prompt. The break-its "all hold-out files into one copy turns the
  alternative's second file into a failure", "excluding mismatched files from Success lets the empty candidate succeed" and "the
  parser accepting a verdict without a file:line" red, restored. Live, attended (§4 step 5): the reference Success with zero
  mismatches, the hand-made alternative Success with mismatches only where the interface is not spec-implied (`fairness.md`); one
  dry `read --reference-only` completes and parses.
- **Cornerstone.** The hold-out gate — two-axis classification (behaviour AND interface, before cell one, blind), one-file-per-copy
  materialisation, the never-materialised T2 harness, Success never vacuous — is ONE story, E4.S3, spec by Fable: every number of
  both arms passes through it.

### E5 — The pilot run and its record (pilot · M · attended; ceiling $207.60 — three cells, D6)

Stories: [PLAN_subagent_policy_ab_E5.md](PLAN_subagent_policy_ab_E5.md) — E5.S1–E5.S10; no cornerstone.

- **Goal.** §4 steps 6–11 run once; `pilot.md` carries every go criterion with its evidence, all three cells labelled *instrument
  check — not a comparison*, the A/A spread reported; the owner has what the go/no-go on the series needs.
- **Scope.** The seeded order and the evidence manifest — `bench policy order`, `SeededDraws` from a seed committed first (E5.S1,
  D16); E5.S2 closed as covered by E3.S3/E3.S4 (D18); the prediction and the seed committed before the draw (E5.S3); the evidence
  commit (cap $40, wall 120 min, prompts, pins incl. the effort file and both hooks, the drawn order — E5.S4); three cells (A1, B,
  A2) back to back, attended, one at a time, each committed before the next, every Agent prompt classified into `compliance.md` by
  an Opus judge (E5.S5, D19); `score` + the post-hoc leak check (E5.S6); `sheet` → `read`: the codex reading of the three candidates
  and the reference in ONE batch, the ≥ 90 % reference gate, the spot-check of every *not met* into `handcheck.md` by an Opus judge
  (labelled a model checking a model) before the key is opened; codex spend recorded apart (E5.S7); `pilot.md` drafted by an Opus
  judge from the committed records, the main session verifying (E5.S8, D19); §8 unit 9: the docs completed — `module_policy_ab.md`
  (created by the first code story, D20), `architecture.md`, `module_tests.md` re-checked, both READMEs, the §7 table into
  `todo/PLAN_code_lane.md` and `todo/PLAN_scout_model_calibration.md` (E5.S9); close-out and the owner's decision (E5.S10).
- **Depends on.** E1.S13, E2.S7–E2.S8, E3.S3–E3.S4 and E3.L1–E3.L3, E4.S5–E4.S6 and E4.S8–E4.S9; codex on this machine; an
  attending operator.
- **Done when.** §11's pilot-run lines: the prediction and seed committed before the draw; all three cells with `total_cost_usd`
  (or unknown charge) within $207.60, cost check ≤ 2 %, every effort record `high`, no unreviewed content flag; a stop handled as
  §3.6 (cause fixed with a RED test, D17 applied, counted); the codex reading parsed, the reference ≥ 90 % met (else no-go, named),
  every *not met* spot-checked; `pilot.md` with §4's table row by row — the capture-repeats note, the contrast and the caps-sized
  row included — and coverage labelled *codex-read*; docs synced; cell trees, copies and the assessment tree removed by exact path
  once their records are committed (the frozen pilot tree after the merge); `node .claude/rules/shared/tools/plan-lifecycle.mjs`
  clean.

### E6 — The series (series · L) — GATED: not a line of it before the owner's go on `pilot.md`

- **Goal.** §5 — 3 stories × 2 arms × 3 runs, exploratory; `RESULTS_policy_ab.md` handed to `dew_flow_conventions ·
  research/PLAN_subagent_model_tiers.md` as a PR. Its stories document is written only after the go; its re-split orders §12.
- **Scope.** All of §12, anchors as its table gives them: `StreamedClaudeLaunch` extracted from `CliScoutRunner`; the spend ledger +
  `resume`; `PolicyRunLimits` and `PolicySchedule` (9 blocks by `SlotRotation.Rotated`); `prune`; the `tests/FakeCli` knobs; the
  resolver layout for story 2 (the `.agents/conventions` mount, the loader-way hash, the `BEGIN RULE` covariate); the `direct` merge
  style for story 3; the full blind assessor grown from E4's codex reader, with `assess-candidate.md`/`assess-pairwise.md`; the
  assessor calibration before the series' first cell (planted variants, ≥ 90 % R-agreement, `HandCheck`/`GateHandChecks`); the
  route × agent access matrix; the OS-level boundary if E3 found shell reads open — a security judgement, Fable outside the quota;
  the owner decides; WSL under the 2026-10-07 constraints. Then the series itself: the committed prediction, 9 blocks, 2 spare
  generations, RESULTS.
- **Depends on.** E5 and the owner's go; the calibration and, if required, the OS-level boundary before the first cell.
- **Done when.** §5's reporting lines met, the prediction's sha cited, *exploratory* in the RESULTS title and status line, voids
  re-run within 2 spare generations, and §11's last line: the RESULTS PR open in `dew_flow_conventions`.
