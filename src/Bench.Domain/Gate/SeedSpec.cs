namespace Bench.Domain.Gate;

/// <summary>One planted defect — the trial's shape, field for field, because the blinded assessor reads it
/// and the strict rubric is judged against its trigger, mechanism and consequence.
/// <para>
/// This record lives in memory and in the suite file OUTSIDE git; what the database holds of it is
/// <see cref="Ref"/> — the id and the cross-epic flag — and nothing else, because <see cref="Old"/> and
/// <see cref="New"/> quote private code.
/// </para></summary>
public sealed record SeedSpec
{
    private SeedSpec(
        SeedId id, string file, string old, string @new, string what,
        string trigger, string mechanism, string consequence, bool crossEpic)
    {
        Id = id;
        File = file;
        Old = old;
        New = @new;
        What = what;
        Trigger = trigger;
        Mechanism = mechanism;
        Consequence = consequence;
        CrossEpic = crossEpic;
    }

    public SeedId Id { get; }

    /// <summary>Repository-relative path of the file the defect was planted in.</summary>
    public string File { get; }

    /// <summary>The line as it was; empty for a seed that ADDS a line.</summary>
    public string Old { get; }

    /// <summary>The line as planted; empty for a seed that REMOVES a line.</summary>
    public string New { get; }

    public string What { get; }

    public string Trigger { get; }

    public string Mechanism { get; }

    public string Consequence { get; }

    /// <summary>Whether the defect sits on a seam between two epics — the class of finding a feature review
    /// exists to catch, and reported apart for that reason.</summary>
    public bool CrossEpic { get; }

    /// <summary>A seed that removes a line — its evidence is in the pack when its FILE's hunks are.</summary>
    public bool IsRemoval => New.Trim().Length == 0;

    public static Outcome<SeedSpec> Of(
        string? id, string? file, string? old, string? @new, string? what,
        string? trigger, string? mechanism, string? consequence, bool crossEpic) =>
        SeedId.Parse(id).Match(
            parsed => Described(parsed, Clean(file), old ?? string.Empty, @new ?? string.Empty,
                Clean(what), Clean(trigger), Clean(mechanism), Clean(consequence), crossEpic),
            Outcome<SeedSpec>.Failure);

    /// <summary>What the database and a report hold of a seed.</summary>
    public SeedRef Ref => new(Id, CrossEpic);

    /// <summary>Length-prefixed (<see cref="CanonicalFields"/>): every field but the id is free text, and text
    /// moved from <see cref="What"/> into <see cref="Trigger"/> is a different seed for the rubric that judges
    /// the trigger.</summary>
    public string Canonical =>
        CanonicalFields.Of("seed", Id.Value, File, Old, New, What, Trigger, Mechanism, Consequence, CrossEpic ? "cross-epic" : "in-epic");

    private static Outcome<SeedSpec> Described(
        SeedId id, string file, string old, string @new, string what,
        string trigger, string mechanism, string consequence, bool crossEpic)
    {
        var refusal = (file.Length, what.Length, trigger.Length * mechanism.Length * consequence.Length) switch
        {
            (0, _, _) => $"seed {id} names no file — a planted defect is somewhere",
            (_, 0, _) => $"seed {id} says nothing about WHAT was planted",
            (_, _, 0) => $"seed {id} needs a trigger, a mechanism and a consequence — the strict rubric judges all three, "
                + "and a seed missing one can never be 'supported'",
            _ => string.Empty,
        };

        return refusal.Length > 0
            ? Outcome<SeedSpec>.Failure(refusal)
            : Outcome<SeedSpec>.Success(new SeedSpec(id, file, old, @new, what, trigger, mechanism, consequence, crossEpic));
    }

    private static string Clean(string? value) => (value ?? string.Empty).Trim();
}

/// <summary>What the database holds of a seed: enough to count a hit and to say whether it crossed an
/// epic, and no text at all.</summary>
public sealed record SeedRef(SeedId Id, bool CrossEpic);
