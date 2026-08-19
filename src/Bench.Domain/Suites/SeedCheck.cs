using Bench.Domain.Authoring;

namespace Bench.Domain.Suites;

/// <summary>What the repository says about a commit an author cited.</summary>
public sealed record CommitFact(bool Exists, DateOnly Date)
{
    public static CommitFact Unknown => new(false, default);

    public static CommitFact On(DateOnly date) => new(true, date);
}

public enum SeedFault
{
    /// <summary>The cited commit is not in this repository at all.</summary>
    NoSuchCommit,

    /// <summary>It exists, and it did not land on the date the question claims.</summary>
    WrongDate,
}

public sealed record SeedDefect(string Reference, SeedFault Fault, string Detail)
{
    public string Describe => $"seed {Reference}: {Detail}";
}

/// <summary>Whether a question's seed date is what the repository says, rather than what an author wrote down.
/// <para>
/// <b>RETRACTED 2026-08-19 — the finding this file was built on was our own arithmetic.</b> The first `pr-diff`
/// batch was recorded as citing three commits and dating every one of them <c>2026-08-16</c> when the history it
/// had been handed says <c>2026-08-17</c>: "off by one day, three times, systematically". It was not. A bare
/// <c>"at": "2026-08-17"</c> deserialises to midnight in the READING machine's offset and was then normalised to
/// UTC, which on this UTC+2 machine stores it as <c>2026-08-16T22:00Z</c> and reads back as the day before.
/// Replayed through the real types, a report reading <i>"the author dated it 2026-08-16"</i> is what an author
/// that wrote <c>2026-08-17</c> — the CORRECT date — produces. Both models copied the dates faithfully, and the
/// three questions a reviewer rejected for this were rejected over a defect of ours. Fixed at the boundary, in
/// <see cref="QuestionSeed.Written"/>.
/// </para>
/// <para>
/// The check still earns its place, for the reason it can now be stated honestly: the seed date is the ONLY
/// input to the memorisation check — a question dated before a subject's training cutoff may be answered from
/// memory, and a date shifted the wrong way turns <i>may recall</i> into <i>clear</i>. A sha and a date are
/// comparable for free, whoever got them wrong, and this class of defect cost three reviewer launches to find
/// by hand.
/// </para>
/// <para>
/// Only a <c>commit</c> seed is checkable this way. A `pr` reference lives on a forge, an `issue` likewise, and
/// <c>unstated</c> makes no claim to check — an honest absence rather than a wrong date.
/// </para></summary>
public static class SeedCheck
{
    public const string CommitKind = "commit";

    /// <summary>The calendar day a seed claims, which is the only thing a seed date ever means.
    /// <para>
    /// Reading <c>UtcDateTime</c> is safe here and nowhere else: <see cref="QuestionSeed.At"/> is normalised to
    /// UTC by the domain type, and <see cref="QuestionSeed.Written"/> keeps the WALL date an author wrote when
    /// it builds one. Both halves are needed — without the second, a bare <c>"at": "2026-05-14"</c> read on a
    /// UTC+2 machine becomes the previous day at 22:00Z, which is the arithmetic that manufactured the
    /// "authors date a day early" finding of 2026-08-18.
    /// </para>
    /// <para>
    /// One copy, used by the gate here and by the authoring pass's derivation, because two spellings of this
    /// comparison is how one of them came to be wrong while the other looked right.
    /// </para></summary>
    public static DateOnly Stated(DateTimeOffset at) => DateOnly.FromDateTime(at.UtcDateTime);

    public static IReadOnlyList<SeedDefect> Verify(QuestionSeed seed, Func<string, CommitFact> lookup)
    {
        var reference = seed.Reference.Trim();

        if (!string.Equals(seed.Kind.Trim(), CommitKind, StringComparison.OrdinalIgnoreCase) || reference.Length == 0)
        {
            return [];
        }

        var fact = lookup(reference);

        if (!fact.Exists)
        {
            return [new SeedDefect(
                reference,
                SeedFault.NoSuchCommit,
                "no such commit in this repository, so nothing dates this question")];
        }

        // A seed with no date at all is `unstated` and claims nothing; only a stated date can be wrong.
        if (seed.At == default)
        {
            return [];
        }

        var claimed = Stated(seed.At);

        return claimed == fact.Date
            ? []
            : [new SeedDefect(
                reference,
                SeedFault.WrongDate,
                $"the question dates it {claimed:yyyy-MM-dd} and the repository says {fact.Date:yyyy-MM-dd} — "
                + "the seed date is the memorisation check's only input, and a shifted one turns 'may recall' into 'clear'")];
    }
}
