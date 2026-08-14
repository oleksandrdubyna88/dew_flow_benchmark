using Bench.Domain.Suites;

namespace Bench.Domain.Splitting;

public enum SplitHalf
{
    /// <summary>The half a configuration is allowed to be CHOSEN on.</summary>
    Selection,

    /// <summary>The half it must then survive. Never used to choose anything.</summary>
    HeldOut,
}

/// <summary>Splitting a suite into the half that selects and the half that confirms.
/// <para>
/// This is not statistical decoration; it is the guard against the one failure this kind of harness
/// reliably produces. Sweeping configurations over a fixed question set does not merely risk a wrong
/// answer — it risks a <em>right measurement of noise</em>, and upstream that happened three separate
/// times with entirely convincing numbers. A candidate reranker pool won its grid on 32 matched
/// targets and then lost the full set 80 against 88. An admission filter was "the first configuration
/// that improves both sides" at 60/107 and cost 24 of 182 two days later. A 28-cell grid was
/// predicted flat before it ran, and was. Multiply the cells and the rate of false winners goes with
/// them.
/// </para>
/// <para>
/// The assignment is derived from the suite id and the question id — deliberately NOT the suite
/// version, so freezing a new version cannot reshuffle the halves underneath a comparison that spans
/// versions. And it is derived by <see cref="StableHash"/> rather than <c>GetHashCode</c>, which is
/// randomised per process: a split that re-assigns on re-run would defeat itself invisibly.
/// </para></summary>
public static class SeedSplit
{
    public static Outcome<SplitHalf> Assign(string suiteId, string questionId) =>
        StableHash.Bucket($"{suiteId}/{questionId}", 2)
            .Match(
                bucket => Outcome<SplitHalf>.Success(bucket == 0 ? SplitHalf.Selection : SplitHalf.HeldOut),
                Outcome<SplitHalf>.Failure);

    public static Outcome<IReadOnlyDictionary<string, SplitHalf>> Assign(Suite suite)
    {
        var assigned = new Dictionary<string, SplitHalf>(StringComparer.Ordinal);

        foreach (var question in suite.Questions)
        {
            var half = Assign(suite.Id, question.Id);

            if (half is Outcome<SplitHalf>.Fail fail)
            {
                return Outcome<IReadOnlyDictionary<string, SplitHalf>>.Failure(fail.Reason);
            }

            assigned[question.Id] = ((Outcome<SplitHalf>.Ok)half).Value;
        }

        return Outcome<IReadOnlyDictionary<string, SplitHalf>>.Success(assigned);
    }

    /// <summary>Whether a winner may be announced. A configuration that won only on the half that
    /// chose it is <em>unproven</em>, which is a different word from "worse" and must render as such.</summary>
    public static ProofState Proof(bool wonOnSelection, bool wonOnHeldOut) =>
        (wonOnSelection, wonOnHeldOut) switch
        {
            (true, true) => ProofState.Confirmed,
            (true, false) => ProofState.Unproven,
            (false, true) => ProofState.Suspicious,
            _ => ProofState.NotAWinner,
        };
}

public enum ProofState
{
    /// <summary>Won on the half that selected it AND on the half that did not. Report it.</summary>
    Confirmed,

    /// <summary>Won only where it was chosen. Not a result — the state every false winner lands in.</summary>
    Unproven,

    /// <summary>Won only on the held-out half. Report it as odd, never as a win: it is more likely a
    /// split artefact than a discovery, and it is worth seeing rather than hiding.</summary>
    Suspicious,

    NotAWinner,
}
