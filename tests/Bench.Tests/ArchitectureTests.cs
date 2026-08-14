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

    private static IEnumerable<AssemblyName> Referenced(string fileName) =>
        Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory, fileName)).GetReferencedAssemblies();

    private static bool IsRuntime(AssemblyName name) =>
        name.Name!.StartsWith("System", StringComparison.Ordinal)
        || name.Name == "netstandard"
        || name.Name == "mscorlib";
}
