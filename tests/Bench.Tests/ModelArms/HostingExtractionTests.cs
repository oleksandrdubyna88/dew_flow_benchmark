using Bench.Cli;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>The CLI hosts' shared plumbing — the flag parser, the exit-code contract and the root
/// cancellation token — lives in <c>Bench.Hosting</c>, because two hosts need it and an exe may not
/// reference an exe. The namespace stays <c>Bench.Cli</c> so no <c>using</c> in the CLI changed.
/// <para>
/// What this does NOT prove: that the moved code behaves as it did. <c>CommandLineTests</c> and
/// <c>ShutdownSignalTests</c> prove that, and they are deliberately unedited by the move.
/// </para></summary>
public sealed class HostingExtractionTests
{
    private static readonly string[] MovedFiles = ["CommandLine.cs", "ExitCodes.cs", "ShutdownSignal.cs"];

    [Fact]
    public void The_cli_parser_and_exit_codes_live_in_Bench_Hosting_under_their_old_namespace()
    {
        Type[] moved = [typeof(CommandLine), typeof(CommandLineException), typeof(ExitCodes), typeof(ShutdownSignal)];

        foreach (var type in moved)
        {
            type.Assembly.GetName().Name.Should().Be("Bench.Hosting", $"{type.Name} must be shared by the CLI hosts");
            type.Namespace.Should().Be("Bench.Cli", $"{type.Name} keeps its namespace so no using changed");
        }
    }

    [Fact]
    public void The_cli_host_no_longer_carries_its_own_copy()
    {
        var root = Cli.Repository.Root;

        foreach (var file in MovedFiles)
        {
            File.Exists(Path.Combine(root, "hosts", "Cli", file)).Should().BeFalse($"hosts/Cli/{file} moved to src/Bench.Hosting");
            File.Exists(Path.Combine(root, "src", "Bench.Hosting", file)).Should().BeTrue($"src/Bench.Hosting/{file} is where it lives now");
        }
    }
}
