using Bench.Application;
using Bench.Domain;
using Bench.Domain.Telemetry;

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

    /// <summary>The record a spool line produced, or a loud failure naming why it did not.</summary>
    public static ToolTelemetry Ok(this LineVerdict verdict) =>
        verdict switch
        {
            LineVerdict.Read read => read.Record,
            LineVerdict.UnknownVersion unknown => throw new InvalidOperationException(
                $"expected a readable line, got an unknown version: {unknown.Reason}"),
            LineVerdict.Unreadable unreadable => throw new InvalidOperationException(
                $"expected a readable line, got: {unreadable.Reason}"),
            _ => throw new InvalidOperationException("unreachable"),
        };

    /// <summary>Why a line was refused, whichever way it was refused.</summary>
    public static string Reason(this LineVerdict verdict) =>
        verdict switch
        {
            LineVerdict.UnknownVersion unknown => unknown.Reason,
            LineVerdict.Unreadable unreadable => unreadable.Reason,
            _ => throw new InvalidOperationException("expected a refusal, got a readable line"),
        };
}
