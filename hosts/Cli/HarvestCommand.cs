using Bench.Application;
using Bench.Domain;
using Bench.Domain.Authoring;
using Bench.Infrastructure.Git;
using Bench.Domain.Targets;

namespace Bench.Cli;

/// <summary>`bench questions harvest --repo <url> --commit <fix sha>` — the cheaper door for code tasks
/// (todo/PLAN_investigate_vs_implement.md §3.5), in its report-only first form: the fix is read out of
/// a pinned checkout and the DERIVED half of a candidate is printed — base commit, seed date, causal
/// anchors, hidden-test candidates. Nothing lands in the bank and no gate runs yet, and the printout
/// says so on its last line: a verb that looked like it had banked a task would be worse than no verb.
/// <para>
/// The commit is asked for in full, not as a shorthand: the record pins forty characters, and the
/// checkout provider verifies them against the mirror before anything is read.
/// </para></summary>
public static class HarvestCommand
{
    /// <summary>Per git read. Generous next to what rev-parse/show/diff cost, and a tenth of the
    /// checkout timeout — by the time these run, the mirror already exists.</summary>
    private static readonly TimeSpan GitTimeout = TimeSpan.FromMinutes(2);

    public static async Task<int> RunAsync(
        CommandLine command,
        ICheckoutProvider checkouts,
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

        return parsed is Outcome<FixDiff>.Fail unreadable
            ? Fail(
                error,
                $"the fix's own diff could not be read — a reader gap, not the invocation's: {unreadable.Reason}",
                ExitCodes.Environment)
            : Print(output, fix, ((Outcome<FixDiff>.Ok)parsed).Value);
    }

    private static int Print(TextWriter output, HarvestedFix fix, FixDiff diff)
    {
        var causal = diff.CausalAnchors(fix.Base);

        output.WriteLine($"fix      {fix.Fix.Value[..12]} — {fix.Subject}");
        output.WriteLine($"base     {fix.Base.Value[..12]} — the tree the solver will investigate");
        output.WriteLine($"seed     {fix.AuthoredOn:yyyy-MM-dd} — the fix's author date, derived and never typed");

        if (causal.Count == 0)
        {
            output.WriteLine(
                "causal   none — every change the fix made is in test files, so an investigate arm would have nothing to score");
        }
        else
        {
            output.WriteLine($"causal   {causal.Count} anchor(s) in the base tree:");

            foreach (var anchor in causal)
            {
                output.WriteLine(
                    $"         {anchor.FilePath}" + (anchor.Lines.IsWhole ? " (whole file)" : $" #{anchor.Lines.Canonical}"));
            }
        }

        if (diff.TestFiles.Count == 0)
        {
            output.WriteLine("tests    none — the fix changed no test file; hidden tests will need authoring");
        }
        else
        {
            output.WriteLine($"tests    {diff.TestFiles.Count} file(s) the fix touched — the hidden-test candidates:");

            foreach (var file in diff.TestFiles)
            {
                output.WriteLine($"         {file}");
            }
        }

        output.WriteLine(
            "note     printed only: no gate has run (red at base / green with fix), and nothing landed in the bank");

        // No causal anchor means no candidate: the verb completed and produced nothing measurable,
        // which is exactly the state the NoReport code exists to name.
        return causal.Count == 0 ? ExitCodes.NoReport : ExitCodes.Pass;
    }

    private static int Fail(TextWriter error, string reason, int code)
    {
        error.WriteLine($"bench: {reason}");
        return code;
    }
}
