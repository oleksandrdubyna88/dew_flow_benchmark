# Module — Model arms: the launcher of the part-6 experiment, in C#

> Status: **E1.S2 built, 2026-10-29 — the two projects, their references and the architecture guard exist; every other section below is an empty heading owned by the story named in it.**
> Plan: [PLAN_model_arms_E1.md](../todo/PLAN_model_arms_E1.md) (stories) under [PLAN_model_arms.md](../todo/PLAN_model_arms.md) (the design). Nothing here launches a model yet.

## Purpose

The owner compares coding pipelines ("arms") of headless Claude Code sessions on one frozen plan in a target repository: three schemes
(C, CR, SRS) times three runs, every arm ending in the product's multi-vendor code gate. The harness that launched those runs was a
set of bash scripts with inline node and the experiment's facts as constants at the top of each script. This module is its port to C#,
a console of its own beside the benchmark, not part of it. It ports the RUN half only (launch, resume, limits, the gate step, the
chain, isolation by file modes, the objective checks); the analysis half is a later plan. Its behaviour is the shell harness's, its
facts live in one refusing experiment file, and it is built so a decision can be tested without starting a process.

## Projects and the guard

```mermaid
flowchart LR
    exe["tools/model-arms/ModelArms<br/>console: model-arms<br/>files · processes · clock · environment"]
    core["tools/model-arms/ModelArms.Core<br/>decisions: PURE, facts arrive as parameters"]
    app["Bench.Application<br/>ports"]
    dom["Bench.Domain"]
    infra["Bench.Infrastructure"]
    host["Bench.Hosting"]

    exe --> core
    exe --> infra
    exe --> host
    core --> app
    core --> dom
```

| Project | Role | References |
|---|---|---|
| `tools/model-arms/ModelArms.Core` (`ModelArms.Core`) | Every decision of the module: the experiment's facts and their refusals, paths, the arm map, the launch options, the briefs. **Pure by construction** (plan D1): opens no file, starts no process, reads no clock, no environment. | `Bench.Domain`, `Bench.Application`; no packages |
| `tools/model-arms/ModelArms` (assembly `model-arms`) | The only project that touches the world. Today a stub `Program.Main` that answers the configuration exit code so the executable links. | `ModelArms.Core`, `Bench.Infrastructure`, `Bench.Hosting` |

The console also carries two content globs, so no later story edits its project file to ship a file: `tools/model-arms/briefs/*.template.md`
and `tools/model-arms/examples/*.json` are copied beside the binary under `briefs/` and `examples/`. Both folders are empty until their stories.
Both projects are in `dew_flow_benchmark.slnx` under a `/tools/` folder, and `Bench.Tests` references both as ordinary references so their
assemblies are in the test output on purpose, not by accident.

**The guard** is `tests/Bench.Tests/ModelArms/ModelArmsArchitectureTests.cs`; it reuses `ArchitectureTests.Referenced` and `IsRuntime` (widened to `internal`, not copied).

| Test | What it holds |
|---|---|
| `Core_references_only_the_runtime_the_domain_and_the_application_layer` | `ModelArms.Core.dll` references the runtime, `Bench.Domain` and `Bench.Application` only |
| `Core_never_references_infrastructure_or_entity_framework` | the same assembly names no `Bench.Infrastructure`, no `Microsoft.EntityFrameworkCore*`, no `Npgsql*` |
| `Nothing_under_src_or_hosts_references_the_model_arms_module` | no `*.csproj` under `src/` or `hosts/` names `ModelArms` or `model-arms`, and no product assembly (`Bench.*.dll` except the test assembly, `bench.dll`) references `ModelArms.Core` or `model-arms`; both scans also assert they found something |
| `Every_core_decider_is_named_in_the_pure_list` | every public static class of `ModelArms.Core` is named in `PureDeciders`, one marker line per story, and everything named lives in `ModelArms.Core` |

What the guard does not prove: that a Core type is pure inside its body. A list of referenced assemblies cannot see `File.ReadAllText`, because the
runtime is always allowed. The pure list only makes each decider named on purpose. The first decider's story (E1.S3) added the two
companions that keep the scans alive: `CorePublicStaticClasses()` must contain `ExperimentFile` (the public-static-class scan still finds a known
instance; shown red by making the scan's predicate match nothing), and `Referenced("Bench.Tests.dll")` must contain `ModelArms.Core` (the
assembly half's known positive).

Separately, `Bench.Hosting` (the parser, the exit codes, the shutdown signal) is held a leaf by `ArchitectureTests`, beside the Domain, Contracts and Delivered leaves.

## The experiment file (E1.S3)

`ExperimentFile.Read(json, location)` turns the text of `experiment.json` into an `Experiment`, or refuses it with every problem of the file in
one answer, one sentence per line (`Outcome<Experiment>`, the reason is the lines joined by `\n`). Core receives the TEXT and an
`ExperimentLocation(Root, Home, EnclosingGitTop, ThisRepository)`; the console reads the file and answers the location's four questions.
The worked example `tools/model-arms/examples/part6.experiment.json` is the one source the tests and, later, the `init` template are derived from.

**Reading.** `JsonNode.Parse` with comments skipped (the example carries `//` comments), trailing commas refused and a member written twice
refused at any depth (`AllowDuplicateProperties = false`; it arrives as a `JsonException`, whereas the default would throw `ArgumentException` on later access, outside any catch). A malformed text, or one whose
root is not an object, is a single problem. The walk is `StrictJson`, a reader that records a sentence per problem instead of stopping at the
first; this is why the catalog's `UnmappedMemberHandling.Disallow` is not used (it names one unknown member and quits). A file that leaves any
problem builds no `Experiment`.

| Members | Rule |
|---|---|
| top level, required | `schema` `target` `scenarios` `baseline` `known` `steps` `schemes` `rounds` `limits` `tools` `awake` |
| top level, optional | `tasks` (absent: `TaskLimits.Shell`, 4 / 7 / 4) · `coai` (absent: `CoaiFacts.None`) · `isolation` (absent: `IsolationFacts.None`) |
| `target`, all required | `source` `planBranch` `base` `plan` `branch` `rulesSubmodule` `lowPriority` `build` `suite{command,ctrfFile,namePrefix}` `format{solution,verbs}` `extension{directory,command}` `nodeModules` `testGlobs` |
| `limits` / `tasks` / `tools` / `coai` / `isolation` | six limits / `min` `max` `maxParallel` / `write` `read` `gate` `deny` / `binary` `settings` `side` / `closed` `unlisted`; inside a present object every member is required |

**What is refused, and the sentence it gets.** An unknown member: `target.bulid is not a member this build knows (known: …) — refused rather
than ignored`. A missing one: `target.base is missing`. Then the value rules:

- `schema` is exactly `model-arms/experiment-v1`.
- Absolute (`Path.IsPathFullyQualified`): `target.source`, `coai.binary`, `coai.settings`. Relative, not rooted, no `..` segment: `target.plan`,
  `rulesSubmodule`, `format.solution`, `extension.directory`, each element of `nodeModules` and `testGlobs`, and `scenarios` / `baseline` (names inside the root).
  `target.base` is `CommitSha.Parse`, its reason quoted. Every other text is non-empty.
- Every command (`lowPriority`, `build`, `suite.command`, `extension.command`) is a non-empty array whose first element passes
  `ExecutableRule.Refusal` against `[target.source, <root>]` (D17, the static half): a bare name is fine; a relative path
  (`./x`) is refused because it resolves against the target; an absolute path equal to or under a forbidden root is refused (normalised, ordinal,
  with a trailing separator, so a sibling that shares a prefix is not "under"). A file under the target may only be an ARGUMENT, which is why
  the suite runs as `dotnet <tests>.dll`. The forbidden roots are the target and the experiment root itself (which holds `base`, `r` and
  everything else an arm could write). "Under" is compared ignoring case, so it fails closed on a case-insensitive host, and a name containing
  `:` is a path (`C:x.exe` is drive-relative on Windows), never a bare name. **Symbolic links are not followed by this static rule**: a link
  outside every forbidden root that points inside one passes it. Resolving an executable on a host, following the link to its final target
  and checking that against the same roots is E1.S8's `ExecutableResolution`.
- **A JSON `null` where an object belongs** (`"limits": null`, `steps.code: null`, `target.suite: null`, ...) is refused with
  `<path> must be a JSON object`: an object-valued member is read through one accessor that takes the parent and the member name, so a
  present-but-null member is never mistaken for an absent one. `steps`, `schemes` and `rounds` must not be empty.
- `steps`: keys are step kinds (`code review fix hreview gate split splitFix taskCode`; `steps.lint is not a step kind (…)`), each value
  `{model, minutes > 0, mode ∈ write|read|gate}`. `schemes`: names are letters; each entry is exactly `tasks` or `<file name>:<step kind>`
  (`^[0-9]+[a-z]?-[A-Za-z0-9-]+:[A-Za-z]+$`) whose kind is defined under `steps`. `rounds`: non-empty arrays of arm names whose scheme (`ArmNames.SchemeOf`:
  `SRS2` is `SRS`) is among the schemes: `rounds[0][1] names 'XY2', whose scheme 'XY' is not among the schemes (C, CR, SRS)`.
- `limits.*` and `tasks.*` are whole numbers above zero and `tasks.min <= tasks.max`; `awake` is `auto|windows|wsl|none`; `coai.side` names one folder (no
  separator, no `..`).
- **Placeholders.** An `isolation` entry is `<root>`, starts `<root>/`, starts `~/`, or is absolute; `<root>` expands to `Root` and a leading `~` to
  `Home` (`Path.Combine`), so `Experiment.Isolation` holds paths only. Anything else is refused.
- **Placement** (`ExperimentFile.Placement`, added to every `Read` first): a root equal to or under `ThisRepository` is refused (`inside this
  repository`); any non-empty `EnclosingGitTop` is refused (`inside the git checkout`), because the arms' clones would nest in it.

What this does NOT prove: that any path exists or is readable, that an executable resolves on this host, that `scenarios.json` parses, that an arm is named
twice in the rounds, or that a host can set modes (E1.S8 and E1.S10, `validate`). The example's paths are under `/example/` and the tests' location is
`/x/...`, so these tests assume a POSIX path syntax (the suite runs on ubuntu CI and WSL).

## Paths and the arm map (E1.S4)

`RootPaths` and `ArmPaths` (`tools/model-arms/ModelArms.Core/Experiment/ArmPaths.cs`) are the one spelling of every path of an experiment
root and of one arm; nothing else in the module concatenates a path. Every member is a `Path.Combine`; none opens a file or makes a folder
(`make` creates them, a later epic). `ArmPaths(Root, Token)` is built by `RootPaths.Arm(token)`. A name taken from a caller (arm, step, task id,
side, token) must be one path segment: empty, `.`, `..` or containing `/` or `\` throws `ArgumentException`, a programming error because callers
pass names validated upstream. A file name the operator configured goes through `RootPaths.Of`, which refuses by value
(`'<x>' must be a file name inside the experiment root`) a rooted name, a drive prefix or any `..` segment. `n` and `i` are printed invariant.

| `RootPaths` member | Path under the root |
|---|---|
| `ExperimentFile` | `experiment.json` |
| `Briefs` | `briefs` |
| `Base` | `base` |
| `ArmMapFile` | `arms.map.json` |
| `Arms` | `r` |
| `Pids` · `PidFile(arm)` · `PidLog(arm)` | `pids` · `pids/<arm>.pid` · `pids/<arm>.log` |
| `Chain` | `chain` |
| `Made(arm)` | `chain/made-<arm>` |
| `Locked` · `Unlocked` | `chain/locked` · `chain/unlocked` |
| `ObjectiveMarker(arm)` | `chain/objective-<arm>` |
| `ArmsDone` | `chain/arms-done` |
| `Executables` | `chain/executables.txt` |
| `GateLock` · `LockedModes` | `.gate.lock` · `.locked-modes` |
| `LockedModesRestored(stamp)` | `.locked-modes.restored-<UTC stamp>` (where `unlock` moves `.locked-modes` to) |
| `LockLog` · `KeepAwakeLog` | `lock.log` · `keep-awake.log` |
| `Of(relative)` | `<relative>`, refused when it leaves the root |
| `Arm(token)` | an `ArmPaths` |

| `ArmPaths` member | Path under `r/<token>` |
|---|---|
| `Folder` | `r/<token>` itself |
| `CloneFolder` | `arm` |
| `Out` · `Notes` · `Work` · `Tmp` · `Worktrees` | `out` · `notes` · `work` · `tmp` (the CLI's `TMPDIR`) · `wt` |
| `Worktree(id)` | `wt/<id>` |
| `CoaiSettings(side)` | `coai-data/<side>/settings.json` |
| `McpNone` · `McpCoai` | `mcp-none.json` · `mcp-coai.json` |
| `Session(step)` | `out/<step>.session` |
| `Stream(step, n)` · `Stderr(step, n)` · `Prompt(step, n)` | `out/<step>.a<n>.jsonl` · `.a<n>.err` · `.a<n>.prompt.md` |
| `Done(step)` · `Before(step)` · `Diff(step)` | `out/<step>.done` · `.before` · `.diff` |
| `Timing` | `out/timing.jsonl` |
| `ObjectiveFolder` · `Objective` | `out/objective` · `out/objective.json` |
| `TasksPlanLog` | `out/tasks-plan.log` |
| `BatchDone(i)` | `out/batch-<i>.done` |
| `TaskBefore(id)` | `out/task-<id>.before` |
| `CoderHead(id)` · `CoderDiff(id)` | `out/task-<id>-coder.head` · `out/task-<id>-coder.diff` (the shell named these after a model; no path of the module names one) |
| `Picked(id)` · `PickRecord(id)` · `PickLog(id)` | `out/picked-<id>` · `out/pick-<id>.json` · `out/pick-<id>.log` |
| `WorktreeBuildLog(id)` | `out/wt-build-<id>.log` |
| `PrepBuildLog` · `ObjectiveLog` | `out/prep-build.log` (make's warm build) · `out/objective.out` |
| `CodeNotes` · `HReviewNotes` · `FixNotes` · `GateNotes` · `SplitNotes` | `notes/code.md` · `hreview.md` · `fix.md` · `gate.md` · `split.md` |
| `ReviewJson` · `GateJson` | `notes/review.json` · `notes/gate.json` |
| `TaskNotes(id)` | `notes/<id>-code.md` |
| `TasksJson` · `BatchesJson` | `work/tasks.json` · `work/batches.json` |
| `TaskBrief(id)` | `work/<id>.md` |

`ArmPathsTests` holds one hand-written spelling per row and compares the rows with the members the two types really have, so a path added
without a spelling fails the class. The parent plan's section 4.3 missed nine files the shell wrote (`notes/split.md`, `out/tasks-plan.log`,
`out/batch-<i>.done`, `out/task-<id>.before`, `out/pick-<id>.log`, `out/wt-build-<id>.log`, `out/prep-build.log`, `out/objective.out`, `.locked-modes.restored-<stamp>`); they are spelled here. The member for the
clone folder is `CloneFolder`, not `Clone`: C# reserves `Clone` in a record (CS8859).

**The arm map** (`ArmMap`, `arms.map.json`) holds arm name to a random 16-hex token (8 bytes), the folder name of the arm so that no arm can
guess another's. The shell's tokens were 12 hex; `ArmMap.Read` refuses such a map and a run mints its own.

| Operation | Behaviour |
|---|---|
| `Read(json)` | an object of strings; every arm that is wrong is named, one sentence per line: a token that is not 16 lower-case hex, two arms with one token, an arm name not matching letters-then-digits, an arm twice, a value that is not a string; all of them in one read, not only the first |
| `ToJson()` | an indented JSON object of strings in insertion order, `\n` newlines, like the shell's map — not byte-identical (tokens, indent and file name differ) |
| `Extend(existing, arms, eightRandomBytes)` | every existing entry unchanged, then a fresh token for each arm not yet present; a draw that collides with a token already in the map is drawn again. Arms absent from `arms` are kept. A random source that does not give 8 bytes, or collides 64 times in a row, and an `arms` entry that is not an arm name throw (programming errors). The source is a parameter, so the type stays pure; the console passes `RandomNumberGenerator` |
| `TokenOf(arm)` | the token, or a refusal naming the known arms |
| `Leaks(text, ownArm, schemes)` | what the text would reveal to `ownArm`: every arm name of the map (its own too) and every scheme name found as a whole word (not touching an ASCII letter or digit, case-sensitive), and every token other than its own found anywhere. Its own token is allowed: it is the arm's working directory and is in every path field of a brief. Empty means clean |
| `Scrub(line)` | every token of the map replaced by `<token>`. The console's one writer calls this; nothing keeps its own copy |

What the tests do not prove: that the randomness is unpredictable (the tests count; the console supplies the entropy), and that a verb calls
`Leaks` or `Scrub` at the right places (the brief filler and the console writer own that and test it).

## The launch boundary (E1.S5, E1.S6)

**The one launcher, widened.** A step is one headless `claude -p`, started through the benchmark's existing launcher (`CliArgv` builds the
argv, `ProcessRunner` runs it); the module adds no second launcher. E1.S5 widened it, E1.S6 decides what to ask of it.

| Widening (E1.S5) | Where | Claude spelling |
|---|---|---|
| `AgentAskOptions.SessionId` / `ResumeSessionId` (a `Guid`, `Guid.Empty` = none) | `src/Bench.Application/CliAgent.cs` | `--session-id <guid "D">` / `--resume <guid "D">`, right after the model; both asked together are refused |
| `AgentAskOptions.PermissionMode` (`NotAsked`, `Default`, `AcceptEdits`, `Plan`) | same | `--permission-mode default\|acceptEdits\|plan`; refused beside a read-only sandbox, which already spells `plan` |
| `AgentAskOptions.PermittedTools` | same | `--allowedTools <name> <name> …`, each name its own token — the PERMISSION list with patterns (`Bash(git *)`), not `--tools` (`AllowedTools`, the built-in set) |
| `AgentAsk.ErrorOutput` (an `AgentOutputSink`, `None` by default) and `ProcessRunner`'s `stderrLine` | `CliAgent.cs`, `src/Bench.Infrastructure/Process/ProcessRunner.cs` | stderr is handed to the sink line by line while the process runs (the runner's file writer is E2); the whole stderr is still returned |

Codex, gemini and agy refuse the session id, the session to resume, the permission mode and the permission allow-list by name
(`CliArgv.Unsupported`); the first three widenings are claude's alone. `ErrorOutput` is not refused by any CLI: `CliAgentRuntime` hands the sink to
`ProcessRunner` for every runtime, because stderr is the process's, not a flag's. The existing argv suites run unedited.

**`StepLaunch`** (`tools/model-arms/ModelArms.Core/Launch/StepLaunch.cs`, pure) turns a step's mode, the tool lists, the arm's paths and the
session into an `AgentAskOptions` and a whole `AgentAsk`. `StepSession` is a closed pair: `Fresh(id)` becomes `SessionId`, `Resume(id)`
becomes `ResumeSessionId` (plan D9), so a launch is never both. Whatever the mode: `JsonEvents` (`--output-format stream-json --verbose`),
`McpServersOff` (`--strict-mcp-config`) and `AddDirectories = [Notes, Work]` (`--add-dir` twice). Per mode:

| Mode | `--permission-mode` | `--allowedTools` | `--disallowedTools` | `--mcp-config` |
|---|---|---|---|---|
| `Write` | `acceptEdits` | `tools.write` | `tools.deny` | `mcp-none.json` |
| `Read` | `default` | `tools.read` | `Edit Write NotebookEdit` then `tools.deny` | `mcp-none.json` |
| `Gate` | `acceptEdits` | `tools.write` then `tools.gate` | `tools.deny` | `mcp-coai.json` |

These are the flags of the shell harness's `run-arm.sh` (`:86-89`, `:106-108`); the lists keep the configured order. `Ask` is
`CliClaude`, the executable the console resolved, the prompt (it rides stdin), the working directory, the model of the step and the wall the
caller computed (the wall arithmetic and the sinks are E2's: `Output` and `ErrorOutput` stay `None`, and the runner sets them with `with`).
Nothing here resolves `claude`, starts a process or reads a clock.

**`ArmChildEnvironment`** (`…/Experiment/ArmChildEnvironment.cs`, pure; plan D19) is the environment an arm's claude runs under, passed as
`AgentEnvironment.Only(...)` so the launcher REPLACES the harness's own rather than merging into it:

- the parent's variables as the shell harness handed them on (`HOME`, `PATH`, the locale, claude's configuration), **minus**
  `BENCH_*` and `ConnectionStrings__*` (ordinal, ignoring case), every name `CoaiEnvironment.IsHarnessVariable` accepts (`BENCH_*`, `COAI_*`),
  every name `CoaiEnvironment.IsSecretName` accepts (`*_KEY`, `*_TOKEN`, `*_SECRET`, `*_PASSWORD`, the creds key) **unless it starts
  `CLAUDE_` or `ANTHROPIC_`**, and `SUBAGENT_MODEL_GUARD` and `CLAUDE_CODE_DISABLE_BACKGROUND_TASKS` whatever their value;
- **plus** `TMPDIR = <arm>/tmp` (`ArmPaths.Tmp`), replacing any spelling of it in the parent. Nothing else is added, and the result's
  comparer is ordinal.

`StepLaunch.Ask` adds exactly one thing more, because only a launch knows it: `PWD` is the request's working directory, and the runner's
`OLDPWD` is dropped. The shell ran `(cd "$DIR" && … claude …)`, so its child saw its own folder as `PWD` and the experiment's start folder
nowhere; a process started with a working directory does not update the variable, so without this the child would inherit the runner's `PWD`
and `OLDPWD`. This is the single named exception to "nothing else is added".

Why the exception for `CLAUDE_*` / `ANTHROPIC_*`: claude logs in through `CLAUDE_CODE_OAUTH_TOKEN` or `ANTHROPIC_API_KEY`, both secret-named,
so the unqualified secret rule would launch nine arms that cannot authenticate (E1 stories §8 item 1; parent D19 as corrected). Why no scout
flag: the arms run under the operator's hooks and settings exactly as part 6 measured them; a flag the shell never set would change what the
experiment compares. Why one `TMPDIR` per arm, shared by the parallel tasks of a batch: part 6 ran them that way without a collision, and a
per-task folder would be a condition no run has measured. The default `/tmp/claude-<uid>` is replaced because it would show every arm's name.

**Known differences from the shell's child, all deliberate.** The C# child is not byte-for-byte the shell's environment: (1) `COAI_*` is
stripped (the shell passed the operator's); (2) every non-claude secret-named variable is stripped (`GITHUB_TOKEN`, `DB_PASSWORD`, a vendor
`*_API_KEY`); (3) `TPL_*` is not passed (the shell exported them for its own template filling, `research/local-model-coding/harness/t-lib.sh:14`, `:62`,
`:106`; the module fills briefs in-process, so a child has no use for them); and `PWD` is matched to the shell's (above). Items (1) and (2) have one owner: the E2 gate step decides how a provider's key
reaches the coai server — through the `env` of `mcp-coai.json`, not through the child's own environment — so nothing here needs them.

**What this does NOT prove.** That claude authenticates with what is left, that it accepts the argv or its flag order (the tests compare
the multiset of flag groups, not the shell's order), or that any process starts: `StepLaunchTests` and `ArmChildEnvironmentTests` read the
values these functions return and the argv `CliArgv` builds from them. A variable named like a secret that is not claude's (a vendor key the
operator exported for another tool) is stripped; an arm that needs one must be given it by another road, which is a decision for a later epic.

## Briefs (E1.S7)

A brief is a template with `{{NAME}}` placeholders, filled once per step from facts the module already holds. The eight templates are the part-6
harness's files, copied byte for byte (`tools/model-arms/briefs/*.template.md`, shipped beside the binary under `briefs/` by the console's content
glob); `The_eight_shipped_templates_are_the_shells_byte_for_byte` holds their SHA-256 values, typed out once in the test, so an edited copy fails there.

| Step kind | Template | `NOTES` is | Extra fields |
|---|---|---|---|
| Code | `1-code.template.md` | `notes/code.md` | none |
| Review | `2-review.template.md` | `-` (the shell passed a dash; the review step writes `notes/review.json`, the `REVIEW` field) | none |
| Fix | `3-fix.template.md` | `notes/fix.md` | none |
| HReview | `hr-review-fix.template.md` | `notes/hreview.md` | none |
| Gate | `gate.template.md` | `notes/gate.md` | none |
| Split | `x-split.template.md` | `notes/split.md` | none |
| SplitFix | `x-split-fix.template.md` | `notes/split.md` | `PROBLEMS` |
| TaskCode | `t2-coder.template.md` | `notes/<task>-code.md` | `TASK`, `TASK_BRIEF`, `TASK_FILES`, `BEFORE` |

`BriefTemplates.NameOf(kind)` is the one spelling of the file names; `All` is derived from the kinds. `BriefFields.For(kind, paths, facts)` gives every kind the same
thirteen fields (`PLAN BASE BRANCH SCEN_COUNT DEADLINE KNOWN REVIEW CODE_NOTES HREVIEW_NOTES FIX_NOTES WORK GATE_JSON NOTES`; the path ones are `ArmPaths` members,
`SCEN_COUNT` is printed invariant) and adds the task or the problems for the kinds above. `HREVIEW_NOTES` and `BEFORE` appear in no template; they are provided as
the shell provided them, and a field a template does not use is never an error.

**`BriefFiller.Fill(template, fields, guard, templateName = "")`** replaces every placeholder whose name is a field, in one pass (a value is never expanded again), and refuses in this order:

1. a placeholder left without a field: `the brief has unfilled fields: {{A}} {{B}}` (each name once, in order of appearance);
2. a template that itself holds an arm name, a scheme name or another arm's token (an experiment root's override is the operator's text):
   `the template '<name>' would carry 'CR' into a brief — …`;
3. a substituted value in which `ArmMap.Leaks` finds one of those: `the field NOTES would carry 'C1' into a brief — no brief names an arm, a scheme or another
   arm's token (D15)`. The arm's OWN token is allowed: it is in every path field, and the arm's working directory shows it anyway (plan E1 section 8, item 2).
   A value that begins with `guard.Root` is checked without that exact prefix, so a native Windows root such as `C:\exp` does not make its drive letter a scheme named `C`;
4. a result that still holds a placeholder, which only a value can have carried: `the filled brief still holds {{PLAN}} — …`.

A field the template never uses is not checked. `ArmMap.Leaks` matches an arm name and a scheme of several letters as a whole word in any case ("as c1 did", "the srs arm");
a one-letter scheme only in its own case, so the `/mnt/c/` of a WSL path is not a leak.

`BriefTemplateFiles(shipped, overrides).Read(kind)` (console, `ModelArms.Adapters`) takes a step kind, never a file name, and resolves `BriefTemplates.NameOf` inside, so no
operator text can reach outside the two folders. It returns the experiment root's own copy when it exists, else the shipped one, else
`no brief template '<name>' in <overrides> or <shipped>`. The text is read as strict UTF-8; an override that exists but cannot be read (permissions, invalid UTF-8) is a refusal naming it,
never a fall back to the shipped file. A root `.gitattributes` line marks the templates `-text`, so a checkout never converts the bytes the SHA-256 values pin.

What this does NOT prove: that a brief reads well to a model; that the deadline string has the format the launcher prints (E2.S4 owns it); that the resume notice,
the prompt file or its path are right (E2); or that a template edited in an experiment root still carries every field it needs: such a copy is checked by `Fill`
at run time, not here.

## The root, trust and the host (E1.S8, E1.S10)

The first half (E1.S8): the root's requirements, its trust, executables on a host, and the two data files beside `experiment.json`. Five pure
deciders in `ModelArms.Core.Experiment`, one console adapter, `ModelArms.Adapters.RootFacts`. E1.S10 adds `HostChecks` and the `validate` verb
(the second half); nothing here is called by a verb yet (E1.S9's gate, E1.S10's validate and E4.S4's `prep-base` call it).

**`RootRequirements`** (D18, parent §4.12) is the table of which files of the root each verb needs, in the order they are checked. The names come
from `RootPaths` (one spelling of the layout); the scenarios and baseline names are the configured ones. `FirstUnmet(requirements, root, exists)` is the
first requirement the host does not meet, as a sentence naming the path and the verb that writes the file, or the empty string.

| Verb | Requirements, in order (`!` = must NOT exist) |
|---|---|
| `init` | `!` the root |
| `version`, `help` | none (an unknown verb also none: the command line refuses it first) |
| `validate` | root, `experiment.json`, scenarios |
| `prep-base` | root, `experiment.json`, scenarios, `!` baseline |
| `make`, `chain` | root, `experiment.json`, scenarios, baseline |
| `run-arm`, `run-all`, `checks` | the same, `arms.map.json`, then `chain/made-<arm>` for each arm given |
| `status`, `lock`, `unlock`, `gate-lock` | root, `experiment.json` |

`chain` requires what `make` requires (its first action is making every unmade arm), and `lock`, `unlock`, `gate-lock` require the root and `experiment.json`
(deviations 3 of the stories document). Producers: root, `experiment.json`, scenarios are written by `init`; the baseline by `prep-base`; `arms.map.json` and the
`made-` markers by `make`.

**`RootTrust.Decide(posix, currentUid, files, root)`** (D17 a) judges facts the console read. Off POSIX the answer is `NotPerformed` with the line
`the ownership and mode check of <root> was not performed — POSIX only (D17)`: a check that was not performed is not a check that passed. On POSIX each file whose owner is not the
current user, and each whose mode has a group-write or other-write bit, is a problem of its own naming the path (the mode as three octal digits, with the
`chmod go-w` to run); none is `Trusted`. The caller passes the root folder, `experiment.json` and `scenarios.json`. A trust path that is itself a symbolic link is refused by
`RootFacts.FileFactsAsync` before any fact is read (`<path> is a symbolic link to '<target>' — the trust check judges the file itself, not a link that can be re-pointed`): the last segment may not
be a link, an ancestor may (`~/git` to `/mnt/d/git` is every WSL home), and a trailing separator is trimmed first because `readlink("link/")` follows the link and reports none.

**`ExecutableResolution.Resolve`** (D17 b) turns a command's executable into the path to run, and the path it returns is the REAL path: the one the kernel opens, every link and every `..` applied in the
kernel's own order. The harness launches and prints that path (not the one written), because judging the real path and launching the written one would leave the link between check and launch for an arm to
re-point. A bare name is searched in the PATH directories (on Windows also with `.exe`, empty entries skipped); a PATH candidate that is a dangling link or a loop is skipped and the search moves on, as `execvp`
does; a name found nowhere is `<name> is not on PATH on this host`. A spelled absolute path must exist and be executable, and one the kernel cannot resolve is refused naming the operating system's reason; a relative path
is refused; the verbatim and device prefixes (`\\?\`, `\\.\`, `\??\`, `//?/`, `//./`, `/??/`) are refused as a spelling on every platform, and so is a NUL character: in an executable (the first case of the textual rule), in every text of the experiment file (refused once in the reader, naming the field), and in a forbidden root, which `PathScope` answers as "under" (fail closed) and `Resolve` refuses before it asks the kernel about the root. `Path.GetFullPath` throws on a NUL, so a NUL is a refusal and never a thrown exception. Both the executable as written and as it really is are judged against both the roots as written and
the roots as they really are (`{written, real} x {roots, real roots}`): a link inside the target that points out of it is refused by its spelling, and a root spelled through a link is compared by where it
really is. A root that does not exist yet (the root `init` is about to make) is resolved through its nearest existing ancestor with the remaining segments appended. The forbidden roots are
`ExecutableRule.ForbiddenRoots(target.source, root)`, compared ignoring case.

**`ScenariosFile.Read`** reads `scenarios.json` (`[{"id","kind","name"}]`, kind `cs` or `node`) with the same strict walk as the experiment file: unknown and missing members named, a repeated id named,
an empty list refused, every problem in one answer. **`BaselineFile`** (D24) is `{"schema":"model-arms/baseline-v1","base","measuredAt","host","mcpFailed","tsFailed","suiteTotal","extensionTotal","note"}`
(the shell's failure keys kept: `mcpFailed` is the main suite's, `tsFailed` the extension's). `Compose` refuses, all problems at once and one sentence each: a run that did not complete (`absent is not zero`);
a completed run that exited non-zero and named no failing test (`the <which> suite exited <code> and named no failing test`); a total of zero (`counted 0 tests`); more failures than tests
(`named <n> failing tests of <total>`); a blank host and a negative total. A completed run that exited 0 with failures named is NOT refused: the exit code is the runner's opinion, the names are the measurement.
The instant is written in UTC ending in `Z`. `Read` refuses unknown members, another schema, a missing member (the note may be empty, not absent), a sha that is not 40 hex and an instant that is not UTC.

**`RootFacts`** (console) reads what the deciders judge: `GitTopOf(path)` (nearest ancestor-or-self holding `.git`, a path that does not exist yet included), `ThisRepository(baseDirectory)` (nearest ancestor holding
`dew_flow_benchmark.slnx`), `RealPathOf` (the kernel's answer, below), `IsExecutableFile` (the real path resolves, exists and, off Windows, has any execute bit; false, never an exception, for a dangling link, a loop or a
folder), `CurrentUidAsync` (`id -u`) and `FileFactsAsync` (owner, mode and file type from ONE `stat` call on ONE absolute, separator-trimmed path, so all three are facts of one inode — a `..` after a symbolic link climbs from the link's target in the kernel's order but from the link's folder in .NET's, and the two used to read two different files; macOS `stat -f`; a trust path that is a link, or holds a NUL, is refused).
`RealPathOf` asks the kernel and never re-implements it: libc `realpath(3)` on Linux, macOS and FreeBSD; on Windows a handle opened on the path with backup semantics (so folders open too) and
`GetFinalPathNameByHandleW`, with the `\\?\` prefix stripped. A platform not on that list is refused (`no real-path resolver is probed for this platform`), not given another platform's answer. A failure carries the
operating system's own words and no decision is taken on an errno number or a message. The module reads links nowhere else (`ModelArmsArchitectureTests.The_module_reads_links_only_through_RootFacts_RealPathOf`):
.NET's own link resolution joins a link's text with its folder and collapses `..` as text, so for `home/c/claude -> sub/../bin/claude` with `sub` linked to `<target>/x` it answers `home/c/bin/claude` where the kernel runs
`<target>/bin/claude` (measured on Linux, 2026-10-29). The scouts' path audit in `src` has the same textual join (it runs nothing and reports; it is named here, not changed).

What the tests do NOT prove: that a link is still what it was when it was checked (the real path is returned and launched, which closes the gap between check and launch for the executable, not for a folder an arm
re-links later); that a file outside the forbidden roots is safe to run; ownership on a file system that reports no Unix owner; any of the facts on Windows, where the trust check is declared not performed and links
need a privilege (those tests skip, saying why). The live trust and owner tests run on Linux only (they read `/proc`); the real-link tests skip where links cannot be made.
Stated gaps, not checks:
- **Hard links and bind mounts.** A hard link is a second NAME for the same file, and a path check sees only names; a bind mount likewise shows one tree under two paths. The only check is to compare device and
  inode of the resolved file with every file under both roots: a full walk of a checkout per executable, racing against arms that write, and blind to a link made after the check. It is not done, and no link-count
  warning is added. The premise "making that link means an arm writing outside its own arm" holds only for a link made DURING the experiment: a hard link that ALREADY exists (the operator's, `cp -l`, a package
  store's, a tool that installs its builtins as links) lets an in-place write to the file under the target change the executable outside it, and D17 does not see that.
- **The kernel's `..` rule.** A textual check cannot see that `..` is applied to the folder a link points to; only the kernel's answer can, which is why the real path is asked of the kernel and `ExecutableRule` stays a textual first line.
- **`argv[0]`-sensitive programs.** The real path is what is launched, so a program that behaves by the name it was started under (a multi-call binary) runs under its real name. `claude`, `npm`, `dotnet`, `node` and `git` do not.
- **Names looked up by the programs the harness starts.** The configured executable is resolved and refused by this module; a name that a started program looks up itself is not. A shebang interpreter
  (`#!/usr/bin/env node`) and every child process find their programs through the inherited `PATH`, and an entry of it under a forbidden root, or an empty or relative entry, is refused only when the CONFIGURED name
  happens to be found there (observed: `#!/usr/bin/env mynode` ran the target's `mynode`). Closing it means handing the children a sanitised `PATH`, which is the launch story's decision (E1.S10 notes), not this one's.
- **Windows app-execution aliases** (`python.exe` under `WindowsApps`) cannot be opened as a file; they are refused with the operating system's message, not resolved.
- **Not observed on this host:** the Windows paths, including the 8.3 short-name test (`An_8_3_short_name_spelling_of_the_target_is_refused`, which skips here). They are written to the handle-based design and go red then green on the operator's Windows run.

## The command line (E1.S9)

## The first run: init and validate (E1.S11)
