using System.Text.Json;
using Bench.Domain;
using Bench.Domain.Authoring;
using Bench.Domain.Bank;
using Bench.Domain.Runs;
using Bench.Domain.Targets;

namespace Bench.Application.Bank;

/// <summary>What an import did, and everything it refused.
/// <para>
/// A refusal costs its own row and nothing more — the same discipline the telemetry spool reader already
/// applies to a bad line. An import of two hundred questions must not be lost because one of them names a
/// group nobody created.
/// </para></summary>
public sealed record BankImportReport(
    int Groups, int Reviewers, int Questions, int Reviews, IReadOnlyList<string> Refusals)
{
    public static BankImportReport Empty => new(0, 0, 0, 0, []);

    public BankImportReport Plus(BankImportReport other) => new(
        Groups + other.Groups,
        Reviewers + other.Reviewers,
        Questions + other.Questions,
        Reviews + other.Reviews,
        [.. Refusals, .. other.Refusals]);

    public BankImportReport Refusing(string reason) => this with { Refusals = [.. Refusals, reason] };

    public string Describe =>
        $"{Groups} group(s), {Reviewers} reviewer(s), {Questions} question(s), {Reviews} review(s), "
        + $"{Refusals.Count} refused";
}

/// <summary>Reading a bank import file, and applying it.
/// <para>
/// The question shape is <see cref="QuestionJson"/>'s — the same one a suite file uses — so a question
/// authored into a file and one imported into the bank produce the same <see cref="Domain.Suites.Question"/>,
/// and therefore the same suite hash. That is what makes a test built from the bank comparable to one
/// built from a file.
/// </para></summary>
public static class BankImport
{
    public static Outcome<BankFile> Read(string json)
    {
        try
        {
            var file = JsonSerializer.Deserialize<BankFile>(json, QuestionJson.ReaderOptions);

            return file is null
                ? Outcome<BankFile>.Failure("the import file is empty")
                : Validate(file);
        }
        catch (JsonException ex)
        {
            // A human or a model authored this file. Malformed input is a validation answer the caller
            // renders, never an exception out of an import of two hundred questions.
            return Outcome<BankFile>.Failure($"the import file is not valid JSON: {ex.Message}");
        }
    }

    /// <summary>Applies a read file to the bank: groups, then reviewers, then questions, then marks.
    /// <para>
    /// The order is the dependency order, and each stage's failures are recorded rather than thrown, so an
    /// import that names one unknown group still lands the other hundred and ninety-nine questions with a
    /// line saying exactly what it would not take.
    /// </para></summary>
    public static async Task<BankImportReport> ApplyAsync(
        IQuestionBank bank, BankFile file, TimeProvider clock, CancellationToken cancellationToken)
    {
        var report = await AddGroupsAsync(bank, file, cancellationToken);
        report = report.Plus(await AddReviewersAsync(bank, file, cancellationToken));

        var groups = await bank.GroupsAsync(cancellationToken);

        if (groups is not Outcome<IReadOnlyList<QuestionGroup>>.Ok(var known))
        {
            return report.Refusing(groups.Match(_ => string.Empty, reason => reason));
        }

        var byKey = known.ToDictionary(g => g.Key.Value, StringComparer.Ordinal);

        foreach (var question in file.Questions)
        {
            report = report.Plus(await AddQuestionAsync(bank, file, question, byKey, clock, cancellationToken));
        }

        return report;
    }

    private static async Task<BankImportReport> AddGroupsAsync(
        IQuestionBank bank, BankFile file, CancellationToken cancellationToken)
    {
        var report = BankImportReport.Empty;

        foreach (var group in file.Groups)
        {
            var created = QuestionGroup.Create(group.Key, group.Title, group.Ordinal);

            report = created is Outcome<QuestionGroup>.Ok(var value)
                ? Counted(await bank.AddGroupAsync(value, cancellationToken), report, r => r with { Groups = r.Groups + 1 })
                : report.Refusing(created.Reason());
        }

        return report;
    }

    private static async Task<BankImportReport> AddReviewersAsync(
        IQuestionBank bank, BankFile file, CancellationToken cancellationToken)
    {
        var report = BankImportReport.Empty;

        foreach (var reviewer in file.Reviewers)
        {
            var created = Reviewer.Create(reviewer.Key, reviewer.DisplayName, reviewer.Ordinal, reviewer.Model);

            report = created is Outcome<Reviewer>.Ok(var value)
                ? Counted(await bank.AddReviewerAsync(value, cancellationToken), report, r => r with { Reviewers = r.Reviewers + 1 })
                : report.Refusing(created.Reason());
        }

        return report;
    }

    private static async Task<BankImportReport> AddQuestionAsync(
        IQuestionBank bank,
        BankFile file,
        BankQuestionFile question,
        IReadOnlyDictionary<string, QuestionGroup> groups,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        if (!groups.TryGetValue(Slug.Clean(question.Group), out var group))
        {
            return BankImportReport.Empty.Refusing(
                $"question '{question.Id}' names group '{question.Group}', which is not in the bank");
        }

        var built = Build(file, question, group, clock);

        if (built is not Outcome<BankQuestion>.Ok(var bankQuestion))
        {
            return BankImportReport.Empty.Refusing($"question '{question.Id}': {built.Reason()}");
        }

        var added = await bank.AddAsync(bankQuestion.As(State(question.State)), cancellationToken);

        return added is Outcome<BankQuestion>.Fail failed
            ? BankImportReport.Empty.Refusing($"question '{question.Id}': {failed.Reason}")
            : (await MarksAsync(bank, question, clock, cancellationToken)) with { Questions = 1 };
    }

    private static async Task<BankImportReport> MarksAsync(
        IQuestionBank bank, BankQuestionFile question, TimeProvider clock, CancellationToken cancellationToken)
    {
        var report = BankImportReport.Empty;

        foreach (var review in question.Reviews)
        {
            var marked = await bank.ReviewAsync(
                question.Id,
                review.Reviewer,
                Verdict(review.Verdict),
                review.Note,
                clock.GetUtcNow(),
                review.Model,
                cancellationToken);

            report = marked is Outcome<QuestionReview>.Fail failed
                ? report.Refusing($"question '{question.Id}' review by '{review.Reviewer}': {failed.Reason}")
                : report with { Reviews = report.Reviews + 1 };
        }

        return report;
    }

    private static Outcome<BankQuestion> Build(
        BankFile file, BankQuestionFile question, QuestionGroup group, TimeProvider clock) =>
        RepoUrl.Parse(file.TargetRepo).Match(
            repo => CommitSha.Parse(file.AuthoredAtCommit).Match(
                commit => BankQuestion.Create(
                    group.Id,
                    question.Ordinal,
                    Kind(question.Kind),
                    QuestionJson.ToQuestion(question.ToQuestionFile(), commit),
                    question.CodeTask,
                    Source(question.Source),
                    question.AuthorModel,
                    Seed(question.Seed),
                    repo,
                    commit,
                    clock.GetUtcNow()),
                Outcome<BankQuestion>.Failure),
            Outcome<BankQuestion>.Failure);

    /// <summary>A question that declares no seed gets one dated to the beginning of time, and that is the
    /// SAFE direction rather than a shrug: the memorisation check compares the seed's date to a subject's
    /// training cutoff, so an unstated provenance reads as "this subject may recall it" instead of quietly
    /// certifying it as clear.</summary>
    private static QuestionSeed Seed(SeedFile? seed) =>
        seed is null
            ? new QuestionSeed("unstated", string.Empty, default)
            // Written, not constructed: an imported file carries a bare "at" exactly as an authored one does, and
            // converting that calendar day into an instant in this machine's offset is what shifted a whole batch by
            // a day. The import had the same hazard on the same line, unexercised until something imported one.
            : QuestionSeed.Written(
                seed.Kind.Length > 0 ? seed.Kind : "unstated", seed.Reference, seed.At);

    private static BankImportReport Counted<T>(
        Outcome<T> outcome, BankImportReport report, Func<BankImportReport, BankImportReport> increment) =>
        outcome is Outcome<T>.Fail failed
            // An existing group or reviewer is the normal case on a second import, not a fault: it is
            // recorded so the operator sees what was skipped, and nothing is counted as added.
            ? report.Refusing(failed.Reason)
            : increment(report);

    private static TaskKind Kind(string kind) =>
        Enum.TryParse<TaskKind>(kind, ignoreCase: true, out var parsed) ? parsed : TaskKind.Reading;

    private static AuthoringSource Source(string source) =>
        Enum.TryParse<AuthoringSource>(source, ignoreCase: true, out var parsed) ? parsed : AuthoringSource.Human;

    private static CandidateState State(string state) =>
        Enum.TryParse<CandidateState>(state, ignoreCase: true, out var parsed) ? parsed : CandidateState.Proposed;

    private static ReviewVerdict Verdict(string verdict) =>
        Enum.TryParse<ReviewVerdict>(verdict, ignoreCase: true, out var parsed) ? parsed : ReviewVerdict.Rejected;

    private static Outcome<BankFile> Validate(BankFile file) =>
        file.Questions.Count == 0 && file.Groups.Count == 0 && file.Reviewers.Count == 0
            ? Outcome<BankFile>.Failure("the import file declares no groups, no reviewers and no questions")
            : Outcome<BankFile>.Success(file);

    private static string Reason<T>(this Outcome<T> outcome) => outcome.Match(_ => string.Empty, reason => reason);
}

/// <param name="TargetRepo">Which repository these questions are about. One file, one target: an anchor is
/// true at exactly one tree, and a file that mixed targets could not state a commit at all.</param>
/// <param name="AuthoredAtCommit">The commit every anchor in this file was verified against.</param>
public sealed record BankFile
{
    public string TargetRepo { get; init; } = string.Empty;

    public string AuthoredAtCommit { get; init; } = string.Empty;

    public IReadOnlyList<GroupFile> Groups { get; init; } = [];

    public IReadOnlyList<ReviewerFile> Reviewers { get; init; } = [];

    public IReadOnlyList<BankQuestionFile> Questions { get; init; } = [];
}

public sealed record GroupFile
{
    public string Key { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public int Ordinal { get; init; }
}

public sealed record ReviewerFile
{
    public string Key { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public int Ordinal { get; init; }

    /// <summary>The registry key that answers for this slot. Left empty in a seed file on purpose: binding a
    /// model to a reviewer is a local decision with a stated cost (see <c>bench questions bind</c>), and a
    /// committed default would make it look like the project's recommendation.</summary>
    public string Model { get; init; } = string.Empty;
}

public sealed record SeedFile
{
    public string Kind { get; init; } = string.Empty;

    public string Reference { get; init; } = string.Empty;

    public DateTimeOffset At { get; init; }
}

public sealed record ReviewFile
{
    public string Reviewer { get; init; } = string.Empty;

    public string Verdict { get; init; } = "Approved";

    public string Note { get; init; } = string.Empty;

    /// <summary>Which model made this mark, when one did. Empty means a person — the honest default for a file
    /// somebody wrote by hand, and the reason this field is not required.</summary>
    public string Model { get; init; } = string.Empty;
}

public sealed record BankQuestionFile
{
    public string Group { get; init; } = string.Empty;

    public int Ordinal { get; init; }

    public string Kind { get; init; } = "Reading";

    public string State { get; init; } = "Proposed";

    public string Source { get; init; } = "Human";

    public string AuthorModel { get; init; } = string.Empty;

    public string CodeTask { get; init; } = string.Empty;

    public SeedFile? Seed { get; init; }

    public string Id { get; init; } = string.Empty;

    public string Prompt { get; init; } = string.Empty;

    public string ReferenceAnswer { get; init; } = string.Empty;

    public IReadOnlyList<ExpectationFile> Expectations { get; init; } = [];

    public IReadOnlyList<ReviewFile> Reviews { get; init; } = [];

    /// <summary>The suite-facing half of this row, so the question itself is mapped by
    /// <see cref="QuestionJson"/> rather than by a second mapper that would drift from it.</summary>
    public QuestionFile ToQuestionFile() => new()
    {
        Id = Id,
        Prompt = Prompt,
        ReferenceAnswer = ReferenceAnswer,
        Expectations = Expectations,
    };
}
