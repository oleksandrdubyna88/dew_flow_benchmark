using Bench.Tests.Probes.Driver;
using FluentAssertions;
using ModelArms.Core.Experiment;
using Xunit;
using ArmsProgram = ModelArms.Program;
using GateSeams = ModelArms.GateSeams;

namespace Bench.Tests.ModelArms;

/// <summary>The git floor of E4.S0 (D23), moved out of <c>ValidateVerbTests</c> (the file limit): at <c>validate</c>, on the portable road, and at EVERY admission asks git its version exactly once, from the experiment root, under the pins, and a refused git runs nothing else. One theory over six roads; each road builds its own fixture and replaces <c>&lt;temp&gt;/bin/git</c> by the recording shim (<see cref="ExperimentRoot.ReplaceGit"/> deletes the link first).
/// <para>
/// What this does NOT prove: Windows (the shim is a shell script); the exit code of an admitted road — each verb's own tests hold it; a vendor git carrying a version number without the fix (the floor reads a number).
/// </para></summary>
public sealed class GitFloorAdmissionTests
{
    private static void Posix()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "the fixture's executables, links and modes are POSIX");
    }

    private static async Task<(int Code, string Output, string Error)> ValidateAsync(ExperimentRoot root, string verb)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = await ArmsProgram.RunAsync([verb, "--experiment", root.Root], output, error, root.Environment, TestContext.Current.CancellationToken);
        return (code, output.ToString(), error.ToString());
    }

    /// <summary>The git floor at <c>validate</c> (D23, E4.S0): the source is asked only of a git whose version passes, and every refusal is still printed at once. The shim answers <c>--version</c> with the printed text and logs every other call — the marker file is the proof that no repository question was asked of a refused git. The shim REPLACES the fixture's link to the host's git
    /// (<see cref="ExperimentRoot.ReplaceGit"/> deletes the link first, so the operator's git is never written through).</summary>
    [Theory]
    [InlineData("2.44.0", 3)]
    [InlineData("2.44.1", 0)]
    [InlineData("2.45.0", 0)]
    [InlineData("2.39.5 (Apple Git-154)", 0)]
    [InlineData("2.39.3", 3)]
    [InlineData("nonsense", 3)]
    public async Task A_git_below_the_floor_is_refused_at_validate_and_at_run_arm_admission(string printed, int exit)
    {
        Posix();
        using var root = await new ExperimentRoot().WithTargetAsync();
        var git = root.ReplaceGit(Sentinel.LogsAllButVersion(root.MarkerFile("git"), printed));

        var (code, output, error) = await ValidateAsync(root, "validate");

        code.Should().Be(exit, output + error);
        if (exit == 0)
        {
            output.Should().Contain($"  git → {git}");
            File.ReadAllLines(root.MarkerFile("git")).Count(line => line.StartsWith("rev-parse", StringComparison.Ordinal)).Should().Be(4, "an admitted git is asked the source's four questions");
            return;
        }

        File.Exists(root.MarkerFile("git")).Should().BeFalse("no source question was asked of a refused git: the shim logs every call but --version");
        error.Should().Contain($"git at {git}: ", "the refusal names the git it is about, by its real path").And.Contain("the source was not checked: git's version ");
        if (printed == "nonsense")
        {
            error.Should().Contain("not a version this harness can read").And.Contain("nonsense");
            return;
        }

        error.Should().Contain(printed).And.Contain(GitFloor.Sentence).And.Contain("the source was not checked: git's version is refused");
    }

    /// <summary>The floor is a fact about an EXECUTABLE and takes no road: the gate runs on the Mac's seams (the portable road, handed in on this Linux host — a Mac's road composed on a real Mac is unobserved, E3 §8 item 25) and the floor still refuses. The road's own line comes FIRST: the positive that the portable road was the one taken.</summary>
    [Fact]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public async Task On_the_portable_road_the_floor_is_checked_too()
    {
        Posix();
        using var root = await new ExperimentRoot().WithTargetAsync();
        File.SetUnixFileMode(root.Root, (UnixFileMode)Convert.ToInt32("700", 8));
        foreach (var file in new[] { "experiment.json", "scenarios.json" })
        {
            File.SetUnixFileMode(Path.Combine(root.Root, file), (UnixFileMode)Convert.ToInt32("600", 8));
        }

        root.ReplaceGit(Sentinel.LogsAllButVersion(root.MarkerFile("git"), "2.44.0"));

        var (code, error) = await ValidateVerbTests.ThroughValidateAsync(root, PlatformKit.MacSeams());

        code.Should().Be(3, error);
        error.Should().Contain(GitFloor.Sentence);
        var lines = error.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Should().ContainSingle(line => line.Contains("NOT verified", StringComparison.Ordinal));
        lines[0].Should().Contain("NOT verified", "the road's line comes first: the portable road was taken");
    }

    public static TheoryData<string> Roads() => ["validate", "prep-base", "make", "run-arm", "checks", "checks --parse-only"];

    /// <summary>The consultant's check (E4.S0): every admission asks git exactly ONCE for its version, from the experiment root, under the pins — and a refused git runs NOTHING else: exactly one recorded block, not "no other call" (a count of zero and a count of two both fail). The same road with an admitted version: the first block is that call and the second is a repository question — the positive
    /// that an admitted version reaches the later operations. The exit code of the admitted road is each verb's own (their tests assert it); this asserts the sequence.</summary>
    [Theory]
    [MemberData(nameof(Roads))]
    public async Task Every_admission_asks_git_its_version_exactly_once_from_the_experiment_root_and_a_refused_git_runs_nothing_else(string road)
    {
        Posix();
        const string Pinned = "-c core.fsmonitor=false -c core.hooksPath= -c submodule.recurse=false";

        var refused = await AdmissionAsync(road, "2.44.0");

        refused.Code.Should().Be(3, $"{road}: git 2.44.0 is below the floor");
        var only = refused.Blocks.Should().ContainSingle($"{road}: exactly one git call for a refused git — the version").Subject;
        only.Call.Should().Be(Pinned + " --version");
        only.Cwd.Should().Be(refused.Root, $"{road}: asked from the experiment root");

        var admitted = await AdmissionAsync(road, "2.44.1");

        admitted.Blocks.Should().HaveCountGreaterThan(1, $"{road}: an admitted git reaches the later operations");
        admitted.Blocks.Count(block => block.Call.EndsWith("--version", StringComparison.Ordinal)).Should().Be(1, $"{road}: the version is asked exactly once in the whole recording, not only before the second call");
        admitted.Blocks[0].Call.Should().Be(Pinned + " --version");
        admitted.Blocks[0].Cwd.Should().Be(admitted.Root);
        admitted.Blocks[1].Call.Should().NotEndWith("--version", $"{road}: the version is asked once").And.Match(road is "validate" or "prep-base" ? "rev-parse*" : Pinned + "*", $"{road}: the second call is a repository question");
    }

    private sealed record Admission(int Code, string Root, IReadOnlyList<(string Call, string Cwd, IReadOnlyList<string> Env)> Blocks);

    private static IReadOnlyList<(string Call, string Cwd, IReadOnlyList<string> Env)> BlocksOf(string marker) => File.Exists(marker) ? Sentinel.Blocks(marker) : [];

    private static async Task<Admission> AdmissionAsync(string road, string printed)
    {
        switch (road)
        {
            case "validate" or "prep-base":
                using (var root = await new ExperimentRoot().WithTargetAsync())
                {
                    var marker = root.MarkerFile("git-calls");
                    root.ReplaceGit(Sentinel.Body(marker, printed));
                    var run = await ValidateAsync(root, road);
                    return new Admission(run.Code, root.Root, BlocksOf(marker));
                }

            case "make":
                using (var kit = await MakeKit.CreateAsync("C1"))
                {
                    var marker = kit.Root.MarkerFile("git-calls");
                    kit.Root.ReplaceGit(Sentinel.Body(marker, printed));
                    var run = await kit.MakeAsync();
                    return new Admission(run.Code, kit.Root.Root, BlocksOf(marker));
                }

            case "run-arm":
                using (var root = new ExperimentRoot())
                using (var fake = new FakeCli())
                {
                    await root.MakeArmAsync("C1", fake);
                    RunArmKit.Replay(fake, "result-success.jsonl");
                    var marker = root.MarkerFile("git-calls");
                    root.ReplaceGit(Sentinel.Body(marker, printed));
                    var run = await RunArmKit.RunAsync(root, "C1");
                    return new Admission(run.Code, root.Root, BlocksOf(marker));
                }

            default:
                using (var kit = await ChecksKit.CreateAsync())
                {
                    var marker = kit.Root.MarkerFile("git-calls");
                    kit.Root.ReplaceGit(Sentinel.Body(marker, printed));
                    var run = await kit.RunAsync(parseOnly: road.EndsWith("--parse-only", StringComparison.Ordinal));
                    return new Admission(run.Code, kit.Root.Root, BlocksOf(marker));
                }
        }
    }
}
