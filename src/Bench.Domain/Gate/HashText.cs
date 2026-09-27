namespace Bench.Domain.Gate;

/// <summary>The one spelling of "enough of a hash to prove which thing it was". Every stamp in the gate
/// context quotes twelve hex characters, and a slice taken with <c>[..12]</c> throws on anything shorter — a
/// hand-built record, a truncated import, a future hash of another width. A stamp is a label; producing one
/// must never be the thing that fails.</summary>
public static class HashText
{
    public const int ShortLength = 12;

    /// <summary>The first <see cref="ShortLength"/> characters, or the whole value when it is shorter.</summary>
    public static string Short(string hash) => hash.Length > ShortLength ? hash[..ShortLength] : hash;
}
