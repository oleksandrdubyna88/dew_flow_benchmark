using System.Text.Json;
using Bench.Domain;
using Bench.Domain.Suites;
using Bench.Domain.Targets;

namespace Bench.Application;

/// <summary>Reads a suite from JSON and freezes it. The authored file is the DRAFT; what gets measured
/// is the frozen version it produces, and the file can never be the thing a result names — the version
/// hash is. That separation is the point: upstream, a measured question set lived only in a database
/// while the file claiming to be it had drifted several versions behind, and nothing ever said so.
/// <para>
/// The question shape itself lives in <see cref="QuestionJson"/>, shared with the bank import: a suite
/// file and a bank row describe the same thing, and two readers of one format is two formats wearing one
/// name.
/// </para></summary>
public static class SuiteJsonLoader
{
    public static Outcome<Suite> Load(string json, CommitSha authoredAt) =>
        Parse(json).Match(file => Build(file, authoredAt), Outcome<Suite>.Failure);

    private static Outcome<SuiteFile> Parse(string json)
    {
        try
        {
            var file = JsonSerializer.Deserialize<SuiteFile>(json, QuestionJson.ReaderOptions);
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
            (acc, q) => acc.Match(s => s.With(QuestionJson.ToQuestion(q, authoredAt)), Outcome<Suite>.Failure));

        return draft.Match(s => s.Freeze(), Outcome<Suite>.Failure);
    }

    private sealed record SuiteFile
    {
        public string Id { get; init; } = string.Empty;

        public IReadOnlyList<QuestionFile> Questions { get; init; } = [];
    }
}
