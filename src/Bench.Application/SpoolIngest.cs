using Bench.Domain.Telemetry;

namespace Bench.Application;

/// <summary>One line this build did not take, and whether anything ever could.
/// <para>
/// <paramref name="Retryable"/> decides the FILE's fate, not just the line's: a spool holding a record
/// written by a newer emitter must survive until a build that understands it runs, while a spool whose
/// only fault is a half-written last line — the normal shape of a killed emitter — has nothing left to
/// give and can be retired.
/// </para></summary>
public sealed record SpoolRefusal(int Line, string Reason, bool Retryable)
{
    public override string ToString() => $"line {Line}: {Reason}";
}

/// <summary>One bounded batch of a spool: what it yielded, and what it refused.</summary>
public sealed record SpoolChunk(IReadOnlyList<ToolTelemetry> Records, IReadOnlyList<SpoolRefusal> Refused);

/// <summary>Turns a spool file's text into records, without touching a disk or a database.
/// <para>
/// Pure on purpose: everything that decides what a spool MEANS — which lines are readable, which are
/// refused, and whether the file may be retired — is testable without a filesystem, and the host is
/// left with nothing but the IO.
/// </para></summary>
public static class SpoolIngest
{
    /// <summary>Reads every line, keeping the readable records and the reasons the rest were refused.
    /// <para>
    /// A bad line costs that line and nothing more. A spool is written by a process that can be killed
    /// mid-write, so the last line of a file is routinely half a record — a reader that aborts on the
    /// first failure would discard a whole run's telemetry over its final byte.
    /// </para></summary>
    public static (IReadOnlyList<ToolTelemetry> Records, IReadOnlyList<SpoolRefusal> Refused) Read(string text) =>
        Read(NumberedLines(text));

    /// <summary>The same reading, in BOUNDED batches, over lines that arrive one at a time.
    /// <para>
    /// The whole-text overload is fine for a fixture and wrong for a spool: the ingest read a file with
    /// <c>ReadAllTextAsync</c> and handed every record to the store in one call, so both the memory and
    /// the store's parameter list were bounded by how productive the emitter had been rather than by
    /// anything this process chose. A 24/7 emitter is exactly the case that makes that a size nobody
    /// picked.
    /// </para>
    /// <para>
    /// Refusals travel with the chunk that produced them and keep their FILE line numbers, because the
    /// caller's decision — retire this file, or keep it for a build that can read it — is about the file
    /// as a whole and must survive being made one chunk at a time.
    /// </para></summary>
    public static async IAsyncEnumerable<SpoolChunk> ReadChunksAsync(
        IAsyncEnumerable<string> lines,
        int chunkSize,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Capacity is capped independently of the chunk: the size is operator input, and a list asked to
        // reserve a billion slots up front fails with an allocation error rather than a bounded read —
        // which would be this method's own guarantee breaking in its first line.
        var buffer = new List<(string Line, int Number)>(Math.Min(chunkSize, 1024));
        var number = 0;

        await foreach (var line in lines.WithCancellation(cancellationToken))
        {
            buffer.Add((line.Trim('\r', ' '), ++number));

            if (buffer.Count < chunkSize)
            {
                continue;
            }

            yield return Chunk(buffer);
            buffer.Clear();
        }

        if (buffer.Count > 0)
        {
            yield return Chunk(buffer);
        }
    }

    private static SpoolChunk Chunk(IReadOnlyList<(string Line, int Number)> lines)
    {
        var (records, refused) = Read(lines.Where(x => x.Line.Length > 0));

        return new SpoolChunk(records, refused);
    }

    private static (IReadOnlyList<ToolTelemetry> Records, IReadOnlyList<SpoolRefusal> Refused) Read(
        IEnumerable<(string Line, int Number)> lines)
    {
        var records = new List<ToolTelemetry>();
        var refused = new List<SpoolRefusal>();

        foreach (var (line, number) in lines)
        {
            switch (TelemetryCodec.ReadLine(line))
            {
                case LineVerdict.Read read:
                    records.Add(read.Record);
                    break;
                case LineVerdict.UnknownVersion unknown:
                    refused.Add(new SpoolRefusal(number, unknown.Reason, Retryable: true));
                    break;
                case LineVerdict.Unreadable unreadable:
                    refused.Add(new SpoolRefusal(number, unreadable.Reason, Retryable: false));
                    break;
            }
        }

        return (records, refused);
    }

    private static IEnumerable<(string Line, int Number)> NumberedLines(string text) =>
        text
            .Split('\n')
            .Select((line, index) => (Line: line.Trim('\r', ' '), Number: index + 1))
            .Where(x => x.Line.Length > 0);
}
