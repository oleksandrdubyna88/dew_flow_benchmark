# PLAN — Policy A/B, E1: freeze the story and the arms

> Status: **plan only, nothing implemented yet, 2026-10-14.** Epic E1 of [PLAN_subagent_policy_ab.md](PLAN_subagent_policy_ab.md).
>
> Stories split by Opus (epic → stories). Scenarios and briefs below are the judge's; every code story is written by a Sonnet
> coder, reviewed by Opus, fixed by Sonnet. **No cornerstone in this epic** — E1 is not one of the plan's cornerstone areas
> (E2 the leak boundary, E4 the hold-out gate); the parent's ledger reads `Cornerstones: 2 of 3 — E2.S4, E4.S3` and E1 adds none.
> Aligned 2026-10-14 with the parent's main-session decisions D9–D23 (D14 strike, D15 effort pin, D20 docs, D21 commands).

## The epic, restated (from the parent's § *Epics*, E1)

**Goal.** The pilot story and both arms exist as committed bytes before any model runs — `research/policy-ab/stories.json`,
`research/policy-ab/arms.json`, `research/policy-ab/prices.json` with the Fable row (parent §3.1, §3.2, §3.12). **Nothing in
this epic launches claude.**

**Done when (parent).** §9 *Overlay*, *Effort pin*, *Environment* green; the break-its "overlay merge dropping the project's own
hooks", "B's env keeping the parent's `off`", "`Explicit` dropping one model's entry" red with the real symptom, then restored; the
scouts suite unchanged; `stories.json` per §3.2 with red-at-P / green-at-S for the spec-implied tests; the three records committed.

**Not in this epic** (do not build here): the launch verb `run`, `rules-probe`, `access-check` (E3); the stream reader,
`CommandAudit`, `PathAudit`, `PolicyAudit`, `PolicyRuleReached`, the post-hoc leak check (E2); `SolutionSignals`, interface
mismatch, teeth, the reading sheet, the codex reader (E4); anything in the parent's §12.

## Story map

| Id | Title | Size | Depends on | Model |
|---|---|---|---|---|
| E1.S1 | A story tree keeps its project settings and refuses a competing effort | S (~150) | — | Coder: sonnet |
| E1.S2 | One effort for every model: `Explicit(effort, models)`, the effort rule, `PolicyPinnedSettings`, `permissions.json` | M (~300) | — | Coder: sonnet |
| E1.S3 | The arm's child environment: `ProbeChildEnvironment.ForPolicyArm` | S (~200) | — | Coder: sonnet |
| E1.S4 | The overlay merge: `PolicyOverlay`, `PolicyMount`, the guard entry, the effort-pin entry | M (~320) | — | Coder: sonnet |
| E1.S5 | The hook scripts: the audit hook `prompts/policy-ab/policy-audit.mjs` and the effort pin `prompts/policy-ab/effort-pin.mjs` (parent D15) | S (~260) | — | Coder: sonnet |
| E1.S6 | Arm sources and the overlay writer: bytes from the conventions mirror, verified by blob id; the overlay written into a given tree and hashed; `arms.json`'s shape (E3.S1 builds the cell tree with it) | M (~350) | S2, S4, S5 | Coder: sonnet |
| E1.S7 | The twice-run baseline over E4.S1's junit reader | S (~150) | E4.S1 (or builds its reader to E4.S1's spec) | Coder: sonnet |
| E1.S8 | Reference facts from the mirror: lineage, changed files, added lines per file, P's identifiers and text | M (~250) | — | Coder: sonnet |
| E1.S9 | The frozen statement and the reference-only tokens (pure) | M (~300) | — | Coder: sonnet |
| E1.S10 | The hold-out set, the checklist, the classification, `stories.json`'s shape (pure) | M (~350) | S9, E4.S1 | Coder: sonnet |
| E1.S11 | `bench policy prepare` — facts, statement, records, `arms.json` | L (~400) | S1, S4, S6, S8, S9, S10 | Coder: sonnet |
| E1.S12 | `bench policy prepare` — gates: baseline twice at P, tests at S, harvest per hold-out file | M (~350) | S7, S11 | Coder: sonnet |
| E1.S13 | Freeze the pilot: inputs, `prices.json` with the Fable row, one attended `prepare`, the three records committed | ops | S12; E4.S3's cornerstone SPEC | Judge: opus (main session) |

S1–S5, S8 and S9 run in parallel; S7 as soon as E4.S1's reader exists (or builds it). Sizes are changed lines including tests.

## Shared brief — every coder reads this first

- **Repository rules** — `CLAUDE.md` §1–§8: records for data, `Outcome<T>` for expected failures (never `throw` for control
  flow), no null in business logic (`[]`, `string.Empty`), primary constructors, collection expressions, `private static` where
  no state is read, cyclomatic complexity ≤ 4, files ≤ 800 lines, `TreatWarningsAsErrors`. Processes as exe + argv with a
  timeout, never a shell string.
- **Where the new code lives** — the sibling context the parent names (`Bench.*.Policy`): pure types in
  `src/Bench.Domain/Policy/`, ports (if any) in `src/Bench.Application/Policy/`, adapters in `src/Bench.Infrastructure/Policy/`,
  the verb in `hosts/Cli/Policy*.cs`, tests in `tests/Bench.Tests/Policy/`. Every new pure decider is added to the list in
  `ArchitectureTests.The_gate_decisions_live_where_no_model_can_be_reached` (`tests/Bench.Tests/ArchitectureTests.cs:109`, list
  from `:111`) — that list is what keeps a decision away from a model or a store.
- **Widen, never copy** (reuse-first): every scouts piece named below is widened additively; the scouts' own behaviour is
  unchanged and **their existing tests are the proof** — run them unedited.
- **Build and test** (never `dotnet test`):
  ```bash
  node .claude/rules/shared/tools/low-priority.mjs dotnet build dew_flow_benchmark.slnx -c Release -m:4
  node .claude/rules/shared/tools/low-priority.mjs ./tests/Bench.Tests/bin/Release/net10.0/Bench.Tests.exe --filter-namespace "Bench.Tests.Policy"
  node .claude/rules/shared/tools/low-priority.mjs ./tests/Bench.Tests/bin/Release/net10.0/Bench.Tests.exe --filter-namespace "Bench.Tests.Scouts"
  node .claude/rules/shared/tools/low-priority.mjs ./tests/Bench.Tests/bin/Release/net10.0/Bench.Tests.exe --filter-namespace "Bench.Tests.Probes"
  node .claude/rules/shared/tools/low-priority.mjs ./tests/Bench.Tests/bin/Release/net10.0/Bench.Tests.exe --filter-class "*ArchitectureTests"
  ```
  The whole suite once at the end of each story.
- **New code has no failing predecessor**, so its RED is the test written first against a missing type or member (a compile
  failure naming it is the symptom) or against a stub returning the wrong value; a **break-it** is the reverse proof: plant the
  named defect in the finished code, watch the named test go red **with the real symptom**, restore, watch green, and record
  both observations in the PR. A break-it never disables or weakens a tripwire.
- **The arm constants**, used by several stories, verified 2026-10-14 in `dew_flow_conventions`:
  arm A = `3df745a7ce9959c251c65dd688c16be0295e8724` (`d88e293^`), arm B = `ea3252f2bd88db1fc7e698c2c9a1a58ca7b916c0`. The two
  rule files: `common/subagent-models.md`, `common/planning-docs.md`. B's guard: `settings/hooks/subagent-model.mjs` (no
  imports; `DENY_MARKER = 'subagent-models.md:'` at `:35`; entry guarded by `process.argv[1]?.endsWith('subagent-model.mjs')` at
  `:168`) and its entry in `settings/settings.json`: `PreToolUse`, matcher `Agent|Task`, `"command": "node"`,
  `"args": ["${CLAUDE_PROJECT_DIR}/.claude/hooks/subagent-model.mjs"]`.
- **`PolicyArm`** is created by whichever of S3/S4 lands first, with exactly this text; the other rebases onto it:
  ```csharp
  namespace Bench.Domain.Policy;

  /// <summary>The two subagent-model policies under comparison (plan §3.1): A — conventions 3df745a7 (d88e293^), Opus by default;
  /// B — release ea3252f2, models by role.</summary>
  public enum PolicyArm { A, B }
  ```
- **Docs** (parent D20): each story that widens a scouts piece adds one line to `research/module_scouts.md` naming the widening;
  the FIRST code story of any epic to land creates `research/module_policy_ab.md` and every story — this epic's included — adds
  its paragraph there and its rows to `research/module_tests.md` in the same task. `architecture.md` and the completed module doc
  are E5.S9's.

---

## E1.S1 — A story tree keeps its project settings and refuses a competing effort

- **Goal.** `ScoutTrees` builds the pilot's frozen tree with the project's own `.claude/settings.json` still in it (a cell must
  load the project layer as a real session does), and refuses a tree whose committed settings could compete with the pinned
  effort. The scouts' trees are byte-identical to today.
- **Scope.**
  - `ScoutTreeSpec` — `src/Bench.Application/Scouts/ScoutPorts.cs:158` (`Privacy` init property at `:160` is the pattern).
  - `ScoutTrees.StageAsync` — `src/Bench.Infrastructure/Scouts/ScoutTrees.cs:138-152` (the removal list at `:147`, the grant
    check at `:148`); `FoundAsync` — `:122-134` (a tree found again is re-checked the same way).
  - `ScoutGroundTruth.ClaudeProjectSettings` — `src/Bench.Domain/Scouts/ScoutCandidates.cs:175` (read, not changed).
  - Tests: `tests/Bench.Tests/Scouts/ScoutTreesTests.cs` (fixture `DatedGitRepo` + `Builder` at `:258`).
- **Brief.**
  - Add `public bool KeepProjectSettings { get; init; }` (default `false`) to `ScoutTreeSpec`, with a doc comment naming plan
    §3.2: the policy re-run keeps `.claude/settings.json`; `.claude/settings.local.json` is still removed.
  - In `StageAsync`, the removal list becomes `spec.Remove ∪ (KeepProjectSettings ? [".claude/settings.local.json"] :
    ClaudeProjectSettings)`. Write that choice as a `private static IReadOnlyList<string> SettingsRemovals(ScoutTreeSpec spec)`;
    do not edit `ClaudeProjectSettings` (the scouts' G computation reads it).
  - After the grant guard, a new pure check `ScoutEffortGuard.Refusal(string settingsJson)` in
    `src/Bench.Domain/Scouts/ScoutEffortGuard.cs`: when the kept `.claude/settings.json` exists and its top-level object names
    `effortLevel` or `modelSettings`, answer *"the tree's own .claude/settings.json names \"<key>\" — a project layer would
    compete with the pinned effort (plan §3.2); the story is refused"*; unparseable JSON is a refusal too (lenient parse:
    comments and trailing commas allowed, as `ScoutPinnedSettings` does at `ScoutPinnedSettings.cs:19`). Empty when fine.
    Run it in `StageAsync` (through `Withheld`, like the grant refusal) and in `FoundAsync`, only when `KeepProjectSettings`.
  - The tree hash (`Hashed`, `:322`) is unchanged in shape — the kept settings file is simply one more file in it.
  - Nothing else about the build changes: the plan is NOT removed for the policy (the caller passes `Remove = []`).
- **Test scenarios** (`ScoutTreesTests` + `ScoutEffortGuardTests`):
  - `A_tree_built_to_keep_project_settings_keeps_settings_json_and_still_removes_settings_local_json` — RED: compile failure
    on `KeepProjectSettings`.
  - `A_scout_tree_still_drops_both_project_settings_files` — the existing scouts expectation, unchanged (proof the default holds).
  - `A_kept_tree_whose_settings_name_effortLevel_or_modelSettings_is_refused_by_the_key` — both keys, one test row each.
  - `A_kept_tree_whose_settings_grant_outside_it_is_still_refused` — the grant guard still runs on a kept file.
  - `A_kept_tree_found_again_is_re_checked_for_the_effort_keys` — plant `effortLevel` into a built tree's settings; `BuildAsync`
    again refuses.
  - `ScoutEffortGuard`: a table — `{}` → empty; `permissions` only (creds at `9f62e4db`'s shape) → empty;
    `effortLevel` → refusal naming it; `modelSettings` → refusal naming it; `{ // comment` malformed → refusal.
- **Done when.** The scenarios green; `--filter-namespace "Bench.Tests.Scouts"` green with no scouts test edited; the
  architecture test lists `ScoutEffortGuard`.
- **Depends on.** Nothing.
- **Coder: sonnet.**

## E1.S2 — One effort for every model

- **Goal.** One settings text, byte-identical for both arms, under which the main session and every child — whatever its
  model — resolve to effort `high` (owner decision D4), plus the fixed permissions block, written once and held.
- **Scope.**
  - `ScoutPinnedSettings.Explicit(string)` — `src/Bench.Domain/Scouts/ScoutPinnedSettings.cs:97-104`; `EffortLevels` `:88`;
    `FileNameFor` `:92`; the canonical writer options `:21`.
  - `ScoutArtifacts.ExplicitEffort()` — `src/Bench.Infrastructure/Scouts/ScoutArtifacts.cs:183-202` (the write-once-and-hold
    pattern to mirror, not to call).
  - New: `src/Bench.Domain/Policy/ClaudeEffortRule.cs`, `src/Bench.Domain/Policy/PolicyPinnedSettings.cs`,
    `src/Bench.Infrastructure/Policy/PolicyArtifacts.cs`, `prompts/policy-ab/permissions.json`.
  - Tests: `tests/Bench.Tests/Scouts/ScoutPinnedSettingsTests.cs` (existing `Explicit` assertions at `:62-64`, unedited),
    new `tests/Bench.Tests/Policy/PolicyPinnedSettingsTests.cs`.
- **Brief.**
  - **Widen** `ScoutPinnedSettings`: `public static Outcome<string> Explicit(string effort, IReadOnlyList<string> models)` writes
    `{"effortLevel": level, "modelSettings": {"<alias>": {"effortLevel": level}, …}}` with the aliases in the order given,
    through the same canonical options (indented, LF, trailing newline). The one-argument `Explicit(effort)` becomes
    `Explicit(effort, [])`, and an empty `models` list yields today's text exactly — no `modelSettings` key. Refusals: the
    existing effort refusal; an alias not matching `^[a-z][a-z0-9-]*$`; a duplicate alias — each naming the alias.
  - `ClaudeEffortRule.For(string settingsJson, string modelAlias) → Outcome<string>` — the host's measured rule (parent §2, the
    row "Effort is per the CHILD's own model, measured"; `research/module_scouts.md:531`): `modelSettings.<alias>.effortLevel`
    when present, else the top-level `effortLevel`, else a refusal *"no effort applies to <alias>"*. It models the host; it is
    used by tests and by nothing that launches.
  - `prompts/policy-ab/permissions.json` — the permissions OBJECT itself (not wrapped):
    `{"defaultMode": "acceptEdits", "allow": ["Edit", "Write", "Bash", "PowerShell"], "deny": [...]}`. Deny, in the
    `Tool(prefix:*)` form: `Bash(gh:*)`, `Bash(curl:*)`, `Bash(wget:*)`, `Bash(git clone:*)`, `Bash(git fetch:*)`,
    `Bash(git pull:*)`, `Bash(git push:*)`, `Bash(git remote:*)`, `Bash(git ls-remote:*)`, `Bash(npm install:*)`,
    `Bash(npm i:*)`, `Bash(npm ci:*)`, `Bash(npm view:*)`, `PowerShell(Invoke-WebRequest:*)`, `PowerShell(Invoke-RestMethod:*)`,
    `PowerShell(iwr:*)`, `PowerShell(irm:*)`, `PowerShell(curl:*)`, `PowerShell(wget:*)`, `PowerShell(Start-BitsTransfer:*)`,
    `WebFetch`, `WebSearch`. This is PREVENTION only: a deny list is known to miss PowerShell spellings, and the detection that
    the campaign stops on is E2's `CommandAudit` — E2's cornerstone spec may extend this list; it never shortens it.
  - `PolicyPinnedSettings` (pure): `Compose(string effortText, string permissionsJson) → Outcome<string>` — the effort object's
    keys, then `"permissions": <the object>`, canonical. `Verify(string text) → Outcome<string>` — keys are a subset of
    `effortLevel`, `modelSettings`, `permissions`; `permissions` carries no `additionalDirectories` (a grant) and its
    `defaultMode` is not `bypassPermissions`; each refusal names the key. The arm's effort text is `ScoutPinnedSettings.Explicit(PolicyPinnedSettings.Effort,
    PolicyPinnedSettings.Models)` with the constants `Effort = "high"` and `Models = ["sonnet", "opus", "fable"]` defined here.
  - `PolicyArtifacts(string artifactRoot)` (Infrastructure): `Folder = "policy"`;
    `PinnedSettings(string permissionsJson) → Outcome<string>` writes `<root>/policy/` +
    `ScoutPinnedSettings.FileNameFor("high")` (= `pinned-settings.effort-high.json`) ONCE through `ArtifactCommit.Stage`
    (`src/Bench.Infrastructure/Gate/ArtifactCommit.cs:17`), and when it exists holds it to exactly the composed text — else
    *"<path> does not hold the arm settings it is named for — delete it and run again to pin it afresh"* (the
    `ScoutArtifacts.cs:199-201` wording). `PinnedSettingsHash() → Outcome<string>` = lower-hex SHA-256 of the file's BYTES.
- **Test scenarios** (`PolicyPinnedSettingsTests`, `ScoutPinnedSettingsTests`):
  - `The_arm_settings_resolve_to_high_for_sonnet_opus_fable_and_an_unnamed_model` — `ClaudeEffortRule.For` over the composed
    text for `sonnet`, `opus`, `fable` and `haiku` (unnamed → the top level) — all `high`. RED: compile failure on the overload.
  - `The_arm_settings_text_is_byte_identical_for_both_arms_and_on_every_compose` — two composes, `SequenceEqual` on UTF-8 bytes.
  - `Explicit_without_models_still_writes_the_single_key_text_of_PR_93` — `Explicit("high")` and `Explicit("high", [])` both equal
    `"{\n  \"effortLevel\": \"high\"\n}\n"`; the existing assertions at `ScoutPinnedSettingsTests.cs:62-64` stay unedited.
  - `An_unknown_effort_a_malformed_alias_or_a_duplicate_alias_is_refused_by_name`.
  - `A_settings_layer_naming_one_model_beats_the_top_level_and_the_rule_says_so` — `{"effortLevel":"high","modelSettings":
    {"sonnet":{"effortLevel":"xhigh"}}}` → sonnet `xhigh`, opus `high` (the 2026-10-14 overlay; the rule's own teeth).
  - `The_permissions_block_allows_exactly_four_tools_and_denies_every_listed_network_verb` — reads the committed
    `prompts/policy-ab/permissions.json`: keys exactly `defaultMode`/`allow`/`deny`, `allow` equals the four, every deny entry
    matches `^(Bash|PowerShell)\(.+:\*\)$` or is `WebFetch`/`WebSearch`.
  - `A_pinned_file_with_a_grant_a_bypass_mode_or_a_foreign_key_is_refused_by_name` — `Verify`.
  - `The_pinned_file_is_written_once_and_a_changed_file_is_refused` — `PolicyArtifacts` over a temp root.
  - `Every_model_stays_high_even_under_a_project_layer_that_names_one` — the composed text layered OVER a project-layer stand-in
    `{"modelSettings":{"fable":{"effortLevel":"xhigh"}}}` by the test helper `Layered` (note below): sonnet, opus, fable, haiku
    all `high`.
  - **Break-it (parent §9):** *`Explicit` dropping one model's entry fails the every-model test* — plant: skip the last alias in
    the `modelSettings` loop; the scenario above goes red with *fable resolved to xhigh*; restore; green. Record both.
- **Done when.** Scenarios and the break-it observed; scouts suite green unedited; `ClaudeEffortRule` and
  `PolicyPinnedSettings` in the architecture list.
- **Depends on.** Nothing.
- **Coder: sonnet.**

> Note for the coder on the break-it: with only the pinned file, a dropped per-model entry still resolves to `high` through the
> top level — that is exactly why parent §3.3 names every model ("a per-model entry [in the project layer] would beat a
> top-level-only pin"). The test helper `Layered(pinned, project)` (pinned keys win, `modelSettings` merged per alias, pinned
> alias wins) is what makes the drop visible. Put the helper in the test project, not in production.

## E1.S3 — The arm's child environment

- **Goal.** The cell's claude process gets the scouts' allow-list, the fixed git and background variables, A's guard opt-out —
  and B can never carry that opt-out, whatever the parent holds.
- **Scope.**
  - `ProbeChildEnvironment` — `src/Bench.Domain/Probes/ProbeChildEnvironment.cs` (`Names` `:25-30`, `Passes` `:37`,
    `ModelGuardVariable` `:44`, `BackgroundTasksVariable` `:52`, `ForScouts` `:58-61`, `Fixed` `:63`, `WithGuardOff` `:75`).
  - `CoaiEnvironment.Minimal` — `src/Bench.Domain/Gate/CoaiEnvironment.cs:259` (drops `BENCH_*`/`COAI_*` at `:277` and
    secret-named variables at `:286` whatever the predicate says).
  - New: `src/Bench.Domain/Policy/PolicyChildInputs.cs`; `src/Bench.Infrastructure/Policy/PolicyPath.cs`.
  - Tests: `tests/Bench.Tests/Probes/ProbeChildEnvironmentTests.cs` (existing, unedited), new
    `tests/Bench.Tests/Policy/PolicyChildEnvironmentTests.cs`.
- **Brief.**
  - The parent's signature `ForPolicyArm(parent, guardOff)` cannot carry the empty git-config path or the PATH filter, and the
    domain may not probe the filesystem — so it takes a record:
    `public sealed record PolicyChildInputs(PolicyArm Arm, string EmptyGitConfigFile, IReadOnlyList<string> PathDirectoriesToDrop);`
    and `public static ChildEnvironment ForPolicyArm(IReadOnlyDictionary<string, string> parent, PolicyChildInputs inputs)`
    beside `ForScouts`.
  - Composition: start from `parent` with every spelling of `SUBAGENT_MODEL_GUARD` removed and `PATH` (any case — Windows spells
    `Path`) rewritten without the listed directories (compare full paths case-insensitively, trailing separator ignored, split on
    `Path.PathSeparator`); then FIXED (any parent spelling replaced, via `Fixed`): `CLAUDE_CODE_DISABLE_BACKGROUND_TASKS=1`,
    `GIT_CONFIG_GLOBAL=<EmptyGitConfigFile>`, `GIT_CONFIG_NOSYSTEM=1`, `GIT_TERMINAL_PROMPT=0`, `GIT_AUTHOR_NAME=policy-ab`,
    `GIT_AUTHOR_EMAIL=policy-ab@invalid`, `GIT_COMMITTER_NAME=policy-ab`, `GIT_COMMITTER_EMAIL=policy-ab@invalid`; arm A adds
    `SUBAGENT_MODEL_GUARD=off`. The `CoaiEnvironment.Minimal` predicate admits `Passes(name)` ∪ those fixed names, and admits
    `SUBAGENT_MODEL_GUARD` **only when `inputs.Arm == PolicyArm.A`** — so for B the name is excluded structurally, not by value.
    `.WithSecret(SecretValue.None)` as `ForScouts` does. A blank git-config path is made impossible rather than checked:
    `PolicyChildInputs.Of(arm, emptyGitConfigFile, drop) → Outcome<PolicyChildInputs>` refuses a blank or relative path, and
    `ForPolicyArm` takes only the validated record.
  - `PolicyPath.DirectoriesHolding(string pathValue, IReadOnlyList<string> executables)` (Infrastructure) — the PATH entries
    containing `gh` or `gh.exe`; the caller passes `["gh", "gh.exe"]`. I/O lives here, never in the domain.
  - The empty git-config file itself is E3's to create per cell (one empty file under the artefact root); this story only
    carries its path.
- **Test scenarios** (`PolicyChildEnvironmentTests`):
  - `Arm_B_never_carries_the_model_guard_even_when_the_parent_sets_it_off` — parent with `SUBAGENT_MODEL_GUARD=off` and with
    `subagent_model_guard=OFF` → B's `Names` has no such name in any case. RED: compile failure on `ForPolicyArm`.
  - `Arm_A_carries_the_model_guard_off_whatever_the_parent_holds` — parent absent / `on` / `off` → `off`.
  - `Neither_arm_carries_CLAUDECODE_a_session_variable_or_a_secret` — parent with `CLAUDECODE=1`, `CLAUDE_CODE_SSE_PORT=1`,
    `CLAUDE_CODE_ENTRYPOINT=cli`, `GITHUB_TOKEN`, `BENCH_DB` → none pass; `CLAUDE_CODE_DISABLE_BACKGROUND_TASKS=1` is the only
    `CLAUDE_CODE_*` name.
  - `Both_arms_carry_the_fixed_git_identity_and_no_credential_path` — the eight fixed variables with their values, a parent
    `GIT_CONFIG_GLOBAL=C:\\Users\\x\\.gitconfig` replaced.
  - `A_PATH_directory_holding_gh_is_dropped_and_every_other_entry_kept_in_order` — `Path` spelling included.
  - `PolicyChildInputs_refuses_a_blank_or_relative_git_config_path`.
  - `PolicyPath_finds_the_directories_holding_gh` — temp dirs with an empty `gh.exe` / `gh`.
  - **Break-it (parent §9):** *B's env keeping the parent's `off`* — plant: admit `ModelGuardVariable` for both arms in the
    predicate (and stop removing it from the parent); the first scenario goes red with the name present; restore; green.
- **Done when.** Scenarios and the break-it observed; `ProbeChildEnvironmentTests` green unedited.
- **Depends on.** Nothing.
- **Coder: sonnet.**

## E1.S4 — The overlay merge

- **Goal.** A pure function that turns a story's project settings into the arm's settings — the project's keys and hooks kept,
  the audit hook added for both arms, B's guard added for B alone — and a pure reading of where the conventions are mounted.
- **Scope.**
  - New: `src/Bench.Domain/Policy/PolicyOverlay.cs`, `src/Bench.Domain/Policy/PolicyMount.cs`.
  - Moved: `ScoutSubmodules` (pure, today in Infrastructure — `src/Bench.Infrastructure/Scouts/ScoutTrees.cs:374-400`, `Urls`
    at `:380`) to `src/Bench.Domain/Scouts/ScoutSubmodules.cs`, same members, so the domain can read `.gitmodules`
    (reuse-first move 2.4); `ScoutTrees` keeps calling it.
  - Tests: new `tests/Bench.Tests/Policy/PolicyOverlayTests.cs`, `PolicyMountTests.cs`; fixtures under
    `tests/Bench.Tests/Fixtures/policy/` — `settings-permissions-only.json` (creds at `9f62e4db`, verbatim:
    `defaultMode: acceptEdits`, allow `Edit, Write, NotebookEdit, Bash, PowerShell`), `settings-with-hooks.json` (coai's shape:
    a `SessionStart` `load-instructions.mjs` and a `PreToolUse` `Bash|PowerShell` `build-flags.mjs` entry),
    `conventions-ea3252f-settings.json` (a verbatim copy of `ea3252f:settings/settings.json`).
- **Brief.**
  - `PolicyOverlay.GuardEntry(string conventionsSettingsJson) → Outcome<string>` — the `hooks.PreToolUse` entry whose
    `matcher` is exactly `Agent|Task`, returned as its canonical JSON text; refused when absent, when there are two, or when
    none of its hook `args`/`command` strings ends with `subagent-model.mjs`.
  - `PolicyOverlay.Apply(string projectSettingsJson, PolicyArm arm, string guardEntryJson) → Outcome<string>` —
    `projectSettingsJson` is `"{}"` when the tree has no `.claude/settings.json`; `guardEntryJson` must be non-empty for B and
    is ignored for A. Result: the project object with every existing key in its original order and each existing value
    deep-equal; `hooks.PreToolUse` = the project's entries, then `{"matcher": "*", "hooks": [AUDIT]}`, then
    `{"matcher": "Agent|Task", "hooks": [EFFORT_PIN]}` (both arms, parent D15), then (B) the guard entry;
    `hooks.InstructionsLoaded` = the project's entries, then `{"hooks": [AUDIT]}` (no matcher). `AUDIT` =
    `{"type": "command", "command": "node", "args": ["${CLAUDE_PROJECT_DIR}/.claude/hooks/policy-audit.mjs"]}` and `EFFORT_PIN`
    the same with `effort-pin.mjs` — the `command` + `args` form B's guard uses. Canonical output (indented, LF, trailing newline).
  - Refusals (`Outcome.Fail`, each naming the cause): the project settings already register a hook whose strings name
    `subagent-model.mjs`, `policy-audit.mjs` or `effort-pin.mjs` ("the project already wires a model guard / audit / effort pin —
    the arms would differ by more than the overlay"); the project settings name `effortLevel`/`modelSettings` (defence in depth
    behind S1); unparseable JSON; `hooks` or a hook event that is not the expected JSON kind.
  - `PolicyOverlay.Targets(string mount, PolicyArm arm) → IReadOnlyList<string>` — the tree-relative paths the overlay writes:
    `<mount>/common/subagent-models.md`, `<mount>/common/planning-docs.md`, `.claude/hooks/policy-audit.mjs`,
    `.claude/hooks/effort-pin.mjs`, (B) `.claude/hooks/subagent-model.mjs`, `.claude/settings.json`. E3.S3 drops candidate
    sections touching exactly these paths, and E2.S4 stops a cell that edits one of them mid-run (parent D12).
    `ensure-user-guard.mjs` is never among them.
  - `PolicyMount.Resolve(string gitmodulesText) → Outcome<string>` — the submodule whose url's last path segment is
    `dew_flow_conventions` or `dew_flow_conventions.git` (case-insensitive). None → *"the tree carries no dew_flow_conventions
    submodule"*; `.agents/conventions` → *"the resolver layout is series-only (plan §12)"*; any other path but
    `.claude/rules/shared` → refusal naming it; else the path.
- **Test scenarios** (`PolicyOverlayTests`, `PolicyMountTests`):
  - `Arm_A_over_a_permissions_only_tree_adds_exactly_the_two_audit_entries_and_the_effort_pin_entry` — RED: compile failure on
    `PolicyOverlay`.
  - `Arm_B_over_a_permissions_only_tree_adds_the_audit_and_effort_pin_entries_and_one_Agent_Task_guard_entry`.
  - `The_effort_pin_entry_is_byte_identical_in_both_arms`.
  - `Both_arms_over_a_tree_without_settings_create_the_file_with_only_the_overlay`.
  - `Both_arms_over_a_tree_with_hooks_keep_every_project_key_and_hook_byte_for_byte_and_append_after_them` — each pre-existing
    node's `ToJsonString()` identical before and after; project entries first.
  - `The_merge_is_byte_stable_across_two_applications`.
  - `A_project_that_already_wires_a_guard_or_the_audit_hook_is_refused`.
  - `The_guard_entry_is_read_from_the_conventions_settings_and_a_missing_or_doubled_one_is_refused`.
  - `The_overlay_targets_never_include_ensure_user_guard`.
  - `PolicyMountTests`: creds `.gitmodules` (path `.claude/rules/shared`, url `…/dew_flow_conventions.git`) → the path;
    coai's `.agents/conventions` → the series-only refusal; no conventions submodule → refusal; `tools/conventions` → refusal
    naming it.
  - **Break-it (parent §9):** *the overlay merge dropping the project's own hooks* — plant: build `hooks.PreToolUse` from the
    overlay entries alone; the byte-for-byte scenario goes red naming the lost `build-flags.mjs` entry; restore; green.
- **Done when.** Scenarios and the break-it observed; `ScoutTreesTests` green after the `ScoutSubmodules` move;
  `PolicyOverlay`, `PolicyMount`, `ScoutSubmodules` in the architecture list.
- **Depends on.** Nothing (fixtures stand in for the conventions text).
- **Coder: sonnet.**

## E1.S5 — The hook scripts: the audit hook and the effort pin

- **Goal.** `prompts/policy-ab/policy-audit.mjs`, the observe-only hook both arms carry (parent §3.6): one JSON line per
  `PreToolUse` and `InstructionsLoaded` event into the cell tree's `.git/policy-audit.ndjson`, never a print, never a block. And
  `prompts/policy-ab/effort-pin.mjs` (parent D15), the one deny hook both arms carry identically: an Agent call whose own
  `effort` argument is present and not `high` is denied.
- **The effort pin, briefly** (same constraints as the audit hook below: ES module, `node:` imports only, no environment, a 5 s
  stdin deadline): read the `PreToolUse` payload; when `tool_name` is `Agent` or `Task` and `tool_input.effort` is a string other
  than `high` (ordinal), print the hook decision that denies the call with a reason opening `policy-effort-pin:` (the same
  `permissionDecision`/`permissionDecisionReason` shape B's guard prints, `ea3252f:settings/hooks/subagent-model.mjs:126`); in
  every other case — no `effort`, `effort: "high"`, another tool, unparseable stdin — print nothing and exit 0. It never reads the
  audit log. Tests in `tests/Bench.Tests/Policy/PolicyEffortPinHookTests.cs`, run as S5's audit-hook tests run:
  `An_agent_call_with_effort_xhigh_is_denied_with_the_marker` (RED: no script) · `An_agent_call_with_effort_high_or_without_effort_is_allowed`
  · `Another_tool_with_an_effort_key_is_ignored` · `Malformed_stdin_allows_and_prints_nothing`. Break-it: deny on ANY present
  `effort` (the `!== "high"` comparison dropped) → the `effort: "high"` row goes red; restore. The live proof is E3.L1 (the
  probe's step 3).
- **Scope.** New `prompts/policy-ab/policy-audit.mjs`, `prompts/policy-ab/effort-pin.mjs`; new
  `tests/Bench.Tests/Policy/PolicyAuditHookTests.cs`, `PolicyEffortPinHookTests.cs`. Launch through
  `ProcessRunner.RunAsync(exe, argv, cwd, timeout, stdin, environment, ct)` (the overload `ScoutTrees` uses,
  `src/Bench.Infrastructure/Scouts/ScoutTrees.cs:285`). The payload shape is recorded in the conventions' model-tiers E2.S1.0
  (2026-10-13): PreToolUse keys `session_id`, `transcript_path`, `cwd`, `scratchpad_dir`, `prompt_id`, `permission_mode`,
  `effort` (`{"level":"high"}`), `hook_event_name`, `tool_name`, `tool_input`, `tool_use_id`; a child's call also carries
  `agent_id` and `agent_type`. InstructionsLoaded keys (parent §2): `session_id`, `transcript_path`, `cwd`, `scratchpad_dir`,
  `hook_event_name`, `file_path`, `memory_type`, `load_reason`.
- **Brief.**
  - ES module, Node ≥ 20, imports only `node:fs`, `node:crypto`, `node:path`, `node:url`. Reads no environment variable.
  - **The log path comes from the script's own location**, not the process cwd and not the payload:
    `resolve(dirname(fileURLToPath(import.meta.url)), '..', '..', '.git', 'policy-audit.ndjson')`. (The parent §3.6 says "from its
    own cwd"; a hook's cwd can follow the session's `cd`, the script's location cannot — this is the stricter reading of the
    same intent.) No `.git` directory there → exit 0, write nothing.
  - Stdin: read to end with a 64 MiB limit and a 5 s deadline (a `Write` payload carries a whole file).
  - Line fields: `ts` (UTC ISO-8601), `event` (= `hook_event_name`), `session_id`; when present `agent_id`, `agent_type`,
    `tool_name`, `tool_use_id`; `effort` copied VERBATIM from the payload when present (E2 reads `.level`); for
    `InstructionsLoaded` also `file_path`, `memory_type`, `load_reason`, and `sha256` = lower-hex SHA-256 of the file's BYTES —
    `""` plus `readError` when it cannot be read. `tool_input` is never logged (size; E2 reads commands from the stream).
  - Unparseable or oversize stdin → one line `{"ts", "event": "unparsed", "bytes": n}`. An unknown event → logged with its
    `event` and the common fields.
  - One `appendFileSync` per line (one write call, line < 4 KiB — atomic append for parallel hooks). Every path — including an
    append failure — ends `process.exit(0)` with nothing on stdout or stderr.
  - LF line endings in the committed file; S6 normalises to LF again before writing it into a tree (the byte pin must not
    depend on a checkout's `core.autocrlf`).
- **Test scenarios** (`PolicyAuditHookTests`, each in a temp tree `<t>/.claude/hooks/policy-audit.mjs` + `<t>/.git/`, run with
  a minimal environment of `PATH` and `SystemRoot` only):
  - `A_main_session_tool_call_logs_its_event_tool_and_effort_and_prints_nothing` — RED: the script does not exist.
  - `A_child_tool_call_logs_its_agent_id_and_agent_type`.
  - `An_instructions_loaded_event_logs_the_file_and_the_sha256_of_its_bytes` — equals .NET `SHA256.HashData(File.ReadAllBytes)`.
  - `An_unreadable_instructions_file_logs_an_empty_hash_and_the_read_error`.
  - `Malformed_stdin_logs_an_unparsed_line_and_exits_zero`.
  - `Run_from_another_working_directory_the_hook_still_writes_into_its_own_tree`.
  - `Without_a_git_directory_the_hook_writes_nothing_anywhere_and_exits_zero`.
  - `Two_events_append_two_lines` — and the effort object is byte-for-byte the payload's.
- **Done when.** Scenarios green on this machine (node v24.18.0) and on the CI runner's node.
- **Depends on.** Nothing.
- **Coder: sonnet.**

## E1.S6 — Arm sources and the overlay writer

- **Goal.** Each arm's bytes come from the conventions mirror at its sha, verified against git's own blob ids, and one writer puts
  an arm's overlay into a given tree and answers the byte hash of every file it wrote. `arms.json`'s record shape lives here.
  The cell tree itself — fresh copy, one commit, restore, clean status, pins re-checked — is **E3.S1's**, which calls this writer
  (E3.S1 step 5); this story builds no cell tree.
- **Scope.**
  - Reused: `GitCheckoutProvider.EnsureMirrorAsync` (`src/Bench.Infrastructure/Git/GitCheckoutProvider.cs:88`); the archive
    flags of `ScoutTrees.ExtractAsync` (`src/Bench.Infrastructure/Scouts/ScoutTrees.cs:176`: `-c core.autocrlf=false -c
    core.eol=lf archive --format=zip`); `GitCommand.ReadAsync` (`src/Bench.Infrastructure/Git/GitHistory.cs:35`);
    `PolicyOverlay.Apply` / `Targets` / `GuardEntry` (S4).
  - New: `src/Bench.Domain/Policy/GitBlobId.cs`, `src/Bench.Domain/Policy/PolicyArmsFile.cs`,
    `src/Bench.Infrastructure/Policy/PolicyArmSources.cs`, `src/Bench.Infrastructure/Policy/PolicyOverlayWriter.cs`.
  - Tests: `tests/Bench.Tests/Policy/PolicyArmSourcesTests.cs`, `PolicyOverlayWriterTests.cs`, `GitBlobIdTests.cs`,
    `PolicyArmsFileTests.cs` (local repositories through `DatedGitRepo`, `tests/Bench.Tests/Infrastructure/DatedGitRepo.cs:16`).
- **Brief.**
  - `GitBlobId.Of(ReadOnlySpan<byte> bytes) → string` — SHA-1 over `"blob <len>\0" + bytes`, lower hex: the id
    `git rev-parse <sha>:<path>` prints. Pure.
  - `PolicyArmSources.LoadAsync(RepoUrl conventions, CommitSha sha, PolicyArm arm, ct) → Outcome<PolicyArmSource>` — mirror via
    `EnsureMirrorAsync(MeasurementTarget.At(conventions, sha))`; extract `common/subagent-models.md`, `common/planning-docs.md`
    and (B) `settings/hooks/subagent-model.mjs`, `settings/settings.json` with `git archive` (autocrlf pinned off) into a temp
    folder removed on every path out; each file's `GitBlobId.Of(bytes)` must equal `git rev-parse <sha>:<path>`, else refuse
    naming the path (an encoding drift is a different rule text). B: refuse a guard script holding a line that starts with
    `import ` (it is copied alone into `.claude/hooks/`; an import would break it at a later sha); the guard entry is
    `PolicyOverlay.GuardEntry` over the settings text. Records: `PolicyArmSource(PolicyArm Arm, CommitSha Sha,
    IReadOnlyList<PolicySourceFile> Files, string GuardEntryJson)`, `PolicySourceFile(string SourcePath, ReadOnlyMemory<byte>
    Bytes, string Sha256)` — never a mutable shared array.
  - `PolicyOverlayWriter.Write(string treeRoot, string mount, PolicyArmSource arm, ReadOnlyMemory<byte> auditHook) →
    Outcome<IReadOnlyList<PolicyPinnedFile>>` with `PolicyPinnedFile(string TreePath, string Sha256)`:
    the rule files to `<mount>/common/` (A writes them even where the bytes are already identical), the audit hook — the bytes of
    `prompts/policy-ab/policy-audit.mjs` with CRLF normalised to LF (one helper, `PolicyOverlayWriter.Lf(bytes)`) — to
    `.claude/hooks/policy-audit.mjs`, the effort pin (same LF rule) to `.claude/hooks/effort-pin.mjs`, (B) the guard to
    `.claude/hooks/subagent-model.mjs`, and `.claude/settings.json` =
    `PolicyOverlay.Apply` over the tree's file (or `"{}"`). Writes exactly `PolicyOverlay.Targets(mount, arm)` — nothing else,
    `ensure-user-guard.mjs` never — refuses a target that resolves outside `treeRoot` or through a reparse point, then re-reads
    each written file and answers its BYTE SHA-256 (lower hex). No commit, no restore: those are E3.S1's steps 6 and 8, and the
    order matters there (`InitOneCommitAsync` adds with `-f`, `ScoutTrees.cs:279`, so a restore before the commit would commit
    `node_modules`).
  - `PolicyArmsFile` (pure): the `arms.json` record, canonical writer and reader (round trip). Shape: `{"conventionsRepository",
    "arms": [{"arm": "A"|"B", "conventionsSha", "files": [{"source", "target", "sha256"}], "guardEntry": {…} (B only)}],
    "auditHook": {"source": "prompts/policy-ab/policy-audit.mjs", "target", "sha256"}, "effortPin": {"source":
    "prompts/policy-ab/effort-pin.mjs", "target", "sha256"}, "pinnedSettings": {"file", "sha256"},
    "permissions": {"source", "sha256"}, "overlays": [{"story", "arm", "mount", "files": [{"target", "sha256"}]}]}` — the
    `overlays` rows are the writer's manifest per story × arm, which E3.S1 step 7 compares a cell's files against. Absent
    B-only fields are absent keys in JSON and empty values in C# (no null).
- **Test scenarios:**
  - `The_writer_puts_exactly_the_arm_targets_into_a_tree_and_answers_their_byte_hashes` — both arms over a fixture tree with a
    `.claude/rules/shared` submodule copy; RED: compile failure on `PolicyOverlayWriter`.
  - `Arm_B_alone_carries_the_guard_and_no_tree_ever_carries_ensure_user_guard` — the fixture conventions repository DOES hold
    `settings/hooks/ensure-user-guard.mjs`; nothing under `.claude/hooks/` names it.
  - `Arm_A_writes_its_rule_files_even_where_the_tree_already_holds_identical_bytes` — the manifest lists them; hashes equal the
    source hashes.
  - `Two_writes_of_one_arm_into_two_trees_answer_identical_manifests`.
  - `A_CRLF_audit_hook_is_written_as_LF_and_hashes_like_the_LF_file`.
  - `Sources_are_read_byte_exact_from_the_mirror_at_each_sha` — a local "conventions" repository with the two files at two
    commits; A at the first, B at the second.
  - `A_source_whose_bytes_do_not_match_their_git_blob_id_is_refused` and the `GitBlobId` table: empty bytes →
    `e69de29bb2d1d6434b8b29ae775ad8c2e48c5391`; `"hello\n"` → `ce013625030ba8dba906f756967f9e9ca394464a`; a CRLF variant ≠ LF.
  - `A_guard_script_with_an_import_is_refused`.
  - `PolicyArmsFile_round_trips_and_writes_canonical_bytes`.
- **Done when.** Scenarios green; `GitBlobId`, `PolicyArmsFile` in the architecture list; the writer's signature is the one
  E3.S1 calls.
- **Depends on.** S2 (the pinned-settings hash for `arms.json`), S4 (`Apply`, `Targets`, `GuardEntry`), S5 (both hooks' bytes).
- **Coder: sonnet.**

## E1.S7 — The twice-run baseline (over E4.S1's junit reader)

- **Goal.** The pure rule that turns two baseline runs at P into stable-green / failing / flaky / skipped, so `prepare` can list
  the tests the neighbour signal leaves out (parent §3.2 "Gates at P"). The junit reader itself is **E4.S1's `NodeTestReport`**
  (with `NodeTestCase`, `NodeTestOutcome`, `TestPathMap`) — one reader, never two. If E1 reaches this story before E4.S1 has
  landed, the coder builds `NodeTestReport` here **exactly as E4.S1's brief specifies** (its fixture recipe and its tests
  included) and E4.S1 then reuses it; the shape is not re-decided here.
- **Scope.** New `src/Bench.Domain/Policy/PolicyBaseline.cs`; tests `tests/Bench.Tests/Policy/PolicyBaselineTests.cs`. Reads
  E4.S1's `NodeTestReport` (`src/Bench.Domain/Policy/NodeTestReport.cs`).
- **Brief.**
  - Identity is E4.S1's: (`TestFile`, `SuitePath`, `Name`). `PolicyBaseline.Of(NodeTestReport first, NodeTestReport second)
    → PolicyBaseline` with one `PolicyBaselineEntry(string TestFile, string SuitePath, string Name, PolicyBaselineState State,
    string Reason)` per identity seen in either run; `PolicyBaselineState { StableGreen, Failing, Flaky, Skipped,
    NotComparable }`: passed twice → stable green; failed (or cancelled) twice → failing; one pass and one fail, or present in
    one run only → flaky; skipped or todo in both → skipped; skipped/todo in one and run in the other → flaky; an identity in
    either report's `Duplicates` → not comparable (E4.S1: "consumers treat a duplicate as not comparable, never pick one").
  - `Excluded` = failing ∪ flaky ∪ not comparable, each with its reason — what the neighbour signal leaves out, and what E4.S4
    reads from `stories.json`'s `baseline.excluded`.
- **Test scenarios** (a table over hand-built `NodeTestReport` values — no XML needed here):
  - `Two_passes_are_stable_green_and_two_failures_are_failing` — RED: no `PolicyBaseline`.
  - `A_pass_and_a_failure_in_either_order_is_flaky`.
  - `A_test_present_in_one_run_only_is_flaky`.
  - `Skipped_twice_is_skipped_and_skipped_once_is_flaky`.
  - `A_cancelled_test_counts_as_a_failure`.
  - `A_duplicate_identity_is_not_comparable_and_excluded`.
- **Done when.** Scenarios green; `PolicyBaseline` in the architecture list; `NodeTestReport` exists once in the solution.
- **Depends on.** E4.S1's `NodeTestReport` (or builds it to E4.S1's spec, see Goal).
- **Coder: sonnet.**

## E1.S8 — Reference facts from the mirror

- **Goal.** What `prepare` reads out of the story's bare mirror without a `ScoutCandidate` in disguise: the fix's lineage, the
  files P..S changed (with their status), the added lines per file, and P's identifiers and normalised text.
- **Scope.**
  - `ScoutHistory` — `src/Bench.Infrastructure/Scouts/ScoutHistory.cs` (`FactsAsync` `:15`, `LineageAsync` `:42` private,
    `AddedLinesAsync` `:59`, `MirrorAsync` `:75`).
  - `ScoutParentCheck.Refusal` — `src/Bench.Domain/Scouts/ScoutParentCheck.cs:30` (`Linear` `:38`).
  - `IdentifiersAt` — `hosts/Cli/ScoutsTasksCommand.cs:314` (private static; reads tree files: ≤ 2 MB, NUL in the first 8 KB =
    binary), used at `:202`.
  - New: `src/Bench.Domain/Policy/UnifiedDiff.cs`, `src/Bench.Infrastructure/Scouts/ScoutTreeText.cs`.
  - Tests: `tests/Bench.Tests/Scouts/` (existing history / parent-check / tasks tests, unedited), new
    `tests/Bench.Tests/Policy/UnifiedDiffTests.cs`, `tests/Bench.Tests/Scouts/ScoutTreeTextTests.cs`,
    `tests/Bench.Tests/Scouts/ScoutHistoryLineageTests.cs`.
- **Brief.**
  - `ScoutParentCheck.Refusal(ScoutMergeStyle style, CommitSha parent, ScoutBaseRef baseRef, ScoutParentFacts facts)` — the
    overload the candidate overload now delegates to (behaviour identical).
  - `ScoutHistory`: `MirrorAsync(string repository, CommitSha at, ct)` (the candidate form delegates);
    `public LineageAsync(string repository, CommitSha fix, ScoutMergeStyle style, ScoutBaseRef baseRef, ct)` (the private one
    delegates); `public ChangedFilesAsync(string repository, CommitSha parent, CommitSha fix, ct) →
    Outcome<IReadOnlyList<ChangedFile>>` from `git diff --name-status --no-renames P S` with `ChangedFile(char Status, string
    Path)`; `public AddedLinesByFileAsync(string repository, CommitSha parent, CommitSha fix, ct) →
    Outcome<IReadOnlyDictionary<string, IReadOnlyList<string>>>` over `git diff --no-color --unified=0 P S`, parsed by the pure
    `UnifiedDiff.AddedLinesByFile(string diff)` (`+++ b/<path>` opens a file, `+++ /dev/null` a deletion; `+` lines that are
    not headers are added lines). The existing `AddedLinesAsync` keeps its output.
  - Move `IdentifiersAt` to `ScoutTreeText.Identifiers(ScoutTree tree) → IReadOnlySet<string>` (Infrastructure, same file
    filter); `ScoutsTasksCommand` calls it. Add `ScoutTreeText.Phrases(ScoutTree tree) → Func<string, bool>` — whether a
    normalised phrase (lower-case, whitespace runs collapsed to one space) occurs in any text file of the tree, same filter;
    build the normalised texts once.
- **Test scenarios:**
  - `The_parent_check_overload_answers_what_the_candidate_overload_answers` — the existing table driven through both. RED:
    compile failure on the overload.
  - `Lineage_changed_files_and_added_lines_per_file_are_read_from_a_bare_mirror` — a `DatedGitRepo` with a squash-shaped commit
    (one parent), an added file, a modified file, a deleted file.
  - `UnifiedDiff`: a table — two files, a deletion, a file with a `+++` inside its content line (`++++x` is content), CRLF input.
  - `Tree_identifiers_and_phrases_skip_binary_and_oversized_files`.
  - The scouts tasks tests stay green unedited (the move is invisible to them).
- **Done when.** Scenarios green; `--filter-namespace "Bench.Tests.Scouts"` green unedited; `UnifiedDiff` in the architecture
  list.
- **Depends on.** Nothing.
- **Coder: sonnet.**

## E1.S9 — The frozen statement and the reference-only tokens (pure)

- **Goal.** The spec section exactly as it stood at P, the scope note leak-checked, the statement hashed; and the tokens only
  the reference could have handed a candidate (parent §3.2, used post hoc by E2).
- **Scope.** `LeakCheck.Find` and `ContainsWord` — `src/Bench.Domain/Scouts/LeakCheck.cs:30`, `:50`;
  `ScoutGroundTruth.AddedIdentifiers` / `Identifiers` — `src/Bench.Domain/Scouts/ScoutCandidates.cs:203-207`. New
  `src/Bench.Domain/Policy/PolicySpecSection.cs`, `PolicyStatement.cs`, `PolicyReferenceOnly.cs` (and E4.S1's
  `PolicyDiffFilter.IsDocPath`, see below); tests
  in `tests/Bench.Tests/Policy/`.
- **Brief.**
  - `PolicySpecSection.Extract(string markdown, string heading) → Outcome<string>` — LF-normalised; the one line that starts
    with `heading` (refused when none or more than one), through the line before the next heading of the same or a higher level
    (`#`..`###` for a `###` heading); trailing blank lines trimmed.
  - `PolicyStatement.Freeze(string section, string scopeNote, IReadOnlyList<string> holdoutFiles, IReadOnlyList<string>
    addedIdentifiers, PolicyModelLines modelLines) → Outcome<PolicyStatement>` with `enum PolicyModelLines { Keep, Strike }`:
    - `Strike` replaces every whole-word `Opus`, `Sonnet`, `Fable`, `Haiku` in the SECTION with `[model withheld]` before
      anything else (deterministic; the spec file in the tree keeps P's bytes); `Keep` leaves the section as it is.
    - `Text` = `section.TrimEnd() + "\n\n---\n\nScope note (written for this re-run, from the text above only): " +
      scopeNote.Trim() + "\n"`; `Hash` = `StableHash.Of(Text)`.
    - `AllowedLeaks` = `LeakCheck.Find(section, holdoutFiles, addedIdentifiers)` — names the spec itself carried at P; listed,
      never refused.
    - Refused when `LeakCheck.Find(scopeNote, …)` finds a token NOT among `AllowedLeaks` (the scope note is the main session's
      text, written after the fact); the refusal names every such token. A blank scope note is refused.
    - `ModelMentions` = each line of the ORIGINAL section and of the scope note containing, as a whole word (`LeakCheck.ContainsWord`), `Opus`, `Sonnet`, `Fable`,
      `Haiku`, `xhigh` or `effort` — recorded, never refused; with `Strike` this list IS the strike list committed with the
      statement. (The pilot's spec says `(Opus)` in its E1 heading and **Model: Opus** on T1 and T2; the parent's D14 decided:
      struck, identically for both arms — they were the then-policy's role assignment, not story content.)
  - `PolicyStatement.CheckText(string text, PolicyStatement statement, holdoutFiles, addedIdentifiers) → Outcome<bool>` — the
    same rule for any other text derived from the statement (the checklist, S10): tokens beyond `AllowedLeaks` refuse.
  - The doc-path rule is E4.S1's (`PolicyDiffFilter`: under `todo/` or `research/`, ends `.md`, or `CHANGELOG.md` at any
    depth — parent §3.7), exposed as `public static bool PolicyDiffFilter.IsDocPath(string path)` so its file-section filter
    and this story share ONE rule. If E4.S1 has not landed, add `IsDocPath` to a `PolicyDiffFilter` stub with exactly that rule
    and E4.S1 builds the rest around it.
  - `PolicyReferenceOnly.Of(IReadOnlyDictionary<string, IReadOnlyList<string>> addedLinesByFile, IReadOnlySet<string>
    identifiersAtParent, Func<string, bool> phraseAtParent, string statementText) → PolicyReferenceOnly` over non-doc files only:
    - `Identifiers` = `ScoutGroundTruth.AddedIdentifiers(lines, identifiersAtParent)` minus every identifier that occurs as a
      whole word in `statementText`; each with the first file it came from.
    - `Phrases` = from each added line, the comment text — a trimmed line starting with `//`, `/*` or `*` (marker and a closing
      `*/` stripped), or the part after `//` when the code before it holds no quote character — normalised (lower-case,
      whitespace collapsed); kept when it has ≥ 6 words (a word holds a letter or digit) and neither `phraseAtParent(phrase)` nor
      the normalised statement contains it; distinct, ordered.
- **Test scenarios:**
  - `The_spec_section_runs_from_its_heading_to_the_next_heading_of_its_level` — with the pilot's shape: `### E1 — Foundations…`
    through the line before `### E2 — The doors…`; a `####` inside stays. RED: no extractor.
  - `A_missing_or_doubled_heading_is_refused`.
  - `A_name_the_spec_carried_at_P_is_allowed_and_listed` — `slotTable.test.ts` in the section, as a hold-out basename.
  - `A_hold_out_name_or_added_identifier_only_the_scope_note_carries_is_refused_by_name`.
  - `Model_and_effort_words_in_the_statement_are_recorded_line_by_line`.
  - `Striking_model_lines_withholds_every_model_name_and_keeps_the_mentions_on_record`.
  - `The_statement_hash_is_stable_and_changes_with_one_character_of_the_scope_note`.
  - `A_reference_only_identifier_is_one_the_change_added_that_the_statement_does_not_name`.
  - `A_reference_only_phrase_is_a_six_word_comment_absent_from_P_and_the_statement` — five words → none; present at P → none;
    a URL string containing `//` in code → not a comment.
  - `Added_lines_in_doc_paths_never_yield_reference_only_tokens`.
- **Done when.** Scenarios green; the three types in the architecture list.
- **Depends on.** Nothing (E4.S1's `IsDocPath`, or its stub).
- **Coder: sonnet.**

## E1.S10 — The hold-out set, the checklist, the classification, `stories.json`

- **Goal.** The judgement inputs the main session writes are validated mechanically, and the whole story record has one shape,
  one writer and one reader that E2–E5 consume.
- **Scope.** New `src/Bench.Domain/Policy/PolicyStoryInput.cs` (the input file), `PolicyHoldout.cs`, `PolicyChecklist.cs`,
  `PolicyStoriesFile.cs`; tests in `tests/Bench.Tests/Policy/`.
- **Brief.**
  - **The input file** `research/policy-ab/inputs/<story>.json` (committed; its hash recorded in `stories.json` — a small
    deviation from parent §3.12's list: the judgement inputs need a committed source for their hashes). Fields: `story` (a
    `ScoutTaskId`-shaped slug), `repository`, `parent`, `fix`, `mergeStyle` (`pr-squash` only in the pilot — others refused
    *"series-only (plan §12)"*), `number`, `baseRefOid` (must equal `parent`), `prCommits` (full shas),
    `spec: {file, heading}`, `scopeNote`, `statementModelLines` (`strike` — parent D14; `keep` refused in the pilot),
    `testGlobs` (the story's test paths, pilot `src_vs_code/src/test/**` — read by E4.S4; empty refused), `workingDirectory`,
    `gates: {restore, typecheck, compile, test, harvestBuild, harvestTest}` (semicolon-joined
    `GateCommand`s, `src/Bench.Infrastructure/Git/HarvestGates.cs:10` — parsed in the verb, stored as strings here; `test`
    carries the `{report}` placeholder S12 fills; every command an executable + arguments, never a shell string — parent D21),
    `compiled: {sourceRoot, outRoot, sourceExt, outExt}`, `testSuffix` (`.test.ts`), `testInfrastructure` (paths; a path ending
    `/` is a prefix), `requirements: [{id, text, verify, anchors?}]` (`anchors`: optional names that mark the code bearing on the
    item — E4.S8's bearing ranges), `classification: [{file, test, behaviourImplied,
    interfaceImplied, reason}]`. `PolicyStoryInput.Read(json) → Outcome<PolicyStoryInput>` refuses a missing or malformed field
    by name.
  - `PolicyHoldout.Files(IReadOnlyList<ChangedFile> changed, string testSuffix, IReadOnlyList<string> infrastructure)` —
    changed test files (status `A` or `M`, path ending with `testSuffix`) minus infrastructure; infrastructure returned apart.
    The pilot answers `src_vs_code/src/test/slotTable.test.ts`, `src_vs_code/src/test/syncPinRule.test.ts`; `typedFixtures.test.ts`
    and `fixtures/typed/` are infrastructure (parent §3.2).
  - `PolicyHoldout.Tests(file, NodeTestReport atS, NodeTestReport atP, classification)` (E4.S1's report; ids by its
    (`TestFile`, `SuitePath`, `Name`), the source path through its `TestPathMap`) →
    the hold-out tests of one file: every test at S whose id is absent at P MUST be classified; a classified test must exist at
    S; a test present at both and unclassified is a NEIGHBOUR. Refusals name the test. `SpecImplied` = behaviour ∧ interface;
    a beyond-spec entry with a blank `reason` is refused. (Same-name tests whose body changed are the main session's to list —
    the diff is evidence, not a rule here.)
  - `PolicyChecklist.Of(requirements, testInfrastructure)` — ids exactly `R1…Rk` in order, k ≥ 1, non-blank `text` and `verify`;
    every test-infrastructure path's basename (or last directory name for a prefix) appears in some item's `text` or `verify`
    (parent §3.2: infrastructure deliverables ARE R-items); hash = `StableHash.Of` the canonical JSON. The verb also runs
    `PolicyStatement.CheckText` over every item (S9).
  - `PolicyStoriesFile` — `research/policy-ab/stories.json` = `{"stories": [ … ]}`, each story: `frozen` (false until S12's
    gates ran), the input's identity and commits, `inputSha256`, `treeHash`, `mount`, `keepProjectSettings: true`, `workingDirectory`, `gates`, `statement:
    {text, hash, allowedLeaks, modelMentions}`, `checklist: {items, hash}`, `holdout: {files, infrastructure, tests: [{file,
    id, specImplied, behaviourImplied, interfaceImplied, reason}]}`, `baseline: {runs: 2, excluded: [{id, state}]}`,
    `harvest: [{file, redAtBase, redDetail, greenWithFix, greenDetail}]`, `referenceOnly: {identifiers, phrases}`,
    `reference: {changedFiles}`, `preparedOn` (yyyy-MM-dd, UTC). Writer: canonical, stories ordered by id, replacing a story's
    entry by id and keeping the others byte-identical. Reader for E2–E5.
  - The classification FIELDS follow E4.S3's cornerstone spec (the hold-out gate, Fable). If that spec changes them, this record
    follows it in the same PR; if E4's spec is not written when this story starts, build exactly the fields above.
- **Test scenarios:**
  - `The_pilot_shaped_change_yields_two_hold_out_files_and_lists_the_harness_as_infrastructure` — the creds #176 file list
    (16 paths, parent §2) as input. RED: no type.
  - `An_added_test_left_unclassified_is_refused_by_name`.
  - `A_classified_test_that_does_not_exist_at_S_is_refused`.
  - `A_test_present_at_P_and_S_and_unclassified_is_a_neighbour`.
  - `A_beyond_spec_test_without_a_reason_is_refused`.
  - `Checklist_ids_must_run_R1_to_Rk_and_every_infrastructure_deliverable_must_be_named`.
  - `A_non_pilot_merge_style_or_a_base_ref_other_than_P_is_refused`.
  - `The_stories_file_round_trips_and_rewriting_one_story_leaves_the_other_byte_identical`.
- **Done when.** Scenarios green; the types in the architecture list.
- **Depends on.** E4.S1 (`NodeTestReport`), S9 (`PolicyStatement.CheckText`); E4.S3's cornerstone spec (Fable) for the
  classification fields when it exists.
- **Coder: sonnet.**

## E1.S11 — `bench policy prepare`: facts, statement, records, `arms.json`

- **Goal.** One verb freezes a story from its input file and both arms from their shas — everything except running the
  story's own gates (S12).
- **Scope.** `hosts/Cli/Program.cs:92-118` (`Dispatch`, the `"scouts"` arm at `:113`; add `"policy" => PolicyCommand.RunAsync(…)`; `Help` `:438`); new
  `hosts/Cli/PolicyCommand.cs` (the noun: `prepare` only; E3/E4 add their sub-verbs to its switch, the `ScoutsCommand.cs`
  pattern `:32-54`), `hosts/Cli/PolicyPrepareCommand.cs`; exit codes as the scouts' (`ScoutsCommand.cs:17-20`: 0 produced ·
  3 environment · 4 configuration · 5 nothing produced). Tests `tests/Bench.Tests/Cli/PolicyPrepareCommandTests.cs`. **The
  catalogue (parent D20):** widen `ModuleTestsCatalogTests` (`tests/Bench.Tests/Cli/ModuleTestsCatalogTests.cs:61-66` reads
  sub-verbs from `ScoutsCommand.cs` only) to read the `"<verb>" =>` arms of `hosts/Cli/PolicyCommand.cs` too, and add the
  `bench policy prepare` row to `research/module_tests.md` in this story; E3 and E4 add theirs as each verb lands.
- **Brief.** Flags: `--input <research/policy-ab/inputs/x.json>`, `--arm-a <40-hex>`, `--arm-b <40-hex>`, `--trees-root
  <dir>`, `--artifact-root <dir>` (falls back to `BENCH_ARTIFACT_ROOT`), `--out <research/policy-ab>`, `--on <yyyy-MM-dd>`
  (default today, UTC). In order, each refusal naming what to fix:
  1. read the input (S10) → 4 on any defect; refuse `--trees-root` inside any git working tree and run
     `ScoutGrantGuard.Refusal(treesRoot, mirrorRoot, userSettingsFiles)` (`src/Bench.Infrastructure/Scouts/ClaudeGrants.cs:89`)
     → 4;
  2. lineage: `ScoutHistory.LineageAsync` + `ScoutParentCheck.Refusal` (S8) with `PrSquash`; `baseRefOid == parent`; the
     `prCommits` are recorded as given and only parsed (a squash's PR commits are not on `main`'s history, so git cannot
     verify them) → 4 on a refusal, 3 on a git failure;
  3. the frozen tree: `ScoutTrees` over `<trees-root>/stories` with `ScoutTreeSpec(story, repository, parent, []) {
     KeepProjectSettings = true }` (S1) → 3/4;
  4. `PolicyMount.Resolve` over the tree's `.gitmodules` (S4) → 4;
  5. changed files, added lines per file (S8); `ScoutTreeText.Identifiers`/`Phrases` over the tree; hold-out files (S10);
     reference-only tokens (S9); `ScoutGroundTruth.AddedIdentifiers` over all added non-doc lines for the leak check;
  6. the spec section from the TREE's copy of `spec.file` (it is P's text — the plan is not removed); the statement (S9); the
     checklist (S10) and its leak check;
  7. both arms: `PolicyArmSources.LoadAsync` (S6) for A and B from the mount's url; `PolicyArtifacts.PinnedSettings` (S2) with
     `prompts/policy-ab/permissions.json`; per arm, `PolicyOverlayWriter.Write` (S6) into a scratch folder holding only a copy of
     the tree's `.claude/settings.json` (removed by exact path afterwards) → the `overlays` row (target + byte hash per file);
     `PolicyArmsFile` written to `<out>/arms.json` (replacing this story's overlay rows, keeping others);
  8. `stories.json` written with this story's entry — `baseline`, `harvest` and `holdout.tests` stay EMPTY until S12 fills them,
     and the entry carries `"frozen": false` until then.
  - Re-running with identical inputs writes byte-identical files (no clock in the records except `--on`).
- **Test scenarios** (a fixture built from `DatedGitRepo`: a "story" repository with a `.claude/rules/shared` submodule from a
  local "conventions" repository holding the two rule files at two commits, a settings file with permissions only, a plan file
  with a `### E1 — …` section, and a squash-shaped fix commit adding a test file and a source file):
  - `Prepare_freezes_the_story_facts_and_both_arms_into_the_two_records` — RED: unknown verb `policy`.
  - `Prepare_twice_with_the_same_inputs_writes_byte_identical_records`.
  - `A_trees_root_inside_a_git_working_tree_is_refused_with_exit_4`.
  - `A_fix_with_two_parents_is_refused_as_not_a_squash`.
  - `A_scope_note_naming_a_hold_out_file_is_refused_naming_it_and_nothing_is_written`.
  - `A_story_whose_conventions_sit_at_agents_conventions_is_refused_as_series_only`.
  - `An_unknown_policy_sub_verb_names_the_known_ones`.
- **Done when.** Scenarios green; `bench policy prepare` listed in `bench help`.
- **Depends on.** S1, S4, S6, S8, S9, S10 (S2 and S5 through S6).
- **Coder: sonnet.**

## E1.S12 — `bench policy prepare`: the gates

- **Goal.** The story's own commands, run as its rules prescribe: the baseline twice at P (per-test, junit), the tests at S
  enumerated, and the harvest gates per hold-out FILE — red at P, green at S — so `stories.json` is frozen with the evidence
  parent §3.2 and §4 ask for.
- **Scope.** `HarvestGates.RunAsync` — `src/Bench.Infrastructure/Git/HarvestGates.cs:50` (one build + one test command, run in
  the scratch worktree's ROOT, whole-suite red/green; `Report` `:133-141` says when red is "by COMPILATION");
  `ScratchWorktree.AddAsync` / `RemoveAsync` — `src/Bench.Infrastructure/Git/ScratchWorktree.cs:13`, `:42`;
  `GateProcess.RunAsync` — `:71`; `PolicyPrepareCommand` (S11); S7's reader and baseline; S10's `PolicyHoldout.Tests`.
- **Brief.**
  - **Baseline at P, twice**: a copy of the frozen tree WITHOUT `.git` (`ScoutTrees.InitOneCommitAsync` on it so `git` behaves;
    under `<trees-root>/baseline-<story>`, removed by exact path at the end, on every path out); restore once; then per run:
    typecheck, compile, test-with-junit — the input's `gates.test` must carry `--test-reporter=junit
    --test-reporter-destination=<report>` (the verb substitutes `{report}` in the command with a fresh path under the artefact
    root; refuse a test command without `{report}`). Read both reports (E4.S1's `NodeTestReport.Read`) → `PolicyBaseline` (S7)
    → `baseline.excluded`. A
    typecheck or compile failure at P is an environment refusal (3) naming the step — the baseline must build.
  - **Tests at S**: a scratch worktree at `fix` from the mirror (`ScratchWorktree.AddAsync`), restore, compile, one junit run →
    the S results; `PolicyHoldout.Tests` per hold-out file against the P results → `holdout.tests` (refusals → 4).
  - **Harvest per hold-out file**: `HarvestGates.RunAsync(mirror, P, S, [file], build, test, timeout)` once per hold-out file,
    with `build` = the restore and `test` = the story's own `npm test`, both run from the worktree ROOT (where HarvestGates runs
    them) — for the pilot `npm.cmd;--prefix;src_vs_code;ci` and `npm.cmd;--prefix;src_vs_code;test`, carried in the input as
    `gates.harvestBuild` / `gates.harvestTest`. (`ProcessRunner` starts without a shell, so on Windows a bare `npm` is not found —
    `npm.cmd`, as E4's shared rules record; `npm test` then runs its own `tsc && node --test` script.) Record each `GateReport` (red detail included — "red by
    COMPILATION" is the expected shape for `slotTable.test.ts`, whose spec RED is TS2339). A hold-out file not red at P or not
    green at S → 4, naming it. This is per FILE: HarvestGates cannot say which test was red; the per-test picture is the
    baseline's and the S run's.
  - Then `stories.json`'s entry gets `baseline`, `holdout.tests`, `harvest` and `"frozen": true`. A gate timeout (`--gate-timeout-
    minutes`, default 30) per phase; the scratch worktree and baseline copy are removed on every path out.
  - Attended: `npm ci` reaches the network — this is the harness, never a model; no cell exists yet.
- **Test scenarios** (the S11 fixture, with gate commands that need no npm: restore/typecheck/compile = `node;--version`,
  test = `node;--test;--test-reporter=junit;--test-reporter-destination={report};tests/*.test.mjs` over plain `.mjs` tests):
  - `Prepare_runs_the_baseline_twice_and_lists_a_flaky_test_as_excluded` — a test that fails on its first run only (a marker
    file it creates). RED: `baseline` empty.
  - `A_test_failing_at_P_twice_is_excluded_as_failing`.
  - `The_hold_out_file_is_red_at_P_and_green_at_S_and_the_story_is_frozen`.
  - `A_hold_out_file_already_green_at_P_refuses_the_freeze_naming_it`.
  - `An_added_test_at_S_missing_from_the_classification_refuses_the_freeze`.
  - `A_test_command_without_the_report_placeholder_is_refused`.
  - `The_baseline_copy_and_the_scratch_worktree_are_gone_after_a_refusal`.
- **Done when.** Scenarios green; whole suite green.
- **Depends on.** S7 (and E4.S1's reader through it), S11.
- **Coder: sonnet.**

## E1.S13 — Freeze the pilot

- **Goal.** The parent's E1 Done-when, met on the real story: `stories.json` (creds #176, frozen), `arms.json`, `prices.json`
  with a Fable row — committed before any model runs.
- **Who.** The main session (Opus) writes every judgement input; no coder writes code here except the one small test below.
- **Steps.**
  1. **The judgement inputs**, in `research/policy-ab/inputs/creds-176.json`, from P's text only
     (`dew_flow_creds_for_devs · todo/PLAN_typed_stored_secrets.md:429-462` at `9f62e4db`):
     - the scope note (parent §3.2, verbatim intent: "epic E1 only — T1 and T2; E2/E3 are not in scope; E1 DoD's
       `review_plan`/`review_code`, the consultation and the PR are unavailable here — skip them and say so");
     - R1…Rk with "how to verify" — T1's table column, `RevisionSecrets` from `SMALL_FIELDS`, the snapshot walking the table,
       `SEALABLE_MAPS` asserted; T2's `StoredSecret`, the harness in `npm test`, the three fixtures with their expected TS2322 /
       compiles, the `tsconfig`/ESLint exclusions, the harness's teeth; "no behaviour change";
     - the two-axis classification of every test S added to `slotTable.test.ts` and `syncPinRule.test.ts`, blind to every
       candidate, **under the rules E4.S3's cornerstone spec sets** (do not classify before that spec exists);
     - the commands, exe + argv with no shell (so `npm.cmd` on Windows, `tsc` through node — the `package.json` scripts at P are
       `tsc -p ./ --noEmit`, `tsc -p ./` and `npm run compile && node --test "out/test/*.test.js"`):
       `gates.restore = npm.cmd;ci`, `typecheck = node;node_modules/typescript/bin/tsc;-p;./;--noEmit`,
       `compile = node;node_modules/typescript/bin/tsc;-p;./`,
       `test = node;--test;--test-reporter=spec;--test-reporter=junit;--test-reporter-destination=stdout;--test-reporter-destination={report};out/test/*.test.js`,
       `harvestBuild = npm.cmd;--prefix;src_vs_code;ci`, `harvestTest = npm.cmd;--prefix;src_vs_code;test`, `workingDirectory =
       src_vs_code`; `testInfrastructure = [src_vs_code/src/test/typedFixtures.test.ts, src_vs_code/src/test/fixtures/typed/]`.
  2. **The model lines in the statement — decided (parent D14): struck.** P's spec says `(Opus)` in its E1 heading and
     **Model: Opus** on T1 and T2; verbatim it would tell arm B's session to put the stories on Opus, against B's role table. The
     input carries `statementModelLines: strike`; the statement's `modelMentions` is the strike list, committed with it; the spec
     file in the tree stays P's bytes. `pilot.md` restates it.
  3. **`research/policy-ab/prices.json`** — a copy of `research/scout-calibration/prices.json` with a Fable row: the exact model
     id and its five prices read from the vendor's pricing page (`source` and `readOn` updated to that read), `cliRates` kept.
     E3's rules-probe compares the resolved `fable` id with this row; a mismatch is a pin stop there.
     Small edit (main session or sonnet, one file, ≤ 50 lines, bounded before starting): a test
     `tests/Bench.Tests/Policy/PolicyRecordsTests.cs` — `The_policy_price_table_parses_and_prices_a_fable_model` through
     `ScoutFiles.Prices` (`src/Bench.Domain/Scouts/ScoutFiles.cs:274`).
  4. **One attended `prepare`**: `bench policy prepare --input research/policy-ab/inputs/creds-176.json --arm-a 3df745a7ce9959c251c65dd688c16be0295e8724
     --arm-b ea3252f2bd88db1fc7e698c2c9a1a58ca7b916c0 --trees-root <outside every repository> --artifact-root <outside git>
     --out research/policy-ab`. Expected: two hold-out files, red at P (slotTable by COMPILATION — TS2339, the spec's own RED),
     green at S; the statement's `allowedLeaks` listing `slotTable.test.ts` and the names T2 carries; the arms' rule-file hashes
     for A equal the pin's own (parent §2: the pin `8355757` is byte-identical to `d88e293^`); `frozen: true`.
  5. **Commit** `research/policy-ab/{inputs/creds-176.json, stories.json, arms.json, prices.json}` and the test — before any cell
     of either arm (E3, E5).
- **Done when.** The four files committed with `frozen: true`; the price test green; the whole suite green;
  `node .claude/rules/shared/tools/plan-lifecycle.mjs` clean.
- **Depends on.** S12; E4.S3's cornerstone SPEC (Fable — the classification rules); E4's code is not needed beyond E4.S1.
- **Judge: opus (the main session)** — the scope note, R-items, classification and the model-line question are judgements; the
  one test is a small edit.

## Epic Definition of Done

- [ ] S1–S12 merged, each with its scenarios green and its break-its (S2, S3, S4) observed red-then-green and recorded.
- [ ] The scouts and probes suites green with no test edited; the architecture guard lists every new pure type.
- [ ] `research/policy-ab/stories.json` (creds #176, `frozen: true`, red-at-P / green-at-S per hold-out file, the baseline
      excluded list), `arms.json` and `prices.json` (with Fable) committed before any claude launch.
- [ ] `research/module_scouts.md` names each scouts widening (S1, S2, S3, S4's move, S8); `research/module_policy_ab.md` and the
      `research/module_tests.md` rows carry every E1 story (parent D20); `ModuleTestsCatalogTests` reads the `policy` sub-verbs.
- [ ] No cell tree, no claude launch, no network in a test.
