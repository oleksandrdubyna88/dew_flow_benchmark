using System.Globalization;
using Bench.Domain;
using Bench.Domain.Authoring;
using Bench.Domain.Targets;

namespace Bench.Infrastructure.Git;

/// <summary>Reading one fix commit out of a repository (todo/PLAN_investigate_vs_implement.md §3.5,
/// step 3's git half) — over <see cref="GitCommand"/>, the family's one git launcher.
/// <para>
/// Works against a bare mirror or a worktree alike: everything here is plumbing over commits, no
/// checkout needed. Two shapes are refused by name before anything is read: a ROOT commit (there is no
/// buggy tree before it, so there is nothing to investigate) and a MERGE commit (its first-parent diff
/// lands every change of the branch at once, and a task derived from it would ask a solver to
/// rediscover a whole feature rather than one defect — harvest the fix commit itself).
/// </para></summary>
public static class FixHarvest
{
    public static async Task<Outcome<HarvestedFix>> ReadAsync(
        string repositoryDir, string fixRef, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var reference = (fixRef ?? string.Empty).Trim();

        if (reference.Length == 0)
        {
            return Outcome<HarvestedFix>.Failure("a fix reference is required — an empty ref resolves to nothing");
        }

        var resolved = await Git(repositoryDir, timeout, cancellationToken,
            "rev-parse", "--verify", $"{reference}^{{commit}}");

        if (resolved is Outcome<string>.Fail unresolved)
        {
            return Outcome<HarvestedFix>.Failure(
                $"could not resolve '{reference}' to a commit in {repositoryDir}: {unresolved.Reason}");
        }

        var fixSha = ((Outcome<string>.Ok)resolved).Value.Trim();

        if (await Git(repositoryDir, timeout, cancellationToken,
                "rev-parse", "--verify", "--quiet", $"{fixSha}^2") is Outcome<string>.Ok)
        {
            return Outcome<HarvestedFix>.Failure(
                $"{fixSha[..10]} is a merge commit — its diff lands a whole branch at once, and a task derived "
                + "from it would ask for a feature's rediscovery rather than one defect's. Harvest the fix commit itself");
        }

        var parent = await Git(repositoryDir, timeout, cancellationToken,
            "rev-parse", "--verify", "--quiet", $"{fixSha}^");

        if (parent is Outcome<string>.Fail)
        {
            return Outcome<HarvestedFix>.Failure(
                $"{fixSha[..10]} has no parent — a first commit cannot be harvested: there is no buggy tree before it");
        }

        return await AssembleAsync(
            repositoryDir, fixSha, ((Outcome<string>.Ok)parent).Value.Trim(), timeout, cancellationToken);
    }

    private static async Task<Outcome<HarvestedFix>> AssembleAsync(
        string repositoryDir, string fixSha, string baseSha, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var meta = await Git(repositoryDir, timeout, cancellationToken,
            "show", "-s", "--format=%as%n%s%n%b", fixSha);

        if (meta is Outcome<string>.Fail unread)
        {
            return Outcome<HarvestedFix>.Failure($"could not read {fixSha[..10]}: {unread.Reason}");
        }

        var diff = await Git(repositoryDir, timeout, cancellationToken,
            "diff", "--no-color", baseSha, fixSha);

        if (diff is Outcome<string>.Fail undiffed)
        {
            return Outcome<HarvestedFix>.Failure(
                $"could not diff {baseSha[..10]}..{fixSha[..10]}: {undiffed.Reason}");
        }

        return Compose(
            fixSha, baseSha, ((Outcome<string>.Ok)meta).Value, ((Outcome<string>.Ok)diff).Value);
    }

    /// <summary>Pure assembly over what git said, so the parsing rules are testable without a repository.</summary>
    internal static Outcome<HarvestedFix> Compose(string fixSha, string baseSha, string meta, string diffText)
    {
        var lines = meta.Replace("\r\n", "\n").Split('\n');

        if (lines.Length < 2)
        {
            return Outcome<HarvestedFix>.Failure($"git described {fixSha[..10]} in a shape this reader does not know: '{meta.Trim()}'");
        }

        // The AUTHOR date, as a calendar day and never an instant — the QuestionSeed.Written lesson:
        // normalising a day through a timezone moved it across midnight, and the rejections that
        // followed were charged to authors who had copied correctly.
        if (!DateOnly.TryParseExact(lines[0].Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var authoredOn))
        {
            return Outcome<HarvestedFix>.Failure($"'{lines[0].Trim()}' is not a date this reader can keep as a day");
        }

        return CommitSha.Parse(fixSha).Match(
            fix => CommitSha.Parse(baseSha).Match(
                parent => Outcome<HarvestedFix>.Success(new HarvestedFix(
                    fix,
                    parent,
                    authoredOn,
                    lines[1].Trim(),
                    string.Join('\n', lines.Skip(2)).Trim(),
                    diffText)),
                Outcome<HarvestedFix>.Failure),
            Outcome<HarvestedFix>.Failure);
    }

    private static Task<Outcome<string>> Git(
        string workingDirectory, TimeSpan timeout, CancellationToken cancellationToken, params string[] arguments) =>
        GitCommand.RunAsync(workingDirectory, timeout, cancellationToken, arguments);
}
