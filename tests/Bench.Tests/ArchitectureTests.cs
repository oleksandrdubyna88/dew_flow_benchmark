using System.Reflection;
using FluentAssertions;
using Xunit;

namespace Bench.Tests;

/// <summary>The layer rules, guarded from the first commit so a violation is a red build rather than a
/// review comment nobody makes. The upstream system this replaces had no such guard, and its coupling
/// accumulated exactly where nothing was watching.</summary>
public sealed class ArchitectureTests
{
    [Fact]
    public void Domain_references_nothing_beyond_the_runtime()
    {
        Referenced("Bench.Domain.dll").Should().OnlyContain(
            r => IsRuntime(r),
            "Bench.Domain depends on NOTHING — that is the whole point of the layer");
    }

    [Fact]
    public void Contracts_reference_nothing_beyond_the_runtime()
    {
        Referenced("Bench.Contracts.dll").Should().OnlyContain(
            r => IsRuntime(r),
            "a wire contract that can reference the domain is a contract that leaks it");
    }

    [Fact]
    public void Delivered_references_nothing_beyond_the_runtime()
    {
        // A SIBLING LEAF, not a layer — it may not see Bench.Domain either. That is what keeps the port
        // liftable whole into a package or another repository, and what keeps another product's vocabulary
        // out of this one's domain. Strings in, values out.
        Referenced("Bench.Delivered.dll").Should().OnlyContain(
            r => IsRuntime(r),
            "the delivered-work module is a leaf: it never calls a model, touches a store or reads a file");
    }

    [Fact]
    public void Nothing_but_the_Application_layer_may_reference_the_Delivered_module()
    {
        // The module owns every RULE and the stage owns the IO. A surface reaching past the stage into the
        // module would be a second orchestration, and the recompute property — policy replayed over stored
        // payloads with zero model calls — only holds while there is exactly one.
        foreach (var assembly in new[] { "Bench.Domain.dll", "Bench.Contracts.dll", "Bench.Api.dll", "Bench.Ui.dll" })
        {
            Referenced(assembly).Should().NotContain(
                r => r.Name == "Bench.Delivered",
                $"{assembly} must reach the delivered-work module through the Application layer or not at all");
        }
    }

    [Fact]
    public void Application_does_not_reach_into_Infrastructure()
    {
        Referenced("Bench.Application.dll").Should().NotContain(
            r => r.Name == "Bench.Infrastructure",
            "the Application layer owns the ports; adapters implement them, never the reverse");
    }

    [Fact]
    public void Api_does_not_reach_into_Infrastructure()
    {
        Referenced("Bench.Api.dll").Should().NotContain(
            r => r.Name == "Bench.Infrastructure",
            "an endpoint that knows an adapter is an endpoint that cannot be re-hosted");
    }

    /// <summary>The session analyzer cannot reach a model, and this is what makes that checkable.
    /// <para>
    /// <c>Bench.Domain</c> references nothing beyond the runtime — the first test above says so — which
    /// means a type living there provably cannot call anything, a model runtime included. That is the whole
    /// guarantee behind <c>todo/ai_math/PLAN_math_over_ai.md</c>'s first constraint: an analyzer that asked
    /// a model would inherit its variance into the denominator of every later measurement, and the corpus
    /// would end up measuring its own judge.
    /// </para>
    /// <para>
    /// Naming the types is what keeps it enforced. Moving one of them up into <c>Bench.Application</c> —
    /// where <c>IModelRuntime</c> and <c>IJudge</c> are declared — would quietly make the guarantee
    /// unprovable, and nothing else in this suite would notice.
    /// </para></summary>
    [Fact]
    public void The_session_detectors_live_where_no_model_can_be_reached()
    {
        Type[] analyzer =
        [
            typeof(Domain.Sessions.SessionAnalysis),
            typeof(Domain.Sessions.PhaseClassifier),
            typeof(Domain.Sessions.ToolTaxonomy),
            typeof(Domain.Sessions.CommandClassifier),
            typeof(Domain.Sessions.ToolTarget),
        ];

        analyzer.Should().OnlyContain(
            type => type.Assembly.GetName().Name == "Bench.Domain",
            "the detectors are deterministic BY CONSTRUCTION — a layer that depends on nothing cannot ask "
            + "a model, and that is cheaper to guarantee than to remember");
    }

    /// <summary>Every gate type that DECIDES anything lives in <c>Bench.Domain</c> — the same argument as the
    /// session detectors: a layer that depends on nothing cannot reach a model, a store or a file, so a suite
    /// stamp, a matrix order, a claim, a pin comparison, a report figure and the one vendor-row producer are
    /// pure by construction. Naming them is what keeps that enforced; moving one up into the Application
    /// layer would quietly make the guarantee unprovable.</summary>
    [Fact]
    public void The_gate_decisions_live_where_no_model_can_be_reached()
    {
        Type[] deciders =
        [
            typeof(Domain.Runs.Claimable),
            typeof(Domain.Runs.SlotRotation),
            typeof(Domain.Gate.GateSuite),
            typeof(Domain.Gate.GateMatrix),
            typeof(Domain.Gate.GateCellLifecycle),
            typeof(Domain.Gate.ProductPin),
            typeof(Domain.Gate.ReviewerDefinition),
            typeof(Domain.Gate.ReviewerEndpoint),
            typeof(Domain.Gate.CoaiVendorRow),
            typeof(Domain.Gate.CoaiVendorsSetting),
            typeof(Domain.Gate.GatePopulation),
            typeof(Domain.Gate.RepositoryRelative),
            typeof(Domain.Gate.FileHashKey),
            typeof(Domain.Gate.PythonRound),
            typeof(Domain.Gate.GateRunFacts),
            typeof(Domain.Gate.FailureCauses),
            typeof(Domain.Gate.GateVerdict),
            typeof(Domain.Gate.RubricCatalog),
            typeof(Domain.Gate.GateReport),
            typeof(Domain.Gate.Quantile),
            typeof(Domain.Gate.SeedEvidence),
            typeof(Domain.Gate.CellPaths),
            typeof(Domain.Gate.ArtifactPath),
            typeof(Domain.Gate.ArtifactRef),
            typeof(Domain.Gate.PublicationGuard),
            typeof(Domain.Gate.FailureRedaction),
            typeof(Domain.Gate.BlindExport),
            typeof(Domain.Gate.AssessorOutput),
            typeof(Domain.Gate.AssessmentPending),
            typeof(Domain.Gate.VendorFamily),
            typeof(Domain.Gate.HandCheckGate),
            typeof(Domain.Gate.HandCheckAnswers),
        ];

        deciders.Should().OnlyContain(
            type => type.Assembly.GetName().Name == "Bench.Domain",
            "a gate decision made where a model or a store is reachable is a decision nobody can replay");
    }

    /// <summary>The gate's two storage PORTS are declared in the Application layer and implemented in Infrastructure —
    /// the rule every other port here follows — and nothing in the domain knows EF exists (the first test in this file
    /// says the domain references nothing; this names the gate's own pair so moving one is a red build).</summary>
    [Fact]
    public void The_gate_storage_ports_live_in_the_application_layer_and_their_adapters_in_infrastructure()
    {
        typeof(global::Bench.Application.Gate.IGateStore).Assembly.GetName().Name.Should().Be("Bench.Application");
        typeof(global::Bench.Application.Gate.IGateArtifactStore).Assembly.GetName().Name.Should().Be("Bench.Application");
        typeof(global::Bench.Infrastructure.Persistence.PostgresGateStore).Should().Implement<global::Bench.Application.Gate.IGateStore>();
        typeof(global::Bench.Infrastructure.Gate.FileSystemGateArtifactStore).Should().Implement<global::Bench.Application.Gate.IGateArtifactStore>();
        typeof(global::Bench.Application.Gate.IGateVerdictStore).Assembly.GetName().Name.Should().Be("Bench.Application");
        typeof(global::Bench.Application.Gate.IGateAssessmentFiles).Assembly.GetName().Name.Should().Be("Bench.Application");
        typeof(global::Bench.Infrastructure.Persistence.PostgresGateVerdictStore).Should().Implement<global::Bench.Application.Gate.IGateVerdictStore>();
        typeof(global::Bench.Infrastructure.Gate.FileSystemGateAssessmentFiles).Should().Implement<global::Bench.Application.Gate.IGateAssessmentFiles>();
        typeof(Domain.Gate.GateRun).Assembly.GetReferencedAssemblies().Should().NotContain(
            r => r.Name!.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal), "the gate domain stays free of EF");
    }

    /// <summary>The console is mounted by another repository's Blazor WebAssembly host, so a reference here is
    /// a reference there: the domain would pull rules into a browser, and infrastructure would fail to load
    /// at all. The gate pages (E6) must not be the ones to break this.</summary>
    [Fact]
    public void Ui_references_the_contracts_and_nothing_else_of_this_solution()
    {
        Referenced("Bench.Ui.dll").Where(r => r.Name!.StartsWith("Bench.", StringComparison.Ordinal))
            .Select(r => r.Name)
            .Should().BeEquivalentTo(["Bench.Contracts"],
                "the console may reference the wire shapes and NOTHING else — it runs in a browser somebody else hosts");
    }

    /// <summary>The vendor list the product reads is produced in exactly ONE place, and this is what makes that
    /// a fact rather than a convention: a second spelling of the literal is a second producer, and two producers
    /// of one request drift until a run measures a vendor nobody configured.</summary>
    [Fact]
    public void The_vendors_setting_is_spelled_in_exactly_one_production_file()
    {
        var spelled = ProductionFilesSpelling("COAI_VENDORS");

        spelled.Should().ContainSingle(
            "a recipe becomes a request in exactly one place — every other file takes a CoaiVendorsSetting value, "
            + $"and the offenders are: {string.Join(", ", spelled)}");
    }

    /// <summary>The companion the scan above needs: a tree scan that matches nothing passes forever, so this
    /// asserts it still finds the sanctioned producer by path.</summary>
    [Fact]
    public void The_vendors_scan_still_finds_the_sanctioned_producer()
    {
        ProductionFilesSpelling("COAI_VENDORS").Should().Contain(
            Path.Combine("src", "Bench.Domain", "Gate", "CoaiVendorsSetting.cs"),
            "the scan is only a guard while it can see the one file that is allowed to spell the literal");
    }

    /// <summary>The literal scan's planted negative: a host file that writes the variable by hand is found by
    /// path. A fixed directory per scenario, removed afterwards, so runs reuse one artefact.</summary>
    [Fact]
    public void A_planted_host_file_spelling_the_variable_is_found()
    {
        var root = Path.Combine(Path.GetTempPath(), "bench-arch-planted-vendors-literal");
        Directory.CreateDirectory(Path.Combine(root, "hosts", "Planted"));
        File.WriteAllText(Path.Combine(root, "hosts", "Planted", "Launch.cs"), "env[\"COAI_VENDORS\"] = otherJson;");

        try
        {
            FilesSpelling(root, "COAI_VENDORS").Should().Equal([Path.Combine("hosts", "Planted", "Launch.cs")]);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>The one-producer rule as a TYPE fact, over every production assembly that can see the domain: the
    /// setting has no constructor but a private one, and nothing anywhere RETURNS a setting except its one factory.
    /// A forwarding helper, an <c>internal</c> "sealer", a second public factory — each is a second producer, and
    /// each is named here by type and member.</summary>
    [Fact]
    public void Nothing_but_the_one_factory_can_produce_a_vendors_setting()
    {
        string.Join(", ", SettingProducers(ProductionAssemblies())).Should().BeEmpty(
            $"the only way to a CoaiVendorsSetting is {SanctionedProducer}; everything else takes the value");
    }

    [Fact]
    public void The_producer_scan_still_sees_the_sanctioned_factory()
    {
        SettingMembers(ProductionAssemblies()).Should().Contain(SanctionedProducer,
            "a reflection scan that sees nothing passes forever — it must find the one factory it allows");
    }

    [Fact]
    public void A_planted_forwarding_factory_is_found_by_the_producer_scan()
    {
        SettingProducers([typeof(ArchitectureTests).Assembly]).Should().Equal(["PlantedVendorsForwarder.Forward"],
            "a helper that hands back a setting it did not build is still a door a second producer walks through");
    }

    /// <summary>The variable's NAME is the setting's own: exposed, it let a host write <c>env[name] = anything</c>
    /// beside the value the one producer made. No non-private string constant or static field in production
    /// code may hold it.</summary>
    [Fact]
    public void No_production_assembly_exposes_the_variable_name()
    {
        ExposedLiteral(ProductionAssemblies(), "COAI_VENDORS").Should().BeEmpty(
            "the setting applies itself to an environment — nobody else needs, or may have, the name");
        ExposedLiteral([typeof(ArchitectureTests).Assembly], "COAI_VENDORS").Should().Equal(["PlantedVendorsForwarder.Name"],
            "the planted constant proves the literal scan can see a public const at all");
    }

    private const string SanctionedProducer = "CoaiVendorsSetting.From";

    private const BindingFlags Every = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    /// <summary>Every assembly of this solution that can reference the domain, and so could hold a producer.</summary>
    private static IReadOnlyList<Assembly> ProductionAssemblies() =>
        [.. new[] { "Bench.Domain", "Bench.Application", "Bench.Infrastructure", "Bench.Api", "bench" }
            .Select(name => Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory, $"{name}.dll")))];

    private static IReadOnlyList<string> SettingProducers(IReadOnlyList<Assembly> assemblies) =>
        [.. SettingMembers(assemblies).Where(member => member != SanctionedProducer)];

    /// <summary>Every member that makes or hands back a setting: a non-private constructor of the type, or a
    /// method whose return type is the setting or an outcome of one. The compiler's own <c>&lt;Clone&gt;$</c> (a
    /// copy of a value somebody already produced) and the lambdas inside the sanctioned factory's body are
    /// excluded — both are the one producer, not another.</summary>
    private static IReadOnlyList<string> SettingMembers(IReadOnlyList<Assembly> assemblies)
    {
        var setting = typeof(Domain.Gate.CoaiVendorsSetting);

        return [.. assemblies.SelectMany(a => a.GetTypes())
            .SelectMany(type => type.GetMethods(Every).Cast<MethodBase>().Concat(type.GetConstructors(Every)))
            .Where(member => Produces(member, setting) && !IsTheFactorysOwnMachinery(member, setting))
            .Select(member => $"{member.DeclaringType!.Name}.{member.Name}")
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];
    }

    private static bool Produces(MethodBase member, Type setting) => member switch
    {
        ConstructorInfo ctor => ctor.DeclaringType == setting && !ctor.IsPrivate,
        MethodInfo method => method.ReturnType == setting || method.ReturnType == typeof(Domain.Outcome<>).MakeGenericType(setting),
        _ => false,
    };

    private static bool IsTheFactorysOwnMachinery(MethodBase member, Type setting) =>
        member.Name == "<Clone>$"
        || member.Name.StartsWith('<') && IsWithin(member.DeclaringType!, setting);

    private static bool IsWithin(Type type, Type outer) =>
        type == outer || type.DeclaringType is { } parent && IsWithin(parent, outer);

    /// <summary><c>Type.Field</c> of every non-private string CONSTANT holding <paramref name="literal"/>. Constants
    /// only: reading a static field's value would run type initialisers across every assembly, and a scan must
    /// not be the thing that has side effects.</summary>
    private static IReadOnlyList<string> ExposedLiteral(IReadOnlyList<Assembly> assemblies, string literal) =>
        [.. assemblies.SelectMany(a => a.GetTypes())
            .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(field => field.IsLiteral && !field.IsPrivate && Equals(field.GetRawConstantValue(), literal))
            .Select(field => $"{field.DeclaringType!.Name}.{field.Name}")
            .Order(StringComparer.Ordinal)];

    /// <summary>Repository-relative paths of the production files whose text contains <paramref name="literal"/>.
    /// Reads <c>src/</c> and <c>hosts/</c> from the source tree; build output is skipped, because a scan over
    /// generated files would find the literal in every assembly's copied resources.</summary>
    private static IReadOnlyList<string> ProductionFilesSpelling(string literal) => FilesSpelling(Cli.Repository.Root, literal);

    private static IReadOnlyList<string> FilesSpelling(string root, string literal) =>
        [.. new[] { "src", "hosts" }
            .Select(folder => Path.Combine(root, folder))
            .Where(Directory.Exists)
            .SelectMany(folder => Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories))
            .Where(path => !path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(segment => segment is "bin" or "obj"))
            .Where(path => File.ReadAllText(path).Contains(literal, StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(root, path))
            .Order(StringComparer.Ordinal)];

    private static IEnumerable<AssemblyName> Referenced(string fileName) =>
        Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory, fileName)).GetReferencedAssemblies();

    private static bool IsRuntime(AssemblyName name) =>
        name.Name!.StartsWith("System", StringComparison.Ordinal)
        || name.Name == "netstandard"
        || name.Name == "mscorlib";
}

/// <summary>PLANTED — the two shapes of a second producer the review found open: a helper that hands back a
/// setting, and a public constant naming the variable. Lives in the test assembly so the scans above can prove
/// they see both; nothing calls it.</summary>
internal static class PlantedVendorsForwarder
{
    public const string Name = "COAI_VENDORS";

    public static Domain.Gate.CoaiVendorsSetting Forward(Domain.Gate.CoaiVendorsSetting setting) => setting;
}
