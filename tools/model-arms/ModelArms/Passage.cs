namespace ModelArms;

/// <summary>The gate's step result: the value the next step needs, or the stop — an exit code and the lines to print. Two cases and a code because the
/// stops are not alike (exit 3 the environment is not ready, exit 4 the configuration is wrong), which an <c>Outcome&lt;T&gt;</c>'s one reason cannot say.</summary>
internal abstract record Passage<T>
{
    private Passage() { }

    public sealed record Through(T Value) : Passage<T>;

    public sealed record Stopped(int ExitCode, IReadOnlyList<string> Lines) : Passage<T>;

    public static Passage<T> Stop(int exitCode, params string[] lines) => new Stopped(exitCode, lines);

    public TResult Match<TResult>(Func<T, TResult> through, Func<Stopped, TResult> stopped) =>
        this switch
        {
            Through t => through(t.Value),
            Stopped s => stopped(s),
            _ => throw new InvalidOperationException("unreachable"),
        };

    public Passage<TNext> Bind<TNext>(Func<T, Passage<TNext>> next) =>
        Match(next, stop => new Passage<TNext>.Stopped(stop.ExitCode, stop.Lines));

    public Passage<TNext> Map<TNext>(Func<T, TNext> next) =>
        Match<Passage<TNext>>(value => new Passage<TNext>.Through(next(value)), stop => new Passage<TNext>.Stopped(stop.ExitCode, stop.Lines));
}

/// <summary>Steps chained so the first stop ends the walk and nothing after it runs.</summary>
internal static class PassageSteps
{
    public static async Task<Passage<TNext>> Then<T, TNext>(this Task<Passage<T>> first, Func<T, Passage<TNext>> next) =>
        (await first).Match(next, stop => new Passage<TNext>.Stopped(stop.ExitCode, stop.Lines));

    public static async Task<Passage<TNext>> ThenAsync<T, TNext>(this Task<Passage<T>> first, Func<T, Task<Passage<TNext>>> next) =>
        await (await first).Match(
            next,
            stop => Task.FromResult<Passage<TNext>>(new Passage<TNext>.Stopped(stop.ExitCode, stop.Lines)));
}
