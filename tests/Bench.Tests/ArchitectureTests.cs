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

    private static IEnumerable<AssemblyName> Referenced(string fileName) =>
        Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory, fileName)).GetReferencedAssemblies();

    private static bool IsRuntime(AssemblyName name) =>
        name.Name!.StartsWith("System", StringComparison.Ordinal)
        || name.Name == "netstandard"
        || name.Name == "mscorlib";
}
