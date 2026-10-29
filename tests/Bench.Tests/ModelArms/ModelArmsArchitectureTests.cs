using System.Reflection;
using FluentAssertions;
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

        // E1.S6

        // E1.S7

        // E1.S8

        // E1.S9

        // E1.S10

        // E1.S11

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

        var naming = projects.Where(NamesTheModule).Select(path => Path.GetRelativePath(Cli.Repository.Root, path).Replace('\\', '/')).ToList();
        naming.Should().BeEmpty("the product does not know the module exists; only the module knows the product");

        var assemblies = ProductionAssemblies();
        assemblies.Should().Contain("Bench.Domain.dll", "the assembly scan must still see a known product assembly");

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
    }

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
