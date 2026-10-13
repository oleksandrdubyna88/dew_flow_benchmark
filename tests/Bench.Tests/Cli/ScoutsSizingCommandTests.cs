using Bench.Cli;
using Bench.Domain.Scouts;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Cli;

/// <summary><c>bench scouts sizing --run … --out …</c> (E2 S8, §3.8, D7): the sizing simulation over a run the real <c>run</c> settled with the
/// fake claude — printed and written as Markdown, the same bytes for the same seed, reported and never a gate (exit 0 whether a task count is
/// found or not), and naming no task, repository or statement. Written RED first: the verb did not exist (exit 4, "unknown scouts sub-verb").</summary>
[Collection("postgres")]
public sealed class ScoutsSizingCommandTests(PostgresFixture postgres)
{
    private static readonly ScoutArmId[] TwoArms = [ScoutArmId.A1, ScoutArmId.B1];

    [Fact]
    public async Task Sizing_prints_and_writes_the_simulation_the_same_for_the_same_seed_and_names_no_task()
    {
        var setup = await ScoutsCliSetup.StartAsync(postgres, tasks: 2);
        await using var _ = setup;
        setup.Answer(TwoArms);
        var (ran, runOutput, runError) = await setup.RunAsync(setup.Services(), setup.RunArgs(TwoArms));
        ran.Should().Be(ExitCodes.Pass, runOutput + runError);
        var run = ScoutsCliSetup.RunIdOf(runOutput).ToString();
        var first = Path.Combine(setup.ArtifactRoot, "sizing-1.md");
        var second = Path.Combine(setup.ArtifactRoot, "sizing-2.md");

        var (code, output, error) = await setup.RunAsync(setup.Services(), "sizing", "--run", run, "--out", first, "--simulations", "40", "--resamples", "200");
        var again = await setup.RunAsync(setup.Services(), "sizing", "--run", run, "--out", second, "--simulations", "40", "--resamples", "200");

        code.Should().Be(ExitCodes.Pass, output + error);
        again.Code.Should().Be(ExitCodes.Pass, again.Error);
        var written = await File.ReadAllTextAsync(first, TestContext.Current.CancellationToken);
        written.Should().Be(await File.ReadAllTextAsync(second, TestContext.Current.CancellationToken), "the same seed sizes the same");
        written.Should().Contain("reported, not a gate").And.Contain("| 8 |").And.Contain("| 30 |").And.Contain("W1")
            .And.Contain("not simulated", "no claim of the run is assessed, so the sizing is of recall alone and says so");
        output.Should().Contain("sizing").And.Contain(first);
        foreach (var task in setup.Tasks)
        {
            (written + output).Should().NotContain(task.Statement).And.NotContain(task.Repository).And.NotContain(task.Id.Value);
        }
    }

    [Fact]
    public async Task Sizing_refuses_a_missing_out_an_unknown_run_and_bad_settings()
    {
        var setup = await ScoutsCliSetup.StartAsync(postgres);
        await using var _ = setup;
        var out_ = Path.Combine(setup.ArtifactRoot, "sizing.md");

        (await setup.RunAsync(setup.Services(), "sizing", "--run", Guid.NewGuid().ToString())).Code.Should().Be(ExitCodes.Configuration);
        (await setup.RunAsync(setup.Services(), "sizing", "--run", Guid.NewGuid().ToString(), "--out", out_)).Code.Should().Be(ExitCodes.Configuration);
        (await setup.RunAsync(setup.Services(), "sizing", "--run", Guid.NewGuid().ToString(), "--out", out_, "--simulations", "1")).Code.Should().Be(ExitCodes.Configuration);
        File.Exists(out_).Should().BeFalse();
    }
}
