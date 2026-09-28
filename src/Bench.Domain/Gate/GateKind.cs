namespace Bench.Domain.Gate;

/// <summary>Which of the product's three review gates a run drove. Stored as a NAME wherever it is stored,
/// because an ordinal changes meaning the day somebody inserts a member and these rows are published.</summary>
public enum GateKind
{
    /// <summary><c>open</c> → <c>review_plan</c> over the plan text at the variant head.</summary>
    Plan,

    /// <summary>After a plan round passed: <c>review_code</c> over <c>base..variant</c> — the planted-defect diff.</summary>
    Code,

    /// <summary><c>review_feature</c> from the checkout at the variant, with the suite's epics and lessons.</summary>
    Feature,
}

/// <summary>The one reading of a gate WORD — <c>plan</c>, <c>code</c>, <c>feature</c>, any case — for every surface
/// that takes one (the report verb, the API route, the page). <c>Enum.TryParse</c> alone also accepts a number
/// (<c>"7"</c>), which is how <c>--gate 7</c> was once taken for a gate.</summary>
public static class GateWord
{
    public static Outcome<GateKind> Parse(string? word) =>
        (word ?? string.Empty).ToLowerInvariant() switch
        {
            "plan" => Outcome<GateKind>.Success(GateKind.Plan),
            "code" => Outcome<GateKind>.Success(GateKind.Code),
            "feature" => Outcome<GateKind>.Success(GateKind.Feature),
            _ => Outcome<GateKind>.Failure($"'{word}' is not a gate — plan, code or feature"),
        };

    public static string Of(GateKind gate) => gate.ToString().ToLowerInvariant();
}
