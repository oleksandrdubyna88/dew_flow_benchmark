using System.Text.Json;
using System.Text.Json.Serialization;
using Bench.Domain.Bank;

namespace Bench.Application.Bank;

/// <summary>A stored question as the JSON a reviewer is shown.
/// <para>
/// <b>Provenance is deliberately left out.</b> The file shape can carry <c>authorModel</c>, <c>state</c> and
/// existing <c>reviews</c>, and a reviewer is shown none of them: a mark that knows which model wrote the
/// question, or how the other reviewers voted, is partly about the author and partly about agreement. What is
/// shown is the question, its reference answer, its expectations and its seed — the seed because the reviewer
/// is asked whether the date looks invented.
/// </para>
/// <para>
/// It writes <see cref="BankQuestionFile"/> rather than a shape of its own, so what a reviewer reads is
/// literally the shape an author writes and the bank imports. One format.
/// </para></summary>
public static class BankExport
{
    private static readonly JsonSerializerOptions Pretty = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static BankQuestionFile ForReview(BankEntry entry) => new()
    {
        Group = entry.Group.Key.Value,
        Ordinal = entry.Question.Ordinal,
        Kind = entry.Question.Kind.ToString(),
        Id = entry.Question.Question.Id,
        Prompt = entry.Question.Question.Prompt,
        ReferenceAnswer = entry.Question.Question.ReferenceAnswer,
        Expectations = [.. entry.Question.Question.Expectations.Select(QuestionJson.FromExpectation)],
        Seed = entry.Question.Seed.Kind == "unstated"
            ? null
            : new SeedFile
            {
                Kind = entry.Question.Seed.Kind,
                Reference = entry.Question.Seed.Reference,
                At = entry.Question.Seed.At,
            },
    };

    public static string Json(BankEntry entry) => JsonSerializer.Serialize(ForReview(entry), Pretty);
}
