namespace Bench.Tests.Telemetry;

/// <summary>The spool fixture, and where it comes from.
/// <para>
/// <c>Fixtures/mcp-spool-v0.jsonl</c> is not hand-authored. It was produced by
/// <c>dew_flow_mcp</c>'s own <c>SpoolUsageSink</c> tests and copied here verbatim, because a fixture I
/// wrote myself would prove only that this codec agrees with my idea of the other repository — which
/// is exactly the failure a cross-repository contract has to rule out.
/// </para>
/// <para>
/// When the emitter's shape changes, this file is REPLACED from a fresh emitter run rather than
/// edited: an edited fixture is a fixture that has quietly stopped being evidence.
/// </para></summary>
internal static class Fixture
{
    public static string Path => System.IO.Path.Combine(AppContext.BaseDirectory, "Fixtures", "mcp-spool-v0.jsonl");

    public static string Text => File.ReadAllText(Path);

    /// <summary>The three lines, in the order the emitter wrote them: answered, refused, error.</summary>
    public static IReadOnlyList<string> Lines =>
        [.. Text.Split('\n').Select(l => l.Trim('\r')).Where(l => l.Length > 0)];
}
