using Bench.Application;
using Bench.Application.Bank;
using Bench.Application.Registry;
using Bench.Domain;
using Bench.Domain.Authoring;
using Bench.Domain.Bank;
using Bench.Domain.Registry;
using Bench.Domain.Targets;
using Bench.Infrastructure.Models;

namespace Bench.Cli;

/// <summary>The question bank, from the command line.
/// <para>
/// Questions arrive two ways now. By IMPORT — authored elsewhere, reviewed elsewhere — and by AUTHOR, which
/// drives a CLI agent to write them (`research/PLAN_question_authoring.md`). Both land as the same rows through
/// the same admission rules, and the authored ones are <c>Proposed</c> until something vouches for them —
/// which is what keeps "a machine wrote a thousand overnight" from meaning "a thousand are measurable".
/// </para></summary>
public static class QuestionsCommand
{
    public static async Task<int> RunAsync(
        CommandLine command,
        IQuestionBank bank,
        ICliAgentRuntime agents,
        IModelRegistry registry,
        ISecretSource secrets,
        ICheckoutProvider checkouts,
        WorkspaceTrust trust,
        TimeProvider clock,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken) =>
        command.Operand(0) switch
        {
            "import" => await ImportAsync(command, bank, clock, output, error, cancellationToken),
            "author" => await AuthorAsync(command, bank, agents, registry, secrets, checkouts, trust, clock, output, error, cancellationToken),
            "list" => await ListAsync(command, bank, output, error, cancellationToken),
            "groups" => await GroupsAsync(bank, output, error, cancellationToken),
            "reviewers" => await ReviewersAsync(bank, output, error, cancellationToken),
            "bind" => await BindAsync(command, bank, registry, output, error, cancellationToken),
            "vet" => await VetAsync(command, bank, agents, registry, secrets, checkouts, trust, clock, output, error, cancellationToken),
            "review" => await ReviewAsync(command, bank, clock, output, error, cancellationToken),
            "accept" or "reject" => await StateAsync(command, bank, output, error, cancellationToken),
            "move" => await MoveAsync(command, bank, clock, output, error, cancellationToken),
            var other => Fail(
                error,
                other.Length == 0
                    ? $"bench questions needs an action — {Actions}"
                    : $"unknown questions action '{other}' — try {Actions}"),
        };

    private const string Actions =
        "'import', 'author', 'vet', 'list', 'groups', 'reviewers', 'bind', 'review', 'accept', 'reject' or 'move'";

    /// <summary>`bench questions author` — a CLI agent writes candidates for one group.
    /// <para>
    /// Every author is resolved BEFORE any launch: a disabled row, a runtime that is not a CLI, and an
    /// executable reference that resolves to nothing on this machine are each refused by name. Discovering any
    /// of them at question forty of a batch is discovering it three hundred launches too late — the same rule
    /// <c>bench run</c> follows for its subjects.
    /// </para>
    /// <para>
    /// Candidates land <c>Proposed</c>, and nothing here accepts anything. A pipeline that both wrote and
    /// vouched for its own questions would produce a bank whose quality is its author's opinion of itself.
    /// </para></summary>
    private static async Task<int> AuthorAsync(
        CommandLine command,
        IQuestionBank bank,
        ICliAgentRuntime agents,
        IModelRegistry registry,
        ISecretSource secrets,
        ICheckoutProvider checkouts,
        WorkspaceTrust trust,
        TimeProvider clock,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var inputs = AuthoringInputs.Read(command);

        if (inputs is Outcome<AuthoringInputs>.Fail bad)
        {
            return Fail(error, bad.Reason);
        }

        var settings = ((Outcome<AuthoringInputs>.Ok)inputs).Value;
        var found = await GroupAsync(bank, settings.Group, cancellationToken);

        if (found is not Outcome<QuestionGroup>.Ok(var group))
        {
            error.WriteLine($"bench: {found.Match(_ => string.Empty, reason => reason)}");
            return ExitCodes.Environment;
        }

        // The target's TREE, at the pinned commit, before any agent is launched. Found live on the first
        // batch: asked to write about a commit it had no access to, the agent refused and said so rather than
        // inventing line numbers — the correct answer, and a defect in how it had been called. An author with
        // no repository cannot anchor a question in one.
        var tree = await checkouts.EnsureAsync(MeasurementTarget.At(settings.Target, settings.Commit), cancellationToken);

        if (tree is not Outcome<string>.Ok(var worktree))
        {
            error.WriteLine($"bench: the target could not be checked out — {tree.Match(_ => string.Empty, reason => reason)}");
            return ExitCodes.Environment;
        }

        output.WriteLine($"group    {group.Key} — {group.Title}");
        output.WriteLine($"target   {settings.Target.Value}@{settings.Commit.Value[..12]}");
        output.WriteLine($"tree     {worktree}");
        output.WriteLine($"trust    {Trusted(trust, worktree, command)}");

        var request = new AuthoringRequest(
            group, settings.Target, settings.Commit, settings.Count, settings.Ordinal, settings.Wall, worktree);

        var written = 0;

        foreach (var key in settings.Authors)
        {
            var author = await ResolveAuthorAsync(registry, secrets, key, cancellationToken);

            if (author is not Outcome<ResolvedAuthor>.Ok(var resolved))
            {
                error.WriteLine($"bench: {author.Match(_ => string.Empty, reason => reason)}");
                return ExitCodes.Environment;
            }

            var report = await AuthoringPass.RunAsync(
                agents,
                bank,
                settings.PromptRoot,
                request with { Ordinal = request.Ordinal + written },
                resolved.Model,
                resolved.Executable,
                clock.GetUtcNow(),
                cancellationToken);

            output.WriteLine($"authored {report.Describe}");

            if (report.PromptHash.Length > 0)
            {
                output.WriteLine($"prompt   {report.PromptHash[..12]}  (prompts/author/{group.Key})");
            }

            if (report.Note.Length > 0)
            {
                // What the author said outside its JSON. The first live batch reported a blocked git in the
                // worktree this way, and nothing else in the system could have carried that fact.
                output.WriteLine($"  note   {report.Note.Split('\n')[0]}");
            }

            foreach (var collision in report.Collisions)
            {
                // With three authors writing a third of a group each, "which question did this repeat" is the
                // operator's next question, and a bare count cannot answer it.
                output.WriteLine($"  dupe   {collision}");
            }

            foreach (var rejection in report.Rejected)
            {
                // Printed, never counted and dropped: a rejection is the only record of what a source gets
                // wrong, and it is what the next edit to the prompt gets made from.
                output.WriteLine($"  reject {rejection}");
            }

            written += report.Proposed;
        }

        output.WriteLine();
        output.WriteLine($"proposed {written} question(s) — none accepted, because nothing has vouched for them yet");

        return written > 0 ? ExitCodes.Pass : ExitCodes.NoReport;
    }

    private sealed record ResolvedAuthor(RegisteredModel Model, string Executable);

    private static async Task<Outcome<ResolvedAuthor>> ResolveAuthorAsync(
        IModelRegistry registry, ISecretSource secrets, string key, CancellationToken cancellationToken)
    {
        var found = await registry.FindAsync(key, cancellationToken);

        return found.Match(
            model => ModelResolution.Executable(model, secrets).Match(
                executable => Outcome<ResolvedAuthor>.Success(new ResolvedAuthor(model, executable)),
                Outcome<ResolvedAuthor>.Failure),
            Outcome<ResolvedAuthor>.Failure);
    }

    private static async Task<Outcome<QuestionGroup>> GroupAsync(
        IQuestionBank bank, string key, CancellationToken cancellationToken)
    {
        var groups = await bank.GroupsAsync(cancellationToken);

        return groups.Match(
            all => all.FirstOrDefault(g => string.Equals(g.Key.Value, key, StringComparison.OrdinalIgnoreCase)) is { } group
                ? Outcome<QuestionGroup>.Success(group)
                : Outcome<QuestionGroup>.Failure(
                    $"the bank has no group '{key}' — it holds "
                    + (all.Count == 0 ? "none" : string.Join(", ", all.Select(g => g.Key.Value)))),
            Outcome<QuestionGroup>.Failure);
    }

    /// <param name="PromptRoot">Where the catalog lives. Configuration rather than a constant, because the
    /// prompt is the largest measured axis in this system and an operator comparing two of them needs to be
    /// able to point at two directories.</param>
    private sealed record AuthoringInputs(
        string Group,
        IReadOnlyList<string> Authors,
        RepoUrl Target,
        CommitSha Commit,
        int Count,
        int Ordinal,
        TimeSpan Wall,
        string PromptRoot)
    {
        /// <summary>Ten minutes. An agent asked for ten questions about an unfamiliar repository reads files
        /// before it writes any, and a ceiling tight enough to feel brisk is one that reports working authors
        /// as hangs.</summary>
        private const int DefaultWallSeconds = 600;

        public static Outcome<AuthoringInputs> Read(CommandLine command)
        {
            var group = command.Value("group");
            var authors = command.List("authors");
            var count = command.Int("count", 5);
            var wall = command.Int("wall-seconds", DefaultWallSeconds);

            if (group.Length == 0 || authors.Count == 0)
            {
                return Outcome<AuthoringInputs>.Failure(
                    "--group and --authors are required — an authoring pass with no author writes nothing, and one "
                    + "with no group has nowhere to put what it writes");
            }

            if (count < 1 || wall < 1)
            {
                return Outcome<AuthoringInputs>.Failure("--count and --wall-seconds must both be positive");
            }

            return RepoUrl.Parse(command.Value("repo")).Match(
                repo => CommitSha.Parse(command.Value("commit")).Match(
                    commit => Outcome<AuthoringInputs>.Success(new AuthoringInputs(
                        group,
                        authors,
                        repo,
                        commit,
                        count,
                        command.Int("ordinal", 1),
                        TimeSpan.FromSeconds(wall),
                        command.Value("prompts", "prompts"))),
                    Outcome<AuthoringInputs>.Failure),
                Outcome<AuthoringInputs>.Failure);
        }
    }

    private static async Task<int> ImportAsync(
        CommandLine command,
        IQuestionBank bank,
        TimeProvider clock,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var path = command.Value("file");

        if (path.Length == 0)
        {
            return Fail(error, "--file is required");
        }

        if (!File.Exists(path))
        {
            error.WriteLine($"bench: import file not found: {path}");
            return ExitCodes.Environment;
        }

        var read = BankImport.Read(await File.ReadAllTextAsync(path, cancellationToken));

        if (read is not Outcome<BankFile>.Ok(var file))
        {
            return Fail(error, read.Reason());
        }

        return Report(output, await BankImport.ApplyAsync(bank, file, clock, cancellationToken));
    }

    /// <summary>A refusal is REPORTED, never silent, and it decides the exit code.
    /// <para>
    /// Exit 1 rather than 0 when anything was refused: an import that quietly took 190 of 200 questions and
    /// exited green is how a selection ends up measuring a set nobody agreed to. Nothing was measured, so
    /// it is not a regression either — but it is not a pass.
    /// </para></summary>
    private static int Report(TextWriter output, BankImportReport report)
    {
        output.WriteLine($"imported {report.Describe}");

        foreach (var refusal in report.Refusals)
        {
            output.WriteLine($"refused  {refusal}");
        }

        return report.Refusals.Count > 0 ? ExitCodes.Regression : ExitCodes.Pass;
    }

    private static async Task<int> ListAsync(
        CommandLine command,
        IQuestionBank bank,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var query = Query(command);
        var listed = await bank.QuestionsAsync(query, cancellationToken);

        return listed.Match(
            entries =>
            {
                output.WriteLine($"{entries.Count} question(s) — {query.Describe}");

                foreach (var entry in entries)
                {
                    // The state is printed rather than implied: only Accepted questions may enter a test,
                    // and a listing that hid the difference would make a selection look larger than it is.
                    output.WriteLine(
                        $"  {entry.Question.State,-8}  {entry.Group.Key.Value,-16} {entry.Question.Ordinal,4}  "
                        + $"{entry.Question.Question.Id}");
                }

                return ExitCodes.Pass;
            },
            reason => Fail(error, reason));
    }

    private static async Task<int> GroupsAsync(
        IQuestionBank bank, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var listed = await bank.GroupsAsync(cancellationToken);

        return listed.Match(
            groups =>
            {
                output.WriteLine($"{groups.Count} group(s)");

                foreach (var group in groups)
                {
                    output.WriteLine($"  {group.Ordinal,3}  {group.Key.Value,-16}  {group.Title}");
                }

                return ExitCodes.Pass;
            },
            reason => Fail(error, reason));
    }

    private static async Task<int> ReviewAsync(
        CommandLine command,
        IQuestionBank bank,
        TimeProvider clock,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var question = command.Value("question");
        var reviewer = command.Value("reviewer");

        if (question.Length == 0 || reviewer.Length == 0)
        {
            return Fail(error, "--question and --reviewer are required");
        }

        var verdict = command.Value("verdict", "approved");

        if (!Enum.TryParse<ReviewVerdict>(verdict, ignoreCase: true, out var parsed))
        {
            return Fail(error, $"unknown verdict '{verdict}' — try 'approved' or 'rejected'");
        }

        var marked = await bank.ReviewAsync(
            question, reviewer, parsed, command.Value("note"), clock.GetUtcNow(), cancellationToken);

        return marked.Match(
            _ =>
            {
                output.WriteLine($"{reviewer} marked {question} {parsed}");
                return ExitCodes.Pass;
            },
            reason => Fail(error, reason));
    }

    private static async Task<int> StateAsync(
        CommandLine command,
        IQuestionBank bank,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var question = command.Value("question");

        if (question.Length == 0)
        {
            return Fail(error, "--question is required");
        }

        var state = command.Operand(0) == "accept" ? CandidateState.Accepted : CandidateState.Rejected;
        var changed = await bank.SetStateAsync(question, state, cancellationToken);

        return changed.Match(
            value =>
            {
                output.WriteLine($"{value.Question.Id} is now {value.State}"
                    + (value.IsSelectable ? " — it may enter a test" : " — it may not enter a test"));
                return ExitCodes.Pass;
            },
            reason => Fail(error, reason));
    }

    private static async Task<int> MoveAsync(
        CommandLine command,
        IQuestionBank bank,
        TimeProvider clock,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var question = command.Value("question");
        var to = command.Value("to");
        var reason = command.Value("reason");

        if (question.Length == 0 || to.Length == 0)
        {
            return Fail(error, "--question and --to are required");
        }

        if (reason.Length == 0)
        {
            // The history row exists to EXPLAIN a finished report's disagreement with the bank. A move with
            // no reason records that something changed and nothing about why.
            return Fail(error, "--reason is required — the move history is what explains a report's snapshot later");
        }

        var moved = await bank.MoveAsync(question, to, reason, clock.GetUtcNow(), cancellationToken);

        return moved.Match(
            move =>
            {
                output.WriteLine($"moved {question} from {move.From} to {move.To}");
                output.WriteLine("note     tests created before now keep the group they froze — their reports do not move");
                return ExitCodes.Pass;
            },
            failure => Fail(error, failure));
    }

    /// <summary>`bench questions reviewers` — the slots, and which model answers for each.
    /// <para>
    /// Read-only and printed in ordinal order, mirroring <c>groups</c>. The interesting column is the binding:
    /// a slot with none is a slot only a person can complete, so no automatic pass can ever satisfy the strict
    /// promotion rule while it exists.
    /// </para></summary>
    private static async Task<int> ReviewersAsync(
        IQuestionBank bank, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var listed = await bank.ReviewersAsync(cancellationToken);

        return listed.Match(
            reviewers =>
            {
                output.WriteLine($"{reviewers.Count} reviewer slot(s)");

                foreach (var reviewer in reviewers)
                {
                    output.WriteLine($"  {reviewer.Ordinal,3}  {reviewer.Key.Value,-14}  "
                        + $"{(reviewer.IsHuman ? "— a person marks this slot" : reviewer.ModelKey)}");
                }

                return Bound(reviewers, output);
            },
            reason => Fail(error, reason));
    }

    /// <summary>Says what the current bindings mean before anything is run with them, because the one thing an
    /// operator cannot see from a list of three identical model keys is that it is one opinion.</summary>
    private static int Bound(IReadOnlyList<Reviewer> reviewers, TextWriter output)
    {
        var models = reviewers.Where(r => !r.IsHuman).Select(r => r.ModelKey).ToList();

        if (models.Count > 1 && models.Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1)
        {
            output.WriteLine();
            output.WriteLine($"note     {models.Count} slots are bound to '{models[0]}' — {SelfReview.OneOpinion}");
        }

        return ExitCodes.Pass;
    }

    /// <summary>`bench questions bind` — names the model that answers for a reviewer slot.
    /// <para>
    /// A separate verb rather than a flag on the pass: which model reviewed a question has to be readable from
    /// the bank years later, and a binding typed on one command line would leave the marks unattributable.
    /// </para></summary>
    private static async Task<int> BindAsync(
        CommandLine command,
        IQuestionBank bank,
        IModelRegistry registry,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var reviewer = command.Value("reviewer");
        var model = command.Value("model");

        if (reviewer.Length == 0)
        {
            return Fail(error, "--reviewer is required — it names the slot being bound");
        }

        // Refused here rather than at the first launch of a batch: a binding to a registry key that does not
        // exist is a configuration mistake, and finding it forty launches in is finding it too late.
        if (model.Length > 0 && await registry.FindAsync(model, cancellationToken) is Outcome<RegisteredModel>.Fail unknown)
        {
            return Fail(error, unknown.Reason);
        }

        var bound = await bank.BindReviewerAsync(reviewer, model, cancellationToken);

        return bound.Match(
            slot =>
            {
                output.WriteLine(slot.IsHuman
                    ? $"{slot.Key} is now a human slot — no pass will mark it"
                    : $"{slot.Key} is answered by {slot.ModelKey}");
                return ExitCodes.Pass;
            },
            reason => Fail(error, reason));
    }

    /// <summary>`bench questions vet` — every bound reviewer slot marks a group's proposed questions.
    /// <para>
    /// The target is checked out first, exactly as authoring does: a reviewer is asked whether an expectation
    /// resolves at that commit, which is not a question anything can answer without the tree.
    /// </para>
    /// <para>
    /// Nothing here decides a threshold. The marks go in, and <see cref="Promotion"/>'s strict rule — every
    /// configured reviewer approved — is what moves a question's state.
    /// </para></summary>
    private static async Task<int> VetAsync(
        CommandLine command,
        IQuestionBank bank,
        ICliAgentRuntime agents,
        IModelRegistry registry,
        ISecretSource secrets,
        ICheckoutProvider checkouts,
        WorkspaceTrust trust,
        TimeProvider clock,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var inputs = VettingInputs.Read(command);

        if (inputs is Outcome<VettingInputs>.Fail bad)
        {
            return Fail(error, bad.Reason);
        }

        var settings = ((Outcome<VettingInputs>.Ok)inputs).Value;
        var found = await GroupAsync(bank, settings.Group, cancellationToken);

        if (found is not Outcome<QuestionGroup>.Ok(var group))
        {
            error.WriteLine($"bench: {found.Reason()}");
            return ExitCodes.Environment;
        }

        var slots = await SlotsAsync(bank, registry, secrets, cancellationToken);

        if (slots is not Outcome<IReadOnlyList<ReviewerSlot>>.Ok(var resolved))
        {
            error.WriteLine($"bench: {slots.Reason()}");
            return ExitCodes.Environment;
        }

        var tree = await checkouts.EnsureAsync(MeasurementTarget.At(settings.Target, settings.Commit), cancellationToken);

        if (tree is not Outcome<string>.Ok(var worktree))
        {
            error.WriteLine($"bench: the target could not be checked out — {tree.Reason()}");
            return ExitCodes.Environment;
        }

        output.WriteLine($"group    {group.Key} — {group.Title}");
        output.WriteLine($"slots    {string.Join(", ", resolved.Select(s => $"{s.Reviewer.Key}={s.Model.Config.ModelId}"))}");
        output.WriteLine($"tree     {worktree}");
        output.WriteLine($"trust    {Trusted(trust, worktree, command)}");

        var report = await VettingPass.RunAsync(
            agents,
            bank,
            settings.PromptRoot,
            new VettingRequest(
                group, settings.Target, settings.Commit, settings.Wall, worktree, settings.AllowSelfReview, settings.Limit),
            resolved,
            clock.GetUtcNow(),
            cancellationToken);

        return Report(report, group, output);
    }

    private static int Report(VettingReport report, QuestionGroup group, TextWriter output)
    {
        output.WriteLine($"vetted   {report.Describe}");

        if (report.PromptHash.Length > 0)
        {
            output.WriteLine($"prompt   {report.PromptHash[..12]}  (prompts/review/{group.Key})");
        }

        if (report.Cost.Length > 0)
        {
            // Never silent. The escape hatch is the operator's to take, and what it costs travels with the run
            // that took it rather than living in a chat log.
            output.WriteLine($"cost     {report.Cost}");
        }

        foreach (var question in report.Questions)
        {
            output.WriteLine($"  {question.QuestionId}  {question.Decision.Kind} — {question.Decision.Reason}");

            // The mechanical defects first: they are why nothing was launched for this question, and they are the
            // one thing here an operator can fix in a minute.
            foreach (var line in question.Defects.Select(d => $"defect  {d}")
                .Concat(question.Marks)
                .Concat(question.Skipped.Select(s => $"skipped {s}")))
            {
                output.WriteLine($"    {line}");
            }
        }

        foreach (var refusal in report.Refusals)
        {
            output.WriteLine($"  refuse {refusal}");
        }

        return report.Questions.Count > 0 ? ExitCodes.Pass : ExitCodes.NoReport;
    }

    /// <summary>Every slot with a model bound, resolved on this machine. A slot with none is skipped rather
    /// than refused — a human reviewer is a legitimate row, it simply cannot be driven.</summary>
    private static async Task<Outcome<IReadOnlyList<ReviewerSlot>>> SlotsAsync(
        IQuestionBank bank, IModelRegistry registry, ISecretSource secrets, CancellationToken cancellationToken)
    {
        var listed = await bank.ReviewersAsync(cancellationToken);

        if (listed is not Outcome<IReadOnlyList<Reviewer>>.Ok(var reviewers))
        {
            return Outcome<IReadOnlyList<ReviewerSlot>>.Failure(listed.Reason());
        }

        var slots = new List<ReviewerSlot>();

        foreach (var reviewer in reviewers.Where(r => !r.IsHuman))
        {
            var resolved = await ResolveAuthorAsync(registry, secrets, reviewer.ModelKey, cancellationToken);

            if (resolved is Outcome<ResolvedAuthor>.Fail bad)
            {
                return Outcome<IReadOnlyList<ReviewerSlot>>.Failure($"reviewer '{reviewer.Key}': {bad.Reason}");
            }

            var value = ((Outcome<ResolvedAuthor>.Ok)resolved).Value;
            slots.Add(new ReviewerSlot(reviewer, value.Model, value.Executable));
        }

        return Outcome<IReadOnlyList<ReviewerSlot>>.Success(slots);
    }

    private sealed record VettingInputs(
        string Group,
        RepoUrl Target,
        CommitSha Commit,
        int Limit,
        TimeSpan Wall,
        bool AllowSelfReview,
        string PromptRoot)
    {
        /// <summary>Five minutes. A reviewer reads one question and checks its anchors — a smaller job than
        /// authoring five, and a ceiling that keeps a batch of thirty from becoming an afternoon.</summary>
        private const int DefaultWallSeconds = 300;

        public static Outcome<VettingInputs> Read(CommandLine command)
        {
            var group = command.Value("group");
            var limit = command.Int("limit", 10);
            var wall = command.Int("wall-seconds", DefaultWallSeconds);

            if (group.Length == 0)
            {
                return Outcome<VettingInputs>.Failure("--group is required — vetting walks one group's proposed questions");
            }

            if (limit < 1 || wall < 1)
            {
                return Outcome<VettingInputs>.Failure("--limit and --wall-seconds must both be positive");
            }

            return RepoUrl.Parse(command.Value("repo")).Match(
                repo => CommitSha.Parse(command.Value("commit")).Match(
                    commit => Outcome<VettingInputs>.Success(new VettingInputs(
                        group,
                        repo,
                        commit,
                        limit,
                        TimeSpan.FromSeconds(wall),
                        command.Has("allow-self-review"),
                        command.Value("prompts", "prompts"))),
                    Outcome<VettingInputs>.Failure),
                Outcome<VettingInputs>.Failure);
        }
    }

    /// <summary>Pre-trusts the checked-out tree in the agent CLI's own config, and says what happened.
    /// <para>
    /// Measured 2026-08-18: two of four authoring groups produced nothing and burned their whole 900-second wall
    /// each, because the CLI would not act in an untrusted workspace and then waited for a dialog no headless run
    /// can answer. Half an hour, and the wall was the only thing that ended it.
    /// </para>
    /// <para>
    /// It never fails the verb. A tree that could not be pre-trusted still gets measured — it just risks the
    /// gate, and the printed line is what tells the operator that is what happened. <c>--no-trust</c> skips it
    /// for anyone who would rather answer the dialog themselves.
    /// </para></summary>
    private static string Trusted(WorkspaceTrust trust, string worktree, CommandLine command) =>
        command.Has("no-trust")
            ? "skipped (--no-trust) — an untrusted workspace makes the agent wait for a dialog nothing can answer"
            : trust.Ensure(worktree).Match(result => result.Describe, reason => $"could not be set — {reason}");

    /// <summary>The selection vocabulary, shared by <c>list</c> and by <c>bench run --bank-group</c>: a
    /// group and an ordinal range, which is how the operator describes this material out loud.</summary>
    public static BankQuery Query(CommandLine command) => new(
        command.Value("group"),
        command.Int("from", 0),
        command.Int("to", 0),
        command.Has("accepted"));

    private static int Fail(TextWriter error, string reason)
    {
        error.WriteLine($"bench: {reason}");
        return ExitCodes.Configuration;
    }

    private static string Reason<T>(this Outcome<T> outcome) => outcome.Match(_ => string.Empty, reason => reason);
}
