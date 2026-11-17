using Bench.Tests.Leftovers;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Infrastructure;

/// <summary>E6 S5, row E6-k — a test class that made a temp path removes it. Each class below used to leave one behind in the temp root of every run (a folder made with
/// <c>CreateTempSubdirectory</c> and never removed, a product workspace never disposed, a file written into a temp root's parent). The proof runs the class ALONE in a child of this
/// executable whose temp root is an owned folder: the child exits 0 and its temp root holds no entry afterwards.
/// <para>What this does NOT prove: a class that is not a row here (a new never-removed site in another class needs its row), and Windows' temp root (the child's <c>TEMP</c> and <c>TMP</c> are set as well, but no Windows leg runs it).</para></summary>
public sealed class TempLeftoverChildRunTests
{
    [Theory]
    [InlineData("DailyRunFileSinkTests")]
    [InlineData("FilesystemEngineTests")]
    [InlineData("QlnEngineTests")]
    [InlineData("TracePortTests")]
    [InlineData("FindingAssessorTests")]
    [InlineData("VettingPassTests")]
    public void A_class_that_made_temp_paths_leaves_its_temp_root_empty(string name)
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        if (name == "VettingPassTests")
        {
            DockerGate.Require(); // the class is Postgres-bound: it runs where a daemon answers, fails under GitHub Actions without one, skips elsewhere
        }

        using var temp = TempDir.For("leftover-run");
        var env = new Dictionary<string, string?> { ["TMPDIR"] = temp.Path, ["TEMP"] = temp.Path, ["TMP"] = temp.Path, ["TESTSTODELETE_RUN"] = null };

        var child = SuiteChild.Run(["--no-ansi", "--filter-class", "*." + name], env, TimeSpan.FromMinutes(5));

        child.TimedOut.Should().BeFalse();
        child.ExitCode.Should().Be(0, child.StdOut + child.StdErr);
        var left = Directory.EnumerateFileSystemEntries(temp.Path).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).ToList();
        left.Should().BeEmpty($"{name} removes every temp path it made (it left {left.Count}: {string.Join(", ", left.Take(4))})");
    }
}
