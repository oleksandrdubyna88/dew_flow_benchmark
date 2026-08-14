namespace Bench.Domain;

/// <summary>An expected success-or-failure result. Never thrown; always returned and matched.
/// Validation failures in this domain are ordinary answers — a malformed commit sha is something a
/// caller handles, not an exception that unwinds a run of ten thousand legs.</summary>
public abstract record Outcome<T>
{
    private Outcome() { }

    public sealed record Ok(T Value) : Outcome<T>;

    public sealed record Fail(string Reason) : Outcome<T>;

    public static Outcome<T> Success(T value) => new Ok(value);

    public static Outcome<T> Failure(string reason) => new Fail(reason);

    public TResult Match<TResult>(Func<T, TResult> ok, Func<string, TResult> fail) =>
        this switch
        {
            Ok o => ok(o.Value),
            Fail f => fail(f.Reason),
            _ => throw new InvalidOperationException("unreachable"),
        };
}
