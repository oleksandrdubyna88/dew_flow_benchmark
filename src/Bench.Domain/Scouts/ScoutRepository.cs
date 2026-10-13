using System.Text.RegularExpressions;
using Bench.Domain.Targets;

namespace Bench.Domain.Scouts;

/// <summary>What a task's repository may be (plan §3.1, D10): a PUBLIC repository's <c>owner/name</c> slug, which the trees' url template turns
/// into the url its mirror is cloned from, or a repository url used AS IT IS — https://, ssh://, git@ or file://. The last is how a private
/// repository is read: from the operator's local clone, read only (<c>file:////wsl.localhost/Ubuntu/home/&lt;user&gt;/git/&lt;repo&gt;</c>, verified
/// 2026-10-12: Windows git clones from it and <c>git ls-remote</c> answers), because its remote is not reachable from this machine. One rule,
/// read by the task's freeze and by the trees' url resolution, so the two cannot disagree about what a repository is.</summary>
public static partial class ScoutRepository
{
    public const string Forms = "an owner/name slug, or a repository url: https://, ssh://, git@ or file://";

    [GeneratedRegex("^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$")]
    private static partial Regex Slug();

    public static bool IsSlug(string repository) => Slug().IsMatch(repository.Trim());

    public static bool IsUrl(string repository) => RepoUrl.Parse(repository) is Outcome<RepoUrl>.Ok;

    /// <summary>Why <paramref name="repository"/> is not a repository, naming it and the legal forms; empty when it is one.</summary>
    public static string Refusal(string repository) =>
        IsSlug(repository) || IsUrl(repository) ? string.Empty : $"'{repository}' is not a repository — {Forms}";
}
