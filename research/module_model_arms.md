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

> Superseded by [*The step contract (E2.S0)*](#the-step-contract-e2s0) where it speaks of step kinds, `rounds` and the schema name; kept as the E1 record (the reading, the path and executable rules and the placeholders are unchanged).

`ExperimentFile.Read(json, location)` turns the text of `experiment.json` into an `Experiment`, or refuses it with every problem of the file in
one answer, one sentence per line (`Outcome<Experiment>`, the reason is the lines joined by `\n`). Core receives the TEXT and an
`ExperimentLocation(Root, Home, EnclosingGitTop, ThisRepository)`; the console reads the file and answers the location's four questions.
The worked example `tools/model-arms/examples/part6.experiment.json` is the one source the tests and the `init` template are derived from (*The first run*, below).

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
- **The template's placeholder** (`UnfilledValues`, run by `Read` after the walk): a string still equal to `<fill me>`, at ANY path, is refused as `<path> is still the template's <fill me> — replace it`. Each
  field's own rule rejects the placeholder only when it dislikes the text (an absolute path, a sha, a choice); `known`, `steps.*.model`, `tools.*[*]`, `coai.side`, `target.planBranch` and the like would take it. The rule is on the
  reader's one road in, not on the fields; a path an earlier problem already names (a line starting with it, then a space or a colon) is not said twice. `ExampleExperimentTests.Every_value_the_template_leaves_unfilled_is_named_by_the_reader`
  derives the list from the template itself.
- **Placement** (`ExperimentFile.Placement`, added to every `Read` first): a root equal to or under `ThisRepository` is refused (`inside this
  repository`); any non-empty `EnclosingGitTop` is refused (`inside the git checkout`), because the arms' clones would nest in it.

What this does NOT prove: that any path exists or is readable, that an executable resolves on this host, that `scenarios.json` parses, that an arm is named
twice in the rounds, or that a host can set modes (E1.S8 and E1.S10, `validate`). The example's paths are under `/example/` and the tests' location is
`/x/...`, so these tests assume a POSIX path syntax (the suite runs on ubuntu CI and WSL).

## The step contract (E2.S0)

Parent decisions D25 (a step's identity is configuration, its execution kind is closed) and D27 (runs are named); schema `model-arms/experiment-v2`. E1 keyed `steps` by a closed `StepKind` and
picked one template per kind and one notes file per kind; one day later part 7's CHH scheme (two read-only Haiku reviews briefed differently, a fixer briefed with both verdicts, a diff
spanning three steps) could not be said in it. The module ports a harness: the owner changes a scheme between parts, and the runner must not be rebuilt for that. What the runner must KNOW
is closed; everything else is the file's.

**Four kinds, and what each closes.** `StepKind { Session, Review, Gate, TaskCode }`, the file's words `session review gate taskCode`.

| Kind | What the runner does that only the kind can decide |
|---|---|
| `session` | one claude session; nothing else |
| `review` | the last result's JSON block is written to the file its `verdict` output names (derived, required by construction: regenerated or resumable, D21) |
| `gate` | runs under the gate's lock and reads the file its `status` output names; mode is `gate` and only a gate's is |
| `taskCode` | the task pipeline's coder, launched per task in its worktree; carries no bindings, because its notes, snapshot and diff are named per task (`TaskNotes(id)`, `task-<id>.before`, …) |

**A step** is `steps.<id>` — the id is letters and digits, starting with a letter (`StepId.Parse`) — with the members `kind model minutes mode subagents brief outputs notes verdict status snapshot diffFrom`
(required `kind model minutes mode brief`). `StepSpec` carries them: `Brief` is a template stem; `Outputs` are bindings, a brief FIELD name (`[A-Z_]+`, the filler's own `{{NAME}}`) to a path under the
arm folder (`OutputBinding`: relative, forward slashes, under exactly `notes/` or `work/` — the two folders a launch grants, so `Notes/x.md` is refused, not read as the same folder — with no empty, `.` or `..`
segment, no control, format, line-separator or non-ASCII-space character (NUL, a newline, U+200B, U+2028, U+00A0) and no space at either end of a segment; two DESTINATIONS are compared without regard to case, which fails closed); `Notes` names the field `NOTES` stands for (one of the step's own outputs or an EARLIER step's, which the step then appends to — the split fix appends to `SPLIT_NOTES`; absent: `-`);
`Verdict` / `Status` name one of the step's own outputs; `Snapshot` takes `out/<name>.before` before the first attempt; `DiffFrom` names the step id whose `.before` the `out/<name>.diff` is written from
(its own id included). Equality of a `StepSpec` and of `TaskLimits` is by value, `Outputs` included: a record compares a list by reference.

**The sentences** (every one starts with its dotted path, then a space or a colon — `UnfilledValues.Refuse` de-duplicates by that prefix; `StepContractTests` asserts the prefix on every refusal and
`Every_refusal_of_the_step_contract_starts_with_its_dotted_path` holds it in one place):

| member | rule | the sentence |
|---|---|---|
| `schema` | exactly `model-arms/experiment-v2` | v1: `schema is 'model-arms/experiment-v1' — this build reads 'model-arms/experiment-v2': steps are keyed by a step id and carry kind, brief and outputs; the notes files are bindings; rounds moved to runs.<name>.rounds`; any other: `schema is '<x>' — this build reads exactly 'model-arms/experiment-v2'` |
| `rounds` at the top level | known only to be refused | `rounds — moved to runs.<name>.rounds (schema v2)` |
| `steps.<key>` | the key through `StepId.Parse` | `steps.<key>: '<key>' is not a step id — letters and digits, starting with a letter (hA, splitFix)` |
| `kind` | one of the four | `steps.<id>.kind is '<x>' — legal: session, review, gate, taskCode` |
| `mode` | the three, then the pairing | `steps.<id>.mode is '<m>' — a gate step's mode is gate` · `steps.<id>.mode is 'gate' — only a gate step runs in gate mode (this step's kind is <k>)` |
| `subagents`, `snapshot` | `true` or `false`; default `false` | `steps.<id>.subagents must be true or false` |
| `brief` | a stem: `[A-Za-z0-9-]+` | `steps.<id>.brief: '<x>' is not a template stem — letters, digits and dashes, no separator and no '..' (hA-trace names hA-trace.template.md)` |
| `outputs` | each pair through `OutputBinding.Parse`, after refusing a name every brief is filled with (`BriefFields.FixedNames`: the seven facts, `NOTES`, the task fields, `PROBLEMS`); two fields of one step with one path | `steps.<id>.outputs.<F>: <F> is a field every brief is filled with — a binding cannot replace it` (and `tasks.outputs.<F>: …`) · `steps.<id>.outputs.<F>: <F> is not a field name — capitals and underscores, as a template's {{NAME}}` · `steps.<id>.outputs.<F>: '<path>' is not an output path — relative, under notes/ or work/, forward slashes, no '..' (notes/hA.json)` · `steps.<id>.outputs — <A> and <B> both name <path>` |
| `verdict` / `status` | required for `review` / `gate`, one of the step's own outputs; refused on any other kind | `steps.<id>.verdict is missing — a review step names the output its last result's JSON block is written to` · `steps.<id>.status is missing — a gate step names the output it reads status from` · `steps.<id>.verdict is '<X>' — not one of the step's outputs (<list or none>)` · `steps.<id>.verdict — only a review step has a verdict (this step's kind is <k>)` |
| `notes` | a field some step or the tasks block binds, and never one a step binds as its VERDICT (the map-level check; the position check is the scheme's) | `steps.<id>.notes is '<X>' — no step binds it` · `steps.<id>.notes is '<F>' — the verdict of step <r>, which the runner writes; notes are a file the model writes` |
| `diffFrom` | a defined step id (the placement and the snapshot are the scheme's) | `steps.<id>.diffFrom is '<x>' — not a step id defined under steps (<ids>)` |
| a `taskCode` step | `outputs` (non-empty), `notes`, `verdict`, `status`, `snapshot: true`, `diffFrom` | `steps.<id>.<member> — a taskCode step has no bindings: the task pipeline names its notes, snapshot and diff per task (E4)` |
| the map | a field bound by two steps | `steps.<later>.outputs — <F> is already bound by step <earlier>` |
| `schemes.<S>[<i>]` | `tasks`, or `<file name>:<step id>` over a defined id | `schemes.<S>[<i>] is '<text>' — an entry is "tasks" or "<file name>:<step id>", such as "1-code:code"` · `… — step id '<id>' is not defined under steps (defined: <ids>)` |
| a scheme | a name placed twice; `tasks` twice; a `taskCode` step as a plain entry | `schemes.<S> — '<name>' is placed twice` · `schemes.<S> — tasks is placed twice` · `schemes.<S>[<i>] (<name>): a taskCode step runs only inside a tasks entry` |
| a scheme, per position | `notes` not visible; `diffFrom` not placed at or before (inside one tasks entry the block's order, split before split fix before the task coder, decides), or without a snapshot | `schemes.<S>[<i>] (<name>): notes <X> is bound by no step at or before this position (visible: <fields>)` · `schemes.<S>[<i>] (<name>): diffFrom <x> is not placed at or before this position` · `… diffFrom <x> has no snapshot` |
| a scheme, the producers | one path from two producers; one field from a step and the tasks block; in a scheme with a tasks entry, a file the task pipeline writes (`work/tasks.json`, `work/batches.json`, a task's brief `work/t<n>.md` or notes `notes/t<n>-code.md`, the id being `TaskIds.Pattern` (`^t[0-9]+\z`), defined once in Core and the one E4's `tasks.json` checker reads; `work/summary.md` is free — the shapes are read from `ArmPaths.TasksJson`, `BatchesJson`, `TaskBrief`, `TaskNotes` by `ArmPaths.IsPipelineFile`) | `schemes.<S> — <path> is written by two of its producers: <p1> (<F1>) and <p2> (<F2>)` · `schemes.<S> — <F> is bound by step <id> and by the tasks block` · `schemes.<S> — <path> is a file of the task pipeline, which writes it: <p> (<F>) cannot bind it` |
| `tasks` | `min max maxParallel split splitFix taskCode` required, `outputs` optional; the ids checked when the block is present OR a scheme has a `tasks` entry | `tasks.split is '<x>' — not a step id defined under steps (<ids>)` · `tasks.split is '<x>' — its kind is <k>; the split is a session step` (`the split fix is a session step`, `the task coder is a taskCode step`) · `tasks.splitFix is '<x>' — the split fix is a step of its own, not the split` · `tasks.outputs.<F>: <reason>` |
| `runs` | a map, at least one; a name `[A-Za-z0-9-]+` | `runs is empty — it needs at least one member` · `runs.<name> — a run name is letters, digits and dashes: it names the folder chain/<name>/` |
| `runs.<name>.rounds` | non-empty arrays of arm names of defined schemes | `runs.<name>.rounds must be a non-empty array of arrays of arm names` · `runs.<name>.rounds[<i>][<j>] names '<arm>', whose scheme '<s>' is not among the schemes (<names>)` |
| across runs | an arm twice in one run; an arm in two runs; a closed arm that is not an arm of an EARLIER run | `runs.<name>.rounds — arm <A> is named twice` · `runs.<name2> — arm <A> is already in run <name1>` · `runs.<name>.closedArms[<i>] names '<A>', which no run names` · `…, an arm of this run` · `…, an arm of a later run <m>` |

The reader is `StepSection` (one step), `FlowSection` (the map and the schemes), `TaskSection` (the block), `SchemeChecks` (one scheme) and `RunSection` (`runs`), each internal and under 300 lines.
An absent `tasks` block means the shell's names (`TaskLimits.Shell`: 4 / 7 / 4, `split`, `splitFix`, `taskCode`, no outputs). `closed` goes through the same `<root>` / `~` expansion as `isolation`.

**What a brief sees: `SchemePositions`.** `Outputs(scheme, steps, tasks)` is every output the scheme's entries bind, in scheme order, each with its position and its producer (the entry's file name,
`2-hA`; the split's or the split fix's id; `tasks` for the block's own outputs). A tasks entry contributes the split's outputs, then the split fix's, then the block's own — in that order, so the
split fix may append to the split's notes. `Visible(scheme, position, id, …)` is what a brief at that position may use: every output of an EARLIER position, the step's OWN outputs (the gate's
template names its own `{{GATE_JSON}}`, `hfix.template.md` its own `{{NOTES}}`) and, inside a tasks entry, the block's steps up to and including `id` — never the block's own `outputs`, which are for the
steps AFTER it. On the SRS scheme of the example: `Outputs` begins `(0, split, SPLIT_NOTES)`, `(0, tasks, CODE_NOTES)`, `(1, 3-hreview, HREVIEW_NOTES)`, and `Visible(SRS, 1, hreview)` holds `SPLIT_NOTES`,
`CODE_NOTES` and `HREVIEW_NOTES`. `CODE_NOTES` there is the path the shell handed SRS's reviewers although no SRS step wrote it (`run-arm.sh:44`, `t-lib.sh:103-105`) — kept as measured, as an optional
binding. `BriefFields.For(spec, visible, tasks, paths, facts)` fills `PLAN BASE BRANCH SCEN_COUNT DEADLINE KNOWN WORK`, `NOTES` (a task coder's `TaskNotes`, else `-` when the step names none, else the path
of the visible binding it names), one field per visible binding, `TASK TASK_BRIEF TASK_FILES BEFORE` for a `taskCode` step and `PROBLEMS` for the split fix. The reader refuses a binding named like any of these fixed
names (`BriefFields.FixedNames` is the one list the builder spells its fields from and the reader refuses by, for steps and the tasks block alike); should one get through anyway, the facts come last and win. The rehearsal and the runner both ask `Visible`, so they cannot fill by two rules.

**One producer per destination, and bound is not produced.** Among the producers a scheme places, two different producers never bind the same normalised path (compared without regard to case):
`hB.outputs.HB_JSON = notes/hA.json` beside `hA`'s would make CHH's fixer read one verdict twice, and is refused at the READ naming the scheme, both producers, both fields and the path. It is per scheme,
not per map: two steps no scheme places together may write one path. `notes` naming an earlier step's output is appending, not producing. A step placed twice in one scheme is refused by the same rule
whenever it binds anything (not ruled on separately). `StepSpec.ModelWritten` is `Outputs` minus the `verdict`: the verdict is what the runner derives; every other binding — the `tasks` block's `CODE_NOTES`
included — is what the MODEL is briefed to write, which the runner never requires: E2.S4 RECORDS an absent one (the `End` line's `missing`) and logs each visible earlier output whose file does not exist
before a consumer's launch, and the arm goes on, as the shell's did. No exit code is added to D16's table.

**The CHH walk.** From the example to the files it binds: `1-code:code` binds `notes/code.md` (`CODE_NOTES`, also `NOTES`); `2-hA:hA` is a `review` in `read` mode with `snapshot: true`, its verdict `HA_JSON` is
`notes/hA.json`; `3-hB:hB` the same with `HB_JSON` → `notes/hB.json`; `4-hfix:hfix` is a `session` in `write` mode with `diffFrom: hA`, so `out/4-hfix.diff` runs from the snapshot taken before the first review to
HEAD after the fix, and its brief at position 3 sees `CODE_NOTES`, `HA_JSON`, `HB_JSON` and its own `HFIX_NOTES` (`notes/hfix.md`) and none of `REVIEW`, `FIX_NOTES`, `GATE_JSON`; `5-review:review` → `notes/review.json`;
`6-fix:fix` → `notes/fix.md`; `7-gate:gate` → `notes/gate.md` and `notes/gate.json` (`status`). Eight files for CHH (`notes/code.md hA.json hB.json hfix.md review.json fix.md gate.md gate.json`); with
`hreview.md` and `split.md`, which CR and SRS bind, the example binds ten, `CODE_NOTES` once. `ExampleExperimentTests.The_example_binds_every_file_the_shell_wrote` holds the set; a scheme placing `4-hfix` before `2-hA` and `3-hB` is refused by the rehearsal
naming the scheme, the position and both fields (`hfix.template.md at schemes.X[1] (4-hfix): the brief has unfilled fields: {{HA_JSON}} {{HB_JSON}}`). That the two calls write two distinct verdict
CONTENTS is E2.S8's CHH scenario.

**Runs.** `runs` replaces `rounds`: a map of run name → `{ rounds: [[arms]…], closedArms: [arm names], closed: [paths] }`. `chain <run>` runs one and keeps its markers under `chain/<run>/` (E5); `closedArms`
are folders resolved through the arm map at lock time and closed for the run (part 7 closed part 6's nine arms this way); `closed` are more paths, expanded. An arm is in one round of one run;
`run-all` and `run-arm` take arms and need no run.

**Templates.** `BriefTemplates.NameOf(stem)` → `<stem>.template.md`, refusing a stem with a separator or `..`; `Shipped` is the eleven stems `1-code 2-review 3-fix gate hr-review-fix t2-coder x-split x-split-fix
hA-trace hB-tests hfix` — part 6's eight and part 7's three, copied byte for byte from the part-7 harness (`/home/jinx/git/coai-exp2/briefs/`); the sums are in `BriefFillerTests`, typed out once.
`BriefTemplateFiles.Read(stem)` reads the experiment root's `briefs/` first, then the shipped folder, so a root may ADD a template a step names. `BriefRehearsal.Problems` fills every template at every position
of every scheme (a tasks entry is three fills: split, split fix, task coder) and returns every problem at once, `<stem>.template.md at schemes.<S>[<i>] (<name or id>): <the filler's reason>`; a template no
folder holds is said at each position that needs it and names the step: `nowhere.template.md at schemes.C[0] (1-code): step code: no brief template 'nowhere.template.md' in <overrides> or <shipped>`;
`StemsUsed` is the stems the schemes place, which `HostFactsReader` reads.

**Deviations of E2.S0 from its spec** (what shipped differently; the plan's §8 item 47 carries the same list):

1. **The output folder is exact, and case is compared only between destinations.** The spec said `notes` or `work` "exactly" but its clash scenarios used `Notes/Code.md`; the first build compared the folder
   without regard to case, and `Notes/x.md` validated although it is a folder no launch grants on a case-sensitive host (review R1). Decided: the folder is exact; the clash scenarios use a case variant after
   the folder (`notes/Code.md` beside `notes/code.md`); the path theory has three tests (paths, field names, the two-fields clash).
2. **Scheme `X` of the rehearsal scenario uses `hfix2`.** `hfix` has `diffFrom: hA`, which a scheme placing it before `hA` makes the reader refuse; the scenario adds `hfix2` (no `diffFrom`, field `HFIXTWO_NOTES`)
   so the file reads and the rehearsal is what refuses.
3. **`StepSpec` and `TaskLimits` carry `Equals` and `GetHashCode`**: a record compares a list by reference, and a re-read of one file must be equal.
4. **Types the spec did not list:** `TaskSection` (the block's members and its ids), `PlacedStep` and `SchemePositions.Steps` (one iteration of placements for the reader and the rehearsal),
   `BriefFields.FixedNames`, `ArmPaths.IsPipelineFile`, the test helper `BriefExample`. `StepKinds.Parse` became `StepKinds.Choices`.
5. **The not-found template row** of `ValidateVerbTests` is built as review R10 asked: a step whose brief stem exists nowhere (exit 4, the template, the step id and the position named); the old test, which
   tests an unknown field in an override, is renamed to say so.
6. **`HostChecks` detects a gate step by kind**, and the two flow rows (arm named twice, tasks entry without its steps) moved from `validate` to the reader.
7. **Prefix guards are stricter than specified:** `ExampleExperimentTests.Every_value_the_template_leaves_unfilled…` refuses the net's own words for four v2 members, and a `brief` edit is in the prefix
   test's list, so break-it (j) turns both guards red.
8. **Decisions taken in review** (not in the spec): a binding cannot be named like a fact (R4); a `notes` never names a verdict (R3); `tasks.splitFix` is not `tasks.split` (R2); `diffFrom` inside a tasks
   entry follows the block's order (R5); a path segment holds no control or format character and no edge space, and `ExecutableRule.Printable` escapes them (R7); a closed arm is an arm of an earlier run only
   (R8); a `taskCode` step is placed only through a tasks entry (R9); a scheme that runs the task pipeline refuses a binding of its files (R11); a binding refused is kept by name so nothing says it twice (R13).

**What this does NOT prove.** That a path exists, that a model writes any file it is briefed to write (the runner records it, never requires it), that a template reads well to a model, that
`metrics.mjs` or any consumer reads the bindings' files (E2.S8's `PinnedConsumer` and E6's check do), or anything about the task pipeline's per-task paths beyond `TaskNotes(id)` (E4). One gap the
reader leaves: a step placed twice in one scheme is refused only through the destination rule, so only when it binds something. One cause is said once: a binding that was refused is kept by name, and a
`verdict`, `status` or `notes` that names it says nothing more.

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
| `NotesFile(binding)` | the binding's relative path under the arm folder (`notes/hA.json`, `work/a/b.md`), each segment through `Segment` (E2.S0: the fixed `CodeNotes … GateJson` members are gone, a step's notes files are its bindings) |
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

### The delegation policy (E2.S0)

**What the code does.** No step starts a subagent unless its spec says so. `StepLaunch.SubagentTools` is `["Agent", "Task"]`, the tool that starts a subagent under both names the CLI has used. `StepLaunch.Options(spec, …)`
applies the mode as above and THEN `Delegation`: unless `spec.Subagents`, `DisallowedTools` is `[Agent, Task, <the mode's deny list>]` and `PermittedTools` is the mode's list without `Agent` and `Task` (ordinal) — so
the policy holds whatever the file's `tools` lists say (the example's `tools.write` keeps `Agent`, as the shell's did; the policy strips it at launch, and `"subagents": true` on a step gives it back). A fresh launch and a
resumed one go through the same `Options`, so a resume is never wider than the launch it continues. With `subagents: true` the policy adds nothing and removes nothing. The incident behind it is the shell's part 6: a Haiku review
step's cost was mostly Opus and Sonnet subagents, and the shell's own repair covered read steps only (`run-arm.sh:90-92`), while the example's Haiku steps are write mode with `Agent` permitted. `StepLaunchTests` pins the argv (`No_step_starts_a_subagent_unless_its_spec_says_so`,
`A_resume_is_never_wider_than_the_launch_it_continues`); the example says `"subagents": false` eleven times.

**What that proves, and what it does not.** The tests prove PRESENCE: the argv carries `--disallowedTools Agent Task …` and no `Agent` in `--allowedTools`. Whether the CLI honours a deny list — on a fresh launch and, the
open question, on a `--resume` of a session that HAD the tool — is the receiver's decision, so it was measured once (`common.security`: a control at a boundary you do not own is a request until you have seen the answer).

**The measurement** (claude 2.1.310, this host, 2026-10-30, by Fable before the spec; `claude -p --model haiku --output-format stream-json --verbose --strict-mcp-config --mcp-config '{"mcpServers":{}}' --permission-mode default`,
a prompt asking the model to start a subagent that replies `PONG`, else to answer `NOAGENT`):

| run | launch | the `init` line's `tools` | `Agent` tool_use events | subagent messages (`parent_tool_use_id` set) | result | `modelUsage` |
|---|---|---|---|---|---|---|
| A | fresh, `--allowedTools Read --disallowedTools Agent Task` | no `Agent`, no `Task` | none | none | `NOAGENT` | haiku only |
| B | `--resume` of A, the same deny | no `Agent`, no `Task` | none | none | `NOAGENT` | haiku only |
| C | fresh, `--allowedTools Read Agent Task` (the positive control) | `Agent` present | two | a Sonnet subagent answered `PONG` | `PONG` | haiku + sonnet |
| D | `--resume` of C with `--allowedTools Read --disallowedTools Agent Task` | no `Agent` | ONE — the model still emitted it, and the CLI refused it: `tool_use_error: No such tool available: Agent. Agent is disabled for this session, in subagents as well as here.` | none | `NOAGENT` | haiku + **`claude-sonnet-5-5`, 5 output tokens, $0.0758** (haiku $0.0074) — although no subagent ran |

Read as: the default deny holds on this CLI build on a fresh launch (A) and on a resumed one (B, D) — on D the session's earlier tool inventory did not survive the resume, and a call the model emitted from memory was
refused by the CLI itself. **The anomaly of D is the caveat for every cost metric**: a resumed haiku attempt carried a sonnet line in `modelUsage` with no subagent — an internal CLI call on resume, whose purpose this
measurement did not establish.

**Three decisions** (E2 stories §8 item 43, for the analysis plan): (1) a delegation check — E6's consumer check, the analysis plan's cost split (M3) — reads TOOL EVENTS (`tool_use` named `Agent` or `Task`, present in D as a
refused call) and SUBAGENT MESSAGES (a line whose `parent_tool_use_id` is non-null), **never `modelUsage` alone**; (2) `modelUsage`'s per-model cost is still the cost, so the analysis reports it beside the delegation
facts and never infers one from the other; (3) M3 re-runs A–D on the measurement CLI version and records the version beside the result, because a later build may change what a resume keeps. Out of bounds here: a
per-step model guard for the subagents a step IS allowed to start, `--agents`, and any change to `CliArgv` or `AgentAskOptions`.

## Briefs (E1.S7)

> Superseded by [*The step contract (E2.S0)*](#the-step-contract-e2s0) where it speaks of a template per step kind and a fixed field set per kind; kept as the E1 record. The filler below is unchanged.

A brief is a template with `{{NAME}}` placeholders, filled once per step from facts the module already holds. The eleven templates are the shell
harness's files, copied byte for byte (`tools/model-arms/briefs/*.template.md`, shipped beside the binary under `briefs/` by the console's content
glob); `The_eleven_shipped_templates_are_the_shells_byte_for_byte` holds their SHA-256 values, typed out once in the test, so an edited copy fails there.
A step names its template by a stem (`"brief": "hA-trace"`), so a template is no longer one per kind: where each shipped template is used in the example, and what a brief at that position sees,
is the placement table below (`NOTES` per the step's `notes` binding; the fields are `SchemePositions.Visible`'s).

| Template | Used by (example) | `NOTES` is | Fields beyond the facts and the visible bindings |
|---|---|---|---|
| `1-code` | `code` | `notes/code.md` | none |
| `2-review` | `review` | `-` (the review writes `notes/review.json`, the `REVIEW` field) | none |
| `3-fix` | `fix` | `notes/fix.md` | none |
| `hr-review-fix` | `hreview` | `notes/hreview.md` | none |
| `gate` | `gate` | `notes/gate.md` | none (`GATE_JSON` is its own status output) |
| `x-split` | `split` | `notes/split.md` | none |
| `x-split-fix` | `splitFix` | `notes/split.md` (appended to) | `PROBLEMS` |
| `t2-coder` | `taskCode` | `notes/<task>-code.md` | `TASK`, `TASK_BRIEF`, `TASK_FILES`, `BEFORE` |
| `hA-trace` | `hA` | `-` | none (`CODE_NOTES` earlier) |
| `hB-tests` | `hB` | `-` | none |
| `hfix` | `hfix` | `notes/hfix.md` | none (`HA_JSON`, `HB_JSON`, `CODE_NOTES` earlier) |

`BriefTemplates.NameOf(stem)` is the one spelling of the file names and refuses a stem with a separator or `..`; `Shipped` is the eleven stems. `BriefFields.For(spec, visible, tasks, paths, facts)` gives the seven
facts (`PLAN BASE BRANCH SCEN_COUNT DEADLINE KNOWN WORK`; `SCEN_COUNT` is printed invariant), `NOTES`, one field per visible binding, and the task or the problems for the steps that carry them. A field a
template does not use is never an error: the shell passed every path to every template, and so does this.

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

`BriefTemplateFiles(shipped, overrides).Read(stem)` (console, `ModelArms.Adapters`) takes a template stem, never a file name, and resolves `BriefTemplates.NameOf` inside, so no
operator text can reach outside the two folders. It returns the experiment root's own copy when it exists, else the shipped one, else
`no brief template '<name>' in <overrides> or <shipped>`. The text is read as strict UTF-8; an override that exists but cannot be read (permissions, invalid UTF-8) is a refusal naming it,
never a fall back to the shipped file. A root `.gitattributes` line marks the templates `-text`, so a checkout never converts the bytes the SHA-256 values pin.

What this does NOT prove: that a brief reads well to a model; that the deadline string has the format the launcher prints (E2.S4 owns it); that the resume notice,
the prompt file or its path are right (E2); or that a template edited in an experiment root still carries every field it needs: such a copy is checked by `Fill`
at run time, not here.

## The root, trust and the host (E1.S8, E1.S10)

The first half (E1.S8): the root's requirements, its trust, executables on a host, and the two data files beside `experiment.json`. Five pure
deciders in `ModelArms.Core.Experiment`, one console adapter, `ModelArms.Adapters.RootFacts`. E1.S10 adds `HostChecks` and the `validate` verb
(the second half, below); the gate (E1.S9), `validate` (E1.S10) and `prep-base` (E4.S4) call the first half.

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

**`RootTrust.Decide(road, currentUid, files, root)`** (D17 a; the `bool posix` became `TrustRoad` in E3.S0 — `Kernel` judges owner and mode as described here, `Portable` the mode alone, see *The platform roads (E3.S0)*) judges facts the console read. On Windows (`NotPerformed`) the answer is `NotPerformed` with the line
`the ownership and mode check of <root> was not performed — POSIX only (D17)`: a check that was not performed is not a check that passed. On POSIX each file whose owner is not the
current user, and each whose mode has a group-write or other-write bit, is a problem of its own naming the path (the mode as three octal digits, with the
`chmod go-w` to run); none is `Trusted`. The caller passes the root folder, `experiment.json` and `scenarios.json`. A trust path that is itself a symbolic link is refused by
`RootFacts.ReadFacts` before any fact is read (`<path> is a symbolic link to '<target>' — the trust check judges the file itself, not a link that can be re-pointed`): the last segment may not
be a link, an ancestor may (`~/git` to `/mnt/d/git` is every WSL home), and a trailing separator is trimmed first because `readlink("link/")` follows the link and reports none.

**`ExecutableResolution.Resolve`** (D17 b) turns a command's executable into the path to run, and the path it returns is the REAL path: the one the kernel opens, every link and every `..` applied in the
kernel's own order. The harness launches and prints that path (not the one written), because judging the real path and launching the written one would leave the link between check and launch for an arm to
re-point. A bare name is searched in the PATH directories (on Windows also with `.exe`, empty entries skipped); a PATH candidate that is a dangling link or a loop is skipped and the search moves on, as `execvp`
does; a name found nowhere is `<name> is not on PATH on this host`. A spelled absolute path must exist and be executable, and one the kernel cannot resolve is refused naming the operating system's reason; a relative path
is refused; the verbatim and device prefixes (`\\?\`, `\\.\`, `\??\`, `//?/`, `//./`, `/??/`) are refused as a spelling on every platform, and so is a NUL character: in an executable (the first case of the textual rule), in every text of the experiment file (refused once in the reader, naming the field; every sentence that echoes input writes a NUL as the two characters `\0`, `ExecutableRule.Printable`), and in a forbidden root, which `PathScope` answers as "under" (fail closed) and `Resolve` refuses before it asks the kernel about the root. `Path.GetFullPath` throws on a NUL, so a NUL is a refusal and never a thrown exception. Both the executable as written and as it really is are judged against both the roots as written and
the roots as they really are (`{written, real} x {roots, real roots}`): a link inside the target that points out of it is refused by its spelling, and a root spelled through a link is compared by where it
really is. A root that does not exist yet (the root `init` is about to make) is resolved through its nearest existing ancestor with the remaining segments appended. The forbidden roots are
`ExecutableRule.ForbiddenRoots(target.source, root)`, compared ignoring case.

**`ScenariosFile.Read`** reads `scenarios.json` (`[{"id","kind","name"}]`, kind `cs` or `node`) with the same strict walk as the experiment file: unknown and missing members named, a repeated id named,
an empty list refused, every problem in one answer. **`BaselineFile`** (D24) is `{"schema":"model-arms/baseline-v1","base","measuredAt","host","mcpFailed","tsFailed","suiteTotal","extensionTotal","note"}`
(the shell's failure keys kept: `mcpFailed` is the main suite's, `tsFailed` the extension's). `Compose` refuses, all problems at once and one sentence each: a run that did not complete (`absent is not zero`);
a completed run that exited non-zero and named no failing test (`the <which> suite exited <code> and named no failing test`); a total of zero (`counted 0 tests`); more failures than tests
(`named <n> failing tests of <total>`); a blank host and a negative total. A completed run that exited 0 with failures named is NOT refused: the exit code is the runner's opinion, the names are the measurement.
The instant is written in UTC ending in `Z`. `Read` refuses unknown members, another schema, a missing member (the note may be empty, not absent), a sha that is not 40 hex and an instant that is not UTC.

**`RootFacts`** (console) reads what the deciders judge: `GitTopOf(path)` (nearest ancestor-or-self holding `.git`, a path that does not exist yet included, asked of the kernel's real path first and of the spelling second), `ThisRepository(baseDirectory)` (nearest ancestor holding
`dew_flow_benchmark.slnx`), `RealPathOf` (the kernel's answer, below), `IsExecutableFile` (the real path resolves, exists and, off Windows, has any execute bit; false, never an exception, for a dangling link, a loop or a
folder), `CurrentUid` (`geteuid(2)` through libc, no process: the number `id -u` prints) and `FileFacts` (the owner, the mode, the kind, the device, the inode and the change time, read by ONE `statx(2)` call
with `AT_SYMLINK_NOFOLLOW` on ONE absolute, separator-trimmed path, so all of them are facts of one inode — a `..` after a symbolic link climbs from the link's target in the kernel's order but from the link's folder in .NET's,
and the two used to read two different files; a trust path that is a link, or holds a NUL, is refused). **No process is started for a fact.** The first version asked `id` and `stat` by name, and a bare name handed to `Process.Start`
is resolved by the runtime beside the binary, in the CURRENT DIRECTORY, then along PATH: a marker-writing `./stat` and `./id` in the target ran, and a clean PATH with the working directory at the target root answered `valid`
while `./git` ran four times (the E1.S10 review, measured). `KernelFacts` (console) is the one P/Invoke file: `struct statx` is defined once for every architecture (`include/uapi/linux/stat.h`: 256 bytes, explicitly padded),
the offsets the module reads are that contract (mask @0, uid @20, mode @28, inode @32, ctime {sec @96, nsec @104}, device major @136, minor @140), probed on this host (x86_64, glibc 2.43, kernel 6.18) against `lstat(2)` and
`stat -c '%u %f %Hd %Ld %i %.9Z'` — field for field equal, the ctime finer than the tool's (the tool prints microseconds: the module's ctime had been 1000× coarser than the kernel's). The request is `STATX_BASIC_STATS`, and the
answer's mask must carry type, mode, owner, inode and ctime, else the file is unread naming what the file system did not report. `FileIdentity.Device` is `major << 32 | minor`, a comparison key and not `makedev`'s encoding.
A libc without `statx` (glibc < 2.28, musl < 1.1.24) is unread by `KernelFacts` — which no longer refuses a platform it is not asked on (E3.S0, the owner's ledger 2.1 overturned this story's decision 4): **macOS and FreeBSD RUN**, on the portable road
(*The platform roads (E3.S0)*, below), which says what it did not verify in one printed line. The old `stat -f` text reader was deleted rather than re-pointed at `/usr/bin/stat`, and a probed `lstat` layout (144 bytes, 64-bit inode:
plain `lstat` on arm64, `lstat$INODE64` on x86_64) is the follow-up of E3 §8 item 25 — not written blind. `RawMode` decodes the type bits (`0xF000`; `0xA000` is a link) and the permission bits (`0xFFF`) of the integer. `Present(path)` is the lstat-level question "is there a directory entry by this name": a dangling
link is there; `ENOENT` and `ENOTDIR` — the two errno POSIX defines as "no such entry along the path", read by `Present` alone, the only place the module reads an errno number — are "nothing there"; any other answer (a folder the user may not search) is
a failure carrying the operating system's words.
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

### The host: `HostChecks`, `validate` and `prep-base`'s guard (E1.S10, D16, D17 c, D18, D23)

`validate` reads the HOST and says, in one answer, everything that stops the experiment running on it; the experiment file itself was read by the gate, so every member the file
refuses is already named there (`target.bulid`, `colour`, a missing `limits` in one answer, exit 4). Three pieces, the first two pure:

- **`HostChecks`** (Core) — `NeededExecutables(experiment, host)` (`git`, `claude`, `dotnet`, the first word of `lowPriority`, `build`, `suite.command` and `extension.command`, `pwsh.exe` when `awake` is `wsl` or
  is `auto` on a WSL host, the coai binary when a scheme places a `gate`-kind step; each once, in that order) and `Decide(experiment, facts)`, one `HostProblem(text, environment)` per finding, all at once. `HostKinds.From`
  names the machine: Windows; else WSL when `WSL_DISTRO_NAME` is set or `/proc/version` mentions Microsoft (ignoring case); else other POSIX.
- **`BriefRehearsal`** (Core) — fills every template the schemes use, at every position of every scheme (E2.S0), through `BriefFiller` and its leak guard (E1.S7 review, R4): the arms are the ones the runs name (an `<scheme>1` for a scheme no run
  uses), each with a token minted here, the facts are the experiment's own plan, base, branch and KNOWN, and each brief is filled for the first arm of its scheme. A KNOWN or an override template that would be
  refused at a step's launch (`C#`, `(C)`: a scheme name in free text) is refused now, naming the template, the position and the field: `1-code.template.md at schemes.C[0] (1-code): the field KNOWN would carry 'C' into a brief — … (D15)`.
- **`HostFactsReader`** and **`SourceFacts`** (console) read what `Decide` judges: the host kind, each needed executable through `ExecutableResolution.Resolve` against the PATH of the **environment map the verb was
  handed** (never the process's) and `ExecutableRule.ForbiddenRoots(target.source, root)`, the entries of that PATH, `scenarios.json`, the brief templates (the root's `briefs` folder first, the shipped one second) and
  what git says about the source: `rev-parse --git-dir`, then `rev-parse --verify --quiet` for `refs/heads/<planBranch>`, `<base>^{commit}` and `<base>:<plan>` (the plan is asked only of a commit that exists). D23 names
  `cat-file -e`, but that exits 128 for a missing object where `GitCommand.AskAsync` takes only exit 1 as "no"; `rev-parse --verify --quiet` asks the same question with 1 as the answer.

| Finding (exit) | Sentence |
|---|---|
| an executable the host lacks (3) | `claude is not on PATH on this host` (the three the harness always needs name themselves), or `<role> <path> does not exist` for a spelled path with no directory entry (`coai.binary /x/coai-mcp does not exist`, `target.suite.command[0] /opt/no-such-dir/tool does not exist`) |
| an executable the configuration names and D17 refuses (4) | `… is really '<real>', which is under '<root>' … (D17)`; `the real path of '<p>' cannot be resolved: <OS reason> — refused rather than assumed safe (D17)` for a spelled path that EXISTS (a dangling link, a loop, a folder the user may not search); `<p> is not an executable file …`; a link into the target on PATH is the operator's configuration, not an unready host |
| the source was not asked (3) | `the source was not checked: git is absent` · `…: git is refused` · `…: PATH is refused` · `…: HOME is refused` (a refused PATH is named before a refused HOME, and both before git's own state) |
| `HOME` or `XDG_CONFIG_HOME` under a root (4) | `HOME '<p>' is under '<root>' — git reads its global config from there … (D17)`, or `is really '<real>'`, `is a relative path`, `cannot be resolved` |
| git could not answer (3) | `git could not answer about target.source: <first line of git's stderr>` — `fatal: detected dubious ownership …` is the operator adding `safe.directory` to THEIR config |
| the source (4) | `target.source <p> does not exist` · `… is not a git repository` · `the plan branch <b> does not exist in <p>` · `the base commit <sha> is not in <p>` · `the plan <plan> does not exist at <sha>` |
| `scenarios.json` (4) | the reader's own sentences, one per line: `holds no scenario`, `lists the id 'S1' 2 times` |
| coai | `a scheme has a gate step and coai is not configured` (4; a gate step is one of kind `gate`) · `coai.settings <p> does not exist` (4); the binary is one of the needed executables, so a missing one is `coai.binary <p> does not exist` at 3 |
| the host (4) | `isolation by file modes is POSIX only — remove "isolation" on Windows` · `awake "wsl" needs a Wsl host; this host is OtherPosix` |
| the flow | none since E2.S0: a tasks entry without its steps and an arm named twice are the reader's refusals now (*The step contract*), so they stop at the gate, not at `validate` |
| the PATH (4) | `PATH holds an empty entry …` · `PATH entry 'bin' is a relative path …` · `PATH entry '<e>' is under '<root>' …` · `PATH entry '<e>' is really '<real>', which is under '<root>' …` · `PATH entry '<e>' cannot be resolved: <why> — refused rather than assumed safe`, each ending `(D17)` |
| a template (4) | `<stem>.template.md at schemes.<S>[<i>] (<name>): <the filler's refusal>` · `no brief template '<name>' in <overrides> or <shipped>` |

**The exit code** is 4 when any finding is not an environment one, else 3 (D16), and it is decided on the TYPE of the answer — `ExecutableAnswer` is `Found(real)`, `Absent(reason)` (the host lacks it: 3) or `Refused(reason)` (the configuration names what the harness will not run: 4) — never on a word in a sentence. The success line is `model-arms validate: <root> is valid on this host (<HostKind>)`, exit 0. The resolved executables are printed first, to
standard output, each as `  <name> → <real path>`: the path the harness will launch (D17 c), never the configured name. Findings go to standard error under `model-arms validate: <n> problem(s):`.
**`prep-base`** runs the same checks and prints them the same way; with none it says `validated; the baseline measurement is not built yet — story E4.S4 of todo/PLAN_model_arms.md` and exits 4. A root that already
has a `baseline.json` never reaches it: the gate refuses (`baseline.json already exists`).

**The PATH (E1.S8 re-review, R5).** A real path for the configured executable is only as safe as the PATH its children inherit: a shebang interpreter (`#!/usr/bin/env node`) and `dotnet` starting `git` look their names up
there. `ExecutableResolution.PathEntryRefusal` refuses an empty entry, a relative one, one under a forbidden root as written, one that really is under one, and one whose existing component the kernel cannot resolve. The real path of an
entry or a root is the kernel's answer climbed over ABSENT components only (`Climb`): a component that does not exist is passed over to the ancestor above, one that exists and cannot be resolved (a dangling link, a loop, a
folder the user may not search) refuses — because the answer above it is not where it will lead once an arm makes something there (`home/link -> <target>/notyet`). A forbidden root that cannot be resolved the same way refuses
every executable and every entry (fail closed, as a NUL root does). `validate` refuses the PATH it was given; it does not rewrite it — the children's filtered PATH at launch is E2's.

**The source's git facts.** Nothing the module starts is looked up by name: every launch names an absolute path the module resolved and judged, under an environment it built, from a working directory it chose
(`ModelArmsArchitectureTests.The_module_launches_nothing_by_a_bare_name`, which scans `tools/model-arms` for `GitCommand.`, the shared launcher given a literal name, `Process.Start(` and a `ProcessStartInfo` built in place; a theory per shape and a companion prove the pattern still matches each, and the fixture's own bare `git`). At this story that is ONE launch — git, by the real path `Resolve` found, under the
verb's environment minus every `GIT_*` name (`GitEnvironment`: a parent's `GIT_DIR`, `GIT_WORK_TREE`, `GIT_CEILING_DIRECTORIES`, `GIT_CONFIG_*`, `GIT_EXEC_PATH`, `GIT_SSH*`… would redirect the question or name a program), with the
source as its working directory — and only while a git was found and PATH is clean, because git looks its own helpers up in PATH. Four questions, each its own launch: is the source the top of a checkout (`rev-parse
--show-toplevel` equal, by real path, to the source: a plain folder INSIDE a checkout answers `--git-dir` with success, measured), and does it hold the plan branch, the base commit and the plan at that commit
(`rev-parse --verify --quiet`, exit 1 is the "no"). A source with no `.git` in it or any ancestor is not a repository and git is not asked (exit 128 means both "not a repository" and "dubious ownership"); the three
later questions are independent, and the plan is asked of a base that exists. **What `rev-parse` runs:** it is a builtin that reads the config and the object database and runs no configured program — no hook, no
`core.fsmonitor` (consulted on an index refresh), no `core.sshCommand` or credential helper (transport), no diff or filter driver, no pager, no editor; `alias.rev-parse` never shadows a builtin. No flag is passed to switch
any of it off: `ValidateVerbTests.A_target_config_that_names_programs_is_read_but_none_of_them_runs` is the observation (a marker shim under every key git 2.53 runs a program from, none ran), and replacing the first question by
`git status --porcelain` makes it go red with `core.fsmonitor` run — the proof it looks. The git that answers is the host's, trusted as the system is; the target's `.git/config` is READ.
**The operator's own global config.** Git reads its global config from `HOME` (`.gitconfig`) and `XDG_CONFIG_HOME` (`git/config`), and the environment passes both as the verb's map holds them — that is where `safe.directory`
lives, and the operator's own config is theirs as `experiment.json` is. A global config can also name a program, a file git appends its trace events to (observed: git appended `trace2` events to a file the target chose) or
`safe.directory=*`, so a `HOME` or `XDG_CONFIG_HOME` that an arm could write to is the same hole as a PATH entry. `validate` refuses (exit 4, naming the variable and D17) a variable whose value is relative, is under
a forbidden root as written or by its kernel real path (the same `Climb` as a PATH entry), or has an existing component the kernel cannot resolve; an unset or empty variable is fine, and while either is refused the source is
not asked (`the source was not checked: HOME is refused`; a refused PATH is named first). `ValidateVerbTests.A_home_the_target_could_write_is_refused_and_git_is_not_asked_there` puts the `trace2` config under the target's
`h/` and `x/` and sees no trace file. Not covered: `GIT_CONFIG_GLOBAL` and the other `GIT_*` names are removed by `GitEnvironment`, not refused; Windows' `USERPROFILE` is not looked at.

**`scenarios.json` is read here, not by the gate** (E1.S9: the gate judges it in step 6 and keeps the facts it judged it by, `ExperimentInput.JudgedScenarios`). The reader reads it at the path those facts name (the normalised path
the gate judged), then stats it again with the gate's own helper (`ExperimentGate.StillTheSame`, now internal) and refuses with exit 4 when `RootTrust.Changes` is not empty
(`scenarios.json changed between its trust check and its read (inode, ctime) — refused (D17)`). A verb that was handed no judged facts refuses (exit 3) rather than read the file unjudged.

**Deviations from the story's text**, each because the code's contract differs:
1. An executable that D17 refuses is exit 4; the story says every failed `Path` is the environment. A link into the target on PATH is a wrong configuration, and the story's own test (`suite.command[0]` under the target → 4
   naming D17) expects it. The first version decided that on a substring of the reason (`(D17)`), which made an absent spelled file exit 4 and the same file present without `x` exit 3; the review replaced it with the typed
   `ExecutableAnswer`, and the exit is decided on the type.
2. `HostFacts` carries `Source` (a `SourceAnswer`: Missing, Asked, NotAsked, Unanswered — in place of the story's five booleans, so a git that could not answer is never a "no"), `PathRefusals` and `Templates`, all `required`;
   and `HostFactsReader.ReadAsync(ExperimentInput, GateSeams)` returns a `Passage<HostFacts>` — the swap of `scenarios.json` is a stop with its own exit code, not a fact. `ValidateVerb.RunAsync` has an internal overload taking
   the gate's seams, so a test can stand where the race is. `NeededExecutables` returns `NeededExecutable(role, name)` so an absent one is named by the field that asked for it.
3. `ExperimentRoot.WithTargetAsync` writes the coai settings where the example names them (`<temp>/example/vault/connectOtherAis/wsl-ubuntu/settings.json`), not `coai/settings.json`.
4. Two Core types beyond the story's list: `BriefRehearsal` (R4), and `Scheme.KindsUsed` / `SchemeEntry.Tasks.Kinds` (one reading of which step kinds a scheme runs, used by `Decide` and the rehearsal) — both replaced by `SchemePositions.Steps` in E2.S0.

What the tests do NOT prove:
- **That the fabricated executables run.** The fixture's `claude`, `dotnet`, `node`, `npm` and `pwsh.exe` are empty files: only their resolution is exercised; its `git` is a link to the host's, and it is the only program launched.
  The git that answers is the host's, trusted as the kernel is; the target's `.git/config` is read, and that no program runs from it is observed for the keys `A_target_config_that_names_programs_is_read_but_none_of_them_runs`
  lists (the keys of git 2.53 known to name one), not for a key git learns later.
- **The window between `Resolve` and the launch.** Whoever can write ABOVE the roots can re-point the real path of the git that was found (the gate's own residual, E1.S9); a hard link is not seen at all (E1.S8).
- **File systems and platforms.** `statx` was read on this host's ext4 and the scratch tmpfs; a file system that omits a basic field is refused, not observed. macOS and FreeBSD take the portable road (E3.S0, *The platform roads*; unobserved on a real Mac) and Windows is
  `NotPerformed`; the shim and link scenarios skip on Windows. `EACCES` on `Present` off Linux — or on a Linux without a working `statx` — reads as absent. The product's own bare `git` launches in `src` (`GitHistory`,
  `ScoutTrees`) are the benchmark's flows over the operator's clones, named and left; the structural test covers `tools/model-arms` only.
- **The PATH beyond one validation.** The check sees the PATH of the map at the moment of `validate`; nothing keeps it so, and the children get the inherited PATH at launch until E2 filters it. A dangling link or a link made
  after the check is not seen, a hard link not at all (see the first-half gaps), and an entry that does not exist is judged by its nearest existing ancestor, so an entry outside the roots that an arm later creates is not seen.
- **The rehearsal's stand-ins.** Fields only a run knows (a task's files, the deadline, a split's problem list) are filled with names-free stand-ins, so a name those carry later is not found here. The arms are the runs' arms
  with minted tokens, not the `arms.map.json` of `make`.
- **A gate-side race.** `scenarios.json` is stat-ed again after the read, with the facts the gate judged it by: a swap in the window between that first stat and the read is seen, a swap after the second stat is not, and an
  inode number reused by a file system is narrowed by the ctime but not closed. The re-stat is a POSIX check; on Windows the trust check is declared not performed and so is this one.
- **Windows.** The fixture's modes, links and executables are POSIX; the host-kind and `Decide` cases that name Windows run on fabricated facts on every platform, but nothing here has run on a Windows host.
- **The unrefused.** A configured `claude` or `dotnet` that exists and is executable is not run, versioned or logged in; a coai binary that exists is not started; `isolation` is not applied.

## The command line (E1.S9)

`model-arms <verb> [positional words] [--flags]`. The line is read in two halves (D20) so the shared flag parser stays flags-only: `ModelArmsCommandLine.Split(args)` (Core, pure)
takes the verb and exactly its positional words by a per-verb table, and `Bench.Cli.CommandLine.Parse` reads the rest unchanged.

| Verb | Words | Verb | Words |
|---|---|---|---|
| `init` | `<root>` | `run-arm`, `checks` | `<arm>` |
| `run-all` | `<arm>...` (one or more) | `gate-lock` | `status` or `hold` |
| `validate`, `prep-base`, `make`, `lock`, `unlock`, `chain`, `status`, `version`, `help` | none | | |

Each verb names the flags it takes, and any other `--name` is refused naming them (`'--experimnt' is not a flag of validate — validate takes the flags --experiment`), so a misspelt `--experiment` cannot be dropped
while the root falls back to the environment: `--experiment` for every verb that reads an experiment, `--seconds` for `gate-lock`, `--parse-only` for `checks`, none for `init`, `version` and `help`.
`run-all` also allows `--seconds`: no spec names a flag for it yet (E4.S3 specifies the verb), and the command line's first scenario passes one; the later epics' flags (E4.S1 `lock`, E4.S4 `prep-base`/`make`/`status`, E4.S5
`chain`) are added to the table by the story that specifies them.

No arguments is `help`. A first word that is not a verb is refused naming every verb. The words are the consecutive tokens after the verb that do not start with `--`; a count outside the
verb's shape, or a word outside `status|hold`, is refused (`<verb> takes <shape>: model-arms <usage>`). After the words a `--flag` may be followed by ONE bare token, its value (the shared
parser's own rule); any other bare token is refused (`'<x>' is not a flag or a value`). `VerbLine.Rest` is the verb and the flag tokens.

**`Program`** (console) maps what happened to the exit codes of D16: `0` the verb did its work; `3` a fault nobody expected (`model-arms <verb>: unexpected <Type>: <message>`) or a host that could not
say; `4` a line that does not split, a flag the verb cannot read (`CommandLineException`), a verb whose story is not built yet (`model-arms <verb>: not built yet — story <id> of todo/PLAN_model_arms.md`);
`5` a stop the operator asked for (Ctrl+C, SIGTERM), after which the same command resumes. `Dispatch` has one `"verb" =>` arm per line, and
`ModelArmsProgramTests.Every_verb_the_command_line_accepts_has_a_dispatch_arm` reads the source and holds the arms equal to `ModelArmsCommandLine.Verbs`. `version` and `help` need no root.
Which story replaces which `NotBuilt` arm: `init` E1.S11, `validate` E1.S10, `prep-base` and `make` and `status` E4.S4, `lock` and `unlock` E4.S1, `run-arm` E2.S7, `run-all` E4.S3, `chain` E4.S5,
`checks` E5.S2, `gate-lock` E2.S6.

**`ExperimentGate.RunAsync(input, verb)`** is the one road in for every verb that reads an experiment (a later story replaces a `NotBuilt` arm with `ExperimentGate.RunAsync(input, SomeVerb.RunAsync)`).
Each step refuses what the one before did not, and a refusal ends the walk:

1. The root: `--experiment`, else `MODEL_ARMS_EXPERIMENT` (the map the verb was given, never the process's own); none is exit 4. It is made absolute and its trailing separator trimmed ONCE.
2. `RootRequirements.ToRead`: the root and `experiment.json` exist (exit 4, naming `init`).
3. Trust (D17 a) of the root and `experiment.json`. On Windows the `NotPerformed` line is printed to the error writer and the walk goes on. Elsewhere `RootFacts.ReadFacts` is asked for the NORMALISED
   paths and its answer is judged: a kernel read that fails is exit 3 with its reason (the host could not say); a refusal (a symbolic link, a NUL, a root that is not a folder, a file that is not a regular
   file — `is a FIFO, not a regular file` — a wrong owner, a group- or other-writable mode) is exit 4 with every problem printed. When one path is unread and another refused, the exit stays 3 and both are
   printed, the unread lines first. `FileFacts.Path` is the path whose owner, mode and inode were read; the ONE `statx` call also reads the device, the inode and the change time with nanoseconds, and
   `FileFacts.Identity` carries them with the kind of file from the type bits.
4. `experiment.json` is read from that judged path, never from the operator's spelling again (a file that cannot be read is exit 3; the read observes the stop token), then stat-ed AGAIN: a device, inode,
   type, owner, mode or change time (`ctime`) that differs from what was judged is exit 4 (`experiment.json changed between its trust check and its read — refused (D17)`, naming the facts that differ; `RootTrust.Changes`). It then goes through `ExperimentFile.Read` with the home directory (`HOME`, else
   `USERPROFILE`), the git checkout enclosing the root and this binary's repository. A refusal prints `experiment.json is refused:` and each problem indented (exit 4).
5. `RootRequirements.For(verb, arms, scenarios, baseline)` with the arms the verb was given (`run-arm`, `run-all`, `checks`) and none otherwise: the first file missing names the verb that writes it (exit 4).
6. Trust of `scenarios.json` when the root has one, as in 3.
7. The verb, with `ExperimentInput(input, RootPaths(root), experiment)`.

`RootFacts.ReadFacts` (with an expected `FileKind`: the gate asks for a directory and regular files) is `FileFacts` with the cause kept (`FactsRead.Read`, `.Refused`, `.Unread`): the gate needs the difference between a file the operator must fix (4) and a host that could not be
asked (3), and `FileFacts` is now its two-state view. The gate and its seams are synchronous: there is no process to wait for.

**The residual race (D17).** A file read by path cannot be tied to the `stat` before it. Before the second look, 18 of 1500 gate passes read a link's target swapped in with an `mv -T` loop between the first
`stat` and the read (measured by the E1.S9 review). The second `stat` refuses a swap that is still visible after the read; its change time also refuses a swap and a swap back
(`ln -f experiment.json .keep; mv -T link experiment.json; mv -T .keep experiment.json`), which leaves the same inode under the name and passed 101 of 1500 passes with device and inode alone. It does not close:
inode numbers can be reused on some file systems (the ctime narrows that but does not close it); a swap AFTER the second `stat`; and anything above the root — whoever can write the root (only the operator, by D17)
or ANY ANCESTOR of it can swap the root or an ancestor, because ancestors are not judged. The gate reads `experiment.json` only; `scenarios.json` is judged by the gate and read by the verb (E1.S10), which must
look at it again after reading it, with the same `RootTrust.Changes`.

What the tests do NOT prove: the race itself (the swap test stands inside the window through `GateSeams.AfterRead`, deterministically; no test races a real writer against the gate); the trust steps on Windows
(declared not performed, never run); the ctime on a file system that keeps none or keeps seconds only (the comparison then only narrows less); that `FactsRead.Unread` is produced by a real kernel read that fails (the seam test feeds it, and the unreadable `experiment.json` stands in for the real host); macOS and FreeBSD (the trust check is refused there as unprobed, and the platform test is written, not observed on this host); that the process-level `Main` maps Ctrl+C to exit 5 (the seam under it is tested with a token cancelled by hand, `ShutdownSignal` has its own
tests); that a verb reads `scenarios.json` from the path the gate judged (the gate judges `RootPaths.Of(scenarios)` and passes the root on; the verb that reads the file, E1.S10, reads that same path and is the
next reader of it).

## The first run: init and validate (E1.S11)

The order of a first run, each step refusing what the one before did not leave (D18): **`init <root>`** → the operator fills `experiment.json` and `scenarios.json` → **`validate`** → **`prep-base`** (its guard today;
the baseline is E4.S4). `init` is the only verb that does not go through `ExperimentGate`: there is no experiment to read and the root it is asked about does not exist yet.

**The worked example and the template (D24, D18).** `tools/model-arms/examples/part6.experiment.json` is the part-6 run's values with `/example/...` paths and `//` comments. It travels beside the binary
(`examples/`, a content glob in the console's project file, so the unit is the one file) and is the ONE source of two things: `ExampleExperimentTests.The_shipped_part6_example_validates_over_a_fixture_root_once_its_paths_exist`
creates the paths it names on a fixture root and `validate` accepts it (exit 0), and `ExperimentTemplate.From(exampleText)` (Core, pure) derives the template `init` writes. The template is the example read with
`Utf8JsonReader` (comments allowed) and written with `Utf8JsonWriter` (indented, `\n`, unescaped `<` and `>`): property names, structure, array lengths and comments are kept, every string value becomes `<fill me>`
EXCEPT the direct values of the top-level members `schema`, `scenarios` and `baseline` (a fixed schema name and two file names), and every number becomes `0`. The two cannot drift: a member added to the example is in the
template, and `ExampleExperimentTests.The_init_template_is_the_example_with_its_values_blanked` reads both back and compares every path, kind and array length, every string and every number, and that each comment of
the example occurs in the template.

**Comments.** JSON has no comments; the example's `//` lines are written back as `/* … */` (the writer's one comment form, plan deviation 7), and the experiment reader skips both. The writer puts a comment right
after the value written before it, which would hang the comment about `target` under `schema`'s value, ahead of the comma; the template holds a comment back and writes it behind the property name that follows it
(`"target":` then `/* The repository the arms work on: … */` then `{`), pinned by `A_comment_sits_after_the_name_of_the_member_it_describes_not_after_the_value_before_it`. A comment with no name after it
(before a closing bracket) stays where it was.

**`init <root>`** takes exactly one word and no flag (`ModelArmsCommandLine`). The root is made absolute and its trailing separator trimmed, then, in this order, each refusal ending the walk:

1. **The root is there** — exit 4, `RootRequirements.For("init", …)` through `FirstUnmet`: `<root> already exists — init creates a new root and never writes into an existing one`. "There" is the directory entry by that name
   (`RootFacts.Host.Present`, the kernel's `statx` without following a link), so a root that is a symbolic link — live or dangling — exists and is refused; `File.Exists`/`Directory.Exists` would call a dangling link absent
   and `init` would then fail trying to make it. A kernel that cannot say is exit 3.
2. **The parent does not exist** — exit 4, `<parent> does not exist — init creates one folder, not a path`. "Exists" is the directory entry (`RootFacts.Host.Present`), so a dangling-link parent passes here and is named in step 4.
3. **Placement** — exit 4, `ExperimentFile.Placement` (the same two rules the gate applies to every `experiment.json`): `the experiment root <root> is inside this repository (<repo>) — a root lives outside every checkout`
   and/or `… is inside the git checkout <top> — the arms' clones would nest in it`. `RootFacts.GitTopOf` answers for a root that does not exist yet, and asks of where the root REALLY is: the kernel's real path of its nearest existing ancestor with the absent segments appended
   (`ExecutableResolution.RealPathClimbing`), the spelling second. A root spelled `L/b/exp` with `L` a link into a checkout has no checkout above its text; the gate asks the same question the same way, so `validate` refuses such a root too.
4. **The parent is a symbolic link** — exit 4, `<parent> is a symbolic link to '<target>' — the trust check judges the file itself, not a link that can be re-pointed; put the real file here or pass its real path (D17)` (the
   sentence is `RootFacts.ReadFacts`'s, asked with the expected kind `Directory`; a parent that is another kind of file is refused the same way, a read the host could not make is exit 3). Only the PARENT is
   judged: an ancestor above it may be a link, as the gate allows (a WSL home usually is). Off Windows only: there the verb prints `the symbolic-link and owner-only checks were not performed — POSIX only (D17)`.
5. **The shipped example cannot be read** — exit 3, before anything is made.

Then it makes the root EXCLUSIVELY: `mkdir(2)` through `KernelFacts.MakeFolder` (a P/Invoke with `SetLastError`, mode `0700`), which answers `EEXIST` for anything at the name — a folder another process made since step 1 is exit 4
(`<root> already exists …`) and is left exactly as it was (`Directory.CreateDirectory` would adopt it silently and keep its mode, a `0777` folder staying `0777`); any other errno is exit 3 with the operating system's words. Since E3.S0 the
`mkdir` is libc's on every POSIX host and a failure is classified by `Present` (an entry is there: exit 4; none: exit 3 with the words of the failure), not by `EEXIST`'s number; on Windows there are no modes and the runtime makes the folder. The root is then judged again with `ReadFacts` (a link swapped in since step 1 is refused before a file goes through it), and
its mode is SET (`File.SetUnixFileMode` 0700): the mode given to `mkdir` is narrowed by the umask, so a umask that takes the owner's bits (`0277`) would leave a folder `init` cannot fill. `briefs/` is made and set the same way, and
`experiment.json` (the template) and `scenarios.json` (`[]` and a newline) are written with `FileMode.CreateNew`, their mode set to 0600 on the open handle before the first byte. An existing file or a link at the name is an error and never
written through. A host that will not do any of it is exit 3 (`cannot create <path>: <reason>`), adding `init stopped part-way: remove <root> and run init again` when a part-made root is left behind; `init` removes nothing itself.
A stop signal is honoured once, before anything is made (a root half-made by a stop would be refused as existing by the same command run again).
On success: exit 0 and one line on standard output,

```
created <root>: experiment.json (a template — replace every <fill me>), scenarios.json (empty), briefs/ (template overrides by name). Next: model-arms validate --experiment <root>
```

The modes are what make the next step pass: the gate's trust check (D17 a) wants the root and its files owned by the operator and not group- or other-writable, and `0700`/`0600` are that under any umask, because they are set and not only asked for
(`InitVerbTests.Init_makes_the_root_and_its_files_owner_only_under_this_processs_umask` reads the real modes back under the test process's umask, and
`ModelArmsScenarioTests.Init_through_the_binary_makes_the_modes_owner_only_under_a_umask_that_would_leave_the_owner_nothing` runs the binary in a shell that set `umask 0277` first — the child's own umask, so no state of the test process is touched; `Init_then_validate_over_a_filled_experiment_reaches_the_host_checks_not_a_trust_refusal` fills the root with
the fixture's `experiment.json` and `scenarios.json` and `validate` exits 0).

**The walk, as it runs** (by hand, once, on this host: `model-arms init /tmp/ma-try && model-arms validate --experiment /tmp/ma-try`). `validate` on the template exits 4: every member is present (no line says `is missing`;
`ExampleExperimentTests.A_template_is_read_back_by_the_experiment_reader_with_its_comments_and_every_required_member` holds that in Core), and every value is unfilled, so the gate prints the reader's problems, 259 of
them on the shipped example as of E2.S0's schema v2 (one per unfilled value or refusal; a free text or a list element no other rule rejects gets `<path> is still the template's <fill me> — replace it`), one per line:

```
model-arms validate: experiment.json is refused:
  steps.code.minutes is 0 — it must be greater than zero
  steps.code.mode is '<fill me>' — legal: write, read, gate
  …
  steps.code.brief: '<fill me>' is not a template stem — letters, digits and dashes, no separator and no '..' (hA-trace names hA-trace.template.md)
  steps.code.outputs.CODE_NOTES: '<fill me>' is not an output path — relative, under notes/ or work/, forward slashes, no '..' (notes/hA.json)
  schemes.C[0] is '<fill me>' — an entry is "tasks" or "<file name>:<step id>", such as "1-code:code"
  …
  target.source is '<fill me>' — it must be an absolute path
  target.base is '<fill me>': a commit sha is 40 hexadecimal characters, got 9 — abbreviations are ambiguous and are refused
  runs.part6.rounds[0][0]: '<fill me>' is not an arm name — letters then a number, such as C1 or SRS2
  …
  limits.waitSeconds is 0 — it must be greater than zero
  …
  coai.binary is '<fill me>' — it must be an absolute path
  isolation.closed[0] is '<fill me>' — it must be <root>, start with <root>/ or ~/, or be an absolute path
  awake is '<fill me>' — legal: auto, windows, wsl, none
```

The operator replaces every `<fill me>` and every `0` (the example beside the binary shows what a filled file looks like), writes the scenarios (`[{"id","kind","name"}]`, kind `cs` or `node`; an empty list is refused:
`scenarios.json holds no scenario`), and runs `validate` again. Once the file is valid the refusals are the host's, quoted in the table of *The host* above (exit 3 for a CLI the host lacks, 4 for the rest); a pass prints the
resolved executables and `model-arms validate: <root> is valid on this host (<HostKind>)`, exit 0. `prep-base` then runs the same checks and today says `validated; the baseline measurement is not built yet — story E4.S4`
(exit 4). A verb that needs a file the first run has not made yet names it and the verb that writes it (`<root> does not exist — `model-arms init <root>` creates it`, `<path> is missing — `model-arms <verb>` writes it`).

Every refusal of `init`, as the binary prints it (all lines start `model-arms init: `; the exit is the right-hand column):

| Situation | Line | Exit |
|---|---|---|
| the root exists, as a folder, a file or a link (live or dangling), including one made after the check | `<root> already exists — init creates a new root and never writes into an existing one` | 4 |
| the parent is missing | `<parent> does not exist — init creates one folder, not a path` | 4 |
| inside a git checkout / inside this repository | `the experiment root <root> is inside the git checkout <top> — the arms' clones would nest in it` · `… is inside this repository (<repo>) — a root lives outside every checkout` | 4 |
| the parent is a symbolic link, live or dangling | `<parent> is a symbolic link to '<target>' — the trust check judges the file itself, …` | 4 |
| an empty name · a NUL character | `the root is an empty name — pass the folder to create` · `the root '<root>' holds a NUL character` | 4 |
| no word, or two (the command line) | `init takes exactly one argument, the root folder: model-arms init <root>` (no `init:` prefix) | 4 |
| the host will not make it (an errno other than `EEXIST`, a platform whose `mkdir` is not probed) | `cannot create <path>: <reason>` (+ `— init stopped part-way: remove <root> and run init again`) · `the shipped example <file> cannot be read: <reason>` | 3 |

**Deviations from the story's text**, each because the code's contract differs:
1. The story's `InitVerb.RunAsync` checks "the root exists" with the module's table; the verb asks the kernel for the directory entry (`RootFacts.Host.Present`) rather than `File.Exists || Directory.Exists`, which is what
   the gate's own step 2 uses: a dangling link at the root would pass the latter and `CreateDirectory` would fail on it.
2. The story's list has no link check; the review of E1.S10 bound one: a parent folder that is a symbolic link is refused (exit 4), the root is judged again after it is made, and the files are created exclusively.
   The gate itself does not judge ancestors, so the check covers the parent only. The fix round after the E1.S11 review added: the root is made by an exclusive `mkdir(2)` and its mode set explicitly (the story's
   `Directory.CreateDirectory(root, mode)` adopts an existing folder and is narrowed by the umask); the parent's existence is the directory entry, not `Directory.Exists`; `GitTopOf` is asked of the kernel's real path; and
   `ExperimentFile.Read` refuses any string still equal to `<fill me>` (`UnfilledValues`), which the story did not name.
3. `ExperimentGate.Home` is `internal` (was `private`) so `init` builds the same `ExperimentLocation` as the gate; `ScenariosFile.FileName` is `public const` (was `private`) so the file's name is spelt once.
4. The comments are placed behind the name they describe (above), a change to the writer's own placement that the story's text ("comments kept") does not mention.
5. `InitVerbTests.Init_refuses_a_root_inside_a_git_checkout` makes its checkout in a `TempFolder` (a `testsToDelete-…` folder holding a `.git` folder: `GitTopOf` asks for existence only) and not with `TempGitRepo`, whose
   `bench-src-<guid>` folder carries no leftover prefix.
6. One test beyond the story's six: the scenario `ModelArmsScenarioTests.Init_through_the_binary_makes_a_root_that_validate_reads_and_refuses_until_it_is_filled` (the binary as a process, init then validate then init
   again), because a new flow ships with its scenario (common.scenario-tests) and the catalogue row now names it.

What the tests do NOT prove:
- **The window between the parent's judgement and the `mkdir`.** The parent is judged (not a link), then the root is made through the path, and `mkdir` follows every link ABOVE the root. Whoever can write an ancestor of the
  parent (the grandparent or higher) can swap the parent for a link in that window and redirect where the root is made; the second judgement then sees a real folder at the redirected place and passes. This is the gate's own ancestor
  residual (it does not judge ancestors either), no test stands in the window, and `init` makes no claim beyond the parent. Name only the operator's own folders as the parent.
- **A race for one root.** Off Windows, two `init` processes racing for one name cannot both make it: `mkdir` is exclusive and the loser is exit 4 (on Windows the runtime makes the folder and one made at the name between the check and the creation is adopted silently; Windows already declares the link and mode checks not performed) (`Init_never_adopts_a_folder_that_appears_at_the_root_after_the_existence_check` puts the folder in
  by a seam and `The_kernels_mkdir_…` reads the three answers from the real call). The second `ReadFacts` after the creation is not tested — no seam stands between the creation and it. Removing that second judgement turns no test red.
- **An ancestor that is a link.** Only the parent folder is judged; a link above it is accepted, as the gate accepts it.
- **The modes on a file system without POSIX modes** (a Windows drive mounted into WSL, `/mnt/c`): the test reads the modes on this host's `tmpfs`/ext4 back; on a mount that ignores `chmod`, `init` asks for 0700/0600 and
  the next `validate` reports what the file system says (an untrusted mode, exit 4), not `init`.
- **Windows.** The modes and links are POSIX; there the verb prints that the checks were not performed and the tests skip. Nothing here has run on a Windows host.
- **A umask in this process.** The in-process modes test runs under the test process's umask; the `0277` case runs the binary as a child, so a umask that takes the owner's bits is observed through a shell, not through the in-process path.
- **A comment that holds `*/`.** The writer refuses such a comment; the shipped example has none, and the template of an example that had one would throw. The example is a file that ships, tested on every run.
- **That a filled template is a GOOD experiment.** `init` followed by the fixture's values validates; whether an operator's values are right is `validate`'s and the pilot's.

## The result envelope and the step's end (E2.S1)

Two pure types in `ModelArms.Core` answer one question — how did this attempt end? — and every later decision (done or not, wait or stop, resume or recover) is a function of the answer. The runner reads the attempt's stream and its `.err`
and hands the TEXT in; neither type opens a file.

**`ClaudeResultEnvelope.LastOf(stream)`** (`Launch/ClaudeResultEnvelope.cs`) reads the LAST line of a `stream-json` stream that is a JSON object whose `type` is the string `result`, wherever the key sits. The recorded 429 line has `"type":"result"` 20th of 25 keys,
at character 1264 of 1418, so a reader that looks at the start of a line (`StartsWith("{\"type\":\"result\"")`, the shape `grep '"type":"result"'` suggests) never sees it. The reading rules:

- The stream is split on `\n`, one trailing `\r` dropped per line, lines numbered from 1. A line that is blank, or whose first non-space character is not `{`, is skipped without parsing. Every other line is parsed with `AllowDuplicateProperties` and `AllowTrailingCommas`
  (the same options as `ProbeJson`, for the same reason: a duplicated key in a real stream must skip a line and never fault the step). The WHOLE reading of a line — parse, the `type` check and every field — is inside one guard that catches `JsonException`
  (malformed), `ArgumentException` (a RAW lone surrogate cannot be transcoded to UTF-8) and `InvalidOperationException` (an ESCAPED lone surrogate in a key or in a `type` value makes `TryGetProperty` and `ValueEquals` throw, and so does any wrong-kind `Get…`); a line that raises one is
  skipped. The walk starts at the END of the stream, so the first result object met is the last one. A bad LAST line does not hide the real last result — but see *A cut last line* below: one that was probably a result puts it in doubt.
- No `GetString`, `GetBoolean` or `GetInt64` is called before the element's kind was checked (the wrong-kind call throws `InvalidOperationException`, not `JsonException` — the product's lesson). One more reader hole, found by a row of
  `An_odd_field_in_a_result_object_reads_as_its_safe_default_and_nothing_throws`: a well-formed JSON string that holds a lone surrogate escape (`"\ud800"`) makes `GetString` throw too, so a string is read through one helper that falls back to decoding the string itself (`Launch/JsonStringText.cs`: every escape as a JSON parser reads it, a well-formed pair kept, a lone half written as U+FFFD; the words in it still count). The first version fell back to the escaped raw JSON text; the E2.S5 review showed that turned a valid verdict beside such an escape into an `unparsed` (E2 §8 item 53).
- `IsError` is false only when `is_error` is the JSON literal `false` or is absent; ANY other value (`true`, the string `"true"`, `1`, `null`, an object) reads as an error — the safe side of the incident's direction, which was a false "done" (the shell took any truthy value as an error; the
  story's text said "only the literal `true`" and is changed, deviation 6). `ApiErrorStatus` is `Number(n)` for an integral JSON number or a string of 1 to 18 ASCII digits, else `Unavailable` with a reason that names the field — absent, `null`, a fraction, an empty or
  non-digit string, a boolean each read as unavailable, never as 0 and never as a throw. `ApiError`, `Result` and `Subtype` are the strings or `""`; `Errors` is the string items of `errors`. `Subtype` is kept and decides nothing: a real 429 prints `"subtype":"success"` beside `"is_error":true`.
- Failure (`no line of the stream is a result object`) is a value, never a throw.

**The envelope carries no cost, on purpose.** A resumed attempt's result carries the SESSION's cumulative `total_cost_usd` and `modelUsage` (measured on the part-6 attempts, 2026-10; this story does not re-measure it), never the attempt's own. So the cost of a step that was
resumed is the LAST attempt's number per session — never the sum of its attempts' results. The type does not read either field, so no caller can add two of them by mistake; whoever reads cost later (E2.S2's timing line, E4's report) takes it from the last result of the last attempt of each session.

**A cut last line (R7 of the fix round).** If the LAST non-blank line of the stream does not parse (or cannot be read) and holds the text `"type":"result"` (or the same with a space after the colon), it was probably a result that was cut off — and it may have been the failing, last word. `LastOf`
then returns the earlier result it found with `FollowedByUnreadResult` set, and the classifier does not call an earlier SUCCESS finished: the end is `Interrupted` and the step resumes. An earlier limit or error stands (nothing that follows can turn it into a success). Only the last non-blank line is asked.
A cut BEFORE the text `"type":"result"` (the recorded 429 line has it at character 1264 of 1418) leaves nothing that says the line was a result and cannot be told from noise — a stated limit, not a pass.

**`StepClassifier.Classify(lastResult, stderr, attempt)`** (`Steps/StepClassifier.cs`) decides in this order, the first match winning. `<text>` is the result when it is 200 characters or fewer, else its first 200 followed by `…` (the cut never lands inside a surrogate pair);
`an error result with no text` when the result is empty or whitespace.

| order | when | answer | the exact reason |
|---|---|---|---|
| 1 | the launch was refused (`!attempt.Started`) | `NotStarted` | `attempt.NotStartedReason`, untouched |
| 2a | a last result whose captured `ApiErrorStatus` is 429 or 529 — WHATEVER `is_error` says | `Limited` | `api_error_status 429 — <text>` (or `529`) |
| 2b | … or an error (`IsError`) and `LimitWords.FirstIn(ApiError)` is not empty | `Limited` | `api_error '<the api_error field>' — <text>` |
| 2c | … or an error and `LimitWords.FirstIn(Result + "\n" + string.Join("\n", Errors))` is not empty | `Limited` | `the words '<match>' in the result — <text>` ("the result" covers the `errors` items) |
| 3 | a last result and `IsError`, none of the above | `Errored` | `<text>` |
| 4a | a last result that is not an error, and the stream's last line was a result line that could not be read (`FollowedByUnreadResult`) | `Interrupted` | `the last line of the stream is a result line that could not be read — an earlier success is not trusted` |
| 4b | a last result that is not an error | `Finished` | — (also when the wall then killed the tree or the operator stopped it: a session that printed its own end has ended; the words are never read from a success) |
| 5 | no result, and `LimitWords.FirstIn` over the last `StderrTailCharacters` (4000) characters of stderr is not empty | `Limited` | `the words '<match>' on stderr` |
| 6 | no result, `WallKilled` | `HardDeadline` | — |
| 7 | no result, `StoppedByOperator` | `Interrupted` | `stopped by the operator` |
| 8 | otherwise | `Interrupted` | `no result line and no limit — the process ended without saying how` |

Stderr is read ONLY when there is no result (the shell's rule, `run-arm.sh:60-62`): a success result with limit words on stderr is `Finished`, an error result with them is decided by its own fields. The classifier reads its three inputs and nothing else — in particular no
`rate_limit_event` line and no field of the synthetic assistant line that carries `api_error_status` 429 at ITS top level: those are facts for the record, not inputs (D6). `Word` is the spelling `.done` and the end line use: `finished`, `hard-deadline`, `limited`, `errored`,
`interrupted`, `not-started`; only `Finished` and `HardDeadline` are done (`IsDone`).

**The words.** `LimitWords.FirstIn` (`Steps/LimitWords.cs`) is the shell's `RX` (`run-arm.sh:56`) character for character, as a .NET regex, `IgnoreCase | CultureInvariant`, match timeout 1000 ms. The timeout is NOT caught and it CAN fire: a pathological text (measured by the review: `hit your ` repeated 20,000 times, 180 KB) throws `RegexMatchTimeoutException` after about a second, while a real result or a 4000-character tail never comes near it. The exception reaches the
step runner — loud, and the step is never marked done — rather than becoming a silent `Errored` or a limit. The words are searched only when `api_error` names no limit (a structured field decides before the words, so a pathological text cannot turn a limit that `api_error` names into an exception; `An_api_error_that_names_a_limit_decides_before_the_words_are_searched`), and once; the text searched is unchanged:

```
usage.?limit|spend limit|session limit|rate.?limit|hit your (\w+ )*limit|limit reached|limit will reset|resets? at|overloaded|\b529\b|\b429\b|too many requests
```

It returns the matched text in its original case. .NET's leftmost-match rule decides which alternative is reported when several could: in `You've hit your individual spend limit …` the match is `hit your individual spend limit` (it starts at character 7, `spend limit` at 23); in
`API Error: 529 Overloaded` it is `529`. The one documented difference from node: `\w` and `\b` are Unicode in .NET and ASCII in node — a superset, so nothing the shell matched is missed. It is this module's own list (D6), not `ReviewerAccountOut` (which refuses a plain 429 on
purpose).

**The incident (2026-10-28).** The part-6 experiment's repair script (`repair-limit-2026-10-28.sh:2-5`, kept as the record) says it plainly: at about 21:45 UTC the session limit was reached (`You've hit your individual spend limit … your session limit resets 1:20am
(Europe/Paris)`, `api_error_status` 429), `run-arm.sh`'s limit detector missed that wording, and every step after it was marked done in seconds — nine of them. The fix it names is this type's order: the structured fields (`api_error_status`, `api_error`) decide first,
the words are the fallback, and an error result is never "done". The recorded review attempt (`2-review.a1.jsonl`) shows the other half: it printed THREE result lines — a success ("I'm waiting for the full MCP suite…"), then two limit results — and that day's `timing.jsonl`
line recorded `"result":true,"limited":false` (E2 document §2). A reader of the first result sees a finished step; `The_2026_10_28_review_attempt_is_limited_by_its_last_result_never_finished_by_its_first` holds both halves.

**Why `ReviewerAccountOut` and `ClaudeEnvelope` are not reused** (reuse-first, each weighed): `ReviewerAccountOut` refuses a plain 429 on purpose (a reviewer's 429 is a transient the product retries, not an account that is out); `ClaudeEnvelope` reads the FIRST recognisable object of
a `json` output — the opposite of what a stream needs — and widening either would change a reader its callers depend on; `ProbeJson` is `internal` to the domain. So this reader is its own, and the product's lessons (`GetString` on the wrong kind throws `InvalidOperationException`)
are applied in it, with the product's envelope lines as fixtures.

**The fixtures** (`tests/Bench.Tests/Fixtures/model-arms/`, read through `ModelArmsFixtures`). A line that carried an arm's token is committed with the token replaced by `0123456789ab` (the same length, the JSON still valid); no committed file holds the token.

| file | source | sha256 (verbatim lines) |
|---|---|---|
| `result-429-usage-limit-reached.jsonl` | the part-6 first arm's `3-fix.a1.jsonl:8`, byte for byte | `895d7980445470e7ad4588cbdea7a0b7878b47582544b58b82d008c59df52a52` |
| `stream-2026-10-28-review-three-results.jsonl` | `2-review.a1.jsonl:310`, `:317`, `:323` (token replaced in line 1 only) | line 2 `20d59284b73beaba53c2127b6dba5e18738813edace799f290a7b8c522551dd8`, line 3 `3087925f6188b168051cda9f95d59d14160b7e5f2c3262524bf870fec715d494` |
| `stream-2026-10-28-fix-limited-head.jsonl` | one synthetic `init` line (the recorded one is 7.4 KB of the operator's machine), then `3-fix.a1.jsonl:6`, `:7`, `:8` byte for byte | line 2 `1ce54cee01e1ad8e934fbb02d38409e4b31b1d8ea1a5a87eed295b1f8e61c03e`, line 3 `5a2a200b22b52aadb7f8658eeb5e33ca6ad9e472cc024836e66a463213677395`, line 4 as above |
| `err-2026-10-28-untrusted-workspace.txt` | the one line of `3-fix.a1.err`, token replaced | — |
| `result-429-monthly-spend-limit.jsonl`, `result-429-string-status.jsonl`, `result-400-model-unsupported.jsonl` | the product's `AClaudeReasonInItsEnvelopeTests.cs` at `80b829ef`, lines 23, 87 and 27, verbatim | — |
| `result-success.jsonl`, `stream-no-result.jsonl` | synthetic (the second is the first's first two lines); `__CWD__` is the fake CLI's own substitution | — |

**What these do NOT prove.** That claude 2.1.x still prints these shapes (the lines are a record, not a contract); anything about a stream's non-result lines (the reader skips them); that claude prints a result before the wall kills it; what a real 529 looks like (the 529 line is the
429 line with another status, and says so); that the words list is complete (it is the shell's list: an account-out message in other words is an `Errored` until the list learns it); and the cumulative-cost fact above, which this story records and does not re-measure.

**Deviations from the story's text**, each because the code's contract or a row that failed first said so:
1. `LimitWords` is its own file (`Steps/LimitWords.cs`): the story lists `StepClassifier.cs (StepClassifier, LimitWords — two files, not one …)` and then counts seventeen files with one; the stated reason (two responsibilities, each short) wins, so the commits carry eighteen paths.
2. `LastOf` walks the stream from its end and returns the first result object it meets, instead of walking forward and keeping the last — the same answer (the LAST result), without keeping and disposing every earlier document.
3. A string holding a lone surrogate escape is decoded with the lone half as U+FFFD (above; first read as its raw JSON text, changed by the E2.S5 fix round, E2 §8 item 53): the story's "nothing throws" covers `GetString` on the wrong kind; this is the same exception from the right kind. The row that found it is `An_odd_field_…`'s last.
4. `Head` never cuts a surrogate pair in two: a lone surrogate in a reason would make the line writers of E2.S2 throw. Found by `A_head_never_cuts_a_surrogate_pair_in_two`, red against the story's exact `text[..200]`.
5. One row and one test beyond the story's 27, for 3 and 4; and `An_odd_field_…` asserts `LineNumber == 1` on every row, because its last row (all defaults) would otherwise pass against the stub.
6. **A deviation from the spec's text, decided by the work's owner after the Opus review (R6, safe side — E2 §8 item 48).** The story said "`IsError` is true only for the JSON literal `true`"; the shell took any truthy `is_error` as an error, and the incident's direction was a false "done". So
   (a) a captured `api_error_status` 429 or 529 is `Limited` whatever `is_error` says (before: only an error could be a limit); (b) an `is_error` that is present and not the literal `false` reads as an error. The story's scenario row `{"type":"result","is_error":"true",…}` → `IsError == false` now reads `true`.
7. **A deviation from the spec's text (R7, same decision).** A cut last result line is not skipped silently: it flags the earlier result (above). The story's scenario `Every_shape_that_is_not_a_result_object_is_skipped_and_nothing_throws` asserted, for the truncated shape, that `LastOf(Success + "\n" + shape)` is the success at line 1 and
   nothing else; it still is, and now also asserts the flag (true for the truncated shape and for the unreadable result line, false for every other shape). `C5` gains one member: `ClaudeResultEnvelope.FollowedByUnreadResult` (an `init` property, no constructor change — like `AttemptFacts.Started`).
8. R1: the guard catches three exceptions where the story named one (`JsonException`); R4: `Status` is split into `StatusOf`, `StatusOfNumber` and the absent case to keep every method at complexity 4 or less; R3: `.gitattributes` has `tests/Bench.Tests/Fixtures/model-arms/** -text`, held by
   `The_fixtures_are_exempt_from_line_ending_conversion_because_their_bytes_are_pinned`; R2: a comment and this document, no code (the words are computed once in the classifier).

### Evidence (E2.S1)

What the runner printed, kept here because a commit message cannot be amended (force-push is blocked). Source runs: 2026-10-30 on this host (WSL, .NET SDK 10.0.114), `Release`, through `low-priority.mjs`.

**The fixtures (spec step 1).** The five hashes recomputed on the files as committed: `result-429-usage-limit-reached.jsonl` `895d7980…52`; the review stream's line 2 `20d59284…1dd8` and line 3 `3087925f…d494`; the fix stream's line 2 `1ce54cee…c03e` and line 3 `5a2a200b…7395` (line 4 equals the first file's hash). All equal
the plan's. The arm token (read from the experiment's map file, never typed) occurs 0 times in every committed fixture and in `tests/Bench.Tests/ModelArms/`; `0123456789ab` occurs once in the review stream and once in the err file.

**The reds of step 3 — the 27 scenarios against the stubs** (66 test cases, every theory row counted, all failing on an assertion; none threw, none was a compile error; the first message of each method):

| scenario | first red |
|---|---|
| `A_result_whose_type_key_comes_last_is_still_read` | Expected value to be 1, but found 0 (difference of -1). |
| `A_stream_without_a_result_reads_as_none` | Expected boolean to be True because a stream with no result line has no result to read, but found False. |
| `An_odd_field_in_a_result_object_reads_as_its_safe_default_and_nothing_throws` | Expected value to be 1 because the object is the stream's one line, but found 0 (difference of -1). |
| `Api_error_status_reads_as_a_number_and_as_a_string` | Expected collection to contain only items matching wasCaptured because the product's 2.1.284 envelope carries the status as a number, and its string form is read as one, bu… |
| `Every_shape_that_is_not_a_result_object_is_skipped_and_nothing_throws` | Expected before to be 2, but found 0 (difference of -2). |
| `Subtype_success_on_an_error_result_is_ignored` | Expected both.Select(envelope => envelope.IsError) to contain only items matching isError because subtype "success" sits beside is_error true on a real 429, but {False, Fal… |
| `The_last_result_line_is_read_whatever_lines_precede_it` | Expected value to be 3 because the third line is the last result; the first is a success that came before the limit, but found 0 (difference of -3). |
| `A_529_overload_is_limited` | Expected End(line) to be Limited { Reason = "api_error_status 529 — API Error: 529 Overloaded" }, but found Interrupted { Why = "stub — not classified yet" }. |
| `A_killed_tree_is_the_hard_deadline` | Expected end to be HardDeadline { }, but found Interrupted { Why = "stub — not classified yet" }. |
| `A_launch_the_executable_refused_is_not_started` | Expected end to be NotStarted { Reason = "the executable /x/bin/claude does not exist" }, but found Interrupted { Why = "stub — not classified yet" }. |
| `A_monthly_spend_limit_429_is_limited` | Expected end to be Limited { Reason = "api_error_status 429 — You've hit your monthly spend limit. Switch to another model to continue." }, but found Interrupted { Why = "s… |
| `A_result_printed_before_the_wall_killed_the_tree_is_finished` | Expected End(Read("result-success.jsonl"), string.Empty, Killed) to be Finished { }, but found Interrupted { Why = "stub — not classified yet" }. |
| `A_stop_by_the_operator_without_a_result_is_interrupted` | Expected End(NoResult, string.Empty, Stopped) to be Interrupted { Why = "stopped by the operator" }, but found Interrupted { Why = "stub — not classified yet" }. |
| `A_string_429_is_limited` | Expected End(line) to be Limited { Reason = "api_error_status 429 — You've hit your monthly spend limit." }, but found Interrupted { Why = "stub — not classified yet" }. |
| `A_success_result_is_finished` | Expected end to be Finished { }, but found Interrupted { Why = "stub — not classified yet" }. |
| `A_success_result_is_finished_whatever_stderr_says` | Expected End(Read("result-success.jsonl"), "Claude AI usage limit reached") to be Finished { }, but found Interrupted { Why = "stub — not classified yet" }. |
| `An_error_result_that_is_not_a_limit_is_errored_never_finished` | Expected end to be Errored { Text = "API Error: 400 Claude Code 2.1.258 does not support this model; version 2.1.280 or newer is required. Run 'claude update', or update th… |
| `An_errored_ends_text_is_the_results_first_200_characters_or_says_there_is_none` | Expected End(ErrorResult(new string('a', 300))) to be Errored { Text = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa… |
| `Every_end_has_its_word_and_only_finished_and_hard_deadline_are_done` | Expected end.Word to be "finished" with a length of 8, but "" has a length of 0, differs near "" (index 0). |
| `Every_word_of_the_shells_limit_pattern_is_a_limit_and_a_plain_error_is_not` | Expected string to be "limit reached" with a length of 13 because the matched text, in its original case, but "" has a length of 0, differs near "" (index 0). |
| `Limit_words_on_stderr_without_a_result_are_limited` | Expected End(NoResult, "Claude AI usage limit reached. Your limit will reset at 1am (Europe/Paris).\n") to be Limited { Reason = "the words 'usage limit' on stderr" }, but … |
| `No_result_and_no_limit_is_interrupted` | Expected end to be Interrupted { Why = "no result line and no limit — the process ended without saying how" }, but found Interrupted { Why = "stub — not classified yet" }. |
| `The_2026_10_28_line_is_limited_by_its_structured_fields_whatever_the_words_say` | Expected verbatim to be Limited { Reason = "api_error_status 429 — You've hit your individual spend limit · run /usage-credits to ask your admin for a higher limit · your s… |
| `The_2026_10_28_review_attempt_is_limited_by_its_last_result_never_finished_by_its_first` | Expected end to be Limited { Reason = "api_error_status 429 — You've hit your individual spend limit · run /usage-credits to ask your admin for a higher limit · your sessio… |
| `The_limited_fix_stream_is_read_from_its_result_line_and_never_from_the_rate_limit_event` | Expected value to be 4, but found 0 (difference of -4). |
| `The_real_untrusted_workspace_warning_on_stderr_is_not_a_limit` | Expected End(NoResult, Untrusted) to be Interrupted { Why = "no result line and no limit — the process ended without saying how" }, but found Interrupted { Why = "stub — no… |
| `The_stderr_tail_is_the_last_4000_characters` | Expected End(NoResult, "rate limit\n"+ new string('x', 4000)) to be Interrupted { Why = "no result line and no limit — the process ended without saying how" } because the w… |

**The break-its of step 5, each planted in the finished code, built, observed red, restored (`git diff` empty).** (a) first result instead of last: `Expected value to be 3 because the third line is the last result; the first is a success that came before the limit, but found 1.` and `…Limited {…}, but found Finished { }` for the review attempt.
(b) words before the structured fields: `The_2026_10_28_line_is_limited_by_its_structured_fields_…` and six more red, e.g. `…Limited { Reason = api_error_status 429 — … }, but found Limited { Reason = the words 'hit your monthly spend limit' in the resu…`. (c) a number-only status: `Expected collection to contain only items matching wasCaptured … but {False} do(es) not match`
and `A_string_429_is_limited` found the words' reason. (d) any `is_error` finished: `An_error_result_that_is_not_a_limit_is_errored_never_finished` found `Finished { IsDone = True }` (33 red in all). (e) stderr read despite a result: `Expected … Finished { }, but found Limited { Reason = the words 'usage limit' on stderr }` (the only red).
(f) the whole stderr: `The_stderr_tail_is_the_last_4000_characters` (the only red). (g) a prefix reader: `expected success, got failure: no line of the stream is a result object` (nine red). (h) the `rate_limit_event` read as a limit: `The_limited_fix_stream_…` red at its first assertion (`Expected boolean to be True, but found False`: `LastOf` of the result-less
text is no longer a failure). (i) `GetString` unguarded: `Did not expect any exception, but found System.InvalidOperationException: The requested operation requires an element of type 'String', but the target element has type 'Number'.` (j) `"is_error":"true"` read as true: obsolete after the fix round (R6 makes it an error).

The fix round's own break-its: (k) the flag never set: `A_last_line_cut_inside_a_result_flags_…` (`Expected … FollowedByUnreadResult to be True because the cut line holds the marker, but found False`), `A_success_followed_by_a_cut_result_line_…` (`Expected … Interrupted {…} because the earlier success is not the last word, but found Finished`) and the shape theory.
(l) a status limit only for an error: `A_captured_429_or_529_is_limited_whatever_is_error_says` (`Expected … Limited { Reason = api_error_status 429 — … }` for `is_error` false; 1 red). (m) `IsError` only for the literal `true`: ten red, e.g. `Expected boolean to be True because only the literal false, or no is_error at all, reads as success …, but found False.`
(n) `ArgumentException` not caught: `A_raw_lone_surrogate_in_a_line_never_throws_…`: `Did not expect any exception, but found System.ArgumentException: Cannot transcode invalid UTF-16 string to UTF-8 JSON text.` (o) `InvalidOperationException` not caught: the two escaped-surrogate rows: `Did not expect any exception, but found System.InvalidOperationException: Cannot read incomplete UTF-16 JSON text as string with missing low surrogate.`
(p) the `.gitattributes` line removed: `The_fixtures_are_exempt_from_line_ending_conversion_…` red. (q) a flagged success finished: the cut-line scenario red.

**The fix round's reds** (the commit before the fix, 18 of 88 test cases of the two classes, all assertions): the escaped-surrogate rows threw `InvalidOperationException` out of `LastOf`, the raw one `ArgumentException`; `"is_error":"true"` and its kin read as success; a captured 429 beside `is_error` false read as `Finished`; a success followed by a cut result line read as `Finished`; `.gitattributes` had no line for the fixtures.

**The runs.** Round 1 (`57341a4`): `ClaudeResultEnvelopeTests`, `StepEndTests` and `ModelArmsArchitectureTests` 83 of 83; namespace `Bench.Tests.ModelArms` 547 total, 544 passed, 3 skipped; whole suite 3611 total, 3025 passed, 572 failed, 14 skipped. Fix round: the three classes 103 of 103; the namespace 567 total, 564 passed, 3 skipped, 0 failed; whole suite 3631 total,
3045 passed, 572 failed, 14 skipped. Both whole-suite runs: every failure is in the main-branch list of failures taken on this host (the Postgres and Docker tests cannot run here) and none is new; one test of that list passed (`ProcessRunnerCancellationTests.External_cancellation_kills_the_child_it_was_waiting_on`). `dotnet format --verify-no-changes` exits 0; the build has 0 errors and one warning,
`ASPIRE010` from the AppHost's Aspire targets, which this change does not touch; `plan-lifecycle.mjs` is clean.

## Timing, budget and recovery (E2.S2)

Eight pure deciders in `ModelArms.Core.Steps` (and two ports in `ModelArms.Core.Ports`) hold what a step leaves on disk and what the next entry does with it: the **timing lines**, the **`.done` marker**, the gate's **`.continue` marker**, the **budget**
arithmetic, the **resume notice**, the **transcript path**, **`StepRecovery.Decide`** and **`StepOutputs.Reconcile`**. None opens a file: the runner (E2.S4) reads the facts and hands them in. The formats are the shell's
(`coai-exp2/run-arm.sh` of the part-6 experiment), kept byte-compatible so `metrics.mjs` reads a line this build wrote (D5); the divergences are named.

**The formats.** One JSON dialect for the three files (`LineJson` writes, `JsonRow` reads): compact, keys in the order below, the relaxed encoder (`+01:00` stays `+01:00`, an em dash stays an em dash — the default encoder would escape both), a lone surrogate written
as U+FFFD, instants UTC as `yyyy-MM-ddTHH:mm:ssZ`, an uncaptured number as JSON `null` (*absent is not zero*).

| line | keys, in order |
|---|---|
| `start` | `kind, arm, step, attempt, how, session, model, start_utc, budget_s, deadline, head, stream` |
| `end` | `kind, arm, step, attempt, end_utc, wall_s, exit, result, limited, head, end`, then `recovered:true` only when recovered, then `missing:[…]` only when non-empty (E2 §8 item 41) |
| `arm-done` | `kind, arm, done_utc` |
| `stopped` | `kind, arm, step, exit, why, at_utc` |
| `gate-wait` | `kind, arm, step, after, wait, why, at_utc` — one DECIDED blocked round of a gate step (`after` the attempt that was blocked, `wait` 1-based); spends no budget (E2 §8 item 44) |
| `.done` | `attempts, exit, stream, end`, then `recovered:true` when recovered — the shell's three keys first (`review_json` and the repair script read `stream`) |
| `.continue` | `after, why` — "attempt `after` FINISHED; continue its session" |

*Reading.* `TimingLines.Read` returns one line per non-empty line of text. A line that is not a JSON object, whose `kind` is unknown, or one of whose REQUIRED fields (`kind, arm, step, attempt, start_utc / end_utc / at_utc / done_utc, budget_s`; `after`, `wait` for a gate-wait) is missing or of
the wrong kind, is `Other(raw)` — kept, never dropped, never a throw. A missing `limited`, `recovered`, `result` or `end` reads `false` / `""`; a missing `missing` reads `[]`; a missing or `null` `wall_s` / `exit` reads `CapturedCount.Unavailable("the file does not carry it")` — that fixed reason is
what makes `Read(Write(x))` equal `x`. A field that is present and of the wrong kind (`"wait":"1"`, `"limited":"yes"`) makes the line `Other` rather than guessing. `DoneMarkers.Read` takes the shell's marker (no `end` → `State = ""`) and the previous generation's
recovered marker (`"attempts":"recovered"`, read as `Attempts = 0, Recovered = true`, no exit); a non-object, a torn file or any other `attempts` is refused, naming it. `ContinueMarkers.Read` wants `after` a positive whole number and `why` text or absent (→ `""`) and refuses
anything else naming `after`, `why` or the shape; a torn file is a value, not a throw.

**Budget.** `UsedSeconds(timing, step)` = Σ `wall_s` over the `end` lines of `step` that are not `limited` and whose wall was captured (a start with no end, a gate-wait, a captured-nothing wall spend nothing — the part-5 shell guessed from the stream's mtime; this build does not).
`RemainingSeconds` = `max(minutes · 60 − used, floor)` (`run-arm.sh:84`). The recorded 2026-10-28 attempt (a 429, recorded `"wall_s":888,"limited":false` by the day's detector) is 888 used and the resume's 1512 = 2400 − 888 — the day's arithmetic, kept as evidence in
`The_remaining_budget_excludes_limited_attempts_and_floors_at_the_minimum`. `Wall(r, g)` = `r + g` seconds (D8). **`Deadline(start, r, local)`** = `HH:mm UTC (HH:mm ±hh:mm)` of `start + r`: the UTC clock, then the local clock with the zone's offset AT THAT INSTANT
(`+01:00`, `-04:00`, `+00:00`) — the shell printed `%Z` (`CET`), .NET has no portable abbreviation and a zone's id differs by host, so the offset is the one spelling every host writes the same (E2 §8 item 32; the minutes are the shell's). The clock, never the date. A deadline
that crosses the DST switch carries the offset of the deadline, not of the start (`2026-10-25T00:30Z` + 1800 s in Paris is `01:00 UTC (02:00 +01:00)`). `ResumeNotice.For(base, deadline)` is the part-6 shell's four lines, `$BASE` and `$deadline` substituted, ending with a newline.

**Transcripts.** `ClaudeHome(env)` = `CLAUDE_CONFIG_DIR` when non-empty, else `<HOME>/.claude`, else `""`. `PathOf(home, cwd, id)` = `<home>/projects/<slug>/<id:D>.jsonl`, `slug` = `cwd` with every UTF-16 unit outside `[A-Za-z0-9]` replaced by `-`; `""` when `home` is empty (neither `HOME` nor `CLAUDE_CONFIG_DIR`: never a relative path — the adapter reads `""` as "no transcript" and starts the step fresh, the safe side; claude itself may fall back to the passwd home — assumed, not measured). **A slug longer than 200** is cut to its first 200 and `-` + `abs(h)` in base 36 is appended, `h` being the 31-multiplier hash (`h = (h<<5) - h + charCode | 0`) of the ORIGINAL path over its UTF-16 units — **read from the claude 2.1.310 binary (its function `FH`), not measured with a real session**; the tests' expected values are computed with node from the same JS (the one-liner is in the test's doc), rows at 200 and 201 characters. Measured on this host:
`/home/jinx/git/dew_flow_conventions` is the folder `-home-jinx-git-dew-flow-conventions`; recorded: the C1 arm's clone is `-home-jinx-git-coai-exp2-r-<token>-arm`. A character outside the basic plane is two dashes (claude is JavaScript) — read, not measured; so is `CLAUDE_CONFIG_DIR` relocating
the projects folder. `ISessionTranscripts.Exists(cwd, id)` and `IGateLock` (`TryAcquire`, `AcquireAsync`) are declared in `Ports/`; their adapters are E2.S4 and E2.S6.

**The recovery table — `StepRecovery.Decide(step, facts, spec, limits)`, first match wins.** `remaining` = `RemainingSeconds(spec.Minutes, timing, step, limits.MinimumRemainingSeconds)` for both launches.

| facts | entry |
|---|---|
| `Continue` is Ok, `After == LastAttempt` and `LastAttempt > 0` — **whatever `DoneMarker` says** | `Resume(session, LastAttempt + 1, remaining)` when `Session` reads and `TranscriptExists`; else `Fresh(LastAttempt + 1, remaining)`. The gate asked for this completed session to go on (E2.S7); a `.done` beside it is a gate that died between its two writes. A marker naming another attempt is STALE and decides nothing |
| `DoneMarker` | `Skip` |
| `LastAttempt > 0` and `LastEnd is Finished` | `RecoveredDone(Finished, LastAttempt)` — never resumed (the previous generation's arm C) |
| `LastAttempt > 0` and the `End` line of that attempt has `State == "hard-deadline"` | `RecoveredDone(HardDeadline, LastAttempt)` — the runner died between the end line and `.done`; the stream read back alone cannot see the wall |
| `Session` failed, or `!TranscriptExists` | `Fresh(LastAttempt + 1, remaining)` |
| otherwise | `Resume(session, LastAttempt + 1, remaining)` — an error result of any kind, a limit, an interruption |

`LastAttempt > 0` in the first row only shows beside a `.done` (with nothing to continue the other rows already launch fresh); `A_continue_marker_that_cannot_be_read_decides_nothing` asserts it there.

**The outputs table — `StepOutputs.Reconcile(spec, facts)`** over the step's BINDINGS, never a kind table (D21, D25). `Of(spec)`: `Verdict` when `spec.Verdict` is bound, `StepDiff` when `spec.DiffFrom` is set, and for a `taskCode` step `CoderHead`, `CoderDiff` (its bindings are refused by the reader).
`missing` = `Of(spec)` minus `Present`, in that order.

| missing | input it needs | when the input is absent |
|---|---|---|
| nothing | — | `Complete` |
| `Verdict` | the final result is **Finished by the classifier** (`StepClassifier.Classify(result, "", Ran)`) and its text is not blank | `Resumable("the review's final stream has no result text — no verdict to read; the step stays resumable")` |
| `StepDiff` | `BeforeExists` — the `.before` of the step `diffFrom` names | `Resumable("<diffFrom>.before is missing — the step's diff has no base; the step stays resumable")` |
| `CoderHead`, `CoderDiff` | `TaskBeforeExists` | `Resumable("the task's .before is missing — …")` |
| every input present | — | `Regenerate(missing)` — by the one function the first run used |

The FIRST missing output whose input is absent answers. A `HardDeadline` review with no result is resumable, not done with a hole (D21 over D8 for this one case, §8 item 3).

**What this does NOT prove.** That the runner reads these facts correctly from disk (E2.S4's restart matrix does) or writes the regenerated bytes equal to the first run's; that `metrics.mjs` reads a line this build wrote (E5's compatibility test, through the pinned consumer); that claude honours `--resume` after a limit
(the pilot); anything about the transcript path beyond the slug rule above.

**Deviations from the story's text**, each because the landed contract or a row that failed first said so:
1. `StepOutputs` is over `StepSpec` (`Of(spec)`, `NeedsBefore(spec)`, `Reconcile(spec, facts)`, `DerivedOutput.Verdict`), as C12's table says — the story's body still said `kind` and `ReviewJson`. Two scenario names follow the parent's §7 (`…no_verdict_file…`, `…regenerated_from_the_named_snapshot`) and the two continue-marker scenarios
   use the parent's names (`A_continue_marker_naming_the_last_attempt_resumes_that_finished_session_whatever_done_says`, `A_stale_continue_marker_decides_nothing`) where the story's table carried older ones; the story's `A_continue_marker_without_a_transcript_starts_the_step_fresh` keeps its name.
2. **E2 §8 item 51 (E2.S5 as built — the shared `Extract` predicate; the E2.S4 follow-up is item 52):** `Reconcile`'s check of the verdict's input uses the classifier's predicate, as `ReviewAnswer.Extract` does, so a captured 429/529 whose `is_error` says false, and an earlier success followed by a cut result line, are NOT regenerable. Scenario
   `A_result_the_classifier_does_not_call_finished_is_no_verdict_to_regenerate_from`; red first against the story's predicate (`not IsError`, non-blank), `Expected type to be Resumable … but found Regenerate.`
3. `A_complete_step_needs_nothing` is a theory over steps of the worked example (a binding-free `code` and `splitFix`, a verdict, a snapshot-and-diff, a task coder), because the story's "over every `StepKind`" cannot be expressed over a kind when the outputs follow from the bindings; the plant is on the binding-free step.
4. Beyond the story: `JsonRow` / `LineJson` (shared reader and writer, two files); `TimingLines.NotRecorded`; `DoneMarkers.Read` accepts the previous generation's `"attempts":"recovered"`; a sixth deadline row (the DST crossing); a lone-surrogate scenario; a scenario that a line which is not an object, has no known kind or lacks a required field is kept as `Other`; the `Regenerate` answer compares its list by value.
5. The story says the resume notice is "the shell's four lines" and cites `run-arm.sh:93-97` of the research copy (three lines); the part-6 shell (`coai-exp2/run-arm.sh:98-101`) has four, and is the text used.
6. E2 §8 item 50 (E2.S3's follow-up, owned here), CLOSED at the fix round: `FakeCliByCallTests.A_launch_with_a_session_id_leaves_the_transcript_claude_would_when_asked` asserts `PathOf(<home>/.claude, <the cwd handed to RunAsync>, g)` is the file the fake left (and the `CLAUDE_CONFIG_DIR` case through `PathOf`); `SeenCwd` stays in the other tests with a note that it hides the symlinked-cwd gap. `The_transcript_path_replaces_every_character_outside_letters_and_digits_with_a_dash` keeps the slug rule measured on this host.
7. Fix round (the Opus review of `4eb879c`): R3 the long-path rule above; R4 `PathOf` is `""` without a home; R5 the 16 part-5 fill-script start lines (`"how":"script"`, no `budget_s`, `session` or `deadline`) stay `Other` — a NAMED, tested exception (`The_part_5_fill_script_start_lines_are_kept_as_other_with_their_bytes`); **an analysis must not count a fill script's start through `TimingLines.Read`**; R6 each wall is clamped to [0, `int.MaxValue`] before `UsedSeconds` sums it (a negative wall counts as 0, a huge one never overflows). Reds: R3 "differ at index 220", R4 the relative path returned, R6 `System.OverflowException`; R5 is a characterisation and passes on the reader as written.

### Evidence (E2.S2)

Source runs: 2026-10-30 on this host (WSL, .NET SDK 10.0.114), `Release`, through `low-priority.mjs`; the MTP executable, never `dotnet test`. `timing-arm-C.jsonl` is a byte copy of `research/local-model-coding/data/arms/C/timing.jsonl` (`cmp` silent). The two 2026-10-28 lines are copied from the arm's
`timing.jsonl` with the arm's token replaced by `0123456789ab`.

**The reds against the stubs** (commit `6aa0d7c`; 71 test cases of the five classes, 47 failed, every one on an assertion; none threw, none was a compile error; `A_complete_step_needs_nothing` passes on the stub by design): `A_done_marker_skips` — *Expected type to be StepEntry+Skip … but found StepEntry+Fresh.*;
`A_last_stream_that_finished_without_a_marker_…` — *Expected entry to be StepEntry+RecoveredDone*; `A_last_stream_whose_result_is_an_error_resumes` (3 rows) — *Expected entry to be StepEntry+Resume*; `A_session_without_a_transcript_…` — *Expected Decide(…) to be StepEntry+Fresh*;
`The_remaining_budget_…` — *Expected StepBudget.UsedSeconds(timing, Step) to be 600 … but found 0*; `Timing_of_a_real_arm_reads_back_every_line` — *Expected lines to contain 8 item(s), but found 0*; the three continue-marker scenarios as above; the writer rows — *Expected TimingLines.Write(line) to be "{\"kind\":…" … but "" has a length of 0*;
the deadline rows — *Expected StepBudget.Deadline(…) to be "22:12 UTC (23:12 +01:00)" … but "" has a length of 0*; the resume notice and the transcript paths likewise; `ArmPathsTests` — *Expected actual to be "/e/r/abc/out/4-gate.continue" … but "/e/r/abc" has a length of 8*; the StepOutputs scenarios — *Expected type to be Resumable … but found Complete*, *Expected StepOutputs.Of(Step("review")) to be equal to {Verdict}, but found empty collection*.

**The break-its**, each planted in the finished code, built, observed red, restored (`git diff` empty): (a) the plant of `A_complete_step_needs_nothing` — `Of` answers `[StepDiff]` for the step `code`: *Expected type to be Complete … but found Resumable.* (and `A_steps_derived_outputs_…`: *Expected StepOutputs.Of(Step("code")) to be empty, but found … StepDiff*);
(b) a marker honoured whatever attempt it names: `A_stale_continue_marker_decides_nothing` (2 rows) *Expected … to be StepEntry+RecoveredDone*; (c) the `LastAttempt > 0` guard removed: `A_continue_marker_that_cannot_be_read_decides_nothing` *Expected type to be Skip because the same marker beside a .done is still no order, but found Resume*; (d) no marker row at all (the table as first
written): the two `…whatever_done_says` rows and `A_continue_marker_without_a_transcript_…` red; (e) limited attempts spending budget: `The_remaining_budget_…` *Expected … UsedSeconds … to be 600 … but found 1488 (difference of 888)*; (f) the end line of ANY attempt saying `hard-deadline`: `A_last_attempt_whose_end_line_says_hard_deadline_…` *Expected type to be Resume because an end line of an EARLIER attempt says nothing about the last one, but found RecoveredDone*; (g) the surrogate not replaced: the scenario red on the escape the serializer writes instead; (h) `GetString`'s guard removed: `System.InvalidOperationException : Cannot read incomplete UTF-16 JSON text as string with missing low surrogate.`; (i) the deadline's offset taken at the START instant: the DST row red, *differ at index 12*;
(j) item 51's predicate reverted to the story's: *Expected type to be Resumable … but found Regenerate.*

**The runs** (at `72e801d`, before the fix round): `StepRecoveryTests` 15, `StepOutputsTests` 14, `TimingLineTests` 25; namespace `Bench.Tests.ModelArms` 622 total, 619 passed, 3 skipped; whole suite 3686 total, 3100 passed, 572 failed, 14 skipped.
**After the fix round** (the merge of `feat/model-arms-e2`, then R1, R3–R6, citations): namespace `Bench.Tests.ModelArms` 689 total, 686 passed, 3 skipped, 0 failed; whole suite 3762 total, 3176 passed, 572 failed, 14 skipped — every one of the 572 is in the main-branch list of failures taken on this host (the Postgres and Docker tests cannot run here), none is new.
Build 0 errors, one `ASPIRE010` from the AppHost's Aspire targets, which this story does not touch; `dotnet format --verify-no-changes` clean; `plan-lifecycle.mjs` clean. Fix-round break-its (planted, seen red, restored): the slug's dash changed → `FakeCliByCallTests` red (*Expected SessionTranscripts.PathOf(…) to be … names the file the fake left, but they differ*); the hash taken over the slug instead of the original path →
the long-path rows red (*differ at index 221*); the wall clamp removed → `System.OverflowException`.

## The launcher's follow-ups (E2.S3)

Three changes to shared test and launcher code, made before the step runner (E2.S4) is built on them. None adds a script field beyond the two named, and every
existing script and every existing caller behaves as before.

**The fake CLI replays a file per call (`stdoutByCall`).** `tests/FakeCli`'s `Script.StdoutForCall(n)` reads `stdoutByCall.<n>`, `n` being the call number the fake's own
log gave the launch (per model, 1-based). It is chosen before every other replay (`stdoutByPrompt`, `stdoutByPromptContaining`, `stdoutFile`); a call with no entry falls
through to them exactly as before. One test scripts a step's attempts in order — a limited attempt, then its resume — without hashing prompts.

**The fake leaves the transcript claude would (`transcripts: true`).** With the flag set, a launch whose argv carries `--session-id <guid>` or `--resume <guid>` creates, before
it answers, the EMPTY file `<home>/projects/<slug>/<guid:D>.jsonl`, `home` being `CLAUDE_CONFIG_DIR` when set and non-empty, else `$HOME/.claude` — read from the environment,
never from the platform's idea of the user's home, so a test that gives the fake no home cannot reach the developer's own `~/.claude`; with neither variable set the fake prints
`fake-cli: transcripts: neither CLAUDE_CONFIG_DIR nor HOME is set; no transcript left` on stderr and writes nothing — and `slug` the working directory with every character outside
`[A-Za-z0-9]` replaced by `-`. An existing file is not truncated. **The fake accepts no more than claude does**: `--resume <guid>` for a session with no transcript exits 1 with claude's own
sentence on stderr, `No conversation found with session ID: <guid>`, so a runner that resumes a session it never started goes red against the fake as it would live; and the other half — measured on claude 2.1.310, 2026-10-30 — `--session-id <guid>` for an id whose transcript already exists exits 1 with `Error: Session ID <guid> is already in use.` and writes nothing,
so a runner that starts a session it already started, instead of resuming it, goes red the same way; and a launch the fake refuses for another reason (`refuseArgvContaining`) leaves no transcript. Why: through `run-arm` the runner's session probe is the real `ClaudeSessionTranscripts`,
and a fake that leaves no transcript makes every re-entry `Fresh` with a new id, so no `--resume` could ever be asserted (E2 plan §8 item 36). The slug rule is written a second time
on purpose — the fake references nothing of the module — and `SessionTranscripts.PathOf` (E2.S2) is the module's own spelling of the same path; until it lands the tests spell the path
a third time, in the test, and the comparison with `PathOf` is for the integration to add.

**`ProcessRunner` observes both readers (E1 §8 item 15, R1).** The shared core observes BOTH reader tasks on every path out of the wait — completed, timed out, the caller's
cancellation, a sink's fault — through one private helper, `BothAsync`, which is `Task.WhenAll` over (stdout, stderr). Consequences:

| Situation | Before | Now |
|---|---|---|
| a stdout sink throws while a stderr sink is inside a call | the launch threw at once; the stderr sink then ran on, after the launch had returned | the launch throws after the stderr reader has ended |
| both sinks throw | the stderr fault was never observed (raised by the finalizer later); its pump's catch could cancel an already disposed budget | the stdout reader's fault is thrown (argument order, with its own stack), the stderr's is observed and dropped |
| the caller cancels | the caller's `OperationCanceledException` at once, readers running on | an `OperationCanceledException` carrying the CALLER'S token — the same form on every cancelled path, the wait's own cancellation (or the sink fault it outranked) as its inner exception |

**The bound after the kill (`ProcessRunner.DrainAfterKill`, 10 s).** A reader ends when every holder of the pipe's write end has closed it; a descendant that escaped the tree kill (a new
session, a double fork — `(setsid sleep 40 &)`) holds it for as long as it lives, and an unbounded wait would hold a cancelled or timed-out launch that long (measured: 12.3 s for a
`sleep 12.3`; base code 1.1 s). The wait for the readers AFTER the kill — the wall's and the caller's cancellation, the two paths that kill — is therefore bounded by the one constant.
Past it the launch answers as the path does: the caller's cancellation, or `TimedOut`, or a reader's fault that is already there. A `TimedOut` past the bound carries what each reader had read SO FAR
(the pump keeps its text where the launch can read it, buffered or line by line), and says that it is partial: `ProcessAttempt.TimedOut.ReadersAbandoned` is true, and `Describe` adds that what the child
printed after the bound is missing — an empty text with the flag set means "nothing arrived yet", never "printed nothing" (absent is not zero). The abandoned readers are not cancelled; they end when the
descendant lets go, and a continuation observes their faults and then disposes the wait's budget (so an abandoned pump whose catch cancels the budget never meets a disposed source). A completed child's wait is not
bounded: its readers end with its pipes, as before. **This is a deviation from E2.S3's spec item 1** ("awaited to their end on every path"), decided at the fix round (E2 §8 item 49): the
spec's wait had no ceiling (`common.reliability`).

The wait, the wall, the tree kill and the two result shapes are unchanged; `PumpAsync`, `Reader`, `Utf8`, `Merge` and `Kill` are untouched.

What the tests pin: `FakeCliByCallTests` (the by-call replay and its fallback; the transcript under `HOME`, under `CLAUDE_CONFIG_DIR`, for `--resume` of an existing session, never truncated, none
without the flag, without a session argument, for a refused launch, or with neither `HOME` nor `CLAUDE_CONFIG_DIR` — and then the real home is not reached for; a resume of an unknown session, and a `--session-id` already in use, refused with
claude's sentences); `ProcessRunnerStderrSinkTests` (`A_launch_never_returns_while_a_sink_is_still_running`, `A_cancelled_launch_ends_both_readers_before_it_throws_the_callers_cancellation`,
`A_cancellation_the_caller_asked_for_outranks_the_sinks_fault`, `A_plainly_cancelled_launch_throws_the_callers_own_token`, and `When_both_sinks_throw_the_stdout_readers_fault_surfaces_and_the_other_is_observed`,
finalizer-driven: it waits for the killed child's pipes to close before collecting); `ProcessRunnerDrainTests` (a cancelled and a timed-out launch over a `setsid` descendant that outlives the bound return within
bound + margin — each takes about ten seconds, the price of the bound; the timed-out one keeps the lines read before the bound and is marked `ReadersAbandoned`, one whose pipes closed is not; and
`An_abandoned_readers_fault_is_observed_and_the_budget_outlives_it` watches the continuation from the outside — the descendant writes a line at 14 s, the abandoned stdout sink throws on it, and after the
finalizers no unobserved fault and no first-chance `ObjectDisposedException` out of the pump may have been seen; it takes about sixteen seconds).

What they do not prove: Windows (a job object is how an escaped descendant would be held there); that the fake's transcript path is the module's own
(the other fake-CLI tests read the cwd the fake saw, which hides a symbolic-linked temp folder; E2 §8 item 50 is closed by E2.S2: the first transcript test asserts `PathOf` over the cwd handed to the launcher); the encoding half of E1.S5's criterion 4 (UTF-8 only when a sink is given) — on Linux the default is already UTF-8, so only a Windows run could see it go wrong; that a real
claude puts its transcript where the fake does on a host nobody measured; the byte reader (`StreamAsync`) beyond what the shared core does for it.


## The step runner (E2.S4)

`StepRunner` (`Adapters/StepRunner.cs`, over `StepRun`, `StepAttempt`, `StepLog`) runs ONE step of ONE arm end to end: `RunAsync(StepContext, stopping)` returns `Done(End, Skipped, Recovered)` or `Stopped(ExitCode, Why)`; `Recover(StepContext)` is the same decision without a launch (the gate step asks it, E2.S7). The context is C8 in C12's shape: the arm, its paths, the scheme and the position of the step's entry, the experiment, the two RESOLVED executables (`ClaudeExecutable`, `GitExecutable`), the clone as the working directory, the parent's environment, the briefs, the leak guard, the facts a brief states, `DeriveOutputs`.
The files it touches go through `ArmFiles`; its git is `ArmGit`; the files it derives are `DerivedOutputs`'; whether claude left a transcript is `ClaudeSessionTranscripts`. Nothing else writes the arm's `out/` folder.

```mermaid
sequenceDiagram
    participant V as run-arm / gate
    participant R as StepRun
    participant F as ArmFiles (out/)
    participant G as ArmGit
    participant C as claude (the launcher)
    V->>R: RunAsync(context, stopping)
    loop until done or stopped
        R->>F: facts: .done, .session, last stream, timing, .continue
        R->>R: StepRecovery.Decide
        alt Skip / RecoveredDone
            R->>F: reconcile outputs, .done (recovered), delete .continue
            R-->>V: Done
        else Fresh / Resume
            R->>G: HEAD to .before (once, when the step snapshots)
            R->>F: .session (fresh), prompt (replaced while its stream does not exist)
            R->>G: HEAD short
            Note over R,F: Prepared: the prompt is on disk, no stream, no start line
            R->>F: create the stream and .err, then the start line
            R->>C: TranscriptAsync (stdout and stderr go to the files line by line)
            C-->>R: transcript, or the operator's stop
            R->>F: classify FROM THE FILES, end line (wall, exit, missing)
            alt limited
                R->>R: wait, loop (the same session is resumed)
            else done
                R->>F: reconcile outputs, .done, delete .continue
                R-->>V: Done
            else stopped
                R->>F: stopped line
                R-->>V: Stopped(exit)
            end
        end
    end
```

**The exits of a step** (D16; the shell's `exit 2` is 5 and its `exit 3` is 3 — a step never exits the verb itself):

| the step | `.done` | the end line's `end` | the last timing line | outcome |
|---|---|---|---|---|
| `Finished` / `HardDeadline`, outputs complete or regenerated | written, `end` = the state | the state | the end line | `Done` |
| done, but the review's VERDICT input is missing (no result — cut by its wall — or a result with no text) | not written | the state | `stopped`, 5, the reason | `Stopped(5)`; **the next entry LAUNCHES**: the session is resumed as the next attempt at the floor budget (`Fresh` when claude left no transcript) — `StepRun.Decide` asks recovery's table again over facts in which that attempt did not finish (§8 item 3) |
| done, but the `.before` a diff needs is missing, or git cannot make the diff | not written | the state | `stopped`, 5, the reason | `Stopped(5)`, PERMANENT: a relaunch cures neither, so every entry stops again until the operator acts |
| `Limited`, waits left | — | `limited` | the end line; the wait follows | loops, `waitSeconds` |
| `Limited`, `limits.tries` waits spent | — | `limited` | `stopped`, 3, `still limited after N waits` | `Stopped(3)` |
| `Errored` / `Interrupted` | — | the state | `stopped`, 5 | `Stopped(5)` |
| `NotStarted` (no executable, argv refused) | — | `not-started` | `stopped`, 3, the reason | `Stopped(3)` |
| the brief refused (unfilled field, leak, no template) — before the start line | — | — | `stopped`, 4 | `Stopped(4)` |
| the operator's stop, during an attempt or a wait | — | the classified state | `stopped`, 5, `stopped by the operator — run the same command again to resume` | `Stopped(5)` |
| HEAD cannot be read for `.before` (nothing launched) | — | — | `stopped`, 5 | `Stopped(5)` |

**The log.** Every line is `<utc yyyy-MM-ddTHH:mm:ssZ> [<arm>] <text>`, passed through `ArmMap.Scrub` before it is written: a token never reaches it (the attempt's line names the stream's path, so the test sees `<token>` in its place). It is the verb's output — `pids/<arm>.log` once detached (D13); there is no `run.log` (E1 §8 item 1). The lines are those of the story: `already done — skipped`, `had finished before its runner died — marked done, not resumed`, `continuing its finished session as the gate asked after attempt N (why)`, `<step>.continue could not be read (…) — ignored`, `attempt N (fresh|resume, session id, budget Ns) → stream`, `LIMITED (reason) — waiting Ns, then resuming the same session (wait k)`, `done without writing FIELD (path)`, `FIELD (path) was not written by step X — the brief names a file that does not exist`, `attempt N was prepared by an earlier run and never launched — its prompt is rewritten`, `done (word)`, and every stop.

**The step's git (D17 d, E2 §8 item 25).** `ArmGit` is built ONCE per run from `context.GitExecutable` — the REAL path `run-arm` resolved, never a name, so no file in the clone or on `PATH` is looked up in its place — with the arm's clone as its working directory and `GitEnvironment.Of(context.ParentEnvironment)`: the verb's environment minus every `GIT_*`. A parent's `GIT_DIR`, `GIT_WORK_TREE`, `GIT_CEILING_DIRECTORIES`, `GIT_CONFIG_*`, `GIT_EXTERNAL_DIFF` or `GIT_PAGER` would redirect the question or name a program; `HOME` and `PATH` pass as judged. The arm's claude keeps its `GIT_*` untouched (D19): the harness's git and the agent's git are two processes with two environments, on purpose.
The launch is the shared launcher's replacing overload (60 s). **The clone is written by the model under test**, so its own `.git/config` and `.gitattributes` are the model's: every question starts `-c core.fsmonitor=false`, and the diff is `-c diff.external= diff --no-ext-diff --no-textconv <from> HEAD` — observed (a `diff.external` and a `textconv` driver planted in a clone, each pointing at a marker shim: the marker stays empty and `.diff` is the plain patch; without the flags the shim runs and its output is the `.diff`). **Residual:** git still READS the clone's config. Checked and closed: `diff.external`, `diff.<driver>.command`, `diff.<driver>.textconv`, `core.fsmonitor`, and the program a partial clone's promisor remote names (`remote.origin.uploadpack`) that a lazy fetch of a blob the model deleted would run (`GIT_NO_LAZY_FETCH=1` is in the environment ArmGit hands git — the variable, not `--no-lazy-fetch`, which older gits refuse; planted on git 2.53: without it the program ran, with it the diff fails and the step stops, exit 5); a pager is not started for a piped stdout; a commit-to-commit diff runs no clean/smudge filter (read, not planted). Not checked: any other key that can name a program on a path these three questions do not take, or a key a later git adds. The shim scenario's argv lines carry the flags. Nothing in `ArmGit` names the module's bare-name git helper: `The_module_launches_nothing_by_a_bare_name` is red the moment it does (observed, below), and `The_steps_git_is_in_the_scanned_tree_…` proves the scan can see the file. `diff` takes its base from a file, so the base must be a hex commit id — a file's text is never an option for git.
`HEAD short` that git cannot give is `""` in the start and end line and one log line; `HEAD` for `.before` that git cannot give stops the step (exit 5) before any launch — a snapshot with no commit is no snapshot; a failed diff is the resumable stop.

**What the runner writes, and when.** `.session` before the launch (`Replace`, one file rewritten when a fresh id replaces a lost transcript's); the prompt through `WritePrompt`, which REPLACES a prompt whose stream does not exist and throws when the stream does; the stream and `.err` are created new and filled line by line as claude prints them; `.before`, `.done`, the verdict file and `.diff` are written once (staged beside, renamed; an existing file is the runtime's `IOException`, and the staged file is removed). Attempts are counted by NAME: the number of an attempt is reserved by its STREAM.
Classification reads the files back — the same bytes recovery will read (D6) — so a run that dies after the launch and a re-entry decide alike. A done attempt's model-written outputs that are absent are logged and recorded in the end line's `missing`, never required.

**The restart matrix** is the story's table; each row is a test: done and no marker → skip; marker after N with `.done` left → resume, `.done` removed first; died between the prompt and the stream → resume as attempt N+1 again, the prompt rewritten, one launch, one start line; stale marker → the ordinary rows; finished stream without `.done` → recovered, `.done` written, marker removed; a lost transcript → fresh with a new id.

**Deviations** (E2 §8 item 56): (1) the start line is written AFTER the stream exists, not before (item 55 — `metrics.mjs` counts two start lines for one stream twice, and cannot be made to read one: it keeps no per-attempt dedupe); `Prepared` is therefore the prompt on disk with no stream and no start line; (2) the restart test and its name changed with it; (3) HEAD for `.before` stops the step; (4) the arm's token in the bench is a valid 16-hex token, minted by `ArmMap.Extend`, because `ArmMap.Read` refuses `tok…`; (5) the bench's temp root carries `testsToDelete-modelarms-`; (6) a stream with no start line is possible only in the window between the two creations and recovery reads it as an interrupted attempt; (7) `StepAttempt` and `StepRun` and `StepLog` split the flow for the complexity limit.

**The break-its** (planted, built, seen red, restored; `git diff` empty): the session file written after the launch — `The_session_file_is_written_before_the_launch`; `ArmGit` given the parent's environment without `GitEnvironment.Of` — `An_ambient_GIT_DIR_…` (the clone's HEAD expected, the other repository's found) and the shim test; the prompt through `WriteOnce` — `System.IO.IOException : The file '…/4-gate.a2.prompt.md' already exists.` (`LinkOrCopyFile`, on .NET 10.0.114 / Linux) in the restart scenario and the prompt's rewrite test; `ArmGit`'s head through the bare-name helper — `The_module_launches_nothing_by_a_bare_name`: `Expected collection to be empty … but found … {"ArmGit.cs"}`; the start line before the stream — the restart scenario (`Expected … Count(start => start.Attempt == 2) to be 0 … but found 1`) and the pinned consumer (`{1, 2, 2} contains 1 item(s) too many`); a failed derivation not turned into a stop — `A_diff_git_cannot_make_stops_the_step_resumable_…`; a verdict failure turned into success — `A_review_whose_last_result_is_a_limit_…`.

**The runs** (2026-10-30, WSL, .NET SDK 10.0.114, `Release`, through `low-priority.mjs`): the four new classes and the architecture tests, 58 cases, 58 passed in 12.5 s (`StepRunnerTests` alone under 60 s); namespace `Bench.Tests.ModelArms` 732 total, 729 passed, 3 skipped; whole suite 3805 total, 3219 passed, 572 failed, 14 skipped — every one of the 572 is in the main-branch list of failures taken on this host (Postgres and Docker tests), none is new. Build 0 errors, the one `ASPIRE010`; `dotnet format --verify-no-changes` clean; `plan-lifecycle.mjs` clean. The reds against the stubs: 42 cases, 42 failed on an assertion, none on a compile error or an exception.

**The fix round** (the Opus review of `80c8cf6`; E2 §8 item 56): a review with no readable verdict (cut by its wall, or finished with no text) stops resumable and the NEXT entry launches — see the exit table; the diff and every git question are hardened against the clone's own config; the operator's stop is a `stopped` line wherever it lands. Reds first, each an assertion: the second entry of the cut review (`Expected type to be Done … not Stopped … no verdict to read`) and of the blank review; the planted `diff.external` and `textconv` (`Expected … marker … to be empty … but found "ext"` / `"conv"`); a pre-cancelled run and a cancel during the diff (`Expected object to be <null> … but found System.OperationCanceledException`). Break-its, planted in the finished code, seen red, restored: the override of recovery's decision removed (the two review scenarios); `--no-ext-diff` and `-c diff.external=` removed (the external plant, and the shim scenario's argv); `--no-textconv` removed (the textconv plant); the cancellation filter made false (both stop scenarios). Runs: namespace `Bench.Tests.ModelArms` 737 total, 734 passed, 3 skipped; whole suite 3810 total, 3224 passed, 572 failed, 14 skipped — all 572 in the main-branch list, none new; build 0 errors; `dotnet format` and `plan-lifecycle.mjs` clean.

**What this does NOT prove.** That the pinned consumers read a runner's restart cleanly in one window: a runner that dies after the stream is created and before the start line leaves a stream with NO start line — `metrics6.mjs` counts it as an attempt (`s.attempts++` before `if (!last) continue`) and `metrics.mjs`'s transcript window for an interrupted attempt before the gap runs to `Date.now()` (the later messages are counted twice); a reader dedupe is E6's. That a relaunched verdictless attempt is cheap: each such entry costs one PAID attempt (the log says `<step>: attempt N finished without a verdict — resuming it`), so `run-arm` and `run-all` (E2.S8, E4) must not repeat an exit 5 by themselves. That a real claude behaves as the recorded streams do (the fake replays them), that a limit lifts after `waitSeconds`, or that its transcript is where the fake leaves it on another host (E2.S3's follow-ups); two runners over one arm's folder (one process per arm); a power cut inside a staged write; the gate's lock and loop (E2.S7); the task steps (E4); that `metrics.mjs` reads every line this build writes (E5's compatibility test reads all four readers — `PinnedConsumer` here covers the attempts of one step).

## The review's answer (E2.S5)

One pure type, `ReviewAnswer` (`Steps/ReviewAnswer.cs`), gives the TEXT of a review step's verdict file: `Extract(Outcome<ClaudeResultEnvelope> lastResult) : Outcome<string>` (C10; `review_json`, `run-arm.sh:132-140`). The runner (E2.S4's `DerivedOutputs`) hands it `ClaudeResultEnvelope.LastOf` of the review's FINAL attempt and writes
the text to the output the step's `verdict` binding names — `notes/review.json` for the Opus review, `notes/hA.json` and `notes/hB.json` for CHH's two Haiku reviews (D25). This type knows none of those names and writes nothing: nothing in it assumes one `review.json`.

**When there is an answer.** Only from a result that `StepClassifier` calls finished. `Extract` asks the classifier the same question the runner asked about the attempt (`Classify(lastResult, "", AttemptFacts.Ran)`; stderr and the launcher's facts matter only when there is no result, and then the answer is a failure anyway), so a verdict file is
never written from a result the step did not finish on. What it gives for each `StepEnd` kind:

| the attempt ended as | what `Extract` gives | why |
|---|---|---|
| `Finished` | the answer below | the last result is a success the stream's last word agrees with |
| `Limited` — a captured 429 or 529 (whatever `is_error` says), an error with limit words | Failure | a limit is not a review that found nothing (the 2026-10-28 incident: the repair script's finding, `repair-limit-2026-10-28.sh:27-29`) |
| `Errored` | Failure | an error result is never a review |
| `Interrupted` — no result, or the stream's last line is a result line that could not be read (`FollowedByUnreadResult`) | Failure | no result; or an earlier success the cut last line may have overturned |
| `HardDeadline` — no result, the wall killed the tree | Failure | there is nothing to read |
| `NotStarted` — no stream | Failure | the launch was refused; `LastOf("")` is a failure |

The Failure's reason is one sentence, `the review's last result is an error or absent — no review.json is written` (the story's text; for a CHH step the file is `hA.json` or `hB.json` and the runner's own line says which). A Failure writes NO file. It goes to E2.S4's `DerivedOutputs`, which must turn it into the `Resumable` stop (exit 5, with the reason) and never skip it silently (E2 §8 item 52; E2.S2's `Reconcile` uses the same classifier predicate, so a step with such a result is named resumable before `Extract` is asked). Nothing in this type enforces that: it is E2.S4's test.
The verdict comes from the LAST result only: a stream of three results (the recorded 2026-10-28 review: a success, then two 429s) has the 429 for its last, so there is no verdict — the success before it is not read. And the other way round, an earlier result that holds a verdict does not leak into the last result's `unparsed`.

**The answer.** The shell's span — from the FIRST `{` to the LAST `}` of the result text (`/\{[\s\S]*\}/`, greedy; no regex here, `IndexOf` and `LastIndexOf`, so a megabyte costs two linear scans) — byte for byte, when that span parses as a JSON object
(`JsonDocument.Parse`: comments, trailing commas and single quotes refused; a name defined twice at ANY depth refused; the reader's depth limit applies). Otherwise `{"verdict":"unparsed","raw":<the whole text as a JSON string>}`: the shell wrote an invalid span as it was and a reader that then crashed on it is worse than an honest
`unparsed` (E2 §8 item 7). The raw text is written readable (`JavaScriptEncoder.UnsafeRelaxedJsonEscaping`: an apostrophe, an angle bracket and a letter outside ASCII stay as they are; a character outside the basic plane is a surrogate-pair escape) and is NEVER cut, however long. Nothing in `Extract` throws: the parse guard catches
`JsonException` (malformed, too deep, a duplicated name) and `ArgumentException` (a RAW lone surrogate cannot be transcoded); a lone surrogate can reach the text two ways, and each is U+FFFD in the answer. An ESCAPED one (`\ud83d`, a model cutting an emoji in two) is decoded by `ClaudeResultEnvelope`'s reader, not here (R2 below); a RAW one, which only a hand-built envelope can carry, is written by the JSON writer as U+FFFD (pinned by `A_lone_surrogate_in_the_result_never_throws`; a scrub of our own was written first, the break-it showed it changed nothing, and it was deleted). So the raw text is cut and changed in no other way: a result that holds a lone half reads, in `raw` and in a span, as the text with that half replaced.

**The attacks, and what the spec decides** (`The_span_rule_decides_every_attack_and_never_throws`, one row each; the result text in the first column):

| the result text | the answer | decided by |
|---|---|---|
| prose, the object, prose with no braces | the object | the span |
| `{"a":1} and {"b":2}`; two objects on two lines | `unparsed` | the greedy span is both objects and a text between them — not JSON (the story's fourth scenario). A reviewer that prints two objects gets `unparsed`, with both in `raw` |
| the object in a ```` ```json ```` fence | the object | the span starts at the first `{`, so the fence is outside it |
| the same fence, then prose with braces (`See {the notes}.`); braces in prose BEFORE the object (`I wrote {braces} here: {…}`) | `unparsed` | the span reaches the prose's brace and is not JSON. The fail-safe reading: nothing is searched for "the right object" |
| braces and an escaped quote inside strings (`{"note":"} and {", …}`) | the object | the span is the whole text and parses |
| an unbalanced `{`; an extra `}`; `} then {`; a lone `{` | `unparsed` | no span, or a span that does not parse |
| `[{"verdict":"ready"}]` | `{"verdict":"ready"}` | the shell's span, kept: the brackets are not part of it. A known surprise, pinned so a change is a decision |
| `[{"id":"R1"},{"id":"R2"}]` | `unparsed` | the span `{"id":"R1"},{"id":"R2"}` is not one object |
| `{}`; an object whose string holds a lone surrogate ESCAPE (`"\ud800"`) | the span | a JSON object; the escape is well-formed JSON (no verdict is required — the schema is the fixer's) |
| a comment, a trailing comma, single quotes | `unparsed` | refused by the reader (the story: "comments refused") |
| a name defined twice, at the top or nested | `unparsed` | **deviation 2** |
| an empty result; white space only | `unparsed` with `raw` `""` / the white space | no span. E2.S2's `StepOutputs` already calls a result with no text resumable before `Extract` is asked, so an empty `raw` is reached only by a caller that asks anyway |
| about a megabyte: prose then the object; one object; no braces at all; 500 000 `{` then 500 000 `}`; 500 000 `{}` pairs | the object / the object / `unparsed` / `unparsed` / `unparsed` | the same rules (`A_result_of_a_megabyte_…`); the nested one is `unparsed` by the reader's depth limit, with ALL of its text in `raw` |

**Deviations from the story's text** (each decided where the story was silent or looser; the fail-safe reading, as the instruction for this story says):
1. **A result is read for a verdict only when the classifier calls it finished.** The story's rule is "no result, or an `IsError` result → Failure". The classifier's rules were widened after the story was written (E2.S1's fix round, E2 §8 item 48): a captured 429 or 529 is a limit even when `is_error` is false or absent, and an earlier success is not trusted when the stream's last line was a
   result line that could not be read. Read by the story's rule alone, both would be written as a review (`unparsed`, or worse a verdict from the earlier result) — the very defect the story exists to prevent. Asking the classifier keeps one decision in one place (reuse-first). Four rows of `A_review_is_read_only_from_a_result_the_classifier_calls_finished` and `An_envelope_followed_by_an_unread_result_…` are red without it.
2. **A name defined twice anywhere in the span makes the whole answer `unparsed`.** The story says "parses as a JSON object" and is silent on duplicates, which `JsonDocument` accepts by default (and `JSON.parse` resolves by keeping the LAST — so `{"verdict":"ready","verdict":"fix-then-ready"}` would read as one verdict here and another there). A verdict with no single reading is not read at all; the `raw` keeps
   both. The cost is that a duplicate deep in a finding loses the whole review's structure to `unparsed`, but never its text. Rows: `a name defined twice …` (two).
3. **The raw text is written readable, not in the serializer's default escaping.** The story's example shows `I'm waiting …` with a plain apostrophe; the default encoder writes `I'm`. Same JSON, readable by a person and by a fixer's prompt.
4. **Five scenarios beyond the story's four** (the story is thin on attacks; they are written in its style — Given / When / Then, red against the stub — before the code): only the last result decides; a review is read only from a finished result (every `StepEnd` kind, eight rows and a test for the two kinds with no result); the attacks on the span (23 rows); a megabyte; a lone surrogate (and a hand-built envelope with `FollowedByUnreadResult`).

5. **R2 of the fix round — a change to E2.S1's reader (E2 §8 item 53).** The Opus review's probe — the result `{"verdict":"ready","findings":[]} trailing \ud83d` through `LastOf` — came back `unparsed` with a double-escaped `raw`, because `ClaudeResultEnvelope.StringOf` read a string holding an escaped lone surrogate as its escaped raw text. It now decodes the string as a JSON parser does (`Launch/JsonStringText.cs`: every escape; a well-formed pair kept; a lone half
   U+FFFD), so the verdict is returned. Red first: 11 failures by assertion (the new `A_string_with_an_escaped_lone_surrogate_…` theory, the new `An_escaped_lone_surrogate_in_the_result_text_does_not_turn_a_valid_verdict_into_unparsed`, and E2.S1's own `An_odd_field_…` row for `"\ud800"`, which pinned the raw fallback `\\ud800` and now expects `\uFFFD`). Break-its: a lone half kept as it is — 11 red; a pair never joined — 2 red;
   `StringOf` back to the raw fallback — 11 red.

**What this does NOT prove.** What a real review model prints — the span rule is the shell's, and a reviewer that writes two objects or prose with braces after its verdict gets `unparsed` (measured on the recorded texts only: the `:310` prose and the synthetic success). That the file is written (E2.S4's `DerivedOutputs`) or that its path is the bound output's. That anything downstream reads an `unparsed` verdict: the part-6/7 pinned table scripts (`make-tables6.mjs:39-40`, `make-tables7.mjs:95,109` on the research branch) read `findings` and THROW on an `unparsed` verdict or on an object with no `findings`, as they did on the shell's own output — an arm whose review is `unparsed` needs a person before its table is built. Anything about the verdict's schema (`findings`, `severity`): the fixer and the analysis read that.
**The runs** (2026-10-30 on this host, WSL, .NET SDK 10.0.114, `Release`, through `low-priority.mjs`). Round 1, red against the stub `Success("")`: `ReviewAnswerTests` 41 of 41 failed, every one by assertion (none by exception or compile error — the first draft of the helper threw `JsonReaderException` on the stub's empty text and was changed to assert the answer's first characters); green 41 of 41. Fix round (R2), red before the code: 11 of 82 failed by assertion
across `ReviewAnswerTests` and `ClaudeResultEnvelopeTests`; green after. Final: `ReviewAnswerTests` 42 of 42, `ClaudeResultEnvelopeTests` 40 of 40, `StepEndTests` 58 of 58, `ModelArmsArchitectureTests` 15 of 15; the namespace `Bench.Tests.ModelArms` 619 total, 616 passed, 3 skipped, 0 failed; the whole suite 3683 total, 3097 passed, 572 failed, 14 skipped — the 572 are exactly the main-branch list of failures taken on this host
(the Postgres and Docker tests cannot run here), none new; one test of that list (`ProcessRunnerCancellationTests.External_cancellation_kills_the_child_it_was_waiting_on`) passed. `dotnet format --verify-no-changes` exits 0; the build has 0 errors and the one `ASPIRE010` warning from the AppHost's Aspire targets, which this change does not touch; `plan-lifecycle.mjs` is clean.
**The break-its** (each: change one production line, build, watch the named tests go red, restore with `git checkout`, `git diff` empty): the duplicate-name refusal removed — 2 rows red; the classifier gate removed (every `Ok` is read) — 8 red (five rows of the end table, the hand-built flag, the error result, the three-result stream); the JSON check of the span removed — 14 red (the story's fourth scenario, eleven attack rows, the megabyte, the lone surrogate); the span ending at the FIRST `}` — 9 red; the default encoder — 2 red (the apostrophe, the readable text); `ArgumentException` left uncaught — 1 red (the lone surrogate). A seventh, the scrub of lone surrogates of our own, stayed GREEN when removed — it was inert and is deleted.

That a megabyte is fast on a slow machine: the test asserts the answer, not a time (there is no regex and no recursion of ours in the path; the reader's own depth limit stops the nested case). That the JSON writer's U+FFFD for a lone surrogate holds in a runtime other than the .NET 10.0.114 this was run on.

## The gate lock and the MCP configs (E2.S6)

Four pieces, read by the gate step (E2.S7) and by `make` (E4.S4): the lock one gate step at a time takes (`FileGateLock`, `gate-lock`), what `gate.json` said (`GateStatus`), and the two MCP configuration files (`McpConfig`). The gate lock is
`<root>/.gate.lock`; **D11's lock (`.isolation.lock`, held by `lock` and `unlock`) is a different lock** with different holders and is not touched here.

### The lock (`Adapters/FileGateLock.cs`, D10)

**The atomic operation it rests on** (common.reliability): the kernel's own open-with-no-sharing. `TryAcquire(path)` opens the file `OpenOrCreate`, `ReadWrite`, `FileShare.None`, through .NET — **never through `flock(1)`**: nothing the module starts is looked up
by name (the bare-name scan of `ModelArmsArchitectureTests` sees no launch here), and a lock against a non-.NET holder is out of bounds (parent §9). On Linux the runtime takes `flock(LOCK_EX | LOCK_NB)` on the descriptor; a conflict surfaces as an `IOException`
**of exactly that type** whose `HResult` is the raw errno (`EWOULDBLOCK`, 11 on Linux) — observed on .NET 10.0.14; a missing folder, a refused permission and every other IO fault are NOT conflicts and fly. On Windows the share mode is enforced by the file system and the exception carries
`ERROR_SHARING_VIOLATION` or `ERROR_LOCK_VIOLATION` (`0x80070020`, `0x80070021`; from the Win32 documentation, **not observed — nothing here ran on Windows**). Since E3.S0 the conflict code off Windows is MEASURED at construction (`LockProbe`, *The platform roads*), not tabled; the two Windows literals stay a table, and no platform is refused for want of a number. A conflict is `null`; the stream is the held lock and `Dispose` releases it. The file is created once and never deleted: one empty
file per root.

**What the path may be (D17, review R1).** `.gate.lock` is a regular file of the root's own, or absent. `FileGateLock.Take(path, create, note)` is the one road in (`TryAcquire`, `AcquireAsync` and the verb all go through it): it reads the path WITHOUT following a link, with the module's `statx` reader (`RootFacts.ReadFacts`, `FileKind.Regular`, the reader that judges `experiment.json`), and refuses a link — live or dangling — a folder, a FIFO, a device or any other kind with a sentence naming the path, what it is and D17 (`… is a FIFO, not a regular file — refused (D17)`). A missing file is made with `FileMode.CreateNew` (`O_CREAT | O_EXCL`: never follows a link, fails on one planted in the window; the created stream IS the held lock, so there is no reopen), an existing one is opened with `FileMode.Open` (never created through a link), and **after the open the inode the handle really holds (`statx` on the descriptor, `AT_EMPTY_PATH`) is compared, device and inode, with what the path now names**; a mismatch is refused. The answer is a value (`GateLockAnswer`: `Held`, `Busy`, `Absent`, `Refused`, `Unread`); the port-shaped members throw `GateLockRefusedException` for the last two, because the port has no other way to say them. **`status` creates nothing** (a root with no lock file is `free`). *The residual swap window:* between the check and the open, and after the comparison, a process that can write the root can still swap the file; the comparison closes the swap visible at the instant it is made. Only the operator can write the root (D17), and a link in a PARENT folder of the root is D17's root check's. Seen red before the fix: a dangling link made `status` CREATE a file outside the root, a link to an existing file made `hold` lock the outside file, a link to a folder or a folder ended as `unexpected UnauthorizedAccessException` (exit 3), and a FIFO and a link to `/dev/null` were accepted as the lock.

`AcquireAsync(path, ct)` loops `ct.ThrowIfCancellationRequested()`, `TryAcquire`, `Task.Delay(poll, clock, ct)`. The token is looked at before every try, so a waiter told to stop **never takes the lock** (`A_cancelled_token_acquires_nothing_even_when_the_lock_is_free`); a poll of zero is
refused (a retry loop has a pause). **Every wait has a ceiling — the caller's**: the lock cannot know how long a gate step may queue, so the token carries it (the verb gives one, 15 minutes; the gate step gives its own).

**When the holder dies — and why there is no heartbeat and no fencing.** The lock belongs to the open file description, not to a record anybody wrote. On **Linux** the kernel closes a dead process's descriptors, so a holder that exits, crashes or is killed frees the lock at the same
instant: observed with `SIGKILL` on this host (the holder killed, the next try succeeded; `A_holder_that_is_killed_frees_the_lock_and_a_waiter_gets_it` runs it through the console and the shared launcher's tree kill). There is nothing stale to expire and nothing to fence. On **Windows** the system
closes the handle when the process ends, which frees the share mode the same way — documented, **not observed**. **When the holder does NOT die (review R2): the wedged holder.** A holder that is stopped or hung (measured with `SIGSTOP`; `A_holder_that_is_stopped_keeps_the_lock_and_status_names_it_until_it_is_killed`) keeps the lock until something kills it; the kernel has no reason to free it, and every waiter is bounded only by its own ceiling or token — the lock has none to give. The caller that waits must carry a ceiling and say so to the operator: that is an **obligation of E2.S7** (E2 §8 item 57). To make a wedge findable, a holder writes the sidecar **`.gate.lock.holder`** (never into the locked file) after it holds the lock — JSON `{"pid", "start_ticks", "since_utc"}`, written to `.gate.lock.holder.tmp` and renamed over, so a reader never sees half a note — and removes it BEFORE it releases (or the next holder's note could be the one deleted). `gate-lock status` prints `held by pid N since <utc>` only when the note names a live process whose **kernel start time** is the one the note recorded, else `held (holder unknown)`. **The start time is `starttime`, field 22 of `/proc/<pid>/stat`, in clock ticks since boot (review N1):** an integer every reader sees the same. The first version compared two `Process.StartTime` readings at whole-second precision, and one pid's start time drifts by a millisecond or two between readings — a live holder that started near a second boundary read as `holder unknown` (2 of 9 runs of the gate-lock classes; 1 of 30 status reads). The stat line's command name (field 2) can hold spaces and `)`, so the fields are counted after the LAST `)`. Off Linux the kernel's tick is not there; since E3.S0 the note also carries `start_unix` (`Process.StartTime`, whole UTC seconds) and a holder is believed on the other POSIX hosts by that second within 2 s (*The platform roads*); Windows names no holder, as before. A killed holder leaves a stale note: it never makes a free lock look held (`free` is the kernel's answer), and the next holder overwrites it. The note is informational — a write that fails does not fail the lock. **The note is read as a stranger's file (review N2):** opened non-blocking and close-on-exec through `KernelFacts.ReadSmall`, its kind judged from the DESCRIPTOR's facts (a regular file; a FIFO, a device or a folder is refused before a byte is read), opened with `O_NOFOLLOW` (a link fails at the open with `ELOOP`: nothing outside the root is opened — measured by the review: without the flag a note that was a link to an outside FIFO with a blocked writer released that writer), the path required to still name the same inode (device and inode, as the lock's own check, for a swap after the open), and never read past 4 KiB — a bigger file, a FIFO, a link or text that is not a note is `holder unknown` at once, never an exception and never a wait. Before this, `File.ReadAllText` followed a FIFO swapped in after a type check and hung `status` for 1300 s (ignoring SIGTERM), and a 200 MB note cost 1 GB of memory (1.2–3 GB gave an `OutOfMemoryException`, exit 3); E2.S7's gate step calls `WhoHolds`, so it would have inherited both. The open flags (`O_NOCTTY`, `O_NONBLOCK`, `O_NOFOLLOW`, `O_CLOEXEC`) are the values probed on this host (x86-64 Linux); aarch64's `O_NOFOLLOW` differs (`0100000`), so the kernel road is Linux x86-64's alone (E3.S0: elsewhere the note is read through `BoundedFile`'s portable road). **The kernel lock is the truth, never the sidecar.** Growth: two small files per root, overwritten.

**Which file systems honour the lock — measured, not assumed (review R4).** On .NET 10.0.14 inside the WSL VM the lock was honoured on ext4, on tmpfs and on the 9p mount of the Windows drive (`/mnt/c`): an in-process double open on each (this host, again in the fix round), and — measured by the Opus review — a `flock(1)` pair and two `hold` processes through a link to `/mnt/c` all serialised. **A Windows process does not see a lock taken inside the VM** on the same file. **(Superseded by E3.S0: the honour check, below, observes it at every acquisition.) NFS and CIFS were not probed.** A file system that does not support `flock` makes the runtime ignore the lock silently (its source: "the only error we care about is EWOULDBLOCK"), so two holders could both be told they hold it; nothing here detects that. Other residuals: the lock is advisory on Linux (only a process that asks respects it).

**The switch that turns the lock off** (the story's instruction: confirm the name and quote the source). Construction **refuses** a runtime with file locking switched off — *"file locking is disabled in this runtime — the gate lock would be inert"* (exit 3 in the verb). Checked: the names
`System.IO.DisableFileLocking` (the `AppContext` switch, also a `runtimeconfig.json` property) and `DOTNET_SYSTEM_IO_DISABLEFILELOCKING` (the variable) are in `System.Private.CoreLib.dll` 10.0.14 on this host (`strings`), and in the runtime's source, `SafeFileHandle.Unix.cs` of dotnet/runtime `main`, read
2026-10-30 through a page fetch: `AppContextConfigHelper.GetBooleanConfig("System.IO.DisableFileLocking", "DOTNET_SYSTEM_IO_DISABLEFILELOCKING", …)`, `if (DisableFileLocking) return false;` in `CanLockTheFile`, and the comment *"Lock the file if requested via FileShare. This is only advisory locking. FileShare.None
implies an exclusive lock on the file and all other modes use a shared lock."* **The Microsoft documentation page for `FileShare` does not mention the switch** (checked: that one page; other documentation pages were not searched). So the rule the refusal uses is the one MEASURED on 10.0.14 (Linux), by a probe that opens the same file twice in one process:
the variable decides when it reads as a boolean (`1`, `true`, `TRUE` off; `0`, `false` on), any other value (`yes`, empty) leaves the decision to the switch, and neither set means locking is on — **a variable set to `0` beats a switch set to true, and a variable `1` beats a switch set to false**.
`File_locking_switched_off_in_the_runtime_is_refused_as_the_runtime_reads_it` pins those thirteen rows. Not observed: Windows (the switch is read in the Unix file code; the refusal there is stricter than needed, as the story asked).

**An inherited handle (D10's warning; review R7).** The runtime opens the descriptor close-on-exec; `FileShare.Inheritable` is never passed — it makes the runtime take `LOCK_SH` instead (a break-it: the lock then conflicts with nobody, five tests red). Two tests watch it. `A_child_launched_while_the_lock_is_held_does_not_keep_it` shows release by `Dispose` with a child alive (the fake CLI in `hang` mode) — but **it cannot see the flag**: measured, with the flag cleared by `fcntl` and a child alive, the lock was free after `Dispose`, because the runtime unlocks explicitly on release. **`A_holder_killed_while_its_child_lives_frees_the_lock_and_the_child_holds_no_descriptor_of_it` does:** a test-only helper project (`tests/GateHolder`, `gate-holder`, launched from its own output folder like the fake CLI — no hidden mode in production code) takes the lock through `FileGateLock`, starts `/usr/bin/sleep 40` by its absolute path and waits; the test kills the HOLDER by pid (the launcher's tree kill would take the child too) and reads the child's `/proc/<pid>/fd` and the status line. With close-on-exec cleared by `fcntl` the child keeps a descriptor of `.gate.lock` and the test is red (planted and seen).

### The verb (`Verbs/GateLock.cs`)

`model-arms gate-lock status` looks and creates nothing: `model-arms gate-lock: free` (also for a root with no lock file) or `model-arms gate-lock: held by pid N since <utc>` / `model-arms gate-lock: held (holder unknown)`, exit 0; a lock path that is not a regular file is refused with the D17 sentence, exit 4; a host read that failed is exit 3. `gate-lock hold --seconds N` waits for the lock (at most 15 minutes; then, in its own words, `the lock was not free within … s — `gate-lock status` says who has it`, exit 3 (true since the sidecar) — a distinct message, never a cancellation, and the
operator's stop during the wait stays a stop, 5), prints `model-arms gate-lock: held <path>` and flushes, keeps the lock N seconds, **releases it, then** prints `model-arms gate-lock: released`, exit 0; a stop while holding releases through the `using` and travels up as every verb's stop (5). `N` is
1 to 86400: absent or outside → exit 4, `gate-lock hold takes --seconds N, from 1 to 86400` (the upper bound is added: `Task.Delay` itself refuses about 49 days, and a longer hold is no probe). The verb arrives through `ExperimentGate`, so the root is trusted before anything is taken. The dispatch arm is
`"gate-lock" => ExperimentGate.RunAsync(input, GateLockVerb.RunAsync)`.

### `GateStatus` (`Launch/GateStatus.cs`, C9)

**Changed by E2.S7's round four (E2 §8 item 60): the rule below, "not JSON reads as `missing`", is replaced.** `Read` has a fourth answer, `Unjudgeable(reason)`, for text that is PRESENT and is no status — empty or blank, not a JSON object, a `status` that is not text, a word outside `GateStatus.Words` (the gate brief's six: `in_progress`, `proceed`, `good_enough`, `rounds_spent`, `call_human`, `blocked`, matched exactly) — and `missing` is kept for no file and for an object that says no status (`{}`, `"status": null`). The gate step stops on it, resumable (the gate step's section). A trailing comma is still tolerated (the port is lenient), so a blocked object with one is judged blocked. Rows of `GateStatusTests` that changed: the theory that pinned `missing` for `not json`, `[]`, `{"status":3}`, `""` and a truncated object now pins Unjudgeable (and is joined by blank text, `"status":""`, `Blocked`, `done`); `{}` and `{"status":null}` stay `missing` in their own theory; the lone-surrogate STATUS row (`{"status":"\ud83d"}`) moved from `missing` to Unjudgeable; new rows: every brief word read as itself, the trailing comma.

`Read(Outcome<string>)` over the text of `gate.json` (`run-arm.sh:153`, `:156`): no file, text that is not a JSON object or has no string `status` → `("missing", "")`; else `(status, why)` with `why` `""` when it is not a string. It reuses `JsonRow` (E2.S2), so a model's lone surrogate (an emoji cut in two) in either
member is not text and nothing throws. `WaitAgain(reading, waitsSoFar, gateTries)` = blocked and fewer waits spent than allowed (`:154`). **`WaitsSoFar(timing, step)`** counts the `GateWait` lines of that step in the parsed `timing.jsonl` — the durable count a restart reads back (E2 §8 item 44); a start
line, a line of another step and a line the reader kept as `Other` are not waits.

### `McpConfig` (`Launch/McpConfig.cs`, E1 §8 item 16 decided)

`None()` is `{"mcpServers":{}}`; `Coai(coai, paths, harnessEnvironment)` is the shell's object (`after6.sh:34`) indented one space as `JSON.stringify(…, null, 1)` does (`type: stdio`, `command`, `args: []`, `env: {COAI_DATA_DIR: <paths.CoaiData>, COAI_DATA_SIDE: <side>}`), printable text — ASCII, accented letters, `< & >` — kept as it is. **The relaxed encoder is not `JSON.stringify` for everything (review R8):** it escapes characters outside the BMP (an emoji becomes a surrogate-pair escape), DEL, C1 controls, U+2028/U+2029, U+FEFF and unassigned code points (all observed on 10.0.14), which `JSON.stringify` writes raw, and writes a lone surrogate as U+FFFD. So the PARSED value equals the input for every well-formed string and the bytes equal the shell's only for printable BMP text; a **lone surrogate in the command, the side, the data folder or the key is refused** (`ArgumentException` naming the field), because the replacement would be a different command or key. **Neither text ends in a newline**
(the shell's `echo` and `console.log` added one; the writer, `make`, adds it). **When the harness's own environment holds a non-empty `COAI_CREDS_KEY`, that one variable is added to `env`** — and nothing else of the harness's environment is. A configuration with no binary is a caller's bug (`ArgumentException`), as is a lone surrogate.

*Why this and nothing wider.* The coai server reads its one vault key from its layered configuration, the copied settings file first and its own environment over it (`KeyVault.cs:35`, `:76-95`); under the shell claude inherited the harness's whole environment and handed it on, so an exported key reached the server;
under D19 the arm's claude never sees `COAI_*` or a secret-named variable, so the key must travel in the one place the server's environment is written — this file's `env` — and never by widening the arm's environment. Providers that sign in through their own CLI homes (codex, agy, vibe in part 6) need nothing. Rejected:
forwarding every `*_KEY` (a provider list nobody wrote down), and `${VAR}` expansion (claude expands from its own environment, which D19 stripped).

*The consequence, stated.* An exported key is written into `r/<token>/mcp-coai.json`: `make` (E4.S4) writes it with mode `0600` — the same exposure as the copied settings file the panel already writes the key into (`after6.sh:32`) — and the experiment's published record must never carry it (E5.S5 excludes
`mcp-coai.json` and `coai-data/`). The key's value is never logged or printed: it exists only in the returned text.

**What this guarantees, exactly (E2 §8 item 34): the config REQUESTS the delivery.** `McpConfigTests` prove that the text carries the key as a member of the coai server's `env` (and cannot be made to add or replace another member by a key with quotes in it), and that `ArmChildEnvironment.Of` never lets `COAI_CREDS_KEY` into the arm's own environment.
Whether claude hands a stdio server the `env` member of its config is claude's behaviour; no test here starts claude. **The proof is milestone M2's key check (E3.S5)**: the real `claude` over a generated `mcp-coai.json`, `providers` asked, the key absent from the copied settings file so the `env` member is the only road, and
the fallback named if the key does not arrive (the copied settings file — the shell's own road, `after6.sh:32`).

### Tests

| Class | Tests | What each holds |
|---|---|---|
| `FileGateLockTests` (32 incl. rows) | `A_second_process_holding_the_lock_makes_the_first_wait` | a child `gate-lock hold --seconds 4` holds; `TryAcquire` is `null`; `AcquireAsync` returns more than 3 s after the child printed `held`; the child printed `released` and exited 0; the returned handle makes another child's `status` say `held` |
| | `Cancellation_while_waiting_releases_nothing_and_surfaces_as_cancelled`, `A_cancelled_token_acquires_nothing_even_when_the_lock_is_free` | the cancelled waiter throws; the holder is untouched; a cancelled token takes nothing from a free lock |
| | `A_child_launched_while_the_lock_is_held_does_not_keep_it`, `A_holder_that_is_killed_frees_the_lock_and_a_waiter_gets_it` | release with a live child; the killed holder |
| | `Status_says_free_and_held_in_process`, `Gate_lock_hold_needs_its_seconds` (4 rows), `A_stop_while_holding_…`, `A_stop_while_waiting_…`, `A_hold_that_cannot_get_the_lock_within_its_ceiling_…` | the verb's lines, exits and the stop-versus-ceiling distinction |
| | `File_locking_switched_off_in_the_runtime_is_refused_as_the_runtime_reads_it` (17 rows, among them `" 1"`, `"1 "`, `" 0"`, `"0 "`: the runtime does not trim), `A_runtime_with_locking_off_…`, `A_poll_of_zero_…` | the refusal rule and its construction |
| `GateLockPathTests` (10) | `A_lock_path_that_is_not_a_regular_file_is_refused_…` (6 rows: a link to a file outside, a dangling link, a link to a folder, a folder, a FIFO, a link to `/dev/null`) | `status` and `hold` both exit 4 with a sentence naming the path, the kind and D17; nothing outside is created or changed |
| | `Status_on_a_root_with_no_lock_file_…`, `A_link_planted_between_the_check_and_the_open_…`, `A_different_file_swapped_in_after_the_open_…`, `A_link_planted_where_the_file_was_absent_…` | status creates nothing; the comparison after the open catches a link and another inode; the exclusive create does not follow a planted link |
| `GateLockHolderTests` (21 incl. rows) | the start time is field 22 read after the last `)` (9 rows); a note that names the live pid and the kernel's ticks is believed (and 300 reads in a row all name it); a note far bigger than a note is `unknown`; a 200 MB note allocates under 256 KiB in the reader; a FIFO, a link to a FIFO and a folder as the note return at once; a link to an outside FIFO with a blocked writer is not opened and the writer stays blocked; the sidecar named and removed; a stale or untrue sidecar prints `holder unknown`; a sidecar that is a link or not JSON is not believed; a SIGSTOPped holder keeps the lock and is named; a killed holder whose child lives frees the lock and the child holds no descriptor of it | |
| `GateStatusTests` (10) | the story's two plus the no-file, the not-text reason and the lone-surrogate rows | |
| `McpConfigTests` (9) | the story's three plus a key with quotes, a path with `"`, `<&>` and accents, an emoji, a lone surrogate in the command and in the key, and the gate-less refusal | |
| `ArmPathsTests` | `CoaiData` → `/e/r/abc/coai-data` | |
| `ModelArmsArchitectureTests` | `GateStatus` and `McpConfig` named in the pure list | |

The two-process tests start the console's apphost (`ExperimentRoot.ConsoleBinary`, through `ModelArmsBinary`) and the test-only `gate-holder` — real processes, never a double of the subject — and kill and wait for every child they start (`await using`, and a kill of the sleeping child by pid). Temp folders are `testsToDelete-modelarms-…` (`TempFolder`).

**Break-its**, each planted in the finished code, observed red, restored byte for byte (the plant script copies the file and restores the copy, never `git checkout`, so uncommitted work survives) with `git diff` empty. Round one: `GateStatus.Read` with an unguarded `GetString()` — `{"status":3}` red as `InvalidOperationException … requires an element of type 'String'`, `[]` likewise; the key never yielded — the named McpConfig test red (`Expected env.ToJsonString() to be …`); `Indented = false` — the shape test red; `ArmChildEnvironment`'s strip of `COAI_*` and secret names removed — the named test red (`Expected ArmChildEnvironment.Of(harness, Paths).Keys {"COAI_CREDS_KEY", "HOME", "TMPDIR"} to not have any items matching …`); `FileShare.ReadWrite` — five red (`Expected gateLock.TryAcquire(…) to be <null> … but found System.IO.FileStream`); `FileShare.None | Inheritable` — the same five; `ThrowIfCancellationRequested` removed — the cancelled-token test red; the `when (!Stopping.IsCancellationRequested)` of the ceiling's catch removed — `A_stop_while_waiting_…` red. Fix round: the path check skipped — 4 red (the folder, the link to a folder, the dangling link, status creating the file); the comparison after the open removed — the two swap tests red (`Expected type to be GateLockAnswer+Refused … but found GateLockAnswer+Held`); `CreateNew` replaced by `OpenOrCreate` — `A_link_planted_where_the_file_was_absent_…` red (`Expected File.Exists(outside) to be False … but found True`: a file was created through the link); status with `create: true` — red; close-on-exec cleared by `fcntl` after the open — `A_holder_killed_while_its_child_lives_…` red (the child's `/proc/<pid>/fd` lists the lock file); the sidecar not removed on release — red; the holder note believed without the liveness check — both unknown-holder rows red; the sidecar not written — the holder tests red at their first assertion; the lone-surrogate check removed — both rows red; the variable read with `Trim()` — the four whitespace rows red. Round three: the oversize refusal removed — the 10 MB-of-blanks note believed, red; `IndexOf(')')` for the first `)` — two stat rows red; the start ticks not compared — the live-pid-other-start row red; the path not consulted (a link followed) — the link-as-note test red; the open without `O_NONBLOCK` — the FIFO test red after its 5 s (a FIFO open waits for a writer). Round four: the open without `O_NOFOLLOW` — `A_note_that_is_a_link_to_an_outside_FIFO_…` red (`Expected writer.IsCompleted to be False … but found True`: the blocked writer was released). The huge-note test was red on the old reader as written: 840 MB allocated for a 200 MB note.

### Not proven (said, not hidden)

The lock on Windows and macOS (no code of the story ran there; the Windows conflict codes are documentation, the mapping fails closed elsewhere; the POSIX path facts do not exist on Windows, so the path check is not performed there); NFS and CIFS (a file system without `flock` makes the runtime ignore the lock); a non-.NET holder; a swap of `.gate.lock` that is made after the comparison; a link in a parent folder of the root (D17's root check); that a gate step prints its wait (E2.S7's obligation); that claude hands the coai server the `env` member of `mcp-coai.json` (M2, E3.S5); and the verb on CI's Linux runner — it ran only on this WSL host.

### Deviations from the story (also E2 §8 item 57)

1. `ModelArmsBinary` runs the console's **apphost** (`ExperimentRoot.ConsoleBinary`, as the scenario tests do) through the shared launcher, not `dotnet model-arms.dll` located through the bin folder: it reuses what exists and is an absolute path. It adds the child handling (line sink with arrival instants, kill and reap) and starts the test-only `gate-holder` the same way.
2. `A_second_process_holding_the_lock_makes_the_first_wait` asserts the wait by the instants (acquired more than 3 s after `held`; `released` printed; exit 0), not by `acquiredAt >= releasedAt`: the lock is freed BEFORE `released` is printed, so that comparison is a race.
3. The disable-locking switch was checked and the rule differs from the story's sentence. The names `System.IO.DisableFileLocking` and `DOTNET_SYSTEM_IO_DISABLEFILELOCKING` are in `System.Private.CoreLib.dll` 10.0.14 and in dotnet/runtime `SafeFileHandle.Unix.cs`; the Microsoft `FileShare` page does not mention them (only that page was checked). Measured on 10.0.14: a variable that reads as a boolean (`1`/`true`/`0`/`false`, any case, untrimmed) decides, even against the switch; any other value leaves it to the switch. The refusal is `FileGateLock.Create` (an `Outcome`) and the constructor, by that rule; the conflict code is an explicit Linux/Windows mapping that fails closed elsewhere (Windows codes from documentation, not observed).
4. `hold` got a 15-minute wait ceiling (exit 3, its own words; the operator's stop stays a stop, 5) and `--seconds` an upper bound of 86400 — added by common.reliability and the clamp rule; the story had neither.
5. Scenarios beyond the story's nine: a cancelled token on a free lock; the killed holder; `status` in process; a stop while holding, a stop while waiting, the ceiling; the refusal table (17 rows); the poll of zero; `GateStatus` no-file, not-text and lone-surrogate rows; `McpConfig` hostile key, odd path, emoji, lone surrogate, gate-less refusal; and the review round's (items 8–10).
6. `WorkerLiveness` (listed under *Uses*) is used only by `status`'s check of the holder's note (is that pid alive); the lock itself needs no pid — the kernel is the liveness.
7. The story's inherited-descriptor scenario cannot see close-on-exec (measured: with the flag cleared and a child alive, the lock is still free after `Dispose`, because the runtime unlocks on release). A second test, `A_holder_killed_while_its_child_lives_frees_the_lock_and_the_child_holds_no_descriptor_of_it`, with a test-only helper project (`tests/GateHolder`), kills a holder whose child lives and is red with the flag cleared (review R7). Nothing is "argued, not tested" any more.
8. `McpConfig` texts carry no trailing newline (`make` adds it); `Coai` with no binary, and any lone surrogate in the command, side, data folder or key, throws `ArgumentException` naming the field. The relaxed encoder is not `JSON.stringify` for characters outside printable BMP text (review R8; the module doc says what it escapes).
9. **The lock path is checked (review R1, D17).** `FileGateLock.Take` reads `.gate.lock` without following a link, refuses a link, folder, FIFO or device (verb exit 4, the sentence names path, kind and D17), creates a missing file with `FileMode.CreateNew` and compares the open handle's device and inode with the path after the open; `status` creates nothing. **Not the review's wording:** the created file is not reopened — `CreateNew` with `FileShare.None` already holds the lock on the file it made, so a reopen would only add a window.
10. **The holder's sidecar (review R2) changes the story's `status` line.** A holder writes `.gate.lock.holder` (pid, process start, since) after it has the lock and removes it before it releases; `status` prints `held by pid N since <utc>` for a live holder and `held (holder unknown)` otherwise (the story printed `held`). The note records the kernel's `starttime` (field 22 of `/proc/<pid>/stat`, an integer — two `Process.StartTime` readings drift and made a live holder read as unknown) and is read through `KernelFacts.ReadSmall` (non-blocking, descriptor-judged, at most 4 KiB), so a FIFO, a link or a huge note is `holder unknown` at once; off Linux the holder is believed by its start second (E3.S0), and on Windows none is named. The wedged-holder residual and the file systems measured (ext4, tmpfs, 9p honoured; a Windows process does not see the lock; NFS and CIFS not probed) are in the module doc and the adapter's header.

## The gate step (E2.S7)

`GateStep` (`Adapters/GateStep.cs`: `GatePoint`, `GateProbe`, `GateStep`, and the per-call `GateRun` it builds) is `run-arm.sh:144-162` kept whole — lock, step, unlock, read the status, blocked → wait, loop — and rewritten by E2 §8 item 44: **the status is judged on EVERY entry, and the wait count is DURABLE.**
`RunAsync(context, stopping)` returns the runner's `StepOutcome`. It reads no `.done`, keeps no count of its own and never calls `StepRunner.Recover`: it asks the runner to RUN, and the runner's recovery is the one rule.

```mermaid
flowchart TD
    A["log: waiting for the gate lock"] --> B{"TryAcquire"}
    B -- free --> D
    B -- busy --> C["say who holds it and the ceiling, then AcquireAsync under the ceiling"]
    C -- ceiling ran out --> X5["stopped, exit 5, holder named"]
    C -- held --> D["runner.RunAsync with DeriveOutputs false"]
    B -- path refused --> X4["stopped, exit 4"]
    D --> E["lock released"]
    E -- Stopped --> S["return it at once"]
    E -- Done, skipped or not --> F["judge: gate.json and the gate-wait lines of this step"]
    F --> G{"blocked and waits below gateTries"}
    G -- no --> H["diff once from .before, log gate status, Done"]
    G -- yes --> I["gate-wait line, marker, remove .done, wait"] --> A
```

**The order of a blocked round, and why.** (1) the `gate-wait` line (`TimingLine.GateWait`: arm, step, `after` = the last attempt, `wait` = the round, `why`, time); (2) the `.continue` marker `{"after":N,"why":"gate blocked: …"}`, replaced in place; (3) the `.done` is removed; (4) the wait. A try is spent when it is DECIDED, so a death between (1) and (2) costs one try and never an extra round; the marker is written BEFORE `.done` goes, because a finished stream without `.done` AND without a marker is read by the runner as "died after finishing" and marked done again
(the swap break-it below). The marker is the intent; the runner resumes the SAME session because the marker names the last attempt — never because `.done` is gone.
**Why a timing line and not a field of the marker:** the runner deletes the marker when the step is done, so a count kept there would read zero on the entry after a gate that finished still blocked. `timing.jsonl` is append-only, is read by the runner already and survives the step; `metrics.mjs` filters `start` and `end` and passes an unknown kind.

| a gate killed at | what the files hold | the next entry |
|---|---|---|
| before `StatusRead` | the runner's `.done`, no marker, no new line | judges again from `gate.json` |
| `StatusRead` → `WaitRecorded` | the same | the same |
| `WaitRecorded` → `ContinueWritten` | one line more, `.done`, no marker | the runner skips; the judgement COUNTS the line (a spent try) and decides from the CURRENT status |
| `ContinueWritten` → `DoneRemoved` | marker and `.done` | the runner removes `.done` (E2.S4 step 1) and resumes |
| after `DoneRemoved` | the marker alone | honoured: the session is resumed |

A gate that finished still blocked (its tries spent) is NOT revived by a restart: the count is read back, and `waits == gateTries` ends it (`gate status blocked — after N waits, finished as the shell did`). To revive it, raise `limits.gateTries` in the experiment file (re-read at every entry, D4) and run the arm again; the next entry waits once more. Deliberate, and in the log.

**The wait for the lock has a ceiling** (E2 §8 item 57, common.reliability). The gate takes the lock with `TryAcquire` first; when another process holds it, the gate prints `<step>: the gate lock is held by pid N since <utc> — waiting at most <ceiling> for it (…)` (`FileGateLock.WhoHolds`: `by pid N since <utc>`, or `(holder unknown)`) and waits through `AcquireAsync` under a linked token that carries the ceiling. The ceiling is OURS: it is told apart from the operator's stop by the stop token's state (never by the exception's type). When it runs out the step stops resumable — exit 5, a `stopped` line whose reason holds the holder's description — and the lock is never broken; the operator's stop is `stopped by the operator` (5). A path the lock refuses (a link, a folder, a FIFO: `GateLockRefusedException`) is exit 4. `GateStep.DefaultLockCeiling` is six hours: a gate step runs up to its own `minutes` under the lock (150 in the worked example) and a few arms queue behind it. It is a constructor parameter, not an experiment-file limit (deviation 1); the verb that builds the gate (E2.S8) passes the value it wants.
An exit 5 from the runner is returned at once, never repeated inside the loop: a relaunch costs one paid attempt (E2 §8 item 56), so the NEXT entry decides.

**The model's file and text are a stranger's** (D17; E2.S7 review R1). `notes/gate.json` is read by `GateStatusFile`, not by `ArmFiles.ReadText` (which has no size limit, follows links and blocks on a FIFO — measured: a FIFO hung the arm, the open being synchronous so neither the ceiling nor the stop token reached it; a link was followed outside `notes/`; a 20 MB `why` was copied whole into every `gate-wait` line). Since E3.S0 it goes through `BoundedFile` (C16), which owns both roads: on Linux x86-64 `KernelFacts.ReadSmall` (`O_NONBLOCK | O_NOFOLLOW | O_NOCTTY | O_CLOEXEC`, the descriptor judged a regular file and compared with the path) with the named bound **`GateStatusFile.LimitBytes` = 64 KiB**; elsewhere through a bounded fallback (one bounded-read helper, `Portable`; a link is refused from the file's own attributes, a folder or device is not a regular file, at most limit + 1 bytes are read, shared with a writer). Three answers: absent (the model wrote none: the shell's `missing`, the gate finishes), text, or **refused** — a link, a FIFO, a folder, a file past the limit. **A refused file is not `missing`, and neither is text that is there and cannot be judged (round four):** finishing a gate whose answer could not be read would let a blocked gate pass, so the step stops resumable (exit 5, `the gate's status cannot be judged — <reason>; fix it and run me again (removing it finishes the gate as missing)`; for text that was read the sentence also quotes `it begins: <the first 200 printable characters>`) with no round recorded; the next entry judges again, so it stops until the operator acts, as it does for a missing `.before`. **What the bytes may be (re-check R6):** UTF-8 text. A leading UTF-8 byte order mark is taken off before decoding on BOTH roads, by one helper — kept, it is U+FEFF in front of `{`, the JSON does not parse and a BLOCKED gate would read as `missing` and pass (the shell's `require()` and the first build's `StreamReader` both dropped it). A UTF-16 byte order mark and any NUL byte (UTF-16 without a mark, a file zero-padded by a crashed write) are refused with the reason named, never decoded to garbage and read as missing; CRLF is JSON whitespace. Text that decodes but is not a JSON object with an allowed `status` is UNJUDGEABLE, not `missing` (below). The `why` that is WRITTEN DOWN (the `gate-wait` line and the marker, which every later entry reads) is clamped to **2048 characters**, never cut inside a surrogate pair; in a log line `why` and `status` go through `ExecutableRule.Printable` and a 300-character clamp (a newline in a reason would otherwise split the line).

### Tests (`GateStepTests` 21 cases and `GateStepInputTests` 28 cases over `StepBench`; `GateStepKit` holds the recording, refusing and busy locks, the probes and the writers of `gate.json`)

| Test | What it holds |
|---|---|
| `A_blocked_status_removes_done_waits_and_resumes_up_to_the_tries` | blocked twice then proceed: 3 calls of one session, 2 waits of 1800 s, each wait finding `.done` absent, the marker present naming the calls so far, and the `gate-wait` line already written |
| `A_gate_killed_after_done_and_before_its_round_was_recorded_or_ordered_is_judged_again_on_restart` (4 rows) | `(StatusRead, 1)`, `(StatusRead, 2)`, `(WaitRecorded, 1)`, `(WaitRecorded, 2)`: the files after the death, then a second gate over a new runner — calls, waits, lines, the skip when the tries are already spent |
| `A_gate_finished_still_blocked_after_its_tries_is_not_revived_by_a_restart_unless_the_tries_are_raised` | gate 1: 3 calls; gate 2 over the same bench: nothing new; `gateTries` raised in the file: one more wait and call |
| `A_runner_killed_between_the_continue_marker_and_the_resume_resumes_on_the_next_entry` (2 rows) | `ContinueWritten` and `DoneRemoved`: the marker is on disk, `.done` is there or not, and gate 2 resumes the session (call 2 `--resume`, `attempts` 2, lock events `acquire, launch, release`) |
| `A_finished_gate_removes_its_continue_marker_and_a_stale_one_is_ignored` | the finished gate leaves no marker; a marker for an attempt that has started orders nothing |
| `The_lock_is_released_during_the_wait` | `acquire, launch, release, wait, acquire, launch, release, wait, acquire, launch, release`, always on `<root>/.gate.lock` |
| `The_before_snapshot_is_taken_before_the_first_attempt_and_the_diff_only_from_a_finished_step` | a limited attempt's wait sees no `.diff`; `.before` is HEAD before call 1; a reviewer step through the runner alike |
| `Still_blocked_after_every_wait_finishes_the_step_with_its_status`, `A_gate_step_that_is_done_regenerates_a_missing_diff_and_launches_nothing`, `A_blocked_round_leaves_no_diff_behind` | the shell's fall-through; the finalization writes a missing diff; no diff inside a round |
| `A_gate_that_wrote_no_status_is_judged_missing_…`, `A_stopped_step_is_returned_at_once_…`, `A_stop_during_a_blocked_round_…`, `A_finished_gate_whose_before_snapshot_is_gone_stops_resumable_…` | added: no status file, no wait; exit 5 is not repeated; the operator's stop leaves the marker and a `stopped` line; a missing `.before` is the resumable stop and no diff |
| `A_lock_path_the_lock_refuses_…`, `A_lock_held_by_another_process_is_waited_for_at_most_the_ceiling_…`, `A_stop_while_waiting_for_the_lock_…` | added: exit 4; the REAL `FileGateLock` held by the test-only `gate-holder` process, a 400 ms ceiling, the line naming the holder before the wait and the `stopped` line naming it after; the operator's stop is not the ceiling |
| `GateStepInputTests` (fix round; the re-check's cases are the last seven below) | `A_gate_json_that_is_a_FIFO_…` (bounded by a 10 s backstop: it returns at once), `…a_link_is_not_followed_…`, `…bigger_than_the_limit_…` (5 MB: refused, `timing.jsonl` under 16 KiB), `A_why_longer_than_the_clamp_…` (marker and line at most 2048 characters, no half emoji), `The_bounded_read_that_off_Linux_x64_takes_over_…` (the fallback called directly: link, folder, exactly the limit, past it, absent), `A_lock_ceiling_the_timer_cannot_hold_…` (refused at construction, the largest accepted), `The_holder_a_busy_gate_names_is_the_one_the_lock_it_was_given_describes`; re-check R8: `A_status_that_is_there_and_cannot_be_judged_stops_resumable_…` (11 rows: a Markdown fence, text after the object, two objects, a `//` comment, a byte order mark twice, white space before the mark, an over-long encoded quote, an invalid byte inside the word, `Blocked`, an unknown word, an empty file — each exit 5 with the reason, `it begins:` and no `gate-wait` line), `An_object_with_no_status_finishes_the_gate_as_missing` (2), `A_blocked_object_with_a_trailing_comma_is_still_judged_blocked`; re-check R6: `A_blocked_status_is_read_as_blocked_whatever_wraps_it` (a UTF-8 byte order mark, CRLF), `A_proceed_status_behind_a_byte_order_mark_…`, `A_status_that_is_not_UTF_8_text_is_refused_by_name_…` (UTF-16 both orders, a trailing NUL), `The_portable_read_takes_a_byte_order_mark_off_and_refuses_…` |

**Break-its** (planted in the finished code from a copy, built, seen red, the copy restored — `git diff` empty): the marker written AFTER `.done` is removed — `(DoneRemoved)` `Expected File.Exists(…continue) to be True because the intent is written before the .done that contradicts it is removed`, and with that early assertion taken out `Expected calls to contain 2 item(s), but found 1` (gate 2 recovers the finished stream as done); the old step 1 (a skipped step is returned without judging) — `(StatusRead, 1)` `Expected collection to contain 2 item(s) because a gate that was blocked is judged again…, but found 1`, plus the not-revived test and the diff regeneration; a LOCAL waits count — `(WaitRecorded, 2)` `… contain 2 item(s) …, but found 3` and the not-revived test `… contain 3 item(s) … but found 5`; the lock wait without its ceiling — `Expected string "stopped by the operator — …" to contain "held by pid"` (the backstop token of the test ended the unbounded wait after 20 s); the runner deriving the diff inside the loop — `A_blocked_round_leaves_no_diff_behind`: `Expected File.Exists(…4-gate.diff) to be False because the diff belongs to a finished gate, not to a round`; the marker written BEFORE the round's line — both `(WaitRecorded)` rows `Expected File.Exists(…continue) to be False because no marker was written yet`; the lock refusal not caught — the exit 4 test fails with the exception.

**Deviations from the story** (the same seven, numbered alike, are E2 §8 item 58). (1) The lock's ceiling is the constructor's `lockCeiling` (default six hours), not a key of `limits`: a new key would change the experiment file's schema and E1's readers for a number E2.S8 knows how to choose; it must be positive and at most `GateStep.MaxLockCeiling` (`uint.MaxValue - 1` ms, about 49.7 days — what `CancelAfter` can hold; refused when the gate is built, review R2). (2) The constructor takes the clock and the map too (`GateStep(runner, gateLock, clock, wait, log, map, lockCeiling, probe)`): the gate writes its own `gate-wait` and `stopped` lines. (3) The lock is taken with `TryAcquire` first and `AcquireAsync` only when it is busy, so the "who holds it" line is never printed for a free lock; the holder is described by the lock the gate was given (`IGateLock.Describe`, which `FileGateLock` answers from the holder's note), not by a static call (review R4). (4) The ceiling scenario holds the lock with the test-only `gate-holder` process, not a `gate-lock hold` child: the verb needs a valid experiment root and the bench's root is not one; both take `.gate.lock` through `FileGateLock` and write the same note. (5) Scenarios beyond the story's ten rows — the first round's six (no status file, exit 5 not repeated, the operator's stop in a blocked round, a missing `.before`, the lock's refusal, the ceiling, the stop while waiting) and the fix round's seven in `GateStepInputTests` — and `StepBench` got `RereadExperiment`. (6) The model's file and text are read and written as a stranger's: `gate.json` through a bounded no-follow road with a 64 KiB limit, a refused file a resumable stop and not `missing`, the persisted `why` clamped to 2048 characters, the log's to 300 through `Printable` (the section above; review R1). (7) Two small things: the red-phase stub passed `DeriveOutputs = false` to the runner, because with the runner's default the diff scenarios were already green against it; and `GateRun` builds its own `ArmGit` for the finalization, as `StepRun` does — the context holds no shared one.

### Not proven (said, not hidden)

A real gate model writing `gate.json` (the test writes it — the fake cannot) and a real 30-minute round (the pilot); the lock's wait on Windows (the holder helper is Linux's, so the ceiling scenario skips there); the bounded read of `gate.json` off Linux x86-64 — since E3.S0 it is `BoundedFile`'s portable road, which answers a FIFO by a 5 s ceiling, not by its kind (before it, a FIFO at `gate.json` on macOS or aarch64 waited for a writer; the limit and the link check hold); a link in a PARENT folder of `notes/`; a `.done` with no stream at all (`after` would be 0, the marker unreadable and ignored — the rounds are still bounded by `gateTries`); a gate killed at a point no `GatePoint` names.

**The runs** (2026-10-30, WSL, .NET SDK 10.0.114, `Release`, through `low-priority.mjs`): the new classes and their neighbours (`GateStepTests`, `GateStepInputTests`, `GateStatusTests`, `StepRunnerTests`, `StepRestartTests`, `ModelArmsArchitectureTests`), 113 cases, 113 passed; namespace `Bench.Tests.ModelArms` 880 total, 877 passed, 3 skipped; whole suite 3953 total, 3367 passed, 572 failed, 14 skipped — every one of the 572 is in the main-branch list of failures taken on this host (Postgres and Docker tests), none is new. Build 0 errors, the one `ASPIRE010`; `dotnet format --verify-no-changes` clean. The reds against the one-run stub: 21 of 21 cases by an assertion (the kill rows: `Expected a <System.IO.IOException> to be thrown, but no exception was thrown.`; the blocked rounds: `Expected bench.Fake.Calls() to contain 3 item(s), but found 1`; the lock events, the ceiling, the stop lines and the diff scenarios by their own assertions); the exit 4 case was added after the code and its break-it is an escaping exception. **The fix round** (the Opus review of `e92724d`): seven `GateStepInputTests` cases, each red by an assertion against stubs before the code (the FIFO test, bounded by a 10 s backstop, reported `the gate reads gate.json without waiting`; the link and the 5 MB file ended `Done` where a stop was expected; the marker's why was 12063 characters against 2062; the busy lock's holder read `(holder unknown)` against the double's words; the ceiling above 49.7 days was accepted). Break-its seen red: the clamp removed (the marker assertion above), the holder asked of the static `FileGateLock.WhoHolds` (`…to contain "held by the double, pid 4242, since never"`), the maximum check removed (`Expected a <ArgumentOutOfRangeException> to be thrown`). The architecture scan `The_module_reads_links_only_through_RootFacts_RealPathOf` caught a first draft that read a link's target in `GateStatusFile`; the check now reads the file's own attributes. **Round three** (the re-check of `3a2fe0d`): a blocked status behind a UTF-8 byte order mark was judged `missing` and passed — red first (`Expected bench.GateWaits() to contain 1 item(s) because a blocked status behind a UTF-8 byte order mark is a blocked gate, never a missing one, but found 0`; the proceed row ended `gate status missing`; UTF-16 and NUL ended `Done` where a stop was expected); the CRLF row was green at once (regression). **Round four** (E2 §8 item 60): 21 cases red by assertion before the code (the nine `GateStatusTests` rows and the lone-surrogate status row: `Expected boolean to be True because '…' is there and says nothing the gate can act on`; the eleven step rows ended `Done` where `Stopped(5)` was expected); `{}`, `{"status":null}`, the trailing comma and the six brief words were green at once (regression rows). Not red-first: the fallback's folder and exact-limit rows pass against the stub's behaviour by construction and are read as regression rows.

## run-arm (E2.S8)
`run-arm <arm> --experiment <root>` (`Verbs/RunArm.cs`, `RunArmAdmission.cs`, `RunArmWalk.cs`; E2.S8) runs ONE arm of ANY scheme without a `tasks` block: C, CR and the part-7 CHH. It walks the scheme's entries over the step contract (C12) — a gate step through `GateStep`, every
other step through `StepRunner` — and knows four kinds of step and nothing about C, CR or CHH by name. A scheme with a `tasks` block (SRS) is refused until E4 (exit 4, `story E4.S3`). Exit 0 the arm is done; 3 the host or the account (a CLI that is not there, a limit still on after the
waits); 4 a wrong configuration; 5 a step stopped and the same command resumes it.

```mermaid
flowchart TD
    G["ExperimentGate: root, trust, experiment.json, files the stage needs (arms.map.json, chain/made-ARM, baseline.json)"] --> A1
    A1["scheme: ARM's letters among the experiment's schemes, no tasks block, else 4"] --> A2["token: arms.map.json, else 3 (unreadable) or 4"]
    A2 --> A3["PATH: every entry judged (D17), the first refusal is 4"] --> A4["claude, then git, by ExecutableResolution.Resolve: absent 3, refused 4"]
    A4 --> A5["scenarios.json as the gate judged it: its count, else 4"] --> P["print: resolved executables, each as name → REAL path (D17 c)"]
    P --> W["walk: entry by entry, the first Stopped ends it"]
    W -- all done --> D["arm-done, unless the last timing line already is this arm's; log: arm done; 0"]
    W -- Stopped(code, why) --> S["stderr: model-arms run-arm ARM: why; code"]
```

**One value, printed and launched** (D17 c, d): the two REAL paths admission resolves are the paths printed in `resolved executables:` and the paths every `StepContext` carries as `ClaudeExecutable` and `GitExecutable`; nothing in the verb is launched by a bare name, and the lines are written
before the first launch. A link is printed as what it resolves to (the fixture's `claude` is a link to the fake's apphost, so the line names the apphost). The PATH is judged BEFORE the executables are looked up in it: git and claude look their own helpers up in that PATH for hours, so a PATH entry
that is under the target or the root, relative, empty or unresolvable is exit 4 with `ExecutableResolution.PathEntryRefusal`'s sentence, and nothing is printed or launched. The order inside admission is this document's: PATH first, then `HOME` and `XDG_CONFIG_HOME` (git reads its global config there and a claude its `settings.json` and hooks; the same helper `validate` uses, `ConfigFolders`), then `claude`, then `git`, then the scenarios.

**The walk** (`RunArmWalk`). One `StepRunner` and one `GateStep` are built over the SAME clock, the SAME log writer (the verb's output) and the SAME `ArmMap` (E2 §8 item 58: the gate writes its own `gate-wait` and `stopped` lines and scrubs its own log through them; a gate built with another clock
stamps another time, and one built with another map prints the token). The gate lock is the root's own file lock (`FileGateLock.Create(TimeProvider.System, 2 s)`, a host that cannot lock is exit 3). The wait of a limit and of a blocked gate round is `Task.Delay(span, stopping)`: bounded by the experiment's
`limits`, cancellable by the operator's stop. The first `Stopped` ends the walk; nothing after it launches. At the end `arm-done` is appended **unless this arm already has one after the timing's last `start` or `gate-wait` line** — nothing has run since — so a finished arm run again adds nothing (the shell appended one per run) and a `stopped` line left after an `arm-done` does not earn a second one (fix round, review R2). A rerun of a finished arm still takes the gate lock (the gate judges every entry); an operator's stop while it waits for a lock somebody else holds returns exit 5 and, when the gate step is done and `gate.json` asks for no more rounds, writes no `stopped` line (`GateRun.IsFinal`, which also requires this arm's `arm-done` by `TimingLines.IsArmDone` — the rule `run-arm` writes it by — so a stop during the gate's finalization diff, before the arm is done, still writes its `stopped` line: D16, the last line says what happened; last-round review R6, `A_stop_during_the_gates_finalization_diff_is_a_stopped_line_because_the_arm_is_not_yet_done`, red `Expected type to be …Stopped … but found …End`).

**An exit 5 is never repeated by this verb** (E2 §8 item 56). A relaunch of an attempt that ended without a verdict costs one paid attempt; the verb returns the stop and the operator, or E4's `run-all`, decides. The scenario `An_exit_5_is_never_repeated_by_run_arm_itself` pins one call for one
interrupted step; planting a retry in the walk turns it and the interruption theory red.

**The gate lock's ceiling is a total time, and this is the choice for `run-arm` (E2 §8 items 59 and 61).** The gate step waits for the lock under a ceiling that is the TOTAL it may wait. Under several arms at once each holder keeps the lock for its gate step's wall, the grace it is killed after, AND the usage-limit waits the
runner spends inside the gate step while still holding it AND the floor each resumed attempt is given after a limit even when the wall is spent (`StepRecovery`: `max(minutes × 60 − used, minimumRemainingSeconds)`); the last arm of a round of N waits up to (N − 1) such spans, and a shorter ceiling stops it with exit 5 behind gates that are all alive (a relaunch costs a paid attempt). `GateLockCeiling.For(arms, gateMinutes, killGraceSeconds, limitTries, limitWaitSeconds, minimumRemainingSeconds, maximum)` (Core, pure)
is that number: `max(1, arms − 1) × (gateMinutes × 60 + killGraceSeconds + limitTries × (limitWaitSeconds + minimumRemainingSeconds))` seconds, never under 60 s, never over the timer's maximum (`GateStep.MaxLockCeiling`), in `long` with each factor clamped first. `arms` is `GateLockCeiling.QueueOf(experiment.Runs, arm, armsInMap)`: the size of the largest round of the
run that names the arm (the arms of an earlier run and the closed arms of this one are not queued behind it), and only when no run names the arm every arm of `arms.map.json`, which `run-arm` says in one log line before the first launch (`C4 is in no run — the gate lock's wait is sized from the N arms of arms.map.json, closed arms of earlier runs included`).
The worked example (C1 in the round of three of `part6`, a 150-minute gate, a 600 s grace, 40 × 900 s of limit waits, 40 × 300 s of resume floors) is 2 × (9 000 + 600 + 36 000 + 12 000) = 115 200 s = 32 h, where the gate's default is 6 h; the busy-lock scenario (a 1-minute fixture gate) prints `waiting at most 1622 min`.
(The first choice of this round counted wall plus grace over every arm of the map; the coai code gate over E2 found that it expired behind healthy gates — it left out the limit waits and resume floors under the lock and counted closed arms. This is the fix, within item 58 (1), "a number E2.S8 knows how to choose", and item 59's "at least (arms − 1) × the gate step's minutes".) **What remains is a wedged holder, which only a heartbeat
(E4) tells from a slow one** — in the example a wedged gate holder keeps the next arm of its round waiting about 32 h before exit 5 (about 7 days in the 12-arm map fallback), until E4's heartbeat lands: the holder is alive (`IGateLock.Describe` names a live pid) and its stream grows. The heartbeat is NOT built; until then a wedged holder is told apart only when the ceiling runs out. E4's `run-all` sizes its own the same way from the round it starts, or builds the heartbeat. The ceiling is what the gate prints in `waiting at most <ceiling> for it` when it must queue.

**What the arm leaves** (a C arm after a clean run; `<arm>` is `r/<token>/`, the clone is `<arm>/arm`): `out/<step>.session`, `out/<step>.a<N>.prompt.md` / `.jsonl` / `.err`, `out/<step>.done`, the derived `notes/review.json` (a review's verdict, from its last result), `out/4-gate.before` and `out/4-gate.diff`
(the gate's snapshot and the one diff written when the gate finished), and `out/timing.jsonl` ending in the arm's one `arm-done` line:

```
out:   1-code.done 1-code.session 2-review.done 2-review.session 3-fix.done 3-fix.session 4-gate.before 4-gate.diff 4-gate.done 4-gate.session   (+ the .a1.jsonl/.err/.prompt.md of each)
notes: review.json   = {"verdict":"ready","findings":[]}
```

**By hand, once** (2026-10-30; the script is the one the M1 record below came from — a scratch root, the fake as `claude`, the binary by its path):
`env PATH=<root-bin> HOME=<home> FAKE_CLI_HOME=<fake-home> ./tools/model-arms/ModelArms/bin/Release/net10.0/model-arms run-arm C1 --experiment <root>` printed

```
resolved executables:
  claude → <repo>/tests/FakeCli/bin/Release/net10.0/fake-cli
  git → /usr/bin/git
2026-10-30T12:53:53Z [C1] 1-code attempt 1 (fresh, session 9cdffa0f-…, budget 300s) → <root>/r/<token>/out/1-code.a1.jsonl
```

### M1: a fake arm of each scheme, a limit waited out, an interruption resumed, read by the pinned `metrics.mjs` (2026-10-30)

Each scheme's arm (C1, CR1, CHH1) was run on a scratch root with the fake's script `{stdoutFile: result-success.jsonl, stdoutByCall: {<n>: result-429-usage-limit-reached.jsonl, <m>: stream-no-result.jsonl}}` and `limits.waitSeconds` 1: the first run waits the limit out (resuming the same session)
and is cut mid-step (exit 5, no `.done`, the session kept); the second run resumes the cut step with ONE call and finishes the arm; a third run of C1 launched nothing and left one `arm-done`. The timing of the interrupted step, as written (C1, `3-fix`):

```
start 3-fix a1 fresh        end 3-fix a1 end=interrupted result=False
stopped 3-fix exit=5        (3-fix INTERRUPTED (no result line and no limit — the process ended without saying how) — run me again to resume it)
start 3-fix a2 resume       end 3-fix a2 end=finished result=True          … arm-done
```

The pinned `research/local-model-coding/harness/metrics.mjs` (copied with its one root line rewritten, the arm's `timing.jsonl` linked where it looks; the pinned file is never edited) exited 0 for every scheme and named every step, the cut step as two attempts (the first `complete=False`, `cost=None`, the second
`complete=True`), the limited step as two attempts too (the 429 result is a result: `complete=True`, `cost=0`, then the resumed attempt):

| arm | calls (first run, all) | steps in `report` (attempts each) | derived files |
|---|---|---|---|
| C1 (limit on call 2, cut call 4) | 4, 6 | `1-code`(1) `2-review`(2: api_error, completed) `3-fix`(2: interrupted, completed) `4-gate`(1) | `notes/review.json`, `out/4-gate.before`, `out/4-gate.diff` |
| CR1 (limit on call 2, cut call 4) | 4, 7 | `1-code`(1) `2-hreview`(2) `3-review`(2: interrupted, completed) `4-fix`(1) `5-gate`(1) | `out/2-hreview.before` / `.diff`, `notes/review.json`, the gate's pair |
| CHH1 (limit on call 2, cut call 5) | 5, 9 | `1-code`(1) `2-hA`(2) `3-hB`(1) `4-hfix`(2: interrupted, completed) `5-review`(1) `6-fix`(1) `7-gate`(1) | `notes/hA.json`, `notes/hB.json`, `notes/review.json`, `out/2-hA.before`, `out/4-hfix.diff` (from `2-hA.before`), the gate's pair |

**M1 proves the shape, not the cost figures.** The recorded success has no `total_cost_usd`, so every `cost` above is `None` and the arm total is NaN. The pinned `metrics.mjs` (`:140`, `:144`) sums each attempt's `total_cost_usd` and `modelUsage` with no per-session dedupe, and a resumed attempt's result carries the SESSION's cumulative cost, so a resumed step is counted twice on a real run; the consumers must take the last result per session (E2 §8 item 62, owner E6).

(The fake commits nothing, so every diff is empty; the CHH theory commits a file in the clone between the two runs and shows the diff spans it.)

### Tests (`RunArmSchemeTests` 25 cases — 23 methods, the interruption theory's three rows and the config-folder theory's two counted — and `GateLockCeilingTests` 10; `RunArmKit` holds the helpers; `ExperimentRoot.MakeArmAsync` makes the arm)

The twelve of the story (the theory counted once), each seen red on an assertion against the `NotBuilt` stub, and eight added: `Scheme_C_runs_its_four_steps_in_order_against_the_fake`, `Scheme_CR_inserts_the_fresh_review_and_fix`, `Scheme_CHH_runs_two_read_only_reviews_to_two_verdict_files_a_fix_that_reads_both_and_a_diff_from_the_first_snapshot`
(two replays with distinct verdicts, `notes/hA.json` and `notes/hB.json` each from its own call), `An_unknown_arm_is_refused_with_exit_4`, `A_finished_arm_run_again_launches_nothing_and_writes_one_arm_done`, `An_srs_arm_is_refused_until_the_task_pipeline_exists`,
`Run_arm_prints_the_resolved_claude_and_git_before_the_first_launch`, `A_limited_step_inside_an_arm_waits_and_the_arm_still_finishes`, `Run_arm_launches_the_git_it_resolved_and_never_the_process_git`,
`The_opus_review_and_the_haiku_review_are_launched_without_the_agent_tool_fresh_and_resumed`, `A_sequential_arm_leaves_the_files_the_example_binds_and_the_consumers_read_them`, the interruption theory
`An_arm_interrupted_mid_step_is_resumed_by_the_next_run_arm_and_completes_for_every_sequential_scheme` (C / `2-review` / call 2, CR / `2-hreview` / call 2, CHH / `4-hfix` / call 4); added: a refused PATH entry (4), a `HOME` under the root and an `XDG_CONFIG_HOME` under the target (4, fix round R1), and a missing `claude` (3) before any launch, a finished arm stopped at a busy gate lock and a `stopped` line after `arm-done` (one `arm-done`, fix round R2), `An_exit_5_is_never_repeated_by_run_arm_itself`,
`The_gate_and_the_runner_share_one_clock_one_log_and_one_map` (a pre-written blocked `gate.json` naming the token; a fixed clock; the wait the verb was given), `The_gate_lock_ceiling_is_the_one_sized_from_the_queue_and_not_the_default_six_hours` (a busy-lock double, three arms made, the operator's stop
once `waiting at most 1622 min` is printed; a fourth arm made but named by no run is not queued behind C1's round), the fallback scenario for an arm in no run (the log line, 811 min), and the pure `GateLockCeilingTests`. `PinnedConsumer` (E2.S4's minimal helper) gained `MetricsAsync(root, arm, ct)` and `Listed(arm)` (C1 → `C`, CR1 → `CR1`, CHH1 → `CH1`); it was extended, not duplicated.

**Break-its** (planted in the finished code from a copy, built, seen red, the copy restored): `Delegation` removed from `StepLaunch` — `Expected call.Group("--disallowedTools").Take(2) to be equal to {"Agent","Task"} because 1-code fresh starts no subagent unless its spec says so, but {"Read(//…/.claude/**)", …} differs at index 0`;
an exit 5 repeated once by the walk — `Expected fake.Calls() to contain a single item because a relaunch of a verdictless attempt costs one paid attempt` and the interruption theory `Expected first.Code to be 5` (the repeat resumed the step and the arm finished); the gate built with `TimeProvider.System` —
`Expected … to represent the same point in time as <2026-10-28 21:32:38 +0h> … but <2026-10-30 12:49:17 +0h> does not`; with `ArmMap.Empty` — `Did not expect output … to contain "00000000000000a1" because no token reaches the log, whichever step wrote the line`; with a null writer — `Expected output … to contain "the gate was blocked"`;
the runner given no transcripts (re-enters `Fresh`) — `Expected at to be greater than or equal to 0 because call 3's argv should carry --resume` (3 rows); the default six-hour ceiling — `Expected output … to contain "waiting at most 22 min"`; the ceiling formula over `arms` instead of `arms − 1` — `Expected 8h, but found 10h and 40m`
(and two more rows); git launched as the bare name `git` — `Expected collection to contain at least 3 item(s) because the git shim should have recorded the arm's git runs … but found 0` (the shim, not the process's git, is what the PATH holds); the printed `claude` line a name and not the real path, `arm-done` written whenever the walk finishes
(`Expected root.ArmDones("C1") to contain a single item`), the `tasks` refusal and the PATH refusal removed — each red on its own scenario. The first run of the git-bare break-it was an exception, not an assertion (`FileNotFoundException` on the marker the shim never wrote); the scenario now asserts the missing runs.

**Fix round (the Opus review of `8c0017f`), red first, then the break-its.** R1: the two config-folder scenarios failed `Expected run.Code to be 4 because resolved executables: …` before the fix and fail the same way with the refusal removed from `RunArmAdmission.FirstRefusal`. R2: `A_finished_arm_whose_rerun_is_stopped_at_the_busy_gate_lock_keeps_exactly_one_arm_done` failed `Expected type to be …ArmDone because the arm stays done … but found …Stopped` before the fix and with `IsFinal` removed from the gate's stop; `A_stopped_line_after_arm_done_does_not_earn_the_arm_a_second_arm_done` fails `Expected root.ArmDones("C1") to contain a single item` with the old last-line dedupe. R4 (the interruption theory also asserts the resumed call's two tool groups equal the first call's and its prompt IS the resume notice) was green at once — the code was right — and goes red on all three rows when the resume sends the brief again (`Expected calls[call].Prompt to start with "This session was interrupted …`).

### Deviations from the story (also E2 §8 item 61)

(1) **`claude → …` is the REAL path.** The story's scenario text reads `claude → <temp>/bin/claude`; the fixture's `claude` is a link to the fake's apphost and D17 c prints the path the kernel opens, so the line names the apphost and the test resolves the link itself. (2) **The shim scenario's environment line is exactly
`GIT_NO_LAZY_FETCH=1`**, not "no line starting `GIT_`" (E2 §8 item 56, R5: the harness's own variable); and the arm's PATH is the fixture's `bin` alone, so the shim names `env` and `grep` by their paths. (3) **`ScenariosAsync` of `HostFactsReader` is `internal`** and `run-arm` reads `scenarios.json` through it (the gate's judged facts and the second `stat`),
not through a second read of the file named in the story: one road in. (4) **Admission order**: PATH, `HOME` and `XDG_CONFIG_HOME`, `claude`, `git`, then the scenarios (the story lists resolution first). (5) **The lock** is `FileGateLock.Create(TimeProvider.System, 2 s)` — the landed factory, not a constructor — and a host that cannot lock is exit 3.
(6) **A seam for tests**: `RunArmSeams(Clock, Wait, Lock)`, internal like `GateSeams`, so a test hands the verb a fixed clock, a recording wait and a busy lock; production passes `RunArmSeams.Real`. (7) **Scenarios beyond the twelve**: the PATH refusal, the missing `claude`, the exit 5 not repeated, the shared clock, log and map, the ceiling, and `GateLockCeilingTests`.
(8) (fix round, review R1, D26 c) `HOME` and `XDG_CONFIG_HOME` are judged by `run-arm` too — after the PATH entries and before the executables are looked up, by `ConfigFolders.Refusals`, the one helper `validate` shares (moved out of `HostFactsReader`): the first refusal is exit 4 with its sentence, nothing printed, nothing launched. Probed before the fix: `HOME=<target>/h` with a `.gitconfig` naming trace2 targets made the harness's own git append trace events to files the target chose. D26 (c) outranks the story's PATH-only text.

### Not proven (said, not hidden)

A real claude (the fake replays recorded streams and leaves an empty transcript — that `--resume` after a limit or a cut behaves as the shell observed is the pilot, parent build order step 6); the gate lock against a second arm under `run-all` (the lock's own tests and the busy-lock double here; the ceiling's
sizing is a number, not a measured queue); a wedged holder told from a healthy one (no heartbeat); `make` (the fixture makes the arm: the map, the marker, the baseline, the clone, the two MCP configs); Windows (the scenarios skip: links and POSIX paths); that the other three readers of the arm's files (E6) read what this
verb writes — only `metrics.mjs` is run.

## The platform roads (E3.S0)

Story E3.S0 (the parent's ledger 2.1: the owner overturned E1.S10's decision 4 — *"macOS and FreeBSD are fine [untested]; do not forbid their use"*). **A check the host can perform is performed; a check it cannot is named in one printed line per verb; nothing is refused for want of a reader;
nothing passes as done that was not done.** Windows keeps every behaviour it has. Three ways to run were weighed: a kernel `lstat` layout written from headers (decision 4's refused road: an offset nobody probed reads a WRONG owner or mode without an error — it is the follow-up of
E3 §8 item 25, with a self-verifying probe, not this story); `/usr/bin/stat -f` by absolute path (a second reader of a text nobody observed, and a launch inside the trust check); **the runtime's own portable facts — chosen**: `FileInfo.Attributes`, `File.GetUnixFileMode`, `Length` and
`LastWriteTimeUtc` are code the runtime ships and tests on those hosts, so they give a TRUE answer for what they cover, and they cannot answer the owner, the inode, the device, the change time or whether a file is a FIFO — exactly the list the printed line names.

**C18 — the table** (`ModelArms.Core.Experiment.PlatformRoads.Of(PlatformFacts)`, pure; `PlatformFactsReader` in the adapters reports this host: `OperatingSystem.Is*`, the process architecture, `StatxIsAvailable` and `RuntimeInformation.OSDescription`):

| Host | Trust | Bounded | Holder | LibcMkdir | The line names (NotVerified) |
|---|---|---|---|---|---|
| Linux x86-64, `statx` | Kernel | Kernel | ProcTicks | yes | — (empty) |
| Linux, another architecture, `statx` | Kernel | Portable | ProcTicks | yes | the bounded clause |
| Linux without `statx` | Portable | Portable | ProcTicks | yes | the owner, the kind of file, the inode and change time, the held lock file against its path, the bounded clause |
| macOS; FreeBSD; any other POSIX | Portable | Portable | ProcessStartTime | yes | the same four, the bounded clause, and *a lock holder's start is believed within 2 s of its note* |
| Windows | NotPerformed | Portable | None | no | — (Windows prints its own *not performed* lines where it does today) |

`StatxIsAvailable` is one `statx` of `/` that throws neither `EntryPointNotFoundException` nor `DllNotFoundException` AND returns 0: a `-1` (`ENOSYS` under a seccomp filter, an old kernel) is unavailable too, its words kept in the platform description for the line. Every row is explicit, with no
catch-all (`common.platform-limits`): a POSIX host not named takes the portable roads and its description is in the line. The portable bounded ceiling is one constant, `PlatformRoads.BoundedCeilingSeconds` (5), which `BoundedFile` waits by and the line prints.

**The ten decisions** (the parent's D10, D17 (a) and D26 (a) as amended):
1. *The trust facts.* `RootFacts.ReadFacts(path, road)`: Kernel → `KernelFacts.Read` (`statx`, unchanged); Portable → `PortableFacts.Read`: a link by its `ReparsePoint` attribute (`Kind = Link`, `Mode = None`, nothing read through it — and `RootFacts` refuses it naming the target), a folder `Directory`, anything else
   `Unknown` (a regular file, a FIFO and a device are not told apart), the mode by `File.GetUnixFileMode` read AFTER the link judgement, `OwnerUid = null`, the identity empty, a `FileWitness(Length, WriteTicks)`, `Source = Portable`; an absent entry or a denied folder is `Unread` with its words (exit 3), never a throw.
   `FileFacts` carries an optional owner (`null` is "not captured"; the Windows `-1` sentinel is gone).
2. *The judgement.* `RootTrust.Decide(TrustRoad, uid, files, root)`: NotPerformed → the line, as today; Kernel → owner and mode; Portable → the mode alone (`go-w` → exit 4 naming the file, the octal mode and `chmod go-w`). `FactsRead.MustBe`: on a portable fact an untold kind satisfies "a regular file" and never "a directory".
3. *The re-read comparison.* `RootTrust.Changes` compares by source: Kernel the six facts as before; Portable `kind`, `mode`, `length`, `mtime`; two sources differ in `source` alone — never a silent `0 == 0` over an identity nobody captured.
4. *The effective uid* — `geteuid` through libc, unchanged, unused by the judgement on the portable road.
5. *`Present`* — `statx` where `StatxIsAvailable`, `Path.Exists` elsewhere (a dangling link is there; `EACCES` reads as absent — a stated gap, not a check).
6. *The exclusive root* — libc `mkdir(path, 0700)` on every POSIX host (a path and a mode, no struct); a failure is classified by `Present` — an entry is there → `AlreadyThere` (exit 4), none → `Failed` with the words of the `mkdir` failure (exit 3): a STATE, not an errno number. `init` prints the road's line first, judges the folders on the road's facts, and Windows makes the folder by the runtime as before.
7. *The gate lock's conflict code is a MEASUREMENT* off Windows: `LockProbe.Measure(Path.GetTempPath())` holds `.model-arms-lock-probe-<8 hex bytes>` with `FileShare.None`, a second open must throw an `IOException` of exactly that type (its `HResult` is the code: 11 on Linux, observed), the holder is released and a THIRD open must succeed (the
   control: an `EIO` or a descriptor limit is never taken for the conflict); a second open that succeeds is *inert* (no gate lock is attempted). `Decide(Exception? secondOpen, Exception? released)` is the pure half. **The held file is verified honoured** after the path check: a second open must throw the measured code, else both streams are released and the answer is `Unread` *not honoured on this file system*
   (a file system without `flock`: NFS, CIFS?) — closing D10's residual at one open per acquisition. On the portable road the held file's inode is NOT compared (the line says so); the path's kind is judged again.
8. *The holder's note* — `start_ticks` (Linux) and `start_unix` (every platform), believed on `ProcTicks` by equal ticks, on `ProcessStartTime` when the pid is alive and `|StartTime − start_unix| ≤ 2 s`, on `None` never; read through `BoundedFile` on every road.
9. *The bounded no-follow read* — `BoundedFile.Read(path, limit, road)` (C16; `GateStatusFile` and the holder note delegate): a link is refused by the path's attributes, an absent path is `Absent`; Kernel → `KernelFacts.ReadSmall`; Portable → the runtime's open-and-read on a background thread under the 5 s ceiling (a FIFO with no writer or a silent one is told by the ceiling:
   `Refused … was not read within 5 s`, the thread left to end when its open returns); a folder is refused; the `FileAttributes.Device` test is gone (the runtime never sets it on Unix — an inert measure).
10. *`GateLockPath.Of(path, road)`* — Windows `Unjudged`; elsewhere through `RootFacts.ReadFacts(path, FileKind.Regular, road)`: a link and a folder are refused on the portable road, a FIFO is not told (the line).

**The one line.** `ExperimentGate` prints `road.Line` to the error writer ONCE, between step 2 (`Present`) and step 3 (the trust judgement), when it is not empty — before any refusal it qualifies; `init` prints it first. No adapter prints it. The road reaches the verbs through `GateSeams.Road` (default: this host's, computed once per process) and
`InitVerb.RunAsync(input, makeRoot, road)`; the adapters take the road as a parameter (`FileGateLock(clock, poll, road)`, `GateLockPath.Of(path, road)`, `GateStatusFile.Read(path, road)`), a test hands the Mac's. The defaults that read the current road (`FileGateLock(clock, poll)`, `GateStatusFile.Read(path)`, `GateHolderNote.Describe(path)`) exist for the E2 callers, unedited.

**Residuals, said.** The link judgement and the mode are two calls on the portable road — a link made between them has its TARGET's mode read; a swap that keeps the length and the write time is invisible to the witness (named in the line, by design). On the portable bounded road the link check precedes the open, so a link swapped in between is followed
(the runtime has no no-follow open); a device file is read as its bytes up to the limit or refused past it; a thread blocked in a FIFO's open stays until a writer arrives or the process ends (it is a background thread). `File.GetUnixFileMode` reads the file the link judgement saw only if nothing was swapped in the window.

### Tests (E3.S0)

`PlatformRoadsTests` (the table, per host; no host refused), `RootTrustTests` (the portable judgement, `Changes` by source), `FactsReadTests`, `PortableFactsTests`, `BoundedFileTests` (both roads; the FIFO ceiling), `LockProbeTests`, `FileGateLockTests` (the honour check; the conflict code is measured), `GateLockHolderTests` (`Believe`, the note's two members), `GateLockPathTests`
(the portable road), `KernelFactsTests` (mkdir classified by presence), `InitVerbTests` (the line first), `ModelArmsProgramTests` (the gate on the portable road: the line once, the mode judged, the witness compared, a swap the witness cannot see named as unverified). On this Linux host the non-Linux roads are reached by handing the Mac's `PlatformRoad` to the gate, to `init` and to the lock
through their seams (`PlatformKit.MacRoad`), and by calling the portable readers directly; the pure-list marker is split into `// E3.S0` … `// E3.S5`.

### Not proven (said, not hidden)

Any road COMPOSED on a real macOS or FreeBSD host — the units run here on Linux and the Mac's road is handed in through seams (**E3 §8 item 25**); `Process.StartTime` on macOS and FreeBSD; libc `geteuid` and `mkdir` on them (POSIX, no layout — not observed); `flock` on APFS, UFS and ZFS (the probe and the honour check observe it at the first run there — that IS the proof, when it runs);
that `File.GetUnixFileMode` reads the file the link judgement saw; `EACCES` on `Present` reading as absent off Linux; a thread left behind by the ceiling until its open returns; a swap that keeps the length and the write time. Linux x86-64 behaves as before, by test.

## The objective readers and the report (E3.S1)

## The target's tools and prep-base (E3.S2)

## make (E3.S3)

## status (E3.S4)

## checks, M2 and the key check (E3.S5)
