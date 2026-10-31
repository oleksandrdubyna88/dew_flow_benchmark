using System.ComponentModel;
using System.Diagnostics;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary><see cref="ShimFiles"/>: a script this suite writes and starts at once always starts, even while other threads of the process fork — the condition under which a plainly written one fails with <i>Text file busy</i> (<c>ETXTBSY</c>), which made the recording git shim of
/// <c>ArmGitWideningTests</c> sometimes never run. Six threads fork <c>/bin/true</c> without pause while four write and start shims; the count of starts that failed must be zero, and every shim must have run its own body.
/// <para>
/// What this does NOT prove: that no other writer in the suite starts a script it has just written (the ones the ModelArms tests have are routed through <see cref="ShimFiles"/>; a new one is a new risk), or anything on Windows (shell scripts).
/// </para></summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class ShimFilesTests
{
    [Fact]
    public async Task A_shim_written_while_other_threads_fork_always_starts_and_runs_its_own_body()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "the shims are shell scripts");
        using var temp = new TempFolder();
        var stop = false;
        var forkers = Enumerable.Range(0, 6).Select(_ => Task.Run(() =>
        {
            while (!Volatile.Read(ref stop))
            {
                using var other = Process.Start(new ProcessStartInfo("/bin/true") { UseShellExecute = false })!;
                other.WaitForExit();
            }
        }, TestContext.Current.CancellationToken)).ToList();
        var failures = new System.Collections.Concurrent.ConcurrentBag<string>();
        var writers = Enumerable.Range(0, 4).Select(writer => Task.Run(() =>
        {
            for (var i = 0; i < 60; i++)
            {
                var ran = temp.Combine($"ran-{writer}-{i}");
                var shim = ShimFiles.Write(temp.Combine("bin", $"shim-{writer}-{i}"), $"#!/bin/sh\necho ran > '{ran}'\n");
                try
                {
                    using var started = Process.Start(new ProcessStartInfo(shim) { UseShellExecute = false })!;
                    started.WaitForExit();
                    if (!File.Exists(ran))
                    {
                        failures.Add($"{shim} started but did not run its body");
                    }
                }
                catch (Win32Exception ex)
                {
                    failures.Add(ex.Message);
                }
            }
        }, TestContext.Current.CancellationToken)).ToList();

        await Task.WhenAll(writers);
        Volatile.Write(ref stop, true);
        await Task.WhenAll(forkers);

        failures.Should().BeEmpty("a shim handed on by ShimFiles has been started once already, so no process still holds it for writing");
    }

    [Fact]
    public void A_probe_runs_nothing_of_the_shims_own_body()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "the shims are shell scripts");
        using var temp = new TempFolder();
        var ran = temp.Combine("ran");

        ShimFiles.Write(temp.Combine("shim"), $"#!/bin/sh\necho ran >> '{ran}'\n");

        File.Exists(ran).Should().BeFalse("the start that proves the file startable exits at its guard line");
    }
}
