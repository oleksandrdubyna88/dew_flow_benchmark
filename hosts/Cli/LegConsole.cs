using Bench.Application;
using Bench.Domain.Runs;

namespace Bench.Cli;

/// <summary>Renders a campaign as it happens, one line per leg and one per metric.
/// <para>
/// A class rather than a function because it numbers the scored legs, and the number is what an agent
/// reading the output correlates against. Numbering is a RENDERING concern: the drain counts what
/// happened and says nothing about how it is displayed.
/// </para></summary>
public sealed class LegConsole(TextWriter output)
{
    /// <summary>What this renderer has printed a number for. Not the campaign's tally — the drain owns
    /// that, and reporting one count from two sources is how two counts start disagreeing.</summary>
    private int _printed;

    public void Write(LegEvent step)
    {
        switch (step)
        {
            case LegEvent.Scored scored:
                Report(scored.Result);
                break;

            // A leg that threw. It is named as a FAULT rather than a refusal, because "the harness broke
            // here" and "the subject could not be reached" are two different pieces of news.
            case LegEvent.Faulted faulted:
                output.WriteLine($"  leg     FAULT   — {faulted.Reason}");
                break;

            case LegEvent.Refused refused:
                output.WriteLine($"  leg     REFUSED — {refused.Reason}");
                break;
        }
    }

    private void Report(LegResult result)
    {
        _printed++;
        output.WriteLine($"  leg {_printed,-3} {(result.Passed ? "pass" : "FAIL")}  {Trim(result.Prompt, 58)}");

        foreach (var metric in result.Metrics)
        {
            output.WriteLine($"         {(metric.Failed ? "✗" : "·")} {metric.Name} = {metric.Value}  ({metric.Reason})");
        }
    }

    private static string Trim(string text, int max) =>
        text.Length <= max ? text : text[..max].TrimEnd() + "…";
}
