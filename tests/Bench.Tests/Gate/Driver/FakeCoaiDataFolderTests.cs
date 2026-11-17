using System.Text.Json.Nodes;
using Bench.Infrastructure.Gate;
using Bench.Infrastructure.Process;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Gate.Driver;

/// <summary>E6 S5 (D6) — where the fake coai keeps its data. The driver names the data folder inside its own owned root (<c>COAI_DATA_DIR</c>), and the fake
/// has no folder of its own to fall back on: with the variable unset or empty it refuses, but only where it first needs the folder — the version answer
/// and <c>--probe-api</c> never do. Every launch here runs under a temp root the test owns, so a regression writes inside a folder the test removes.</summary>
public sealed class FakeCoaiDataFolderTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Dictionary<string, string> Temp(string folder) => new() { ["TMPDIR"] = folder, ["TEMP"] = folder, ["TMP"] = folder };

    [Fact]
    public async Task A_review_through_the_driver_writes_its_data_inside_the_drivers_root()
    {
        using var fake = new FakeCoai();
        using var extra = TempDir.For("coai-extra-temp");

        await using (var session = (await fake.OpenAsync(Temp(extra.Path))).Ok())
        {
            (await session.CallToolAsync("review_plan", new JsonObject { ["repoPath"] = "r", ["branch"] = "b" }, TimeSpan.FromSeconds(30), Ct)).Ok();
        }

        Directory.Exists(Path.Combine(fake.Root, "data", "sessions")).Should().BeTrue("the driver's COAI_DATA_DIR is a folder inside its owned root");
        Directory.EnumerateFileSystemEntries(extra.Path).Should().BeEmpty("nothing the fake made lies in the temp root it was launched under");
    }

    [Fact]
    public async Task The_fake_coai_with_no_data_folder_refuses_where_it_first_needs_one()
    {
        using var fake = new FakeCoai();
        using var extra = TempDir.For("coai-extra-temp");
        var launch = fake.Launch(extra: new Dictionary<string, string>(Temp(extra.Path)) { ["COAI_DATA_DIR"] = string.Empty });

        await using (var session = (await new McpStdioSessionFactory().OpenAsync(launch, Ct)).Ok())
        {
            var refused = await session.CallToolAsync("review_plan", new JsonObject { ["repoPath"] = "r", ["branch"] = "b" }, TimeSpan.FromSeconds(30), Ct);

            refused.Reason().Should().NotBeEmpty("the review fails: the fake exited where it first needed its data folder");
        }

        (await File.ReadAllTextAsync(launch.StderrPath, Ct)).Should().Contain("COAI_DATA_DIR is unset");
        var env = fake.Environment(new Dictionary<string, string>(Temp(extra.Path)) { ["COAI_DATA_DIR"] = string.Empty });
        var version = (await ProcessRunner.RunAsync(FakeCoai.Executable, ["--version"], fake.Root, TimeSpan.FromSeconds(60), string.Empty, env, Ct))
            .Should().BeOfType<ProcessAttempt.Completed>().Subject.Result;
        var probe = (await ProcessRunner.RunAsync(
                FakeCoai.Executable, ["--probe-api", "--vendor", "grok", "--model", "grok-4.7", "--endpoint", "https://api.x.ai/v1", "--dialect", "xai", "--timeout-seconds", "60"],
                fake.Root, TimeSpan.FromSeconds(60), string.Empty, env, Ct))
            .Should().BeOfType<ProcessAttempt.Completed>().Subject.Result;

        version.ExitCode.Should().Be(0);
        version.StandardOutput.Should().Contain("connect-other-ais");
        probe.ExitCode.Should().Be(0, "the probe never needs the data folder");
        Directory.EnumerateFileSystemEntries(extra.Path).Should().BeEmpty();
        Directory.Exists(Path.Combine(fake.Root, "data")).Should().BeFalse();
    }
}
