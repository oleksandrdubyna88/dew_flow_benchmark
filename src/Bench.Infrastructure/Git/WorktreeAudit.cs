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
    /// <summary>What the leg changed, as git's own porcelain listing — empty means clean. ONLY the one
    /// file <see cref="DenyWritesAsync"/> planted is excluded, never the `.claude` folder: the target
    /// repositories this harness drives COMMIT a substantial `.claude/` tree (rules, settings), and a
    /// whole-folder exclusion made the audit blind to a subject editing exactly the files that govern
    /// what a session may do — found by review, in the one directory this harness also writes into.</summary>
    public static async Task<Outcome<string>> ChangesAsync(
        string worktree, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var status = await GitCommand.RunAsync(
            worktree, timeout, cancellationToken,
            "status", "--porcelain", "--", ".", $":(exclude){PlantedFile}");

        return status.Match(
            listing => Outcome<string>.Success(listing.Trim()),
            reason => Outcome<string>.Failure($"the worktree could not be audited: {reason}"));
    }

    /// <summary>The one path the audit may not flag, because we wrote it. `settings.local.json`, not
    /// `settings.json`: the target repositories commit their own `settings.json` (often with
    /// `permissions.allow` for the write tools), and overwriting a TRACKED file would both destroy the
    /// repo's own configuration in the diff the subject sees and show up as a modification the audit
    /// must then excuse. The local file merges over it, deny wins over allow, and it is conventionally
    /// untracked.</summary>
    public const string PlantedFile = ".claude/settings.local.json";

    /// <summary>Plants the deny-writes settings for whatever session runs in this tree. Written whole —
    /// the worktree is the leg's own and disposable (the `WorkspaceTrust` surgery exists for the
    /// OPERATOR's config; this file is ours).</summary>
    public static async Task DenyWritesAsync(string worktree, CancellationToken cancellationToken)
    {
        var target = Path.Combine(worktree, PlantedFile.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);

        var settings = JsonSerializer.Serialize(
            new { permissions = new { deny = new[] { "Write", "Edit", "NotebookEdit" } } },
            new JsonSerializerOptions { WriteIndented = true });

        await File.WriteAllTextAsync(target, settings, cancellationToken);
    }
}
