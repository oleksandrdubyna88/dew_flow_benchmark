using Bench.Domain.Targets;

namespace Bench.Domain.Gate;

/// <summary>Which gates a task can be run through. A plan-only task — the seeded 8-defect plan — hosts the
/// plan gate and nothing else; asking it for the code gate is refused by name rather than run against a diff
/// that does not exist.</summary>
public sealed record HostedGates
{
    private HostedGates(bool plan, bool code, bool feature)
    {
        Plan = plan;
        Code = code;
        Feature = feature;
    }

    public bool Plan { get; }

    public bool Code { get; }

    public bool Feature { get; }

    public static HostedGates All { get; } = new(true, true, true);

    public static Outcome<HostedGates> Of(IReadOnlyList<GateKind> kinds) =>
        kinds.Count == 0
            ? Outcome<HostedGates>.Failure("a task hosts at least one gate — a task no gate can run is not a task")
            : Outcome<HostedGates>.Success(new HostedGates(
                kinds.Contains(GateKind.Plan), kinds.Contains(GateKind.Code), kinds.Contains(GateKind.Feature)));

    public bool Hosts(GateKind gate) => gate switch
    {
        GateKind.Plan => Plan,
        GateKind.Code => Code,
        GateKind.Feature => Feature,
        _ => false,
    };

    public IReadOnlyList<GateKind> Kinds =>
        [.. new[] { GateKind.Plan, GateKind.Code, GateKind.Feature }.Where(Hosts)];

    public string Canonical => string.Join(',', Kinds.Select(k => k.ToString().ToLowerInvariant()));
}

/// <summary>Where a task's clone sits on THIS machine. Deliberately not part of any canonical form: a suite
/// whose clones moved is the same suite, and a stamp that changed when a directory did would re-identify
/// every run measured against it.</summary>
public sealed record CloneLocation
{
    private CloneLocation(string path) => Path = path;

    public string Path { get; }

    public static Outcome<CloneLocation> Parse(string? path)
    {
        var trimmed = (path ?? string.Empty).Trim();

        return trimmed.Length == 0
            ? Outcome<CloneLocation>.Failure("a task names where its clone is — a local path or a url; the checkout provider mirrors it read-only")
            : Outcome<CloneLocation>.Success(new CloneLocation(trimmed));
    }
}

/// <summary>The frozen seeded case one task is: the base, the variant head the defects were planted into,
/// the plan the product reviews, and the two caller inputs of <c>review_feature</c>. All of it hashes into
/// the suite stamp; none of it says where the repository is.</summary>
public sealed record GateCase
{
    private GateCase(CommitSha @base, CommitSha variantHead, string planPath, string epics, string lessons, IReadOnlyList<string> absentSubmodules)
    {
        Base = @base;
        VariantHead = variantHead;
        PlanPath = planPath;
        Epics = epics;
        Lessons = lessons;
        AbsentSubmodules = absentSubmodules;
    }

    public CommitSha Base { get; }

    public CommitSha VariantHead { get; }

    /// <summary>Repository-relative path of the plan document at the variant head.</summary>
    public string PlanPath { get; }

    /// <summary>The <c>epics</c> argument of <c>review_feature</c>, as the text sent. Empty for a task that
    /// hosts no feature gate.</summary>
    public string Epics { get; }

    /// <summary>The <c>lessons</c> argument of <c>review_feature</c>, as the text sent.</summary>
    public string Lessons { get; }

    /// <summary>Submodules the variant head pins that the task is measured WITHOUT — repository-relative, <c>/</c>-separated,
    /// in ordinal order. ts2 (E7, 2026-09-28) pins its rules at a url that no longer resolves, and the calibration measured
    /// it with the folder empty; the clone leaves exactly these uninitialised and fetches every other one. Empty for a
    /// task whose every submodule is part of it.</summary>
    public IReadOnlyList<string> AbsentSubmodules { get; }

    public static Outcome<GateCase> Of(CommitSha @base, CommitSha variantHead, string? planPath, string? epics, string? lessons) =>
        Of(@base, variantHead, planPath, epics, lessons, []);

    public static Outcome<GateCase> Of(
        CommitSha @base, CommitSha variantHead, string? planPath, string? epics, string? lessons, IReadOnlyList<string> absentSubmodules)
    {
        var plan = (planPath ?? string.Empty).Trim();
        var outside = RepositoryRelative.Refusal(plan);
        var absent = absentSubmodules.Select(p => p.Trim().Replace('\\', '/')).ToList();

        var refusal = (plan.Length, outside.Length, @base.Value == variantHead.Value, AbsentRefusal(absent)) switch
        {
            (0, _, _, _) => "a case names the plan the product reviews, as a repository-relative path",
            (_, > 0, _, _) => $"{outside} — the plan path is REPOSITORY-relative and stays inside the checkout, so the suite says nothing about this machine",
            (_, _, true, _) => $"the variant head is the base ({@base.Short}) — there is no diff to review and nothing was planted",
            (_, _, _, { Length: > 0 } bad) => bad,
            _ => string.Empty,
        };

        return refusal.Length > 0
            ? Outcome<GateCase>.Failure(refusal)
            : Outcome<GateCase>.Success(new GateCase(@base, variantHead, plan, epics ?? string.Empty, lessons ?? string.Empty, [.. absent.Order(StringComparer.Ordinal)]));
    }

    /// <summary>Length-prefixed (<see cref="CanonicalFields"/>): the epics and lessons are free text, and a
    /// separator between them would be forgeable from inside either. The absent submodules are appended ONLY when there are
    /// any, with their count, so every case recorded before the field existed keeps the canonical form — and the stamp — it
    /// had.</summary>
    public string Canonical => CanonicalFields.Of(
    [
        "case", Base.Value, VariantHead.Value, PlanPath, Epics, Lessons,
        .. AbsentSubmodules.Count == 0
            ? []
            : (IEnumerable<string>)["absent-submodules", AbsentSubmodules.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), .. AbsentSubmodules],
    ]);

    private static string AbsentRefusal(IReadOnlyList<string> absent)
    {
        var blank = absent.Any(p => p.Length == 0);
        var outside = absent.Select(p => RepositoryRelative.Refusal(p)).FirstOrDefault(r => r.Length > 0) ?? string.Empty;
        var twice = absent.GroupBy(p => p, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1)?.Key ?? string.Empty;

        return (blank, outside.Length, twice.Length) switch
        {
            (true, _, _) => "an absent submodule is blank — name the path the variant head pins it at",
            (_, > 0, _) => $"{outside} — an absent submodule is a REPOSITORY-relative path inside the checkout",
            (_, _, > 0) => $"the case names absent submodule '{twice}' twice",
            _ => string.Empty,
        };
    }
}

/// <summary>One frozen seeded task: what the database knows it as (id, language, hosted gates, calibration
/// flag, seed refs), the case every gate runs against, and where its clone is on this machine.
/// <para>
/// <see cref="Canonical"/> is built from named fields and <see cref="Repository"/> is not one of them, so paths
/// are out of the suite stamp BY CONSTRUCTION rather than by a filter somebody remembers to apply.
/// </para></summary>
public sealed record GateTask
{
    private GateTask(
        GateTaskId id, string language, HostedGates hosts, bool isCalibration,
        GateCase @case, IReadOnlyList<SeedSpec> seeds, CloneLocation clone)
    {
        Id = id;
        Language = language;
        Hosts = hosts;
        IsCalibration = isCalibration;
        Case = @case;
        Seeds = seeds;
        Repository = clone;
    }

    public GateTaskId Id { get; }

    /// <summary>A label a per-task table is grouped by — <c>C#</c>, <c>TSX</c>. Refused blank; not otherwise
    /// constrained, because the value is the trial's own spelling.</summary>
    public string Language { get; }

    public HostedGates Hosts { get; }

    /// <summary>A calibration task is measured and reported APART — it was used to settle a transport, so its
    /// numbers describe the tuning as much as the model.</summary>
    public bool IsCalibration { get; }

    public GateCase Case { get; }

    public IReadOnlyList<SeedSpec> Seeds { get; }

    public CloneLocation Repository { get; }

    public bool IsSeeded => Seeds.Count > 0;

    public static Outcome<GateTask> Of(
        GateTaskId id, string? language, HostedGates hosts, bool isCalibration,
        GateCase @case, IReadOnlyList<SeedSpec> seeds, CloneLocation clone)
    {
        var lang = (language ?? string.Empty).Trim();
        var duplicate = seeds.GroupBy(s => s.Id.Value, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);

        var refusal = (lang.Length, duplicate) switch
        {
            (0, _) => $"task '{id}' names no language — the per-task table is grouped by it",
            (_, not null) => $"task '{id}' carries seed '{duplicate.Key}' twice — a hit could not say which one it hit",
            _ => string.Empty,
        };

        return refusal.Length > 0
            ? Outcome<GateTask>.Failure(refusal)
            : Outcome<GateTask>.Success(new GateTask(id, lang, hosts, isCalibration, @case, [.. seeds], clone));
    }

    /// <summary>What the database and a report hold of a task — no text, no path.</summary>
    public TaskSummary Summary => new(Id, Language, IsCalibration, Hosts, [.. Seeds.Select(s => s.Ref)]);

    /// <summary>The task's contribution to the suite stamp. The clone location is not an input. Length-prefixed,
    /// with the seed COUNT as a field of its own, so one seed whose free text spells a second can never read as
    /// two.</summary>
    public string Canonical =>
        CanonicalFields.Of(
        [
            "task", Id.Value, Language, Hosts.Canonical, IsCalibration ? "calibration" : "measured", Case.Canonical,
            Seeds.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
            .. Seeds.OrderBy(s => s.Id.Value, StringComparer.Ordinal).Select(s => s.Canonical),
        ]);
}

/// <summary>A task as the database and the report see it.</summary>
public sealed record TaskSummary(
    GateTaskId Id, string Language, bool IsCalibration, HostedGates Hosts, IReadOnlyList<SeedRef> Seeds)
{
    public bool IsSeeded => Seeds.Count > 0;

    /// <summary>What a RECORDED task set is compared by (<c>gate_suite_tasks</c>, E6): the seeds in id order, so the order a
    /// suite file listed them in is not a difference, and every field length-prefixed.</summary>
    public string Canonical =>
        CanonicalFields.Of(
        [
            "summary", Id.Value, Language, IsCalibration ? "calibration" : "measured", Hosts.Canonical,
            Seeds.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
            .. Seeds.OrderBy(s => s.Id.Value, StringComparer.Ordinal).Select(s => CanonicalFields.Of(s.Id.Value, s.CrossEpic ? "cross-epic" : "in-epic")),
        ]);
}
