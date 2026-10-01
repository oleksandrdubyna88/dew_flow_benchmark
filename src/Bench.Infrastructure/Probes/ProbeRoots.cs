using Bench.Domain;
using Bench.Infrastructure.Gate;

namespace Bench.Infrastructure.Probes;

/// <summary>The two roots a probe verb writes under, checked together before anything runs.
/// <list type="bullet">
/// <item>The ARTEFACT root holds the answers, stdout and stderr the write-up cites — the gate's root, opened through
/// <see cref="FileSystemGateArtifactStore.Open"/> so it is refused inside a git checkout for the gate's reason.</item>
/// <item>The WORK root holds the per-attempt fixtures (<c>cwd/</c>, <c>outside/canary.txt</c>) and is wiped per run on every verb's
/// entry (finding 3). It is refused inside a git checkout too: a CLI started in a fixture there would read the repository's own
/// instruction files (a <c>CLAUDE.md</c> above its working directory) and measure them along with itself.</item>
/// <item>The two must be DISJOINT. Both use the same relative layout <c>probes/&lt;run&gt;/&lt;cell&gt;/…</c>, and the work root's
/// cleanup deletes every cell folder no live owner holds — over the artefact root, that is the evidence.</item>
/// </list></summary>
public static class ProbeRoots
{
    /// <summary>Where the fixtures live when <c>--work-root</c> is not given — beside the CLI's checkout root, outside every repository.</summary>
    public static string DefaultWorkRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "bench", "probes-work");

    /// <summary>The canonical artefact root and work root, both created, or the refusal that names the rule broken.</summary>
    public static Outcome<(string Artifacts, string Work)> Check(string artifactRoot, string workRoot) =>
        FileSystemGateArtifactStore.Open(artifactRoot, TimeProvider.System, ArtifactProbe.None).Match(
            artifacts => Work(workRoot).Match(
                work => Disjoint(artifacts.Root, work),
                Outcome<(string, string)>.Failure),
            Outcome<(string, string)>.Failure);

    /// <summary>The work root alone — what <c>bench probes sweep</c> needs when it is given no artefact root.</summary>
    public static Outcome<string> Work(string workRoot)
    {
        if (string.IsNullOrWhiteSpace(workRoot))
        {
            return Outcome<string>.Failure("the probes need a work root for their fixtures — pass --work-root");
        }

        return ArtifactContainment.Resolve(workRoot).Match(
            canonical => FileSystemGateArtifactStore.GitCheckoutAbove(canonical) is { Length: > 0 } checkout
                ? Outcome<string>.Failure(
                    $"the work root {canonical} is inside the git checkout at {checkout} — a CLI started in a fixture there would read that "
                    + "repository's instruction files and measure them with itself; choose a root outside every repository")
                : Created(canonical),
            Outcome<string>.Failure);
    }

    private static Outcome<string> Created(string canonical)
    {
        Directory.CreateDirectory(canonical);
        return Outcome<string>.Success(canonical);
    }

    private static Outcome<(string, string)> Disjoint(string artifacts, string work) =>
        ArtifactContainment.IsWithin(artifacts, work) || ArtifactContainment.IsWithin(work, artifacts)
            ? Outcome<(string, string)>.Failure(
                $"the work root {work} and the artefact root {artifacts} overlap — the fixture cleanup deletes every cell folder under the "
                + "work root's probes/<run>/, and over the artefact root that is the evidence; give them two separate folders")
            : Outcome<(string, string)>.Success((artifacts, work));
}
