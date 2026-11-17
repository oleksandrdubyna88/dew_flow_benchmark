using System.Diagnostics;
using System.Globalization;
using System.Text.Json.Nodes;
using Bench.Tests.Leftovers;
using FluentAssertions;
using ModelArms.Adapters;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>An attended pilot's scaffold killed right after its first child exists leaves what its own teardown needs: ONE manifest <c>bench-mN-manifest-&lt;id&gt;.json</c> naming EVERY path the scaffold means to make, written before the first of them, and nothing but those paths beside it
/// (row E6-l of <c>todo/PLAN_test_leftovers_E6.md</c>). Each scaffold runs as a child of this executable with <c>BENCH_PILOT_OUT</c> a folder of this test's and <c>TMPDIR</c> another, so that nothing of the child, not even the <c>clr-debug-pipe-*</c> FIFOs a killed .NET process leaves in its temp root, lands in the machine's temp folder.
/// <para>
/// What this does NOT prove: a kill at any other moment than right after the first child; that the pilot's teardown removes a killed scaffold's output (the operator's step); Windows (skipped: SIGKILL).
/// </para></summary>
public sealed class PilotScaffoldKillTests
{
    private static readonly TimeSpan Ceiling = TimeSpan.FromSeconds(120);

    [Fact]
    public async Task The_M2_scaffold_killed_after_its_first_child_leaves_a_manifest_naming_every_child()
    {
        await KilledAfterItsFirstChildAsync("m2", "Writes_the_scratch_target_and_root_for_the_pilot", ["target", "root"]);
    }

    [Fact]
    public async Task The_M3_scaffold_killed_after_its_first_child_leaves_a_manifest_naming_every_child()
    {
        await KilledAfterItsFirstChildAsync("m3", "Writes_the_scratch_target_root_home_and_claude_for_the_pilot", M3PilotScaffold.Names);
    }

    private static async Task KilledAfterItsFirstChildAsync(string pilot, string method, IReadOnlyList<string> names)
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("the proof kills the child with SIGKILL: there is no such signal on Windows");
            return;
        }

        using var childTemp = new TempFolder();
        using var pilotOut = new TempFolder();
        var env = new Dictionary<string, string?> { ["TMPDIR"] = childTemp.Path, [PilotOut.Variable] = pilotOut.Path, ["TESTSTODELETE_RUN"] = null };
        using var child = Process.Start(SuiteChild.StartInfoFor(System.Environment.ProcessPath!, ["--no-ansi", "--explicit", "only", "--filter-method", "*" + method], env))!;
        var drained = Task.WhenAll(child.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken), child.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken));
        try
        {
            await WaitForFirstChildAsync(pilotOut.Path, $"bench-{pilot}-target-*", child, drained);

            child.HasExited.Should().BeFalse("the child must still be running when its first child appears: a scaffold that ended on its own was not interrupted, and a green on it proves nothing");
            child.Kill(entireProcessTree: true);
            await child.WaitForExitAsync(TestContext.Current.CancellationToken);
            child.ExitCode.Should().Be(137, "SIGKILL: the scaffold had no chance to clean anything");

            var manifest = Directory.GetFiles(pilotOut.Path, $"bench-{pilot}-manifest-*.json").Should().ContainSingle("exactly one manifest, claimed before anything else was made").Subject;
            var tree = JsonNode.Parse(await File.ReadAllTextAsync(manifest, TestContext.Current.CancellationToken))!;
            var id = tree["id"]!.GetValue<string>();
            manifest.Should().Be(Path.Combine(pilotOut.Path, $"bench-{pilot}-manifest-{id}.json"));
            tree["uid"]!.GetValue<string>().Should().Be(RootFacts.CurrentUid().ToString(CultureInfo.InvariantCulture), "the manifest names the user who made it");
            var listed = tree["paths"]!.AsArray().Select(entry => entry!["path"]!.GetValue<string>()).ToList();
            var intended = names.Select(name => Path.Combine(pilotOut.Path, $"bench-{pilot}-{name}-{id}")).ToList();

            listed.Should().BeEquivalentTo([.. intended, manifest], "the manifest names EVERY path the scaffold means to make, and itself, under the operator's folder and with its id");
            Directory.GetFileSystemEntries(pilotOut.Path).Where(entry => entry != manifest).Should().OnlyContain(entry => intended.Contains(entry), "nothing but the manifest's own paths lies beside it");
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

    private static async Task WaitForFirstChildAsync(string folder, string pattern, Process child, Task<string[]> drained)
    {
        var until = DateTime.UtcNow + Ceiling;
        while (DateTime.UtcNow < until)
        {
            if (Directory.GetFileSystemEntries(folder, pattern).Length > 0)
            {
                return;
            }

            if (child.HasExited)
            {
                var said = drained.IsCompletedSuccessfully ? string.Join(string.Empty, drained.Result).Trim() : string.Empty;
                Assert.Fail($"the child ended (exit {child.ExitCode}) before its first output '{pattern}' appeared in BENCH_PILOT_OUT {folder}; what it said: {said}");
            }

            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.Fail($"no '{pattern}' appeared in BENCH_PILOT_OUT {folder} within {Ceiling.TotalSeconds:0} s");
    }
}
