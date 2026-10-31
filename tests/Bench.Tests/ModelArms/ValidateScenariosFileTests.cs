using FluentAssertions;
using Xunit;
using GateSeams = ModelArms.GateSeams;

namespace Bench.Tests.ModelArms;

/// <summary>The scenarios file as the gate judged it (D17), moved out of <c>ValidateVerbTests</c> (the file limit): a file swapped between its trust check and its read is refused, one that stays as judged is read and validates. Both drive <c>validate</c> through the gate with a seam (<see cref="ValidateVerbTests.ThroughValidateAsync"/>).
/// <para>
/// What this does NOT prove: Windows (the swap is a POSIX rename and mode); the portable road's witness (<c>ModelArmsProgramTests</c>).
/// </para></summary>
public sealed class ValidateScenariosFileTests
{
    private static void Posix()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "the fixture's executables, links and modes are POSIX");
    }

    [Fact]
    public async Task A_scenarios_file_swapped_between_its_trust_check_and_its_read_is_refused_with_exit_4()
    {
        Posix();
        using var root = await new ExperimentRoot().WithTargetAsync();
        var scenarios = Path.Combine(root.Root, "scenarios.json");
        var seams = GateSeams.Real with
        {
            AfterRead = path =>
            {
                if (path == scenarios && !OperatingSystem.IsWindows())
                {
                    var other = Path.Combine(root.Root, "other.json");
                    File.WriteAllText(other, """[{"id":"S9","kind":"cs","name":"Swapped"}]""");
                    File.SetUnixFileMode(other, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                    File.Move(other, scenarios, overwrite: true);
                }

                return Task.CompletedTask;
            },
        };

        var (code, error) = await ValidateVerbTests.ThroughValidateAsync(root, seams);

        code.Should().Be(4);
        error.Should().Contain("scenarios.json changed between its trust check and its read").And.Contain("inode");
    }

    [Fact]
    public async Task A_scenarios_file_that_stays_as_it_was_judged_is_read_once_and_validates()
    {
        Posix();
        using var root = await new ExperimentRoot().WithTargetAsync();
        var reads = new List<string>();
        var seams = GateSeams.Real with { AfterRead = path => { reads.Add(Path.GetFileName(path)); return Task.CompletedTask; } };

        var (code, error) = await ValidateVerbTests.ThroughValidateAsync(root, seams);

        error.Should().BeEmpty();
        code.Should().Be(0);
        reads.Should().Contain("scenarios.json", "validate looks at scenarios.json again after reading it, through the gate's own seam");
    }
}
