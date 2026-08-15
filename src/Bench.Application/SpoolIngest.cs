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
    public static (IReadOnlyList<ToolTelemetry> Records, IReadOnlyList<SpoolRefusal> Refused) Read(string text)
    {
        var records = new List<ToolTelemetry>();
        var refused = new List<SpoolRefusal>();

        foreach (var (line, number) in NumberedLines(text))
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
