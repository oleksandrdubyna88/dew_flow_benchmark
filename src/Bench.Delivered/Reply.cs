namespace Bench.Delivered;

/// <summary>What reading a model's reply produced: a value, or the reason it could not be read.
///
/// <para><b>Why this is not <c>Outcome&lt;T&gt;</c>.</b> It is the same shape, and it is re-declared here for
/// exactly one reason: this module is a leaf that may not reference <c>Bench.Domain</c>, which is where
/// <c>Outcome&lt;T&gt;</c> lives. The alternative — letting the module see the domain to borrow one type —
/// would cost the property the leaf exists for. <c>Bench.Ui</c> makes the identical trade with its
/// <c>Read&lt;T&gt;</c> and says so in the same words.</para>
///
/// <para><b>An unreadable reply is a refusal, never a guess.</b> There is no third case and no partial
/// value: a weigher whose answer lost a field has not produced a low-confidence reading, and salvaging what
/// parsed would put a number nobody produced into a published score.</para>
/// </summary>
public abstract record Reply<T>
{
    private Reply()
    {
    }

    public sealed record Ok(T Value) : Reply<T>;

    public sealed record Invalid(string Why) : Reply<T>;

    /// <summary>The reason, or empty on success. For a caller that logs rather than branches.</summary>
    public string Reason => this is Invalid invalid ? invalid.Why : string.Empty;

    public static Reply<T> Read(T value) => new Ok(value);

    public static Reply<T> Refuse(string reason) => new Invalid(reason);
}
