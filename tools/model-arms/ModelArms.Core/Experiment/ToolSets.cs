namespace ModelArms.Core.Experiment;

/// <summary>The PERMISSION lists a step runs under (<c>--allowedTools</c> / <c>--disallowedTools</c>): what a write step may use,
/// what a read step may use, the gate step's own tools, and what no step may read.</summary>
public sealed record ToolSets(
    IReadOnlyList<string> Write,
    IReadOnlyList<string> Read,
    IReadOnlyList<string> Gate,
    IReadOnlyList<string> Deny);
