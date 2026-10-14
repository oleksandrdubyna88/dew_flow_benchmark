# PLAN — Subagent-model policy A/B: does the role table deliver a story as well as "every subagent on Opus", faster, cheaper?

> Status: **plan only, nothing implemented yet, 2026-10-14.** Scope: a new **Policy** section of this benchmark (sibling
> context `Bench.*.Policy`, `bench policy` verbs) that re-runs an already-merged story from its pre-implementation spec under
> two subagent-model policies with the main session held fixed and every model at effort `high`; widenings of the scouts'
> launcher, tree builder, stream reader, child environment and pinned settings, and of `SolutionSignals` (signals 3 and 4);
> records under `research/policy-ab/`.
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
Everything that differs between the arms is the policy as deployed: its rule text, and for B its guard.

**What a single-story re-run actually measures — said plainly.** The work is one story, so the parts of the new rule that act
inside a story are what move: Sonnet coders and Sonnet scouts (⌈1.5·N⌉) against Opus coders and Opus scouts, under an Opus
orchestrator, plus the main-session edit bound (≤ 1 file, ≤ 50 lines) that pushes B to delegate where A may write everything
itself. **Fable enters only if the run itself treats the work as a cornerstone or a security judgement** — story 3 is chosen
because its own plan tagged it Fable. The rule's plan → epics split (Fable) and epic → stories split (Opus) are **not measured** —
a re-run starts from a story, not a plan (a stated limitation, §10).

**What it does not measure.** The rule's value across a whole plan; repositories outside the family; the coai review gate,
the consultants and every MCP tool (all off in both arms — §3.3); Workflow `agent()` calls (no hook sees them); a later model
behind an alias; any effort level but `high`; the run-to-run variance of either arm in the pilot (one run per arm cannot
estimate it).

### Owner decisions (2026-10-14, recorded so nobody re-asks them)

| # | Decision |
|---|---|
| D1 | **Pilot first**: 1 story × 2 arms × 1 run. The owner then decides on the full series, 3 stories × 2 arms × 3 runs. |
| D2 | Arm A's CHILD process may run with `SUBAGENT_MODEL_GUARD=off` — the fourth measuring harness with that opt-out. Never a working session, never arm B. |
| D3 | The main session chooses the stories. Chosen 2026-10-14 (§3.2): **creds #176** (pilot), **coai #751**, **creds `5158ac24`** (Fable-tagged). Story ids remain an INPUT to `prepare`; §3.2's rows are candidates until `stories.json` is committed. |
| D4 | **Effort `high` for EVERY model**, identical bytes in both arms, pinned and audited per child (owner order 2026-10-14) — an arm comparison at unequal effort measures effort, not the model (§2, §6). |

## 2. What exists — verified 2026-10-14

| Fact | Where |
|---|---|
| History-free tree: `git archive P` from the bare mirror, submodules at their pinned sha, listed paths and the project's Claude settings removed, fresh `git init` + ONE commit, tree hash re-checked before every cell | `src/Bench.Infrastructure/Scouts/ScoutTrees.cs:35` (summary), `:48` (class), settings removal hard-coded at `:147` via `ScoutGroundTruth.ClaudeProjectSettings` (`src/Bench.Domain/Scouts/ScoutCandidates.cs:175`); grant guard `:132`, `:151` |
| The tree's spec record | `ScoutTreeSpec`, `src/Bench.Application/Scouts/ScoutPorts.cs:158` |
| P verified per merge style (squash → one parent, P = fix^1; rebase → `baseRefOid`; merge → first parent); a **local squash** — one parent, P is it, keyed by its sha with no PR number — already exists and is exactly a direct commit's shape | `ScoutParentCheck.Refusal`, `src/Bench.Domain/Scouts/ScoutParentCheck.cs:30`, `Linear` `:38`; `ScoutMergeStyle.LocalSquash`, `src/Bench.Domain/Scouts/ScoutCandidates.cs:21` |
| The one confined launch: `-p --model opus --output-format stream-json --verbose --tools … --strict-mcp-config --mcp-config <empty> --forward-subagent-text --max-budget-usd --no-session-persistence --setting-sources "" --settings <pinned> --plugin-dir <pinned>`; never `--restricted` | `ScoutLaunch.Options`, `src/Bench.Application/Scouts/ScoutLaunch.cs:44-56`; `--restricted` rationale `:15-17` |
| `AgentSettingSources.Only(["project"])` already exists (no widening needed for project-only sources) | `src/Bench.Application/CliAgent.cs:195`; `AgentAskOptions` at `:100` |
| claude argv: `CliArgv.For` → `Claude(...)`; `--permission-mode plan` only for read-only; `--plugin-dir` last | `src/Bench.Infrastructure/Models/CliAgentRuntime.cs:68`, `:111-130` |
| Cell runner: mark launching → launch with a line sink to `stream.ndjson` → commit stderr/argv/prompt/seal (a refused commit THROWS) → read → settle | `CliScoutRunner`, `src/Bench.Infrastructure/Scouts/CliScoutRunner.cs:40`; `Ask` `:139-153`; `MeasureAsync` `:183-200`; `CommitAsync` `:203-220`. It is coupled to `ScoutRun`/`ScoutCell`/`IScoutStore`/`ScoutPrompts` |
| Child environment: allow-listed names only (no `CLAUDECODE`, no `CLAUDE_CODE_*` session variables, no secret-named variable), plus `SUBAGENT_MODEL_GUARD=off` and `CLAUDE_CODE_DISABLE_BACKGROUND_TASKS=1` FORCED | `ProbeChildEnvironment`, `src/Bench.Domain/Probes/ProbeChildEnvironment.cs:25-30` (names), `:58-61` (`ForScouts`) |
| Stream reader grouped by `parent_tool_use_id`: per Agent call `subagent_type`/`model`/prompt, child tools with input, fate and result, `Fallbacks`, the envelope's `modelUsage` and `subagent_stats` | `ClaudeSubagents.Read`, `src/Bench.Domain/Probes/ClaudeSubagents.cs:29` |
| **Gap:** a MAIN-session tool call is `(Id, Name)` only — no input, no fate, no result | `MainToolCall`, `src/Bench.Domain/Probes/SubagentTranscript.cs:77` |
| **Gap:** the path audit reads children only, and only `Read`/`Glob`/`Grep`/`LS` | `PathAudit.AuditedTools` and `Audit`, `src/Bench.Domain/Scouts/PathAudit.cs:49`, `:54-62` |
| Tokens per model (deduplicated by message id), cost, the 2 % cost check, the CLI's observed Sonnet cache-read rate | `ScoutTokens.Attribute` `src/Bench.Domain/Scouts/ScoutCost.cs:83`; `CliRate` `:26`; `PriceTable` `:30`; `ScoutCost.Check` `:208`; `research/scout-calibration/prices.json` |
| **Gap:** the price table has no Fable row | `research/scout-calibration/prices.json` (opus, sonnet, haiku only) |
| Spend ledger (reservation = cap × 1.2) and the pure recovery decider | `SpendLedger`, `src/Bench.Domain/Scouts/SpendLedger.cs:17`, `ReservationFor` `:33`; `ScoutAttemptLifecycle.Recover`, `src/Bench.Domain/Scouts/ScoutAttempt.cs:100` |
| Pinned settings carry ONLY `effortLevel` and `modelSettings` | `ScoutPinnedSettings.AllowedKeys`, `src/Bench.Domain/Scouts/ScoutPinnedSettings.cs:16` |
| **One explicit effort** (PR #93, `origin/main 48b52be`, not yet in this worktree's base): `ScoutPinnedSettings.EffortLevels` (`low`…`max`), `FileNameFor(effort)` (a file per level, so the settings pin names the condition), `Explicit(effort)` → `{"effortLevel": level}` with NO `modelSettings`; `ScoutArtifacts.Effort` + `ExplicitEffort()` write that file once and refuse a file saying anything else; verb flag `--effort` | `origin/main · src/Bench.Domain/Scouts/ScoutPinnedSettings.cs:88`, `:92`, `:97`; `origin/main · src/Bench.Infrastructure/Scouts/ScoutArtifacts.cs:52`, `:183`; method and measurement `origin/main · research/module_scouts.md:531` |
| **Effort is per the CHILD's own model, measured.** A child takes `modelSettings.<its alias>.effortLevel`, else the top-level `effortLevel` — never the parent's. The operator's settings (`effortLevel: xhigh`, only `opus` pinned `high`) ran the scouts' pilot Sonnet children at `xhigh`: 2 Sonnet scouts 92 s / $0.73 at `xhigh` against 41 s / $0.29 at `high`, 1 Opus scout $0.39 at `high`. PR #93 read it from persisted child transcripts' `effort` / `perTurnEffort`. A second route, probed 2026-10-14 in a scratch tree: a project `PreToolUse` hook logging the payload's `effort` with `agent_id` — all three models set `high` logged `{"level":"high"}` for the main Opus, a Sonnet child and a Fable child; a one-run `--settings` overlay `{"modelSettings":{"sonnet":{"effortLevel":"xhigh"}}}` made the Sonnet child log `xhigh` (teeth). The operator's user settings are now `high` for every model | `origin/main · research/module_scouts.md:531`; `origin/main · todo/PLAN_scout_model_calibration.md:495`; the hook probe is the main session's, scratch only (no fixture yet — E1 records one) |
| An **`InstructionsLoaded`** hook event exists in claude 2.1.289 (the installed CLI): input `file_path`, `memory_type` (`User`/`Project`/`Local`/`Managed`), `load_reason` (`session_start`, `nested_traversal`, `path_glob_match`, `include`, `compact`); observability-only, it cannot block | read from the installed binary's hook-input schema, 2026-10-14; **never exercised live yet** — the rules-probe is its first use (§3.4) |
| Global slot rotation (order balancing across a whole matrix) | `SlotRotation.Rotated`, `src/Bench.Domain/Runs/SlotRotation.cs:19` |
| Scoring at base: apply diff in a scratch worktree, build gate, hidden tests by `git checkout <fix> -- <tests>`, test gate; `Passed` includes `RightFiles` | `SolutionSignals.RunAsync`, `src/Bench.Infrastructure/Git/SolutionSignals.cs:53-90`; checkout `:117-118`; `TouchesCause` `:159`; `Passed` `:19` |
| Signals 3 (teeth) and 4 (neighbour suite) specified, not built | `todo/PLAN_code_lane.md:161-166` |
| Harvest gates: red at base with the fix's tests, green at the fix | `HarvestGates.RunAsync`, `src/Bench.Infrastructure/Git/HarvestGates.cs:50` |
| Model-authored code is built **attended only** until the sandbox assertions pass | `todo/PLAN_code_lane.md:114-135` (§4.2) |
| Blinded export (fresh ids, shuffled); codex read-only assessor options; strict rubric | `BlindExport`, `src/Bench.Domain/Gate/BlindExport.cs:133`; `FindingAssessor.OptionsFor`, `src/Bench.Application/Gate/FindingAssessor.cs:85-87`; `prompts/gate-assess/strict.md` |
| A fake CLI that replays a recorded stream per prompt hash (writes no file into its cwd) | `tests/FakeCli/Program.cs:118` |
| The CLI subject runtime: worktree + trust + audit, `--output-format json`, guard off at `:80`; refuses a cost ceiling `:58-61` — not a write-capable stream-json policy launcher | `src/Bench.Infrastructure/Models/CliSubjectRuntime.cs:45` |
| Run-to-run variance rivals effects; three configurations reversed by a wider check | `research/MEASURED_LESSONS.md:17-43` (§1); a model marking its own homework `:199` (§4d) |
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
    "out/test/*.test.js"`); no Docker, no network after `npm ci`.
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
  | creds #176 (pilot) | `.claude/rules/shared` | `8355757` (2026-09-26) | `permissions` only (`acceptEdits`; allow Edit, Write, NotebookEdit, Bash, PowerShell) — **no hooks** | **native**: no `.agents/`, no loader in the session path; Claude Code's own `.claude/rules/**` discovery |
  | creds `5158ac24` | `.claude/rules/shared` | `fba970b7` (2026-09-16) | `permissions` only — no hooks | native |
  | coai #751 | `.agents/conventions` | `bb022429` (2026-10-09) | `permissions` + `SessionStart` `load-instructions.mjs` + `PreToolUse` `Bash\|PowerShell` `build-flags.mjs` | **resolver**: the SessionStart hook, then `ENTRY.md`'s procedure |

  The pilot is an **unmigrated consumer** (the conventions README at `8355757`: "An unmigrated consumer still uses its committed
  `CLAUDE.md` and `.claude/rules/` tree"). `subagent-models.md` carries no `paths:` frontmatter in either version (A: `tasks`
  only; B at `ea3252f`: `tasks` only), so native discovery loads it at session start whatever the task — no task selection, no
  loader output. 63 of the pin's 76 `.md` files carry no `paths:` (≈ 449 KB, the conventions' own `research/` and `todo/`
  included); which of them Claude Code actually loads is **unmeasured** — the `InstructionsLoaded` record lists it per cell.
- **The policy release changed exactly two rule files** — `git diff --stat d88e293^ ea3252f -- common csharp typescript rust
  ENTRY.md AGENTS.md`: `common/subagent-models.md` (+112/−49) and `common/planning-docs.md` (+15, the epics/stories layout the new
  rule points at) — plus `settings/hooks/subagent-model.mjs` (no imports; `DENY_MARKER = 'subagent-models.md:'` at `:35`) and its
  `settings/settings.json` entry. The old catalog at `8355757` and `bb022429` (`tools/lib/rule-catalog.mjs:9` `TASKS`, task check
  `:92`) accepts every task name the new rule's frontmatter lists — relevant only to the resolver layout.
- **The loader's hash is not the file's hash.** `rule-catalog.mjs:44-47` `normalizedText(file) = folded(readFileSync(file,
  "utf8"))` (`folded` from `tools/lib/rule-body.mjs`), `:98` `hash: sha256(text)`; printed as `BEGIN RULE <id> sha256:<hash>` by
  `tools/lib/rule-cli.mjs:115` — the same lines at `8355757`, `fba970b7` and `bb022429`. Native discovery reads the file's bytes
  and prints no hash at all.
- **`settings/hooks/ensure-user-guard.mjs` WRITES `~/.claude`** (it self-installs the user guard). It must never run in a cell.
- **In the resolver layout the loader selects `--task inspect` at session start**, and `common.subagent-models` is not selected for
  `inspect` in either version — there the rule reaches a session only if it follows `ENTRY.md` for its real task. In the native
  layout it always arrives. §3.4 makes both observable.
- All three repositories are **public** — the reference is on GitHub.

## 3. Design

### 3.1 Arms

| | Arm A — old policy | Arm B — new policy |
|---|---|---|
| Rule text | `common/subagent-models.md` and `common/planning-docs.md` as of conventions `d88e293^` | the same two files as of release `ea3252f` |
| Guard | none wired; child env `SUBAGENT_MODEL_GUARD=off` (D2) | `settings/hooks/subagent-model.mjs` from `ea3252f` wired as `PreToolUse` `Agent\|Task` in the tree's project settings; the variable ABSENT from the child env, whatever the parent holds |
| Main session | `claude -p --model opus` | identical |
| Effort | `effortLevel: high` + `modelSettings` `sonnet`/`opus`/`fable` each `high` (§3.3) | identical bytes |
| Audit hook | `.claude/hooks/policy-audit.mjs` (observe-only, §3.6) | identical bytes |
| Everything else | the frozen story tree, the run prompt, tools, caps, walls, env | identical |

**The overlay is the only difference, applied the same way to both arms.** Per cell, the frozen story tree is copied to a fresh
path; the arm's two rule files are written over `<mount>/common/`, where `<mount>` is the path `.gitmodules` gives the
`dew_flow_conventions` submodule — `.claude/rules/shared` (native layout) or `.agents/conventions` (resolver layout); any other
mount, or none, is refused at `prepare`. A writes its files even where the tree already holds them (at all three chosen pins the
bytes are already identical — §2), so each arm's rule text is deterministic whatever the story's pin. Both arms get the audit
hook `.claude/hooks/policy-audit.mjs` (identical bytes) registered for `PreToolUse` (matcher `*`) and `InstructionsLoaded`; B
additionally gets the guard at `.claude/hooks/subagent-model.mjs` and its ONE `PreToolUse` `Agent|Task` entry. Both entries are
merged into `.claude/settings.json` (created if absent) by a pure `PolicyOverlay.Apply(projectSettingsJson, arm)` that keeps the
project's own hooks byte-for-byte. `ensure-user-guard.mjs` is never copied. The overlay is committed into the cell tree's one
commit, so `git status` is clean at launch and the diff never contains it.

**Each overlay rule file is pinned by TWO hashes**: the SHA-256 of its **bytes** (what native discovery reads, and what the audit
hook hashes at `InstructionsLoaded`) and the **loader's** `sha256(normalizedText(file))` — computed at `prepare` by importing the
TREE's own `tools/lib/rule-catalog.mjs` (`normalizedText`, `sha256`) over the overlay file, never by re-implementing `folded` in
C#. The story's layout says which one a probe compares (§3.4); both are in `arms.json`.

Known asymmetry, recorded not fixed: B's rule links `../research/RESULTS_subagent_model_tiers.md`, which the story's submodule
pin does not hold — a dangling link in B's tree. Overlaying the conventions research folder would add text A never sees.

### 3.2 The story contract

A story is frozen by `bench policy prepare` before any cell, written to `research/policy-ab/stories.json`. **Story ids are an
INPUT** (D3); the table below holds candidates until `stories.json` is committed.

- **Repository, P, S, merge style** — `squash` (one parent, P is it; `baseRefOid` and the PR's commit list recorded), `rebase`
  (P = `baseRefOid`), `merge` (first parent), or **`direct`**: a single commit pushed without a PR — one parent, P is it, keyed by
  its sha, which is exactly `ScoutMergeStyle.LocalSquash`'s rule (`ScoutCandidates.cs:21`, checked by `ScoutParentCheck.Linear`
  `:38`), plus a check that S lies on the default branch's first-parent chain; later commits on the same issue are recorded as
  *outside the reference*. P verified by `ScoutParentCheck` semantics against the mirror.
- **The tree** — `ScoutTrees` with a new `ScoutTreeSpec.KeepProjectSettings` (default false; scouts unchanged): the project's
  `.claude/settings.json` STAYS (in the resolver layout it carries the repository's own rule-loading and build-flags hooks; in the
  native layout its permissions); a committed `.claude/settings.local.json` is still removed; the grant guard still refuses a tree
  whose own settings grant outside it; **a tree whose committed settings name `effortLevel` or `modelSettings` is refused** (none
  of the three does — §2), so no project layer competes with the pinned effort. Unlike the scouts, the story's plan is NOT
  removed — at P it is the spec. Tree hash recorded.
- **The frozen statement** — the spec section as it stood at P, plus a **scope note** written by the main session from P's text
  only (story 1: "epic E1 only — T1 and T2; E2/E3 are not in scope; E1 DoD's `review_plan`/`review_code`, the consultation and the
  PR are unavailable here — skip them and say so"). Leak-checked by `LeakCheck.Find` (`src/Bench.Domain/Scouts/LeakCheck.cs:30`)
  against the reference diff's added identifiers and hold-out file stems — names the spec itself already carried at P (story 1's
  spec names `typedFixtures.test.ts` and its three fixtures) are allowed and listed — hashed, committed **before cell one**.
- **The requirement checklist** — R1…Rk derived from the frozen statement only, each with "how to verify"; hashed, committed
  before cell one. This is the assessor's yardstick (§3.8).
- **The hold-out set and its classification** — the reference's test files (story 1: `src_vs_code/src/test/slotTable.test.ts`,
  `typedFixtures.test.ts` with the fixtures under `src_vs_code/src/test/fixtures/typed/`, `syncPinRule.test.ts`; `slotTable` and
  `syncPinRule` exist at P, so only the tests S added or changed are hold-out — the rest are neighbours). Every hold-out test is
  classified, before cell one and blind to every candidate, as **spec-implied** (a reasonable implementation of the frozen
  statement must pass it) or **beyond-spec** (it encodes a decision taken later — a review round, a format the spec never named).
  The classification and the reason per test go into `stories.json`. Primary scoring uses spec-implied tests only; beyond-spec
  results are reported apart.
- **Gates at P** — the build/typecheck and test commands per the repository's rules, recorded in `stories.json` (story 1, from
  `src_vs_code`: `npm run typecheck`, then `npm run compile` and `node --test --test-reporter=spec --test-reporter=junit
  --test-reporter-destination=stdout --test-reporter-destination=<report> "out/test/*.test.js"` — the repository's own `npm test`
  with a per-test report added; a .NET story uses its MTP executable with `--report-trx`, never `dotnet test`), run TWICE at P:
  tests failing or flaky at P are listed and excluded from the neighbour signal; `HarvestGates` confirms red-at-P / green-at-S for
  the spec-implied tests.
- **The reference diff** — S's diff (for `direct`, the one commit only), scored blind beside the candidates as a REFERENCE, never
  as an arm (it had a human in the loop and, for the PRs, review rounds; its time and cost are not comparable).

| # | Story | Size | Fit | Caveat |
|---|---|---|---|---|
| 1 (pilot) | creds #176 `9369b4d3`, P `9f62e4db` — typed stored secrets E1 (T1 slot table, T2 `StoredSecret` + compile-fail harness) | 404 changed lines (211 test), 16 files, TypeScript | small; spec final at P; native rule layout; `npm test` | one `perf(tests)` fix — speed only; deps restored at `prepare` (§3.5) |
| 2 | coai #751 `3ebf208c`, P `af7f2856` — paths per side E2, stories 2.1–2.4 | ≈ 904 changed lines (458 test), 26 files, TypeScript | medium; the resolver layout | two fix commits → classify per test; `test/host/sideSeenScenario.ts` skipped (needs a VS Code host) — `npm test`, never `test:host`; small edits in `panelView`/`helpCoverage`/`configTransfer` tests classified per test |
| 3 | creds `5158ac24` (direct), P `42421d67` — consent policy S2.2, tagged **Fable** by its plan | 324 changed lines (160 test), 7 files, TypeScript | the only one where Fable can plausibly enter; native layout | hold-out `brokerMcpRoutes.test.ts` (+139) with its `brokerWorld.ts` helper (both exist at P); follow-ups `7fb31fea`, `c5368da7` outside the spec |

### 3.3 One cell

```
claude -p --model opus --output-format stream-json --verbose
       --tools Task Bash PowerShell Read Edit Write Glob Grep TodoWrite
       --strict-mcp-config --mcp-config <empty> --forward-subagent-text --max-budget-usd <cap>
       --no-session-persistence --setting-sources project --settings <artefact root>/policy/pinned-settings.effort-high.json
       (run prompt on stdin, cwd = the cell's fresh tree copy, NO --plugin-dir)
```

- **`--setting-sources project`** (not `""`): the tree's own settings and hooks load as in a real session, the operator's user
  settings do not — so the user-level guard, the user permission list, the user-granted directories, `enabledPlugins` and the
  user MCP servers all drop out of both arms. Whether `CLAUDE.md`/`AGENTS.md` and `.claude/rules/**` load under an empty source
  list is unmeasured; `project` removes the question, and §3.4 proves what arrived.
- **No plugins** in either arm: `feature-dev`'s agents carry `model: sonnet` in their frontmatter and would put Sonnet into arm A
  through a call that names no model. Built-in agent types only (`general-purpose`, `Explore`, `Plan`); the audit shows what they
  resolved to.
- **Pinned settings — effort `high` for every model (D4).** PR #93's `ScoutPinnedSettings.Explicit(effort)` is **widened, not
  copied**: an overload `Explicit(effort, models)` adds `modelSettings.<alias>.effortLevel = effort` for each named alias; the
  scouts' one-argument call keeps today's text (its tests are the proof). The arm file is `Explicit("high", ["sonnet", "opus",
  "fable"])` — `{"effortLevel": "high", "modelSettings": {"sonnet": {"effortLevel": "high"}, "opus": {…}, "fable": {…}}}` —
  written once as `pinned-settings.effort-high.json` the way `ScoutArtifacts.ExplicitEffort()` does, plus one fixed `permissions`
  block from `prompts/policy-ab/permissions.json` (`defaultMode: acceptEdits`; allow Edit, Write, Bash, PowerShell; deny the
  network verbs of §3.5). A new `PolicyPinnedSettings` composes the two; **identical bytes for A and B**; its hash is the settings
  pin. *Why every model is named although the top level already says `high`:* PR #93 omitted `modelSettings` because the scouts
  load no other settings layer (`--setting-sources ""`); a cell loads the project layer, and a per-model entry there would beat a
  top-level-only pin — naming every model closes that, and `prepare`'s refusal (§3.2) closes it twice. The operator's own
  settings play no part.
- **Child environment** — a new `ProbeChildEnvironment.ForPolicyArm(parent, guardOff)` composing the same allow-list:
  `CLAUDE_CODE_DISABLE_BACKGROUND_TASKS=1` (Agent results stay synchronous so they can be read; a backgrounded agent stops the
  cell as in the scouts), `GIT_CONFIG_GLOBAL=<empty file>` + `GIT_CONFIG_NOSYSTEM=1` (no credential helper, no url rewrite),
  `GIT_TERMINAL_PROMPT=0`, fixed `GIT_AUTHOR_*`/`GIT_COMMITTER_*` (`policy-ab`, `policy-ab@invalid` — environment, never git
  config); A adds `SUBAGENT_MODEL_GUARD=off`; B structurally excludes that name. `PATH` is filtered to drop a directory holding
  `gh`. The scrubber and `argv.json` (names only) as in the scouts.
- **Fresh path per cell** (`<runs root>/<story>/<cell id>`): claude's per-project memory folder is keyed by the cwd, so every cell
  starts with no memory and no transcript of another cell.
- **One cell at a time, attended** (`PLAN_code_lane.md` §4.2: model-authored code is built only while an operator watches);
  never alongside a test suite or another build.
- **The run prompt** (`prompts/policy-ab/run.md`, hashed, identical across arms): implement the story the frozen statement names,
  from the spec file at its path; first follow the repository's instruction-loading procedure for an `implement` task (`AGENTS.md`
  → `ENTRY.md` where the tree has them; the committed `CLAUDE.md` and `.claude/rules/` otherwise); work autonomously — nobody
  answers questions, record decisions in the final summary; the review gate, the consultants and all MCP tools are unavailable
  here — skip those steps and say so; do not reach the network; do not commit — leave every change in the working tree; build and
  run the tests the repository's rules prescribe before finishing; end with a summary of changes, tests run and their result. It
  names no model, no effort and no policy.
- **After exit**: `git add -A` + `git diff --cached --binary <base>` in the cell tree is the candidate (a commit the session made
  anyway is included); hunks touching overlay paths are dropped and recorded. The audit hook's log lives in `.git/` (§3.6), never
  in the diff.

### 3.4 Rules reached, guard live, effort pinned — proved, not assumed

**Before the pilot — `bench policy rules-probe`** (cap $3 per arm, its own prompt `prompts/policy-ab/rules-probe.md`), in a cell
tree prepared exactly as for a measured cell, with ONE difference: the probe keeps session persistence (its fresh cwd gives it its
own per-project folder, removed by `prune`) so the child transcripts exist for the effort cross-check. The probe asks for:
(1) **resolver layout only** — run the repository's rule loader for an `implement` task and quote the `BEGIN RULE
common.subagent-models sha256:<h>` header line it printed; (2) launch ONE `general-purpose` Agent with NO model and the prompt
"reply OK"; (3) launch one Agent each with `model: sonnet`, `opus`, `fable`, each told to run `git status` once with Bash and then
reply OK (one tool call each, so the audit hook logs its effort).

Pass criteria, read from records — the stream and the audit log — never from the model's prose alone:

- **Rule reached.** *Native layout*: an `InstructionsLoaded` record whose `file_path` is `<mount>/common/subagent-models.md` with
  `load_reason: session_start`, and the hook's SHA-256 of that file's BYTES equals the arm's **byte** pin. *Resolver layout*: the
  loader ran without a catalog error, the quoted `<h>` equals the arm's **loader-way** pin `sha256(normalizedText(file))` (§3.1),
  and the `SessionStart` hook's output appears in the stream.
- **Guard.** In **B** the no-model Agent call was DENIED with a result opening `subagent-models.md:` (the guard's `DENY_MARKER`);
  in **A** it ran and its child resolved to the Opus id.
- **Effort.** The main session and every child that ran logged `effort.level = "high"` in the audit log; the same children's
  transcripts say `high` in `effort` / `perTurnEffort` (PR #93's reading) — the two sources agree once, here, and the cells then
  rely on the hook alone (§3.6).

**The effort break-it, live, once** (cap $2): arm B's probe tree with the pinned settings replaced, for this run only, by the
same text plus `modelSettings.sonnet.effortLevel = "xhigh"` (the 2026-10-14 overlay); only step (3)'s Sonnet child is asked for.
`PolicyAudit` must VOID it naming the Sonnet child and `xhigh`. A probe that does not catch it is a no-go.

Either arm failing any criterion is a no-go (§4). The streams and audit logs become fixtures.

**In every measured cell** (a covariate, never a void): whether the arm's rule reached the main session — native: the
`InstructionsLoaded` record with the byte pin; resolver: the `BEGIN RULE common.subagent-models sha256:` header with the
loader-way pin in the main session's tool results (`PolicyRuleReached`, pure, over the audit log and the widened main-call
results); the full `InstructionsLoaded` list (the session's starting context, identical across arms bar the two overlay files);
for B, how many Agent calls the guard denied and how many digest lines it injected.

### 3.5 Isolation and leak controls

The scouts' access check covered read-only tools. A coding cell executes shell, so the boundary is every route to the reference:

| Route | Prevention | Detection (read-back, fail-stop) |
|---|---|---|
| GitHub via `gh`/`git`/`curl`/`Invoke-WebRequest` in Bash or PowerShell | no git remote in the tree; no credential helper (§3.3); `gh` off `PATH`; permission deny rules for the network verbs; web tools absent from `--tools`; MCP off | `CommandAudit` (new, pure): every main and child Bash/PowerShell command text checked for network verbs and hosts — any hit STOPS the campaign |
| Sibling clones on `D:\rsd` (originals, mirrors, other cells' trees, scout trees, the artefact root, the hold-out tests) | the trees root is outside every repository; the grant guard | `PathAudit` widened to main calls and to `Edit`/`Write`; `CommandAudit` resolves absolute paths, `..`, `~`, `%VAR%`/`$env:` in command text — outside the cell tree (toolchain dirs on an allow-list) STOPS |
| The plan's later "as built" notes | the spec is P's text; the scope note is leak-checked | — |
| The user config (`~/.claude`, other projects' memory) | `--setting-sources project`; fresh cwd | path rule above (`~/.claude` is outside the tree) |
| The audit log (`.git/policy-audit.ndjson`) | inside the cell's `.git`, never in the diff | any tool input naming it STOPS (tampering) |
| Dependencies needing the network mid-run | `prepare` restores once (`npm ci`; `dotnet restore` for a .NET story) in the cell copy BEFORE the wall starts, identically for both arms | a restore attempted mid-run is a network verb → detected |

**`bench policy access-check`** (live, before cell one, ≤ $5 per arm): plant random canaries in a sibling cell tree, the mirror
root, the trees root's parent, a copy standing for an original clone, and a junction escaping the tree; ask the main session AND
one child of each built-in type to read each with `Read`, with `Bash`/`PowerShell` (`type`/`cat`/`Get-Content`), with
`git -C <outside> log`, and to fetch `https://github.com` with each shell. Outcome per (route × agent): denied = protected;
attempted and detected = detected-only; content returned and NOT detected = exit 3, the campaign never starts. A child that never
attempts is *inconclusive*. **Expected on native Windows:** file tools denied, shell reads NOT denied (no OS sandbox).

**Decided (was O2): the pilot's access-check result decides.** If shell reads outside the tree — the original clones above all —
come back *protected*, the series runs as the pilot did. If they are *detected-only*, the pilot may still run under fail-stop
detection, but **the full series requires an OS-level sandbox** (WSL2 with Claude Code's own sandbox, or a container) and
`pilot.md`'s go/no-go says so in its own row; the owner decides then — the sandbox is new infrastructure, and WSL carries the
2026-10-07 deletion incident's constraints (no deletion from WSL, script files only).

### 3.6 Read-back and the policy audit — every cell

**The audit hook** (`prompts/policy-ab/policy-audit.mjs`, hashed into the pins, written by the overlay as
`.claude/hooks/policy-audit.mjs` in both arms): on `PreToolUse` (every tool) and `InstructionsLoaded` it appends one JSON line to
`<cell tree>/.git/policy-audit.ndjson` — event, `agent_id` and agent type when present, `tool_name`, the payload's `effort`, and for
`InstructionsLoaded` the `file_path`, `memory_type`, `load_reason` and the SHA-256 of the file's bytes. It prints nothing, exits 0,
never blocks, and takes no environment variable (the path comes from the hook's own cwd). A node start per tool call adds wall time
equally to both arms; it is recorded, not subtracted.

**Why the hook, not the transcripts, is the effort source for cells.** Both are proven (§2). Transcripts need session persistence,
which the cells turn off (no transcript of one cell can feed another, nothing accumulates under `~/.claude`); the hook works with
persistence off, logs the MAIN session as well as every child, per call, as it happens, and its teeth were shown with an `xhigh`
overlay. Its weakness: a child that makes no tool call logs no effort — it is counted *unobserved*. The rules-probe shows the two
sources agree (§3.4).

Per Agent call: `subagent_type`, the `model` argument (or none), `resolvedModel`, the child's `message.model`, **the child's
effort levels from the audit log** (matched to the stream's Agent calls by agent type and order — by id if the payload carries
the tool-use id), duration, tools by kind (write tools → it wrote code; read-only → it gathered), files and lines it changed,
deduplicated tokens, and any fallback. Per cell: Agent calls by (type × resolved model), the main session's effort levels, the main
session's own edits (files, added + removed lines), the delegation share (lines written by children ÷ all lines written), guard
denials (B), unobserved children, and `modelUsage`'s internal Haiku calls listed apart.

- **Compliance is reported, not voided** — it is the policy as followed. A: share of children resolved to Opus (the old rule's
  claim is 100 %). B: every launch named an alias; Sonnet children that wrote code; read-only children per model; whether the
  main session stayed within one file and 50 lines; Fable launches and their stated reason. The pilot reads every Agent prompt by
  hand to check the classification.
- **Stops** (cell unscored, campaign stops — `ScoutReason.IsStop` semantics): a leak detected (§3.5); a pin moved (CLI version,
  resolved opus/sonnet/fable ids, settings hash, overlay hashes, audit-hook hash, tree hash); a `model_refusal_fallback` anywhere
  (the arm's model changed under it) — re-run once as a new generation, counted; **any logged effort other than `high`**, main
  session or child, or a main session with no effort record at all — the pinned settings did not govern, a configuration defect a
  plain re-run would repeat: the campaign stops, the cause is fixed, the cell re-runs as a new generation, counted; a backgrounded
  agent; a cost-check gap > 2 %.
- **Unobserved children** (no tool call, so no effort record) are reported per cell, never silently counted as `high`.
- **Outcomes, never voids**: the wall reached, the cap exhausted, a failed build, red tests — scored with their cost and time.

### 3.7 Metrics

| Metric | Definition | Role |
|---|---|---|
| **Success** | builds/typechecks ∧ every spec-implied hold-out test green ∧ no neighbour test red that was green twice at P | **primary** |
| **Requirement coverage** | R-items verified met ÷ k, by the blind assessor (§3.8), partial = ½ | **primary** |
| Verified residual defects | blind reviewer findings, each verified under the strict rubric (trigger, mechanism, consequence); count by value high/medium | secondary |
| Engineering quality | anchored 1–5 per axis: conventions fit, scope creep, test adequacy (anchors written into the rubric) | secondary |
| Teeth | revert the candidate's non-test changes, keep its tests: its new or changed tests must go red | secondary |
| Own suite | the candidate's tree as delivered (its own tests, before hold-out materialisation) builds and is green | secondary |
| Beyond-spec hold-out | beyond-spec tests green ÷ beyond-spec tests | reported |
| Pairwise preference | blind A-vs-B, both orders (§3.8) | secondary |
| Wall time | launch → exit; `result.duration_ms`; per child `totalDurationMs`; `duration_api_ms` and `num_turns` where the envelope carries them (reader widened, else *not captured*) | reported — "speed" |
| Cost | `total_cost_usd`; per model from tokens at the published price; cache read/write apart | reported |
| Policy audit | §3.6, the effort audit included | reported (effort: a stop) |

`TouchesCause` is NOT a quality signal here: a valid alternative implementation may touch other files. `SolutionReport.Passed`
(which includes it) is not used.

### 3.8 Scoring instruments

**Mechanical** — `SolutionSignals` widened, not copied: an overload taking a prepared tree (a fresh copy of the frozen story tree,
submodules included — a scratch worktree has none) and a hidden-test materialiser port (`git -C <mirror> show S:<path>`, fixture
folders included), reusing `SignalsAsync`; the build and test commands from `stories.json`; per-test results read from the
runner's report — node:test's junit reporter for the TypeScript stories, the MTP runner's `--report-trx` for a .NET one; signal 3
(teeth) and signal 4 (neighbours against the twice-run baseline) built here as `PLAN_code_lane.md:165-166` specifies. The attended
rule holds.

**Blind assessment** — codex, read-only, `FindingAssessor.OptionsFor` options, run AFTER all cells of a story:
1. *Assessment repository* per story: the frozen tree as base, each candidate (both arms' and the reference) as one commit under a
   fresh random id (the `BlindExport` fresh-id-and-shuffle rule, extracted into a shared helper rather than copied); model names
   (`opus`, `sonnet`, `fable`, `claude-*`) scrubbed from diff text with `ScoutScrubber` and a deny-list, the count recorded; the
   sessions' summaries, arm, cost, time and stream WITHHELD.
2. *Independent scoring* (`prompts/policy-ab/assess-candidate.md`): per candidate, R1…Rk met/partial/not met/unresolved with
   file:line; residual defects; the three quality axes against anchors. Findings are then verified under `strict.md`'s rules.
3. *Pairwise* (`prompts/policy-ab/assess-pairwise.md`): same story, A vs B, randomised order, then the swapped order; verdicts
   first / second / tie / insufficient evidence. A win counts only when both orders agree; disagreement is an order-sensitive tie.

**Calibration before any number is read** (the session does it, item by item into `research/policy-ab/handcheck.md`), in two
steps. **Step 1, BEFORE cell one** — the five candidate-free items: the reference and **four planted variants** of it — a
requirement removed; a defect the hold-out tests catch; a defect they miss; a valid alternative shape (same behaviour, different
files/structure) — each through the full assessor. **Step 2, after the cells** — the pilot's two candidates join, hand-read the
same way. Pass: ≥ 90 % agreement with the hand reading over all R-verdicts; every planted defect found and ranked below the
reference; the valid alternative not penalised for its shape. The scouts' 20/20 assessor sample held one "wrong" item — weak
evidence for a new rubric, so it is not borrowed. A failed step 1 revises the rubric and re-runs step 1 before any cell launches;
a failed step 2 revises the rubric and re-runs the calibration, never the cells.

**No external multi-vendor review of the candidates** (decided 2026-10-14, was O5): the coai consultant and gate run nowhere
inside a cell, and the review gate is not a scorer — its findings are not calibrated against a hand reading, so they would add an
uncalibrated instrument beside a calibrated one.

### 3.9 Money, time and interruption

- **Identical envelope**: one cap and one wall for BOTH arms of a story, frozen before cell one (scouts used per-arm caps; here a
  per-arm cap would change the estimand).
- **Pilot numbers**: cap **$40** per cell (`--max-budget-usd`), reservation $48, wall **120 min**; pilot ceiling **$165.60** =
  2 × $48 (the cells) + $48 (one stop-and-re-run generation) + rules-probe 2 × $3.60 + the effort break-it $2.40 + access-check
  2 × $6 (each reservation = cap × 1.2). Basis: the scouts' heaviest cell was $2.29 for under two minutes of reading; a story run
  is long and multi-turn with build/test cycles. A rough single-Opus estimate (≈ 200 turns over a ~150 k cached context ≈ 30 M
  cache-read tokens ≈ $6, + ~200 k output ≈ $4, + ~2 M cache writes ≈ $10) gives ~$20; A's Opus children may double it. The pilot
  story (404 changed lines) is smaller than that estimate assumes. $40 is headroom, not a forecast — the pilot measures it. A cap
  or wall reached is an outcome.
- `PolicyRunLimits` (pure, like `ScoutRunLimits`): a pilot refuses a cap above $60, a wall above 180 min, a ceiling above $200; the
  series takes cap and wall from `schedule.json` only = the larger arm's pilot cell × 1.5 and × 2, the owner approving the numbers.
- **Ledger and recovery without a database**: an attempt marker file written (and flushed) BEFORE the launch; the stream written
  line by line; settled from the stream's `total_cost_usd`, or charged its whole reservation as unknown cost — the pure
  `SpendLedger` and `ScoutAttemptLifecycle.Recover` over a file adapter. A crash loses at most the cell in flight; `bench policy
  resume` settles and continues.
- Assessor (codex) spend is reported separately, never inside an arm.

### 3.10 Persistence — the smallest honest option

**Files, no Postgres** for the pilot AND the series: 2 + 18 serial, attended cells need resume, a durable launch mark and a
ledger, not claims across hosts. The artefact root holds streams and markers (local only); `research/policy-ab/` holds the
committed records. No harvest: harvest lands a Kind `Fix` row in the bank (`src/Bench.Domain/Authoring/CodeTask.cs:15`) and
these are stories, not bug fixes. A DB store, a read API and a console tab are NOT built; if the owner wants them, they are their
own plan (the scouts' store is the model).

### 3.11 Reuse map

| Piece | How |
|---|---|
| `ScoutTrees`, `ScoutTreeSpec` | widened: `KeepProjectSettings` (scouts' default unchanged) |
| `ScoutParentCheck`, `ScoutMergeStyle.LocalSquash` (for `direct`), `LeakCheck`, `ScoutScrubber`, `SpendLedger`, `ScoutAttemptLifecycle.Recover`, `ScoutTokens`, `ScoutCost.Check`, `PriceTable`/`CliRate`, `ScoutPins.Continue` semantics, `SlotRotation` | reused as they are (pins: a `PolicyPins` record adds fable, the overlay hashes and the audit-hook hash, composing the same continue rule) |
| `ScoutPinnedSettings.Explicit`, `EffortLevels`, `FileNameFor`; `ScoutArtifacts.ExplicitEffort()`'s write-once-and-hold rule (PR #93) | `Explicit` widened with a `models` overload (§3.3); the rest reused; `PolicyPinnedSettings` composes it with the permissions block |
| `CliScoutRunner` | **the shared half extracted**: open stream → durable mark (a delegate) → launch with the line sink → commit stderr/argv/prompt/seal becomes `StreamedClaudeLaunch`; `CliScoutRunner` calls it with unchanged behaviour (its tests are the refactor's proof), `PolicyCellRunner` calls it too |
| `ScoutArtifacts` | widened from `ScoutAttempt`-keyed to an attempt-folder key both runners pass |
| `ClaudeSubagents` / `SubagentTranscript` | widened: `MainToolCall` gains `Input`, `Fate`, `ResultText` (additive); envelope gains `duration_api_ms`, `num_turns` as *captured or not* |
| `PathAudit` | widened: the audited tool set and the main session become parameters; scouts pass today's values |
| `ProbeChildEnvironment` | widened: `ForPolicyArm` beside `ForScouts`, same allow-list |
| `SolutionSignals` | widened: prepared-tree overload + materialiser port + per-test report readers; signals 3–4 added |
| `FindingAssessor.OptionsFor`, `strict.md`, `BlindExport`'s id rule | reused; the id rule extracted into a shared helper |
| `tests/FakeCli` | widened: a `writeFiles` script knob so a replayed cell leaves a diff in its cwd, and an audit-log knob so a replayed cell leaves `.git/policy-audit.ndjson` |
| **New** | `PolicyOverlay`, the audit hook script, `CommandAudit`, `PolicyRuleReached`, `PolicyAudit` with its effort audit (§3.6), `PolicyRunLimits`, `PolicySchedule`, the story/checklist/hold-out records, `PolicyCellRunner`, `bench policy` verbs — none has a counterpart to widen: the scout read-back expects exactly N frozen Agent calls and voids anything else, and no existing piece reads a hook log |

`CliSubjectRuntime` and `CliAgentRuntime.AskAsync` are not used: one is `--output-format json` with a worktree onto the full
mirror, the other buffers; neither streams a write-capable session.

### 3.12 Prompts and records

`prompts/policy-ab/`: `run.md`, `rules-probe.md`, `access-check.md`, `assess-candidate.md`, `assess-pairwise.md`,
`permissions.json`, `policy-audit.mjs` — each hashed into the pins. `research/policy-ab/`: `arms.json` (conventions shas, both
hashes per overlay file, the pinned-settings hash), `stories.json` (§3.2), `prices.json` (a copy of the scouts' with a **Fable**
row, URL and date — no B cell runs before it exists), `schedule.json`, `cells.jsonl` (per cell: arm, story, run, generation, pins,
outcome, every metric, the audit with effort), `pilot.md`, `handcheck.md`, `access-check.md`; the series write-up
`research/RESULTS_policy_ab.md`. All three stories are from public repositories; diffs and statements may be committed, raw streams
and audit logs are not (they are large and carry machine paths).

### 3.13 Growth budget

| Surface | Projected size | Retired by |
|---|---|---|
| Frozen story trees | 3 × ~60 MB (with the conventions submodule) | `bench policy prune` after RESULTS is merged — only directories the run created, path checked |
| Cell trees (with `node_modules`/`out`, `bin/obj` for .NET) | 20 cells × ~1.5 GB ≈ 30 GB worst case | each pruned once its candidate diff and scores are committed; `prune --run` sweeps the rest |
| Raw streams, markers, audit logs | 20 × ~5 MB, local | kept until promotion, then gzipped; never committed |
| claude per-project folders (one per cell cwd; the probes' with transcripts) | ~25 small folders under `~/.claude/projects` | `prune` removes exactly the slugs of the cells' and probes' paths, nothing else |
| Committed records | < 5 MB | kept with RESULTS |
| Interrupted cell | at most one in flight | `resume` settles it from its marker and stream (§3.9) |

## 4. The pilot — 1 story × 2 arms × 1 run, and the owner's go/no-go

Order: (1) `prepare` the pilot story (tree, statement, checklist, hold-out classification, baseline twice, gates, both hashes per
overlay file) → commit; (2) `rules-probe` both arms, then the effort break-it; (3) `access-check` both arms; (4) calibration step 1
— the five candidate-free items (the reference and the four planted variants, §3.8) — passes **before cell one**; (5) evidence
commit — pins, caps, prompts, schedule, the arm order (drawn by seed); (6) the two cells, back to back, attended; (7) mechanical
scoring; (8) blind assessment of the two candidates + calibration step 2; (9) `pilot.md`.

**It checks the instrument, not the policy.** One run per arm cannot estimate variance (MEASURED_LESSONS §1); the pilot reports
both cells' numbers labelled *instrument check — not a comparison*.

| Go criterion for the full series | Evidence |
|---|---|
| Both arms launched; each arm's rule reached its session (by the layout's hash) or the gap is explained; B's guard denied the probe's no-model launch, A's did not | §3.4 streams and audit logs, `pilot.md` |
| Actual model assignment matches the arm definition: A's children all on the Opus id (or every exception explained); B's launches all named an alias | §3.6 audit, hand-read prompts |
| **Effort equal and `high` everywhere**: every logged level — main session and every child, both cells and both probes — is `high`; hook and transcripts agreed in the probe; the `xhigh` break-it was caught; unobserved children counted | audit logs, `cells.jsonl`, `pilot.md` |
| No leak route returned content undetected; shell routes' status known, and — if shell reads of the original clones were open — the row "the series needs an OS sandbox" present (§3.5) | access-check + per-cell audits |
| Capture complete: diff extracted, cost check within 2 %, time and tokens per model present | `cells.jsonl` |
| Scoring discriminates: calibration steps 1 and 2 passed (§3.8); the mechanical gates ran green at S and on the reference | `handcheck.md` |
| Caps sized: neither cell clipped by a cap the owner considers unrepresentative | pilot spend vs $40 / 120 min |

A no-go names the failing criterion and its fix; the owner decides on a re-pilot.

## 5. The full series — 3 stories × 2 arms × 3 runs (EXPLORATORY)

- **Order**: 9 blocks (repeat outermost, then story), each block one A and one B cell; the leading arm by
  `SlotRotation.Rotated([A, B], globalBlockIndex)` — 5 : 4 at nine blocks, so position is reported as a covariate. One cell at a
  time; a story's two arms never separated by more than one block (cache and time-of-day drift fall on both).
- **Pinned and frozen before cell one**: CLI version, resolved ids, the effort file (`high` for every model), settings/overlay/
  audit-hook/prompt hashes, story trees, caps, walls, the dependency restore's lockfile hashes, and a written **prediction**
  (committed sha cited in RESULTS). A pin move stops the series; a moved alias is a new series.
- **Reported, every run**: per story × arm successes/3, coverage, defects, quality, teeth; time and cost as median and range;
  per-story B − A; then the equal-story-weighted mean of the per-story differences; pairwise wins/ties/losses; the rule-delivery
  layout per story (native or resolver) beside its numbers. The three stories are the generalisation units: no p-values, no power
  claim, the label **exploratory** in the RESULTS title and status line.
- Void cells re-run as generations up to 2 spare cells; further voids are reported, not replaced.
- Projected spend: 18 × the pilot-derived reservation (≈ $300–900 at $20–40 a cell); the ceiling is the owner's number.

## 6. Threats to validity

- **One story ≠ the policy.** The plan-level Fable work is untouched (§1, §10); story 3 (tagged Fable by its own plan) is the only
  chance for Fable to appear.
- **Variance.** Three runs per arm per story; a single difference reads as noise until the series shows its direction per story.
- **Unequal effort between models** — the measured precedent: the scouts' pilot ran Sonnet at `xhigh` and Opus at `high` because a
  child takes its OWN model's effort from the settings, and the comparison it produced (Sonnet slower and dearer) was partly an
  effort comparison (§2). Here every model is pinned `high` in identical bytes for both arms, a unit test proves every model
  resolves to `high`, and every cell's per-child effort audit stops the campaign on any other level (§3.3, §3.6). Residual: a
  child that makes no tool call is *unobserved*, reported per cell.
- **Spec gap.** The pilot's spec is final at P (the PR only ticks it) and its one fix commit is speed-only, so the gap is small
  there; story 2 has two fix commits whose behaviours may be beyond-spec; story 3's follow-ups are outside the reference. The
  classification (§3.2) keeps beyond-spec tests out of the primary signal, at the cost of a judgement made before any candidate
  exists — the reasons are committed for audit.
- **Policy reach differs by layout.** In the native layout (stories 1 and 3) both arms' rule texts load at session start
  unconditionally; in the resolver layout (story 2) a rule reaches a session only if it follows `ENTRY.md` for its task — there A
  can degrade to "Opus default" and B to "the guard's digest". §3.4 measures it per cell rather than forcing a delivery channel
  real sessions do not have, and §5 reports the layout beside each story's numbers.
- **Detection-only shell boundary** on Windows (§3.5); a determined model could evade a text audit. The pilot runs under it;
  the series does not if shell reads were open (§3.5's decision).
- **The audit hook is visible.** The model can read `.claude/hooks/policy-audit.mjs` and its settings entry — identical in both
  arms, so it cannot differ between them; reading or touching its log is a stop.
- **Blinding is partial**: style may reveal the arm; the order-swapped pairwise and anchored rubrics limit, not remove, it.
- **Prompt cache**: a later cell may hit an earlier cell's cached prefix; cache tokens are reported apart and order is balanced.
- **Environment differs from daily use**: no MCP, no gate, no plugins, no background tasks, no memory, effort `high` for every
  model. Identical across arms; the absolute numbers are not the operator's daily ones.

## 7. Boundaries with other plans

| Item | Built by | The other plan's part |
|---|---|---|
| Signals 3 (teeth) and 4 (neighbours) in `SolutionSignals` | **this plan** | `PLAN_code_lane.md` consumes them; its sandbox §4.2 stays its own |
| History-free trees, launch, ledger, pins, the explicit-effort file | `PLAN_scout_model_calibration.md` (built; effort via PR #93) | this plan widens `ScoutTrees`, `PathAudit`, `ClaudeSubagents`, `ScoutArtifacts`, `ProbeChildEnvironment`, `ScoutPinnedSettings.Explicit` additively; the scouts' behaviour is unchanged and its suite is the proof |
| The scout half of the rule (⌈1.5·N⌉ Sonnet readers) | `PLAN_scout_model_calibration.md` | here scouts appear only as the policy uses them inside a story |
| The verdict on the shared rule | `dew_flow_conventions · research/PLAN_subagent_model_tiers.md` | this plan hands its RESULTS there as a PR, after the series |

Order: none blocks the other; this plan's widenings land behind the scouts' existing tests. Disjoint otherwise.

## 8. Build order

1. Widen the reader: `MainToolCall` input/fate/result; envelope `duration_api_ms`/`num_turns`. RED fixture first: a
   **hand-recorded, scrubbed** `claude -p --output-format stream-json --verbose` coding run on a throwaway tree, with main-session
   Bash/Edit calls, recorded once by the operator (no runner records one yet), together with its audit-hook log.
2. Extract `StreamedClaudeLaunch` from `CliScoutRunner`; widen `ScoutArtifacts` keys. The scouts suite must stay green unchanged.
3. `ScoutTreeSpec.KeepProjectSettings` (+ the `effortLevel`/`modelSettings` refusal); `PolicyOverlay` (pure) + the overlay writer
   + the audit hook script; both hashes per overlay file (the loader's way via the tree's own module); `ProbeChildEnvironment.
   ForPolicyArm`; `ScoutPinnedSettings.Explicit(effort, models)` + `PolicyPinnedSettings` + the every-model-resolves-to-`high`
   test; `PolicyPins`.
4. `CommandAudit` (pure) and `PathAudit` widened; the audit-log reader; `PolicyRuleReached`; `PolicyAudit` with the effort audit.
5. `PolicyRunLimits`, the file ledger adapter, `PolicyCellRunner`, `PolicySchedule`; verbs `prepare`, `rules-probe` (with the
   effort break-it), `access-check`, `run`, `resume`, `status`, `prune`; `FakeCli` `writeFiles` and audit-log knobs.
6. `SolutionSignals` prepared-tree overload, materialiser port, per-test report readers (node:test junit, MTP trx), signals 3 and
   4; verb `score`.
7. The blind assessment: assessment repository builder, the shared fresh-id helper, the two assessor prompts, verbs `assess`,
   `handcheck`, `report`.
8. `prices.json` with the Fable row (source URL, date); the pilot story frozen; the pilot (§4).
9. `module_policy_ab.md` + the `architecture.md` section + `research/module_tests.md` rows; **the §7 boundary table written into
   `PLAN_code_lane.md` and `PLAN_scout_model_calibration.md`** (both sides, planning-docs); the owner's go/no-go.

## 9. Test plan

All from the test executable, never `dotnet test`. Offline by default; live streams and audit logs become fixtures (scrubbed).

- **Reader**: the hand-recorded coding stream (§8 step 1) — main Bash/Edit inputs and results read; a guard denial classified
  (fixture from the rules probe); `duration_api_ms` absent reads *not captured*, never 0. Scouts' reader tests unchanged and green.
- **Overlay**: A and B over a tree WITHOUT project hooks (creds at `9f62e4db`: `permissions` only) and over one WITH them (coai at
  `af7f2856`: `load-instructions` + `build-flags`), and over a tree with no `.claude/settings.json`; the merge adds exactly the
  audit entries (both arms) and one `Agent|Task` guard entry (B); existing hooks and permissions kept byte-for-byte;
  `ensure-user-guard.mjs` never written; both mounts (`.claude/rules/shared`, `.agents/conventions`) written, any other refused;
  the overlay hashes stable; the loader-way hash equals what the tree's own `rule-cli` prints for the file.
- **Effort pin**: the arm settings text is byte-identical for A and B; its top level and `modelSettings.sonnet`, `.opus`,
  `.fable` all say `high`; a resolver following the measured rule (the model's own `modelSettings` entry, else the top level) gives
  `high` for sonnet, opus, fable AND an unnamed model (haiku); `Explicit(effort)` without models still yields PR #93's text;
  `prepare` refuses a tree whose settings name `effortLevel` or `modelSettings`.
- **Effort audit** as a table over audit-log fixtures: all `high` → pass; one child `xhigh` (the 2026-10-14 overlay's record) →
  VOID naming the child and the level; the main session `xhigh` → VOID; no main record → VOID; a child with no tool call →
  *unobserved*, counted, not VOID.
- **Environment**: B's child lacks `SUBAGENT_MODEL_GUARD` even when the parent sets it `off`; A's has `off`; neither carries
  `CLAUDECODE`, `CLAUDE_CODE_*` (except the forced background switch), a secret-named variable or a `gh` directory on `PATH`.
- **CommandAudit** as a table: `gh pr view`, `git clone https://…`, `curl`, `iwr`, `Invoke-RestMethod`, `npm view`, an absolute
  `D:\rsd\…` path, `..\..\`, `%USERPROFILE%`, `$env:HOME`, a reference to `.git/policy-audit.ndjson`, a compound command with one
  bad half → stop; `npm run typecheck`, `node --test …`, `dotnet build`, the MTP executable, `git diff`, a path inside the tree, an
  allow-listed toolchain dir → fine.
- **Extraction refactor**: `CliScoutRunner`'s whole existing suite and `ScoutsCrashTests` pass unchanged.
- **Runner e2e (fake CLI)**: a replayed cell that writes two files and an audit log → candidate diff extracted, overlay hunks
  dropped, effort audited, settled from `total_cost_usd`; a crash before the marker releases the reservation; after the marker
  with no result charges it; `resume` settles once.
- **Limits**: `PolicyRunLimits` as a table; the same cap for both arms enforced (a per-arm schedule is refused).
- **SolutionSignals**: a fixture repository with a correct solution, a solution whose test has no teeth (signal 3 red), and one
  that breaks a neighbour (signal 4 red); per-test results parsed from node:test's junit report and from an MTP trx; a
  pre-existing flaky test excluded.
- **Blind assessment**: no arm, model name, cost or time reaches the assessment repository (planted strings); ids fresh and
  shuffled; the pairwise swap and the consistency rule as a table.
- **Break-it** (each red with the real symptom, then restored; no tripwire disabled): `CommandAudit` without the `gh` verb lets the
  planted `gh` command through; B's env keeping the parent's `off`; the overlay merge dropping the project's own hooks; the
  extraction losing the durable mark (the crash test goes red); the scrubber not applied to assessed diffs; **the effort audit
  ignoring child records lets the `xhigh` Sonnet fixture through**; **`Explicit` dropping one model's entry fails the
  every-model test**; and live, once, the `xhigh` overlay in the rules-probe (§3.4).

## 10. Open question, decisions taken, limitations

**The ONE owner question — O4: an A/A pair in the pilot?** One more arm-A cell on the pilot story, back to back with the first.
*Cost*: cap $40, reservation $48 (≈ $20–40 expected), about two more attended hours; the pilot ceiling becomes $213.60, so
`PolicyRunLimits`' pilot bound moves from $200 to $220 if yes. *What it buys*: a first look at how far two runs of the SAME arm
land apart on the same story — whether a pilot A–B difference is even readable against A–A — and a check that the cost and time
capture repeats. *What it does not buy*: a variance estimate (one pair), or anything the series' three runs per arm will not
give anyway. D1 did not include it.

**Resolved, recorded so nobody re-asks:**

- **O1 (story 1's spec written with its code)** — resolved by D3's re-selection: coai #754 dropped for exactly that reason (§2);
  the pilot is creds #176, whose E1 text is final at P.
- **O2 (an OS-level sandbox)** — decided in §3.5: the pilot's access-check decides; open shell reads of the original clones make
  the sandbox a requirement of the series, stated in `pilot.md`, and the owner decides then.
- **O3 (story 3 an epic)** — resolved by D3: coai #766 dropped (§2); story 3 is creds `5158ac24`, one Fable-tagged story of 324
  changed lines.
- **O5 (external multi-vendor review of candidates)** — resolved: no review runs inside a cell, and the gate is not a scorer
  (§3.8).

**Limitation (was O6):** a plan-level lane (a plan → epics → stories run) is the only way to measure the rule's Fable split; it is
out of scope here, and RESULTS must say so.

## 11. Definition of Done

- [ ] The widenings landed with the scouts suite green and unchanged; every new pure piece has table tests; every break-it went
      red and back.
- [ ] The pilot story frozen before cell one: tree hash, P verified by its merge style, statement + scope note leak-checked and
      hashed, checklist and hold-out classification with reasons, baseline run twice, gates at P and S, both hashes per overlay
      file.
- [ ] `rules-probe` passed for both arms: the arm's rule reached the session by the layout's hash, B's guard denied, A's child
      resolved to Opus, every logged effort `high` with hook and transcripts agreeing; the `xhigh` break-it caught.
- [ ] `access-check` ran for every route × agent; no content returned undetected; the shell routes' status is recorded, with the
      OS-sandbox consequence stated if they were open.
- [ ] Calibration step 1 passed before the first cell.
- [ ] Caps, wall, prompts, pins (the effort file and the audit hook included), `prices.json` (with Fable) and the arm order
      committed before the first cell.
- [ ] Both pilot cells settled; ledger = Σ `total_cost_usd` + Σ unknown charges; the cost check within 2 % per cell; every
      cell's effort audit `high` for the main session and every observed child.
- [ ] The assessor calibration (both steps) passed and is recorded item by item in `handcheck.md`.
- [ ] `pilot.md` reports both cells labelled *instrument check*, the audit per arm, every go criterion with its evidence, and the
      answer to O4 if the owner gave one.
- [ ] `research/module_policy_ab.md`, `architecture.md`, `research/module_tests.md`, `research/README.md`, `todo/README.md`
      updated; the boundary table of §7 written into `PLAN_code_lane.md` and `PLAN_scout_model_calibration.md` too.
- [ ] Open tail: the owner's go/no-go on the series; if go, the series and `RESULTS_policy_ab.md` (exploratory), handed to
      `dew_flow_conventions · research/PLAN_subagent_model_tiers.md` as a PR there.

## Epics

Split 2026-10-14 (plan → epics, Fable) after the question consultant was asked what is important: the pilot is an **instrument
check, not a comparison**, and what voids it silently is (a) a leak of the reference through the shell, (b) the rule text not
reaching an arm's session, (c) an assessor read before it is calibrated, (d) children not on the model — or not at the effort —
the arm claims. E1–E6 build only what the pilot needs; E7 runs it and writes the go/no-go; E8 is the series — nothing in it is
built before the owner decides (D1). Stories per epic: one document `todo/PLAN_subagent_policy_ab_E<n>.md`, written when the epic
starts (Opus), linked from the epic in that commit (both sides). **Story ids are an INPUT** to E4's `prepare`, E7 and E8 (D3;
§3.2's rows are candidates until `stories.json` is committed) — no critical path waits on them. Size: S ≈ 1–2 stories, M ≈ 3–4,
L ≈ 5–7. Pilot tag: *prerequisite* = lands before cell one; *GATE* = produces §4's evidence.

**Cornerstones: 0 of 4 — none marked yet.** Q = ⌈2·8/5⌉ = 4. Reserved areas (≤ 1 story each), marked by Opus in the stories
document that specifies it and recorded on this line in the same commit: (1) **the leak boundary** — `CommandAudit` and the
`access-check` verdicts (E1/E4); (2) **rules reached** — `PolicyOverlay`, the two rule hashes (bytes; the loader's way), the
`rules-probe` pass rule (E3/E4); (3) **the child-model + effort audit** — `PolicyAudit` per Agent call and for the main session,
the hook log as the effort source (E1); (4) **the assessor calibration protocol** (E6).

### E1 — Read-back: the reader widened, the audits (§8 steps 1 and 4) · pilot: prerequisite · size M

- **Goal.** Everything a cell is judged by is read from a record — the stream or the audit-hook log — main session included; a
  fact from a record, never prose.
- **Scope.** `MainToolCall` gains `Input`/`Fate`/`ResultText`, the envelope `duration_api_ms`/`num_turns` as *captured or not*
  (`SubagentTranscript.cs:77`, `ClaudeSubagents.cs:29`); `CommandAudit` (pure, §3.5 table); `PathAudit` with the tool set and the
  main session as parameters (`PathAudit.cs:49-62`; scouts pass today's values); the audit-log reader (`PreToolUse` effort lines,
  `InstructionsLoaded` lines); `PolicyRuleReached` (native: the `InstructionsLoaded` byte hash; resolver: the `BEGIN RULE` header);
  `PolicyAudit` (§3.6) with the **effort audit** — main session and every child `high`, anything else VOID and a stop, a child
  without a tool call *unobserved*. Fixture: a hand-recorded `claude -p --output-format stream-json` coding stream on a throwaway
  tree, scrubbed, with its audit log (no runner records one yet); the effort fixtures from the 2026-10-14 hook probe (all `high`;
  the `xhigh` Sonnet child). `research/module_policy_ab.md` is created here and grown by every epic that lands code.
- **Depends on.** Nothing. **Cornerstone areas** 1 (the detection half), 3.
- **Done when.** §9 *Reader*, *CommandAudit* and *Effort audit* tables green; the scouts' reader and `PathAudit` suites unchanged;
  an absent `duration_api_ms` reads *not captured*, never 0; break-its "`CommandAudit` without the `gh` verb lets the planted
  command through" and "the effort audit ignoring child records lets the `xhigh` Sonnet fixture through" went red and back;
  `PolicyRuleReached` compares against the hashes E3 pins, not a hash of its own.

### E2 — `StreamedClaudeLaunch` extracted from `CliScoutRunner` (§8 step 2) · pilot: prerequisite · size S

- **Goal.** One durable-mark → launch → line-sink → commit path for both runners; the scouts' behaviour unchanged.
- **Scope.** `CliScoutRunner.cs:139-153, :183-220` → `StreamedClaudeLaunch` (the durable mark as a delegate); `ScoutArtifacts`
  keyed by an attempt folder both runners pass (§3.11).
- **Depends on.** Nothing; parallel to E1 and E3.
- **Done when.** The whole `CliScoutRunner` suite and `ScoutsCrashTests` pass unchanged; break-it "the extraction loses the
  durable mark" turns the crash test red and back; E4 needs no second launcher.

### E3 — The arms materialised: tree spec, overlay, effort pin, environment, pins, prices (§8 step 3; §3.1, §3.3) · pilot: prerequisite · M

- **Goal.** Two cell trees differing in the policy alone, every model at effort `high` in identical bytes, every difference
  pinned; the guard off for A by environment, absent for B.
- **Scope.** `ScoutTreeSpec.KeepProjectSettings` (default false; `ScoutPorts.cs:158`, `ScoutTrees.cs:147`) and the refusal of a
  tree whose settings name `effortLevel`/`modelSettings`; `PolicyOverlay.Apply` (pure) + the overlay writer — the conventions mount
  read from `.gitmodules` (`.claude/rules/shared` or `.agents/conventions`, any other refused), two rule files for both arms, the
  audit hook and its `PreToolUse *` + `InstructionsLoaded` entries for both, for B `.claude/hooks/subagent-model.mjs` and ONE
  `PreToolUse` `Agent|Task` entry merged, `ensure-user-guard.mjs` never; `ProbeChildEnvironment.ForPolicyArm` beside `ForScouts`
  (`ProbeChildEnvironment.cs:58-61`; the git, `gh`-off-`PATH` and background-task controls of §3.3); **the effort pin** —
  `ScoutPinnedSettings.Explicit(effort, models)` widened from PR #93 (`origin/main · ScoutPinnedSettings.cs:97`), written once as
  `pinned-settings.effort-high.json` by `ScoutArtifacts.ExplicitEffort()`'s rule (`origin/main · ScoutArtifacts.cs:183`),
  `PolicyPinnedSettings` + `prompts/policy-ab/permissions.json`; `PolicyPins` (fable id, both hashes per overlay file, the
  audit-hook and settings hashes, composing `ScoutPins.Continue`); `arms.json`; `research/policy-ab/prices.json` with the Fable
  row (URL, date). **The rule-hash pins are two**: the file's bytes, and `sha256(normalizedText(file))` computed by importing the
  tree's own loader (`8355757 · tools/lib/rule-catalog.mjs:44-47, :98`, printed by `tools/lib/rule-cli.mjs:115`; the same lines at
  `fba970b7` and `bb022429`) — never a C# re-implementation of `folded`.
- **Depends on.** Nothing; parallel to E1 and E2 (rebased onto `origin/main` for PR #93's `Explicit`). **Cornerstone area** 2
  (overlay + hashes).
- **Done when.** §9 *Overlay*, *Effort pin* and *Environment* green over a tree without project hooks (creds at `9f62e4db`:
  `permissions` only — kept byte-for-byte) and one with them (coai at `af7f2856`: `load-instructions` and `build-flags` — kept
  byte-for-byte); every model in the arm settings resolves to `high`, A's and B's settings bytes equal; B's child lacks
  `SUBAGENT_MODEL_GUARD` with the parent `off`, A's has `off`; break-its "the merge drops the project's hooks", "B keeps the
  parent's `off`" and "`Explicit` drops one model" red and back; the loader-way hash equals the tree's own `rule-cli` output; the
  Fable price row exists before any B launch; the overlay hashes stable across two writes.

### E4 — The cell runner and the `bench policy` verbs, pilot shape (§8 step 5) · pilot: prerequisite, checks (a) (b) (d) · size L

- **Goal.** One attended cell runs end to end and settles its money without a database; §3.4/§3.5's live checks exist as verbs.
- **Scope.** `PolicyRunLimits` in pilot bounds only (cap ≤ $60, wall ≤ 180 min, ceiling ≤ $200 — $220 if the owner answers O4
  yes; one cap for both arms — a per-arm schedule refused); the file ledger adapter over `SpendLedger` +
  `ScoutAttemptLifecycle.Recover`; `PolicyCellRunner` on `StreamedClaudeLaunch`; `PolicySchedule` for ONE block, arm order drawn by
  seed (the 9-block rotation is E8's); verbs `prepare` (arguments: repository, S, P, merge style `squash`/`rebase`/`merge`/`direct`
  — freezes tree, statement + scope note via `LeakCheck`, checklist, hold-out classification, baseline twice, gates, both rule
  hashes → `stories.json`), `rules-probe` (per layout, the four-child effort coverage, the transcript cross-check, the live `xhigh`
  break-it), `access-check`, `run`, `resume`, `status`, `prune`; `prompts/policy-ab/run.md`, `rules-probe.md`, `access-check.md`;
  `tests/FakeCli` `writeFiles` and audit-log knobs.
- **Depends on.** E1, E2, E3. **Cornerstone areas** 1 (`access-check`: route × agent, exit 3 on undetected content), 2
  (`rules-probe`).
- **Done when.** §9 *Runner e2e* on the fake CLI: two written files → candidate diff, overlay hunks dropped and recorded, effort
  audited, settled from `total_cost_usd`; a crash before the marker releases, after it charges, `resume` settles once; *Limits*
  table green; `prepare` accepts a `direct` commit (one parent, on the first-parent chain) and a squash, and refuses a direct commit
  off that chain; `rules-probe`/`access-check` yield §3.4/§3.5's verdicts from fixture streams and logs (a denial opening
  `subagent-models.md:`, a no-model child resolved to Opus, an `InstructionsLoaded` byte hash, an `xhigh` child VOIDed — each
  classified); `prune` removes only paths the run created.

### E5 — Mechanical scoring: `SolutionSignals` widened, signals 3 and 4 (§8 step 6; §3.8) · pilot: prerequisite · size M

- **Goal.** *Success* and the mechanical secondaries of §3.7 computed from a prepared tree per hold-out TEST, attended.
- **Scope.** The prepared-tree overload + the hidden-test materialiser port (`git -C <mirror> show S:<path>`, fixture folders
  included) reusing `SignalsAsync` (`SolutionSignals.cs:53-118`); the story's commands from `stories.json`; per-test results from
  node:test's junit report (the pilot) and the MTP runner's trx; signals 3 (teeth) and 4 (neighbours against the twice-run
  baseline) as `PLAN_code_lane.md:165-166`; verb `score`; `cells.jsonl` metric rows. `Passed`/`TouchesCause` unused (§3.7).
- **Depends on.** E3 (tree spec); otherwise parallel to E4 on a fixture repository.
- **Done when.** §9 *SolutionSignals*: correct solution green; no-teeth solution → signal 3 red; neighbour-breaking → signal 4
  red; a flaky-at-P test excluded; the per-test parse proven on the real runner's report (node:test junit from the pilot tree at
  P); §7's `PLAN_code_lane.md` row true.

### E6 — The blinded assessor and its calibration instrument (§8 step 7; §3.8) · pilot: prerequisite, carries check (c) · size L

- **Goal.** A codex assessor that cannot see the arm, and a calibration protocol that must pass before any number is read.
- **Scope.** Assessment-repository builder (frozen tree + one commit per candidate under a fresh id — `BlindExport.cs:133`'s rule
  extracted into a shared helper, not copied); `ScoutScrubber` + a model-name deny-list over diff text, count recorded;
  `prompts/policy-ab/assess-candidate.md`, `assess-pairwise.md`; `FindingAssessor.OptionsFor` reused; verbs `assess`, `handcheck`,
  `report`; the planted-variant recipe (a requirement removed; a defect the hold-out catches; one it misses; a valid alternative
  shape), the two-step order (the five candidate-free items before cell one, the candidates after) and the pass rule (≥ 90 %
  R-agreement; every planted defect found and below the reference; the alternative not penalised).
- **Depends on.** E3 (scrubber, pins); a frozen reference diff to plant on (E4 `prepare` over the pilot story). **Cornerstone area** 4.
- **Done when.** §9 *Blind assessment*: planted arm/model/cost/time strings never reach the assessment repository; ids fresh and
  shuffled; pairwise swap + consistency rule as a table; break-it "scrubber not applied to assessed diffs" red and back; one
  read-only dry run of `assess` over a fixture candidate completes; `handcheck.md`'s item-by-item format fixed.

### E7 — The pilot: freeze, probe, check, calibrate, two cells, report (§8 steps 8–9; §4) · pilot: **GATE** · size M

- **Goal.** Every §4 go-criterion has its evidence, both cells labelled *instrument check — not a comparison*; the owner can decide.
- **Scope.** INPUT: the pilot story id (D3: creds #176). Order as §4: (1) `prepare` → commit `stories.json`; (2) `rules-probe` both
  arms + the effort break-it, streams and logs kept as fixtures; (3) `access-check` both arms → `access-check.md` (route × agent),
  with the OS-sandbox row if shell reads were open (§3.5); (4) calibration step 1 on the five candidate-free items passes BEFORE
  cell one; (5) evidence commit: pins (effort file and audit hook included), cap $40 / wall 120 min, prompts, schedule, arm order;
  (6) two cells back to back, attended, one at a time, each effort-audited; (7) `score`; (8) `assess`, the two candidates join
  `handcheck.md` (step 2), every Agent prompt hand-read; (9) `pilot.md`. Docs: `module_policy_ab.md` completed, the
  `architecture.md` section, `module_tests.md` rows, both READMEs, and the §7 boundary table written into `PLAN_code_lane.md` and
  `PLAN_scout_model_calibration.md` (§8 step 9; planning-docs *both sides*).
- **Depends on.** E1–E6, codex access, an attending operator, the owner's answer to O4 (or its absence = no A/A). No story-2/3 work.
- **Done when.** `pilot.md` carries one row per §4 criterion with its evidence file — the effort row and the shell-route/OS-sandbox
  row included; ledger = Σ `total_cost_usd` + Σ unknown, within the §3.9 pilot ceiling ($165.60; $213.60 with the A/A pair); cost
  check ≤ 2 % per cell; a no-go names the failing criterion and its fix; the owner's decision (go / re-pilot / stop) is recorded
  in `pilot.md`.

### E8 — The full series, 3 × 2 × 3 (§5) · **gated on the owner's go after E7 — nothing here is built before it** · size L

- **Goal.** The exploratory comparison with its variance visible per story, handed to `dew_flow_conventions` as a PR.
- **Scope.** INPUT: story ids 2 and 3 (D3: coai #751, creds `5158ac24`), the re-pilot if ordered, the OS sandbox in place if the
  pilot found shell reads open (§3.5). `PolicySchedule` 9 blocks via `SlotRotation.Rotated` (`SlotRotation.cs:19`), one A and one
  B per block, a story's arms never more than one block apart; `PolicyRunLimits` series mode (cap/wall = the larger pilot cell ×
  1.5 / × 2 from `schedule.json`, owner-approved); 2 spare generation cells; `prepare` for stories 2–3 (story 2 in the resolver
  layout, story 3 a `direct` commit; restore before the wall, lockfile hashes pinned); the prediction committed; 18 attended cells,
  each effort-audited; `score`/`assess` per story; `research/RESULTS_policy_ab.md` (*exploratory* in title and status, the
  plan-level limitation of §10 stated); `prune`; the PR to `dew_flow_conventions · research/PLAN_subagent_model_tiers.md`.
- **Depends on.** The owner's go and E7's cap sizing.
- **Done when.** 18 cells settled or reported void beyond the two spares; per story × arm successes/3, coverage, defects, quality,
  teeth, time and cost as median and range; per-story B − A and the equal-weighted mean; pairwise wins/ties/losses; the layout per
  story reported; the PR opened; this plan and every `_E<n>.md` promoted together.
