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
/// <b>Measured 2026-08-18, and it is the reason this file exists.</b> The first `pr-diff` batch was handed 345
/// lines of real history and told, in the shared contract, to take `seed.at` from it VERBATIM. It cited three
/// commits that all exist, whose subjects match its questions — and stored every one of them dated
/// <c>2026-08-16</c> when the history it had been given says <c>2026-08-17</c>. Off by one day, three times,
/// systematically. An author will not copy a date; it will reason about one.
/// </para>
/// <para>
/// That matters more than a day, because the seed date is the ONLY input to the memorisation check: a question
/// dated before a subject's training cutoff may be answered from memory, and a date shifted the wrong way turns
/// <i>may recall</i> into <i>clear</i>. Codex found this class of defect by reading git history during review
/// and rejected three questions for it, which is expensive; a sha and a date are comparable for free.
/// </para>
/// <para>
/// Only a <c>commit</c> seed is checkable this way. A `pr` reference lives on a forge, an `issue` likewise, and
/// <c>unstated</c> makes no claim to check — an honest absence rather than a wrong date.
/// </para></summary>
public static class SeedCheck
{
    public const string CommitKind = "commit";

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

        var claimed = DateOnly.FromDateTime(seed.At.UtcDateTime);

        return claimed == fact.Date
            ? []
            : [new SeedDefect(
                reference,
                SeedFault.WrongDate,
                $"the question dates it {claimed:yyyy-MM-dd} and the repository says {fact.Date:yyyy-MM-dd} — "
                + "the seed date is the memorisation check's only input, and a shifted one turns 'may recall' into 'clear'")];
    }
}
