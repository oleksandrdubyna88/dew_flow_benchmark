using Bench.Domain;
using ModelArms.Core.Experiment;

namespace ModelArms.Adapters;

/// <summary>What one read of a trust path came to. Three states, never two: the host <see cref="Unread"/> (a <c>stat</c> that could not run or whose
/// answer could not be read: the environment is not ready, exit 3) is not the path <see cref="Refused"/> (a symbolic link, a NUL: the operator's file
/// is wrong, exit 4), and neither is <see cref="Read"/>.</summary>
public abstract record FactsRead
{
    private FactsRead() { }

    /// <summary>The facts of the file itself; <see cref="FileFacts.Path"/> is the normalised path whose owner and mode were read.</summary>
    public sealed record Read(FileFacts Facts) : FactsRead;

    /// <summary>The path is not one the trust check judges; the reason names it.</summary>
    public sealed record Refused(string Reason) : FactsRead;

    /// <summary>The host could not say; the reason is the tool's.</summary>
    public sealed record Unread(string Reason) : FactsRead;

    /// <summary>The older two-state shape, for the callers that do not care which way it failed.</summary>
    public Outcome<FileFacts> ToOutcome() =>
        this switch
        {
            Read read => Outcome<FileFacts>.Success(read.Facts),
            Refused refused => Outcome<FileFacts>.Failure(refused.Reason),
            Unread unread => Outcome<FileFacts>.Failure(unread.Reason),
            _ => throw new InvalidOperationException("unreachable"),
        };
}
