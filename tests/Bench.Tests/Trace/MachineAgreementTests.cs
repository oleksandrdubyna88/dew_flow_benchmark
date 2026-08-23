using Bench.Domain.Trace;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Trace;

/// <summary>Whether the runs behind a comparison came off one machine.
/// <para>
/// The rule these pin is that <em>nobody looked</em> is never rendered as <em>they agree</em>. A comparison
/// silent about its hardware reads as one that checked, and a gap between two arms measured on two machines
/// is a gap about the machines — indistinguishable from a real backend result once it is averaged.
/// </para></summary>
public sealed class MachineAgreementTests
{
    [Fact]
    public void Runs_on_one_machine_agree_and_the_comparison_may_be_read_as_being_about_the_ARM()
    {
        var agreement = MachineAgreement.Of([Here(), Here()]);

        agreement.State.Should().Be(MachineConsensus.OneMachine);
        agreement.Machines.Should().Be(1);
        agreement.OnOneMachine.Should().BeTrue();
    }

    [Fact]
    public void Runs_on_two_machines_are_NAMED_rather_than_folded_into_one_average_in_silence()
    {
        var elsewhere = Here() with { Hostname = "bench-02", MachineId = "9a4d…" };

        var agreement = MachineAgreement.Of([Here(), elsewhere]);

        // The headline guard. Nothing recorded which machine produced a row until MachineFacts existed, so
        // two machines' results merged silently; the comparison must now say the difference is confounded.
        agreement.State.Should().Be(MachineConsensus.SeveralMachines);
        agreement.Machines.Should().Be(2);
        agreement.OnOneMachine.Should().BeFalse();
        agreement.Describe.Should().Contain("DIFFERENT machines").And.Contain("not attributable to the backend");
    }

    [Fact]
    public void A_DRIVER_update_alone_makes_it_two_machines()
    {
        var updated = Here() with
        {
            Adapters = [Adapter() with { DriverVersion = "32.0.31036.1000" }],
        };

        // Same host, same name, same everything a listing would show. A driver is exactly the change that
        // moves numbers while leaving every other field alone, which is why the fingerprint carries it.
        MachineAgreement.Of([Here(), updated]).State.Should().Be(MachineConsensus.SeveralMachines);
    }

    [Fact]
    public void Runs_that_recorded_no_machine_are_NOT_RECORDED_rather_than_one_machine()
    {
        var agreement = MachineAgreement.Of([MachineFacts.NotRecorded, MachineFacts.NotRecorded]);

        // The captured-or-zero rule, applied where it is easiest to miss: two unrecorded runs share an empty
        // fingerprint, and counting that as agreement would have every pre-probe comparison claim one machine.
        agreement.State.Should().Be(MachineConsensus.NotRecorded);
        agreement.Machines.Should().Be(0);
        agreement.OnOneMachine.Should().BeFalse();
        agreement.Describe.Should().Contain("no run here recorded the machine");
    }

    [Fact]
    public void One_recorded_machine_beside_an_unrecorded_run_vouches_for_neither()
    {
        var agreement = MachineAgreement.Of([Here(), MachineFacts.NotRecorded]);

        // The state a real database actually produces: some runs probed, some stored before the probe
        // existed. Reading it as OneMachine would let one probed run vouch for a population nobody read.
        agreement.State.Should().Be(MachineConsensus.PartlyRecorded);
        agreement.Machines.Should().Be(1);
        agreement.Unrecorded.Should().Be(1);
        agreement.OnOneMachine.Should().BeFalse();
        agreement.Describe.Should().Contain("1 recorded none");
    }

    [Fact]
    public void An_empty_population_says_nothing_was_recorded_rather_than_throwing()
    {
        MachineAgreement.Of([]).State.Should().Be(MachineConsensus.NotRecorded);
        MachineAgreement.Nothing.State.Should().Be(MachineConsensus.NotRecorded);
    }

    [Fact]
    public void Every_state_says_something_because_silence_reads_as_agreement()
    {
        MachineFacts[][] populations =
        [
            [Here(), Here()],
            [Here(), Here() with { Hostname = "bench-02" }],
            [Here(), MachineFacts.NotRecorded],
            [MachineFacts.NotRecorded],
        ];

        populations.Should().OnlyContain(p => MachineAgreement.Of(p).Describe.Length > 0);
    }

    // ---- scaffolding -------------------------------------------------------------------------------

    private static AdapterFacts Adapter() =>
        new("AMD Radeon AI PRO R9700", 32L * 1024 * 1024 * 1024, "32.0.31035.1003", "2026-07-24", 0);

    private static MachineFacts Here() => new()
    {
        Hostname = "bench-01",
        MachineId = "6f1c…",
        Os = new OsFacts("windows", "Professional", "25H2", "10.0.26200.8653"),
        Wsl = new WslFacts("2.7.10.0", "1.611.1-81528511", "10.0.26100.1"),
        Cpu = new CpuFacts("AMD Ryzen 9", 12, 24, "High performance"),
        TotalRamBytes = 96L * 1024 * 1024 * 1024,
        Adapters = [Adapter()],
        Volume = new VolumeFacts("D:\\", "NTFS", 4096, 900L * 1024 * 1024 * 1024, 2000L * 1024 * 1024 * 1024),
    };
}
