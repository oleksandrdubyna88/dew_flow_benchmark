using System.Globalization;
using System.Text.Json.Nodes;
using ModelArms.Adapters;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>One path a pilot scaffold names: its <paramref name="Name"/>, whether the scaffold CREATES it (<paramref name="Created"/>; <c>root</c> is only a name, <c>init</c> makes it), and whether the scaffold makes it as a plain folder (<paramref name="PlainFolder"/>; the target is made by its own builder).</summary>
internal sealed record PilotEntry(string Name, bool Created, bool PlainFolder = false);

/// <summary>What one scaffold run claimed: its id, the manifest, and every entry with its exact path. The catalogue is the ONE list: the manifest names every entry, the creation loop makes the plain folders, the teardown removes only what this run created, and the kill test reads the same names.</summary>
internal sealed record PilotRun(string Pilot, string Id, string Uid, string Manifest, IReadOnlyList<(PilotEntry Entry, string Path)> Paths)
{
    public string PathOf(string name) => Paths.Single(each => each.Entry.Name == name).Path;

    /// <summary>The paths this run creates (never a name that is only named), in order.</summary>
    public IReadOnlyList<string> CreatedPaths => [.. Paths.Where(each => each.Entry.Created).Select(each => each.Path)];

    /// <summary>The plain folders of the catalogue, by name.</summary>
    public IEnumerable<string> PlainFolders => Paths.Where(each => each.Entry.PlainFolder).Select(each => each.Path);
}

/// <summary>The manifest of an attended pilot's scaffold (row E6-l of <c>todo/PLAN_test_leftovers_E6.md</c>): <c>bench-&lt;pilot&gt;-manifest-&lt;id&gt;.json</c> in the operator's folder, claimed exclusively FIRST, naming every exact path before any exists; a path that already exists refuses the run (nothing a scaffold did not make is ever removed);
/// the teardown on failure removes only what this run created, children first, and keeps the manifest, naming it, when any child could not be removed.
/// <para>
/// What this does NOT prove: removal of a path another user owns or one on a file system that refuses <c>chmod</c> (it comes back as left, and the manifest stays).
/// </para></summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
internal static class PilotManifest
{
    /// <summary>Claims the manifest, builds every path, refuses if any already exists (the claim is removed again: it is this run's own file), and writes the manifest. <paramref name="nextId"/> is the seam for the id (default: eight hex characters of a new guid); a manifest name already taken draws a new id.</summary>
    internal static async Task<PilotRun> StartAsync(string output, string pilot, IReadOnlyList<PilotEntry> catalogue, Func<string>? nextId = null)
    {
        nextId ??= () => Guid.NewGuid().ToString("N")[..8];
        var (id, manifest) = Claim(output, pilot, nextId);
        var run = new PilotRun(pilot, id, RootFacts.CurrentUid().ToString(CultureInfo.InvariantCulture), manifest, [.. catalogue.Select(entry => (entry, Path.Combine(output, $"bench-{pilot}-{entry.Name}-{id}")))]);
        try
        {
            if (Existing(run) is { } taken)
            {
                throw new InvalidOperationException($"{taken} already exists: the scaffold makes nothing and removes nothing it did not make — name another BENCH_PILOT_OUT or remove it");
            }

            await WriteAsync(run);
        }
        catch
        {
            ScaffoldCleanup.Remove(manifest);
            throw;
        }

        return run;
    }

    /// <summary>The failure teardown: the paths this run created, each removed by <paramref name="remove"/> (default <see cref="ScaffoldCleanup.Remove"/>, which never throws), then the manifest ONLY when every child is gone. Null when all went; otherwise what to say: what was left and, when a child is, the manifest's path.</summary>
    internal static string? CleanUp(PilotRun run, Func<string, IReadOnlyList<string>>? remove = null)
    {
        remove ??= ScaffoldCleanup.Remove;
        List<string> left = [.. run.CreatedPaths.SelectMany(remove)];
        if (left.Count > 0)
        {
            return $"cleanup left (remove by hand): {string.Join(", ", left)}; the manifest {run.Manifest} is kept and names every path";
        }

        return remove(run.Manifest) is { Count: > 0 } manifestLeft ? $"cleanup left (remove by hand): {string.Join(", ", manifestLeft)}" : null;
    }

    private static (string Id, string Path) Claim(string output, string pilot, Func<string> nextId)
    {
        while (true)
        {
            var id = nextId();
            var path = Path.Combine(output, $"bench-{pilot}-manifest-{id}.json");
            try
            {
                using var created = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                return (id, path);
            }
            catch (IOException) when (File.Exists(path))
            {
            }
        }
    }

    /// <summary>The first path of the run that is already there (a link, even a broken one, counts), or null.</summary>
    private static string? Existing(PilotRun run) =>
        run.Paths.Select(each => each.Path).FirstOrDefault(path => (int)new FileInfo(path).Attributes != -1);

    private static async Task WriteAsync(PilotRun run)
    {
        var entries = new JsonArray([.. run.Paths.Select(each => (JsonNode?)new JsonObject { ["name"] = each.Entry.Name, ["path"] = each.Path }), (JsonNode?)new JsonObject { ["name"] = "manifest", ["path"] = run.Manifest }]);
        await File.WriteAllTextAsync(run.Manifest, new JsonObject { ["id"] = run.Id, ["uid"] = run.Uid, ["paths"] = entries }.ToJsonString(M2PilotScaffold.Pretty) + "\n", TestContext.Current.CancellationToken);
    }
}
