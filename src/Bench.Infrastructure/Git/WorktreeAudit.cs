using System.Text.Json;
using Bench.Domain;

namespace Bench.Infrastructure.Git;

/// <summary>The two halves of not trusting a CLI subject with a tree
/// (todo/PLAN_investigate_vs_implement.md §3.6): deny the write tools BEFORE the leg, and READ the
/// tree after. The settings are advisory hardening — an agent has more ways to write than its file
/// tools — so the audit is the evidence, and it is evidence rather than assumption: a leg that wrote
/// is MARKED, never silently trusted to have read.</summary>
public static class WorktreeAudit
{
    /// <summary>What the leg changed, as git's own porcelain listing — empty means clean. The
    /// `.claude` folder is excluded because <see cref="DenyWritesAsync"/> planted it: flagging our own
    /// hardening as the agent's write would make every audited leg read dirty.</summary>
    public static async Task<Outcome<string>> ChangesAsync(
        string worktree, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var status = await GitCommand.RunAsync(
            worktree, timeout, cancellationToken,
            "status", "--porcelain", "--", ".", ":(exclude).claude");

        return status.Match(
            listing => Outcome<string>.Success(listing.Trim()),
            reason => Outcome<string>.Failure($"the worktree could not be audited: {reason}"));
    }

    /// <summary>Plants `.claude/settings.json` denying the file-writing tools for whatever session runs
    /// in this tree. Written whole — the worktree is the leg's own and disposable, so there are no
    /// sibling settings to preserve (the `WorkspaceTrust` surgery exists for the OPERATOR's config;
    /// this file is ours).</summary>
    public static async Task DenyWritesAsync(string worktree, CancellationToken cancellationToken)
    {
        var folder = Path.Combine(worktree, ".claude");
        Directory.CreateDirectory(folder);

        var settings = JsonSerializer.Serialize(
            new { permissions = new { deny = new[] { "Write", "Edit", "NotebookEdit" } } },
            new JsonSerializerOptions { WriteIndented = true });

        await File.WriteAllTextAsync(Path.Combine(folder, "settings.json"), settings, cancellationToken);
    }
}
