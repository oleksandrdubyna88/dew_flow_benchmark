using Bench.Application.Bank;
using Bench.Domain;
using Bench.Domain.Authoring;
using Bench.Domain.Bank;

namespace Bench.Cli;

/// <summary>The question bank, from the command line.
/// <para>
/// Phase 1 of the bank: questions arrive by IMPORT — authored elsewhere, reviewed elsewhere — and this
/// verb is what puts them somewhere a test can select from. The authoring pipeline that drives three CLI
/// agents to write and review candidates is a later plan; the schema underneath is already the shape it
/// needs, so that plan adds verbs rather than tables.
/// </para></summary>
public static class QuestionsCommand
{
    public static async Task<int> RunAsync(
        CommandLine command,
        IQuestionBank bank,
        TimeProvider clock,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken) =>
        command.Operand(0) switch
        {
            "import" => await ImportAsync(command, bank, clock, output, error, cancellationToken),
            "list" => await ListAsync(command, bank, output, error, cancellationToken),
            "groups" => await GroupsAsync(bank, output, error, cancellationToken),
            "review" => await ReviewAsync(command, bank, clock, output, error, cancellationToken),
            "accept" or "reject" => await StateAsync(command, bank, output, error, cancellationToken),
            "move" => await MoveAsync(command, bank, clock, output, error, cancellationToken),
            var other => Fail(
                error,
                other.Length == 0
                    ? "bench questions needs an action — 'import', 'list', 'groups', 'review', 'accept', 'reject' or 'move'"
                    : $"unknown questions action '{other}' — try 'import', 'list', 'groups', 'review', 'accept', 'reject' or 'move'"),
        };

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
