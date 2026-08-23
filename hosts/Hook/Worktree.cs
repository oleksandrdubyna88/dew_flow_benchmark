using System.Diagnostics;
using Bench.Contracts;
using Bench.Domain;
using Bench.Domain.Sessions;

namespace Bench.Hook;

/// <summary>The ground truth: what <c>git status --porcelain</c> says, hashed.
/// <para>
/// This is the check that settles what a tool NAME cannot. <c>Bash</c> is <c>git status</c> and it is also
/// <c>rm -rf</c>, and an allowlist alone can only ever be a guess about which. Two readings around one
/// call — before and after — turn that guess into an observation, and where the two instruments disagree
/// the disagreement itself is stored rather than resolved in silence.
/// </para>
/// <para>
/// <b>Only the digest travels, never the output.</b> The porcelain listing is the operator's uncommitted
/// file names, and this database is published unedited.
/// </para>
/// <para>
/// <b>Known blind spots, recorded rather than papered over:</b> a write outside the repository and a write
/// to a path <c>.gitignore</c> excludes are both invisible here. That is exactly why the allowlist reading
/// is kept beside this one instead of being replaced by it.
/// </para></summary>
internal static class Worktree
{
    /// <summary>How long git gets before the reading is abandoned.
    /// <para>
    /// A budget rather than a wait, because this runs inside the operator's tool call. A tree big enough to
    /// exceed it yields <see cref="WorktreeDto.NotChecked"/> — which is a state the whole schema is built
    /// to carry honestly, and infinitely better than a tool call that hangs on instrumentation.
    /// </para></summary>
    private const int BudgetMs = 1500;

    /// <summary>Reads the tree, unless this call could not possibly have touched it.
    /// <para>
    /// The gate is what makes this affordable — reads and searches are the majority of every session and
    /// paying git for each would put a process launch in front of the cheapest calls an agent makes. But
    /// it is drawn by the TOOL and never by the command, and that distinction is the whole design.
    /// </para>
    /// <para>
    /// <b>A shell call is always checked, even one the allowlist calls read-only.</b> The first version
    /// skipped those, which quietly removed the only case the ground truth exists for: an allowlist entry
    /// that is WRONG can only be caught by checking the calls it claims are safe. Skipping them made the
    /// disagreement detector unable to fire for the exact commands it was written to catch — an instrument
    /// that could only confirm what it already believed.
    /// </para>
    /// <para>
    /// <c>Read</c>, <c>Grep</c> and <c>Glob</c> stay unchecked, and that is a different claim: they cannot
    /// write because of what they ARE, not because of what a table says about their arguments.
    /// </para></summary>
    public static WorktreeDto Read(string cwd, string toolName, string command)
    {
        if (cwd.Length == 0)
        {
            return WorktreeDto.NotChecked("the hook payload named no working directory");
        }

        var taxonomy = ToolTaxonomy.ClaudeCode;
        var name = ToolTaxonomy.Strip(toolName);

        if (!taxonomy.Shell.Contains(name) && taxonomy.Classify(toolName, command) is ToolClass.Read or ToolClass.Neutral)
        {
            return WorktreeDto.NotChecked($"{toolName} cannot change tracked files");
        }

        return Porcelain(cwd);
    }

    /// <summary>The current branch, read from <c>.git/HEAD</c> rather than from <c>git rev-parse</c>.
    /// <para>
    /// A file read against a process launch, in a path that runs before every tool call an agent makes. A
    /// detached head or a shape this does not recognise answers empty, which reads as "not recorded" rather
    /// than as a branch nobody can find.
    /// </para></summary>
    public static string Branch(string cwd)
    {
        try
        {
            var head = Path.Combine(GitFolder(cwd), "HEAD");

            if (!File.Exists(head))
            {
                return string.Empty;
            }

            var text = File.ReadAllText(head).Trim();

            return text.StartsWith("ref: refs/heads/", StringComparison.Ordinal) ? text[16..] : string.Empty;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    private static WorktreeDto Porcelain(string cwd)
    {
        try
        {
            using var git = Start(cwd);

            if (git is null)
            {
                return WorktreeDto.NotChecked("git could not be started");
            }

            var output = git.StandardOutput.ReadToEnd();

            if (!git.WaitForExit(BudgetMs))
            {
                Stop(git);
                return WorktreeDto.NotChecked($"git status exceeded its {BudgetMs} ms budget");
            }

            return git.ExitCode == 0
                ? Digest(output)
                : WorktreeDto.NotChecked($"git status exited {git.ExitCode} — not a repository?");
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            return WorktreeDto.NotChecked($"git status failed: {ex.Message.Split('\n')[0]}");
        }
    }

    private static WorktreeDto Digest(string porcelain)
    {
        var lines = porcelain.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // The digest covers the CONTENT of the listing, not its length: two changes that cancel out in
        // count are still two changes, and a counter alone would call that "nothing happened".
        return new WorktreeDto(true, StableHash.Of(porcelain), lines.Length, string.Empty);
    }

    private static Process? Start(string cwd) =>
        Process.Start(new ProcessStartInfo("git")
        {
            // exe + argv, never a shell string — the family's rule, and it matters more here than most
            // places because the working directory is whatever an agent happened to be pointed at.
            ArgumentList = { "status", "--porcelain" },
            WorkingDirectory = cwd,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        });

    private static void Stop(Process git)
    {
        try
        {
            git.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // It finished between the timeout and the kill. Nothing to do, and certainly nothing worth
            // failing a tool call over.
        }
    }

    /// <summary>Where this checkout's git metadata lives — a folder normally, a FILE in a worktree, whose
    /// content names the real directory. Worktrees are how this family measures anything, so the second
    /// case is the common one rather than the exotic one.</summary>
    private static string GitFolder(string cwd)
    {
        var candidate = Path.Combine(cwd, ".git");

        if (!File.Exists(candidate))
        {
            return candidate;
        }

        var pointer = File.ReadAllText(candidate).Trim();

        return pointer.StartsWith("gitdir: ", StringComparison.Ordinal) ? pointer[8..].Trim() : candidate;
    }
}
