using System.Text.Json;
using Bench.Domain;
using Bench.Domain.Suites;
using Bench.Domain.Targets;

namespace Bench.Application;

/// <summary>The wire shape of a question, and the ONE place it is mapped to and from the domain.
/// <para>
/// Extracted from <see cref="SuiteJsonLoader"/> when the question bank needed the same shape: a suite file
/// and a bank import describe the same thing, and two readers of one format is two formats with one name.
/// The bank stores its expectations in exactly this shape, so a question authored into a file and one
/// imported into the bank produce the same <see cref="Question"/> — and therefore the same suite hash.
/// </para>
/// <para>
/// The anchor's commit comes from OUTSIDE the payload — a suite file's target, a bank row's
/// <c>AuthoredAtCommit</c> — because an anchor is true at exactly one tree and a per-expectation commit in
/// the file would let one question claim several.
/// </para></summary>
public static class QuestionJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static readonly JsonSerializerOptions Compact = new() { WriteIndented = false };

    public static Outcome<Question> ToQuestion(QuestionFile question, CommitSha authoredAt) =>
        Read(question.Expectations, authoredAt).Match(
            expectations => Outcome<Question>.Success(
                new Question(question.Id, question.Prompt, expectations, question.ReferenceAnswer)),
            reason => Outcome<Question>.Failure($"question '{question.Id}': {reason}"));

    /// <summary>Every expectation, or the first refusal.
    /// <para>The whole list fails on one bad entry rather than loading the rest: a question missing the
    /// expectation its author wrote is scored against the wrong thing, silently, forever.</para></summary>
    private static Outcome<IReadOnlyList<Expectation>> Read(
        IReadOnlyList<ExpectationFile> files, CommitSha authoredAt)
    {
        var read = new List<Expectation>(files.Count);

        foreach (var file in files)
        {
            var one = ToExpectation(file, authoredAt);
            if (one is Outcome<Expectation>.Fail failure)
            {
                return Outcome<IReadOnlyList<Expectation>>.Failure(failure.Reason);
            }

            read.Add(((Outcome<Expectation>.Ok)one).Value);
        }

        return Outcome<IReadOnlyList<Expectation>>.Success(read);
    }

    /// <summary>The expectations of a stored question, as the JSON a bank row holds.</summary>
    public static string WriteExpectations(IReadOnlyList<Expectation> expectations) =>
        JsonSerializer.Serialize(
            expectations.Select(FromExpectation).ToList(),
            Compact);

    /// <summary>Reads them back against the commit the row was authored at. A malformed payload is a
    /// VALUE: a hand-edited row is a real event, and the bank refuses it by name rather than throwing out
    /// of a listing.</summary>
    public static Outcome<IReadOnlyList<Expectation>> ReadExpectations(string json, CommitSha authoredAt)
    {
        try
        {
            var files = JsonSerializer.Deserialize<List<ExpectationFile>>(json, Options) ?? [];

            return Read(files, authoredAt);
        }
        catch (JsonException ex)
        {
            return Outcome<IReadOnlyList<Expectation>>.Failure($"the expectations are not valid JSON: {ex.Message}");
        }
    }

    public static JsonSerializerOptions ReaderOptions => Options;

    /// <summary>
    /// One expectation, or a refusal naming the kind.
    ///
    /// <para><b>It used to fall back to <see cref="ExpectationKind.File"/>.</b> A misspelt kind therefore
    /// became a file expectation against whatever path the entry carried — often none — which then scored
    /// as a retrieval miss the author never wrote, forever, silently. Every other unknown value in this
    /// system is refused by name: an unknown axis field, an unknown funnel stage, an unknown telemetry
    /// version. This one was the exception, and adding tool kinds is exactly what turns a latent typo trap
    /// into a live one, since <c>ToolUsed</c> and <c>ToolUsedd</c> differ by a keystroke.</para>
    ///
    /// <para>Reached by TWO paths, which is why the guard is here rather than in a loader: a suite read from
    /// JSON and a question rehydrated from a bank row both arrive at this method.</para>
    /// </summary>
    private static Outcome<Expectation> ToExpectation(ExpectationFile expectation, CommitSha authoredAt)
    {
        if (!Enum.TryParse<ExpectationKind>(expectation.Kind, ignoreCase: true, out var kind))
        {
            var named = expectation.Kind.Length > 0 ? $"'{expectation.Kind}'" : "an empty kind";
            return Outcome<Expectation>.Failure(
                $"{named} is not an expectation kind this build knows — it knows "
                + string.Join(", ", Enum.GetNames<ExpectationKind>()));
        }

        var anchor = expectation.Member.Length == 0
            ? SourceAnchor.File(expectation.File, authoredAt)
            : SourceAnchor.Member(
                expectation.File, expectation.Member, new LineSpan(expectation.Start, expectation.End), authoredAt);

        return Outcome<Expectation>.Success(new Expectation(kind, anchor, expectation.Text, expectation.Required));
    }

    /// <summary>One expectation as the JSON a file carries. Public because a stored question is also shown to
    /// a REVIEWER, and a second mapping of the same seven fields is the duplication this class exists to
    /// prevent.</summary>
    public static ExpectationFile FromExpectation(Expectation expectation) => new()
    {
        Kind = expectation.Kind.ToString(),
        File = expectation.Anchor.FilePath,
        Member = expectation.Anchor.MemberKey,
        Text = expectation.Text,
        Start = expectation.Anchor.Lines.Start,
        End = expectation.Anchor.Lines.End,
        Required = expectation.Required,
    };
}

public sealed record QuestionFile
{
    public string Id { get; init; } = string.Empty;

    public string Prompt { get; init; } = string.Empty;

    public string ReferenceAnswer { get; init; } = string.Empty;

    public IReadOnlyList<ExpectationFile> Expectations { get; init; } = [];
}

public sealed record ExpectationFile
{
    public string Kind { get; init; } = "File";

    public string File { get; init; } = string.Empty;

    public string Member { get; init; } = string.Empty;

    public string Text { get; init; } = string.Empty;

    public int Start { get; init; }

    public int End { get; init; }

    public bool Required { get; init; } = true;
}
