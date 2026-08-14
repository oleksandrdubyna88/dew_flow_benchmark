using Bench.Domain;

namespace Bench.Tests;

/// <summary>Unwrapping helpers for tests. Deliberately loud: a test that silently treats a failure as
/// a success is worse than no test, so the failure reason travels into the assertion message.</summary>
internal static class OutcomeEx
{
    public static T Ok<T>(this Outcome<T> outcome) =>
        outcome switch
        {
            Outcome<T>.Ok ok => ok.Value,
            Outcome<T>.Fail fail => throw new InvalidOperationException($"expected success, got failure: {fail.Reason}"),
            _ => throw new InvalidOperationException("unreachable"),
        };

    public static string Reason<T>(this Outcome<T> outcome) =>
        outcome switch
        {
            Outcome<T>.Fail fail => fail.Reason,
            _ => throw new InvalidOperationException("expected a failure, got success"),
        };

    public static bool Failed<T>(this Outcome<T> outcome) => outcome is Outcome<T>.Fail;
}
