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

    public static Question ToQuestion(QuestionFile question, CommitSha authoredAt) =>
        new(question.Id,
            question.Prompt,
            [.. question.Expectations.Select(e => ToExpectation(e, authoredAt))],
            question.ReferenceAnswer);

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

            return Outcome<IReadOnlyList<Expectation>>.Success(
                [.. files.Select(e => ToExpectation(e, authoredAt))]);
        }
        catch (JsonException ex)
        {
            return Outcome<IReadOnlyList<Expectation>>.Failure($"the expectations are not valid JSON: {ex.Message}");
        }
    }

    public static JsonSerializerOptions ReaderOptions => Options;

    private static Expectation ToExpectation(ExpectationFile expectation, CommitSha authoredAt)
    {
        var kind = Enum.TryParse<ExpectationKind>(expectation.Kind, ignoreCase: true, out var parsed)
            ? parsed
            : ExpectationKind.File;

        var anchor = expectation.Member.Length == 0
            ? SourceAnchor.File(expectation.File, authoredAt)
            : SourceAnchor.Member(
                expectation.File, expectation.Member, new LineSpan(expectation.Start, expectation.End), authoredAt);

        return new Expectation(kind, anchor, expectation.Text, expectation.Required);
    }

    private static ExpectationFile FromExpectation(Expectation expectation) => new()
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
