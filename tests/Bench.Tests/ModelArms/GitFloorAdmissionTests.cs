using Bench.Tests.Probes.Driver;
using FluentAssertions;
using Xunit;
using ArmsProgram = ModelArms.Program;

namespace Bench.Tests.ModelArms;

/// <summary>The consultant's check of E4.S0 (D23), moved out of <c>ValidateVerbTests</c> (the file limit): the git floor at EVERY admission asks git its version exactly once, from the experiment root, under the pins, and a refused git runs nothing else. One theory over six roads; each road builds its own fixture and replaces <c>&lt;temp&gt;/bin/git</c> by the recording shim (<see cref="ExperimentRoot.ReplaceGit"/> deletes the link first).
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
