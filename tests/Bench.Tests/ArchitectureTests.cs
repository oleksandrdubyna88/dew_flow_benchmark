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
            typeof(Domain.Gate.GateMatrix),
            typeof(Domain.Gate.GateCellLifecycle),
            typeof(Domain.Gate.ProductPin),
        ];

        deciders.Should().OnlyContain(
            type => type.Assembly.GetName().Name == "Bench.Domain",
            "a gate decision made where a model or a store is reachable is a decision nobody can replay");
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
            Path.Combine("src", "Bench.Domain", "Gate", "CoaiVendorRow.cs"),
            "the scan is only a guard while it can see the one file that is allowed to spell the literal");
    }

    /// <summary>Repository-relative paths of the production files whose text contains <paramref name="literal"/>.
    /// Reads <c>src/</c> and <c>hosts/</c> from the source tree; build output is skipped, because a scan over
    /// generated files would find the literal in every assembly's copied resources.</summary>
    private static IReadOnlyList<string> ProductionFilesSpelling(string literal) =>
        [.. new[] { "src", "hosts" }
            .SelectMany(folder => Directory.EnumerateFiles(Path.Combine(Cli.Repository.Root, folder), "*.cs", SearchOption.AllDirectories))
            .Where(path => !path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(segment => segment is "bin" or "obj"))
            .Where(path => File.ReadAllText(path).Contains(literal, StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(Cli.Repository.Root, path))
            .Order(StringComparer.Ordinal)];

    private static IEnumerable<AssemblyName> Referenced(string fileName) =>
        Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory, fileName)).GetReferencedAssemblies();

    private static bool IsRuntime(AssemblyName name) =>
        name.Name!.StartsWith("System", StringComparison.Ordinal)
        || name.Name == "netstandard"
        || name.Name == "mscorlib";
}
