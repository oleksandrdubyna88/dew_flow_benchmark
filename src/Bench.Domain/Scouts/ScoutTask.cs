using System.Text.RegularExpressions;
using Bench.Domain.Gate;
using Bench.Domain.Targets;

namespace Bench.Domain.Scouts;

/// <summary>A task's id — a slug, because it is a file name in <c>research/scout-calibration/</c> and a column in a published table.</summary>
public sealed partial record ScoutTaskId
{
    private ScoutTaskId(string value) => Value = value;

    public string Value { get; }

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,63}$")]
    private static partial Regex Slug { get; }

    public static Outcome<ScoutTaskId> Parse(string? value)
    {
        var trimmed = (value ?? string.Empty).Trim();

        return Slug.IsMatch(trimmed)
            ? Outcome<ScoutTaskId>.Success(new ScoutTaskId(trimmed))
            : Outcome<ScoutTaskId>.Failure($"'{trimmed}' is not a task id — lower-case letters, digits and '-', at most 64, starting with a letter or digit");
    }

    public override string ToString() => Value;
}

/// <summary>The three kinds of question, roughly a third each (§3.1).</summary>
public enum ScoutQuestionKind
{
    WhereDecided,
    HowEndToEnd,
    WhatMustChange,
}

/// <summary>One task (§3.1): a past change in a PUBLIC family repository, investigated at its parent commit P. Frozen before any run:
/// the statement, the ground truth G, the files removed from the tree (the PR's own plan when it existed at P), and the tree's hash.
/// <para>
/// <see cref="Frozen"/> is the only door, and it runs the leak check — a statement naming the answer is refused, never stored.
/// </para></summary>
public sealed partial record ScoutTask(
    ScoutTaskId Id,
    string Repository,
    CommitSha Parent,
    CommitSha Fix,
    ScoutQuestionKind Kind,
    string Statement,
    IReadOnlyList<string> GroundTruth,
    IReadOnlyList<string> Removals,
    string TreeHash)
{
    public const int MaxSentences = 3;

    /// <summary>Freezes a task. Refused: a repository that is not <c>owner/name</c>, an empty or over-long statement, an empty ground
    /// truth, a ground-truth or removed path that is not repository-relative, a tree hash that is not SHA-256 — and a statement that
    /// leaks (<see cref="LeakCheck"/>), naming every leaked token so the operator can rewrite it.</summary>
    public static Outcome<ScoutTask> Frozen(
        ScoutTaskId id,
        string repository,
        CommitSha parent,
        CommitSha fix,
        ScoutQuestionKind kind,
        string statement,
        IReadOnlyList<string> groundTruth,
        IReadOnlyList<string> addedIdentifiers,
        IReadOnlyList<string> removals,
        string treeHash)
    {
        var refusal = Shape(repository, statement, groundTruth, removals, treeHash);

        if (refusal.Length > 0)
        {
            return Outcome<ScoutTask>.Failure($"task {id}: {refusal}");
        }

        var leaks = LeakCheck.Find(statement, groundTruth, addedIdentifiers);

        return leaks.Count > 0
            ? Outcome<ScoutTask>.Failure($"task {id}: the statement leaks the answer — {string.Join(", ", leaks.Select(l => $"{l.Kind} '{l.Token}'"))}")
            : Outcome<ScoutTask>.Success(new ScoutTask(id, repository.Trim(), parent, fix, kind, statement.Trim(), [.. groundTruth], [.. removals], treeHash.Trim().ToLowerInvariant()));
    }

    /// <summary>Sentences, counted as the runs of text ended by <c>.</c>, <c>!</c> or <c>?</c> followed by a space or the end.</summary>
    public static int Sentences(string statement) =>
        SentenceEnd().Split(statement.Trim()).Count(part => part.Trim().Length > 0);

    [GeneratedRegex(@"(?<=[.!?])\s+")]
    private static partial Regex SentenceEnd();

    [GeneratedRegex("^[0-9a-fA-F]{64}$")]
    private static partial Regex Sha256();

    private static string Shape(string repository, string statement, IReadOnlyList<string> groundTruth, IReadOnlyList<string> removals, string treeHash) =>
        (ScoutRepository.Refusal(repository).Length == 0, statement.Trim().Length > 0, Sentences(statement) <= MaxSentences) switch
        {
            (false, _, _) => ScoutRepository.Refusal(repository),
            (_, false, _) => "the statement is empty",
            (_, _, false) => $"the statement has {Sentences(statement)} sentences — at most {MaxSentences}",
            _ => Paths(groundTruth, removals, treeHash),
        };

    private static string Paths(IReadOnlyList<string> groundTruth, IReadOnlyList<string> removals, string treeHash) =>
        (groundTruth.Count > 0, groundTruth.Concat(removals).Select(RepositoryRelative.Refusal).FirstOrDefault(r => r.Length > 0), Sha256().IsMatch(treeHash.Trim())) switch
        {
            (false, _, _) => "the ground truth is empty — a task without G has nothing to find",
            (_, { } outside, _) => outside,
            (_, _, false) => $"'{treeHash}' is not a tree hash — SHA-256, 64 hex characters",
            _ => string.Empty,
        };
}
