using Bench.Domain;
using Bench.Domain.Targets;
using Bench.Infrastructure.Process;

namespace Bench.Infrastructure.Git;

/// <summary>Running one git command in a directory and reading its output as a value.
/// <para>
/// Extracted from <see cref="GitCheckoutProvider"/>, which had the only copy: mirror, fetch and worktree all go
/// through it, and now so does history. One launcher, one refusal shape.
/// </para></summary>
public static class GitCommand
{
    public static async Task<Outcome<string>> RunAsync(
        string workingDirectory, TimeSpan timeout, CancellationToken cancellationToken, params string[] arguments)
    {
        var attempt = await ProcessRunner.RunAsync("git", arguments, workingDirectory, timeout, cancellationToken);

        return attempt switch
        {
            ProcessAttempt.Completed c when c.Result.Ok => Outcome<string>.Success(c.Result.Output),
            ProcessAttempt.Completed c => Outcome<string>.Failure($"git {arguments[0]} exited {c.Result.ExitCode}: {c.Result.Output}"),
            _ => Outcome<string>.Failure($"git {arguments[0]} {attempt.Describe}"),
        };
    }
}

/// <summary>The target's change history, rendered for an author's brief.
/// <para>
/// <b>Why this exists at all.</b> A question's seed date is the input to the whole memorisation check, and no
/// author could establish one: an agent launched inside a <c>git worktree</c> cannot read the history, because
/// the worktree's <c>.git</c> is a redirect FILE its CLI declines to follow. Claude reported the gap and left
/// every seed <c>unstated</c> — which reads as <i>may recall</i>; Gemini lost a whole batch call to trying,
/// exiting 1 while narrating <i>"I will run a git log command to find the modification history/date"</i>.
/// </para>
/// <para>
/// The fix is that <b>we</b> can read what the agent will not: our own <c>git</c> follows the redirect without
/// complaint. So the history is gathered here and put INTO the brief, which also makes it better evidence than a
/// tool call would be — the dates arrive as data the author cannot invent, and the same brief is what the prompt
/// hash covers.
/// </para></summary>
public static class GitHistory
{
    /// <summary>How much history a brief can carry. A prompt is not a place for a repository's whole log, and an
    /// author asked to pick ten questions does not need five hundred merges to choose from.</summary>
    public const int MaxBytes = 16 * 1024;

    /// <summary>The changes reachable from <paramref name="commit"/> along first-parent, each with its date and
    /// the files it touched.
    /// <para>
    /// First-parent deliberately: it is the branch's own timeline, so a change's date is when it landed HERE
    /// rather than when somebody started a branch. That is the date a memorisation check needs.
    /// </para>
    /// <para>
    /// <b>Commits, not merges — corrected on measurement.</b> This was written as <c>--merges</c>, because the
    /// group it serves is called <c>pr-diff</c> and its brief said the seed is a pull request's merge date. Run
    /// against the actual target it returned NOTHING: <c>dew_flow_rag_qln</c> is trunk-based with a linear
    /// history and has no merge commits at all, so the group was specified around a workflow this repository does
    /// not use. A commit's date is just as real a date, and its conventional-commit subject describes the change
    /// at least as well as a merge subject would. Dropping the filter also keeps merges when a target HAS them,
    /// since first-parent includes them — so this is the general form rather than a second one.
    /// </para></summary>
    public static async Task<Outcome<string>> ChangesAsync(
        string worktree, CommitSha commit, int count, TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (count < 1)
        {
            return Outcome<string>.Failure($"a history of {count} changes would tell an author nothing");
        }

        var log = await GitCommand.RunAsync(
            worktree,
            timeout,
            cancellationToken,
            "log",
            "--first-parent",
            "--diff-merges=first-parent",
            "--name-only",
            $"-n{count}",
            "--date=short",
            "--pretty=format:%n%h %ad %s",
            commit.Value);

        return log.Match(
            text => Outcome<string>.Success(Bounded(text.Trim())),
            reason => Outcome<string>.Failure(reason));
    }

    /// <summary>Truncated at a line boundary, and SAYING it was. A brief that silently ends mid-record invites an
    /// author to cite a merge whose file list it never saw.</summary>
    private static string Bounded(string text)
    {
        if (System.Text.Encoding.UTF8.GetByteCount(text) <= MaxBytes)
        {
            return text;
        }

        var lines = text.Split('\n');
        var kept = new List<string>();
        var bytes = 0;

        foreach (var line in lines)
        {
            bytes += System.Text.Encoding.UTF8.GetByteCount(line) + 1;

            if (bytes > MaxBytes)
            {
                break;
            }

            kept.Add(line);
        }

        return string.Join('\n', kept)
            + $"\n\n[history truncated at {MaxBytes / 1024} KB — {lines.Length - kept.Count} more record(s) exist]";
    }
}
