using Bench.Infrastructure.Process;
using FluentAssertions;
using ModelArms.Core.Experiment;
using ModelArms.Adapters;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>The scenario harness of the module's verbs: the console's own binary, started as a process the way an operator starts it, with a working directory and a PATH the
/// test chose — because the defects these guard against (a program looked up in the current directory or on a PATH entry in the target) exist only in a real process, not in
/// an in-process call. The subject is never doubled; what is placed in the target are marker-writing shims, a program the target could hold.
/// <para>
/// What this does NOT prove: any verb but <c>validate</c> and <c>prep-base</c> (the others are not built yet; the catalogue says so), the binary on Windows (the shims and links
/// are POSIX), a launch made by a later story (the structural test in <c>ModelArmsArchitectureTests</c> sees those land), or that the fixture's <c>claude</c>, <c>dotnet</c>,
/// <c>node</c> and <c>npm</c> run (empty files; only git is launched).
/// </para></summary>
public sealed class ModelArmsScenarioTests
{
    private static async Task<(int Code, string Output, string Error)> RunBinaryAsync(ExperimentRoot root, string workingDirectory, params string[] arguments)
    {
        var environment = root.Environment.ToDictionary(variable => variable.Key, variable => variable.Value);
        foreach (var name in new[] { "DOTNET_ROOT", "DOTNET_ROOT_X64" })
        {
            if (System.Environment.GetEnvironmentVariable(name) is { Length: > 0 } value)
            {
                environment[name] = value;
            }
        }

        var attempt = await ProcessRunner.RunAsync(
            ExperimentRoot.ConsoleBinary, [.. arguments, "--experiment", root.Root], workingDirectory, TimeSpan.FromSeconds(60), string.Empty, environment, TestContext.Current.CancellationToken);
        var done = attempt.Should().BeOfType<ProcessAttempt.Completed>(attempt.Describe).Subject.Result;
        return (done.ExitCode, done.StandardOutput, done.StandardError);
    }

    private static string Target(ExperimentRoot root) => Path.Combine(root.Temp, "example", "target");

    private static void Posix() => Assert.SkipWhen(OperatingSystem.IsWindows(), "the shims and the fixture's links are POSIX");

    [Fact]
    public async Task Validate_started_inside_the_target_runs_nothing_the_target_placed()
    {
        Posix();
        using var root = await new ExperimentRoot().WithTargetAsync();
        foreach (var name in new[] { "git", "id", "stat" })
        {
            root.Shim(Path.Combine(Target(root), name), name);
        }

        var (code, output, error) = await RunBinaryAsync(root, Target(root), "validate");

        error.Should().BeEmpty();
        code.Should().Be(0, output);
        output.Should().Contain($"  git → {ExperimentRoot.RealGit}");
        root.Markers().Should().BeEmpty("a program looked up by name in the current directory would be the target's own");
    }

    [Fact]
    public async Task Validate_with_a_target_PATH_entry_ahead_refuses_the_entry_and_runs_nothing_from_it()
    {
        Posix();
        using var root = await new ExperimentRoot().WithTargetAsync();
        var bin = Path.Combine(Target(root), "bin");
        foreach (var name in new[] { "git", "id", "stat" })
        {
            root.Shim(Path.Combine(bin, name), name);
        }

        root.UsePath(string.Join(Path.PathSeparator, bin, Path.Combine(root.Temp, "bin")));

        var (code, _, error) = await RunBinaryAsync(root, root.Temp, "validate");

        code.Should().Be(4);
        error.Should().Contain($"PATH entry '{bin}'").And.Contain("D17").And.Contain("the source was not checked: PATH is refused");
        root.Markers().Should().BeEmpty("the check that keeps an arm's file from running must not run one");
    }

    [Fact]
    public async Task Prep_base_through_the_binary_runs_the_same_checks_and_is_refused_until_E4_S4()
    {
        Posix();
        using var root = await new ExperimentRoot().WithTargetAsync();

        var (code, output, error) = await RunBinaryAsync(root, root.Temp, "prep-base");

        code.Should().Be(4);
        output.Should().Contain("resolved executables:");
        error.Should().Contain("E4.S4");
    }

    [Fact]
    public void The_catalogue_names_every_verb_the_command_line_accepts()
    {
        var catalogue = File.ReadAllText(Path.Combine(Cli.Repository.Root, "research", "module_tests.md"));

        ModelArmsCommandLine.Verbs.Should().NotBeEmpty("a derived list that is empty passes forever");
        ModelArmsCommandLine.Verbs.Where(verb => !catalogue.Contains($"| `model-arms {verb}` |", StringComparison.Ordinal)).Should().BeEmpty(
            "every verb of the command line has a row in research/module_tests.md, covered or not covered with its reason");
    }
}
