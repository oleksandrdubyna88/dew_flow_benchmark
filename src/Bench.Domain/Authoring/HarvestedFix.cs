using Bench.Domain.Targets;

namespace Bench.Domain.Authoring;

/// <summary>One merged fix, read out of the repository for harvesting
/// (todo/PLAN_investigate_vs_implement.md §3.5): everything a code-task candidate needs that is the
/// REPOSITORY's to say, not an author's.
/// <para>
/// The base commit is the fix's parent — the tree where the bug is live and the solver investigates.
/// The date is the author date of the fix itself, kept as a calendar day (the
/// <c>QuestionSeed.Written</c> lesson: converting a day to an instant moved it across midnight once,
/// and three questions were rejected over our own arithmetic). Deriving all three is the same
/// principle as seed dates and diagnosis anchors: the author names the change, the repository dates
/// and locates it.
/// </para></summary>
public sealed record HarvestedFix(
    CommitSha Fix,
    CommitSha Base,
    DateOnly AuthoredOn,
    string Subject,
    string Body,
    string DiffText);
