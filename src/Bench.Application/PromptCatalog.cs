using Bench.Domain;
using Bench.Domain.Targets;

namespace Bench.Application;

/// <summary>A prompt as it was SENT, and the identity of the template it came from.</summary>
/// <param name="Hash">Of the TEMPLATE — shared part and group brief together — never of the rendering. A
/// rendering differs per call (a different repository, a different count) while the prompt that wrote a
/// hundred questions is one thing, and "these questions came from that prompt" has to be a fact rather than a
/// recollection.
/// <para>
/// Why it is stored at all: rewriting one ordering instruction moved a measured score <b>16.5 points of
/// 63</b> where swapping 4 tools for 18 moved 1 (`todo/PLAN_tool_benchmark.md`). A prompt living in a C#
/// string literal is an unversioned axis with the largest measured effect in the system.
/// </para></param>
public sealed record RenderedPrompt(string Text, string Hash, string Source);

/// <summary>What an author is briefed with.</summary>
/// <param name="Count">How many questions this call asks for. Part of the brief rather than a loop around it,
/// because an author writing five questions at once avoids repeating itself in a way five separate calls
/// cannot — and repetition is what deduplication then has to throw away.</param>
/// <param name="History">The target's merge history, or empty. Filled for every group and USED by the brief that
/// needs it: `pr-diff` questions are seeded from merged pull requests and their dates, and an agent launched
/// inside a worktree cannot read that history for itself — the first batch returned every seed `unstated`.</param>
public sealed record AuthoringBrief(
    string Group, string GroupTitle, RepoUrl Target, CommitSha Commit, int Count, string History = "");

/// <summary>What a reviewer is briefed with: the same target, and the one question it is judging.</summary>
public sealed record ReviewBrief(
    string Group, string GroupTitle, RepoUrl Target, CommitSha Commit, string QuestionJson);

/// <summary>The prompt catalog on disk: one shared contract per role, one brief per group.
/// <para>
/// <b>Shared plus specific, rather than one file per group carrying a copy of the contract.</b> The JSON
/// shape, the seed rule and the mandatory properties are the same for every group, and five copies of them
/// are five things to keep true — the duplication this repository refuses everywhere else. The hash covers
/// BOTH parts, so changing the shared contract changes every group's identity, which is correct: the prompt
/// did change.
/// </para>
/// <para>
/// Read from disk at call time and never cached. A catalog cached for the life of a process is a catalog an
/// operator edits and then cannot explain why nothing changed.
/// </para></summary>
public static class PromptCatalog
{
    /// <summary>The groups this catalog can brief. <c>code-writing</c> is absent deliberately: its authoring
    /// needs three extra gates — the bug reproduces, the reference fix works, the tree is rebuilt to the buggy
    /// state — and those need a sandbox worktree and a build (`todo/PLAN_code_lane.md`). Refused by name here
    /// rather than authored badly.</summary>
    public static IReadOnlyList<string> Groups =>
        ["code-lookup", "semantic-intent", "pr-diff", "bug-root-cause", "adversarial"];

    public const string AuthorRole = "author";

    public const string ReviewRole = "review";

    public static Outcome<RenderedPrompt> Author(string root, AuthoringBrief brief) =>
        brief.Count < 1
            ? Outcome<RenderedPrompt>.Failure($"an authoring brief must ask for at least one question, got {brief.Count}")
            : Render(root, AuthorRole, brief.Group, new Dictionary<string, string>
            {
                ["target"] = brief.Target.Value,
                ["commit"] = brief.Commit.Value,
                ["group"] = brief.Group,
                ["groupTitle"] = brief.GroupTitle,
                ["count"] = brief.Count.ToString(),
                ["history"] = brief.History.Length > 0
                    ? brief.History
                    : "(no history was gathered for this call — do not invent dates)",
            });

    public static Outcome<RenderedPrompt> Review(string root, ReviewBrief brief) =>
        brief.QuestionJson.Trim().Length == 0
            ? Outcome<RenderedPrompt>.Failure("a review brief with no question in it would ask for a verdict about nothing")
            : Render(root, ReviewRole, brief.Group, new Dictionary<string, string>
            {
                ["target"] = brief.Target.Value,
                ["commit"] = brief.Commit.Value,
                ["group"] = brief.Group,
                ["groupTitle"] = brief.GroupTitle,
                ["question"] = brief.QuestionJson,
            });

    private static Outcome<RenderedPrompt> Render(
        string root, string role, string group, IReadOnlyDictionary<string, string> values)
    {
        if (!Groups.Contains(group, StringComparer.OrdinalIgnoreCase))
        {
            return Outcome<RenderedPrompt>.Failure(
                $"'{group}' is not a group this catalog briefs — it knows {string.Join(", ", Groups)}. "
                + "code-writing is deliberately absent: its authoring needs a sandbox worktree and a build");
        }

        var shared = Path.Combine(root, role, "_shared.md");
        var brief = Path.Combine(root, role, $"{group}.md");

        if (!File.Exists(shared) || !File.Exists(brief))
        {
            return Outcome<RenderedPrompt>.Failure(
                $"the {role} prompt for '{group}' is incomplete — expected both '{shared}' and '{brief}'");
        }

        var template = $"{File.ReadAllText(shared).TrimEnd()}\n\n{File.ReadAllText(brief).TrimEnd()}\n";

        return Fill(template, values).Match(
            text => Outcome<RenderedPrompt>.Success(
                new RenderedPrompt(text, StableHash.Of(template), $"{role}/{group}")),
            Outcome<RenderedPrompt>.Failure);
    }

    /// <summary>Substitutes every placeholder, and REFUSES a template with one left over.
    /// <para>
    /// The refusal is the point. A prompt sent with a literal <c>{{commit}}</c> in it does not fail — the agent
    /// answers anyway, plausibly, about no particular commit, and the questions it writes look exactly like
    /// the ones it would have written correctly. That is a whole batch of unattributable candidates produced
    /// by a defect nothing surfaces.
    /// </para></summary>
    private static Outcome<string> Fill(string template, IReadOnlyDictionary<string, string> values)
    {
        var text = values.Aggregate(
            template, (current, value) => current.Replace($"{{{{{value.Key}}}}}", value.Value, StringComparison.Ordinal));

        var unfilled = Unfilled(text);

        return unfilled.Count == 0
            ? Outcome<string>.Success(text)
            : Outcome<string>.Failure(
                $"the prompt still carries {string.Join(", ", unfilled)} — an agent answers a template as "
                + "readily as a brief, and its questions would look exactly like correct ones");
    }

    private static IReadOnlyList<string> Unfilled(string text) =>
        [.. System.Text.RegularExpressions.Regex.Matches(text, @"\{\{[a-zA-Z]+\}\}")
            .Select(m => m.Value)
            .Distinct(StringComparer.Ordinal)];
}
