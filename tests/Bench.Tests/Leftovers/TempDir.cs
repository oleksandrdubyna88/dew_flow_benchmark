using Bench.Tests.Leftovers;

namespace Bench.Tests;

/// <summary>
/// The scratch folder the tests of E6 S3-S6 use: <c>using var work = TempDir.For("thing");</c> — a thin, disposable wrapper over <see cref="OwnedDir"/>.
/// The folder is <c>testsToDelete-&lt;owner&gt;-bench-&lt;what&gt;-&lt;id&gt;</c>, claimed first (its claim lies beside it), and it is removed with its claim when the wrapper is disposed.
/// <para>Shape: <c>dew_flow_connect_other_ais</c> <c>src_mcp/tests_leftovers/TempDir.cs</c> at <c>e060d77e</c> (<c>For</c>, <c>Path</c>, <c>Root</c>, <c>At</c>, the string conversion), built on
/// <see cref="OwnedDir"/> instead of <c>CreateTempSubdirectory</c>: benchmark's helper is the adapted copy of <c>dew_flow_mcp</c>, which makes the owned folder (plan D5: one owned folder per helper,
/// never a second top-level folder).</para>
/// <para><b>It never throws on the way out</b> (<see cref="OwnedDir.Dispose"/> never does): a folder that could not be removed stays claimed, with one warning, and the end-of-run audit names it.
/// <paramref name="what"/> obeys <c>Owned.CheckWhat</c> (a length budget, no separators).</para>
/// </summary>
public sealed class TempDir : IDisposable
{
    private readonly OwnedDir _owned;

    private TempDir(OwnedDir owned) => _owned = owned;

    /// <summary>Where it is. Also what an implicit conversion to <see cref="string"/> gives you.</summary>
    public string Path => _owned.Path;

    /// <summary>The temp root, as the helper's one door gives it: the same string the owned folders are made in.</summary>
    public static string Root => Owned.TempRoot();

    /// <summary>A fresh claimed folder, named for the test that wanted it.</summary>
    public static TempDir For(string what) => new(OwnedDir.Create(what));

    /// <summary>So it can be passed anywhere a path is taken, without <c>.Path</c> at every call.</summary>
    public static implicit operator string(TempDir directory) => directory.Path;

    /// <summary>A path inside it.</summary>
    public string At(params string[] parts) => System.IO.Path.Combine([Path, .. parts]);

    public void Dispose() => _owned.Dispose();
}
