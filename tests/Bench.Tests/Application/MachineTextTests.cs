using Bench.Application;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Application;

/// <summary>Reading a machine's own reports, against output captured from this machine on 2026-08-19 rather
/// than against an idealised shape. Two of these parsers exist only because the real output surprised
/// somebody.</summary>
public sealed class MachineTextTests
{
    [Fact]
    public void The_WSL_version_block_arrives_UTF16_and_is_read_anyway()
    {
        // Verified on this machine: wsl.exe writes UTF-16LE, so a reader that decoded it as UTF-8 hands the
        // parser W\0S\0L\0 — the first bytes are 87,0,83,0,76,0. The proper fix is at the read and is done
        // there too; this keeps the parser correct whichever way the output arrived.
        var mangled = string.Join(string.Empty, RealWslOutput.Select(c => c + "\0"));

        var facts = MachineText.Wsl(mangled);

        facts.Runtime.Should().Be("2.7.10.0");
        facts.Direct3D.Should().Be("1.611.1-81528511");
        facts.DxCore.Should().Be("10.0.26100.1-240331-1435.ge-release");
    }

    [Fact]
    public void The_same_block_read_cleanly_parses_identically()
    {
        MachineText.Wsl(RealWslOutput).Should().Be(MachineText.Wsl(string.Join(string.Empty, RealWslOutput.Select(c => c + "\0"))));
    }

    [Fact]
    public void A_host_that_is_not_under_WSL_has_no_WSL_layer_rather_than_empty_strings_pretending_to_be_one()
    {
        MachineText.Wsl(string.Empty).Should().Be(Bench.Domain.Trace.WslFacts.None);
    }

    [Fact]
    public void The_distro_is_read_from_VERSION_ID_and_not_from_the_prose_line()
    {
        var (release, codename) = MachineText.OsRelease("""
            PRETTY_NAME="Ubuntu 26.04 LTS"
            NAME="Ubuntu"
            VERSION_ID="26.04"
            VERSION="26.04 LTS (Resolute Raccoon)"
            VERSION_CODENAME=resolute
            ID=ubuntu
            """);

        release.Should().Be("26.04", "the quotes are punctuation in that file, not part of the value");
        codename.Should().Be("resolute");
    }

    [Fact]
    public void Total_memory_is_the_kernels_KiB_and_not_SI_kilobytes()
    {
        // Real line from this machine's distro. kB in /proc/meminfo means KiB; using 1000 is a 2.4 % error
        // nobody would ever notice.
        MachineText.TotalMemoryBytes("MemTotal:       47066772 kB\nMemFree:        12827220 kB")
            .Should().Be(47_066_772L * 1024);
    }

    [Fact]
    public void A_meminfo_nobody_could_read_is_zero_bytes_which_the_caller_stores_as_unknown()
    {
        MachineText.TotalMemoryBytes(string.Empty).Should().Be(0);
    }

    [Fact]
    public void The_processor_model_and_its_LOGICAL_count_are_read_and_the_physical_one_is_not_guessed()
    {
        var (model, logical) = MachineText.CpuInfo("""
            processor	: 0
            model name	: AMD Ryzen AI 9 HX 370 w/ Radeon 890M
            processor	: 1
            model name	: AMD Ryzen AI 9 HX 370 w/ Radeon 890M
            """);

        model.Should().Be("AMD Ryzen AI 9 HX 370 w/ Radeon 890M");
        logical.Should().Be(2, "one line per logical processor, which is the only count this file states plainly");
    }

    /// <summary>Verbatim from <c>wsl.exe --version</c> on this machine, 2026-08-19.</summary>
    private const string RealWslOutput = """
        WSL version: 2.7.10.0
        Kernel version: 6.18.33.2-2
        WSLg version: 1.0.73.2
        MSRDC version: 1.2.6676
        Direct3D version: 1.611.1-81528511
        DXCore version: 10.0.26100.1-240331-1435.ge-release
        Windows version: 10.0.26200.8653
        """;
}
