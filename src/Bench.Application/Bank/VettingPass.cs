using System.Text.Json;
using Bench.Domain;
using Bench.Domain.Authoring;
using Bench.Domain.Bank;
using Bench.Domain.Registry;
using Bench.Domain.Suites;
using Bench.Domain.Targets;

namespace Bench.Application.Bank;

/// <summary>One reviewer slot, resolved on this machine: the row, the registry model bound to it, and the
/// executable that reference points at here.
/// <para>
/// Resolved before any launch, exactly as an author is. Discovering at question forty that a slot's executable
/// reference points at nothing is discovering it forty launches too late.
/// </para></summary>
public sealed record ReviewerSlot(Reviewer Reviewer, RegisteredModel Model, string Executable);

/// <param name="Limit">How many <c>Proposed</c> questions this pass walks. A ceiling rather than "all of
/// them", because every question costs one launch per slot and an operator sizing a batch needs to be able to
/// stop it at a known number.</param>
/// <param name="AllowSelfReview">Takes a mark from a model that authored the question, at the cost
/// <see cref="SelfReview.Cost"/> states. Off by default.</param>
public sealed record VettingRequest(
    QuestionGroup Group,
    RepoUrl Target,
    CommitSha Commit,
    TimeSpan Wall,
    string Worktree,
    bool AllowSelfReview,
    int Limit);

/// <param name="Marks">What each slot said, as <c>reviewer-1 approved</c> / <c>reviewer-2 rejected: …</c>.</param>
/// <param name="Skipped">Slots that did NOT mark this question, and why — a refused self-review, an agent that
/// could not be reached, an unreadable answer. Reported rather than silently absent: a question that looks
/// unreviewed for a reason nobody recorded is one somebody re-runs the whole pass over.</param>
/// <param name="Defects">What a mechanical check found before anything was launched: ground truth that does not
/// resolve against the tree, or a scoring term that cannot discriminate. Non-empty means NO reviewer was asked —
/// there is nothing to judge until it is fixed, and paying three agents to discover substring arithmetic is what
/// this field exists to stop. Both kinds are here because both came from real reviewer rejections.</param>
public sealed record QuestionVerdicts(
    string QuestionId,
    IReadOnlyList<string> Marks,
    PromotionDecision Decision,
    IReadOnlyList<string> Skipped,
    IReadOnlyList<string> Defects)
{
    public bool Broken => Defects.Count > 0;
}

public sealed record VettingReport(
    string PromptHash,
    IReadOnlyList<QuestionVerdicts> Questions,
    IReadOnlyList<string> Refusals,
    string Cost = "")
{
    public static VettingReport Nothing(string reason) => new(string.Empty, [], [reason]);

    public int Accepted => Questions.Count(q => q.Decision.Kind == PromotionKind.Accept);

    public int Rejected => Questions.Count(q => q.Decision.Kind == PromotionKind.Reject);

    public int Waiting => Questions.Count(q => q.Decision.Kind == PromotionKind.Wait);

    /// <summary>Questions no reviewer was launched for, because their ground truth does not resolve.</summary>
    public int Broken => Questions.Count(q => q.Broken);

    /// <summary>Launches this pass did NOT spend, because a mechanical check answered first. The number the
    /// pre-gate exists for.</summary>
    public int LaunchesSaved { get; init; }

    public string Describe =>
        $"{Questions.Count} question(s): {Accepted} accepted, {Rejected} rejected, {Waiting} waiting"
        + (Broken > 0 ? $", {Broken} with broken anchors ({LaunchesSaved} launch(es) not spent)" : string.Empty)
        + (Refusals.Count > 0 ? $", {Refusals.Count} refused" : string.Empty);
}

/// <summary>Driving each configured reviewer over a group's proposed questions, and letting the marks decide.
/// <para>
/// The marks are stored through <see cref="IQuestionBank.ReviewAsync"/> — the same path
/// <c>bench questions review</c> uses — so a mark written by an agent and one typed by a person are
/// indistinguishable afterwards. That is correct rather than sloppy: a review is a judgement, and its
/// provenance is the reviewer slot, which is recorded either way.
/// </para>
/// <para>
/// <b>The question's own state moves only under <see cref="Promotion"/>'s strict rule.</b> Nothing here
/// invents a threshold, and nothing accepts a question because most reviewers liked it.
/// </para></summary>
public static class VettingPass
{
    public static async Task<VettingReport> RunAsync(
        ICliAgentRuntime agent,
        IQuestionBank bank,
        string promptRoot,
        VettingRequest request,
        IReadOnlyList<ReviewerSlot> slots,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (slots.Count == 0)
        {
            return VettingReport.Nothing(
                "no reviewer slot has a model bound — bind one with 'bench questions bind --reviewer <key> "
                + "--model <registry key>', because a slot that names nobody cannot be marked by anything");
        }

        var configured = await bank.ReviewersAsync(cancellationToken);
        var pending = await ProposedAsync(bank, request, cancellationToken);

        if (configured is not Outcome<IReadOnlyList<Reviewer>>.Ok(var reviewers))
        {
            return VettingReport.Nothing(configured.Match(_ => string.Empty, reason => reason));
        }

        if (pending is not Outcome<IReadOnlyList<BankEntry>>.Ok(var questions))
        {
            return VettingReport.Nothing(pending.Match(_ => string.Empty, reason => reason));
        }

        var verdicts = new List<QuestionVerdicts>();
        var refusals = new List<string>();
        var hash = string.Empty;
        var cost = string.Empty;

        foreach (var entry in questions)
        {
            var vetted = await VetAsync(agent, bank, promptRoot, request, slots, reviewers, entry, now, cancellationToken);

            verdicts.Add(vetted.Verdicts);
            refusals.AddRange(vetted.Refusals);
            hash = vetted.PromptHash.Length > 0 ? vetted.PromptHash : hash;
            cost = vetted.Cost.Length > 0 ? vetted.Cost : cost;
        }

        return new VettingReport(hash, verdicts, refusals, cost)
        {
            LaunchesSaved = verdicts.Count(v => v.Broken) * slots.Count,
        };
    }

    private sealed record Vetted(QuestionVerdicts Verdicts, IReadOnlyList<string> Refusals, string PromptHash, string Cost);

    /// <summary>One question through every slot, then the promotion rule over the marks the bank now holds.
    /// <para>
    /// The decision reads the marks BACK from the bank rather than counting the ones this pass just wrote,
    /// because a slot may have marked this question in an earlier pass and "every configured reviewer has
    /// approved" is a question about the bank, not about this run.
    /// </para></summary>
    private static async Task<Vetted> VetAsync(
        ICliAgentRuntime agent,
        IQuestionBank bank,
        string promptRoot,
        VettingRequest request,
        IReadOnlyList<ReviewerSlot> slots,
        IReadOnlyList<Reviewer> reviewers,
        BankEntry entry,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var marks = new List<string>();
        var skipped = new List<string>();
        var refusals = new List<string>();
        var hash = string.Empty;
        var cost = string.Empty;

        // The mechanical half FIRST, and nothing is launched when it fails. Both halves were taken from what live
        // reviewers actually wrote: every note led with the anchor check, and the two rejections the panel
        // produced were both substring arithmetic about the scoring terms.
        var broken = AnchorCheck
            .Verify(entry.Question.Question, Reader(request.Worktree))
            .Where(proof => !proof.Resolved)
            .Select(proof => proof.Describe)
            .Concat(QuestionSanity.Check(entry.Question.Question).Select(defect => defect.Describe))
            .ToList();

        if (broken.Count > 0)
        {
            return new Vetted(
                new QuestionVerdicts(
                    entry.Question.Question.Id,
                    marks,
                    new PromotionDecision(
                        PromotionKind.Wait,
                        "a mechanical check found a defect, so no reviewer was asked — a question whose ground "
                        + "truth or scoring term is wrong has nothing to judge until it is fixed"),
                    skipped,
                    broken),
                refusals,
                hash,
                cost);
        }

        foreach (var slot in slots)
        {
            var asked = await AskAsync(agent, promptRoot, request, slot, entry, cancellationToken);

            hash = asked.PromptHash.Length > 0 ? asked.PromptHash : hash;
            cost = asked.Cost.Length > 0 ? asked.Cost : cost;

            if (asked.Verdict is not Outcome<ReviewAnswer>.Ok(var answer))
            {
                skipped.Add($"{slot.Reviewer.Key}: {asked.Verdict.Match(_ => string.Empty, reason => reason)}");
                continue;
            }

            var stored = await bank.ReviewAsync(
                entry.Question.Question.Id, slot.Reviewer.Key.Value, answer.Verdict, answer.Note, now, cancellationToken);

            stored.Match(
                _ =>
                {
                    marks.Add($"{slot.Reviewer.Key} {answer.Verdict.ToString().ToLowerInvariant()}: {answer.Note}");
                    return 0;
                },
                reason =>
                {
                    refusals.Add($"'{entry.Question.Question.Id}' / {slot.Reviewer.Key}: {reason}");
                    return 0;
                });
        }

        var decision = await DecideAsync(bank, reviewers, entry, cancellationToken);

        return new Vetted(
            new QuestionVerdicts(entry.Question.Question.Id, marks, decision, skipped, []), refusals, hash, cost);
    }

    private sealed record Asked(Outcome<ReviewAnswer> Verdict, string PromptHash, string Cost);

    /// <summary>One slot's verdict on one question: the self-review gate, the rendered brief, the launch, and
    /// the answer read as a value.</summary>
    private static async Task<Asked> AskAsync(
        ICliAgentRuntime agent,
        string promptRoot,
        VettingRequest request,
        ReviewerSlot slot,
        BankEntry entry,
        CancellationToken cancellationToken)
    {
        var allowed = SelfReview.Check(
            slot.Reviewer.Key.Value,
            slot.Model.Config.ModelId,
            entry.Question.AuthorModel,
            request.AllowSelfReview);

        if (allowed is Outcome<string>.Fail refused)
        {
            return new Asked(Outcome<ReviewAnswer>.Failure(refused.Reason), string.Empty, string.Empty);
        }

        var prompt = PromptCatalog.Review(
            promptRoot,
            new ReviewBrief(
                request.Group.Key.Value, request.Group.Title, request.Target, request.Commit, BankExport.Json(entry)));

        if (prompt is not Outcome<RenderedPrompt>.Ok(var brief))
        {
            return new Asked(
                Outcome<ReviewAnswer>.Failure(prompt.Match(_ => string.Empty, reason => reason)), string.Empty, string.Empty);
        }

        var answered = await agent.AskAsync(
            new AgentAsk(slot.Model.Runtime, slot.Executable, brief.Text, request.Worktree, request.Wall),
            cancellationToken);

        return new Asked(
            answered.Match(answer => Read(answer.Text), Outcome<ReviewAnswer>.Failure),
            brief.Hash,
            ((Outcome<string>.Ok)allowed).Value);
    }

    private static async Task<PromotionDecision> DecideAsync(
        IQuestionBank bank, IReadOnlyList<Reviewer> reviewers, BankEntry entry, CancellationToken cancellationToken)
    {
        var held = await bank.ReviewsAsync([entry.Question.Id], cancellationToken);

        if (held is not Outcome<IReadOnlyList<QuestionReview>>.Ok(var marks))
        {
            return new PromotionDecision(PromotionKind.Wait, held.Match(_ => string.Empty, reason => reason));
        }

        var decision = Promotion.Decide(reviewers, marks);
        var state = decision.Kind switch
        {
            PromotionKind.Accept => CandidateState.Accepted,
            PromotionKind.Reject => CandidateState.Rejected,
            _ => CandidateState.Proposed,
        };

        if (state == CandidateState.Proposed)
        {
            return decision;
        }

        var moved = await bank.SetStateAsync(entry.Question.Question.Id, state, cancellationToken);

        return moved is Outcome<BankQuestion>.Fail stuck
            ? decision with { Reason = $"{decision.Reason}, but the state could not be moved: {stuck.Reason}" }
            : decision;
    }

    /// <summary>The group's proposed questions, oldest ordinal first, capped by the request's limit.
    /// <para>
    /// Filtered on <see cref="CandidateState.Proposed"/> here rather than in the query, because the bank's
    /// query offers "accepted only" and nothing else — and re-vetting an already-decided question would
    /// overwrite a human's mark with a machine's.
    /// </para></summary>
    private static async Task<Outcome<IReadOnlyList<BankEntry>>> ProposedAsync(
        IQuestionBank bank, VettingRequest request, CancellationToken cancellationToken)
    {
        var found = await bank.QuestionsAsync(new BankQuery(request.Group.Key.Value), cancellationToken);

        return found.Match(
            entries => Outcome<IReadOnlyList<BankEntry>>.Success(
                [.. entries.Where(e => e.Question.State == CandidateState.Proposed).Take(request.Limit)]),
            Outcome<IReadOnlyList<BankEntry>>.Failure);
    }

    /// <summary>Reads a file of the checked-out tree for the anchor check, through the shared path guard.
    /// <para>
    /// An anchor's path arrives as DATA — an agent wrote it — so it can spell its way out of the tree, and a
    /// path that escapes reads as <see cref="TreeFile.Absent"/>: from the question's point of view an anchor
    /// pointing outside the repository points at nothing, which is exactly the verdict wanted.
    /// </para></summary>
    private static Func<string, TreeFile> Reader(string worktree) =>
        relative =>
        {
            if (RootedPath.Under(worktree, relative) is not Outcome<string>.Ok(var full) || !File.Exists(full))
            {
                return TreeFile.Absent;
            }

            return TreeFile.Of(File.ReadAllLines(full));
        };

    /// <param name="Note">Mandatory on a rejection: it is the only record of what an author gets wrong, and
    /// the next edit to an authoring prompt is made from exactly this text.</param>
    private sealed record ReviewAnswer(ReviewVerdict Verdict, string Note);

    /// <summary>A reviewer's answer as a verdict, or a named refusal. Never a repair.
    /// <para>
    /// <b>A missing or unreadable verdict is NOT an approval.</b> The import's <c>ReviewFile</c> shape defaults
    /// its verdict to <c>Approved</c>, which is right for a file a person wrote and wrong here in the way that
    /// matters most: an agent whose answer lost its verdict field would silently approve every question it was
    /// shown. So this reads its own shape, and demands the word.
    /// </para></summary>
    private static Outcome<ReviewAnswer> Read(string text)
    {
        var payload = AgentJson.Extract(text, '{', '}', Parses);

        try
        {
            var file = JsonSerializer.Deserialize<ReviewAnswerFile>(payload.Json, QuestionJson.ReaderOptions);

            return file is null
                ? Outcome<ReviewAnswer>.Failure("the reviewer answered with nothing")
                : Verdict(file);
        }
        catch (JsonException ex)
        {
            return Outcome<ReviewAnswer>.Failure(
                $"the reviewer's answer is not a verdict object: {ex.Message} It began: {Sample(payload.Json)}");
        }
    }

    private static Outcome<ReviewAnswer> Verdict(ReviewAnswerFile file)
    {
        if (!Enum.TryParse<ReviewVerdict>(file.Verdict.Trim(), ignoreCase: true, out var verdict))
        {
            return Outcome<ReviewAnswer>.Failure(
                $"'{file.Verdict}' is not a verdict — the contract asks for 'approved' or 'rejected', and an "
                + "unreadable verdict must never read as approval");
        }

        return verdict == ReviewVerdict.Rejected && file.Note.Trim().Length == 0
            ? Outcome<ReviewAnswer>.Failure(
                "a rejection with no note is refused — it is the only record of what an author gets wrong, and "
                + "'low quality' teaches nobody anything")
            : Outcome<ReviewAnswer>.Success(new ReviewAnswer(verdict, file.Note.Trim()));
    }

    private static bool Parses(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<ReviewAnswerFile>(json, QuestionJson.ReaderOptions)
                is { Verdict.Length: > 0 };
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string Sample(string text) => text.Length <= 200 ? text : text[..200] + "…";

    /// <summary>The reviewer's wire shape. Its verdict has NO default on purpose — see <see cref="Read"/>.</summary>
    private sealed record ReviewAnswerFile
    {
        public string Verdict { get; init; } = string.Empty;

        public string Note { get; init; } = string.Empty;
    }
}
