using Bench.Application;
using Bench.Application.Bank;
using Bench.Domain;
using Bench.Domain.Authoring;
using Bench.Domain.Bank;
using Bench.Domain.Runs;
using Bench.Domain.Suites;
using Bench.Domain.Targets;
using Bench.Infrastructure.Git;

namespace Bench.Cli;

/// <summary>`bench questions harvest --repo <url> --commit <fix sha>` — the cheaper door for code tasks
/// (todo/PLAN_investigate_vs_implement.md §3.5). Two modes, chosen by <c>--statement-file</c>:
/// <para>
/// WITHOUT it, the verb is report-only: the derived half of a candidate is printed — base commit, seed
/// date, causal anchors, hidden-test candidates — and the last line says nothing landed.
/// </para>
/// <para>
/// WITH it, the candidate LANDS in the bank as <c>Proposed</c>: the statement is the operator's (the
/// author names the symptom; the repository derives everything else), the two gates run first — red at
/// base with the fix's own tests, green at the fix — unless <c>--no-gates</c> skips them out loud, and
/// a failed gate refuses the landing with the gate's own verdict. The panel then vets the candidate
/// exactly as it vets everything else.
/// </para></summary>
public static class HarvestCommand
{
    private const string DefaultGroup = "code-writing";

    /// <summary>Per git read. Generous next to what rev-parse/show/diff cost — by the time these run,
    /// the mirror already exists.</summary>
    private static readonly TimeSpan GitTimeout = TimeSpan.FromMinutes(2);

    public static async Task<int> RunAsync(
        CommandLine command,
        ICheckoutProvider checkouts,
        IQuestionBank bank,
        TimeProvider clock,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var repo = RepoUrl.Parse(command.Value("repo"));

        if (repo is Outcome<RepoUrl>.Fail badRepo)
        {
            return Fail(error, $"--repo: {badRepo.Reason}", ExitCodes.Configuration);
        }

        var commit = CommitSha.Parse(command.Value("commit"));

        if (commit is Outcome<CommitSha>.Fail badSha)
        {
            return Fail(
                error,
                $"--commit must name the FIX commit, all forty characters — {badSha.Reason}",
                ExitCodes.Configuration);
        }

        // Landing flags are validated BEFORE the checkout: a refused invocation must not cost a clone.
        var landing = command.Has("statement-file");

        if (landing && command.Value("statement-author").Trim().Length == 0)
        {
            return Fail(
                error,
                "landing needs --statement-author — a set's ceiling becomes its author's ceiling, and the statement's author must be readable later",
                ExitCodes.Configuration);
        }

        if (landing && !command.Has("no-gates") && (!command.Has("gate-build") || !command.Has("gate-test")))
        {
            return Fail(
                error,
                "landing runs the gates — pass --gate-build and --gate-test (exe;arg;arg), or skip them out loud with --no-gates",
                ExitCodes.Configuration);
        }

        var target = MeasurementTarget.At(
            ((Outcome<RepoUrl>.Ok)repo).Value, ((Outcome<CommitSha>.Ok)commit).Value);
        var tree = await checkouts.EnsureAsync(target, cancellationToken);

        if (tree is Outcome<string>.Fail unavailable)
        {
            return Fail(error, $"the target could not be checked out — {unavailable.Reason}", ExitCodes.Environment);
        }

        var harvested = await FixHarvest.ReadAsync(
            ((Outcome<string>.Ok)tree).Value, target.Commit.Value, GitTimeout, cancellationToken);

        if (harvested is Outcome<HarvestedFix>.Fail refused)
        {
            // A root commit, a merge, a sha the mirror lacks: facts about what the invocation NAMED.
            return Fail(error, refused.Reason, ExitCodes.Configuration);
        }

        var fix = ((Outcome<HarvestedFix>.Ok)harvested).Value;
        var parsed = FixDiff.Parse(fix.DiffText);

        if (parsed is Outcome<FixDiff>.Fail unreadable)
        {
            return Fail(
                error,
                $"the fix's own diff could not be read — a reader gap, not the invocation's: {unreadable.Reason}",
                ExitCodes.Environment);
        }

        var diff = ((Outcome<FixDiff>.Ok)parsed).Value;
        var causal = diff.CausalAnchors(fix.Base);

        PrintMaterial(output, fix, diff, causal);

        if (causal.Count == 0)
        {
            output.WriteLine(
                "note     nothing landed: a fix with no causal anchor gives an investigate arm nothing to score");
            return ExitCodes.NoReport;
        }

        return landing
            ? await LandAsync(
                command, bank, clock, target, ((Outcome<string>.Ok)tree).Value, fix, diff, causal,
                output, error, cancellationToken)
            : PrintedOnly(output);
    }

    private static void PrintMaterial(
        TextWriter output, HarvestedFix fix, FixDiff diff, IReadOnlyList<SourceAnchor> causal)
    {
        output.WriteLine($"fix      {fix.Fix.Value[..12]} — {fix.Subject}");
        output.WriteLine($"base     {fix.Base.Value[..12]} — the tree the solver will investigate");
        output.WriteLine($"seed     {fix.AuthoredOn:yyyy-MM-dd} — the fix's author date, derived and never typed");

        output.WriteLine(causal.Count == 0
            ? "causal   none — every change the fix made is in test files"
            : $"causal   {causal.Count} anchor(s) in the base tree:");

        foreach (var anchor in causal)
        {
            output.WriteLine(
                $"         {anchor.FilePath}" + (anchor.Lines.IsWhole ? " (whole file)" : $" #{anchor.Lines.Canonical}"));
        }

        output.WriteLine(diff.TestFiles.Count == 0
            ? "tests    none — the fix changed no test file; hidden tests will need authoring"
            : $"tests    {diff.TestFiles.Count} file(s) the fix touched — the hidden-test candidates:");

        foreach (var file in diff.TestFiles)
        {
            output.WriteLine($"         {file}");
        }
    }

    private static int PrintedOnly(TextWriter output)
    {
        output.WriteLine(
            "note     printed only: no gate has run, and nothing landed in the bank — pass --statement-file to land a candidate");
        return ExitCodes.Pass;
    }

    private static async Task<int> LandAsync(
        CommandLine command,
        IQuestionBank bank,
        TimeProvider clock,
        MeasurementTarget target,
        string tree,
        HarvestedFix fix,
        FixDiff diff,
        IReadOnlyList<SourceAnchor> causal,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var gates = await GatesAsync(command, tree, fix, diff, output, error, cancellationToken);

        if (gates is Outcome<(bool Ran, string Detail)>.Fail gateFailed)
        {
            return Fail(error, gateFailed.Reason, ExitCodes.Regression);
        }

        var statementPath = command.Value("statement-file");

        if (!File.Exists(statementPath))
        {
            return Fail(error, $"statement file not found: '{statementPath}'", ExitCodes.Environment);
        }

        var statement = (await File.ReadAllTextAsync(statementPath, cancellationToken)).Trim();

        if (statement.Length == 0)
        {
            return Fail(error, "the statement file is empty — a task nobody can read is a task nobody can solve", ExitCodes.Configuration);
        }

        var (ran, detail) = ((Outcome<(bool Ran, string Detail)>.Ok)gates).Value;

        return await StoreAsync(
            command, bank, clock, target, fix, statement, causal, ran, detail, output, error, cancellationToken);
    }

    /// <summary>The two gates, or the explicit refusal to run them. A FAILED gate comes back as this
    /// method's failure carrying the gate's own verdict — the candidate never lands, per the code
    /// lane's rule that a malformed task is refused with the reason rather than banked.</summary>
    private static async Task<Outcome<(bool Ran, string Detail)>> GatesAsync(
        CommandLine command,
        string tree,
        HarvestedFix fix,
        FixDiff diff,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (command.Has("no-gates"))
        {
            output.WriteLine("gates    SKIPPED (--no-gates) — the candidate lands ungated, and this line is its record");
            return Outcome<(bool, string)>.Success((false, "skipped by --no-gates at harvest"));
        }

        var build = GateCommand.Parse(command.Value("gate-build"));
        var test = GateCommand.Parse(command.Value("gate-test"));

        if (build is Outcome<GateCommand>.Fail badBuild)
        {
            return Outcome<(bool, string)>.Failure($"--gate-build: {badBuild.Reason}");
        }

        if (test is Outcome<GateCommand>.Fail badTest)
        {
            return Outcome<(bool, string)>.Failure($"--gate-test: {badTest.Reason}");
        }

        var timeout = TimeSpan.FromSeconds(command.Int("gate-timeout-seconds", 900));
        var report = await HarvestGates.RunAsync(
            tree, fix.Base, fix.Fix, diff.TestFiles,
            ((Outcome<GateCommand>.Ok)build).Value, ((Outcome<GateCommand>.Ok)test).Value,
            timeout, cancellationToken);

        if (report is Outcome<GateReport>.Fail broken)
        {
            return Outcome<(bool, string)>.Failure(broken.Reason);
        }

        var verdict = ((Outcome<GateReport>.Ok)report).Value;
        output.WriteLine($"gates    {(verdict.Passed ? "passed" : "FAILED")} — {verdict.Describe}");

        return verdict.Passed
            ? Outcome<(bool, string)>.Success((true, verdict.Describe))
            : Outcome<(bool, string)>.Failure(
                $"the gates refused the task: {verdict.Describe}. Nothing landed — a bank row carrying a failed gate would read as a task somebody vouched for");
    }

    private static async Task<int> StoreAsync(
        CommandLine command,
        IQuestionBank bank,
        TimeProvider clock,
        MeasurementTarget target,
        HarvestedFix fix,
        string statement,
        IReadOnlyList<SourceAnchor> causal,
        bool gatesRan,
        string gateDetail,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var groupKey = command.Value("group", DefaultGroup);
        var groups = await bank.GroupsAsync(cancellationToken);

        if (groups is Outcome<IReadOnlyList<QuestionGroup>>.Fail noBank)
        {
            return Fail(error, noBank.Reason, ExitCodes.Environment);
        }

        var group = ((Outcome<IReadOnlyList<QuestionGroup>>.Ok)groups).Value
            .FirstOrDefault(g => string.Equals(g.Key.Value, groupKey, StringComparison.Ordinal));

        if (group is null)
        {
            return Fail(error, $"the bank has no group '{groupKey}'", ExitCodes.Configuration);
        }

        var siblings = await bank.QuestionsAsync(new BankQuery(groupKey), cancellationToken);
        var ordinal = siblings is Outcome<IReadOnlyList<BankEntry>>.Ok(var entries) ? entries.Count + 1 : 1;

        var landed = CodeTask.Harvested(fix.Base, fix.Fix, fix.DiffText, gatesRan, gateDetail).Match(
            task => BankQuestion.Create(
                group.Id,
                ordinal,
                TaskKind.Fix,
                new Question(
                    command.Value("id", $"fix-{fix.Fix.Value[..12]}"),
                    statement,
                    [.. causal.Select(Expected)],
                    ReferenceAnswer: string.Empty),
                CodeTaskCodec.Write(task),
                AuthoringSource.BugsAndTests,
                command.Value("statement-author"),
                QuestionSeed.Written("commit", fix.Fix.Value, new DateTimeOffset(fix.AuthoredOn.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)),
                target.Repo,
                fix.Base,
                clock.GetUtcNow()),
            Outcome<BankQuestion>.Failure);

        if (landed is Outcome<BankQuestion>.Fail unbuildable)
        {
            return Fail(error, unbuildable.Reason, ExitCodes.Configuration);
        }

        var stored = await bank.AddAsync(((Outcome<BankQuestion>.Ok)landed).Value, cancellationToken);

        return stored.Match(
            question =>
            {
                output.WriteLine(
                    $"landed   {question.Question.Id} → group '{groupKey}' as Proposed (ordinal {ordinal}) — the panel vets it like everything else");
                return ExitCodes.Pass;
            },
            reason => Fail(error, reason, ExitCodes.Regression));
    }

    /// <summary>A causal anchor as the landed question's retrieval expectation: the anchors double as
    /// the rag lane's ground truth, and there is exactly one definition of "found" either way.</summary>
    private static Expectation Expected(SourceAnchor anchor) =>
        anchor.IsWholeFile && anchor.Lines.IsWhole
            ? Expectation.File(anchor)
            : new Expectation(ExpectationKind.Member, anchor, string.Empty, Required: true);

    private static int Fail(TextWriter error, string reason, int code)
    {
        error.WriteLine($"bench: {reason}");
        return code;
    }
}
