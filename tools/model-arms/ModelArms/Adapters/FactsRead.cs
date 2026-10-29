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

    /// <summary>The same answer when the file is of the <paramref name="expected"/> kind; otherwise the file is refused naming what it is. A FIFO read as a
    /// file blocks until someone writes to it, and a folder read as a file is an access error: neither is a root or a file the harness may take.</summary>
    public FactsRead MustBe(FileKind expected) =>
        this is Read { Facts: { } facts } && facts.Identity.Kind != expected
            ? new Refused($"{facts.Path} is {Describe(facts.Identity.Kind)}, not {Describe(expected)} — refused (D17)")
            : this;

    private static string Describe(FileKind kind) =>
        kind switch
        {
            FileKind.Regular => "a regular file",
            FileKind.Directory => "a directory",
            FileKind.Link => "a symbolic link",
            FileKind.Fifo => "a FIFO",
            FileKind.Socket => "a socket",
            FileKind.CharacterDevice => "a character device",
            FileKind.BlockDevice => "a block device",
            _ => "a file of a type stat did not name",
        };

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
