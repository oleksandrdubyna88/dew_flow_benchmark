Write xUnit tests for a C# repo. Style: C# 14 / .NET 10, xUnit v3 + FluentAssertions, file-scoped namespace, 4-space indent, Allman braces, target-typed `new`, collection expressions `[..]`, no comments beyond one short /// summary per test mentioning the scenario id. Output ONLY the C# members (no usings, no class wrapper) in one ```csharp block.

Context (class ConfinementPlannerTests, already has these members):
```csharp
private const string RootA = "D:/projects/alpha";
private const string RootB = "D:/projects/beta";
private static readonly string Scratch = ...; // the row's empty folder, a temp path
private static readonly string RowFolder = ...; // the row's own folder
private static readonly string RowTree = Path.Combine(RowFolder, "tree"); // the row's copy of the roots
private static Confinement.Planned Plan(string runtime, CapabilityGrant grant) => ...; // plans, asserts Planned
// builds a planned question launch: the working folder is Scratch for a none plan, RowTree for a disk plan; OwnedScratch = RowFolder
private static ConsultantLaunch Planned(string runtime, CapabilityGrant grant, string model = "") => ...;
```
Existing Vibe test to imitate:
```csharp
[Fact]
public void AVibeQuestionLaunch_IsThePlansPair_InItsScratchOrItsSnapshot_OnTheRowsOwnHome()
{
    var consultant = new Runners.Vibe.VibeConsultant(new Runners.Vibe.VibeRuntime("vibe", Runners.Platform.HostKind.Windows));
    var none = consultant.Build(Planned("vibe", CapabilityGrant.None, "glm-5-3"));
    none.Request.WorkingDirectory.Should().Be(Scratch);
    none.Request.Arguments.Should().NotContain("--add-dir").And.NotContain("--resume", "one shot");
    none.Role.Should().Be(ConsultantRoles.Question);
    var disk = consultant.Build(Planned("vibe", CapabilityGrant.Disk(RootA, RootB), "glm-5-3"));
    disk.Request.WorkingDirectory.Should().Be(RowTree);
    disk.Request.Arguments.Should().NotContain(RootA).And.NotContain(RootB);
    foreach (var request in (Runners.Processes.ProcessRequest[])[none.Request, disk.Request])
    {
        request.InheritsEnvironment.Should().BeFalse();
    }
}
```
A Kimi consultant is built in this file like this (a fake person's paths, so building reads nothing):
```csharp
private static Runners.Kimi.KimiPlaces PersonsPlaces(string root) => Runners.Kimi.KimiPlaces.ForShim(root, Path.Combine(RowFolder, "persons-bin"));

private static Runners.Kimi.KimiConsultant Kimi() => new(new Runners.Kimi.KimiRuntime("kimi", Runners.Platform.HostKind.Windows,
    new Runners.Kimi.KimiSeams(PersonsPlaces, () => ("coai-mcp", []))));
```
The shim argv has `--ask-acp --profile kimi --model <m> --cwd <folder> --home <folder>` and no `--session` for a one-shot row. `Runners.Kimi.KimiEnvironment.Refusal(ProcessRequest request, KimiPlaces places)` is a static method returning an empty string when the request is admitted; the places for a launch are `PersonsPlaces(<the value after "--home" in the argv>)`. A planned launch can be made to carry flags by `var plan = Plan("kimi", CapabilityGrant.None) with { Flags = ["--yolo"] };` and `Planned("kimi", CapabilityGrant.None) with { Confinement = new LaunchConfinement.Planned(plan) }`.

Write exactly these members, exact test names (the helper methods PersonsPlaces and Kimi above you may include as given):
1. `AKimiQuestionLaunch_IsTheShimRequest_InItsFolderOrItsCopy_OnTheRowsHome` ([Fact]): for Planned("kimi", None) and Planned("kimi", Disk(RootA, RootB)) built by Kimi().Build: none WorkingDirectory == Scratch, disk WorkingDirectory == RowTree; for both: argv contains "--ask-acp"; the value after "--home" starts with RowFolder + Path.DirectorySeparatorChar; argv does not contain "--session"; argv does not contain RootA or RootB; InheritsEnvironment is false; KimiEnvironment.Refusal(request, PersonsPlaces(home)) is empty; Role == ConsultantRoles.Question.
2. `APlanWithFlags_IsRefusedForKimi` ([Fact]): building a launch whose plan carries flags ["--yolo"] throws ArgumentException whose message contains "shim" (use `Action act = () => Kimi().Build(launch); act.Should().Throw<ArgumentException>().WithMessage("*shim*");`).
