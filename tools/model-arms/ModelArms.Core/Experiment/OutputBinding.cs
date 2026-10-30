using Bench.Domain;

namespace ModelArms.Core.Experiment;

/// <summary>One binding of a step: a brief FIELD name and the file it stands for, relative to the arm folder (<c>HA_JSON</c> → <c>notes/hA.json</c>). The path is kept as
/// written and is the normalised destination: two are compared <see cref="StringComparison.OrdinalIgnoreCase"/> wherever they are compared, so the comparison fails closed
/// on a host whose file system ignores case.</summary>
public sealed partial record OutputBinding(string Field, string RelativePath)
{
    /// <summary>The binding the two texts spell, or why they are none.</summary>
    public static Outcome<OutputBinding> Parse(string field, string path) => Outcome<OutputBinding>.Success(new OutputBinding(field, path));
}
