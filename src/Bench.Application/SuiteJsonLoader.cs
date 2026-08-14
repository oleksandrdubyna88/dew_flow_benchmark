using System.Text.Json;
using Bench.Domain;
using Bench.Domain.Suites;
using Bench.Domain.Targets;

namespace Bench.Application;

/// <summary>Reads a suite from JSON and freezes it. The authored file is the DRAFT; what gets measured
/// is the frozen version it produces, and the file can never be the thing a result names — the version
/// hash is. That separation is the point: upstream, a measured question set lived only in a database
/// while the file claiming to be it had drifted several versions behind, and nothing ever said so.</summary>
public static class SuiteJsonLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static Outcome<Suite> Load(string json, CommitSha authoredAt) =>
        Parse(json).Match(file => Build(file, authoredAt), Outcome<Suite>.Failure);

    private static Outcome<SuiteFile> Parse(string json)
    {
        try
        {
            var file = JsonSerializer.Deserialize<SuiteFile>(json, Options);
            return file is null
                ? Outcome<SuiteFile>.Failure("the suite file is empty")
                : Outcome<SuiteFile>.Success(file);
        }
        catch (JsonException ex)
        {
            // Expected failure: a human authored this file by hand. A malformed suite is a validation
            // answer the caller renders, not an exception that unwinds a run of ten thousand legs.
            return Outcome<SuiteFile>.Failure($"the suite file is not valid JSON: {ex.Message}");
        }
    }

    private static Outcome<Suite> Build(SuiteFile file, CommitSha authoredAt)
    {
        if (string.IsNullOrWhiteSpace(file.Id))
        {
            return Outcome<Suite>.Failure("the suite file has no id");
        }

        var draft = file.Questions.Aggregate(
            Outcome<Suite>.Success(Suite.Draft(file.Id)),
            (acc, q) => acc.Match(s => s.With(ToQuestion(q, authoredAt)), Outcome<Suite>.Failure));

        return draft.Match(s => s.Freeze(), Outcome<Suite>.Failure);
    }

    private static Question ToQuestion(QuestionFile question, CommitSha authoredAt) =>
        new(question.Id,
            question.Prompt,
            [.. question.Expectations.Select(e => ToExpectation(e, authoredAt))],
            question.ReferenceAnswer);

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

    private sealed record SuiteFile
    {
        public string Id { get; init; } = string.Empty;

        public IReadOnlyList<QuestionFile> Questions { get; init; } = [];
    }

    private sealed record QuestionFile
    {
        public string Id { get; init; } = string.Empty;

        public string Prompt { get; init; } = string.Empty;

        public string ReferenceAnswer { get; init; } = string.Empty;

        public IReadOnlyList<ExpectationFile> Expectations { get; init; } = [];
    }

    private sealed record ExpectationFile
    {
        public string Kind { get; init; } = "File";

        public string File { get; init; } = string.Empty;

        public string Member { get; init; } = string.Empty;

        public string Text { get; init; } = string.Empty;

        public int Start { get; init; }

        public int End { get; init; }

        public bool Required { get; init; } = true;
    }
}
