using System.Security.Cryptography;
using Bench.Application.Probes;
using Bench.Domain;
using Bench.Domain.Probes;
using Bench.Infrastructure.Gate;

namespace Bench.Infrastructure.Probes;

/// <summary>The probes' fixtures on the local filesystem under ONE work root — <see cref="IProbeFixtures"/>' adapter.
/// <para>
/// A fresh directory and two fresh random tokens per attempt (D7): a token that repeats can be remembered, and two attempts
/// of one cell must share nothing. <see cref="DeleteStranded"/> is the cleanup finding 3 asked for: keyed to the DIRECTORY,
/// not to the sweep — every cell folder under <c>probes/&lt;run&gt;/</c> whose cell is not Claimed by a live owner on this host
/// goes, Pending cells included. Its scope is a security property: the run's root is resolved on the real filesystem (links
/// included) and must lie under the work root; a cell folder that is a link is removed as ITSELF, never followed; a tree
/// that does not resolve under the run's root is left alone.
/// </para></summary>
public sealed class ProbeFixtures(string workRoot) : IProbeFixtures
{
    private readonly string _root = Path.GetFullPath(workRoot);

    /// <summary>The canonical work root every fixture lives under.</summary>
    public string Root => _root;

    public Outcome<ProbeFixture> Begin(ProbeKind probe, ProbeAttemptScope scope)
    {
        var relative = ProbePaths.AttemptRoot(scope);
        var root = Absolute(relative);

        if (Directory.Exists(root))
        {
            return Outcome<ProbeFixture>.Failure($"{relative} already exists — an attempt's fixture is made once; a leftover is the entry step's to delete, never continued");
        }

        var tokens = Tokens();
        var cwd = Path.Combine(root, ProbePaths.CwdFolder);
        var outside = Path.Combine(root, ProbePaths.OutsideFolder);
        var inside = Path.Combine(cwd, ProbePaths.InsideFile);
        var canary = Path.Combine(outside, ProbePaths.CanaryFile);

        Directory.CreateDirectory(cwd);
        Directory.CreateDirectory(outside);
        Write(ProbePaths.Layout(probe), inside, canary, tokens);

        return Outcome<ProbeFixture>.Success(new ProbeFixture(root, cwd, inside, outside, canary, tokens));
    }

    private static void Write(ProbeFixtureLayout layout, string inside, string canary, ProbeTokens tokens)
    {
        if (layout.Inside)
        {
            File.WriteAllText(inside, tokens.Inside + "\n");
        }

        if (layout.Outside)
        {
            File.WriteAllText(canary, tokens.Outside + "\n");
        }
    }

    /// <summary>Twelve hex characters from the system's random generator behind a prefix that says which file it is —
    /// long enough that no answer finds one by accident, and the two can never contain each other.</summary>
    private static ProbeTokens Tokens() =>
        ProbeTokens.Of("IN-" + Hex(), "OUT-" + Hex()).Match(t => t, reason => throw new InvalidOperationException($"the generated tokens were refused — {reason}"));

    private static string Hex() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6));

    /// <summary>Removes the attempt's root, then the generation and cell folders above it when they are left empty — so a settled
    /// cell leaves no husk for <see cref="DeleteStranded"/> to count. The run's root stays: other lanes are creating under it.</summary>
    public void Delete(ProbeFixture fixture)
    {
        if (!Directory.Exists(fixture.Root) || !ProbeTrees.IsUnder(fixture.Root, _root))
        {
            return;
        }

        if (ProbeTrees.TryDeleteTree(fixture.Root))
        {
            PruneEmptyParents(fixture.Root, levels: 2);
        }
    }

    private static void PruneEmptyParents(string path, int levels)
    {
        var parent = Path.GetDirectoryName(path);

        for (var level = 0; level < levels && parent is not null; level++, parent = Path.GetDirectoryName(parent))
        {
            try
            {
                if (!Directory.Exists(parent) || Directory.EnumerateFileSystemEntries(parent).Any())
                {
                    return;
                }

                Directory.Delete(parent);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return; // another attempt of the cell is being made beside it, or the OS still holds it — leave it
            }
        }
    }

    public int DeleteStranded(Guid runId, IReadOnlySet<Guid> liveCells)
    {
        var runRoot = Absolute(ProbePaths.RunRoot(runId));

        if (!Directory.Exists(runRoot) || !ProbeTrees.IsUnder(runRoot, _root))
        {
            return 0;
        }

        return Directory.EnumerateDirectories(runRoot).Count(folder => !IsLive(folder, liveCells) && ProbeTrees.Remove(folder, runRoot));
    }

    private static bool IsLive(string folder, IReadOnlySet<Guid> liveCells) =>
        ProbePaths.CellOf(Path.GetFileName(folder)) is Outcome<Guid>.Ok { Value: var cell } && liveCells.Contains(cell);

    private string Absolute(Bench.Domain.Gate.ArtifactPath path) => Path.Combine([_root, .. path.Segments]);
}
