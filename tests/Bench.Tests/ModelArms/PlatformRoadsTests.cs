using FluentAssertions;
using ModelArms.Adapters;
using ModelArms.Core.Experiment;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>C18: one pure table says, per host, which road each fact source takes and which checks the host did NOT make. macOS and FreeBSD run under the rule "a check the host can perform is performed; a check it cannot is named in one printed line;
/// nothing is refused for want of a reader" (the parent's ledger 2.1). Every host here is a <see cref="PlatformFacts"/> handed in, so the Mac's table row is tested on this Linux host.
/// <para>
/// What this does NOT prove: that any road composes on a real macOS or FreeBSD host (E3 §8 item 25) — this proves the table, not the host.
/// </para></summary>
public sealed class PlatformRoadsTests
{
    private static readonly (string Row, PlatformFacts Host, TrustRoad Trust, BoundedRoad Bounded, HolderRoad Holder, bool LibcMkdir, bool SaysSomething)[] Table =
    [
        ("Linux x64 statx", new(false, true, false, false, true, true, "Linux 6.18"), TrustRoad.Kernel, BoundedRoad.Kernel, HolderRoad.ProcTicks, true, false),
        ("Linux arm64 statx", new(false, true, false, false, false, true, "Linux 6.1 arm64"), TrustRoad.Kernel, BoundedRoad.Portable, HolderRoad.ProcTicks, true, true),
        ("Linux x64 no statx", new(false, true, false, false, true, false, "Linux 3.10"), TrustRoad.Portable, BoundedRoad.Portable, HolderRoad.ProcTicks, true, true),
        ("macOS arm64", new(false, false, true, false, false, false, "macOS 15"), TrustRoad.Portable, BoundedRoad.Portable, HolderRoad.ProcessStartTime, true, true),
        ("FreeBSD x64", new(false, false, false, true, true, false, "FreeBSD 14"), TrustRoad.Portable, BoundedRoad.Portable, HolderRoad.ProcessStartTime, true, true),
        ("Windows", new(true, false, false, false, true, false, "Windows 11"), TrustRoad.NotPerformed, BoundedRoad.Portable, HolderRoad.None, false, false),
    ];

    public static TheoryData<string> Rows => [.. Table.Select(row => row.Row)];

    [Theory]
    [MemberData(nameof(Rows))]
    public void Each_host_takes_its_declared_roads_and_names_what_it_does_not_verify(string name)
    {
        var (row, host, trust, bounded, holder, libcMkdir, saysSomething) = Table.Single(candidate => candidate.Row == name);
        var road = PlatformRoads.Of(host);

        road.Trust.Should().Be(trust, row);
        road.Bounded.Should().Be(bounded, row);
        road.Holder.Should().Be(holder, row);
        road.LibcMkdir.Should().Be(libcMkdir, row);
        (road.Line.Length > 0).Should().Be(saysSomething, $"{row}: the line is empty exactly for Linux x86-64 and Windows");
        (road.NotVerified.Count > 0).Should().Be(saysSomething, row);
    }

    [Fact]
    public void The_macs_line_names_the_owner_the_file_kind_the_inode_the_ceiling_the_swap_the_tick_and_the_host()
    {
        var line = PlatformRoads.Of(new(false, false, true, false, false, false, "macOS 15")).Line;

        line.Should().StartWith("trust check on macOS 15: NOT verified — ").And.EndWith("(D17; the roads: E3.S0)");
        foreach (var named in new[] { "the owner", "regular file", "inode and change time", "5 s ceiling", "swapped in", "2 s", "held file" })
        {
            line.Should().Contain(named);
        }
    }

    [Fact]
    public void A_linux_without_a_working_statx_reads_the_bounded_files_by_the_portable_road_and_says_the_trust_clauses()
    {
        var line = PlatformRoads.Of(new(false, true, false, false, true, false, "Linux 3.10")).Line;

        foreach (var named in new[] { "the owner", "regular file", "inode and change time", "5 s ceiling", "swapped in" })
        {
            line.Should().Contain(named);
        }

        line.Should().NotContain("2 s", "a Linux holder is still believed by the kernel's tick");
    }

    [Fact]
    public void No_host_is_refused_for_want_of_a_reader()
    {
        var unnamed = PlatformRoads.Of(new(false, false, false, false, true, false, "Illumos"));

        unnamed.Trust.Should().Be(TrustRoad.Portable);
        unnamed.Bounded.Should().Be(BoundedRoad.Portable);
        unnamed.Holder.Should().Be(HolderRoad.ProcessStartTime);
        unnamed.Line.Should().Contain("Illumos").And.Contain("NOT verified");
        foreach (var row in Table)
        {
            var act = () => PlatformRoads.Of(row.Host);
            act.Should().NotThrow($"{row.Row} is a road, never a throw");
        }
    }

    [Fact]
    public void This_hosts_facts_name_it_and_its_road_is_the_tables_row_for_them()
    {
        var facts = PlatformFactsReader.Current();

        facts.Windows.Should().Be(OperatingSystem.IsWindows());
        facts.Linux.Should().Be(OperatingSystem.IsLinux());
        facts.Description.Should().NotBeNullOrWhiteSpace();
        PlatformFactsReader.CurrentRoad.Should().BeEquivalentTo(PlatformRoads.Of(facts), "the road is the table's row for the facts the host reported, computed once");
        if (OperatingSystem.IsLinux())
        {
            facts.StatxAvailable.Should().Be(KernelFacts.StatxProbe().Match(available => available, _ => false), "statx counts as available only where a statx of / returned 0");
            KernelFacts.StatxProbe().Match(available => available, _ => false).Should().BeTrue("this suite's Linux hosts answer statx; a seccomp-filtered one would take the portable road, said in the line");
        }
    }
}
