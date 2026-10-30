using System.Text.RegularExpressions;
using Bench.Domain.Trace;

namespace ModelArms.Core.Checks;

/// <summary>What the extension's node test runner says of a run (<c>objective-checks.sh:35-36</c>, <c>:58</c>): its count, the names it printed as failed (each once) and the names it printed as passed.</summary>
public sealed record NodeTestLog(CapturedCount Tests, IReadOnlyList<string> Failed, IReadOnlySet<string> Passed);

/// <summary>The shell's reads of <c>ts-tests.log</c>. A run through <c>npm</c> prints the runner's summary more than once (part 6's C1 printed <c>ℹ tests 6540</c> and then, from a second runner, <c>ℹ tests 4</c>), and the shell took the FIRST;
/// so does this. A failed test is a <c>✖</c> at the START of a line; a passed one is a <c>✔</c> after any blanks, because the runner indents the tests of a nested suite.</summary>
public static class TsTestLog
{
    private static readonly Regex Tests = new(@"^ℹ tests (\d+)", JsLog.Options);
    private static readonly Regex Failed = new(@"^✖ (.+?) \(\d", JsLog.Options);
    private static readonly Regex Passed = new($@"^[{JsLog.Blank}]*✔ (.+?) \(\d", JsLog.Options);

    public static NodeTestLog Read(string log)
    {
        var lines = JsLog.Lines(log);
        return new NodeTestLog(
            JsLog.Count(Tests, lines, "`ℹ tests` count"),
            [.. Names(Failed, lines).Distinct(StringComparer.Ordinal)],
            new HashSet<string>(Names(Passed, lines), StringComparer.Ordinal));
    }

    private static IEnumerable<string> Names(Regex pattern, string lines) => pattern.Matches(lines).Select(match => match.Groups[1].Value);
}
