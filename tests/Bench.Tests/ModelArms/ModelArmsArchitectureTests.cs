using System.Reflection;
using FluentAssertions;
using ModelArms.Core.Experiment;
using ModelArms.Core.Launch;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>The model-arms module's boundary, guarded from its first commit. Its decisions live in
/// <c>ModelArms.Core</c>, which stays pure: it sees the domain and the application layer's ports and
/// nothing that does IO. Nothing in the product references the module back.
/// <para>
/// What this does NOT prove: that a Core type is pure inside its own body. A reference list cannot see a
/// <c>File.ReadAllText</c> (the runtime is always allowed); the pure-list test below only makes sure every
/// decider is NAMED, so a story that adds one has to say so on purpose.
/// </para></summary>
public sealed class ModelArmsArchitectureTests
{
    private const string Core = "ModelArms.Core.dll";

    /// <summary>The public static classes of <c>ModelArms.Core</c>, one marker per story so parallel stories touch
    /// disjoint lines. A decider added to Core is added here in the same story, under its own marker.</summary>
    private static readonly Type[] PureDeciders =
    [
        // E1.S3
        typeof(ExperimentFile), typeof(ArmNames), typeof(ExecutableRule), typeof(StepKinds),

        // E1.S6
        typeof(StepLaunch), typeof(ArmChildEnvironment),

        // E1.S7
        typeof(global::ModelArms.Core.Briefs.BriefTemplates), typeof(global::ModelArms.Core.Briefs.BriefFields), typeof(global::ModelArms.Core.Briefs.BriefFiller),

        // E1.S8
        typeof(RootRequirements), typeof(RootTrust), typeof(ExecutableResolution), typeof(ScenariosFile), typeof(BaselineFile),

        // E1.S9
        typeof(ModelArmsCommandLine),

        // E1.S10
        typeof(HostKinds), typeof(HostChecks), typeof(global::ModelArms.Core.Briefs.BriefRehearsal), typeof(GitEnvironment),

        // E1.S11
        typeof(ExperimentTemplate),

        // E2.S0
        typeof(SchemePositions),

        // E2

        // E3

        // E4

        // E5
    ];

    [Fact]
    public void Core_references_only_the_runtime_the_domain_and_the_application_layer()
    {
        ArchitectureTests.Referenced(Core).Should().OnlyContain(
            r => ArchitectureTests.IsRuntime(r) || r.Name == "Bench.Domain" || r.Name == "Bench.Application",
            "the module's decisions see the domain and the ports and nothing that does IO — facts arrive as parameters");
    }

    [Fact]
    public void Core_never_references_infrastructure_or_entity_framework()
    {
        ArchitectureTests.Referenced(Core).Should().NotContain(
            r => r.Name == "Bench.Infrastructure" || r.Name!.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
                || r.Name!.StartsWith("Npgsql", StringComparison.Ordinal),
            "an adapter, a database mapper or a driver in Core is the first step to a decision that reads a file");
    }

    [Fact]
    public void Nothing_under_src_or_hosts_references_the_model_arms_module()
    {
        var projects = ProductionProjects();
        projects.Should().NotBeEmpty("a scan that matches nothing passes forever");
        NamesTheModule(Path.Combine(Cli.Repository.Root, "tests", "Bench.Tests", "Bench.Tests.csproj")).Should().BeTrue(
            "the pattern must still match the one project that references the module on purpose");

        var naming = projects.Where(NamesTheModule).Select(path => Path.GetRelativePath(Cli.Repository.Root, path).Replace('\\', '/')).ToList();
        naming.Should().BeEmpty("the product does not know the module exists; only the module knows the product");

        var assemblies = ProductionAssemblies();
        assemblies.Should().Contain("Bench.Domain.dll", "the assembly scan must still see a known product assembly");
        ArchitectureTests.Referenced("Bench.Tests.dll").Should().Contain(
            r => r.Name == "ModelArms.Core",
            "the assembly scan must still find the one assembly that references the module on purpose — a scan that matches nothing passes forever");

        foreach (var assembly in assemblies)
        {
            ArchitectureTests.Referenced(assembly).Should().NotContain(
                r => r.Name == "ModelArms.Core" || r.Name == "model-arms",
                $"{assembly} must not reference the model-arms module");
        }
    }

    [Fact]
    public void Every_core_decider_is_named_in_the_pure_list()
    {
        var foreign = PureDeciders.Where(t => t.Assembly.GetName().Name != "ModelArms.Core").Select(t => t.FullName).ToList();

        foreign.Should().BeEmpty("the pure list names ModelArms.Core's deciders and nothing else");

        var unnamed = CorePublicStaticClasses().Except(PureDeciders).Select(t => t.FullName).ToList();

        unnamed.Should().BeEmpty("every public static class in ModelArms.Core is a decider and must be named in PureDeciders");
        CorePublicStaticClasses().Should().Contain(typeof(ExperimentFile), "a structural scan that matches nothing passes forever");
    }

    /// <summary>D17's real path is the kernel's answer, asked in one place. .NET's own link resolution joins a link's text with its folder and collapses
    /// <c>..</c> as text, so a second site that read links itself would be a second, wrong, answer. The scan reads whole files, across lines.
    /// <para>
    /// What this does NOT prove: that no other API reads a link (<c>Directory.ResolveLinkTarget</c> is matched; a P/Invoke of <c>readlink</c> is not).
    /// </para></summary>
    [Fact]
    public void The_module_reads_links_only_through_RootFacts_RealPathOf()
    {
        var sites = FilesReadingLinks(Path.Combine(Cli.Repository.Root, "tools", "model-arms"));

        sites.Should().BeSubsetOf(["RootFacts.cs"], "the module asks the kernel for a real path in one place; reading a link's text is the defect");
    }

    [Fact]
    public void The_link_scan_still_finds_the_scouts_link_audit()
    {
        FilesReadingLinks(Path.Combine(Cli.Repository.Root, "src")).Should().Contain(
            "FileSystemLinkTargets.cs", "the pattern must still match the one known instance outside the module — a scan that matches nothing passes forever");
    }

    /// <summary>Nothing the module starts is looked up by name: a bare name handed to <c>Process.Start</c> is resolved by the runtime — beside the binary, in the
    /// CURRENT DIRECTORY, then along PATH — so an arm's file in the target could be what answers the question that exists to keep arms' files from running
    /// (measured on 2026-10-29 with marker-writing shims). Every launch names the absolute path the module resolved and judged. The scan reads whole files,
    /// across lines.
    /// <para>
    /// What this does NOT prove: that no other API starts a process (<c>Process.Start</c> itself is not matched), or that a launch by an absolute path is made
    /// under the right environment — only that the two shared launchers are not given a literal name and the git helper that does so is not used.
    /// </para></summary>
    [Fact]
    public void The_module_launches_nothing_by_a_bare_name()
    {
        var sites = FilesLaunchingByName(Path.Combine(Cli.Repository.Root, "tools", "model-arms"));

        sites.Should().BeEmpty("a launch by a literal name is looked up in the cwd before PATH; name the absolute path Resolve found");
    }

    [Fact]
    public void The_bare_name_scan_still_finds_the_fixtures_bare_git()
    {
        FilesLaunchingByName(Path.Combine(Cli.Repository.Root, "tests", "Bench.Tests", "ModelArms")).Should().Contain(
            "ExperimentRoot.cs", "the pattern must still match the one known bare launch — it builds the fixture and is not the subject — or the scan above passes forever");
    }

    /// <summary>Every shape a process is started by that a literal name can ride on: the module's git helper, the shared launcher given a literal, <c>Process.Start</c> and
    /// a <c>ProcessStartInfo</c> built or initialised in place.</summary>
    private static readonly System.Text.RegularExpressions.Regex LaunchByName = new(
        "GitCommand\\.|ProcessRunner\\.\\w+\\(\\s*\"[A-Za-z]|Process\\.Start\\(|new\\s+ProcessStartInfo\\b|ProcessStartInfo\\s*\\{",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    [Theory]
    [InlineData("var git = GitCommand.RunAsync(dir, budget, ct, \"rev-parse\");")]
    [InlineData("var attempt = ProcessRunner.RunAsync(\n    \"git\", args, dir);")]
    [InlineData("using var process = System.Diagnostics.Process.Start(start)!;")]
    [InlineData("var start = new ProcessStartInfo(\"ln\");")]
    [InlineData("var start = new  ProcessStartInfo { FileName = \"ln\" };")]
    [InlineData("var start = new() { };\nProcessStartInfo{ }")]
    public void The_bare_name_pattern_still_matches_each_launch_shape(string fixture)
    {
        LaunchByName.IsMatch(fixture).Should().BeTrue("a pattern that stops matching one of its shapes passes forever for that shape");
    }

    [Fact]
    public void The_bare_name_pattern_does_not_match_a_launch_by_a_resolved_path()
    {
        LaunchByName.IsMatch("ProcessRunner.RunAsync(git, arguments, source, Budget, string.Empty, environment, ct)").Should().BeFalse();
    }

    private static IReadOnlyList<string> FilesLaunchingByName(string folder) =>
        [.. Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(segment => segment is "bin" or "obj"))
            .Where(path => LaunchByName.IsMatch(File.ReadAllText(path)))
            .Select(Path.GetFileName)
            .Cast<string>()
            .Order(StringComparer.Ordinal)];

    private static IReadOnlyList<string> FilesReadingLinks(string folder) =>
        [.. Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(segment => segment is "bin" or "obj"))
            .Where(path => System.Text.RegularExpressions.Regex.IsMatch(File.ReadAllText(path), @"ResolveLinkTarget\s*\(|\.LinkTarget\b"))
            .Select(Path.GetFileName)
            .Cast<string>()
            .Order(StringComparer.Ordinal)];

    private static IReadOnlyList<Type> CorePublicStaticClasses() =>
        [.. Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory, Core)).GetTypes()
            .Where(t => t is { IsAbstract: true, IsSealed: true, IsPublic: true } && !(t.Namespace ?? string.Empty).EndsWith(".Ports", StringComparison.Ordinal))];

    /// <summary>Every project file under <c>src/</c> and <c>hosts/</c>, read from the source tree; build output skipped.</summary>
    private static IReadOnlyList<string> ProductionProjects() =>
        [.. new[] { "src", "hosts" }
            .Select(folder => Path.Combine(Cli.Repository.Root, folder))
            .Where(Directory.Exists)
            .SelectMany(folder => Directory.EnumerateFiles(folder, "*.csproj", SearchOption.AllDirectories))
            .Where(path => !path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(segment => segment is "bin" or "obj"))
            .Order(StringComparer.Ordinal)];

    private static bool NamesTheModule(string project)
    {
        var text = File.ReadAllText(project);
        return text.Contains("ModelArms", StringComparison.Ordinal) || text.Contains("model-arms", StringComparison.Ordinal);
    }

    /// <summary>The product's own assemblies in the output folder: <c>Bench.*.dll</c> and <c>bench.dll</c>, minus this test
    /// assembly, which references the module on purpose.</summary>
    private static IReadOnlyList<string> ProductionAssemblies() =>
        [.. Directory.EnumerateFiles(AppContext.BaseDirectory, "*.dll")
            .Select(Path.GetFileName)
            .Where(name => name is "bench.dll" || name!.StartsWith("Bench.", StringComparison.Ordinal))
            .Where(name => name != "Bench.Tests.dll")
            .Cast<string>()
            .Order(StringComparer.Ordinal)];
}
