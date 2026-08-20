using Bench.Domain.Trace;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Trace;

/// <summary>The machine a number came off.
/// <para>
/// Every rule here exists because a number was once read off a machine nobody recorded. The sharpest is the
/// health code: a card at <c>31</c> is still enumerated while everything runs on the CPU, so a whole campaign
/// can be CPU numbers wearing a GPU's label with no other field disagreeing.
/// </para></summary>
public sealed class MachineFactsTests
{
    [Fact]
    public void A_run_stored_before_any_of_this_existed_is_NOT_RECORDED_rather_than_a_blank_machine()
    {
        MachineFacts.NotRecorded.Recorded.Should().BeFalse();
        MachineFacts.NotRecorded.Fingerprint.Should().BeEmpty(
            "a fingerprint over nothing would make every unrecorded run look like the same machine");
    }

    [Fact]
    public void Two_runs_on_the_same_machine_share_a_fingerprint()
    {
        Here().Fingerprint.Should().Be(Here().Fingerprint);
        Here().Fingerprint.Should().NotBeEmpty();
    }

    [Fact]
    public void A_DRIVER_update_makes_it_a_different_machine()
    {
        var updated = Here() with
        {
            Adapters = [Adapter() with { DriverVersion = "32.0.31036.1000" }],
        };

        updated.Fingerprint.Should().NotBe(Here().Fingerprint,
            "a driver is exactly the kind of change that moves numbers and leaves every other field alone");
    }

    [Fact]
    public void A_WINDOWS_PATCH_makes_it_a_different_machine()
    {
        // The UBR, and the reason it is carried at all: Win32_OperatingSystem.Version stops at 10.0.26200,
        // so a version without the patch cannot tell two runs a month apart.
        var patched = Here() with { Os = Os() with { Build = "10.0.26200.8700" } };

        patched.Fingerprint.Should().NotBe(Here().Fingerprint);
    }

    [Fact]
    public void A_D3D_SHIM_update_makes_it_a_different_machine()
    {
        // The GPU passthrough layer, delivered by the Windows driver package into /usr/lib/wsl/lib. It is
        // where the 155 s boundary finding lives, and no other field would show a change in it.
        var reshimmed = Here() with { Wsl = Wsl() with { Direct3D = "1.612.0-81600000" } };

        reshimmed.Fingerprint.Should().NotBe(Here().Fingerprint);
    }

    [Fact]
    public void FREE_SPACE_does_not_move_the_fingerprint()
    {
        // The trap this property is most likely to fall into. Free space changes by the minute, and a
        // fingerprint that followed it would give every run its own machine — destroying the one comparison
        // the fingerprint exists to make possible.
        var emptier = Here() with { Volume = Volume() with { FreeBytes = 17 } };

        emptier.Fingerprint.Should().Be(Here().Fingerprint);
    }

    [Fact]
    public void A_card_that_fell_off_the_bus_is_visible_without_reading_a_single_timing()
    {
        var faulted = Here() with { Adapters = [Adapter() with { HealthCode = 31 }] };

        faulted.UnhealthyAdapters.Should().ContainSingle();
        faulted.Adapters[0].IsHealthy.Should().BeFalse();
        faulted.Adapters[0].Describe.Should().Contain("UNHEALTHY").And.Contain("31");
        Here().UnhealthyAdapters.Should().BeEmpty("the ordinary case must cost the operator no attention");
    }

    [Fact]
    public void The_OS_is_described_by_its_build_and_never_by_a_product_name()
    {
        // Windows reports "Windows 10 Pro" from a Windows 11 machine — the registry value was never updated —
        // so the record carries no product name to be tempted by. 26200 is the truth.
        Os().Describe.Should().Contain("26200").And.Contain("25H2");
        Os().Canonical.Should().NotContain("Windows 10");
    }

    [Fact]
    public void An_unread_machine_and_an_unread_field_are_different_states()
    {
        var readButEmpty = new MachineFacts { Hostname = "bench-01" };

        readButEmpty.Recorded.Should().BeTrue("somebody asked, and the answer was thin");
        readButEmpty.Fingerprint.Should().NotBeEmpty();
        readButEmpty.Os.Describe.Should().Be("os unknown");
    }

    // ---- scaffolding -------------------------------------------------------------------------------

    private static OsFacts Os() => new("windows", "Professional", "25H2", "10.0.26200.8653");

    private static WslFacts Wsl() => new("2.7.10.0", "1.611.1-81528511", "10.0.26100.1");

    private static AdapterFacts Adapter() =>
        new("AMD Radeon AI PRO R9700", 32L * 1024 * 1024 * 1024, "32.0.31035.1003", "2026-07-24", 0);

    private static VolumeFacts Volume() => new("D:\\", "NTFS", 4096, 900L * 1024 * 1024 * 1024, 2000L * 1024 * 1024 * 1024);

    private static MachineFacts Here() => new()
    {
        Hostname = "bench-01",
        MachineId = "6f1c…",
        Os = Os(),
        Wsl = Wsl(),
        Cpu = new CpuFacts("AMD Ryzen 9", 12, 24, "High performance"),
        TotalRamBytes = 96L * 1024 * 1024 * 1024,
        Adapters = [Adapter()],
        Volume = Volume(),
    };
}
