using FluentAssertions;
using ModelArms.Adapters;
using ModelArms.Core.Experiment;
using Xunit;
using Bench.Tests.Infrastructure;
using ArmsProgram = ModelArms.Program;
using ModelArmsApp = ModelArms;

namespace Bench.Tests.ModelArms;

/// <summary>The <c>init</c> verb (D18): a new root with a template <c>experiment.json</c>, an empty <c>scenarios.json</c> and a <c>briefs/</c> folder, made only
/// where the module would later accept a root, owner-only so the gate's trust check passes on the first <c>validate</c>, and never through a link.
/// <para>
/// What this does NOT prove: that two <c>init</c> calls racing for one root cannot both pass the existence check (the files are created with <c>CreateNew</c>, so
/// neither overwrites the other, but the second may write into the first's folder), that a root on a file system without POSIX modes is owner-only, that an ancestor
/// above the parent folder is not a link (the gate allows one, a WSL home is usually one), or anything on Windows, where the modes and the link check are not performed.
/// </para></summary>
public sealed class InitVerbTests
{
    private static readonly IReadOnlyDictionary<string, string> NoEnvironment = new Dictionary<string, string>();
    private static readonly UnixFileMode OwnerOnlyFolder = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private static readonly UnixFileMode OwnerOnlyFile = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    private static async Task<(int Code, string Output, string Error)> RunAsync(IReadOnlyList<string> args, IReadOnlyDictionary<string, string>? environment = null)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = await ArmsProgram.RunAsync([.. args], output, error, environment ?? NoEnvironment, TestContext.Current.CancellationToken);
        return (code, output.ToString(), error.ToString());
    }

    private static Task<(int Code, string Output, string Error)> InitAsync(string root) => RunAsync(["init", root]);

    private static void Posix() => Assert.SkipWhen(OperatingSystem.IsWindows(), "modes and links are POSIX");

    [Fact]
    public async Task Init_creates_the_root_the_template_the_empty_scenarios_and_the_briefs_folder()
    {
        using var temp = new TempFolder();
        var root = temp.Combine("fresh");

        var (code, output, error) = await InitAsync(root);

        code.Should().Be(0, error);
        File.Exists(Path.Combine(root, "experiment.json")).Should().BeTrue("init writes the template");
        File.ReadAllText(Path.Combine(root, "scenarios.json")).Should().Be("[]\n");
        Directory.Exists(Path.Combine(root, "briefs")).Should().BeTrue();
        Directory.EnumerateFileSystemEntries(Path.Combine(root, "briefs")).Should().BeEmpty("the briefs folder holds the operator's overrides only");
        output.Should().Contain($"created {root}: experiment.json (a template — replace every <fill me>), scenarios.json (empty), briefs/ (template overrides by name)")
            .And.Contain($"Next: model-arms validate --experiment {root}");
        File.ReadAllText(Path.Combine(root, "experiment.json")).Should().Be(
            ExperimentTemplate.From(File.ReadAllText(ModelArmsApp.ModuleFiles.Beside(AppContext.BaseDirectory).ExampleFile)),
            "the template is the shipped example with its values blanked");
    }

    [Fact]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public async Task Init_makes_the_root_and_its_files_owner_only_whatever_the_umask()
    {
        Posix();
        using var temp = new TempFolder();
        var root = temp.Combine("fresh");

        var (code, _, error) = await InitAsync(root);

        code.Should().Be(0, error);
        Directory.Exists(root).Should().BeTrue("init made the root whose mode is read below");
        File.GetUnixFileMode(root).Should().Be(OwnerOnlyFolder);
        File.GetUnixFileMode(Path.Combine(root, "briefs")).Should().Be(OwnerOnlyFolder);
        File.GetUnixFileMode(Path.Combine(root, "experiment.json")).Should().Be(OwnerOnlyFile);
        File.GetUnixFileMode(Path.Combine(root, "scenarios.json")).Should().Be(OwnerOnlyFile);
    }

    [Fact]
    public async Task Init_refuses_an_existing_root()
    {
        using var temp = new TempFolder();
        var root = temp.Folder("exists");

        var (code, output, error) = await InitAsync(root);

        code.Should().Be(4);
        error.Should().Contain("already exists");
        output.Should().BeEmpty();
        Directory.EnumerateFileSystemEntries(root).Should().BeEmpty("init never writes into an existing folder");
    }

    [Fact]
    public async Task The_template_has_every_required_member_and_validate_refuses_it_until_filled()
    {
        using var temp = new TempFolder();
        var root = temp.Combine("fresh");
        (await InitAsync(root)).Code.Should().Be(0);

        var (code, _, error) = await RunAsync(["validate", "--experiment", root]);

        code.Should().Be(4);
        error.Should().Contain("experiment.json is refused").And.Contain("<fill me>");
        error.Split('\n').Should().NotContain(line => line.Contains("is missing", StringComparison.Ordinal), "every member is present; only its value is unfilled");
        error.Should().NotContain("trust", "a root init made passes the trust check");
    }

    [Fact]
    public async Task Init_then_validate_over_a_filled_experiment_reaches_the_host_checks_not_a_trust_refusal()
    {
        Posix();
        using var fixture = await new ExperimentRoot().WithTargetAsync();
        var root = Path.Combine(fixture.Temp, "fresh");
        (await InitAsync(root)).Code.Should().Be(0);
        File.Exists(Path.Combine(root, "experiment.json")).Should().BeTrue("init wrote the template this test then fills");
        File.WriteAllText(Path.Combine(root, "experiment.json"), File.ReadAllText(Path.Combine(fixture.Root, "experiment.json")));
        File.WriteAllText(Path.Combine(root, "scenarios.json"), File.ReadAllText(Path.Combine(fixture.Root, "scenarios.json")));

        var (code, output, error) = await RunAsync(["validate", "--experiment", root], fixture.Environment);

        code.Should().Be(0, error);
        output.Should().Contain("resolved executables:").And.Contain("is valid on this host");
    }

    [Fact]
    public async Task Init_refuses_a_root_inside_a_git_checkout()
    {
        using var repository = await TempGitRepo.CreateAsync();
        var root = Path.Combine(repository.Path, "exp");

        var (code, _, error) = await InitAsync(root);

        code.Should().Be(4);
        error.Should().Contain("inside the git checkout");
        Directory.Exists(root).Should().BeFalse("a refused root is not created");
    }

    [Fact]
    public async Task Init_refuses_a_root_inside_this_repository()
    {
        var root = Path.Combine(Cli.Repository.Root, "testsToDelete-modelarms-init-refused");
        try
        {
            var (code, _, error) = await InitAsync(root);

            code.Should().Be(4);
            error.Should().Contain("inside this repository");
            Directory.Exists(root).Should().BeFalse("a refused root is not created");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Init_creates_one_folder_not_a_path()
    {
        using var temp = new TempFolder();
        var parent = temp.Combine("missing");

        var (code, _, error) = await InitAsync(Path.Combine(parent, "fresh"));

        code.Should().Be(4);
        error.Should().Contain($"{parent} does not exist — init creates one folder, not a path");
        Directory.Exists(parent).Should().BeFalse();
    }

    [Fact]
    public async Task Init_refuses_a_root_that_is_a_symbolic_link_and_writes_through_neither_a_live_nor_a_dangling_one()
    {
        Posix();
        using var temp = new TempFolder();
        temp.RequireLinks();
        var real = temp.Folder("real");
        var live = temp.Combine("live-link");
        var dangling = temp.Combine("dangling-link");
        File.CreateSymbolicLink(live, real);
        File.CreateSymbolicLink(dangling, temp.Combine("nowhere"));

        var viaLive = await InitAsync(live);
        var viaDangling = await InitAsync(dangling);

        viaLive.Code.Should().Be(4);
        viaDangling.Code.Should().Be(4);
        Directory.EnumerateFileSystemEntries(real).Should().BeEmpty("nothing was written through the link");
        Directory.Exists(temp.Combine("nowhere")).Should().BeFalse("a dangling link is not followed to create its target");
    }

    [Fact]
    public async Task Init_refuses_a_parent_folder_that_is_a_symbolic_link()
    {
        Posix();
        using var temp = new TempFolder();
        temp.RequireLinks();
        var real = temp.Folder("real-parent");
        var link = temp.Combine("parent-link");
        File.CreateSymbolicLink(link, real);

        var (code, _, error) = await InitAsync(Path.Combine(link, "fresh"));

        code.Should().Be(4);
        error.Should().Contain("symbolic link");
        Directory.EnumerateFileSystemEntries(real).Should().BeEmpty("nothing was written through the link");
    }

    [Fact]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public async Task Init_exits_3_when_the_host_will_not_let_it_create_the_root()
    {
        Posix();
        Assert.SkipWhen(RootFacts.CurrentUid() == 0, "root may create a folder in a read-only parent");
        using var temp = new TempFolder();
        var parent = temp.Folder("closed");
        File.SetUnixFileMode(parent, UnixFileMode.UserRead | UnixFileMode.UserExecute);

        var (code, output, error) = await InitAsync(Path.Combine(parent, "fresh"));

        code.Should().Be(3);
        error.Should().Contain("model-arms init").And.Contain(Path.Combine(parent, "fresh"));
        output.Should().BeEmpty();
    }

    [Fact]
    public async Task Init_refuses_a_root_holding_a_NUL_character()
    {
        var (code, _, error) = await InitAsync("/tmp/a\0b");

        code.Should().Be(4);
        error.Should().Contain("NUL");
    }

    [Fact]
    public async Task Init_refuses_an_empty_root_name()
    {
        var (code, _, error) = await InitAsync(string.Empty);

        code.Should().Be(4);
        error.Should().Contain("model-arms init").And.Contain("empty");
    }
}
