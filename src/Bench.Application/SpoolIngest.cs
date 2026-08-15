using Bench.Domain;
using Bench.Domain.Telemetry;

namespace Bench.Application;

/// <summary>Turns a spool file's text into records, without touching a disk or a database.
/// <para>
/// Pure on purpose: everything that decides what a spool MEANS — which lines are readable, which are
/// refused and why — is testable without a filesystem, and the host is left with nothing but the IO.
/// </para></summary>
public static class SpoolIngest
{
    /// <summary>Reads every line, keeping the readable records and the reasons the rest were refused.
    /// <para>
    /// A bad line costs that line and nothing more. A spool is written by a process that can be killed
    /// mid-write, so the last line of a file is routinely half a record — a reader that aborts on the
    /// first failure would discard a whole run's telemetry over its final byte.
    /// </para></summary>
    public static (IReadOnlyList<ToolTelemetry> Records, IReadOnlyList<string> Refused) Read(string text)
    {
        var records = new List<ToolTelemetry>();
        var refused = new List<string>();

        foreach (var (line, number) in NumberedLines(text))
        {
            switch (TelemetryCodec.ReadLine(line))
            {
                case Outcome<ToolTelemetry>.Ok ok:
                    records.Add(ok.Value);
                    break;
                case Outcome<ToolTelemetry>.Fail fail:
                    refused.Add($"line {number}: {fail.Reason}");
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
