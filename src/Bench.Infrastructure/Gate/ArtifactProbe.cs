namespace Bench.Infrastructure.Gate;

/// <summary>The steps of the artefact store's two crash-sensitive protocols, in the order they happen.</summary>
public enum ArtifactStep
{
    /// <summary>The bytes are in the staging file; not yet flushed.</summary>
    Staged,

    /// <summary>The staging file is flushed to the disk.</summary>
    Flushed,

    /// <summary>SHA-256 and length are computed; the file is still under its staging name.</summary>
    Hashed,

    /// <summary>The file is under its real name.</summary>
    Renamed,

    /// <summary>Prune: a call's facts file was flushed to disk and its release was logged; no body deleted yet.</summary>
    FactsDurable,

    /// <summary>Prune: one body was deleted.</summary>
    BodyDeleted,
}

/// <summary>Where a test stands in for a process dying. Production passes <see cref="None"/>, which does nothing;
/// a test overrides <see cref="Reached"/> to throw at one step and then inspects what the disk and the database
/// were left holding — which is the only way "a crash at any step leaves a recoverable state" is a checked claim
/// rather than a hope.</summary>
public class ArtifactProbe
{
    public static ArtifactProbe None { get; } = new();

    public virtual void Reached(ArtifactStep step)
    {
    }
}
