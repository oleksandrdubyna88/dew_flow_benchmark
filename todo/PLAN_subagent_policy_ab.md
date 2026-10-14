# PLAN — Subagent-model policy A/B: does the role table deliver a story as well as "Opus by default", faster, cheaper?

> Status: **plan only, nothing implemented yet, 2026-10-14 — revised after an independent review the same day.** Scope: a new
> **Policy** section of this benchmark (sibling context `Bench.*.Policy`, `bench policy` verbs) that re-runs an already-merged
> story from its pre-implementation spec under two subagent-model policies with the main session held fixed and every model at
> effort `high`. The **pilot** is built small (§8); everything a full series needs and the pilot does not is listed apart in §12
> and built only after the owner's go. Records under `research/policy-ab/`.
>
> Related: [module_scouts.md](../research/module_scouts.md) (the pieces this composes),
> [PLAN_scout_model_calibration.md](PLAN_scout_model_calibration.md) (the scout half of the same rule),
> [PLAN_code_lane.md](PLAN_code_lane.md) (signals 3–4 and the attended-only rule), [module_gate.md](../research/module_gate.md)
> (the blinded assessor), [MEASURED_LESSONS.md](../research/MEASURED_LESSONS.md). The rule under test lives in
> `dew_flow_conventions · common/subagent-models.md`; its plan and results are `dew_flow_conventions ·
> research/PLAN_subagent_model_tiers.md` and `research/RESULTS_subagent_model_tiers.md`.

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
| D6 | **A/A cell: yes** (owner, 2026-10-14). The pilot is THREE cells — A, B and a second A on the same story — ceiling $207.60 (§3.9); the order is drawn from the one committed seed (§3.3). |
| D7 | **The requirement reading is done by ANOTHER VENDOR, not the operator** (owner, 2026-10-14): codex, read-only, reads each candidate and the anonymised reference against R1…Rk (§3.8). No pairwise and no planted-variant calibration in the pilot; the reference must score ≥ 90 % met, and the main session spot-checks every "not met" — labelled a model checking a model. Coverage is reported as *codex-read*. |
| D8 | **Canaries in `~/.claude` approved** (owner, 2026-10-14): three small files — in `~/.claude/projects/D--rsd-dew-flow-creds-for-devs/`, its `memory/` and `~/.claude/file-history/` — removed afterwards by exact path (§3.5). |

## 2. What exists — verified 2026-10-14

| Fact | Where |
|---|---|
| History-free tree: `git archive P` from the bare mirror, submodules at their pinned sha, listed paths and the project's Claude settings removed, fresh `git init` + ONE commit, tree hash re-checked before every cell | `src/Bench.Infrastructure/Scouts/ScoutTrees.cs:35` (summary), `:48` (class), settings removal hard-coded at `:147` via `ScoutGroundTruth.ClaudeProjectSettings` (`src/Bench.Domain/Scouts/ScoutCandidates.cs:175`); grant guard `:132`, `:151` |
| The tree's spec record | `ScoutTreeSpec`, `src/Bench.Application/Scouts/ScoutPorts.cs:158` |
| P verified per merge style (squash → one parent, P = fix^1; rebase → `baseRefOid`; merge → first parent); a **local squash** — one parent, P is it, keyed by its sha — is exactly a direct commit's shape (series only, §12) | `ScoutParentCheck.Refusal`, `src/Bench.Domain/Scouts/ScoutParentCheck.cs:30`, `Linear` `:38`; `ScoutMergeStyle.LocalSquash`, `src/Bench.Domain/Scouts/ScoutCandidates.cs:21` |
| The one confined launch: `-p --model opus --output-format stream-json --verbose --tools … --strict-mcp-config --mcp-config <empty> --forward-subagent-text --max-budget-usd --no-session-persistence --setting-sources "" --settings <pinned> --plugin-dir <pinned>`; never `--restricted` | `ScoutLaunch.Options`, `src/Bench.Application/Scouts/ScoutLaunch.cs:44-56`; `--restricted` rationale `:15-17` |
| `AgentSettingSources.Only(["project"])` already exists | `src/Bench.Application/CliAgent.cs:195`; `AgentAskOptions` at `:100` |
| claude argv: `CliArgv.For` → `Claude(...)`; `--permission-mode plan` only for read-only; `--plugin-dir` last | `src/Bench.Infrastructure/Models/CliAgentRuntime.cs:68`, `:111-130` |
| Cell runner: mark launching → launch with a line sink to `stream.ndjson` → commit stderr/argv/prompt/seal → read → settle. Coupled to `ScoutRun`/`ScoutCell`/`IScoutStore`/`ScoutPrompts`; the pilot does NOT extract from it (D5) | `CliScoutRunner`, `src/Bench.Infrastructure/Scouts/CliScoutRunner.cs:40`; `Ask` `:139-153`; `MeasureAsync` `:183-200`; `CommitAsync` `:203-220` |
| Child environment: allow-listed names only (no `CLAUDECODE`, no `CLAUDE_CODE_*` session variables, no secret-named variable), plus `SUBAGENT_MODEL_GUARD=off` and `CLAUDE_CODE_DISABLE_BACKGROUND_TASKS=1` FORCED | `ProbeChildEnvironment`, `src/Bench.Domain/Probes/ProbeChildEnvironment.cs:25-30` (names), `:58-61` (`ForScouts`) |
| Stream reader grouped by `parent_tool_use_id`: per Agent call `subagent_type`/`model`/prompt, child tools with input, fate and result, `Fallbacks`, the envelope's `modelUsage` and `subagent_stats` | `ClaudeSubagents.Read`, `src/Bench.Domain/Probes/ClaudeSubagents.cs:29` |
| **Gap:** a MAIN-session tool call is `(Id, Name)` only — no input, no fate, no result | `MainToolCall`, `src/Bench.Domain/Probes/SubagentTranscript.cs:77` |
| **Gap:** the path audit reads children only, and only `Read`/`Glob`/`Grep`/`LS` | `PathAudit.AuditedTools` and `Audit`, `src/Bench.Domain/Scouts/PathAudit.cs:49`, `:54-62` |
| The statement leak check: basenames and stems of ground-truth files, identifiers the change added — whole-word, case-insensitive | `LeakCheck.Find(statement, groundTruth, addedIdentifiers)`, `src/Bench.Domain/Scouts/LeakCheck.cs:30` |
| Tokens per model (deduplicated by message id), cost, the 2 % cost check, the CLI's observed Sonnet cache-read rate | `ScoutTokens.Attribute` `src/Bench.Domain/Scouts/ScoutCost.cs:83`; `CliRate` `:26`; `PriceTable` `:30`; `ScoutCost.Check` `:208`; `research/scout-calibration/prices.json` |
| **Gap:** the price table has no Fable row — and Fable is legitimate in BOTH arms here (§1) | `research/scout-calibration/prices.json` (opus, sonnet, haiku only) |
| Spend ledger (reservation = cap × 1.2) and the pure recovery decider | `SpendLedger`, `src/Bench.Domain/Scouts/SpendLedger.cs:17`, `ReservationFor` `:33`; `ScoutAttemptLifecycle.Recover`, `src/Bench.Domain/Scouts/ScoutAttempt.cs:100` |
| **One explicit effort** (PR #93): `ScoutPinnedSettings.EffortLevels`, `FileNameFor(effort)` (a file per level, so the settings pin names the condition), `Explicit(effort)` → `{"effortLevel": level}` with NO `modelSettings`; `ScoutArtifacts.Effort` + `ExplicitEffort()` write that file once and refuse a file saying anything else; the pinned file may carry ONLY `effortLevel` and `modelSettings` | `src/Bench.Domain/Scouts/ScoutPinnedSettings.cs:16` (`AllowedKeys`), `:88`, `:92`, `:97`; `src/Bench.Infrastructure/Scouts/ScoutArtifacts.cs:52`, `:183`; `research/module_scouts.md:531` |
| **Effort is per the CHILD's own model, measured.** A child takes `modelSettings.<its alias>.effortLevel`, else the top-level `effortLevel` — never the parent's. The operator's settings (`effortLevel: xhigh`, only `opus` pinned `high`) ran the scouts' pilot Sonnet children at `xhigh`: 2 Sonnet scouts 92 s / $0.73 at `xhigh` against 41 s / $0.29 at `high`, 1 Opus scout $0.39 at `high`. PR #93 read it from persisted child transcripts' `effort` / `perTurnEffort`. A second route, probed 2026-10-14 in a scratch tree: a project `PreToolUse` hook logging the payload's `effort` with `agent_id` — all three models set `high` logged `{"level":"high"}` for the main Opus, a Sonnet child and a Fable child; a one-run `--settings` overlay `{"modelSettings":{"sonnet":{"effortLevel":"xhigh"}}}` made the Sonnet child log `xhigh` (teeth). The operator's user settings are now `high` for every model | `research/module_scouts.md:531`; `todo/PLAN_scout_model_calibration.md:495`; the hook probe is the main session's, scratch only (the pilot's rules-probe records it as a fixture) |
| **Rule delivery in the native layout, measured ($0.01 spike, scratch, 2026-10-14).** Under `--setting-sources project` with `--strict-mcp-config`, a project `InstructionsLoaded` hook fired with `load_reason: session_start`, `memory_type: Project` for BOTH `CLAUDE.md` and a nested `.claude/rules/shared/common/probe-rule.md`; payload keys `session_id`, `transcript_path`, `cwd`, `scratchpad_dir`, `hook_event_name`, `file_path`, `memory_type`, `load_reason`; the model quoted both files' codewords. `system/init` listed 22 bundled skills, agents `claude`, `claude-code-guide`, `Explore`, `general-purpose`, `Plan`, `statusline-setup`, and built-in plugins only. Backup if the hook ever stops firing: `--debug` output names the memory files loaded | the main session's spike; claude 2.1.289 |
| Global slot rotation (series only) | `SlotRotation.Rotated`, `src/Bench.Domain/Runs/SlotRotation.cs:19` |
| Scoring at base: apply diff in a scratch worktree, build gate, hidden tests by `git checkout <fix> -- <tests>`, test gate; `Passed` includes `RightFiles` | `SolutionSignals.RunAsync`, `src/Bench.Infrastructure/Git/SolutionSignals.cs:53-90`; checkout `:117-118`; `TouchesCause` `:159`; `Passed` `:19` |
| Signals 3 (teeth) and 4 (neighbour suite) specified, not built | `todo/PLAN_code_lane.md:161-166` |
| Harvest gates: red at base with the fix's tests, green at the fix | `HarvestGates.RunAsync`, `src/Bench.Infrastructure/Git/HarvestGates.cs:50` |
| Model-authored code is built **attended only** until the sandbox assertions pass | `todo/PLAN_code_lane.md:114-135` (§4.2) |
| Blinding and a codex read-only reader already exist: fresh ids by `BlindedId.Mint`, already used outside the gate; the codex options (`-s read-only`, the output schema enforced, the final message to a file, MCP off); the scouts' claim assessor — one batch → one codex launch with a history-free tree as its working directory, batch files in a fresh folder OUTSIDE that tree, the answer read and refused when malformed; strict rubric and the hand-check record (series) | `ScoutAssessorLaunch`, `src/Bench.Application/Scouts/ScoutClaimAssessor.cs:9`; batch folder `:11`; `AskAsync` `:29-47`; the reply parser `ScoutAssessment.Read`, `src/Bench.Domain/Scouts/ScoutAssessment.cs:123`, its schema `:70`; `BlindedId.Mint`, `src/Bench.Domain/Gate/BlindExport.cs:31` (used by `src/Bench.Domain/Scouts/ScoutAssessment.cs:107`); `FindingAssessor.OptionsFor`, `src/Bench.Application/Gate/FindingAssessor.cs:85-87`; `prompts/gate-assess/strict.md`; `ScoutClaimAssessor`, `src/Bench.Application/Scouts/ScoutClaimAssessor.cs:25`; `HandCheck`, `src/Bench.Domain/Gate/HandCheck.cs:12`; `GateHandChecks`, `src/Bench.Application/Gate/GateHandChecks.cs:17` |
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
    is legitimate in both arms.
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
- All three repositories are **public** — the reference is on GitHub.

## 3. Design

### 3.1 Arms

| | Arm A — old policy | Arm B — new policy |
|---|---|---|
| Rule text | `common/subagent-models.md` and `common/planning-docs.md` as of conventions `d88e293^` — Opus by default, **Fable for security, cryptography, authn/authz and new architecture** | the same two files as of release `ea3252f` — models by role, plus the per-story process (§1) |
| Guard | none wired; child env `SUBAGENT_MODEL_GUARD=off` (D2) | `settings/hooks/subagent-model.mjs` from `ea3252f` wired as `PreToolUse` `Agent\|Task` in the tree's project settings; the variable ABSENT from the child env, whatever the parent holds |
| Main session | `claude -p --model opus` | identical |
| Effort | `effortLevel: high` + `modelSettings` `sonnet`/`opus`/`fable` each `high` (§3.3) | identical bytes |
| Audit hook | `.claude/hooks/policy-audit.mjs` (observe-only, §3.6) | identical bytes |
| Everything else | the frozen story tree, the run prompt, tools, caps, walls, env | identical |

**The overlay is the only difference, applied the same way to both arms.** Per cell, the frozen story tree is copied to a fresh
path; the arm's two rule files are written over `<mount>/common/`, where `<mount>` is the path `.gitmodules` gives the
`dew_flow_conventions` submodule. The pilot accepts the **native** mount `.claude/rules/shared` only; the resolver mount
`.agents/conventions` is series-only (§12); anything else is refused at `prepare`. A writes its files even where the tree already
holds them (at all three chosen pins the bytes are already identical — §2), so each arm's rule text is deterministic whatever the
story's pin. Both arms get the audit hook `.claude/hooks/policy-audit.mjs` (identical bytes) registered for `PreToolUse`
(matcher `*`) and `InstructionsLoaded`; B additionally gets the guard at `.claude/hooks/subagent-model.mjs` and its ONE
`PreToolUse` `Agent|Task` entry. Both are merged into `.claude/settings.json` (created if absent) by a pure
`PolicyOverlay.Apply(projectSettingsJson, arm)` that keeps the project's own keys and hooks byte-for-byte.
`ensure-user-guard.mjs` is never copied. The overlay is committed into the cell tree's one commit, so `git status` is clean at
launch and the diff never contains it. Each overlay file's **byte** SHA-256 is a pin — it is what native discovery reads and what
the audit hook hashes at `InstructionsLoaded`.

Known asymmetry, recorded not fixed: B's rule links `../research/RESULTS_subagent_model_tiers.md`, which the story's submodule
pin does not hold — a dangling link in B's tree. Overlaying the conventions research folder would add text A never sees.

### 3.2 The story contract — `bench policy prepare`, its own early unit

A story is frozen by `bench policy prepare` before any cell, written to `research/policy-ab/stories.json`. **Story ids are an
INPUT** (D3); the table below holds candidates until `stories.json` is committed. Scoring, the codex reading and any later
calibration all depend on this record; nothing else does (§8).

- **Repository, P, S, merge style** — the pilot accepts `squash` (one parent, P is it; the PR's `baseRefOid` and commit list
  recorded), P verified by `ScoutParentCheck` semantics against the mirror. `direct` (story 3) is series-only (§12).
- **The tree** — `ScoutTrees` with a new `ScoutTreeSpec.KeepProjectSettings` (default false; scouts unchanged): the project's
  `.claude/settings.json` STAYS; a committed `.claude/settings.local.json` is still removed; the grant guard still refuses a tree
  whose own settings grant outside it; **a tree whose committed settings name `effortLevel` or `modelSettings` is refused** (none
  of the three does — §2), so no project layer competes with the pinned effort. Unlike the scouts, the story's plan is NOT
  removed — at P it is the spec. Tree hash recorded.
- **The frozen statement** — the spec section as it stood at P, plus a **scope note** written by the main session from P's text
  only (pilot: "epic E1 only — T1 and T2; E2/E3 are not in scope; E1 DoD's `review_plan`/`review_code`, the consultation and the
  PR are unavailable here — skip them and say so"). Leak-checked by `LeakCheck.Find` against the reference diff's added
  identifiers and hold-out file stems — names the spec itself already carried at P (the pilot's spec names `typedFixtures.test.ts`
  and its three fixtures) are allowed and listed — hashed, committed **before cell one**.
- **The requirement checklist** — R1…Rk derived from the frozen statement only, each with "how to verify"; hashed, committed
  before cell one. It is the codex reader's yardstick in the pilot (§3.8) and the calibrated assessor's in the series. Test-infrastructure
  deliverables (the pilot's T2 harness, its fixtures, the `tsconfig`/ESLint exclusions) are R-items.
- **The hold-out set and its classification (scored without rewarding the reference's shape).** The reference's test files,
  minus test infrastructure. Pilot: the S versions of `src_vs_code/src/test/slotTable.test.ts` and `syncPinRule.test.ts` (both
  exist at P, so only the tests S added or changed are hold-out — the rest are neighbours). `typedFixtures.test.ts` and the
  fixtures under `src_vs_code/src/test/fixtures/typed/` are **T2's deliverable, not a yardstick**: never materialised over the
  candidate's own harness; they are scored only through R-items and teeth (§3.7). Every hold-out test is classified, before cell
  one and blind to every candidate, on **two axes**: is its *behaviour* implied by the frozen statement, and is its *interface* (the
  names, signatures and module paths it calls) implied by it? **Spec-implied** = both; otherwise *beyond-spec*, with the reason.
  The classification goes into `stories.json`.
- **Gates at P** — the commands per the repository's rules, recorded in `stories.json` (pilot, from `src_vs_code`: `npm ci` once,
  `npm run typecheck`, then `npm run compile` and `node --test --test-reporter=spec --test-reporter=junit
  --test-reporter-destination=stdout --test-reporter-destination=<report> "out/test/*.test.js"` — the repository's own `npm test`
  with a per-test report added), run TWICE at P: tests failing or flaky at P are listed and excluded from the neighbour signal;
  `HarvestGates` confirms red-at-P / green-at-S for the spec-implied tests.
- **The reference diff** — S's diff, scored beside the candidates as a REFERENCE, never as an arm (it had a human in the loop and
  review rounds; its time and cost are not comparable).
- **Reference-only tokens** (for the post-hoc leak check, §3.5): identifiers the reference added and comment phrases (≥ 6 words)
  it added, minus anything present in P's tree or in the frozen statement — a candidate cannot reach them except through the
  reference.

| # | Story | Size | Fit | Caveat |
|---|---|---|---|---|
| 1 (pilot) | creds #176 `9369b4d3`, P `9f62e4db` — typed stored secrets E1 (T1 slot table, T2 `StoredSecret` + compile-fail harness) | 404 changed lines (211 test), 16 files, TypeScript | small; spec final at P; native rule layout; `npm test` | one `perf(tests)` fix — speed only; deps restored at `prepare` (§3.5) |
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

- **The launch is a small verb, not an extracted runner** (D5): `bench policy run` re-checks the pins (CLI version, tree hash,
  settings hash, overlay and audit-hook hashes) and refuses on any mismatch, writes `argv.json` (names only), the prompt and an
  attempt marker, launches the exact argv above with the child environment below, tees stdout line by line to `stream.ndjson` and
  stderr to its own file. `--max-budget-usd` enforces the cap; the wall is a process timeout. `CliScoutRunner` is untouched.
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
  network verbs of §3.5). A new `PolicyPinnedSettings` composes the two; **identical bytes for A and B**; its hash is the settings
  pin. *Why every model is named although the top level already says `high`:* PR #93 omitted `modelSettings` because the scouts
  load no other settings layer (`--setting-sources ""`); a cell loads the project layer, and a per-model entry there would beat a
  top-level-only pin — naming every model closes that, and `prepare`'s refusal (§3.2) closes it twice.
- **Child environment** — a new `ProbeChildEnvironment.ForPolicyArm(parent, guardOff)` composing the same allow-list:
  `CLAUDE_CODE_DISABLE_BACKGROUND_TASKS=1` (Agent results stay synchronous; a backgrounded agent stops the cell as in the scouts),
  `GIT_CONFIG_GLOBAL=<empty file>` + `GIT_CONFIG_NOSYSTEM=1` (no credential helper, no url rewrite), `GIT_TERMINAL_PROMPT=0`, fixed
  `GIT_AUTHOR_*`/`GIT_COMMITTER_*` (`policy-ab`, `policy-ab@invalid` — environment, never git config); A adds
  `SUBAGENT_MODEL_GUARD=off`; B structurally excludes that name. `PATH` is filtered to drop a directory holding `gh`.
- **Fresh path per cell** (`<runs root>/<story>/<cell id>`): claude's per-project memory folder is keyed by the cwd, so every cell
  starts with no memory and no transcript of another cell.
- **One cell at a time, attended** (`PLAN_code_lane.md` §4.2: model-authored code is built only while an operator watches);
  never alongside a test suite or another build. **Cell order (three cells, D6)**: one seed, committed before cell one, drives
  one `Random(seed)`; its first draw, `Next(3)`, puts the B cell at position 1, 2 or 3 with equal chance, and the two A cells take
  the other two positions (named A1 and A2 in run order) — so the A/A cell's position is drawn by the same seeded draw as the A/B
  order. The seed, the draw and the resulting order are committed with the evidence.
- **The run prompt** (`prompts/policy-ab/run.md`, hashed, identical across arms): implement the story the frozen statement names,
  from the spec file at its path; **follow the repository's own instructions** for loading its rules and for how work is done
  there; work autonomously — nobody answers questions, record decisions in the final summary; the review gate, the consultants
  and all MCP tools are unavailable here — skip those steps and say so; do not reach the network; do not commit — leave every
  change in the working tree; build and run the tests the repository's rules prescribe before finishing; end with a summary of
  changes, tests run and their result. It names no model, no effort and no policy.
- **After exit**: `git add -A` + `git diff --cached --binary <base>` in the cell tree is the candidate (a commit the session made
  anyway is included); hunks touching overlay paths are dropped and recorded. The audit hook's log lives in `.git/` (§3.6), never
  in the diff.

### 3.4 Rules reached, guard live, effort pinned — proved, not assumed

**Before the pilot — `bench policy rules-probe`** (cap $3 per arm, its own prompt `prompts/policy-ab/rules-probe.md`), in a cell
tree prepared exactly as for a measured cell, with ONE difference: the probe keeps session persistence (its fresh cwd gives it its
own per-project folder, removed afterwards by exact path) so child transcripts exist for the effort cross-check. The probe asks
the session to (1) launch ONE `general-purpose` Agent with NO model and the prompt "reply OK"; (2) launch one Agent each with
`model: sonnet`, `opus`, `fable`, each told to run `git status` once with Bash and then reply OK (one tool call each, so the audit
hook logs its effort).

Pass criteria, read from records — the stream and the audit log — never from the model's prose:

- **Rule reached (hard).** An `InstructionsLoaded` record whose `file_path` is `<mount>/common/subagent-models.md` with
  `load_reason: session_start`, and the hook's SHA-256 of that file's bytes equals the arm's byte pin. The mechanism is measured
  (§2); `--debug` is the backup if the event ever stops firing.
- **Guard.** In **B** the no-model Agent call was DENIED with a result opening `subagent-models.md:` (the guard's `DENY_MARKER`);
  in **A** it ran and its child resolved to the Opus id.
- **Effort.** The main session and every child that ran logged `effort.level = "high"`; the same children's transcripts say
  `high` in `effort` / `perTurnEffort` (PR #93's reading) — the two sources agree once, here, and cells then rely on the hook alone.
- **`system/init`** lists are identical across the two probes.

**The effort break-it, live, once** (cap $2): arm B's probe tree with the pinned settings replaced, for this run only, by the same
text plus `modelSettings.sonnet.effortLevel = "xhigh"` (the 2026-10-14 overlay); only the Sonnet child of step (2) is asked for.
The effort audit must stop it, naming `xhigh`. A probe that does not catch it is a no-go.

Either arm failing any criterion is a no-go (§4). The streams and audit logs become fixtures.

**In every measured cell**: the same `InstructionsLoaded` check (`PolicyRuleReached`, pure over the audit log and the pins) — in
the native layout a cell whose session did not load its arm's rule is VOID, not a covariate; the full `InstructionsLoaded` list
(the session's starting context, identical across arms bar the two overlay files); for B, how many Agent calls the guard denied
and how many digest lines it injected.

### 3.5 Isolation and leak controls

The scouts' access check covered read-only tools. A coding cell executes shell and writes scripts, so the boundary is every route
to the reference:

| Route | Prevention | Detection (read-back) |
|---|---|---|
| GitHub via `gh`/`git`/`curl`/`Invoke-WebRequest` in Bash or PowerShell | no git remote in the tree; no credential helper (§3.3); `gh` off `PATH`; permission deny rules for the network verbs; web tools absent from `--tools`; MCP off | `CommandAudit` (new, pure): every main and child Bash/PowerShell command text checked for network verbs and hosts — any hit STOPS the campaign |
| Sibling clones on `D:\rsd` (originals, mirrors, other cells' trees, the artefact root, the hold-out tests) | the trees root is outside every repository; the grant guard | `PathAudit` widened to main calls and to `Edit`/`Write`; `CommandAudit` resolves absolute paths, `..`, `~`, `%VAR%`/`$env:` in command text — outside the cell tree (toolchain dirs on an allow-list) STOPS |
| **`~/.claude`** — the reference verbatim in `projects/<creds slugs>/*.jsonl`, `file-history`, the `memory/` folders; other projects' memory | `--setting-sources project`; fresh cwd | the path rule above (`~/.claude` is outside the tree) — and canaries there in the access check |
| **A script the model writes** (a `.js`/`.mjs`/`.cjs`/`.ts`/`.ps1`/`.sh`/`.py` file, or a `package.json` script) that opens the network or reads outside the tree | — | **content scan** in `CommandAudit` over every `Write`/`Edit` content: `fetch(`, `http`/`https`/`net`/`dgram` modules, `child_process` with `git clone`/`curl`/`gh`, `Invoke-WebRequest`/`Invoke-RestMethod`, absolute paths outside the tree, `os.homedir()`/`~`/`.claude`. A hit FLAGS the cell — unscored until the operator reads the hunk and clears (recorded) or voids it; a later command that runs the flagged file is a stop |
| The plan's later "as built" notes | the spec is P's text; the scope note is leak-checked | — |
| The audit log (`.git/policy-audit.ndjson`) | inside the cell's `.git`, never in the diff | any tool input naming it STOPS (tampering) |
| Dependencies needing the network mid-run | `prepare` restores once (`npm ci`) in the cell copy BEFORE the wall starts, identically for both arms | a restore attempted mid-run is a network verb → detected |

**Post-hoc, every candidate**: `LeakCheck.Find` over the candidate's diff text with the story's reference-only identifiers (§3.2),
plus a phrase match for the reference-only comment phrases. A hit is reported and reviewed by the operator; a **strong hit** — a
reference-only comment phrase verbatim, or three or more reference-only identifiers that the frozen statement does not motivate —
voids the cell.

**`bench policy access-check`** (live, before cell one, cap $5, ONE run in an arm-B tree — the routes do not depend on the arm,
whose only environment difference is the guard variable, and B's guard is why the probe names every child's model): random
canaries planted in a sibling cell tree, the mirror root, a stand-in for an original clone, a junction escaping the tree, and —
each as one new uniquely named file, removed afterwards by exact path, **approved by the owner (D8)** — in
`~/.claude/projects/D--rsd-dew-flow-creds-for-devs/`, its `memory/` folder and `~/.claude/file-history/`. **One probe per route**, by the main session: read with `Read`; read with
Bash/PowerShell (`cat`/`type`/`Get-Content`); `git -C <outside> log`; fetch `https://github.com` with a shell; write a small script
that reads a canary and run it; plus ONE `opus` child reading one outside canary with a shell, to show children fall under the
same audit. Outcome per route: denied = protected; attempted and detected = detected-only; content returned and NOT detected =
exit 3, the campaign never starts; never attempted = inconclusive. **Expected on native Windows:** file tools denied, shell
reads NOT denied (no OS sandbox).

**Decided: the pilot's access-check result decides the series' boundary.** If shell reads outside the tree — the original clones
and `~/.claude` above all — come back *protected*, the series runs as the pilot did. If they are *detected-only*, the pilot may
still run under fail-stop detection, but **the full series requires an OS-level boundary** — an OS sandbox (WSL2 with Claude
Code's own sandbox, or a container) and/or an outbound Windows Firewall block for the cell processes — and `pilot.md`'s go/no-go
says so in its own row; **the owner decides then**. Neither is built for the pilot: both are machine-level changes (the firewall
rule needs elevation), and WSL carries the 2026-10-07 deletion incident's constraints (no deletion from WSL, script files only).

### 3.6 Read-back and the policy audit — every cell

**The audit hook** (`prompts/policy-ab/policy-audit.mjs`, hashed into the pins, written by the overlay as
`.claude/hooks/policy-audit.mjs` in both arms): on `PreToolUse` (every tool) and `InstructionsLoaded` it appends one JSON line to
`<cell tree>/.git/policy-audit.ndjson` — event, `agent_id` and agent type when present, `tool_name`, the payload's `effort`, and
for `InstructionsLoaded` the `file_path`, `memory_type`, `load_reason` and the SHA-256 of the file's bytes. It prints nothing,
exits 0, never blocks, and takes no environment variable (the path comes from its own cwd). A node start per tool call adds wall
time equally to both arms; it is recorded, not subtracted.

**Why the hook, not the transcripts, is the effort source for cells.** Both are proven (§2). Transcripts need session
persistence, which the cells turn off (no transcript of one cell can feed another, nothing accumulates under `~/.claude`); the hook
works with persistence off, logs the MAIN session as well as every child, per call, as it happens, and its teeth were shown with an
`xhigh` overlay. A child that makes no tool call logs no effort; the pilot counts those as *unobserved* = Agent calls that ran −
distinct `agent_id`s in the log. The stop rule below needs no matching of log lines to Agent calls, so the pilot does none.

Per Agent call (from the stream): `subagent_type`, the `model` argument (or none), `resolvedModel`, the child's `message.model`,
duration, tools by kind (write tools → it wrote code; read-only → it gathered), files and lines it changed, deduplicated tokens,
any fallback. Per cell: Agent calls by (type × resolved model), the effort levels seen (main and children), unobserved children,
the main session's own edits (files, added + removed lines), the delegation share (lines written by children ÷ all lines written),
guard denials (B), the `system/init` lists, and `modelUsage`'s internal Haiku calls listed apart.

- **Compliance is reported, not voided** — it is the policy as followed. **A**: Opus everywhere except its Fable carve-out —
  children on Opus, Fable launches each with their stated reason checked against A's carve-out (security, cryptography, authn/authz,
  new architecture); a Sonnet child in A is a deviation. **B**: every launch named an alias; Sonnet children that wrote code;
  read-only children per model; whether the main session stayed within one file and 50 lines; Fable launches and their reason; the
  process steps visible (a brief/scenarios before a coder, an Opus review). The pilot's main session reads every Agent prompt by
  hand to check the classification.
- **Contrast present** (a §4 row): A launched at least one child, and B ran at least one Sonnet coder (a Sonnet child that used a
  write tool). Otherwise `pilot.md` says plainly what was actually compared.
- **Stops** (cell unscored, campaign stops — `ScoutReason.IsStop` semantics): a leak detected (§3.5); a pin moved (CLI version,
  resolved opus/sonnet/fable ids, settings hash, overlay hashes, audit-hook hash, tree hash, the `system/init` lists); the arm's
  rule not loaded (§3.4); a `model_refusal_fallback` anywhere — re-run once as a new generation, counted; **any logged effort other
  than `high`**, main session or child, or a main session with no effort record at all — the pinned settings did not govern, a
  configuration defect a plain re-run would repeat: the campaign stops, the cause is fixed, the cell re-runs as a new generation,
  counted; a backgrounded agent; a cost-check gap > 2 %.
- **Outcomes, never voids**: the wall reached, the cap exhausted, a failed build, red tests — scored with their cost and time.

### 3.7 Metrics

| Metric | Definition | Role |
|---|---|---|
| **Success** | the candidate typechecks and builds ∧ every spec-implied hold-out test that **compiled against the candidate** is green ∧ no neighbour test red that was green twice at P | **primary** |
| Interface mismatch | per hold-out file: it did not compile against the candidate's API (errors only inside the hold-out file) — its OWN outcome, never Success = false; for a test whose interface is spec-implied, the R-item covering that interface carries the deviation | reported beside Success |
| **Requirement coverage** | R-items met ÷ k, partial = ½ — **codex-read** in the pilot (another vendor, sanity-checked against the reference, "not met" spot-checked — §3.8, D7), by the calibrated assessor in the series | **primary** (pilot: labelled *codex-read*) |
| Teeth | revert the candidate's non-test changes, keep its tests: its new or changed tests must go red — the pilot's T2 harness included (with `type StoredSecret = string`-style reverts it must fail) | secondary |
| Own suite | the candidate's tree as delivered (its own tests, before any hold-out) builds and is green | secondary |
| Beyond-spec hold-out | beyond-spec tests green ÷ beyond-spec tests that compiled | reported |
| Wall time | launch → exit; `result.duration_ms`; per child `totalDurationMs`; `duration_api_ms` and `num_turns` where the envelope carries them (reader widened, else *not captured*) | reported — "speed" |
| Cost | `total_cost_usd`; per model from tokens at the published price; cache read/write apart | reported |
| Policy audit | §3.6, the effort audit and the contrast included | reported (effort: a stop) |
| *Series only (§12)*: verified residual defects, engineering quality 1–5, pairwise preference | the blinded assessor | — |

`TouchesCause` is NOT a quality signal here: a valid alternative implementation may touch other files. `SolutionReport.Passed`
(which includes it) is not used. **Plan, research and docs hunks** (`todo/`, `research/`, `*.md`, `CHANGELOG.md`) are stripped from
every candidate AND the reference before any scoring or reading; a document the arm's own rule requires is never counted as scope
creep.

### 3.8 Scoring instruments

**Mechanical, per hold-out FILE** — `SolutionSignals` widened, not copied: an overload taking a prepared tree (a fresh copy of the
frozen story tree with the candidate applied — a scratch worktree has no submodules) and a hidden-test materialiser port
(`git -C <mirror> show S:<path>`). **Each hold-out file is materialised alone, into its own copy of the candidate tree**, compiled
and run there; a file that does not compile is recorded as *interface mismatch* for that file and touches no other file's result.
The neighbour suite runs once on the candidate tree without any hold-out. Per-test results come from node:test's junit report.
Signal 3 (teeth) and signal 4 (neighbours against the twice-run baseline) are built here as `PLAN_code_lane.md:165-166` specifies;
the reference's test infrastructure is never materialised (§3.2). The attended rule holds.

**The gate's fairness check (pilot):** one hand-made **valid alternative shape** of the reference — same behaviour, different file
and structure where the spec leaves the shape open — goes through the mechanical gate. It must score Success, with interface
mismatches only where the interface is not spec-implied. This is the evidence that the gate does not reward the reference's shape.

**The codex reading (pilot, D7).** The pilot's quality reading against R1…Rk is done by **another vendor** — codex, read-only —
not by the operator and not by a Claude model reading Claude's work (a model marking its own homework passed every answer it got
wrong, `MEASURED_LESSONS.md:199`, §4d). Minimal by design:

- **The reading sheet** (`research/policy-ab/reading-sheet.md`, verb `sheet`): per R-item its "how to verify", and for each of the
  three candidates and the reference — under fresh `BlindedId.Mint` ids, docs hunks stripped, model names scrubbed with
  `ScoutScrubber`, arm, model, cost and time withheld — the files and line ranges that bear on it.
- **The assessment tree**: a history-free copy of the frozen story tree; under `candidates/<id>/` each candidate's changed files in
  full (post-image) and its diff, docs-stripped and scrubbed; the reference is one of them under its own fresh id. The id → arm key
  is written to the batch folder only after the answer is read (the `ScoutClaimAssessor` rule, `ScoutClaimAssessor.cs:24`).
- **The launch** reuses `ScoutClaimAssessor.AskAsync`'s shape (`ScoutClaimAssessor.cs:29-47`): ONE batch → ONE codex launch,
  `ICliAgentRuntime.AskAsync` with `AgentAsk(CliCodex, executable, prompt, assessmentTree, wall, model)` and
  `FindingAssessor.OptionsFor(ModelRuntimeKind.CliCodex, batchDirectory)` (`FindingAssessor.cs:85-87`: `-s read-only`, the output
  schema enforced, the answer file, MCP off); prompt, schema and answer in a fresh batch folder outside the tree
  (`ScoutClaimAssessor.cs:11`). The prompt and schema are new (`prompts/policy-ab/read-requirements.md`) and live in a pure
  `PolicyRequirementReading` beside `ScoutAssessment`: `Prompt`, `Schema`, `Read`. A thin `PolicyRequirementReader` mirrors
  `ScoutClaimAssessor`; no shared base is extracted for the ~10 lines they share (the batch file writes and the one `AskAsync`
  call) — the two read different rows and a base class would couple the scouts to this plan.
- **The answer**: per (candidate id × R-item) exactly one verdict — met / partial / not met / unresolved — with a
  `candidates/<id>/<path>:<line>` for every verdict but *unresolved*. `PolicyRequirementReading.Read` (modelled on
  `ScoutAssessment.Read`, `ScoutAssessment.cs:123`) REFUSES a malformed answer, naming the defect: an unknown id or R-item, a
  missing or duplicate (id × R-item) row, a verdict outside the four, a missing file:line, a file:line outside that candidate's
  files. A refused answer is re-asked once; refused twice, the reading is *not captured*. No pairwise, no defect hunt, no quality
  axes in the pilot.
- **Sanity on the reference (a no-go gate)**: the anonymised reference, read in the SAME batch, must be read as met on ≥ 90 % of
  R-items; below that, the checklist or the reader is broken → no-go, before any candidate's coverage is read.
- **Spot-check**: the main session checks every *not met* verdict — candidates' and reference's — against the diff and records
  agree / disagree with the reason in `research/policy-ab/handcheck.md`, labelled **"a model checking a model — weaker than a
  human reading, the owner's choice (D7)"**. Disagreements are reported; the verdict is not overwritten.
- Coverage is reported as ***codex-read***; codex spend (or its subscription use) is reported apart, never inside an arm or the
  pilot ceiling.

**Series only (§12):** the full blind assessor (defects, quality axes, pairwise in both orders) and its calibration with planted
variants — both grown from the pilot's reader.

### 3.9 Money and time — the pilot

- **Identical envelope**: one cap and one wall for BOTH arms, frozen before cell one (scouts used per-arm caps; here a per-arm cap
  would change the estimand).
- **Pilot numbers**: cap **$40** per cell (`--max-budget-usd`), wall **120 min**. Ceiling **$207.60**, each item at cap × 1.2 for
  overshoot: 3 × $48 (the cells A, B and the A/A cell, D6) + $48 (one stop-and-re-run generation) + rules-probe 2 × $3.60 + the
  effort break-it $2.40 + access-check $6 = $144 + $48 + $7.20 + $2.40 + $6. Basis: the scouts' heaviest cell was $2.29 for under two minutes of reading; a story run is long and multi-turn
  with build/test cycles. A rough single-Opus estimate (≈ 200 turns over a ~150 k cached context ≈ 30 M cache-read tokens ≈ $6,
  + ~200 k output ≈ $4, + ~2 M cache writes ≈ $10) gives ~$20; A's Opus children may double it. The pilot story (404 changed
  lines) is smaller than that estimate assumes. $40 is headroom, not a forecast. A cap or wall reached is an outcome.
- **Spend is recorded, not ledgered**, in the pilot: each run's `total_cost_usd` (or, for a run with no result, its whole cap × 1.2
  as unknown cost) goes into `cells.jsonl`; before every launch the run verb refuses when recorded spend + this launch's cap × 1.2
  would pass the ceiling. The series' ledger form is set by the re-split (§12).
- Codex (the reader, D7) is reported apart — never inside an arm or this ceiling.

### 3.10 Persistence

**Files, no Postgres**: the artefact root holds streams, markers and audit logs (local only); `research/policy-ab/` holds the
committed records. No harvest: harvest lands a Kind `Fix` row in the bank (`src/Bench.Domain/Authoring/CodeTask.cs:15`) and these
are stories, not bug fixes. A DB store, a read API and a console tab are NOT built.

### 3.11 Reuse map — the pilot

| Piece | How |
|---|---|
| `ScoutTrees`, `ScoutTreeSpec` | widened: `KeepProjectSettings` + the effort-key refusal (scouts' default unchanged) |
| `ScoutParentCheck`, `LeakCheck`, `ScoutScrubber`, `ScoutTokens`, `ScoutCost.Check`, `PriceTable`/`CliRate` | reused as they are (`LeakCheck.Find` also over candidate diffs, §3.5) |
| `ScoutPinnedSettings.Explicit`, `EffortLevels`, `FileNameFor`; `ScoutArtifacts.ExplicitEffort()`'s write-once-and-hold rule | `Explicit` widened with a `models` overload (§3.3); the rest reused; `PolicyPinnedSettings` composes it with the permissions block |
| `ClaudeSubagents` / `SubagentTranscript` | widened: `MainToolCall` gains `Input`, `Fate`, `ResultText` (additive); envelope gains `duration_api_ms`, `num_turns` as *captured or not*; the `system/init` lists read |
| `PathAudit` | widened: the audited tool set and the main session become parameters; scouts pass today's values |
| `ProbeChildEnvironment` | widened: `ForPolicyArm` beside `ForScouts`, same allow-list |
| `SolutionSignals` | widened: prepared-tree overload + materialiser port + one-file-per-copy hold-out runs + the junit reader; signals 3–4 added |
| `HarvestGates` | reused for red-at-P / green-at-S at `prepare` |
| `FindingAssessor.OptionsFor`, `ScoutClaimAssessor.AskAsync`'s launch shape, `ScoutAssessorLaunch`, `ScoutAssessment.Read`'s refusal style, `BlindedId.Mint` | reused for the codex reader (§3.8); its prompt, schema and row parser are new (`PolicyRequirementReading`) |
| **New** | `PolicyOverlay`, the audit hook script, `CommandAudit` (with the content scan), the reference-only phrase match, `PolicyRuleReached`, `PolicyAudit` with its effort audit, the story/checklist/hold-out records, the reading sheet and assessment tree, `PolicyRequirementReading` + `PolicyRequirementReader`, `bench policy` verbs `prepare`, `rules-probe`, `access-check`, `run`, `score`, `sheet`, `read` |

`CliScoutRunner` and `CliSubjectRuntime` are not used by the pilot; `CliAgentRuntime.AskAsync` is used only for the codex reader,
as `ScoutClaimAssessor` uses it.

### 3.12 Prompts and records — the pilot

`prompts/policy-ab/`: `run.md`, `rules-probe.md`, `access-check.md`, `read-requirements.md`, `permissions.json`,
`policy-audit.mjs` — each hashed into the pins. `research/policy-ab/`: `arms.json` (conventions shas, the overlay files' byte hashes, the pinned-settings hash),
`stories.json` (§3.2), `prices.json` (a copy of the scouts' with a **Fable** row, URL and date — **no cell of either arm runs before
it exists**, since Fable is legitimate in both), `cells.jsonl` (per cell: arm, story, generation, pins, outcome, every metric, the
audit with effort), `reading-sheet.md` (and the codex verdicts per id × R-item, keyed back to arms after the answer is read),
`handcheck.md` (the main session's spot-check of every *not met*), `access-check.md`, `pilot.md`. All three stories are from public
repositories; diffs and statements may be committed, raw streams and audit logs are not (large, carry machine paths).

### 3.13 Growth budget — the pilot

| Surface | Projected size | Retired by |
|---|---|---|
| The frozen pilot tree | ~60 MB with the conventions submodule | removed by hand (exact path) after `pilot.md` is merged |
| Cell trees (with `node_modules`/`out`) + scoring copies (one per hold-out file per candidate) + the assessment tree | 3 cells × ~1 GB + ~10 copies × ~1 GB + ~0.1 GB ≈ 13 GB worst case | each removed by exact path once its scores are committed |
| Raw streams, markers, audit logs, the codex batch folder | ~7 × ~5 MB, local | kept until promotion, then gzipped; never committed |
| claude per-project folders (one per cwd; the probes' with transcripts) | ~7 small folders under `~/.claude/projects` | removed by the exact slugs of the pilot's paths, nothing else |
| The access-check canaries in `~/.claude` | 3 small files | removed by exact path right after the check |
| Committed records | < 2 MB | kept with RESULTS |

## 4. The pilot — 1 story × 2 arms × 1 run + one A/A cell, and the owner's go/no-go

Order: (1) `prepare` the pilot story (tree, statement, checklist, hold-out classification, reference-only tokens, baseline twice,
gates, byte hashes) → commit; (2) `prices.json` with the Fable row; (3) `rules-probe` both arms, then the effort break-it;
(4) `access-check`; (5) the fairness check — the valid-alternative variant through the mechanical gate; (6) evidence commit — pins,
caps, prompts, the seed and the drawn three-cell order (§3.3); (7) the three cells — A1, B, A2 in the drawn order — back to back,
attended, one at a time; (8) mechanical scoring and the post-hoc leak check; (9) the reading sheet → the codex reading in one batch
(three candidates + the reference) → the reference sanity check → the spot-check into `handcheck.md`; (10) `pilot.md`.

**It checks the instrument, not the policy.** One run per arm cannot estimate variance (MEASURED_LESSONS §1); the pilot reports
all three cells' numbers labelled *instrument check — not a comparison*. The A/A pair (D6) is reported as *how far two runs of the
same arm land apart on this story* — a first look, not a variance estimate.

| Go criterion for the full series | Evidence |
|---|---|
| **Rule reached — hard**: in both probes and every cell, the `InstructionsLoaded` record of the arm's own `subagent-models.md` with its byte pin. No "or explained" | audit logs, `pilot.md` |
| Guard live: B's guard denied the probe's no-model launch, A's did not | §3.4 streams |
| Model assignment matches the arm's rule: A's children on Opus except Fable launches that fall under A's carve-out (each reason checked); B's launches all named an alias | §3.6 audit, hand-read prompts |
| **Contrast present**: A launched ≥ 1 child and B ran ≥ 1 Sonnet coder — else `pilot.md` says plainly what was compared | §3.6 audit |
| **Effort equal and `high` everywhere**: every logged level — main session and every child, all three cells and both probes — is `high`; hook and transcripts agreed in the probe; the `xhigh` break-it was caught; unobserved children counted; `system/init` lists identical | audit logs, `cells.jsonl` |
| No leak route returned content undetected; every route's status known; no unreviewed content flag; no strong post-hoc hit — and, if shell reads of the original clones or `~/.claude` were open, the row "the series needs an OS-level boundary" present (§3.5) | `access-check.md`, per-cell audits |
| Capture complete: diff extracted, cost check within 2 %, time and tokens per model present — and the **capture repeats** across the A/A pair (the same fields present and readable in both A cells) | `cells.jsonl` |
| Scoring discriminates: the hold-out red at P and green at S; the valid-alternative variant scored Success (§3.8); the codex reading parsed for all three candidates and the reference, the reference read as met on ≥ 90 % of R-items, every *not met* spot-checked | `stories.json`, `cells.jsonl`, `reading-sheet.md`, `handcheck.md` |
| **Caps sized**: each cell spent < 0.8 × cap ($32) and ran < 0.8 × wall (96 min); otherwise cap and wall are resized (× 1.5 / × 2 of the larger cell) before the series, and a clipped cell is re-run under them | pilot spend and time |

A no-go names the failing criterion and its fix; the owner decides on a re-pilot.

## 5. The full series — 3 stories × 2 arms × 3 runs (EXPLORATORY, built after the owner's go)

- **Order**: 9 blocks (repeat outermost, then story), each block one A and one B cell; the leading arm by
  `SlotRotation.Rotated([A, B], globalBlockIndex)` — 5 : 4 at nine blocks, so position is reported as a covariate. One cell at a
  time; a story's two arms never separated by more than one block (cache and time-of-day drift fall on both).
- **Pinned and frozen before cell one**: CLI version, resolved ids, the effort file (`high` for every model), settings/overlay/
  audit-hook/prompt hashes, the `system/init` lists, story trees, caps, walls, the dependency restore's lockfile hashes, and a
  written **prediction** (committed sha cited in RESULTS). A pin move stops the series; a moved alias is a new series.
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
  resolves to `high`, and every cell's effort audit stops the campaign on any other level (§3.3, §3.6). Residual: a child that
  makes no tool call is *unobserved*, reported per cell.
- **Shape bias in the hold-out.** The reference's tests encode its interface; a valid alternative can fail to compile against them.
  Each hold-out file runs alone, *interface mismatch* is its own outcome, tests are classified on behaviour AND interface, the
  reference's test infrastructure is never imposed, and the valid-alternative variant proves the gate (§3.2, §3.8).
- **Spec gap.** The pilot's spec is final at P and its one fix commit is speed-only; story 2 has two fix commits whose behaviours
  may be beyond-spec; story 3's follow-ups are outside the reference.
- **Policy reach differs by layout.** In the native layout (stories 1 and 3) both arms' rule texts load at session start
  unconditionally — measured, and a hard pass condition; in the resolver layout (story 2, series) a rule reaches a session only if
  it follows `ENTRY.md` for its task — there it is a covariate, and §5 reports the layout beside each story's numbers.
- **The reference is on this machine.** Transcripts, file history and memory under `~/.claude` hold it verbatim (§2). Shell reads
  are detection-only on Windows; written scripts are content-scanned; candidates are checked post hoc for reference-only tokens. A
  determined model could still evade a text audit — the series does not run without an OS-level boundary if shell reads were open
  (§3.5).
- **The audit hook is visible.** The model can read `.claude/hooks/policy-audit.mjs` and its settings entry — identical in both
  arms; reading or touching its log is a stop.
- **Blinding is partial**: the codex reading uses fresh ids and scrubbed diffs, but style may reveal the arm.
- **The pilot's reader is a model, checked by a model** (D7, the owner's choice): codex reads, the reference must score ≥ 90 % met,
  and Claude spot-checks every *not met* — weaker than a human reading; coverage is labelled *codex-read*, and the series grows the
  reader into a calibrated assessor before any series number is read (§12).
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

Each unit lists what it depends on. Units without a dependency run in parallel.

1. **Reader** — `MainToolCall` input/fate/result; envelope `duration_api_ms`/`num_turns`; the `system/init` lists. RED fixture
   first: a **hand-recorded, scrubbed** `claude -p --output-format stream-json --verbose` coding run on a throwaway tree, with
   main-session Bash/Edit calls and its audit-hook log, recorded once by the operator. *Depends on nothing* — except its one
   guard-denial case, which needs the rules-probe's arm-B stream: that case closes when unit 5 records the fixture (E3).
2. **`prepare`** — `ScoutTreeSpec.KeepProjectSettings` + the effort-key refusal; statement + scope note (`LeakCheck`); checklist;
   hold-out set and two-axis classification; reference-only tokens; gates at P twice and at S; `stories.json`. *Depends on nothing.*
3. **The arms** — `PolicyOverlay` (pure) + the overlay writer + the audit hook script; byte hashes; `ProbeChildEnvironment.
   ForPolicyArm`; `ScoutPinnedSettings.Explicit(effort, models)` + `PolicyPinnedSettings` + `prompts/policy-ab/permissions.json`
   (the permissions block it composes, §3.3) + the every-model-resolves-to-`high` test; `arms.json`; `prices.json` with the Fable
   row. *Depends on 2 (the tree).*
4. **The audits** — `CommandAudit` (pure, with the content scan) and `PathAudit` widened; the audit-log reader; `PolicyAudit` with
   the effort audit and the contrast; `PolicyRuleReached` (*depends on 3's pins*); the post-hoc reference-token check (*depends on
   2's tokens*). *Depends on 1.*
5. **The verbs** — `run` (pin re-check, the exact argv, the tee, the spend check), `rules-probe` with the effort break-it,
   `access-check`; prompts `run.md`, `rules-probe.md`, `access-check.md`. *Depends on 3 and 4.*
6. **Mechanical scoring** — `SolutionSignals` prepared-tree overload, materialiser, one copy per hold-out file, *interface
   mismatch*, the junit reader, signals 3 and 4, the docs-hunk strip; verb `score`; the valid-alternative variant. *Depends on 2.*
7. **The reading sheet and the codex reader** — verb `sheet` (fresh ids, scrubbed, docs stripped, R-items with the bearing
   ranges) and the assessment tree; `PolicyRequirementReading` (pure: `Prompt`, `Schema`, `Read` with its refusals) +
   `PolicyRequirementReader` on `FindingAssessor.OptionsFor` + `prompts/policy-ab/read-requirements.md`; verb `read` (one batch,
   the reference included, the ≥ 90 % sanity gate, the spot-check sheet for `handcheck.md`). *Depends on 2.*
8. **The pilot** (§4) and `pilot.md`. *Depends on 1–7, codex on this machine and an attending operator.*
9. **Docs** — `research/module_policy_ab.md` + the `architecture.md` section + `research/module_tests.md` rows; **the §7 boundary
   table written into `PLAN_code_lane.md` and `PLAN_scout_model_calibration.md`** (both sides, planning-docs); the owner's
   go/no-go recorded.

## 9. Test plan — the pilot

All from the test executable, never `dotnet test`. Offline by default; live streams and audit logs become fixtures (scrubbed).

- **Reader**: the hand-recorded coding stream — main Bash/Edit inputs and results read; `duration_api_ms` absent reads *not
  captured*, never 0; the `system/init` lists read. Scouts' reader tests unchanged and green. A guard denial classified — this
  case uses the rules-probe's arm-B stream, so it closes with E3's fixture, not with unit 1.
- **Codex reading** (`PolicyRequirementReading`, against fake codex replies — no live codex in the suite): a well-formed reply →
  one verdict per (id × R-item), file:lines kept; REFUSED, each naming its defect: an unknown id, an unknown R-item, a missing
  (id × R-item) row, a duplicate row, a verdict outside met/partial/not met/unresolved, a met/partial/not-met verdict without a
  file:line, a file:line outside that candidate's `candidates/<id>/` files, a reply that is not JSON; the reference at 8/10 met →
  the sanity gate fails, at 9/10 → passes; the id → arm key absent from the batch folder until the answer is read; no arm, model,
  cost or time string in the assessment tree or the prompt (planted strings).
- **Overlay**: A and B over a tree without project hooks (creds at `9f62e4db`: `permissions` only), over a settings fixture WITH
  hooks, and over a tree with no `.claude/settings.json`; the merge adds exactly the audit entries (both arms) and one `Agent|Task`
  guard entry (B); existing keys and hooks kept byte-for-byte; `ensure-user-guard.mjs` never written; a mount other than
  `.claude/rules/shared` refused; the byte hashes stable across two writes.
- **Effort pin**: the arm settings text is byte-identical for A and B; its top level and `modelSettings.sonnet`, `.opus`, `.fable`
  all say `high`; a resolver following the measured rule (the model's own `modelSettings` entry, else the top level) gives `high`
  for sonnet, opus, fable AND an unnamed model (haiku); `Explicit(effort)` without models still yields PR #93's text; `prepare`
  refuses a tree whose settings name `effortLevel` or `modelSettings`.
- **Effort audit** as a table over audit-log fixtures: all `high` → pass; one child `xhigh` (the 2026-10-14 overlay's record) → stop
  naming the level; the main session `xhigh` → stop; no main record → stop; a child with no tool call → *unobserved*, counted.
- **Rule reached**: an `InstructionsLoaded` fixture with the arm's byte hash → reached; the other arm's hash, or no record → void.
- **Environment**: B's child lacks `SUBAGENT_MODEL_GUARD` even when the parent sets it `off`; A's has `off`; neither carries
  `CLAUDECODE`, `CLAUDE_CODE_*` (except the forced background switch), a secret-named variable or a `gh` directory on `PATH`.
- **CommandAudit** as a table: `gh pr view`, `git clone https://…`, `curl`, `iwr`, `Invoke-RestMethod`, `npm view`, an absolute
  `D:\rsd\…` path, `~/.claude/projects/…`, `..\..\`, `%USERPROFILE%`, `$env:HOME`, a reference to `.git/policy-audit.ndjson`, a
  compound command with one bad half → stop; `npm run typecheck`, `node --test …`, `git diff`, a path inside the tree, an
  allow-listed toolchain dir → fine. **Content scan**: a written `.mjs` with `fetch("https://…")`, with `require("https")`, with
  `child_process` running `git clone`, with `os.homedir()` + `.claude`, a `package.json` script calling `curl` → flag; a test file
  importing `child_process` for a local `node` call inside the tree → no flag.
- **Post-hoc leak check**: a candidate diff carrying a reference-only comment phrase → strong hit; two reference-only identifiers
  → reported; an identifier the frozen statement names → none.
- **Mechanical scoring**: a fixture TypeScript repository with a correct solution; a valid alternative whose module layout differs
  (its hold-out file → *interface mismatch*, the other hold-out file still runs and counts); a solution whose test has no teeth
  (signal 3 red); one that breaks a neighbour (signal 4 red); a pre-existing flaky test excluded; per-test results parsed from
  node:test's junit report; docs hunks stripped.
- **Break-it** (each red with the real symptom, then restored; no tripwire disabled): `CommandAudit` without the `gh` verb lets the
  planted `gh` command through; the content scan without `fetch(` lets the planted script through; B's env keeping the parent's
  `off`; the overlay merge dropping the project's own hooks; **the effort audit ignoring child records lets the `xhigh` Sonnet
  fixture through**; **`Explicit` dropping one model's entry fails the every-model test**; **materialising all hold-out files into
  one copy turns the valid alternative's second file into a failure**; **the reading parser accepting a verdict without a
  file:line lets the planted malformed reply through**; and live, once, the `xhigh` overlay in the rules-probe (§3.4).

## 10. Decisions taken, limitations

No owner question is open. **Answered 2026-10-14:**

- **O4 — an A/A cell in the pilot: YES (D6).** One more arm-A cell on the pilot story; cap $40 (≈ $20–40 expected), about two more
  attended hours; the pilot ceiling is $207.60. It buys a first look at how far two runs of the SAME arm land apart — whether a
  pilot A–B difference is even readable against A–A — and a check that the capture repeats; not a variance estimate.
- **Who reads the candidates: ANOTHER VENDOR (D7).** Codex, read-only, against the requirement checklist with the reading sheet;
  the reference must score ≥ 90 % met; the main session spot-checks every *not met* (a model checking a model — the owner's
  choice); coverage labelled *codex-read* (§3.8). The operator does not read.
- **Canaries in `~/.claude`: APPROVED (D8).**

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
      red and back.
- [ ] The pilot story frozen before cell one: tree hash, P verified, statement + scope note leak-checked and hashed, checklist,
      two-axis hold-out classification with reasons, reference-only tokens, baseline run twice, gates at P and S, byte hashes.
- [ ] `prices.json` with the Fable row committed before any cell of either arm.
- [ ] `rules-probe` passed for both arms: the arm's rule loaded with its byte hash, B's guard denied, A's no-model child on Opus,
      every logged effort `high` with hook and transcripts agreeing, `system/init` identical; the `xhigh` break-it caught.
- [ ] `access-check` ran one probe per route (the `~/.claude` canaries included); no content returned undetected; every route's
      status recorded, with the OS-level-boundary consequence stated if shell reads were open.
- [ ] The valid-alternative variant scored Success through the mechanical gate.
- [ ] Caps, wall, prompts, pins (the effort file and the audit hook included) and the seed with the drawn three-cell order
      committed before the first cell.
- [ ] All three pilot cells (A1, B, A2) recorded with `total_cost_usd` (or their unknown charge) within the $207.60 ceiling; the
      cost check within 2 % per cell; every cell's effort audit `high` for the main session and every observed child; no unreviewed
      content flag.
- [ ] The codex reading parsed for the three candidates and the reference in one batch; the reference read as met on ≥ 90 % of
      R-items; every *not met* spot-checked in `handcheck.md` under its model-checks-model label; codex spend reported apart.
- [ ] `pilot.md` reports all three cells labelled *instrument check*, the A/A spread, the audit per arm, the contrast, coverage as
      *codex-read*, and every go criterion with its evidence.
- [ ] `research/module_policy_ab.md`, `architecture.md`, `research/module_tests.md`, `research/README.md`, `todo/README.md`
      updated; the boundary table of §7 written into `PLAN_code_lane.md` and `PLAN_scout_model_calibration.md` too.
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
| The full blind assessor | the pilot's codex reader (§3.8) widened: a model-name deny-list beside `ScoutScrubber`; prompts `assess-candidate.md`, `assess-pairwise.md`; verified residual defects under `strict.md`; anchored quality axes; pairwise in both orders | the series reports defects, quality and preference, which the pilot's reader does not read |
| The assessor calibration | planted variants of the reference (a requirement removed; a defect the hold-out catches; one it misses; a valid alternative shape — which also goes through the mechanical gate) run BEFORE the series' first cell; pass ≥ 90 % R-agreement with a hand reading, every planted defect found and below the reference, the alternative not penalised; recorded with `HandCheck` (`HandCheck.cs:12`) / `GateHandChecks` (`GateHandChecks.cs:17`) | the pilot's reader is only sanity-checked (D7); no series number is read before the assessor is calibrated |
| The route × agent access matrix | every route tried by the main session AND one child of each built-in type | the pilot's one-probe-per-route result decides whether it is needed |
| The OS-level boundary | an OS sandbox (WSL2 + Claude Code's sandbox, or a container) and/or an outbound Windows Firewall block for the cell processes (needs elevation) | required if the pilot found shell reads open (§3.5); the owner decides |

## Epics

Six epics — five for the pilot, one for the series; cornerstone quota Q = ⌈2·6/5⌉ = 3. Stories: one document per epic,
`todo/PLAN_subagent_policy_ab_E<n>.md`, written by Opus when that epic starts and never before (E6's only after the owner's go);
each links this plan, and this section links back then. The pilot is an INSTRUMENT check (§4, D5): the split follows what a wrong
instrument would corrupt silently. The question consultant and the independent review (2026-10-14) ranked four areas — (1) the leak
boundary: the reference is on this machine verbatim (§3.5); (2) rule reached (§3.4); (3) the child-model and effort audit: any level
but `high` STOPS the campaign (D4, §3.6); (4) hold-out scoring that must not reward the reference's shape (§3.2, §3.8). (2) and (3)
are measured mechanisms, each with a live break-it and a hard go criterion — Opus-spec stories; (1) and (4) are judgements no later
round undoes — the two cornerstone areas. The third slot is unassigned; a mark outside those areas goes to the operator. **E1–E4
gate the cells (E5); `pilot.md` and the owner's go gate E6.**

Cornerstones: 0 of 3 — E2 (area: the leak boundary), E4 (area: the hold-out gate)

### E1 — Freeze the story and the arms (pilot · M)

- **Goal.** The pilot story and both arms exist as committed bytes before any model runs — `stories.json`, `arms.json`,
  `prices.json` with the Fable row (§3.1, §3.2, §3.12). Nothing in this epic launches claude.
- **Scope.** §8 unit 2 — `prepare`: `ScoutTreeSpec.KeepProjectSettings` (`src/Bench.Application/Scouts/ScoutPorts.cs:158`,
  the removal at `src/Bench.Infrastructure/Scouts/ScoutTrees.cs:147`) + the effort-key refusal; statement + scope note through
  `LeakCheck.Find` (`src/Bench.Domain/Scouts/LeakCheck.cs:30`); R1…Rk; the hold-out set with its two-axis classification;
  reference-only tokens; gates at P twice and at S via `HarvestGates.RunAsync` (`src/Bench.Infrastructure/Git/HarvestGates.cs:50`).
  §8 unit 3: `PolicyOverlay.Apply` (pure) + its writer + `prompts/policy-ab/policy-audit.mjs`; `ProbeChildEnvironment.ForPolicyArm`
  beside `ForScouts` (`src/Bench.Domain/Probes/ProbeChildEnvironment.cs:58`); the `Explicit(effort, models)` overload of
  `src/Bench.Domain/Scouts/ScoutPinnedSettings.cs:97` + `PolicyPinnedSettings` + `prompts/policy-ab/permissions.json`; byte hashes.
- **Depends on.** Nothing; inside the epic the overlay writer follows the tree.
- **Done when.** §9 *Overlay*, *Effort pin*, *Environment* green; the break-its "overlay merge dropping the project's own hooks",
  "B's env keeping the parent's `off`", "`Explicit` dropping one model's entry" red with the real symptom, then restored; the scouts
  suite unchanged; `stories.json` per §3.2 with red-at-P / green-at-S for the spec-implied tests; the three records committed.

### E2 — Read-back and the audits (pilot · M) — cornerstone area: the leak boundary

- **Goal.** Every record a cell leaves can be read, and every stop of §3.6 — a leak, a pin moved, the rule not loaded, an effort
  other than `high`, an unobserved child — is a pure function over those records, table-tested before anything runs live.
- **Scope.** §8 unit 1, the reader (`src/Bench.Domain/Probes/ClaudeSubagents.cs:29`): `MainToolCall` input/fate/result
  (`src/Bench.Domain/Probes/SubagentTranscript.cs:77`), the envelope's `duration_api_ms`/`num_turns` as captured-or-not, the
  `system/init` lists; RED fixture: the operator's hand-recorded, scrubbed coding stream + audit log. §8 unit 4: `CommandAudit`
  (pure — network verbs and hosts, path resolution, the content scan over `Write`/`Edit`); `PathAudit` with tool set and the main
  session as parameters (`src/Bench.Domain/Scouts/PathAudit.cs:49`, `:54`); the audit-log reader; `PolicyAudit` (effort audit,
  unobserved count, contrast, compliance per arm); `PolicyRuleReached`; the post-hoc check — `LeakCheck.Find` over diffs + phrases.
- **Depends on.** The reader on nothing; `PolicyRuleReached` on E1's pins, the post-hoc check on E1's tokens.
- **Done when.** §9 *Reader*, *Effort audit*, *Rule reached*, *CommandAudit* with *Content scan*, *Post-hoc leak check* green; the
  break-its "`CommandAudit` without the `gh` verb", "content scan without `fetch(`", "effort audit ignoring child records lets the
  `xhigh` Sonnet fixture through" red then restored; the scouts' reader tests unchanged. Guard-denial case: closed by E3's fixture.
- **Cornerstone.** The leak boundary — `CommandAudit`, the content scan, the `~/.claude` canary routes E3 exercises and the post-hoc
  strong-hit rule — is ONE story, spec by Fable: a hole here returns the reference silently and nothing downstream can tell.

### E3 — The verbs and the pre-flight (pilot · M · live, ≈ $14 at cap × 1.2)

- **Goal.** The launcher exists and the instrument is proven live before any cell: both arms' rule reached by byte hash, B's guard
  denying and A's not, effort `high` for the main session and every child, every leak route with a known status.
- **Scope.** §8 unit 5: `bench policy run` (pin re-check, the exact argv of §3.3, the tee to `stream.ndjson`, §3.9's ceiling check),
  `rules-probe` with the effort break-it (§3.4), `access-check` (§3.5); prompts `run.md`, `rules-probe.md`, `access-check.md`. Live,
  once each: the probe in both arms ($3), the `xhigh` break-it ($2), the access-check in an arm-B tree ($5) — canaries in a sibling
  cell tree, the mirror root, a clone stand-in, a junction, `~/.claude/projects/D--rsd-dew-flow-creds-for-devs/`, its `memory/` and
  `~/.claude/file-history/`, each removed afterwards by exact path; `research/policy-ab/access-check.md`.
- **Depends on.** E1 and E2.
- **Done when.** §3.4's four criteria hold in BOTH probes, read from records: `InstructionsLoaded` of `subagent-models.md` with the
  arm's byte pin; B's no-model launch denied with `subagent-models.md:`, A's child on the Opus id; every logged effort `high`, hook
  and transcripts agreeing; `system/init` identical. The live `xhigh` break-it caught, naming the level. One probe per route, no
  content returned undetected (exit 3 otherwise), every status recorded, the OS-level-boundary row written if shell reads were open;
  probe streams and audit logs committed as scrubbed fixtures; the probes' `~/.claude/projects` slugs removed by exact path.

### E4 — The scoring gate, the reading sheet and the codex reader (pilot · M) — cornerstone area: the hold-out gate

- **Goal.** A candidate is scored per hold-out FILE without rewarding the reference's shape; another vendor (codex, read-only)
  reads every candidate and the reference blind against R1…Rk (D7).
- **Scope.** §8 unit 6: `SolutionSignals` prepared-tree overload + the materialiser port (`git -C <mirror> show S:<path>`;
  `src/Bench.Infrastructure/Git/SolutionSignals.cs:53`, replacing the checkout at `:117`); one copy per hold-out file; *interface
  mismatch* as its own outcome; the junit reader; signals 3 and 4 per `todo/PLAN_code_lane.md:165-166`; the docs-hunk strip; verb
  `score`; the valid alternative (§3.8). §8 unit 7: verb `sheet` — `BlindedId.Mint` (`src/Bench.Domain/Gate/BlindExport.cs:31`),
  `ScoutScrubber`, docs stripped, arm/model/cost/time withheld, R-items with their bearing ranges →
  `research/policy-ab/reading-sheet.md`; the assessment tree (`candidates/<id>/`); `PolicyRequirementReading` (pure: prompt,
  schema, `Read` refusing malformed verdicts — modelled on `src/Bench.Domain/Scouts/ScoutAssessment.cs:123`) and
  `PolicyRequirementReader` launching codex as `src/Bench.Application/Scouts/ScoutClaimAssessor.cs:29-47` does, with
  `FindingAssessor.OptionsFor` (`src/Bench.Application/Gate/FindingAssessor.cs:85-87`) and the batch folder outside the tree
  (`ScoutClaimAssessor.cs:11`); `prompts/policy-ab/read-requirements.md`; verb `read` — one batch with the reference, the ≥ 90 %
  sanity gate, the spot-check list for `handcheck.md`. No pairwise, no planted-variant calibration.
- **Depends on.** E1 (`stories.json`: classification, R-items, baseline). Independent of E2 and E3.
- **Done when.** §9 *Mechanical scoring* green: the correct solution; the valid alternative → *interface mismatch* on one file while
  the other still runs and counts; no teeth → signal 3 red; a broken neighbour → signal 4 red; flaky test excluded; junit parsed;
  docs stripped. §9 *Codex reading* green over fake codex replies: a well-formed reply parsed; every malformed shape refused naming
  its defect; the sanity gate at 8/10 fails and at 9/10 passes; the key absent until the answer is read; no planted arm/model/cost/
  time string reaches the tree or prompt. The break-its "all hold-out files into one copy turns the alternative's second file into a
  failure" and "the parser accepting a verdict without a file:line" red, restored. Live, attended (§4 step 5): the hand-made
  alternative scores Success, mismatches only where the interface is not spec-implied; one dry `read` over the reference alone
  completes and parses.
- **Cornerstone.** The hold-out gate — two-axis classification (behaviour AND interface, before cell one, blind), one-file-per-copy
  materialisation, the never-materialised T2 harness — is ONE story, spec by Fable: every number of both arms passes through it.

### E5 — The pilot run and its record (pilot · M · attended; ceiling $207.60 — three cells, D6)

- **Goal.** §4 steps 6–10 run once; `pilot.md` carries every go criterion with its evidence, all three cells labelled *instrument
  check — not a comparison*, the A/A spread reported; the owner has what the go/no-go on the series needs.
- **Scope.** The evidence commit (cap $40, wall 120 min, prompts, pins incl. the effort file and audit hook, the seed and the
  three-cell order drawn from it — `Random(seed).Next(3)` places B, the two A cells take the other slots, §3.3); three cells
  (A1, B, A2) back to back, attended, one at a time; `score` + the post-hoc leak check; `sheet` → `read`: the codex reading of the
  three candidates and the reference in ONE batch, the ≥ 90 % reference gate, the main session's spot-check of every *not met* into
  `handcheck.md` (labelled a model checking a model); codex spend recorded apart; `cells.jsonl`, `reading-sheet.md`,
  `handcheck.md`, `pilot.md`. §8 unit 9: the research docs it lists, both READMEs, the §7 table into `todo/PLAN_code_lane.md` and
  `todo/PLAN_scout_model_calibration.md`.
- **Depends on.** E1–E4; codex on this machine; an attending operator.
- **Done when.** §11's pilot-run lines: all three cells with `total_cost_usd` (or unknown charge) within $207.60, cost check
  ≤ 2 %, every effort record `high`, no unreviewed content flag; a stop handled as §3.6 (cause fixed, re-run as a generation,
  counted); the codex reading parsed, the reference ≥ 90 % met (else no-go, named), every *not met* spot-checked; `pilot.md` with
  §4's table row by row — the capture-repeats note, the contrast and the caps-sized row included — and coverage labelled
  *codex-read*; docs synced; §3.13's trees removed by exact path; `node .claude/rules/shared/tools/plan-lifecycle.mjs` clean.

### E6 — The series (series · L) — GATED: not a line of it before the owner's go on `pilot.md`

- **Goal.** §5 — 3 stories × 2 arms × 3 runs, exploratory; `RESULTS_policy_ab.md` handed to `dew_flow_conventions ·
  research/PLAN_subagent_model_tiers.md` as a PR. Its stories document is written only after the go; its re-split orders §12.
- **Scope.** All of §12, anchors as its table gives them: `StreamedClaudeLaunch` extracted from `CliScoutRunner`; the spend ledger +
  `resume`; `PolicyRunLimits` and `PolicySchedule` (9 blocks by `SlotRotation.Rotated`); `prune`; the `tests/FakeCli` knobs; the
  resolver layout for story 2 (the `.agents/conventions` mount, the loader-way hash, the `BEGIN RULE` covariate); the `direct` merge
  style for story 3; the full blind assessor grown from E4's codex reader, with `assess-candidate.md`/`assess-pairwise.md`; the assessor calibration before the
  series' first cell (planted variants, ≥ 90 % R-agreement, `HandCheck`/`GateHandChecks`); the route × agent access matrix; the
  OS-level boundary if E3 found shell reads open — a security judgement, Fable outside the quota; the owner decides; WSL under the
  2026-10-07 constraints. Then the series itself: the committed prediction, 9 blocks, 2 spare generations, RESULTS.
- **Depends on.** E5 and the owner's go; the calibration and, if required, the OS-level boundary before the first cell.
- **Done when.** §5's reporting lines met, the prediction's sha cited, *exploratory* in the RESULTS title and status line, voids
  re-run within 2 spare generations, and §11's last line: the RESULTS PR open in `dew_flow_conventions`.
