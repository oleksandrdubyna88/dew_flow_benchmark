using System.Text.Json.Nodes;
using FluentAssertions;
using ModelArms.Adapters;
using ModelArms.Core.Experiment;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary><c>chain</c> as its own PROCESS over the real detached launch (E5.S4, §8 item 45; Linux x86-64): the run's lock is a close-on-exec descriptor of the chain alone, so a chain killed beside its live runner frees the lock and the runner — reparented away from the chain by E5.S3's fork — neither holds it nor dies with the chain, and the next chain of the same run is not refused. The experiment is cut to <c>part6</c>'s one round
/// of <c>C1</c>, with no keep-awake (there is none to start on this host) and its first call hung, so the runner lives until the kit kills it.
/// <para>
/// What this does NOT prove: that the keep-awake of the chain is released when the chain is killed (the scope's own tests); Windows; a chain killed in the middle of a stage's write (the staged file a <c>-9</c> can leave is §8 item 54's).
/// </para></summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class ChainProcessTests
{
    private static readonly TimeSpan Within = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task A_killed_chain_frees_its_run_lock_while_its_detached_runner_lives_on()
    {
        await using var kit = await RunAllKit.MakeAsync("C1");
        ChainKit.Provide(kit.Root);
        Directory.CreateDirectory(Path.Combine(kit.Root.Root, "base"));
        kit.Root.Edit(top =>
        {
            top["awake"] = "none";
            top["runs"] = new JsonObject { ["part6"] = new JsonObject { ["rounds"] = new JsonArray(new JsonArray("C1")) } };
        });
        kit.Fake.Rewrite(ExperimentRoot.Script(new JsonObject { ["hangOnCall"] = 1 }));
        var lockPath = kit.Layout.ChainLock("part6");

        await using var first = ModelArmsBinary.Start(kit.Root, "chain", "part6");
        var runner = await kit.RunnerPidAsync("C1", Within);
        (await IsolationKit.UntilAsync(() => kit.Fake.Calls().Count > 0, Within)).Should().BeTrue("the runner's first step started the fake: " + kit.LogOf("C1"));
        File.Exists(lockPath).Should().BeTrue();
        await first.KillAsync();
        await Task.Delay(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);

        Directory.Exists($"/proc/{runner}").Should().BeTrue("the detached runner outlives the chain that started it");
        var real = Path.Combine(RealPath.Of(Path.GetDirectoryName(lockPath)!).Ok(), Path.GetFileName(lockPath)); // the kernel names a descriptor by its real path: a link in a parent folder (a /tmp that is one) must not hide a match
        foreach (var pid in kit.Fake.Calls().Select(call => call.Pid).Append(runner).Distinct())
        {
            DescriptorsOf(pid).Should().NotContain(real, $"pid {pid} holds no descriptor of chain/part6/.chain.lock: the lock's handle is close-on-exec, so the launch did not hand it on");
        }

        await using var second = ModelArmsBinary.Start(kit.Root, "chain", "part6");
        await second.LineAsync("resolved executables:", TimeSpan.FromSeconds(30));
        second.Printed().Should().NotContain("holds chain/part6/.chain.lock", "the killed chain's lock is free: this chain took it and went on to resolve its executables");
    }

    /// <summary>Where each open descriptor of <paramref name="pid"/> points, as the kernel says.</summary>
    private static IReadOnlyList<string> DescriptorsOf(int pid) =>
        [.. Directory.EnumerateFileSystemEntries($"/proc/{pid}/fd").Select(entry => new FileInfo(entry).LinkTarget).OfType<string>()];
}
