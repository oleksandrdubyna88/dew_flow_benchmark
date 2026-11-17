using System.Diagnostics;
using Bench.Tests.Leftovers;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>The scratch folder every real-link and real-mode test uses must always go: a test run removes what it created (common.test-leftovers),
/// including a folder holding a link to its own parent and a folder closed to its owner.
/// <para>
/// What this does NOT prove: the same on Windows, where this shape cannot be made without a privilege.
/// </para></summary>
public sealed class TempFolderTests
{
    [Fact]
    public void Dispose_removes_a_folder_holding_a_link_to_its_own_parent_and_a_folder_closed_to_its_owner()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("links and modes are POSIX; the probe shape is measured on Linux");
            return;
        }

        for (var round = 0; round < 10; round++)
        {
            var temp = new TempFolder();
            var path = temp.Path;
            var probe = temp.Folder("probe");
            Directory.CreateSymbolicLink(System.IO.Path.Combine(probe, "probe-link"), probe);
            temp.File("x", "low", "inside.txt");
            File.SetUnixFileMode(temp.Combine("low"), UnixFileMode.UserRead | UnixFileMode.UserExecute);

            var dispose = temp.Dispose;

            dispose.Should().NotThrow($"round {round}: a link to the folder's own parent must not stop the walk");
            Directory.Exists(path).Should().BeFalse($"round {round}: the folder must be gone");
        }
    }

    [Fact]
    public void A_TempFolder_is_an_owned_folder_claimed_beside_it_and_removed_with_its_claim()
    {
        var temp = new TempFolder();
        var path = temp.Path;
        var claim = path + ".owner";
        try
        {
            var leaf = System.IO.Path.GetFileName(path);
            Owned.FullName().IsMatch(leaf).Should().BeTrue($"{leaf} is a name of the owner form");
            leaf.Should().Contain("-modelarms-");
            File.Exists(claim).Should().BeTrue("the claim lies beside the folder, written before it");
            Owner.Parse(File.ReadAllText(claim)).Should().Be(Owner.RunRecord());
            if (OperatingSystem.IsLinux())
            {
                File.GetUnixFileMode(path).Should().Be(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
        }
        finally
        {
            temp.Dispose();
        }

        Directory.Exists(path).Should().BeFalse("the folder is removed by its exact path");
        File.Exists(claim).Should().BeFalse("and its claim with it");
    }

    [Fact]
    public void Dispose_never_throws_when_the_walk_fails_and_what_stays_keeps_its_claim()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("modes are POSIX; the closed folder is made on Linux");
            return;
        }

        var warnings = new List<string>();
        var walks = 0;
        var temp = new TempFolder(
            _ =>
            {
                if (walks++ == 0)
                {
                    throw new IOException("planted walk failure");
                }
            },
            warnings.Add);
        var path = temp.Path;
        var claim = path + ".owner";
        var low = temp.Combine("low");
        temp.File("x", "low", "inside.txt");
        File.SetUnixFileMode(low, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        try
        {
            var dispose = temp.Dispose;

            dispose.Should().NotThrow("a failed mode walk is one warning, never an exception out of Dispose");
            warnings.Count(warning => warning.Contains("planted walk failure", StringComparison.Ordinal) && warning.Contains(path, StringComparison.Ordinal)).Should().Be(1);
            warnings.Should().Contain(warning => warning.Contains("could not remove", StringComparison.Ordinal), "the owned removal could not enter the closed subfolder and said so");
            Directory.Exists(path).Should().BeTrue("the removal could not enter the closed subfolder");
            File.Exists(claim).Should().BeTrue("what stays keeps its claim for the end-of-run audit");
        }
        finally
        {
            File.SetUnixFileMode(low, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            temp.Dispose();
        }

        Directory.Exists(path).Should().BeFalse("the retry removes the folder once the subfolder is open");
        File.Exists(claim).Should().BeFalse();
    }

    [Fact(Timeout = 180_000)]
    public async Task A_TempFolder_of_a_killed_test_process_is_collected_by_the_next_sweep()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("the proof kills the child with SIGKILL: there is no such signal on Windows");
            return;
        }

        using var report = new TempFolder();
        using var childTemp = new TempFolder();
        var env = new Dictionary<string, string?> { [KillChildOut] = report.Combine("child.txt"), ["TMPDIR"] = childTemp.Path, ["TESTSTODELETE_RUN"] = null };
        using var child = Process.Start(SuiteChild.StartInfoFor(System.Environment.ProcessPath!, ["--no-ansi", "--filter-method", "*KillChild_makes_a_TempFolder_and_waits_to_be_killed"], env))!;
        var drained = Task.WhenAll(child.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken), child.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken));
        try
        {
            var folder = await WaitForChildFolderAsync(report.Combine("child.txt"), child);
            var claim = folder + ".owner";
            Directory.Exists(folder).Should().BeTrue("the child's folder exists before the kill");
            File.Exists(claim).Should().BeTrue("and so does its claim: a folder with no claim is a legacy name the sweep never removes");

            child.Kill();
            await child.WaitForExitAsync(TestContext.Current.CancellationToken);
            child.ExitCode.Should().Be(137, "SIGKILL: the child had no chance to dispose anything");

            var result = LeftoversSweep.Run(new SweepOptions(childTemp.Path, DateTimeOffset.UtcNow, TimeSpan.Zero, Liveness.ProbeProcess, Mine: path => (path == folder || path == claim) && Scope.IsMine(path)));

            result.Removed.Should().Equal([folder], "the sweep removed the dead child's folder, and nothing else (the report lists a claim only when it is an orphan's)");
            Directory.Exists(folder).Should().BeFalse();
            File.Exists(claim).Should().BeFalse();
        }
        finally
        {
            if (!child.HasExited)
            {
                child.Kill(entireProcessTree: true);
            }

            try
            {
                await drained.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            }
            catch (TimeoutException)
            {
                // The killed child's pipes are not read to their end; nothing depends on them.
            }
        }
    }

    /// <summary>The child of the kill proof: makes a <see cref="TempFolder"/>, reports its path (a temp file, then a move) and waits to be killed. Returns at once when the variable is unset, so every normal run is green.</summary>
    [Fact(Timeout = 180_000)]
    public async Task KillChild_makes_a_TempFolder_and_waits_to_be_killed()
    {
        var report = System.Environment.GetEnvironmentVariable(KillChildOut);
        if (string.IsNullOrEmpty(report))
        {
            return;
        }

        using var temp = new TempFolder();
        File.WriteAllText(report + ".part", temp.Path + "\n");
        File.Move(report + ".part", report);
        await Task.Delay(TimeSpan.FromMinutes(2), TestContext.Current.CancellationToken);
    }

    private const string KillChildOut = "BENCH_E6S4_KILL_CHILD_OUT";

    internal static async Task<string> WaitForChildFolderAsync(string report, Process child)
    {
        var until = DateTime.UtcNow + TimeSpan.FromSeconds(120);
        while (DateTime.UtcNow < until)
        {
            if (File.Exists(report))
            {
                return (await File.ReadAllTextAsync(report, TestContext.Current.CancellationToken)).Trim();
            }

            child.HasExited.Should().BeFalse("the child must still be waiting when its folder is reported");
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException("the child did not report its folder within 120 s");
    }
}
