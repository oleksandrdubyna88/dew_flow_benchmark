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
/// <param name="Note">Anything the author said OUTSIDE its JSON. Kept and reported rather than dropped: the
/// first live batch prefaced its answer with "git access to this worktree is blocked, so I cannot date the
/// members" — a fact about the environment that the questions themselves could never carry, and the only place
/// it exists.</param>
/// <param name="Collisions">One line per candidate dropped as a duplicate, naming what it duplicated. Printed
/// rather than counted away: with three authors writing a third of a group each, "which question did this repeat"
/// is the operator's next question, and the stored id is the answer.</param>
public sealed record AuthoringReport(
    string AuthorModel,
    string PromptHash,
    int Proposed,
    int Duplicates,
    IReadOnlyList<string> Rejected,
    string Note = "",
    IReadOnlyList<string>? Collisions = null,
    IReadOnlyList<string>? Corrections = null)
{
    public IReadOnlyList<string> Collisions { get; init; } = Collisions ?? [];

    /// <summary>Facts the pass fixed rather than took the author's word for. Reported, never silent: a correction
    /// nobody sees is a defect nobody knows the author keeps making.</summary>
    public IReadOnlyList<string> Corrections { get; init; } = Corrections ?? [];

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
/// <param name="Worktree">The target's tree on disk, at the pinned commit. The agent is launched THERE, because
/// an author that cannot read the repository cannot anchor a question in it — found live on the first batch,
/// where the agent refused to invent line numbers for a commit it had no access to.</param>
/// <param name="History">The target's merge history, gathered by the caller because the agent cannot read it
/// from inside a worktree. Empty for the groups that do not need it.</param>
public sealed record AuthoringRequest(
    QuestionGroup Group,
    RepoUrl Target,
    CommitSha Commit,
    int Count,
    int Ordinal,
    TimeSpan Wall,
    string Worktree,
    string History = "",
    Func<string, CommitFact>? Commits = null);

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
            new AuthoringBrief(
                request.Group.Key.Value,
                request.Group.Title,
                request.Target,
                request.Commit,
                request.Count,
                request.History));

        if (prompt is not Outcome<RenderedPrompt>.Ok(var brief))
        {
            return AuthoringReport.Nothing(author.Config.ModelId, prompt.Match(_ => string.Empty, r => r));
        }

        var answered = await agent.AskAsync(
            new AgentAsk(author.Runtime, executable, brief.Text, request.Worktree, request.Wall, author.Config.ModelId),
            cancellationToken);

        if (answered is not Outcome<AgentAnswer>.Ok(var answer))
        {
            return AuthoringReport.Nothing(author.Config.ModelId, answered.Match(_ => string.Empty, r => r))
                with { PromptHash = brief.Hash };
        }

        var payload = Payload(answer.Text);
        var stored = await StoreAsync(bank, request, author, brief.Hash, Parse(payload.Json), now, cancellationToken);

        return stored with { Note = payload.Said };
    }

    /// <summary>The answer as question files, or the parse error as the batch's single rejection.
    /// <para>
    /// A fenced answer is unwrapped and nothing else is: agents wrap JSON in a code fence often enough that
    /// refusing it would reject good work over a formatting habit, while any further repair would start
    /// editing the questions themselves.
    /// </para></summary>
    private static Outcome<IReadOnlyList<BankQuestionFile>> Parse(string text)
    {
        var json = AgentJson.Unfence(text);

        try
        {
            var files = JsonSerializer.Deserialize<List<BankQuestionFile>>(json, Json) ?? [];

            return files.Count == 0
                ? Outcome<IReadOnlyList<BankQuestionFile>>.Failure(
                    "the author answered with an empty array — a legible outcome the contract offers for "
                    + "'I could not write one', and never a parse failure")
                : Outcome<IReadOnlyList<BankQuestionFile>>.Success(files);
        }
        catch (JsonException ex)
        {
            // The sample is what makes this rejection useful. Without it the report says an answer was
            // unreadable and nothing about WHAT was said — and the next edit to the prompt is made from
            // exactly that text. Found on the first live batch, where the reason alone explained nothing.
            return Outcome<IReadOnlyList<BankQuestionFile>>.Failure(
                $"the author's answer is not the shape the bank reads: {ex.Message} It began: {Sample(json)}");
        }
    }

    private static string Sample(string text) =>
        text.Length <= 200 ? text : text[..200] + "…";

    /// <summary>The JSON array out of an answer that may be wrapped in a fence or prefaced with prose.
    /// <para>
    /// Through <see cref="AgentJson"/>, which the vetting pass shares. It exists because of the first live
    /// batch: twice the agent had done the work — read the tree, found the members, verified the line numbers —
    /// and prefaced the array with a caveat about what it could NOT do. Discarding the whole answer for that
    /// threw away real work and told the operator only that something was unreadable. So the array is taken and
    /// the caveat is REPORTED, which is how the environment finding underneath it became visible at all.
    /// </para></summary>
    private static AgentPayload Payload(string text) =>
        AgentJson.Extract(text, '[', ']', candidate => Parse(candidate) is Outcome<IReadOnlyList<BankQuestionFile>>.Ok);

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
        var collisions = new List<string>();
        var stored = 0;

        // Against the BANK, not only against this batch. Measured 2026-08-18: one author over two calls left two
        // pairs of questions about one member in the same group, and nothing noticed — the in-batch check cannot
        // see a call that already finished. Three authors writing a third of a group each turn that into the
        // normal case, and a suite holding two questions about the same lines double-counts that member in every
        // score it ever produces.
        var alreadyThere = await StoredAsync(bank, request, cancellationToken);

        foreach (var candidate in admitted.Candidates.Where(c => !duplicates.Contains(c.Id)))
        {
            if (alreadyThere.TryGetValue(Dedup.MemberKey(candidate), out var twin))
            {
                collisions.Add($"'{candidate.Id}' repeats {twin} — same member, already in this group");
                continue;
            }

            var added = await bank.AddAsync(
                Row(candidate, request, author.Config.ModelId, now, request.Ordinal + stored), cancellationToken);

            if (added is Outcome<BankQuestion>.Fail refused)
            {
                rejected.Add($"'{candidate.Id}': {refused.Reason}");
                continue;
            }

            stored++;
        }

        return new AuthoringReport(author.Config.ModelId, promptHash, stored, duplicates.Count + collisions.Count, rejected)
        {
            Collisions = collisions,
            Corrections = admitted.Corrections,
        };
    }

    /// <summary>What this GROUP already holds, by member-level key, so a new candidate can be compared against
    /// the bank. Scoped to the group deliberately: a lookup question and a bug question about one member are two
    /// different questions, and dropping the second because of the first would delete real coverage.</summary>
    private static async Task<Dictionary<string, string>> StoredAsync(
        IQuestionBank bank, AuthoringRequest request, CancellationToken cancellationToken)
    {
        var found = await bank.QuestionsAsync(new BankQuery(request.Group.Key.Value), cancellationToken);

        return found.Match(
            entries => entries
                .GroupBy(e => Dedup.MemberKey(e.Question.Question), StringComparer.Ordinal)
                .Where(g => g.Key.Length > 0)
                .ToDictionary(g => g.Key, g => $"'{g.First().Question.Question.Id}'", StringComparer.Ordinal),
            // A bank we cannot read is not a bank with nothing in it, but refusing the whole pass over it would
            // throw away an author's work for a transient database. The pass continues and the report says so.
            _ => []);
    }

    /// <summary>Each file through the EXISTING admission rules. A question with no retrieval expectation and a
    /// candidate that does not name its author are already refused there, by name.</summary>
    private static (IReadOnlyList<QuestionCandidate> Candidates, IReadOnlyList<string> Rejected, IReadOnlyList<string> Corrections) Admit(
        IReadOnlyList<BankQuestionFile> files, AuthoringRequest request, string authorModel, DateTimeOffset now)
    {
        var candidates = new List<QuestionCandidate>();
        var rejected = new List<string>();
        var corrections = new List<string>();

        foreach (var file in files)
        {
            var read = QuestionJson.ToQuestion(file.ToQuestionFile(), request.Commit);
            if (read is Outcome<Question>.Fail unreadable)
            {
                // A rejection with its reason, not a throw: an author writing a kind this build does not
                // know is an ordinary event in a pass driven by a model, and the pass must keep going and
                // say which candidate it dropped.
                rejected.Add(unreadable.Reason);
                continue;
            }

            var question = ((Outcome<Question>.Ok)read).Value;
            var dated = Dated(Seed(file), request.Commits);

            if (dated.Correction.Length > 0)
            {
                corrections.Add(dated.Correction);
            }

            var proposed = QuestionCandidate.Propose(AuthoringSource.Synthetic, authorModel, dated.Seed, question);

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

        return (candidates, rejected, corrections);
    }

    /// <summary>The seed as the author declared it — and <c>unstated</c> at the beginning of time when it did
    /// not, which reads as <i>may recall</i> rather than as safe. The same rule the bank import follows, and
    /// the prompt says so in as many words: a guessed date is the one lie the whole memorisation check rests
    /// on.
    /// <para>
    /// Built through <see cref="QuestionSeed.Written"/>, which keeps the calendar day the author typed instead of
    /// converting it to an instant in our timezone — the arithmetic that made two faithful authors look like they
    /// dated a day early.
    /// </para>
    /// <para>
    /// <b>A <c>commit</c> seed keeps its kind even with no date.</b> The first shape flattened every dateless
    /// seed to <c>unstated</c>, and <see cref="Dated"/> acts only on <c>commit</c> — so an author doing exactly
    /// what the contract asks, naming the change and omitting a date it could not establish, disarmed the
    /// derivation written for that very case, and the question reached the bank reading <i>may recall</i> while
    /// its date sat in our own git the whole time. The KIND says what the seed is; the DATE says whether it
    /// claims a when. Conflating them cost the pr-diff group its dates.
    /// </para></summary>
    private static QuestionSeed Seed(BankQuestionFile file) =>
        file.Seed is { Kind.Length: > 0 } seed && (seed.At != default || Datable(seed))
            ? QuestionSeed.Written(seed.Kind.Trim(), seed.Reference.Trim(), seed.At)
            : new QuestionSeed("unstated", file.Seed?.Reference.Trim() ?? string.Empty, default);

    /// <summary>A seed <see cref="Dated"/> can date from the repository for itself: a commit, named.</summary>
    private static bool Datable(SeedFile seed) =>
        string.Equals(seed.Kind.Trim(), SeedCheck.CommitKind, StringComparison.OrdinalIgnoreCase)
        && seed.Reference.Trim().Length > 0;

    /// <summary>A commit seed's date taken from the REPOSITORY, not from the author.
    /// <para>
    /// <b>The reason first written here was wrong, and the record says so rather than quietly improving.</b> This
    /// was justified by "handed 345 lines of history and told to copy the dates verbatim, an author dated three
    /// commits a day early — systematically". Replayed through the real types on 2026-08-19, that finding is the
    /// bank's own timezone arithmetic: a bare date became midnight in the reading machine's offset and was
    /// normalised to UTC, one day back. The authors had copied correctly. The boundary is fixed in
    /// <see cref="QuestionSeed.Written"/>.
    /// </para>
    /// <para>
    /// The derivation stays, on the argument that survives: the author names the CHANGE and the repository dates
    /// it, which is the same principle as the harness performing retrieval instead of trusting a model's account
    /// of it. It also serves the honest case the contract asks for — an author that cannot establish a date omits
    /// it, and a named commit is datable without it. A disagreement is still reported as a correction, and NOW
    /// that line means something: with the shift removed, one that appears is the author's.
    /// </para></summary>
    private static (QuestionSeed Seed, string Correction) Dated(
        QuestionSeed seed, Func<string, CommitFact>? commits)
    {
        if (commits is null || !string.Equals(seed.Kind, SeedCheck.CommitKind, StringComparison.OrdinalIgnoreCase)
            || seed.Reference.Length == 0)
        {
            return (seed, string.Empty);
        }

        var fact = commits(seed.Reference);

        if (!fact.Exists)
        {
            return (seed, string.Empty);
        }

        var landed = new DateTimeOffset(fact.Date.ToDateTime(default), TimeSpan.Zero);
        var claimed = seed.At == default ? "nothing" : $"{SeedCheck.Stated(seed.At):yyyy-MM-dd}";

        // The DAY, through the one place that reads a seed's day — never a second spelling of this comparison.
        return seed.At != default && SeedCheck.Stated(seed.At) == fact.Date
            ? (seed, string.Empty)
            : (seed with { At = landed },
               $"seed {seed.Reference}: the author dated it {claimed}, the repository says {fact.Date:yyyy-MM-dd} — taken from the repository");
    }

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
