using FluentAssertions;
using ModelArms.Adapters;
using ModelArms.Core.Experiment;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>What the two <see cref="ArmGit"/> test classes (the questions E3 widened it with, the ones E4.S2 added for the task pipeline) share: a scratch folder removed on dispose, the environment a question is asked under, the host's git for the live proofs, and the small repositories they ask about.</summary>
public abstract class ArmGitTestBase : IDisposable
{
    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    private protected readonly TempFolder _temp = new();

    protected IReadOnlyDictionary<string, string> Environment => new Dictionary<string, string>(StringComparer.Ordinal) { ["HOME"] = _temp.Folder("home"), ["PATH"] = "/usr/bin:/bin" };

    protected ArmGit In(string folder, string? git = null) => new(git ?? ExperimentRoot.RealGit, folder, Environment);

    public void Dispose() => _temp.Dispose();

    protected string SentinelFile => Sentinel.Marker(_temp);

    protected bool Ran => Sentinel.Ran(_temp);

    /// <summary>A program a clone's config or hooks folder can name (see <see cref="Sentinel.PlantProgram"/>).</summary>
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    protected string PlantProgram(string name, string? body = null) => Sentinel.PlantProgram(_temp, name, body);

    protected static void Redirty(string file) => Sentinel.Redirty(file);

    protected Task HostGitAsync(string folder, params string[] arguments) => Sentinel.HostGitAsync(folder, Environment, Ct, arguments);

    protected Task ProveLiveAsync(string folder, string dirtyFile, params string[] hostQuestion) => Sentinel.ProveLiveAsync(_temp, Environment, Ct, folder, dirtyFile, hostQuestion);

    protected async Task<string> ClonedAsync()
    {
        var (source, _, _) = await SourceAsync();
        var clone = Path.Combine(_temp.Path, "clone");
        (await In(_temp.Path).CloneAsync(source, "plan", clone, Ct)).Ok();
        return clone;
    }

    /// <summary>A clone holding a submodule <c>sub</c> (a file <c>s.txt</c>) at the superproject's HEAD.</summary>
    protected async Task<(string Clone, string Sub, string ModuleFolder)> WithSubmoduleAsync()
    {
        var clone = await ClonedAsync();
        var sub = _temp.Folder("subsrc");
        await TargetBench.GitAsync(sub, "init", "--quiet", "--initial-branch", "main");
        _temp.File("one\n", "subsrc", "s.txt");
        await CommitAllAsync(sub, "sub first");
        await TargetBench.GitAsync(clone, "-c", "protocol.file.allow=always", "submodule", "add", "--quiet", sub, "sub");
        await CommitAllAsync(clone, "add the submodule");
        return (clone, Path.Combine(clone, "sub"), Path.Combine(clone, ".git", "modules", "sub"));
    }

    protected static async Task CommitAllAsync(string folder, string message)
    {
        await TargetBench.GitAsync(folder, "add", "-A");
        await TargetBench.GitAsync(folder, "-c", "user.name=test", "-c", "user.email=test@example.invalid", "-c", "commit.gpgsign=false", "commit", "--quiet", "-m", message);
    }

    /// <summary>A source of two commits: <c>a.txt</c>, then <c>src/B.cs</c> and <c>a.txt</c> changed.</summary>
    protected async Task<(string Source, string First, string Second)> SourceAsync()
    {
        var source = _temp.Folder("source");
        await TargetBench.GitAsync(source, "init", "--quiet", "--initial-branch", "plan");
        _temp.File("one\n", "source", "a.txt");
        await CommitAllAsync(source, "first");
        var first = (await TargetBench.GitAsync(source, "rev-parse", "HEAD")).Trim();
        _temp.File("class B { }\n", "source", "src", "B.cs");
        _temp.File("one\ntwo\n", "source", "a.txt");
        await CommitAllAsync(source, "second");
        return (source, first, (await TargetBench.GitAsync(source, "rev-parse", "HEAD")).Trim());
    }

    /// <summary>The filter drivers the HOST's git itself reports in <paramref name="folder"/> under <paramref name="environment"/> — the same HOME, the same PATH, the SYSTEM scope the host has (a CI runner with git-lfs installed has <c>filter.lfs.*</c> there): <c>config --name-only -z --get-regexp ^filter\.</c>, asked of the host's git directly, never through <see cref="ArmGit"/>.</summary>
    protected async Task<IReadOnlyList<string>> HostDriversAsync(string folder, IReadOnlyDictionary<string, string> environment)
    {
        var attempt = await Bench.Infrastructure.Process.ProcessRunner.RunAsync(ExperimentRoot.RealGit, ["config", "--name-only", "-z", "--get-regexp", @"^filter\."], folder, TimeSpan.FromSeconds(60), string.Empty, GitEnvironment.Of(environment), Ct);
        var done = attempt.Should().BeOfType<Bench.Infrastructure.Process.ProcessAttempt.Completed>(attempt.Describe).Subject;
        done.Result.ExitCode.Should().BeOneOf([0, 1], "git config --get-regexp: 1 is no match");
        return [.. done.Result.StandardOutput.Split('\0', StringSplitOptions.RemoveEmptyEntries).Select(name => name[7..name.LastIndexOf('.')]).Distinct(StringComparer.Ordinal)];
    }

    /// <summary>The <c>GIT_CONFIG_*</c> lines ArmGit must hand a question for those drivers, as it emits them: four pairs per driver in the order git listed them (<c>clean</c>, <c>smudge</c>, <c>process</c> empty, <c>required</c> false) and their count — none for no driver. Built here from the HOST's list, not copied from ArmGit, so a driver git reads that the neutralisation does not name fails.</summary>
    protected static IReadOnlyList<string> Neutralisation(IReadOnlyList<string> drivers)
    {
        string[] variables = ["clean", "smudge", "process", "required"];
        string[] values = ["", "", "", "false"];
        var lines = drivers.SelectMany((driver, d) => variables.SelectMany((variable, v) => new[] { $"GIT_CONFIG_KEY_{(d * 4) + v}=filter.{driver}.{variable}", $"GIT_CONFIG_VALUE_{(d * 4) + v}={values[v]}" })).ToList();
        return drivers.Count == 0 ? [] : [$"GIT_CONFIG_COUNT={drivers.Count * 4}", .. lines];
    }

    protected static bool IsEnumeration(string call) => call.Contains(" config --name-only -z --get-regexp ", StringComparison.Ordinal);

    protected static bool Smudges(string call) => call.Contains(" clone ", StringComparison.Ordinal) || call.Contains(" --detach ", StringComparison.Ordinal) || call.Contains(" submodule update ", StringComparison.Ordinal);

    /// <summary>What one recorded call's <c>GIT_*</c> variables must be: <paramref name="fixedLines"/> plus — for a question that is neutralised (not the driver enumeration itself, not a question that writes the base's worktree) — exactly <paramref name="neutralisation"/>, whatever that is on this host.</summary>
    protected static void GitVariablesShould(string call, IEnumerable<string> env, IReadOnlyList<string> fixedLines, IReadOnlyList<string> neutralisation, string because) =>
        env.Where(line => line.StartsWith("GIT_", StringComparison.Ordinal)).Should().BeEquivalentTo(IsEnumeration(call) || Smudges(call) ? fixedLines : [.. fixedLines, .. neutralisation], $"{call}: {because}");

    protected const string Attributes = "info/attributes";

    protected void AppendConfig(string folder, string text) => File.AppendAllText(Path.Combine(folder, "config"), text);

    protected void AppendAttributes(string gitFolder, string text)
    {
        Directory.CreateDirectory(Path.Combine(gitFolder, "info"));
        File.AppendAllText(Path.Combine(gitFolder, Attributes), text);
    }
}
