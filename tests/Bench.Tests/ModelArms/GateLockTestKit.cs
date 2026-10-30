using FluentAssertions;
using ModelArms.Adapters;
using Xunit;
using ModelArmsApp = ModelArms;

namespace Bench.Tests.ModelArms;

/// <summary>What the gate lock's test classes share: the lock under test, the verb run in process, and the files of a root a lock leaves.</summary>
internal static class GateLockTestKit
{
    public static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(100);

    public static readonly TimeSpan Fast = TimeSpan.FromSeconds(30);

    public static string LockOf(ExperimentRoot root) => Path.Combine(root.Root, ".gate.lock");

    public static string HolderOf(ExperimentRoot root) => LockOf(root) + ".holder";

    public static FileGateLock Lock() => new(TimeProvider.System, Poll);

    /// <summary>The verb run in this process through the real parser, with a ten-second ceiling of its own: a verb that was meant to refuse and holds instead ends as a failed assertion and not as a hung run.</summary>
    public static async Task<(int Code, string Output, string Error)> RunAsync(ExperimentRoot root, params string[] args)
    {
        using var ceiling = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        ceiling.CancelAfter(TimeSpan.FromSeconds(10));
        var (output, error) = (new StringWriter(), new StringWriter());
        try
        {
            var code = await ModelArmsApp.Program.RunAsync([.. args, "--experiment", root.Root], output, error, root.Environment, ceiling.Token);
            return (code, output.ToString().Trim(), error.ToString().Trim());
        }
        catch (OperationCanceledException)
        {
            Assert.Fail($"the verb was still running after 10 s (it printed: {output.ToString().Trim()})");
            throw;
        }
    }

    /// <summary>The status line of a root, asked of the console as another process.</summary>
    public static async Task<string> StatusAsync(ExperimentRoot root)
    {
        var (code, output, error) = await ModelArmsBinary.RunAsync(root, "gate-lock", "status");
        code.Should().Be(0, error);
        return output.Trim();
    }

    /// <summary>The names of the files in the root's own folder (not its subfolders), so a test can say what a lock left there.</summary>
    public static IReadOnlyList<string> RootFiles(ExperimentRoot root) =>
        [.. Directory.EnumerateFileSystemEntries(root.Root).Select(Path.GetFileName).Cast<string>().Order(StringComparer.Ordinal)];
}
