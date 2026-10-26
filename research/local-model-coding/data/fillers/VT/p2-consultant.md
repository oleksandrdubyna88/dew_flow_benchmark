You write C# (.NET 10, latest C#, xUnit v3 + FluentAssertions). Output ONLY code blocks, each preceded by one line naming where it goes. No explanations.

## Existing production code: `KimiConsultant.Build` (file src_mcp/runners/Kimi/KimiConsultant.cs)
```csharp
    public ReviewerInvocation Build(ConsultantLaunch launch)
    {
        ConsultantLaunches.MustBeLaunchable(launch);
        var settings = launch.Settings with { OwnedScratch = OwnedFolder(launch), Model = ModelOf(launch.Settings) };
        var built = inner.Build(ConsultantLaunches.RoleOf(launch), launch.Prompt, Cwd(launch), string.Empty, launch.OutputDir, settings);
        var request = built.Request with { Arguments = [.. built.Request.Arguments, .. SessionOf(launch)] };
        ConsultantLaunches.MustCarryNoLineBreak(request.Arguments);

        return built with { Request = launch.Confinement is LaunchConfinement.Planned ? ConsultantLaunches.ForQuestion(request) : request };
    }
    private static string Cwd(ConsultantLaunch launch) =>
        launch.Confinement is LaunchConfinement.Planned planned ? ConsultantLaunches.Cwd(launch, planned.Plan) : SnapshotOf(launch);
```
`LaunchConfinement.Planned` is a record with a property `Plan` of type `Confinement.Planned`; that has `Flags` (IReadOnlyList<string>) and `Leading` (IReadOnlyList<string>), both lists of argv strings the plan adds.

## Task A — production: refuse a plan with flags
Spec: a plan that carries ANY flag (Flags or Leading non-empty) is refused with `ArgumentException` whose message is: "a Kimi row's confinement is the shim's; a plan with flags is not one coai made for kimi" (param name `nameof(launch)`). Do it with a new `private static void MustCarryNoFlags(ConsultantLaunch launch)` in the style of the existing private helpers (switch/pattern, cyclomatic complexity <= 4, no nested ifs), called as the second line of `Build`, right after `ConsultantLaunches.MustBeLaunchable(launch);`. Give me the new method and the exact new line.

## Task B — tests, a new class `KimiQuestionLaunchTests` (own file)
This is the style of existing test helpers (from ConfinementPlannerTests):
```csharp
    private static readonly string RowFolder = Path.Combine(Path.GetTempPath(), "coai-question-" + Guid.NewGuid().ToString("N"));
    private static readonly string RowTree = Path.Combine(RowFolder, "tree");

    private static ConsultantLaunch Planned(string runtime, CapabilityGrant grant, string model = "") => new(
        Repo, "the question", string.Empty, Answers, new ReviewerSettings(runtime) { Model = model, OwnedScratch = RowFolder })
    {
        Confinement = new LaunchConfinement.Planned(Plan(runtime, grant)),
        Workdir = RowTree,
    };
```
(`Repo` and `Answers` are strings; `Plan(runtime, grant)` = `ConfinementPlanner.Plan(runtime, grant).Should().BeOfType<Confinement.Planned>().Which`. For a none plan, `ConsultantLaunches.Cwd(launch, plan)` returns the folder in `launch.Scratch` when the plan's Cwd is Scratch, and `launch.Workdir` when Snapshot — so the helper must set BOTH `Scratch = RowCwd` (an empty folder `RowCwd = Path.Combine(RowFolder, "cwd")`) and `Workdir = RowTree`. `Scratch` is a settable init property of ConsultantLaunch, like `Workdir`.)
A Kimi consultant is built like this (a fake person's paths, so building reads nothing real):
```csharp
    private static KimiConsultant Consultant() => new(new KimiRuntime("kimi", HostKind.Windows,
        new KimiSeams(root => KimiPlaces.ForShim(root, Path.Combine(RowFolder, "persons-bin")), () => ("coai-mcp", []))));
```
`Build(launch)` returns `ReviewerInvocation` with `.Request` (`WorkingDirectory`, `Arguments` list, `InheritsEnvironment` bool). The shim's home argument is the value after `AcpWords.HomeFlag`. Namespaces: CoaiMcp.Core.QuestionConsult, CoaiMcp.Runners.Acp, CoaiMcp.Runners.Consultation, CoaiMcp.Runners.Kimi, CoaiMcp.Runners.Platform, CoaiMcp.Runners.Reviewers; file-scoped namespace `CoaiMcp.Tests`.
Write the whole class (usings, summary, helpers) with two tests:
1. `[Fact] AKimiQuestionLaunch_IsTheShimRequest_InItsFolderOrItsCopy_OnTheRowsHome`: for `Planned("kimi", CapabilityGrant.None)` the request's WorkingDirectory is `RowCwd`; for `Planned("kimi", CapabilityGrant.Disk(RootA, RootB))` it is `RowTree`; both: the `--home` value's parent directory is `RowFolder`; no `--session` argument; no argument equals or contains RootA or RootB; `InheritsEnvironment` is false.
2. `[Fact] APlanWithFlags_IsRefusedForKimi`: a launch whose plan is the real none plan but with `Flags = ["--yolo"]` (use `plan with { Flags = ["--yolo"] }`) -> `Build` throws `ArgumentException` whose message contains "shim". Also a plan `with { Leading = ["--search"] }` throws.
RootA = "D:/projects/alpha", RootB = "D:/projects/beta". The test host may be Linux; do not use Windows-only APIs.
