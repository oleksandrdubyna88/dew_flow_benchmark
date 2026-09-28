namespace Bench.Domain.Gate;

/// <summary>A person's reading of an assessor's verdicts against the code — measurement rule 2, and the gate on every
/// strict percentage: no strict-supported % is shown for a population until the verdicts it is computed from were
/// hand-checked.
/// <para>
/// Keyed by (rubric, assessor) over a SET of campaigns, not one campaign: an imported history can be many small
/// campaigns, and twenty verdicts read across them is the check the rule asks for. What the database holds is the
/// counts and the hash of the answered sample file; the file itself — finding text, notes, the person's comments —
/// stays in the artefact root.
/// </para></summary>
public sealed record HandCheck
{
    /// <summary>Twenty verdicts read by a person — the number the plan's Definition of Done names.</summary>
    public const int MinVerdicts = 20;

    private HandCheck(IReadOnlyList<Guid> campaigns, Rubric rubric, GateReviewerId assessor, int read, int agreed, string noteHash, DateTimeOffset recordedAt)
    {
        Campaigns = campaigns;
        Rubric = rubric;
        Assessor = assessor;
        Read = read;
        Agreed = agreed;
        NoteHash = noteHash;
        RecordedAt = recordedAt;
    }

    public IReadOnlyList<Guid> Campaigns { get; }

    public Rubric Rubric { get; }

    public GateReviewerId Assessor { get; }

    /// <summary>Verdicts the person read against the code.</summary>
    public int Read { get; }

    /// <summary>Of those, the ones the person agreed with.</summary>
    public int Agreed { get; }

    /// <summary>SHA-256 of the answered sample file in the artefact root — the record's evidence, by hash.</summary>
    public string NoteHash { get; }

    public DateTimeOffset RecordedAt { get; }

    public static Outcome<HandCheck> Of(
        IReadOnlyList<Guid> campaigns, Rubric rubric, GateReviewerId assessor, int read, int agreed, string? noteHash, DateTimeOffset recordedAt)
    {
        var hash = (noteHash ?? string.Empty).Trim();

        var refusal = (campaigns.Count, read, agreed, hash.Length == 64 && hash.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f')) switch
        {
            (0, _, _, _) => "a hand-check covers at least one campaign",
            (_, < MinVerdicts, _, _) => $"a hand-check reads at least {MinVerdicts} verdicts against the code, got {read}",
            (_, _, < 0, _) => $"agreed cannot be negative, got {agreed}",
            (_, _, var a, _) when a > read => $"agreed ({agreed}) cannot exceed the verdicts read ({read})",
            (_, _, _, false) => "a hand-check carries the SHA-256 of its answered sample file",
            _ => string.Empty,
        };

        return refusal.Length > 0
            ? Outcome<HandCheck>.Failure(refusal)
            : Outcome<HandCheck>.Success(new HandCheck([.. campaigns.Distinct()], rubric, assessor, read, agreed, hash, recordedAt));
    }

    public bool Covers(Guid campaign, GateReviewerId assessor, Rubric rubric) =>
        Rubric == rubric && Assessor == assessor && Campaigns.Contains(campaign);
}

/// <summary>Whether a population's strict verdicts may be shown as a percentage: every (campaign, assessor) whose
/// verdicts the figure is computed from has a recorded hand-check under that rubric. A lenient rubric is not gated —
/// it is an imported label, not a strict reading.</summary>
public static class HandCheckGate
{
    public static bool Allows(IReadOnlyList<HandCheck> checks, Rubric rubric, IEnumerable<(Guid Campaign, GateReviewerId Assessor)> used) =>
        rubric.Kind != RubricKind.Strict
        || used.Distinct().All(pair => checks.Any(c => c.Covers(pair.Campaign, pair.Assessor, rubric)));
}

/// <summary>One answered row of a hand-check sample, as the person left it.</summary>
/// <param name="Reading">The verdict word the sample showed — compared with the stored verdict, so a row edited after
/// the draw is refused rather than counted as checked.</param>
public sealed record HandCheckAnswer(BlindedId Id, string BatchId, string Reading, bool Answered, bool Agree);

/// <summary>A drawn verdict the answers are checked against: the key entry it stands for and the verdict as STORED.</summary>
public sealed record HandCheckTruth(BlindedId Id, Guid Campaign, string BatchId, string Reading);

/// <summary>An answered sample, checked before it may count: every answered row is a row that was DRAWN, names the
/// verdict as it is stored (same batch, same reading), appears once, and at least <see cref="HandCheck.MinVerdicts"/>
/// are answered. Returns (read, agreed).</summary>
public static class HandCheckAnswers
{
    public static Outcome<(int Read, int Agreed)> Verify(IReadOnlyList<HandCheckAnswer> answers, IReadOnlyList<HandCheckTruth> drawn)
    {
        var truth = drawn.ToDictionary(t => t.Id.Value, StringComparer.Ordinal);
        var answered = answers.Where(a => a.Answered).ToList();
        var twice = answered.GroupBy(a => a.Id.Value, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);
        var stranger = answered.FirstOrDefault(a => !truth.ContainsKey(a.Id.Value));
        var edited = answered.FirstOrDefault(a => truth.TryGetValue(a.Id.Value, out var t) && !Same(a, t));

        var refusal = (twice, stranger, edited, answered.Count) switch
        {
            ({ } g, _, _, _) => $"row {g.Key} is answered {g.Count()} times — each drawn verdict is read once",
            (_, { } s, _, _) => $"row {s.Id} was not drawn for this sample — only the drawn verdicts count as checked",
            (_, _, { } e, _) => $"row {e.Id} no longer shows the verdict as stored — a sample edited after the draw is not a check of the stored verdicts",
            (_, _, _, < HandCheck.MinVerdicts) => $"{answered.Count} row(s) answered — a hand-check reads at least {HandCheck.MinVerdicts}; set \"agree\" to true or false on each",
            _ => string.Empty,
        };

        return refusal.Length > 0
            ? Outcome<(int, int)>.Failure(refusal)
            : Outcome<(int, int)>.Success((answered.Count, answered.Count(a => a.Agree)));
    }

    private static bool Same(HandCheckAnswer answer, HandCheckTruth truth) =>
        string.Equals(answer.BatchId, truth.BatchId, StringComparison.Ordinal)
        && string.Equals(answer.Reading, truth.Reading, StringComparison.OrdinalIgnoreCase);
}
