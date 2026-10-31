using Bench.Domain;

namespace ModelArms.Adapters;

/// <summary>Outcomes chained so the first failure ends the walk and nothing after it runs — what <c>PassageSteps</c> is for a <c>Passage</c>, for the adapters that return an <see cref="Outcome{T}"/> (the objective run: the folder, then the logs, then each stage). The failure's words travel unchanged.</summary>
internal static class OutcomeChain
{
    /// <summary>The failure's words, or the empty text for a success.</summary>
    public static string Reason<T>(this Outcome<T> outcome) => outcome is Outcome<T>.Fail failed ? failed.Reason : string.Empty;

    public static Outcome<TNext> Bind<T, TNext>(this Outcome<T> first, Func<T, Outcome<TNext>> next) => first.Match(next, Outcome<TNext>.Failure);

    public static Outcome<TNext> Map<T, TNext>(this Outcome<T> first, Func<T, TNext> next) => first.Match(value => Outcome<TNext>.Success(next(value)), Outcome<TNext>.Failure);

    public static async Task<Outcome<TNext>> BindAsync<T, TNext>(this Task<Outcome<T>> first, Func<T, Task<Outcome<TNext>>> next) =>
        await (await first).Match(next, reason => Task.FromResult(Outcome<TNext>.Failure(reason)));

    public static async Task<Outcome<TNext>> MapAsync<T, TNext>(this Task<Outcome<T>> first, Func<T, TNext> next) =>
        (await first).Map(next);
}
