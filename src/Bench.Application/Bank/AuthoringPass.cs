using System.Text.Json;
using Bench.Domain;
using Bench.Domain.Authoring;
using Bench.Domain.Bank;
using Bench.Domain.Registry;
using Bench.Domain.Runs;
using Bench.Domain.Suites;
using Bench.Domain.Targets;

namespace Bench.Application.Bank;

/// <summary>What one author produced, and what was thrown away.
/// <para>
/// <see cref="Rejected"/> is reported rather than swallowed for the reason a rejection needs a reason at all:
/// it is the only record of what a source gets WRONG, and an authoring pass whose failures are invisible is a
/// pass nobody can improve the prompt from.
/// </para></summary>
/// <param name="Duplicates">Candidates thrown away because another candidate in the SAME batch already said
/// it. Three authors on one group independently write the question about the most obvious member in the
/// repository, so this number is expected to be non-zero rather than alarming.</param>
public sealed record AuthoringReport(
    string AuthorModel,
    string PromptHash,
    int Proposed,
    int Duplicates,
    IReadOnlyList<string> Rejected)
{
    public static AuthoringReport Nothing(string authorModel, string reason) =>
        new(authorModel, string.Empty, 0, 0, [reason]);

    public string Describe =>
        $"{AuthorModel}: {Proposed} proposed"
        + (Duplicates > 0 ? $", {Duplicates} duplicate(s)" : string.Empty)
        + (Rejected.Count > 0 ? $", {Rejected.Count} rejected" : string.Empty);
}

/// <param name="Group">Which of the five reading groups is being written.</param>
/// <param name="Count">How many questions to ask of each author in one call.</param>
/// <param name="Ordinal">Where in the group the first accepted question lands. The operator quotes ordinals
/// ("group 1, questions 1–10"), so they are assigned rather than generated.</param>
public sealed record AuthoringRequest(
    QuestionGroup Group,
    RepoUrl Target,
    CommitSha Commit,
    int Count,
    int Ordinal,
    TimeSpan Wall);

/// <summary>Driving a CLI agent to write questions, and admitting what it wrote through the rules the bank
/// already has.
/// <para>
/// <b>Nothing here repairs an answer.</b> A malformed reply becomes a REJECTION carrying the parse error, and
/// the reason is attribution rather than strictness: a pass that fixes its author's JSON produces questions
/// that are partly the pass's, and "which model wrote this set" stops being answerable — which is the one
/// property the founding plan insists on, because a set's ceiling becomes its author's ceiling.
/// </para>
/// <para>
/// Admission is <see cref="QuestionCandidate.Propose"/> and <see cref="Dedup.Find"/>, both already here. A
/// second admission rule would drift from the first, and the drifted one would be the unread one.
/// </para></summary>
public static class AuthoringPass
{
    /// <summary>Reads an author's answer as <see cref="BankQuestionFile"/> — the shape
    /// <c>bench questions import</c> already reads, seed and all.
    /// <para>
    /// Not a shape of its own, and not <see cref="QuestionFile"/> either: that one carries no seed, and the
    /// seed is the input to the whole memorisation check. Using the import's shape means an authored batch is
    /// literally an importable file, so there is ONE format rather than two that agree until somebody edits
    /// one.
    /// </para></summary>
    private static readonly JsonSerializerOptions Json = QuestionJson.ReaderOptions;

    public static async Task<AuthoringReport> RunAsync(
        ICliAgentRuntime agent,
        IQuestionBank bank,
        string promptRoot,
        AuthoringRequest request,
        RegisteredModel author,
        string executable,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var prompt = PromptCatalog.Author(
            promptRoot,
            new AuthoringBrief(request.Group.Key.Value, request.Group.Title, request.Target, request.Commit, request.Count));

        if (prompt is not Outcome<RenderedPrompt>.Ok(var brief))
        {
            return AuthoringReport.Nothing(author.Config.ModelId, prompt.Match(_ => string.Empty, r => r));
        }

        var answered = await agent.AskAsync(
            new AgentAsk(author.Runtime, executable, brief.Text, Environment.CurrentDirectory, request.Wall),
            cancellationToken);

        if (answered is not Outcome<AgentAnswer>.Ok(var answer))
        {
            return AuthoringReport.Nothing(author.Config.ModelId, answered.Match(_ => string.Empty, r => r))
                with { PromptHash = brief.Hash };
        }

        return await StoreAsync(bank, request, author, brief.Hash, Parse(answer.Text), now, cancellationToken);
    }

    /// <summary>The answer as question files, or the parse error as the batch's single rejection.
    /// <para>
    /// A fenced answer is unwrapped and nothing else is: agents wrap JSON in a code fence often enough that
    /// refusing it would reject good work over a formatting habit, while any further repair would start
    /// editing the questions themselves.
    /// </para></summary>
    private static Outcome<IReadOnlyList<BankQuestionFile>> Parse(string text)
    {
        var json = Unfence(text);

        try
        {
            var files = JsonSerializer.Deserialize<List<BankQuestionFile>>(json, Json) ?? [];

            return files.Count == 0
                ? Outcome<IReadOnlyList<BankQuestionFile>>.Failure("the author answered with an empty array")
                : Outcome<IReadOnlyList<BankQuestionFile>>.Success(files);
        }
        catch (JsonException ex)
        {
            return Outcome<IReadOnlyList<BankQuestionFile>>.Failure(
                $"the author's answer is not the shape the bank reads: {ex.Message}");
        }
    }

    private static string Unfence(string text)
    {
        var trimmed = text.Trim();

        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var firstBreak = trimmed.IndexOf('\n');
        var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);

        return firstBreak > 0 && lastFence > firstBreak
            ? trimmed[(firstBreak + 1)..lastFence].Trim()
            : trimmed;
    }

    private static async Task<AuthoringReport> StoreAsync(
        IQuestionBank bank,
        AuthoringRequest request,
        RegisteredModel author,
        string promptHash,
        Outcome<IReadOnlyList<BankQuestionFile>> parsed,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (parsed is not Outcome<IReadOnlyList<BankQuestionFile>>.Ok(var files))
        {
            return AuthoringReport.Nothing(author.Config.ModelId, parsed.Match(_ => string.Empty, r => r))
                with { PromptHash = promptHash };
        }

        var admitted = Admit(files, request, author.Config.ModelId, now);
        var duplicates = Dedup.Find([.. admitted.Candidates]).SelectMany(c => c.CandidateIds.Skip(1)).ToHashSet(StringComparer.Ordinal);
        var rejected = admitted.Rejected.ToList();
        var stored = 0;

        foreach (var candidate in admitted.Candidates.Where(c => !duplicates.Contains(c.Id)))
        {
            var added = await bank.AddAsync(
                Row(candidate, request, author.Config.ModelId, now, request.Ordinal + stored), cancellationToken);

            if (added is Outcome<BankQuestion>.Fail refused)
            {
                rejected.Add($"'{candidate.Id}': {refused.Reason}");
                continue;
            }

            stored++;
        }

        return new AuthoringReport(author.Config.ModelId, promptHash, stored, duplicates.Count, rejected);
    }

    /// <summary>Each file through the EXISTING admission rules. A question with no retrieval expectation and a
    /// candidate that does not name its author are already refused there, by name.</summary>
    private static (IReadOnlyList<QuestionCandidate> Candidates, IReadOnlyList<string> Rejected) Admit(
        IReadOnlyList<BankQuestionFile> files, AuthoringRequest request, string authorModel, DateTimeOffset now)
    {
        var candidates = new List<QuestionCandidate>();
        var rejected = new List<string>();

        foreach (var file in files)
        {
            var question = QuestionJson.ToQuestion(file.ToQuestionFile(), request.Commit);
            var proposed = QuestionCandidate.Propose(AuthoringSource.Synthetic, authorModel, Seed(file), question);

            proposed.Match(
                candidate =>
                {
                    candidates.Add(candidate);
                    return 0;
                },
                reason =>
                {
                    rejected.Add($"'{file.Id}': {reason}");
                    return 0;
                });
        }

        return (candidates, rejected);
    }

    /// <summary>The seed as the author declared it — and <c>unstated</c> at the beginning of time when it did
    /// not, which reads as <i>may recall</i> rather than as safe. The same rule the bank import follows, and
    /// the prompt says so in as many words: a guessed date is the one lie the whole memorisation check rests
    /// on.</summary>
    private static QuestionSeed Seed(BankQuestionFile file) =>
        file.Seed is { Kind.Length: > 0 } seed && seed.At != default
            ? new QuestionSeed(seed.Kind.Trim(), seed.Reference.Trim(), seed.At)
            : new QuestionSeed("unstated", file.Seed?.Reference.Trim() ?? string.Empty, default);

    private static BankQuestion Row(
        QuestionCandidate candidate,
        AuthoringRequest request,
        string authorModel,
        DateTimeOffset now,
        int ordinal) =>
        BankQuestion.Create(
            request.Group.Id,
            ordinal,
            TaskKind.Reading,
            candidate.Question,
            codeTaskJson: string.Empty,
            AuthoringSource.Synthetic,
            authorModel,
            candidate.Seed,
            request.Target,
            request.Commit,
            now).Match(row => row, _ => throw new InvalidOperationException(
                "a candidate that passed Propose cannot fail Create — they share the same rule"));
}
