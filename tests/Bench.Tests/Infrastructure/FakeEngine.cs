using Bench.Application;
using Bench.Domain;
using Bench.Domain.Engines;
using Bench.Domain.Runs;

namespace Bench.Tests.Infrastructure;

/// <summary>
/// The tool surface a test offers a subject: a declared tool list, and a record of what was called.
///
/// <para>One double rather than one per test class. There were two — a "stub" that served three named tools
/// so ordering could be asserted, and a "recording" one that captured invocations — and they were about to
/// become three, which is the point at which a second implementation of a capability is a defect rather
/// than a convenience: they had already drifted on the one thing they shared, the schema they advertise.</para>
///
/// <para>Defaults to the three-tool set the ordering assertions need, in a deliberate order. Pass
/// <paramref name="tools"/> for a different surface and <paramref name="answer"/> for a refusal or a
/// failure, which is the case worth testing most: a refused call is a VALUE the subject can read and
/// correct itself from, never an exception that ends the leg.</para>
/// </summary>
internal sealed class FakeEngine(IReadOnlyList<EngineTool>? tools = null, ToolAnswer? answer = null) : IEngine
{
    /// <summary>Every call, in order, with the arguments as they arrived.</summary>
    public List<(string Tool, string Arguments)> Invocations { get; } = [];

    public EngineRef Describe => EngineRef.Filesystem();

    public string TraceContractVersion => string.Empty;

    public IReadOnlyList<EngineTool> Tools { get; } = tools ??
    [
        new EngineTool("read", "reads a file", Schema),
        new EngineTool("search", "searches", Schema),
        new EngineTool("list", "lists a directory", Schema),
    ];

    /// <summary>A real JSON Schema, because the runtime's guard refuses anything that only parses — and a
    /// double that advertised the shorthand would let a test pass on a request no model could answer.</summary>
    private const string Schema = """{"type":"object"}""";

    public Task<Outcome<string>> WarmAsync(string checkoutPath, CancellationToken cancellationToken) =>
        Task.FromResult(Outcome<string>.Success("warm"));

    public Task<ToolAnswer> InvokeAsync(string tool, string argumentsJson, CancellationToken cancellationToken)
    {
        Invocations.Add((tool, argumentsJson));
        return Task.FromResult(answer ?? ToolAnswer.Success("lines 1-3 of 3"));
    }
}
