using Bench.Application;
using Bench.Application.Bank;
using Bench.Domain;
using Bench.Domain.Authoring;
using Bench.Domain.Bank;
using Bench.Domain.Models;
using Bench.Domain.Runs;
using Bench.Domain.Suites;
using Bench.Domain.Targets;
using Bench.Infrastructure.Git;

namespace Bench.Cli;

/// <summary>`bench solve` — ONE implement-given-diagnosis leg, ATTENDED
/// (todo/PLAN_investigate_vs_implement.md §3.4, step 8).
/// <para>
/// This is the shape the isolation gate allows and nothing more: one task, one subject, invoked by an
/// operator who is watching, never queued, never drained, and NOTHING persisted — building a tree that
/// contains a model-authored diff is arbitrary code execution by design, and the queue stays closed by
/// `PLAN_code_lane.md` §4.2 until its three isolation assertions pass. What this buys today is the
/// calibration work that section names: the prompt, the extraction and the signals exercised on real
/// tasks while the sandbox is still open work.
/// </para>
/// <para>
/// The subject is handed the statement and the REFERENCE DIAGNOSIS — the causal anchors, and the
/// mechanism where one has been authored — and asked for a unified diff. Scored by
/// <see cref="SolutionSignals"/>: applies · builds · touches a causal file · hidden tests green.
/// </para></summary>
public static class SolveCommand
{
    public static async Task<int> RunAsync(
        CommandLine command,
        IQuestionBank bank,
        ICheckoutProvider checkouts,
        IModelRuntime runtime,
        TextWriter output,
        TextWriter error,
        CancellationToken stopping)
    {
        var questionId = command.Value("question");

        if (questionId.Length == 0)
        {
            return Fail(error, "--question names the code task to solve — a bank question id", ExitCodes.Configuration);
        }

        var endpoint = Endpoint(command);

        if (endpoint is Outcome<ModelEndpoint>.Fail badModel)
        {
            return Fail(error, badModel.Reason, ExitCodes.Configuration);
        }

        var build = GateCommand.Parse(command.Value("gate-build"));
        var test = GateCommand.Parse(command.Value("gate-test"));

        if (build is Outcome<GateCommand>.Fail badBuild)
        {
            return Fail(error, $"--gate-build: {badBuild.Reason} — an implement leg without gates measures nothing", ExitCodes.Configuration);
        }

        if (test is Outcome<GateCommand>.Fail badTest)
        {
            return Fail(error, $"--gate-test: {badTest.Reason}", ExitCodes.Configuration);
        }

        return await SolveAsync(
            command, bank, checkouts, runtime, questionId,
            ((Outcome<ModelEndpoint>.Ok)endpoint).Value,
            ((Outcome<GateCommand>.Ok)build).Value,
            ((Outcome<GateCommand>.Ok)test).Value,
            output, error, stopping);
    }

    private static async Task<int> SolveAsync(
        CommandLine command,
        IQuestionBank bank,
        ICheckoutProvider checkouts,
        IModelRuntime runtime,
        string questionId,
        ModelEndpoint endpoint,
        GateCommand build,
        GateCommand test,
        TextWriter output,
        TextWriter error,
        CancellationToken stopping)
    {
        var entries = await bank.QuestionsAsync(new BankQuery(), stopping);

        if (entries is not Outcome<IReadOnlyList<BankEntry>>.Ok(var all))
        {
            return Fail(error, entries.Match(_ => string.Empty, reason => reason), ExitCodes.Environment);
        }

        var entry = all.FirstOrDefault(e => string.Equals(e.Question.Question.Id, questionId, StringComparison.Ordinal));

        if (entry is null)
        {
            return Fail(error, $"the bank holds no question '{questionId}'", ExitCodes.Configuration);
        }

        if (entry.Question.Kind != TaskKind.Fix)
        {
            return Fail(error, $"'{questionId}' is a {entry.Question.Kind} question — only a fix task can be solved", ExitCodes.Configuration);
        }

        var payload = CodeTaskCodec.Read(entry.Question.CodeTaskJson);

        if (payload is not Outcome<CodeTask>.Ok(var task))
        {
            return Fail(error, payload.Match(_ => string.Empty, reason => reason), ExitCodes.Configuration);
        }

        var reference = FixDiff.Parse(task.ReferenceDiff);

        if (reference is not Outcome<FixDiff>.Ok(var referenceDiff))
        {
            return Fail(error, reference.Match(_ => string.Empty, reason => reason), ExitCodes.Environment);
        }

        var tree = await checkouts.EnsureAsync(
            MeasurementTarget.At(entry.Question.TargetRepo, task.BaseCommit), stopping);

        if (tree is Outcome<string>.Fail unavailable)
        {
            return Fail(error, unavailable.Reason, ExitCodes.Environment);
        }

        output.WriteLine($"task     {questionId} — base {task.BaseCommit.Value[..12]}, fix {task.FixCommit.Value[..12]}");
        output.WriteLine($"subject  {endpoint.Model.Id} @ {endpoint.BaseUrl}");
        output.WriteLine("mode     ATTENDED — one task, one subject, nothing queued, nothing persisted;");
        output.WriteLine("         the queue stays closed by PLAN_code_lane §4.2 until its isolation assertions pass");

        return await AskAndScoreAsync(
            command, runtime, entry, task, referenceDiff, ((Outcome<string>.Ok)tree).Value,
            endpoint, build, test, output, error, stopping);
    }

    private static async Task<int> AskAndScoreAsync(
        CommandLine command,
        IModelRuntime runtime,
        BankEntry entry,
        CodeTask task,
        FixDiff referenceDiff,
        string tree,
        ModelEndpoint endpoint,
        GateCommand build,
        GateCommand test,
        TextWriter output,
        TextWriter error,
        CancellationToken stopping)
    {
        var wall = Budget.Of(BudgetKind.Wall, BudgetScope.Question, command.Int("wall-seconds", 600));
        var asked = await runtime.AskAsync(
            new ModelRequest(
                endpoint,
                Sampling.Deterministic(command.Int("seed", 7)),
                string.Empty,
                Prompt(entry.Question.Question, task),
                [wall.ConfirmedBy("solve")]),
            stopping);

        if (asked is Outcome<ModelAnswer>.Fail unanswered)
        {
            return Fail(error, unanswered.Reason, ExitCodes.Environment);
        }

        var answer = ((Outcome<ModelAnswer>.Ok)asked).Value;

        if (!answer.Text.WasCaptured)
        {
            return Fail(error, $"the subject returned no text: {answer.Text.Reason}", ExitCodes.NoReport);
        }

        var diff = DiffFence.Extract(answer.Text.Value);

        if (diff is Outcome<string>.Fail nodiff)
        {
            // A verdict about the SOLUTION, not the instrument — printed as the leg's result.
            output.WriteLine($"diff     none — {nodiff.Reason}");
            return ExitCodes.Regression;
        }

        var report = await SolutionSignals.RunAsync(
            tree, task.BaseCommit, task.FixCommit,
            ((Outcome<string>.Ok)diff).Value,
            [.. entry.Question.Question.RetrievalExpectations.Select(e => e.Anchor.FilePath).Distinct(StringComparer.OrdinalIgnoreCase)],
            referenceDiff.TestFiles,
            build, test,
            TimeSpan.FromSeconds(command.Int("gate-timeout-seconds", 900)),
            stopping);

        return report.Match(
            verdict => Print(output, verdict),
            reason => Fail(error, reason, ExitCodes.Environment));
    }

    private static int Print(TextWriter output, SolutionReport report)
    {
        output.WriteLine($"signals  {report.Describe}");

        foreach (var metric in report.Metrics())
        {
            output.WriteLine($"         {(metric.Value == "true" ? "·" : "✗")} {metric.Name} = {metric.Value}  ({metric.Reason})");
        }

        return report.Passed ? ExitCodes.Pass : ExitCodes.Regression;
    }

    /// <summary>The implement-given-diagnosis prompt (§3.4): the statement, the reference diagnosis —
    /// anchors, and the mechanism where one was authored — and NEVER the reference diff or a hidden
    /// test. What is held fixed is the investigation; the measurement is the hands alone.</summary>
    private static string Prompt(Question question, CodeTask task)
    {
        var anchors = string.Join(
            "\n",
            question.RetrievalExpectations.Select(e =>
                $"- {e.Anchor.FilePath}" + (e.Anchor.Lines.IsWhole ? string.Empty : $" (lines {e.Anchor.Lines.Canonical})")));

        var mechanism = task.Mechanism.Length > 0
            ? task.Mechanism
            : "(no mechanism has been authored for this task — the anchors above are the diagnosis)";

        return $"""
            {question.Prompt}

            THE DIAGNOSIS (reference — the defect is caused at these places):
            {anchors}

            Mechanism: {mechanism}

            Fix the defect at commit {task.BaseCommit.Value}. You cannot read the repository; work from
            the diagnosis above. Answer with exactly ONE fenced block containing a unified diff that
            `git apply` accepts against that commit, and nothing else:

            ```diff
            diff --git a/path b/path
            ...
            ```
            """;
    }

    private static Outcome<ModelEndpoint> Endpoint(CommandLine command) =>
        ModelRef.Parse(command.Value("model"), ModelHosting.Local).Match(
            model => ModelEndpoint.Parse(model, command.Value("model-url")),
            Outcome<ModelEndpoint>.Failure);

    private static int Fail(TextWriter error, string reason, int code)
    {
        error.WriteLine($"bench: {reason}");
        return code;
    }
}
