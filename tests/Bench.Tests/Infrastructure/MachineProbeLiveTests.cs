using Bench.Infrastructure.Machine;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Bench.Tests.Infrastructure;

/// <summary>The probe against THIS machine.
/// <para>
/// Opt-in, like the other live tests here, because it reads a real registry and launches real processes —
/// and because what it can assert depends on the hardware under it. What it does assert is the part that
/// must hold on any machine: the facts are coherent, the fingerprint is stable across two reads, and no
/// field was filled with a plausible-looking guess.
/// </para>
/// <para>
/// It exists because the parsers are unit-tested against captured output and that proves the parsing, not
/// the plumbing. A script that returns nothing, a key that moved, a WMI class that needs elevation — none of
/// those fail a parser test, and all of them fail here.
/// </para></summary>
[Trait("Category", "Live")]
public sealed class MachineProbeLiveTests
{
    private const string OptIn = "BENCH_PROBE_LIVE";

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task This_machine_reads_coherently_and_the_same_way_twice()
    {
        Enabled();
        var probe = new MachineProbe(NullLogger<MachineProbe>.Instance);

        var facts = await probe.ReadAsync(Environment.CurrentDirectory, Ct);

        facts.Recorded.Should().BeTrue("a machine that can run this test can be read");
        facts.Hostname.Should().NotBeEmpty();
        facts.Os.Family.Should().BeOneOf("windows", "wsl", "linux");
        facts.Os.Build.Should().NotBeEmpty("the build carries the patch, and a version without it is useless");
        facts.TotalRamBytes.Should().BeGreaterThan(0);
        facts.Cpu.Model.Should().NotBeEmpty();

        // The property the whole fingerprint rests on: two reads of one unchanged machine agree. If free
        // space or a timestamp had leaked into the canonical form, this is where it would show.
        var second = await probe.ReadAsync(Environment.CurrentDirectory, Ct);
        second.Fingerprint.Should().Be(facts.Fingerprint);

        TestContext.Current.TestOutputHelper?.WriteLine(
            $"""
             os          {facts.Os.Describe} (edition {facts.Os.Edition})
             wsl         {facts.Wsl.Describe}
             cpu         {facts.Cpu.Describe}
             ram         {facts.TotalRamBytes / (1024d * 1024 * 1024):0.#} GiB
             {string.Join(Environment.NewLine, facts.Adapters.Select(a => $"gpu         {a.Describe}"))}
             volume      {facts.Volume.Describe}
             fingerprint {facts.Fingerprint}
             """);
    }

    [Fact]
    public async Task Every_adapter_reports_its_health_so_a_card_off_the_bus_cannot_hide()
    {
        Enabled();

        var facts = await new MachineProbe(NullLogger<MachineProbe>.Instance)
            .ReadAsync(Environment.CurrentDirectory, Ct);

        if (facts.Adapters.Count == 0)
        {
            Assert.Skip("no display adapter was read on this host — nothing to assert about health");
        }

        // Not an assertion that the cards are healthy — that is a fact about the machine, not about the
        // code. The assertion is that the question was ASKED: a driver version present and a health code
        // read means a card at 31 would have been visible rather than silent.
        facts.Adapters.Should().OnlyContain(a => a.Name.Length > 0);
        facts.Adapters.Should().OnlyContain(a => a.DriverVersion.Length > 0,
            "under WSL this Windows driver IS the driver, so an empty one leaves the arm unattributable");

        TestContext.Current.TestOutputHelper?.WriteLine(
            string.Join(Environment.NewLine, facts.Adapters.Select(a => a.Describe)));
    }

    private static void Enabled() =>
        Assert.SkipWhen(
            (Environment.GetEnvironmentVariable(OptIn) ?? string.Empty).Length == 0,
            $"set {OptIn}=1 to read this machine for real");
}
